namespace RegelIde.Data.Tests;

/// <summary>
/// Ekte nettverkskall mot de to LEVENDE, eksterne API-ene docs/31 bygger på (Store norske leksikon,
/// Kartverkets Sentralt stadnamnregister) — bevisst, ikke mocket, samme "test mot ekte data"-kultur og
/// <c>[Trait("Category", "LiveIntegration")]</c>-eksklusjon fra vanlig `dotnet test` som
/// <see cref="LovdataBulkHenterTests"/> (se RegelIde.Data.Tests.csproj og den klassekommentaren for
/// hvorfor/hvordan disse kjøres bevisst med <c>-p:VSTestTestCaseFilter="Category=LiveIntegration"</c>).
/// Verifiserer nøyaktig Johanns to foreslåtte eksempler (docs/31-oppdraget): "Advokatforeningen" mot
/// SNL, et kjent stedsnavn mot SSR. Bruker samme delte embedded Postgres som resten av
/// <see cref="DataTestCollection"/> — cache-tabellen er tom for disse testenes (faste) søketermer ved
/// FØRSTE kjøring, men idempotent ved gjentatt kjøring (cache-hit andre gang, fortsatt riktig svar).
/// </summary>
[Trait("Category", "LiveIntegration")]
[Collection(DataTestCollection.Navn)]
public class EksternNavneoppslagTjenesteLiveTests(EmbeddedPostgresFixture fixture)
{
    [Fact]
    public async Task SlaOppSnlAsync_Advokatforeningen_gir_bekreftet_institusjon_med_orgnr_og_alias()
    {
        using var http = new HttpClient();
        await using var db = fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(http, db);

        var resultat = await tjeneste.SlaOppSnlAsync("Advokatforeningen");

        Assert.True(resultat.Treff);
        Assert.Equal("936575668", resultat.Organisasjonsnummer);
        Assert.Contains("Advokatforeningen", resultat.Alias!);
        Assert.NotNull(resultat.EksternUrl);
        Assert.Contains("Den_Norske_Advokatforening", resultat.EksternUrl);
    }

    [Fact]
    public async Task SlaOppSsrAsync_Bergen_gir_bekreftet_stedsnavn()
    {
        using var http = new HttpClient();
        await using var db = fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(http, db);

        var resultat = await tjeneste.SlaOppSsrAsync("Bergen");

        Assert.True(resultat.Treff);
        Assert.NotNull(resultat.TaksonomiKategori); // "By"/"Kommune" — begge er reelle, gyldige SSR-objekttyper for Bergen.
    }
}
