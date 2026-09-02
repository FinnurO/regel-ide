namespace RegelIde.Kildekonvertering;

/// <summary>
/// Offentlig inngang til konverteringspipelinen (docs/08-byggesteg1-teknisk-design.md §3.1).
/// Dekker steg 3-7 (parse, metadata, nodetre, kryssreferanser, AKN-serialisering). Steg 1 (hent) og
/// steg 8-10 (skriv til DB, sett status, menneskelig verifisering) forutsetter et faktisk bibliotek/
/// databaselag og er bevisst utenfor denne byggeøktens scope (kun konverteringspipelinen, jf. veikartet).
/// </summary>
public static class LovdataKonverterer
{
    /// <summary>
    /// Kjører steg 3-7: parser allerede UTF-8-dekodet Lovdata-HTML til nodetre + FRBR-metadata, samler
    /// kryssreferanser, og serialiserer kanonisk AKN-XML. Referansielt transparent i alt unntatt
    /// <paramref name="importDato"/> (§3) — samme <paramref name="kildeHtmlUtf8"/> gir alltid samme
    /// metadata/noder/referanser/AKN-body.
    /// </summary>
    public static KonverteringResultat Konverter(string kildeHtmlUtf8, DateOnly? importDato = null)
    {
        var dato = importDato ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var parsed = LovdataHtmlParser.Parse(kildeHtmlUtf8);
        var aknXml = AknXmlSkriver.Skriv(parsed.Metadata, parsed.Noder, dato);

        return new KonverteringResultat
        {
            Metadata = parsed.Metadata,
            Noder = parsed.Noder,
            Referanser = parsed.Referanser,
            Hjemler = parsed.Hjemler,
            Endringer = parsed.Endringer,
            AknXml = aknXml,
            ImportDato = dato,
            RaaHtml = kildeHtmlUtf8,
        };
    }
}
