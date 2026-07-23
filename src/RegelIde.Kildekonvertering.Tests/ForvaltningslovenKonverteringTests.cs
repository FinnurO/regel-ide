namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Forvaltningsloven — nevnt som kandidat "infrastrukturlov" i 06-veikart.md §3.1.1. Avdekket to nye
/// strukturelle varianter i ekte data: kapitler nummerert med romertall (ikke bare underinndelinger),
/// og en dokumentnivå-merknad (varsel om fremtidig opphevelse av hele loven) direkte i documentBody.
/// </summary>
public class ForvaltningslovenKonverteringTests
{
    private static readonly KonverteringResultat Resultat =
        LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 7, 23));

    private const string LovEli = "https://lovdata.no/eli/lov/1967/02/10/nor";

    [Fact]
    public void Metadata_er_korrekt_avledet_fra_header()
    {
        Assert.Equal(Kildetype.Lov, Resultat.Metadata.Kildetype);
        Assert.Equal(LovEli, Resultat.Metadata.Eli);
        Assert.Equal("LOV-1967-02-10", Resultat.Metadata.Datokode);
        Assert.Equal("Lov om behandlingsmåten i forvaltningssaker (forvaltningsloven)", Resultat.Metadata.Tittel);
        Assert.Equal("stortinget", Resultat.Metadata.FrbrAuthorHref);
    }

    [Fact]
    public void Kapittel_med_romertall_som_hovednummerering_far_korrekt_eId()
    {
        // I motsetning til alkoholloven (der romertall kun brukes til underinndelinger inni et
        // arabisk-nummerert kapittel) er forvaltningslovens TOPPNIVÅ-kapitler selv romertallsnummerert.
        var kapittel1 = Resultat.Noder.Single(n => n.NodeType == NodeType.Kapittel && n.Nummer == "I");
        Assert.Equal("kap-I", kapittel1.Eid);
        Assert.Equal("Lovens område. Definisjoner.", kapittel1.Overskrift);
    }

    [Fact]
    public void Paragraf_1_har_ikke_kapittelprefiks_i_paragrafnummeret()
    {
        // Forvaltningsloven nummererer paragrafer løpende (§ 1, § 2, …), ikke kapittel-paragraf
        // (§ 1-1) som alkoholloven — bekrefter at eId-konstruksjonen ikke antar bindestrek-formatet.
        var eid = $"{LovEli}/§1";
        var paragraf = Resultat.Noder.Single(n => n.Eid == eid);
        Assert.Equal("(lovens generelle virkeområde).", paragraf.Overskrift);

        var ledd1 = Resultat.Noder.Single(n => n.Eid == $"{eid}/ledd-1");
        Assert.StartsWith("Loven gjelder den virksomhet som drives av forvaltningsorganer", ledd1.Tekst);
    }

    [Fact]
    public void Konvertering_er_referensielt_transparent()
    {
        var andreGangen = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 7, 23));
        Assert.Equal(Resultat.AknXml, andreGangen.AknXml);
    }
}
