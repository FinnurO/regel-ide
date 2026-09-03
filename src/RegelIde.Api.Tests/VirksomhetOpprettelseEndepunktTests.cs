using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, 2026-08-30, brukertilbakemelding] <c>POST /api/virksomheter</c> — oppretter en virksomhet med
/// KUN navn (ingen org.nummer), motstykket til <c>/api/virksomheter/fra-brreg</c> for aktører uten
/// egen Brreg-registrering (f.eks. Kystvakten, som er del av Forsvaret).
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class VirksomhetOpprettelseEndepunktTests
{
    private readonly EmbeddedPostgresApiFixture _fixture;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public VirksomhetOpprettelseEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Oppretter_virksomhet_med_kun_navn_uten_overordnet_enhet()
    {
        var svar = await _client.PostAsJsonAsync("/api/virksomheter", new OpprettVirksomhetRequest("Kystvakten (test)", null));
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);

        var opprettet = await svar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger);
        Assert.NotNull(opprettet);
        Assert.Equal("Kystvakten (test)", opprettet!.Navn);
        Assert.Null(opprettet.Organisasjonsnummer);
        Assert.Null(opprettet.Forvaltningsniva);
        Assert.Null(opprettet.OverordnetEnhetId);
        Assert.True(opprettet.Aktiv);
    }

    [Fact]
    public async Task Oppretter_virksomhet_knyttet_til_en_eksisterende_overordnet_enhet()
    {
        Guid forsvaretId;
        await using (var db = _fixture.NyDbContext())
        {
            forsvaretId = Guid.NewGuid();
            db.Virksomheter.Add(new Virksomhet { Id = forsvaretId, Navn = "Forsvaret (test)" });
            await db.SaveChangesAsync();
        }

        var svar = await _client.PostAsJsonAsync("/api/virksomheter", new OpprettVirksomhetRequest("Kystvakten, del av Forsvaret (test)", forsvaretId));
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);

        var opprettet = await svar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger);
        Assert.Equal(forsvaretId, opprettet!.OverordnetEnhetId);

        await using var db2 = _fixture.NyDbContext();
        var rad = await db2.Virksomheter.SingleAsync(v => v.Id == opprettet.Id);
        Assert.Equal(forsvaretId, rad.OverordnetEnhetId);
    }

    [Fact]
    public async Task Avviser_tomt_navn()
    {
        var svar = await _client.PostAsJsonAsync("/api/virksomheter", new OpprettVirksomhetRequest("   ", null));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Avviser_overordnet_enhet_som_ikke_finnes_ingen_gjettet_fallback()
    {
        var svar = await _client.PostAsJsonAsync("/api/virksomheter", new OpprettVirksomhetRequest("Test", Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    /// <summary>
    /// [Ny, issue #194] Samme mekanisme som <c>fra-brreg</c> (issue #158, se
    /// <see cref="BrregEndepunktTests.Fra_brreg_oppretter_automatisk_en_navneform_ved_bekreftet_snl_treff"/>)
    /// — den manuelle "kun navn"-opprettelsen gjorde tidligere INGEN SNL-oppslag i det hele tatt. Bruker
    /// den delte fixturens standardstub (<c>AltErInstitusjonHandler</c>, se
    /// <see cref="EmbeddedPostgresApiFixture"/>), som bekrefter ethvert navn som slås opp — ingen egen
    /// <c>WithWebHostBuilder</c>-override nødvendig for selve treff-tilfellet.
    /// </summary>
    [Fact]
    public async Task Oppretter_automatisk_en_navneform_ved_bekreftet_snl_treff()
    {
        var svar = await _client.PostAsJsonAsync("/api/virksomheter", new OpprettVirksomhetRequest("Kystvakten SNL-test", null));
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var opprettet = await svar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger);
        Assert.NotNull(opprettet);

        await using var db = _fixture.NyDbContext();
        var navneform = await db.Begreper.SingleOrDefaultAsync(
            b => b.VirksomhetReferanseId == opprettet!.Id && b.Begrepskategori == "virksomhet");
        Assert.NotNull(navneform);
        Assert.Equal("Kystvakten SNL-test", navneform!.Term);
        Assert.Equal("manuell-opprettelse", navneform.OpprettetAv);
        Assert.NotNull(navneform.SkosUrl);
    }

    /// <summary>[Ny, issue #194] Motsatt gren: ingen SNL-bekreftelse ⇒ INGEN navneform opprettes — samme
    /// "ingen gjettet/algoritmisk fallback"-prinsipp som fra-brreg-veien. Egen
    /// <c>WithWebHostBuilder</c>-override av <see cref="EksternNavneoppslagTjeneste"/> siden den delte
    /// fixturens standardstub bekrefter ethvert navn (se testen over) — må eksplisitt overstyres til
    /// "ingen treff" for at dette scenarioet skal bety noe.</summary>
    [Fact]
    public async Task Oppretter_ingen_navneform_uten_bekreftet_snl_treff()
    {
        using var factoryUtenTreff = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient<EksternNavneoppslagTjeneste>()
                    .ConfigurePrimaryHttpMessageHandler(() => new IngenSnlTreffHandler())));
        using var klientUtenTreff = factoryUtenTreff.CreateClient();

        var svar = await klientUtenTreff.PostAsJsonAsync("/api/virksomheter", new OpprettVirksomhetRequest("Helt ukjent aktør (test)", null));
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var opprettet = await svar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger);

        await using var db = _fixture.NyDbContext();
        var navneform = await db.Begreper.SingleOrDefaultAsync(
            b => b.VirksomhetReferanseId == opprettet!.Id && b.Begrepskategori == "virksomhet");
        Assert.Null(navneform);
    }

    /// <summary>Samme "ingen treff"-stub som <see cref="BrregEndepunktTests.IngenSnlTreffHandler"/> —
    /// duplisert lokalt fremfor å gjøre den private klassen der delt, siden begge testklassene allerede
    /// bygger sine egne små stub-handlere fremfor et delt testbibliotek.</summary>
    private sealed class IngenSnlTreffHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
