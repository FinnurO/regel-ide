using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for Hendelse-/Tjenesteavhengighetregisteret (docs/03-domenemodell.md §1.5,
/// docs/13-backlog.md §2.1, 2026-07-31) — kjører mot ekte embedded Postgres. Bruker egne, unike
/// tjeneste-/hendelsesnavn for å unngå kollisjon med Program.cs' oppstartsseeding.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class HendelseOgTjenesteavhengighetEndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public HendelseOgTjenesteavhengighetEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> HentJuristIdAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist").Id;
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<Guid> OpprettTjenesteAsync(Guid brukerId, string tittel)
    {
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester", brukerId,
            new { Tittel = tittel, Beskrivelse = (string?)null, KompetentMyndighet = (string?)null, Output = (string?)null,
                Tjenestetype = (string?)null, Malgruppe = (string?)null, Kanaler = Array.Empty<string>(), Kostnad = (string?)null,
                Behandlingstid = (string?)null, Kontaktpunkt = (string?)null, KonsekvensVedBrudd = (string?)null, Sprak = Array.Empty<string>() }));
        svar.EnsureSuccessStatusCode();
        var tjeneste = await svar.Content.ReadFromJsonAsync<TjenesteDto>(JsonInnstillinger);
        return tjeneste!.Id;
    }

    [Fact]
    public async Task Oppretter_og_lister_hendelse()
    {
        var juristId = await HentJuristIdAsync();
        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/hendelser", juristId,
            new HendelseRequest("Test-hendelse §1.5", "generell", null)));
        opprettSvar.EnsureSuccessStatusCode();

        var listeSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/hendelser", juristId));
        var liste = await listeSvar.Content.ReadFromJsonAsync<List<HendelseDto>>(JsonInnstillinger);
        Assert.Contains(liste!, h => h.Navn == "Test-hendelse §1.5");
    }

    [Fact]
    public async Task Kobler_tjeneste_til_hendelse_og_lister_symmetrisk()
    {
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await OpprettTjenesteAsync(juristId, "Test-tjeneste-hendelse-A");

        var hendelseSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/hendelser", juristId,
            new HendelseRequest("Test-kontroll/tilsyn", "virksomhetshendelse", null)));
        var hendelse = await hendelseSvar.Content.ReadFromJsonAsync<HendelseDto>(JsonInnstillinger);

        var kobleSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{tjenesteId}/hendelser", juristId,
            new KobleHendelseRequest(hendelse!.Id)));
        kobleSvar.EnsureSuccessStatusCode();

        var forTjeneste = await (await _client.SendAsync(MedBruker(HttpMethod.Get, $"/api/tjenester/{tjenesteId}/hendelser", juristId)))
            .Content.ReadFromJsonAsync<List<HendelseDto>>(JsonInnstillinger);
        Assert.Contains(forTjeneste!, h => h.Id == hendelse.Id);

        var fjernSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/tjenester/{tjenesteId}/hendelser/{hendelse.Id}", juristId));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, fjernSvar.StatusCode);
    }

    [Fact]
    public async Task Oppretter_tjenesteavhengighet_og_henter_riktig_visningstekst_fra_begge_sider()
    {
        var juristId = await HentJuristIdAsync();
        var serveringsbevilling = await OpprettTjenesteAsync(juristId, "Test-serveringsbevilling-avh");
        var skjenkebevilling = await OpprettTjenesteAsync(juristId, "Test-skjenkebevilling-avh");

        var opprettSvar = await _client.SendAsync(MedBruker(
            HttpMethod.Post, $"/api/tjenester/{serveringsbevilling}/avhengigheter", juristId,
            new TjenesteavhengighetRequest(skjenkebevilling, "forutsetning_for", null, null)));
        opprettSvar.EnsureSuccessStatusCode();

        var fraSiden = await (await _client.SendAsync(MedBruker(HttpMethod.Get, $"/api/tjenester/{serveringsbevilling}/avhengigheter", juristId)))
            .Content.ReadFromJsonAsync<List<TjenesteavhengighetDto>>(JsonInnstillinger);
        var visning = Assert.Single(fraSiden!);
        Assert.Equal("fra", visning.Retning);
        Assert.Equal($"er forutsetning for Test-skjenkebevilling-avh", visning.Visningstekst);

        var tilSiden = await (await _client.SendAsync(MedBruker(HttpMethod.Get, $"/api/tjenester/{skjenkebevilling}/avhengigheter", juristId)))
            .Content.ReadFromJsonAsync<List<TjenesteavhengighetDto>>(JsonInnstillinger);
        var visningTil = Assert.Single(tilSiden!);
        Assert.Equal("til", visningTil.Retning);
        Assert.Equal("krever Test-serveringsbevilling-avh", visningTil.Visningstekst);

        var slettSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/tjenester/avhengigheter/{visning.Id}", juristId));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, slettSvar.StatusCode);
    }

    [Fact]
    public async Task Ugyldig_rel_gir_400()
    {
        var juristId = await HentJuristIdAsync();
        var a = await OpprettTjenesteAsync(juristId, "Test-A-ugyldig-rel");
        var b = await OpprettTjenesteAsync(juristId, "Test-B-ugyldig-rel");

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{a}/avhengigheter", juristId,
            new TjenesteavhengighetRequest(b, "ukjent_rel", null, null)));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, svar.StatusCode);
    }
}
