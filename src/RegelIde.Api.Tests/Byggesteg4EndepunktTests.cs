using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for Vilkårstreet (byggesteg 4 runde 1, 2026-07-30) — kjører mot ekte embedded
/// Postgres. Program.cs' egen oppstartsseeding (Byggesteg4VilkarstreSeed) har allerede bygget hele
/// treet fra docs/01-referansemodell.md §5.5 før noen test kjører.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class Byggesteg4EndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Byggesteg4EndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    [Fact]
    public async Task Hent_datasett_uten_bruker_header_fungerer_apne_data()
    {
        var datasett = await _client.GetFromJsonAsync<List<DatasettDto>>("/api/datasett", JsonInnstillinger);
        Assert.Contains(datasett!, d => d.Prop == "styrer.fodselsdato");
        Assert.Contains(datasett!, d => d.Prop == "arrangement.er_lukket_selskap");
    }

    [Fact]
    public async Task Hent_vilkar_viser_de_seedede_fra_referansemodellen()
    {
        var bruker = await HentTestbrukerAsync();
        var vilkar = await (await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/vilkar", bruker.Id)))
            .Content.ReadFromJsonAsync<List<VilkarDto>>(JsonInnstillinger);

        Assert.Contains(vilkar!, v => v.Tittel == "Aldersvilkår");
        Assert.Contains(vilkar!, v => v.Tittel == "Vandelsvilkår" && v.Vurderingstype == "skjonnsbasert");
    }

    [Fact]
    public async Task Hent_regelnoder_viser_rotnoden()
    {
        var bruker = await HentTestbrukerAsync();
        var regelnoder = await (await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/regelnoder", bruker.Id)))
            .Content.ReadFromJsonAsync<List<RegelnodeDto>>(JsonInnstillinger);

        var rotnode = regelnoder!.Single(r => r.Tittel == "Vedtak om skjenkebevilling");
        Assert.True(rotnode.ErRotnode);
    }

    [Fact]
    public async Task Rotnoden_har_tre_barn_inkludert_en_nestet_regelnode()
    {
        var bruker = await HentTestbrukerAsync();
        var regelnoder = await (await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/regelnoder", bruker.Id)))
            .Content.ReadFromJsonAsync<List<RegelnodeDto>>(JsonInnstillinger);
        var rotnode = regelnoder!.Single(r => r.Tittel == "Vedtak om skjenkebevilling");

        var barn = await _client.GetFromJsonAsync<List<RegelnodeBarnDto>>($"/api/regelnoder/{rotnode.Id}/barn", JsonInnstillinger);

        Assert.Equal(3, barn!.Count);
        Assert.Single(barn, b => b.BarnType == "regelnode");
    }

    [Fact]
    public async Task Hent_unntak_viser_det_seedede()
    {
        var bruker = await HentTestbrukerAsync();
        var unntak = await (await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/unntak", bruker.Id)))
            .Content.ReadFromJsonAsync<List<UnntakDto>>(JsonInnstillinger);

        Assert.Contains(unntak!, u => u.Tittel == "Unntak for lukket selskap");
    }

    [Fact]
    public async Task Tjenesten_har_fatt_rotnode_satt_av_seedingen()
    {
        var bruker = await HentTestbrukerAsync();
        var tjenester = await (await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester", bruker.Id)))
            .Content.ReadFromJsonAsync<List<TjenesteDto>>(JsonInnstillinger);
        var tjeneste = tjenester!.Single(t => t.Tittel == "Alminnelig skjenkebevilling");

        Assert.NotNull(tjeneste.RotnodeId);
    }

    [Fact]
    public async Task Oppretter_oppdaterer_og_endrer_status_pa_vilkar()
    {
        var bruker = await HentTestbrukerAsync();
        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/vilkar", bruker.Id,
            new VilkarRequest("API-test-vilkår", null, null, "formell", null, null, null, "regelbasert", null,
                null, null, false, null, null, null, false, null)));
        Assert.Equal(HttpStatusCode.Created, opprettSvar.StatusCode);
        var vilkar = await opprettSvar.Content.ReadFromJsonAsync<VilkarDto>(JsonInnstillinger);
        Assert.Equal("utkast", vilkar!.Status);

        var oppdaterSvar = await _client.SendAsync(MedBruker(HttpMethod.Put, $"/api/vilkar/{vilkar.Id}", bruker.Id,
            new VilkarRequest("API-test-vilkår v2", "Ny beskrivelse", null, "formell", null, null, null, "regelbasert",
                null, null, null, false, null, null, null, false, null)));
        var oppdatert = await oppdaterSvar.Content.ReadFromJsonAsync<VilkarDto>(JsonInnstillinger);
        Assert.Equal("API-test-vilkår v2", oppdatert!.Tittel);

        var statusSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/vilkar/{vilkar.Id}/status", bruker.Id,
            new SettStatusRequest("validert")));
        var medStatus = await statusSvar.Content.ReadFromJsonAsync<VilkarDto>(JsonInnstillinger);
        Assert.Equal("validert", medStatus!.Status);

        var historikkSvar = await _client.GetFromJsonAsync<List<ProveniensDto>>($"/api/vilkar/{vilkar.Id}/historikk", JsonInnstillinger);
        Assert.Contains(historikkSvar!, p => p.Handling == "opprettet");
    }

    [Fact]
    public async Task Skjonnsbasert_vilkar_uten_skjonnsgrunnlag_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/vilkar", bruker.Id,
            new VilkarRequest("Skjønnstest", null, null, "materiell", null, null, null, "skjonnsbasert", null,
                null, null, false, null, null, null, false, null)));

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Kobling_som_skaper_sykel_gir_400_med_forklaring()
    {
        var bruker = await HentTestbrukerAsync();
        var a = await (await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/regelnoder", bruker.Id,
            new RegelnodeRequest("Sykeltest A", null, null, "OG", "Utfall", "boolean", false, null, null, null))))
            .Content.ReadFromJsonAsync<RegelnodeDto>(JsonInnstillinger);
        var b = await (await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/regelnoder", bruker.Id,
            new RegelnodeRequest("Sykeltest B", null, null, "OG", "Utfall", "boolean", false, null, null, null))))
            .Content.ReadFromJsonAsync<RegelnodeDto>(JsonInnstillinger);

        var kobleSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/regelnoder/{a!.Id}/barn", bruker.Id,
            new KobleBarnRequest("regelnode", b!.Id)));
        Assert.Equal(HttpStatusCode.Created, kobleSvar.StatusCode);

        var sykelSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/regelnoder/{b.Id}/barn", bruker.Id,
            new KobleBarnRequest("regelnode", a.Id)));

        Assert.Equal(HttpStatusCode.BadRequest, sykelSvar.StatusCode);
        var feil = await sykelSvar.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("sykel", feil.GetProperty("feil").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Oppretter_unntak_med_manglende_gjelder_regel_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/unntak", bruker.Id,
            new OpprettUnntakRequest("Ugyldig unntak", null, Guid.NewGuid(), "vilkar", Guid.NewGuid(), null)));

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Setter_operator_pa_regelnode()
    {
        var bruker = await HentTestbrukerAsync();
        var regelnode = await (await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/regelnoder", bruker.Id,
            new RegelnodeRequest("Operatortest", null, null, "OG", "Utfall", "boolean", false, null, null, null))))
            .Content.ReadFromJsonAsync<RegelnodeDto>(JsonInnstillinger);

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Put, $"/api/regelnoder/{regelnode!.Id}/operator", bruker.Id,
            new SettOperatorRequest("ELLER")));
        var oppdatert = await svar.Content.ReadFromJsonAsync<RegelnodeDto>(JsonInnstillinger);

        Assert.Equal("ELLER", oppdatert!.BarnOperator);
    }
}
