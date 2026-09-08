using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="SamiskSprakforvaltningSeed"/> mot ekte embedded Postgres — Johanns eget «gruppe av
/// gruppe»-eksempel (issue #164): forvaltningsområdet for samiske språk med de tre kommunekategoriene
/// som MEDLEMSGRUPPER og de navngitte kommunene som konkrete medlemmer.
/// <para>
/// Seeden slår opp sameloven og forskriften på EKSAKT ELI og oppfinner ingenting når de mangler. Det
/// gir to helt ulike ting å teste, og de kan ikke dele database: «hopper over og rapporterer» krever at
/// rettskildene IKKE finnes, mens idempotenstesten krever at de FINNES. Den delte
/// DataTestCollection-databasen gir ingen garantert rekkefølge mellom testmetodene, så
/// hoppe-over-testen kjører mot en helt fersk, tom database (<see cref="NyTomDatabaseAsync"/>) i
/// stedet for å hvile på at den tilfeldigvis kjører først.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class SamiskSprakforvaltningSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public SamiskSprakforvaltningSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private const string SamelovEli = "https://lovdata.no/eli/lov/1987/06/12/56/nor";
    private const string ForskriftEli = "https://lovdata.no/eli/forskrift/2005/06/17/657/nor";
    private const string DefinisjonNodeEid = SamelovEli + "/§3-1/ledd-1/punkt-1";
    private const string MedlemNodeEid = ForskriftEli + "/§1/ledd-1";

    private const string Forvaltningsomradet = "forvaltningsområdet for samiske språk";
    private const string Sprakutvikling = "språkutviklingskommuner";
    private const string Sprakvitalisering = "språkvitaliseringskommuner";
    private const string Sprakstimulering = "språkstimuleringskommuner";

    /// <summary>Kommunal- og distriktsdepartementet — samme navn og orgnr som
    /// <see cref="DepartementSeed"/> bruker. Må finnes som <see cref="Virksomhet"/> fordi en tagg
    /// alltid EIES av en virksomhet, og seeden løser eieren via rettskildens
    /// <see cref="RettskildeEntitet.AnsvarligDepartement"/>.</summary>
    private const string DepartementNavn = "Kommunal- og distriktsdepartementet";
    private const string DepartementOrgnr = "972417858";
    private const string KarasjokOrgnr = "963376030";

    /// <summary>Sameloven § 3-1 første ledd punkt 1 — noden som DEFINERER gruppekonseptene uten å
    /// navngi én kommune. Termene står med små bokstaver, slik seeden slår dem opp som hele ord.</summary>
    private const string DefinisjonTekst =
        "Med forvaltningsområdet for samiske språk menes de kommunene som ved forskrift er inndelt i "
        + "kategoriene språkutviklingskommuner, språkvitaliseringskommuner og språkstimuleringskommuner.";

    /// <summary>Forskriften § 1 første ledd — noden som NAVNGIR medlemmene, ordrett.</summary>
    private const string MedlemTekst =
        "Forvaltningsområdet for samiske språk består av alle språkutviklingskommuner, "
        + "språkvitaliseringskommuner og språkstimuleringskommuner. Karasjok, Kautokeino, Nesseby og "
        + "Tana er språkutviklingskommuner. Porsanger, Kåfjord, Lavangen, Tjeldsund, Hattfjelldal, "
        + "Hamarøy, Røyrvik, Røros og Snåsa er språkvitaliseringskommuner. Saltdal er "
        + "språkstimuleringskommune.";

    /// <summary>
    /// Akseptansekriterium: i et miljø der korpuset ikke er importert skal seeden gjøre INGENTING og
    /// si HVORFOR — ikke oppfinne en rettskilde, en node eller en virksomhet for å få eksempelet til å
    /// se komplett ut.
    /// </summary>
    [Fact]
    public async Task Tomt_korpus_gir_ingen_rader_og_rapporterer_hva_som_mangler()
    {
        await using var db = new RegelIdeDbContext(NyOptions(await NyTomDatabaseAsync()));

        var resultat = await KjorSeedAsync(db);

        Assert.Equal(0, resultat.AntallGruppebegrep);
        Assert.Equal(0, resultat.AntallGruppemedlemskap);
        Assert.Equal(0, resultat.AntallMyndighetstildelinger);
        Assert.Equal(0, resultat.AntallNavneformer);
        Assert.Equal(0, resultat.AntallTagger);
        Assert.NotEmpty(resultat.HoppetOver);
        Assert.Contains(resultat.HoppetOver, h => h.Contains(SamelovEli));
        Assert.Contains(resultat.HoppetOver, h => h.Contains(ForskriftEli));

        // Ingenting oppfunnet: verken rettskilde, gruppebegrep, medlemskap eller virksomhet.
        Assert.Empty(await db.Rettskilder.ToListAsync());
        Assert.Empty(await db.Begreper.ToListAsync());
        Assert.Empty(await db.GruppeMedlemskap.ToListAsync());
        Assert.Empty(await db.Virksomheter.ToListAsync());
    }

    /// <summary>
    /// Eksplisitt akseptansekriterium i issue #164: to kjøringer gir samme radantall. Testen sørger
    /// først for seedens forutsetninger (rettskilder, noder, departement-eier, Karasjok) og kjører så
    /// seeden to ganger. Den sammenligner tallene FØR og ETTER andre kjøring, ikke absolutte
    /// totalsummer — databasen er delt med resten av testene i denne collection-en.
    /// </summary>
    [Fact]
    public async Task Idempotent_ved_gjentatt_kall()
    {
        await using var db = _fixture.NyDbContext();
        var forutsetninger = await SorgForForutsetningerAsync(db);

        var forste = await KjorSeedAsync(db);
        var etterForste = await TellAsync(db, forutsetninger);

        var andre = await KjorSeedAsync(db);
        var etterAndre = await TellAsync(db, forutsetninger);

        Assert.Equal(etterForste, etterAndre);

        // «Finnes»-tallene er de samme etter en idempotent gjentakelse — se
        // SamiskSprakforvaltningSeedResultat sin kommentar for hvorfor det er nettopp DE og ikke
        // antall NYE rader som rapporteres.
        Assert.Equal(forste.AntallGruppebegrep, andre.AntallGruppebegrep);
        Assert.Equal(forste.AntallGruppemedlemskap, andre.AntallGruppemedlemskap);
        Assert.Equal(forste.AntallMyndighetstildelinger, andre.AntallMyndighetstildelinger);
        Assert.Equal(forste.AntallNavneformer, andre.AntallNavneformer);
        Assert.Equal(forste.AntallTagger, andre.AntallTagger);
        Assert.Equal(forste.HoppetOver, andre.HoppetOver);
    }

    /// <summary>
    /// Selve «gruppe av gruppe»-akseptansekriteriet: de tre kommunekategoriene er MEDLEMSGRUPPER av
    /// forvaltningsområdet — ikke tre løse gruppebegrep ved siden av hverandre, og ikke
    /// myndighetstildelinger (som bare kan peke på en virksomhet, aldri på en annen gruppe).
    /// </summary>
    [Fact]
    public async Task De_tre_kommunekategoriene_blir_medlemsgrupper_av_forvaltningsomradet()
    {
        await using var db = _fixture.NyDbContext();
        var forutsetninger = await SorgForForutsetningerAsync(db);
        await KjorSeedAsync(db);

        var overordnet = await FinnGruppebegrepAsync(db, forutsetninger.SamelovId, Forvaltningsomradet);
        var medlemskap = await new GruppeMedlemskapTjeneste(db).MedlemsgrupperForAsync(overordnet.Id);
        Assert.Equal(3, medlemskap.Count);

        var medlemsIder = medlemskap.Select(m => m.UnderordnetGruppeBegrepId).ToList();
        var medlemsTermer = await db.Begreper
            .Where(b => medlemsIder.Contains(b.Id))
            .Select(b => b.Term)
            .ToListAsync();
        Assert.Contains(Sprakutvikling, medlemsTermer);
        Assert.Contains(Sprakvitalisering, medlemsTermer);
        Assert.Contains(Sprakstimulering, medlemsTermer);

        // Medlemskapene hjemles i FORSKRIFTEN (som navngir dem), gruppebegrepene i LOVEN (som
        // definerer dem) — se seedens klassekommentar.
        Assert.All(medlemskap, m => Assert.Equal(forutsetninger.ForskriftId, m.HjemmelRettskildeId));
        Assert.Equal(forutsetninger.SamelovId, overordnet.LovkildeId);
    }

    /// <summary>
    /// Karasjok får sine plikter etter sameloven INDIREKTE: navngitt i forskriften som medlem av
    /// «språkutviklingskommuner», som selv er medlemsgruppe av forvaltningsområdet. «Karasjok» er
    /// dessuten ikke kommunens offisielle navn, og navneformen skal derfor ha grunn <c>'kortform'</c>.
    /// </summary>
    [Fact]
    public async Task Karasjok_far_kortform_navneform_og_myndighetstildeling_i_sprakutviklingskommuner()
    {
        await using var db = _fixture.NyDbContext();
        var forutsetninger = await SorgForForutsetningerAsync(db);
        await KjorSeedAsync(db);

        var navneform = await db.Begreper.SingleAsync(
            b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == forutsetninger.KarasjokId
                 && b.Term == "Karasjok" && b.Entitetsstatus == "gjeldende");
        Assert.Equal("kortform", navneform.Navneformgrunn);

        var gruppe = await FinnGruppebegrepAsync(db, forutsetninger.SamelovId, Sprakutvikling);
        var tildeling = await db.Myndighetstildelinger.SingleAsync(
            m => m.GruppeBegrepId == gruppe.Id && m.VirksomhetId == forutsetninger.KarasjokId);
        Assert.Equal(forutsetninger.ForskriftId, tildeling.HjemmelRettskildeId);
    }

    /// <summary>
    /// [Ny, tagg-synlig-runden, 2026-09-08] Kjeden Johann forventet har TRE ledd: tagget tekst
    /// «Karasjok» → navneformen «Karasjok kommune» → virksomheten. Da må BEGGE navneformene finnes,
    /// og de må ha ulik grunn — kortformen er den som står i forskriftsteksten og bærer taggen, den
    /// gjeldende er mellomleddet visningen resolver til i stedet for virksomhetens tospråklige
    /// registernavn («Karasjoga gielda / Karasjok kommune»).
    /// <para>
    /// Testen dekker samtidig idempotensen for det NYE tilfellet: to navneformer per kommune skal
    /// fortsatt være to etter en gjentatt kjøring, ikke fire. (<see
    /// cref="Idempotent_ved_gjentatt_kall"/> sammenligner delta mellom to kjøringer og ville derfor
    /// ikke fanget at ANTALLET per kommune var galt fra første kjøring.)
    /// </para>
    /// </summary>
    [Fact]
    public async Task Karasjok_far_bade_kortform_og_gjeldende_navneform_og_taggen_peker_pa_kortformen()
    {
        await using var db = _fixture.NyDbContext();
        var forutsetninger = await SorgForForutsetningerAsync(db);
        await KjorSeedAsync(db);
        await KjorSeedAsync(db); // gjentatt kjøring: fortsatt to navneformer, ikke fire.

        var navneformer = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == forutsetninger.KarasjokId
                        && b.Entitetsstatus == "gjeldende")
            .OrderBy(b => b.Term)
            .ToListAsync();

        Assert.Equal(
            [("Karasjok", "kortform"), ("Karasjok kommune", "gjeldende")],
            navneformer.Select(b => (b.Term, b.Navneformgrunn)));

        // Taggen i forskriftsteksten skal peke på KORTFORMEN — det er den strengen som faktisk står
        // der. Peker den på den gjeldende navneformen, er første ledd i kjeden feil.
        var kortform = navneformer.Single(b => b.Term == "Karasjok");
        var tagg = await db.TekstTagger.SingleAsync(
            t => t.RettskildeId == forutsetninger.ForskriftId && t.Kind == "virksomhet"
                 && t.RefId == kortform.Id && t.Entitetsstatus == "gjeldende");
        Assert.Equal("Karasjok", tagg.QuoteExact);
    }

    // ---------- Hjelpere ----------

    private static Task<SamiskSprakforvaltningSeedResultat> KjorSeedAsync(RegelIdeDbContext db)
    {
        var virksomhetOppslag = new VirksomhetOppslagTjeneste(db);
        return SamiskSprakforvaltningSeed.SeedAsync(
            db,
            new VirksomhetsbegrepTjeneste(db),
            new GruppeMedlemskapTjeneste(db),
            new MyndighetstildelingTjeneste(db),
            new TekstTaggTjeneste(db, virksomhetOppslag),
            virksomhetOppslag);
    }

    private static Task<BegrepEntitet> FinnGruppebegrepAsync(RegelIdeDbContext db, Guid lovkildeId, string term) =>
        db.Begreper.SingleAsync(
            b => b.Begrepskategori == "gruppe" && b.LovkildeId == lovkildeId && b.Term == term
                 && b.Entitetsstatus == "gjeldende");

    private sealed record Forutsetninger(Guid SamelovId, Guid ForskriftId, Guid KarasjokId);

    /// <summary>
    /// Legger inn nøyaktig det seeden slår opp — de to rettskildene på eksakt ELI med hver sin
    /// tekstnode, departementet som eier taggene, og Karasjok kommune. Radene skrives direkte med
    /// <c>db.Add</c> fordi korpuset for sameloven/forskriften ikke finnes blant testfixturene; det er
    /// seeden som er under test her, ikke importflyten.
    /// <para>
    /// Slår opp FØR den setter inn, på både virksomheter (orgnr) og rettskilder (ELI): databasen er
    /// delt med resten av collection-en, og de tre testene som trenger forutsetningene her kjører hver
    /// sin gang mot samme database. Et blindt innslag ville veltet på
    /// <c>ux_rettskilder_eli_gjeldende_delt</c> ved andre test.
    /// </para>
    /// </summary>
    private static async Task<Forutsetninger> SorgForForutsetningerAsync(RegelIdeDbContext db)
    {
        // Departementet trengs bare for at taggene skal ha en eier — id-en brukes ikke videre her.
        await SorgForVirksomhetAsync(db, DepartementNavn, DepartementOrgnr, "stat");
        var karasjokId = await SorgForVirksomhetAsync(
            db, "Karasjoga gielda / Karasjok kommune", KarasjokOrgnr, "kommune");

        var samelovId = await SorgForRettskildeAsync(
            db, SamelovEli, "Lov om Sametinget og andre samiske rettsforhold (sameloven)", "sameloven",
            "Lov", DefinisjonNodeEid, "§3-1/ledd-1/punkt-1", "punkt", DefinisjonTekst);
        var forskriftId = await SorgForRettskildeAsync(
            db, ForskriftEli, "Forskrift om forvaltningsområdet for samisk språk", null,
            "Forskrift", MedlemNodeEid, "§1/ledd-1", "ledd", MedlemTekst);

        return new Forutsetninger(samelovId, forskriftId, karasjokId);
    }

    private static async Task<Guid> SorgForVirksomhetAsync(
        RegelIdeDbContext db, string navn, string orgnr, string forvaltningsniva)
    {
        var eksisterende = await db.Virksomheter.FirstOrDefaultAsync(v => v.Organisasjonsnummer == orgnr);
        if (eksisterende is not null) return eksisterende.Id;

        var virksomhet = new Virksomhet
        {
            Id = Guid.NewGuid(),
            Navn = navn,
            Organisasjonsnummer = orgnr,
            Forvaltningsniva = forvaltningsniva,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        return virksomhet.Id;
    }

    private static async Task<Guid> SorgForRettskildeAsync(
        RegelIdeDbContext db, string eli, string tittel, string? kortnavn, string kildetype,
        string nodeEid, string kildeId, string nodeType, string tekst)
    {
        var eksisterende = await db.Rettskilder.FirstOrDefaultAsync(
            r => r.Eli == eli && r.Entitetsstatus == "gjeldende");
        if (eksisterende is not null) return eksisterende.Id;

        var rettskilde = new RettskildeEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = null, // delt/nasjonal rettskilde.
            Doctype = "act",
            Kildetype = kildetype,
            Tittel = tittel,
            Kortnavn = kortnavn,
            Eli = eli,
            // ck_rettskilder_akn_xml krever akn_xml for importrolle='primaer' — en minimal stubb, ikke
            // en oppfunnet fullversjon av dokumentet.
            AknXml = $"<akomaNtoso><act><meta><identification><FRBRWork><FRBRuri value=\"{eli}\"/>"
                     + "</FRBRWork></identification></meta></act></akomaNtoso>",
            AnsvarligDepartement = [DepartementNavn],
            Status = "Gjeldende",
            OpprettetAv = "Testoppsett",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Rettskilder.Add(rettskilde);
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(),
            RettskildeId = rettskilde.Id,
            Eid = nodeEid,
            KildeId = kildeId,
            NodeType = nodeType,
            Tekst = tekst,
            Sorteringsrekkefolge = 0,
        });
        await db.SaveChangesAsync();
        return rettskilde.Id;
    }

    private sealed record Radantall(
        int Gruppemedlemskap, int Gruppebegrep, int Navneformer, int Myndighetstildelinger, int Tagger);

    private static async Task<Radantall> TellAsync(RegelIdeDbContext db, Forutsetninger f) => new(
        await db.GruppeMedlemskap.CountAsync(m => m.HjemmelRettskildeId == f.ForskriftId),
        await db.Begreper.CountAsync(b => b.Begrepskategori == "gruppe" && b.LovkildeId == f.SamelovId),
        await db.Begreper.CountAsync(
            b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == f.KarasjokId),
        await db.Myndighetstildelinger.CountAsync(m => m.HjemmelRettskildeId == f.ForskriftId),
        await db.TekstTagger.CountAsync(t => t.RettskildeId == f.SamelovId || t.RettskildeId == f.ForskriftId));

    /// <summary>
    /// En fersk, migrert database på samme embedded Postgres-instans. Finnes KUN for
    /// <see cref="Tomt_korpus_gir_ingen_rader_og_rapporterer_hva_som_mangler"/>: den testen sier noe om
    /// et TOMT miljø, og i den delte collection-databasen ville den vært prisgitt hvilken rekkefølge
    /// xUnit tilfeldigvis kjører testmetodene i (idempotenstesten her legger nettopp inn de
    /// rettskildene den testen krever at mangler). Databasen ryddes ikke bort eksplisitt — hele
    /// Postgres-instansen med sin datamappe forsvinner når fixturen stopper.
    /// </summary>
    private async Task<string> NyTomDatabaseAsync()
    {
        var navn = $"regelide_samisk_tom_{Guid.NewGuid():N}";
        await using (var master = new RegelIdeDbContext(NyOptions(ByttDatabase("postgres"))))
        {
            // EF1003: et databasenavn er en IDENTIFIKATOR og kan ikke være en SQL-parameter i DDL.
            // Navnet er dessuten generert her, av en Guid — ingen ytre inndata er involvert.
#pragma warning disable EF1003
            await master.Database.ExecuteSqlRawAsync("CREATE DATABASE " + navn + ";");
#pragma warning restore EF1003
        }

        var connString = ByttDatabase(navn);
        await using (var ny = new RegelIdeDbContext(NyOptions(connString)))
        {
            await ny.Database.MigrateAsync();
        }
        return connString;
    }

    private string ByttDatabase(string databasenavn) =>
        _fixture.ConnectionString.Replace("Database=regelide_test", $"Database={databasenavn}");

    private static DbContextOptions<RegelIdeDbContext> NyOptions(string connString) =>
        new DbContextOptionsBuilder<RegelIdeDbContext>().UseNpgsql(connString).Options;
}
