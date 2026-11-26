using System.Text.RegularExpressions;

namespace RegelIde.Data;

/// <summary>[Ny, Refs #486 PR 2] Hva en ordlydsregel sa om én forekomst av ordet «statsråd» (alle bøyninger): lesningen
/// (<see cref="Forekomstlesninger"/>), regelkoden (<see cref="StatsradOrdlydsregler.Regler"/>) og ordene som slo til. <see cref="Lesning"/> er
/// null for <see cref="StatsradOrdlydsregler.DekketKongenIStatsrad"/>: forekomsten står i navneformen «Kongen i statsråd» og tagges ikke.</summary>
public sealed record StatsradTreff(string? Lesning, string Regel, string Grunnlag);

/// <summary>
/// [Ny, Refs #486 PR 2, prosjekteierens avgjørelse 2026-11-25 (alternativ b: sperre og positiv tagg)] <b>Ordlydsreglene for «statsråd».</b>
/// Ordet betyr organet statsrådet, møtet der Kongen i statsråd beslutter, eller rollen statsråd (juristrunden i
/// <c>data/fasit/strukturmodell/statsrad/</c>, ordsperren i <see cref="Ordsperrer"/>, PR 1). Hver regel leser ordlyden rundt én forekomst og
/// setter en tagg per forekomst som forslag, samme mønster som <see cref="RegjeringenOrdlydsregler"/> (#486/#489). Reglene prøves i denne
/// rekkefølgen, og den første som slår til, gjelder:
/// <list type="number">
/// <item><b>«Kongen i statsråd»</b> (også Kronprinsen/Regenten, og genitiv «Kongen i statsråds»): forekomsten står i aktørens navneform og
/// tagges ikke her; den hører til navneformen «Kongen i statsråd».</item>
/// <item><b>Konvensjonstekst</b> (<see cref="Ordsperrer.KonvensjonsnoderAsync"/>): en annen stats statsråd eller regjering («vedkommende
/// statsråd … i virksomhetslandet»). Generisk konvensjonsstat, ingen referent.</item>
/// <item><b>«i statsråd»</b> med et beslutningsord i setningen («vedtak i statsråd», «behandling i statsråd», «oppnevninger i statsråd»):
/// beslutningsformen Kongen i statsråd. Ikke i en rettskilde om krig («når riket er i krig»), der kongen kan være borte og statsrådet kan
/// beslutte (Grl. §§ 13 og 41; suksesjonsforskriften §§ 5 og 6, juristrunden q5): da er det slutning. Uten beslutningsord: slutning.</item>
/// <item><b>Tittel foran et navn</b> («Statsråd Kallmyr har ansvaret», «fra statsråd Jan Christian Vestre»): rollen. Ingen kant avledes av
/// disse (juristrunden q4, prosjekteieren 2026-11-25).</item>
/// <item><b>Intetkjønn foran</b> («det samlede statsråd», «sitt statsråd»): organet statsrådet. [ENDRET, juristmålingen PR 2 (t2)] Bare «et»
/// («et statsråd») er slutning: det kan også være møtet.</item>
/// <item><b>Møtet</b> («avholdelse av statsråd», «hvert statsråd», «forberedende statsråd», «fra statsråd»): slutning (organet eller møtet).</item>
/// <item><b>Rolleord eller sideordning</b> foran («en», «den», «vedkommende», «ansvarlig(e)», «hovedansvarlig», «foredragende», «den enkelte»,
/// «hver enkelt», «annen», «som», «vært», «til»; «statsminister eller», «embetsverk og»): rollen.</item>
/// <item><b>«statsrådet(s)»</b>: organet statsrådet, unntatt «i statsrådet» (ikke «sete i») og «utenfor statsrådet», som er møtet: slutning.</item>
/// <item><b>Bestemt form og flertall</b> («statsråden», «statsrådens», «statsråder», «statsrådene», «statsrådenes»): rollen, unntatt i en
/// lovtittel («lov/forskrift … om pensjonsordning for statsråder»), som er ikke aktør.</item>
/// </list>
/// Alt annet er <see cref="Forekomstlesninger.Slutning"/>: et forslag uten referent som et menneske avgjør.
/// <para><b>Kode, ikke data</b> (samme valg som <see cref="Ordsperrer"/> og <see cref="RegjeringenOrdlydsregler"/>): lista er lukket, og en ny
/// regel er et vedtak.</para>
/// </summary>
public static class StatsradOrdlydsregler
{
    public const string DekketKongenIStatsrad = "dekket:kongen_i_statsrad";
    public const string GeneriskKonvensjonstekst = "generisk:konvensjonstekst";
    public const string KongenIStatsrad = "kongen:i_statsrad";
    public const string RolleTittel = "rolle:tittel_navn";
    public const string OrganIntetkjonn = "organ:intetkjonn";
    public const string RolleRolleord = "rolle:rolleord";
    public const string RolleSideordning = "rolle:sideordning";
    public const string OrganStatsradet = "organ:statsradet";
    public const string RolleBestemtFlertall = "rolle:bestemt_flertall";
    public const string IkkeAktorLovtittel = "ikke_aktor:lovtittel";

    /// <summary>Regelkodene i den rekkefølgen de prøves.</summary>
    public static readonly string[] Regler =
    [
        DekketKongenIStatsrad, GeneriskKonvensjonstekst, KongenIStatsrad, RolleTittel, OrganIntetkjonn, RolleRolleord, RolleSideordning,
        OrganStatsradet, RolleBestemtFlertall, IkkeAktorLovtittel,
    ];

    private const RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>Forekomstene av ordet: «statsråd» med bøyninger som hele ord, uten skille på store og små bokstaver. Bindestrek rett foran er
    /// ikke ordgrense, og en sammensetning («statsrådsprotokollen», «fagstatsråds») er ikke treff (samme ordgrense som
    /// <see cref="AktoridentitetTjeneste"/>).</summary>
    public static readonly Regex Ordet = new(@"(?<![\p{L}\p{N}-])statsråd(?:s|en|ens|et|ets|er|ene|enes)?(?![\p{L}\p{N}])", I);

    private static readonly Regex KongenIForan = new(@"(?<![\p{L}\p{N}-])(?:kongen|kronprinsen|kronprinsessen|kronprinsregenten|regenten)\s+i\s+$", I);
    private static readonly Regex KrigTittel = new(@"(?<![\p{L}])krig(?![\p{L}])", I);
    /// <summary>Beslutningsord i teksten rundt «i statsråd» (lukket liste, målt i korpuset 2026-11-25: alle 14 «i statsråd» uten «Kongen» har et).</summary>
    private static readonly Regex Beslutningsord = new(
        @"(?<![\p{L}])(?:behandl\p{L}*|vedta\p{L}*|vedtak\p{L}*|avgj\p{L}*|beslutt\p{L}*|truffet|fattet|oppnevn\p{L}*|utnevn\p{L}*|resolusjon\p{L}*)(?![\p{L}])", I);
    private static readonly Regex Rolleord = new(
        @"(?<![\p{L}\p{N}-])(?:en|den|vedkommende|ansvarlig|ansvarlige|hovedansvarlig|foredragende|enkelte|hver\s+enkelt|annen|som|vært|til)\s+$", I);
    private static readonly Regex Sideordning = new(@"(?<![\p{L}\p{N}-])(?:statsminister|embetsverk)\s+(?:eller|og)\s+$", I);
    private static readonly Regex TittelNavn = new(@"^\s+(?!Kongen\b|Stortinget\b)\p{Lu}\p{Ll}", RegexOptions.CultureInvariant);
    private static readonly Regex OrganForan = new(@"(?<![\p{L}\p{N}-])(?:det\s+samlede|sitt)\s+$", I);
    private static readonly Regex Lovtittel = new(@"(?<![\p{L}])(?:lov|forskrift)\s[^;]{0,60}?om\s+pensjonsordning\s+for\s+$", I);

    /// <summary>
    /// Leser forekomsten [<paramref name="start"/>, <paramref name="slutt"/>) i <paramref name="tekst"/>. <paramref name="titler"/> er rettskildens
    /// tittel og kortnavn (regel 3: krig). <paramref name="iKonvensjonstekst"/>: noden står i et spenn med konvensjonstekst. Null = ingen regel slo
    /// til (slutning).
    /// </summary>
    public static StatsradTreff? Vurder(string tekst, int start, int slutt, IEnumerable<string?> titler, bool iKonvensjonstekst)
    {
        var ord = tekst[start..slutt];
        var form = ord.ToLowerInvariant();
        var foran = tekst[..start];
        var etter = tekst[slutt..];

        if (form is "statsråd" or "statsråds" && KongenIForan.IsMatch(foran)) return new(null, DekketKongenIStatsrad, "«Kongen i statsråd»");
        if (iKonvensjonstekst) return new(Forekomstlesninger.GeneriskKonvensjonsstat, GeneriskKonvensjonstekst, "konvensjonstekst");

        switch (form)
        {
            case "statsrådet" or "statsrådets":
                // Samme sperre som PR 1 (Ordsperrer): «i statsrådet» (ikke «sete i») og «utenfor statsrådet» er møtet.
                if (Ordsperrer.Vurder(Ordsperrer.Statsradet, tekst, start, start + "statsrådet".Length, false) is not null) return null;
                return new(Forekomstlesninger.Statsradet, OrganStatsradet, Utdrag(foran, ord));
            case "statsråden" or "statsrådens" or "statsråder" or "statsrådene" or "statsrådenes":
                if (Lovtittel.Match(foran) is { Success: true } lt) return new(Forekomstlesninger.IkkeAktor, IkkeAktorLovtittel, $"{lt.Value.Trim()} {ord}");
                return new(Forekomstlesninger.RolleStatsrad, RolleBestemtFlertall, ord);
        }

        // Ubestemt «statsråd» / «statsråds».
        var sperre = Ordsperrer.Vurder(Ordsperrer.Statsrad, tekst, start, slutt, false);
        if (sperre == Ordsperrer.IForan)
        {
            if (titler.Any(t => t is not null && KrigTittel.IsMatch(t))) return null;
            var setning = Setning(tekst, start, slutt);
            return Beslutningsord.Match(setning) is { Success: true } b
                ? new(Forekomstlesninger.KongenIStatsrad, KongenIStatsrad, $"{b.Value} … i {ord}")
                : null;
        }
        if (form == "statsråd" && TittelNavn.IsMatch(etter)) return new(Forekomstlesninger.RolleStatsrad, RolleTittel, $"{ord}{Utdrag(etter, 20)}");
        // [ENDRET, juristmålingen PR 2 (t2)] Bare «det samlede» og «sitt» er organet. Bare «et» kan også være møtet («på et statsråd 12. mai»),
        // så det er slutning (sperret for rollen som før).
        if (sperre == Ordsperrer.Intetkjonn) return OrganForan.IsMatch(foran) ? new(Forekomstlesninger.Statsradet, OrganIntetkjonn, Utdrag(foran, ord)) : null;
        if (sperre == Ordsperrer.Motet) return null;
        if (Sideordning.Match(foran) is { Success: true } so) return new(Forekomstlesninger.RolleStatsrad, RolleSideordning, $"{so.Value.Trim()} {ord}");
        if (Rolleord.Match(foran) is { Success: true } ro) return new(Forekomstlesninger.RolleStatsrad, RolleRolleord, $"{ro.Value.Trim()} {ord}");
        return null;
    }

    /// <summary>Hvorfor en forekomst ble slutning (bare opplysning i kommentaren): ordsperrens grunn, krig i tittelen, eller ingen regel.</summary>
    public static string Slutningsgrunn(string tekst, int start, int slutt, IEnumerable<string?> titler)
    {
        var form = tekst[start..slutt].ToLowerInvariant();
        if (form is "statsrådet" or "statsrådets")
        {
            return Ordsperrer.Vurder(Ordsperrer.Statsradet, tekst, start, start + "statsrådet".Length, false) is { } g
                ? $"møtet ({Ordsperrer.Tekst(g)}): statsrådet eller Kongen i statsråd" : "ingen regel";
        }
        var sperre = Ordsperrer.Vurder(Ordsperrer.Statsrad, tekst, start, slutt, false);
        if (sperre == Ordsperrer.IForan)
        {
            return titler.Any(t => t is not null && KrigTittel.IsMatch(t))
                ? "«i statsråd» i en rettskilde om krig: kongen kan være borte, og statsrådet kan beslutte (Grl. §§ 13 og 41)"
                : "«i statsråd» uten beslutningsord i setningen";
        }
        return sperre == Ordsperrer.Motet ? "møtet: statsrådet eller Kongen i statsråd" : "ingen regel";
    }

    /// <summary>Teksten rundt forekomsten: 80 tegn foran og 60 etter. Ikke avgrenset ved punktum, fordi datoer har punktum («Det ble i statsråd
    /// 16. april 2010 vedtatt at …»).</summary>
    private static string Setning(string tekst, int start, int slutt) =>
        tekst[Math.Max(0, start - 80)..Math.Min(tekst.Length, slutt + 60)];

    private static string Utdrag(string foran, string ord)
    {
        var f = foran.Length <= 30 ? foran : foran[^30..];
        return $"{f.TrimStart()}{ord}";
    }

    private static string Utdrag(string s, int n) => s.Length <= n ? s.TrimEnd() : s[..n].TrimEnd() + " …";
}
