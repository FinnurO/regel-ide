using System.Text.RegularExpressions;

namespace RegelIde.Data;

/// <summary>Et dokument identifisert ved dato og nummer, slik lovteksten siterer det («lov 11. juni 1993 nr. 65»). <see cref="Nr"/> er tom for
/// eldre lover uten nummer («lov 15. juli 1925»).</summary>
public sealed record Dokumentmal(string Type, int Aar, int Mnd, int Dag, string Nr)
{
    public DateOnly Dato => new(Aar, Mnd, Dag);
}

/// <summary>
/// [Ny, #472 PR 1, Johanns avgjørelser 2026-11-18] <b>Tekstlesingen bak hjemmelsstatusen</b> (<see cref="HjemmelsstatusRegel"/>): når er et
/// hjemmelsdokument opphevet ifølge teksten i en annen gjeldende kilde, og når videreføres vedtak gitt i medhold av det. Portert fra
/// fase 1-målingen (<c>scratchpad/sovende/felles.py</c>, <c>m2_opphevet.py</c> og <c>m4_klassifiser.py</c>), med juristrundens rettinger.
/// <para>
/// <b>Delt, ikke koblet inn i #429.</b> Fire av formene her er rettinger av koden bak R8 og R9 i maskinregelen for utgåtte aktører
/// (<see cref="Opphevingskontroll"/>): passiv form, «til» etter nummeret, hele noden som én setning når tittelen har punktum, og vakten mot
/// et mål som bare står i tittelen til noe annet som oppheves. Uten dem ville tallene i #472 vært feil (FOR-1993-11-12-1022 og
/// FOR-1988-04-29-312 i juristens utvalg). [LÅST, oppdraget for #472 PR 1] De kobles IKKE inn i R8/R9 her: det er PR 3, som da bare trenger å
/// kalle disse funksjonene. Det eneste som er endret i <see cref="Opphevingskontroll"/>, er at målmønsteret kan gis utenfra
/// (<see cref="Opphevingskontroll.DelvisMal"/>, <see cref="Opphevingskontroll.VidereforingssetningMal"/>), med samme svar for R8 og R9.
/// </para>
/// </summary>
public static class Hjemmelstekstlesing
{
    private static readonly string[] Maaneder =
        ["januar", "februar", "mars", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "desember"];

    private const string Maaned = "(januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)";

    /// <summary>Dato og nummer fra en dokument-ELI, også for lover uten nummer («…/eli/lov/1925/07/15/nor»). Null for andre former.</summary>
    public static Dokumentmal? Mal(string? eli)
    {
        var m = Regex.Match(eli ?? "", @"/eli/(lov|forskrift)/(\d{4})/(\d{2})/(\d{2})/(\d+)(/|$)");
        var uten = m.Success ? null : Regex.Match(eli ?? "", @"/eli/(lov|forskrift)/(\d{4})/(\d{2})/(\d{2})/nor");
        var t = m.Success ? m : uten is { Success: true } ? uten : null;
        if (t is null) return null;
        int aar = int.Parse(t.Groups[2].Value), mnd = int.Parse(t.Groups[3].Value), dag = int.Parse(t.Groups[4].Value);
        if (mnd is < 1 or > 12 || dag is < 1 or > 31 || !DateOnly.TryParse($"{aar:0000}-{mnd:00}-{dag:00}", out _)) return null;
        return new Dokumentmal(t.Groups[1].Value, aar, mnd, dag, m.Success ? m.Groups[5].Value : "");
    }

    /// <summary>Dokument-ELI-en (alt fram til og med «/nor») av en eId.</summary>
    public static string DokumentEli(string eid)
    {
        var i = eid.IndexOf("/nor", StringComparison.Ordinal);
        return i < 0 ? eid : eid[..(i + 4)];
    }

    public static string DatoRegex(Dokumentmal m) => $@"\b{m.Dag}\.\s*{Maaneder[m.Mnd - 1]}\s+{m.Aar}\b";

    /// <summary>Dato og nummer; for et dokument uten nummer: datoen som ikke følges av «nr.» (fase 1, <c>mal_regex</c>).</summary>
    public static string MalRegex(Dokumentmal m) => m.Nr.Length > 0
        ? DatoRegex(m) + @".{0,15}?nr\.?\s*" + m.Nr + @"\b"
        : DatoRegex(m) + @"(?!.{0,15}?nr\.)";

    /// <summary>Som <see cref="MalRegex"/>, men tåler «4 juni 1976 nr 59» uten punktum (fase 1, <c>mal_regex_los</c>, brukt i
    /// videreføringssøket).</summary>
    public static string MalRegexLos(Dokumentmal m) =>
        $@"\b{m.Dag}\.?\s*{Maaneder[m.Mnd - 1]}\s+{m.Aar}\b" + (m.Nr.Length > 0 ? @".{0,15}?nr\.?\s*" + m.Nr + @"\b" : @"(?!.{0,15}?nr\.?\s*\d)");

    public static string Normaliser(string? tekst) => UtgattAktorRegler.Normaliser(tekst);

    private static string[] Setninger(string tekst) => Opphevingskontroll.Setninger(tekst);

    /// <summary>Om teksten nevner målet med dato og nummer.</summary>
    public static bool Nevner(string? tekst, Dokumentmal m) => Regex.IsMatch(Normaliser(tekst), MalRegex(m));

    private const string Opphevingsverb = "(oppheves|avløser|erstatter|[Oo]ppheving av)";

    /// <summary>Setningen som opphever HELE målet (samme kontroll som <see cref="Opphevingskontroll.FullOpphevingssetning"/>, også for lover
    /// uten nummer), eller null.</summary>
    public static string? FullOpphevingssetning(string? tekst, Dokumentmal m)
    {
        string drx = DatoRegex(m), mrx = MalRegex(m);
        foreach (var s in Setninger(Normaliser(tekst)))
        {
            if (Regex.IsMatch(s, Opphevingsverb) && Regex.IsMatch(s, mrx) && !Opphevingskontroll.DelvisMal(s, drx, mrx)) return s.Trim();
        }
        return null;
    }

    /// <summary>
    /// [Ny, #472, rettingene fase 1 fant i #429-koden] Den utvidede formen: passiv («blir/ble/er … opphevet», «vert oppheva»), en tittel som
    /// begynner med «til» etter nummeret («lov 7. juni 1968 nr. 4 til gjennomføring av …»), og hele noden som én setning når den er kort og
    /// målet er det første daterte dokumentet i den. Den siste formen trengs fordi setningsdelingen deler ved punktum i en tittel:
    /// «Vedtak 12. november 1993 nr. 1022 om … (konkurranseloven). Delegering av myndighet, oppheves.» (LTI/forskrift/2004-04-16-631 kap. III).
    /// Ellers samme delvis-kontroll. Returnerer (sitatet ordrett, setningen omskrevet til aktiv form for vakten), eller null.
    /// </summary>
    public static (string Sitat, string Vaktsetning)? UtvidetOpphevingssetning(string? tekst, Dokumentmal m)
    {
        string drx = DatoRegex(m), mrx = MalRegex(m);
        var tn = Normaliser(tekst);
        var kandidater = Setninger(tn).ToList();
        if (tn.Length < 400)
        {
            var forste = Regex.Match(tn, @"\d{1,2}\.\s*" + Maaned + @"\s+\d{4}");
            var mm = Regex.Match(tn, mrx);
            if (forste.Success && mm.Success && forste.Index == mm.Index) kandidater.Add(tn);
        }
        foreach (var s in kandidater)
        {
            if (!Regex.IsMatch(s, mrx)) continue;
            var passiv = Regex.IsMatch(s, @"\b(blir|ble|er|vert|vart)\b[^.;]{0,160}?\b(opphevet|oppheva|opphevd)\b");
            if (!passiv && !Regex.IsMatch(s, Opphevingsverb)) continue;
            var s2 = Regex.Replace(s, @"\b(blir|ble|er|vert|vart)\s+(opphevet|oppheva|opphevd)\b", "oppheves");
            s2 = Regex.Replace(s2, @"(nr\.?\s*" + (m.Nr.Length > 0 ? m.Nr : "x") + @")\s+til\s+[^.;]*", "$1");
            if (passiv && !Regex.IsMatch(s2, @"\boppheves\b"))
            {
                // «Fra samme dato blir lov … opphevet»: verbet står etter målet.
                s2 = Regex.Replace(s2, @"\b(blir|ble|er|vert|vart)\s+(" + mrx + @"[^.;]*?)\s+(opphevet|oppheva|opphevd)\b", "$2 oppheves");
            }
            if (Regex.IsMatch(s2, Opphevingsverb) && !Opphevingskontroll.DelvisMal(s2, drx, mrx)) return (s.Trim(), s2);
        }
        return null;
    }

    private static readonly Regex AnnetDokument = new(
        @"\b(forskrift\w*|kgl\.?|kongelig\w*|vedtak\w*|delegering\w*|instruks\w*|resolusjon\w*|henvisning\w*|bestemmelse\w*"
        + @"|lovbestemmelse\w*|kapit+el|ikrafttred\w*|ikraftsetting\w*|unntak\w*|endring\w*|vedlegg)\b|§", RegexOptions.IgnoreCase);

    /// <summary>
    /// [Ny vakt, #472 fase 1] Målet er det som oppheves, ikke et dokument som nevnes i tittelen til noe annet som oppheves («oppheves forskrift
    /// … om ikrafttredelse av lov …»). Teksten mellom verbet og målet skal ikke nevne et annet dokument. Vakten avviste 30 setninger i fase 1.
    /// <para>
    /// [ENDRET, #472 PR 1, AK 7] Målets egen dokumenttype kan ha utstederen mellom typen og datoen: «oppheves følgende delegeringer: Kongelig
    /// resolusjon fra Forbruker- og administrasjonsdepartementet av 29. april 1988 nr. 312 …» (DEL/forskrift/2003-12-19-1790 kap. VI). Uten det
    /// avviste vakten FOR-1988-04-29-312, og fase 1 satte den til EN.
    /// </para>
    /// </summary>
    public static bool MalErObjekt(string s, Dokumentmal m)
    {
        var mal = Regex.Match(s, MalRegex(m));
        var verb = Regex.Matches(s, Opphevingsverb).ToList();
        if (!mal.Success || verb.Count == 0) return false;
        var foran = verb.Where(v => v.Index < mal.Index).ToList();
        string seg;
        if (foran.Count > 0)
        {
            var v = foran[^1];
            seg = s[(v.Index + v.Length)..mal.Index];
        }
        else
        {
            // Verbet står etter målet: fra setningens start til målet.
            seg = Regex.Replace(s[..mal.Index], @"^(Fra\s+(samme|den)\s+(tid|dag|dato|tidspunkt)[^,]*,?|Samtidig|Fra\s+samme\s+tid)\s*", "");
        }
        const RegexOptions I = RegexOptions.IgnoreCase;
        // Målets egen dokumenttype rett foran datoen («oppheves forskrift 12. november …», «Lov av …», «kgl.res. av …»), også med utstederen
        // («Kongelig resolusjon fra X-departementet av …»).
        seg = Regex.Replace(seg.Trim(),
            @"((\bmidlertidig|\bmellombels)\s+)?(\blov(a|en)?|\bforskrift(a|en)?|\bkgl\.?\s*res\.?|\bkongelig\s+resolusjon|\b\w*vedtak(et)?|\binstruks(en)?"
            + @"|\bresolusjon(en)?|\bdelegering(en|svedtak)?|\bmidlertidig\s+lov|\bmellombels\s+lov|\bkronprins\w*\s+resolusjon)"
            + @"(\s+fra\s+[\w\s-]{1,80}?(departementet|direktoratet|tilsynet|Kongen))?(\s+av)?\s*$", "", I);
        // «oppheves kongelig resolusjon 27. oktober 2017 nr. 1666 og kongelig resolusjon 18. desember …»: en liste av hele dokumenter.
        seg = Regex.Replace(seg,
            @"(\b(lov|forskrift|kgl\.?\s*res\.?|kongelig\s+resolusjon|vedtak)\w*\s+(av\s+)?\d{1,2}\.\s*\w+\s+\d{4}\s+nr\.?\s*\d+[^;]*?(,|\bog\b)\s*)+$", "", I);
        // Innledningen til en liste: «oppheves følgende (andre) forskrifter:», «gjøres følgende endring i annen lov:».
        seg = Regex.Replace(seg,
            @"(følgende\s+(andre\s+)?(lover|forskrifter|vedtak|delegeringer|kongelige\s+resolusjoner)\s*:|gjøres\s+følgende\s+endring(er)?\s+i\s+an(nen|dre)\s+lov(er)?\s*:)", "", I);
        seg = Regex.Replace(seg.Trim(), @"^\(\d+\)\s*", "");
        seg = Regex.Replace(seg, @"^Fra\s+(den\s+tid|samme\s+tid|forskriftens\s+ikrafttredelse|ikrafttredelsen\s+av\s+\w+\s+her)[^,:]*[,:]?\s*", "", I);
        return !AnnetDokument.IsMatch(seg);
    }

    /// <summary>[Fase 1, E2] Uten «Endrer»-kobling må verbet stå utenfor parentes («(oppheving av juryordningen)» er tittel), og når verbet står
    /// etter målet, skal ikke et annet datert dokument stå mellom målet og verbet.</summary>
    public static bool E2Vilkar(string s, Dokumentmal m)
    {
        const string verb = @"\b(oppheves|oppheva|opphevet|opphevd|avløser|erstatter)\b";
        var mm = Regex.Match(s, MalRegex(m));
        var vv = Regex.Match(s, verb);
        var mellom = mm.Success && vv.Success && vv.Index > mm.Index + mm.Length ? s[(mm.Index + mm.Length)..vv.Index] : "";
        return Regex.IsMatch(Regex.Replace(s, @"\([^)]*\)", ""), verb) && !Regex.IsMatch(mellom, @"\d{1,2}\.\s*" + Maaned + @"\s+\d{4}");
    }

    /// <summary>Om noden nevner et oppheve-ord uten en hel opphevingssetning (bevistrinn E3 når den også nevner målet).</summary>
    public static bool HarOpphevingsord(string? tekst) => Regex.IsMatch(Normaliser(tekst), "(oppheve|avløs|erstatt|opphør)");

    // ---- Videreføring (fase 1, m4_klassifiser.py; juristrunde 1 R3) ----------------------------------------------------------------

    private const string Objekt = @"(forskrift\w*|vedtak\w*|enkeltvedtak\w*|reglement\w*|instruks\w*|vedtekt\w*|delegering\w*|bestemmelse\w*|føresegn\w*|regler|reglar|tillatels\w*|løyve\w*|beslutning\w*|myndighet)";
    private const string Hjemmelsuttrykk = @"(i\s+medh[oa]ld\s+av|med\s+(hjemmel|heimel)\s+i|gitt\s+etter|fastsatt\s+etter|truffet\s+etter|gjeve\s+etter)";
    private const string Videreforingsverb =
        @"(gjelder|gjeld|gjelde|gjelda)\s+(fortsatt|framleis|fremdeles|også|òg|inntil|til|frem\s+til|fram\s+til|i\s+inntil|i\s+den\s+utstrekning|så\s+langt)"
        + @"|(fortsatt|framleis|fremdeles)\s+(gjelde|gjeld|gjelder|stå\s+ved\s+lag)|står\s+(fortsatt\s+)?ved\s+lag|opprettholdes|vedblir|videreføres|blir\s+stående"
        + @"|beholder\s+sin\s+gyldighet|skal\s+gjelde\s+(inntil|til|frem|fram)";
    private const string Utelat = @"^\W*(\(\d+\)\s*)?(Det\s+samme|Tilsvarende|Bestemmelsene\s+i\s+(loven|lova)\s+her|Loven|Lova|Reglene\s+i|Kongen\s+kan|Første|Den\s+gjelder)";
    private const string Generisk =
        @"(lov\w*\s+som\s+(oppheves|blir\s+opphevet|blir\s+oppheva|vert\s+oppheva)|bestemmelser\s+som\s+(oppheves|blir\s+opphevet)|reglar\s+som\s+blir\s+oppheva|regler\s+som\s+(blir\s+opphevet|oppheves)"
        + @"|lovene\s+nevnt|lover\s+nevnt|lovene\s+som\s+er\s+nevnt|lover\s+som\s+er\s+nevnt|de\s+lover\s+som|tidligere\s+lov\w*\s+som\s+oppheves"
        + @"|noen\s+bestemmelse\s+som\s+oppheves|lovbestemmelser\s+som\s+(oppheves|blir))";

    /// <summary>En setning som viderefører noe gitt i medhold av en lov: objekt, hjemmelsuttrykk og videreføringsverb, og ikke en innledning
    /// som «Det samme gjelder» eller «Kongen kan».</summary>
    public static bool ErVidereforing(string s) =>
        Regex.IsMatch(s, Objekt, RegexOptions.IgnoreCase) && Regex.IsMatch(s, Hjemmelsuttrykk, RegexOptions.IgnoreCase)
        && Regex.IsMatch(s, Videreforingsverb, RegexOptions.IgnoreCase) && !Regex.IsMatch(s, Utelat);

    /// <summary>V1: #429s <see cref="Opphevingskontroll.Videreforingssetning"/>, også for lover uten nummer.</summary>
    public static string? VidereforingV1(string? tekst, Dokumentmal m) => Opphevingskontroll.VidereforingssetningMal(tekst, MalRegex(m));

    /// <summary>V2/V2a: utvidet ordform med målet (dato uten punktum godtas).</summary>
    public static string? VidereforingV2(string? tekst, Dokumentmal m)
    {
        var rx = MalRegexLos(m);
        return Setninger(Normaliser(tekst)).FirstOrDefault(s => Regex.IsMatch(s, rx) && ErVidereforing(s))?.Trim();
    }

    /// <summary>V3: generisk form («forskrifter gitt med hjemmel i lov som oppheves …», «lovene nevnt i første ledd»). [ENDRET, #472 PR 1,
    /// målt 2026-11-20] Også bokmålsformen «regler som blir opphevet» (fase 1 hadde bare nynorsk «reglar som blir oppheva»): uhl. 2024 § 17-3
    /// «Forskrifter og enkeltvedtak som er fastsatt med hjemmel i regler som blir opphevet eller endret med denne loven, gjelder til departementet
    /// fastsetter noe annet.» Uten den sto 465 hjemmelsrader til uhl. 2005 som opphevet uten videreføring.</summary>
    public static string? VidereforingV3(string? tekst) =>
        Setninger(Normaliser(tekst)).FirstOrDefault(s => Regex.IsMatch(s, Generisk, RegexOptions.IgnoreCase) && ErVidereforing(s))?.Trim();

    /// <summary>V4: kortnavnene den opphevende loven selv bruker om målet («oppheves straffeloven 22. mai 1902 nr. 10», «lov … om viltet
    /// (viltloven)»).</summary>
    public static IEnumerable<string> Kortnavn(string? tekst, Dokumentmal m)
    {
        var t = Normaliser(tekst);
        foreach (Match x in Regex.Matches(t, MalRegexLos(m)))
        {
            var foran = Regex.Match(t[Math.Max(0, x.Index - 40)..x.Index], @"\b(\w{4,}(loven|lova))\s+(av\s+)?$");
            if (foran.Success && foran.Groups[1].Value.ToLowerInvariant() is not ("denne" or "loven" or "lova")) yield return foran.Groups[1].Value;
            var etter = Regex.Match(t[(x.Index + x.Length)..], @"^[^()]{0,160}?\((\w{4,}(loven|lova))\)");
            if (etter.Success) yield return etter.Groups[1].Value;
        }
    }

    public static string? VidereforingV4(string? tekst, string kortnavn)
    {
        var k = Regex.Escape(kortnavn);
        return Setninger(Normaliser(tekst)).FirstOrDefault(s => Regex.IsMatch(s, @"\b" + k + @"\b") && ErVidereforing(s)
            && Regex.IsMatch(s, @"(i\s+medh[oa]ld\s+av|med\s+(hjemmel|heimel)\s+i)\s+(\w+\s+){0,3}?" + k))?.Trim();
    }

    /// <summary>[Juristrunde 1, R3] Forbeholdet i videreføringen: «så langt de passer», «med mindre de strider mot», «så langt de også kunne ha
    /// vært gitt …». Gir kunnskapsstatus fortolkning, ikke en annen status.</summary>
    public static string? Forbehold(string s)
    {
        var m = Regex.Match(s,
            @"\b(så\s+langt\s+(de|dei|det|den)\s+(passer|passar|høver)|i\s+den\s+utstrekning|så\s+langt\s+(de|dei)\s+ikke\s+strider|så\s+langt\s+(de|dei)\s+ikkje\s+strid\w*"
            + @"|med\s+mindre\s+(de|dei)\s+strider|så\s+langt\s+(de|dei)\s+(også\s+)?kunne\s+(ha\s+vært|vere)\s+gitt|ikke\s+er\s+i\s+strid|ikkje\s+er\s+i\s+strid)\b",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Value : null;
    }

    /// <summary>[Juristrunde 1, R3] Tidsfristen i videreføringen: «frem til 31. desember 2019» (fagskoleloven 2018 § 44 tredje ledd), «inntil
    /// 1. januar 2020», «for budsjett- og regnskapsåret 1993» (siste dag i året). En dato som følges av «nr.», er et dokument, ikke en frist.
    /// «inntil Kongen bestemmer» er ingen frist.</summary>
    public static DateOnly? Tidsfrist(string s)
    {
        foreach (Match x in Regex.Matches(s, @"\b(frem\s+til|fram\s+til|inntil|til\s+og\s+med|til)\s+(og\s+med\s+)?(den\s+)?(\d{1,2})\.\s*" + Maaned + @"\s+(\d{4})(?!\s*nr)",
                     RegexOptions.IgnoreCase))
        {
            var mnd = Array.IndexOf(Maaneder, x.Groups[5].Value.ToLowerInvariant()) + 1;
            if (DateOnly.TryParse($"{x.Groups[6].Value}-{mnd:00}-{int.Parse(x.Groups[4].Value):00}", out var d)) return d;
        }
        var aar = Regex.Match(s, @"\bfor\s+(budsjett-\s+og\s+regnskaps|budsjett|regnskaps)?året\s+(\d{4})\b", RegexOptions.IgnoreCase);
        return aar.Success ? new DateOnly(int.Parse(aar.Groups[2].Value), 12, 31) : null;
    }

    /// <summary>[Juristrunde 1, R3, S12] Avgrensningen i videreføringen: kapitler og paragrafer som står mellom målet og verbet
    /// («… lov 19. november 1982 nr. 66 om helsetjenesten i kommunene kapittel 4a og § 7-9 gjelder …»). Tom = ingen avgrensning.</summary>
    public static (IReadOnlyList<string> Kapitler, IReadOnlyList<string> Paragrafer) Avgrensning(string s, Dokumentmal m)
    {
        var mal = Regex.Match(s, MalRegexLos(m));
        if (!mal.Success) return ([], []);
        var etter = s[(mal.Index + mal.Length)..];
        var verb = Regex.Match(etter, @"\b(gjelder|gjeld|gjelde|skal|står|opprettholdes|vedblir|videreføres|blir|beholder)\b");
        var seg = verb.Success ? etter[..verb.Index] : etter;
        var kap = Regex.Matches(seg, @"\b(?:kapittel|kap\.)\s+([0-9]+[a-z]?|[IVXLC]+)\b").Select(x => x.Groups[1].Value).ToList();
        var par = new List<string>();
        foreach (Match x in Regex.Matches(seg, @"§§?\s*(\d+[a-z]?(?:-\d+[a-z]?)?)((?:\s*(?:,|og)\s*(?:§\s*)?\d+[a-z]?(?:-\d+[a-z]?)?)*)"))
        {
            par.Add(x.Groups[1].Value);
            par.AddRange(Regex.Matches(x.Groups[2].Value, @"\d+[a-z]?(?:-\d+[a-z]?)?").Select(y => y.Value));
        }
        return (kap, par);
    }

    /// <summary>Paragrafen («7-9», «13-1») i en hjemmels-eId, eller null når hjemmelen er hele dokumentet.</summary>
    public static string? Paragraf(string eid)
    {
        var m = Regex.Match(eid, @"/§([^/]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Dokumenttypene en videreføringssetning nevner: forskrift, vedtak, delegering, instruks, reglement, vedtekt.</summary>
    public static IReadOnlySet<string> Dokumenttyper(string s)
    {
        var t = new HashSet<string>();
        if (Regex.IsMatch(s, @"\b(forskrift\w*|føresegn\w*|bestemmelse\w*|regler|reglar)\b", RegexOptions.IgnoreCase)) t.Add("forskrift");
        if (Regex.IsMatch(s, @"\b(enkelt)?vedtak\w*\b", RegexOptions.IgnoreCase)) t.Add("vedtak");
        if (Regex.IsMatch(s, @"\bdelegering\w*\b", RegexOptions.IgnoreCase)) t.Add("delegering");
        if (Regex.IsMatch(s, @"\binstruks\w*\b", RegexOptions.IgnoreCase)) t.Add("instruks");
        if (Regex.IsMatch(s, @"\breglement\w*\b", RegexOptions.IgnoreCase)) t.Add("reglement");
        if (Regex.IsMatch(s, @"\bvedtekt\w*\b", RegexOptions.IgnoreCase)) t.Add("vedtekt");
        return t;
    }

    // ---- Annet ---------------------------------------------------------------------------------------------------------------------

    /// <summary>[Juristrunde 1, R4, godtatt i svaret] Et vedtak som tildeler kompetanse: «gis myndighet», «delegeres», «overføres», «tildeles».
    /// Brukes bare for å finne hva som erstatter et opphevet kompetanseledd, i samme vedtak som opphever det.</summary>
    public static bool TildelerKompetanse(string? tekst) =>
        Regex.IsMatch(Normaliser(tekst), @"\b(gis\s+(med\s+dette\s+)?(myndighet|kompetanse)|delegeres|delegerer|overføres|tildeles|får\s+myndighet)\b", RegexOptions.IgnoreCase);

    /// <summary>[Juristrunde 2, R2 godtatt] Loven kan settes i kraft til ulik tid: da er ikraftsettingen av opphevingsbestemmelsen ikke kontrollert
    /// (verdipapirsentralloven § 12-1, enhetsregisterloven 2025 § 13-1).</summary>
    public static bool UlikIkraftsetting(string? tekst) =>
        Regex.IsMatch(Normaliser(tekst), @"\b(til|fra)\s+(forskjellig|ulik|ulike)\s+(tid|tidspunkt)", RegexOptions.IgnoreCase);

    /// <summary>[Juristrunde 2, R5 godtatt] Hjemmelen er en endringslov som navngir nøyaktig én lov: «lov 13. desember 2002 nr. 77 om endringer
    /// i lov 10. juni 1988 nr. 40 om …». Returnerer ELI-en til loven som endres, eller null (også når tittelen sier «og enkelte andre lover»).</summary>
    public static string? EndringslovTil(string? hjemmelstekst, Dokumentmal endringslov)
    {
        var t = Normaliser(hjemmelstekst);
        var m = Regex.Match(t, MalRegex(endringslov));
        if (!m.Success) return null;
        var etter = t[(m.Index + m.Length)..];
        var e = Regex.Match(etter, @"^\s*om\s+endring(ar|er)?\s+i\s+(lov(a|en)?\s+)?(av\s+)?(\d{1,2})\.\s*" + Maaned + @"\s+(\d{4})\s+nr\.?\s*(\d+)\b([^.;]*)");
        if (!e.Success) return null;
        if (Regex.IsMatch(e.Groups[9].Value, @"\b(andre\s+lover|enkelte\s+andre|m\.\s*fl\.|og\s+(i\s+)?lov(a|en)?\s+\d)", RegexOptions.IgnoreCase)) return null;
        var mnd = Array.IndexOf(Maaneder, e.Groups[6].Value) + 1;
        return $"https://lovdata.no/eli/lov/{e.Groups[7].Value}/{mnd:00}/{int.Parse(e.Groups[5].Value):00}/{e.Groups[8].Value}/nor";
    }

    /// <summary>[Fase 1, END] Hjemmelsteksten omtaler dokumentet som en endringslov: «lov … nr. N om endring(er) i …». Gjelder også når
    /// <see cref="EndringslovTil"/> ikke kan følge den (flere lover i tittelen).</summary>
    public static bool ErEndringslov(string? hjemmelstekst, Dokumentmal endringslov)
    {
        var t = Normaliser(hjemmelstekst);
        var m = Regex.Match(t, MalRegex(endringslov));
        return m.Success && Regex.IsMatch(t[(m.Index + m.Length)..], @"^\s*om\s+endring(ar|er)?\b");
    }

    /// <summary>«§ 48 andre ledd» eller «kap. III første ledd» for en eId; ellers eId-en etter «/nor/».</summary>
    public static string Bestemmelsesetikett(string eid)
    {
        if (AktorkoTjeneste.Bestemmelse(eid) is { } b) return b;
        var k = Regex.Match(eid, @"(?:^|/)kap-([^/]+)(?:/ledd-(\d+))?(?:/punkt-(\d+))?$");
        if (k.Success)
        {
            var s = "kap. " + k.Groups[1].Value;
            if (k.Groups[2].Success) s += " " + Ledd(int.Parse(k.Groups[2].Value));
            if (k.Groups[3].Success) s += " punkt " + k.Groups[3].Value;
            return s;
        }
        return UtgattAktorFunnregel.Bestemmelsesetikett(eid);
    }

    private static readonly string[] Ordenstall = ["", "første", "andre", "tredje", "fjerde", "femte", "sjette", "sjuende", "åttende", "niende", "tiende"];
    private static string Ledd(int n) => n < Ordenstall.Length ? $"{Ordenstall[n]} ledd" : $"ledd {n}";
}
