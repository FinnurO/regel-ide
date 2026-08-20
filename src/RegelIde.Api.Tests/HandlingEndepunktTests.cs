using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>Integrasjonstester for Handling-endepunktene (2026-08-20), mot ekte embedded Postgres.</summary>
[Collection(ApiTestCollection.Navn)]
public class HandlingEndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public HandlingEndepunktTests(EmbeddedPostgresApiFixture fixture)
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
                Tjenestetype = (string?)null, Malgruppe = Array.Empty<string>(), Kanaler = Array.Empty<string>(), Kostnad = (string?)null,
                Behandlingstid = (string?)null, Kontaktpunkt = (string?)null, KonsekvensVedBrudd = (string?)null, Sprak = Array.Empty<string>() }));
        svar.EnsureSuccessStatusCode();
        var tjeneste = await svar.Content.ReadFromJsonAsync<TjenesteDto>(JsonInnstillinger);
        return tjeneste!.Id;
    }

    private static object NyHandlingBody(string navn, string handlingstype, string? utfortAv = "soker") => new
    {
        Navn = navn, Handlingstype = handlingstype, Bruksomraade = (string?)null, UtfortAv = utfortAv,
        Kanaler = new[] { new { Kanal = "elektronisk", Adresse = (string?)null } },
        Behandlingstid = new { Frist = "60 dager", Hjemmel = new { Lov = "serveringsloven", Henvisning = "§ 10" } },
        Kostnad = (object?)null, Vedlegg = (object?)null, Veiledningstekst = (object?)null, Arsaker = (object?)null,
        Resultat = (object?)null, Merknad = (string?)null,
    };

    [Fact]
    public async Task Oppretter_og_lister_handling_under_en_tjeneste()
    {
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await OpprettTjenesteAsync(juristId, "Test-tjeneste-handling-A");

        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{tjenesteId}/handlinger", juristId,
            NyHandlingBody("Søknad", "soke")));
        Assert.Equal(HttpStatusCode.Created, opprettSvar.StatusCode);
        var opprettet = await opprettSvar.Content.ReadFromJsonAsync<HandlingDto>(JsonInnstillinger);
        Assert.Equal("utkast", opprettet!.Status);
        Assert.Single(opprettet.Kanaler);
        Assert.Equal("elektronisk", opprettet.Kanaler[0].Kanal);
        Assert.Equal("serveringsloven", opprettet.Behandlingstid.Hjemmel!.Lov);

        var listeSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, $"/api/tjenester/{tjenesteId}/handlinger", juristId));
        var liste = await listeSvar.Content.ReadFromJsonAsync<List<HandlingDto>>(JsonInnstillinger);
        Assert.Single(liste!);
    }

    [Fact]
    public async Task Oppdaterer_og_sletter_handling()
    {
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await OpprettTjenesteAsync(juristId, "Test-tjeneste-handling-B");
        var opprettet = await (await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{tjenesteId}/handlinger", juristId,
            NyHandlingBody("Melding", "melde"))))
            .Content.ReadFromJsonAsync<HandlingDto>(JsonInnstillinger);

        var oppdaterSvar = await _client.SendAsync(MedBruker(HttpMethod.Put, $"/api/tjenester/handlinger/{opprettet!.Id}", juristId,
            NyHandlingBody("Melding v2", "melde")));
        Assert.Equal(HttpStatusCode.OK, oppdaterSvar.StatusCode);
        var oppdatert = await oppdaterSvar.Content.ReadFromJsonAsync<HandlingDto>(JsonInnstillinger);
        Assert.Equal("Melding v2", oppdatert!.Navn);
        Assert.Equal(2, oppdatert.Versjon);

        var slettSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/tjenester/handlinger/{opprettet.Id}", juristId));
        Assert.Equal(HttpStatusCode.NoContent, slettSvar.StatusCode);

        var hentSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, $"/api/tjenester/handlinger/{opprettet.Id}", juristId));
        Assert.Equal(HttpStatusCode.NotFound, hentSvar.StatusCode);
    }

    [Fact]
    public async Task Ukjent_handlingstype_gir_400()
    {
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await OpprettTjenesteAsync(juristId, "Test-tjeneste-handling-C");

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{tjenesteId}/handlinger", juristId,
            NyHandlingBody("Noe", "ukjent-type")));

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Setter_status_pa_handling()
    {
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await OpprettTjenesteAsync(juristId, "Test-tjeneste-handling-D");
        var opprettet = await (await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{tjenesteId}/handlinger", juristId,
            NyHandlingBody("Klage", "klage"))))
            .Content.ReadFromJsonAsync<HandlingDto>(JsonInnstillinger);

        var statusSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/handlinger/{opprettet!.Id}/status", juristId,
            new { Status = "publisert" }));
        Assert.Equal(HttpStatusCode.OK, statusSvar.StatusCode);
        var oppdatert = await statusSvar.Content.ReadFromJsonAsync<HandlingDto>(JsonInnstillinger);
        Assert.Equal("publisert", oppdatert!.Status);
    }
}
