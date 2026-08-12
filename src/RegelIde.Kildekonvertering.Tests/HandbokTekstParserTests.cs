namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Hovedtest for <see cref="HandbokTekstParser"/> mot Bergen kommunes ekte retningslinjer (SD-24-113,
/// se data/kilder/raw-handbok/README.md) — docs/15-handbok-dokumentgraf-notat.md §8 Trinn 1 punkt 1s
/// eksplisitte verifiseringsmål: "Bergens retningslinjer inn med korrekt kapittel/punkt-tre og Eid
/// som løser «punkt 4.7»".
/// </summary>
public class HandbokTekstParserTests
{
    [Fact]
    public void Segmenterer_alle_ti_kapitler_i_dokumentrekkefolge()
    {
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        var kapitler = resultat.Noder.Where(n => n.NodeType == "kapittel").ToList();

        Assert.Equal(10, kapitler.Count);
        Assert.Equal(["kap1", "kap2", "kap3", "kap4", "kap5", "kap6", "kap7", "kap8", "kap9", "kap10"],
            kapitler.Select(k => k.Eid).ToArray());
        // Stigende sortering på tvers av HELE dokumentet, ikke bare innad i kapittel-lista.
        Assert.Equal(resultat.Noder.Select(n => n.SorteringsRekkefolge).ToArray(),
            resultat.Noder.Select(n => n.SorteringsRekkefolge).Order().ToArray());
    }

    [Fact]
    public void Kapittelnummer_og_overskrift_leses_ut_uten_gjettet_tittel()
    {
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        var kap4 = resultat.Noder.Single(n => n.Eid == "kap4");
        var kap9 = resultat.Noder.Single(n => n.Eid == "kap9");

        Assert.Equal("4", kap4.Nummer);
        Assert.Equal("Skjenkebevillinger", kap4.Overskrift);
        // Overskrift med interne bindestreker ("salgs- og skjenkesteder") skal IKKE kuttes ved første
        // bindestrek etter kapittelnummeret — kun separator-bindestreken rett etter tallet er skilletegnet.
        Assert.Equal("Kontroll med salgs- og skjenkesteder", kap9.Overskrift);
    }

    [Fact]
    public void Punkt_4_1_til_4_10_ligger_under_kap4_med_riktig_eid()
    {
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        var punkter = resultat.Noder.Where(n => n.NodeType == "punkt" && n.Nummer!.StartsWith("4.")).ToList();

        Assert.Equal(10, punkter.Count);
        foreach (var p in punkter)
        {
            Assert.Equal("kap4", p.ParentEid);
            Assert.Equal($"kap4/pkt{p.Nummer}", p.Eid);
        }
    }

    [Fact]
    public void Punkt_4_7_finnes_og_lar_seg_lose_som_kryssreferansemal()
    {
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        var punkt47 = resultat.Noder.Single(n => n.Eid == "kap4/pkt4.7");

        Assert.Contains("spisesteder", punkt47.Tekst);
    }

    [Fact]
    public void Definisjon_4_3_hoteller_inneholder_parameteren_30_rom()
    {
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        var punkt43 = resultat.Noder.Single(n => n.Eid == "kap4/pkt4.3");

        Assert.Contains("minst 30 rom med dusj/bad", punkt43.Tekst);
        Assert.NotNull(punkt43.TekstHash);
        Assert.Equal(LovdataIdentifikatorer.BeregnTekstHash(punkt43.Tekst!), punkt43.TekstHash);
    }

    [Fact]
    public void Kapittel_uten_punktnummerering_barer_egen_tekst_direkte_pa_kapittelnoden()
    {
        // Kapittel 7 har INGEN X.Y-punkt i det hele tatt — hele kapittelteksten skal ligge direkte på
        // kapittel-noden, ikke gå tapt (§0.1: ingen informasjon skal forkastes stille).
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        var kap7 = resultat.Noder.Single(n => n.Eid == "kap7");

        Assert.Contains("25 ambulerende skjenkebevillinger", kap7.Tekst);
    }

    [Fact]
    public void Sidebrytningsstoy_lekker_ikke_inn_i_noden_tekst_og_splitter_ikke_punktet_den_avbryter()
    {
        // 3.2s brødtekst starter fysisk på NESTE side i PDF-en, rett etter en "Dok.nr.: ... Side N av M"-
        // linje (se data/kilder/raw-handbok/README.md) — dette er akkurat notatets eksempel-case for
        // sidebrytningsfiltrering, og bekrefter at filtrert støy ikke kutter et punkt i to.
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());

        foreach (var node in resultat.Noder)
        {
            Assert.DoesNotContain("Side", node.Tekst ?? "");
            Assert.DoesNotContain("Dok.nr", node.Tekst ?? "");
        }

        var punkt32 = resultat.Noder.Single(n => n.Eid == "kap3/pkt3.2");
        Assert.Contains("Vareutvalg rettet mot spesielle forbrukergrupper", punkt32.Tekst);
    }

    [Fact]
    public void Enkeltsifrede_lister_inni_et_kapittel_blir_ikke_feiltolket_som_nye_punkt()
    {
        // Kapittel 2s "1./2./3./4."-dokumentasjonsliste er ÉN-sifret nummerering, ikke X.Y — skal
        // forbli løpetekst på kap2, ikke opprette fire falske "punkt"-noder.
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        var kap2 = resultat.Noder.Single(n => n.Eid == "kap2");

        Assert.DoesNotContain(resultat.Noder, n => n.ParentEid == "kap2" && n.NodeType == "punkt");
        Assert.Contains("Kopi av brukstillatelsen fra bygningsmyndighetene", kap2.Tekst);
        Assert.Contains("yrkesskadeforsikring", kap2.Tekst);
    }

    [Fact]
    public void Hjemlet_i_finner_bade_paragraf_1_7d_og_paragraf_4_5_mot_alkoholloven()
    {
        // §8 Trinn 1 punkt 3s eksplisitte verifiseringsmål.
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        var hjemler = resultat.Referanser.Where(r => r.Type == HandbokReferansetype.HjemletI).ToList();

        var paragraf17d = hjemler.Single(r => r.EksternParagraf == "§1-7 d");
        Assert.Equal("Alkoholloven", paragraf17d.EksternLovnavn);
        Assert.Equal("kap1/pkt1.1", paragraf17d.FraNodeEid);

        var paragraf45 = hjemler.Single(r => r.EksternParagraf == "§4-5");
        Assert.Equal("alkoholloven", paragraf45.EksternLovnavn);
        Assert.Equal("kap7", paragraf45.FraNodeEid);
    }

    [Fact]
    public void Kryssrefererer_loser_punkt_4_7_fra_4_8_mot_dokumentets_eget_eid_register()
    {
        var resultat = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        var kryssref = Assert.Single(resultat.Referanser, r => r.Type == HandbokReferansetype.Kryssrefererer);

        Assert.Equal("kap4/pkt4.8", kryssref.FraNodeEid);
        Assert.Equal("kap4/pkt4.7", kryssref.TilEid);
        Assert.Contains("4.7", kryssref.Utdrag);
    }
}
