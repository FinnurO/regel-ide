namespace RegelIde.Kildekonvertering.Tests;

internal static class Testdata
{
    public static string LesAlkoholloven() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "alkoholloven-LOV-1989-06-02-27.html"));

    public static string LesAlkoholforskriften() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "alkoholforskriften-FOR-2005-06-08-538.html"));

    public static string LesForvaltningsloven() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "forvaltningsloven-LOV-1967-02-10.html"));

    /// <summary>
    /// Ekte tekst (ikke syntetisk) fra Bergen kommunes retningslinjer SD-24-113 — se
    /// data/kilder/raw-handbok/README.md for proveniens (hentet via WebFetch + PDF-tekstlag 2026-08-12).
    /// </summary>
    public static string LesBergenRetningslinjer() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "bergen-retningslinjer-SD-24-113.txt"));

    /// <summary>
    /// Ekte tekst (ikke syntetisk) fra Bergen kommunes FORSKRIFT om salgs-, skjenke- og
    /// åpningstider (Dok.nr SD-24-114) — se data/kilder/raw-handbok/README.md for proveniens og for
    /// det reelle strukturfunnet (bare tallpunktum-overskrifter, ikke "Kapittel N", på toppnivå).
    /// </summary>
    public static string LesBergenForskrift() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "bergen-forskrift-salgs-skjenke-apningstider.txt"));
}
