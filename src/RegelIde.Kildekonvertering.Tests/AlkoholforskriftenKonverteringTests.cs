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
        Assert.Equal(["Helse- og omsorgsdepartementet"], Resultat.Metadata.AnsvarligDepartement);
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
    public void Punkt_med_flere_direkte_legalP_far_teksten_etter_underlista_som_avslutning()
    {
        // § 14-3 ledd-1 punkt-14: tekst+underliste, så en oppfølgende setning som andre legalP.
        // [ENDRET, avslutningsnode-runden, 2026-10-09, issue #361] Den oppfølgende setningen står ETTER
        // underlista i kilden og ble før limt inn i punktets egen tekst (og dermed vist foran
        // underpunktene). Nå er den en avslutningsnode under punktet, sortert etter underpunktene.
        var punkt14Eid = $"{ForskriftEli}/§14-3/ledd-1/punkt-14";
        var punkt14 = Resultat.Noder.Single(n => n.Eid == punkt14Eid);
        Assert.StartsWith("På hjemmesidene til produsenter og grossister", punkt14.Tekst);
        Assert.DoesNotContain("Nærmere krav til innhold", punkt14.Tekst);
        Assert.DoesNotContain("  ", punkt14.Tekst);

        // Underpunktene fra <ol> midt i teksten er egne noder under punkt-14
        var underpunkter = Resultat.Noder.Where(n => n.ParentEid == punkt14Eid && n.NodeType == NodeType.Punkt).ToList();
        Assert.Equal(3, underpunkter.Count);

        var avslutning = Resultat.Noder.Single(n => n.Eid == $"{punkt14Eid}/avslutning");
        Assert.Equal(NodeType.Avslutning, avslutning.NodeType);
        Assert.Equal(punkt14Eid, avslutning.ParentEid);
        Assert.Equal(
            "Nærmere krav til innhold, utforming og plassering av opplysningene kan fastsettes av Helsedirektoratet.",
            avslutning.Tekst);
        Assert.All(underpunkter, p => Assert.True(p.SorteringsRekkefolge < avslutning.SorteringsRekkefolge));
    }

    [Fact]
    public void Leddfortsettelse_etter_liste_pa_leddniva_blir_avslutningsnode()
    {
        // § 7-2 ledd-1: tekst før listen ("… herunder"), en nøstet liste, og en
        // <p class="leddfortsettelse"> med tekst etter listen.
        // [ENDRET, avslutningsnode-runden, 2026-10-09, issue #361] Testen het før
        // «Tekst_etter_hoppet_over_liste_pa_leddniva_bevares_med_mellomrom» og krevde at leddteksten var
        // «… herunder Det skal legges vekt på …» — nettopp den sammenlimingen #361 retter. Nå har leddet
        // bare innledningen, og fortsettelsen er en egen node etter punktene.
        var leddEid = $"{ForskriftEli}/§7-2/ledd-1";
        var ledd = Resultat.Noder.Single(n => n.Eid == leddEid);
        Assert.Equal(
            "Folkehelseinstituttet kan i samarbeid med Statistisk sentralbyrå bestemme hvordan offisiell " +
            "statistikk skal utarbeides, herunder",
            ledd.Tekst);

        var avslutning = Resultat.Noder.Single(n => n.Eid == $"{leddEid}/avslutning");
        Assert.Equal(NodeType.Avslutning, avslutning.NodeType);
        Assert.Equal(leddEid, avslutning.ParentEid);
        Assert.Equal("kapittel-7-paragraf-2-ledd-1-avslutning", avslutning.KildeId);
        Assert.Equal(
            "Det skal legges vekt på statistikkhensyn og på hensynet til de berørte parters kostnader ved " +
            "innhenting av opplysninger og utarbeidelse av statistikk.",
            avslutning.Tekst);
        Assert.Equal(LovdataIdentifikatorer.BeregnTekstHash(avslutning.Tekst!), avslutning.TekstHash);

        // Rekkefølgen er innledning → punkter → avslutning, slik den står i forskriften.
        var barn = Resultat.Noder.Where(n => n.ParentEid == leddEid).OrderBy(n => n.SorteringsRekkefolge)
            .Select(n => n.Eid[(leddEid.Length + 1)..]).ToList();
        Assert.Equal(["punkt-1", "punkt-2", "avslutning"], barn);
        Assert.True(ledd.SorteringsRekkefolge < Resultat.Noder.Single(n => n.Eid == $"{leddEid}/punkt-1").SorteringsRekkefolge);
    }

    [Fact]
    public void Konvertering_er_referensielt_transparent()
    {
        var andreGangen = LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 7, 23));
        Assert.Equal(Resultat.AknXml, andreGangen.AknXml);
    }
}
