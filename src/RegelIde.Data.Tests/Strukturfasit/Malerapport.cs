using System.Globalization;
using System.Text;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Lager målerapporten (markdown) som skrives både til
/// testutskriften og til <c>data/fasit/strukturmodell/maling-monster.md</c>.
/// <para>
/// Rapporten er deterministisk — ingen tidsstempel, faste sorteringer — slik at fila bare endrer seg
/// i git når mønstrene eller fasiten faktisk endrer seg. En diff i den er dermed selv en måling.
/// </para>
/// </summary>
internal static class Malerapport
{
    private static readonly CultureInfo Norsk = CultureInfo.GetCultureInfo("nb-NO");
    private const int AntallEksempler = 20;

    public static string Lag(IReadOnlyList<Kildemaling> malinger, IReadOnlyList<(string Id, string Grunn)> forkastede)
    {
        var sb = new StringBuilder();
        var monstre = MonsterStrukturkonverterer.Monstre;

        sb.AppendLine("# Måling: mønsterkonvertering mot strukturfasiten");
        sb.AppendLine();
        sb.AppendLine("Generert av `MonsterStrukturkonvertererMalingTests` (`src/RegelIde.Data.Tests/Strukturfasit/`) — **ikke rediger for hånd**.");
        sb.AppendLine("Kjør `dotnet test src/RegelIde.Data.Tests --filter \"FullyQualifiedName~Strukturfasit\"` for å regenerere. Sak: #307, designgrunnlag: `docs/33` §5.");
        sb.AppendLine();
        sb.AppendLine("**Forbehold:** fasiten er KI-annotert og ikke menneskelig verifisert (`docs/33` §2). Tallene er bare så gode som fasiten,");
        sb.AppendLine("og tersklene i testen er regresjonsvern, ikke kvalitetskrav — de låses først etter gjennomgangen i #309.");
        sb.AppendLine();
        sb.AppendLine("**Treffregel:** samme eId + kategori + type, og for hvert endepunkt fasiten har (fra/til): samme tekstform uten skille på store/små");
        sb.AppendLine("bokstaver, der «tekstform» er aktørens tekstform ∪ varianter (minst én felles skrivemåte). Én-til-én. «Uten endepunktkrav» =");
        sb.AppendLine("bare eId + kategori + type — forskjellen mellom de to viser hvor mye av feilen som er feil/manglende aktør, ikke feil gjenkjenning.");
        sb.AppendLine();

        var alle = Tall.For(malinger, _ => true);
        sb.AppendLine("## Totalt");
        sb.AppendLine();
        sb.AppendLine($"{alle.Fasit} fasitutsagn, {alle.Predikert} predikerte, {alle.Treff} treff → presisjon **{P(alle.Presisjon)}**, gjenfinning **{P(alle.Gjenfinning)}**");
        sb.AppendLine($"(uten endepunktkrav: presisjon {P(alle.PresisjonUtenEndepunkt)}, gjenfinning {P(alle.GjenfinningUtenEndepunkt)}).");
        sb.AppendLine();

        sb.AppendLine("## Per kategori (kanttype, `docs/33` §4.3)");
        sb.AppendLine();
        sb.AppendLine("Bokstaven følger `STD`-tabellen i `designtest.py`. `annet:*`-typer står i egen rad; `bistar`/`samarbeider_med` er bevisst senere lag (§4.4).");
        sb.AppendLine();
        Tabellhode(sb, "Kategori");
        foreach (var b in Strukturmaling.Bokstavrekkefolge)
        {
            Tabellrad(sb, b, Tall.For(malinger, r => r.Bokstav == b));
        }
        sb.AppendLine();

        sb.AppendLine("## Per kilde");
        sb.AppendLine();
        Tabellhode(sb, "Kilde");
        foreach (var m in malinger)
        {
            Tabellrad(sb, m.Kilde, Tall.For([m], _ => true));
        }
        sb.AppendLine();

        sb.AppendLine("## Per type mønsterlaget produserer");
        sb.AppendLine();
        Tabellhode(sb, "Kategori / type");
        foreach (var (kat, type) in monstre.Select(x => (x.Kategori, x.Type)).Distinct())
        {
            Tabellrad(sb, $"{Strukturmaling.Bokstav(kat, type)} {kat} / {type}", Tall.For(malinger, r => r.Utsagn.Kategori == kat && r.Utsagn.Type == type));
        }
        sb.AppendLine();

        sb.AppendLine("### Per kilde, K normgivningskompetanse — fasitens forskriftskompetanse før #341 (terskel ≥ 0,9 presisjon)");
        sb.AppendLine();
        Tabellhode(sb, "Kilde");
        foreach (var m in malinger)
        {
            Tabellrad(sb, m.Kilde, Tall.For([m], r => r.Utsagn.Type == "normgivningskompetanse"));
        }
        sb.AppendLine();

        sb.AppendLine("## Per mønster");
        sb.AppendLine();
        sb.AppendLine("«Gjenkjent» = andelen av mønsterets utsagn der fasiten har et utsagn med samme eId + kategori + type (uansett aktør, ikke én-til-én).");
        sb.AppendLine("Stor avstand mellom presisjon og gjenkjent betyr at mønsteret finner riktig utsagn, men feil eller manglende aktør.");
        sb.AppendLine();
        sb.AppendLine("| Mønster | Type | Predikert | Treff | Presisjon | Gjenkjent | Korpusgrunnlag (`docs/33` §1) |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---|");
        var fasitgrupper = malinger.SelectMany(m => m.Fasit).Select(f => (f.Kilde, f.Gruppe)).ToHashSet();
        foreach (var mo in monstre)
        {
            var kildeId = mo.Oppdagelseskilde;
            var rader = malinger.SelectMany(m => m.Predikert).Where(r => r.Monster == kildeId).ToList();
            var treff = malinger.Sum(m => m.Treff.Count(t => t.Predikert.Monster == kildeId));
            var gjenkjent = rader.Count(r => fasitgrupper.Contains((r.Kilde, r.Gruppe)));
            double? Andel(int n) => rader.Count == 0 ? null : (double)n / rader.Count;
            sb.AppendLine($"| `{mo.Id}` | {mo.Type} | {rader.Count} | {treff} | {P(Andel(treff))} | {P(Andel(gjenkjent))} | {Celle(mo.Korpusgrunnlag)} |");
        }
        sb.AppendLine();

        FalskePositive(sb, malinger);
        FalskeNegative(sb, malinger);

        sb.AppendLine("## Forkastede mønstre");
        sb.AppendLine();
        if (forkastede.Count == 0)
        {
            sb.AppendLine("Ingen.");
        }
        foreach (var (id, grunn) in forkastede)
        {
            sb.AppendLine($"- `{id}` — {grunn}");
        }
        return sb.ToString();
    }

    private static void FalskePositive(StringBuilder sb, IReadOnlyList<Kildemaling> malinger)
    {
        var fp = malinger.SelectMany(m => m.FalskePositive).ToList();
        sb.AppendLine($"## Falske positive ({fp.Count})");
        sb.AppendLine();
        sb.AppendLine("Gruppert på mønster og om eId+kategori+type fantes i fasiten (= feil/manglende aktør) eller ikke (= feil gjenkjenning).");
        sb.AppendLine();
        var fasitGrupper = malinger.SelectMany(m => m.Fasit).Select(f => (f.Kilde, f.Gruppe)).ToHashSet();
        string Arsak(Rad r) => fasitGrupper.Contains((r.Kilde, r.Gruppe)) ? "feil/manglende aktør" : "ikke i fasiten";
        sb.AppendLine("| Mønster | Årsak | Antall |");
        sb.AppendLine("|---|---|---:|");
        var grupper = fp.GroupBy(r => (r.Monster, Arsak: Arsak(r)))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Monster, StringComparer.Ordinal).ThenBy(g => g.Key.Arsak, StringComparer.Ordinal)
            .ToList();
        foreach (var g in grupper) sb.AppendLine($"| `{g.Key.Monster}` | {g.Key.Arsak} | {g.Count()} |");
        sb.AppendLine();
        sb.AppendLine($"De {AntallEksempler} vanligste (én fra hver gruppe i tur, vanligste gruppe først):");
        sb.AppendLine();
        sb.AppendLine("| # | Kilde | eId | Mønster | fra → til | Sitat |");
        sb.AppendLine("|---|---|---|---|---|---|");
        var i = 0;
        foreach (var r in RoundRobin(grupper.Select(g => g.ToList()).ToList()).Take(AntallEksempler))
        {
            sb.AppendLine($"| {++i} | {r.Kilde} | {Celle(KortEid(r.Utsagn.Eid))} | `{r.Monster}` | {Celle(Aktorer(r))} | {Celle(r.Utsagn.Sitat)} |");
        }
        sb.AppendLine();
    }

    private static void FalskeNegative(StringBuilder sb, IReadOnlyList<Kildemaling> malinger)
    {
        var fn = malinger.SelectMany(m => m.FalskeNegative).ToList();
        sb.AppendLine($"## Falske negative ({fn.Count})");
        sb.AppendLine();
        sb.AppendLine("Gruppert på kategori/type. Typer mønsterlaget ikke har mønster for, er forventet her — de er KI-lagets (#308) nevner.");
        sb.AppendLine();
        var produsert = MonsterStrukturkonverterer.Monstre.Select(m => (m.Kategori, m.Type)).ToHashSet();
        sb.AppendLine("| Kategori / type | Har mønster | Antall |");
        sb.AppendLine("|---|---|---:|");
        var grupper = fn.GroupBy(r => (r.Utsagn.Kategori, r.Utsagn.Type))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Kategori, StringComparer.Ordinal).ThenBy(g => g.Key.Type, StringComparer.Ordinal)
            .ToList();
        foreach (var g in grupper.Take(40))
        {
            sb.AppendLine($"| {Strukturmaling.Bokstav(g.Key.Kategori, g.Key.Type)} {g.Key.Kategori} / {g.Key.Type} | {(produsert.Contains(g.Key) ? "ja" : "nei")} | {g.Count()} |");
        }
        if (grupper.Count > 40)
        {
            sb.AppendLine($"| … {grupper.Count - 40} typer til (alle `annet:*` eller ≤ {grupper[40].Count()} utsagn) | | {grupper.Skip(40).Sum(g => g.Count())} |");
        }
        sb.AppendLine();
        // Eksemplene er hentet fra typene mønsterlaget HAR mønster for — det er der en falsk negativ sier
        // noe om mønstrene. De øvrige typene er talt i tabellen over.
        var medMonster = grupper.Where(g => produsert.Contains(g.Key)).Select(g => g.ToList()).ToList();
        sb.AppendLine($"De {AntallEksempler} vanligste blant typene med mønster (én fra hver type i tur, vanligste type først):");
        sb.AppendLine();
        sb.AppendLine("| # | Kilde | eId | Type | fra → til (fasit) | Sitat |");
        sb.AppendLine("|---|---|---|---|---|---|");
        var i = 0;
        foreach (var r in RoundRobin(medMonster).Take(AntallEksempler))
        {
            sb.AppendLine($"| {++i} | {r.Kilde} | {Celle(KortEid(r.Utsagn.Eid))} | {r.Utsagn.Type} | {Celle(Aktorer(r))} | {Celle(r.Utsagn.Sitat)} |");
        }
        sb.AppendLine();
    }

    private static IEnumerable<Rad> RoundRobin(IReadOnlyList<List<Rad>> grupper)
    {
        for (var runde = 0; ; runde++)
        {
            var noe = false;
            foreach (var g in grupper)
            {
                if (runde >= g.Count) continue;
                noe = true;
                yield return g[runde];
            }
            if (!noe) yield break;
        }
    }

    private static void Tabellhode(StringBuilder sb, string forste)
    {
        sb.AppendLine($"| {forste} | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
    }

    private static void Tabellrad(StringBuilder sb, string navn, Tall t) =>
        sb.AppendLine($"| {navn} | {t.Fasit} | {t.Predikert} | {t.Treff} | {P(t.Presisjon)} | {P(t.Gjenfinning)} | {P(t.PresisjonUtenEndepunkt)} | {P(t.GjenfinningUtenEndepunkt)} |");

    /// <summary>Prosent med én desimal og norsk desimalkomma; «–» når udefinert.</summary>
    public static string P(double? verdi) => verdi is null ? "–" : (verdi.Value * 100).ToString("0.0", Norsk) + " %";

    private static string Aktorer(Rad r) => $"{r.Fra?.Tekstform ?? "null"} → {r.Til?.Tekstform ?? "null"}";

    private static string KortEid(string eid) => eid.Replace("https://lovdata.no/eli/", "", StringComparison.Ordinal);

    private static string Celle(string tekst)
    {
        var t = tekst.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        return t.Length > 160 ? t[..160] + " …" : t;
    }
}
