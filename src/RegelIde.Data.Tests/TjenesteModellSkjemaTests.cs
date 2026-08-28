using System.Text.Json.Nodes;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, 2026-08-28] Anti-drift-vern for <see cref="TjenesteModellSkjema"/>: skjemaets <c>enum</c>-
/// lister for kodefelt MÅ speile de faktiske <c>GyldigeX</c>-arrayene i domenelaget nøyaktig — denne
/// testen feiler hvis noen legger til/fjerner en gyldig verdi ett sted uten å oppdatere skjemaet.
/// Ingen DB nødvendig — bygger skjemaet rent i minnet.
/// </summary>
public class TjenesteModellSkjemaTests
{
    private static string[] EnumVerdier(JsonObject skjema, string egenskap) => skjema["$defs"]!["Rettighet"]!
        ["properties"]![egenskap]!["enum"]!.AsArray()
        .Where(v => v is not null)
        .Select(v => v!.GetValue<string>())
        .ToArray();

    [Fact]
    public void Status_enum_matcher_GyldigeStatuser()
    {
        var skjema = TjenesteModellSkjema.Bygg();
        Assert.Equal(TjenesteregisterTjeneste.GyldigeStatuser, EnumVerdier(skjema, "status"));
    }

    [Fact]
    public void Type_enum_matcher_GyldigeRettighetstyper()
    {
        var skjema = TjenesteModellSkjema.Bygg();
        Assert.Equal(TjenesteregisterTjeneste.GyldigeRettighetstyper, EnumVerdier(skjema, "type"));
    }

    [Fact]
    public void Handlingstype_og_utfort_av_enum_matcher_kodelistene()
    {
        var skjema = TjenesteModellSkjema.Bygg();
        var handling = skjema["$defs"]!["Handling"]!["properties"]!;

        var handlingstyper = handling["handlingstype"]!["enum"]!.AsArray()
            .Where(v => v is not null).Select(v => v!.GetValue<string>()).ToArray();
        var utfortAv = handling["utfort_av"]!["enum"]!.AsArray()
            .Where(v => v is not null).Select(v => v!.GetValue<string>()).ToArray();

        Assert.Equal(HandlingregisterTjeneste.GyldigeHandlingstyper, handlingstyper);
        Assert.Equal(HandlingregisterTjeneste.GyldigeUtfortAv, utfortAv);
    }

    [Fact]
    public void Rel_enum_matcher_GyldigeRel()
    {
        var skjema = TjenesteModellSkjema.Bygg();
        var rel = skjema["$defs"]!["Avhengighet"]!["properties"]!["rel"]!["enum"]!.AsArray()
            .Select(v => v!.GetValue<string>()).ToArray();
        Assert.Equal(TjenesteavhengighetregisterTjeneste.GyldigeRel, rel);
    }

    [Fact]
    public void Skjemaet_dekker_bade_enkelt_og_flertallsform()
    {
        var skjema = TjenesteModellSkjema.Bygg();
        var oneOf = skjema["oneOf"]!.AsArray();
        Assert.Equal(2, oneOf.Count);
        Assert.Equal("#/$defs/Rettighet", oneOf[0]!["$ref"]!.GetValue<string>());
        Assert.Equal("array", oneOf[1]!["properties"]!["rettigheter"]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// (2026-08-28) Johann vil ha en committet kopi av skjemaet liggende i docs/ (i hvert fall til
    /// applikasjonen er stabil) — slik at han kan se/dele det uten å kjøre opp API-et. Denne testen er
    /// selve syncvernet: feiler den, er den committede filen utdatert og må regenereres (se
    /// docs/23-tjeneste-modell-eksport-og-skjema.md §2 for kommandoen).
    /// </summary>
    [Fact]
    public void Committet_docs_kopi_er_i_sync_med_det_genererte_skjemaet()
    {
        var repoRot = FinnRepoRot();
        var filsti = Path.Combine(repoRot, "docs", "tjeneste-modell.schema.json");
        Assert.True(File.Exists(filsti), $"Fant ikke {filsti} — er den flyttet/omdøpt?");

        var committet = JsonNode.Parse(File.ReadAllText(filsti));
        var generert = TjenesteModellSkjema.Bygg();

        Assert.True(JsonNode.DeepEquals(committet, generert),
            "docs/tjeneste-modell.schema.json har driftet fra TjenesteModellSkjema.Bygg() — regenerer filen (se docs/23 §2).");
    }

    /// <summary>Går opp fra testkjørerens bin-katalog til mappen som inneholder src/RegelIde.sln.</summary>
    private static string FinnRepoRot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "RegelIde.sln")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException("Fant ikke repo-roten (ingen ancestor-mappe med src/RegelIde.sln).");
        }
        return dir.FullName;
    }
}
