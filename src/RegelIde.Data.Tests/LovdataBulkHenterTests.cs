using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Ekte nettverkskall mot Lovdatas offisielle bulk-API — bevisst, ikke mocket, samme prinsipp som
/// resten av prosjektets "test mot ekte data"-kultur. Kan være treg (laster ned hele arkivet).
/// [Ny, 2026-08-28] Alle fire faktaene her er tagget `Category=LiveIntegration` og ekskludert fra
/// standard `dotnet test` via `VSTestTestCaseFilter` i RegelIde.Data.Tests.csproj — se
/// LovdataFullimportTjenesteTests for hvorfor.
/// </summary>
[Trait("Category", "LiveIntegration")]
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

        // 2026-07-29: "forvaltningssaker" er ren ASCII og fanget IKKE opp den ekte mojibake-bugen
        // (UTF-8-bytes feilaktig dekodet som cp1252, se LovdataBulkHenter.cs) — verifiser derfor
        // eksplisitt et ord med norske bokstaver, som ville vist "behandlingsmÃ¥ten" ved feilen.
        Assert.Contains("behandlingsmåten", resultat.Metadata.Tittel);
        Assert.DoesNotContain("Ã", resultat.Metadata.Tittel);
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

    /// <summary>Byggesteg 5 runde 2 (Lovdata-katalog) — ekte nettverkskall, samme kultur som testene over.</summary>
    [Fact]
    public async Task Henter_alle_oppforinger_og_finner_forvaltningsloven()
    {
        using var http = new HttpClient();
        var henter = new LovdataBulkHenter(http);

        var oppføringer = new List<(string Datokode, string Tittel, string Type)>();
        await foreach (var oppføring in henter.HentAlleOppforingerAsync())
        {
            oppføringer.Add(oppføring);
        }

        Assert.True(oppføringer.Count > 100, "Forventet et stort antall lover+forskrifter i bulk-arkivene.");
        var forvaltningsloven = Assert.Single(oppføringer, o => o.Datokode == "LOV-1967-02-10");
        Assert.Contains("forvaltningssaker", forvaltningsloven.Tittel);
        Assert.Equal("lov", forvaltningsloven.Type);
        Assert.Contains(oppføringer, o => o.Type == "forskrift");
    }
}
