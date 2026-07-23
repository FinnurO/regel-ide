using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RegelIde.Kildekonvertering;

/// <summary>
/// eId-konstruksjon per docs/08-byggesteg1-teknisk-design.md §1.2 (låst arkitekturbeslutning) og
/// tekst_hash per §3.4. Samlet i én klasse fordi begge er rene, deterministiske funksjoner av
/// allerede-ekstrahert data — ingen HTML-parsing skjer her.
/// </summary>
public static partial class LovdataIdentifikatorer
{
    // Løpenummeret er valgfritt: eldre lover uten flere vedtak samme dag identifiseres av Lovdata
    // med bare datoen, f.eks. "LOV-1927-04-05" (bekreftet i ekte kryssreferansedata — alkoholloven
    // viser til flere slike). ELI-en utelater da tilsvarende siste segment.
    [GeneratedRegex(@"^(LOV|FOR)-(\d{4})-(\d{2})-(\d{2})(?:-(\S+))?$")]
    private static partial Regex DatokodeMønster();

    /// <summary>
    /// Avleder den verifiserte ELI-URI-en direkte fra Lovdatas Datokode-felt
    /// (f.eks. "LOV-1989-06-02-27" → "https://lovdata.no/eli/lov/1989/06/02/27/nor",
    /// "LOV-1927-04-05" → "https://lovdata.no/eli/lov/1927/04/05/nor").
    /// Se §1.2: "Verifisert direkte" for lovnivå — samme deterministiske omforming gjelder forskrifter.
    /// </summary>
    public static string AvledEliFraDatokode(string datokode, out Kildetype kildetype)
    {
        var m = DatokodeMønster().Match(datokode);
        if (!m.Success)
        {
            throw new FormatException(
                $"Datokode '{datokode}' matcher ikke forventet mønster LOV|FOR-ÅÅÅÅ-MM-DD[-løpenummer]. " +
                "Ingen gjettet fallback-verdi produseres (§3.3).");
        }

        kildetype = m.Groups[1].Value == "LOV" ? Kildetype.Lov : Kildetype.Forskrift;
        var segment = kildetype == Kildetype.Lov ? "lov" : "forskrift";
        var aar = m.Groups[2].Value;
        var maaned = m.Groups[3].Value;
        var dag = m.Groups[4].Value;
        var lopenummer = m.Groups[5].Success ? "/" + m.Groups[5].Value : "";
        return $"https://lovdata.no/eli/{segment}/{aar}/{maaned}/{dag}{lopenummer}/nor";
    }

    /// <summary>Kapittel-eId: "kap-{N}", uavhengig av rettskildens ELI (§1.2, tabellen).</summary>
    public static string KapittelEid(string kapittelNummer) => $"kap-{kapittelNummer}";

    /// <summary>
    /// Underinndeling (romertall, §3.2) sin eId. Ikke eksplisitt spesifisert i §1.2s tabell —
    /// naturlig utvidelse av samme mønster som kapittel-nivået: lokal, ikke ELI-prefikset.
    /// </summary>
    public static string UnderinndelingEid(string parentKapittelEid, string romertall) =>
        $"{parentKapittelEid}/rom-{romertall}";

    /// <summary>Paragraf-eId: "{lov-eli}/§X-Y" (§1.2, tabellen).</summary>
    public static string ParagrafEid(string lovEli, string paragrafnummer) => $"{lovEli}/{paragrafnummer}";

    /// <summary>Ledd-eId: "{paragraf-eId}/ledd-N" (§1.2, tabellen), N er 1-basert løpenummer innenfor paragrafen.</summary>
    public static string LeddEid(string paragrafEid, int leddIndeks) => $"{paragrafEid}/ledd-{leddIndeks}";

    /// <summary>Punkt-eId: "{ledd-eId}/punkt-N" (§1.2, tabellen), N er 1-basert løpenummer innenfor leddet.</summary>
    public static string PunktEid(string leddEid, int punktIndeks) => $"{leddEid}/punkt-{punktIndeks}";

    /// <summary>
    /// tekst_hash presis definisjon, §3.4: SHA-256 av normalisert tekst.
    /// Normalisering (i rekkefølge): (1) tagger fjernet — inkl. interne &lt;a&gt;-referanser,
    /// som kun bidrar med sin synlige tekst (kalleren gir oss allerede ren tekst via
    /// TekstSegment-konkatenering, §1.2.1/Tekstsegment), (2) Unicode NFC, (3) whitespace kollapset til
    /// ett mellomrom, trimmet.
    /// </summary>
    public static string BeregnTekstHash(string ekstrahertPlainTekst)
    {
        var normalisert = NormaliserForHash(ekstrahertPlainTekst);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalisert));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormaliserForHash(string tekst)
    {
        var nfc = tekst.Normalize(NormalizationForm.FormC);
        var kollapset = WhitespaceMønster().Replace(nfc, " ").Trim();
        return kollapset;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceMønster();
}
