using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="EksternTjenestelisteHandlingSeed"/> — kobler allerede høstede kommune-/fylkeskommune-/
/// statsforvalter-tjenester (<see cref="EksternKildeEntitet"/>) inn i domenemodellen.
/// <para>
/// **Isolasjon** — samme delte, ikke-transaksjonelle embedded Postgres-instans som resten av
/// RegelIde.Data.Tests (se <see cref="EmbeddedPostgresFixture"/>/<see cref="DataTestCollection"/>), samme
/// "ingen wipe av delte tabeller, ferske unike verdier per test"-mønster som
/// <see cref="OppgaveregisterHandlingSeedTests"/>. Hver test fjerner KUN sine egne
/// <see cref="EksternKildeEntitet"/>-rader (kildetype-scopet) før kjøring, se <see cref="NyKildeAsync"/>/
/// <see cref="KjorSeedIsolertAsync"/>.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class EksternTjenestelisteHandlingSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public EksternTjenestelisteHandlingSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static int _teller;
    private static string NyOrgnr() => (920_000_000 + Interlocked.Increment(ref _teller)).ToString();

    private static string TjenesteRecordJson(
        string url, string tjenestenavn, string beskrivelse, string? kategori, string? tema,
        params (string Organisasjon, string Orgnr)[] tilbydere)
    {
        var tilbysAv = string.Join(",", tilbydere.Select(t =>
            $$"""{ "organisasjon": "{{t.Organisasjon}}", "organisasjonsnummer": "{{t.Orgnr}}" }"""));
        var kategoriFelt = kategori is null ? "" : $""", "kategori": "{kategori}" """;
        var temaFelt = tema is null ? "" : $""", "tema": "{tema}" """;
        return $$"""
        {
          "url": "{{url}}",
          "tjenestenavn": "{{tjenestenavn}}",
          "beskrivelse": "{{beskrivelse}}"
          {{kategoriFelt}}
          {{temaFelt}},
          "tilbys_av": [ {{tilbysAv}} ]
        }
        """;
    }

    private static async Task<Virksomhet> LeggTilVirksomhetAsync(RegelIdeDbContext db, string navn, string orgnr)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = navn, Organisasjonsnummer = orgnr, OpprettetTidspunkt = DateTimeOffset.UtcNow };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    private static async Task<EksternKildeEntitet> NyKildeAsync(RegelIdeDbContext db, string kildetype, string eksternId, string raaJson)
    {
        await db.EksterneKilder.Where(k => k.Kildetype == kildetype && k.EksternId == eksternId).ExecuteDeleteAsync();
        var k = new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = kildetype, EksternId = eksternId,
            RaaJson = raaJson, InnholdsHash = "irrelevant-for-denne-testen", HentetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.EksterneKilder.Add(k);
        await db.SaveChangesAsync();
        return k;
    }

    /// <summary>Kjører seeden scopet til KUN de eksterne kildene testen selv nettopp opprettet, samme
    /// isolasjonsmønster som <see cref="OppgaveregisterHandlingSeedTests.KjorSeedIsolertAsync"/>.</summary>
    private static async Task<EksternTjenestelisteHandlingSeedResultat> KjorSeedIsolertAsync(
        RegelIdeDbContext db, string kildetype, params string[] behold)
    {
        await db.EksterneKilder.Where(k => k.Kildetype == kildetype && !behold.Contains(k.EksternId)).ExecuteDeleteAsync();
        return await EksternTjenestelisteHandlingSeed.SeedAsync(db, kildetype);
    }

    [Fact]
    public async Task Kjent_virksomhet_gir_ny_handling_under_kildetype_navngitt_plassholder()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        var virksomhet = await LeggTilVirksomhetAsync(db, "Testkommunen " + orgnr, orgnr);
        var kilde = await NyKildeAsync(db, KommuneTjenesteHenter.Kildetype, "K-" + orgnr,
            TjenesteRecordJson("https://skjema.no/test/1", "Ledsagerbevis - søknad", "", "Helse og omsorg", null,
                ("TESTKOMMUNEN", orgnr)));

        var resultat = await KjorSeedIsolertAsync(db, KommuneTjenesteHenter.Kildetype, kilde.EksternId);

        Assert.Equal(KommuneTjenesteHenter.Kildetype, resultat.Kildetype);
        Assert.Equal(1, resultat.KildeRaderTotalt);
        Assert.Equal(1, resultat.TilbydereTotalt);
        Assert.Equal(1, resultat.NyeHandlinger);
        Assert.Equal(1, resultat.NyeTjenester);
        Assert.Equal(0, resultat.HoppetOverUsikkerVirksomhet);

        var tjeneste = await db.Tjenester.SingleAsync(t => t.VirksomhetId == virksomhet.Id);
        Assert.Equal("Kommunale skjema — " + virksomhet.Navn, tjeneste.Tittel);
        Assert.Equal("utkast", tjeneste.Status);

        var handling = await db.Handlinger.SingleAsync(h => h.TjenesteId == tjeneste.Id);
        Assert.Equal("Ledsagerbevis - søknad", handling.Navn);
        Assert.Equal("annet", handling.Handlingstype); // ingen klassifiserende kildefelt, se punkt (e)
        Assert.Null(handling.Bruksomraade);
        Assert.Equal("soker", handling.UtfortAv);
        Assert.Equal(kilde.Id, handling.EksternKildeId);
        // Tom beskrivelse -> fallback til kategori, se klassekommentaren punkt (f).
        Assert.Equal("Helse og omsorg", handling.Merknad);
    }

    [Fact]
    public async Task Ukjent_virksomhet_hoppes_over_men_telles_ikke_som_hele_raden()
    {
        await using var db = _fixture.NyDbContext();

        var ukjentOrgnr = NyOrgnr(); // bevisst IKKE lagt til noen Virksomhet.
        var kilde = await NyKildeAsync(db, TjenestelisteImporter.FylkeskommuneDialog, "F-" + ukjentOrgnr,
            TjenesteRecordJson("https://dialog.test/1", "Test dialogtjeneste", "En ekte beskrivelse.", "", null,
                ("UKJENT FYLKE", ukjentOrgnr)));

        var resultat = await KjorSeedIsolertAsync(db, TjenestelisteImporter.FylkeskommuneDialog, kilde.EksternId);

        Assert.Equal(1, resultat.KildeRaderTotalt);
        Assert.Equal(1, resultat.TilbydereTotalt);
        Assert.Equal(0, resultat.NyeHandlinger);
        Assert.Equal(1, resultat.HoppetOverUsikkerVirksomhet);
        Assert.Equal(0, resultat.NyeTjenester);
        Assert.False(await db.Handlinger.AnyAsync(h => h.EksternKildeId == kilde.Id));
    }

    [Fact]
    public async Task Statsforvalter_med_flere_tilbydere_dupliserer_handling_under_hver_kjent_tilbyder()
    {
        await using var db = _fixture.NyDbContext();

        var orgnrA = NyOrgnr();
        var orgnrB = NyOrgnr();
        var orgnrUkjent = NyOrgnr(); // bevisst ikke lagt til.
        var embeteA = await LeggTilVirksomhetAsync(db, "Statsforvalteren i A " + orgnrA, orgnrA);
        var embeteB = await LeggTilVirksomhetAsync(db, "Statsforvalteren i B " + orgnrB, orgnrB);

        var kilde = await NyKildeAsync(db, TjenestelisteImporter.Statsforvalter, "S-" + orgnrA,
            TjenesteRecordJson("https://statsforvalteren.no/test/1", "Meld frå om kritikkverdig forhold",
                "Ein arbeidstakar kan varsle.", null, "Folk og samfunn",
                ("A", orgnrA), ("B", orgnrB), ("Ukjent", orgnrUkjent)));

        var resultat = await KjorSeedIsolertAsync(db, TjenestelisteImporter.Statsforvalter, kilde.EksternId);

        Assert.Equal(1, resultat.KildeRaderTotalt);
        Assert.Equal(3, resultat.TilbydereTotalt); // tre tilbydere i raden ...
        Assert.Equal(2, resultat.NyeHandlinger); // ... men kun to med kjent virksomhet.
        Assert.Equal(1, resultat.HoppetOverUsikkerVirksomhet);
        Assert.Equal(2, resultat.NyeTjenester); // én plassholder PER embete, ikke delt.

        // SAMME kilderad (samme EksternKildeId) gir to DISTINKTE Handling-rader, under to
        // forskjellige plassholder-Tjenester — se klassekommentaren punkt (b).
        var handlinger = await db.Handlinger.Where(h => h.EksternKildeId == kilde.Id).ToListAsync();
        Assert.Equal(2, handlinger.Count);
        Assert.Equal(2, handlinger.Select(h => h.TjenesteId).Distinct().Count());

        var tjenesteA = await db.Tjenester.SingleAsync(t => t.VirksomhetId == embeteA.Id);
        var tjenesteB = await db.Tjenester.SingleAsync(t => t.VirksomhetId == embeteB.Id);
        Assert.Equal("Statsforvalter-tjeneste — " + embeteA.Navn, tjenesteA.Tittel);
        Assert.Equal("Statsforvalter-tjeneste — " + embeteB.Navn, tjenesteB.Tittel);
        Assert.Contains(handlinger, h => h.TjenesteId == tjenesteA.Id);
        Assert.Contains(handlinger, h => h.TjenesteId == tjenesteB.Id);
    }

    [Fact]
    public async Task Rekjoring_med_uendrede_data_er_en_no_op()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        await LeggTilVirksomhetAsync(db, "Testkommunen igjen " + orgnr, orgnr);
        var kilde = await NyKildeAsync(db, KommuneTjenesteHenter.Kildetype, "K-rekjor-" + orgnr,
            TjenesteRecordJson("https://skjema.no/test/2", "Serveringsbevilling - søknad", "", "Næring", null,
                ("TESTKOMMUNEN", orgnr)));

        var forste = await KjorSeedIsolertAsync(db, KommuneTjenesteHenter.Kildetype, kilde.EksternId);
        Assert.Equal(1, forste.NyeHandlinger);

        var andre = await KjorSeedIsolertAsync(db, KommuneTjenesteHenter.Kildetype, kilde.EksternId);
        Assert.Equal(0, andre.NyeHandlinger);
        Assert.Equal(0, andre.OppdaterteHandlinger);
        Assert.Equal(1, andre.UendretHandlinger);
        Assert.Equal(0, andre.NyeTjenester); // fant eksisterende plassholder, ikke en ny duplikat.

        Assert.Equal(1, await db.Handlinger.CountAsync(h => h.EksternKildeId == kilde.Id)); // ingen duplikat
    }

    [Fact]
    public async Task Massesletting_fjerner_handlinger_og_tomme_plassholdere_rorer_aldri_eksternkilde()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        var virksomhet = await LeggTilVirksomhetAsync(db, "Testkommunen slett " + orgnr, orgnr);
        var kilde = await NyKildeAsync(db, KommuneTjenesteHenter.Kildetype, "K-slett-" + orgnr,
            TjenesteRecordJson("https://skjema.no/test/3", "Ledsagerbevis - søknad", "", "Helse og omsorg", null,
                ("TESTKOMMUNEN", orgnr)));

        await KjorSeedIsolertAsync(db, KommuneTjenesteHenter.Kildetype, kilde.EksternId);
        var tjenesteId = (await db.Tjenester.SingleAsync(t => t.VirksomhetId == virksomhet.Id)).Id;
        Assert.True(await db.Handlinger.AnyAsync(h => h.TjenesteId == tjenesteId));

        var slettResultat = await EksternTjenestelisteHandlingSeed.SlettKonverterteAsync(db, KommuneTjenesteHenter.Kildetype);

        Assert.Equal(1, slettResultat.SlettedeHandlinger);
        Assert.Equal(1, slettResultat.SlettedeTjenester);
        Assert.False(await db.Handlinger.AnyAsync(h => h.EksternKildeId == kilde.Id));
        Assert.False(await db.Tjenester.AnyAsync(t => t.Id == tjenesteId));
        // EksternKilde-raden (proveniensen/rå-høstingen) skal ALDRI røres av massesletting.
        Assert.True(await db.EksterneKilder.AnyAsync(k => k.Id == kilde.Id));

        // Re-kjøring etter sletting gir identisk resultat tilbake (samme kilderad, fortsatt der).
        var gjenopprettetResultat = await EksternTjenestelisteHandlingSeed.SeedAsync(db, KommuneTjenesteHenter.Kildetype);
        Assert.True(await db.Handlinger.AnyAsync(h => h.EksternKildeId == kilde.Id));
        var nyTjeneste = await db.Tjenester.SingleAsync(t => t.VirksomhetId == virksomhet.Id);
        var nyHandling = await db.Handlinger.SingleAsync(h => h.TjenesteId == nyTjeneste.Id);
        Assert.Equal("Ledsagerbevis - søknad", nyHandling.Navn);
        Assert.Equal("Helse og omsorg", nyHandling.Merknad);
    }

    [Fact]
    public async Task Ukjent_kildetype_kaster_argument_exception()
    {
        await using var db = _fixture.NyDbContext();

        await Assert.ThrowsAsync<ArgumentException>(() => EksternTjenestelisteHandlingSeed.SeedAsync(db, "ukjent_kildetype"));
        await Assert.ThrowsAsync<ArgumentException>(() => EksternTjenestelisteHandlingSeed.SlettKonverterteAsync(db, "ukjent_kildetype"));
    }

    [Fact]
    public async Task Beskrivelse_brukes_fremfor_kategori_naar_begge_finnes()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        await LeggTilVirksomhetAsync(db, "Testfylket " + orgnr, orgnr);
        var kilde = await NyKildeAsync(db, TjenestelisteImporter.FylkeskommuneDialog, "F-besk-" + orgnr,
            TjenesteRecordJson("https://dialog.test/2", "Test med begge felt", "Den ekte beskrivelsen vinner.",
                "En kategori", null, ("TESTFYLKET", orgnr)));

        await KjorSeedIsolertAsync(db, TjenestelisteImporter.FylkeskommuneDialog, kilde.EksternId);

        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        Assert.Equal("Den ekte beskrivelsen vinner.", handling.Merknad);
    }
}
