using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
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

            // RETTET 2026-07-29: antakelsen om at Lovdatas bulk-filer er cp1252-kodet (tidligere
            // her og i data/kilder/README.md) viste seg å være FEIL — bekreftet ved en ekte live
            // henting via /api/rettskilder/lovdata som produserte klassisk "UTF-8 lest som cp1252"-
            // mojibake ("Â§", "formÃ¥l", "InnfÃ¸rsel"). Arkivets XML-filer er faktisk UTF-8. Dette er
            // etter alt å dømme SAMME rotårsak som mojibake-hendelsen 2026-07-23 (se data/kilder/
            // README.md) — den ble den gang antatt å være en ekstern engangsfeil under manuell
            // nedlasting og rettet kun på de statiske fixture-filene, ikke i denne metoden, som
            // dermed beholdt den samme feilen uoppdaget helt til nå.
            return Encoding.UTF8.GetString(raaBytes);
        }

        throw new InvalidOperationException(
            $"Fant ikke noen fil for datokode '{datokode}' i Lovdata-arkivet. Ingen gjettet fallback.");
    }

    /// <summary>
    /// Delt lavnivå-iterasjon over BEGGE bulk-arkiv (lov+forskrift) — laster ned og dekomprimerer
    /// hvert arkiv ÉN gang, yielder allerede UTF-8-dekodet rå HTML for hver oppføring. Brukt av både
    /// <see cref="HentAlleOppforingerAsync"/> (trekker kun ut tittel, byggesteg 5 runde 2 katalogen)
    /// og <see cref="HentAlleDokumenterAsync"/> (trekker ut hele dokumentet, full Lovdata-synk,
    /// docs/13-backlog.md §6) — å laste ned og dekomprimere de samme ~26 MB arkivene to ganger for
    /// to ulike formål ville vært unødvendig tregt.
    /// </summary>
    private async IAsyncEnumerable<(string Datokode, string Type, string Html)> HentAlleArkivEntrierAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var (arkivUrl, type) in new[] { (LoverUrl, "lov"), (ForskrifterUrl, "forskrift") })
        {
            var arkivBytes = await http.GetByteArrayAsync(arkivUrl, ct);
            using var arkivStrøm = new MemoryStream(arkivBytes);
            using var leser = ReaderFactory.Open(arkivStrøm);
            while (leser.MoveToNextEntry())
            {
                ct.ThrowIfCancellationRequested();
                if (leser.Entry.IsDirectory) continue;

                var navn = Path.GetFileName(leser.Entry.Key ?? "");
                var m = ArkivFilnavnMønster().Match(navn);
                if (!m.Success) continue;

                using var entryStrøm = leser.OpenEntryStream();
                using var minne = new MemoryStream();
                await entryStrøm.CopyToAsync(minne, ct);
                var html = Encoding.UTF8.GetString(minne.ToArray());

                var dato = m.Groups[2].Value;
                var løpenummer = int.Parse(m.Groups[3].Value);
                var datokode = $"{(type == "lov" ? "LOV" : "FOR")}-{dato[..4]}-{dato[4..6]}-{dato[6..8]}" +
                    (løpenummer == 0 ? "" : $"-{løpenummer}");

                yield return (datokode, type, html);
            }
        }
    }

    /// <summary>
    /// Byggesteg 5 runde 2 (Lovdata-katalog/søk, docs/14-byggesteg5-teknisk-design.md) — itererer ALLE
    /// oppføringer i begge bulk-arkiv og trekker kun ut tittel + datokode + type, IKKE hele
    /// AKN-tre-konverteringen (<see cref="LovdataKonverterer"/> i RegelIde.Kildekonvertering er
    /// unødvendig tung bare for en katalograd). Brukes av <see cref="LovdataKatalogTjeneste"/> til å
    /// (gjen)bygge en søkbar katalog — selve oppslaget på én lov (<see cref="HentRaaHtmlAsync"/>) er
    /// uendret og fortsatt datokode-only.
    /// </summary>
    public async IAsyncEnumerable<(string Datokode, string Tittel, string Type)> HentAlleOppforingerAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var (datokode, type, html) in HentAlleArkivEntrierAsync(ct))
        {
            var tittel = LesTittelBesteForsok(html)
                ?? throw new FormatException($"Oppføring '{datokode}' mangler påkrevd metadatafelt 'title'. Ingen gjettet fallback.");

            yield return (datokode, tittel, type);
        }
    }

    /// <summary>
    /// Beste-forsøk tittel-uttrekk fra Lovdatas metadata-blokk (<c>&lt;dd class="title"&gt;</c>) — helt
    /// uavhengig av <c>RegelIde.Kildekonvertering.LovdataKonverterer</c>s strukturelle AKN-parsing.
    /// Returnerer null (aldri kaster) i motsetning til bruken i <see cref="HentAlleOppforingerAsync"/>,
    /// som SELV velger å kaste når tittelen mangler — her, brukt av <see cref="LovdataFullimportTjeneste"/>
    /// til å likevel få en tittel med i importstatus-raden for dokumenter som IKKE lar seg AKN-konvertere,
    /// er tittelen en bekvemmelighet for triage, ikke en forutsetning.
    /// </summary>
    public static string? LesTittelBesteForsok(string html)
    {
        try
        {
            var dokument = new HtmlDocument();
            dokument.LoadHtml(html);
            var tittelNode = dokument.DocumentNode.SelectSingleNode("//dd[@class='title']");
            return tittelNode is null ? null : HtmlEntity.DeEntitize(tittelNode.InnerText.Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Full Lovdata-synkronisering (docs/13-backlog.md §6, "Daglig Lovdata-synkronisering") — som
    /// <see cref="HentAlleOppforingerAsync"/>, men yielder hele det rå HTML-dokumentet i stedet for
    /// bare tittelen, slik at hver oppføring kan konverteres til AKN direkte
    /// (<c>RegelIde.Kildekonvertering.LovdataKonverterer.Konverter</c>) uten et eget nettverkskall per
    /// dokument — <see cref="HentRaaHtmlAsync"/> laster ned HELE arkivet på nytt for hvert kall, helt
    /// uaktuelt i en løkke over tusenvis av dokumenter. Brukt av <see cref="LovdataFullimportTjeneste"/>.
    /// </summary>
    public IAsyncEnumerable<(string Datokode, string Type, string Html)> HentAlleDokumenterAsync(CancellationToken ct = default) =>
        HentAlleArkivEntrierAsync(ct);

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
