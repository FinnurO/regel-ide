namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Asserter mot alkoholforskriften — spesielt de nøstede punktlistene (§6-2, §14-3) som avdekket at
/// et punkt kan ha flere direkte legalP-"ledd" og at punktlister kan nøstes vilkårlig dypt (§3.2).
/// </summary>
public class AlkoholforskriftenKonverteringTests
{
    private static readonly KonverteringResultat Resultat =
        LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 7, 23));

    private const string ForskriftEli = "https://lovdata.no/eli/forskrift/2005/06/08/538/nor";

    [Fact]
    public void Metadata_er_korrekt_avledet_fra_header()
    {
        Assert.Equal(Kildetype.Forskrift, Resultat.Metadata.Kildetype);
        Assert.Equal(ForskriftEli, Resultat.Metadata.Eli);
        Assert.Equal("Forskrift om omsetning av alkoholholdig drikk mv. (alkoholforskriften)", Resultat.Metadata.Tittel);
        Assert.Equal("Alkoholforskriften", Resultat.Metadata.Kortnavn);
        Assert.Equal("Helse- og omsorgsdepartementet", Resultat.Metadata.AnsvarligDepartement);
        // Forskrift: FRBRauthor er departementet, ikke Stortinget (Vedlegg A.1)
        Assert.Equal("helse-og-omsorgsdepartementet", Resultat.Metadata.FrbrAuthorHref);
    }

    [Fact]
    public void Punkt_med_nostet_underliste_har_kun_egen_innledningstekst_som_bladtekst()
    {
        // § 6-2 ledd-1 punkt-1: "Salg:" etterfulgt av en nøstet liste med to satser.
        var punkt1Eid = $"{ForskriftEli}/§6-2/ledd-1/punkt-1";
        var punkt1 = Resultat.Noder.Single(n => n.Eid == punkt1Eid);
        Assert.Equal("Salg:", punkt1.Tekst);

        var underpunkt1 = Resultat.Noder.Single(n => n.Eid == $"{punkt1Eid}/punkt-1");
        Assert.Equal(NodeType.Punkt, underpunkt1.NodeType);
        Assert.Equal("0,26 kr pr. vareliter for alkoholholdig drikk i gruppe 1", underpunkt1.Tekst);

        var underpunkt2 = Resultat.Noder.Single(n => n.Eid == $"{punkt1Eid}/punkt-2");
        Assert.Equal("0,75 kr pr. vareliter for alkoholholdig drikk i gruppe 2", underpunkt2.Tekst);
    }

    [Fact]
    public void Punkt_med_flere_direkte_legalP_konkatenerer_tekst_i_dokumentrekkefolge()
    {
        // § 14-3 ledd-1 punkt-14: tekst+underliste (a/b), så en oppfølgende setning som andre legalP.
        var punkt14Eid = $"{ForskriftEli}/§14-3/ledd-1/punkt-14";
        var punkt14 = Resultat.Noder.Single(n => n.Eid == punkt14Eid);
        Assert.StartsWith("På hjemmesidene til produsenter og grossister", punkt14.Tekst);
        Assert.EndsWith("Nærmere krav til innhold, utforming og plassering av opplysningene kan fastsettes av Helsedirektoratet.", punkt14.Tekst);
        // Skjøtepunktet mellom de to legalP-blokkene skal ha nøyaktig ett mellomrom, ikke null
        // (kildeteksten har INGEN whitespace mellom </article></ol></article> og neste <article>)
        // og ikke to (fra dobbel mellomrom-innsetting ved både liste-hopp og legalP-skjøt).
        Assert.Contains("vilkår: ", punkt14.Tekst);
        Assert.DoesNotContain("  ", punkt14.Tekst);

        // De to underpunktene (a/b) fra <ol type="a"> midt i teksten er egne noder under punkt-14
        Assert.Contains(Resultat.Noder, n => n.Eid == $"{punkt14Eid}/punkt-1");
        Assert.Contains(Resultat.Noder, n => n.Eid == $"{punkt14Eid}/punkt-2");
    }

    [Fact]
    public void Tekst_etter_hoppet_over_liste_pa_leddniva_bevares_med_mellomrom()
    {
        // § 7-2 ledd-1: tekst før listen ("… herunder"), en nøstet liste, og en
        // <p class="leddfortsettelse"> med tekst etter listen — reell case som avdekket at
        // teksten ellers smelter sammen uten mellomrom ("herunderDet skal …").
        var ledd = Resultat.Noder.Single(n => n.Eid == $"{ForskriftEli}/§7-2/ledd-1");
        Assert.Equal(
            "Folkehelseinstituttet kan i samarbeid med Statistisk sentralbyrå bestemme hvordan offisiell " +
            "statistikk skal utarbeides, herunder Det skal legges vekt på statistikkhensyn og på hensynet " +
            "til de berørte parters kostnader ved innhenting av opplysninger og utarbeidelse av statistikk.",
            ledd.Tekst);
        Assert.DoesNotContain("  ", ledd.Tekst);
    }

    [Fact]
    public void Konvertering_er_referensielt_transparent()
    {
        var andreGangen = LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 7, 23));
        Assert.Equal(Resultat.AknXml, andreGangen.AknXml);
    }
}
