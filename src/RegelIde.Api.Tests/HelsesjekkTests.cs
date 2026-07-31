using System.Net;

namespace RegelIde.Api.Tests;

/// <summary>
/// Begge helse-stiene må svare likt. <c>/health</c> finnes fordi Altinns app-Helm-chart har
/// hardkodet den stien og den ikke er konfigurerbar i values.yaml; uten den ville probene truffet
/// SPA-fallbacken og fått 200 text/html uansett tilstand.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class HelsesjekkTests
{
    private readonly HttpClient _client;

    public HelsesjekkTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Theory]
    [InlineData("/helse")]
    [InlineData("/health")]
    public async Task Svarer_200_json_naar_databasen_svarer(string sti)
    {
        var svar = await _client.GetAsync(sti);

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        // Det avgjørende: JSON, ikke text/html. Traff vi SPA-fallbacken ville dette vært html,
        // og proben ville passert uavhengig av om databasen faktisk svarer.
        Assert.Equal("application/json", svar.Content.Headers.ContentType?.MediaType);
        Assert.Contains("oppe", await svar.Content.ReadAsStringAsync());
    }
}
