using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// docs/13-backlog.md §9 — <c>/api/virksomheter/brreg-sok</c> og <c>/api/virksomheter/fra-brreg</c>.
/// Samme <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/>-
/// overstyring av <see cref="BrregKlient"/>s <see cref="HttpMessageHandler"/> som
/// <see cref="EksterneKilderEndepunktTests"/> bruker for <see cref="OppgaveregisterHenter"/> — ingen
/// ekte nettverkskall mot data.brreg.no i test-suiten.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class BrregEndepunktTests
{
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public BrregEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class RutetStubHandler(IReadOnlyDictionary<string, (HttpStatusCode Status, string Body)> ruter) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var sti = request.RequestUri!.AbsolutePath + request.RequestUri.Query;
            var treff = ruter.FirstOrDefault(r => sti.Contains(r.Key));
            var (status, body) = treff.Value == default ? (HttpStatusCode.NotFound, "{}") : treff.Value;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private HttpClient LagKlientMedStub(IReadOnlyDictionary<string, (HttpStatusCode, string)> ruter)
    {
        var factoryMedStub = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient<BrregKlient>().ConfigurePrimaryHttpMessageHandler(() => new RutetStubHandler(ruter))));
        return factoryMedStub.CreateClient();
    }

    private const string StatpedJson = """
    {
      "organisasjonsnummer": "974761084",
      "navn": "STATPED",
      "organisasjonsform": { "kode": "ORGL", "beskrivelse": "Organisasjonsledd" },
      "institusjonellSektorkode": { "kode": "6100", "beskrivelse": "Statsforvaltningen" },
      "hjemmeside": "www.statped.no"
    }
    """;

    [Fact]
    public async Task Brreg_sok_proxyer_treff_fra_brreg()
    {
        using var klient = LagKlientMedStub(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/enheter?"] = (HttpStatusCode.OK, $$"""{ "_embedded": { "enheter": [{{StatpedJson}}] } } """),
        });

        var svar = await klient.GetAsync("/api/virksomheter/brreg-sok?q=Statped");
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var treff = await svar.Content.ReadFromJsonAsync<List<BrregEnhetDto>>(JsonInnstillinger);
        var enhet = Assert.Single(treff!);
        Assert.Equal("974761084", enhet.Organisasjonsnummer);
        Assert.Equal("STATPED", enhet.Navn);
    }

    [Fact]
    public async Task Brreg_sok_uten_soketekst_gir_tom_liste_uten_a_kalle_brreg()
    {
        var kallteBrreg = false;
        using var factoryMedStub = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient<BrregKlient>().ConfigurePrimaryHttpMessageHandler(() =>
                    new TellendeHandler(() => kallteBrreg = true))));
        using var klient = factoryMedStub.CreateClient();

        var svar = await klient.GetAsync("/api/virksomheter/brreg-sok?q=");
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var treff = await svar.Content.ReadFromJsonAsync<List<BrregEnhetDto>>(JsonInnstillinger);
        Assert.Empty(treff!);
        Assert.False(kallteBrreg);
    }

    private sealed class TellendeHandler(Action pakall) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            pakall();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task Fra_brreg_oppretter_ny_virksomhet_med_feltene_fra_brreg_men_uten_forvaltningsniva()
    {
        using var klient = LagKlientMedStub(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/enheter/974761084"] = (HttpStatusCode.OK, StatpedJson),
        });

        var svar = await klient.PostAsJsonAsync("/api/virksomheter/fra-brreg", new OpprettVirksomhetFraBrregRequest("974761084"));
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);

        var opprettet = await svar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger);
        Assert.NotNull(opprettet);
        Assert.Equal("STATPED", opprettet!.Navn);
        Assert.Equal("974761084", opprettet.Organisasjonsnummer);
        Assert.Equal("ORGL", opprettet.OrganisasjonsformKode);
        Assert.Equal("6100", opprettet.Sektorkode);
        // [LÅST, docs/20 §4/§7.2] Aldri gjettet fra Brreg-data — se Program.cs-kommentaren på selve endepunktet.
        Assert.Null(opprettet.Forvaltningsniva);
        Assert.NotNull(opprettet.SistBrregSynkronisert);

        await using var db = _fixture.NyDbContext();
        var rad = await db.Virksomheter.SingleAsync(v => v.Organisasjonsnummer == "974761084");
        Assert.Equal("STATPED", rad.Navn);
        Assert.True(rad.Aktiv);
        var nettside = await db.VirksomhetNettsider.SingleOrDefaultAsync(n => n.VirksomhetId == rad.Id);
        Assert.NotNull(nettside);
        Assert.Equal("www.statped.no", nettside!.Url);
        Assert.Equal("Hovedside", nettside.Type);
    }

    [Fact]
    public async Task Fra_brreg_nekter_a_opprette_duplikat_organisasjonsnummer()
    {
        // Eget, oppdiktet orgnr — DELT DB på tvers av tester i samme samling (ApiTestCollection), så
        // gjenbruk av Statped-orgnr-et fra testen over kunne race mot rekkefølgen testene faktisk
        // kjører i (bekreftet ved en reell, flaky Conflict-feil i den andre testen første kjøring).
        const string eget = "912340001";
        var duplikatJson = StatpedJson.Replace("974761084", eget);
        Guid virksomhetId;
        await using (var db = _fixture.NyDbContext())
        {
            virksomhetId = Guid.NewGuid();
            db.Virksomheter.Add(new Virksomhet { Id = virksomhetId, Navn = "Allerede i katalogen", Organisasjonsnummer = eget });
            await db.SaveChangesAsync();
        }

        using var klient = LagKlientMedStub(new Dictionary<string, (HttpStatusCode, string)>
        {
            [$"/enheter/{eget}"] = (HttpStatusCode.OK, duplikatJson),
        });

        var svar = await klient.PostAsJsonAsync("/api/virksomheter/fra-brreg", new OpprettVirksomhetFraBrregRequest(eget));

        Assert.Equal(HttpStatusCode.Conflict, svar.StatusCode);
        await using var etterDb = _fixture.NyDbContext();
        Assert.Equal(1, await etterDb.Virksomheter.CountAsync(v => v.Organisasjonsnummer == eget));
    }

    [Fact]
    public async Task Fra_brreg_gir_404_nar_brreg_ikke_finner_organisasjonsnummeret()
    {
        using var klient = LagKlientMedStub(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/enheter/000000000"] = (HttpStatusCode.NotFound, "{}"),
            ["/underenheter/000000000"] = (HttpStatusCode.NotFound, "{}"),
        });

        var svar = await klient.PostAsJsonAsync("/api/virksomheter/fra-brreg", new OpprettVirksomhetFraBrregRequest("000000000"));

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }
}
