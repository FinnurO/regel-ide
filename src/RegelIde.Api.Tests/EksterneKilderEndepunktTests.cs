using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for det nye, frittstående høstelaget (docs/13-backlog.md) — <c>/api/eksterne-kilder</c>.
/// Tabellen er GLOBAL (ingen virksomhet-scoping, samme mønster som Lovdata-katalogen) — hver test
/// nullstiller derfor eksplisitt tabellen selv.
/// <para>
/// POST-endepunktet bruker <see cref="OppgaveregisterHenter"/>, som har en typed <see cref="HttpClient"/>
/// mot det ekte Oppgaveregister-API-et. I motsetning til Lovdata-endepunktenes "ekte nettverkskall er
/// greit"-kultur ellers i prosjektet, krever DENNE oppgaven eksplisitt at test-suiten ikke skal avhenge
/// av nettverkstilgang — derfor overstyres <see cref="OppgaveregisterHenter"/>s <see cref="HttpMessageHandler"/>
/// via <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/>
/// i stedet for å la den treffe data.brreg.no.
/// </para>
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class EksterneKilderEndepunktTests
{
    private readonly EmbeddedPostgresApiFixture _fixture;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public EksterneKilderEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private sealed class StubHandler(string responsBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responsBody, Encoding.UTF8, "application/json") });
    }

    private const string ToSkjemaJson = """
    [
      { "navn": "Test-skjema én", "guid": "TESTA", "eier": { "organisasjonsnummer": 111111111, "etatsnavn": "TESTETATEN" } },
      { "navn": "Test-skjema to", "guid": "TESTB", "eier": { "organisasjonsnummer": 222222222, "etatsnavn": "TESTETATEN" } }
    ]
    """;

    [Fact]
    public async Task Post_hent_oppgaveregister_lagrer_skjemaene_og_returnerer_sammendrag()
    {
        await using (var db = _fixture.NyDbContext())
        {
            await db.EksterneKilder.ExecuteDeleteAsync();
        }

        using var factoryMedStub = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient<OppgaveregisterHenter>().ConfigurePrimaryHttpMessageHandler(() => new StubHandler(ToSkjemaJson))));
        using var klient = factoryMedStub.CreateClient();

        var svar = await klient.PostAsync("/api/eksterne-kilder/oppgaveregister/hent", content: null);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var resultat = await svar.Content.ReadFromJsonAsync<EksternKildeHostingResultatDto>(JsonInnstillinger);
        Assert.NotNull(resultat);
        Assert.Equal(2, resultat!.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);

        await using var etterDb = _fixture.NyDbContext();
        var antall = await etterDb.EksterneKilder.CountAsync(k => k.Kildetype == OppgaveregisterHenter.Kildetype);
        Assert.Equal(2, antall);
    }

    private const string ToRessurserJson = """
    [
      { "identifier": "app_test_en", "resourceType": "AltinnApp" },
      { "identifier": "test-maskinporten", "resourceType": "MaskinportenSchema" }
    ]
    """;

    [Fact]
    public async Task Post_hent_altinn_ressurser_lagrer_kun_altinnapp_og_returnerer_sammendrag()
    {
        await using (var db = _fixture.NyDbContext())
        {
            await db.EksterneKilder.ExecuteDeleteAsync();
        }

        using var factoryMedStub = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient<AltinnRessursHenter>().ConfigurePrimaryHttpMessageHandler(() => new StubHandler(ToRessurserJson))));
        using var klient = factoryMedStub.CreateClient();

        var svar = await klient.PostAsync("/api/eksterne-kilder/altinn-ressurser/hent", content: null);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var resultat = await svar.Content.ReadFromJsonAsync<EksternKildeHostingResultatDto>(JsonInnstillinger);
        Assert.NotNull(resultat);
        Assert.Equal(1, resultat!.Nye); // MaskinportenSchema er filtrert bort

        await using var etterDb = _fixture.NyDbContext();
        var antall = await etterDb.EksterneKilder.CountAsync(k => k.Kildetype == AltinnRessursHenter.Kildetype);
        Assert.Equal(1, antall);
        Assert.True(await etterDb.EksterneKilder.AnyAsync(k => k.Kildetype == AltinnRessursHenter.Kildetype && k.EksternId == "app_test_en"));
    }

    private sealed class HtmlStubHandler(IReadOnlyList<string> htmlSvar) : HttpMessageHandler
    {
        private int _kall;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = htmlSvar[Math.Min(_kall, htmlSvar.Count - 1)];
            _kall++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/html") });
        }
    }

    [Fact]
    public async Task Post_hent_altinn_skjemaoversikt_kryper_og_lagrer_en_tjenesteside()
    {
        await using (var db = _fixture.NyDbContext())
        {
            await db.EksterneKilder.ExecuteDeleteAsync();
        }

        const string indeksHtml = """<html><body><a href="/skjemaoversikt/advokattilsynet/">Advokattilsynet</a></body></html>""";
        const string etatHtml = """<html><body><a href="/skjemaoversikt/advokattilsynet/advokat/">Advokat</a></body></html>""";
        var tjenesteHtml = """<html><body><h1>Advokat</h1></body></html>""";

        using var factoryMedStub = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient<AltinnSkjemaoversiktHenter>()
                    .ConfigurePrimaryHttpMessageHandler(() => new HtmlStubHandler([indeksHtml, etatHtml, tjenesteHtml]))));
        using var klient = factoryMedStub.CreateClient();

        var svar = await klient.PostAsync("/api/eksterne-kilder/altinn-skjemaoversikt/hent", content: null);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var resultat = await svar.Content.ReadFromJsonAsync<EksternKildeHostingResultatDto>(JsonInnstillinger);
        Assert.NotNull(resultat);
        Assert.Equal(1, resultat!.Nye);

        await using var etterDb = _fixture.NyDbContext();
        var rad = await etterDb.EksterneKilder.SingleAsync(k => k.Kildetype == AltinnSkjemaoversiktHenter.Kildetype);
        Assert.Equal("/skjemaoversikt/advokattilsynet/advokat/", rad.EksternId);
        // Postgres' jsonb-kolonne normaliserer whitespace ved lagring/lesing (canonical form, mellomrom
        // etter kolon) — sammenlign derfor på det parsede innholdet, ikke rå tekstlikhet.
        using var raaJson = JsonDocument.Parse(rad.RaaJson);
        Assert.Equal("Advokat", raaJson.RootElement.GetProperty("tjeneste").GetString());
    }

    private const string TreStatsforvalterTjenesterJson = """
    [
      {
        "tjenestenavn": "Test-tjeneste én",
        "url": "https://example.test/statsforvalter/tjeneste-en",
        "tema": "Test",
        "beskrivelse": "Testtjeneste.",
        "tilbys_av": [ { "organisasjon": "Agder", "organisasjonsnummer": "974762994" } ]
      },
      {
        "tjenestenavn": "Test-tjeneste to",
        "url": "https://example.test/statsforvalter/tjeneste-to",
        "tema": "Test",
        "beskrivelse": "Testtjeneste med manglende orgnummer.",
        "tilbys_av": [
          { "organisasjon": "Ukjent embete", "organisasjonsnummer": "" },
          { "organisasjon": "Vestland", "organisasjonsnummer": "974760665" }
        ]
      }
    ]
    """;

    /// <summary>
    /// Statsforvalter-tjeneste-endepunktet er FIL-basert (ingen <c>HttpClient</c> i
    /// <see cref="TjenestelisteImporter"/>) — testen poster derfor den rå JSON-en direkte som
    /// request-body, ingen <see cref="HttpMessageHandler"/>-stub nødvendig.
    /// </summary>
    [Fact]
    public async Task Post_importer_statsforvalter_tjenester_lagrer_tjenestene_og_returnerer_sammendrag_med_manglende_orgnummer()
    {
        await using (var db = _fixture.NyDbContext())
        {
            await db.EksterneKilder.ExecuteDeleteAsync();
        }

        var svar = await _client.PostAsync(
            "/api/eksterne-kilder/statsforvalter-tjenester/importer",
            new StringContent(TreStatsforvalterTjenesterJson, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var resultat = await svar.Content.ReadFromJsonAsync<TjenestelisteHostingResultatDto>(JsonInnstillinger);
        Assert.NotNull(resultat);
        Assert.Equal(2, resultat!.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);
        Assert.Equal(1, resultat.TilbydereMedManglendeOrgnummer);

        await using var etterDb = _fixture.NyDbContext();
        var antall = await etterDb.EksterneKilder.CountAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter);
        Assert.Equal(2, antall);
        Assert.True(await etterDb.EksterneKilder.AnyAsync(k =>
            k.Kildetype == TjenestelisteImporter.Statsforvalter && k.EksternId == "https://example.test/statsforvalter/tjeneste-en"));
    }

    private const string ToFylkeskommuneTjenesterJson = """
    [
      {
        "tjenestenavn": "Test-dialogtjeneste én",
        "url": "https://example.test/fylkeskommune/dialogtjeneste-en",
        "kategori": "Test",
        "beskrivelse": "Testtjeneste.",
        "tilbys_av": [ { "organisasjon": "Test fylkeskommune", "organisasjonsnummer": "921707134" } ]
      },
      {
        "tjenestenavn": "Test-dialogtjeneste to",
        "url": "https://example.test/fylkeskommune/dialogtjeneste-to",
        "kategori": "Test",
        "beskrivelse": "Testtjeneste med manglende orgnummer.",
        "tilbys_av": [ { "organisasjon": "Ukjent fylkeskommune", "organisasjonsnummer": "" } ]
      }
    ]
    """;

    /// <summary>
    /// Samme mønster som Statsforvalter-testen over — fylkeskommune-dialogtjeneste-endepunktet deler
    /// implementasjon (<see cref="TjenestelisteImporter"/>) og er like FIL-basert, men skriver en annen
    /// <see cref="EksternKildeEntitet.Kildetype"/> (<see cref="TjenestelisteImporter.FylkeskommuneDialog"/>).
    /// </summary>
    [Fact]
    public async Task Post_importer_fylkeskommune_tjenester_lagrer_tjenestene_og_returnerer_sammendrag_med_manglende_orgnummer()
    {
        await using (var db = _fixture.NyDbContext())
        {
            await db.EksterneKilder.ExecuteDeleteAsync();
        }

        var svar = await _client.PostAsync(
            "/api/eksterne-kilder/fylkeskommune-tjenester/importer",
            new StringContent(ToFylkeskommuneTjenesterJson, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var resultat = await svar.Content.ReadFromJsonAsync<TjenestelisteHostingResultatDto>(JsonInnstillinger);
        Assert.NotNull(resultat);
        Assert.Equal(2, resultat!.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);
        Assert.Equal(1, resultat.TilbydereMedManglendeOrgnummer);

        await using var etterDb = _fixture.NyDbContext();
        var antall = await etterDb.EksterneKilder.CountAsync(k => k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog);
        Assert.Equal(2, antall);
        Assert.True(await etterDb.EksterneKilder.AnyAsync(k =>
            k.Kildetype == TjenestelisteImporter.FylkeskommuneDialog && k.EksternId == "https://example.test/fylkeskommune/dialogtjeneste-en"));

        // Beviser at de to fil-baserte endepunktene ikke lekker inn i hverandres kildetype selv om de
        // deler samme TjenestelisteImporter-instans.
        Assert.Equal(0, await etterDb.EksterneKilder.CountAsync(k => k.Kildetype == TjenestelisteImporter.Statsforvalter));
    }

    [Fact]
    public async Task Get_lister_hostede_kilder_paginert_og_filtrert_pa_kildetype()
    {
        await using (var db = _fixture.NyDbContext())
        {
            await db.EksterneKilder.ExecuteDeleteAsync();
            db.EksterneKilder.AddRange(
                new EksternKildeEntitet { Id = Guid.NewGuid(), Kildetype = "oppgaveregister_skjema", EksternId = "A", RaaJson = "{\"guid\":\"A\"}", InnholdsHash = "hash-a", HentetTidspunkt = DateTimeOffset.UtcNow },
                new EksternKildeEntitet { Id = Guid.NewGuid(), Kildetype = "oppgaveregister_skjema", EksternId = "B", RaaJson = "{\"guid\":\"B\"}", InnholdsHash = "hash-b", HentetTidspunkt = DateTimeOffset.UtcNow },
                new EksternKildeEntitet { Id = Guid.NewGuid(), Kildetype = "en_annen_kildetype", EksternId = "C", RaaJson = "{\"guid\":\"C\"}", InnholdsHash = "hash-c", HentetTidspunkt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var svar = await _client.GetFromJsonAsync<EksternKildeListeDto>(
            "/api/eksterne-kilder?kildetype=oppgaveregister_skjema", JsonInnstillinger);

        Assert.NotNull(svar);
        Assert.Equal(2, svar!.Totalt);
        Assert.Equal(2, svar.Kilder.Count);
        Assert.All(svar.Kilder, k => Assert.Equal("oppgaveregister_skjema", k.Kildetype));
        Assert.Contains(svar.Kilder, k => k.EksternId == "A");
        Assert.Contains(svar.Kilder, k => k.EksternId == "B");
        Assert.DoesNotContain(svar.Kilder, k => k.EksternId == "C");
    }

    [Fact]
    public async Task Get_uten_kildetype_lister_alle_og_respekterer_start_og_antall()
    {
        await using (var db = _fixture.NyDbContext())
        {
            await db.EksterneKilder.ExecuteDeleteAsync();
            db.EksterneKilder.AddRange(
                new EksternKildeEntitet { Id = Guid.NewGuid(), Kildetype = "oppgaveregister_skjema", EksternId = "A", RaaJson = "{}", InnholdsHash = "hash-a", HentetTidspunkt = DateTimeOffset.UtcNow },
                new EksternKildeEntitet { Id = Guid.NewGuid(), Kildetype = "oppgaveregister_skjema", EksternId = "B", RaaJson = "{}", InnholdsHash = "hash-b", HentetTidspunkt = DateTimeOffset.UtcNow },
                new EksternKildeEntitet { Id = Guid.NewGuid(), Kildetype = "en_annen_kildetype", EksternId = "C", RaaJson = "{}", InnholdsHash = "hash-c", HentetTidspunkt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var forsteSide = await _client.GetFromJsonAsync<EksternKildeListeDto>("/api/eksterne-kilder?start=0&antall=2", JsonInnstillinger);
        Assert.NotNull(forsteSide);
        Assert.Equal(3, forsteSide!.Totalt);
        Assert.Equal(2, forsteSide.Kilder.Count);

        var andreSide = await _client.GetFromJsonAsync<EksternKildeListeDto>("/api/eksterne-kilder?start=2&antall=2", JsonInnstillinger);
        Assert.NotNull(andreSide);
        Assert.Equal(3, andreSide!.Totalt);
        Assert.Single(andreSide.Kilder);
    }
}
