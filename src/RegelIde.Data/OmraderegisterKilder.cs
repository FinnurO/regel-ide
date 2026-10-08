using System.Text.Json;
using System.Text.Json.Serialization;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08] Leser øyeblikksbildene i <c>Seed/</c> som
/// områderegisteret bygges fra. Oppstarten gjør ingen nettverkskall (CLAUDE.md §4): filene fornyes manuelt med
/// <c>Seed/fornye-omraderegister.py</c>, og feltmappingen står i docs/21 (Kartverket-seksjonen).
/// <para>
/// Alt her er ren lesing — ingen database. Både <see cref="OmraderegisterSeed"/> og testene bruker samme lesere,
/// så testene måler nøyaktig de filene seeden bruker (samme begrunnelse som <c>Strukturkanter.Startsett</c>).
/// </para>
/// </summary>
public static class OmraderegisterKilder
{
    public const string Kartverketfil = "kartverket-fylker-kommuner.json";
    public const string BrregKommunefil = "brreg-kommuner.json";
    public const string BrregDomstolfil = "brreg-domstoler.json";
    public const string SsrRettsstedfil = "kartverket-ssr-rettssteder.json";
    public const string SsbFylkesendringfil = "ssb-klass-104-fylkesendringer-2024.json";
    public const string Statsforvalterfil = "statsforvalter-embetsomrader.json";
    public const string Helseregionfil = "helseregioner-rhf.json";

    /// <summary>Alle filene seeden krever. Mangler én, seedes ingenting av registeret (ingen halv inndeling).</summary>
    public static readonly string[] Alle =
        [Kartverketfil, BrregKommunefil, BrregDomstolfil, SsrRettsstedfil, SsbFylkesendringfil, Statsforvalterfil, Helseregionfil];

    /// <summary>Standardmappa: <c>Seed/</c> ved siden av den kjørende assemblyen (filene kopieres dit av csproj).</summary>
    public static string StandardMappe => Path.Combine(AppContext.BaseDirectory, "Seed");

    private static readonly JsonSerializerOptions Valg = new() { PropertyNameCaseInsensitive = true };

    // ---------------- Kartverket ----------------

    public sealed record KartverketKommune(
        [property: JsonPropertyName("kommunenummer")] string Kommunenummer,
        [property: JsonPropertyName("kommunenavnNorsk")] string KommunenavnNorsk);

    public sealed record KartverketFylke(
        [property: JsonPropertyName("fylkesnummer")] string Fylkesnummer,
        [property: JsonPropertyName("fylkesnavn")] string Fylkesnavn,
        [property: JsonPropertyName("kommuner")] IReadOnlyList<KartverketKommune> Kommuner);

    public sealed record Kartverketsnapshot(
        [property: JsonPropertyName("_url")] string Url,
        [property: JsonPropertyName("_hentet")] string Hentet,
        [property: JsonPropertyName("svar")] IReadOnlyList<KartverketFylke> Fylker)
    {
        public IEnumerable<(KartverketFylke Fylke, KartverketKommune Kommune)> Kommuner =>
            Fylker.SelectMany(f => f.Kommuner.Select(k => (f, k)));
    }

    // ---------------- Brreg ----------------

    public sealed record BrregKommune(
        [property: JsonPropertyName("organisasjonsnummer")] string Organisasjonsnummer,
        [property: JsonPropertyName("navn")] string Navn,
        [property: JsonPropertyName("kommunenummer")] string? Kommunenummer);

    public sealed record BrregKommunesnapshot(
        [property: JsonPropertyName("_url")] string Url,
        [property: JsonPropertyName("_hentet")] string Hentet,
        [property: JsonPropertyName("enheter")] IReadOnlyList<BrregKommune> Enheter);

    public sealed record BrregDomstol(
        [property: JsonPropertyName("organisasjonsnummer")] string Organisasjonsnummer,
        [property: JsonPropertyName("navn")] string Navn,
        [property: JsonPropertyName("organisasjonsform")] string Organisasjonsform);

    public sealed record BrregDomstolsnapshot(
        [property: JsonPropertyName("_hentet")] string Hentet,
        [property: JsonPropertyName("enheter")] IReadOnlyList<BrregDomstol> Enheter);

    // ---------------- SSR ----------------

    public sealed record SsrKommune(
        [property: JsonPropertyName("kommunenummer")] string Kommunenummer,
        [property: JsonPropertyName("kommunenavn")] string Kommunenavn);

    public sealed record SsrTreff(
        [property: JsonPropertyName("skrivemåte")] string Skrivemate,
        [property: JsonPropertyName("navneobjekttype")] string Navneobjekttype,
        [property: JsonPropertyName("stedsnummer")] long Stedsnummer,
        [property: JsonPropertyName("kommuner")] IReadOnlyList<SsrKommune> Kommuner);

    public sealed record SsrSok(
        [property: JsonPropertyName("sok")] string Sok,
        [property: JsonPropertyName("treff")] IReadOnlyList<SsrTreff> Treff);

    public sealed record SsrSnapshot(
        [property: JsonPropertyName("_url")] string Url,
        [property: JsonPropertyName("_hentet")] string Hentet,
        [property: JsonPropertyName("navn")] IReadOnlyList<SsrSok> Navn);

    // ---------------- SSB ----------------

    public sealed record SsbEndring(
        [property: JsonPropertyName("oldCode")] string GammelKode,
        [property: JsonPropertyName("oldName")] string GammeltNavn,
        [property: JsonPropertyName("newCode")] string NyKode,
        [property: JsonPropertyName("newName")] string NyttNavn);

    public sealed record SsbSvar([property: JsonPropertyName("codeChanges")] IReadOnlyList<SsbEndring> Endringer);

    public sealed record SsbSnapshot(
        [property: JsonPropertyName("_url")] string Url,
        [property: JsonPropertyName("_hentet")] string Hentet,
        [property: JsonPropertyName("svar")] SsbSvar Svar);

    // ---------------- Kuraterte kilder ----------------

    public sealed record KildeLenke(
        [property: JsonPropertyName("tekst")] string Tekst,
        [property: JsonPropertyName("lenke")] string Lenke);

    public sealed record Statsforvalterembete(
        [property: JsonPropertyName("organisasjonsnummer")] string Organisasjonsnummer,
        [property: JsonPropertyName("navn")] string Navn,
        [property: JsonPropertyName("fylker")] IReadOnlyList<string> Fylker);

    public sealed record StatsforvalterKilde(
        [property: JsonPropertyName("_sekundaerkilde")] KildeLenke Sekundaerkilde,
        [property: JsonPropertyName("embeter")] IReadOnlyList<Statsforvalterembete> Embeter);

    public sealed record Rhf(
        [property: JsonPropertyName("organisasjonsnummer")] string Organisasjonsnummer,
        [property: JsonPropertyName("navn")] string Navn,
        [property: JsonPropertyName("helseregion")] string Helseregion,
        [property: JsonPropertyName("omfatter")] IReadOnlyList<string> Omfatter,
        [property: JsonPropertyName("omfatter_ordrett")] string OmfatterOrdrett,
        [property: JsonPropertyName("lenke")] string Lenke,
        [property: JsonPropertyName("sist_endret")] string SistEndret);

    public sealed record HelseregionKilde([property: JsonPropertyName("rhf")] IReadOnlyList<Rhf> Rhf);

    /// <summary>Alle kildene, lest én gang.</summary>
    public sealed record Kilder(
        Kartverketsnapshot Kartverket, BrregKommunesnapshot BrregKommuner, BrregDomstolsnapshot BrregDomstoler,
        SsrSnapshot Ssr, SsbSnapshot Ssb, StatsforvalterKilde Statsforvaltere, HelseregionKilde Helseregioner);

    /// <summary>Leser alle filene fra <paramref name="mappe"/>. Null hvis én mangler (ingen halv inndeling).</summary>
    public static Kilder? Les(string mappe)
    {
        if (Alle.Any(f => !File.Exists(Path.Combine(mappe, f)))) return null;
        T L<T>(string fil) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(mappe, fil)), Valg)
            ?? throw new InvalidOperationException($"{fil} er tom.");
        return new Kilder(
            L<Kartverketsnapshot>(Kartverketfil), L<BrregKommunesnapshot>(BrregKommunefil),
            L<BrregDomstolsnapshot>(BrregDomstolfil), L<SsrSnapshot>(SsrRettsstedfil), L<SsbSnapshot>(SsbFylkesendringfil),
            L<StatsforvalterKilde>(Statsforvalterfil), L<HelseregionKilde>(Helseregionfil));
    }
}
