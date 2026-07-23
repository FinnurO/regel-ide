using RegelIde.Kildekonvertering;

namespace RegelIde.Api;

/// <summary>
/// In-memory register over rettskilder — leser og konverterer HTML-fixturene direkte fra
/// <c>data/kilder/raw-lovdata/</c> ved oppstart (ingen kopi til byggeoutput — <c>ContentRootPath</c>
/// peker på selve prosjektmappen både under <c>dotnet run</c> og i WebApplicationFactory-tester,
/// ikke bin-outputen, så en relativ sti derfra er enklere og mer robust enn en CopyToOutputDirectory-omvei).
/// Bevisst uten database ("Sett et API for å gi ut rettskilder" — scope for denne økten er
/// å GI UT allerede-konverterte rettskilder, ikke bygge lagringslaget fra §2 i teknisk design).
/// Byttes til et faktisk repository (Postgres, §2) uten at API-kontrakten nedenfor endres.
/// </summary>
public sealed class RettskildeRepository
{
    private readonly Dictionary<string, KonverteringResultat> _vedDatokode;

    public RettskildeRepository(IWebHostEnvironment miljo, IConfiguration config)
    {
        var relativSti = config["RettskilderMappe"] ?? Path.Combine("..", "..", "data", "kilder", "raw-lovdata");
        var mappe = Path.GetFullPath(Path.Combine(miljo.ContentRootPath, relativSti));
        var resultater = new List<KonverteringResultat>();
        foreach (var fil in Directory.EnumerateFiles(mappe, "*.html").OrderBy(f => f))
        {
            var html = File.ReadAllText(fil);
            resultater.Add(LovdataKonverterer.Konverter(html));
        }

        _vedDatokode = resultater.ToDictionary(r => r.Metadata.Datokode);
    }

    public IReadOnlyList<KonverteringResultat> AlleRettskilder() => _vedDatokode.Values.ToList();

    public KonverteringResultat? FinnVedDatokode(string datokode) =>
        _vedDatokode.GetValueOrDefault(datokode);
}
