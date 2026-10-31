using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #440 PR 1] Aktørkøens endepunkter mot hele API-et: svarer de uten innlogging og uten å skrive, summerer fordelingen til antall
/// ventende (AK3), og gir «fordeling» sin egen rute og ikke et navn?
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class AktorkoEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AktorkoEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Fordelingen_summerer_til_antall_ventende_og_lista_og_ukjent_navn_svarer_uten_a_skrive()
    {
        await using var db = _fixture.NyDbContext();
        var ventende = await db.Navnekandidater.CountAsync(k => k.Status == "Venter");
        var forBegreper = await db.Begreper.CountAsync();

        var fordeling = await _client.GetFromJsonAsync<AktorkoFordeling>("/api/aktorko/fordeling", Json);
        Assert.NotNull(fordeling);
        Assert.Equal(ventende, fordeling.Ventende);
        Assert.Equal(ventende, fordeling.Ruter.Sum(r => r.Forekomster));
        Assert.Equal(Aktorkoruter.Alle, fordeling.Ruter.Select(r => r.Rute));

        var liste = await _client.GetFromJsonAsync<AktorkoListe>("/api/aktorko", Json);
        Assert.Equal(fordeling.AntallNavn, liste!.AntallNavn);
        Assert.Equal(fordeling.Ruter.Single(r => r.Rute == Aktorkoruter.Aktorko).Forekomster, liste.Navn.Sum(n => n.Forekomster));

        var ukjent = await _client.GetAsync($"/api/aktorko/{Uri.EscapeDataString("finnes ikke " + Guid.NewGuid())}");
        Assert.Equal(HttpStatusCode.NotFound, ukjent.StatusCode);

        Assert.Equal((ventende, forBegreper), (await db.Navnekandidater.CountAsync(k => k.Status == "Venter"), await db.Begreper.CountAsync()));
    }
}
