namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Header-metadatafeltet &lt;dt class="basedOn"&gt;Hjemmel&lt;/dt&gt; (docs-kommentar i
/// LovdataHtmlParser.HentHjemler) — dokumentnivå-referanser til hvilken paragraf i hvilken lov et
/// dokument er hjemlet i. Bekreftet ekte KUN på alkoholforskriften blant de åtte fixturene i
/// data/kilder/raw-lovdata/ (full gjennomgang 2026-08-30) — alle syv lov-fixturer har 0 forekomster.
/// </summary>
public class HjemmelKonverteringTests
{
    private const string AlkohollovenEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";

    [Fact]
    public void Alkoholforskriften_har_tjue_hjemmelreferanser_alle_til_alkoholloven()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 7, 23));

        Assert.Equal(21, resultat.Hjemler.Count);
        Assert.All(resultat.Hjemler, h => Assert.StartsWith(AlkohollovenEli + "/", h.Eid));
    }

    [Fact]
    public void Hjemmelreferanser_bevarer_kildens_rekkefolge_og_eid_format()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 7, 23));

        // Første og siste <a href="lov/1989-06-02-27/§…"> i den ekte kilde-HTML-en (se
        // data/kilder/raw-lovdata/alkoholforskriften-FOR-2005-06-08-538.html sitt basedOn-felt).
        Assert.Equal($"{AlkohollovenEli}/§1-2", resultat.Hjemler[0].Eid);
        Assert.Equal(0, resultat.Hjemler[0].Sorteringsrekkefolge);
        Assert.Equal($"{AlkohollovenEli}/§10-5", resultat.Hjemler[^1].Eid);
        Assert.Equal(20, resultat.Hjemler[^1].Sorteringsrekkefolge);

        // Samme eId-format som en vanlig paragraf-node/-referanse (LovdataIdentifikatorer.ParagrafEid)
        // — bevisst gjenbrukt, ikke et eget hjemmel-spesifikt format (se RettskildeHjemmel-kommentaren).
        Assert.Contains(resultat.Hjemler, h => h.Eid == $"{AlkohollovenEli}/§1-7c");
    }

    [Fact]
    public void Lov_uten_basedOn_felt_gir_tom_hjemmelliste_ikke_en_feil()
    {
        // Ingen av de syv lov-fixturene har <dt class="basedOn"> i det hele tatt — bekreftet med
        // `grep -c basedOn` mot samtlige filer i data/kilder/raw-lovdata/ under research-fasen.
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 23));

        Assert.Empty(resultat.Hjemler);
    }

    [Fact]
    public void Forvaltningsloven_har_heller_ingen_hjemmelreferanser()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 7, 23));

        Assert.Empty(resultat.Hjemler);
    }

    [Fact]
    public void Hjemmellenke_uten_paragrafnummer_gir_dokumentnivaa_hjemmel()
    {
        // Syntetisk: en hjemmel til en HEL lov (ingen §-suffiks på href-en) — bekreftet ekte og
        // OVERRASKENDE VANLIG (1711 av 5882 dokumenter, full korpusgjennomgang 2026-09-02, dominerende
        // blant delegeringsforskrifter — se HentHjemler-kommentaren), ikke et avvik. Konstruert ved
        // målrettet endring av den ekte alkoholforskriften-HTML-en (samme mønster som
        // EdgeCaseTests.cs), ikke fritt oppdiktet markup.
        var html = Testdata.LesAlkoholforskriften()
            .Replace(
                "<li><a href=\"lov/1989-06-02-27/§1-2\">lov/1989-06-02-27/§1-2</a></li>",
                "<li><a href=\"lov/1989-06-02-27\">lov/1989-06-02-27</a></li>");

        var resultat = LovdataKonverterer.Konverter(html);

        // Samme dokument-ELI-format som RettskildeEndring.Eid alltid har (ingen paragraf-suffiks) —
        // erstatter den ene fjernede paragraf-spesifikke hjemmelen 1:1, resten uendret.
        Assert.Equal(21, resultat.Hjemler.Count);
        Assert.Equal(AlkohollovenEli, resultat.Hjemler[0].Eid);
        Assert.Equal(0, resultat.Hjemler[0].Sorteringsrekkefolge);
    }

    [Fact]
    public void Hjemmellenke_til_hel_forskrift_uten_paragrafnummer_gir_dokumentnivaa_hjemmel()
    {
        // Minimal, målrettet regresjonstest for det EKTE, tidligere blokkerte tilfellet: import av
        // "Forskrift om skipsmedisin" (FOR-2001-03-09-439) feiler fordi dens ekte Hjemmel-felt har
        // <a href="forskrift/1969-06-13-3">forskrift/1969-06-13-3</a> — en hjemmel til en HEL forskrift
        // (til forskjell fra testen over, som dekker en hel LOV). Samme mekanisme
        // (LovdataHrefTolker/AvledEliFraDatokode) er kildetype-uavhengig, men denne testen dekker
        // forskrift-grenen eksplisitt i stedet for å anta at lov-testen over dekker begge.
        var html = Testdata.LesAlkoholforskriften()
            .Replace(
                "<li><a href=\"lov/1989-06-02-27/§1-2\">lov/1989-06-02-27/§1-2</a></li>",
                "<li><a href=\"forskrift/1969-06-13-3\">forskrift/1969-06-13-3</a></li>");

        var resultat = LovdataKonverterer.Konverter(html);

        Assert.Equal(21, resultat.Hjemler.Count);
        Assert.Equal("https://lovdata.no/eli/forskrift/1969/06/13/3/nor", resultat.Hjemler[0].Eid);
        Assert.Equal(0, resultat.Hjemler[0].Sorteringsrekkefolge);
    }

    [Fact]
    public void Ukjent_hjemmellenke_monster_kaster_i_stedet_for_a_gjette()
    {
        // Syntetisk: et href-mønster utenfor lov/forskrift-prefikset (samme prinsipp som LovdataHrefTolker
        // sin klassekommentar sier gjelder EØS-/EU-henvisninger andre steder i header-metadataen).
        var html = Testdata.LesAlkoholforskriften()
            .Replace(
                "<li><a href=\"lov/1989-06-02-27/§1-2\">lov/1989-06-02-27/§1-2</a></li>",
                "<li><a href=\"avtale/1992-11-27-109\">avtale/1992-11-27-109</a></li>");

        var ex = Assert.Throws<FormatException>(() => LovdataKonverterer.Konverter(html));
        Assert.Contains("Ingen gjettet fallback", ex.Message);
    }

    // ---------- Ledd-presisjon (issue #217, 2026-09-10) ----------

    [Fact]
    public void Hjemmel_med_ledd_i_kilden_barer_presiseringen_videre()
    {
        // NTNUs ph.d.-forskrift: «§ 13-1 fjerde ledd». basedOn har BARE paragrafen, miscInformation har
        // ledd-et. Uten sammenstillingen av de to feltene forsvinner «fjerde ledd» — det var issue #217.
        var resultat = LovdataKonverterer.Konverter(Testdata.LesNtnuPhdForskriften(), new DateOnly(2026, 9, 10));

        var hjemmel = Assert.Single(resultat.Hjemler);
        Assert.Equal("https://lovdata.no/eli/lov/2024/03/08/9/nor/§13-1", hjemmel.Eid);
        Assert.Equal("ledd/4", hjemmel.Presisering);
    }

    [Fact]
    public void Hjemmel_uten_presisering_i_kilden_far_ingen_presisering()
    {
        // Alkoholforskriftens 21 hjemler har ingen presisering noe sted — paragrafnivå er da RIKTIG,
        // ikke en mangel (issue #217 «ingen gjettet ledd-presisjon»). Denne testen er vakten mot at
        // sammenstillingen skulle finne på å tilordne en presisering til nærmeste paragraf.
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 7, 23));

        Assert.All(resultat.Hjemler, h => Assert.Null(h.Presisering));
    }

    [Fact]
    public void Presisering_hentes_bare_fra_hjemmelslinja_ikke_fra_andre_metadatalinjer()
    {
        // «Annet om dokumentet» rommer flere linjer. En ledd-lenke i en linje som IKKE er hjemmelslinja
        // skal ikke leses som hjemmelens presisering — her flyttes ledd-lenken til en «Endres av»-linje,
        // og presiseringen skal da forsvinne helt.
        var html = Testdata.LesNtnuPhdForskriften()
            .Replace("<strong>Hjemmel:</strong>", "<strong>Endres av:</strong>");

        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 10));

        Assert.Null(Assert.Single(resultat.Hjemler).Presisering);
    }

    [Fact]
    public void Presiseringsoppslaget_fra_raa_html_finner_samme_ledd_som_parsingen()
    {
        // Etterfyllingstjenesten (HjemmelPresisjonEtterfyllingTjeneste) går denne veien inn, mot rå
        // HTML lagret ved import — den skal se NØYAKTIG det samme som en fersk import ser.
        var oppslag = LovdataHtmlParser.TolkHjemmelPresiseringer(Testdata.LesNtnuPhdForskriften());

        Assert.Equal("ledd/4", oppslag["https://lovdata.no/eli/lov/2024/03/08/9/nor/§13-1"]);
    }

    [Fact]
    public void Presiseringsoppslaget_ignorerer_eu_og_avtale_lenker()
    {
        // Hjemmelslinja inneholder målt 756 eu/- og 53 avtale/-lenker på tvers av korpuset. De er ikke
        // norske rettskilder, og oppslaget her er for PRESISERING av hjemler basedOn alt har funnet —
        // ikke en ny hjemmelskilde. De skal falle stille ut, ikke kaste.
        var html = Testdata.LesNtnuPhdForskriften()
            .Replace("<strong>Hjemmel:</strong>",
                "<strong>Hjemmel:</strong> <a href=\"eu/32007l0045\">2007/45/EF</a>");

        var oppslag = LovdataHtmlParser.TolkHjemmelPresiseringer(html);

        Assert.Equal("ledd/4", Assert.Single(oppslag).Value);
    }
}
