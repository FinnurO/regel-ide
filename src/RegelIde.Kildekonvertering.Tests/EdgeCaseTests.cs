namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Syntetiske edge cases som ikke forekommer i de to ekte testdokumentene, men som ble etterlyst av
/// en ekstern code review (Copilot): robusthet mot tomt/ufullstendig input, norske bokstaver i
/// slugifisering, og flere fotnoter på samme paragraf. Konstruert ved målrettede endringer av den ekte
/// alkoholforskriften-HTML-en, ikke fritt oppdiktet markup — strukturen er dermed fortsatt realistisk.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void Tom_html_gir_forutsigbar_feil_ikke_stille_feilslag_eller_krasj()
    {
        var ex = Assert.Throws<FormatException>(() => LovdataKonverterer.Konverter(""));
        Assert.Contains("documentHeader", ex.Message);
    }

    [Fact]
    public void Html_uten_pakrevd_metadatafelt_feiler_forutsigbart_uten_gjettet_fallback()
    {
        var htmlUtenDepartement = Testdata.LesAlkoholforskriften()
            .Replace("<dt class=\"ministry\">Departement</dt><dd class=\"ministry\"><ul><li>Helse- og omsorgsdepartementet</li></ul></dd>", "");

        var ex = Assert.Throws<FormatException>(() => LovdataKonverterer.Konverter(htmlUtenDepartement));
        Assert.Contains("ministry", ex.Message);
        Assert.Contains("Ingen gjettet fallback", ex.Message);
    }

    [Fact]
    public void Slugifisering_haandterer_norske_bokstaver_ae_oe_aa()
    {
        var html = Testdata.LesAlkoholforskriften()
            .Replace("Helse- og omsorgsdepartementet", "Økonomi- og æøådepartementet");

        var resultat = LovdataKonverterer.Konverter(html);

        Assert.Equal("okonomi-og-aeoadepartementet", resultat.Metadata.FrbrAuthorHref);
        Assert.Equal("Økonomi- og æøådepartementet", resultat.Metadata.FrbrAuthorShowAs);
        // eId-attributter i AKN-XML må uansett være ASCII/URI-sikre
        Assert.DoesNotMatch("[æøåÆØÅ]", resultat.Metadata.FrbrAuthorHref);
    }

    [Fact]
    public void Flere_fotnoter_pa_samme_paragraf_haandteres_i_riktig_rekkefolge()
    {
        // § 3-1 i alkoholloven har i utgangspunktet én fotnote — dupliserer footnote-elementet i
        // footeren for å simulere to fotnoter på samme paragraf (ikke observert i de ekte dokumentene).
        var enkelFotnote = "<article class=\"footnote\" data-name=\"1\" data-unique-footnote-counter=\"1\">" +
            "<span class=\"footnoteLabel\">1</span> Jf. <a href=\"lov/1992-11-27-109/eøsl/a16\">EØS-avtalen art. 16</a>.</article>";
        var toFotnoter = enkelFotnote + "<article class=\"footnote\" data-name=\"2\" data-unique-footnote-counter=\"2\">" +
            "<span class=\"footnoteLabel\">2</span> Se også forarbeidene.</article>";

        var html = Testdata.LesAlkoholloven().Replace(enkelFotnote, toFotnoter);
        Assert.Contains(toFotnoter, html); // sjekker at replace faktisk traff (ellers er testen meningsløs)

        var resultat = LovdataKonverterer.Konverter(html);
        var paragraf = resultat.Noder.Single(n => n.Eid == "https://lovdata.no/eli/lov/1989/06/02/27/nor/§3-1");

        Assert.Equal(2, paragraf.Fotnoter.Count);
        Assert.Equal("1", paragraf.Fotnoter[0].Etikett);
        Assert.Equal("Jf. EØS-avtalen art. 16.", paragraf.Fotnoter[0].Tekst);
        Assert.Equal("2", paragraf.Fotnoter[1].Etikett);
        Assert.Equal("Se også forarbeidene.", paragraf.Fotnoter[1].Tekst);
    }

    /// <summary>
    /// "Kap. N." (forkortet ord) i stedet for "Kapittel N." — bekreftet ekte variant, funnet under
    /// full Lovdata-synkronisering 2026-08-20 (docs/13-backlog.md §6, se f.eks. tannhelsetjenesteloven
    /// LOV-1983-06-03-54). Simulert her ved en målrettet erstatning i den ekte, fungerende
    /// alkoholloven-HTML-en, samme mønster som resten av denne testklassen.
    /// </summary>
    [Fact]
    public void Kapitteloverskrift_med_forkortet_kap_og_punktum_gir_riktig_tittel()
    {
        var ektHeading = "<h2>Kapittel 1. Alminnelige bestemmelser.</h2>";
        var forkortetHeading = "<h2>Kap. 1. Alminnelige bestemmelser.</h2>";
        var html = Testdata.LesAlkoholloven().Replace(ektHeading, forkortetHeading);
        Assert.Contains(forkortetHeading, html);

        var resultat = LovdataKonverterer.Konverter(html);
        var kapittel1 = resultat.Noder.Single(n => n.NodeType == NodeType.Kapittel && n.Nummer == "1");

        Assert.Equal("Alminnelige bestemmelser.", kapittel1.Overskrift);
    }

    /// <summary>
    /// "Kap N." (forkortet ord, UTEN punktum etter selve forkortelsen) — den andre bekreftede
    /// varianten fra samme funn, sett i samme dokument (tannhelsetjenesteloven, forskjellige kapitler).
    /// </summary>
    [Fact]
    public void Kapitteloverskrift_med_forkortet_kap_uten_punktum_gir_riktig_tittel()
    {
        var ektHeading = "<h2>Kapittel 2. Innførsel og utførsel.</h2>";
        var forkortetHeading = "<h2>Kap 2. Innførsel og utførsel.</h2>";
        var html = Testdata.LesAlkoholloven().Replace(ektHeading, forkortetHeading);
        Assert.Contains(forkortetHeading, html);

        var resultat = LovdataKonverterer.Konverter(html);
        var kapittel2 = resultat.Noder.Single(n => n.NodeType == NodeType.Kapittel && n.Nummer == "2");

        Assert.Equal("Innførsel og utførsel.", kapittel2.Overskrift);
    }
}
