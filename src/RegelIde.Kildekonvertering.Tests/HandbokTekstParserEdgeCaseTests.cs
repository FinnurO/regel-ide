namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Syntetiske edge cases som ikke forekommer i Bergen-fixturen (se HandbokTekstParserTests): dypere
/// nummerering (X.Y.Z), overskriftsbasert fallback (§2 Lag 2, "der nummerering mangler"), og en
/// uløst kryssreferanse som skal droppes stille ("ingen gjettet fallback", samme prinsipp som
/// Lovdata-pipelinen).
/// </summary>
public class HandbokTekstParserEdgeCaseTests
{
    [Fact]
    public void Tre_nivas_nummerering_nostes_under_naermeste_to_segments_punkt()
    {
        var tekst = """
            Kapittel 4 - Skjenkebevillinger
            4.1
            Innledende tekst om skjenkebevillinger.
            4.1.2
            Utdypende underpunkt om samme tema.
            """;

        var resultat = HandbokTekstParser.Parse(tekst);

        var punkt41 = Assert.Single(resultat.Noder, n => n.Eid == "kap4/pkt4.1");
        var punkt412 = Assert.Single(resultat.Noder, n => n.Eid == "kap4/pkt4.1/pkt4.1.2");
        Assert.Equal("kap4", punkt41.ParentEid);
        Assert.Equal("kap4/pkt4.1", punkt412.ParentEid);
        Assert.Contains("Utdypende underpunkt", punkt412.Tekst);
    }

    [Fact]
    public void Tre_nivas_nummerering_uten_eksisterende_foreldrepunkt_faller_tilbake_til_kapittelet()
    {
        // 4.1.2 dukker opp UTEN at 4.1 selv finnes som egen node — ingen gjettet mellomnivå, direkte
        // under kapittelet i stedet (§0.1/§3.3: "ingen gjettet fallback", men heller ikke et krasj).
        var tekst = """
            Kapittel 4 - Skjenkebevillinger
            4.1.2
            Underpunkt uten et eksisterende 4.1.
            """;

        var resultat = HandbokTekstParser.Parse(tekst);
        var punkt412 = Assert.Single(resultat.Noder, n => n.Eid == "kap4/pkt4.1.2");
        Assert.Equal("kap4", punkt412.ParentEid);
    }

    [Fact]
    public void Uten_noen_nummerering_faller_parseren_tilbake_til_overskriftsbasert_segmentering()
    {
        var tekst = """
            ## Innledning
            Dette er en innbyggerveileder uten kapittelnummerering.
            ### Hvem gjelder dette for
            Alle som søker om skjenkebevilling.
            ## Slik søker du
            Fyll ut søknadsskjemaet digitalt.
            """;

        var resultat = HandbokTekstParser.Parse(tekst);

        Assert.All(resultat.Noder, n => Assert.Equal("avsnitt", n.NodeType));
        var h2_1 = resultat.Noder.Single(n => n.Eid == "h2-1");
        var h3_1 = resultat.Noder.Single(n => n.Eid == "h2-1/h3-1");
        var h2_2 = resultat.Noder.Single(n => n.Eid == "h2-2");

        Assert.Equal("Innledning", h2_1.Overskrift);
        Assert.Contains("innbyggerveileder", h2_1.Tekst);
        Assert.Equal("h2-1", h3_1.ParentEid);
        Assert.Equal("Hvem gjelder dette for", h3_1.Overskrift);
        Assert.Equal("Slik søker du", h2_2.Overskrift);
    }

    [Fact]
    public void Helt_strukturlos_prosa_blir_en_eneste_avsnittsnode_ikke_forkastet()
    {
        var tekst = "Bare løs prosa uten noen overskrift eller nummerering i det hele tatt.";

        var resultat = HandbokTekstParser.Parse(tekst);

        var node = Assert.Single(resultat.Noder);
        Assert.Equal("h2-1", node.Eid);
        Assert.Equal("avsnitt", node.NodeType);
        Assert.Contains("løs prosa", node.Tekst);
    }

    [Fact]
    public void Ulost_kryssreferanse_mot_ikke_eksisterende_punkt_droppes_stille()
    {
        var tekst = """
            Kapittel 4 - Skjenkebevillinger
            4.1
            Det vises til punkt 4.99 for øvrig, som ikke finnes i dette dokumentet.
            """;

        var resultat = HandbokTekstParser.Parse(tekst);

        Assert.DoesNotContain(resultat.Referanser, r => r.Type == HandbokReferansetype.Kryssrefererer);
    }

    [Fact]
    public void Sidebrytningsstoy_uten_dok_nr_prefiks_filtreres_ogsa_bort()
    {
        var tekst = """
            Kapittel 1 - Innledning
            1.1
            Første del av teksten.
            Side 2 av 5
            Andre del av samme punkt, fortsatt 1.1.
            """;

        var resultat = HandbokTekstParser.Parse(tekst);
        var punkt = resultat.Noder.Single(n => n.Eid == "kap1/pkt1.1");

        Assert.DoesNotContain("Side", punkt.Tekst);
        Assert.Contains("Første del", punkt.Tekst);
        Assert.Contains("Andre del", punkt.Tekst);
    }

    [Fact]
    public void Tom_tekst_gir_ingen_noder_og_ingen_referanser_ikke_krasj()
    {
        var resultat = HandbokTekstParser.Parse("");

        Assert.Empty(resultat.Noder);
        Assert.Empty(resultat.Referanser);
    }
}
