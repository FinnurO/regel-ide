using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Ekte nettverkskall mot Lovdatas offisielle bulk-API — bevisst, ikke mocket, samme prinsipp som
/// resten av prosjektets "test mot ekte data"-kultur. Kan være treg (laster ned hele arkivet).
/// </summary>
public class LovdataBulkHenterTests
{
    [Fact]
    public async Task Henter_forvaltningsloven_og_konverterer_den_riktig()
    {
        using var http = new HttpClient();
        var henter = new LovdataBulkHenter(http);

        var raaHtml = await henter.HentRaaHtmlAsync("LOV-1967-02-10");
        var resultat = LovdataKonverterer.Konverter(raaHtml, new DateOnly(2026, 7, 24));

        Assert.Equal("https://lovdata.no/eli/lov/1967/02/10/nor", resultat.Metadata.Eli);
        Assert.Contains("forvaltningssaker", resultat.Metadata.Tittel);
    }

    [Fact]
    public async Task Henter_alkoholforskriften_med_lopenummer_i_datokoden()
    {
        using var http = new HttpClient();
        var henter = new LovdataBulkHenter(http);

        var raaHtml = await henter.HentRaaHtmlAsync("FOR-2005-06-08-538");
        var resultat = LovdataKonverterer.Konverter(raaHtml, new DateOnly(2026, 7, 24));

        Assert.Equal("https://lovdata.no/eli/forskrift/2005/06/08/538/nor", resultat.Metadata.Eli);
    }

    [Fact]
    public async Task Ukjent_datokode_kaster_tydelig_feil_uten_gjettet_fallback()
    {
        using var http = new HttpClient();
        var henter = new LovdataBulkHenter(http);

        await Assert.ThrowsAsync<InvalidOperationException>(() => henter.HentRaaHtmlAsync("LOV-1900-01-01-999"));
    }
}
