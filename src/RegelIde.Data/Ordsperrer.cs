using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #463, Johanns avgjørelse 2026-11-05 (beslutningsnotatet «Norge, staten og forvaltningsnivåene», modell B), klarsignal
/// 2026-11-17] <b>Ordsperren</b> for en navneform som også er et vanlig ord: kontekstene der ordet i lovteksten IKKE betyr aktøren. Bare
/// «staten» har en sperre i dag. Navneformen er gjeldende for aktøren staten, men ordet står også om en annen stat («den anmodede staten»,
/// «EØS-staten») og i konvensjonstekst. Uten sperren blir disse treffene tagget og koblet til den norske staten.
/// <para>
/// Sperren gjelder IKKE (ordet betyr aktøren) — og alt annet gjelder:
/// <list type="bullet">
/// <item><b>Bindestrek rett foran</b> («EØS-staten», «medlemsstaten» skrives sammen, men «EØS-staten» har bindestrek): ordet er siste ledd i et
/// sammensatt ord, ikke ordet «staten». <see cref="AktoridentitetTjeneste"/> regner nå ikke bindestrek rett foran som ordgrense, så disse
/// finnes ikke som treff; grunnen står her også, så målingen kan vise hvor mange det var med den gamle ordgrensen.</item>
/// <item><b>«den (adj.) staten»</b>: «den» og et adjektiv i svak (bestemt) form, som ender på -e («den andre staten», «den anmodede staten»),
/// eventuelt med ett ord foran («den særlig berørte staten»), eller ingen («den staten der …»). «den utstrekning denne staten» er ikke med. Unntak: <b>«den norske staten»</b> er aktøren og sperres ikke. Kjent pris:
/// et substantiv på -e mellom «den» og «staten» sperrer også («den rente staten oppnår», 1 forekomst målt 2026-11-17).</item>
/// <item><b>«staten der / hvor / hvis»</b>: ordet innleder en relativsetning som sier hvilken stat («staten der vedkommende er bosatt»).</item>
/// <item><b>Spenn med konvensjonstekst</b>: noden står under en overskrift som er en konvensjon, overenskomst, avtale, protokoll eller
/// traktat (eller et EØS-vedlegg), eller rettskilden selv er det (<see cref="ErKonvensjonsoverskrift"/>). Der er «staten» en part.</item>
/// </list>
/// «denne staten» er IKKE med; notatet nevner det ikke (målingen viser antallet, så Johann kan avgjøre det).
/// </para>
/// <para>
/// <b>Kode, ikke data</b> (samme valg som standardreglene i #394 og forleddssperren i #440): lista er lukket, og en ny sperre er et vedtak.
/// </para>
/// </summary>
public static class Ordsperrer
{
    /// <summary>Navneformen som har en ordsperre i dag.</summary>
    public const string Staten = "staten";

    public const string BindestrekForan = "bindestrek_foran";
    public const string DenAdjektiv = "den_adjektiv";
    public const string DerHvorHvis = "der_hvor_hvis";
    public const string Konvensjonstekst = "konvensjonstekst";

    /// <summary>Grunnene, i den rekkefølgen de prøves (en forekomst telles under den første som slår til).</summary>
    public static readonly string[] Grunner = [BindestrekForan, DenAdjektiv, DerHvorHvis, Konvensjonstekst];

    public static string Tekst(string grunn) => grunn switch
    {
        BindestrekForan => "bindestrek rett foran («EØS-staten»)",
        DenAdjektiv => "«den (adj.) staten» (ikke «den norske staten»)",
        DerHvorHvis => "«staten der/hvor/hvis»",
        Konvensjonstekst => "konvensjonstekst",
        _ => grunn,
    };

    /// <summary>Om navneformen (eller registernavnet) har en ordsperre.</summary>
    public static bool HarSperre(string? term) => term is not null && string.Equals(term.Trim(), Staten, StringComparison.OrdinalIgnoreCase);

    private static readonly Regex DenForan = new(@"(?:^|[^\p{L}\p{N}])den((?:\s+\p{Ll}+)?\s+\p{Ll}+e)?\s+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DerHvorHvisEtter = new(@"^\s+(der|hvor|hvis)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Grunnen til at forekomsten [<paramref name="start"/>, <paramref name="slutt"/>) av <paramref name="term"/> i <paramref name="tekst"/> er
    /// sperret, eller null når den gjelder (eller termen ikke har noen sperre). <paramref name="iKonvensjonstekst"/>: noden står i et spenn med
    /// konvensjonstekst (<see cref="KonvensjonsnoderAsync"/>).
    /// </summary>
    public static string? Vurder(string term, string tekst, int start, int slutt, bool iKonvensjonstekst)
    {
        if (!HarSperre(term) || start < 0 || slutt > tekst.Length || slutt <= start) return null;
        if (start > 0 && tekst[start - 1] == '-') return BindestrekForan;
        var foran = tekst[..start];
        var m = DenForan.Match(foran);
        if (m.Success)
        {
            var ord = m.Groups[1].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (!ord.Any(o => string.Equals(o, "norske", StringComparison.OrdinalIgnoreCase))
                && !(ord.Length > 0 && string.Equals(ord[^1], "denne", StringComparison.OrdinalIgnoreCase))) return DenAdjektiv;
        }
        if (DerHvorHvisEtter.IsMatch(tekst[slutt..])) return DerHvorHvis;
        if (iKonvensjonstekst) return Konvensjonstekst;
        return null;
    }

    /// <summary>
    /// En overskrift (eller rettskildetittel) som ER en konvensjonstekst: den begynner (etter et valgfritt «Vedlegg N.», et årstall og «Den»
    /// eller «Nordisk/Europeisk/Internasjonal») med konvensjon, overenskomst, avtale, traktat, pakt eller protokoll fulgt av om/av/mellom/til
    /// («Konvensjon av 25 oktober 1980 om …», «Avtale mellom Kongeriket Norges regjering og …»), med et navn på «-konvensjonen»
    /// («Hongkong-konvensjonen»), eller med «EØS-avtalen vedlegg/protokoll». En overskrift som bare handler OM avtaler («Avtalebrudd»,
    /// «Tariffavtaler», «Overenskomst med fremmed stat», «Sertifikat etter 1992 ansvarskonvensjonen») er ikke konvensjonstekst.
    /// </summary>
    public static bool ErKonvensjonsoverskrift(string? overskrift) =>
        !string.IsNullOrWhiteSpace(overskrift) && Konvensjonsoverskrift.IsMatch(overskrift.Trim());

    private static readonly Regex Konvensjonsoverskrift = new(
        @"^(vedlegg\s+[\p{L}\p{N}.]+\.?\s*)?(\d{4}[\s-]*)?(den\s+)?((nordisk|europeisk|internasjonal|internasjonale)\s+)?"
        + @"((konvensjon|overenskomst|avtale|traktat|pakt|protokoll)(en)?\s+(om|av|mellom|mellem|til)(?![\p{L}\p{N}])"
        + @"|[\p{L}\p{N}-]+-konvensjonen(?![\p{L}\p{N}])"
        + @"|eøs-avtalen\s+(vedlegg|protokoll))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Nodene blant <paramref name="nodeIder"/> som står i et spenn med konvensjonstekst: en av overskriftene fra noden selv og opp til roten er
    /// en konvensjonsoverskrift, eller rettskildens tittel er det. Foreldrene lastes nivå for nivå (høyst 20), så bare de berørte stiene leses.
    /// </summary>
    public static async Task<HashSet<Guid>> KonvensjonsnoderAsync(RegelIdeDbContext db, IReadOnlyCollection<Guid> nodeIder, CancellationToken ct)
    {
        var svar = new HashSet<Guid>();
        if (nodeIder.Count == 0) return svar;
        var noder = new Dictionary<Guid, (Guid? Forelder, string? Overskrift, Guid RettskildeId)>();
        var mangler = nodeIder.Distinct().ToList();
        for (var niva = 0; niva < 20 && mangler.Count > 0; niva++)
        {
            foreach (var del in mangler.Chunk(5000))
            {
                var lest = await db.RettskildeNoder.Where(n => del.Contains(n.Id))
                    .Select(n => new { n.Id, n.ParentNodeId, n.Overskrift, n.RettskildeId }).ToListAsync(ct);
                foreach (var n in lest) noder[n.Id] = (n.ParentNodeId, n.Overskrift, n.RettskildeId);
            }
            mangler = noder.Values.Where(v => v.Forelder is { } f && !noder.ContainsKey(f)).Select(v => v.Forelder!.Value).Distinct().ToList();
        }
        var kildeIder = noder.Values.Select(v => v.RettskildeId).Distinct().ToList();
        var konvensjonskilder = (await db.Rettskilder.Where(r => kildeIder.Contains(r.Id)).Select(r => new { r.Id, r.Tittel }).ToListAsync(ct))
            .Where(r => ErKonvensjonsoverskrift(r.Tittel)).Select(r => r.Id).ToHashSet();
        foreach (var id in nodeIder.Distinct())
        {
            if (!noder.TryGetValue(id, out var n)) continue;
            if (konvensjonskilder.Contains(n.RettskildeId)) { svar.Add(id); continue; }
            Guid? gjeldende = id;
            for (var steg = 0; steg < 25 && gjeldende is { } g && noder.TryGetValue(g, out var x); steg++)
            {
                if (ErKonvensjonsoverskrift(x.Overskrift)) { svar.Add(id); break; }
                gjeldende = x.Forelder;
            }
        }
        return svar;
    }
}

/// <summary>[Ny, issue #463] Én grunn i <see cref="OrdsperreMaling"/>, med inntil fem eksempler (rettskilde, eId og utdrag).</summary>
public sealed record OrdsperreGrunn(string Grunn, string Tekst, int Forekomster, int Rettskilder, IReadOnlyList<OrdsperreEksempel> Eksempler);

public sealed record OrdsperreEksempel(string Rettskilde, string Eid, string Utdrag);

/// <summary>
/// [Ny, issue #463] Målingen av ordsperren over hele korpuset: alle hele-ord-forekomster av termen (uten skille på store og små bokstaver) i
/// gjeldende, ikke opphevede noder i gjeldende rettskilder — de samme nodene <see cref="AktoridentitetTjeneste"/> leser.
/// <see cref="ForekomsterGammelOrdgrense"/> er antallet med ordgrensen fra før #463 (bindestrek var ordgrense); <see cref="Forekomster"/> er
/// antallet med den nye. <see cref="Sperret"/> er per grunn (bindestrek telles bare med den gamle ordgrensen). <see cref="Gjelder"/> er det som
/// blir treff for aktøren. <see cref="DenneStaten"/> er forekomster av «denne staten» blant dem som gjelder (ikke sperret; opplysning).
/// </summary>
public sealed record OrdsperreMaling(
    string Term, int ForekomsterGammelOrdgrense, int Forekomster, int Noder, int Rettskilder, IReadOnlyList<OrdsperreGrunn> Sperret,
    int SperretTotalt, int Gjelder, int GjelderSmaBokstaver, int DenneStaten)
{
    public static async Task<OrdsperreMaling> MalAsync(RegelIdeDbContext db, string term, CancellationToken ct = default)
    {
        var lower = term.Trim().ToLower();
        var noder = await db.RettskildeNoder
            .Where(n => n.Entitetsstatus == "gjeldende" && !n.Opphevet && n.Tekst != null && n.Tekst.ToLower().Contains(lower))
            .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende"), n => n.RettskildeId, r => r.Id,
                (n, r) => new { n.Id, n.RettskildeId, Tittel = r.Kortnavn ?? r.Tittel, n.Eid, n.Tekst })
            .ToListAsync(ct);
        var gammel = new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term.Trim())}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var treff = noder.SelectMany(n => gammel.Matches(n.Tekst!).Select(m => (Node: n, m.Index, Slutt: m.Index + m.Length, m.Value))).ToList();
        var konvensjon = await Ordsperrer.KonvensjonsnoderAsync(db, treff.Select(t => t.Node.Id).Distinct().ToList(), ct);
        var vurdert = treff.Select(t => (t.Node, t.Index, t.Value,
            Grunn: Ordsperrer.Vurder(term, t.Node.Tekst!, t.Index, t.Slutt, konvensjon.Contains(t.Node.Id)))).ToList();
        var denne = new Regex(@"(?:^|[^\p{L}\p{N}])denne\s+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var gjelder = vurdert.Where(v => v.Grunn is null).ToList();
        var grunner = Ordsperrer.Grunner.Select(g =>
        {
            var rader = vurdert.Where(v => v.Grunn == g).ToList();
            return new OrdsperreGrunn(g, Ordsperrer.Tekst(g), rader.Count, rader.Select(r => r.Node.RettskildeId).Distinct().Count(),
                rader.Take(5).Select(r => new OrdsperreEksempel(r.Node.Tittel, r.Node.Eid,
                    r.Node.Tekst![Math.Max(0, r.Index - 50)..Math.Min(r.Node.Tekst!.Length, r.Index + r.Value.Length + 30)])).ToList());
        }).ToList();
        var bindestrek = grunner.First(g => g.Grunn == Ordsperrer.BindestrekForan).Forekomster;
        return new OrdsperreMaling(term.Trim(), treff.Count, treff.Count - bindestrek, treff.Select(t => t.Node.Id).Distinct().Count(),
            treff.Select(t => t.Node.RettskildeId).Distinct().Count(), grunner, vurdert.Count(v => v.Grunn is not null), gjelder.Count,
            gjelder.Count(v => v.Value == lower), gjelder.Count(v => denne.IsMatch(v.Node.Tekst![..v.Index])));
    }
}
