using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #486] Et skrivbart ledd i <see cref="RegjeringenRapport"/>. <see cref="Utfall"/>: <c>opprettet</c> | <c>finnes</c> |
/// <c>konflikt</c> (noe annet har alt navnet; det gjettes ikke at det er det samme, ingenting skrives for leddet) | <c>avvist</c> (et menneske
/// har avvist forslaget; det foreslås ikke på nytt) | <c>ikke_kjort</c> (en forutsetning mangler, se merknaden).</summary>
public sealed record RegjeringenLedd(string Hva, string Utfall, Guid? Id, string? Merknad);

/// <summary>[Ny, issue #486] Én forekomst av «regjeringen»/«regjeringens» og hva skrivingen gjorde med den. <see cref="Regel"/> er null for en
/// slutning. <see cref="Tagg"/>: <see cref="AktoridentitetTjeneste.TaggUtfall"/>. <see cref="Binding"/>: <c>ny</c> | <c>finnes</c> |
/// <c>avvist</c> | <c>ingen_tagg</c> (taggen kunne ikke settes; ingen binding) | <c>ingen_referent</c> (referenten finnes ikke; se hullene).
/// <see cref="FlertydigHvisGodkjent"/>: forekomsten ville gitt «flertydig» i oppslaget om alle regeltaggene ble godkjent og slutningene ikke,
/// fordi en annen forekomst i noden har en regeltagg (juristens vilkår 2).</summary>
public sealed record RegjeringenForekomst(
    Guid RettskildeId, string Rettskilde, string Eid, int Start, string Ord, string Utdrag, string Lesning, string? Regel, string? Grunnlag,
    IReadOnlyList<string> Signal, string Tagg, Guid? TaggId, string Binding, Guid? BindingId, bool FlertydigHvisGodkjent);

/// <summary>[Ny, issue #486] Fordelingen av forekomstene: per lesning og per regel, og hvor mange som ville vært «flertydig».</summary>
public sealed record RegjeringenFordeling(
    int Forekomster, int Noder, int Rettskilder, IReadOnlyDictionary<string, int> PerLesning, IReadOnlyDictionary<string, int> PerRegel,
    int FlertydigeForekomsterHvisGodkjent, int FlertydigeNoderHvisGodkjent, int NoderMedFlereForekomster, int Tagget, int TaggUtenEier,
    int TaggKolliderer);

/// <summary>[Ny, issue #486] Svaret på <c>POST /api/regjeringen[?skriv=true]</c>. Tallene er de skrivingen gir, også i tørrkjøringen (samme
/// arbeid i en transaksjon som rulles tilbake).</summary>
public sealed record RegjeringenRapport(
    bool Skrevet, RegjeringenLedd Statsradet, RegjeringenLedd StatsradetNavneform, RegjeringenLedd StatsradetAktortype,
    RegjeringenLedd StatsradetHarOrgan, RegjeringenLedd KongenIStatsrad, RegjeringenLedd Navneform, RegjeringenFordeling Fordeling,
    IReadOnlyList<RegjeringenForekomst> Forekomster, IReadOnlyList<string> Hull);

/// <summary>
/// [Ny, issue #486, Johanns avgjørelse 2026-11-20 etter juristrunden om «regjeringen» (<c>data/fasit/strukturmodell/regjeringen/</c>)] Skriver
/// <b>organet statsrådet</b> og <b>en tagg per forekomst</b> av «regjeringen»/«regjeringens» etter ordlydsreglene (<see cref="RegjeringenOrdlydsregler"/>).
/// Alt nytt er <b>forslag</b>. Tørrkjøring er standard og gjør det samme i en transaksjon som rulles tilbake. Operasjonen er idempotent:
/// statsrådet har fast id (<see cref="StatsradetId"/>), en tagg og en forekomstbinding som finnes, skrives ikke på nytt, og et avvist forslag
/// foreslås ikke på nytt. Samme mønster som <see cref="NorgeStatenTjeneste"/> (#463). Hovedøkta kjører den etter fletting.
/// <list type="bullet">
/// <item><b>Statsrådet</b>: aktør (organ) uten organisasjonsnummer ved siden av Kongen i statsråd, som finnes. Den gjeldende navneformen
/// «statsrådet» har hjemmel i Grl. § 15 første ledd («Enhver som er medlem av statsrådet …», tekstfunn); aktørtypen organ har hjemmel i Grl. § 12
/// første ledd («Kongen velger selv et råd av stemmeberettigede norske borgere», fortolkning: rådet er et kollegium); <c>G har_organ</c>
/// statsrådet → staten (fortolkning, samme hjemmel), som departementene i #463. «regjeringen» blir IKKE navneform for statsrådet eller Kongen i
/// statsråd.</item>
/// <item><b>«regjeringen»</b> blir en generisk navneform (grunn <c>generisk</c>, ingen aktør, som «departementet»), slik at hver forekomst kan
/// tagges. Den er ikke en navneform FOR noen aktør; betydningen står per forekomst.</item>
/// <item><b>Hver forekomst</b> tagges (eier: rettskildens ansvarlige departement, som i <see cref="AktoridentitetTjeneste"/>) og får en
/// forekomstbinding (<see cref="NavneformBindingEntitet.TekstTaggId"/>) med lesningen fra ordlydsreglene, eller <see cref="Forekomstlesninger.Slutning"/>
/// når ingen regel slo til. Alle er <c>maskinelt_foreslatt</c> og brukes ikke i oppslaget før et menneske har vurdert dem.</item>
/// </list>
/// Før noe skrives, kontrolleres ordlyden i Grl. § 12 første ledd, § 15 første ledd og § 32 første ledd. Ellers skrives ingenting.
/// </summary>
public sealed class RegjeringenTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste, AktoridentitetTjeneste identitet)
{
    /// <summary>Fast id for statsrådet, så skrivingen er idempotent uten å slå opp på navn (CLAUDE.md §4: stabil nøkkel).</summary>
    public static readonly Guid StatsradetId = Guid.Parse("53746174-7372-4864-9001-000000000486");

    public const string Term = "regjeringen";
    public const string Statsradet = "statsrådet";
    public const string KongenIStatsrad = "Kongen i statsråd";

    public const string Grl12Eid = "https://lovdata.no/eli/lov/1814/05/17/nor/§12/ledd-1";
    public const string Grl12Sitat = "Kongen velger selv et råd av stemmeberettigede norske borgere.";
    public const string Grl15Eid = "https://lovdata.no/eli/lov/1814/05/17/nor/§15/ledd-1";
    public const string Grl15Sitat = "Enhver som er medlem av statsrådet";
    public const string Grl32Eid = "https://lovdata.no/eli/lov/1814/05/17/nor/§32/ledd-1";
    public const string Grl32Sitat = "De beslutninger som fattes av regjeringen under kongens fravær, utferdiges i kongens navn";

    public const string Oppdagelseskilde = "manuell";
    public const string HarOrganOppdagelseskilde = "manuell";
    public static string RegelOppdagelseskilde(string lesning) => $"monster:regjeringen-{lesning.Replace('_', '-')}";

    public const string NavneformBegrunnelse =
        "Generisk navneform for «regjeringen» (issue #486, Johann 2026-11-20). Ordet er ikke navneform for statsrådet eller Kongen i statsråd: "
        + "Grunnloven bruker det om styringen av riket (§§ 9, 13, 40, 41, 43, 44), om statsrådskollegiet (§§ 76, 82) og om medlemmene (§ 72), og "
        + "Dok. 19 (2011–2012) pkt. 3.4.4 sier at det er flertydig. Betydningen står per forekomst (tagg per forekomst), etter ordlydsreglene eller "
        + "som slutning et menneske avgjør.";

    public const string StatsradetNavneformBegrunnelse =
        "«statsrådet» står ordrett i Grunnloven § 15 første ledd («" + Grl15Sitat + " …») og i § 12 annet og tredje ledd. Organet er rådet "
        + "kongen velger etter § 12 første ledd. Statsrådet er et eget organ ved siden av Kongen i statsråd (beslutningsformen, §§ 28 og 30). "
        + "Juristrunden om «regjeringen» (#463), Johann 2026-11-20 (#486).";

    public const string StatsradetAktortypeBegrunnelse =
        "Grunnloven § 12 første ledd: «" + Grl12Sitat + " Dette råd skal bestå av en statsminister og i det minste syv andre medlemmer.» "
        + "Ordlyden sier et råd med medlemmer; at rådet er et organ (et kollegium som treffer avgjørelser, § 13 annet ledd), følger av sammenhengen. #486.";

    public const string HarOrganBegrunnelse =
        "Statsrådet er rådet kongen velger etter Grunnloven § 12 første ledd, og et organ for staten på samme måte som departementene (#463, modell B). "
        + "At organet hører til staten, følger av sammenhengen (Grunnloven § 12 og § 13, statens øverste utøvende organ), ikke av ordlyden alene. #486.";

    public async Task<RegjeringenRapport> KjorAsync(bool skriv, string bruker, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var rapport = await GjorAsync(bruker, ct) with { Skrevet = skriv };
            if (skriv && tx is not null) await tx.CommitAsync(ct);
            else if (tx is not null) await tx.RollbackAsync(ct);
            if (!skriv) db.ChangeTracker.Clear();
            return rapport;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private sealed record Hjemmel(Guid RettskildeId, Guid NodeId);

    private async Task<RegjeringenRapport> GjorAsync(string bruker, CancellationToken ct)
    {
        var grl12 = await KontrollerAsync(Grl12Eid, Grl12Sitat, ct);
        var grl15 = await KontrollerAsync(Grl15Eid, Grl15Sitat, ct);
        var grl32 = await KontrollerAsync(Grl32Eid, Grl32Sitat, ct);
        var hull = new List<string>();

        var statsradet = await StatsradetAsync(bruker, ct);
        RegjeringenLedd navneform, aktortype, harOrgan;
        var statsradetFinnes = statsradet.Utfall is "opprettet" or "finnes";
        if (statsradetFinnes)
        {
            navneform = await StatsradetNavneformAsync(grl15, bruker, ct);
            aktortype = await StatsradetAktortypeAsync(grl12, bruker, ct);
            harOrgan = await HarOrganAsync(grl12, bruker, ct);
        }
        else
        {
            navneform = new("navneform «statsrådet»", "ikke_kjort", null, "Statsrådet ble ikke skrevet.");
            aktortype = new("aktørtype organ", "ikke_kjort", null, "Statsrådet ble ikke skrevet.");
            harOrgan = new("G har_organ statsrådet → staten", "ikke_kjort", null, "Statsrådet ble ikke skrevet.");
        }

        var kongen = await KongenIStatsradAsync(ct);
        var generisk = await GeneriskNavneformAsync(bruker, ct);

        var forekomster = await ForekomsterAsync(ct);
        var rader = new List<RegjeringenForekomst>();
        var generiske = new GeneriskNavneformTjeneste(db);
        foreach (var f in forekomster)
        {
            var treff = RegjeringenOrdlydsregler.Vurder(f.Tekst, f.Start, f.Slutt, [f.Tittel, f.Kortnavn]);
            var lesning = treff?.Lesning ?? Forekomstlesninger.Slutning;
            var signal = treff is null ? RegjeringenOrdlydsregler.Signal(f.Tekst, f.Start, f.Slutt) : [];
            var utdrag = f.Tekst[Math.Max(0, f.Start - 60)..Math.Min(f.Tekst.Length, f.Slutt + 60)];
            string taggUtfall = "ikke_kjort", bindingUtfall = "ingen_tagg";
            Guid? taggId = null, bindingId = null;
            if (generisk.Id is { } nfId && generisk.Utfall is "opprettet" or "finnes")
            {
                (taggUtfall, taggId) = await identitet.TaggForekomstAsync(f.RettskildeId, f.Eid, f.Tekst, f.Start, f.Slutt, nfId, bruker, ct);
                if (taggId is { } tid)
                {
                    (bindingUtfall, bindingId) = await ForekomstbindingAsync(generiske, nfId, f, tid, treff, lesning, signal,
                        statsradetFinnes ? StatsradetId : null, kongen.Id, grl15, grl32, bruker, ct);
                }
            }
            rader.Add(new RegjeringenForekomst(f.RettskildeId, f.Kortnavn ?? f.Tittel, f.Eid, f.Start, f.Tekst[f.Start..f.Slutt], utdrag, lesning,
                treff?.Regel, treff?.Grunnlag, signal, taggUtfall, taggId, bindingUtfall, bindingId, false));
        }

        // Juristens vilkår 2: en forekomst uten regeltagg i en node der en annen forekomst har en, gir «flertydig» når regeltaggene er vurdert.
        var perNode = rader.GroupBy(r => (r.RettskildeId, r.Eid)).ToList();
        var flertydigeNoder = 0;
        var merket = new HashSet<(Guid, string, int)>();
        foreach (var g in perNode)
        {
            var medRegel = g.Count(r => r.Regel is not null);
            if (medRegel > 0 && medRegel < g.Count())
            {
                flertydigeNoder++;
                foreach (var r in g.Where(r => r.Regel is null)) merket.Add((r.RettskildeId, r.Eid, r.Start));
            }
            else if (medRegel == g.Count() && g.Select(r => r.Lesning).Distinct().Count() > 1) flertydigeNoder++;
        }
        rader = rader.Select(r => merket.Contains((r.RettskildeId, r.Eid, r.Start)) ? r with { FlertydigHvisGodkjent = true } : r).ToList();

        if (kongen.Id is null) hull.Add($"Kongen i statsråd: {kongen.Merknad} «Regjeringens res.» får ingen forekomstbinding.");
        if (!statsradetFinnes) hull.Add("Statsrådet ble ikke skrevet; rollen statsråd får ingen forekomstbinding.");
        foreach (var r in rader.Where(r => r.Tagg == AktoridentitetTjeneste.TaggUtfall.UtenEier).Select(r => r.Rettskilde).Distinct())
        {
            hull.Add($"{r}: ingen ansvarlig departement i katalogen kan eie taggen. Forekomstene er ikke tagget.");
        }

        var fordeling = new RegjeringenFordeling(
            rader.Count, perNode.Count, rader.Select(r => r.RettskildeId).Distinct().Count(),
            Forekomstlesninger.Alle.ToDictionary(l => l, l => rader.Count(r => r.Lesning == l)),
            RegjeringenOrdlydsregler.Regler.ToDictionary(k => k, k => rader.Count(r => r.Regel == k)),
            merket.Count, flertydigeNoder, perNode.Count(g => g.Count() > 1),
            rader.Count(r => r.TaggId is not null), rader.Count(r => r.Tagg == AktoridentitetTjeneste.TaggUtfall.UtenEier),
            rader.Count(r => r.Tagg == AktoridentitetTjeneste.TaggUtfall.Kolliderer));
        return new RegjeringenRapport(false, statsradet, navneform, aktortype, harOrgan, kongen, generisk, fordeling, rader, hull);
    }

    /// <summary>Bestemmelsen skal stå nøyaktig én gang i en gjeldende rettskilde med ordlyden avgjørelsen bygger på. Ellers skrives ingenting
    /// (CLAUDE.md §8, samme kontroll som #442 og #463).</summary>
    private async Task<Hjemmel> KontrollerAsync(string eid, string maInneholde, CancellationToken ct)
    {
        var n = await db.RettskildeNoder.Where(x => x.Eid == eid && x.Entitetsstatus == "gjeldende")
            .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende" && r.Status == "Gjeldende"), x => x.RettskildeId, r => r.Id,
                (x, r) => new { RettskildeId = r.Id, NodeId = x.Id, x.Tekst }).ToListAsync(ct);
        if (n.Count != 1 || n[0].Tekst is null || !n[0].Tekst!.Contains(maInneholde, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Fant ikke nøyaktig én gjeldende node {eid} med «{maInneholde}» ({n.Count} treff). Ingenting skrives.");
        }
        return new Hjemmel(n[0].RettskildeId, n[0].NodeId);
    }

    private async Task<RegjeringenLedd> StatsradetAsync(string bruker, CancellationToken ct)
    {
        const string hva = "aktøren statsrådet (organ)";
        if (await db.Virksomheter.AnyAsync(v => v.Id == StatsradetId, ct)) return new(hva, "finnes", StatsradetId, null);
        var annen = await db.Virksomheter.Where(v => v.Navn.ToLower() == Statsradet).Select(v => (Guid?)v.Id).FirstOrDefaultAsync(ct)
                    ?? await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.Term.ToLower() == Statsradet)
                        .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        if (annen is not null)
        {
            return new(hva, "konflikt", annen, "Det finnes alt en aktør eller navneform «statsrådet». Det gjettes ikke at det er samme aktør; et menneske avgjør.");
        }
        var v = new Virksomhet { Id = StatsradetId, Navn = Statsradet, Aktiv = true, OpprettetTidspunkt = DateTimeOffset.UtcNow };
        db.Virksomheter.Add(v);
        db.Proveniens.Add(Proveniens("virksomhet", v.Id, "opprettet", bruker, new
        {
            grunn = "Organet statsrådet (Grl. § 12 første ledd og § 15), uten organisasjonsnummer: navnet kommer fra lovteksten, og identiteten er den faste id-en.",
        }));
        await db.SaveChangesAsync(ct);
        return new(hva, "opprettet", v.Id, null);
    }

    private async Task<RegjeringenLedd> StatsradetNavneformAsync(Hjemmel grl15, string bruker, CancellationToken ct)
    {
        const string hva = "navneform «statsrådet» (gjeldende)";
        var avvist = await db.Begreper.AnyAsync(b => b.VirksomhetReferanseId == StatsradetId && b.Godkjenning == Strukturkanter.Avvist
                                                     && b.Term.ToLower() == Statsradet, ct);
        if (avvist) return new(hva, "avvist", null, "Et menneske har avvist navneformen; den foreslås ikke på nytt.");
        var r = await identitet.RegistrerNavnetSomGjeldendeAsync(StatsradetId, new Utsagnsgrunnlag
        {
            Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = grl15.RettskildeId, HjemmelEid = Grl15Eid,
            Kunnskapsstatus = Strukturkanter.Tekstfunn, Godkjenning = Strukturkanter.ManueltForeslatt, Begrunnelse = StatsradetNavneformBegrunnelse,
        }, bruker, ct);
        if (r.Navneform is { } nf && r.Utfall != NavnetSomGjeldende.Fantes)
        {
            nf.OppdagelsesKilde = Oppdagelseskilde;
            await db.SaveChangesAsync(ct);
        }
        return new(hva, r.Utfall == NavnetSomGjeldende.Fantes ? "finnes" : "opprettet", r.Navneform?.Id, r.Merknad);
    }

    private async Task<RegjeringenLedd> StatsradetAktortypeAsync(Hjemmel grl12, string bruker, CancellationToken ct)
    {
        const string hva = "aktørtype organ (fortolkning)";
        var finnes = await db.AktortypeUtsagn.Where(a => a.VirksomhetId == StatsradetId && a.Aktortype == "organ")
            .OrderByDescending(a => a.OpprettetTidspunkt).FirstOrDefaultAsync(ct);
        if (finnes is not null) return new(hva, finnes.Status == Strukturkanter.Avvist ? "avvist" : "finnes", finnes.Id, null);
        var u = await identitet.RegistrerAktortypeAsync(StatsradetId, "organ", null, new Utsagnsgrunnlag
        {
            Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = grl12.RettskildeId, HjemmelEid = Grl12Eid,
            Kunnskapsstatus = Strukturkanter.Fortolkning, Tolkningsargumenter = ["ordlyd", "kontekst_system"],
            Godkjenning = Strukturkanter.ManueltForeslatt, Begrunnelse = StatsradetAktortypeBegrunnelse,
        }, Oppdagelseskilde, bruker, ct);
        return new(hva, "opprettet", u?.Id, null);
    }

    private async Task<RegjeringenLedd> HarOrganAsync(Hjemmel grl12, string bruker, CancellationToken ct)
    {
        const string hva = "G har_organ statsrådet → staten";
        if (!await db.Virksomheter.AnyAsync(v => v.Id == NorgeStatenTjeneste.StatenId, ct))
        {
            return new(hva, "ikke_kjort", null, "Aktøren staten finnes ikke. Kjør POST /api/norge-staten?skriv=true først (#463).");
        }
        var tidligere = await kanttjeneste.GjeldendeInklAvviste
            .Where(k => k.Kategori == Strukturkanter.Organtilhorighet && k.Typekode == "har_organ" && k.FraVirksomhetId == StatsradetId
                        && k.TilVirksomhetId == NorgeStatenTjeneste.StatenId)
            .OrderBy(k => k.Status == Strukturkanter.Avvist ? 1 : 0).FirstOrDefaultAsync(ct);
        if (tidligere is not null) return new(hva, tidligere.Status == Strukturkanter.Avvist ? "avvist" : "finnes", tidligere.Id, null);
        var o = await kanttjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Organtilhorighet, "har_organ",
            Kantnode.Virksomhet(StatsradetId), Kantnode.Virksomhet(NorgeStatenTjeneste.StatenId),
            HjemmelRettskildeId: grl12.RettskildeId, HjemmelEid: Grl12Eid,
            Status: Strukturkanter.ManueltForeslatt, OppdagelsesKilde: HarOrganOppdagelseskilde,
            Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"], Begrunnelse: HarOrganBegrunnelse,
            Kommentar: "Statsrådet er organ for staten (#486)."), bruker, ct);
        return new(hva, o.VarNy ? "opprettet" : "finnes", o.Kant.Id, null);
    }

    /// <summary>Kongen i statsråd finnes (#307, navneform uten orgnr). Nøyaktig én aktør med navneformen; ellers et hull.</summary>
    private async Task<RegjeringenLedd> KongenIStatsradAsync(CancellationToken ct)
    {
        const string hva = "aktøren Kongen i statsråd (finnes, skrives ikke)";
        var ider = await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                                               && b.Godkjenning != Strukturkanter.Avvist && b.Term == KongenIStatsrad)
            .Select(b => b.VirksomhetReferanseId!.Value).Distinct().ToListAsync(ct);
        return ider.Count == 1
            ? new(hva, "finnes", ider[0], null)
            : new(hva, "ikke_kjort", null, $"{ider.Count} aktører har navneformen «{KongenIStatsrad}».");
    }

    private async Task<RegjeringenLedd> GeneriskNavneformAsync(string bruker, CancellationToken ct)
    {
        const string hva = "generisk navneform «regjeringen» (ingen aktør)";
        var finnes = await new GeneriskNavneformTjeneste(db).FinnAsync(Term, ct);
        if (finnes is not null)
        {
            return new(hva, finnes.Godkjenning == Strukturkanter.Avvist ? "avvist" : "finnes", finnes.Id,
                finnes.Godkjenning == Strukturkanter.Avvist ? finnes.AvvisningBegrunnelse : null);
        }
        // Samme kontroll som GeneriskNavneformTjeneste.OpprettEllerFinnAsync: er termen navneform for en aktør, ville oppslaget blitt flertydig.
        var bundne = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                        && b.Godkjenning != Strukturkanter.Avvist && b.Term.ToLower() == Term)
            .Join(db.Virksomheter, b => b.VirksomhetReferanseId, v => v.Id, (b, v) => v.Navn).Take(3).ToListAsync(ct);
        if (bundne.Count > 0)
        {
            return new(hva, "konflikt", null, $"«{Term}» er alt en navneform for {string.Join(", ", bundne)}. Et menneske retter den først; ingenting skrives.");
        }
        var nf = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = null,
            Begrepskategori = "virksomhet",
            VirksomhetReferanseId = null,
            Navneformgrunn = VirksomhetsbegrepTjeneste.Generisk,
            Term = Term,
            Begrunnelse = NavneformBegrunnelse,
            Godkjenning = Strukturkanter.ManueltForeslatt,
            OppdagelsesKilde = Oppdagelseskilde,
            Status = "publisert",
            OpprettetAv = bruker,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(nf);
        db.Proveniens.Add(Proveniens("begrep", nf.Id, "foreslatt", bruker, new { generisk = Term }));
        await db.SaveChangesAsync(ct);
        return new(hva, "opprettet", nf.Id, null);
    }

    private sealed record Forekomst(Guid RettskildeId, string Tittel, string? Kortnavn, string Eid, string Tekst, int Start, int Slutt);

    /// <summary>Forekomstene: hele ord (<see cref="RegjeringenOrdlydsregler.Ordet"/>) i gjeldende, ikke opphevede noder i gjeldende rettskilder —
    /// samme utvalg som juristrundens telling (268 forekomster i 97 rettskilder 2026-11-18). Sortert på rettskilde, dokumentrekkefølge og posisjon.</summary>
    private async Task<List<Forekomst>> ForekomsterAsync(CancellationToken ct)
    {
        var noder = await db.RettskildeNoder
            .Where(n => n.Entitetsstatus == "gjeldende" && !n.Opphevet && n.Tekst != null && n.Tekst.ToLower().Contains(Term))
            .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende"), n => n.RettskildeId, r => r.Id,
                (n, r) => new { r.Id, r.Tittel, r.Kortnavn, n.Eid, n.Tekst, n.Sorteringsrekkefolge })
            .ToListAsync(ct);
        return noder.OrderBy(n => n.Kortnavn ?? n.Tittel, StringComparer.Ordinal).ThenBy(n => n.Id).ThenBy(n => n.Sorteringsrekkefolge)
            .SelectMany(n => RegjeringenOrdlydsregler.Ordet.Matches(n.Tekst!)
                .Select(m => new Forekomst(n.Id, n.Tittel, n.Kortnavn, n.Eid, n.Tekst!, m.Index, m.Index + m.Length)))
            .ToList();
    }

    private async Task<(string Utfall, Guid? BindingId)> ForekomstbindingAsync(
        GeneriskNavneformTjeneste generiske, Guid navneformId, Forekomst f, Guid taggId, RegjeringenTreff? treff, string lesning,
        IReadOnlyList<string> signal, Guid? statsradet, Guid? kongen, Hjemmel grl15, Hjemmel grl32, string bruker, CancellationToken ct)
    {
        // Avvist eller ikke: en forekomstbinding for samme tagg er samme forslag. Et avvist forslag foreslås ikke på nytt.
        var tidligere = await db.NavneformBindinger.Where(b => b.TekstTaggId == taggId && b.Entitetsstatus == "gjeldende")
            .OrderBy(b => b.Status == Strukturkanter.Avvist ? 1 : 0).FirstOrDefaultAsync(ct);
        if (tidligere is not null) return (tidligere.Status == Strukturkanter.Avvist ? "avvist" : "finnes", tidligere.Id);

        var ord = f.Tekst[f.Start..f.Slutt];
        var sitat = $"«{f.Tekst[Math.Max(0, f.Start - 60)..Math.Min(f.Tekst.Length, f.Slutt + 40)].Trim()}»";
        NyNavneformBinding ny;
        var ingen = new Kantnode(null, null);
        var kilde = treff is null ? RegelOppdagelseskilde(lesning) : RegelOppdagelseskilde(lesning);
        switch (lesning)
        {
            case Forekomstlesninger.FremmedStat when treff!.Regel == RegjeringenOrdlydsregler.FremmedSanksjonstittel:
                ny = new(navneformId, f.RettskildeId, ingen, HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid,
                    Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["kontekst_system"],
                    Begrunnelse: $"Fremmed stat: rettskilden er en sanksjonsforskrift ({treff.Grunnlag}), og ordet står om regjeringen i staten tiltakene "
                                 + $"gjelder. Ingen aktør i modellen. Regel 1 (#486). {sitat}");
                break;
            case Forekomstlesninger.FremmedStat:
                ny = new(navneformId, f.RettskildeId, ingen, HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid, Kunnskapsstatus: Strukturkanter.Tekstfunn,
                    Begrunnelse: $"Fremmed stat: ordlyden sier hvilken stat («{treff!.Grunnlag}»). Ingen aktør i modellen. Regel 1 (#486). {sitat}");
                break;
            case Forekomstlesninger.GeneriskKonvensjonsstat:
                ny = new(navneformId, f.RettskildeId, ingen, HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid,
                    Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"],
                    Begrunnelse: $"Generisk konvensjonsstat: en mal eller definisjon som gjelder alle statene, også Norge («{treff!.Grunnlag}»). Ingen kant. "
                                 + $"Regel 2 (#486). {sitat}");
                break;
            case Forekomstlesninger.IkkeAktor:
                ny = new(navneformId, f.RettskildeId, ingen, HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid, Kunnskapsstatus: Strukturkanter.Tekstfunn,
                    Begrunnelse: $"Ikke aktør: {IkkeAktorTekst(treff!.Regel)} («{treff.Grunnlag}»). Regel 3 (#486). {sitat}");
                break;
            case Forekomstlesninger.RolleStatsrad:
                if (statsradet is not { } sid) return ("ingen_referent", null);
                ny = new(navneformId, f.RettskildeId, Kantnode.Virksomhet(sid), HjemmelRettskildeId: grl15.RettskildeId, HjemmelEid: Grl15Eid,
                    Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"],
                    Begrunnelse: $"Rollen statsråd: «{treff!.Grunnlag}» er medlemmene av statsrådet, samme krets som «medlem av statsrådet» i Grunnloven § 15 "
                                 + $"første ledd og «medlemmer av regjeringen» i § 72 tredje ledd nr. 1, som § 72 holder fra statssekretærer og politiske rådgivere (nr. 3); "
                                 + $"jf. «Statsrådets medlemmer» i § 62 annet ledd og «medlem av statsrådet» i § 82 første ledd. Ordet står for kollegiet; "
                                 + $"uttrykket er rollen. Regel 4 (#486). {sitat}");
                break;
            case Forekomstlesninger.Funksjon:
                ny = new(navneformId, f.RettskildeId, ingen, HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid,
                    Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "forarbeider"],
                    Begrunnelse: $"Funksjonen, styret av riket («{treff!.Grunnlag}»), ikke en aktør. Grunnlovsspråkutvalget leser ordet slik i §§ 9, 13, 40, 41, "
                                 + $"43 og 44 (Dok. 19 (2011–2012) pkt. 3.4.4). Regel 5 (#486). {sitat}");
                break;
            case Forekomstlesninger.RegjeringensResolusjon:
                if (kongen is not { } kid) return ("ingen_referent", null);
                ny = new(navneformId, f.RettskildeId, Kantnode.Virksomhet(kid), HjemmelRettskildeId: grl32.RettskildeId, HjemmelEid: Grl32Eid,
                    Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"],
                    Begrunnelse: $"«Regjeringens res.» er vedtaksformen Kongen i statsråd: beslutninger regjeringen fatter under kongens fravær, «utferdiges i "
                                 + $"kongens navn» (Grunnloven § 32). FastsattAvTolker behandler formen likt med kgl.res. Regel 6 (#486). {sitat}");
                break;
            default:
                ny = new(navneformId, f.RettskildeId, ingen, Kunnskapsstatus: Strukturkanter.Uavklart,
                    Begrunnelse: $"[slutning] Ordlyden avgjør ikke hva «{ord}» er her. Det kan være statsrådet (kollegiet, Grl. § 12 første ledd og § 15), "
                                 + "Kongen i statsråd (beslutningsformen, §§ 28 og 30), funksjonen eller noe annet; et menneske avgjør per forekomst. Ingen "
                                 + $"referent er gjettet (Johann 2026-11-20). {sitat}");
                break;
        }
        ny = ny with
        {
            TekstTaggId = taggId, Lesning = lesning, Status = Strukturkanter.MaskineltForeslatt, OppdagelsesKilde = kilde,
            Kommentar = treff is not null
                ? $"Ordlydsregel {treff.Regel} (#486)."
                : signal.Count > 0 ? $"Slutning. Signalord (bare opplysning, ingen regel): {string.Join("; ", signal)}." : "Slutning (#486).",
        };
        var b = await generiske.OpprettBindingAsync(ny, bruker, ct);
        return ("ny", b.Id);
    }

    private static string IkkeAktorTekst(string regel) => regel switch
    {
        RegjeringenOrdlydsregler.IkkeAktorNettsted => "nettstedet",
        RegjeringenOrdlydsregler.IkkeAktorAvtaletittel => "en avtaletittel; folkerettslig er parten staten (r42)",
        RegjeringenOrdlydsregler.IkkeAktorNavn => "en navnedel",
        _ => regel,
    };

    private static ProveniensEntitet Proveniens(string type, Guid id, string handling, string bruker, object referanser)
    {
        var p = ProveniensHjelper.NyRad(type, id, virksomhetId: null, handling, bruker);
        p.KildeReferanserJson = JsonSerializer.Serialize(new { issue = 486, beslutning = "Johann 2026-11-20", detaljer = referanser });
        return p;
    }
}
