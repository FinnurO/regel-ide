namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Asserter mot konkrete, kjente paragrafer i alkoholloven (docs/08-byggesteg1-teknisk-design.md §1.3s
/// eksempel er bygget nettopp fra § 1-1 og § 1-3 i denne loven) og mot de "kjente vanskelige tilfellene"
/// i §3.2 (opphevet paragraf, romertall-underinndeling).
/// </summary>
public class AlkohollovenKonverteringTests
{
    private static readonly KonverteringResultat Resultat =
        LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 23));

    private const string LovEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";

    [Fact]
    public void Metadata_er_korrekt_avledet_fra_header()
    {
        Assert.Equal(Kildetype.Lov, Resultat.Metadata.Kildetype);
        Assert.Equal(LovEli, Resultat.Metadata.Eli);
        Assert.Equal("Lov om omsetning av alkoholholdig drikk m.v. (alkoholloven)", Resultat.Metadata.Tittel);
        Assert.Equal("Helse- og omsorgsdepartementet", Resultat.Metadata.AnsvarligDepartement);
        Assert.Equal("stortinget", Resultat.Metadata.FrbrAuthorHref);
    }

    [Fact]
    public void Paragraf_1_1_har_korrekt_eId_og_overskrift()
    {
        var eid = $"{LovEli}/§1-1";
        var paragraf = Resultat.Noder.Single(n => n.Eid == eid);
        Assert.Equal(NodeType.Paragraf, paragraf.NodeType);
        Assert.Equal("Lovens formål.", paragraf.Overskrift);
        Assert.False(paragraf.Opphevet);

        var ledd1 = Resultat.Noder.Single(n => n.Eid == $"{eid}/ledd-1");
        Assert.Equal(NodeType.Ledd, ledd1.NodeType);
        Assert.StartsWith("Reguleringen av innførsel og omsetning av alkoholholdig drikk", ledd1.Tekst);
        Assert.NotNull(ledd1.TekstHash);
        Assert.Equal(64, ledd1.TekstHash!.Length); // hex-SHA-256
    }

    [Fact]
    public void Paragraf_1_3_ledd_2_har_punkt_med_definisjoner()
    {
        var leddEid = $"{LovEli}/§1-3/ledd-2";
        var ledd = Resultat.Noder.SingleOrDefault(n => n.Eid == leddEid);
        Assert.NotNull(ledd);
        Assert.Equal("I denne loven betyr:", ledd!.Tekst);

        var punkt1 = Resultat.Noder.Single(n => n.Eid == $"{leddEid}/punkt-1");
        Assert.Equal(NodeType.Punkt, punkt1.NodeType);
        Assert.Equal("alkoholfri drikk: drikk som inneholder under 0,7 volumprosent alkohol", punkt1.Tekst);
    }

    [Fact]
    public void Intern_kryssreferanse_fra_1_3_til_1_5_fanges_opp()
    {
        var fraEid = $"{LovEli}/§1-3/ledd-1";
        var tilEid = $"{LovEli}/§1-5";
        Assert.Contains(Resultat.Referanser, r => r.FraNodeEid == fraEid && r.TilEid == tilEid && r.ErInternReferanse);
    }

    [Theory]
    [InlineData("§1-12")]
    [InlineData("§1-13")]
    public void Opphevet_paragraf_produserer_alltid_en_node(string paragrafnummer)
    {
        var eid = $"{LovEli}/{paragrafnummer}";
        var paragraf = Resultat.Noder.Single(n => n.Eid == eid);
        Assert.True(paragraf.Opphevet);
        Assert.Equal("(Opphevet)", paragraf.Overskrift);
        Assert.NotNull(paragraf.OpphevetDato);
        // § 3.2: opphevet paragraf har ingen ledd-barn (ingen legalP-innhold i kilden)
        Assert.DoesNotContain(Resultat.Noder, n => n.ParentEid == eid);
    }

    [Fact]
    public void Opphevet_paragraf_1_12_har_riktig_dato_fra_data_repealeddate()
    {
        var paragraf = Resultat.Noder.Single(n => n.Eid == $"{LovEli}/§1-12");
        Assert.Equal(new DateOnly(2005, 7, 1), paragraf.OpphevetDato);
    }

    [Fact]
    public void Romertall_underinndeling_i_kapittel_3_blir_hcontainer_med_lokal_eId()
    {
        var eid = "kap-3/rom-I";
        var underinndeling = Resultat.Noder.Single(n => n.Eid == eid);
        Assert.Equal(NodeType.Underinndeling, underinndeling.NodeType);
        Assert.Equal("kap-3", underinndeling.ParentEid);
        Assert.Equal("Alminnelige bestemmelser", underinndeling.Overskrift);

        // § 3-1 ligger under romertall-underinndelingen, ikke direkte under kapittelet
        var paragraf31 = Resultat.Noder.Single(n => n.Eid == $"{LovEli}/§3-1");
        Assert.Equal(eid, paragraf31.ParentEid);
    }

    [Fact]
    public void Fotnote_pa_paragraf_3_1_fanges_opp_som_egen_note_atskilt_fra_hovedteksten()
    {
        var paragraf = Resultat.Noder.Single(n => n.Eid == $"{LovEli}/§3-1");
        var fotnote = Assert.Single(paragraf.Fotnoter);
        Assert.Equal("1", fotnote.Etikett);
        Assert.Contains("EØS-avtalen", fotnote.Tekst);

        // Fotnotereferansen (<sup>) er ikke med i selve ledd-teksten
        var leddMedFotnotereferanse = Resultat.Noder
            .Where(n => n.NodeType == NodeType.Ledd && n.ParentEid == paragraf.Eid)
            .Single(n => n.Tekst!.Contains("prisfastsetting"));
        Assert.DoesNotContain("1", leddMedFotnotereferanse.Tekst!.Split("prisfastsetting")[1]);
    }

    [Fact]
    public void Alle_kapittelseksjoner_er_representert_inkl_kap3A()
    {
        // 11 "hovedkapitler" (data/kilder/README.md) + det opphevede, lettermerkede "Kapittel 3A"
        // (Engrossalg, tilføyd 1995/opphevet 2004) = 12 <section>-elementer i rådataen.
        var kapitler = Resultat.Noder.Where(n => n.NodeType == NodeType.Kapittel).Select(n => n.Nummer).ToList();
        Assert.Equal(12, kapitler.Count);
        Assert.Contains("3A", kapitler);
    }

    [Fact]
    public void AknXml_er_velformet_og_inneholder_forventede_elementer()
    {
        var xml = System.Xml.Linq.XDocument.Parse(Resultat.AknXml);
        Assert.Equal("akomaNtoso", xml.Root!.Name.LocalName);
        var alleEid = xml.Descendants().Select(e => e.Attribute("eId")?.Value).Where(v => v is not null).ToList();
        Assert.Contains($"{LovEli}/§1-1", alleEid);
        Assert.Contains("kap-3/rom-I", alleEid);
    }

    [Fact]
    public void Konvertering_er_referensielt_transparent_pa_tvers_av_kjoringer()
    {
        var andreGangen = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 23));
        Assert.Equal(Resultat.AknXml, andreGangen.AknXml);
        Assert.Equal(Resultat.Noder.Count, andreGangen.Noder.Count);
        for (var i = 0; i < Resultat.Noder.Count; i++)
        {
            Assert.Equal(Resultat.Noder[i].Eid, andreGangen.Noder[i].Eid);
            Assert.Equal(Resultat.Noder[i].TekstHash, andreGangen.Noder[i].TekstHash);
        }
    }

    [Fact]
    public void Ulik_importdato_endrer_kun_FRBRManifestation_ikke_resten_av_AKN_body()
    {
        var med2027 = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2027, 1, 1));
        var kroppUtenMeta = System.Text.RegularExpressions.Regex.Replace(Resultat.AknXml, "<meta>.*?</meta>", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        var kroppUtenMeta2027 = System.Text.RegularExpressions.Regex.Replace(med2027.AknXml, "<meta>.*?</meta>", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.Equal(kroppUtenMeta, kroppUtenMeta2027);
        Assert.NotEqual(Resultat.AknXml, med2027.AknXml);
    }

    // ---------- Lagt til etter ekstern code review (Copilot) — se src/README.md ----------

    [Fact]
    public void Paragraf_med_bokstav_1_4a_parses_korrekt()
    {
        var eid = $"{LovEli}/§1-4a";
        var paragraf = Resultat.Noder.Single(n => n.Eid == eid);
        Assert.Equal("Bevillingsplikt", paragraf.Overskrift);

        var ledd1 = Resultat.Noder.Single(n => n.Eid == $"{eid}/ledd-1");
        Assert.Equal("Salg, skjenking og tilvirkning av alkoholholdig drikk kan bare skje på grunnlag av bevilling etter denne lov.", ledd1.Tekst);
    }

    [Fact]
    public void Kapittel_3A_har_bokstav_i_eId_og_nummer()
    {
        var kap3A = Resultat.Noder.Single(n => n.NodeType == NodeType.Kapittel && n.Nummer == "3A");
        Assert.Equal("kap-3A", kap3A.Eid);
    }

    [Fact]
    public void Kryssreferanse_til_lettermerket_paragraf_i_annet_dokument_fanges_opp()
    {
        // § 9-4 ledd-3 viser til markedsføringsloven (LOV-2009-01-09-2) §§ 43 a til 43 c —
        // ekte eksempel på kryssreferanse som er BÅDE ekstern (annet dokument) OG til en
        // bokstavmerket paragraf, jf. Copilot-reviewens etterlysning av begge tilfellene.
        var fraEid = $"{LovEli}/§9-4/ledd-3";
        var forventetTilEid = "https://lovdata.no/eli/lov/2009/01/09/2/nor/§43a";
        var referanse = Resultat.Referanser.Single(r => r.FraNodeEid == fraEid && r.TilEid == forventetTilEid);
        Assert.False(referanse.ErInternReferanse);
    }

    [Fact]
    public void Fotnote_med_innebygd_lenke_beholder_synlig_lenketekst()
    {
        // Fotnoten på § 3-1 (allerede dekket av Fotnote_pa_paragraf_3_1_...-testen) inneholder selv
        // en lenke til EØS-avtalen — bekrefter at lenketeksten inni en fotnote bevares som ren tekst,
        // ikke mistes eller krasjer (fotnoter spores ikke som kryssreferanser, jf. ParseFotnoter).
        var paragraf = Resultat.Noder.Single(n => n.Eid == $"{LovEli}/§3-1");
        var fotnote = Assert.Single(paragraf.Fotnoter);
        Assert.Equal("1", fotnote.Etikett);
        Assert.Equal("Jf. EØS-avtalen art. 16.", fotnote.Tekst);
    }
}
