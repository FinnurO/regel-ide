using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
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
}
