using static RegelIde.Data.Tests.HjemmelsstatusTests;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #502, prosjekteierens avgjørelse 2026-11-27] Flere former for hel oppheving i R8 og den delte tekstlesingen
/// (<see cref="Opphevingskontroll.DelvisMal"/>, <see cref="Hjemmelstekstlesing.LesOppheving"/>). Spørsmålene modellen skal kunne besvare
/// (CLAUDE.md §0):
/// <list type="bullet">
/// <item>Leses «Delegering ‹dato› nr. N av myndighet …» og «oppheves 1. januar 2019» som hele opphevinger, slik ordlyden sier?</item>
/// <item>Leses fortsatt det som avgrenser opphevingen, som delvis: et annet ord etter «av», «fra og med», en dato som ikke er passert, og en
/// dato som følges av noe annet?</item>
/// <item>Avvises de setningene der datoen etter verbet var det eneste som stoppet en feil lesing før (tillatelser og regler gitt i medhold av
/// målet, og «Delegeringen oppheves for saker der …»)?</item>
/// </list>
/// Setningene er fra korpus, med kilden i kommentaren. Sveipet over alle (node, mål) i en kopi av basen står i
/// <c>data/fasit/strukturmodell/juristrunde-472/maling-4/r8-setninger.tsv</c>.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class R8FlereFormerTests(EmbeddedPostgresFixture fixture)
{
    private static Dokumentmal Mal(int aar, int mnd, int dag, string nr) => new("forskrift", aar, mnd, dag, nr);

    private static bool Hel(string tekst, int aar, int mnd, int dag, string nr) =>
        Hjemmelstekstlesing.LesOppheving(tekst, Mal(aar, mnd, dag, nr)).Lesning is not null;

    [Theory]
    // Formen «av» + det som delegeres, og datoen etter verbet (DEL/forskrift/2018-12-20-2052 punkt 5, de fire LTI-funnene i #472).
    [InlineData("Delegering 29. juni 2001 nr. 721 av myndighet til Forsvarsdepartementet etter sikkerhetsloven § 12 andre ledd, § 15 fjerde ledd, § 16 andre ledd og § 29 oppheves 1. januar 2019.", 2001, 6, 29, "721")]
    [InlineData("Delegering 27. juni 2003 nr. 802 av myndighet til Forsvarsdepartementet etter sikkerhetsloven § 2 tredje ledd oppheves 1. januar 2019.", 2003, 6, 27, "802")]
    [InlineData("Delegering 12. februar 2004 nr. 1221 av myndighet til Petroleumstilsynet etter sikkerhetsloven 23 første ledd oppheves 1. januar 2019.", 2004, 2, 12, "1221")]
    // Bare datoen etter verbet.
    [InlineData("Kronprinsregentens resolusjon 4. juli 2003 nr. 900 om fordelingen av ansvar for forebyggende sikkerhetstjeneste og Nasjonal sikkerhetsmyndighet oppheves 1. januar 2019.", 2003, 7, 4, "900")]
    [InlineData("Forskrift 26. oktober 2021 nr. 3092 om nasjonal retningslinje for barnesykepleierutdanning oppheves 1. januar 2026.", 2021, 10, 26, "3092")]
    // Bare «av» + det som delegeres (LTI/forskrift/2021-11-14-3414, LTI/forskrift/2018-12-14-1920, DEL/forskrift/2008-12-19-1538, LTI/forskrift/2021-03-16-1184).
    [InlineData("Delegering 16. april 2018 nr. 852 av myndighet til Kirkerådet til å foreta endringer i kirkevalgreglene på bakgrunn av Kirkemøtets føringer, oppheves.", 2018, 4, 16, "852")]
    [InlineData("Følgende delegeringsvedtak oppheves: Delegering 11. september 2014 nr. 1192 av departementets myndighet til å utrede adopsjonssøkere til utenlandsadopsjon til Barne-, ungdoms- og familieetaten og overgangsregler.", 2014, 9, 11, "1192")]
    [InlineData("Delegeringen har virkning fra 1. januar 2009. Fra samme tidspunkt oppheves Finansdepartementets delegering 26. september 2005 nr. 1093 av utøvelsen av statens partsstilling i prosesser på Skattedirektoratets område.", 2005, 9, 26, "1093")]
    [InlineData("Delegering 17. november 1999 nr. 1578 av fullmakt til Kirkerådets direktør til å fastsette regler om ordinasjon og tilsetting av personer som ikke er cand.theol. oppheves.", 1999, 11, 17, "1578")]
    // Utstederen etter «fra» (LTI/forskrift/2019-07-02-961 kap. II, LTI/forskrift/2001-02-09-125 kap. I).
    [InlineData("Instruksen trer i kraft straks. Samtidig oppheves instruks 18. mars 2017 nr. 357 fra Mattilsynet ved Hovedkontoret til Debio og det regionale Mattilsynet for utøving av delegert myndighet etter økologiforskriften.", 2017, 3, 18, "357")]
    [InlineData("Veiledende retningslinjer av 26. januar 1984 nr. 12 fra Finansdepartementet om delegasjon av myndighet fra ligningsnemnd til ligningskontor oppheves.", 1984, 1, 26, "12")]
    public void Nye_former_leses_som_hele_opphevinger(string tekst, int aar, int mnd, int dag, string nr)
    {
        Assert.True(Hel(tekst, aar, mnd, dag, nr));
        // Samme svar i byggesteinen R8 hadde før #472 PR 3, så kildefeilkøen og hjemmelsstatusen leser likt.
        Assert.NotNull(Opphevingskontroll.FullOpphevingssetning(tekst, new DateOnly(aar, mnd, dag), nr));
    }

    /// <summary>Passiv form med dato (DEL/forskrift/2018-05-04-704 kap. II): bare den utvidede formen i den delte lesingen leser passiv.</summary>
    [Fact]
    public void Passiv_form_med_dato_etter_verbet_er_hel() =>
        Assert.True(Hel("Delegeringsvedtak 18. juni 2004 nr. 907 blir opphevet 1. oktober 2018.", 2004, 6, 18, "907"));

    [Theory]
    // Et annet ord etter «av» enn det som delegeres.
    [InlineData("Forskrift 1. mars 2005 nr. 183 av betydning for fisket i Skagerrak oppheves.", 2005, 3, 1, "183")]
    // «fra og med» etter nummeret er en avgrensning, ikke en utsteder.
    [InlineData("Forskrift 1. mars 2005 nr. 183 fra og med § 3 oppheves.", 2005, 3, 1, "183")]
    // En dato etter verbet som følges av noe annet enn slutten på setningen (SF/forskrift/2005-06-30-745 § 8).
    [InlineData("Forskrift 24. januar 1989 nr. 59 om årsoppgjør for boligbyggelag og borettslag oppheves 1. januar 2006 med virkning for regnskapsår påbegynt 1. januar 2006 eller senere.", 1989, 1, 24, "59")]
    // En dato etter verbet som følges av «nr.», er et dokument.
    [InlineData("Forskrift 1. mars 2005 nr. 183 oppheves 1. januar 2019 nr. 5.", 2005, 3, 1, "183")]
    // En dato som ikke er passert: opphevingen virker ikke ennå.
    [InlineData("Forskrift 9. oktober 2015 nr. 1166 om tildeling av tilskudd til private barnehager oppheves 31. desember 2999.", 2015, 10, 9, "1166")]
    // Formene fra før som fortsatt er delvise (#429 PR 4 (1a)).
    [InlineData("Vedtak 10. januar 2020 nr. 33 om delegering oppheves med unntak av punkt 3.", 2020, 1, 10, "33")]
    [InlineData("Kgl.res. 10. januar 2020 nr. 33 nr. 4 oppheves 1. januar 2021.", 2020, 1, 10, "33")]
    public void Det_som_avgrenser_er_fortsatt_delvis(string tekst, int aar, int mnd, int dag, string nr)
    {
        Assert.False(Hel(tekst, aar, mnd, dag, nr));
        Assert.Null(Opphevingskontroll.FullOpphevingssetning(tekst, new DateOnly(aar, mnd, dag), nr));
    }

    [Theory]
    // Tillatelser gitt i medhold av målet oppheves, ikke målet (SF/forskrift/2017-06-15-844 § 144 første ledd). Før #502 stoppet bare datoen
    // etter verbet lesingen; nå avviser vakten den.
    [InlineData("(1) Tillatelser gitt med hjemmel i forskrift 22. mars 1977 nr. 1 om eksplosive varer, forskrift 12. november 1991 nr. 732 om erverv, handel og innførsel av eksplosiv vare oppheves 1. januar 2019. Slike tillatelser gitt for Svalbard, er fortsatt gjeldende.", 1977, 3, 22, "1")]
    [InlineData("(1) Tillatelser gitt med hjemmel i forskrift 22. mars 1977 nr. 1 om eksplosive varer, forskrift 12. november 1991 nr. 732 om erverv, handel og innførsel av eksplosiv vare oppheves 1. januar 2019. Slike tillatelser gitt for Svalbard, er fortsatt gjeldende.", 1991, 11, 12, "732")]
    // Reglene om ett fag i loven oppheves, ikke loven (DEL/forskrift/1997-03-21-228 kap. IV).
    [InlineData("Reglane om røyrleggjarfaget i lov 19. juni 1970 nr. 59 om handverksnæring med forskrifter, gjort gjeldande i kongeleg resolusjon av 12. desember 1986 nr. 2175 punkt 2, blir oppheva 1. april 1997.", 1970, 6, 19, "59")]
    // Forskrifter gitt i medhold av loven oppheves, ikke loven (DEL/forskrift/2002-12-20-1627 § 1). Denne ble godtatt før #502.
    [InlineData("Fra samme tidspunkt oppheves forskrifter gitt i medhold av lov av 14. juni 1985 nr. 62 om Verdipapirsentral.", 1985, 6, 14, "62")]
    // Neste setning avgrenser opphevingen til nye saker (LTI/forskrift/2016-12-13-1596 kap. II andre ledd).
    [InlineData("Fra samme tid oppheves:Delegering 18. mars 2010 nr. 425 av myndighet fra Helse- og omsorgsdepartementet til Helsedirektoratet etter tvisteloven § 2-5. Delegeringen oppheves for saker der stevning er inngitt etter 31. desember 2016.", 2010, 3, 18, "425")]
    public void Det_som_bare_datoen_stoppet_for_avvises_fortsatt(string tekst, int aar, int mnd, int dag, string nr) =>
        Assert.False(Hel(tekst, aar, mnd, dag, nr));

    [Theory]
    // «regler» er dokumenttypen til målet (SF/forskrift/2017-08-21-1331 § 17), ikke noe gitt i medhold av det.
    [InlineData("Fra samme tidspunkt oppheves regler 6. januar 1995 nr. 18 om tiltak og undersøkelser m.v. ved ulykker og hendelser i Forsvaret.", 1995, 1, 6, "18")]
    // [LÅST, #503] «Opphevelsen gis virkning for …» (arveavgiftsloven, M04 og M06) leses som før, til #503 er avgjort.
    [InlineData("Lov 19. juni 1964 nr. 14 om avgift på arv og visse gaver (arveavgiftsloven) oppheves. Opphevelsen gis virkning for gaver som ytes 1. januar 2014 eller senere og arv etter dødsfall som skjer 1. januar 2014 eller senere.", 1964, 6, 19, "14")]
    public void Hele_opphevinger_fra_for_er_fortsatt_hele(string tekst, int aar, int mnd, int dag, string nr) =>
        Assert.True(Hel(tekst, aar, mnd, dag, nr));

    /// <summary>R8 i #429 og #472: et LTI-vedtak som en annen kilde opphever i formen «Delegering ‹dato› nr. N av myndighet … oppheves 1. januar
    /// 2019.», er opphevet av den andre kilden (DEL/forskrift/2018-12-20-2052 punkt 5 nr. 3 om LTI/forskrift/2003-06-27-802).</summary>
    [Fact]
    public async Task R8_Er_et_LTI_vedtak_opphevet_i_formen_av_myndighet_oppheves_dato_Ja()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        DateOnly dVedtak = new(2003, 6, 27);
        var nVedtak = Nr();
        var vedtak = await k.DokumentAsync("forskrift", dVedtak, nVedtak, "LTI", "Delegering av myndighet til Forsvarsdepartementet etter sikkerhetsloven § 2 tredje ledd.",
            [("ledd-1", "Myndigheten delegeres til Forsvarsdepartementet.", null)]);
        var opphever = await k.DokumentAsync("forskrift", new(2018, 12, 20), Nr(), "DEL", "Delegering av myndighet etter sikkerhetsloven.",
        [
            ("punkt-5", "Følgende vedtak oppheves:", null),
            ("punkt-5/punkt-3", $"Delegering {Ref(dVedtak, nVedtak)} av myndighet til Forsvarsdepartementet etter sikkerhetsloven § 2 tredje ledd oppheves 1. januar 2019.", null),
        ]);

        // Uten «Endrer» godtar ikke R8 setningen.
        Assert.Empty(await Opphevingskontroll.OpphevetAvAnnenKildeAsync(db, [(vedtak.Id, vedtak.Eli!)]));

        await k.EndrerAsync(opphever, vedtak);
        var o = Assert.Single(await Opphevingskontroll.OpphevetAvAnnenKildeAsync(db, [(vedtak.Id, vedtak.Eli!)]));
        Assert.Equal(opphever.Id, o.Value.OpphevendeId);
        Assert.EndsWith("/punkt-5/punkt-3", o.Value.Eid);
        Assert.EndsWith("oppheves 1. januar 2019.", o.Value.Sitat);
    }
}
