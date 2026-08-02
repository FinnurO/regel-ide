namespace RegelIde.Data.Tests;

/// <summary>
/// Tekstlag-sjekken (byggesteg 5 runde 2) — IKKE ekte OCR. Fixtures bygges i minnet av
/// <see cref="TestFilFixtures"/>, ingen filer på disk.
/// </summary>
public class KunnskapsbibliotekTekstUtvinnerTests
{
    private const string LangTekst =
        "Dette er en ekte tekst-PDF for testing av tekstuttrekk og kunnskapsbiblioteket i regel-ide. " +
        "Teksten er bevisst gjort lang nok til å passere terskelen for hva som regnes som et tekstlag.";

    [Fact]
    public void Pdf_med_tekstlag_gir_utvunnet_tekst()
    {
        var pdf = TestFilFixtures.LagPdf(LangTekst);

        var tekst = KunnskapsbibliotekTekstUtvinner.PrøvUtvinnTekst(pdf, "test.pdf");

        Assert.Contains("ekte tekst-PDF", tekst);
    }

    [Fact]
    public void Pdf_uten_tekstlag_avvises_som_sannsynlig_skann()
    {
        var skannetPdf = TestFilFixtures.LagPdf(tekst: null);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            KunnskapsbibliotekTekstUtvinner.PrøvUtvinnTekst(skannetPdf, "skann.pdf"));
        Assert.Contains("tekstlag", ex.Message);
    }

    [Fact]
    public void Docx_med_tekst_gir_utvunnet_tekst()
    {
        var docx = TestFilFixtures.LagDocx(LangTekst);

        var tekst = KunnskapsbibliotekTekstUtvinner.PrøvUtvinnTekst(docx, "test.docx");

        Assert.Contains("ekte tekst-PDF", tekst);
    }

    [Fact]
    public void Ukjent_filendelse_kaster_tydelig_feil_uten_gjettet_fallback()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            KunnskapsbibliotekTekstUtvinner.PrøvUtvinnTekst([1, 2, 3], "dokument.txt"));
        Assert.Contains("Kun PDF", ex.Message);
    }

    [Fact]
    public void Korrupt_pdf_kaster_tydelig_feil()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            KunnskapsbibliotekTekstUtvinner.PrøvUtvinnTekst("dette er ikke en pdf"u8.ToArray(), "korrupt.pdf"));
        Assert.Contains("gyldig PDF", ex.Message);
    }
}
