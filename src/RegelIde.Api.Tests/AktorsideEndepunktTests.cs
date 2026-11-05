using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #441] Aktørsidens endepunkter mot hele API-et: svarer hjemmel og rettskilder for en aktør, gir de 404 for en ukjent, og krever
/// «Godkjenn hjemmelen» innlogging og en hjemmel? Reglene er testet i <c>AktorsideTjenesteTests</c> (Data).
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class AktorsideEndepunktTests(EmbeddedPostgresApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Hjemmel(string Status, string? Hullmerknad);
    private sealed record Tall(int Rettskilder, int Kanter, int KanterIRadene, int KanterIHjemmelen);
    private sealed record Rettskilder(Guid VirksomhetId, Tall Tall);

    [Fact]
    public async Task Hjemmel_og_rettskilder_svarer_for_en_aktor_og_404_for_en_ukjent()
    {
        await using var db = fixture.NyDbContext();
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = "Apitest aktørside " + Guid.NewGuid() };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();

        var h = await _client.GetFromJsonAsync<Hjemmel>($"/api/virksomheter/{v.Id}/hjemmel", Json);
        Assert.Equal(("mangler", AktorkoTjeneste.Hullmerknad), (h!.Status, h.Hullmerknad));
        var r = await _client.GetFromJsonAsync<Rettskilder>($"/api/virksomheter/{v.Id}/rettskilder", Json);
        Assert.Equal((v.Id, 0, 0), (r!.VirksomhetId, r.Tall.Rettskilder, r.Tall.Kanter));

        var ukjent = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/virksomheter/{ukjent}/hjemmel")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/virksomheter/{ukjent}/rettskilder")).StatusCode);

        // Uten innlogget bruker skrives ingenting; med bruker og uten hjemmel er det ingenting å godkjenne (400).
        var uten = await _client.PostAsJsonAsync($"/api/virksomheter/{v.Id}/hjemmel/godkjenn", new { hjemmelArt = (string?)null });
        Assert.False(uten.IsSuccessStatusCode);
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", Json);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/virksomheter/{v.Id}/hjemmel/godkjenn") { Content = JsonContent.Create(new { hjemmelArt = (string?)null }) };
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, brukere!.Single(b => b.Rolle == "Jurist").Id.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(request)).StatusCode);
    }
}
