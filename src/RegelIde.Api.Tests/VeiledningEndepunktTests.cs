using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Tjenestesentrert veiledning fra vilkårstreet (docs/12-fasit-handbok-leveranse.md "Hovedfunn",
/// 2026-07-30) — kjører mot ekte embedded Postgres, med Program.cs sin egen oppstartsseeding
/// (inkl. KommunaleParametreSeed) allerede kjørt før noen test starter.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class VeiledningEndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public VeiledningEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId) =>
        new(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };

    private async Task<Guid> HentTjenesteIdAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        var bruker = brukere!.Single(b => b.Rolle == "Jurist");
        var respons = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester", bruker.Id));
        var tjenester = await respons.Content.ReadFromJsonAsync<List<TjenesteDto>>(JsonInnstillinger);
        return tjenester!.Single(t => t.Tittel == "Alminnelig skjenkebevilling").Id;
    }

    private async Task<Guid> HentVirksomhetIdAsync(string navn)
    {
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);
        return virksomheter!.Single(v => v.Navn == navn).Id;
    }

    [Fact]
    public async Task Veiledningen_folger_beslutningsrekkefolgen_fra_referansemodellen()
    {
        var tjenesteId = await HentTjenesteIdAsync();

        var veiledning = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);

        Assert.Equal("Vedtak om skjenkebevilling", veiledning!.Rot.Tittel);
        Assert.Equal("regelnode", veiledning.Rot.Type);
        Assert.Equal(3, veiledning.Rot.Barn.Count); // V-ALDER, V-VANDEL, R-SKJENKETID, i Rekkefolge
        Assert.Equal("Aldersvilkår", veiledning.Rot.Barn[0].Tittel);
        Assert.Equal("Vandelsvilkår", veiledning.Rot.Barn[1].Tittel);
        var rSkjenketid = veiledning.Rot.Barn[2];
        Assert.Equal("regelnode", rSkjenketid.Type);
        Assert.Single(rSkjenketid.Unntak);
        Assert.Equal("Unntak for lukket selskap", rSkjenketid.Unntak[0].Tittel);
    }

    [Fact]
    public async Task Kommune_spesifikk_verdi_brukes_nar_virksomhet_er_registrert()
    {
        var tjenesteId = await HentTjenesteIdAsync();
        var tonsbergId = await HentVirksomhetIdAsync("Tønsberg kommune");

        var veiledning = await _client.GetFromJsonAsync<VeiledningDto>(
            $"/api/tjenester/{tjenesteId}/veiledning?virksomhetId={tonsbergId}", JsonInnstillinger);

        var klokkeslettsvilkar = veiledning!.Rot.Barn[2].Barn.Single(b => b.Tittel == "Klokkeslettsvilkår");
        var verdi = Assert.Single(klokkeslettsvilkar.InputDatasettVerdier);
        Assert.False(verdi.ErStandardverdi);
        Assert.Contains("08:00", verdi.VerdiJson);
    }

    [Fact]
    public async Task Faller_tilbake_til_standardverdi_uten_virksomhetid()
    {
        var tjenesteId = await HentTjenesteIdAsync();

        var veiledning = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);

        var klokkeslettsvilkar = veiledning!.Rot.Barn[2].Barn.Single(b => b.Tittel == "Klokkeslettsvilkår");
        var verdi = Assert.Single(klokkeslettsvilkar.InputDatasettVerdier);
        Assert.True(verdi.ErStandardverdi);
        Assert.Contains("08:00–01:00", verdi.VerdiJson);
    }

    [Fact]
    public async Task Ukjent_tjeneste_gir_404()
    {
        var respons = await _client.GetAsync($"/api/tjenester/{Guid.NewGuid()}/veiledning");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, respons.StatusCode);
    }
}
