namespace RegelIde.Data.Tests;

internal static class Testdata
{
    public static string LesAlkoholloven() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "alkoholloven-LOV-1989-06-02-27.html"));

    public static string LesForvaltningsloven() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "forvaltningsloven-LOV-1967-02-10.html"));

    public static string LesAlkoholforskriften() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "alkoholforskriften-FOR-2005-06-08-538.html"));

    /// <summary>Ekte tekst fra Bergens retningslinjer SD-24-113 — se data/kilder/raw-handbok/README.md.</summary>
    public static string LesBergenRetningslinjer() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "Handbok", "bergen-retningslinjer-SD-24-113.txt"));

    /// <summary>Ekte tekst fra Bergens forskrift SD-24-114 — se data/kilder/raw-handbok/README.md.</summary>
    public static string LesBergenForskrift() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "Handbok", "bergen-forskrift-salgs-skjenke-apningstider.txt"));

    /// <summary>Ekte fixture fra data/kilder/raw-nettside/&lt;filnavn&gt; — se README der.</summary>
    public static string LesNettsideFixture(string filnavn) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "Nettside", filnavn));

    /// <summary>Tre ekte ressurser (2 AltinnApp + 1 MaskinportenSchema) fra Altinns ressursregister-API, verifisert live (feature/altinn-hostere).</summary>
    public static string LesAltinnRessursliste() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "AltinnHosting", "ressursliste-sample3.json"));

    /// <summary>Ekte skjemaoversikt-indeksside (info.altinn.no/skjemaoversikt), verifisert live (feature/altinn-hostere).</summary>
    public static string LesSkjemaoversiktIndeksside() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "AltinnHosting", "skjemaoversikt-provider-index.html"));

    /// <summary>Ekte tjenesteside (/skjemaoversikt/advokattilsynet/advokat/), verifisert live (feature/altinn-hostere).</summary>
    public static string LesSkjemaoversiktAdvokatside() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "AltinnHosting", "skjemaoversikt-advokat.html"));

    /// <summary>
    /// Fem ekte rader, trimmet fra Johanns ~288-rads Statsforvalter "skjema og tjenester"-uttrekk
    /// (feature/statsforvalter-tjenester-hoster) — dekker en tjeneste tilbudt av alle 10 embeter, en
    /// tilbudt av kun 1, en tilbudt av 2, og et ekte bokmål/nynorsk PDF-variant-par med samme
    /// tjenestenavn men ulik url.
    /// </summary>
    public static string LesStatsforvalterTjenesteliste() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "StatsforvalterHosting", "statsforvalter-tjenester-sample.json"));

    /// <summary>
    /// Fire ekte rader, trimmet fra Johanns ~655-rads fylkeskommune "dialog"-kontaktskjema-uttrekk
    /// (feature/generaliser-tjenesteliste-importer) — strukturelt identisk med Statsforvalter-kilden
    /// (samme <see cref="RegelIde.Data.TjenestelisteImporter"/>) bortsett fra feltnavnet <c>kategori</c>
    /// i stedet for <c>tema</c>, som importøren aldri leser uansett. To rader fra Agder fylkeskommune,
    /// én fra Innlandet fylkeskommune — hver med nøyaktig 1 <c>tilbys_av</c>-oppføring, empirisk
    /// representativt for hele produksjonsuttrekket.
    /// </summary>
    public static string LesFylkeskommuneDialogtjenesteliste() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "FylkeskommuneDialogHosting", "fylkeskommune-dialogtjenester-sample.json"));

    /// <summary>
    /// Tre ekte kommune-objekter/ni ekte tjeneste-records, trimmet fra Johanns ~15 332-rads
    /// kommune.no-uttrekk (feature/kommune-tjenester-hoster) — inkluderer BEVISST den ekte
    /// url-kollisjonen mellom to distinkte kommuner som begge heter "Herøy" (organisasjonsnummer
    /// 872417982 i Nordland vs. 964978840 i Møre og Romsdal), se
    /// <see cref="RegelIde.Data.KommuneTjenesteHenter"/>s klassekommentar.
    /// </summary>
    public static string LesKommuneTjenesteHosting() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "KommuneTjenesteHosting", "treff-sample.json"));
}
