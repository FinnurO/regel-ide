using System.Globalization;
using System.Text.RegularExpressions;

namespace RegelIde.Data;

/// <summary>Én hendelse i et organiseringsvedtak, slik mønstrene leser den ut av ett ledd. <see cref="Navn"/> er normalisert til bestemt
/// form; <see cref="Raa"/> er formen slik den står (for kunnskapsstatus: lik → tekstfunn, ellers normalisering).</summary>
public sealed record Livslopshendelse(string Type, IReadOnlyList<(string Navn, string Raa)> Navn, (string Navn, string Raa)? Fra = null,
    (string Navn, string Raa)? Til = null, string? Objekt = null, bool Utsatt = false, string Regel = "");

/// <summary>
/// [Ny, issue #395 del 3, 2026-10-23] Mønstrene som leser departementenes livsløp ut av organiseringsvedtakene. Rene funksjoner uten
/// database; <see cref="DepartementLivslopTjeneste"/> setter hendelsene sammen til identiteter over tid.
/// <para>
/// <b>Hva de leser</b> (målt på Lovtidend-arkivet 2001–2025 og korpusets konsoliderte vedtak 2026-10-23 — formen er den samme i alle
/// kgl.res. om departementsstrukturen fra 2001 til 2019):
/// </para>
/// <list type="bullet">
/// <item>«Opprettelse av et Kultur- og kirkedepartement, et Utdannings- og forskningsdepartement …» / «Opprettelse av
/// Nærings- og fiskeridepartementet (NFD).»</item>
/// <item>«Nedleggelse av Sosialdepartementet, Helsedepartementet og Arbeids- og administrasjonsdepartementet.»</item>
/// <item>«Landbruksdepartementets navn endres til Landbruks- og matdepartementet», «Justis- og politidepartementets navn endres fra
/// 1. januar 2012 til …», «Fiskeridepartementet endrer navn til …», «Miljøverndepartementet skifter navn til …».</item>
/// <item>«Forvaltningsansvaret for lov 29. april 2005 nr. 20 om … overføres fra Arbeids- og sosialdepartementet til
/// Finansdepartementet.» og tittelformen «Overføring av forvaltningsansvaret for … fra … til …».</item>
/// <item>Vedleggene «Vedlegg 1. Oversikt over lover Arbeids- og sosialdepartementet skal ha forvaltningsansvaret for» med én lov per
/// punkt («Lov 17. juli 1925 nr. 11 om Svalbard, § 4»).</item>
/// </list>
/// <para>
/// <b>Hva de IKKE gjør (CLAUDE.md §8).</b> De leser bare departementer (navnet ender på «departement»/«departementet»); direktorater,
/// avdelinger og statsrådsfordelinger («Statsråd X har ansvaret for …») står utenfor piloten. En navneliste deles bare der hvert ledd
/// er et helt departementsnavn; noe annet i lista gir ingen hendelse. Et navn med skrivefeil i kilden («Kommunal- og
/// regionaldepartementel», FOR-2013-12-13-1455 punkt 7) matcher ikke og rapporteres, det rettes ikke.
/// </para>
/// </summary>
public static partial class DepartementLivslopMonstre
{
    public const string Oppretting = "oppretting";
    public const string Nedleggelse = "nedleggelse";
    public const string Navnebytte = "navnebytte";
    public const string Overforing = "overforing";

    // Ett departementsnavn: «Kulturdepartementet», «Arbeids- og sosialdepartementet», «Barne-, likestillings- og inkluderingsdepartementet»,
    // også ubestemt («et Helse- og omsorgsdepartement») og genitiv («Landbruksdepartementets»). Delene før siste ledd ender på bindestrek.
    private const string Dept = @"\p{Lu}\p{Ll}*(?:-(?:,|\s+og)\s+\p{Ll}+)*?departement(?:ets|et|s)?\b";

    [GeneratedRegex(Dept, RegexOptions.CultureInvariant)] private static partial Regex DeptRegex();

    [GeneratedRegex(@"^\s*Opprettelse av (?<liste>.+)$", RegexOptions.CultureInvariant)] private static partial Regex OpprettRegex();
    [GeneratedRegex(@"^\s*Nedleggelse av (?<liste>.+)$", RegexOptions.CultureInvariant)] private static partial Regex NedleggRegex();
    // «Det opprettes et Digitaliserings- og forvaltningsdepartementet med virkning fra 1. januar 2024.» (FOR-2023-12-20-2166)
    [GeneratedRegex(@"\bDet opprettes (?:et |en )?(?<d>" + Dept + ")", RegexOptions.CultureInvariant)] private static partial Regex DetOpprettesRegex();

    [GeneratedRegex(@"(?<fra>" + Dept + @")\s+navn\s+endres\s+(?:fra\s+\d{1,2}\.\s+\p{Ll}+\s+\d{4}\s+)?til\s+(?<til>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex NavnEndresRegex();
    [GeneratedRegex(@"(?<fra>" + Dept + @")\s+(?:endrer|skifter)\s+navn\s+(?:fra\s+\d{1,2}\.\s+\p{Ll}+\s+\d{4}\s+)?til\s+(?<til>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex EndrerNavnRegex();
    // Omvendt ordstilling etter et adverbial: «Med virkning fra 1. januar 2024 endrer Olje- og energidepartementet navn til Energidepartementet».
    [GeneratedRegex(@"\b(?:endrer|skifter)\s+(?<fra>" + Dept + @")\s+navn\s+til\s+(?<til>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex EndrerXNavnRegex();

    [GeneratedRegex(@"[Ff]orvaltningsansvaret for (?<obj>.+?) (?:overføres|overførast) fra (?<fra>" + Dept + @") til (?<til>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex OverforesRegex();
    [GeneratedRegex(@"^\s*Overføring av forvaltningsansvaret for (?<obj>.+?) fra (?<fra>" + Dept + @") til (?<til>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex OverforingTittelRegex();

    [GeneratedRegex(@"\biverksettes fra det tidspunkt\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UtsattRegex();

    // [ENDRET etter måling 2026-10-23] Overskriftene i korpusets omorganiseringsvedtak 1990–2024 har flere former (alle målt):
    // «Oversikt over lover (som) X skal ha (overført) forvaltningsansvaret for», «Oversikter over lover …», «Oversikt over lover som X har
    // forvaltningsansvar for etter endringene», «Lover X skal ha forvaltningsansvaret for», og overføringsformen «Overføring av
    // forvaltningsansvaret for lover til X», «Lover som overføres fra A til X», «Forvaltningsansvaret for følgende lover overføres fra A til X».
    // Overskrifter uten departement («Overføring av forvaltningen av lover, myndighet, fullmakter …», «Lover») gir ingen tildeling.
    [GeneratedRegex(@"(?:Oversikt(?:er)? over lover (?:som )?|^\s*(?:Vedlegg[^.]*\.\s*)?Lover (?:som )?)(?<d>" + Dept + @") (?:skal ha|har)\s+(?:overført\s+)?(?:forvaltningsansvaret|forvaltningsansvar|ansvaret|ansvar)\b", RegexOptions.CultureInvariant)]
    private static partial Regex VedleggRegex();
    [GeneratedRegex(@"(?:[Ff]orvaltningsansvaret for (?:følgende )?lover|^\s*Lover som overføres|[Ll]over[^.]*?som overføres)\b[^.]*?\btil (?:det nye )?(?<d>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex VedleggOverforingRegex();

    // «Lov 17. juli 1925 nr. 11 om Svalbard», «lov av 3. juni 1994 nr. 15», «Midlertidig lov 29. juni 1951 nr. 34», «Lov 3. mai 1957 om …» (uten nr).
    [GeneratedRegex(@"\b[Ll]ov(?:\s+av)?\s+(?<d>\d{1,2})\.\s*(?<m>januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)\s+(?<y>\d{4})(?:\.?\s*nr\.?\s*(?<n>\d+))?", RegexOptions.CultureInvariant)]
    private static partial Regex LovRegex();

    private static readonly string[] Maneder =
        ["januar", "februar", "mars", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "desember"];

    /// <summary>Bestemt form: «et Helse- og omsorgsdepartement» → «Helse- og omsorgsdepartementet», «Landbruksdepartementets» →
    /// «Landbruksdepartementet». Ingen annen omskriving.</summary>
    public static string Normaliser(string raa)
    {
        var s = raa.Trim();
        if (s.EndsWith("departementets", StringComparison.Ordinal)) return s[..^1];
        if (s.EndsWith("departements", StringComparison.Ordinal)) return s[..^1] + "et";
        if (s.EndsWith("departement", StringComparison.Ordinal)) return s + "et";
        return s;
    }

    /// <summary>Delt navneliste («A, B og C», «et A, et B og et C», med forkortelser i parentes). Null når lista inneholder noe annet enn
    /// departementsnavn — da gjettes ingen deling.</summary>
    public static IReadOnlyList<(string Navn, string Raa)>? Navneliste(string liste)
    {
        var rest = Regex.Replace(liste.Trim().TrimEnd('.', ' '), @"\s*\([^()]*\)", "");
        var navn = new List<(string, string)>();
        var pos = 0;
        foreach (Match m in DeptRegex().Matches(rest))
        {
            var mellom = rest[pos..m.Index].Trim();
            if (mellom is not ("" or "et" or "en" or "," or ", et" or ", en" or "og" or "og et" or "og en" or ", og"))
            {
                return null;
            }
            navn.Add((Normaliser(m.Value), m.Value));
            pos = m.Index + m.Length;
        }
        return navn.Count > 0 && rest[pos..].Trim().Length == 0 ? navn : null;
    }

    /// <summary>Hendelsene i ett ledd. Tom liste = ingen livsløpshendelse (eller en som ikke lot seg lese sikkert — se
    /// <paramref name="ulest"/>).</summary>
    public static IReadOnlyList<Livslopshendelse> LesLedd(string tekst, out string? ulest)
    {
        ulest = null;
        var h = new List<Livslopshendelse>();
        // «Opprettelse av Næringsdepartementet og nedleggelse av Industridepartementet.» (FOR-1987-12-18) — to hendelser i ett ledd.
        var deler = Regex.Split(tekst, @"\s+og\s+(?=nedleggelse av\b)", RegexOptions.CultureInvariant);
        if (deler.Length == 2)
        {
            var a = LesLedd(deler[0], out var ua);
            var b = LesLedd(char.ToUpperInvariant(deler[1][0]) + deler[1][1..], out var ub);
            ulest = ua ?? ub;
            return [.. a, .. b];
        }
        if (OpprettRegex().Match(tekst) is { Success: true } o && DeptRegex().IsMatch(o.Groups["liste"].Value))
        {
            if (Navneliste(o.Groups["liste"].Value) is { } liste) h.Add(new(Oppretting, liste, Regel: "monster:livslop-opprettelse-av"));
            else ulest = "opprettelse: lista inneholder annet enn departementsnavn";
        }
        if (h.Count == 0 && DetOpprettesRegex().Match(tekst) is { Success: true } d)
        {
            h.Add(new(Oppretting, [(Normaliser(d.Groups["d"].Value), d.Groups["d"].Value)], Regel: "monster:livslop-det-opprettes"));
        }
        if (NedleggRegex().Match(tekst) is { Success: true } n && DeptRegex().IsMatch(n.Groups["liste"].Value))
        {
            if (Navneliste(n.Groups["liste"].Value) is { } liste) h.Add(new(Nedleggelse, liste, Regel: "monster:livslop-nedleggelse-av"));
            else ulest = "nedleggelse: lista inneholder annet enn departementsnavn";
        }
        var utsatt = UtsattRegex().IsMatch(tekst);
        foreach (var (r, regel) in new[] { (NavnEndresRegex(), "monster:livslop-navn-endres-til"), (EndrerNavnRegex(), "monster:livslop-endrer-navn-til"),
                     (EndrerXNavnRegex(), "monster:livslop-endrer-x-navn-til") })
        {
            foreach (Match m in r.Matches(tekst))
            {
                var fra = m.Groups["fra"].Value;
                var til = m.Groups["til"].Value;
                h.Add(new(Navnebytte, [], (Normaliser(fra), fra), (Normaliser(til), til), Utsatt: utsatt, Regel: regel));
            }
        }
        foreach (Match m in OverforesRegex().Matches(tekst))
        {
            h.Add(new(Overforing, [], (Normaliser(m.Groups["fra"].Value), m.Groups["fra"].Value),
                (Normaliser(m.Groups["til"].Value), m.Groups["til"].Value), m.Groups["obj"].Value.Trim(), Regel: "monster:livslop-forvaltningsansvaret-overfores"));
        }
        if (h.Count == 0 && ulest is null && Regex.IsMatch(tekst, @"\bnavn\b.*\b(endres|endrer|skifter)\b|\b(endrer|skifter)\b.*\bnavn\b") && tekst.Contains("departement"))
        {
            ulest = "navnebytte: ordlyden har navn og departement, men ikke et lesbart «X endrer navn til Y»";
        }
        return h;
    }

    /// <summary>Tittelformen «Overføring av forvaltningsansvaret for … fra A til B».</summary>
    public static Livslopshendelse? LesOverforingstittel(string tittel) =>
        OverforingTittelRegex().Match(tittel) is { Success: true } m
            ? new(Overforing, [], (Normaliser(m.Groups["fra"].Value), m.Groups["fra"].Value),
                (Normaliser(m.Groups["til"].Value), m.Groups["til"].Value), m.Groups["obj"].Value.Trim(), Regel: "monster:livslop-overforing-tittel")
            : null;

    /// <summary>Departementet i en vedleggsoverskrift, og om overskriften bruker funksjonsordet «forvaltningsansvaret».</summary>
    public static (string Navn, string Raa, bool Funksjonsord)? LesVedleggsoverskrift(string overskrift)
    {
        var m = VedleggRegex().Match(overskrift);
        if (!m.Success) m = VedleggOverforingRegex().Match(overskrift);
        return m.Success
            ? (Normaliser(m.Groups["d"].Value), m.Groups["d"].Value, overskrift.Contains("forvaltningsansvar", StringComparison.OrdinalIgnoreCase))
            : null;
    }

    /// <summary>Datokoden til den FØRSTE loven teksten nevner med dato (og nummer når det står), f.eks. «LOV-1925-07-17-11». Null når
    /// teksten ikke nevner en lov med dato — da settes ingen ELI.</summary>
    public static string? LovDatokode(string tekst)
    {
        var m = LovRegex().Match(tekst);
        if (!m.Success) return null;
        var mnd = Array.IndexOf(Maneder, m.Groups["m"].Value) + 1;
        var dag = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
        var ar = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
        if (mnd < 1 || dag < 1 || dag > 31) return null;
        var kode = $"LOV-{ar:D4}-{mnd:D2}-{dag:D2}";
        return m.Groups["n"].Success ? $"{kode}-{int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture)}" : kode;
    }
}
