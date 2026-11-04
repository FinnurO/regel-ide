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

    /// <summary>«lov 24. juni 1988 nr. 64» for en lov-ELI.</summary>
    public static string Referanse(string eli) => DatoOgNummer(eli) is { } x
        ? $"{x.Type} {x.Dato.Day}. {Maaneder[x.Dato.Month - 1]} {x.Dato.Year} nr. {x.Nr}"
        : UtgattAktorFunnregel.EliKort(eli);

    private static string DatoRegex(DateOnly d) => $@"\b{d.Day}\.\s*{Maaneder[d.Month - 1]}\s+{d.Year}\b";

    /// <summary>Juristens setningsdeling i r8_verifiser.py.</summary>
    private static string[] Setninger(string tekst) => Regex.Split(tekst, @"(?<![\d])(?<=[.;])\s+(?=[A-ZÆØÅ])");

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
    public static bool Delvis(string setning, string datoRegex, string nr)
    {
        if (Regex.IsMatch(setning, @"(?<!\w)(kapit+el|§+|del|punkt|ledd|vedlegg|[IVX]+)(?!\w)\s*[\w.-]*\s+(i|av)\s+(\S+\s+){0,3}?" + datoRegex)
            || Regex.IsMatch(setning, @"\boppheves\s+(kapit+el|§|punkt|pkt\.?|del|ledd|nr\.|vedlegg|[IVX]+)(?!\w)")
            || Regex.IsMatch(setning, datoRegex + @".{0,15}?nr\.?\s*" + nr + @"\s+(kapit+el|§|del|punkt|pkt\.?|vedlegg)(?!\w)")
            || Regex.IsMatch(setning, @"\bfor\s+så\s+vidt\b"))
        {
            return true;
        }
        var mal = Regex.Match(setning, datoRegex + @".{0,15}?nr\.?\s*" + nr + @"\b");
        if (!mal.Success) return true; // ikke målet: ingen hel oppheving av det
        var etter = setning[(mal.Index + mal.Length)..];
        if (!EtterMalet.IsMatch(etter)) return true;
        var verb = Regex.Match(etter, @"\b(oppheves|avløses|erstattes)\b");
        return verb.Success && !EtterVerbet.IsMatch(etter[(verb.Index + verb.Length)..]);
    }

    /// <summary>[Ny, #429 PR 4] Hvitelisten for det som står rett etter målets nummer: slutten på setningen, en liste («og», «samt», komma
    /// og et nytt dokument eller verbet), tittelen («om», «for», «angående», «vedrørende», «hvor», tankestrek, parentes), eller verbet. Formene
    /// er hentet fra de 44 hele opphevingene juristen kontrollerte i måling 3 (<c>jurist429c/kildefeil_opphevende.tsv</c>). «for så vidt» er
    /// tatt ut før denne testen.</summary>
    internal static readonly Regex EtterMalet = new(
        @"^\s*([.;:]|$|(og|samt|eller)\b|[–(]|-\s|(om|for|angående|vedrørende|hvor)\b|(oppheves|avløses|erstattes|er\s+opphevet)\b|med\s+virkning\b"
        + @"|,\s*((oppheves|avløses|erstattes|er\s+opphevet|og|samt)\b|(kgl|kongelig|kronprins|forskrift|vedtak|delegering|instruks|resolusjon|lov|rundskriv)\w*))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>[Ny, #429 PR 4] Hvitelisten for det som står rett etter verbet når verbet står etter målet: slutten på setningen, eller når
    /// opphevingen virker («med virkning fra», «fra», «ved ikraftsetting», «når», «samtidig», «straks», «i sin helhet»). «oppheves for så vidt
    /// gjelder …», «oppheves kapittel 6» og alt annet er delvis.</summary>
    internal static readonly Regex EtterVerbet = new(
        @"^\s*,?\s*([.;]|$|med\s+virkning\b|fra\b|ved\s+ikraft|når\b|samtidig\b|straks\b|i\s+sin\s+helhet\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>En setning som kan videreføre vedtak og forskrifter gitt i medhold av loven med datoen og nummeret (havne- og
    /// farvannsloven § 53 første ledd, yrkestransportlova § 42, arbeidsmiljøloven § 20-2 første ledd i måling 2). Bare et hint til juristen.
    /// [ENDRET, #429 PR 4, Johanns avgjørelse 2026-11-04 (1b)] Godtar også «med hjemmel i» og «(skal) fortsatt gjelde»: ekomloven 2024
    /// § 18-2 første ledd, «Enkeltvedtak og forskrift med hjemmel i lov 4. juli 2003 nr. 83 … skal fortsatt gjelde» (M05 i måling 3), ble
    /// ikke vist.</summary>
    public static string? Videreforingssetning(string? tekst, DateOnly dato, string nr)
    {
        var datoRx = DatoRegex(dato);
        foreach (var s in Setninger(UtgattAktorRegler.Normaliser(tekst)))
        {
            if (Regex.IsMatch(s, @"\b(i\s+medh[oa]ld\s+av|med\s+hjemmel\s+i)\b")
                && Regex.IsMatch(s, @"\b(gjelder|gjeld|skal\s+gjelde|(skal\s+)?fortsatt\s+gjelde)\b")
                && Regex.IsMatch(s, datoRx + @".{0,15}?nr\.?\s*" + nr + @"\b"))
            {
                return s.Trim();
            }
        }
        return null;
    }

    private sealed record Kandidatnode(Guid RettskildeId, string? DokumentId, string Eli, string Tittel, string Eid, string? Tekst, int Rekkefolge);

    /// <summary>Nodene i de gjeldende primærkildene som har et av <paramref name="mal"/> i «Endrer»-feltet, med tekst som kan inneholde en
    /// oppheving eller en videreføring. Kildene som er selve målet, er utelatt.
    /// <para>
    /// [Ny, #429 PR 4, Johanns avgjørelse 2026-11-04 (1c), bare for R9 (<paramref name="medForelderformel"/>)] <b>Opphevingsformelen i noden
    /// over.</b> Deltakerloven 1999 § 32 nr. 2 har formelen i forelderen («Fra samme tid oppheves følgende lover:») og loven som oppheves, i
    /// punktet under («Lov av 16. juni 1972 nr. 57 om regulering av deltagelsen i fisket.», M16 i måling 3). Barnet leses da med forelderens
    /// tekst foran, og opphevingen forankres i barnets eId. Bare når forelderen slutter med formelen (<see cref="UtgattAktorRegler.Opphevingsformel"/>).
    /// R8 bruker ikke dette: avgjørelsen gjaldt R3 og R9, og R8 ville gitt nye kildefeil.
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
                .Select(x => x.Node with { Tekst = formler[x.Forelder].TrimEnd() + " " + x.Node.Tekst }).ToList();
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
        var medDato = kilder.Where(k => DatoOgNummer(k.Eli) is not null).ToList();
        // [Ny, tørrkjøringen 2026-11-02] Sentrale forskrifter (SF) og lover (NL) holdes utenfor: Lovdata vedlikeholder statusen for dem, så
        // står de som gjeldende, er en «oppheving» i en annen kilde delvis. Alle tre SF-treffene i den delte basen var delvise:
        // FOR-1985-06-28-1679 (kapittel 6), FOR-1954-07-08-3 (kapitel VII) og FOR-1992-12-04-915 («… om sosiale tjenester mv. kapittel 5»,
        // som ingen tekstform fanger). Juristens premiss for R8 er at LTI-statusen ikke vedlikeholdes.
        var ider = medDato.Select(k => k.Id).ToList();
        var dokumentIder = await db.Rettskilder.AsNoTracking().Where(r => ider.Contains(r.Id))
            .Select(r => new { r.Id, r.DokumentId }).ToDictionaryAsync(r => r.Id, r => r.DokumentId, ct);
        medDato = medDato.Where(k => dokumentIder.GetValueOrDefault(k.Id) is not { } d
            || !(d.StartsWith("SF/", StringComparison.Ordinal) || d.StartsWith("NL/", StringComparison.Ordinal))).ToList();
        var noder = await NoderSomEndrerAsync(db, medDato.Select(k => k.Eli).Distinct(StringComparer.Ordinal).ToList(), bareLover: false, ct);
        var perMal = noder.GroupBy(x => x.MalEli, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(x => x.Node).ToList(), StringComparer.Ordinal);
        foreach (var (id, eli) in medDato)
        {
            var (_, dato, nr) = DatoOgNummer(eli)!.Value;
            foreach (var n in (perMal.GetValueOrDefault(eli) ?? []).OrderBy(n => n.DokumentId, StringComparer.Ordinal).ThenBy(n => n.Rekkefolge))
            {
                if (n.RettskildeId == id) continue;
                if (FullOpphevingssetning(n.Tekst, dato, nr) is { } s)
                {
                    ut[id] = new Oppheving(n.RettskildeId, n.DokumentId, n.Eli, n.Tittel, n.Eid, s);
                    break;
                }
            }
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
            if (DatoOgNummer(malEli) is not { } x) continue;
            foreach (var n in perKilde.GetValueOrDefault(kilde) ?? [])
            {
                if (FullOpphevingssetning(n.Tekst, x.Dato, x.Nr) is { } s)
                {
                    ut[(kilde, malEli)] = new Oppheving(n.RettskildeId, n.DokumentId, n.Eli, n.Tittel, n.Eid, s);
                    break;
                }
            }
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

        var opphevet = new Dictionary<string, (Oppheving O, Oppheving? V)>(StringComparer.Ordinal);
        foreach (var eli in kandidater)
        {
            if (DatoOgNummer(eli) is not { } x || !perMal.TryGetValue(eli, out var noder)) continue;
            var o = noder.Select(n => (n, s: FullOpphevingssetning(n.Tekst, x.Dato, x.Nr))).FirstOrDefault(p => p.s is not null);
            if (o.s is null) continue;
            var v = noder.Where(n => n.RettskildeId == o.n.RettskildeId)
                .Select(n => (n, s: Videreforingssetning(n.Tekst, x.Dato, x.Nr))).FirstOrDefault(p => p.s is not null);
            opphevet[eli] = (new Oppheving(o.n.RettskildeId, o.n.DokumentId, o.n.Eli, o.n.Tittel, o.n.Eid, o.s),
                v.s is null ? null : new Oppheving(v.n.RettskildeId, v.n.DokumentId, v.n.Eli, v.n.Tittel, v.n.Eid, v.s));
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
