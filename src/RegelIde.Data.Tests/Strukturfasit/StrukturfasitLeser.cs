using System.Text.Json;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Leser fasiten og nodefixturene fra
/// <c>data/fasit/strukturmodell/</c> (lagt inn i #306). Ingen database, ingen nettverk — målingen skal
/// kjøre i en vanlig <c>dotnet test</c> (akseptansekriterium 1 i #307).
/// <para>
/// Filene leses fra repo-roten, ikke kopieres til bin/: rapporten skrives tilbake til samme mappe
/// (<c>maling-monster.md</c>), og fasiten er ~1,5 MB som ikke trenger å dupliseres per bygg.
/// </para>
/// </summary>
internal static class StrukturfasitLeser
{
    public static string FasitMappe => Path.Combine(FinnRepoRot(), "data", "fasit", "strukturmodell");

    /// <summary>
    /// Alle kilder som har BÅDE en fasitfil (<c>&lt;kilde&gt;.json</c>) og en nodefil
    /// (<c>noder/&lt;kilde&gt;.json</c>), sortert på navn. Kommer det en sjette kilde, blir den med
    /// automatisk — uten at testen må endres.
    /// </summary>
    public static IReadOnlyList<Fasitkilde> LesAlle()
    {
        var mappe = FasitMappe;
        var kilder = Directory.GetFiles(mappe, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null && File.Exists(Path.Combine(mappe, "noder", n + ".json")))
            .Select(n => n!)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (kilder.Count == 0)
        {
            throw new InvalidOperationException($"Fant ingen fasit med nodefil i {mappe}.");
        }

        return kilder.Select(n => Les(mappe, n)).ToList();
    }

    private static Fasitkilde Les(string mappe, string navn)
    {
        var fasit = JsonSerializer.Deserialize<Strukturdokument>(File.ReadAllText(Path.Combine(mappe, navn + ".json")))
            ?? throw new InvalidOperationException($"{navn}.json er tom.");
        var noder = JsonSerializer.Deserialize<List<Strukturnode>>(File.ReadAllText(Path.Combine(mappe, "noder", navn + ".json")))
            ?? throw new InvalidOperationException($"noder/{navn}.json er tom.");
        return new Fasitkilde(navn, fasit, noder);
    }

    /// <summary>Går opp fra testkjørerens bin-katalog til mappen som inneholder src/RegelIde.sln.</summary>
    private static string FinnRepoRot()
    {
        // Samme oppslag som TjenesteModellSkjemaTests.FinnRepoRot — robust mot at bin-stien varierer
        // (Debug/Release, net10.0, worktree-mappe).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "RegelIde.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new InvalidOperationException("Fant ikke repo-roten (ingen ancestor-mappe med src/RegelIde.sln).");
    }
}

/// <summary>Én fasitkilde: navnet (filnavnet), fasitdokumentet og nodene den er laget fra.</summary>
internal sealed record Fasitkilde(string Navn, Strukturdokument Fasit, IReadOnlyList<Strukturnode> Noder)
{
    /// <summary>Grunnlaget konverteren får: samme hovedkilde og de samme nodene som annotatøren leste.</summary>
    public Strukturkonverteringsgrunnlag Grunnlag => new(Fasit.Rettskilde, Fasit.Eli, Noder);
}
