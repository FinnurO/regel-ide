namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// [Ny, issue #508] Trykt bokstav og nummer i <c>Nummer</c>, underledd som egen node, og gjennomgangslisten. Akseptansekriteriene i #508
/// (prosjekteierens avgjørelse 2026-11-27) er formulert som spørsmål; hver test svarer på ett av dem mot ekte kilde-HTML fra den delte
/// basen (Testdata/Utdrag, se <see cref="Testdata.LesUtdrag"/>). Ingen eId endres: alle eId-ene under står slik de står i basen i dag.
/// </summary>
public class TryktNummerOgUnderleddTests
{
    private const string Pol = "https://lovdata.no/eli/lov/2018/06/15/38/nor";
    private static readonly DateOnly Dato = new(2026, 11, 27);

    private static KonverteringResultat Konverter(string html) => LovdataKonverterer.Konverter(html, Dato);
    private static KonverteringResultat Utdrag(string fil) => Konverter(Testdata.LesUtdrag(fil));
    private static RettskildeNode Node(KonverteringResultat r, string eidHale) => r.Noder.Single(n => n.Eid == $"{r.Metadata.Eli}/{eidHale}");

    // AK1: Hvilken node er «bokstav f» i personopplysningsloven artikkel 6 nr. 1?
    [Fact]
    public void Bokstav_f_i_art_6_nr_1_finnes_via_forelder_nodetype_og_nummer()
    {
        var r = Konverter(Testdata.LesPersonopplysningsloven());
        var nr1 = r.Noder.Single(n => n.ParentEid == $"{Pol}/a6" && n.NodeType == NodeType.Ledd && n.Nummer == "1");
        var f = r.Noder.Single(n => n.ParentEid == nr1.Eid && n.NodeType == NodeType.Punkt && n.Nummer == "f");

        Assert.Equal($"{Pol}/a6/ledd-1/punkt-6", f.Eid);
        Assert.Equal(Nummerkilder.Bokstav, f.NummerKilde);
        Assert.Equal("6", f.Lopenummer);
        Assert.Equal(Nummerkilder.Tall, nr1.NummerKilde);
        Assert.StartsWith("behandlingen er nødvendig for formål knyttet til de berettigede interessene", f.Tekst);
    }

    // AK1, alternativ B: <num> beholder løpenummeret, så en kilde der bare nummeret endres, får ikke ny AKN.
    [Fact]
    public void Akn_num_er_fortsatt_lopenummeret()
    {
        var r = Konverter(Testdata.LesPersonopplysningsloven());
        Assert.Contains($"<point eId=\"{Pol}/a6/ledd-1/punkt-6\" regelIde:kildeId=\"kapittel-10-kapittel-2-paragraf-2-nummer-1-punkt-6\"><num>6</num>", r.AknXml);
    }

    // AK2: Hva sier art. 8 nr. 1 annet ledd, og hvor står det?
    [Fact]
    public void Art_8_nr_1_annet_ledd_er_egen_underleddsnode()
    {
        var r = Konverter(Testdata.LesPersonopplysningsloven());
        var nr1 = Node(r, "a8/ledd-1");
        var annet = Node(r, "a8/ledd-1/underledd-2");

        Assert.Equal(NodeType.Underledd, annet.NodeType);
        Assert.Equal(nr1.Eid, annet.ParentEid);
        Assert.StartsWith("For disse formål kan medlemsstatene ved lov fastsette en lavere aldersgrense", annet.Tekst);
        Assert.DoesNotContain("For disse formål", nr1.Tekst);
        Assert.Contains($"<subparagraph eId=\"{Pol}/a8/ledd-1/underledd-2\"", r.AknXml);
    }

    // AK2: etter en liste (art. 6 nr. 1) blir det nestede leddet underledd, ikke avslutning (#361), og ingen punkt-eId endres.
    [Fact]
    public void Art_6_nr_1_annet_ledd_etter_lista_er_underledd_ikke_avslutning()
    {
        var r = Konverter(Testdata.LesPersonopplysningsloven());

        Assert.StartsWith("Nr. 1 bokstav f) får ikke anvendelse", Node(r, "a6/ledd-1/underledd-2").Tekst);
        Assert.DoesNotContain(r.Noder, n => n.Eid.StartsWith($"{Pol}/a6/ledd-1/avslutning", StringComparison.Ordinal));
        Assert.Equal(6, r.Noder.Count(n => n.ParentEid == $"{Pol}/a6/ledd-1" && n.NodeType == NodeType.Punkt));
    }

    // AK2 + juristrunden J2: bare nummer og overskrift foran det første nestede leddet. «nr. 4 annet ledd» (ferieloven § 5 (6)) er underledd-2.
    [Fact]
    public void Ferieloven_5_nr_4_annet_ledd_er_underledd_2_og_overskriften_er_ikke_leddtekst()
    {
        var r = Utdrag("ferieloven-LOV-1988-04-29-21-utdrag-5-9.html");
        var nr4 = Node(r, "§5/ledd-4");

        Assert.Equal("Tilleggsfritid ved søndags- og skiftarbeid, uregelmessig arbeidstid mv", nr4.Overskrift);
        Assert.StartsWith("(4) Arbeidstaker som arbeider på søndager kan kreve å få arbeidsfri", nr4.Tekst);
        Assert.StartsWith("Arbeidstaker kan kreve at tiden fra arbeidets avslutning før ferien", Node(r, "§5/ledd-4/underledd-2").Tekst);
        Assert.DoesNotContain(r.Noder, n => n.Eid == $"{r.Metadata.Eli}/§5/ledd-4/underledd-3");
        Assert.Contains("Bestemmelsene i nr. 4 annet ledd kan fravikes", Node(r, "§5/ledd-6").Tekst);
    }

    // Juristrunden J2: overskrift i parentes UTEN kursiv. «Som permittering etter første ledd» viser til leddets egen tekst.
    [Fact]
    public void Permitteringslonnsloven_1_nr_2_overskrift_uten_kursiv()
    {
        var r = Utdrag("permitteringslonnsloven-LOV-1988-05-06-22-utdrag-1-6.html");
        var nr2 = Node(r, "§1/ledd-2");

        Assert.Equal("Permittering", nr2.Overskrift);
        Assert.StartsWith("(2) Med permittering menes i denne lov", nr2.Tekst);
        Assert.StartsWith("Som permittering etter første ledd regnes", Node(r, "§1/ledd-2/underledd-2").Tekst);
        Assert.Contains(r.Gjennomgang, g => g.Eid == nr2.Eid && g.Art == Gjennomgangspunkt.OverskriftUtenKursiv);
    }

    // Juristrunden J2: en hel kursiv setning i hakeparentes er første ledd, ikke en overskrift.
    [Fact]
    public void Sjotransport_art_5_nr_1_kursiv_setning_i_hakeparentes_er_forste_ledd()
    {
        var r = Utdrag("sjotransport-LOV-1992-12-04-121-utdrag-sttl3-a3-a5.html");
        var nr1 = Node(r, "a5/ledd-1");

        Assert.Null(nr1.Overskrift);
        Assert.StartsWith("[1. I tilfelle av alvorlig forstyrrelse", nr1.Tekst);
        Assert.StartsWith("Etter samråd med de øvrige medlemsstatene", Node(r, "a5/ledd-1/underledd-2").Tekst);
        Assert.Contains(r.Gjennomgang, g => g.Eid == nr1.Eid && g.Art == Gjennomgangspunkt.KursivSomIkkeErOverskrift);
    }

    // AK1: lov 1999-06-18-40 § 7: første punkt er trykt «f.».
    [Fact]
    public void Forste_punkt_trykt_f_har_nummer_f_og_beholder_eId()
    {
        var r = Utdrag("lov-1999-06-18-40-utdrag-7.html");
        var p = Node(r, "§7/ledd-2/punkt-1");

        Assert.Equal(("f", Nummerkilder.Bokstav, "1"), (p.Nummer, p.NummerKilde, p.Lopenummer));
        Assert.Equal(new[] { "f", "g", "i" }, r.Noder.Where(n => n.NodeType == NodeType.Punkt).Select(n => n.Nummer));
    }

    // AK4: skatteloven § 10-5: leddet trykt «(6)» har eId ledd-7. Det utrykte «ledd» foran (5) er tekst etter en liste.
    [Fact]
    public void Skatteloven_10_5_trykt_sjette_ledd_er_ledd_7()
    {
        var r = Utdrag("skatteloven-LOV-1999-03-26-14-utdrag-10-5.html");

        Assert.Equal(("6", Nummerkilder.Tall), (Node(r, "§10-5/ledd-7").Nummer, Node(r, "§10-5/ledd-7").NummerKilde));
        Assert.Equal(("5", Nummerkilder.Lopenummer), (Node(r, "§10-5/ledd-5").Nummer, Node(r, "§10-5/ledd-5").NummerKilde));
        Assert.StartsWith("Vurderingen i bokstav a og b", Node(r, "§10-5/ledd-5").Tekst);
    }

    // Tollavgiftsforskriften § 4-1-1: tallisten har trykte tall som Nummer; Kosovo er punkt 21 (docs/35 §2.1).
    [Fact]
    public void Tollavgiftsforskriften_4_1_1_talliste()
    {
        var r = Utdrag("tollavgiftsforskriften-FOR-2022-10-27-1938-utdrag-4-1-1.html");
        var p21 = Node(r, "§4-1-1/ledd-1/punkt-21");

        Assert.Equal(("21", Nummerkilder.Tall), (p21.Nummer, p21.NummerKilde));
        Assert.Contains("Kosovo", p21.Tekst);
    }

    // Strekpunkt: etiketten «-» er ikke trykt; nummeret er løpenummeret, merket strek. Mellomoverskrift-ledd har løpenummer.
    [Fact]
    public void Strekpunkt_og_utrykte_ledd_har_lopenummer()
    {
        var r = Utdrag("forskrift-FOR-2015-12-18-1824-utdrag-5-11.html");

        Assert.Equal(("1", Nummerkilder.Strek), (Node(r, "§5-11/ledd-2/punkt-1/punkt-1").Nummer, Node(r, "§5-11/ledd-2/punkt-1/punkt-1").NummerKilde));
        Assert.Equal(("1", Nummerkilder.Lopenummer, "Bransjer"), (Node(r, "§5-11/ledd-1").Nummer, Node(r, "§5-11/ledd-1").NummerKilde, Node(r, "§5-11/ledd-1").Tekst));
        Assert.Equal(("1", Nummerkilder.Tall), (Node(r, "§5-11/ledd-2").Nummer, Node(r, "§5-11/ledd-2").NummerKilde));
    }

    // AK3: en punktliste som bare står i løpende tekst gir ingen noder, men står i gjennomgangslisten.
    // Teksten er ordrett forskrift 2024-09-17-3171 § 26-2 første ledd (fra den delte basen).
    [Fact]
    public void Punktliste_i_lopende_tekst_gir_ingen_noder_men_listes()
    {
        var tekst = "(1) Med fellesgrader menes et samarbeid mellom flere institusjoner. For fullført fellesgrad utstedes felles vitnemål i form av: " +
                    "a) et vitnemålsdokument utstedt av alle konsortiemedlemmene, b) et vitnemål fra hver av konsortiedeltakerne, eller en kombinasjon av a) og b).";
        var r = Konverter(Avslutningsutdrag.MedParagrafinnhold(
            $"<article class=\"numberedLegalP\" data-numerator=\"1\" id=\"kapittel-11-paragraf-3-nummer-1\">{tekst}</article>"));

        Assert.DoesNotContain(r.Noder, n => n.NodeType == NodeType.Punkt);
        Assert.Contains(r.Gjennomgang, g => g.Art == Gjennomgangspunkt.LøpendePunktliste && g.Eid == $"{Avslutningsutdrag.ParagrafEid}/ledd-1");
    }

    [Fact]
    public void Sitering_av_bokstaver_er_ikke_en_lopende_punktliste()
    {
        var r = Konverter(Avslutningsutdrag.MedParagrafinnhold(
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-ledd-1\">Vilkårene i bokstav a) og b) i første ledd gjelder tilsvarende for andre tilfeller.</article>"));
        Assert.Empty(r.Gjennomgang);
    }
}
