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
