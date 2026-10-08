using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #310 «Strukturmodell 5: nodetype-akse», 2026-10-07] Data-delen av migrasjonen
/// <c>InnforNodetypeakse</c> — kjørt mot rader testen selv har lagt inn. Migrasjonen kjører ellers bare
/// én gang, mot den tomme testbasen, og reklassifiseringen ville vært utestet. Testene kjører NØYAKTIG
/// den SQL-en migrasjonen kjører (<see cref="NodetypeReklassifisering.Sql"/> /
/// <see cref="NodetypeReklassifisering.AktortypeSql"/>), ikke en kopi.
/// <para>
/// Klassen bruker ÉN egen, fersk-migrert database (samme teknikk som
/// <c>SamiskSprakforvaltningSeedTests.NyTomDatabaseAsync</c>), og hver test kjører i en transaksjon som
/// rulles tilbake: reklassifiseringen identifiserer rader på TERM og LOV, og i den delte
/// collection-databasen ville andre testers «kommunene»/«departementet» blitt truffet — og omvendt.
/// [ENDRET 2026-10-07] Var én fersk database PER test (seks stk.). Med det hang senere tester som
/// forventer en DB-skranke-feil (INSERT «active» uten ventehendelse i pg_stat_activity, målt) til 30 s
/// timeout i samme kjøring. Antatt årsak, IKKE verifisert: embedded Postgres' loggutdata (sjekkpunkter
/// ved CREATE DATABASE, ERROR-linjer) skrives til et rør ingen leser, og når det er fullt blokkerer
/// neste ERROR-logging. Én database i stedet for seks fjernet symptomet.
/// </para>
/// <para>
/// [ENDRET, issue #311] Databasen migreres nå bare til <c>InnforNodetypeakse</c> (<see cref="HistoriskSkjema"/>),
/// ikke til siste versjon: SQL-en er frosset sammen med #310-migrasjonen og skriver i
/// <c>myndighetstildelinger</c>/<c>gruppe_medlemskap</c> og kategorien <c>'organ'</c>, som #311 fjernet. Testene
/// prøver dermed det samme som før — mot skjemaet slik det var da migrasjonen kjørte. Radene i de to droppede
/// tabellene legges inn og leses med rå SQL (ingen EF-entitet finnes lenger).
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class NodetypeReklassifiseringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public NodetypeReklassifiseringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private const string HolEli = "https://lovdata.no/eli/lov/2011/06/24/30/nor";
    private const string SamelEli = "https://lovdata.no/eli/lov/1987/06/12/56/nor";
    private const string ReindriftEli = "https://lovdata.no/eli/lov/2007/06/15/40/nor";
    private const string EnlEli = "https://lovdata.no/eli/lov/1990/06/29/50/nor";
    private const string NglTittel = "Lov om felles regler for det indre marked for naturgass (naturgassloven)";
    private const string VgfEli = "https://lovdata.no/eli/forskrift/2013/02/15/201/nor";

    private sealed record Lover(Guid Hol, Guid Samel, Guid Reindrift, Guid Enl, Guid Ngl, Guid Vgf, Guid Annen);

    private static async Task<Lover> LeggInnLoverAsync(RegelIdeDbContext db)
    {
        Guid Ny(string tittel, string? eli)
        {
            var id = Guid.NewGuid();
            db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = id, Doctype = "act", Kildetype = "Lov", Tittel = tittel, Eli = eli, Status = "Gjeldende",
                Importrolle = "referanse", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            return id;
        }
        var lover = new Lover(
            Ny("helse- og omsorgstjenesteloven", HolEli),
            Ny("sameloven", SamelEli),
            Ny("reindriftsloven", ReindriftEli),
            Ny("energiloven", EnlEli),
            // Naturgassloven UTEN ELI — skal treffes på tittel alene (identifisering «via ELI/tittel»).
            Ny(NglTittel, null),
            Ny("vergemålsforskriften", VgfEli),
            Ny("en helt annen lov", "https://lovdata.no/eli/lov/1999/01/01/1/nor"));
        await db.SaveChangesAsync();
        return lover;
    }

    private static BegrepEntitet Gruppe(RegelIdeDbContext db, string term, Guid? lov)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "gruppe", LovkildeId = lov, Term = term, Status = "publisert",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        return b;
    }

    private static TekstTaggEntitet Tagg(RegelIdeDbContext db, Guid eier, Guid rettskilde, Guid refId, int start, string kind = "begrep")
    {
        var t = new TekstTaggEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = eier, RettskildeId = rettskilde, NodeEid = "§1/ledd-1",
            StartOffset = start, EndOffset = start + 5, QuotePrefix = "", QuoteExact = "kongen", QuoteSuffix = "",
            NodeTekstHash = "h", Kind = kind, RefId = refId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.TekstTagger.Add(t);
        return t;
    }

    // [ENDRET, issue #311] Rå SQL — se klassekommentaren.
    private static Task<HistoriskSkjema.Rad> Tildeling(RegelIdeDbContext db, Guid gruppe, Guid virksomhet, Guid hjemmel) =>
        HistoriskSkjema.TildelingAsync(db, gruppe, virksomhet, hjemmel);

    private static Task<HistoriskSkjema.Rad> Medlemskap(RegelIdeDbContext db, Guid over, Guid under, Guid hjemmel) =>
        HistoriskSkjema.MedlemskapAsync(db, over, under, hjemmel);

    private static Virksomhet NyVirksomhet(RegelIdeDbContext db, string navn, string? orgnr = null) =>
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = navn, Organisasjonsnummer = orgnr }).Entity;

    private static Task KjorAsync(RegelIdeDbContext db) => db.Database.ExecuteSqlRawAsync(NodetypeReklassifisering.Sql);

    private static Task<Guid> GruppeForTildelingAsync(RegelIdeDbContext db, Guid tildelingId) =>
        HistoriskSkjema.GuidAsync(db, $"""SELECT gruppe_begrep_id AS "Value" FROM myndighetstildelinger WHERE "Id" = {tildelingId}""");

    private static async Task<BegrepEntitet> HentAsync(RegelIdeDbContext db, Guid id) =>
        await db.Begreper.AsNoTracking().SingleAsync(b => b.Id == id);

    /// <summary>
    /// Hele den godkjente lista (issue #310), inkl. begge sammenslåingene og flytting av koblinger.
    /// Spørsmålet testen besvarer: peker taggene, tildelingene og medlemskapene fortsatt på et levende
    /// begrep med riktig type etter reklassifiseringen (issue #310 AC2)?
    /// </summary>
    [Fact]
    public async Task Godkjent_liste_reklassifiseres_og_koblinger_flyttes_til_overlevende_rad()
    {
        await using var db = new RegelIdeDbContext(NyOptions(await EgenDatabaseAsync()));
        await using var tx = await db.Database.BeginTransactionAsync(); // rulles tilbake ved dispose — se klassekommentaren.
        var l = await LeggInnLoverAsync(db);
        var eier = NyVirksomhet(db, "Kommunal- og distriktsdepartementet");
        var karasjok = NyVirksomhet(db, "Karasjok kommune");
        var rme = NyVirksomhet(db, "Reguleringsmyndigheten for energi");
        var sfTroms = NyVirksomhet(db, "Statsforvalteren i Troms og Finnmark");

        var departementet = Gruppe(db, "departementet", l.Hol);
        var forvaltningsomradet = Gruppe(db, "forvaltningsområdet for samiske språk", l.Samel);
        var kommunene = Gruppe(db, "kommunene", l.Reindrift);
        var kongen = Gruppe(db, "kongen", null);
        var kongenIStatsrad = Gruppe(db, "Kongen i statsråd", null);
        var regEnl = Gruppe(db, "reguleringsmyndighet", l.Enl);
        var regNgl = Gruppe(db, "reguleringsmyndighet", l.Ngl);
        var stimulering = Gruppe(db, "språkstimuleringskommuner", l.Samel);
        var utvikling = Gruppe(db, "språkutviklingskommuner", l.Samel);
        var vitalisering = Gruppe(db, "språkvitaliseringskommuner", l.Samel);
        var statsforvalter = Gruppe(db, "statsforvalter", l.Reindrift);
        var statsforvalteren = Gruppe(db, "Statsforvalteren", l.Vgf);
        var stortinget = Gruppe(db, "stortinget", l.Reindrift);
        await db.SaveChangesAsync();

        // [ENDRET, issue #311] Taggene lagres FØR de rå SQL-innsettingene under, som kjører med en gang.
        // Tagger: to på «kongen» (den ene en EKSAKT dublett av en tagg på «Kongen i statsråd»), én på overlevende.
        Tagg(db, eier.Id, l.Reindrift, kongenIStatsrad.Id, 0);
        var dublettTagg = Tagg(db, eier.Id, l.Reindrift, kongen.Id, 0);
        var flyttetTagg = Tagg(db, eier.Id, l.Reindrift, kongen.Id, 20);
        var taggPaVergemal = Tagg(db, eier.Id, l.Vgf, statsforvalteren.Id, 0);
        await db.SaveChangesAsync();
        // Tildelinger: én på vergemålsforskriftens «Statsforvalteren» (skal flyttes), og en identisk på begge
        // statsforvalter-radene (dubletten skal forsvinne, ikke bli to).
        var tildelingVgf = await Tildeling(db, statsforvalteren.Id, sfTroms.Id, l.Vgf);
        await Tildeling(db, statsforvalter.Id, karasjok.Id, l.Reindrift);
        await Tildeling(db, statsforvalteren.Id, karasjok.Id, l.Reindrift);
        var tildelingRme = await Tildeling(db, regNgl.Id, rme.Id, l.Ngl);
        var tildelingKarasjok = await Tildeling(db, utvikling.Id, karasjok.Id, l.Samel);
        // Medlemskap: språkkategoriene i forvaltningsområdet (skal stå urørt), og «kongen» som medlem av
        // «Kongen i statsråd» (blir selv-medlemskap ved sammenslåing ⇒ slettes).
        var m1 = await Medlemskap(db, forvaltningsomradet.Id, utvikling.Id, l.Samel);
        var m2 = await Medlemskap(db, forvaltningsomradet.Id, vitalisering.Id, l.Samel);
        var m3 = await Medlemskap(db, forvaltningsomradet.Id, stimulering.Id, l.Samel);
        await Medlemskap(db, kongenIStatsrad.Id, kongen.Id, l.Reindrift);
        await db.SaveChangesAsync();

        await KjorAsync(db);
        db.ChangeTracker.Clear();

        // Enkle reklassifiseringer.
        Assert.Equal("rolle", (await HentAsync(db, departementet.Id)).Begrepskategori);
        Assert.Equal("omrade", (await HentAsync(db, forvaltningsomradet.Id)).Begrepskategori);
        Assert.Equal("klasse", (await HentAsync(db, kommunene.Id)).Begrepskategori);
        Assert.Equal("rolle", (await HentAsync(db, regEnl.Id)).Begrepskategori);
        Assert.Equal("rolle", (await HentAsync(db, regNgl.Id)).Begrepskategori); // truffet på TITTEL (ingen ELI).
        foreach (var k in new[] { stimulering, utvikling, vitalisering })
        {
            Assert.Equal("klasse", (await HentAsync(db, k.Id)).Begrepskategori);
        }
        // «stortinget»: Stortinget finnes ikke som virksomhet ⇒ organ-begrep, ingen virksomhet opprettet.
        var stortingetEtter = await HentAsync(db, stortinget.Id);
        Assert.Equal("organ", stortingetEtter.Begrepskategori);
        Assert.False(await db.Virksomheter.AnyAsync(v => v.Organisasjonsnummer == "971524960"));

        // Kongen-sammenslåingen.
        var kongenEtter = await HentAsync(db, kongen.Id);
        var overlevendeKonge = await HentAsync(db, kongenIStatsrad.Id);
        Assert.Equal("arkivert", kongenEtter.Entitetsstatus);
        Assert.Equal("organ", kongenEtter.Begrepskategori);
        Assert.Equal("gjeldende", overlevendeKonge.Entitetsstatus);
        Assert.Equal("organ", overlevendeKonge.Begrepskategori);
        Assert.Null(overlevendeKonge.LovkildeId);
        Assert.Equal(kongenIStatsrad.Id, (await db.TekstTagger.SingleAsync(t => t.Id == flyttetTagg.Id)).RefId);
        var dublettEtter = await db.TekstTagger.SingleAsync(t => t.Id == dublettTagg.Id);
        Assert.Equal("arkivert", dublettEtter.Entitetsstatus); // kunne ikke flyttes (unik indeks) ⇒ arkivert, ikke slettet.
        Assert.Equal(0, await HistoriskSkjema.TellAsync(db, $"""
            SELECT count(*)::int AS "Value" FROM gruppe_medlemskap
            WHERE underordnet_gruppe_begrep_id = {kongen.Id} OR overordnet_gruppe_begrep_id = {kongen.Id}
            """));
        Assert.Equal(0, await HistoriskSkjema.TellAsync(db,
            $"""SELECT count(*)::int AS "Value" FROM gruppe_medlemskap WHERE overordnet_gruppe_begrep_id = underordnet_gruppe_begrep_id"""));

        // Statsforvalter-sammenslåingen: reindriftslovens rad overlever som FAST, nasjonal klasse.
        var sfEtter = await HentAsync(db, statsforvalter.Id);
        Assert.Equal("klasse", sfEtter.Begrepskategori);
        Assert.Null(sfEtter.LovkildeId);
        Assert.Equal("gjeldende", sfEtter.Entitetsstatus);
        var sfArkivert = await HentAsync(db, statsforvalteren.Id);
        Assert.Equal("arkivert", sfArkivert.Entitetsstatus);
        Assert.Equal(statsforvalter.Id, (await db.TekstTagger.SingleAsync(t => t.Id == taggPaVergemal.Id)).RefId);
        Assert.Equal(statsforvalter.Id, await GruppeForTildelingAsync(db, tildelingVgf.Id));
        Assert.Equal(1, await HistoriskSkjema.TellAsync(db, $"""
            SELECT count(*)::int AS "Value" FROM myndighetstildelinger
            WHERE virksomhet_id = {karasjok.Id} AND hjemmel_rettskilde_id = {l.Reindrift}
            """));
        Assert.Equal(0, await HistoriskSkjema.TellAsync(db,
            $"""SELECT count(*)::int AS "Value" FROM myndighetstildelinger WHERE gruppe_begrep_id = {statsforvalteren.Id}"""));

        // Koblinger på reklassifiserte (ikke sammenslåtte) rader er urørt.
        Assert.Equal(regNgl.Id, await GruppeForTildelingAsync(db, tildelingRme.Id));
        Assert.Equal(utvikling.Id, await GruppeForTildelingAsync(db, tildelingKarasjok.Id));
        Assert.Equal(3, await HistoriskSkjema.TellAsync(db, $"""
            SELECT count(*)::int AS "Value" FROM gruppe_medlemskap
            WHERE "Id" IN ({m1.Id}, {m2.Id}, {m3.Id}) AND overordnet_gruppe_begrep_id = {forvaltningsomradet.Id}
            """));

        // Ingen gjeldende 'gruppe'-rader igjen, og alt er sporbart i proveniens.
        Assert.False(await db.Begreper.AnyAsync(b => b.Begrepskategori == "gruppe"));
        Assert.Equal(11, await db.Proveniens.CountAsync(p => p.EndretAv == "migrasjon-310" && p.Handling == "endret"));
        Assert.Equal(2, await db.Proveniens.CountAsync(p => p.EndretAv == "migrasjon-310" && p.Handling == "arkivert"));
    }

    /// <summary>
    /// Identifiseringen er (term, lov) — aldri bare term. Samme term i en ANNEN lov, og en term som ikke
    /// står på lista, røres ikke. En andre kjøring endrer ingenting (idempotent), og en tom base gir ingen
    /// feil (raden finnes ikke ⇒ no-op).
    /// </summary>
    [Fact]
    public async Task Rader_utenfor_lista_rores_ikke_og_andre_kjoring_er_no_op()
    {
        await using var db = new RegelIdeDbContext(NyOptions(await EgenDatabaseAsync()));
        await using var tx = await db.Database.BeginTransactionAsync(); // rulles tilbake ved dispose — se klassekommentaren.
        await KjorAsync(db); // tom base: ingen feil.

        var l = await LeggInnLoverAsync(db);
        var kommuneneAnnenLov = Gruppe(db, "kommunene", l.Annen);
        var departementetAnnenLov = Gruppe(db, "departementet", l.Annen);
        var ikkePaLista = Gruppe(db, "vertskommuner", l.Samel);
        var kommunene = Gruppe(db, "Kommunene", l.Reindrift); // case-insensitiv treff.
        await db.SaveChangesAsync();

        await KjorAsync(db);
        db.ChangeTracker.Clear();
        Assert.Equal("gruppe", (await HentAsync(db, kommuneneAnnenLov.Id)).Begrepskategori);
        Assert.Equal("gruppe", (await HentAsync(db, departementetAnnenLov.Id)).Begrepskategori);
        Assert.Equal("gruppe", (await HentAsync(db, ikkePaLista.Id)).Begrepskategori);
        Assert.Equal("klasse", (await HentAsync(db, kommunene.Id)).Begrepskategori);

        var proveniensFor = await db.Proveniens.CountAsync(p => p.EndretAv == "migrasjon-310");
        await KjorAsync(db);
        Assert.Equal(proveniensFor, await db.Proveniens.CountAsync(p => p.EndretAv == "migrasjon-310"));
    }

    /// <summary>«stortinget» → navneform når Stortinget FINNES som virksomhet (orgnr 971524960): taggen
    /// flyttes til virksomhet-laget, og eksisterer navneformen alt, slås de sammen.</summary>
    [Fact]
    public async Task Stortinget_blir_navneform_nar_virksomheten_finnes()
    {
        await using var db = new RegelIdeDbContext(NyOptions(await EgenDatabaseAsync()));
        await using var tx = await db.Database.BeginTransactionAsync(); // rulles tilbake ved dispose — se klassekommentaren.
        var l = await LeggInnLoverAsync(db);
        var storting = NyVirksomhet(db, "STORTINGET", "971524960");
        var stortinget = Gruppe(db, "stortinget", l.Reindrift);
        await db.SaveChangesAsync();
        var tagg = Tagg(db, storting.Id, l.Reindrift, stortinget.Id, 0);
        await db.SaveChangesAsync();

        await KjorAsync(db);
        db.ChangeTracker.Clear();

        var etter = await HentAsync(db, stortinget.Id);
        Assert.Equal("virksomhet", etter.Begrepskategori);
        Assert.Equal(storting.Id, etter.VirksomhetReferanseId);
        Assert.Null(etter.LovkildeId);
        Assert.Equal("virksomhet", (await db.TekstTagger.SingleAsync(t => t.Id == tagg.Id)).Kind);
    }

    [Fact]
    public async Task Stortinget_slas_sammen_med_eksisterende_navneform()
    {
        await using var db = new RegelIdeDbContext(NyOptions(await EgenDatabaseAsync()));
        await using var tx = await db.Database.BeginTransactionAsync(); // rulles tilbake ved dispose — se klassekommentaren.
        var l = await LeggInnLoverAsync(db);
        var storting = NyVirksomhet(db, "STORTINGET", "971524960");
        var stortinget = Gruppe(db, "stortinget", l.Reindrift);
        var navneform = db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = storting.Id, Term = "Stortinget",
            Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        }).Entity;
        await db.SaveChangesAsync();
        var tagg = Tagg(db, storting.Id, l.Reindrift, stortinget.Id, 0);
        await db.SaveChangesAsync();

        await KjorAsync(db);
        db.ChangeTracker.Clear();

        Assert.Equal("arkivert", (await HentAsync(db, stortinget.Id)).Entitetsstatus);
        var taggEtter = await db.TekstTagger.SingleAsync(t => t.Id == tagg.Id);
        Assert.Equal(navneform.Id, taggEtter.RefId);
        Assert.Equal("virksomhet", taggEtter.Kind);
    }

    /// <summary>Issue #310 AC1: KOMM/FYLK → rettssubjekt; alt annet NULL; et menneskes valg overskrives
    /// aldri. Forvaltningsnivå kommune/fylkeskommune telles med — se <see cref="Virksomhet.Aktortype"/>
    /// for målingen bak (0 rader lokalt har organisasjonsform_kode KOMM/FYLK).</summary>
    [Fact]
    public async Task Aktortype_fylles_bare_der_det_er_entydig()
    {
        await using var db = new RegelIdeDbContext(NyOptions(await EgenDatabaseAsync()));
        await using var tx = await db.Database.BeginTransactionAsync(); // rulles tilbake ved dispose — se klassekommentaren.
        Virksomhet V(string navn, string? orgform, string? niva, string? aktortype = null) =>
            db.Virksomheter.Add(new Virksomhet
            {
                Id = Guid.NewGuid(), Navn = navn, OrganisasjonsformKode = orgform, Forvaltningsniva = niva, Aktortype = aktortype,
            }).Entity;
        var komm = V("Oslo kommune", "KOMM", null);
        var fylk = V("Agder fylkeskommune", "FYLK", null);
        var kommuneNiva = V("Bergen kommune", null, "kommune");
        var fylkeNiva = V("Vestland fylkeskommune", null, "fylkeskommune");
        var orgl = V("Mattilsynet", "ORGL", "stat");
        var statsforvalter = V("Statsforvalteren i Agder", "ORGL", "statsforvalter");
        var manuelt = V("Kommunestyret i Oslo", "KOMM", "kommune", aktortype: "organ");
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(NodetypeReklassifisering.AktortypeSql);
        db.ChangeTracker.Clear();

        async Task<string?> Type(Virksomhet v) => (await db.Virksomheter.AsNoTracking().SingleAsync(x => x.Id == v.Id)).Aktortype;
        Assert.Equal("rettssubjekt", await Type(komm));
        Assert.Equal("rettssubjekt", await Type(fylk));
        Assert.Equal("rettssubjekt", await Type(kommuneNiva));
        Assert.Equal("rettssubjekt", await Type(fylkeNiva));
        Assert.Null(await Type(orgl));
        Assert.Null(await Type(statsforvalter));
        Assert.Equal("organ", await Type(manuelt));
    }

    [Theory]
    [InlineData("KOMM", null, "rettssubjekt")]
    [InlineData("FYLK", null, "rettssubjekt")]
    [InlineData(null, "kommune", "rettssubjekt")]
    [InlineData(null, "fylkeskommune", "rettssubjekt")]
    [InlineData("ORGL", "stat", null)]
    [InlineData("STAT", null, null)]
    [InlineData(null, "statsforvalter", null)]
    [InlineData(null, null, null)]
    public void UtledAktortypeAutomatisk_speiler_migrasjonen(string? orgform, string? niva, string? forventet) =>
        Assert.Equal(forventet, Nodetyper.UtledAktortypeAutomatisk(orgform, niva));

    /// <summary>CHECK-en på aktortype — lukket vokabular (klassens egen database, se klassekommentaren).</summary>
    [Fact]
    public async Task Ukjent_aktortype_avvises_av_databasen()
    {
        await using var db = new RegelIdeDbContext(NyOptions(await EgenDatabaseAsync()));
        await using var tx = await db.Database.BeginTransactionAsync(); // rulles tilbake ved dispose — se klassekommentaren.
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = $"x-{Guid.NewGuid():N}", Aktortype = "kommune" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ---------- Fersk database per test (se klassekommentaren) ----------

    private static readonly SemaphoreSlim DatabaseLas = new(1, 1);
    private static string? _egenDatabase;

    /// <summary>Klassens ene, ferske database — opprettes første gang en test trenger den.</summary>
    private async Task<string> EgenDatabaseAsync()
    {
        await DatabaseLas.WaitAsync();
        try
        {
            return _egenDatabase ??= await NyTomDatabaseAsync();
        }
        finally
        {
            DatabaseLas.Release();
        }
    }

    private Task<string> NyTomDatabaseAsync() =>
        HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_nt310", HistoriskSkjema.Nodetypeakse);

    private static DbContextOptions<RegelIdeDbContext> NyOptions(string connString) =>
        new DbContextOptionsBuilder<RegelIdeDbContext>().UseNpgsql(connString).Options;
}
