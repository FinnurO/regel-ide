using System.Net.Http.Json;
using RegelIde.Api;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstest for konfigurasjonsendepunktene (2026-07-25) — kjører mot ekte embedded Postgres
/// og Program.cs' førstegangs-seeding av tag-kinds.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class KonfigurasjonEndepunktTests
{
    private readonly HttpClient _client;

    public KonfigurasjonEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    /// <summary>[UTVIDET, navnekandidat-wizard-runden, 2026-09-07] Femte kind `'virksomhet'` — se
    /// taggkind-seeden i Program.cs for hvorfor den er tilbake etter å ha blitt reversert 2026-08-22,
    /// og NavnekandidatOppdagelseTjeneste.KoblTilVirksomhetAsync for hva den brukes til.</summary>
    [Fact]
    public async Task Hent_tagg_kinds_returnerer_de_fem_seedede_verdiene_i_riktig_rekkefolge()
    {
        var kinds = await _client.GetFromJsonAsync<List<TaggKindKonfigurasjonDto>>("/api/konfigurasjon/tagg-kinds");

        Assert.NotNull(kinds);
        Assert.Equal(
            ["begrep", "tjeneste", "vilkar", "regel", "virksomhet"],
            kinds!.Select(k => k.Kode));
        Assert.Equal("Begrep", kinds[0].Navn);
        Assert.Equal("accent", kinds[0].Farge);
        // Fargen er verifisert mot installert digdir.css (se seeden) — ikke gjettet.
        Assert.Equal("Virksomhet", kinds[4].Navn);
        Assert.Equal("brand1", kinds[4].Farge);
    }
}
