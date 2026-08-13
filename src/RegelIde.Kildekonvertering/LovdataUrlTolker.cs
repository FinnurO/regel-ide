using System.Text.RegularExpressions;

namespace RegelIde.Kildekonvertering;

/// <summary>
/// Tolker en <c>lovdata.no</c>-URL funnet i en nettside-tekst til en ELI-kandidat i EKSAKT samme
/// form som <see cref="LovdataIdentifikatorer.AvledEliFraDatokode"/> produserer for allerede
/// importerte rettskilder (<c>RettskildeEntitet.Eli</c>) — docs/15-handbok-dokumentgraf-notat.md
/// §3.2 <c>lovdatalenke</c>. Dette er selve "koble alle sammen"-mekanismen: en streng-URL fra en
/// nettside skal kunne slås opp mot en ekte DB-rad UTEN gjetting, kun ved at begge sider produserer
/// samme normaliserte streng.
/// <para>
/// Ren, DB-fri regex-tolkning — INGEN DB-oppslag her (det gjøres av
/// <c>RegelIde.Data.NettsideGrafKobler</c>, som slår opp <see cref="TolkTilEliKandidat"/>s resultat
/// mot <c>RettskildeEntitet.Eli</c>). Samme arkitektur-todeling som
/// <c>HandbokTekstParser.HjemmelMønster</c> (uttrekk) vs. <c>FinnEllerOpprettReferanseStubAsync</c>
/// (DB-oppslag) i forrige runde.
/// </para>
/// <para>
/// **Håndterer KUN det moderne <c>lovdata.no/dokument/{NL|SF}/{lov|forskrift}/{dato}</c>-formatet**
/// — verifisert mot ekte data (bundlingssiden på Bergens nettsted bruker nøyaktig dette formatet
/// for Alkoholloven/Alkoholforskriften, se data/kilder/raw-nettside/README.md). Faktisk observert i
/// SAMME nettside-korpus: minst to ELDRE lovdata-URL-format (<c>lovdata.no/all/nl-ÅÅÅÅMMDD-NNN.html</c>
/// og <c>lovdata.no/cgi-wift/wiftldles?doc=...</c>) — disse gir <c>null</c> her (ingen gjettet
/// fallback), klassifiseres av <see cref="NettsideTekstParser"/> som ordinær <c>lenker_til</c> i
/// stedet for <c>lovdatalenke</c>. Ekte, dokumentert begrensning — se README for full liste.
/// </para>
/// </summary>
public static partial class LovdataUrlTolker
{
    // KUN "NL" (lov) og "SF" (sentral forskrift) — bevisst IKKE "LTI" (Lovtidend-kunngjøring av en
    // ENDRINGSFORSKRIFT, observert på bevillingsgebyr-siden, se README). En LTI-URL peker på selve
    // endringen, ikke den konsoliderte forskriften, og har ingen kjent, verifisert ELI-form her —
    // å tvinge den inn i "forskrift"-segmentet ville vært nøyaktig den gjettingen §0.1 forbyr.
    [GeneratedRegex(@"^https?://(?:www\.)?lovdata\.no/dokument/(?:NL|SF)/(lov|forskrift)/(\d{4})-(\d{2})-(\d{2})(?:-(\S+))?/?(?:[?#].*)?$")]
    private static partial Regex DokumentUrlMønster();

    /// <summary>
    /// F.eks. <c>"https://lovdata.no/dokument/NL/lov/1989-06-02-27"</c> →
    /// <c>"https://lovdata.no/eli/lov/1989/06/02/27/nor"</c> — identisk streng-form til
    /// <see cref="LovdataIdentifikatorer.AvledEliFraDatokode"/>s output for datokode
    /// <c>"LOV-1989-06-02-27"</c>. Returnerer <c>null</c> for alt annet (ikke-lovdata-URL-er, eller
    /// lovdata-URL-er i et av de eldre, ikke-håndterte formatene) — ingen gjettet fallback.
    /// </summary>
    public static string? TolkTilEliKandidat(string url)
    {
        var m = DokumentUrlMønster().Match(url.Trim());
        if (!m.Success) return null;

        var segment = m.Groups[1].Value; // "lov" | "forskrift" — allerede samme segmentnavn som ELI-formen
        var aar = m.Groups[2].Value;
        var maaned = m.Groups[3].Value;
        var dag = m.Groups[4].Value;
        var lopenummer = m.Groups[5].Success ? "/" + m.Groups[5].Value : "";
        return $"https://lovdata.no/eli/{segment}/{aar}/{maaned}/{dag}{lopenummer}/nor";
    }

    /// <summary>Enkel klassifisering brukt av <see cref="NettsideTekstParser"/>: er dette i det hele
    /// tatt en lovdata.no-URL (uansett format, håndtert eller ikke)? Skiller "ordinær ekstern lenke"
    /// fra "lovdata-lenke i et format vi (ennå) ikke tolker" i statistikk/diagnostikk.</summary>
    [GeneratedRegex(@"^https?://(?:www\.)?lovdata\.no/", RegexOptions.IgnoreCase)]
    public static partial Regex ErLovdataUrl();
}
