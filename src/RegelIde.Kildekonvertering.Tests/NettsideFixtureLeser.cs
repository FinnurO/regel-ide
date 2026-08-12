namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Leser TEST-fixtureformatet i data/kilder/raw-nettside/*.txt (se README der for konvensjonen) —
/// et rent fixture-format for DENNE testsuiten, IKKE noe production-kode tolker (i produksjon
/// kommer KanoniskUrl/Tittel fra selve HTTP-hentingen, ikke fra en tekst-header). Skiller
/// metadata-headerlinjene ("KanoniskUrl:"/"Tittel:"/ev. "StiType:"/"Sti:") fra selve RaaTekst-
/// innholdet (alt fra første blanke linje og ut, LENKER-seksjonen inkludert) — det som faktisk gis
/// videre til <see cref="NettsideTekstParser.Parse"/> uendret.
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
