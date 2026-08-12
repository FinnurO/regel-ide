namespace RegelIde.Data.Tests;

/// <summary>
/// Leser TEST-fixtureformatet i data/kilder/raw-nettside/*.txt — se README der. Samme lille
/// hjelper som i RegelIde.Kildekonvertering.Tests (bevisst ikke delt via en shared test-lib for
/// ~20 linjer kode); se den klassens kommentar for begrunnelsen (produksjonskode kjenner ALDRI
/// dette tekst-header-formatet — det er kun fixture-konvensjonen for DENNE runden).
/// </summary>
internal static class NettsideFixtureLeser
{
    public static NettsideFixture Les(string fixtureInnhold)
    {
        var linjer = fixtureInnhold.Replace("\r\n", "\n").Split('\n');
        string? kanoniskUrl = null, tittel = null, stiType = null, sti = null;
        var kroppStart = linjer.Length;

        for (var i = 0; i < linjer.Length; i++)
        {
            var linje = linjer[i];
            if (linje.Length == 0) { kroppStart = i + 1; break; }
            if (linje.StartsWith("KanoniskUrl:")) kanoniskUrl = linje["KanoniskUrl:".Length..].Trim();
            else if (linje.StartsWith("Tittel:")) tittel = linje["Tittel:".Length..].Trim();
            else if (linje.StartsWith("StiType:")) stiType = linje["StiType:".Length..].Trim();
            else if (linje.StartsWith("Sti:")) sti = linje["Sti:".Length..].Trim();
        }

        if (kanoniskUrl is null) throw new FormatException("Fixture mangler 'KanoniskUrl:'-header.");

        var raaTekst = string.Join('\n', linjer[kroppStart..]).Trim();
        return new NettsideFixture(kanoniskUrl, tittel, stiType, sti, raaTekst);
    }
}

internal sealed record NettsideFixture(string KanoniskUrl, string? Tittel, string? StiType, string? Sti, string RaaTekst);
