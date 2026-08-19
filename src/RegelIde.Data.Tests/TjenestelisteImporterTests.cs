using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// docs/13-backlog.md — <see cref="TjenestelisteImporter"/> (rått høstelag, se
/// <see cref="EksternKildeEntitet"/>). Til forskjell fra de tre nettverksbaserte høsterne i dette laget
/// er denne FIL-basert (<see cref="TjenestelisteImporter.ImporterAsync"/> tar en rå JSON-streng direkte,
/// ingen <c>HttpClient</c>/<see cref="HttpMessageHandler"/> å stubbe). Klassen ble opprinnelig bygget kun
/// for Statsforvalter-tjenester (feature/statsforvalter-tjenester-hoster) og deretter generalisert
/// (feature/generaliser-tjenesteliste-importer) da fylkeskommunenes "dialog"-kontaktskjema-oversikt viste
/// seg strukturelt identisk — <see cref="Kildetype"/> er nå en parameter til <c>ImporterAsync</c> i
/// stedet for en klassekonstant. Denne filen dekker begge kjente <see cref="Kildetype"/>-verdier:
/// Statsforvalter-tjenester (fem EKTE rader, se <see cref="Testdata.LesStatsforvalterTjenesteliste"/>)
/// og fylkeskommune-dialogtjenester (fire EKTE rader, se
/// <see cref="Testdata.LesFylkeskommuneDialogtjenesteliste"/>).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class TjenestelisteImporterTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TjenestelisteImporterTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------- Statsforvalter-tjenester ----------

    private static readonly string FemTjenesterJson = Testdata.LesStatsforvalterTjenesteliste();

    private const string BokmalPdfUrl = "https://www.regjeringen.no/contentassets/45b55b2c88e94e79b55b1798053f7ad7/0073b_bokmal.pdf";
    private const string NynorskPdfUrl = "https://www.regjeringen.no/contentassets/45b55b2c88e94e79b55b1798053f7ad7/0073n_nynorsk.pdf";
    private const string EnkeltTilbyderUrl = "https://www.statsforvalteren.no/nb/agder/Plan-og-bygg/Arealforvaltning/Skjema-arealforvaltning-/Skjema-arealforvaltning-/";

    /// <summary>Identisk med <see cref="FemTjenesterJson"/> bortsett fra Arealforvaltning-radens beskrivelse — brukes til å teste at re-import KUN oppdaterer den ene endrede raden.</summary>
    private static readonly string FemTjenesterEndretJson = FemTjenesterJson.Replace(
        "Sjekklister for regulerings- og bebyggelsesplaner.", "Sjekklister for regulerings- og bebyggelsesplaner, ENDRET tekst.");

    [Fact]
    public async Task Statsforvalter_forste_import_oppretter_fem_rader()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await new TjenestelisteImporter(db).ImporterAsync(FemTjenesterJson, TjenestelisteImporter.Statsforvalter);

        Assert.Equal(5, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);
        Assert.Equal(0, resultat.TilbydereMedManglendeOrgnummer); // ekte fixturedata har ingen kjente tomme orgnummer

        var rader = await db.EksterneKilder.Where(k => k.Kildetype == TjenestelisteImporter.Statsforvalter).ToListAsync();
        Assert.Equal(5, rader.Count);
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.RaaJson)));
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.InnholdsHash)));
    }

    [Fact]
    public async Task Statsforvalter_uendret_gjenimport_er_en_no_op()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new TjenestelisteImporter(db).ImporterAsync(FemTjenesterJson, TjenestelisteImporter.Statsforvalter);
        var forHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == TjenestelisteImporter.Statsforvalter)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);

        var resultat = await new TjenestelisteImporter(db).ImporterAsync(FemTjenesterJson, TjenestelisteImporter.Statsforvalter);

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(5, resultat.Uendret);

        var antall = await db.EksterneKilder.CountAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter);
        Assert.Equal(5, antall); // ingen duplikater ved re-import

        var etterHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == TjenestelisteImporter.Statsforvalter)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);
        Assert.Equal(forHentetTidspunkter, etterHentetTidspunkter); // uendret hash ⇒ HentetTidspunkt IKKE bumpet
    }

    [Fact]
    public async Task Statsforvalter_endret_felt_pa_en_tjeneste_oppdaterer_kun_den_raden()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new TjenestelisteImporter(db).ImporterAsync(FemTjenesterJson, TjenestelisteImporter.Statsforvalter);
        // AsNoTracking (se OppgaveregisterHenterTests for full begrunnelse): unngår at "før"-øyeblikksbildet
        // deler identisk objektreferanse med raden det andre ImporterAsync-kallet muterer.
        var forEnkelt = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter && k.EksternId == EnkeltTilbyderUrl);
        var forBokmal = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter && k.EksternId == BokmalPdfUrl);

        var resultat = await new TjenestelisteImporter(db).ImporterAsync(FemTjenesterEndretJson, TjenestelisteImporter.Statsforvalter);

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(1, resultat.Oppdaterte);
        Assert.Equal(4, resultat.Uendret);

        var etterEnkelt = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter && k.EksternId == EnkeltTilbyderUrl);
        Assert.Contains("ENDRET", etterEnkelt.RaaJson);
        Assert.NotEqual(forEnkelt.InnholdsHash, etterEnkelt.InnholdsHash);
        Assert.True(etterEnkelt.HentetTidspunkt > forEnkelt.HentetTidspunkt);

        var etterBokmal = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter && k.EksternId == BokmalPdfUrl);
        Assert.Equal(forBokmal.InnholdsHash, etterBokmal.InnholdsHash);
        Assert.Equal(forBokmal.HentetTidspunkt, etterBokmal.HentetTidspunkt);
    }

    /// <summary>
    /// De to siste radene i fixturen deler NØYAKTIG samme <c>tjenestenavn</c> ("Klage på
    /// forvaltningsvedtak", et ekte bokmål/nynorsk PDF-variant-par) men har ulik <c>url</c> — beviser at
    /// importøren bruker <c>url</c>, ikke <c>tjenestenavn</c>, som identitetsnøkkel.
    /// </summary>
    [Fact]
    public async Task Statsforvalter_to_url_distinkte_men_tjenestenavn_identiske_rader_importeres_som_separate_rader()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new TjenestelisteImporter(db).ImporterAsync(FemTjenesterJson, TjenestelisteImporter.Statsforvalter);

        var bokmal = await db.EksterneKilder.SingleAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter && k.EksternId == BokmalPdfUrl);
        var nynorsk = await db.EksterneKilder.SingleAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter && k.EksternId == NynorskPdfUrl);

        Assert.NotEqual(bokmal.Id, nynorsk.Id);
        Assert.Contains("Klage på forvaltningsvedtak", bokmal.RaaJson);
        Assert.Contains("Klage på forvaltningsvedtak", nynorsk.RaaJson);
        Assert.Contains("0073b_bokmal.pdf", bokmal.RaaJson);
        Assert.Contains("0073n_nynorsk.pdf", nynorsk.RaaJson);
    }

    private const string StatsforvalterTilbyderMedTomtOrgnummerJson = """
    [
      {
        "tjenestenavn": "Syntetisk testtjeneste med manglende orgnummer",
        "url": "https://example.test/syntetisk-manglende-orgnummer",
        "tema": "Test",
        "beskrivelse": "Syntetisk fixture, ikke ekte produksjonsdata — konstruert for å dekke det kjente oppstrøms-skjørhetstilfellet der organisasjonsnummer kan bli en tom streng.",
        "tilbys_av": [
          { "organisasjon": "Ukjent embete", "organisasjonsnummer": "" },
          { "organisasjon": "Agder", "organisasjonsnummer": "974762994" }
        ]
      }
    ]
    """;

    [Fact]
    public async Task Statsforvalter_tomt_orgnummer_i_tilbys_av_telles_og_behandles_ikke_som_gyldig_identifikator()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await new TjenestelisteImporter(db).ImporterAsync(StatsforvalterTilbyderMedTomtOrgnummerJson, TjenestelisteImporter.Statsforvalter);

        Assert.Equal(1, resultat.Nye);
        Assert.Equal(1, resultat.TilbydereMedManglendeOrgnummer); // KUN den tomme telles, ikke den gyldige Agder-oppføringen

        // Raden importeres likevel i sin helhet, verbatim, inkludert den tomme strengen — ingen gjettet fallback,
        // kun synliggjøring av at kvalitetsavviket finnes.
        var rad = await db.EksterneKilder.SingleAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter);
        Assert.Contains("\"organisasjonsnummer\": \"\"", rad.RaaJson);
    }

    [Fact]
    public async Task Unik_indeks_hindrer_duplikat_pa_kildetype_og_ekstern_id()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = TjenestelisteImporter.Statsforvalter, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "a", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = TjenestelisteImporter.Statsforvalter, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "b", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ---------- Fylkeskommune-dialogtjenester ----------

    private static readonly string FireDialogtjenesterJson = Testdata.LesFylkeskommuneDialogtjenesteliste();

    private const string KarriereAgderUrl = "https://dialog.agderfk.no/dialogue/AFK-30";
    private const string SamtykkeLaerlingUrl = "https://dialog.agderfk.no/dialogue/AFK-94";
    private const string InnlandetProvenemndUrl = "https://dialog.innlandetfylke.no/dialogue/IFK-60";

    /// <summary>Identisk med <see cref="FireDialogtjenesterJson"/> bortsett fra Karriere Agder-radens beskrivelse — brukes til å teste at re-import KUN oppdaterer den ene endrede raden.</summary>
    private static readonly string FireDialogtjenesterEndretJson = FireDialogtjenesterJson.Replace(
        "\"kategori\": \"Karriere Agder\",\n    \"beskrivelse\": \"\",",
        "\"kategori\": \"Karriere Agder\",\n    \"beskrivelse\": \"ENDRET tekst.\",");

    [Fact]
    public void Fylkeskommune_fixture_har_faktisk_en_endret_rad()
    {
        // Sikrer at String.Replace over faktisk traff — hvis fixturens whitespace/rekkefølge endres senere
        // uten at denne strengen oppdateres, skal testen som bruker FireDialogtjenesterEndretJson feile
        // tydelig her, ikke stille bli en no-op-test.
        Assert.NotEqual(FireDialogtjenesterJson, FireDialogtjenesterEndretJson);
        Assert.Contains("ENDRET tekst.", FireDialogtjenesterEndretJson);
    }

    [Fact]
    public async Task Fylkeskommune_forste_import_oppretter_fire_rader()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await new TjenestelisteImporter(db).ImporterAsync(FireDialogtjenesterJson, TjenestelisteImporter.FylkeskommuneDialog);

        Assert.Equal(4, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);
        Assert.Equal(0, resultat.TilbydereMedManglendeOrgnummer); // ekte fixturedata har ingen kjente tomme orgnummer

        var rader = await db.EksterneKilder.Where(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog).ToListAsync();
        Assert.Equal(4, rader.Count);
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.RaaJson)));
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.InnholdsHash)));
    }

    [Fact]
    public async Task Fylkeskommune_uendret_gjenimport_er_en_no_op()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new TjenestelisteImporter(db).ImporterAsync(FireDialogtjenesterJson, TjenestelisteImporter.FylkeskommuneDialog);
        var forHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);

        var resultat = await new TjenestelisteImporter(db).ImporterAsync(FireDialogtjenesterJson, TjenestelisteImporter.FylkeskommuneDialog);

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(4, resultat.Uendret);

        var antall = await db.EksterneKilder.CountAsync(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog);
        Assert.Equal(4, antall); // ingen duplikater ved re-import

        var etterHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);
        Assert.Equal(forHentetTidspunkter, etterHentetTidspunkter); // uendret hash ⇒ HentetTidspunkt IKKE bumpet
    }

    [Fact]
    public async Task Fylkeskommune_endret_felt_pa_en_tjeneste_oppdaterer_kun_den_raden()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new TjenestelisteImporter(db).ImporterAsync(FireDialogtjenesterJson, TjenestelisteImporter.FylkeskommuneDialog);
        var forKarriere = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog && k.EksternId == KarriereAgderUrl);
        var forSamtykke = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog && k.EksternId == SamtykkeLaerlingUrl);

        var resultat = await new TjenestelisteImporter(db).ImporterAsync(FireDialogtjenesterEndretJson, TjenestelisteImporter.FylkeskommuneDialog);

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(1, resultat.Oppdaterte);
        Assert.Equal(3, resultat.Uendret);

        var etterKarriere = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog && k.EksternId == KarriereAgderUrl);
        Assert.Contains("ENDRET", etterKarriere.RaaJson);
        Assert.NotEqual(forKarriere.InnholdsHash, etterKarriere.InnholdsHash);
        Assert.True(etterKarriere.HentetTidspunkt > forKarriere.HentetTidspunkt);

        var etterSamtykke = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog && k.EksternId == SamtykkeLaerlingUrl);
        Assert.Equal(forSamtykke.InnholdsHash, etterSamtykke.InnholdsHash);
        Assert.Equal(forSamtykke.HentetTidspunkt, etterSamtykke.HentetTidspunkt);
    }

    /// <summary>Beviser at de to kildetypene lever i separate navnerom — samme url-mønster kunne i teorien kollidert på tvers av kilder, men (Kildetype, EksternId) er sammensatt nøkkel.</summary>
    [Fact]
    public async Task Fylkeskommune_og_statsforvalter_importer_pavirker_ikke_hverandre()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new TjenestelisteImporter(db).ImporterAsync(FemTjenesterJson, TjenestelisteImporter.Statsforvalter);
        await new TjenestelisteImporter(db).ImporterAsync(FireDialogtjenesterJson, TjenestelisteImporter.FylkeskommuneDialog);

        Assert.Equal(5, await db.EksterneKilder.CountAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter));
        Assert.Equal(4, await db.EksterneKilder.CountAsync(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog));
        Assert.True(await db.EksterneKilder.AnyAsync(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog && k.EksternId == InnlandetProvenemndUrl));
    }

    private const string FylkeskommuneTilbyderMedTomtOrgnummerJson = """
    [
      {
        "tjenestenavn": "Syntetisk testtjeneste med manglende orgnummer",
        "url": "https://example.test/fylkeskommune/syntetisk-manglende-orgnummer",
        "kategori": "Test",
        "beskrivelse": "Syntetisk fixture, ikke ekte produksjonsdata — konstruert for å dekke det kjente oppstrøms-skjørhetstilfellet der organisasjonsnummer kan bli en tom streng.",
        "tilbys_av": [
          { "organisasjon": "Ukjent fylkeskommune", "organisasjonsnummer": "" }
        ]
      }
    ]
    """;

    [Fact]
    public async Task Fylkeskommune_tomt_orgnummer_i_tilbys_av_telles_og_behandles_ikke_som_gyldig_identifikator()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await new TjenestelisteImporter(db).ImporterAsync(FylkeskommuneTilbyderMedTomtOrgnummerJson, TjenestelisteImporter.FylkeskommuneDialog);

        Assert.Equal(1, resultat.Nye);
        Assert.Equal(1, resultat.TilbydereMedManglendeOrgnummer);

        var rad = await db.EksterneKilder.SingleAsync(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog);
        Assert.Contains("\"organisasjonsnummer\": \"\"", rad.RaaJson);
    }
}
