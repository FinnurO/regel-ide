using System.Text;
using System.Text.RegularExpressions;
using SharpCompress.Readers;

namespace RegelIde.Data;

/// <summary>
/// Henter en enkelt rettskilde fra Lovdatas offisielle, gratis bulk-datasett
/// (`https://api.lovdata.no/v1/publicData/get/…`, NLOD 2.0 — se data/kilder/README.md for
/// proveniens og filnavnkonvensjonen dette bygger på). Laster ned hele arkivet (~5,8/20 MB) og
/// plukker ut én fil per kall — ingen caching av arkivet ennå, se src/README.md for merknad om det.
/// </summary>
public sealed partial class LovdataBulkHenter(HttpClient http)
{
    private const string LoverUrl = "https://api.lovdata.no/v1/publicData/get/gjeldende-lover.tar.bz2";
    private const string ForskrifterUrl = "https://api.lovdata.no/v1/publicData/get/gjeldende-sentrale-forskrifter.tar.bz2";

    [GeneratedRegex(@"^(LOV|FOR)-(\d{4})-(\d{2})-(\d{2})(?:-(\d+))?$")]
    private static partial Regex DatokodeMønster();

    // Filnavn i arkivet: "nl-19890602-027.xml" (lov, 3 sifre) / "sf-20050608-0538.xml"
    // (forskrift, 4 sifre) — Lovdatas to bulk-datasett nullpadder løpenummeret ULIKT, bekreftet i
    // ekte data (data/kilder/README.md). Matcher derfor på dato+løpenummer som TALL, ikke på
    // eksakt padding-bredde, for å ikke måtte gjette riktig antall sifre per datasett.
    [GeneratedRegex(@"^(nl|sf)-(\d{8})-(\d+)\.xml$")]
    private static partial Regex ArkivFilnavnMønster();

    /// <summary>
    /// Henter rå HTML for en gitt datokode (f.eks. "LOV-1989-06-02-27" eller "LOV-1967-02-10" for
    /// en lov uten løpenummer). Kaster <see cref="InvalidOperationException"/> hvis datokoden ikke
    /// finnes i arkivet — ingen gjettet fallback.
    /// </summary>
    public async Task<string> HentRaaHtmlAsync(string datokode, CancellationToken ct = default)
    {
        var (arkivUrl, dato, løpenummer) = TolkDatokode(datokode);

        // SharpCompress trenger en seekbar strøm for å kjenne igjen tar.bz2-formatet — HttpClients
        // nettverksstrøm er det ikke, så hele arkivet (≤ ~20 MB) lastes ned til minnet først.
        var arkivBytes = await http.GetByteArrayAsync(arkivUrl, ct);
        using var arkivStrøm = new MemoryStream(arkivBytes);
        using var leser = ReaderFactory.Open(arkivStrøm);
        while (leser.MoveToNextEntry())
        {
            if (leser.Entry.IsDirectory) continue;

            var navn = Path.GetFileName(leser.Entry.Key ?? "");
            var m = ArkivFilnavnMønster().Match(navn);
            if (!m.Success || m.Groups[2].Value != dato || int.Parse(m.Groups[3].Value) != løpenummer) continue;

            using var entryStrøm = leser.OpenEntryStream();
            using var minne = new MemoryStream();
            await entryStrøm.CopyToAsync(minne, ct);
            var raaBytes = minne.ToArray();

            // Lovdatas bulk-filer er cp1252-kodet (data/kilder/README.md) — i motsetning til de
            // allerede UTF-8-korrigerte fixture-filene i data/kilder/raw-lovdata/.
            return Encoding.GetEncoding(1252).GetString(raaBytes);
        }

        throw new InvalidOperationException(
            $"Fant ikke noen fil for datokode '{datokode}' i Lovdata-arkivet. Ingen gjettet fallback.");
    }

    private static (string ArkivUrl, string Dato, int Løpenummer) TolkDatokode(string datokode)
    {
        var m = DatokodeMønster().Match(datokode);
        if (!m.Success)
        {
            throw new FormatException(
                $"Datokode '{datokode}' matcher ikke forventet mønster LOV|FOR-ÅÅÅÅ-MM-DD[-løpenummer]. Ingen gjettet fallback.");
        }

        var erLov = m.Groups[1].Value == "LOV";
        var dato = $"{m.Groups[2].Value}{m.Groups[3].Value}{m.Groups[4].Value}";
        var løpenummer = m.Groups[5].Success ? int.Parse(m.Groups[5].Value) : 0;
        return (erLov ? LoverUrl : ForskrifterUrl, dato, løpenummer);
    }
}
