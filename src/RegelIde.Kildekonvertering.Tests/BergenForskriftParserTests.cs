namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Del A ("forskriften som andre PDF-fixture") — bekrefter <see cref="HandbokTekstParser"/>
/// (UENDRET klasse, kun regex-tillegget for toppnivå-tallpunktum-overskrifter, se
/// data/kilder/raw-handbok/README.md) segmenterer Bergens FORSKRIFT om salgs-, skjenke- og
/// åpningstider korrekt — en ANNEN dokumentstruktur enn retningslinjene (§-lignende, men i praksis
/// "N. STORE BOKSTAVER"-seksjoner uten literalen "Kapittel", pluss "N.N"/"N.N.N"-punkter som ER
/// identisk med retningslinjenes mønster).
/// </summary>
public class BergenForskriftParserTests
{
    [Fact]
    public void Tre_toppnivaseksjoner_segmenteres_selv_uten_Kapittel_literalen()
    {
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenForskrift());
        var seksjoner = resultat.Noder.Where(n => n.NodeType == "kapittel").ToList();

        Assert.Equal(3, seksjoner.Count);
        Assert.Equal(["kap1", "kap2", "kap3"], seksjoner.Select(s => s.Eid).ToArray());
        Assert.Equal("SALGSTID FOR ALKOHOLHOLDIG DRIKK UNDER 4,7 VOL. PROSENT ALKOHOL", seksjoner[0].Overskrift);
        Assert.Equal("SKJENKETID", seksjoner[1].Overskrift);
        Assert.Equal("ÅPNINGSTID", seksjoner[2].Overskrift);
    }

    [Fact]
    public void Seksjon_1_barer_egen_salgstidstekst_i_stedet_for_a_ga_tapt()
    {
        // Uten TallpunktumSeksjonMønster ville "1. SALGSTID ..." og all tekst under den blitt
        // stille forkastet (ingen åpen node når den treffes først i dokumentet) — se README.
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenForskrift());
        var kap1 = resultat.Noder.Single(n => n.Eid == "kap1");

        Assert.Contains("kl. 08.00", kap1.Tekst);
        Assert.Contains("kl. 20.00 på hverdager", kap1.Tekst);
    }

    [Fact]
    public void To_og_tre_nivas_punkter_under_seksjon_2_far_riktig_eid_og_forelder()
    {
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenForskrift());

        var pkt21 = resultat.Noder.Single(n => n.Eid == "kap2/pkt2.1");
        Assert.Equal("kap2", pkt21.ParentEid);

        // Tre-segments punkt nøster seg UNDER det 2-segments punktets EGEN eId (§2/PunktEid:
        // "{forelder}/pkt{fulltNummer}") når forelderen finnes — altså "kap2/pkt2.1/pkt2.1.1",
        // ikke "kap2/pkt2.1.1" flatt under kapittelet.
        var pkt211 = resultat.Noder.Single(n => n.Eid == "kap2/pkt2.1/pkt2.1.1");
        Assert.Equal("kap2/pkt2.1", pkt211.ParentEid);
        Assert.Contains("fra kl. 06.00", pkt211.Tekst);

        var pkt23 = resultat.Noder.Single(n => n.Eid == "kap2/pkt2.3");
        Assert.Equal("kap2", pkt23.ParentEid);
        Assert.Contains("en halv time etter utløpet av", pkt23.Tekst);
    }

    [Fact]
    public void Seksjon_3_er_en_egen_node_ikke_smeltet_inn_i_punkt_2_3()
    {
        // Dette er nøyaktig regresjonen fiksen løser: FØR fiksen ble "3. ÅPNINGSTID" og dens
        // brødtekst lest som løpetekst PÅ kap2/pkt2.3, fordi ingen regex fanget opp linjen "3.
        // ÅPNINGSTID" (ett tallsegment, ingen "Kapittel"-literal) og apenEid fortsatt pekte på
        // 2.3 da den ble lest.
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenForskrift());

        var pkt23 = resultat.Noder.Single(n => n.Eid == "kap2/pkt2.3");
        Assert.DoesNotContain("nattkafe", pkt23.Tekst);
        Assert.DoesNotContain("ÅPNINGSTID", pkt23.Tekst ?? "");

        var kap3 = resultat.Noder.Single(n => n.Eid == "kap3");
        Assert.Contains("nattkafe", kap3.Tekst);
        Assert.Contains("Ansvarlig Alkoholhåndtering", kap3.Tekst);
    }

    [Fact]
    public void Sidebrytningsstoy_med_annet_doknr_enn_retningslinjene_filtreres_bort()
    {
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenForskrift());

        foreach (var node in resultat.Noder)
        {
            Assert.DoesNotContain("Side", node.Tekst ?? "");
            Assert.DoesNotContain("SD-24-114", node.Tekst ?? "");
        }
    }

    [Fact]
    public void Ingen_hjemlet_i_treff_forskriftens_egen_lopetekst_siterer_ikke_alkoholloven_inline()
    {
        // Ekte, dokumentert funn (README): forskriften ER selv en hjemlet norm, den SITERER ikke
        // loven i egen brødtekst slik retningslinjene gjør. 0 treff er korrekt, ikke en parserfeil.
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenForskrift());

        Assert.DoesNotContain(resultat.Referanser, r => r.Type == HandbokReferansetype.HjemletI);
    }
}
