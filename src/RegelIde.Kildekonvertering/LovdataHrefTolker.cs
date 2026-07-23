using System.Text.RegularExpressions;

namespace RegelIde.Kildekonvertering;

public sealed record TolketHref(Kildetype Kildetype, string Datokode, string? Paragrafnummer);

/// <summary>
/// Tolker Lovdatas interne href-mønster i løpetekst: "lov/{datokode}[/{§X-Y}]" eller
/// "forskrift/{datokode}[/{§X-Y}]" (data/kilder/README.md, bekreftet mot ekte data — kun disse to
/// prefiksene forekommer i dokumentkroppen). Header-metadata (EØS-henvisninger, «Endrer») bruker andre
/// prefikser («avtale/», «eu/») og behandles ikke her — de er bevisst utenfor kryssreferanse-steget
/// (§3.1 steg 6, Vedlegg A.7).
/// </summary>
public static partial class LovdataHrefTolker
{
    [GeneratedRegex(@"^(lov|forskrift)/([\d-]+)(?:/(§.+))?$")]
    private static partial Regex HrefMønster();

    public static TolketHref? TolkLøpetekstHref(string href)
    {
        var m = HrefMønster().Match(href);
        if (!m.Success)
        {
            return null;
        }

        var kildetype = m.Groups[1].Value == "lov" ? Kildetype.Lov : Kildetype.Forskrift;
        var datokodeSuffiks = m.Groups[2].Value; // f.eks. "1989-06-02-27"
        var prefiks = kildetype == Kildetype.Lov ? "LOV" : "FOR";
        var datokode = $"{prefiks}-{datokodeSuffiks}";
        var paragraf = m.Groups[3].Success ? m.Groups[3].Value : null;
        return new TolketHref(kildetype, datokode, paragraf);
    }
}
