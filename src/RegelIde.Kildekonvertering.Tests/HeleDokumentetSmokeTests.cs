namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Kjører hele konverteringen mot de ekte, fullstendige dokumentene (ikke bare testcase-utdrag) —
/// bekrefter at parseren håndterer alle strukturelle varianter som faktisk finnes i alkoholloven/
/// alkoholforskriften, jf. 06-veikart.md byggesteg 1 ("hele loven, ikke bare de relevante kapitlene").
/// </summary>
public class HeleDokumentetSmokeTests
{
    [Fact]
    public void Konverterer_hele_alkoholloven_uten_feil()
    {
        var html = Testdata.LesAlkoholloven();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 7, 23));
        Assert.NotEmpty(resultat.Noder);
    }

    [Fact]
    public void Konverterer_hele_alkoholforskriften_uten_feil()
    {
        var html = Testdata.LesAlkoholforskriften();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 7, 23));
        Assert.NotEmpty(resultat.Noder);
    }

    [Fact]
    public void Konverterer_hele_forvaltningsloven_uten_feil()
    {
        var html = Testdata.LesForvaltningsloven();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 7, 23));
        Assert.NotEmpty(resultat.Noder);
    }
}
