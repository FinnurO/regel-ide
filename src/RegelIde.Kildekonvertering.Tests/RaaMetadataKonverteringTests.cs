namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Del A (lovdata-raa-metadata-runden, 2026-09-02) — rå, utrunkerte metadatafelt bevart ved siden av
/// de eksisterende, trunkerte feltene (IkrafttredelseRaa/KonsolidertDatoRaa), det helt nye
/// SistEndretVed-feltet ("Sist endret ved"), og RaaHtml (del B — den rå kilde-HTML-en bevart gjennom
/// hele KonverteringResultat i stedet for kastet rett etter parsing).
/// </summary>
public class RaaMetadataKonverteringTests
{
    [Fact]
    public void Alkoholforskriften_bevarer_kompound_dateInForce_uforkortet_ved_siden_av_trunkert_dato()
    {
        // Ekte, bekreftet kompound verdi (data/kilder/raw-lovdata/alkoholforskriften-FOR-2005-06-08-538.html):
        // "2005-07-01, 2006-01-01" — FørsteDato beholder stille kun FØRSTE dato-treff.
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 9, 2));

        Assert.Equal(new DateOnly(2005, 7, 1), resultat.Metadata.Ikrafttredelse);
        Assert.Equal("2005-07-01, 2006-01-01", resultat.Metadata.IkrafttredelseRaa);
    }

    [Fact]
    public void Alkoholloven_bevarer_ikke_kompound_dateInForce_uendret_nar_kilden_ikke_er_kompound()
    {
        // Ekte, IKKE-kompound verdi (alkoholloven-LOV-1989-06-02-27.html): "1990-01-01" — her er
        // trunkert og rå verdi identiske, siden det ikke er noe å trunkere.
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 9, 2));

        Assert.Equal(new DateOnly(1990, 1, 1), resultat.Metadata.Ikrafttredelse);
        Assert.Equal("1990-01-01", resultat.Metadata.IkrafttredelseRaa);
    }

    [Fact]
    public void KonsolidertDatoRaa_bevares_ved_siden_av_trunkert_dato()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 9, 2));

        Assert.Equal(new DateOnly(2026, 7, 20), resultat.Metadata.KonsolidertDato);
        Assert.Equal("2026-07-20", resultat.Metadata.KonsolidertDatoRaa);
    }

    [Fact]
    public void SistEndretVed_fanges_som_ra_tekst()
    {
        // Ekte innhold (alkoholloven-LOV-1989-06-02-27.html):
        // <dd class="lastChangedBy"><a href="lov/2026-05-29-21">lov/2026-05-29-21</a> fra 2026-07-20</dd>
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 9, 2));

        Assert.Equal("lov/2026-05-29-21 fra 2026-07-20", resultat.Metadata.SistEndretVed);
    }

    [Fact]
    public void Lov_uten_dateInForce_lastChangeInForce_eller_lastChangedBy_gir_null_ikke_en_feil()
    {
        // Alle 8 fixturer i data/kilder/raw-lovdata/ har faktisk disse feltene (bekreftet
        // 2026-09-02) — fravær simuleres derfor målrettet ved å fjerne dt+dd-parene fra en ekte
        // fixture, samme mønster som EdgeCaseTests.cs bruker for andre valgfrie header-felt.
        var html = Testdata.LesAlkoholloven()
            .Replace("<dt class=\"dateInForce\">I kraft fra</dt><dd class=\"dateInForce\">1990-01-01</dd>", "")
            .Replace(
                "<dt class=\"lastChangeInForce\">Ikrafttredelse av siste endring</dt><dd class=\"lastChangeInForce\">2026-07-20</dd>",
                "")
            .Replace(
                "<dt class=\"lastChangedBy\">Sist endret ved</dt><dd class=\"lastChangedBy\">" +
                "<a href=\"lov/2026-05-29-21\">lov/2026-05-29-21</a> fra 2026-07-20</dd>",
                "");

        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2));

        Assert.Null(resultat.Metadata.Ikrafttredelse);
        Assert.Null(resultat.Metadata.IkrafttredelseRaa);
        Assert.Null(resultat.Metadata.KonsolidertDato);
        Assert.Null(resultat.Metadata.KonsolidertDatoRaa);
        Assert.Null(resultat.Metadata.SistEndretVed);
    }

    [Fact]
    public void RaaHtml_bevarer_kilde_html_uendret_gjennom_hele_pipelinen()
    {
        var html = Testdata.LesAlkoholloven();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2));

        Assert.Equal(html, resultat.RaaHtml);
    }
}
