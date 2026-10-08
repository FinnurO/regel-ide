using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Sammenligner et konvertert dokument med fasiten.
/// <para>
/// <b>Treffregel [LÅST, #307 «Utforming»]:</b> et predikert utsagn treffer et fasitutsagn når de har
/// samme eId + kategori + type, OG — for hvert endepunkt (fra/til) fasiten HAR — samme tekstform uten
/// skille på store/små bokstaver. Fasiten har null-endepunkt der teksten ikke avgjør aktøren; da
/// kreves ingenting av det predikerte.
/// </para>
/// <para>
/// <b>Tolkning av «samme tekstform»</b> (ikke spesifisert i #307, valgt her og skrevet i rapporten):
/// en aktør i fasit-formatet er én omtaleform MED <c>varianter</c> (FORMAT.md). «Samme tekstform» betyr
/// derfor at de to aktørenes former — tekstform ∪ varianter — har minst én felles skrivemåte. Det gjør
/// at «Sametinget» treffer fasitaktøren «sameting» (variant «Sametinget»), og at
/// «Østre valgkrets/Nuortaguovllu válgabiire» (variant «Østre valgkrets») treffer «Østre valgkrets».
/// Ingen fuzzy-sammenligning utover dette.
/// </para>
/// <para>
/// Hvert fasitutsagn kan treffes av høyst ett predikert, og omvendt (én-til-én). Tildelingen er grådig
/// i dokumentrekkefølge, men i to runder: først par der fasiten har endepunkt som faktisk ble
/// sammenlignet, så resten — slik at et predikert utsagn uten endepunkt ikke «stjeler» fasitraden et
/// presist predikert utsagn ville truffet.
/// </para>
/// </summary>
internal static class Strukturmaling
{
    /// <summary>Sammenlign ett konvertert dokument med fasiten for samme kilde.</summary>
    public static Kildemaling Mal(string kilde, Strukturdokument fasit, Strukturdokument predikert)
    {
        var fasitAktorer = fasit.Aktorer.ToDictionary(a => a.Id);
        var predAktorer = predikert.Aktorer.ToDictionary(a => a.Id);

        var fasitRader = fasit.Utsagn.Select(u => new Rad(kilde, u, Aktor(fasitAktorer, u.Fra), Aktor(fasitAktorer, u.Til))).ToList();
        var predRader = predikert.Utsagn.Select(u => new Rad(kilde, u, Aktor(predAktorer, u.Fra), Aktor(predAktorer, u.Til))).ToList();

        var fasitPerGruppe = fasitRader.ToLookup(r => r.Gruppe);
        var brukt = new HashSet<Rad>(ReferenceEqualityComparer.Instance);
        var treff = new Dictionary<Rad, Rad>(ReferenceEqualityComparer.Instance);

        foreach (var kunMedEndepunkt in new[] { true, false })
        {
            foreach (var p in predRader)
            {
                if (treff.ContainsKey(p)) continue;
                var kandidat = fasitPerGruppe[p.Gruppe].FirstOrDefault(f =>
                    !brukt.Contains(f) && Forenlig(p, f) && (!kunMedEndepunkt || f.Fra is not null || f.Til is not null));
                if (kandidat is null) continue;
                treff[p] = kandidat;
                brukt.Add(kandidat);
            }
        }

        // «Uten endepunktkrav»: bare eId + kategori + type, telt per gruppe (min av antallene). Viser hvor
        // mye av feilen som er GJENKJENNING og hvor mye som er feil/manglende AKTØR.
        var predPerGruppe = predRader.ToLookup(r => r.Gruppe);
        var tpUtenEndepunkt = predPerGruppe.ToDictionary(g => g.Key, g => Math.Min(g.Count(), fasitPerGruppe[g.Key].Count()));

        return new Kildemaling(
            kilde,
            fasitRader,
            predRader,
            treff.Select(kv => (kv.Key, kv.Value)).ToList(),
            predRader.Where(p => !treff.ContainsKey(p)).ToList(),
            fasitRader.Where(f => !brukt.Contains(f)).ToList(),
            tpUtenEndepunkt);
    }

    private static bool Forenlig(Rad pred, Rad fasit) =>
        Endepunkt(pred.Fra, fasit.Fra) && Endepunkt(pred.Til, fasit.Til);

    private static bool Endepunkt(StrukturAktor? pred, StrukturAktor? fasit) =>
        fasit is null || (pred is not null && Former(pred).Overlaps(Former(fasit)));

    private static HashSet<string> Former(StrukturAktor a) =>
        new[] { a.Tekstform }.Concat(a.Varianter ?? []).Select(f => f.Trim().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

    private static StrukturAktor? Aktor(Dictionary<string, StrukturAktor> aktorer, string? id) =>
        id is not null && aktorer.TryGetValue(id, out var a) ? a : null;

    /// <summary>
    /// Kanttypebokstaven fra docs/33 §4.3 (R/K/M/O/A/G/I/T) for en FORMAT.md-type — samme tabell som
    /// <c>STD</c> i <c>data/fasit/strukturmodell/designtest.py</c>. <c>annet:*</c>-typer får egen rad
    /// («annet»): designtest.py sorterer dem videre med nøkkelordregler, men mønsterlaget produserer
    /// ingen av dem, så de ville bare blåst opp gjenfinningsnevneren i R/K uten å si noe om mønstrene.
    /// <c>bistar</c>/<c>samarbeider_med</c> er bevisst senere lag (docs/33 §4.4).
    /// </summary>
    /// <para>[ENDRET, issue #341] Myndighetsrelasjonene er K (kompetanse med motpart); R er struktur + har_delegert_til. K er
    /// hele kompetanselista i <see cref="Strukturkontrakt"/> (typologien), så nye typer ikke havner i «annet».</para>
    public static string Bokstav(string kategori, string type) => (kategori, type) switch
    {
        (_, "administrativt_underordnet" or "sekretariat_for" or "etterfolger" or "rapporterer_til" or "velger" or "ledes_av"
            or "eies_av" or "radgir" or "representerer" or "har_delegert_til" or "oppretter" or "avvikler") => "R",
        ("relasjon", "del_av") => "G",
        (_, "bistar" or "samarbeider_med") => "senere lag",
        ("kompetanse", _) when Strukturkontrakt.TyperPerKategori["kompetanse"].Contains(type) => "K",
        (_, "medlem_av" or "inngar_i") => "M",
        (_, "bestar_av") => "O",
        ("sammensetning_omrade", "del_av") => "O",
        (_, "har_ansvarsomrade" or "har_jurisdiksjon" or "har_sete_i") => "A",
        (_, "skal_finnes") => "T",
        (_, "har_medlemmer" or "har_organ") => "G",
        _ => "annet",
    };

    /// <summary>Rekkefølgen bokstavene vises i (docs/33 §4.3), med I og T selv om fasiten har få/ingen.</summary>
    public static readonly string[] Bokstavrekkefolge = ["R", "K", "M", "O", "A", "G", "I", "T", "annet", "senere lag"];
}

/// <summary>Ett utsagn (fasit eller predikert) med oppslåtte aktører.</summary>
internal sealed record Rad(string Kilde, StrukturUtsagn Utsagn, StrukturAktor? Fra, StrukturAktor? Til)
{
    public (string Eid, string Kategori, string Type) Gruppe => (Utsagn.Eid, Utsagn.Kategori, Utsagn.Type);
    public string Bokstav => Strukturmaling.Bokstav(Utsagn.Kategori, Utsagn.Type);
    public string Monster => Utsagn.Oppdagelseskilde ?? "(manuell)";
}

/// <summary>Resultatet for én kilde.</summary>
internal sealed record Kildemaling(
    string Kilde,
    IReadOnlyList<Rad> Fasit,
    IReadOnlyList<Rad> Predikert,
    IReadOnlyList<(Rad Predikert, Rad Fasit)> Treff,
    IReadOnlyList<Rad> FalskePositive,
    IReadOnlyList<Rad> FalskeNegative,
    IReadOnlyDictionary<(string Eid, string Kategori, string Type), int> TreffUtenEndepunktPerGruppe);

/// <summary>Presisjon/gjenfinning for et utsnitt. Null = udefinert (ingen predikerte / ingen i fasiten).</summary>
internal sealed record Tall(int Fasit, int Predikert, int Treff, int TreffUtenEndepunkt)
{
    public double? Presisjon => Predikert == 0 ? null : (double)Treff / Predikert;
    public double? Gjenfinning => Fasit == 0 ? null : (double)Treff / Fasit;
    public double? PresisjonUtenEndepunkt => Predikert == 0 ? null : (double)TreffUtenEndepunkt / Predikert;
    public double? GjenfinningUtenEndepunkt => Fasit == 0 ? null : (double)TreffUtenEndepunkt / Fasit;

    /// <summary>Regner tallene for de radene i <paramref name="malinger"/> som <paramref name="filter"/> slipper gjennom.</summary>
    public static Tall For(IEnumerable<Kildemaling> malinger, Func<Rad, bool> filter)
    {
        int fasit = 0, pred = 0, treff = 0, utenEndepunkt = 0;
        foreach (var m in malinger)
        {
            fasit += m.Fasit.Count(filter);
            pred += m.Predikert.Count(filter);
            treff += m.Treff.Count(t => filter(t.Predikert));
            // Grupper deler eId+kategori+type, og bokstav/kilde/type er funksjoner av kategori+type,
            // så et filter på dem gjelder hele gruppen likt. Per-mønster-filtre bruker ikke dette tallet.
            utenEndepunkt += m.TreffUtenEndepunktPerGruppe
                .Where(g => m.Predikert.Any(p => p.Gruppe == g.Key && filter(p)))
                .Sum(g => g.Value);
        }
        return new Tall(fasit, pred, treff, utenEndepunkt);
    }
}
