using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace RegelIde.Data;

/// <summary>
/// Forsøker tekstuttrekk fra en opplastet fil til kunnskapsbiblioteket (byggesteg 5 runde 2) — IKKE
/// ekte OCR. Sjekken er bevisst enklere: finnes det allerede et tekstlag, hent det ut; hvis ikke
/// (typisk et rent skann) avvises filen med en tydelig feil i stedet for å bygge en
/// bilde-til-tekst-pipeline. Word-filer har praktisk talt alltid et tekstlag per definisjon — sjekken
/// er reelt sett en PDF-sjekk, men samme kodesti brukes for begge formater for konsistens.
/// </summary>
public static class KunnskapsbibliotekTekstUtvinner
{
    private const int MinimumTegn = 100;

    /// <exception cref="ArgumentException">Ukjent filendelse. Ingen gjettet fallback.</exception>
    /// <exception cref="InvalidOperationException">
    /// Filen mangler et tekstlag (sannsynligvis et skannet dokument), eller kunne ikke leses som
    /// gyldig PDF/Word.
    /// </exception>
    public static string PrøvUtvinnTekst(byte[] innhold, string filnavn)
    {
        var endelse = Path.GetExtension(filnavn).ToLowerInvariant();
        var tekst = endelse switch
        {
            ".pdf" => UtvinnFraPdf(innhold, filnavn),
            ".docx" => UtvinnFraDocx(innhold, filnavn),
            _ => throw new ArgumentException(
                $"Ukjent filtype '{endelse}' for '{filnavn}'. Kun PDF (.pdf) og Word (.docx) støttes. Ingen gjettet fallback."),
        };

        if (tekst.Trim().Length < MinimumTegn)
        {
            throw new InvalidOperationException(
                $"'{filnavn}' ser ut til å mangle et tekstlag (sannsynligvis et skannet dokument uten OCR) — " +
                "kan ikke brukes som KI-kontekst uten separat tekstgjenkjenning først.");
        }

        return tekst;
    }

    private static string UtvinnFraPdf(byte[] innhold, string filnavn)
    {
        try
        {
            using var dokument = PdfDocument.Open(innhold);
            return string.Join("\n", dokument.GetPages().Select(side => side.Text));
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Kunne ikke lese '{filnavn}' som en gyldig PDF: {ex.Message}", ex);
        }
    }

    private static string UtvinnFraDocx(byte[] innhold, string filnavn)
    {
        try
        {
            using var minne = new MemoryStream(innhold);
            using var dokument = WordprocessingDocument.Open(minne, false);
            return dokument.MainDocumentPart?.Document?.Body?.InnerText ?? "";
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Kunne ikke lese '{filnavn}' som en gyldig Word-fil (.docx): {ex.Message}", ex);
        }
    }
}
