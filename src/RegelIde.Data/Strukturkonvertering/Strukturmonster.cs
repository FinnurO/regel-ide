using System.Text.RegularExpressions;

namespace RegelIde.Data.Strukturkonvertering;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Ett navngitt, deterministisk mønster i
/// <see cref="MonsterStrukturkonverterer"/>.
/// <para>
/// <see cref="Id"/> er STABIL: den blir <c>OppdagelsesKilde = "monster:&lt;id&gt;"</c> når resultatet
/// lagres som forslag (#313, docs/33 §4.3), og målerapporten regner presisjon per id. Endres
/// betydningen av et mønster vesentlig, skal det få ny id — ellers blander proveniensen gamle og nye
/// forslag under samme navn.
/// </para>
/// </summary>
/// <param name="Id">Stabil id, kebab-case.</param>
/// <param name="Kategori">FORMAT.md-kategori utsagnet får («kompetanse», «relasjon» …).</param>
/// <param name="Type">FORMAT.md-type («forskriftskompetanse», «klageinstans_for» …).</param>
/// <param name="Beskrivelse">Hvilke formuleringer mønsteret fanger, med eksempel.</param>
/// <param name="Korpusgrunnlag">Hvorfor mønsteret er med: målt korpuspresisjon fra docs/33 §1, eller at den ikke er målt der.</param>
/// <param name="Finn">Finner utsagn i én setning. Får setningen, returnerer null eller flere funn.</param>
public sealed record Strukturmonster(
    string Id,
    string Kategori,
    string Type,
    string Beskrivelse,
    string Korpusgrunnlag,
    Func<string, IReadOnlyList<Monsterfunn>> Finn)
{
    /// <summary>Proveniensverdien som skrives på utsagnet: <c>monster:&lt;id&gt;</c>.</summary>
    public string Oppdagelseskilde => "monster:" + Id;
}

/// <summary>
/// Ett funn i en setning. <see cref="Fra"/>/<see cref="Til"/> er aktørenes TEKSTFORM; tom liste betyr at
/// setningen ikke avgjør endepunktet (blir <c>null</c> i utsagnet). Flere elementer = sideordnede aktører
/// («Reguleringsmyndigheten og klagenemnda»), som gir ett utsagn per kombinasjon.
/// </summary>
public sealed record Monsterfunn(
    string Sitat,
    IReadOnlyList<string> Fra,
    IReadOnlyList<string> Til,
    string? Objekt,
    string Polaritet);

/// <summary>
/// Felles maskineri for mønstre som er én eller flere regulære uttrykk med navngitte grupper:
/// <c>fra</c>/<c>til</c> (aktørfrase), <c>fragen</c>/<c>tilgen</c> (ett ord i genitiv, «Kongens»),
/// og <c>obj</c> (start på kompetansens objekt — resten av setningen fra dit).
/// </summary>
internal sealed record RegexMonsteroppsett(
    bool KreverFra = false,
    bool KreverTil = false,
    string Polaritet = "positiv",
    bool ObjektForan = false,
    bool AlleUttrykk = false);

internal static class RegexMonster
{
    private const int MaksSitatOrd = 40;
    private const int MaksObjektTegn = 200;

    /// <summary>
    /// Kjører uttrykkene i rekkefølge mot setningen. Som standard vinner det FØRSTE uttrykket som gir minst
    /// ett godtatt funn — senere uttrykk er da reserveformer (f.eks. «X kan delegere» uten «til Y» etter
    /// «X kan delegere … til Y»), og skal ikke gi et nytt, dårligere utsagn ved siden av det første.
    /// Med <see cref="RegexMonsteroppsett.AlleUttrykk"/> er uttrykkene i stedet uavhengige ledd i samme
    /// setning (hovedsetning + «at X kan gi forskrift» i bisetningen), og alle funn tas med, ett per
    /// distinkt fra/til.
    /// </summary>
    public static IReadOnlyList<Monsterfunn> Finn(string setning, RegexMonsteroppsett oppsett, IReadOnlyList<Regex> uttrykk)
    {
        var samlet = new List<Monsterfunn>();
        var sett = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rx in uttrykk)
        {
            var funn = new List<Monsterfunn>();
            foreach (Match m in rx.Matches(setning))
            {
                var fra = Endepunkt(m, "fra", "fragen");
                var til = Endepunkt(m, "til", "tilgen");
                if (oppsett.KreverFra && fra.Count == 0) continue;
                if (oppsett.KreverTil && til.Count == 0) continue;

                string? objekt = null;
                var obj = m.Groups["obj"];
                if (obj.Success)
                {
                    objekt = Objekt(setning[obj.Index..]);
                }
                else if (oppsett.ObjektForan && m.Index > 0)
                {
                    objekt = Objekt(setning[..m.Index]);
                }

                funn.Add(new Monsterfunn(Sitat(setning), fra, til, objekt, oppsett.Polaritet));
            }
            if (!oppsett.AlleUttrykk)
            {
                if (funn.Count > 0) return funn;
                continue;
            }
            foreach (var f in funn)
            {
                if (sett.Add(string.Join('|', f.Fra) + "→" + string.Join('|', f.Til))) samlet.Add(f);
            }
        }
        return samlet;
    }

    private static IReadOnlyList<string> Endepunkt(Match m, string gruppe, string genitivgruppe)
    {
        var g = m.Groups[gruppe];
        if (g.Success) return Aktorfrase.Tolk(g.Value);
        var gen = m.Groups[genitivgruppe];
        if (gen.Success && Aktorfrase.UtenGenitiv(gen.Value) is { } utenGenitiv) return Aktorfrase.Tolk(utenGenitiv);
        return [];
    }

    /// <summary>
    /// Sitatet er hele setningen, avkortet til de første <see cref="MaksSitatOrd"/> ordene. Et prefiks av
    /// setningen er fortsatt en eksakt delstreng av nodeteksten, som FORMAT.md krever.
    /// </summary>
    public static string Sitat(string setning)
    {
        var s = setning.Trim();
        var ord = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsWhiteSpace(s[i]) && i > 0 && !char.IsWhiteSpace(s[i - 1]) && ++ord == MaksSitatOrd)
            {
                return s[..i];
            }
        }
        return s;
    }

    private static string? Objekt(string tekst)
    {
        var t = tekst.Trim().TrimEnd('.', ',', ';', ':', ' ');
        if (t.Length == 0) return null;
        if (t.Length <= MaksObjektTegn) return t;
        var kutt = t.LastIndexOf(' ', MaksObjektTegn);
        return t[..(kutt > 0 ? kutt : MaksObjektTegn)];
    }
}
