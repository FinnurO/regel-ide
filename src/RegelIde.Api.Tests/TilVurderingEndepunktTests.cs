using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #517 «Til vurdering»] Endepunktet mot hele API-et: svarer det uten innlogging, uten å skrive, og med de samme tallene som køene
/// (aktørkøen og forslagskøen) gir (AK 1)?
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class TilVurderingEndepunktTests(EmbeddedPostgresApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Tallene_er_de_samme_som_koene_gir_og_kallet_skriver_ingenting()
    {
        await using var db = fixture.NyDbContext();
        var forBegreper = await db.Begreper.CountAsync();
        var forKanter = await db.Strukturkanter.IgnoreQueryFilters().CountAsync();

        var tv = await _client.GetFromJsonAsync<TilVurdering>("/api/til-vurdering", Json);
        Assert.NotNull(tv);

        var fordeling = await _client.GetFromJsonAsync<AktorkoFordeling>("/api/aktorko/fordeling", Json);
        Assert.Equal(fordeling!.AntallNavn, tv.Aktorko.AntallNavn);
        Assert.Equal(tv.Aktorko.AntallNavn, tv.Aktorko.PerHandling.Values.Sum());
        Assert.Equal(tv.Aktorko.PerHandling.GetValueOrDefault(Forhandshandling.Godkjenn), tv.Aktorko.Klare + tv.Aktorko.TrengerSted);

        var perHjemmel = await _client.GetFromJsonAsync<List<JsonElement>>("/api/strukturkanter/forslag-per-hjemmel", Json);
        Assert.True(tv.ForslagskoTotalt >= perHjemmel!.Sum(g => g.GetProperty("antall").GetInt32()));
        Assert.Equal(await db.Strukturkanter.CountAsync(k => Strukturkanter.Ventende.Contains(k.Status)), tv.ForslagskoTotalt);

        Assert.Equal((forBegreper, forKanter), (await db.Begreper.CountAsync(), await db.Strukturkanter.IgnoreQueryFilters().CountAsync()));
    }
}
