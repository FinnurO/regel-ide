using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #463] Et skrivbart ledd i <see cref="NorgeStatenRapport"/>. <see cref="Utfall"/>: <c>opprettet</c> | <c>finnes</c> |
/// <c>konflikt</c> (noe annet har alt navnet eller typen; det gjettes ikke at det er det samme, ingenting skrives) | <c>avvist</c> (et menneske
/// har avvist forslaget; det foreslås ikke på nytt).</summary>
public sealed record NorgeStatenLedd(string Hva, string Utfall, Guid? Id, string? Merknad);

/// <summary>[Ny, issue #463] Én kant: O Norge består av (fylke, Svalbard) eller G departement har_organ staten («er organ for staten»). <see cref="Utfall"/>: <c>ny</c> |
/// <c>finnes</c> | <c>avvist</c>.</summary>
public sealed record NorgeStatenKant(string Fra, string Til, string Hjemmel, string Kunnskapsstatus, string Utfall, Guid? KantId, Guid? PremissKantId);

/// <summary>[Ny, issue #463] Svaret på <c>POST /api/norge-staten[?skriv=true]</c>. Tallene er de skrivingen gir, også i tørrkjøringen (den gjør
/// det samme i en transaksjon som rulles tilbake). <see cref="IkkeRegistrert"/> er det saken ber om, men som modellen ikke har en vei for ennå
/// (navneformer for et område, #464).</summary>
public sealed record NorgeStatenRapport(
    bool Skrevet, NorgeStatenLedd Norge, IReadOnlyList<NorgeStatenKant> Omradekanter, IReadOnlyList<string> OmradeHull,
    NorgeStatenLedd Staten, NorgeStatenLedd Navneform, NorgeStatenLedd Aktortype,
    IReadOnlyList<NorgeStatenKant> Departementskanter, IReadOnlyList<string> DepartementHull, IReadOnlyList<string> IkkeRegistrert);

/// <summary>
/// [Ny, issue #463 (sak 2 av 3 etter beslutningsnotatet «Norge, staten og forvaltningsnivåene»), Johanns avgjørelse 2026-11-05 (modell B),
/// klarsignal 2026-11-17] Skriver området <b>Norge</b>, aktøren <b>staten</b> og kantene mellom dem og resten av modellen. Alt nytt er
/// <b>forslag</b> (aldri godkjent). Operasjonen er idempotent: Norge og staten har faste id-er (<see cref="NorgeId"/>, <see cref="StatenId"/>),
/// en ny kjøring skriver ingenting, og et avvist forslag foreslås ikke på nytt. Tørrkjøringen er standard. Samme mønster som
/// <see cref="KlasseoppsettTjeneste"/> (#442).
/// <list type="bullet">
/// <item><b>Norge</b>: område med typen <see cref="Omradetyper.Rike"/>, hjemmel Grunnloven § 1 første ledd («Kongeriket Norge er et fritt,
/// selvstendig, udelelig og uavhendelig rike»), tekstfunn, <c>manuelt_foreslatt</c>. Ordlyden kontrolleres i korpus før noe skrives.</item>
/// <item><b>Fylkene er del av Norge</b>: <c>O bestar_av</c> Norge → hvert gjeldende fylke (også Oslo), hjemmel kommuneloven § 2-1 første ledd
/// («Norge er inndelt i kommuner og fylkeskommuner …»), fortolkning (ordlyd, kontekst og system: hver kommune hører til et fylke, § 3-1 første
/// ledd), <c>maskinelt_foreslatt</c>, oppdagelseskilde <see cref="FylkeneOppdagelseskilde"/>.</item>
/// <item><b>Svalbard er del av Norge</b>: <c>O bestar_av</c> Norge → Svalbard, hjemmel svalbardloven § 1 første ledd («Svalbard er en del av
/// kongeriket Norge»), tekstfunn, <c>manuelt_foreslatt</c>. Jan Mayen er ikke et område i registeret og får ingen kant (Jan Mayen-loven § 1
/// sier det samme; et område opprettes ikke her).</item>
/// <item><b>Staten</b>: aktør (rettssubjekt) uten organisasjonsnummer, med den gjeldende navneformen «staten» (hjemmel Grl. § 16 første ledd,
/// der ordet står om den norske staten: «understøttes som sådan av staten», tekstfunn) og aktørtypen rettssubjekt som forslag med
/// fortolkning (tvisteloven § 2-1 første ledd bokstav b: «stat» har partsevne). Navneformen har ordsperre (<see cref="Ordsperrer"/>).</item>
/// <item><b>Departementene er organer for staten</b>: [ENDRET, Johanns avgjørelse 2026-11-18 på PR #480: <c>har_organ</c>, ikke <c>del_av</c>]
/// <c>G har_organ</c> fra hvert medlem av klassen «departement» (#442) til staten («Finansdepartementet er organ for staten»; fra staten:
/// «har organet Finansdepartementet»). Retningen følger noderegelen for G (organ → rettssubjekt),
/// kunnskapsstatus <c>avledet</c> med premisset for medlemskapet som premiss (R oppretter fra livsløpet eller M-kanten), og samme hjemmel eller
/// kilde utenfor korpus som premisset. Regelen «et departement er et organ for staten» står i klassens definisjon (modell B).</item>
/// </list>
/// <b>Ikke registrert</b>: navneformene «Kongeriket Norge» og «riket» for området. Navneformer finnes bare for aktører (begreper med
/// <c>virksomhet_referanse_id</c>); for områder bygges de i #464. De står i begrunnelsen og i <see cref="NorgeStatenRapport.IkkeRegistrert"/>.
/// «Kongeriket Norge» er territorielt (svalbardloven og Jan Mayen-loven), ikke navnet på staten (juristens retting i notatet).
/// </summary>
public sealed class NorgeStatenTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste, AktoridentitetTjeneste identitet)
{
    /// <summary>Fast id for området Norge, så skrivingen er idempotent uten å slå opp på navn (CLAUDE.md §4: stabil nøkkel).</summary>
    public static readonly Guid NorgeId = Guid.Parse("4e6f7267-6500-4463-9001-000000000463");

    /// <summary>Fast id for aktøren staten. Staten har ikke ett organisasjonsnummer, så id-en er den stabile nøkkelen.</summary>
    public static readonly Guid StatenId = Guid.Parse("53746174-656e-4463-9001-000000000463");

    public const string GrunnlovenEid = "https://lovdata.no/eli/lov/1814/05/17/nor/§1/ledd-1";
    public const string GrunnlovenSitat = "Kongeriket Norge er et fritt, selvstendig, udelelig og uavhendelig rike.";
    public const string SvalbardlovenEid = "https://lovdata.no/eli/lov/1925/07/17/11/nor/§1/ledd-1";
    public const string SvalbardlovenSitat = "Svalbard er en del av kongeriket Norge.";
    public const string KommunelovenSitat = "Norge er inndelt i kommuner og fylkeskommuner";
    public const string TvistelovenPartsevneEid = "https://lovdata.no/eli/lov/2005/06/17/90/nor/§2-1/ledd-1/punkt-2";
    public const string TvistelovenSitat = "stat, kommuner og fylkeskommuner";
    public const string Grunnloven16Eid = "https://lovdata.no/eli/lov/1814/05/17/nor/§16/ledd-1";
    public const string Grunnloven16Sitat = "understøttes som sådan av staten";

    public const string FylkeneOppdagelseskilde = "monster:norge-fylkene";
    public const string HarOrganOppdagelseskilde = "monster:departement-organ-for-staten";
    public const string Oppdagelseskilde = "manuell";
    private const string Rettssubjekt = "rettssubjekt"; // Nodetyper.Aktortyper[0]; ingen egen konstant der

    public const string NorgeDefinisjon =
        "Riket Norge (Grunnloven § 1: «Kongeriket Norge»). Fylkene og Svalbard er deler av det. Området, ikke rettssubjektet: staten er en egen aktør.";

    public const string NorgeBegrunnelse =
        "Hjemmel: Grunnloven § 1 første ledd («" + GrunnlovenSitat + "»). Navneformene «Kongeriket Norge» (territoriell, slik svalbardloven § 1 "
        + "og Jan Mayen-loven § 1 bruker den; ikke navnet på staten) og «riket» (Grl. § 13, § 16, § 106) hører til området, men kan ikke registreres "
        + "før navneformer for områder er bygget (#464). Johanns avgjørelse 2026-11-05 (modell B), #463.";

    public const string FylkeneBegrunnelse =
        "Kommuneloven § 2-1 første ledd deler Norge inn i kommuner og fylkeskommuner, og § 3-1 første ledd sier at hver kommune hører til et "
        + "fylke og at hvert fylke unntatt Oslo utgjør en fylkeskommune. At fylket (også Oslo) er en del av Norge, følger av sammenhengen, ikke av "
        + "ordlyden alene. Grunnloven § 1 er forankring. #463.";

    public const string StatenAktortypeBegrunnelse =
        "Tvisteloven § 2-1 første ledd bokstav b gir «stat» partsevne, på linje med kommuner og fylkeskommuner, som kommuneloven § 2-1 andre ledd "
        + "kaller rettssubjekter. Ordlyden sier partsevne, ikke rettssubjekt; at staten er et rettssubjekt, følger av sammenhengen. Grunnloven § 1 "
        + "er forankring. Beslutningsnotatet: aktørtypen er fortolkning. #463.";

    public const string StatenNavneformBegrunnelse =
        "«staten» står ordrett om den norske staten i Grunnloven § 16 første ledd («" + Grunnloven16Sitat + "»). Ordet står også om andre stater; "
        + "ordsperren (docs/33 §5.19) stopper «den (adj.) staten» (ikke «den norske staten»), «staten der/hvor/hvis», konvensjonstekst og "
        + "bindestrek rett foran («EØS-staten»). #463.";

    public async Task<NorgeStatenRapport> KjorAsync(bool skriv, string bruker, CancellationToken ct = default)
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

    private async Task<NorgeStatenRapport> GjorAsync(string bruker, CancellationToken ct)
    {
        var grunnloven = await KontrollerAsync(GrunnlovenEid, GrunnlovenSitat, ct);
        var svalbardloven = await KontrollerAsync(SvalbardlovenEid, SvalbardlovenSitat, ct);
        var kommuneloven = await KontrollerAsync(Klasseregler.KommunelovenInndelingEid, KommunelovenSitat, ct);
        var tvisteloven = await KontrollerAsync(TvistelovenPartsevneEid, TvistelovenSitat, ct);
        var grunnloven16 = await KontrollerAsync(Grunnloven16Eid, Grunnloven16Sitat, ct);

        var norge = await NorgeAsync(bruker, ct);
        var (omradekanter, omradeHull) = norge.Utfall is "opprettet" or "finnes"
            ? await OmradekanterAsync(kommuneloven, svalbardloven, bruker, ct)
            : ([], [$"Norge ble ikke skrevet ({norge.Utfall}); ingen kanter."]);

        var staten = await StatenAsync(bruker, ct);
        NorgeStatenLedd navneform, aktortype;
        List<NorgeStatenKant> depKanter = [];
        List<string> depHull = [];
        if (staten.Utfall is "opprettet" or "finnes")
        {
            navneform = await NavneformAsync(grunnloven16, bruker, ct);
            aktortype = await AktortypeAsync(tvisteloven, bruker, ct);
            (depKanter, depHull) = await DepartementskanterAsync(bruker, ct);
        }
        else
        {
            navneform = new("navneform «staten»", "ikke_kjort", null, "Staten ble ikke skrevet.");
            aktortype = new("aktørtype rettssubjekt", "ikke_kjort", null, "Staten ble ikke skrevet.");
            depHull.Add("Staten ble ikke skrevet; ingen kanter.");
        }
        _ = grunnloven;
        return new NorgeStatenRapport(false, norge, omradekanter, omradeHull, staten, navneform, aktortype, depKanter, depHull,
        [
            "«Kongeriket Norge» som navneform for området Norge: navneformer finnes bare for aktører; for områder bygges de i #464.",
            "«riket» som navneform for området Norge: samme grunn (#464).",
        ]);
    }

    /// <summary>Bestemmelsen skal stå nøyaktig én gang i en gjeldende rettskilde med ordlyden avgjørelsen bygger på. Ellers skrives ingenting
    /// (CLAUDE.md §8, samme kontroll som #442).</summary>
    private async Task<Guid> KontrollerAsync(string eid, string maInneholde, CancellationToken ct)
    {
        var n = await db.RettskildeNoder.Where(x => x.Eid == eid && x.Entitetsstatus == "gjeldende")
            .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende" && r.Status == "Gjeldende"), x => x.RettskildeId, r => r.Id,
                (x, r) => new { r.Id, x.Tekst }).ToListAsync(ct);
        if (n.Count != 1 || n[0].Tekst is null || !n[0].Tekst!.Contains(maInneholde, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Fant ikke nøyaktig én gjeldende node {eid} med «{maInneholde}» ({n.Count} treff). Ingenting skrives.");
        }
        return n[0].Id;
    }

    private async Task<NorgeStatenLedd> NorgeAsync(string bruker, CancellationToken ct)
    {
        const string hva = "området Norge (rike)";
        var finnes = await db.Begreper.FirstOrDefaultAsync(b => b.Id == NorgeId, ct);
        if (finnes is not null)
        {
            return new(hva, finnes.Godkjenning == Strukturkanter.Avvist || finnes.Entitetsstatus != "gjeldende" ? "avvist" : "finnes", finnes.Id,
                finnes.AvvisningBegrunnelse);
        }
        var annen = await db.Begreper.Where(b => b.Entitetsstatus == "gjeldende" && b.Begrepskategori == Nodetyper.Omrade
                                                 && (b.Omradetype == Omradetyper.Rike || b.Term.ToLower() == "norge"))
            .Select(b => new { b.Id, b.Term, b.Omradetype }).FirstOrDefaultAsync(ct);
        if (annen is not null)
        {
            return new(hva, "konflikt", annen.Id,
                $"Det finnes alt et område «{annen.Term}» ({annen.Omradetype ?? "uten type"}). Det gjettes ikke at det er Norge; et menneske avgjør.");
        }
        var b = new BegrepEntitet
        {
            Id = NorgeId,
            Term = "Norge",
            Definisjon = NorgeDefinisjon,
            Begrepskategori = Nodetyper.Omrade,
            Omradetype = Omradetyper.Rike,
            LovkildeId = null, // fast, nasjonalt (som klassene i #442); hjemmelen står i lovreferanse_eid
            LovreferanseEid = GrunnlovenEid,
            Kunnskapsstatus = Strukturkanter.Tekstfunn,
            Begrunnelse = NorgeBegrunnelse,
            Godkjenning = Strukturkanter.ManueltForeslatt,
            OppdagelsesKilde = Oppdagelseskilde,
            Status = "publisert",
            OpprettetAv = bruker,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        db.Proveniens.Add(Proveniens("begrep", b.Id, "foreslatt", bruker, new { hjemmel = GrunnlovenEid, omradetype = Omradetyper.Rike }));
        await db.SaveChangesAsync(ct);
        return new(hva, "opprettet", b.Id, null);
    }

    private async Task<(List<NorgeStatenKant>, List<string>)> OmradekanterAsync(Guid kommuneloven, Guid svalbardloven, string bruker, CancellationToken ct)
    {
        var svar = new List<NorgeStatenKant>();
        var hull = new List<string>();
        var fylker = await db.Begreper
            .Where(b => b.Begrepskategori == Nodetyper.Omrade && b.Omradetype == Omradetyper.Fylke && b.Entitetsstatus == "gjeldende" && b.GyldigTil == null)
            .OrderBy(b => b.Omradekode).Select(b => new { b.Id, b.Term }).ToListAsync(ct);
        foreach (var f in fylker)
        {
            svar.Add(await OmradekantAsync(f.Id, f.Term, new NyStrukturkant(Strukturkanter.Omradesammensetning, "bestar_av",
                Kantnode.Begrep(NorgeId), Kantnode.Begrep(f.Id), HjemmelRettskildeId: kommuneloven, HjemmelEid: Klasseregler.KommunelovenInndelingEid,
                Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: FylkeneOppdagelseskilde,
                Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"], Begrunnelse: FylkeneBegrunnelse,
                Kommentar: $"Fylket {f.Term} er en del av Norge (#463)."), "kl. § 2-1 første ledd", bruker, ct));
        }
        var svalbard = await db.Begreper.Where(b => b.Begrepskategori == Nodetyper.Omrade && b.Omradetype == Omradetyper.Annet && b.Term == "Svalbard"
                                                    && b.Entitetsstatus == "gjeldende").Select(b => b.Id).ToListAsync(ct);
        if (svalbard.Count != 1)
        {
            hull.Add($"Svalbard: {svalbard.Count} gjeldende områder med navnet. Ingen kant.");
        }
        else
        {
            svar.Add(await OmradekantAsync(svalbard[0], "Svalbard", new NyStrukturkant(Strukturkanter.Omradesammensetning, "bestar_av",
                Kantnode.Begrep(NorgeId), Kantnode.Begrep(svalbard[0]), HjemmelRettskildeId: svalbardloven, HjemmelEid: SvalbardlovenEid,
                Status: Strukturkanter.ManueltForeslatt, OppdagelsesKilde: Oppdagelseskilde, Kunnskapsstatus: Strukturkanter.Tekstfunn,
                Kommentar: $"«{SvalbardlovenSitat}» (svalbardloven § 1 første ledd, #463)"), "svalbardloven § 1 første ledd", bruker, ct));
        }
        if (fylker.Count == 0) hull.Add("Ingen gjeldende fylker i områderegisteret.");
        return (svar, hull);
    }

    private async Task<NorgeStatenKant> OmradekantAsync(Guid delId, string del, NyStrukturkant ny, string hjemmel, string bruker, CancellationToken ct)
    {
        // Avvist eller ikke: O Norge → delen er samme utsagn uansett hjemmel. Et avvist forslag foreslås ikke på nytt.
        var tidligere = await kanttjeneste.GjeldendeInklAvviste
            .Where(k => k.Kategori == Strukturkanter.Omradesammensetning && k.FraBegrepId == NorgeId && k.TilBegrepId == delId)
            .OrderBy(k => k.Status == Strukturkanter.Avvist ? 1 : 0).FirstOrDefaultAsync(ct);
        if (tidligere is not null)
        {
            return new("Norge", del, hjemmel, tidligere.Kunnskapsstatus ?? "", tidligere.Status == Strukturkanter.Avvist ? "avvist" : "finnes", tidligere.Id, null);
        }
        var o = await kanttjeneste.OpprettAsync(ny, bruker, ct);
        return new("Norge", del, hjemmel, ny.Kunnskapsstatus!, o.VarNy ? "ny" : "finnes", o.Kant.Id, null);
    }

    private async Task<NorgeStatenLedd> StatenAsync(string bruker, CancellationToken ct)
    {
        const string hva = "aktøren staten";
        if (await db.Virksomheter.AnyAsync(v => v.Id == StatenId, ct)) return new(hva, "finnes", StatenId, null);
        var annen = await db.Virksomheter.Where(v => v.Navn.ToLower() == Ordsperrer.Staten).Select(v => (Guid?)v.Id).FirstOrDefaultAsync(ct)
                    ?? await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.Term.ToLower() == Ordsperrer.Staten)
                        .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        if (annen is not null)
        {
            return new(hva, "konflikt", annen, "Det finnes alt en aktør eller navneform «staten». Det gjettes ikke at det er samme aktør; et menneske avgjør.");
        }
        var v = new Virksomhet { Id = StatenId, Navn = Ordsperrer.Staten, Aktiv = true, OpprettetTidspunkt = DateTimeOffset.UtcNow };
        db.Virksomheter.Add(v);
        db.Proveniens.Add(Proveniens("virksomhet", v.Id, "opprettet", bruker, new
        {
            grunn = "Aktøren staten (rettssubjekt), uten organisasjonsnummer: navnet kommer fra lovteksten, og identiteten er den faste id-en.",
        }));
        await db.SaveChangesAsync(ct);
        return new(hva, "opprettet", v.Id, null);
    }

    private async Task<NorgeStatenLedd> NavneformAsync(Guid grunnloven, string bruker, CancellationToken ct)
    {
        const string hva = "navneform «staten» (gjeldende, med ordsperre)";
        var avvist = await db.Begreper.AnyAsync(b => b.VirksomhetReferanseId == StatenId && b.Godkjenning == Strukturkanter.Avvist
                                                     && b.Term.ToLower() == Ordsperrer.Staten, ct);
        if (avvist) return new(hva, "avvist", null, "Et menneske har avvist navneformen; den foreslås ikke på nytt.");
        var r = await identitet.RegistrerNavnetSomGjeldendeAsync(StatenId, new Utsagnsgrunnlag
        {
            Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = grunnloven, HjemmelEid = Grunnloven16Eid,
            Kunnskapsstatus = Strukturkanter.Tekstfunn, Godkjenning = Strukturkanter.ManueltForeslatt, Begrunnelse = StatenNavneformBegrunnelse,
        }, bruker, ct);
        if (r.Navneform is { } nf && r.Utfall != NavnetSomGjeldende.Fantes)
        {
            nf.OppdagelsesKilde = Oppdagelseskilde;
            await db.SaveChangesAsync(ct);
        }
        return new(hva, r.Utfall == NavnetSomGjeldende.Fantes ? "finnes" : "opprettet", r.Navneform?.Id, r.Merknad);
    }

    private async Task<NorgeStatenLedd> AktortypeAsync(Guid tvisteloven, string bruker, CancellationToken ct)
    {
        const string hva = "aktørtype rettssubjekt (fortolkning)";
        var finnes = await db.AktortypeUtsagn.Where(a => a.VirksomhetId == StatenId && a.Aktortype == Rettssubjekt)
            .OrderByDescending(a => a.OpprettetTidspunkt).FirstOrDefaultAsync(ct);
        if (finnes is not null) return new(hva, finnes.Status == Strukturkanter.Avvist ? "avvist" : "finnes", finnes.Id, null);
        var u = await identitet.RegistrerAktortypeAsync(StatenId, Rettssubjekt, null, new Utsagnsgrunnlag
        {
            Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = tvisteloven, HjemmelEid = TvistelovenPartsevneEid,
            Kunnskapsstatus = Strukturkanter.Fortolkning, Tolkningsargumenter = ["ordlyd", "kontekst_system"],
            Godkjenning = Strukturkanter.ManueltForeslatt, Begrunnelse = StatenAktortypeBegrunnelse,
        }, Oppdagelseskilde, bruker, ct);
        return new(hva, "opprettet", u?.Id, null);
    }

    private async Task<(List<NorgeStatenKant>, List<string>)> DepartementskanterAsync(string bruker, CancellationToken ct)
    {
        var svar = new List<NorgeStatenKant>();
        var hull = new List<string>();
        var medlemskap = new KlassemedlemskapTjeneste(db, kanttjeneste);
        var klasse = await medlemskap.KlasseForRegelAsync(Klasseregler.OrganiseringsvedtakDepartement, ct);
        if (klasse is null)
        {
            hull.Add("Klassen «departement» (#442) finnes ikke. Kjør POST /api/klasser/fylkeskommune-departement?skriv=true først. Ingen kanter.");
            return (svar, hull);
        }
        var utledet = await medlemskap.UtledAsync(klasse.Id, ct: ct);
        foreach (var h in utledet?.Hull ?? []) hull.Add($"Hull i klassen departement: {h}");
        foreach (var m in utledet?.Medlemmer ?? [])
        {
            var premiss = await db.Strukturkanter.FirstAsync(k => k.Id == m.PremissKantId, ct);
            var hjemmel = premiss.HjemmelEid is not null
                ? $"{await db.Rettskilder.Where(r => r.Id == premiss.HjemmelRettskildeId).Select(r => r.Kortnavn ?? r.Tittel).FirstOrDefaultAsync(ct)} ({premiss.HjemmelEid})"
                : premiss.KildeUtenforKorpusTekst ?? "";
            var tidligere = await kanttjeneste.GjeldendeInklAvviste
                .Where(k => k.Kategori == Strukturkanter.Organtilhorighet && k.Typekode == "har_organ" && k.FraVirksomhetId == m.VirksomhetId
                            && k.TilVirksomhetId == StatenId)
                .OrderBy(k => k.Status == Strukturkanter.Avvist ? 1 : 0).FirstOrDefaultAsync(ct);
            if (tidligere is not null)
            {
                svar.Add(new(m.Navn, "staten", hjemmel, tidligere.Kunnskapsstatus ?? "", tidligere.Status == Strukturkanter.Avvist ? "avvist" : "finnes",
                    tidligere.Id, m.PremissKantId));
                continue;
            }
            var o = await kanttjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Organtilhorighet, "har_organ",
                Kantnode.Virksomhet(m.VirksomhetId), Kantnode.Virksomhet(StatenId),
                HjemmelRettskildeId: premiss.HjemmelRettskildeId, HjemmelEid: premiss.HjemmelEid,
                KildeUtenforKorpusTekst: premiss.KildeUtenforKorpusTekst, KildeUtenforKorpusLenke: premiss.KildeUtenforKorpusLenke,
                KildeUtenforKorpusType: premiss.KildeUtenforKorpusType, KildeUtenforKorpusDokumentasjon: premiss.KildeUtenforKorpusDokumentasjon,
                Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: HarOrganOppdagelseskilde,
                Kunnskapsstatus: Strukturkanter.Avledet, Premisser: [m.PremissKantId],
                Begrunnelse: $"{m.Navn} er medlem av klassen «departement» ({m.Premissart}, {m.Kunnskapsstatus}), og et departement er et organ for "
                             + "staten (klassens definisjon, beslutningsnotatet modell B, Johann 2026-11-05). Hjemmelen er premissets. #463.",
                Kommentar: "Departementene er organer for staten (#463)."), bruker, ct);
            svar.Add(new(m.Navn, "staten", hjemmel, Strukturkanter.Avledet, o.VarNy ? "ny" : "finnes", o.Kant.Id, m.PremissKantId));
        }
        return (svar, hull);
    }

    private static ProveniensEntitet Proveniens(string type, Guid id, string handling, string bruker, object referanser)
    {
        var p = ProveniensHjelper.NyRad(type, id, virksomhetId: null, handling, bruker);
        p.KildeReferanserJson = JsonSerializer.Serialize(new { issue = 463, beslutning = "Johann 2026-11-05 (modell B)", detaljer = referanser });
        return p;
    }
}
