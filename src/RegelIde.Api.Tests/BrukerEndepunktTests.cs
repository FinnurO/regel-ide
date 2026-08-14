using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for brukerhåndteringen (opprett/rediger/tilordning til virksomhet,
/// 2026-08-13) — kjører mot ekte embedded Postgres. Bruker bevisst IKKE Rolle="Jurist" for nyopprettede
/// testbrukere: en lang rekke andre testfiler i denne collectionen forutsetter at nøyaktig én
/// seedet bruker ("Kari Jurist") har den rollen (se AgderFylkeskommuneSeed.cs).
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class BrukerEndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public BrukerEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> HentEnVirksomhetIdAsync()
    {
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);
        return virksomheter!.First().Id;
    }

    [Fact]
    public async Task Hent_brukere_inkluderer_er_altinn_bruker_felt_og_er_ikke_tom()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);

        Assert.NotNull(brukere);
        Assert.NotEmpty(brukere);
        // Ingen av de seedede testbrukerne er Altinn-tilknyttet.
        Assert.All(brukere!, b => Assert.False(b.ErAltinnBruker));
    }

    [Fact]
    public async Task Oppretter_bruker_og_tilordner_virksomhet()
    {
        var virksomhetId = await HentEnVirksomhetIdAsync();
        var navn = $"Test Saksbehandler {Guid.NewGuid():N}";

        var svar = await _client.PostAsJsonAsync("/api/brukere",
            new OpprettBrukerRequest(navn, "Saksbehandler", virksomhetId), JsonInnstillinger);

        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var opprettet = await svar.Content.ReadFromJsonAsync<BrukerDto>(JsonInnstillinger);
        Assert.Equal(navn, opprettet!.Navn);
        Assert.Equal("Saksbehandler", opprettet.Rolle);
        Assert.Equal(virksomhetId, opprettet.VirksomhetId);
        Assert.False(opprettet.ErAltinnBruker);

        // Skal nå dukke opp i den fulle listen — brukerhåndteringssiden viser samme endepunkt.
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        Assert.Contains(brukere!, b => b.Id == opprettet.Id);
    }

    [Fact]
    public async Task Oppretter_bruker_med_ukjent_rolle_gir_400()
    {
        var virksomhetId = await HentEnVirksomhetIdAsync();

        var svar = await _client.PostAsJsonAsync("/api/brukere",
            new OpprettBrukerRequest("Test Bruker", "Direktør", virksomhetId), JsonInnstillinger);

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Oppretter_bruker_med_ukjent_virksomhet_gir_400()
    {
        var svar = await _client.PostAsJsonAsync("/api/brukere",
            new OpprettBrukerRequest("Test Bruker", "Saksbehandler", Guid.NewGuid()), JsonInnstillinger);

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Oppretter_bruker_med_tomt_navn_gir_400()
    {
        var virksomhetId = await HentEnVirksomhetIdAsync();

        var svar = await _client.PostAsJsonAsync("/api/brukere",
            new OpprettBrukerRequest("   ", "Saksbehandler", virksomhetId), JsonInnstillinger);

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Oppdaterer_rolle_og_virksomhet_pa_eksisterende_bruker()
    {
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);
        var forsteVirksomhet = virksomheter!.First();
        var andreVirksomhet = virksomheter.Skip(1).FirstOrDefault() ?? forsteVirksomhet;

        var opprettSvar = await _client.PostAsJsonAsync("/api/brukere",
            new OpprettBrukerRequest($"Test Saksbehandler {Guid.NewGuid():N}", "Saksbehandler", forsteVirksomhet.Id), JsonInnstillinger);
        var opprettet = await opprettSvar.Content.ReadFromJsonAsync<BrukerDto>(JsonInnstillinger);

        var oppdaterSvar = await _client.PutAsJsonAsync($"/api/brukere/{opprettet!.Id}",
            new OppdaterBrukerRequest("Systemforvalter", andreVirksomhet.Id), JsonInnstillinger);

        Assert.Equal(HttpStatusCode.OK, oppdaterSvar.StatusCode);
        var oppdatert = await oppdaterSvar.Content.ReadFromJsonAsync<BrukerDto>(JsonInnstillinger);
        Assert.Equal("Systemforvalter", oppdatert!.Rolle);
        Assert.Equal(andreVirksomhet.Id, oppdatert.VirksomhetId);
        Assert.Equal(opprettet.Navn, oppdatert.Navn);
    }

    [Fact]
    public async Task Oppdaterer_ukjent_bruker_gir_404()
    {
        var virksomhetId = await HentEnVirksomhetIdAsync();

        var svar = await _client.PutAsJsonAsync($"/api/brukere/{Guid.NewGuid()}",
            new OppdaterBrukerRequest("Saksbehandler", virksomhetId), JsonInnstillinger);

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Oppdaterer_med_ukjent_rolle_gir_400()
    {
        var virksomhetId = await HentEnVirksomhetIdAsync();
        var opprettSvar = await _client.PostAsJsonAsync("/api/brukere",
            new OpprettBrukerRequest($"Test Saksbehandler {Guid.NewGuid():N}", "Saksbehandler", virksomhetId), JsonInnstillinger);
        var opprettet = await opprettSvar.Content.ReadFromJsonAsync<BrukerDto>(JsonInnstillinger);

        var svar = await _client.PutAsJsonAsync($"/api/brukere/{opprettet!.Id}",
            new OppdaterBrukerRequest("Direktør", virksomhetId), JsonInnstillinger);

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }
}
