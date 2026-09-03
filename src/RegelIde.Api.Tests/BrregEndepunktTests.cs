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

    /// <summary>
    /// [Ny, issue #158] "Alle virksomheter må jo ha en navneform" (Johann) — ved et BEKREFTET SNL-treff
    /// skal <c>fra-brreg</c> automatisk opprette en navneform (<see cref="BegrepEntitet"/>,
    /// Begrepskategori="virksomhet") med SNLs egen normalt skrevne form, MENS selve
    /// <see cref="Virksomhet.Navn"/> beholder Brregs rå VERSAL-form UENDRET (den er autoritativ —
    /// Johanns egen korreksjon i issuets kommentarfelt, IKKE en feil å rette).
    /// </summary>
    [Fact]
    public async Task Fra_brreg_oppretter_automatisk_en_navneform_ved_bekreftet_snl_treff()
    {
        const string orgnr = "912340101";
        var enhetJson = StatpedJson.Replace("974761084", orgnr).Replace("STATPED", "MILJØDIREKTORATET");

        var factoryMedStub = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient<BrregKlient>().ConfigurePrimaryHttpMessageHandler(() => new RutetStubHandler(
                    new Dictionary<string, (HttpStatusCode Status, string Body)>
                    {
                        [$"/enheter/{orgnr}"] = (HttpStatusCode.OK, enhetJson),
                    }));
                services.AddHttpClient<EksternNavneoppslagTjeneste>().ConfigurePrimaryHttpMessageHandler(() => new SnlTreffHandler());
            }));
        using var klient = factoryMedStub.CreateClient();

        var svar = await klient.PostAsJsonAsync("/api/virksomheter/fra-brreg", new OpprettVirksomhetFraBrregRequest(orgnr));
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var opprettet = await svar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger);

        // Navn UENDRET i Brregs rå VERSAL-form — LÅST prinsipp, se Program.cs-kommentaren på endepunktet.
        Assert.Equal("MILJØDIREKTORATET", opprettet!.Navn);

        await using var db = _fixture.NyDbContext();
        var navneform = await db.Begreper.SingleOrDefaultAsync(
            b => b.VirksomhetReferanseId == opprettet.Id && b.Begrepskategori == "virksomhet");
        Assert.NotNull(navneform);
        Assert.Equal("Miljødirektoratet", navneform!.Term);
        Assert.Equal("brreg-import", navneform.OpprettetAv);
    }

    /// <summary>[Ny, issue #158] Motsatt gren: ingen SNL-bekreftelse ⇒ INGEN navneform opprettes — ingen
    /// gjettet/algoritmisk versalisering av Brreg-strengen som fallback (Johanns eksplisitte krav).</summary>
    [Fact]
    public async Task Fra_brreg_oppretter_ingen_navneform_uten_bekreftet_snl_treff()
    {
        const string orgnr = "912340102";
        var enhetJson = StatpedJson.Replace("974761084", orgnr).Replace("STATPED", "HELT UKJENT VIRKSOMHET AS");

        var factoryMedStub = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient<BrregKlient>().ConfigurePrimaryHttpMessageHandler(() => new RutetStubHandler(
                    new Dictionary<string, (HttpStatusCode Status, string Body)>
                    {
                        [$"/enheter/{orgnr}"] = (HttpStatusCode.OK, enhetJson),
                    }));
                // Standardstubben på selve Factory-en (IngenEksternTreffHandler) svarer allerede "ingen
                // treff" — ingen egen SNL-overstyring nødvendig her, akkurat poenget med denne testen.
            }));
        using var klient = factoryMedStub.CreateClient();

        var svar = await klient.PostAsJsonAsync("/api/virksomheter/fra-brreg", new OpprettVirksomhetFraBrregRequest(orgnr));
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var opprettet = await svar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger);

        await using var db = _fixture.NyDbContext();
        var navneform = await db.Begreper.SingleOrDefaultAsync(
            b => b.VirksomhetReferanseId == opprettet!.Id && b.Begrepskategori == "virksomhet");
        Assert.Null(navneform);
    }

    /// <summary>Stub for <see cref="EksternNavneoppslagTjeneste"/> — ETT bekreftet organisasjonstreff
    /// ("Miljødirektoratet", samme eksempel som EksternNavneoppslagTjeneste sin egen klassekommentar
    /// bruker for SNLs <c>article_type_id 16</c>), uansett hvilken term som faktisk slås opp.</summary>
    private sealed class SnlTreffHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            string body;
            if (url.Contains("/api/v1/search"))
            {
                body = """
                [{ "article_type_id": 16, "taxonomy_title": "Offentlige etater og direktorater",
                   "article_url": "https://snl.no/Miljodirektoratet",
                   "article_url_json": "https://snl.no/Miljodirektoratet.json" }]
                """;
            }
            else if (url.EndsWith("Miljodirektoratet.json"))
            {
                body = """
                { "headword": "Miljødirektoratet", "url": "https://snl.no/Miljodirektoratet",
                  "metadata": { "organization_name": "Miljødirektoratet" } }
                """;
            }
            else
            {
                body = "[]";
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
