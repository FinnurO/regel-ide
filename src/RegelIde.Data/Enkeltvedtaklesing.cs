using System.Text.RegularExpressions;

namespace RegelIde.Data;

/// <summary>[Ny, #514] Et vedtak lest som enkeltvedtak: parten det er rettet mot, og setningen det er lest av.</summary>
/// <param name="Part">Den navngitte parten («Posten Norge BA», «Lovisenberg diakonale høgskole»).</param>
/// <param name="Sitat">Setningen i vedtaket som gir parten tillatelsen, statusen eller godkjenningen, ordrett.</param>
public sealed record Enkeltvedtaklesning(string Part, string Sitat);

/// <summary>
/// [Ny, #514, prosjekteierens avgjørelse 2026-11-27] <b>Er vedtaket et enkeltvedtak?</b> Noen vedtak i SF og INS er enkeltvedtak, ikke forskrifter
/// eller instrukser: en tillatelse, godkjenning eller status til en navngitt part. Lovdatas base sier ikke det (SF = forskrift, INS = instruks), så
/// det leses av vedtaket selv. Måling 3b i #472 (N14 og N16): «… får Lovisenberg diakonale høgskole med virkning fra 1. juli 2011 status som
/// høgskole …» (INS/forskrift/2011-06-24-650), «… meddeles Posten Norge BA tillatelse til å utgi og godta bruk av frimerker …»
/// (SF/forskrift/2001-08-31-1074).
/// <para>
/// <b>Regelen</b> (alle vilkårene, ellers null; det gjettes ikke, §8):
/// <list type="number">
/// <item><b>Innholdet</b> har en operativ setning med en navngitt part og en avgjørelse: «meddeles/gis/tildeles/innvilges X tillatelse/konsesjon/
/// løyve/dispensasjon/fritak/godkjenning/samtykke/bevilling/autorisasjon/akkreditering/status som …», «får X (med virkning fra ‹dato›) status som/
/// tillatelse …» eller «X (med virkning fra ‹dato›) godkjennes som …». Parten begynner med stor bokstav.</item>
/// <item><b>Tittelen</b> (eller kortnavnet) nevner parten: det første ordet i partens navn står som eget ord i tittelen. Vedtaket er da rettet mot
/// parten også etter tittelen («Tillatelse for Posten Norge BA …», «Lovisenberg diakonale høgskole status som høgskole»).</item>
/// <item><b>Vedtaket er kort</b> (høyst <see cref="MaksNoder"/> noder med tekst) og har ingen tegn på generelle regler («Formål», «Virkeområde»,
/// «Denne forskrift gjelder»).</item>
/// <item><b>Tittelen begynner ikke med «Forskrift»</b> (V05 i måling 4 del 2: TONO-vedtaket har også en generell regel).</item>
/// <item><b>Parten er ikke et statlig organ</b> («Høgskolen i Molde», «Universitetet i …», «Statens …», «…departementet», «…direktoratet»,
/// «…tilsynet»). Status og navn for et statlig organ er et organiseringsvedtak (#438), ikke et enkeltvedtak etter fvl. § 2 første ledd bokstav b.</item>
/// </list>
/// <b>Hjemmelsteksten</b> («vedtak om …» rettet mot en navngitt part) er undersøkt og gir ingen treff blant de korte vedtakene i korpus (0 av
/// 1 533 i kopien 2026-11-27). Den brukes derfor ikke.
/// </para>
/// </summary>
public static class Enkeltvedtaklesing
{
    /// <summary>Høyst så mange noder med tekst. Et enkeltvedtak i korpus har én til tre.</summary>
    public const int MaksNoder = 8;

    private const string Avgjorelse =
        @"(tillatelse|konsesjon|løyve|dispensasjon|fritak|godkjenning|samtykke|bevilling|autorisasjon|akkreditering|status\s+som)";

    private const string Part = @"(?<part>[A-ZÆØÅ][^,;:.]{0,80}?)";

    private const string Virkning = @"(?:med\s+virkning\s+fra\s+\d{1,2}\.\s*\w+\s+\d{4}\s+)?";

    private static readonly Regex[] Operativ =
    [
        new(@"\b(?:meddeles|gis|tildeles|innvilges)\s+" + Part + @"\s+" + Virkning + Avgjorelse, RegexOptions.CultureInvariant),
        new(@"\bfår\s+" + Part + @"\s+" + Virkning + Avgjorelse, RegexOptions.CultureInvariant),
        new(Part + @"\s+" + Virkning + @"(?:godkjennes|akkrediteres)\s+som\b", RegexOptions.CultureInvariant),
    ];

    private static readonly Regex Generell = new(
        @"\b(Denne\s+forskrift(en)?\s+gjelder|Forskriften\s+gjelder|Formål|Virkeområde|Definisjoner)\b", RegexOptions.CultureInvariant);

    private static readonly Regex Statlig = new(
        @"^(Høgskolen|Universitetet|Statens|Staten|Fylkesmannen|Statsforvalteren|Forsvaret|Kystverket|Politi\w*|Riksantikvaren)\b"
        + @"|\w*(departementet|direktoratet|tilsynet|etaten|verket)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Leser vedtaket. <paramref name="nodetekster"/> er tekstene til nodene (ikke merknader), i rekkefølge.</summary>
    public static Enkeltvedtaklesning? Les(string tittel, string? kortnavn, IReadOnlyList<string> nodetekster)
    {
        if (nodetekster.Count is 0 or > MaksNoder) return null;
        // [Ny, juristrunden i måling 4 del 2, V05] En tittel som sier «Forskrift om …», er en forskrift, også når den gir en part en bevilling
        // («Forskrift om fortsatt bevilling til TONO og NCB …», SF/forskrift/1971-06-25-3800, har også en generell regel om avgiften).
        if (Regex.IsMatch(tittel, @"^\s*Forskrift(er)?\b", RegexOptions.IgnoreCase)) return null;
        var tekst = Hjemmelstekstlesing.Normaliser(string.Join(" ", nodetekster));
        if (Generell.IsMatch(tekst)) return null;
        foreach (var rx in Operativ)
        {
            var m = rx.Match(tekst);
            if (!m.Success) continue;
            var part = m.Groups["part"].Value.Trim();
            if (Statlig.IsMatch(part)) return null;
            var forste = Regex.Match(part, @"^[\wÆØÅæøå-]{3,}");
            if (!forste.Success) return null;
            var ord = @"(?<![\wÆØÅæøå])" + Regex.Escape(forste.Value) + @"(?![\wÆØÅæøå])";
            if (!Regex.IsMatch(tittel, ord, RegexOptions.IgnoreCase) && !(kortnavn is not null && Regex.IsMatch(kortnavn, ord, RegexOptions.IgnoreCase)))
                return null;
            var sitat = Regex.Split(tekst, @"(?<=[.;])\s+(?=[A-ZÆØÅ])").FirstOrDefault(s => s.Contains(m.Value, StringComparison.Ordinal))?.Trim() ?? m.Value;
            return new Enkeltvedtaklesning(part, sitat);
        }
        return null;
    }
}
