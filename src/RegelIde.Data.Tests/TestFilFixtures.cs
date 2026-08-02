using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace RegelIde.Data.Tests;

/// <summary>
/// Bygger ekte, gyldige PDF/Word-fixtures i minnet for tester av
/// <see cref="KunnskapsbibliotekTekstUtvinner"/> — ingen eksterne fixture-filer på disk. PDF-en bygges
/// for hånd (minimal, men strukturelt gyldig, med korrekt xref-tabell beregnet fra faktiske
/// byte-offset under bygging) siden PdfPig kun kan LESE, ikke skrive, PDF-er. Word-filen bygges med
/// selve <see cref="WordprocessingDocument"/>-writeren fra samme pakke <see cref="KunnskapsbibliotekTekstUtvinner"/>
/// leser med.
/// </summary>
internal static class TestFilFixtures
{
    /// <summary>Minimal gyldig PDF. Tomt innhold (<paramref name="tekst"/> == null) gir en side uten tekstlag, som en skannet fil.</summary>
    public static byte[] LagPdf(string? tekst)
    {
        var innholdStream = tekst is null ? "" : $"BT /F1 12 Tf 10 100 Td ({tekst}) Tj ET";
        var objekter = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /MediaBox [0 0 300 300] /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {innholdStream.Length} >>\nstream\n{innholdStream}\nendstream",
        };

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new int[objekter.Length];
        for (var i = 0; i < objekter.Length; i++)
        {
            offsets[i] = sb.Length;
            sb.Append($"{i + 1} 0 obj\n{objekter[i]}\nendobj\n");
        }

        var xrefStart = sb.Length;
        sb.Append($"xref\n0 {objekter.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }
        sb.Append($"trailer\n<< /Size {objekter.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    public static byte[] LagDocx(string tekst)
    {
        using var minne = new MemoryStream();
        using (var dokument = WordprocessingDocument.Create(minne, WordprocessingDocumentType.Document))
        {
            var hoveddel = dokument.AddMainDocumentPart();
            hoveddel.Document = new Document(new Body(new Paragraph(new Run(new Text(tekst)))));
            hoveddel.Document.Save();
        }
        return minne.ToArray();
    }
}
