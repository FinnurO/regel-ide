using System.Text;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #308 strukturmodell-KI, 2026-10-07] Rapporten <c>data/fasit/strukturmodell/maling-ki.md</c>: KI-laget
/// side om side med mønsterlaget og unionen, per kategori, per kilde og per type, pluss kastede rader per
/// årsak, kall og tokens. Bygget av lagret KI-utdata (<see cref="KiMaling"/>) og en fersk kjøring av
/// mønsterlaget, med samme treffregel som <see cref="Malerapport"/> (<see cref="Strukturmaling"/>).
/// Deterministisk gitt de lagrede utdataene — ingen klokke utover kjøringens eget tidspunkt.
/// </summary>
internal static class KiMalerapport
{
    private const int AntallEksempler = 20;

    /// <summary>Én kilde med de tre målingene.</summary>
    internal sealed record Kilde(string Navn, Kildemaling Monster, Kildemaling Ki, Kildemaling Union, KiKjoringKilde Kjoring);

    public static IReadOnlyList<Kilde> Mal(IReadOnlyList<Fasitkilde> fasit, KiKjoring kjoring)
    {
        var monster = new MonsterStrukturkonverterer();
        return fasit.Select(f =>
        {
            var m = monster.Konverter(f.Grunnlag);
            var k = KiMaling.LesDokument(f.Navn);
            return new Kilde(f.Navn,
                Strukturmaling.Mal(f.Navn, f.Fasit, m),
                Strukturmaling.Mal(f.Navn, f.Fasit, k),
                Strukturmaling.Mal(f.Navn, f.Fasit, KiMaling.Union(m, k)),
                kjoring.Kilder.Single(x => x.Kilde == f.Navn));
        }).ToList();
    }

    public static string Lag(IReadOnlyList<Kilde> kilder, KiKjoring kjoring)
    {
        var sb = new StringBuilder();
        var m = kilder.Select(k => k.Monster).ToList();
        var ki = kilder.Select(k => k.Ki).ToList();
        var u = kilder.Select(k => k.Union).ToList();

        sb.AppendLine("# Måling: KI-konvertering mot strukturfasiten, side om side med mønsterlaget");
        sb.AppendLine();
        sb.AppendLine("Generert av `KiStrukturkonvertererLiveMalingTests` (live) og `KiMalingRapportTests` (regenererer fra lagret utdata)");
        sb.AppendLine("i `src/RegelIde.Data.Tests/Strukturfasit/` — **ikke rediger for hånd**. Sak: #308, designgrunnlag: `docs/33` §5.");
        sb.AppendLine("KI-utdataene fra kjøringen ligger i `ki-utdata/` (fasit-formatet per kilde + `kjoring.json`).");
        sb.AppendLine();
        sb.AppendLine($"- **Modell:** `{kjoring.Modell}` (`oppdagelseskilde = \"ki:{kjoring.Modell}\"`)");
        sb.AppendLine($"- **Kjørt:** {kjoring.Tidspunkt}. **Antall live-kjøringer av målingen hittil:** {kjoring.Kjoringsnummer}.");
        sb.AppendLine($"- **Systeminstruks:** `KiStrukturkonverterer.SystemInstruks`, avtrykk `{kjoring.Instruksavtrykk}` (SHA-256, 12 tegn). Ikke iterert mot fasiten.");
        sb.AppendLine($"- **Oppdeling:** maks {kjoring.Valg.MaksTegnPerKall} tegn nodetekst per kall, {kjoring.Valg.MaksParallelleKall} kall samtidig, halvering ved ugyldig JSON inntil {kjoring.Valg.MaksDelingsdybde} ganger.");
        sb.AppendLine();
        sb.AppendLine("**Forbehold:** fasiten er KI-annotert (Claude) og ikke menneskelig verifisert (`docs/33` §2, #309). En KI som annoterer");
        sb.AppendLine("som fasit-annotatøren får fordel av det; tallene er bare så gode som fasiten. Én kjøring — KI-svar varierer mellom kjøringer.");
        sb.AppendLine();
        sb.AppendLine("**Treffregel** (som `maling-monster.md`): samme eId + kategori + type, og for hvert endepunkt fasiten har: minst én felles");
        sb.AppendLine("skrivemåte (tekstform ∪ varianter, uten skille på store/små). Én-til-én. **Union** = alle mønsterutsagn + KI-utsagn som ikke har");
        sb.AppendLine("samme eId + kategori + type + fra/til-tekstform som et mønsterutsagn. **Bare mønster / bare KI** = fasitutsagn bare det ene laget traff.");
        sb.AppendLine();

        sb.AppendLine("## Totalt");
        sb.AppendLine();
        Hode(sb, "Lag");
        var fasitAlle = Tall.For(m, _ => true).Fasit;
        foreach (var (navn, mal) in new[] { ("Mønster", m), ("KI", ki), ("Mønster ∪ KI", u) })
        {
            var t = Tall.For(mal, _ => true);
            sb.AppendLine($"| {navn} | {t.Fasit} | {t.Predikert} | {t.Treff} | {P(t.Presisjon)} | {P(t.Gjenfinning)} | {P(t.PresisjonUtenEndepunkt)} | {P(t.GjenfinningUtenEndepunkt)} |");
        }
        var (bareM, bareK) = Bare(kilder, _ => true);
        sb.AppendLine();
        sb.AppendLine($"Av {fasitAlle} fasitutsagn traff bare mønsterlaget {bareM}, bare KI-laget {bareK}.");
        sb.AppendLine();

        sb.AppendLine("## Per kategori (kanttype, `docs/33` §4.3)");
        sb.AppendLine();
        sb.AppendLine("P = presisjon, G = gjenfinning (med endepunktkrav).");
        sb.AppendLine();
        SammenligningHode(sb, "Kategori");
        foreach (var b in Strukturmaling.Bokstavrekkefolge)
        {
            Sammenligning(sb, b, kilder, r => r.Bokstav == b);
        }
        Sammenligning(sb, "**Alle**", kilder, _ => true);
        sb.AppendLine();

        sb.AppendLine("### KI uten endepunktkrav (bare eId + kategori + type)");
        sb.AppendLine();
        sb.AppendLine("Avstanden til tabellen over er utsagn KI-en gjenkjente, men med feil eller manglende aktør.");
        sb.AppendLine();
        sb.AppendLine("| Kategori | KI P u/endepunkt | KI G u/endepunkt | Mønster P u/endepunkt | Mønster G u/endepunkt |");
        sb.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var b in Strukturmaling.Bokstavrekkefolge)
        {
            var tk = Tall.For(ki, r => r.Bokstav == b);
            var tm = Tall.For(m, r => r.Bokstav == b);
            sb.AppendLine($"| {b} | {P(tk.PresisjonUtenEndepunkt)} | {P(tk.GjenfinningUtenEndepunkt)} | {P(tm.PresisjonUtenEndepunkt)} | {P(tm.GjenfinningUtenEndepunkt)} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Per kilde");
        sb.AppendLine();
        SammenligningHode(sb, "Kilde");
        foreach (var k in kilder)
        {
            Sammenligning(sb, k.Navn, [k], _ => true);
        }
        sb.AppendLine();

        sb.AppendLine("## Per type");
        sb.AppendLine();
        sb.AppendLine("Alle typer fra FORMAT.md-listene som står i fasiten eller som et av lagene predikerte. `annet:*` er samlet i kategoritabellen.");
        sb.AppendLine();
        SammenligningHode(sb, "Kategori / type");
        var typer = kilder.SelectMany(k => k.Union.Fasit.Concat(k.Union.Predikert))
            .Select(r => (r.Utsagn.Kategori, r.Utsagn.Type))
            .Where(x => !x.Kategori.StartsWith("annet:", StringComparison.Ordinal) && !x.Type.StartsWith("annet:", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(x => Array.IndexOf(Strukturmaling.Bokstavrekkefolge, Strukturmaling.Bokstav(x.Kategori, x.Type)))
            .ThenByDescending(x => kilder.Sum(k => k.Union.Fasit.Count(r => r.Utsagn.Kategori == x.Kategori && r.Utsagn.Type == x.Type)))
            .ThenBy(x => x.Type, StringComparer.Ordinal)
            .ToList();
        foreach (var (kat, type) in typer)
        {
            Sammenligning(sb, $"{Strukturmaling.Bokstav(kat, type)} {kat} / {type}", kilder, r => r.Utsagn.Kategori == kat && r.Utsagn.Type == type);
        }
        sb.AppendLine();

        Kall(sb, kilder, kjoring);
        Kastet(sb, kilder);
        FalskePositive(sb, ki);
        FalskeNegative(sb, kilder);
        return sb.ToString();
    }

    private static void Kall(StringBuilder sb, IReadOnlyList<Kilde> kilder, KiKjoring kjoring)
    {
        sb.AppendLine("## Kall, tokens og kostnad");
        sb.AppendLine();
        sb.AppendLine("| Kilde | Deler | Kall | Ugyldig JSON | Feilede kall | Tapte noder | Tokens inn | Tokens ut | Sekunder |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var k in kilder.Select(x => x.Kjoring))
        {
            sb.AppendLine($"| {k.Kilde} | {k.AntallDeler} | {k.AntallKall} | {k.UgyldigJson} | {k.FeiledeKall} | {k.TaptNoder} | {N(k.InputTokens)} | {N(k.OutputTokens)} | {k.Sekunder:0} |");
        }
        var alle = kilder.Select(x => x.Kjoring).ToList();
        int? Sum(Func<KiKjoringKilde, int?> f) => alle.All(x => f(x) is null) ? null : alle.Sum(x => f(x) ?? 0);
        sb.AppendLine($"| **Alle** | {alle.Sum(x => x.AntallDeler)} | {alle.Sum(x => x.AntallKall)} | {alle.Sum(x => x.UgyldigJson)} | {alle.Sum(x => x.FeiledeKall)} | {alle.Sum(x => x.TaptNoder)} | {N(Sum(x => x.InputTokens))} | {N(Sum(x => x.OutputTokens))} | {alle.Sum(x => x.Sekunder):0} |");
        sb.AppendLine();
        sb.AppendLine($"**Kostnad:** {KiPris.Beskrivelse(kjoring.Modell, Sum(x => x.InputTokens), Sum(x => x.OutputTokens))}");
        sb.AppendLine();
        var feil = alle.SelectMany(x => x.Kallfeil.Select(f => (x.Kilde, f))).ToList();
        if (feil.Count > 0)
        {
            sb.AppendLine($"Kall med ugyldig JSON eller feil ({feil.Count}, første 300 tegn av svaret):");
            sb.AppendLine();
            foreach (var (kilde, f) in feil.Take(AntallEksempler)) sb.AppendLine($"- {kilde}: {Celle(f)}");
            if (feil.Count > AntallEksempler) sb.AppendLine($"- … {feil.Count - AntallEksempler} til i `ki-utdata/kjoring.json`.");
            sb.AppendLine();
        }
    }

    private static void Kastet(StringBuilder sb, IReadOnlyList<Kilde> kilder)
    {
        var alle = kilder.SelectMany(k => k.Kjoring.Kastet.Select(r => (k.Navn, r))).ToList();
        var gyldige = kilder.Sum(k => k.Ki.Predikert.Count);
        sb.AppendLine($"## Kastede rader i valideringen ({alle.Count} av {alle.Count + gyldige} rader KI-en svarte med)");
        sb.AppendLine();
        sb.AppendLine("Hard validering, ingen reparasjon (#308). Hver rad telles på første regel som slår til, i rekkefølgen i tabellen.");
        sb.AppendLine("Kall med ugyldig JSON telles i tabellen over (radene i dem er ukjente), ikke her.");
        sb.AppendLine();
        sb.AppendLine($"| Årsak | {string.Join(" | ", kilder.Select(k => k.Navn))} | Alle |");
        sb.AppendLine($"|---|{string.Concat(kilder.Select(_ => "---:|"))}---:|");
        foreach (var arsak in KastetArsak.Rekkefolge)
        {
            sb.AppendLine($"| `{arsak}` | {string.Join(" | ", kilder.Select(k => k.Kjoring.Kastet.Count(r => r.Arsak == arsak)))} | {alle.Count(x => x.r.Arsak == arsak)} |");
        }
        sb.AppendLine();

        var sitat = alle.Where(x => x.r.Arsak == KastetArsak.FalsktSitat).GroupBy(x => x.r.Detalj).OrderByDescending(g => g.Count()).ToList();
        if (sitat.Count > 0)
        {
            sb.AppendLine("`falskt_sitat` fordelt på diagnose (bare til forklaring — radene er kastet uansett):");
            sb.AppendLine();
            foreach (var g in sitat) sb.AppendLine($"- {g.Key}: {g.Count()}");
            sb.AppendLine();
        }

        var aktorer = kilder.SelectMany(k => k.Kjoring.KastedeAktorer).GroupBy(kv => kv.Key).OrderByDescending(g => g.Sum(x => x.Value)).ToList();
        sb.AppendLine("Avviste aktører (en rad som peker på en av dem, telles som `ugyldig_aktor`):");
        sb.AppendLine();
        if (aktorer.Count == 0) sb.AppendLine("- Ingen.");
        foreach (var g in aktorer) sb.AppendLine($"- {g.Key}: {g.Sum(x => x.Value)}");
        sb.AppendLine();

        sb.AppendLine($"Eksempler (én fra hver årsak i tur, inntil {AntallEksempler}):");
        sb.AppendLine();
        sb.AppendLine("| # | Kilde | Årsak | eId | Kategori / type | Sitat | Detalj |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        var i = 0;
        var grupper = alle.GroupBy(x => x.r.Arsak).OrderBy(g => KastetArsak.Rekkefolge.ToList().IndexOf(g.Key)).Select(g => g.ToList()).ToList();
        foreach (var (kilde, r) in RoundRobin(grupper).Take(AntallEksempler))
        {
            sb.AppendLine($"| {++i} | {kilde} | `{r.Arsak}` | {Celle(KortEid(r.Eid ?? "null"))} | {Celle($"{r.Kategori} / {r.Type}")} | {Celle(r.Sitat ?? "null")} | {Celle(r.Detalj)} |");
        }
        sb.AppendLine();
    }

    private static void FalskePositive(StringBuilder sb, IReadOnlyList<Kildemaling> ki)
    {
        var fp = ki.SelectMany(m => m.FalskePositive).ToList();
        var fasitGrupper = ki.SelectMany(m => m.Fasit).Select(f => (f.Kilde, f.Gruppe)).ToHashSet();
        string Arsak(Rad r) => fasitGrupper.Contains((r.Kilde, r.Gruppe)) ? "feil/manglende aktør" : "ikke i fasiten";
        sb.AppendLine($"## KI: falske positive ({fp.Count})");
        sb.AppendLine();
        sb.AppendLine("| Kategori | Feil/manglende aktør | Ikke i fasiten |");
        sb.AppendLine("|---|---:|---:|");
        foreach (var b in Strukturmaling.Bokstavrekkefolge)
        {
            var rader = fp.Where(r => r.Bokstav == b).ToList();
            if (rader.Count == 0) continue;
            sb.AppendLine($"| {b} | {rader.Count(r => Arsak(r) == "feil/manglende aktør")} | {rader.Count(r => Arsak(r) == "ikke i fasiten")} |");
        }
        sb.AppendLine();
        sb.AppendLine($"Eksempler (én fra hver kategori i tur, inntil {AntallEksempler}):");
        sb.AppendLine();
        sb.AppendLine("| # | Kilde | eId | Type | Årsak | fra → til | Sitat |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        var i = 0;
        var grupper = Strukturmaling.Bokstavrekkefolge.Select(b => fp.Where(r => r.Bokstav == b).ToList()).Where(g => g.Count > 0).ToList();
        foreach (var r in RoundRobin(grupper).Take(AntallEksempler))
        {
            sb.AppendLine($"| {++i} | {r.Kilde} | {Celle(KortEid(r.Utsagn.Eid))} | {r.Utsagn.Type} | {Arsak(r)} | {Celle(Aktorer(r))} | {Celle(r.Utsagn.Sitat)} |");
        }
        sb.AppendLine();
    }

    private static void FalskeNegative(StringBuilder sb, IReadOnlyList<Kilde> kilder)
    {
        sb.AppendLine("## Falske negative per type (fasitutsagn ingen / bare ett lag fant)");
        sb.AppendLine();
        sb.AppendLine("| Kategori / type | Fasit | Ikke funnet av KI | Ikke funnet av mønster | Ikke funnet av noen |");
        sb.AppendLine("|---|---:|---:|---:|---:|");
        var grupper = kilder.SelectMany(k => k.Union.Fasit).GroupBy(r => (r.Utsagn.Kategori, r.Utsagn.Type))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Type, StringComparer.Ordinal).ToList();
        int Fn(Func<Kilde, Kildemaling> velg, (string K, string T) n) =>
            kilder.Sum(k => velg(k).FalskeNegative.Count(r => r.Utsagn.Kategori == n.K && r.Utsagn.Type == n.T));
        foreach (var g in grupper.Take(30))
        {
            sb.AppendLine($"| {Strukturmaling.Bokstav(g.Key.Kategori, g.Key.Type)} {g.Key.Kategori} / {g.Key.Type} | {g.Count()} | {Fn(k => k.Ki, g.Key)} | {Fn(k => k.Monster, g.Key)} | {Fn(k => k.Union, g.Key)} |");
        }
        if (grupper.Count > 30)
        {
            sb.AppendLine($"| … {grupper.Count - 30} typer til | {grupper.Skip(30).Sum(g => g.Count())} | | | |");
        }
        sb.AppendLine();
    }

    /// <summary>Fasitutsagn (filtrert på fasitraden) som bare mønster- / bare KI-laget traff.</summary>
    private static (int BareMonster, int BareKi) Bare(IEnumerable<Kilde> kilder, Func<Rad, bool> filter)
    {
        int bm = 0, bk = 0;
        foreach (var k in kilder)
        {
            var mTreff = k.Monster.Treff.Where(t => filter(t.Fasit)).Select(t => t.Fasit.Utsagn.Id).ToHashSet(StringComparer.Ordinal);
            var kTreff = k.Ki.Treff.Where(t => filter(t.Fasit)).Select(t => t.Fasit.Utsagn.Id).ToHashSet(StringComparer.Ordinal);
            bm += mTreff.Count(id => !kTreff.Contains(id));
            bk += kTreff.Count(id => !mTreff.Contains(id));
        }
        return (bm, bk);
    }

    private static void SammenligningHode(StringBuilder sb, string forste)
    {
        sb.AppendLine($"| {forste} | Fasit | Mønster pred. | Mønster P | Mønster G | KI pred. | KI P | KI G | Union P | Union G | Bare mønster | Bare KI |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
    }

    private static void Sammenligning(StringBuilder sb, string navn, IReadOnlyList<Kilde> kilder, Func<Rad, bool> filter)
    {
        var tm = Tall.For(kilder.Select(k => k.Monster), filter);
        var tk = Tall.For(kilder.Select(k => k.Ki), filter);
        var tu = Tall.For(kilder.Select(k => k.Union), filter);
        var (bm, bk) = Bare(kilder, filter);
        sb.AppendLine($"| {navn} | {tm.Fasit} | {tm.Predikert} | {P(tm.Presisjon)} | {P(tm.Gjenfinning)} | {tk.Predikert} | {P(tk.Presisjon)} | {P(tk.Gjenfinning)} | {P(tu.Presisjon)} | {P(tu.Gjenfinning)} | {bm} | {bk} |");
    }

    private static void Hode(StringBuilder sb, string forste)
    {
        sb.AppendLine($"| {forste} | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
    }

    private static IEnumerable<T> RoundRobin<T>(IReadOnlyList<List<T>> grupper)
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

    private static string P(double? v) => Malerapport.P(v);

    private static string N(int? v) => v is null ? "–" : v.Value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("nb-NO"));

    private static string Aktorer(Rad r) => $"{r.Fra?.Tekstform ?? "null"} → {r.Til?.Tekstform ?? "null"}";

    private static string KortEid(string eid) => eid.Replace("https://lovdata.no/eli/", "", StringComparison.Ordinal);

    private static string Celle(string tekst)
    {
        var t = tekst.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        return t.Length > 160 ? t[..160] + " …" : t;
    }
}

/// <summary>
/// [Ny, #308] Kostnadsestimat. Prisen gjettes ikke (CLAUDE.md §8): den står bare her når den er lest fra
/// leverandørens egen prisside, med kilde og dato. Miljøvariablene <c>REGELIDE_KI_PRIS_INN_PER_MTOK</c> /
/// <c>_UT_PER_MTOK</c> / <c>REGELIDE_KI_PRIS_VALUTA</c> overstyrer (ny pris, annen modell).
/// </summary>
internal static class KiPris
{
    /// <summary>
    /// Lest fra https://hostyourai.com/pricing 2026-10-07 («EU Hosted Gateway»), rad «DeepSeek V4 Flash»:
    /// Invoer 0,29 €/M, Cached 0,07 €/M, Uitvoer 0,35 €/M. Leverandøren rapporterer ikke hvor mange
    /// inndata-tokens som ble hurtigbufret (bare <c>prompt_tokens</c>), så estimatet bruker full
    /// inndatapris — en øvre grense.
    /// </summary>
    private static readonly Dictionary<string, (double Inn, double Ut, string Valuta, string Kilde)> Kjente = new()
    {
        ["deepseek-ai/DeepSeek-V4-Flash"] = (0.29, 0.35, "EUR", "hostyourai.com/pricing, lest 2026-10-07"),
    };

    public static string Beskrivelse(string modell, int? inn, int? ut)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (inn is null || ut is null)
        {
            return "leverandøren rapporterte ikke tokenforbruk for alle kall, så kostnaden kan ikke regnes ut.";
        }
        (double Inn, double Ut, string Valuta, string Kilde)? pris = null;
        if (double.TryParse(Environment.GetEnvironmentVariable("REGELIDE_KI_PRIS_INN_PER_MTOK"), System.Globalization.NumberStyles.Float, inv, out var pi)
            && double.TryParse(Environment.GetEnvironmentVariable("REGELIDE_KI_PRIS_UT_PER_MTOK"), System.Globalization.NumberStyles.Float, inv, out var pu))
        {
            pris = (pi, pu, Environment.GetEnvironmentVariable("REGELIDE_KI_PRIS_VALUTA") ?? "(valuta ikke oppgitt)", "miljøvariabel ved generering, ikke verifisert her");
        }
        else if (Kjente.TryGetValue(modell, out var kjent))
        {
            pris = kjent;
        }
        if (pris is null)
        {
            return $"prisen per token for `{modell}` er ikke kjent, så kostnaden er ikke estimert. Med prisen p_inn/p_ut per million tokens er kostnaden {(inn.Value / 1e6).ToString("0.###", inv)} · p_inn + {(ut.Value / 1e6).ToString("0.###", inv)} · p_ut.";
        }
        var p = pris.Value;
        var kost = inn.Value / 1e6 * p.Inn + ut.Value / 1e6 * p.Ut;
        return $"≈ {kost.ToString("0.00", inv)} {p.Valuta} for hele kjøringen ({p.Inn.ToString(inv)} {p.Valuta} per million tokens inn, {p.Ut.ToString(inv)} ut; {p.Kilde}). " +
               "Øvre grense: hurtigbufrede inndata-tokens (0,07 €/M) rapporteres ikke separat og er regnet til full pris.";
    }
}
