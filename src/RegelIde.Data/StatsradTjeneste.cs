using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, Refs #486 PR 2] Én forekomst av «statsråd» (alle bøyninger) og hva skrivingen gjorde med den. <see cref="Regel"/> er null for en
/// slutning; <see cref="Lesning"/> er null når forekomsten står i navneformen «Kongen i statsråd» og ikke tagges her. <see cref="Tagg"/>:
/// <see cref="AktoridentitetTjeneste.TaggUtfall"/> eller <c>ikke_tagget</c>. <see cref="Binding"/>: <c>ny</c> | <c>finnes</c> | <c>avvist</c> |
/// <c>ingen_tagg</c> | <c>ingen_referent</c>. <see cref="FlertydigHvisGodkjent"/>: juristens vilkår 2, som for «regjeringen».</summary>
public sealed record StatsradForekomst(
    Guid RettskildeId, string Rettskilde, string Eid, int Start, string Ord, string Utdrag, string? Lesning, string? Regel, string? Grunnlag,
    string? Slutningsgrunn, string Tagg, Guid? TaggId, string Binding, Guid? BindingId, bool FlertydigHvisGodkjent);

/// <summary>[Ny, Refs #486 PR 2] Fordelingen: per lesning og per regel, forekomstene i «Kongen i statsråd» og i rene endringsvedtak (ikke tagget,
/// og ikke med i <see cref="Forekomster"/> for endringsvedtakene), og vilkår 2.</summary>
public sealed record StatsradFordeling(
    int Forekomster, int Noder, int Rettskilder, int IKongenIStatsrad, int IRentEndringsvedtak, IReadOnlyDictionary<string, int> PerLesning, IReadOnlyDictionary<string, int> PerRegel,
    int FlertydigeForekomsterHvisGodkjent, int FlertydigeNoderHvisGodkjent, int Tagget, int TaggUtenEier, int TaggKolliderer);

/// <summary>[Ny, Refs #486 PR 2] Svaret på <c>POST /api/statsrad[?skriv=true]</c>. Tallene er de skrivingen gir, også i tørrkjøringen.</summary>
public sealed record StatsradRapport(
    bool Skrevet, RegjeringenLedd Navneform, RegjeringenLedd Statsradet, RegjeringenLedd Rolle, RegjeringenLedd KongenIStatsrad,
    StatsradFordeling Fordeling, IReadOnlyList<StatsradForekomst> Forekomster, IReadOnlyList<string> Hull);

/// <summary>
/// [Ny, Refs #486 PR 2, prosjekteierens avgjørelse 2026-11-25 (alternativ b: sperre og positiv tagg)] Skriver <b>en tagg per forekomst</b> av
/// ordet «statsråd» (alle bøyninger) etter ordlydsreglene (<see cref="StatsradOrdlydsregler"/>), samme mønster som <see cref="RegjeringenTjeneste"/>
/// (#486/#489) og med juristens tre vilkår for tagger og bindinger (docs/33 §5.19.1). Alt nytt er <b>forslag</b> (<c>maskinelt_foreslatt</c>).
/// Tørrkjøring er standard og gjør det samme i en transaksjon som rulles tilbake. Operasjonen er idempotent: en tagg og en forekomstbinding som
/// finnes, skrives ikke på nytt, og et avvist forslag foreslås ikke på nytt.
/// <list type="bullet">
/// <item><b>«statsråd»</b> blir en generisk navneform (grunn <c>generisk</c>, ingen aktør), som «regjeringen», slik at hver forekomst kan tagges
/// og få en forekomstbinding med lesning. Ordet er flertydig (organet, møtet, rollen). Rollebegrepet «statsråd» og navneformen «statsrådet» for
/// organet står uendret. Også forekomstene av «statsrådet» tagges mot den generiske navneformen, ikke mot organets navneform: en tagg mot
/// organets navneform ville koblet forekomsten til organet straks, uten at et menneske har vurdert den.</item>
/// <item><b>Referentene</b> skrives ikke her: organet statsrådet og rollebegrepet «statsråd» (<c>POST /api/regjeringen?skriv=true</c>, #486) og
/// Kongen i statsråd (#307). Mangler en, får lesningen ingen forekomstbinding, og det står som hull.</item>
/// <item>Forekomstene i «Kongen i statsråd» tagges ikke: de hører til aktørens navneform.</item>
/// </list>
/// Før noe skrives, kontrolleres ordlyden i Grl. § 12 første ledd, § 15 første ledd og § 28 første ledd. Ellers skrives ingenting.
/// </summary>
public sealed class StatsradTjeneste(RegelIdeDbContext db, AktoridentitetTjeneste identitet)
{
    public const string Term = "statsråd";

    public const string Grl28Eid = "https://lovdata.no/eli/lov/1814/05/17/nor/§28/ledd-1";
    public const string Grl28Sitat = "i overensstemmelse med den beslutning som er tatt i statsrådet";

    public const string Oppdagelseskilde = "manuell";
    public static string RegelOppdagelseskilde(string lesning) => $"monster:statsrad-{lesning.Replace('_', '-')}";

    public const string NavneformBegrunnelse =
        "Generisk navneform for «statsråd» (Refs #486, prosjekteierens avgjørelse 2026-11-25). Ordet er flertydig: organet statsrådet («det samlede "
        + "statsråd», Grl. § 15), møtet der Kongen i statsråd beslutter («vedtak i statsråd», Grl. § 28) og rollen statsråd («vedkommende statsråd», "
        + "§ 15). Betydningen står per forekomst (tagg per forekomst), etter ordlydsreglene eller som slutning et menneske avgjør. Rollebegrepet "
        + "«statsråd» og navneformen «statsrådet» for organet står uendret. Juristrunden i data/fasit/strukturmodell/statsrad/.";

    public async Task<StatsradRapport> KjorAsync(bool skriv, string bruker, CancellationToken ct = default)
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

    private async Task<StatsradRapport> GjorAsync(string bruker, CancellationToken ct)
    {
        var grl12 = await KontrollerAsync(RegjeringenTjeneste.Grl12Eid, RegjeringenTjeneste.Grl12Sitat, ct);
        var grl15 = await KontrollerAsync(RegjeringenTjeneste.Grl15Eid, RegjeringenTjeneste.Grl15RolleSitat, ct);
        var grl28 = await KontrollerAsync(Grl28Eid, Grl28Sitat, ct);
        var hull = new List<string>();

        var statsradet = await ReferentAsync("organet statsrådet", db.Virksomheter.AnyAsync(v => v.Id == RegjeringenTjeneste.StatsradetId, ct),
            RegjeringenTjeneste.StatsradetId, ct);
        var rolle = await ReferentAsync("rollebegrepet «statsråd»", db.Begreper.AnyAsync(b => b.Id == RegjeringenTjeneste.StatsradRolleId
            && b.Entitetsstatus == "gjeldende" && b.Godkjenning != Strukturkanter.Avvist, ct), RegjeringenTjeneste.StatsradRolleId, ct);
        var kongen = await KongenIStatsradAsync(ct);
        var generisk = await GeneriskNavneformAsync(bruker, ct);

        var forekomster = await ForekomsterAsync(ct);
        var konvensjon = await Ordsperrer.KonvensjonsnoderAsync(db, forekomster.Select(f => f.NodeId).Distinct().ToList(), ct);
        var rader = new List<StatsradForekomst>();
        var generiske = new GeneriskNavneformTjeneste(db);
        foreach (var f in forekomster)
        {
            var titler = new[] { f.Tittel, f.Kortnavn };
            var treff = StatsradOrdlydsregler.Vurder(f.Tekst, f.Start, f.Slutt, titler, konvensjon.Contains(f.NodeId));
            var utdrag = f.Tekst[Math.Max(0, f.Start - 60)..Math.Min(f.Tekst.Length, f.Slutt + 60)];
            if (treff is { Lesning: null })
            {
                rader.Add(new(f.RettskildeId, f.Kortnavn ?? f.Tittel, f.Eid, f.Start, f.Tekst[f.Start..f.Slutt], utdrag, null, treff.Regel, treff.Grunnlag,
                    null, "ikke_tagget", null, "ingen_tagg", null, false));
                continue;
            }
            var lesning = treff?.Lesning ?? Forekomstlesninger.Slutning;
            var slutningsgrunn = treff is null ? StatsradOrdlydsregler.Slutningsgrunn(f.Tekst, f.Start, f.Slutt, titler) : null;
            string taggUtfall = "ikke_kjort", bindingUtfall = "ingen_tagg";
            Guid? taggId = null, bindingId = null;
            if (generisk.Id is { } nfId && generisk.Utfall is "opprettet" or "finnes")
            {
                (taggUtfall, taggId) = await identitet.TaggForekomstAsync(f.RettskildeId, f.Eid, f.Tekst, f.Start, f.Slutt, nfId, bruker, ct);
                if (taggId is { } tid)
                {
                    var referent = lesning switch
                    {
                        Forekomstlesninger.Statsradet => statsradet.Id is { } s ? Kantnode.Virksomhet(s) : null,
                        Forekomstlesninger.RolleStatsrad => rolle.Id is { } r ? Kantnode.Begrep(r) : null,
                        Forekomstlesninger.KongenIStatsrad => kongen.Id is { } k ? Kantnode.Virksomhet(k) : null,
                        _ => new Kantnode(null, null),
                    };
                    (bindingUtfall, bindingId) = referent is null
                        ? ("ingen_referent", null)
                        : await ForekomstbindingAsync(generiske, nfId, f, tid, treff, lesning, slutningsgrunn, referent, grl12, grl15, grl28, bruker, ct);
                }
            }
            rader.Add(new(f.RettskildeId, f.Kortnavn ?? f.Tittel, f.Eid, f.Start, f.Tekst[f.Start..f.Slutt], utdrag, lesning, treff?.Regel, treff?.Grunnlag,
                slutningsgrunn, taggUtfall, taggId, bindingUtfall, bindingId, false));
        }

        // Juristens vilkår 2 (som for «regjeringen»): en slutning i en node der en annen tagget forekomst har en regeltagg, gir «flertydig» når
        // regeltaggene er vurdert. Forekomstene i «Kongen i statsråd» er ikke tagger av navneformen og telles ikke.
        var taggede = rader.Where(r => r.Lesning is not null).ToList();
        var perNode = taggede.GroupBy(r => (r.RettskildeId, r.Eid)).ToList();
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

        foreach (var (ledd, lesning) in new[] { (statsradet, Forekomstlesninger.Statsradet), (rolle, Forekomstlesninger.RolleStatsrad), (kongen, Forekomstlesninger.KongenIStatsrad) })
        {
            if (ledd.Id is null) hull.Add($"{ledd.Hva}: {ledd.Merknad} Lesningen «{Forekomstlesninger.Tekst(lesning)}» får ingen forekomstbinding.");
        }
        // Som for «regjeringen» (prosjekteierens avgjørelse 2026-11-20 på PR #489): forekomster uten eier blir stående utagget og vises som hull.
        foreach (var g in rader.Where(r => r.Tagg == AktoridentitetTjeneste.TaggUtfall.UtenEier).GroupBy(r => r.Rettskilde))
        {
            hull.Add($"{g.Key}: {g.Count()} forekomst(er) er ikke tagget, fordi ingen ansvarlig departement i katalogen kan eie taggen. Hull; ingen eier gjettes.");
        }

        var lesninger = Forekomstlesninger.Alle.Where(l => rader.Any(r => r.Lesning == l) || l is Forekomstlesninger.Slutning or Forekomstlesninger.RolleStatsrad
            or Forekomstlesninger.Statsradet or Forekomstlesninger.KongenIStatsrad);
        var fordeling = new StatsradFordeling(
            rader.Count, rader.Select(r => (r.RettskildeId, r.Eid)).Distinct().Count(), rader.Select(r => r.RettskildeId).Distinct().Count(),
            rader.Count(r => r.Lesning is null), IEndringsvedtak,
            lesninger.ToDictionary(l => l, l => rader.Count(r => r.Lesning == l)),
            StatsradOrdlydsregler.Regler.ToDictionary(k => k, k => rader.Count(r => r.Regel == k)),
            merket.Count, flertydigeNoder,
            rader.Count(r => r.TaggId is not null), rader.Count(r => r.Tagg == AktoridentitetTjeneste.TaggUtfall.UtenEier),
            rader.Count(r => r.Tagg == AktoridentitetTjeneste.TaggUtfall.Kolliderer));
        return new StatsradRapport(false, generisk, statsradet, rolle, kongen, fordeling, rader, hull);
    }

    /// <summary>Bestemmelsen skal stå nøyaktig én gang i en gjeldende rettskilde med ordlyden. Ellers skrives ingenting (CLAUDE.md §8).</summary>
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

    private static async Task<RegjeringenLedd> ReferentAsync(string hva, Task<bool> finnes, Guid id, CancellationToken ct)
    {
        _ = ct;
        return await finnes
            ? new($"{hva} (finnes, skrives ikke)", "finnes", id, null)
            : new($"{hva} (finnes, skrives ikke)", "ikke_kjort", null, "Finnes ikke. Kjør POST /api/regjeringen?skriv=true først (#486).");
    }

    /// <summary>Kongen i statsråd finnes (#307). Nøyaktig én aktør med navneformen; ellers et hull (samme oppslag som <see cref="RegjeringenTjeneste"/>).</summary>
    private async Task<RegjeringenLedd> KongenIStatsradAsync(CancellationToken ct)
    {
        const string hva = "aktøren Kongen i statsråd (finnes, skrives ikke)";
        var ider = await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                                               && b.Godkjenning != Strukturkanter.Avvist && b.Term == RegjeringenTjeneste.KongenIStatsrad)
            .Select(b => b.VirksomhetReferanseId!.Value).Distinct().ToListAsync(ct);
        return ider.Count == 1
            ? new(hva, "finnes", ider[0], null)
            : new(hva, "ikke_kjort", null, $"{ider.Count} aktører har navneformen «{RegjeringenTjeneste.KongenIStatsrad}».");
    }

    private async Task<RegjeringenLedd> GeneriskNavneformAsync(string bruker, CancellationToken ct)
    {
        const string hva = "generisk navneform «statsråd» (ingen aktør)";
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
        var p = ProveniensHjelper.NyRad("begrep", nf.Id, virksomhetId: null, "foreslatt", bruker);
        p.KildeReferanserJson = JsonSerializer.Serialize(new { issue = 486, beslutning = "prosjekteieren 2026-11-25", detaljer = new { generisk = Term } });
        db.Proveniens.Add(p);
        await db.SaveChangesAsync(ct);
        return new(hva, "opprettet", nf.Id, null);
    }

    /// <summary>Forekomstene i rene endringsvedtak, som ikke tagges (satt av <see cref="ForekomsterAsync"/>).</summary>
    private int IEndringsvedtak;

    private sealed record Forekomst(Guid RettskildeId, string Tittel, string? Kortnavn, Guid NodeId, string Eid, string Tekst, int Start, int Slutt);

    /// <summary>Forekomstene: hele ord (<see cref="StatsradOrdlydsregler.Ordet"/>) i gjeldende, ikke opphevede noder i gjeldende rettskilder — samme
    /// utvalg som juristrundens telling (537 forekomster, 8 av dem sammensetninger som ikke er treff). Sortert på rettskilde, dokumentrekkefølge og
    /// posisjon.</summary>
    private async Task<List<Forekomst>> ForekomsterAsync(CancellationToken ct)
    {
        var noder = await db.RettskildeNoder
            .Where(n => n.Entitetsstatus == "gjeldende" && !n.Opphevet && n.Tekst != null && n.Tekst.ToLower().Contains(Term))
            .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende"), n => n.RettskildeId, r => r.Id,
                (n, r) => new { r.Id, r.Tittel, r.Kortnavn, NodeId = n.Id, n.Eid, n.Tekst, n.Sorteringsrekkefolge, r.RentEndringsvedtak })
            .ToListAsync(ct);
        // [Ny, juristmålingen PR 2 (t3)] Et rent endringsvedtak gjentar teksten i loven det endrer (og den kan være endret senere). Det tagges ikke,
        // som sveipene (Sveipeutvalg); forekomstene telles i rapporten.
        IEndringsvedtak = noder.Where(n => n.RentEndringsvedtak == true).Sum(n => StatsradOrdlydsregler.Ordet.Matches(n.Tekst!).Count);
        return noder.Where(n => n.RentEndringsvedtak != true).OrderBy(n => n.Kortnavn ?? n.Tittel, StringComparer.Ordinal).ThenBy(n => n.Id).ThenBy(n => n.Sorteringsrekkefolge)
            .SelectMany(n => StatsradOrdlydsregler.Ordet.Matches(n.Tekst!)
                .Select(m => new Forekomst(n.Id, n.Tittel, n.Kortnavn, n.NodeId, n.Eid, n.Tekst!, m.Index, m.Index + m.Length)))
            .ToList();
    }

    private async Task<(string Utfall, Guid? BindingId)> ForekomstbindingAsync(
        GeneriskNavneformTjeneste generiske, Guid navneformId, Forekomst f, Guid taggId, StatsradTreff? treff, string lesning, string? slutningsgrunn,
        Kantnode referent, Hjemmel grl12, Hjemmel grl15, Hjemmel grl28, string bruker, CancellationToken ct)
    {
        // Avvist eller ikke: en forekomstbinding for samme tagg er samme forslag. Et avvist forslag foreslås ikke på nytt.
        var tidligere = await db.NavneformBindinger.Where(b => b.TekstTaggId == taggId && b.Entitetsstatus == "gjeldende")
            .OrderBy(b => b.Status == Strukturkanter.Avvist ? 1 : 0).FirstOrDefaultAsync(ct);
        if (tidligere is not null) return (tidligere.Status == Strukturkanter.Avvist ? "avvist" : "finnes", tidligere.Id);

        var ord = f.Tekst[f.Start..f.Slutt];
        var sitat = $"«{f.Tekst[Math.Max(0, f.Start - 60)..Math.Min(f.Tekst.Length, f.Slutt + 40)].Trim()}»";
        NyNavneformBinding ny = (lesning, treff?.Regel) switch
        {
            (Forekomstlesninger.KongenIStatsrad, _) => new(navneformId, f.RettskildeId, referent, HjemmelRettskildeId: grl28.RettskildeId, HjemmelEid: Grl28Eid,
                Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"],
                Begrunnelse: $"Beslutningsformen Kongen i statsråd: «{treff!.Grunnlag}». Saker av viktighet behandles i statsråd (Grunnloven § 28 første ledd: «… i "
                             + $"overensstemmelse med den beslutning som er tatt i statsrådet», og «unntas fra behandling i statsråd»), og det er kongen som "
                             + $"beslutter der (§ 30 annet ledd: «det er denne forbeholdt å fatte beslutning»). Regel «i statsråd» (Refs #486). {sitat}"),
            (Forekomstlesninger.Statsradet, StatsradOrdlydsregler.OrganIntetkjonn) => new(navneformId, f.RettskildeId, referent,
                HjemmelRettskildeId: grl12.RettskildeId, HjemmelEid: RegjeringenTjeneste.Grl12Eid,
                Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"],
                Begrunnelse: $"Organet statsrådet: intetkjønn foran («{treff!.Grunnlag}») viser til rådet (Grunnloven § 12 første ledd: «et råd»), ikke "
                             + $"rollen, som er hankjønn («en statsråd»). Regel «intetkjønn» (Refs #486). {sitat}"),
            (Forekomstlesninger.Statsradet, _) => new(navneformId, f.RettskildeId, referent, HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid,
                Kunnskapsstatus: Strukturkanter.Tekstfunn,
                Begrunnelse: $"Organet statsrådet: «{ord}» er organets navn (navneformen «statsrådet», Grunnloven § 12 første ledd), og ordet står ikke om "
                             + $"møtet («i statsrådet», «utenfor statsrådet»). Regel «statsrådet» (Refs #486). {sitat}"),
            (Forekomstlesninger.RolleStatsrad, StatsradOrdlydsregler.RolleTittel) => new(navneformId, f.RettskildeId, referent,
                HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid, Kunnskapsstatus: Strukturkanter.Tekstfunn,
                Begrunnelse: $"Rollen statsråd: tittelen foran et navn («{treff!.Grunnlag}»). Personen har rollen; ingen kant avledes av forekomsten "
                             + $"(juristrunden q4, prosjekteieren 2026-11-25). Regel «tittel + navn» (Refs #486). {sitat}"),
            (Forekomstlesninger.RolleStatsrad, _) => new(navneformId, f.RettskildeId, referent, HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid,
                Kunnskapsstatus: Strukturkanter.Tekstfunn,
                Begrunnelse: $"Rollen statsråd: «{treff!.Grunnlag}» er medlemmet, i samme betydning som «vedkommende statsråd» i Grunnloven § 15 første ledd "
                             + $"(rollebegrepets hjemmel). Regel «{RegelTekst(treff.Regel)}» (Refs #486). {sitat}"),
            (Forekomstlesninger.IkkeAktor, _) => new(navneformId, f.RettskildeId, referent, HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid,
                Kunnskapsstatus: Strukturkanter.Tekstfunn,
                Begrunnelse: $"Ikke aktør: ordet er en del av en lovtittel («{treff!.Grunnlag}»). Regel «lovtittel» (Refs #486, juristrunden s21). {sitat}"),
            (Forekomstlesninger.GeneriskKonvensjonsstat, _) => new(navneformId, f.RettskildeId, referent, HjemmelRettskildeId: f.RettskildeId, HjemmelEid: f.Eid,
                Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"],
                Begrunnelse: $"Generisk konvensjonsstat: noden står i konvensjonstekst, og ordet gjelder statsråden eller rådet i den staten bestemmelsen "
                             + $"gjelder, ikke bare den norske. Ingen kant. Regel «konvensjonstekst» (Refs #486). {sitat}"),
            _ => new(navneformId, f.RettskildeId, referent, Kunnskapsstatus: Strukturkanter.Uavklart,
                Begrunnelse: $"[slutning] Ordlyden avgjør ikke hva «{ord}» er her ({slutningsgrunn}). Det kan være organet statsrådet (Grl. § 12 første ledd), "
                             + $"Kongen i statsråd (beslutningsformen, §§ 28 og 30) eller rollen statsråd (§ 15); et menneske avgjør per forekomst. Ingen "
                             + $"referent er gjettet. {sitat}"),
        };
        _ = grl15;
        ny = ny with
        {
            TekstTaggId = taggId, Lesning = lesning, Status = Strukturkanter.MaskineltForeslatt, OppdagelsesKilde = RegelOppdagelseskilde(lesning),
            Kommentar = treff is not null ? $"Ordlydsregel {treff.Regel} (Refs #486)." : $"Slutning: {slutningsgrunn} (Refs #486).",
        };
        var b = await generiske.OpprettBindingAsync(ny, bruker, ct);
        return ("ny", b.Id);
    }

    private static string RegelTekst(string regel) => regel switch
    {
        StatsradOrdlydsregler.RolleRolleord => "rolleord foran",
        StatsradOrdlydsregler.RolleSideordning => "sideordning med statsminister/embetsverk",
        StatsradOrdlydsregler.RolleBestemtFlertall => "bestemt form eller flertall",
        _ => regel,
    };
}
