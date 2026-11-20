using System.Text.RegularExpressions;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>[Ny, issue #486] Hva en ordlydsregel sa om én forekomst av «regjeringen»/«regjeringens»: lesningen
/// (<see cref="Forekomstlesninger"/>), regelkoden (<see cref="RegjeringenOrdlydsregler.Regler"/>) og ordene som slo til.</summary>
public sealed record RegjeringenTreff(string Lesning, string Regel, string Grunnlag);

/// <summary>
/// [Ny, issue #486, Johanns avgjørelse 2026-11-20 etter juristrunden i <c>data/fasit/strukturmodell/regjeringen/</c>] <b>Ordlydsreglene for
/// «regjeringen».</b> Ordet betyr ulike ting i ulike setninger: Grunnloven bruker det om styringen av riket (§§ 9, 13, 40, 41, 43, 44), om
/// statsrådskollegiet (§§ 76, 82) og om medlemmene (§ 72), og Dok. 19 (2011–2012) pkt. 3.4.4 sier at ordet er flertydig. Det er derfor ikke en
/// navneform for statsrådet eller Kongen i statsråd. Hver regel leser ordlyden rundt én forekomst og setter en tagg per forekomst
/// (<see cref="NavneformBindingEntitet.TekstTaggId"/>) som forslag. Reglene prøves i denne rekkefølgen, og den første som slår til, gjelder:
/// <list type="number">
/// <item><b>Fremmed stat</b> (prøves først): «regjeringen i X» der X er et navn som ikke er Norge
/// («regjeringen i Republikken Ungarn», unntatt «regjeringen i Kongeriket Norge»); «regjeringen i tredjestaten»; «den <i>nasjonalitet</i>-ske
/// regjeringen» (ikke «den norske»); en stats genitiv foran («Belarus' regjeringen»); «regjeringen for <i>Navn</i>» («Regjeringen for Det Forente
/// Kongerike»); og til sist resten av en sanksjonsforskrift, avgrenset ved tittel (ordlyden går foran tittelen, juristmålingen m1).</item>
/// <item><b>Generisk konvensjonsstat</b>: maler og definisjoner som gjelder alle statene, også Norge: «regjeringen i den staten hvis flagg …»,
/// «i en annen stat», «i det land som …», «i hver av statene», «i medlemsstaten», «den regjeringen som …», og skjemamaler med tomrom («regjeringen
/// i ... (statens offisielle betegnelse)»). Ingen kant, og ikke «fremmed» (juristens tverrgående funn r7/r18/r26/r35).</item>
/// <item><b>Ikke aktør</b>: nettstedet («regjeringen.no»), en avtaletittel («Avtale mellom regjeringen i Kongeriket Norge og …») og en lukket
/// liste over navn der ordet er en navnedel («Regjeringens sikkerhetsutvalg»). Regelen er IKKE et prefiks på «Regjeringens …» (juristens retting
/// av R): «Regjeringens departementer» og «Regjeringens/Samferdselsdepartementets» er ikke navn.</item>
/// <item><b>Rollen statsråd</b>: «medlem(mer) av [X, Y og] regjeringen», også i sideordnede oppramsinger («medlemmer av Kongehuset, Stortinget,
/// regjeringen, Høyesterett»), og «regjeringens (ordinære) medlemmer». Ikke «statsråder, statssekretærer eller politiske rådgivere i
/// regjeringen» (r14: Grl. § 72 tredje ledd holder gruppene fra hverandre).</item>
/// <item><b>Funksjonen</b>, styret av riket: «føre», «forestå», «tiltre», «ivareta», «bestyre» eller «anordne» regjeringen.</item>
/// <item><b>«Regjeringens res.»</b> er vedtaksformen Kongen i statsråd (Grl. § 32), lest med <see cref="FastsattAvTolker.ErRegjeringensResolusjon"/>.
/// En russisk «regjeringens resolusjon» i en sanksjonsforskrift er fanget av regel 1 først (juristens innvending R).</item>
/// </list>
/// Alt annet er <see cref="Forekomstlesninger.Slutning"/>: et forslag uten referent som et menneske avgjør. Det gjelder også «Regjeringen skal
/// legge fram for Stortinget …»: om plikten ligger hos statsrådet eller Kongen i statsråd, er ikke avgjort (Johann 2026-11-20), og det gjettes
/// ikke. <see cref="Signal"/> sier bare hvilke signalord som står der, som opplysning.
/// <para><b>Kode, ikke data</b> (samme valg som <see cref="Ordsperrer"/> og standardreglene): lista er lukket, og en ny regel er et vedtak.</para>
/// </summary>
public static class RegjeringenOrdlydsregler
{
    public const string FremmedSanksjonstittel = "fremmed:sanksjonstittel";
    public const string FremmedRegjeringenIStat = "fremmed:regjeringen_i_stat";
    public const string FremmedNasjonalitet = "fremmed:nasjonalitet";
    public const string FremmedGenitiv = "fremmed:genitiv";
    public const string FremmedFor = "fremmed:regjeringen_for";
    public const string GeneriskStat = "generisk:konvensjonsstat";
    public const string GeneriskMal = "generisk:skjemamal";
    public const string IkkeAktorNettsted = "ikke_aktor:nettsted";
    public const string IkkeAktorAvtaletittel = "ikke_aktor:avtaletittel";
    public const string IkkeAktorNavn = "ikke_aktor:navn";
    public const string RolleMedlemAv = "rolle:medlem_av";
    public const string RolleRegjeringensMedlemmer = "rolle:regjeringens_medlemmer";
    public const string FunksjonStyret = "funksjon:styret_av_riket";
    public const string ResolusjonGrl32 = "resolusjon:grl_32";

    /// <summary>Regelkodene i den rekkefølgen de prøves.</summary>
    public static readonly string[] Regler =
    [
        FremmedRegjeringenIStat, FremmedNasjonalitet, FremmedGenitiv, FremmedFor, FremmedSanksjonstittel, GeneriskStat, GeneriskMal,
        IkkeAktorNettsted, IkkeAktorAvtaletittel, IkkeAktorNavn, RolleMedlemAv, RolleRegjeringensMedlemmer, FunksjonStyret, ResolusjonGrl32,
    ];

    /// <summary>Navnene der ordet er en navnedel (regel 3). Lukket liste, målt i korpuset 2026-11-20; ikke et prefiks.</summary>
    public static readonly string[] Navnedeler =
    [
        "sikkerhetsutvalg", "informasjonsutvalg", "kriseinformasjonsenhet", "biltjeneste", "presse- og informasjonstjeneste", "trykningskontor",
        "kriseutvalg",
    ];

    /// <summary>Forekomstene av ordet: «regjeringen» og «regjeringens» som hele ord, uten skille på store og små bokstaver. Bindestrek rett
    /// foran er ikke ordgrense (samme regel som <see cref="AktoridentitetTjeneste"/> etter #463); punktum etter er det («regjeringen.no»).</summary>
    public static readonly Regex Ordet = new(@"(?<![\p{L}\p{N}-])regjeringens?(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private const RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly Regex Sanksjonstittel = new(@"^(sanksjonsforskrift|forskrift om (sanksjoner|restriktive tiltak))(?![\p{L}])", I);
    // «regjeringen i X»: X begynner med stor bokstav (et navn), og er ikke Norge. Sjekkes uten IgnoreCase for den store bokstaven.
    private static readonly Regex IStatNavn = new(@"^\s+i\s+(?!Kongeriket\s+Norge(?![\p{L}])|Norge(?![\p{L}]))\p{Lu}", RegexOptions.CultureInvariant);
    private static readonly Regex ITredjestat = new(@"^\s+i\s+(den\s+)?tredjestat(en)?(?![\p{L}])", I);
    private static readonly Regex NasjonalitetForan = new(@"(?:^|[^\p{L}])[Dd]en\s+(\p{Ll}+ske)\s+$", RegexOptions.CultureInvariant);
    private static readonly Regex GenitivForan = new(@"(?:^|[^\p{L}])(\p{Lu}[\p{L}-]*(?:'|’))\s+$", RegexOptions.CultureInvariant);
    private static readonly Regex ForNavn = new(@"^\s+for\s+(Det\s+)?\p{Lu}", RegexOptions.CultureInvariant);

    private static readonly Regex GeneriskIStat = new(
        @"^\s+i\s+(?:(?:den|det|en|ei|et|enhver|hver(?:\s+av)?|annen|andre|førstnevnte|sistnevnte|berørte|vedkommende)\s+)*"
        + @"(?:stat|staten|statene|statar|land|landet|landene|medlemsstat|medlemsstaten|medlemsstatene|kontraherende\s+stat(?:en)?|avtalestat(?:en)?|parten)(?![\p{L}])", I);
    private static readonly Regex DenForan = new(@"(?:^|[^\p{L}])den\s+$", I);
    private static readonly Regex SomEtter = new(@"^\s+som(?![\p{L}])", I);
    private static readonly Regex MalEtter = new(@"^\s+i\s*(?:\.{2,}|…|\(statens)", I);
    private static readonly Regex MalForan = new(@"(?:\.{3}|…)\s*$", I);

    private static readonly Regex Nettsted = new(@"^\.no(?![\p{L}])", I);
    private static readonly Regex Avtaletittel = new(
        @"(?:^|[^\p{L}])(?:administrasjonsavtale|avtale|overenskomst|konvensjon|protokoll|traktat)[\p{L}]*(?![\p{L}])[^.;:]{0,120}(?<![\p{L}])mellom\s+(?:[^.;:]{0,160}\s+og\s+)?$", I);
    private static readonly Regex NavnEtter = new(
        @"^\s+(" + string.Join("|", Navnedeler.Select(n => Regex.Escape(n).Replace(@"\ ", @"\s+"))) + @")(?![\p{L}])", I);

    // «medlem(mer) av [X, Y og] regjeringen»: leddene foran er navn med stor forbokstav (Stortinget, Kongehuset), skilt med komma, «og» eller «eller».
    private static readonly Regex MedlemAvForan = new(
        @"(?:^|[^\p{L}])[Mm]edlem(?:met|mer|mene)?\s+av\s+(?:\p{Lu}[\p{L}-]*\s*(?:,\s*|\s+og\s+|\s+eller\s+))*$", RegexOptions.CultureInvariant);
    private static readonly Regex MedlemmerEtter = new(@"^\s+(?:\p{Ll}+\s+)?medlem(?:mer|mene)(?![\p{L}])", RegexOptions.CultureInvariant);

    private static readonly Regex FunksjonForan = new(
        @"(?:^|[^\p{L}])(føre|fører|forestå|forestår|tiltre|tiltrer|tiltrådte|ivareta|ivaretar|bestyre|bestyrer|anordne|anordner|anordnet)\s+$", I);

    /// <summary>
    /// Leser forekomsten [<paramref name="start"/>, <paramref name="slutt"/>) i <paramref name="tekst"/>. <paramref name="titler"/> er
    /// rettskildens tittel og kortnavn (regel 1 avgrenser sanksjonsforskriftene ved tittel). Null = ingen regel slo til (slutning).
    /// </summary>
    public static RegjeringenTreff? Vurder(string tekst, int start, int slutt, IEnumerable<string?> titler)
    {
        var ord = tekst[start..slutt];
        var foran = tekst[..start];
        var etter = tekst[slutt..];
        var genitiv = ord.EndsWith('s') || ord.EndsWith('S');

        // 1. Fremmed stat. [ENDRET, juristmålingen m1 2026-11-20] Ordlyden prøves før tittelen: «den syriske regjeringen» i en sanksjonsforskrift
        // er et tekstfunn, ikke bare en slutning fra tittelen. Tittelregelen tar resten av sanksjonsforskriften, også den russiske «regjeringens
        // resolusjon», før regel 6.
        if (!genitiv && IStatNavn.Match(etter) is { Success: true } m1) return new(Forekomstlesninger.FremmedStat, FremmedRegjeringenIStat, Utdrag(etter, 40));
        if (!genitiv && ITredjestat.IsMatch(etter)) return new(Forekomstlesninger.FremmedStat, FremmedRegjeringenIStat, Utdrag(etter, 30));
        if (NasjonalitetForan.Match(foran) is { Success: true } m2 && !string.Equals(m2.Groups[1].Value, "norske", StringComparison.Ordinal))
        {
            return new(Forekomstlesninger.FremmedStat, FremmedNasjonalitet, $"den {m2.Groups[1].Value} {ord}");
        }
        if (GenitivForan.Match(foran) is { Success: true } m3) return new(Forekomstlesninger.FremmedStat, FremmedGenitiv, $"{m3.Groups[1].Value} {ord}");
        if (!genitiv && ForNavn.IsMatch(etter)) return new(Forekomstlesninger.FremmedStat, FremmedFor, Utdrag(etter, 40));
        if (titler.FirstOrDefault(t => t is not null && Sanksjonstittel.IsMatch(t.Trim())) is { } tittel)
        {
            return new(Forekomstlesninger.FremmedStat, FremmedSanksjonstittel, $"tittelen «{Avkort(tittel)}»");
        }

        // 2. Generisk konvensjonsstat.
        if (!genitiv && GeneriskIStat.Match(etter) is { Success: true } g1) return new(Forekomstlesninger.GeneriskKonvensjonsstat, GeneriskStat, $"{ord}{g1.Value}");
        if (!genitiv && DenForan.IsMatch(foran) && SomEtter.IsMatch(etter)) return new(Forekomstlesninger.GeneriskKonvensjonsstat, GeneriskStat, $"den {ord} som");
        if (MalEtter.IsMatch(etter) || MalForan.IsMatch(foran)) return new(Forekomstlesninger.GeneriskKonvensjonsstat, GeneriskMal, "skjemamal med tomrom");

        // 3. Ikke aktør.
        if (!genitiv && Nettsted.IsMatch(etter)) return new(Forekomstlesninger.IkkeAktor, IkkeAktorNettsted, $"{ord}.no");
        if (!genitiv && Avtaletittel.Match(foran) is { Success: true } a1)
        {
            return new(Forekomstlesninger.IkkeAktor, IkkeAktorAvtaletittel, Avkort(a1.Value.Trim()) + " " + ord);
        }
        if (genitiv && NavnEtter.Match(etter) is { Success: true } n1) return new(Forekomstlesninger.IkkeAktor, IkkeAktorNavn, $"{ord}{n1.Value}");

        // 4. Rollen statsråd.
        if (!genitiv && MedlemAvForan.Match(foran) is { Success: true } r1) return new(Forekomstlesninger.RolleStatsrad, RolleMedlemAv, $"{r1.Value.Trim()} {ord}");
        if (genitiv && MedlemmerEtter.Match(etter) is { Success: true } r2) return new(Forekomstlesninger.RolleStatsrad, RolleRegjeringensMedlemmer, $"{ord}{r2.Value}");

        // 5. Funksjonen.
        if (!genitiv && FunksjonForan.Match(foran) is { Success: true } f1) return new(Forekomstlesninger.Funksjon, FunksjonStyret, $"{f1.Groups[1].Value} {ord}");

        // 6. «Regjeringens res.» = Kongen i statsråd.
        if (genitiv && FastsattAvTolker.ErRegjeringensResolusjon(tekst[start..])) return new(Forekomstlesninger.RegjeringensResolusjon, ResolusjonGrl32, Utdrag(tekst[start..], 30));

        return null;
    }

    /// <summary>Signalord i setningen rundt en slutning (bare opplysning; ingen referent velges av dem, Johann 2026-11-20).</summary>
    public static IReadOnlyList<string> Signal(string tekst, int start, int slutt)
    {
        var fra = Math.Max(0, start - 120);
        var til = Math.Min(tekst.Length, slutt + 120);
        var omkring = tekst[fra..til];
        var signal = new List<string>();
        foreach (var (navn, mønster) in Signaler) if (mønster.IsMatch(omkring)) signal.Add(navn);
        return signal;
    }

    private static readonly (string, Regex)[] Signaler =
    [
        ("«legge fram for» / «foreslå for» Stortinget", new Regex(@"(legge[rs]?|lagt)\s+(fram|frem)|foreslå|fremlegg|proposisjon|melding\s+til\s+stortinget", I)),
        ("«fremme sak for», «forelegge», «bringe opp til»", new Regex(@"fremme[rs]?\s+(sak|forslag)|foreleg|bringe[rs]?\s+(saken\s+)?(opp|inn|fram|frem)", I)),
        ("«rapportere til», «orientere», «rådgi»", new Regex(@"rapporter|orienter|rådgi|rådgiver", I)),
        ("«anmodning fra/til»", new Regex(@"anmod", I)),
        ("«regjeringens politikk/strategi»", new Regex(@"regjeringens\s+[\p{L}-]*(politikk|strategi|handlingsplan|mål)", I)),
        ("«avskåret fra forbindelse med» (utøvende makt)", new Regex(@"avskåret\s+fra", I)),
    ];

    private static string Utdrag(string s, int n) => s.Length <= n ? s.Trim() : s[..n].Trim() + " …";
    private static string Avkort(string s) => s.Length <= 80 ? s : s[..80] + " …";
}
