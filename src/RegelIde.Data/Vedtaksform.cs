using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RegelIde.Data;

/// <summary>Én lest enhet (ledd, punkt, instruks) i kildens rå Lovdata-HTML, med rollen regelen ga den.</summary>
/// <param name="HtmlId">Lovdatas id på elementet (for eksempel <c>kapittel-2-ledd-1</c>), eller null for umerkede instrukser.</param>
public sealed record VedtaksformEnhet(string? HtmlId, string Rolle, string Tekst);

/// <summary>Resultatet av <see cref="Vedtaksform.Klassifiser"/>.</summary>
/// <param name="Klasse">En av <see cref="Vedtaksform.Klasser"/>.</param>
/// <param name="Grunnlag">Regelen og signalene, lesbart for et menneske.</param>
/// <param name="BevisHtmlId">Første ledd med eget innhold (blandet, selvstendig), ellers null.</param>
/// <param name="Bevis">Sitat fra det leddet (avkortet).</param>
public sealed record VedtaksformResultat(
    string Klasse, string Grunnlag, string Kunnskapsstatus, string? BevisHtmlId, string? Bevis,
    IReadOnlyDictionary<string, int> PerRolle, IReadOnlyList<VedtaksformEnhet> Enheter);

/// <summary>
/// [Ny, issue #446 PR 1, prosjekteierens avgjørelse 2026-11-25] <b>Vedtaksformen</b>: om en lov eller forskrift bare endrer, setter i kraft eller
/// opphever andre dokumenter (et «rent» vedtak, som har uttømt virkningen når det er i kraft), eller har eget innhold. Ren funksjon uten
/// database, så samme regel brukes av <see cref="VedtaksformTjeneste"/> og testene.
/// <para>
/// <b>Kilden er Lovdatas rå HTML</b> (<c>rettskilder.innhold</c>), ikke nodene: endringsinstruksene («I lov … gjøres følgende endringer:»,
/// «§ 3 skal lyde:») er <c>defaultP</c>, og parseren gjør dem ikke til noder. Hver enhet får én rolle (<see cref="RolleEndringsinstruks"/>,
/// <see cref="RolleSitertTekst"/>, <see cref="RolleIkrafttredelse"/>, <see cref="RolleOppheving"/>, <see cref="RolleEgetInnhold"/>), setning for
/// setning. Sitert ny tekst er alt som står etter en instruks i samme romertallsdel eller paragraf, og alt i Lovdatas markering for endret eller
/// foreslått tekst (<c>futureLegalArticle</c>, <c>document-change</c>, <c>change</c>).
/// </para>
/// <para>
/// <b>Ingen gjetting (CLAUDE.md §8).</b> En ren klasse krever både at ingen enhet har eget innhold OG et signal til fra kilden: tittelform
/// (endring, oppheving eller ikrafttredelse) eller Lovdatas «Endrer» (changesToDocuments). Spriker de, blir kilden <see cref="Ukjent"/>.
/// [ENDRET etter juristrunden for #446 fase 1, D4/D5] Unntaket for eget innhold gjelder også endrings- og opphevingssetninger: «gis myndighet
/// til å oppheve …», «… oppheves og myndigheten delegeres til …» og «Endringer i lov … får anvendelse på …» er eget innhold.
/// </para>
/// </summary>
public static partial class Vedtaksform
{
    public const string Regelversjon = "446-pr1-v3-2026-11-26";

    public const string RentEndringsvedtak = "rent_endringsvedtak";
    public const string RentIkrafttredelsesvedtak = "rent_ikrafttredelsesvedtak";
    public const string RentOpphevingsvedtak = "rent_opphevingsvedtak";
    public const string Blandet = "blandet";
    public const string Selvstendig = "selvstendig";
    public const string Ukjent = "ukjent";

    /// <summary>[LÅST, prosjekteierens avgjørelse 2026-11-25] De seks klassene. Speilet i CHECK-en <c>ck_rettskilder_vedtaksform</c>.</summary>
    public static readonly IReadOnlyList<string> Klasser = [RentEndringsvedtak, RentIkrafttredelsesvedtak, RentOpphevingsvedtak, Blandet, Selvstendig, Ukjent];
    public static readonly IReadOnlySet<string> Rene = new HashSet<string> { RentEndringsvedtak, RentIkrafttredelsesvedtak, RentOpphevingsvedtak };

    public const string RolleEndringsinstruks = "endringsinstruks";
    public const string RolleSitertTekst = "sitert_tekst";
    public const string RolleIkrafttredelse = "ikrafttredelse";
    public const string RolleOppheving = "oppheving";
    public const string RolleEgetInnhold = "eget_innhold";
    private const string RolleOverskrift = "overskrift";
    private const string RolleIgnorert = "ignorert";

    /// <summary>Rollene som lagres på noden (<c>rettskilde_noder.endringsrolle</c>). Eget innhold lagres ikke: NULL på en node i en klassifisert
    /// kilde betyr eget innhold eller ikke koblet. Speilet i CHECK-en <c>ck_rettskilde_noder_endringsrolle</c>.</summary>
    public static readonly IReadOnlyList<string> LagredeRoller = [RolleEndringsinstruks, RolleSitertTekst, RolleIkrafttredelse, RolleOppheving];

    private const RegexOptions F = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly HashSet<string> Enhetsklasser = ["legalP", "numberedLegalP", "defaultP", "centeredP", "leddfortsettelse", "miscHeadline", "futuretitle"];
    private static readonly HashSet<string> Ignorerte = ["changesToParent", "footnote", "footnotes", "footnoteLegalP", "footnoteDefaultP", "footnotereference"];
    private static readonly HashSet<string> Beholdere = ["section", "legalArticle", "futureLegalArticle", "document-change", "change"];
    private static readonly HashSet<string> Endringsbeholdere = ["futureLegalArticle", "document-change", "change"];

    // ---------------------------------------------------------------- Setningsmønstre
    // Sterk instruks: en setning som bare kan være en endringsinstruks.
    [GeneratedRegex(@"gj[øo]res f[øo]lgende (endring|tilf[øo]yelse)|gjerast f[øo]lgj?ande endring|(blir|vert) (det )?gjort f[øo]lgj?ande endring|\bendres slik\b|\bendrast slik\b|"
        + @"\bskal lyde?\b|\bskal lyda\b|\berstattes (med|av)\b|\bblir erstatta\b|\bvert erstatta\b|\btilf[øo]yes\b|\bstrykes\b|"
        + @"^(Endringer|Endringar) i (andre )?(lover|forskrifter|lov|forskrift)\b[^.]{0,200}:\s*$|f[øo]lgende endringer i (andre )?(lover|forskrifter)|"
        + @"^I f[øo]lgj?ende (lover|forskrifter|vedtak|instrukser)\b|\bendres\b.{0,120}\btil\b|\bendrast\b.{0,120}\btil\b", F)]
    private static partial Regex SterkInstruks();
    // «I lov/forskrift … gjøres/endres/skal …»: instruksen med dokumentet først.
    [GeneratedRegex(@"^(I|Fra .{0,60}?,? i) (lov|lova|loven|forskrift|forskrifta|forskriften|instruks|instruksen|vedtak|vedtaket|reglement|resolusjon|kongelig resolusjon|"
        + @"kgl\. res\.|delegeringsvedtak|delegering|vedtekter|stortingsvedtak|\w*forskrift(en|a)?|\w*instruks(en)?|\w*lov(en|a)?)\b.{0,500}?"
        + @"(gj[øo]res|gjerast|blir (det )?gjort|vert gjort|endres|endrast|oppheves|opphevast|skal|tilf[øo]yes|erstattes)", F)]
    private static partial Regex InstruksMedDokument();
    // Svak instruks: bare inne i en endringsdel («Ny § 5», «blir nytt tredje ledd», «utgår»).
    [GeneratedRegex(@"\bnytt? (§|ledd|punktum|bokstav|nr\.|nummer|kapittel|vedlegg)|\bny(tt|e)? (§|ledd|punktum|bokstav|nr\.|nummer|kapittel|vedlegg)|\bblir ny(tt|e)?\b|\butg[åa]r\b", F)]
    private static partial Regex SvakInstruks();

    [GeneratedRegex(@"\b(trer|trår|tek|tar|trådte)\s+(likevel\s+)?(i\s*)?kraft|\bikraft\b|\bi kraft (straks|fr[aå]|[0-9])|\bsett(es|ast|jast|jes)\s+i\s*kraft|"
        + @"\bsettes i verk\b|\bgjeld(er|e)? (likevel )?fr[aå]\b|\bskal gjelde (likevel )?fr[aå]\b|\btek til [åa] gjelde|\btar til [åa] gjelde|"
        + @"\bgis virkning (fr[aå]|f\.o\.m|for inntekts[åa]ret|fra og med|med)|\bsett?(e|es|je)\b.{0,40}\bi\s*kraft|\bi kraft til (forskjellig|ulik) tid", F)]
    private static partial Regex Ikraft();
    [GeneratedRegex(@"\b(oppheves|opphevast|blir oppheva|blir opphevet|vert oppheva|skal oppheves|oppheves fra|oppheves med virkning)\b", F)]
    private static partial Regex Oppheves();
    [GeneratedRegex(@"\b(lov|lova|loven|forskrift\w*|\w*forskrift(en|a)?|vedtak\w*|instruks\w*|resolusjon\w*|reglement\w*|delegering\w*|vedtekter|nr\.)", F)]
    private static partial Regex Dokumentreferanse();
    // Eget innhold i en setning som ellers ser ut som ikrafttredelse, endring eller oppheving.
    // [ENDRET etter juristrunden for #446 fase 1, D4/D5] + delegering og kompetanse («gis myndighet», «delegeres», «legges til»), og
    // «får (ikke) anvendelse». Substantivet «delegering» er ikke med: «Vedtak … om delegering av myndighet … oppheves» er en ren oppheving.
    [GeneratedRegex(@"\bgjelder ikke\b|\bgjeld ikkje\b|\bfortsatt\b|\bframleis\b|\bfremdeles\b|\bfor (personer|søknader|saker|krav|tilfeller|avtaler|kontrakter|"
        + @"skatteytere|inntekt|foretak|virksomheter|pasienter|elever|studenter|arbeidstakere|søkere|stønadsmottakere|anskaffelser|hendelser|skader)\b|\bovergangs\w*|"
        + @"\bdersom\b|\bmed mindre\b|\bkan gi\b|\bgir (nærmere )?(forskrift|regler)|\bfastsetter (nærmere )?(regler|overgangs|forskrift)|"
        + @"\bsom er (inngått|fremmet|startet|innvilget|reist)|\bgis myndighet\b|\bf[åa]r myndighet\b|\bdelegeres\b|\bdelegerer\b|\btillegges\b|\blegges til\b|"
        + @"\bbemyndig\w*|\bf[åa]r (ikke )?anvendelse\b|\bkan (fastsette|endre|oppheve|gjøre|treffe)\b|\bå oppheve\b", F)]
    private static partial Regex EgetInnholdISetning();
    // [ENDRET etter måling 2 i PR 1, N40] Ikrafttredelsen i en leddsetning er ikke hovedutsagnet: «Meldinger som er under behandling når loven
    // trer i kraft, avsluttes» er en overgangsregel. Men «Forskriften trer i kraft når loven trer i kraft» er ikrafttredelse (hovedsetningen først).
    [GeneratedRegex(@"\b(når|før|etter at|da|inntil|innen)\s+(\S+\s+){1,3}?(trer|trådte|tek|tok)\s+(likevel\s+)?i\s*kraft\b", F)]
    private static partial Regex IkraftILeddsetning();
    [GeneratedRegex(@"^(Loven|Lova|Forskriften|Forskrifta|Endringen(e)?|Endringa|Endringane|Vedtaket|Instruksen|Delegeringen|Denne \w+|Lov\b.{0,300}?|Forskrift\b.{0,300}?|"
        + @"§[^.]{0,80}?|Kapittel[^.]{0,80}?|Del [IVX]+[^.]{0,80}?)\s+(trer|tek|gjelder|gjeld|skal gjelde|settes|setjast|tar til)\b", F)]
    private static partial Regex IkraftIHovedsetning();
    [GeneratedRegex(@"\bkan\b.{0,25}\b(sette|setje)\b.{0,80}\bi\s*kraft|\bkan bestemme at\b.{0,120}\b(trer|tre) i kraft", F)]
    private static partial Regex KanSetteIKraft();
    [GeneratedRegex(@"^(Fastsatt|Fastsett|Fastsette|Hjemmel:|Kongelig resolusjon|Kgl\.? ?res|Statsråd|Foredratt|Vedtatt|Vedteke|Gitt ved|Gjeve ved|"
        + @"Jf\.|Se |Endret ved|Endra ved|Tilføyd|Opphevet ved|Rettet|Rettelse|Lovdatas|Merknad|Henvisning|Innhold|Oversikt|Kunngjort|"
        + @"Senere endringer|Endringer innarbeidet|Endringene er innarbeidet)", F)]
    private static partial Regex Ingress();
    [GeneratedRegex(@"^[\s·.…\-–]*$")] private static partial Regex Ellipse();
    [GeneratedRegex(@"^(Endringer|Endringar) i f[øo]lgj?ende", F)] private static partial Regex Listehode();
    [GeneratedRegex(@"^(Ikrafttredelse|Ikraftsetting|Ikrafttreden|Overgangs\w*|Endringer i andre \w+|Endringar i andre \w+|Iverksetting)[^.]{0,80}\.?$", F)]
    private static partial Regex OverskriftIDefaultP();
    [GeneratedRegex(@"^(Midlertidig |Mellombels )?(Lov|Lova|Forskrift|Forskrifta|Instruks|Vedtak|Kongelig resolusjon)\b.{0,60}\b\d{4}\b", F)]
    private static partial Regex Dokumentliste();
    [GeneratedRegex(@"^(Endringen(e)?|Forskriften|Endringsforskriften|Loven|Lova|Vedtaket|Endringa|Endringane|Instruksen|Denne forskrift)", F)]
    private static partial Regex IkraftSubjekt();
    [GeneratedRegex(@"(?<!\bnr)(?<!\bjf)(?<!\bmv)(?<!\bpkt)(?<!\bkap)(?<!\bbl\.a)(?<!\bca)(?<!\bdvs)(?<!\bf\.eks)(?<!\bstk)(?<=[.!?])\s+(?=[A-ZÆØÅ])")]
    private static partial Regex Setningsskille();
    [GeneratedRegex(@"\s+")] private static partial Regex Mellomrom();

    // ---------------------------------------------------------------- Tittelformer
    // [ENDRET etter juristrunden for #446 fase 1, D9] «Endring av» bare foran et dokument: «endring av navn» og «endring av … struktur» er ikke
    // endringsform i lovteknisk forstand (U18, U22).
    [GeneratedRegex(@"^((forskrift|lov|vedtak|resolusjon|kongelig resolusjon)\s+(om|til)\s+)?(endring|endringer|endringar|endring og oppheving|endringer og opphevelse|"
        + @"endringar og oppheving|tilføyelse i|tillegg til)\s+(i|til)\b|^((forskrift|lov|vedtak|resolusjon)\s+(om|til)\s+)?endring(er|ar)?\s+av\s+(forskrift|lov|vedtak|"
        + @"instruks|resolusjon|kgl\.|kongelig|delegering|reglement|vedtekter|\w*forskrift|\w*instruks|\w*lov)|^midlertidig forskrift om endring(er)? i\b", F)]
    private static partial Regex TittelEndring();
    [GeneratedRegex(@"\bovergangs(regel|regler|reglar|regelen|reglene|bestemmelse\w*|føresegn\w*)\b", F)]
    private static partial Regex TittelOvergang();
    [GeneratedRegex(@"^((forskrift|lov|vedtak|resolusjon)\s+(om|til)\s+)?(oppheving|opphevelse|opphevning)\s+(av)\b", F)]
    private static partial Regex TittelOppheving();
    [GeneratedRegex(@"(^|[.;]\s+)((delvis|meddelelse om|kunngjøring om|forskrift om|vedtak om|resolusjon om)\s+)*(delvis\s+)?ikraft\w*|^(forskrift om )?(iverksetting|iverksettelse) av", F)]
    private static partial Regex TittelIkraft();

    public static bool TittelHarEndringsform(string tittel) => TittelEndring().IsMatch(Norm(tittel));
    public static bool TittelHarOpphevingsform(string tittel) => TittelOppheving().IsMatch(Norm(tittel));
    public static bool TittelHarIkraftform(string tittel) => TittelIkraft().IsMatch(Norm(tittel));

    private sealed class Enhet
    {
        public required string Klasse;
        public required bool Overskrift;
        public string? Id;
        public readonly System.Text.StringBuilder Tekst = new();
        public required int[] Beholdere;
        public required int[] DefaultPForeldre;
        public string Rolle = "";
        public string T = "";
    }

    /// <summary>Klassifiserer én kilde. <paramref name="html"/> er Lovdatas rå HTML; null eller tom gir <see cref="Ukjent"/>.</summary>
    /// <param name="endrerAntall">Antall dokumenter i Lovdatas «Endrer» (changesToDocuments).</param>
    public static VedtaksformResultat Klassifiser(string tittel, string? html, int endrerAntall)
    {
        var t = Norm(tittel);
        bool tEndr = TittelEndring().IsMatch(t), tOpph = TittelOppheving().IsMatch(t), tIkr = TittelIkraft().IsMatch(t);
        var tittelform = tEndr || tOpph || tIkr;
        var tom = new Dictionary<string, int>();
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("<main", StringComparison.Ordinal))
        {
            return new(Ukjent, "Rå Lovdata-tekst mangler, så endringsinstruksene kan ikke leses.", Strukturkanter.Uavklart, null, null, tom, []);
        }

        var enheter = LesEnheter(html, out var beholdertyper);
        SettRoller(enheter, beholdertyper);
        var per = enheter.Where(e => e.Rolle is not (RolleOverskrift or RolleIgnorert)).GroupBy(e => e.Rolle).ToDictionary(g => g.Key, g => g.Count());
        int E = per.GetValueOrDefault(RolleEndringsinstruks), Q = per.GetValueOrDefault(RolleSitertTekst), I = per.GetValueOrDefault(RolleIkrafttredelse),
            O = per.GetValueOrDefault(RolleOppheving), S = per.GetValueOrDefault(RolleEgetInnhold);
        var ut = enheter.Where(e => e.Rolle is not (RolleOverskrift or RolleIgnorert)).Select(e => new VedtaksformEnhet(e.Id, e.Rolle, e.T)).ToList();
        var forsteS = enheter.FirstOrDefault(e => e.Rolle == RolleEgetInnhold);
        string? bevisId = forsteS?.Id, bevis = forsteS is null ? null : Avkort(forsteS.T);
        var form = tEndr ? "endrings" : tOpph ? "opphevings" : "ikrafttredelses";
        var signal = $"tittel: endring={Ja(tEndr)}, oppheving={Ja(tOpph)}, ikrafttredelse={Ja(tIkr)}; Endrer: {endrerAntall}";

        VedtaksformResultat R(string klasse, string grunnlag, string ks = Strukturkanter.Normalisering, bool medBevis = false) =>
            new(klasse, $"Regel {Regelversjon}. {grunnlag} ({signal}.)", ks, medBevis ? bevisId : null, medBevis ? bevis : null, per, ut);

        if (E + Q + I + O + S == 0) return R(Ukjent, "Ingen operativ tekst i kroppen.", Strukturkanter.Uavklart);
        // [ENDRET etter målingen i PR 1, M29] En tittel som sier at kilden har overgangsregler, har eget innhold, selv om leddet ikke
        // gjenkjennes («For skolar som er godkjende etter … gjeld …»).
        if (S == 0 && TittelOvergang().IsMatch(t))
        {
            return R(Blandet, $"Tittelen sier at kilden har overgangsregler («{TittelOvergang().Match(t).Value}»), så den har eget innhold.");
        }
        if (S > 0)
        {
            var hvor = $"første: {bevisId ?? "(uten id)"} «{bevis}»";
            return tittelform
                ? R(Blandet, $"Tittelen har {form}form, men {S} ledd har eget innhold, {hvor}.", medBevis: true)
                : R(Selvstendig, $"{S} ledd har eget innhold, og tittelen har ikke endrings-, opphevings- eller ikrafttredelsesform; {hvor}.", medBevis: true);
        }
        // [ENDRET etter målingen i PR 1, M01/M03/M05/M08] En ren klasse krever tittelform. «Endrer» i hodet alene var ikke nok: et nytt
        // delegeringsvedtak som «endrer» det forrige og opphever det i samme vedtak, står med det forrige i «Endrer», men innholdet står bare i
        // det nye vedtaket (4 av 10 i laget med bare hodesignal). Uten tittelform blir kilden ukjent.
        var harSignal = tittelform;
        var tall = $"instrukser {E}, sitert tekst {Q}, ikrafttredelse {I}, oppheving {O}, eget innhold 0";
        if (E + Q > 0)
        {
            return harSignal
                ? R(RentEndringsvedtak, $"Bare endringsinstrukser, sitert ny tekst, ikrafttredelse og oppheving av andre dokumenter ({tall}).")
                : R(Ukjent, $"Endringsform i teksten ({tall}), men tittelen har ikke endringsform. «Endrer» i hodet alene avgjør ikke (målingen i PR 1).",
                    Strukturkanter.Uavklart);
        }
        if (tEndr && endrerAntall > 0)
        {
            return R(RentEndringsvedtak, $"Endringsdelen er innarbeidet eller utelatt hos Lovdata; det som står igjen, er bare ikrafttredelse og oppheving ({tall}).");
        }
        if (tIkr && I > 0) return R(RentIkrafttredelsesvedtak, $"Bare ikrafttredelse og eventuelt oppheving av andre dokumenter ({tall}).");
        if (O > 0)
        {
            // [ENDRET etter juristrunden for #446 fase 1, D4] Også rene opphevingsvedtak krever et tittel- eller hodesignal.
            return harSignal
                ? R(RentOpphevingsvedtak, $"Bare oppheving av andre dokumenter og eventuelt ikrafttredelse ({tall}).")
                : R(Ukjent, $"Bare opphevingssetninger ({tall}), men tittelen har ikke endrings-, opphevings- eller ikrafttredelsesform.", Strukturkanter.Uavklart);
        }
        return R(Ukjent, $"Bare ikrafttredelsessetninger ({tall}), men tittelen har ikke ikrafttredelsesform.", Strukturkanter.Uavklart);
    }

    private static string Ja(bool b) => b ? "ja" : "nei";

    private static List<Enhet> LesEnheter(string html, out Dictionary<int, string> beholdertyper)
    {
        var start = html.IndexOf("<main", StringComparison.Ordinal);
        var doc = new HtmlDocument();
        doc.LoadHtml(html[start..]);
        var enheter = new List<Enhet>();
        var typer = new Dictionary<int, string>();
        var neste = 0;
        var main = doc.DocumentNode.SelectSingleNode("//main") ?? doc.DocumentNode;
        typer[++neste] = "main";

        void Gå(HtmlNode node, List<int> beholdere, List<Enhet> enhetsstakk)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                if (enhetsstakk.Count > 0) enhetsstakk[^1].Tekst.Append(WebUtility.HtmlDecode(node.InnerText));
                return;
            }
            if (node.NodeType != HtmlNodeType.Element) return;
            var klasse = node.GetAttributeValue("class", "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (Ignorerte.Contains(klasse) || node.Name is "footer" or "sup") return;
            var nyeBeholdere = beholdere;
            if (Beholdere.Contains(klasse))
            {
                typer[++neste] = klasse;
                nyeBeholdere = [.. beholdere, neste];
            }
            var nyStakk = enhetsstakk;
            var overskrift = node.Name is "h1" or "h2" or "h3" or "h4";
            if (Enhetsklasser.Contains(klasse) || overskrift || node.Name is "td" or "th")
            {
                var e = new Enhet
                {
                    Klasse = overskrift ? node.Name : klasse.Length > 0 ? klasse : node.Name,
                    Overskrift = overskrift,
                    Id = node.GetAttributeValue("id", null!),
                    Beholdere = [.. nyeBeholdere],
                    DefaultPForeldre = [.. enhetsstakk.Where(x => x.Klasse == "defaultP").Select(x => enheter.IndexOf(x))],
                };
                enheter.Add(e);
                nyStakk = [.. enhetsstakk, e];
            }
            foreach (var barn in node.ChildNodes) Gå(barn, nyeBeholdere, nyStakk);
        }

        foreach (var barn in main.ChildNodes) Gå(barn, [1], []);
        beholdertyper = typer;
        return enheter;
    }

    private static void SettRoller(List<Enhet> enheter, Dictionary<int, string> typer)
    {
        int? endringsdel = null;
        var listehode = false;
        foreach (var u in enheter)
        {
            u.T = Norm(u.Tekst.ToString());
            var t = u.T;
            if (u.Overskrift && !OverskriftIDefaultP().IsMatch(t)) { u.Rolle = RolleOverskrift; continue; }
            if (t.Length == 0 || Ellipse().IsMatch(t)) { u.Rolle = RolleIgnorert; continue; }
            if (Listehode().IsMatch(t)) { listehode = true; u.Rolle = RolleIgnorert; continue; }
            if ((u.Klasse is "defaultP" or "centeredP" || u.Overskrift) && OverskriftIDefaultP().IsMatch(t))
            {
                // [ENDRET etter målingen i PR 1, M40] En overskrift «Overgangsregel …» eller «Ikrafttredelse …» utenfor Lovdatas markering for endret
                // tekst avslutter endringsdelen: det som kommer etter, er kildens eget innhold, ikke sitert tekst.
                if (!u.Beholdere.Any(b => Endringsbeholdere.Contains(typer.GetValueOrDefault(b, "")))) endringsdel = null;
                u.Rolle = RolleOverskrift;
                continue;
            }
            if (listehode && Dokumentliste().IsMatch(t)) { u.Rolle = RolleIgnorert; continue; }
            listehode = false;
            if (u.DefaultPForeldre.Any(i => i >= 0 && Listehode().IsMatch(Norm(enheter[i].Tekst.ToString()))) && !SterkInstruks().IsMatch(t))
            {
                u.Rolle = RolleIgnorert; continue;
            }
            if (endringsdel is { } d && !u.Beholdere.Contains(d)) endringsdel = null;
            var iEndringsbeholder = u.Beholdere.Any(b => Endringsbeholdere.Contains(typer.GetValueOrDefault(b, "")));
            var setninger = Setningsskille().Split(t).Where(s => s.Trim().Length > 0).ToList();
            var iDel = endringsdel is not null || iEndringsbeholder;
            var roller = setninger.Select(s => SetningRolle(s, iDel)).ToList();

            if (roller.Count > 0 && roller.Contains(RolleEndringsinstruks) && (roller[0] == RolleEndringsinstruks || u.Klasse == "defaultP"))
            {
                // Instruksen gjelder resten av nærmeste romertallsdel eller paragraf.
                endringsdel = u.Beholdere.Reverse().FirstOrDefault(b => typer.GetValueOrDefault(b) is "section" or "legalArticle" or "main");
                u.Rolle = RolleEndringsinstruks;
                continue;
            }
            if (iDel)
            {
                // En ikraftsetning på toppnivå i en kilde uten romertallsdeler avslutter endringsdelen.
                if (!iEndringsbeholder && endringsdel is { } dd && typer.GetValueOrDefault(dd) == "main" && roller.FirstOrDefault() == RolleIkrafttredelse
                    && IkraftSubjekt().IsMatch(t))
                {
                    endringsdel = null;
                }
                else
                {
                    u.Rolle = RolleSitertTekst;
                    continue;
                }
            }
            if (u.Klasse is "defaultP" or "centeredP" or "miscHeadline" or "futuretitle" && Ingress().IsMatch(t)) { u.Rolle = RolleIgnorert; continue; }
            if (u.Klasse is "miscHeadline" or "futuretitle") { u.Rolle = RolleOverskrift; continue; }
            u.Rolle = roller.Contains(RolleEgetInnhold) || roller.Contains(RolleEndringsinstruks) ? RolleEgetInnhold
                : roller.Contains(RolleIkrafttredelse) ? RolleIkrafttredelse
                : roller.Contains(RolleOppheving) ? RolleOppheving
                : RolleEgetInnhold;
        }
    }

    /// <summary>Rollen til én setning. <paramref name="iEndringsdel"/>: svake instrukser («Ny § 5») teller bare inne i en endringsdel.</summary>
    internal static string SetningRolle(string s, bool iEndringsdel)
    {
        var egetInnhold = EgetInnholdISetning().IsMatch(s) || (IkraftILeddsetning().IsMatch(s) && !IkraftIHovedsetning().IsMatch(s.TrimStart()));
        if (SterkInstruks().IsMatch(s) || InstruksMedDokument().IsMatch(s)) return RolleEndringsinstruks;
        if (iEndringsdel && SvakInstruks().IsMatch(s) && !egetInnhold) return RolleEndringsinstruks;
        if (Ikraft().IsMatch(s)) return egetInnhold && !KanSetteIKraft().IsMatch(s) ? RolleEgetInnhold : RolleIkrafttredelse;
        if (Oppheves().IsMatch(s) && Dokumentreferanse().IsMatch(s) && !egetInnhold) return RolleOppheving;
        return RolleEgetInnhold;
    }

    private static string Norm(string s) => Mellomrom().Replace(s.Replace(' ', ' '), " ").Trim();
    private static string Avkort(string s) => s.Length <= 300 ? s : s[..300] + "…";

    // ---------------------------------------------------------------- Ikraftstatus

    public const string IkraftJa = "ja";
    public const string IkraftDelvis = "delvis";
    public const string IkraftIkkeIKraft = "ikke_i_kraft";
    public const string IkraftUtlopt = "utlopt";
    public const string IkraftUkjent = "ukjent";
    /// <summary>[LÅST, prosjekteierens avgjørelse 2026-11-25] Speilet i CHECK-en <c>ck_rettskilder_ikraft_status</c>.</summary>
    public static readonly IReadOnlyList<string> Ikraftstatuser = [IkraftJa, IkraftDelvis, IkraftIkkeIKraft, IkraftUtlopt, IkraftUkjent];

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")] private static partial Regex IsoDato();
    [GeneratedRegex(@"^\s*(\d{4}-\d{2}-\d{2})\s*[–-]\s*(\d{4}-\d{2}-\d{2})\s*$")] private static partial Regex Intervall();
    [GeneratedRegex(@"(Inntektsåret|Budsjettåret|Skatteåret)\s+(\d{4})", F)] private static partial Regex Ar();
    [GeneratedRegex(@"bestemmer|fastset|samråd|når\b|ikke i kraft", F)] private static partial Regex Udatert();

    /// <summary>
    /// Ikraftstatusen slik Lovdatas «I kraft fra» (<c>ikrafttredelse_raa</c>) sier den. Uttrykk uten dato («Kongen bestemmer») gir
    /// <see cref="IkraftUkjent"/>: feltet er frosset fra kunngjøringen og kan være utdatert (juristrunden D8), så tjenesten følger en
    /// ikraftsetting i korpus før den gir opp. <see cref="IkraftDelvis"/> betyr at minst én dato er passert og minst én ligger fram i tid.
    /// </summary>
    public static (string Status, string Grunnlag) IkraftFraFelt(string? raa, DateOnly? ikrafttredelse, DateOnly idag)
    {
        if (string.IsNullOrWhiteSpace(raa))
        {
            return ikrafttredelse is { } d
                ? (d <= idag ? IkraftJa : IkraftIkkeIKraft, $"«I kraft fra» mangler; ikrafttredelse {d:yyyy-MM-dd}.")
                : (IkraftUkjent, "«I kraft fra» mangler.");
        }
        var felt = $"«I kraft fra»: «{raa.Trim()}»";
        if (Intervall().Match(raa) is { Success: true } iv)
        {
            var fra = DateOnly.ParseExact(iv.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var til = DateOnly.ParseExact(iv.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return (til < idag ? IkraftUtlopt : fra > idag ? IkraftIkkeIKraft : IkraftJa, $"{felt} (tidsbegrenset).");
        }
        var datoer = IsoDato().Matches(raa).Select(m => DateOnly.ParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList();
        datoer.AddRange(Ar().Matches(raa).Select(m => new DateOnly(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), 1, 1)));
        var rest = Ar().Replace(IsoDato().Replace(raa, ""), "");
        var udatert = Udatert().IsMatch(rest);
        if (udatert || datoer.Count == 0) return (IkraftUkjent, $"{felt}; minst én del har ingen dato.");
        var passert = datoer.Count(d => d <= idag);
        return (passert == datoer.Count ? IkraftJa : passert == 0 ? IkraftIkkeIKraft : IkraftDelvis, $"{felt}.");
    }

    [GeneratedRegex(@"\b(\d{1,2})\.\s*(januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)\s+(\d{4})", F)]
    private static partial Regex NorskDato();
    private static readonly string[] Maneder = ["januar", "februar", "mars", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "desember"];
    // En ikraftsetting av HELE loven: «Loven trer i kraft …», «Lov … trer i kraft …», uten paragrafer, deler eller unntak.
    [GeneratedRegex(@"^(Loven|Lova|Lov\b.{0,300}?)\s+(trer|tek|skal gjelde|gjeld|gjelder)\b.{0,20}\bkraft|^(Loven|Lova|Lov\b.{0,300}?)\s+(skal gjelde|gjeld|gjelder)\s+fr[aå]", F)]
    private static partial Regex HelLovIKraft();
    [GeneratedRegex(@"§|\bdel(en)?\s+[IVX]+\b|\bkapittel\b|\bkap\.|\bunntak\b|\bbortsett\b|\bpunkt\b|\bnr\.\s*\d+\s+(første|annet|andre)|\bbestemmelsene? i\b", F)]
    private static partial Regex Delvis();

    /// <summary>
    /// [Juristrunden for #446 fase 1, D8] Leser en ikraftsetting i korpus: setningene med rollen ikrafttredelse. Gir (hel, dato) når én setning
    /// setter hele loven i kraft på en dato som er passert, ellers null. Ingen gjetting: en ikraftsetting av deler gir null.
    /// </summary>
    public static (DateOnly Dato, string Sitat)? HelIkraftsetting(VedtaksformResultat ikraftsetting, DateOnly idag)
    {
        foreach (var e in ikraftsetting.Enheter.Where(e => e.Rolle == RolleIkrafttredelse))
        {
            foreach (var s in Setningsskille().Split(e.Tekst))
            {
                if (!HelLovIKraft().IsMatch(s) || Delvis().IsMatch(s)) continue;
                var m = NorskDato().Match(s);
                if (!m.Success) continue;
                var dato = new DateOnly(int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                    Array.IndexOf(Maneder, m.Groups[2].Value.ToLowerInvariant()) + 1, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
                if (dato <= idag) return (dato, Avkort(s.Trim()));
            }
        }
        return null;
    }

    /// <summary>
    /// [Prosjekteierens avgjørelse 2026-11-25, valg C] Avleder «uttømt virkning» (<c>rettskilder.rent_endringsvedtak</c>) av formen og ikraftstatusen:
    /// <list type="bullet">
    /// <item><c>true</c>: ren form, og vedtaket er i kraft eller utløpt.</item>
    /// <item><c>false</c>: blandet eller selvstendig, eller ren form der det er sikkert at vedtaket ikke er (helt) i kraft.</item>
    /// <item><c>null</c>: formen er ukjent eller ikke vurdert, eller ren form der ikrafttredelsen er ukjent.</item>
    /// </list>
    /// </summary>
    public static bool? UttomtVirkning(string? klasse, string? ikraftStatus) => klasse switch
    {
        null or Ukjent => null,
        Blandet or Selvstendig => false,
        _ when Rene.Contains(klasse) => ikraftStatus switch
        {
            IkraftJa or IkraftUtlopt => true,
            IkraftDelvis or IkraftIkkeIKraft => false,
            _ => null,
        },
        _ => null,
    };
}
