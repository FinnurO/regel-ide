using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #442] Én klasse i <see cref="KlasseoppsettRapport"/>. <see cref="Utfall"/>: <c>opprettet</c> | <c>finnes</c> (en gjeldende klasse
/// har alt regelen) | <c>konflikt</c> (et fast nodebegrep har alt termen; det gjettes ikke at det er samme klasse, ingenting skrives).</summary>
public sealed record KlasseoppsettKlasse(string Term, string Regel, string Utfall, Guid? KlasseId, string? Merknad);

/// <summary>[Ny, issue #442] Én territoriekant fylkeskommune → fylke. <see cref="Utfall"/>: <c>ny</c> | <c>finnes</c> | <c>avvist</c> (et menneske har
/// avvist det samme forslaget; det foreslås ikke på nytt).</summary>
public sealed record KlasseoppsettTerritoriekant(string Fylkeskode, string Fylke, Guid VirksomhetId, string Fylkeskommune, string Utfall, Guid? KantId);

/// <summary>[Ny, issue #442] Én medlemskant departement → klassen. <see cref="Premiss"/>: <c>navnevedtak</c> (hjemmel i korpus) | <c>kgl_res</c>
/// (kilde utenfor korpus). <see cref="Utfall"/> som for territoriekantene.</summary>
public sealed record KlasseoppsettMedlemskant(Guid VirksomhetId, string Departement, string Premiss, string Kilde, string Utfall, Guid? KantId);

/// <summary>[Ny, issue #442] Arkiveringen av den lovspesifikke klassen «kommunene». <see cref="Utfall"/>: <c>arkivert</c> | <c>alt_arkivert</c> |
/// <c>ikke_funnet</c> | <c>har_medlemmer</c> (nektet: klassen har medlemmer, og da er den ikke det juristrunden vurderte).</summary>
public sealed record KlasseoppsettArkivering(string Utfall, Guid? KlasseId, int Tagger, int Kanter, string? Begrunnelse);

/// <summary>[Ny, issue #442] Svaret på <c>POST /api/klasser/fylkeskommune-departement[?skriv=true]</c>. Tallene er de skrivingen gir, også i
/// tørrkjøringen (den gjør det samme i en transaksjon som rulles tilbake). <see cref="MedlemmerFylkeskommune"/> og <see cref="MedlemmerDepartement"/>
/// er medlemsreglenes svar etter skrivingen (eller etter tørrkjøringen, før tilbakerullingen).</summary>
public sealed record KlasseoppsettRapport(
    bool Skrevet, IReadOnlyList<KlasseoppsettKlasse> Klasser,
    IReadOnlyList<KlasseoppsettTerritoriekant> Territoriekanter, IReadOnlyList<string> Uparet,
    IReadOnlyList<KlasseoppsettMedlemskant> Medlemskanter, IReadOnlyList<string> DepartementerUtenPremiss,
    KlasseoppsettArkivering Kommunene,
    int MedlemmerFylkeskommune, IReadOnlyList<string> HullFylkeskommune,
    int MedlemmerDepartement, IReadOnlyList<string> HullDepartement);

/// <summary>
/// [Ny, issue #442, Johanns avgjørelse 2026-11-05 (beslutningsnotatet «Norge, staten og forvaltningsnivåene», modell B), juristrunden N8, N9, N12,
/// N18] Skriver klassene «fylkeskommune» og «departement», territoriekantene fylkeskommune → fylke og medlemskantene for de sju departementene
/// uten oppretterkant, og arkiverer klassen «kommunene». Alt nytt er <b>forslag</b> (aldri godkjent), og operasjonen er idempotent: en ny kjøring
/// skriver ingenting, og et avvist forslag foreslås ikke på nytt. Tørrkjøringen er standard.
/// <list type="bullet">
/// <item><b>Klassen «fylkeskommune»</b>: hjemmel kommuneloven § 2-1 første ledd (tekstfunn), medlemsregel <see cref="Klasseregler.OmraderegisterFylkeskommune"/>.
/// Grunnloven § 49 andre ledd og § 72 tredje ledd bokstav b står i begrunnelsen som forankring, ikke som hjemmel (notatet avsnitt 2).</item>
/// <item><b>Klassen «departement»</b>: ingen hjemmel (ingen lovdefinisjon), medlemsregel <see cref="Klasseregler.OrganiseringsvedtakDepartement"/>.
/// Grl. § 12 andre ledd og § 28 er forankring.</item>
/// <item><b>Territoriekantene</b> (N9): hjemmel kommuneloven § 3-1 første ledd, tekstfunn, paret med navneregelen i fjerde ledd: fylkeskommunen for
/// fylket «X» er aktøren med forvaltningsnivå fylkeskommune og navnet «X fylkeskommune» (registernavn eller navneform, uten forskjell på store og
/// små bokstaver, siden Brreg skriver «AGDER FYLKESKOMMUNE»). Ikke nøyaktig ett treff → ingen kant, fylket listes (#345-mønsteret). Registeret er
/// ikke kilden for kanten. Oslo får ingen kant: loven unntar det, og ordet kontrolleres i lovteksten før noe skrives.</item>
/// <item><b>Medlemskantene</b> (N12): for et levende departement uten livsløpets R oppretter. Har det en navneform hjemlet i navnevedtaket
/// (<see cref="DepartementNavneformHjemmelTjeneste"/>), blir vedtaket hjemmelen; ellers kgl.res. utenfor korpus fra
/// <c>Seed/departementer-kgl-res-442.json</c>, slått opp på organisasjonsnummeret. Begge er fortolkning: vedtaket forutsetter departementet,
/// men oppretter det ikke.</item>
/// <item><b>«kommunene»</b> (N18): ordet står i tittelen på en annen lov i reindriftsloven § 2 andre ledd, og klassen har 0 medlemmer. Det fantes
/// ingen arkiveringsvei for en klasse; den følger mønsteret fra #310 (<c>entitetsstatus = 'arkivert'</c>, aldri DELETE) og avvisningen av en
/// navneform (#385: avvist med begrunnelse). Taggen arkiveres som en slettet tagg (<see cref="TekstTaggTjeneste.SlettAsync"/>), og M-kanten
/// settes foreldet (en godkjent kant kan ikke avvises). De gamle verdiene står i proveniensen.</item>
/// </list>
/// </summary>
public sealed class KlasseoppsettTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste)
{
    public const string FylkeskommuneTerm = "fylkeskommune";
    public const string DepartementTerm = "departement";
    public const string NavneregelOppdagelseskilde = "monster:fylkeskommune-navneregel";
    public const string NavnevedtakOppdagelseskilde = "monster:klasse-departement-navnevedtak";
    public const string Ressursnavn = "RegelIde.Data.Seed.departementer-kgl-res-442.json";

    /// <summary>Reindriftsloven § 2 andre ledd, der «kommunene» står i tittelen på lov 21. desember 1984 nr. 101 (verksnivå-eId).</summary>
    public const string KommuneneEid = "https://lovdata.no/eli/lov/2007/06/15/40/nor/§2/ledd-2";

    public const string FylkeskommuneDefinisjon =
        "Fylkeskommunene Norge er inndelt i (kommuneloven § 2-1 første ledd), hver et eget rettssubjekt (annet ledd). Hvert fylke unntatt Oslo "
        + "utgjør en fylkeskommune med samme navn som fylket (§ 3-1 første og fjerde ledd). Medlemmene utledes av territoriekantene.";

    public const string FylkeskommuneBegrunnelse =
        "Hjemmel: kommuneloven § 2-1 første ledd («Norge er inndelt i kommuner og fylkeskommuner med en egen folkevalgt ledelse»). Forankring, "
        + "ikke hjemmel: Grunnloven § 49 andre ledd (lokalt folkevalgt nivå, i entall; nevner ikke fylkeskommunen, så forankringen er svakere enn "
        + "for kommunen) og § 72 tredje ledd bokstav b (forutsetter fylkestinget). Medlemsregelen har kilde i § 3-1 første ledd andre punktum og "
        + "fjerde ledd. Johanns avgjørelse 2026-11-05 (#442), juristrunden N8.";

    public const string DepartementDefinisjon =
        "Departementene: organer for staten som Kongen i statsråd oppretter og gir navn ved organiseringsvedtak (kgl.res.). Ingen lov definerer "
        + "«departement». Medlemmene utledes av organiseringsvedtakene.";

    public const string DepartementBegrunnelse =
        "Ingen lovdefinisjon: Grunnloven bruker ikke ordet, og ingen lov definerer det (juristrunden N12). Klassen har derfor ingen hjemmel, men "
        + "en medlemsregel med organiseringsvedtakene som kilde. Forankring: Grunnloven § 12 andre ledd («Kongen fordeler forretningene blant "
        + "statsrådets medlemmer …») og § 28. Statsministerens kontor er ikke medlem etter den alminnelige regelen (statsansatteloven § 2 tredje "
        + "ledd); karanteneloven § 1 er sak 3. Johanns avgjørelse 2026-11-05 (#442).";

    public const string KommuneneBegrunnelse =
        "Arkivert (#442, Johanns avgjørelse 2026-11-05, juristrunden N18 «holder»): «kommunene» i reindriftsloven § 2 andre ledd er en del av "
        + "tittelen på lov 21. desember 1984 nr. 101 om reindrift i kommunene Meldal, Midtre Gauldal, Oppdal, Rennebu, Rindal, Sunndal og Surnadal, "
        + "altså en henvisning til en annen lov. Det er ikke en klasse reindriftsloven oppretter, og den har verken hjemmel eller medlemsregel. "
        + "0 medlemmer. Skal de sju kommunene bli et område, hører hjemmelen hjemme i Trollheimenloven § 1, med gyldighet (Meldal er i dag del "
        + "av Orkland).";

    public async Task<KlasseoppsettRapport> KjorAsync(bool skriv, string bruker, CancellationToken ct = default)
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

    private async Task<KlasseoppsettRapport> GjorAsync(string bruker, CancellationToken ct)
    {
        var (kommunelovId, _) = await KontrollerLovteksten(ct);

        var klasser = new List<KlasseoppsettKlasse>
        {
            await KlasseAsync(FylkeskommuneTerm, Klasseregler.OmraderegisterFylkeskommune, FylkeskommuneDefinisjon, Klasseregler.KommunelovenInndelingEid,
                Strukturkanter.Tekstfunn, FylkeskommuneBegrunnelse, null, bruker, ct),
            await KlasseAsync(DepartementTerm, Klasseregler.OrganiseringsvedtakDepartement, DepartementDefinisjon, null,
                Strukturkanter.Fortolkning, DepartementBegrunnelse, ["kontekst_system"], bruker, ct),
        };

        var (territoriekanter, uparet) = await TerritoriekanterAsync(kommunelovId, bruker, ct);

        var departementKlasse = klasser[1].KlasseId;
        var (medlemskanter, utenPremiss) = departementKlasse is { } dk ? await MedlemskanterAsync(dk, bruker, ct) : ([], []);

        var kommunene = await ArkiverKommuneneAsync(bruker, ct);

        await db.SaveChangesAsync(ct);
        var medlemskap = new KlassemedlemskapTjeneste(db, kanttjeneste);
        var fk = klasser[0].KlasseId is { } fid ? await medlemskap.UtledAsync(fid, ct: ct) : null;
        var dep = departementKlasse is { } did ? await medlemskap.UtledAsync(did, ct: ct) : null;
        return new KlasseoppsettRapport(false, klasser, territoriekanter, uparet, medlemskanter, utenPremiss, kommunene,
            fk?.Medlemmer.Count ?? 0, fk?.Hull ?? [], dep?.Medlemmer.Count ?? 0, dep?.Hull ?? []);
    }

    /// <summary>Kommuneloven skal stå i korpuset med ordlyden avgjørelsen bygger på. Ellers skrives ingenting (CLAUDE.md §8).</summary>
    private async Task<(Guid RettskildeId, string Tekst)> KontrollerLovteksten(CancellationToken ct)
    {
        async Task<(Guid, string)> Node(string eid, string maInneholde)
        {
            var n = await db.RettskildeNoder.Where(x => x.Eid == eid && x.Entitetsstatus == "gjeldende")
                .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende" && r.Status == "Gjeldende"), x => x.RettskildeId, r => r.Id,
                    (x, r) => new { r.Id, x.Tekst }).ToListAsync(ct);
            if (n.Count != 1 || n[0].Tekst is null || !n[0].Tekst!.Contains(maInneholde, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Fant ikke nøyaktig én gjeldende node {eid} med «{maInneholde}» ({n.Count} treff). Ingenting skrives.");
            }
            return (n[0].Id, n[0].Tekst!);
        }
        await Node(Klasseregler.KommunelovenInndelingEid, "inndelt i kommuner og fylkeskommuner");
        await Node(Klasseregler.KommunelovenNavneregelEid, "samme navn som fylket");
        return await Node(Klasseregler.KommunelovenFylkeskommuneEid, $"Hvert fylke unntatt {Klasseregler.UnntattFylke} utgjør en fylkeskommune");
    }

    private async Task<KlasseoppsettKlasse> KlasseAsync(string term, string regel, string definisjon, string? hjemmelEid, string kunnskapsstatus,
        string begrunnelse, List<string>? argumenter, string bruker, CancellationToken ct)
    {
        var finnes = await db.Begreper.FirstOrDefaultAsync(b => b.Medlemskapsregel == regel && b.Entitetsstatus == "gjeldende", ct);
        if (finnes is not null) return new(term, regel, "finnes", finnes.Id, finnes.Term == term ? null : $"Klassen med regelen heter «{finnes.Term}».");
        var samme = await db.Begreper.Where(b => b.Entitetsstatus == "gjeldende" && b.LovkildeId == null && b.Omradetype == null
                                                 && b.Term.ToLower() == term && Nodetyper.MedGruppefunksjon.Contains(b.Begrepskategori!))
            .Select(b => new { b.Id, b.Begrepskategori }).FirstOrDefaultAsync(ct);
        if (samme is not null)
        {
            return new(term, regel, "konflikt", samme.Id,
                $"Det finnes alt et fast nodebegrep «{term}» ({samme.Begrepskategori}). Det gjettes ikke at det er samme klasse; et menneske avgjør.");
        }
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            Term = term,
            Definisjon = definisjon,
            Begrepskategori = Nodetyper.Klasse,
            LovkildeId = null, // fast, nasjonalt begrep (#298), som klassen «kommune»
            LovreferanseEid = hjemmelEid,
            Medlemskapsregel = regel,
            Kunnskapsstatus = kunnskapsstatus,
            Begrunnelse = begrunnelse,
            Tolkningsargumenter = argumenter,
            Godkjenning = Strukturkanter.ManueltForeslatt,
            OppdagelsesKilde = "manuell",
            Status = "publisert",
            OpprettetAv = bruker,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        var p = ProveniensHjelper.NyRad("begrep", b.Id, virksomhetId: null, "foreslatt", bruker);
        p.KildeReferanserJson = JsonSerializer.Serialize(new { issue = 442, beslutning = "Johann 2026-11-05", medlemskapsregel = regel, hjemmel = hjemmelEid });
        db.Proveniens.Add(p);
        await db.SaveChangesAsync(ct);
        return new(term, regel, "opprettet", b.Id, null);
    }

    private async Task<(List<KlasseoppsettTerritoriekant>, List<string>)> TerritoriekanterAsync(Guid kommunelovId, string bruker, CancellationToken ct)
    {
        var fylker = await db.Begreper
            .Where(b => b.Begrepskategori == Nodetyper.Omrade && b.Omradetype == Omradetyper.Fylke && b.Omradekode != null
                        && b.Entitetsstatus == "gjeldende" && b.GyldigTil == null && b.Term != Klasseregler.UnntattFylke)
            .OrderBy(b => b.Omradekode).Select(b => new { b.Id, b.Term, Kode = b.Omradekode! }).ToListAsync(ct);
        var kandidater = await db.Virksomheter
            .Where(v => v.Forvaltningsniva == "fylkeskommune" && v.Livsstatus == null && v.ErstattetAvId == null)
            .Select(v => new { v.Id, v.Navn }).ToListAsync(ct);
        var kandidatIder = kandidater.Select(v => v.Id).ToList();
        var navneformer = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                        && kandidatIder.Contains(b.VirksomhetReferanseId.Value))
            .Select(b => new { Id = b.VirksomhetReferanseId!.Value, b.Term }).ToListAsync(ct);
        var navn = kandidater.Select(v => (v.Id, Navn: v.Navn)).Concat(navneformer.Select(n => (n.Id, Navn: n.Term))).ToList();
        var visning = await new VirksomhetVisningsnavnTjeneste(db).AlleAsync(ct);

        var svar = new List<KlasseoppsettTerritoriekant>();
        var uparet = new List<string>();
        foreach (var f in fylker)
        {
            var forventet = $"{f.Term} fylkeskommune";
            var treff = navn.Where(n => string.Equals(n.Navn.Trim(), forventet, StringComparison.OrdinalIgnoreCase)).Select(n => n.Id).Distinct().ToList();
            if (treff.Count != 1)
            {
                uparet.Add($"{f.Kode} {f.Term}: {treff.Count} treff på «{forventet}» blant aktørene med forvaltningsnivå fylkeskommune. Ingen kant.");
                continue;
            }
            var vid = treff[0];
            var vNavn = visning.GetValueOrDefault(vid) ?? kandidater.First(v => v.Id == vid).Navn;
            var tidligere = await kanttjeneste.GjeldendeInklAvviste.Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade"
                    && k.FraVirksomhetId == vid && k.TilBegrepId == f.Id && k.HjemmelEid == Klasseregler.KommunelovenFylkeskommuneEid)
                .OrderBy(k => k.Status == Strukturkanter.Avvist ? 1 : 0).FirstOrDefaultAsync(ct);
            if (tidligere is not null)
            {
                svar.Add(new(f.Kode, f.Term, vid, vNavn, tidligere.Status == Strukturkanter.Avvist ? "avvist" : "finnes", tidligere.Id));
                continue;
            }
            var ny = await kanttjeneste.OpprettAsync(new NyStrukturkant(
                Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(vid), Kantnode.Begrep(f.Id),
                HjemmelRettskildeId: kommunelovId, HjemmelEid: Klasseregler.KommunelovenFylkeskommuneEid,
                Kommentar: $"Navneregel: fylkeskommunen for fylket «{f.Term}» er «{forventet}» (kommuneloven § 3-1 fjerde ledd: «Fylkeskommunen skal ha "
                           + "samme navn som fylket»), slått opp blant aktørene med forvaltningsnivå fylkeskommune. Hjemmel § 3-1 første ledd andre punktum: "
                           + "«Hvert fylke unntatt Oslo utgjør en fylkeskommune.» (#442, juristrunden N9)",
                Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: NavneregelOppdagelseskilde,
                Kunnskapsstatus: Strukturkanter.Tekstfunn), bruker, ct);
            svar.Add(new(f.Kode, f.Term, vid, vNavn, ny.VarNy ? "ny" : "finnes", ny.Kant.Id));
        }
        return (svar, uparet);
    }

    private async Task<(List<KlasseoppsettMedlemskant>, List<string>)> MedlemskanterAsync(Guid klasseId, string bruker, CancellationToken ct)
    {
        var levende = await db.Virksomheter.Where(v => v.Livsstatus == null && v.ErstattetAvId == null).Select(v => v.Id).ToListAsync(ct);
        var levendeSett = levende.ToHashSet();
        var medOppretter = (await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
                .Where(k => k.Kategori == Strukturkanter.Relasjon && k.Typekode == "oppretter" && k.TilVirksomhetId != null
                            && k.OppdagelsesKilde.StartsWith(Klasseregler.LivslopPrefiks)).ToListAsync(ct), ct))
            .Select(k => k.TilVirksomhetId!.Value).ToHashSet();
        var navnevedtak = (await db.Begreper
                .Where(b => b.Begrepskategori == "virksomhet" && b.Navneformgrunn == "gjeldende" && b.Entitetsstatus == "gjeldende"
                            && b.HjemmelArt == "navnevedtak" && b.OppdagelsesKilde == DepartementNavneformHjemmelTjeneste.OppdagelsesKilde
                            && b.VirksomhetReferanseId != null && b.LovkildeId != null && b.LovreferanseEid != null)
                .ToListAsync(ct))
            .Where(b => levendeSett.Contains(b.VirksomhetReferanseId!.Value) && !medOppretter.Contains(b.VirksomhetReferanseId!.Value))
            .GroupBy(b => b.VirksomhetReferanseId!.Value).ToList();
        var visning = await new VirksomhetVisningsnavnTjeneste(db).AlleAsync(ct);
        var svar = new List<KlasseoppsettMedlemskant>();
        var utenPremiss = new List<string>();

        foreach (var g in navnevedtak)
        {
            if (g.Count() != 1)
            {
                utenPremiss.Add($"{visning.GetValueOrDefault(g.Key) ?? g.Key.ToString()}: {g.Count()} navneformer hjemlet i navnevedtaket. Ingen valgt.");
                continue;
            }
            var nf = g.Single();
            var tittel = await db.Rettskilder.Where(r => r.Id == nf.LovkildeId).Select(r => r.Kortnavn ?? r.Tittel).FirstAsync(ct);
            var sitat = await db.RettskildeNoder.Where(n => n.RettskildeId == nf.LovkildeId && n.Eid == nf.LovreferanseEid && n.Entitetsstatus == "gjeldende")
                .Select(n => n.Tekst).FirstOrDefaultAsync(ct);
            var navn = visning.GetValueOrDefault(g.Key) ?? nf.Term;
            svar.Add(await MedlemskantAsync(klasseId, g.Key, navn, "navnevedtak", $"{tittel} ({nf.LovreferanseEid})",
                new NyStrukturkant(Strukturkanter.Medlemskap, "medlem_av", Kantnode.Virksomhet(g.Key), Kantnode.Begrep(klasseId),
                    HjemmelRettskildeId: nf.LovkildeId, HjemmelEid: nf.LovreferanseEid,
                    Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: NavnevedtakOppdagelseskilde,
                    Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"],
                    Begrunnelse: $"Navnevedtaket gir {navn} navnet{(sitat is null ? "" : $": «{Avkort(sitat)}»")}. Vedtaket forutsetter at enheten er et "
                                 + "departement, men oppretter det ikke, og det finnes ingen R oppretter i livsløpet (juristrunden N12, #442).",
                    Kommentar: "Premiss for klassen «departement» (medlemsregelen organiseringsvedtak_departement)."), bruker, ct));
        }

        var harNavnevedtak = navnevedtak.Select(g => g.Key).ToHashSet();
        foreach (var d in LesKglRes().Departementer)
        {
            var v = await db.Virksomheter.Where(x => x.Organisasjonsnummer == d.Organisasjonsnummer && x.Livsstatus == null && x.ErstattetAvId == null)
                .Select(x => new { x.Id, x.Navn }).FirstOrDefaultAsync(ct);
            if (v is null)
            {
                utenPremiss.Add($"{d.Navn} ({d.Organisasjonsnummer}): ingen levende aktør med organisasjonsnummeret. Ingen kant.");
                continue;
            }
            if (medOppretter.Contains(v.Id) || harNavnevedtak.Contains(v.Id)) continue; // premisset står alt i korpus
            var navn = visning.GetValueOrDefault(v.Id) ?? v.Navn;
            svar.Add(await MedlemskantAsync(klasseId, v.Id, navn, "kgl_res", $"Kgl.res. {d.Dato} ({d.Lenke})",
                new NyStrukturkant(Strukturkanter.Medlemskap, "medlem_av", Kantnode.Virksomhet(v.Id), Kantnode.Begrep(klasseId),
                    KildeUtenforKorpusTekst: $"Kgl.res. {d.Dato}, gjengitt på regjeringen.no: «{d.Sitat}»", KildeUtenforKorpusLenke: d.Lenke,
                    KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: Strukturkanter.Sekundaer,
                    Status: Strukturkanter.ManueltForeslatt, OppdagelsesKilde: "manuell",
                    Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"],
                    Begrunnelse: d.Begrunnelse + " Navnet er eldre enn det eldste organiseringsvedtaket i korpuset (#442).",
                    Kommentar: "Premiss for klassen «departement» (medlemsregelen organiseringsvedtak_departement)."), bruker, ct));
        }
        return (svar, utenPremiss);
    }

    private async Task<KlasseoppsettMedlemskant> MedlemskantAsync(Guid klasseId, Guid vid, string navn, string premiss, string kilde, NyStrukturkant ny,
        string bruker, CancellationToken ct)
    {
        // Avvist eller ikke: en M-kant fra aktøren til klassen uten avgrensning er samme premiss. Et avvist forslag foreslås ikke på nytt.
        var tidligere = await kanttjeneste.GjeldendeInklAvviste
            .Where(k => k.Kategori == Strukturkanter.Medlemskap && k.FraVirksomhetId == vid && k.TilBegrepId == klasseId)
            .OrderBy(k => k.Status == Strukturkanter.Avvist ? 1 : 0).FirstOrDefaultAsync(ct);
        if (tidligere is not null) return new(vid, navn, premiss, kilde, tidligere.Status == Strukturkanter.Avvist ? "avvist" : "finnes", tidligere.Id);
        var opprettet = await kanttjeneste.OpprettAsync(ny, bruker, ct);
        return new(vid, navn, premiss, kilde, opprettet.VarNy ? "ny" : "finnes", opprettet.Kant.Id);
    }

    private async Task<KlasseoppsettArkivering> ArkiverKommuneneAsync(string bruker, CancellationToken ct)
    {
        var kandidater = await db.Begreper.Where(b => b.Term == "kommunene" && b.Begrepskategori == Nodetyper.Klasse && b.LovreferanseEid == KommuneneEid)
            .ToListAsync(ct);
        var klasse = kandidater.SingleOrDefault(b => b.Entitetsstatus == "gjeldende");
        if (klasse is null)
        {
            var arkivert = kandidater.FirstOrDefault(b => b.Entitetsstatus == "arkivert");
            return new(arkivert is null ? "ikke_funnet" : "alt_arkivert", arkivert?.Id, 0, 0, arkivert?.AvvisningBegrunnelse);
        }
        var kanter = await db.Strukturkanter.Where(k => k.FraBegrepId == klasse.Id || k.TilBegrepId == klasse.Id).ToListAsync(ct);
        if (kanter.Any(k => k.Kategori == Strukturkanter.Medlemskap && k.TilBegrepId == klasse.Id))
        {
            return new("har_medlemmer", klasse.Id, 0, kanter.Count,
                "Klassen har medlemmer. Juristrunden vurderte en klasse med 0 medlemmer; ingenting arkiveres før et menneske har sett på det.");
        }
        var tagger = await db.TekstTagger.Where(t => t.RefId == klasse.Id && t.Entitetsstatus == "gjeldende").ToListAsync(ct);
        var na = DateTimeOffset.UtcNow;
        foreach (var t in tagger)
        {
            t.Entitetsstatus = "arkivert";
            var pt = ProveniensHjelper.NyRad("tekst_tagg", t.Id, t.VirksomhetId, "arkivert", bruker);
            pt.KildeReferanserJson = JsonSerializer.Serialize(new { issue = 442, grunn = "Klassen «kommunene» er arkivert.", klasse = klasse.Id });
            db.Proveniens.Add(pt);
        }
        foreach (var k in kanter)
        {
            var pk = ProveniensHjelper.NyRad(StrukturkantTjeneste.ProveniensType, k.Id, virksomhetId: null, "arkivert", bruker);
            pk.KildeReferanserJson = JsonSerializer.Serialize(new
            {
                issue = 442, felt = "entitetsstatus", forEntitetsstatus = k.Entitetsstatus, forStatus = k.Status,
                grunn = "Klassen «kommunene» er arkivert. En godkjent kant kan ikke avvises; den settes foreldet uten erstatning.",
            });
            db.Proveniens.Add(pk);
            k.Entitetsstatus = Strukturkanter.Foreldet;
            k.SistEndretAv = bruker;
            k.SistEndretTidspunkt = na;
        }
        var p = ProveniensHjelper.NyRad("begrep", klasse.Id, klasse.VirksomhetId, "arkivert", bruker);
        p.KildeReferanserJson = JsonSerializer.Serialize(new
        {
            issue = 442, juristkort = "N18", forEntitetsstatus = klasse.Entitetsstatus, forStatus = klasse.Status, forGodkjenning = klasse.Godkjenning,
            tagger = tagger.Select(t => t.Id), kanter = kanter.Select(k => k.Id), begrunnelse = KommuneneBegrunnelse,
        });
        db.Proveniens.Add(p);
        klasse.Entitetsstatus = "arkivert";
        klasse.Status = "arkivert";
        klasse.Godkjenning = Strukturkanter.Avvist;
        klasse.AvvisningBegrunnelse = KommuneneBegrunnelse;
        klasse.SistEndretAv = bruker;
        klasse.SistEndretTidspunkt = na;
        await db.SaveChangesAsync(ct);
        return new("arkivert", klasse.Id, tagger.Count, kanter.Count, KommuneneBegrunnelse);
    }

    private static string Avkort(string s) => s.Length <= 200 ? s : s[..200] + " …";

    internal static KglResFil LesKglRes()
    {
        using var strom = typeof(KlasseoppsettTjeneste).Assembly.GetManifestResourceStream(Ressursnavn)
            ?? throw new InvalidOperationException($"Fant ikke den innebygde ressursen {Ressursnavn}.");
        return JsonSerializer.Deserialize<KglResFil>(strom, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"{Ressursnavn} er tom.");
    }

    internal sealed record KglResFil([property: JsonPropertyName("departementer")] List<KglRes> Departementer);

    internal sealed record KglRes(string Organisasjonsnummer, string Navn, string Dato, string Lenke, string Sitat, string Begrunnelse);
}
