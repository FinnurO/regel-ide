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
}
