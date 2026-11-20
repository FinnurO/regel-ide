using System.Text.RegularExpressions;

namespace RegelIde.Kildekonvertering;

/// <summary>
/// [Ny, fastsatt-av-runden, 2026-09-10, issue #215] Organet som FASTSATTE en forskrift, hentet fra
/// hjemmelslinja i header-metadataen («Annet om dokumentet» → <c>Hjemmel:</c>).
///
/// <para>
/// <b>Hvorfor ikke <c>etat</c>-feltet:</b> det ser ut som svaret, men er det ikke. Målt på 60
/// forskrifter hadde 56 <c>etat</c> satt, men bare 6 matchet en virksomhet i katalogen — resten er
/// departementets egen AVDELING («Barnehageavd.», «Skattelovavd.», «Klimaavdelingen»). Å koble
/// <c>etat</c> mot katalogen ville gitt et galt svar i 50 av 56 tilfeller. Feltet blir stående som
/// den frie teksten det er.
/// </para>
///
/// <para>
/// <b>Målt i korpuset 2026-09-10</b> (500 forskrifter, 489 med hjemmelslinje):
/// </para>
/// <list type="bullet">
///   <item>331 har «Fastsatt av {organ}» — 110 unike organer, alle ekte forvaltningsorganer.</item>
///   <item>125 har «Fastsatt <b>ved</b> kgl.res» — en annen preposisjon, og et annet organ (Kongen i
///   statsråd). Behandles egen, se <see cref="FastsattAv.ErKongenIStatsrad"/>.</item>
///   <item>33 har ingen frase i det hele tatt (f.eks. «Kunngjøring fra … av stortingsvedtak»).</item>
/// </list>
///
/// <para>
/// Tolkeren gjetter ingenting: finner den ikke frasen, returnerer den <c>null</c>, og teksten blir
/// stående i <c>annetOmDokumentet</c> som før. Den navngir heller ikke et organ den ikke leste — det
/// er kalleren som slår navnet opp mot katalogen, og et navn uten treff blir stående ukoblet.
/// </para>
/// </summary>
public static partial class FastsattAvTolker
{
    /// <param name="Tekst">Frasen som den STÅR i kilden, inkludert en eventuell «styret ved»-
    /// presisering. Det er denne som vises når ingen kobling finnes — og den skal vises da, ikke
    /// forkastes.</param>
    /// <param name="Organnavn">Navnet som skal slås opp i katalogen. For «styret ved X» er dette X:
    /// styret er organet INNAD i institusjonen, og det er institusjonen som finnes i katalogen.
    /// <c>null</c> når det ikke finnes et navn å slå opp (kgl.res).</param>
    /// <param name="ErKongenIStatsrad">«kgl.res»/«Kronprinsreg.res». Kongen i statsråd er et
    /// GRUPPEBEGREP, ikke en virksomhet, og kan derfor ikke kobles via
    /// <c>FastsattAvVirksomhetId</c>. Står ukoblet i denne runden — bevisst og dokumentert (issue #215
    /// kriterium 3), ikke et hull. Flagget finnes for at en senere runde skal kunne koble de 125
    /// radene til gruppebegrepet uten å parse teksten på nytt.</param>
    public sealed record FastsattAv(string Tekst, string? Organnavn, bool ErKongenIStatsrad);

    /// <summary>«Fastsatt av {organ}» — det som følger, fram til en av terminatorene under.</summary>
    [GeneratedRegex(@"Fastsatt\s+av\s+(?<hale>.{1,160})", RegexOptions.IgnoreCase)]
    private static partial Regex FastsattAvMønster();

    /// <summary>
    /// «Fastsatt ved kgl.res» og de tre andre resolusjonsformene. Egen preposisjon i kilden («ved»,
    /// ikke «av»), og derfor et eget mønster — ikke en variant av det over.
    ///
    /// <para>
    /// Alle fire formene er MÅLT i korpuset, ikke antatt. De forkortede («kgl.res.»,
    /// «Kronprinsreg.res.») er de vanlige; de utskrevne ble funnet ved å lete etter rader der frasen
    /// FANTES men ikke ble tolket — 3 av 120 stikkprøvde:
    /// </para>
    /// <list type="bullet">
    ///   <item>«Fastsatt ved <b>Kronprinsregentens res.</b> av 9. november 1956» (Vedtekter for
    ///   Krigsskadeskipnadens motorvognavdeling)</item>
    ///   <item>«Fastsatt ved <b>Regjeringens res.</b> 5. november 1999» (kystvaktinstruksen)</item>
    /// </list>
    /// <para>
    /// Alle fire er Kongen i statsråd, og behandles derfor likt.
    /// </para>
    /// </summary>
    [GeneratedRegex(
        @"Fastsatt\s+ved\s+(?<form>kgl\.?\s?res\.?|kronprinsreg\.?\s?res\.?|kronprinsregentens\s+res\.?|regjeringens\s+res\.?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex KglResMønster();

    /// <summary>
    /// [Ny, issue #486] «Regjeringens res.» eller «regjeringens resolusjon» i løpende tekst («opprettet ved regjeringens resolusjon 4. juli
    /// 1991», «Regjeringens res. av 5. november 1999 nr. 1145»), lest fra ordet «regjeringens». Samme vedtaksform som den siste i
    /// <see cref="KglResMønster"/>, og derfor samme organ: Kongen i statsråd («Alle fire er Kongen i statsråd»). Grl. § 32 sier hvorfor:
    /// beslutninger regjeringen fatter under kongens fravær, «utferdiges i kongens navn». Den utskrevne formen står ikke i hjemmelslinjene,
    /// men i normteksten (14 gjeldende forskrifter er fastsatt ved «Regjeringens res.», juristrunden 2026-11-18), og
    /// <see cref="KglResMønster"/> er derfor ikke endret: det ville endret tolkningen av hjemmelslinjene uten at det er målt.
    /// </summary>
    [GeneratedRegex(@"^regjeringens\s+res(?:\.|olusjon(?![\p{L}]))", RegexOptions.IgnoreCase)]
    private static partial Regex RegjeringensResolusjonMønster();

    /// <summary>[Ny, issue #486] Om teksten (fra og med ordet «regjeringens») er vedtaksformen «Regjeringens res.»: Kongen i statsråd.</summary>
    public static bool ErRegjeringensResolusjon(string tekstFraOrdet) =>
        !string.IsNullOrEmpty(tekstFraOrdet) && RegjeringensResolusjonMønster().IsMatch(tekstFraOrdet);

    /// <summary>
    /// Der organnavnet slutter. Alle formene er MÅLT i korpuset, ikke antatt: datoen («3. februar
    /// 2026»), hjemmelshalvdelen («med hjemmel i», «i medhold av»), og vanlig
    /// setningsinterpunksjon. Uten en terminator ville hele resten av hjemmelslinja blitt lest som
    /// organnavn.
    /// </summary>
    [GeneratedRegex(@"\s*(med hjemmel|i medhold|\d{1,2}\.\s*\w+\s*\d{4}|\bden\b|jf\.|,|\.\s|\.$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex TerminatorMønster();

    /// <summary>«styret ved X» / «styret for X» — X er institusjonen som finnes i katalogen.</summary>
    [GeneratedRegex(@"^styret\s+(ved|for)\s+(?<institusjon>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex StyretMønster();

    /// <summary>
    /// Tolker hjemmelslinja. <c>null</c> når den ikke inneholder noen fastsettelsesfrase — det er
    /// tilfellet for 33 av 500 målte forskrifter, og ikke en feil.
    /// </summary>
    public static FastsattAv? Tolk(string? hjemmelslinje)
    {
        if (string.IsNullOrWhiteSpace(hjemmelslinje)) return null;

        // «ved kgl.res» sjekkes FØRST: en linje kan i prinsippet inneholde begge formene, og da er
        // det kgl.res som er fastsettelsen (den andre ville vært en henvisning).
        var kgl = KglResMønster().Match(hjemmelslinje);
        if (kgl.Success)
        {
            return new FastsattAv(kgl.Groups["form"].Value.Trim(), null, ErKongenIStatsrad: true);
        }

        var m = FastsattAvMønster().Match(hjemmelslinje);
        if (!m.Success) return null;

        var hale = m.Groups["hale"].Value;
        var terminator = TerminatorMønster().Match(hale);
        var frase = (terminator.Success ? hale[..terminator.Index] : hale).Trim().TrimEnd('.', ',');
        if (frase.Length == 0) return null;

        var styret = StyretMønster().Match(frase);
        return styret.Success
            // Presiseringen «styret ved» BEVARES i teksten. At det er styret som er organet innad er
            // en opplysning kilden gir, og den skal ikke kastes bare fordi oppslaget bruker
            // institusjonsnavnet.
            ? new FastsattAv(frase, styret.Groups["institusjon"].Value.Trim(), ErKongenIStatsrad: false)
            : new FastsattAv(frase, frase, ErKongenIStatsrad: false);
    }
}
