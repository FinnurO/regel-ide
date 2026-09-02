namespace RegelIde.Data.Tests;

/// <summary>
/// Ren funksjon, ingen database/klokke-mocking nødvendig (administrasjon-Lovdata-resynk, GitHub-issue
/// #104) — se <see cref="LovdataResynkPlanlegging"/>s klassekommentar for hvorfor dette bevisst er
/// trukket ut av <see cref="LovdataResynkPlanleggerTjeneste"/>.
/// </summary>
public class LovdataResynkPlanleggingTests
{
    private static readonly DateTimeOffset Naa = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Aldri_kjort_men_intervall_er_null_eller_ikke_positivt_gir_false(int? intervallTimer)
    {
        Assert.False(LovdataResynkPlanlegging.SkalKjoreNaa(Naa, sisteKjoringStartet: null, intervallTimer));
    }

    [Fact]
    public void Aldri_kjort_for_og_intervall_satt_gir_true()
    {
        Assert.True(LovdataResynkPlanlegging.SkalKjoreNaa(Naa, sisteKjoringStartet: null, intervallTimer: 24));
    }

    [Fact]
    public void Innenfor_intervallet_siden_siste_kjoring_gir_false()
    {
        var siste = Naa - TimeSpan.FromHours(23);
        Assert.False(LovdataResynkPlanlegging.SkalKjoreNaa(Naa, siste, intervallTimer: 24));
    }

    [Fact]
    public void Noyaktig_pa_intervallgrensen_gir_true()
    {
        var siste = Naa - TimeSpan.FromHours(24);
        Assert.True(LovdataResynkPlanlegging.SkalKjoreNaa(Naa, siste, intervallTimer: 24));
    }

    [Fact]
    public void Forbi_intervallet_gir_true()
    {
        var siste = Naa - TimeSpan.FromHours(200);
        Assert.True(LovdataResynkPlanlegging.SkalKjoreNaa(Naa, siste, intervallTimer: 168)); // ukentlig
    }

    [Fact]
    public void Intervall_null_gir_false_selv_om_siste_kjoring_var_for_lenge_siden()
    {
        var siste = Naa - TimeSpan.FromDays(365);
        Assert.False(LovdataResynkPlanlegging.SkalKjoreNaa(Naa, siste, intervallTimer: null));
    }
}
