using System.Globalization;
using System.Text.RegularExpressions;

namespace RegelIde.Data;

/// <summary>Én hendelse i et organiseringsvedtak, slik mønstrene leser den ut av ett ledd. <see cref="Navn"/> er normalisert til bestemt
/// form; <see cref="Raa"/> er formen slik den står (for kunnskapsstatus: lik → tekstfunn, ellers normalisering).</summary>
// [ENDRET, Johanns avgjørelse U1 på #397] + Virkning: datoen leddet selv sier («Med virkning fra 1. januar 1988 …»). Gjelder bare der teksten
// sier det; ellers brukes vedtakets dato.
public sealed record Livslopshendelse(string Type, IReadOnlyList<(string Navn, string Raa)> Navn, (string Navn, string Raa)? Fra = null,
    (string Navn, string Raa)? Til = null, string? Objekt = null, bool Utsatt = false, string Regel = "", DateOnly? Virkning = null);

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
    /// <summary>[Ny, Johanns avgjørelse P1 på #397] Opphør uten ordet «nedlegges» («overtar alle arbeidsoppgavene til det nåværende X»,
    /// «det tidligere X»). Gir R avvikler med kunnskapsstatus fortolkning.</summary>
    public const string Opphor = "opphor";
    /// <summary>[Ny, Johanns avgjørelse P2 på #397] «Y overtar … fra X» (Fra = X, Til = Y). Gir R etterfolger når X opphører i samme vedtak.</summary>
    public const string Overtar = "overtar";

    // Ett departementsnavn: «Kulturdepartementet», «Arbeids- og sosialdepartementet», «Barne-, likestillings- og inkluderingsdepartementet»,
    // også ubestemt («et Helse- og omsorgsdepartement») og genitiv («Landbruksdepartementets»). Delene før siste ledd ender på bindestrek.
    private const string Dept = @"\p{Lu}\p{Ll}*(?:-(?:,|\s+og)\s+\p{Ll}+)*?departement(?:ets|et|s)?\b";

    [GeneratedRegex(Dept, RegexOptions.CultureInvariant)] private static partial Regex DeptRegex();

    [GeneratedRegex(@"^\s*Opprettelse av (?<liste>.+)$", RegexOptions.CultureInvariant)] private static partial Regex OpprettRegex();
    [GeneratedRegex(@"^\s*Nedleggelse av (?<liste>.+)$", RegexOptions.CultureInvariant)] private static partial Regex NedleggRegex();
    // «Det opprettes et Digitaliserings- og forvaltningsdepartementet med virkning fra 1. januar 2024.» (FOR-2023-12-20-2166)
    [GeneratedRegex(@"\bDet opprettes (?:et |en )?(?:nytt departement(?:et)?,\s*)?(?<d>" + Dept + ")", RegexOptions.CultureInvariant)] private static partial Regex DetOpprettesRegex();

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

    // [Ny, issue #397] Formene i vedtakene fra 1990 og 1993 (FOR-1989-12-22-1273, FOR-1992-12-18-1074, konsoliderte): «Med virkning fra
    // 1. januar 1990 opprettes fire nye departementer med funksjoner og myndighet som beskrevet i foredraget og vedlegg til denne
    // resolusjon: A, B, C samt D.» og «Fra samme dato nedlegges A, B og C.»
    // [ENDRET etter juristrunden på #397] + kommaformen i FOR-1972-05-05-8645: «… opprettes to nye departementer, Miljøverndepartementet og
    // Forbruker- og administrasjonsdepartementet [med myndighetsområde som nevnt i foredraget …]». Juristrunde 2: lista slutter ved den faste
    // formelen «med funksjoner …»/«med myndighet…», som i 1990-formen står foran kolonet.
    [GeneratedRegex(@"\bopprettes\s+(?:\p{L}+\s+)?nye\s+departementer\b(?:[^:]*:|,)\s*(?<liste>.+?)(?:\s+med\s+(?:funksjoner|myndighet\w*)\b.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex OpprettesNyeRegex();
    [GeneratedRegex(@"\bnedlegges\s+(?<liste>.+)$", RegexOptions.CultureInvariant)] private static partial Regex NedleggesRegex();
    // [Ny, issue #397] Navnebytte som liste (FOR-1990-12-21-1022 punkt 2): «Med virkning fra 1. januar 1991 endrer følgende departementer
    // navn; Kirke- og kulturdepartementet til Kulturdepartementet, Familie- og forbrukerdepartementet til Barne- og familiedepartementet, og
    // Utdannings- og forskningsdepartementet til Kirke-, utdannings- og forskningsdepartementet.»
    [GeneratedRegex(@"\bendrer\s+følgende\s+departementer\s+navn\s*[;:,]\s*(?<liste>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NavnelisteEndresRegex();
    [GeneratedRegex(@"(?<fra>" + Dept + @")\s+til\s+(?<til>" + Dept + ")", RegexOptions.CultureInvariant)] private static partial Regex FraTilRegex();
    // [Ny, issue #397] Vedtakene fra 1997 og 1998 (FOR-1996-12-20-1156, FOR-1997-12-19-1316) har en overskrift per nytt departement
    // («Nytt Planleggings- og samordningsdepartement (PSD)») og i samme kapittel «Planleggings- og samordningsdepartementet organiseres
    // ved opprettelsen i 5 avdelinger». Begge må stå; overskriften alene er ikke nok.
    [GeneratedRegex(@"^\s*Nytt\s+(?<d>" + Dept + @")\s*(?:\([^()]*\))?\s*$", RegexOptions.CultureInvariant)] private static partial Regex NyttRegex();
    [GeneratedRegex(@"^\s*(?<d>" + Dept + @")\s+organiseres\s+ved\s+opprettelsen\b", RegexOptions.CultureInvariant)]
    private static partial Regex OrganiseresVedOpprettelsenRegex();
    // [Ny, issue #397] Et kapittel som bare heter et departement, under et «Vedlegg» (FOR-2018-04-27-638, FOR-2019-05-03-560).
    [GeneratedRegex(@"^\s*(?<d>" + Dept + @")\s*$", RegexOptions.CultureInvariant)] private static partial Regex BareDepartementRegex();

    // [Ny, Johanns avgjørelse U1 på #397] «Med virkning fra 1. januar 1988 …» i leddet.
    [GeneratedRegex(@"\b[Mm]ed\s+virkning\s+fra\s+(?<d>\d{1,2})\.\s*(?<m>januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)\s+(?<y>\d{4})", RegexOptions.CultureInvariant)]
    private static partial Regex VirkningRegex();
    // [Ny, Johanns avgjørelse P1 på #397] Opphør uten «nedlegges» (FOR-1996-12-20-1156, FOR-1997-12-19-1316).
    [GeneratedRegex(@"\bovertar\s+(?:alle\s+arbeidsoppgavene|de\s+resterende\s+oppgavene)\s+til\s+(?:det\s+(?:nåværende|tidligere)\s+)?(?<x>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex OvertarAlleRegex();
    [GeneratedRegex(@"\bdet\s+tidligere\s+(?<x>" + Dept + ")", RegexOptions.CultureInvariant)] private static partial Regex DetTidligereRegex();
    // [Ny, Johanns avgjørelse P2 på #397] «Y (FORK) overtar … fra [det nåværende] X» og «Y overtar alle arbeidsoppgavene til X».
    [GeneratedRegex(@"(?<y>" + Dept + @")(?:\s*\([^()]*\))?\s+overtar\b[^.]*?\bfra\s+(?:det\s+(?:nåværende|tidligere)\s+)?(?<x>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex OvertarFraRegex();
    [GeneratedRegex(@"(?<y>" + Dept + @")(?:\s*\([^()]*\))?\s+overtar\s+(?:alle\s+arbeidsoppgavene|de\s+resterende\s+oppgavene)\s+til\s+(?:det\s+(?:nåværende|tidligere)\s+)?(?<x>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex OvertarAlleTilRegex();
    // [Ny, Johanns avgjørelse U2 på #397] FOR-1989-12-22-1273 vedlegg 4: «Forvaltningen av følgende lover overføres fra X» under «Til Y overføres følgende:».
    [GeneratedRegex(@"^\s*Forvaltningen\s+av\s+følgende\s+lover\s+overføres\s+fra\s+(?<x>" + Dept + ")", RegexOptions.CultureInvariant)]
    private static partial Regex ForvaltningenOverforesFraRegex();
    [GeneratedRegex(@"^\s*Til\s+(?<y>" + Dept + @")\s+overføres\s+følgende", RegexOptions.CultureInvariant)] private static partial Regex TilOverforesFolgendeRegex();

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
    // [Ny, issue #397] Tittelen før datoen: «Lov om folkebibliotek av 20. desember 1985 nr. 108.» (FOR-1989-12-22-1273 vedlegg 3),
    // «Lov om grunnskolen og den vidaregåande opplæringa (opplæringslova) av 17. juli 1998 nr. 61» (FOR-2001-12-21-1485 vedlegg 2).
    // Brukes bare når teksten BEGYNNER med «Lov om» og verken en annen lov eller «endring» står før datoen («Lov om endring i … av …»: datoen kan høre til den endrede loven).
    [GeneratedRegex(@"^\s*(?:Midlertidig\s+)?[Ll]ov\s+om\s+(?:(?!\b[Ll]ov\b|[Ee]ndring)[^;])*?\s+av\s+(?<d>\d{1,2})\.\s*(?<m>januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)\s+(?<y>\d{4})(?:\.?\s*nr\.?\s*(?<n>\d+))?", RegexOptions.CultureInvariant)]
    private static partial Regex LovTittelForstRegex();

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
            // [ENDRET, issue #397] + «samt» (FOR-1989-12-22-1273: «… Utdannings- og forskningsdepartementet samt Kirke- og kulturdepartementet»).
            if (mellom is not ("" or "et" or "en" or "," or ", et" or ", en" or "og" or "og et" or "og en" or ", og" or "samt" or ", samt" or "samt et"))
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
        var h = LesLeddUtenVirkning(tekst, out ulest);
        // [Ny, Johanns avgjørelse U1 på #397] Sier leddet «med virkning fra <dato>», gjelder den datoen for hendelsene i leddet.
        return Virkning(tekst) is { } v ? h.Select(x => x with { Virkning = v }).ToList() : h;
    }

    /// <summary>[Ny, issue #397 U1] Datoen i «med virkning fra D. måned ÅÅÅÅ», ellers null.</summary>
    public static DateOnly? Virkning(string tekst)
    {
        var m = VirkningRegex().Match(tekst);
        if (!m.Success) return null;
        var mnd = Array.IndexOf(Maneder, m.Groups["m"].Value) + 1;
        var dag = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
        var ar = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
        try { return new DateOnly(ar, mnd, dag); } catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>[Ny, issue #397 P1] Opphør i en overskrift («Organisatorisk deling av det tidligere Nærings- og energidepartementet»).</summary>
    public static IReadOnlyList<Livslopshendelse> LesOverskrift(string overskrift) =>
        DetTidligereRegex().Match(overskrift) is { Success: true } m
            ? [new(Opphor, [(Normaliser(m.Groups["x"].Value), m.Groups["x"].Value)], Regel: "monster:livslop-det-tidligere")]
            : [];

    /// <summary>[Ny, issue #397 U2] «Forvaltningen av følgende lover overføres fra X» → X, ellers null.</summary>
    public static (string Navn, string Raa)? LesForvaltningenOverforesFra(string tekst) =>
        ForvaltningenOverforesFraRegex().Match(tekst) is { Success: true } m ? (Normaliser(m.Groups["x"].Value), m.Groups["x"].Value) : null;

    /// <summary>[Ny, issue #397 U2] «Til Y overføres følgende:» → Y, ellers null.</summary>
    public static (string Navn, string Raa)? LesTilOverforesFolgende(string overskrift) =>
        TilOverforesFolgendeRegex().Match(overskrift) is { Success: true } m ? (Normaliser(m.Groups["y"].Value), m.Groups["y"].Value) : null;

    private static IReadOnlyList<Livslopshendelse> LesLeddUtenVirkning(string tekst, out string? ulest)
    {
        ulest = null;
        var h = new List<Livslopshendelse>();
        // «Opprettelse av Næringsdepartementet og nedleggelse av Industridepartementet.» (FOR-1987-12-18) — to hendelser i ett ledd.
        // [ENDRET etter juristrunden på #397] + «nedlegges Industridepartementet og det opprettes et nytt departementet, Næringsdepartementet»
        // (FOR-1987-12-18-971).
        var deler = Regex.Split(tekst, @"\s+og\s+(?=nedleggelse av\b|det opprettes\b)", RegexOptions.CultureInvariant);
        if (deler.Length == 2)
        {
            var a = LesLeddUtenVirkning(deler[0], out var ua);
            var b = LesLeddUtenVirkning(char.ToUpperInvariant(deler[1][0]) + deler[1][1..], out var ub);
            ulest = ua ?? ub;
            return [.. a, .. b];
        }
        if (OpprettRegex().Match(tekst) is { Success: true } o && DeptRegex().IsMatch(o.Groups["liste"].Value))
        {
            if (Navneliste(o.Groups["liste"].Value) is { } liste) h.Add(new(Oppretting, liste, Regel: "monster:livslop-opprettelse-av"));
            else ulest = "opprettelse: lista inneholder annet enn departementsnavn";
        }
        if (h.Count == 0 && OpprettesNyeRegex().Match(tekst) is { Success: true } on)
        {
            if (Navneliste(on.Groups["liste"].Value) is { } liste) h.Add(new(Oppretting, liste, Regel: "monster:livslop-opprettes-nye-departementer"));
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
        if (!h.Any(x => x.Type == Nedleggelse) && NedleggesRegex().Match(tekst) is { Success: true } nl && DeptRegex().IsMatch(nl.Groups["liste"].Value))
        {
            if (Navneliste(nl.Groups["liste"].Value) is { } liste) h.Add(new(Nedleggelse, liste, Regel: "monster:livslop-nedlegges"));
            else ulest ??= "nedleggelse: lista inneholder annet enn departementsnavn";
        }
        var utsatt = UtsattRegex().IsMatch(tekst);
        if (NavnelisteEndresRegex().Match(tekst) is { Success: true } nle)
        {
            if (NavnebytteListe(nle.Groups["liste"].Value) is { } par)
            {
                foreach (var (fra, til) in par)
                {
                    h.Add(new(Navnebytte, [], (Normaliser(fra), fra), (Normaliser(til), til), Utsatt: utsatt, Regel: "monster:livslop-endrer-folgende-navn"));
                }
            }
            else ulest ??= "navnebytte: lista har noe annet enn «A til B»-par av departementsnavn";
        }
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
        // [Ny, Johanns avgjørelser P1 og P2 på #397] Opphør uten «nedlegges» og «Y overtar … fra X». Tjenesten avgjør om X faktisk
        // opphører (lever X i dag, eller opphører ikke i samme vedtak, er det en vanlig overføring og gir ingenting).
        foreach (Match m in OvertarAlleRegex().Matches(tekst))
        {
            h.Add(new(Opphor, [(Normaliser(m.Groups["x"].Value), m.Groups["x"].Value)], Regel: "monster:livslop-overtar-alle-oppgavene-til"));
        }
        foreach (Match m in DetTidligereRegex().Matches(tekst))
        {
            h.Add(new(Opphor, [(Normaliser(m.Groups["x"].Value), m.Groups["x"].Value)], Regel: "monster:livslop-det-tidligere"));
        }
        foreach (var (r, regel) in new[] { (OvertarFraRegex(), "monster:livslop-overtar-fra"), (OvertarAlleTilRegex(), "monster:livslop-overtar-alle-oppgavene-til") })
        {
            foreach (Match m in r.Matches(tekst))
            {
                if (Normaliser(m.Groups["x"].Value) == Normaliser(m.Groups["y"].Value)) continue;
                h.Add(new(Overtar, [], (Normaliser(m.Groups["x"].Value), m.Groups["x"].Value), (Normaliser(m.Groups["y"].Value), m.Groups["y"].Value),
                    Objekt: tekst.Trim(), Regel: regel));
            }
        }
        foreach (Match m in OverforesRegex().Matches(tekst))
        {
            h.Add(new(Overforing, [], (Normaliser(m.Groups["fra"].Value), m.Groups["fra"].Value),
                (Normaliser(m.Groups["til"].Value), m.Groups["til"].Value), m.Groups["obj"].Value.Trim(), Regel: "monster:livslop-forvaltningsansvaret-overfores"));
        }
        // [Ny etter juristrunden på #397] «slås … sammen til ett departement» (FOR-1989-12-22-1273 kap-IV) leses ikke, men rapporteres.
        if (h.Count == 0 && ulest is null && Regex.IsMatch(tekst, @"\bslås\b.*\bsammen\b") && tekst.Contains("departement"))
        {
            ulest = "sammenslåing: «slås … sammen» leses ikke (hvem som opphører, sier ikke teksten med et mønster vi har)";
        }
        if (h.Count == 0 && ulest is null && Regex.IsMatch(tekst, @"\bnavn\b.*\b(endres|endrer|skifter)\b|\b(endrer|skifter)\b.*\bnavn\b") && tekst.Contains("departement"))
        {
            ulest = "navnebytte: ordlyden har navn og departement, men ikke et lesbart «X endrer navn til Y»";
        }
        return h;
    }

    /// <summary>[Ny, issue #397] «A til B, C til D, og E til F» → parene. Null når lista inneholder noe annet enn slike par.</summary>
    public static IReadOnlyList<(string Fra, string Til)>? NavnebytteListe(string liste)
    {
        var rest = Regex.Replace(liste.Trim().TrimEnd('.', ' '), @"\s*\([^()]*\)", "");
        var par = new List<(string, string)>();
        var pos = 0;
        foreach (Match m in FraTilRegex().Matches(rest))
        {
            if (rest[pos..m.Index].Trim() is not ("" or "," or "og" or ", og")) return null;
            par.Add((m.Groups["fra"].Value, m.Groups["til"].Value));
            pos = m.Index + m.Length;
        }
        return par.Count > 0 && rest[pos..].Trim().Length == 0 ? par : null;
    }

    /// <summary>[Ny, issue #397] Departementet i en overskrift «Nytt X (FORK)», ellers null.</summary>
    public static (string Navn, string Raa)? LesNyttDepartement(string overskrift) =>
        NyttRegex().Match(overskrift) is { Success: true } m ? (Normaliser(m.Groups["d"].Value), m.Groups["d"].Value) : null;

    /// <summary>[Ny, issue #397] Departementet i «X organiseres ved opprettelsen …», ellers null.</summary>
    public static (string Navn, string Raa)? LesOrganiseresVedOpprettelsen(string tekst) =>
        OrganiseresVedOpprettelsenRegex().Match(tekst) is { Success: true } m ? (Normaliser(m.Groups["d"].Value), m.Groups["d"].Value) : null;

    /// <summary>[Ny, issue #397] En overskrift som bare er et departementsnavn («Utenriksdepartementet»), ellers null.</summary>
    public static (string Navn, string Raa)? LesBareDepartement(string overskrift) =>
        BareDepartementRegex().Match(overskrift) is { Success: true } m ? (Normaliser(m.Groups["d"].Value), m.Groups["d"].Value) : null;

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
        if (!m.Success) m = LovTittelForstRegex().Match(tekst);
        if (!m.Success) return null;
        var mnd = Array.IndexOf(Maneder, m.Groups["m"].Value) + 1;
        var dag = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
        var ar = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
        if (mnd < 1 || dag < 1 || dag > 31) return null;
        var kode = $"LOV-{ar:D4}-{mnd:D2}-{dag:D2}";
        return m.Groups["n"].Success ? $"{kode}-{int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture)}" : kode;
    }
}
