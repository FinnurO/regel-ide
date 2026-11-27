using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, #429 PR 3] Én oppheving funnet i teksten til en annen gjeldende primærkilde: hvem som opphever, i hvilken node, og setningen
/// ordrett (beviset).</summary>
public sealed record Oppheving(Guid OpphevendeId, string? OpphevendeDokumentId, string OpphevendeEli, string OpphevendeTittel, string Eid, string Sitat)
{
    /// <summary>«FOR-2006-06-30-733 § 1 første ledd» — Lovdatas korte referanse og bestemmelsen.</summary>
    public string Etikett => $"{UtgattAktorFunnregel.Dokumentreferanse(OpphevendeDokumentId) ?? UtgattAktorFunnregel.EliKort(OpphevendeEli)} "
        + UtgattAktorFunnregel.Bestemmelsesetikett(Eid);
}

/// <summary>[Ny, #429 PR 3, R9] Hjemmelsloven til en rettskilde er opphevet av en gjeldende lov i korpus.</summary>
/// <param name="Hjemmelsreferanse">«lov 24. juni 1988 nr. 64».</param>
/// <param name="FraHjemmelsfelt">Hjemmelen står i Lovdatas hjemmelsfelt for rettskilden.</param>
/// <param name="SitertIEider">Nodene som siterer loven med dato og nummer (tom når den bare står i hjemmelsfeltet).</param>
/// <param name="Viderefores">En setning i den opphevende loven som kan videreføre vedtak og forskrifter gitt i medhold av den gamle loven
/// («Enkeltvedtak og forskrifter gitt i medhold av lov … gjelder også»). Null = ikke funnet, som ikke betyr at det ikke finnes.</param>
public sealed record HjemmelOpphevet(
    string HjemmelEli, string Hjemmelsreferanse, bool FraHjemmelsfelt, IReadOnlyList<string> SitertIEider, Oppheving Opphevet, Oppheving? Viderefores)
{
    /// <summary>Hvor hjemmelen er lest: «Lovdatas hjemmelsfelt» og/eller «sitert i § 1 første ledd».</summary>
    public string Grunnlag => string.Join(" og ", (FraHjemmelsfelt ? new[] { "Lovdatas hjemmelsfelt" } : [])
        .Concat(SitertIEider.Count == 0 ? [] : [$"sitert i {string.Join(", ", SitertIEider.Select(UtgattAktorFunnregel.Bestemmelsesetikett))}"]));

    /// <summary>Gjelder et funn med bestemmelsene <paramref name="eider"/>: alltid når hjemmelen står i hjemmelsfeltet, ellers bare når en
    /// av funnets egne noder siterer loven.</summary>
    public bool GjelderNoder(IEnumerable<string> eider) => FraHjemmelsfelt || eider.Any(e => SitertIEider.Contains(e, StringComparer.Ordinal));
}

/// <summary>
/// [Ny, #429 PR 3, Johanns avgjørelser 2026-11-02 etter måling 2] <b>Opphevinger lest av teksten</b>, for R8 og R9 i maskinregelen for
/// utgåtte aktører og for premissporten.
/// <para>
/// Lovdatas «Endrer»-felt (<see cref="RettskildeEndringEntitet"/>) skiller ikke endring fra oppheving: tabellen har ingen slik kolonne, og
/// juristen fant at fire av åtte målvedtakskoblinger var opphevinger merket som endringer. Den eneste måten å skille dem på uten å gjette, er
/// å lese teksten i kilden som står med «Endrer»: en setning med «oppheves», «avløser», «erstatter» eller «Oppheving av» og målets dato og
/// nummer («15. november 2005 nr. 1286»). Det er juristens verifisering (<c>jurist429b_skript/r8_verifiser.py</c>), portert ordrett.
/// </para>
/// <para>
/// [LÅST, Johann 2026-11-02] <b>Delvise opphevinger holdes utenfor.</b> «I forskrift … oppheves kapittel 6», «kapitel VII i forskrift …»
/// og «I av kgl.res. …» opphever en del, ikke kilden. Juristens skript skilte ikke disse ut (tre av 45 rettskilder i måling 2). Formene er
/// konservative: en del foran målet («kapittel/§/del/punkt/ledd/vedlegg/romertall … i/av [inntil tre ord] &lt;dato&gt;») eller rett etter
/// «oppheves» («oppheves kapittel 6»). En oppheving som ikke kjennes igjen, teller som delvis — ikke som hel (CLAUDE.md §8).
/// </para>
/// <para>
/// [ENDRET, #472 PR 3, Johanns avgjørelse 2026-11-18 (4)] R8, R9 og målvedtaket leser teksten med hjemmelsstatusens lesing
/// (<see cref="Hjemmelstekstlesing.LesOppheving"/>): <see cref="FullOpphevingssetning"/> først, så passiv, «til» etter nummeret og hele
/// noden når tittelen har punktum, og alt gjennom vakten mot et mål i tittelen til noe annet. Kravet om «Endrer» og unntaket for SF og NL
/// i R8 står fast. [ENDRET, prosjekteierens avgjørelse 2026-11-24] R8 leser også formelen i foreldrenoden. <see cref="FullOpphevingssetning"/> og
/// <see cref="Videreforingssetning"/> er beholdt som byggesteinene de er.
/// </para>
/// </summary>
public static class Opphevingskontroll
{
    private static readonly string[] Maaneder =
        ["januar", "februar", "mars", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "desember"];

    private const string Maaned = "(januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)";

    /// <summary>Dato og nummer fra en dokument-ELI: «…/eli/lov/1988/06/24/64/nor» → (lov, 1988-06-24, «64»). Null for andre former.</summary>
    public static (string Type, DateOnly Dato, string Nr)? DatoOgNummer(string? eli)
    {
        var m = Regex.Match(eli ?? "", @"/eli/(lov|forskrift)/(\d{4})/(\d{2})/(\d{2})/(\d+)(/|$)");
        if (!m.Success) return null;
        if (!DateOnly.TryParse($"{m.Groups[2].Value}-{m.Groups[3].Value}-{m.Groups[4].Value}", out var d)) return null;
        return (m.Groups[1].Value, d, m.Groups[5].Value);
    }

    /// <summary>«lov 24. juni 1988 nr. 64» for en lov-ELI. [ENDRET, #472 PR 3] «lov 15. juli 1925» for en lov uten nummer
    /// (<see cref="Hjemmelstekstlesing.Mal"/>).</summary>
    public static string Referanse(string eli) => Hjemmelstekstlesing.Mal(eli) is { } x
        ? $"{x.Type} {x.Dag}. {Maaneder[x.Mnd - 1]} {x.Aar}" + (x.Nr.Length > 0 ? $" nr. {x.Nr}" : "")
        : UtgattAktorFunnregel.EliKort(eli);

    private static string DatoRegex(DateOnly d) => $@"\b{d.Day}\.\s*{Maaneder[d.Month - 1]}\s+{d.Year}\b";

    /// <summary>Juristens setningsdeling i r8_verifiser.py. [ENDRET, #472 PR 1] <c>internal</c> slik at <see cref="Hjemmelstekstlesing"/> deler
    /// den; uendret.</summary>
    internal static string[] Setninger(string tekst) => Regex.Split(tekst, @"(?<![\d])(?<=[.;])\s+(?=[A-ZÆØÅ])");

    /// <summary>Setningen som opphever HELE dokumentet med datoen og nummeret, eller null. Se klassekommentaren for den delvise
    /// opphevingen.</summary>
    public static string? FullOpphevingssetning(string? tekst, DateOnly dato, string nr)
    {
        var datoRx = DatoRegex(dato);
        foreach (var s in Setninger(UtgattAktorRegler.Normaliser(tekst)))
        {
            if (Regex.IsMatch(s, "(oppheves|avløser|erstatter|[Oo]ppheving av)") && Regex.IsMatch(s, datoRx + @".{0,15}?nr\.?\s*" + nr + @"\b")
                && !Delvis(s, datoRx, nr))
            {
                return s.Trim();
            }
        }
        return null;
    }

    /// <summary>[LÅST, Johann 2026-11-02] Delvis oppheving: en del står foran målet («kapitel VII i forskrift …», «I av kgl.res. …»), rett
    /// etter «oppheves» («oppheves kapittel 6») eller rett etter målets nummer («… nr. 77 kapittel I til VII-a»). Delordene må stå som egne
    /// ord: [ENDRET, tørrkjøringen 2026-11-02] uten ordgrensen ble «del» lest inn i «delegering» og «delegeringsvedtak», og to hele
    /// opphevinger ble regnet som delvise (FOR-2013-02-25-231 og FOR-2014-08-15-1083).
    /// <para>
    /// [ENDRET, #429 PR 4, Johanns avgjørelse 2026-11-04 (1a) etter måling 3] <b>Hviteliste, ikke svarteliste.</b> Svartelisten over slapp
    /// gjennom to delvise opphevinger som ble lest som hele: «… 10. januar 2020 nr. 33, oppheves for så vidt gjelder vedtaket del I …»
    /// (FOR-2022-10-21-1777 punkt 2, funnet F06 ble feilaktig forklart bort) og «… 21. desember 1990 nr. 1017 pkt. II» (FOR-2005-04-29-378).
    /// En oppheving er nå hel bare når det som står RETT ETTER målets nummer, og rett etter verbet når verbet står etter målet, er på en liste
    /// over former som ikke avgrenser (<see cref="EtterMalet"/>, <see cref="EtterVerbet"/>). Alt annet teller som delvis (CLAUDE.md §8).
    /// Dagens tre svartelisteformer står i tillegg, og «for så vidt» hvor som helst i setningen er alltid delvis. Tittelen på målet («om
    /// delegering av myndighet etter lov … § 2 tredje ledd») leses ikke: den inneholder paragrafer og ledd i nesten alle de 44 hele
    /// opphevingene juristen kontrollerte, og kan ikke skilles fra en avgrensning uten å gjette.
    /// </para></summary>
    public static bool Delvis(string setning, string datoRegex, string nr) =>
        DelvisMal(setning, datoRegex, datoRegex + @".{0,15}?nr\.?\s*" + nr + @"\b");

    /// <summary>[Ny, #472 PR 1] Samme kontroll som <see cref="Delvis"/>, med målmønsteret gitt utenfra. <see cref="Hjemmelstekstlesing"/>
    /// trenger det for lover uten nummer («lov 15. juli 1925»), der målet er datoen som IKKE følges av «nr.». For et mål med nummer er
    /// mønsteret det samme som før (<c>\b</c> mellom et siffer og et mellomrom slår alltid til), så R8 og R9 får samme svar.</summary>
    internal static bool DelvisMal(string setning, string datoRegex, string malRegex)
    {
        if (Regex.IsMatch(setning, @"(?<!\w)(kapit+el|§+|del|punkt|ledd|vedlegg|[IVX]+)(?!\w)\s*[\w.-]*\s+(i|av)\s+(\S+\s+){0,3}?" + datoRegex)
            || Regex.IsMatch(setning, @"\boppheves\s+(kapit+el|§|punkt|pkt\.?|del|ledd|nr\.|vedlegg|[IVX]+)(?!\w)")
            || Regex.IsMatch(setning, malRegex + @"\s+(kapit+el|§|del|punkt|pkt\.?|vedlegg)(?!\w)")
            || Regex.IsMatch(setning, @"\bfor\s+så\s+vidt\b", RegexOptions.IgnoreCase))
        {
            return true;
        }
        var mal = Regex.Match(setning, malRegex);
        if (!mal.Success) return true; // ikke målet: ingen hel oppheving av det
        var etter = setning[(mal.Index + mal.Length)..];
        if (!EtterMalet.IsMatch(etter)) return true;
        var verb = Regex.Match(etter, @"\b(oppheves|avløses|erstattes)\b");
        if (!verb.Success) return false;
        var etterVerbet = EtterVerbet.Match(etter[(verb.Index + verb.Length)..]);
        if (!etterVerbet.Success) return true;
        // [Ny, #502] En dato rett etter verbet som ikke er passert («Forskrift 9. oktober 2015 nr. 1166 … oppheves 31. desember 2026.»): opphevingen
        // virker ikke ennå, så målet er ikke opphevet. Det leses ikke som en hel oppheving (som «ikke i kraft» i hjemmelsstatusen).
        return etterVerbet.Groups["dato"].Success && DatoFraTekst(etterVerbet.Groups["dato"].Value) is { } d && d > Idag();
    }

    private static DateOnly Idag() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>«1. januar 2019» → 2019-01-01, ellers null.</summary>
    private static DateOnly? DatoFraTekst(string s)
    {
        var m = Regex.Match(s, @"(\d{1,2})\.\s*" + Maaned + @"\s+(\d{4})", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var mnd = Array.IndexOf(Maaneder, m.Groups[2].Value.ToLowerInvariant()) + 1;
        return DateOnly.TryParse($"{m.Groups[3].Value}-{mnd:00}-{int.Parse(m.Groups[1].Value):00}", out var d) ? d : null;
    }

    /// <summary>[Ny, #429 PR 4] Hvitelisten for det som står rett etter målets nummer: slutten på setningen, en liste («og», «samt», komma
    /// og et nytt dokument eller verbet), tittelen («om», «for», «angående», «vedrørende», «hvor», tankestrek, parentes), eller verbet. Formene
    /// er hentet fra de 44 hele opphevingene juristen kontrollerte i måling 3 (<c>jurist429c/kildefeil_opphevende.tsv</c>). «for så vidt» er
    /// tatt ut før denne testen.
    /// <para>
    /// [ENDRET, #502, prosjekteierens avgjørelse 2026-11-27] To former til, hentet fra opphevingene R8 leste som delvise (8 LTI-funn i #472):
    /// <list type="bullet">
    /// <item><b>En tittel som begynner med «av» og det som delegeres:</b> «Delegering 29. juni 2001 nr. 721 av myndighet til Forsvarsdepartementet
    /// …» (DEL/forskrift/2018-12-20-2052 punkt 5), «… nr. 1192 av departementets myndighet …», «… nr. 1093 av utøvelsen av statens
    /// partsstilling …», «… nr. 1578 av fullmakt til …». Bare «av» med myndighet, mynde, kompetanse, fullmakt eller utøvelse, også med en eier
    /// foran («departementets», «Kongens»). Et annet ord etter «av» er fortsatt delvis.</item>
    /// <item><b>Utstederen etter «fra»:</b> «Samtidig oppheves instruks 18. mars 2017 nr. 357 fra Mattilsynet ved Hovedkontoret til Debio …»
    /// (LTI/forskrift/2019-07-02-961 kap. II). Bare «fra» med et navn med stor bokstav, så «fra og med § 3» er fortsatt delvis.</item>
    /// </list>
    /// </para></summary>
    internal static readonly Regex EtterMalet = new(
        @"^\s*([.;:]|$|(og|samt|eller)\b|[–(]|-\s|(om|for|angående|vedrørende|hvor)\b|(oppheves|avløses|erstattes|er\s+opphevet)\b|med\s+virkning\b"
        + @"|,\s*((oppheves|avløses|erstattes|er\s+opphevet|og|samt)\b|(kgl|kongelig|kronprins|forskrift|vedtak|delegering|instruks|resolusjon|lov|rundskriv)\w*)"
        + @"|av\s+(\w+s\s+)?(myndighet|mynde|kompetanse|fullmakt|utøvelse)\w*\b"
        + @"|(?-i:fra\s+[A-ZÆØÅ]))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>[Ny, #429 PR 4] Hvitelisten for det som står rett etter verbet når verbet står etter målet: slutten på setningen, eller når
    /// opphevingen virker («med virkning fra», «fra», «ved ikraftsetting», «når», «samtidig», «straks», «i sin helhet»). «oppheves for så vidt
    /// gjelder …», «oppheves kapittel 6» og alt annet er delvis.
    /// <para>
    /// [ENDRET, #502, prosjekteierens avgjørelse 2026-11-27] <b>En dato rett etter verbet</b>, når setningen slutter der: «Delegering 27. juni
    /// 2003 nr. 802 av myndighet … oppheves 1. januar 2019.» (DEL/forskrift/2018-12-20-2052 punkt 5), «Forskrift 26. oktober 2021 nr. 3092 om
    /// nasjonal retningslinje for barnesykepleierutdanning oppheves 1. januar 2026.» Datoen sier når opphevingen virker, som «fra ‹dato›».
    /// En dato som følges av «nr.», er et dokument, og en dato som følges av noe annet («oppheves 1. januar 2006 med virkning for regnskapsår
    /// påbegynt …»), er fortsatt delvis. En dato som ikke er passert, gir ingen hel oppheving: opphevingen virker ikke ennå («… nr. 1166 …
    /// oppheves 31. desember 2026.», SF/forskrift/2026-03-23-454 § 25).
    /// </para></summary>
    internal static readonly Regex EtterVerbet = new(
        @"^\s*,?\s*([.;]|$|med\s+virkning\b|fra\b|ved\s+ikraft|når\b|samtidig\b|straks\b|i\s+sin\s+helhet\b"
        + @"|(den\s+)?(?<dato>\d{1,2}\.\s*(januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)\s+\d{4})\s*([.;]|$))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>En setning som kan videreføre vedtak og forskrifter gitt i medhold av loven med datoen og nummeret (havne- og
    /// farvannsloven § 53 første ledd, yrkestransportlova § 42, arbeidsmiljøloven § 20-2 første ledd i måling 2). Bare et hint til juristen.
    /// [ENDRET, #429 PR 4, Johanns avgjørelse 2026-11-04 (1b)] Godtar også «med hjemmel i» og «(skal) fortsatt gjelde»: ekomloven 2024
    /// § 18-2 første ledd, «Enkeltvedtak og forskrift med hjemmel i lov 4. juli 2003 nr. 83 … skal fortsatt gjelde» (M05 i måling 3), ble
    /// ikke vist.</summary>
    public static string? Videreforingssetning(string? tekst, DateOnly dato, string nr) =>
        VidereforingssetningMal(tekst, DatoRegex(dato) + @".{0,15}?nr\.?\s*" + nr + @"\b");

    /// <summary>[Ny, #472 PR 1] <see cref="Videreforingssetning"/> med målmønsteret gitt utenfra (også lov uten nummer). Delt med
    /// <see cref="Hjemmelstekstlesing"/> som videreføringsformen V1. Samme svar som før for et mål med nummer.</summary>
    internal static string? VidereforingssetningMal(string? tekst, string malRegex)
    {
        foreach (var s in Setninger(UtgattAktorRegler.Normaliser(tekst)))
        {
            if (Regex.IsMatch(s, @"\b(i\s+medh[oa]ld\s+av|med\s+hjemmel\s+i)\b")
                && Regex.IsMatch(s, @"\b(gjelder|gjeld|skal\s+gjelde|(skal\s+)?fortsatt\s+gjelde)\b")
                && Regex.IsMatch(s, malRegex))
            {
                return s.Trim();
            }
        }
        return null;
    }

    private sealed record Kandidatnode(Guid RettskildeId, string? DokumentId, string Eli, string Tittel, string Eid, string? Tekst, int Rekkefolge);

    /// <summary>
    /// [Ny, #472 PR 3] <b>Den første opphevingen av <paramref name="m"/> i <paramref name="noder"/></b>, lest med den delte tekstlesingen
    /// (<see cref="Hjemmelstekstlesing.LesOppheving"/>): hel setning, passiv, «til» etter nummeret, hele noden når tittelen har punktum, og
    /// vakten mot et mål i tittelen til noe annet. En hel setning går foran den utvidede formen, slik E1 går foran E1u i hjemmelsstatusen;
    /// ellers vinner den første noden i rekkefølgen. Erstatter <see cref="FullOpphevingssetning"/> i R8, R9 og målvedtaket, slik at #429 og
    /// #472 gir samme svar på samme tekst.
    /// </summary>
    private static (Kandidatnode Node, string Sitat)? ForsteOppheving(IEnumerable<Kandidatnode> noder, Dokumentmal m)
    {
        (Kandidatnode, string)? utvidet = null;
        foreach (var n in noder)
        {
            if (Hjemmelstekstlesing.LesOppheving(n.Tekst, m).Lesning is not { } l) continue;
            if (!l.Utvidet) return (n, l.Sitat);
            utvidet ??= (n, l.Sitat);
        }
        return utvidet;
    }

    /// <summary>[Ny, #472 PR 3] Alle gjeldende noder med tekst i <paramref name="kilder"/>, i lovens rekkefølge (som <c>NoderPerKilde</c> i
    /// <see cref="HjemmelsstatusRegel"/>).</summary>
    private static async Task<Dictionary<Guid, List<Kandidatnode>>> AlleNoderAsync(RegelIdeDbContext db, IReadOnlyCollection<Guid> kilder, CancellationToken ct)
    {
        if (kilder.Count == 0) return [];
        var noder = await db.RettskildeNoder.AsNoTracking()
            .Where(n => kilder.Contains(n.RettskildeId) && n.Entitetsstatus == "gjeldende" && !n.Opphevet && !n.ErMerknad && n.Tekst != null)
            .Join(db.Rettskilder, n => n.RettskildeId, r => r.Id,
                (n, r) => new Kandidatnode(r.Id, r.DokumentId, r.Eli ?? "", r.Kortnavn ?? r.Tittel, n.Eid, n.Tekst, n.Sorteringsrekkefolge))
            .ToListAsync(ct);
        return noder.GroupBy(n => n.RettskildeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Rekkefolge).ThenBy(n => n.Eid, StringComparer.Ordinal).ToList());
    }

    /// <summary>Nodene i de gjeldende primærkildene som har et av <paramref name="mal"/> i «Endrer»-feltet, med tekst som kan inneholde en
    /// oppheving eller en videreføring. Kildene som er selve målet, er utelatt.
    /// <para>
    /// [Ny, #429 PR 4, Johanns avgjørelse 2026-11-04 (1c), bare for R9 (<paramref name="medForelderformel"/>)] <b>Opphevingsformelen i noden
    /// over.</b> Deltakerloven 1999 § 32 nr. 2 har formelen i forelderen («Fra samme tid oppheves følgende lover:») og loven som oppheves, i
    /// punktet under («Lov av 16. juni 1972 nr. 57 om regulering av deltagelsen i fisket.», M16 i måling 3). Barnet leses da med forelderens
    /// tekst foran, og opphevingen forankres i barnets eId. Bare når forelderen slutter med formelen (<see cref="UtgattAktorRegler.Opphevingsformel"/>).
    /// R8 bruker ikke dette: avgjørelsen gjaldt R3 og R9, og R8 ville gitt nye kildefeil.
    /// [ENDRET, prosjekteierens avgjørelse 2026-11-24, fraviker #429 PR 4 (1c)] R8 leser også formelen i noden over, slik R3, R9 og
    /// hjemmelsstatusen gjør, så R8 gir samme svar som hjemmelsstatusen på samme tekst. Målt i en kopi av basen: to kilder til går til
    /// kildefeilkøen (LTI/forskrift/2001-12-21-1708 og LTI/forskrift/2013-02-01-134).
    /// </para></summary>
    private static async Task<List<(string MalEli, Kandidatnode Node)>> NoderSomEndrerAsync(
        RegelIdeDbContext db, IReadOnlyCollection<string> mal, bool bareLover, CancellationToken ct, bool medForelderformel = false)
    {
        if (mal.Count == 0) return [];
        var par = new List<(string MalEli, Guid Kilde)>();
        foreach (var bit in mal.Chunk(2000))
        {
            var rader = await db.RettskildeEndringer.AsNoTracking().Where(e => bit.Contains(e.EndringEid))
                .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende" && r.Importrolle == "primaer" && r.Eli != null),
                    e => e.RettskildeId, r => r.Id, (e, r) => new { e.EndringEid, r.Id, r.Eli })
                .ToListAsync(ct);
            par.AddRange(rader.Where(x => x.Eli != x.EndringEid && (!bareLover || x.Eli!.Contains("/eli/lov/", StringComparison.Ordinal)))
                .Select(x => (x.EndringEid, x.Id)));
        }
        var kilder = par.Select(p => p.Kilde).Distinct().ToList();
        if (kilder.Count == 0) return [];
        var noder = await db.RettskildeNoder.AsNoTracking()
            .Where(n => kilder.Contains(n.RettskildeId) && n.Entitetsstatus == "gjeldende" && !n.Opphevet && !n.ErMerknad && n.Tekst != null
                        && (n.Tekst.Contains("pphev") || n.Tekst.Contains("avløser") || n.Tekst.Contains("erstatter") || n.Tekst.Contains("medh")
                            || n.Tekst.Contains("med hjemmel")))
            .Join(db.Rettskilder, n => n.RettskildeId, r => r.Id,
                (n, r) => new { n.Id, Node = new Kandidatnode(r.Id, r.DokumentId, r.Eli!, r.Kortnavn ?? r.Tittel, n.Eid, n.Tekst, n.Sorteringsrekkefolge) })
            .ToListAsync(ct);
        var formler = medForelderformel
            ? noder.Where(n => UtgattAktorRegler.Opphevingsformel(UtgattAktorRegler.Normaliser(n.Node.Tekst))).ToDictionary(n => n.Id, n => n.Node.Tekst!)
            : [];
        var barn = new List<Kandidatnode>();
        if (formler.Count > 0)
        {
            var formelIder = formler.Keys.ToList();
            barn = (await db.RettskildeNoder.AsNoTracking()
                    .Where(n => n.ParentNodeId != null && formelIder.Contains(n.ParentNodeId.Value) && n.Entitetsstatus == "gjeldende" && !n.Opphevet
                                && !n.ErMerknad && n.Tekst != null)
                    .Join(db.Rettskilder, n => n.RettskildeId, r => r.Id,
                        (n, r) => new { Forelder = n.ParentNodeId!.Value, Node = new Kandidatnode(r.Id, r.DokumentId, r.Eli!, r.Kortnavn ?? r.Tittel, n.Eid, n.Tekst, n.Sorteringsrekkefolge) })
                    .ToListAsync(ct))
                .Select(x => x.Node with { Tekst = Hjemmelstekstlesing.MedForelderformel(formler[x.Forelder], x.Node.Tekst) }).ToList();
        }
        var alle = noder.Select(n => n.Node).Concat(barn).ToList();
        var perKilde = alle.GroupBy(n => n.RettskildeId).ToDictionary(g => g.Key, g => g.OrderBy(n => n.Rekkefolge).ThenBy(n => n.Eid, StringComparer.Ordinal).ToList());
        // Én rad per (mål, node): samme kilde kan stå med «Endrer» for flere mål.
        return par.Distinct().SelectMany(p => (perKilde.GetValueOrDefault(p.Kilde) ?? []).Select(n => (p.MalEli, n))).ToList();
    }

    /// <summary>
    /// <b>R8:</b> hvilke av <paramref name="kilder"/> som er opphevet i sin helhet av en ANNEN gjeldende primærkilde i korpus. Kilden må stå i
    /// den opphevendes «Endrer»-felt, og teksten må si det (se klassekommentaren). Første treff i lovens rekkefølge vinner; flere
    /// opphevende kilder gir ett treff, ikke flere.
    /// </summary>
    public static async Task<Dictionary<Guid, Oppheving>> OpphevetAvAnnenKildeAsync(
        RegelIdeDbContext db, IReadOnlyCollection<(Guid Id, string Eli)> kilder, CancellationToken ct = default)
    {
        var ut = new Dictionary<Guid, Oppheving>();
        var medDato = kilder.Where(k => Hjemmelstekstlesing.Mal(k.Eli) is not null).ToList();
        // [Ny, tørrkjøringen 2026-11-02] Sentrale forskrifter (SF) og lover (NL) holdes utenfor: Lovdata vedlikeholder statusen for dem, så
        // står de som gjeldende, er en «oppheving» i en annen kilde delvis. Alle tre SF-treffene i den delte basen var delvise:
        // FOR-1985-06-28-1679 (kapittel 6), FOR-1954-07-08-3 (kapitel VII) og FOR-1992-12-04-915 («… om sosiale tjenester mv. kapittel 5»,
        // som ingen tekstform fanger). Juristens premiss for R8 er at LTI-statusen ikke vedlikeholdes.
        var ider = medDato.Select(k => k.Id).ToList();
        var dokumentIder = await db.Rettskilder.AsNoTracking().Where(r => ider.Contains(r.Id))
            .Select(r => new { r.Id, r.DokumentId }).ToDictionaryAsync(r => r.Id, r => r.DokumentId, ct);
        medDato = medDato.Where(k => dokumentIder.GetValueOrDefault(k.Id) is not { } d
            || !(d.StartsWith("SF/", StringComparison.Ordinal) || d.StartsWith("NL/", StringComparison.Ordinal))).ToList();
        // [ENDRET, prosjekteierens avgjørelse 2026-11-24] Også formelen i noden over («Fra samme tid oppheves:», «Følgende delegeringsvedtak oppheves:»).
        var noder = await NoderSomEndrerAsync(db, medDato.Select(k => k.Eli).Distinct(StringComparer.Ordinal).ToList(), bareLover: false, ct,
            medForelderformel: true);
        var perMal = noder.GroupBy(x => x.MalEli, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(x => x.Node).ToList(), StringComparer.Ordinal);
        foreach (var (id, eli) in medDato)
        {
            var noderForMal = (perMal.GetValueOrDefault(eli) ?? []).Where(n => n.RettskildeId != id)
                .OrderBy(n => n.DokumentId, StringComparer.Ordinal).ThenBy(n => n.Rekkefolge);
            if (ForsteOppheving(noderForMal, Hjemmelstekstlesing.Mal(eli)!) is { } o)
                ut[id] = new Oppheving(o.Node.RettskildeId, o.Node.DokumentId, o.Node.Eli, o.Node.Tittel, o.Node.Eid, o.Sitat);
        }
        return ut;
    }

    /// <summary>
    /// [Ny, #429 PR 3, Johanns avgjørelse 2026-11-02: «skilles endring fra oppheving i målvedtakskoblingen»] For hvert par (kilde, mål): om
    /// kildens egen tekst opphever HELE målet. <c>rettskilde_endringer</c> har ingen kolonne som skiller endring fra oppheving, så det leses av
    /// teksten med samme verifisering som R8. Ikke funnet betyr «endring eller oppheving, ikke avklart» — ikke «endring».
    /// </summary>
    public static async Task<Dictionary<(Guid Kilde, string MalEli), Oppheving>> OpphevesAvKildenAsync(
        RegelIdeDbContext db, IReadOnlyCollection<(Guid Kilde, string MalEli)> par, CancellationToken ct = default)
    {
        var ut = new Dictionary<(Guid, string), Oppheving>();
        var kilder = par.Select(p => p.Kilde).Distinct().ToList();
        if (kilder.Count == 0) return ut;
        var noder = await db.RettskildeNoder.AsNoTracking()
            .Where(n => kilder.Contains(n.RettskildeId) && n.Entitetsstatus == "gjeldende" && !n.Opphevet && !n.ErMerknad && n.Tekst != null
                        && (n.Tekst.Contains("pphev") || n.Tekst.Contains("avløser") || n.Tekst.Contains("erstatter")))
            .Join(db.Rettskilder, n => n.RettskildeId, r => r.Id,
                (n, r) => new Kandidatnode(r.Id, r.DokumentId, r.Eli ?? "", r.Kortnavn ?? r.Tittel, n.Eid, n.Tekst, n.Sorteringsrekkefolge))
            .ToListAsync(ct);
        var perKilde = noder.GroupBy(n => n.RettskildeId).ToDictionary(g => g.Key, g => g.OrderBy(n => n.Rekkefolge).ToList());
        foreach (var (kilde, malEli) in par.Distinct())
        {
            if (Hjemmelstekstlesing.Mal(malEli) is not { } m) continue;
            if (ForsteOppheving(perKilde.GetValueOrDefault(kilde) ?? [], m) is { } o)
                ut[(kilde, malEli)] = new Oppheving(o.Node.RettskildeId, o.Node.DokumentId, o.Node.Eli, o.Node.Tittel, o.Node.Eid, o.Sitat);
        }
        return ut;
    }

    /// <summary>Lovene en tekst siterer som «lov (av) 24. juni 1988 nr. 64», som dokument-ELI-er.</summary>
    public static IEnumerable<string> SiterteLover(string? tekst)
    {
        foreach (Match m in Regex.Matches(UtgattAktorRegler.Normaliser(tekst), @"\blov\s+(?:av\s+)?(\d{1,2})\.\s*" + Maaned + @"\s+(\d{4})\s+nr\.?\s*(\d+)\b"))
        {
            var mnd = Array.IndexOf(Maaneder, m.Groups[2].Value) + 1;
            var dag = int.Parse(m.Groups[1].Value);
            if (mnd < 1 || dag < 1 || dag > 31) continue;
            yield return $"https://lovdata.no/eli/lov/{m.Groups[3].Value}/{mnd:00}/{dag:00}/{m.Groups[4].Value}/nor";
        }
    }

    /// <summary>
    /// <b>R9:</b> for hver rettskilde, hjemmelslovene som er opphevet av en gjeldende lov i korpus. Hjemmelen leses to steder: Lovdatas
    /// hjemmelsfelt (<see cref="RettskildeHjemmelEntitet"/>) og lover som siteres med dato og nummer i <paramref name="nodetekster"/> (nodene
    /// funnet står i; i N10 står hjemmelen bare i teksten). En lov som selv er gjeldende primær i korpus, er ikke opphevet.
    /// [Johanns avgjørelse 2026-11-02 (3), valgt etter juristens begrunnelse] Resultatet undertrykker ikke funnet; det blir en premissmangel.
    /// </summary>
    public static async Task<Dictionary<Guid, List<HjemmelOpphevet>>> HjemmelOpphevetAsync(
        RegelIdeDbContext db, IReadOnlyCollection<Guid> rettskildeIder,
        IReadOnlyDictionary<Guid, IReadOnlyList<(string Eid, string? Tekst)>> nodetekster, CancellationToken ct = default)
    {
        var ut = new Dictionary<Guid, List<HjemmelOpphevet>>();
        if (rettskildeIder.Count == 0) return ut;

        // (rettskilde, lov-ELI, eId som siterer den — null for hjemmelsfeltet)
        var hjemler = new List<(Guid Rk, string Eli, string? Eid)>();
        foreach (var bit in rettskildeIder.Chunk(2000))
        {
            var rader = await db.RettskildeHjemler.AsNoTracking().Where(h => bit.Contains(h.RettskildeId))
                .Join(db.Rettskilder.IgnoreQueryFilters(), h => h.HjemmelRettskildeId, r => r.Id, (h, r) => new { h.RettskildeId, r.Eli })
                .ToListAsync(ct);
            hjemler.AddRange(rader.Where(x => x.Eli is not null && x.Eli.Contains("/eli/lov/", StringComparison.Ordinal))
                .Select(x => (x.RettskildeId, x.Eli!, (string?)null)));
        }
        foreach (var (rk, noder) in nodetekster)
        {
            foreach (var (eid, tekst) in noder)
            {
                foreach (var eli in SiterteLover(tekst)) hjemler.Add((rk, eli, eid));
            }
        }
        if (hjemler.Count == 0) return ut;

        var eliSett = hjemler.Select(h => h.Eli).Distinct(StringComparer.Ordinal).ToList();
        var iKraft = (await db.Rettskilder.AsNoTracking()
                .Where(r => r.Eli != null && eliSett.Contains(r.Eli) && r.Entitetsstatus == "gjeldende" && r.Importrolle == "primaer")
                .Select(r => r.Eli!).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var kandidater = eliSett.Where(e => !iKraft.Contains(e)).ToList();
        var noderSomEndrer = await NoderSomEndrerAsync(db, kandidater, bareLover: true, ct, medForelderformel: true);
        var perMal = noderSomEndrer.GroupBy(x => x.MalEli, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Node).OrderBy(n => n.DokumentId, StringComparer.Ordinal).ThenBy(n => n.Rekkefolge).ToList(), StringComparer.Ordinal);

        var funnet = new Dictionary<string, (Dokumentmal M, Kandidatnode N, string S)>(StringComparer.Ordinal);
        foreach (var eli in kandidater)
        {
            if (Hjemmelstekstlesing.Mal(eli) is not { } m || !perMal.TryGetValue(eli, out var noder)) continue;
            if (ForsteOppheving(noder, m) is { } o) funnet[eli] = (m, o.Node, o.Sitat);
        }
        // [ENDRET, #472 PR 3] Videreføringen leses i ALLE nodene til den opphevende loven, med formene V1–V4 fra hjemmelsstatusen
        // (Hjemmelstekstlesing.Videreforinger), ikke bare i nodene med et oppheve-ord eller «medhold»: V2 godtar også «gitt etter» og
        // «fastsatt etter», og V3 den generiske formen («regler som blir opphevet», uhl. 2024 § 17-3). V2a (en annen lov) er ikke med: R9
        // leser den opphevende loven.
        // [ENDRET, #501 (#472 PR 5)] Kortnavnet fra tittelen til den opphevede loven godtas som mål i V4 når det er entydig, med samme
        // register som hjemmelsstatusen (Kortnavnregister.LastAsync).
        var egne = await AlleNoderAsync(db, funnet.Values.Select(f => f.N.RettskildeId).Distinct().ToList(), ct);
        var kortnavn = funnet.Count > 0 ? await Kortnavnregister.LastAsync(db, ct) : Kortnavnregister.Tomt;
        var opphevet = new Dictionary<string, (Oppheving O, Oppheving? V)>(StringComparer.Ordinal);
        foreach (var (eli, (m, n, s)) in funnet)
        {
            var v = Hjemmelstekstlesing.Videreforinger(egne.GetValueOrDefault(n.RettskildeId) ?? [], x => x.Tekst, m, null, kortnavn.Entydig(eli))
                .Select(x => ((string Form, Kandidatnode Node, string Sitat)?)x).FirstOrDefault();
            opphevet[eli] = (new Oppheving(n.RettskildeId, n.DokumentId, n.Eli, n.Tittel, n.Eid, s),
                v is { } vv ? new Oppheving(vv.Node.RettskildeId, vv.Node.DokumentId, vv.Node.Eli, vv.Node.Tittel, vv.Node.Eid, vv.Sitat) : null);
        }
        foreach (var g in hjemler.Where(h => opphevet.ContainsKey(h.Eli)).GroupBy(h => (h.Rk, h.Eli)))
        {
            var (o, v) = opphevet[g.Key.Eli];
            if (!ut.TryGetValue(g.Key.Rk, out var liste)) ut[g.Key.Rk] = liste = [];
            liste.Add(new HjemmelOpphevet(g.Key.Eli, Referanse(g.Key.Eli), g.Any(h => h.Eid is null),
                g.Where(h => h.Eid is not null).Select(h => h.Eid!).Distinct(StringComparer.Ordinal).ToList(), o, v));
        }
        return ut;
    }

    /// <summary>Premissmangelen for R9 i klartekst. Den begynner alltid med «Hjemmelsloven kan være opphevet» (UI-et kjenner den igjen
    /// på det, som «Etterfølger ikke vurdert»).</summary>
    public static string Premissmangeltekst(HjemmelOpphevet h) =>
        $"Hjemmelsloven kan være opphevet: rettskilden bygger på {h.Hjemmelsreferanse} ({h.Grunnlag}), og {h.Opphevet.Etikett} opphever den "
        + $"(«{Kort(h.Opphevet.Sitat)}»). "
        + (h.Viderefores is { } v ? $"{v.Etikett} kan videreføre vedtak og forskrifter gitt i medhold av den («{Kort(v.Sitat)}»). " : "")
        + $"Juristen må vurdere om vedtaket fortsatt har virkning (vurderingspunktet «{Funn.HjemmelOverskrift}»).";

    public static string Kort(string s) => s.Length <= 220 ? s : s[..217].TrimEnd() + " …";
}
