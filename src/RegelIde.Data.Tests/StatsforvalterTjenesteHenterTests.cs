using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// docs/13-backlog.md — Statsforvalter-tjenester-importøren (rått høstelag, se
/// <see cref="EksternKildeEntitet"/>). Til forskjell fra de tre andre høsterne i dette laget er denne
/// FIL-basert (<see cref="StatsforvalterTjenesteHenter.ImporterAsync"/> tar en rå JSON-streng direkte,
/// ingen <c>HttpClient</c>/<see cref="HttpMessageHandler"/> å stubbe). Primærfixturen
/// (<see cref="Testdata.LesStatsforvalterTjenesteliste"/>) er fem EKTE rader trimmet fra Johanns
/// ~288-rads produksjonsuttrekk — se den metodens kommentar for hva de fem dekker.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class StatsforvalterTjenesteHenterTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public StatsforvalterTjenesteHenterTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly string FemTjenesterJson = Testdata.LesStatsforvalterTjenesteliste();

    private const string BokmalPdfUrl = "https://www.regjeringen.no/contentassets/45b55b2c88e94e79b55b1798053f7ad7/0073b_bokmal.pdf";
    private const string NynorskPdfUrl = "https://www.regjeringen.no/contentassets/45b55b2c88e94e79b55b1798053f7ad7/0073n_nynorsk.pdf";
    private const string EnkeltTilbyderUrl = "https://www.statsforvalteren.no/nb/agder/Plan-og-bygg/Arealforvaltning/Skjema-arealforvaltning-/Skjema-arealforvaltning-/";

    /// <summary>Identisk med <see cref="FemTjenesterJson"/> bortsett fra Arealforvaltning-radens beskrivelse — brukes til å teste at re-import KUN oppdaterer den ene endrede raden.</summary>
    private static readonly string FemTjenesterEndretJson = FemTjenesterJson.Replace(
        "Sjekklister for regulerings- og bebyggelsesplaner.", "Sjekklister for regulerings- og bebyggelsesplaner, ENDRET tekst.");

    [Fact]
    public async Task Forste_import_oppretter_fem_rader()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await new StatsforvalterTjenesteHenter(db).ImporterAsync(FemTjenesterJson);

        Assert.Equal(5, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);
        Assert.Equal(0, resultat.TilbydereMedManglendeOrgnummer); // ekte fixturedata har ingen kjente tomme orgnummer

        var rader = await db.EksterneKilder.Where(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype).ToListAsync();
        Assert.Equal(5, rader.Count);
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.RaaJson)));
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.InnholdsHash)));
    }

    [Fact]
    public async Task Uendret_gjenimport_er_en_no_op()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new StatsforvalterTjenesteHenter(db).ImporterAsync(FemTjenesterJson);
        var forHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);

        var resultat = await new StatsforvalterTjenesteHenter(db).ImporterAsync(FemTjenesterJson);

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(5, resultat.Uendret);

        var antall = await db.EksterneKilder.CountAsync(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype);
        Assert.Equal(5, antall); // ingen duplikater ved re-import

        var etterHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);
        Assert.Equal(forHentetTidspunkter, etterHentetTidspunkter); // uendret hash ⇒ HentetTidspunkt IKKE bumpet
    }

    [Fact]
    public async Task Endret_felt_pa_en_tjeneste_oppdaterer_kun_den_raden()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new StatsforvalterTjenesteHenter(db).ImporterAsync(FemTjenesterJson);
        // AsNoTracking (se OppgaveregisterHenterTests for full begrunnelse): unngår at "før"-øyeblikksbildet
        // deler identisk objektreferanse med raden det andre ImporterAsync-kallet muterer.
        var forEnkelt = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype && k.EksternId == EnkeltTilbyderUrl);
        var forBokmal = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype && k.EksternId == BokmalPdfUrl);

        var resultat = await new StatsforvalterTjenesteHenter(db).ImporterAsync(FemTjenesterEndretJson);

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(1, resultat.Oppdaterte);
        Assert.Equal(4, resultat.Uendret);

        var etterEnkelt = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype && k.EksternId == EnkeltTilbyderUrl);
        Assert.Contains("ENDRET", etterEnkelt.RaaJson);
        Assert.NotEqual(forEnkelt.InnholdsHash, etterEnkelt.InnholdsHash);
        Assert.True(etterEnkelt.HentetTidspunkt > forEnkelt.HentetTidspunkt);

        var etterBokmal = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype && k.EksternId == BokmalPdfUrl);
        Assert.Equal(forBokmal.InnholdsHash, etterBokmal.InnholdsHash);
        Assert.Equal(forBokmal.HentetTidspunkt, etterBokmal.HentetTidspunkt);
    }

    /// <summary>
    /// De to siste radene i fixturen deler NØYAKTIG samme <c>tjenestenavn</c> ("Klage på
    /// forvaltningsvedtak", et ekte bokmål/nynorsk PDF-variant-par) men har ulik <c>url</c> — beviser at
    /// importøren bruker <c>url</c>, ikke <c>tjenestenavn</c>, som identitetsnøkkel.
    /// </summary>
    [Fact]
    public async Task To_url_distinkte_men_tjenestenavn_identiske_rader_importeres_som_separate_rader()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new StatsforvalterTjenesteHenter(db).ImporterAsync(FemTjenesterJson);

        var bokmal = await db.EksterneKilder.SingleAsync(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype && k.EksternId == BokmalPdfUrl);
        var nynorsk = await db.EksterneKilder.SingleAsync(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype && k.EksternId == NynorskPdfUrl);

        Assert.NotEqual(bokmal.Id, nynorsk.Id);
        Assert.Contains("Klage på forvaltningsvedtak", bokmal.RaaJson);
        Assert.Contains("Klage på forvaltningsvedtak", nynorsk.RaaJson);
        Assert.Contains("0073b_bokmal.pdf", bokmal.RaaJson);
        Assert.Contains("0073n_nynorsk.pdf", nynorsk.RaaJson);
    }

    private const string TilbyderMedTomtOrgnummerJson = """
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
    public async Task Tomt_orgnummer_i_tilbys_av_telles_og_behandles_ikke_som_gyldig_identifikator()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await new StatsforvalterTjenesteHenter(db).ImporterAsync(TilbyderMedTomtOrgnummerJson);

        Assert.Equal(1, resultat.Nye);
        Assert.Equal(1, resultat.TilbydereMedManglendeOrgnummer); // KUN den tomme telles, ikke den gyldige Agder-oppføringen

        // Raden importeres likevel i sin helhet, verbatim, inkludert den tomme strengen — ingen gjettet fallback,
        // kun synliggjøring av at kvalitetsavviket finnes.
        var rad = await db.EksterneKilder.SingleAsync(k => k.Kildetype == StatsforvalterTjenesteHenter.Kildetype);
        Assert.Contains("\"organisasjonsnummer\": \"\"", rad.RaaJson);
    }

    [Fact]
    public async Task Unik_indeks_hindrer_duplikat_pa_kildetype_og_ekstern_id()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = StatsforvalterTjenesteHenter.Kildetype, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "a", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = StatsforvalterTjenesteHenter.Kildetype, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "b", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
