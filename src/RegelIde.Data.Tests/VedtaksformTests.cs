namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #446 PR 1] Regelen for vedtaksformen (<see cref="Vedtaksform"/>), uten database. HTML-en er forkortede utdrag av Lovdatas rå HTML
/// for kildene som er nevnt i testnavnet (lest i den delte basen 2026-11-25), med samme klasser og id-er.
/// </summary>
public class VedtaksformTests
{
    private static string Html(string kropp) =>
        $"<!DOCTYPE html><html><body><header class=\"documentHeader\"></header><main class=\"documentBody\" id=\"dokument\"><h1>Tittel</h1>{kropp}</main></body></html>";

    private static string Del(int nr, string romertall, string innhold) =>
        $"<section class=\"section\" id=\"kapittel-{nr}\"><h2>{romertall}</h2>{innhold}</section>";

    private static readonly DateOnly Idag = new(2026, 11, 25);

    [Fact]
    public void Rettsgebyret_SF_2025_12_17_2649_er_rent_endringsvedtak()
    {
        var html = Html(Del(1, "I",
                "<article class=\"defaultP\">I forskrift 15. februar 1983 nr. 86 etter rettsgebyrloven m.m. (rettsgebyrforskriften) gjøres følgende endring:</article>"
                + "<article class=\"defaultP\">§ 2-1 skal lyde:</article><article class=\"legalP\" id=\"kapittel-1-ledd-1\">Rettsgebyret er 1&#xa0;345 kroner.</article>")
            + Del(2, "II", "<article class=\"legalP\" id=\"kapittel-2-ledd-1\">Forskriften trer i kraft 1. januar 2026.</article>"));
        var r = Vedtaksform.Klassifiser("Forskrift om endring i rettsgebyrforskriften", html, 1);
        Assert.Equal(Vedtaksform.RentEndringsvedtak, r.Klasse);
        Assert.Equal("normalisering", r.Kunnskapsstatus);
        var roller = VedtaksformTjeneste.NodeRoller(r);
        Assert.Equal(Vedtaksform.RolleSitertTekst, roller["kapittel-1-ledd-1"]);
        Assert.Equal(Vedtaksform.RolleIkrafttredelse, roller["kapittel-2-ledd-1"]);
    }

    [Fact]
    public void Revisorloven_DEL_1999_06_25_711_ikrafttredelse_og_overgangsregler_er_blandet_med_grunnlag()
    {
        var html = Html("<article class=\"legalArticle\" id=\"paragraf-1\"><h2 class=\"legalArticleHeader\">§ 1. Ikrafttredelse</h2>"
            + "<article class=\"legalP\" id=\"paragraf-1-ledd-1\">Lov 15. januar 1999 nr. 2 om revisjon og revisorer (revisorloven) trer i kraft 1. august 1999. "
            + "Fra samme tidspunkt oppheves forskrift 19. september 1990 nr. 766 om revisjon og revisorer.</article>"
            + "<article class=\"legalP\" id=\"paragraf-1-ledd-2\">Revisorloven kapittel 4 og 5 gjelder ikke for revisors revisjon av årsregnskap for regnskapsåret 1998.</article></article>"
            + "<article class=\"legalArticle\" id=\"paragraf-2\"><h2 class=\"legalArticleHeader\">§ 2. Overgangsregler</h2>"
            + "<article class=\"legalP\" id=\"paragraf-2-ledd-1\">Revisorloven § 3-3 første til sjette ledd gjelder ikke for personer som nevnt i lovens § 11-1 annet ledd.</article>"
            + "<article class=\"changesToParent\">Endret ved forskrift 21 juni 2001 nr. 673.</article></article>");
        var r = Vedtaksform.Klassifiser(
            "Forskrift om ikrafttredelse av og overgangsregler til lov av 15. januar 1999 nr. 2 om revisjon og revisorer (revisorloven).", html, 3);
        Assert.Equal(Vedtaksform.Blandet, r.Klasse);
        Assert.Equal("paragraf-1-ledd-2", r.BevisHtmlId);
        Assert.Contains("gjelder ikke", r.Bevis);
        Assert.Contains("paragraf-1-ledd-2", r.Grunnlag);
    }

    [Fact]
    public void Ikraftsetting_LTI_2007_12_07_1371_er_rent_ikrafttredelsesvedtak()
    {
        var html = Html("<ul class=\"defaultList\"><li><article class=\"listArticle\" id=\"punkt-1\"><article class=\"legalP\" id=\"punkt-1-ledd-1\">"
            + "Lov 15. desember 2006 nr. 72 om endringer i lov 19. juni 1964 nr. 14 om avgift på arv og visse gaver del I skal gjelde fra 1. januar 2008.</article></article></li>"
            + "<li><article class=\"listArticle\" id=\"punkt-2\"><article class=\"legalP\" id=\"punkt-2-ledd-1\">Lov 15. desember 2006 nr. 73 om endringer i lov 19. juni 1969 "
            + "nr. 66 om merverdiavgift del III, med unntak av § 28 fjerde ledd første punktum, skal gjelde fra 1. januar 2008.</article></article></li></ul>");
        var r = Vedtaksform.Klassifiser("Ikrafttredelse av lovendringer 15. desember 2006 som følge av reorganiseringen av skatteetaten", html, 0);
        Assert.Equal(Vedtaksform.RentIkrafttredelsesvedtak, r.Klasse);
        // Punktet har id-en til listeelementet i nodene.
        Assert.Equal(Vedtaksform.RolleIkrafttredelse, VedtaksformTjeneste.NodeRoller(r)["punkt-2"]);
    }

    [Fact]
    public void Oppheving_av_delegeringsvedtak_LTI_2015_03_17_228_er_rent_opphevingsvedtak()
    {
        var html = Html(Del(1, "I", "<article class=\"legalP\" id=\"kapittel-1-ledd-1\">Vedtak 17. februar 2006 nr. 173 om delegering av myndighet til Mattilsynet "
                + "etter matloven § 9 oppheves.</article>")
            + Del(2, "II", "<article class=\"legalP\" id=\"kapittel-2-ledd-1\">Vedtaket trer i kraft straks.</article>"));
        Assert.Equal(Vedtaksform.RentOpphevingsvedtak,
            Vedtaksform.Klassifiser("Vedtak om oppheving av delegering av myndighet til Mattilsynet etter matloven § 9", html, 1).Klasse);
    }

    [Fact]
    public void Endringslov_med_hjemmel_for_overgangsregler_NL_2025_06_20_111_er_blandet()
    {
        var html = Html(
            "<article class=\"defaultP\">Endringer i følgende lover:<ol class=\"defaultList\"><li><article class=\"listArticle\" id=\"punkt-1\">"
            + "<article class=\"legalP\" id=\"punkt-1-ledd-1\">Lov 21. juni 2002 nr. 45 om yrkestransport med motorvogn og fartøy (yrkestransportlova).</article></article></li></ol></article>"
            + Del(1, "I", "<article class=\"document-change\" id=\"kapittel-1-dokumentendring-1\"><article class=\"defaultP\">I lov 21. juni 2002 nr. 45 om yrkestransport med "
                + "motorvogn og fartøy gjøres følgende endringer:</article><article class=\"change\" id=\"c1\"><article class=\"defaultP\">§ 9 andre ledd skal lyde:</article>"
                + "<article class=\"numberedLegalP\" id=\"kapittel-1-dokumentendring-1-endring-1-nummer-1\">(2) Løyvehavaren skal sikre at betaling vert registrert.</article></article></article>")
            + Del(3, "III", "<ol class=\"defaultList\"><li><article class=\"listArticle\" id=\"kapittel-3-punkt-1\"><article class=\"legalP\" id=\"kapittel-3-punkt-1-ledd-1\">"
                + "Loven gjelder fra den tid<sup class=\"footnotereference\">1</sup> Kongen bestemmer.</article></article></li>"
                + "<li><article class=\"listArticle\" id=\"kapittel-3-punkt-2\"><article class=\"legalP\" id=\"kapittel-3-punkt-2-ledd-1\">Departementet kan gi nærmere "
                + "overgangsbestemmelser.</article></article></li></ol>"));
        var r = Vedtaksform.Klassifiser("Lov om endringer i yrkestransportloven mv. (drosjetilbud, internkontroll, kontrollutrustning og tilknytning til sentral)", html, 2);
        Assert.Equal(Vedtaksform.Blandet, r.Klasse);
        Assert.Equal("kapittel-3-punkt-2-ledd-1", r.BevisHtmlId);
        var roller = VedtaksformTjeneste.NodeRoller(r);
        Assert.Equal(Vedtaksform.RolleSitertTekst, roller["kapittel-1-dokumentendring-1-endring-1-nummer-1"]);
        Assert.Equal(Vedtaksform.RolleIkrafttredelse, roller["kapittel-3-punkt-1"]);
        Assert.False(roller.ContainsKey("kapittel-3-punkt-2")); // eget innhold lagres ikke
        Assert.False(roller.ContainsKey("punkt-1")); // listen over endrede lover er ikke tekst i vedtaket
    }

    [Fact]
    public void Delegering_med_myndighet_til_a_oppheve_DEL_2000_12_01_1200_er_ikke_rent_D4()
    {
        var html = Html(Del(1, "I", "<article class=\"legalP\" id=\"kapittel-1-ledd-1\">Sosial- og helsedepartementet gis myndighet til å oppheve eller endre forskrifter "
                + "gitt med hjemmel i lov av 19. juni 1969 nr. 57 om sykehus m.v. og med hjemmel i de lover som oppheves ved ikrafttredelse av lov av 2. juli 1999 nr. 64.</article>")
            + Del(2, "II", "<article class=\"legalP\" id=\"kapittel-2-ledd-1\">Delegeringen trer i kraft 1. januar 2001.</article>"));
        Assert.Equal(Vedtaksform.Selvstendig,
            Vedtaksform.Klassifiser("Delegering av myndighet til Sosial- og helsedepartementet etter sykehusloven og helsepersonelloven.", html, 0).Klasse);
    }

    [Fact]
    public void Oppheving_med_ny_delegering_DEL_1999_12_10_1279_er_ikke_rent_D4()
    {
        var html = Html(Del(1, "I", "<article class=\"legalP\" id=\"kapittel-1-ledd-1\">Med hjemmel i lov av 11. juni 1993 nr. 101 om luftfart § 15-3, vedtas at myndighet "
                + "delegert til Luftfartsverket etter kgl.res. 15. mars 1996 nr. 270 oppheves og myndigheten delegeres til Luftfartstilsynet.</article>")
            + Del(2, "II", "<article class=\"legalP\" id=\"kapittel-2-ledd-1\">Vedtaket trer i kraft 1. januar 2000.</article>"));
        Assert.Equal(Vedtaksform.Selvstendig,
            Vedtaksform.Klassifiser("Delegering av myndighet til Luftfartstilsynet etter luftfartsloven vedrørende luftfart på Svalbard og Jan Mayen.", html, 3).Klasse);
    }

    [Fact]
    public void Overgangsregler_om_anvendelse_av_endringer_SF_2012_06_08_499_er_ikke_rent_D5()
    {
        var html = Html("<article class=\"legalArticle\" id=\"kapittel-1-paragraf-1\"><h3>§ 1</h3><article class=\"legalP\" id=\"kapittel-1-paragraf-1-ledd-1\">Endringer "
            + "i lov 16. juli 1999 nr. 69 om offentlige anskaffelser får anvendelse på anskaffelser der konkurransen kunngjøres etter lovens ikrafttredelse.</article></article>"
            + "<article class=\"legalArticle\" id=\"kapittel-2-paragraf-1\"><h3>§ 3</h3><article class=\"legalP\" id=\"kapittel-2-paragraf-1-ledd-1\">Denne forskrift trer "
            + "i kraft 1. juli 2012.</article></article>");
        Assert.Equal(Vedtaksform.Selvstendig, Vedtaksform.Klassifiser("Forskrift om overgangsregler for lov 11. mai 2012 nr. 25 om endringer i lov om offentlige "
            + "anskaffelser og i kommuneloven", html, 2).Klasse);
    }

    [Fact]
    public void Delegering_av_ikraftsettingsmyndighet_DEL_2014_06_20_794_er_ikke_rent_D5()
    {
        var html = Html("<article class=\"legalP\" id=\"ledd-1\">Delegering av Kongens myndighet etter lovvedtaket del IV til å bestemme ikrafttredelse av lovens del III "
            + "om endringer i foretakspensjonsloven § 2-3 annet ledd nytt annet punktum, ny § 4-7a, ny § 4-7b og § 11-2 nytt femte ledd delegeres til Finansdepartementet.</article>");
        Assert.Equal(Vedtaksform.Selvstendig, Vedtaksform.Klassifiser(
            "Delegering av Kongens myndighet til å bestemme ikrafttredelse av endringer i foretakspensjonsloven", html, 1).Klasse);
    }

    [Fact]
    public void Ikraftsetting_og_endring_av_kgl_res_DEL_1984_01_20_213_er_rent_endringsvedtak_U28()
    {
        var html = Html(Del(1, "I", "<article class=\"legalP\" id=\"kapittel-1-ledd-1\">Lov av 3. juni 1983 nr. 50 om endringer i lov av 31. mai 1974 nr. 19 om konsesjon "
                + "(konsesjonsloven), § 5, tredje ledd, trer i kraft straks.</article>")
            + Del(2, "II", "<article class=\"defaultP\">Kgl.res. av 13. desember 1974 om ikrafttredelse av lov 31. mai 1974 nr. 19 om konsesjon, punkt 4 skal lyde:</article>"
                + "<article class=\"legalP\" id=\"kapittel-2-ledd-1\">Den myndighet som for øvrig er tillagt Kongen i §§ 2, 3 og 6 legges til Landbruksdepartementet.</article>"));
        Assert.Equal(Vedtaksform.RentEndringsvedtak, Vedtaksform.Klassifiser("Ikrafttredelse av § 5 tredje ledd i lov av 3. juni 1983 nr. 50 om endringer i lov av "
            + "31. mai 1974 nr. 19 om konsesjon (konsesjonsloven). Delegering av myndighet.", html, 0).Klasse);
    }


    [Fact]
    public void Nytt_delegeringsvedtak_med_bare_hodesignal_er_ukjent_M03()
    {
        // DEL/forskrift/2025-06-26-1340: «endrer» det forrige vedtaket og opphever det; innholdet står bare her.
        var html = Html(Del(1, "I", "<article class=\"legalP\" id=\"kapittel-1-ledd-1\">Delegeringsvedtak 1. januar 2020 nr. 1 endres slik:</article>"
                + "<article class=\"legalP\" id=\"kapittel-1-ledd-2\">Myndighet etter energiloven § 2-1 legges til Norges vassdrags- og energidirektorat.</article>")
            + Del(2, "II", "<article class=\"legalP\" id=\"kapittel-2-ledd-1\">Vedtaket trer i kraft 1. juli 2025. Fra samme tidspunkt oppheves delegeringsvedtak "
                + "1. januar 2020 nr. 1.</article>"));
        var r = Vedtaksform.Klassifiser("Delegering av myndighet til Norges vassdrags- og energidirektorat etter energiloven", html, 1);
        Assert.Equal(Vedtaksform.Ukjent, r.Klasse);
        Assert.Equal("uavklart", r.Kunnskapsstatus);
    }

    [Fact]
    public void Overgangsregel_etter_endringsinstruks_er_eget_innhold_M40()
    {
        // NL/lov/2017-12-19-118: «– – –» etter instruksen, så en overskrift «Overgangsregel …» og regelen.
        var html = Html(Del(1, "I", "<article class=\"legalP\" id=\"kapittel-1-ledd-1\">I lov 6. juni 1975 nr 29 om eigedomsskatt til kommunane gjøres følgende "
                + "endringer: – – –</article><article class=\"defaultP\">Overgangsregel til eigedomsskattelova §§ 3 og 4:</article>"
                + "<article class=\"legalP\" id=\"kapittel-1-ledd-2\">Kommunar som i skatteåret 2018 skreiv ut eigedomsskatt på verk og bruk kan frå og med "
                + "skatteåret 2019 skrive ut eigedomsskatt på eit særskilt fastsett grunnlag.</article>")
            + Del(2, "II", "<article class=\"legalP\" id=\"kapittel-2-ledd-1\">Loven trer i kraft 1. januar 2019.</article>"));
        var r = Vedtaksform.Klassifiser("Lov om endringer i lov 6. juni 1975 nr. 29 om eigedomsskatt til kommunane (eigedomsskattelova)", html, 1);
        Assert.Equal(Vedtaksform.Blandet, r.Klasse);
        Assert.Equal("kapittel-1-ledd-2", r.BevisHtmlId);
    }

    [Fact]
    public void Tittel_med_overgangsregler_er_blandet_M29()
    {
        var html = Html("<article class=\"legalP\" id=\"ledd-1\">Lova tek til å gjelde 1. august 2003.</article>"
            + "<article class=\"legalP\" id=\"ledd-2\">For skolar som er godkjende etter privatskulelova, gjeld godkjenninga vidare.</article>");
        Assert.Equal(Vedtaksform.Blandet, Vedtaksform.Klassifiser(
            "Ikraftsetjing og overgangsreglar til lov av 4. juli 2003 nr. 84 om frittståande skolar (friskolelova).", html, 0).Klasse);
    }

    [Theory]
    [InlineData("Vedtak om endring av navn på Arkitekthøgskolen i Oslo til Arkitektur- og designhøgskolen i Oslo.", false)]
    [InlineData("Forskrift om endring av landbruksfagskulane sin struktur og plass i skulesystemet", false)]
    [InlineData("Forskrift om endring av forskrift om grunnbeløp", true)]
    [InlineData("Endring i vedtak om delegering av myndighet fra Finansdepartementet til Finanstilsynet", true)]
    [InlineData("Lov om endringar i lov om Overenskomst om internasjonal jernbanetrafikk (COTIF-lova)", true)]
    public void Tittelform_for_endring_D9(string tittel, bool forventet) => Assert.Equal(forventet, Vedtaksform.TittelHarEndringsform(tittel));

    [Fact]
    public void Uten_ra_tekst_er_ukjent_og_uavklart()
    {
        var r = Vedtaksform.Klassifiser("Vedtak om endring i stortingsvedtak 18. desember 2025 nr. 2755", null, 0);
        Assert.Equal(Vedtaksform.Ukjent, r.Klasse);
        Assert.Equal("uavklart", r.Kunnskapsstatus);
    }

    [Fact]
    public void Endringsform_uten_signal_i_tittel_eller_hode_er_ukjent()
    {
        var html = Html(Del(1, "I", "<article class=\"legalP\" id=\"kapittel-1-ledd-1\">I forskrift 1. januar 2000 nr. 1 om noe gjøres følgende endring:</article>"
            + "<article class=\"legalP\" id=\"kapittel-1-ledd-2\">Ny tekst.</article>"));
        Assert.Equal(Vedtaksform.Ukjent, Vedtaksform.Klassifiser("Vedtak om noe", html, 0).Klasse);
    }

    [Theory]
    [InlineData("2021-04-01", Vedtaksform.IkraftJa)]
    [InlineData("2027-01-01", Vedtaksform.IkraftIkkeIKraft)]
    [InlineData("2010-01-01, 2027-01-01", Vedtaksform.IkraftDelvis)]
    [InlineData("2004-01-17 – 2004-12-31", Vedtaksform.IkraftUtlopt)]
    [InlineData("Kongen bestemmer", Vedtaksform.IkraftUkjent)]
    [InlineData("2020-01-01, Kongen bestemmer", Vedtaksform.IkraftUkjent)]
    [InlineData("Inntektsåret 2021", Vedtaksform.IkraftJa)]
    [InlineData("Når endringer i overenskomst trer i kraft", Vedtaksform.IkraftUkjent)]
    public void Ikraftstatus_fra_Lovdatas_felt(string raa, string forventet) =>
        Assert.Equal(forventet, Vedtaksform.IkraftFraFelt(raa, null, Idag).Status);

    [Fact]
    public void Ikraftsetting_av_hele_loven_LTI_2021_03_26_968_leses_men_ikke_en_delvis()
    {
        var hel = Vedtaksform.Klassifiser("Ikraftsetting av lov 26. mars 2021 nr. 15 om endringer i straffegjennomføringsloven mv.",
            Html("<article class=\"legalP\" id=\"ledd-1\">Loven trer i kraft 1. april 2021.</article>"), 0);
        Assert.Equal(new DateOnly(2021, 4, 1), Vedtaksform.HelIkraftsetting(hel, Idag)?.Dato);
        var delvis = Vedtaksform.Klassifiser("Delvis ikraftsetting av lov …",
            Html("<article class=\"legalP\" id=\"ledd-1\">Lov 1. januar 2020 nr. 1 om noe § 5 trer i kraft 1. april 2021.</article>"), 0);
        Assert.Null(Vedtaksform.HelIkraftsetting(delvis, Idag));
    }

    [Theory]
    [InlineData(Vedtaksform.RentEndringsvedtak, Vedtaksform.IkraftJa, true)]
    [InlineData(Vedtaksform.RentOpphevingsvedtak, Vedtaksform.IkraftUtlopt, true)]
    [InlineData(Vedtaksform.RentEndringsvedtak, Vedtaksform.IkraftIkkeIKraft, false)]
    [InlineData(Vedtaksform.RentEndringsvedtak, Vedtaksform.IkraftDelvis, false)]
    [InlineData(Vedtaksform.RentEndringsvedtak, Vedtaksform.IkraftUkjent, null)]
    [InlineData(Vedtaksform.Blandet, Vedtaksform.IkraftJa, false)]
    [InlineData(Vedtaksform.Selvstendig, Vedtaksform.IkraftUkjent, false)]
    [InlineData(Vedtaksform.Ukjent, Vedtaksform.IkraftJa, null)]
    [InlineData(null, null, null)]
    public void Uttomt_virkning_valg_C(string? klasse, string? ikraft, bool? forventet) =>
        Assert.Equal(forventet, Vedtaksform.UttomtVirkning(klasse, ikraft));
}
