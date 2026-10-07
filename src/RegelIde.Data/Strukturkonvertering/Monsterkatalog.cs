using System.Text.RegularExpressions;

namespace RegelIde.Data.Strukturkonvertering;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Mønstrene i <see cref="MonsterStrukturkonverterer"/>.
/// <para>
/// Korpustallene i hvert mønsters <c>Korpusgrunnlag</c> er fra docs/33 §1 (regex-treff i 757 lover +
/// 5110 forskrifter × presisjon ved manuell lesing av 15 tilfeldige treff, 2026-10-07). De er
/// størrelsesordener, og de gjelder FORMULERINGEN, ikke nøyaktig dette uttrykket. Presisjonen DETTE
/// uttrykket faktisk har mot fasiten står i <c>data/fasit/strukturmodell/maling-monster.md</c>.
/// </para>
/// <para>
/// Mønstrene er formulert generelt (ordstilling, verb, ledd), aldri som en bestemt setning fra
/// fasiten — et mønster som bare treffer én kjent setning måler ingenting og er forkastet (se
/// «Forkastede mønstre» i målerapporten).
/// </para>
/// </summary>
internal static class Monsterkatalog
{
    private const RegexOptions Valg = RegexOptions.CultureInvariant;

    // --- Byggeklosser ------------------------------------------------------------------------------
    // Setningsstart: Lovdata markerer tekst som ikke er i kraft med «[».
    private const string Start = @"^\[?";

    // Subjekt først i setningen: stort forbokstav + inntil fem ord, så kort som mulig (lat) — verbet
    // som følger avgrenser det. Aktorfrase.Tolk avviser det som ikke kan være en aktør.
    private const string Subjekt = @"(?<fra>\p{Lu}[\p{L}\-]*(?:\s+\p{L}[\p{L}\-]*){0,5}?)";

    // Samme, men som mål for relasjonen (instruksjon: «X kan ikke instrueres»).
    private const string SubjektTil = @"(?<til>\p{Lu}[\p{L}\-]*(?:\s+\p{L}[\p{L}\-]*){0,5}?)";

    // Modalverb med valgfritt «selv» før («Kommunestyret selv kan») og et valgfritt adverb etter.
    private const string Modal = @"\s+(?:selv\s+)?(?:kan|skal)\s+(?:også\s+|likevel\s+|dessuten\s+)?";

    // Sideordnet verbfrase før kompetanseverbet: «kan fatte enkeltvedtak eller gi forskrift om».
    private const string SideordnetVerb = @"(?:[^.;:]{0,60}?\s+eller\s+)?";

    // Subjekt ETTER modalverbet (omvendt ordstilling: «For konsesjoner … kan departementet gi …»):
    // ett ord, evt. med genitivledd foran og «i statsråd» etter, evt. to sideordnede.
    private const string SubjektOmvendt =
        @"(?<fra>(?:\p{L}+s\s+)?\p{L}[\p{L}\-]*(?:\s+i\s+statsråd)?(?:\s+(?:eller|og)\s+\p{L}[\p{L}\-]*)?)";

    // Aktør etter preposisjon («påklages til klagenemnda», «fastsatt av Kongen i statsråd»,
    // «delegeres til Kommunal- og regionaldepartementet», «oppnevnt av Statens helsetilsyn»).
    private static string Etter(string gruppe) =>
        $@"(?<{gruppe}>(?:\p{{L}}+s\s+)?(?:\p{{L}}+-\s+og\s+)?\p{{L}}[\p{{L}}\-]*(?:\s+i\s+statsråd)?)";

    private const string Tall =
        @"(?:\d+|to|tre|fire|fem|seks|sju|syv|åtte|ni|ti|elleve|tolv|tretten|fjorten|femten|seksten|sytten|atten|nitten|tjue)";

    public static IReadOnlyList<Strukturmonster> Bygg() =>
    [
        // ---- K forskriftskompetanse ---------------------------------------------------------------
        Regex("forskrift-gi", "kompetanse", "forskriftskompetanse",
            "«X kan gi (nærmere) forskrift(er) om …», «X gir forskrift om …», «kan X gi forskrift …», «X kan fatte enkeltvedtak eller gi forskrift om …».",
            "docs/33 §1: «kan gi forskrift om» 5651 treff, 100 % presisjon.",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + "(?:" + Modal + SideordnetVerb + @"(?:gi|fastsette)|\s+gir)\s+(?<obj>(?:de\s+)?(?:nærmere\s+|utfyllende\s+)?forskrift(?:er)?\b)",
            @"\b(?:kan|skal)\s+" + SubjektOmvendt + @"\s+(?:også\s+)?(?:gi|fastsette)\s+(?<obj>(?:de\s+)?(?:nærmere\s+|utfyllende\s+)?forskrift(?:er)?\b)"),

        Regex("forskrift-i-ved", "kompetanse", "forskriftskompetanse",
            "«X kan i/ved forskrift (gi/fastsette/bestemme/stille …)», «X fastsetter ved forskrift …».",
            "docs/33 §1: variant av «kan gi forskrift om» (5651 treff, 100 %); formen er ikke målt separat.",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + "(?:" + Modal + @"|\s+fastsetter\s+)(?<obj>(?:i|ved)\s+forskrift\b)",
            @"\b(?:kan|skal)\s+" + SubjektOmvendt + @"\s+(?<obj>(?:i|ved)\s+forskrift\b)"),

        Regex("forskrift-naermere-regler", "kompetanse", "forskriftskompetanse",
            "«X kan gi nærmere/utfyllende regler/bestemmelser om …», «Nærmere regler om … kan gis av X», «Nærmere regler … kan X gi».",
            "Ikke målt i docs/33 §1. Med fordi lovteksten bruker «gi nærmere regler» om forskrift uten å si ordet; presisjonen måles mot fasiten.",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + "(?:" + Modal + @"(?:gi|fastsette)|\s+(?:gir|fastsetter))\s+(?<obj>(?:de\s+)?(?:nærmere|utfyllende)\s+(?:regler|bestemmelser|bestemmelse)\b)",
            @"\b(?:kan|skal)\s+" + SubjektOmvendt + @"\s+gi\s+(?<obj>(?:nærmere|utfyllende)\s+(?:regler|bestemmelser)\b)",
            @"(?<obj>(?:Nærmere|Utfyllende)\s+(?:regler|bestemmelser|forskrifter)\b)[^.;]{0,150}?\bgis\s+av\s+" + Etter("fra"),
            @"(?<obj>(?:Nærmere|Utfyllende)\s+(?:regler|bestemmelser)\b)[^.;]{0,150}?\bkan\s+" + SubjektOmvendt + @"\s+gi\b"),

        Regex("forskrift-gitt-av", "kompetanse", "forskriftskompetanse",
            "«forskrift fastsatt av X», «forskrifter gitt av X», «regler gitt av X».",
            "Ikke målt i docs/33 §1 (passiv form av forskriftskompetanse). Krever at aktøren står i setningen.",
            new RegexMonsteroppsett(KreverFra: true),
            @"\b(?<obj>forskrift|forskrifter|regler)\s+(?:gitt|fastsatt)\s+av\s+" + Etter("fra")),

        // ---- K vedtakskompetanse ------------------------------------------------------------------
        Regex("vedtak-treffe", "kompetanse", "vedtakskompetanse",
            "«X kan/skal treffe/fatte (enkelt)vedtak …», «X treffer vedtak …», «X kan ved/i enkeltvedtak …», og omvendt ordstilling.",
            "docs/33 §1: «treffer vedtak / avgjøres av» 1465 treff, 73 % presisjon.",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + "(?:" + Modal + SideordnetVerb + @"(?:treffe|fatte)|\s+(?:treffer|fatter))\s+(?<obj>(?:enkelt)?vedtak\b|avgjørelse\b)",
            Start + Subjekt + Modal + @"(?<obj>(?:ved|i)\s+enkeltvedtak\b)",
            @"\b(?:kan|skal)\s+" + SubjektOmvendt + @"\s+(?:også\s+)?(?:treffe|fatte)\s+(?<obj>(?:enkelt)?vedtak\b)",
            @"\b(?:kan|skal)\s+" + SubjektOmvendt + @"\s+(?<obj>(?:ved|i)\s+enkeltvedtak\b)"),

        Regex("vedtak-avgjores-av", "kompetanse", "vedtakskompetanse",
            "«(Vedtak/avgjørelse om …) treffes/fattes/avgjøres/besluttes/vedtas av X». Objektet er det som står foran verbet.",
            "docs/33 §1: «treffer vedtak / avgjøres av» 1465 treff, 73 %. Krever at aktøren står i setningen — «av» uten aktør er ikke et kompetanseutsagn.",
            new RegexMonsteroppsett(KreverFra: true, ObjektForan: true),
            @"\b(?:avgjøres|treffes|fattes|besluttes|vedtas)\s+(?:[^\s.;,]+\s+){0,4}?av\s+" + Etter("fra")),

        Regex("vedtak-avgjor", "kompetanse", "vedtakskompetanse",
            "«X avgjør (i tvilstilfelle) …».",
            "Ikke målt separat i docs/33 §1 (del av «avgjøres av»-familien).",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + @"\s+avgjør\s+(?<obj>\S.*)"),

        // ---- R klageinstans_for -------------------------------------------------------------------
        new Strukturmonster("klage-paklages-til", "relasjon", "klageinstans_for",
            "«(Enkeltvedtak fattet av Y) … kan påklages (…) til X» → X klageinstans for Y. Y er null når setningen ikke sier hvem førsteinstansen er.",
            "docs/33 §1: «klageinstans for / påklages til» 1718 treff, 67 % presisjon. Førsteinstansen er ofte implisitt.",
            KlagePaklagesTil),

        Regex("klage-er-klageinstans", "relasjon", "klageinstans_for",
            "«X er klageinstans (for enkeltvedtak truffet av Y)», «Klageinstans for Ys vedtak er X».",
            "docs/33 §1: «klageinstans for / påklages til» 1718 treff, 67 %.",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + @"\s+er\s+(?:også\s+)?klageinstans\b(?:\s+for\s+(?:enkelt)?vedtak(?:\s+(?:truffet|fattet))?\s+av\s+" + Etter("til") + ")?",
            @"\bKlageinstans\s+for\s+(?<tilgen>\p{L}+)\s+vedtak\s+er\s+" + Etter("fra")),

        Regex("klage-ikke-paklages", "relasjon", "klageinstans_for",
            "«vedtak av Y … kan ikke påklages», «Ys vedtak kan ikke påklages» → negativ, uten klageinstans. Krever Y — «Vedtak om registrering kan ikke påklages» sier ingenting om en aktør.",
            "docs/33 §1: del av «klageinstans for / påklages til» (1718 treff, 67 %); negativ form ikke målt separat.",
            new RegexMonsteroppsett(KreverTil: true, Polaritet: "negativ"),
            @"\bvedtak\s+(?:fattet\s+av|truffet\s+av|av)\s+" + Etter("til") + @"[^.;]{0,80}?\b(?:ikke\s+kan|kan\s+ikke)\s+påklages",
            Start + @"(?<tilgen>\p{Lu}\p{L}+)\s+vedtak\s+kan\s+ikke\s+påklages"),

        // ---- R administrativt_underordnet ---------------------------------------------------------
        Regex("administrativt-underordnet", "relasjon", "administrativt_underordnet",
            "«X er administrativt underordnet/underlagt Y».",
            "docs/33 §1: 775 treff på «underordnet», 13 % — signalet er KUN i frasen «administrativt underordnet», som er det eneste mønsteret tar.",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + @"\s+er\s+administrativt\s+(?:underordnet|underlagt)\s+" + Etter("til")),

        // ---- G har_medlemmer ----------------------------------------------------------------------
        Regex("har-medlemmer", "organsammensetning", "har_medlemmer",
            "«X skal ha (minst) N medlemmer/dommere/representanter», «X består av N medlemmer». Til = substantivet etter tallet, slik det står.",
            "docs/33 §1: «består av N medlemmer» 147 treff, 100 % presisjon.",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + @"(?:\s+skal)?\s+(?:ha|har|bestå\s+av|består\s+av)\s+(?:minst\s+|inntil\s+|høyst\s+|i\s+alt\s+)?" + Tall
                + @"\s+(?:\p{L}+\s+)?(?<til>\p{L}*(?:medlemmer|dommere|representanter|personer))\b"),

        // ---- R oppnevner --------------------------------------------------------------------------
        new Strukturmonster("oppnevnt-av", "relasjon", "oppnevner",
            "«Y oppnevnt/utnevnt av X», «Y oppnevnes/utnevnes (som …) av X» → X oppnevner Y. Y = de ett–to ordene rett foran verbet, ellers null.",
            "docs/33 §1: «oppnevner / utnevner» 1422 treff, 33 % presisjon.",
            OppnevntAv),

        Regex("oppnevner-aktivt", "relasjon", "oppnevner",
            "«X oppnevner/utnevner … til/av Y» («Kongen oppnevner medlemmene av Innstillingsrådet»). Y null hvis ingen «til/av» følger.",
            "docs/33 §1: «oppnevner / utnevner» 1422 treff, 33 %.",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + @"\s+(?:oppnevner|utnevner)\b(?:[^.;]{0,160}?\b(?:til|av)\s+(?!å\b)" + Etter("til") + ")?"),

        // ---- R instruksjon (negativ) --------------------------------------------------------------
        Regex("instruksjon-kan-ikke-instrueres", "relasjon", "instruksjon",
            "«X (er uavhengig og) kan ikke instrueres (av Y)» → negativ instruksjon, Y = null når agenten ikke står i setningen.",
            "docs/33 §1: «instruere» 908 treff, 27 % — og nesten bare NEGATIV («kan ikke instruere») = uavhengighet. Mønsteret tar bare den negative formen.",
            new RegexMonsteroppsett(KreverTil: true, Polaritet: "negativ"),
            Start + SubjektTil + @"(?:\s+er\s+[^.;]{0,80}?\s+og)?\s+(?:kan|skal)\s+ikke\s+instrueres\b(?:\s+av\s+" + Etter("fra") + ")?"),

        Regex("instruksjon-kan-ikke-instruere", "relasjon", "instruksjon",
            "«X kan/skal ikke instruere Y», «X skal ikke gi instruks(er) til Y» → negativ instruksjon.",
            "docs/33 §1: «instruere» 908 treff, 27 %, nesten bare negativ.",
            new RegexMonsteroppsett(KreverFra: true, Polaritet: "negativ"),
            Start + Subjekt + @"\s+(?:kan|skal)\s+ikke\s+(?:instruere\s+" + Etter("til") + @"|gi\s+instrukse?r?\s+til\s+" + Etter("til") + ")"),

        // ---- R delegerer_til ----------------------------------------------------------------------
        Regex("delegerer-kan-delegere", "relasjon", "delegerer_til",
            "«X kan delegere (myndighet …) til Y», «X delegerer … til Y», «X kan bemyndige Y». Y null når den ikke kan bestemmes («til disse»).",
            "docs/33 §1: «delegerer til» 2875 treff, 47 % presisjon — nesten alltid avgrenset til paragraf (avgrensningen tolkes ikke her).",
            new RegexMonsteroppsett(KreverFra: true),
            Start + Subjekt + "(?:" + Modal + @"delegere|\s+delegerer)\b[^.;]{0,200}?\btil\s+(?!å\b)" + Etter("til"),
            Start + Subjekt + Modal + @"bemyndige\s+" + Etter("til"),
            Start + Subjekt + "(?:" + Modal + @"delegere|\s+delegerer)\b"),

        Regex("delegerer-delegeres-til", "relasjon", "delegerer_til",
            "«Xs myndighet (etter …) delegeres til Y» → X delegerer til Y (genitiv-s fjernet: «Kongens» → «Kongen»); «… delegeres til Y» uten X → fra null.",
            "docs/33 §1: «delegerer til» 2875 treff, 47 %.",
            new RegexMonsteroppsett(KreverTil: true),
            @"(?<fragen>\p{Lu}\p{L}+)\s+myndighet\b[^.;]{0,200}?\bdelegeres\s+(?:delvis\s+|også\s+)?til\s+" + Etter("til"),
            @"\bdelegeres\s+(?:delvis\s+|også\s+)?til\s+" + Etter("til")),

        // ---- O bestar_av / A har_sete_i: strukturerte lister i inndelingsforskrifter ---------------
        new Strukturmonster("inndeling-rettskrets", "sammensetning_omrade", "bestar_av",
            "«(… har rettskretsen) N tingrett, med rettssted(er) i …, som dekker kommunene A, B og C» → N består av hver kommune; «som dekker A kommune» → én.",
            "docs/33 §1 nevner strukturerte kommunelister i inndelingsforskrifter som høypresisjonskilde (ikke tallfestet).",
            s => Inndeling.Rettskrets(s, kommuner: true)),

        new Strukturmonster("inndeling-rettssted", "ansvarsomrade", "har_sete_i",
            "Samme struktur som inndeling-rettskrets: «N tingrett, med rettssteder i A og B, …» → N har sete i A og i B.",
            "Samme kilde som inndeling-rettskrets.",
            s => Inndeling.Rettskrets(s, kommuner: false)),

        new Strukturmonster("inndeling-kommuneliste", "sammensetning_omrade", "bestar_av",
            "«Navn (/samisk navn): kommunene A, B og C i X fylke (og kommunene …)» og «… samt fylkene A, B og C» → Navn består av hvert listeelement. Lister som er definert ved retning («fra og med … og nordover») tas IKKE.",
            "Strukturerte kommunelister (docs/33 §1, ikke tallfestet). Retnings- og komplementdefinisjoner kan ikke avgjøres uten et områderegister (#312).",
            Inndeling.Kommuneliste),

        new Strukturmonster("inndeling-utgjor", "sammensetning_omrade", "bestar_av",
            "«Lagsognene/Kommunene/Fylkene A, B og C utgjør N» → N består av hvert element.",
            "Strukturerte lister i inndelingsforskrifter (docs/33 §1, ikke tallfestet).",
            Inndeling.Utgjor),

        new Strukturmonster("inndeling-sogner", "sammensetning_omrade", "bestar_av",
            "«Til lagsognet/rettskretsen N sogner A tingrett og B tingrett» → N består av hver.",
            "Strukturerte lister i inndelingsforskrifter (docs/33 §1, ikke tallfestet).",
            Inndeling.Sogner),

        new Strukturmonster("inndeling-bestar-av-liste", "sammensetning_omrade", "bestar_av",
            "«N består (i tillegg) av A, B og C», der ALLE elementene er egennavn (stor forbokstav). «Styret består av ni medlemmer» tas ikke (det er har-medlemmer).",
            "Ikke målt i docs/33 §1. Generell listeform; kravet om egennavn holder organsammensetning ute.",
            Inndeling.BestarAvListe),
    ];

    private static Strukturmonster Regex(string id, string kategori, string type, string beskrivelse, string korpus,
        RegexMonsteroppsett oppsett, params string[] uttrykk)
    {
        var kompilert = uttrykk.Select(u => new Regex(u, Valg)).ToList();
        return new Strukturmonster(id, kategori, type, beskrivelse, korpus,
            setning => RegexMonster.Finn(setning, oppsett, kompilert));
    }

    // ---- Spesialmønstre som trenger mer enn ett uttrykk per setning ---------------------------------

    private static readonly Regex PaklagesTil = new(
        @"\bpåklages\b[^.;]{0,100}?\btil\s+(?!å\b)" + Etter("fra"), Valg);

    private static readonly Regex IkkePaklages = new(@"\b(?:ikke\s+kan|kan\s+ikke|ikke)\s+påklages\b", Valg);

    private static readonly Regex Forsteinstans = new(
        @"\b(?:fattet|truffet)\s+av\s+" + Etter("til") + @"|^\[?(?<tilgen>\p{Lu}\p{L}+)\s+vedtak\b", Valg);

    private static IReadOnlyList<Monsterfunn> KlagePaklagesTil(string setning)
    {
        var m = PaklagesTil.Match(setning);
        if (!m.Success) return [];
        // «… ikke kan påklages til …» er ikke en klageinstans — den negative formen har eget mønster.
        if (IkkePaklages.IsMatch(setning[..(m.Index + "påklages".Length)])) return [];

        var fra = Aktorfrase.Tolk(m.Groups["fra"].Value);
        if (fra.Count == 0) return [];

        IReadOnlyList<string> til = [];
        var forste = Forsteinstans.Match(setning[..m.Index]);
        if (forste.Success)
        {
            til = forste.Groups["til"].Success
                ? Aktorfrase.Tolk(forste.Groups["til"].Value)
                : Aktorfrase.UtenGenitiv(forste.Groups["tilgen"].Value) is { } u ? Aktorfrase.Tolk(u) : [];
        }
        return [new Monsterfunn(RegexMonster.Sitat(setning), fra, til, null, "positiv")];
    }

    private static readonly Regex OppnevntAvUttrykk = new(
        @"\b(?:oppnevnt|utnevnt|oppnevnes|utnevnes|oppnevnte|utnevnte)\s+(?:som\s+\p{L}+\s+)?av\s+" + Etter("fra"), Valg);

    private static readonly Regex HjelpeverbBakerst = new(@"(?:\s+(?:som|er|blir|ble|skal|kan|bli|være|eller|og))+$", Valg);

    private static IReadOnlyList<Monsterfunn> OppnevntAv(string setning)
    {
        var funn = new List<Monsterfunn>();
        foreach (Match m in OppnevntAvUttrykk.Matches(setning))
        {
            var fra = Aktorfrase.Tolk(m.Groups["fra"].Value);
            if (fra.Count == 0) continue;

            // Den som oppnevnes står rett foran verbet («særskilt klagenemnd oppnevnt av Sametinget»).
            // Prøv de to siste ordene, så det siste; hjelpeverb og «som» skrelles av først. Ellers null.
            var foran = HjelpeverbBakerst.Replace(setning[..m.Index].TrimEnd(' ', ','), "");
            var ord = foran.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            IReadOnlyList<string> til = [];
            if (ord.Length >= 2 && !ord[^2].EndsWith(',')) til = Aktorfrase.Tolk(ord[^2] + " " + ord[^1]);
            if (til.Count == 0 && ord.Length >= 1) til = Aktorfrase.Tolk(ord[^1]);

            funn.Add(new Monsterfunn(RegexMonster.Sitat(setning), fra, til, null, "positiv"));
        }
        return funn;
    }
}
