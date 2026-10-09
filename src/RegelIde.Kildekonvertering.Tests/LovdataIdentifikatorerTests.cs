namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Direkte enhetstester for eId-/ELI-avledningen, uavhengig av HTML-parsing. Skrevet etter ekstern
/// code review (Copilot) etterlyste eksplisitt dekning av datokoder uten løpenummer og bokstavvarianter.
/// </summary>
public class LovdataIdentifikatorerTests
{
    [Theory]
    [InlineData("LOV-1989-06-02-27", "https://lovdata.no/eli/lov/1989/06/02/27/nor")]
    [InlineData("FOR-2005-06-08-538", "https://lovdata.no/eli/forskrift/2005/06/08/538/nor")]
    // Eldre lover uten løpenummer (bekreftet i ekte kryssreferansedata, se AlkohollovenKonverteringTests) —
    // ELI-en skal utelate løpenummer-segmentet helt, ikke sette et tomt/ugyldig segment.
    [InlineData("LOV-1927-04-05", "https://lovdata.no/eli/lov/1927/04/05/nor")]
    [InlineData("LOV-1967-02-10", "https://lovdata.no/eli/lov/1967/02/10/nor")]
    public void AvledEliFraDatokode_produserer_korrekt_eli(string datokode, string forventetEli)
    {
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out _);
        Assert.Equal(forventetEli, eli);
        Assert.DoesNotContain("//", eli.Replace("https://", ""));
    }

    [Fact]
    public void AvledEliFraDatokode_skiller_lov_og_forskrift()
    {
        LovdataIdentifikatorer.AvledEliFraDatokode("LOV-1989-06-02-27", out var lovType);
        LovdataIdentifikatorer.AvledEliFraDatokode("FOR-2005-06-08-538", out var forskriftType);
        Assert.Equal(Kildetype.Lov, lovType);
        Assert.Equal(Kildetype.Forskrift, forskriftType);
    }

    [Fact]
    public void ParagrafEid_med_bokstav_i_paragrafnummer()
    {
        var eid = LovdataIdentifikatorer.ParagrafEid("https://lovdata.no/eli/lov/1989/06/02/27/nor", "§1-4a");
        Assert.Equal("https://lovdata.no/eli/lov/1989/06/02/27/nor/§1-4a", eid);
    }

    [Fact]
    public void KapittelEid_med_bokstav_i_kapittelnummer()
    {
        Assert.Equal("kap-3A", LovdataIdentifikatorer.KapittelEid("3A"));
    }

    [Theory]
    [InlineData(1, "https://lovdata.no/eli/lov/1990/06/29/50/nor/§10-2/ledd-2/avslutning")]
    [InlineData(2, "https://lovdata.no/eli/lov/1990/06/29/50/nor/§10-2/ledd-2/avslutning-2")]
    [InlineData(3, "https://lovdata.no/eli/lov/1990/06/29/50/nor/§10-2/ledd-2/avslutning-3")]
    public void AvslutningEid_er_avslutning_uten_suffiks_for_den_forste_og_med_lopenummer_ellers(int indeks, string forventet)
    {
        // [Ny, avslutningsnode-runden, 2026-10-09, issue #361] Johanns beslutning: «{ledd-eId}/avslutning
        // (-2, -3 … hvis flere)».
        Assert.Equal(forventet, LovdataIdentifikatorer.AvslutningEid("https://lovdata.no/eli/lov/1990/06/29/50/nor/§10-2/ledd-2", indeks));
    }

    [Fact]
    public void Ugyldig_datokode_kaster_uten_gjettet_fallback()
    {
        var ex = Assert.Throws<FormatException>(() => LovdataIdentifikatorer.AvledEliFraDatokode("LOV-89-6-2-27", out _));
        Assert.Contains("Ingen gjettet fallback", ex.Message);
    }
}
