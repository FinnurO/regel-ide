namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Header-metadatafeltet &lt;dt class="changesToDocuments"&gt;Endrer&lt;/dt&gt; (docs-kommentar i
/// LovdataHtmlParser.HentEndringer/RettskildeEndring) — hvilke(t) andre dokument(er) en rettskilde
/// ENDRER. TIL FORSKJELL FRA Hjemmel-feltet (kun bekreftet ekte i alkoholforskriften), er dette feltet
/// bekreftet ekte i BÅDE alkoholloven OG alkoholforskriften (og tre andre fixturer, se
/// LovdataHtmlParser.HentEndringer sin klassekommentar) — begge har faktisk innhold med lenker,
/// stikk i strid med den opprinnelige (ubekreftede) antagelsen om at ingen fixturer hadde innhold der.
/// </summary>
public class EndringKonverteringTests
{
    [Fact]
    public void Alkoholloven_har_to_endringsreferanser_uten_paragrafnummer()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 9, 2));

        // Ekte innhold i <dd class="changesToDocuments"> (se data/kilder/raw-lovdata/
        // alkoholloven-LOV-1989-06-02-27.html): "lov/1927-04-05" og "lov/1900-05-31-5", begge rene
        // dokument-nivå-lenker (ingen §-suffiks).
        Assert.Equal(2, resultat.Endringer.Count);
        Assert.Equal("https://lovdata.no/eli/lov/1927/04/05/nor", resultat.Endringer[0].Eid);
        Assert.Equal(0, resultat.Endringer[0].Sorteringsrekkefolge);
        Assert.Equal("https://lovdata.no/eli/lov/1900/05/31/5/nor", resultat.Endringer[1].Eid);
        Assert.Equal(1, resultat.Endringer[1].Sorteringsrekkefolge);
    }

    [Fact]
    public void Alkoholforskriften_har_en_endringsreferanse_til_en_annen_forskrift()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 9, 2));

        // Ekte innhold: "forskrift/1997-12-11-1292" (se data/kilder/raw-lovdata/
        // alkoholforskriften-FOR-2005-06-08-538.html).
        Assert.Single(resultat.Endringer);
        Assert.Equal("https://lovdata.no/eli/forskrift/1997/12/11/1292/nor", resultat.Endringer[0].Eid);
        Assert.Equal(0, resultat.Endringer[0].Sorteringsrekkefolge);
    }

    [Fact]
    public void Forvaltningsloven_uten_changesToDocuments_innhold_gir_tom_endringsliste()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 9, 2));

        Assert.Empty(resultat.Endringer);
    }

    [Fact]
    public void Endringslenke_med_paragrafnummer_kaster_i_stedet_for_a_gjette()
    {
        // Syntetisk: en (ubekreftet) Endrer-lenke MED paragrafnummer — motsatt av Hjemmel-feltet, der
        // ALLE bekreftede Endrer-forekomster mangler paragrafnummer (se HentEndringer-kommentaren).
        // Konstruert ved målrettet endring av den ekte alkoholloven-HTML-en.
        var html = Testdata.LesAlkoholloven()
            .Replace(
                "<li><a href=\"lov/1927-04-05\">lov/1927-04-05</a></li>",
                "<li><a href=\"lov/1927-04-05/§1-1\">lov/1927-04-05/§1-1</a></li>");

        var ex = Assert.Throws<FormatException>(() => LovdataKonverterer.Konverter(html));
        Assert.Contains("paragrafnummer", ex.Message);
        Assert.Contains("Ingen gjettet fallback", ex.Message);
    }

    [Fact]
    public void Ukjent_endringslenke_monster_kaster_i_stedet_for_a_gjette()
    {
        var html = Testdata.LesAlkoholloven()
            .Replace(
                "<li><a href=\"lov/1927-04-05\">lov/1927-04-05</a></li>",
                "<li><a href=\"avtale/1992-11-27-109\">avtale/1992-11-27-109</a></li>");

        var ex = Assert.Throws<FormatException>(() => LovdataKonverterer.Konverter(html));
        Assert.Contains("Ingen gjettet fallback", ex.Message);
    }

    [Fact]
    public void Endringslenke_uten_href_kaster_i_stedet_for_a_gjette()
    {
        var html = Testdata.LesAlkoholloven()
            .Replace(
                "<li><a href=\"lov/1927-04-05\">lov/1927-04-05</a></li>",
                "<li><a>lov/1927-04-05</a></li>");

        var ex = Assert.Throws<FormatException>(() => LovdataKonverterer.Konverter(html));
        Assert.Contains("href", ex.Message);
        Assert.Contains("Ingen gjettet fallback", ex.Message);
    }
}
