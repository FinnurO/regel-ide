using System.Text.RegularExpressions;

namespace RegelIde.Kildekonvertering;

public sealed record TolketHref(Kildetype Kildetype, string Datokode, string? Paragrafnummer)
{
    /// <summary>
    /// [Ny, hjemmel-presisjon-runden, 2026-09-10, issue #217] Paragrafnummeret ALENE, uten en
    /// eventuell presisering etter det.
    ///
    /// <para><see cref="Paragrafnummer"/> er bevisst uendret: det er ALT etter datokoden, altså
    /// «§13-1/ledd/4» for en href som presiserer et ledd. Alle eksisterende kallsteder
    /// (løpetekst-kryssreferanser) bygger paragraf-eId-er direkte fra det feltet, og å dele det opp
    /// der ville endret data de allerede har skrevet. De to egenskapene her er derfor et TILLEGG som
    /// hjemmel-stien bruker, ikke en omskriving.</para>
    /// </summary>
    public string? BareParagrafnummer => Paragrafnummer?.Split('/', 2)[0];

    /// <summary>
    /// Presiseringen etter paragrafnummeret, i Lovdatas EGEN form: «ledd/4», «ledd/3/bokstav/a»,
    /// «ledd/4/setning/2». <c>null</c> når href-en bare peker på paragrafen.
    ///
    /// <para>Målt i korpuset 2026-09-10 (400 forskrifter, 335 med hjemmelslinje, 1712
    /// paragrafnivå-lenker): 38 lenker har presiseringen i href-en, og NULL lenke har et ordenstall
    /// («fjerde ledd») i lenketeksten UTEN at href-en også har «/ledd/4». Presisjonen er altså
    /// struktur i kilden, ikke bare prosa — den skal leses, ikke tolkes ut av tekst.</para>
    /// </summary>
    public string? Presisering =>
        Paragrafnummer is null || !Paragrafnummer.Contains('/') ? null : Paragrafnummer.Split('/', 2)[1];
}

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
