using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, issue #341, Johanns beslutning 3 2026-10-08] Leksikonet styrer mønsterlaget: hvert mønster har nøyaktig én regel og
/// omvendt, typene er gyldige i kontrakten (FORMAT.md), familien er typens, og normform står bare på normgivning. KI-instruksen
/// lister uttrykkene (KI skal bare foreslå for det leksikonet IKKE kjenner). Ingen database.
/// </summary>
public class KompetanseleksikonTests
{
    [Fact]
    public void Hvert_monster_har_en_regel_og_hver_regel_et_monster()
    {
        var monstre = MonsterStrukturkonverterer.Monstre.Select(m => m.Id).ToList();
        var regler = Kompetanseleksikon.Regler.Select(r => r.Id).ToList();
        Assert.Equal(regler.Count, regler.Distinct().Count());
        Assert.Equal(monstre.Order(), regler.Order());
        Assert.False(string.IsNullOrWhiteSpace(Kompetanseleksikon.Versjon));
    }

    [Fact]
    public void Monstrene_far_kategori_type_og_normform_fra_leksikonet()
    {
        foreach (var m in MonsterStrukturkonverterer.Monstre)
        {
            var r = Kompetanseleksikon.For(m.Id);
            Assert.Equal((r.Kategori, r.Type, r.Normform), (m.Kategori, m.Type, m.Normform));
        }
    }

    [Fact]
    public void Reglene_er_gyldige_i_kontrakten_og_familien_er_typens()
    {
        foreach (var r in Kompetanseleksikon.Regler)
        {
            Assert.True(Strukturkontrakt.ErGyldigType(r.Kategori, r.Type), $"{r.Id}: {r.Kategori}/{r.Type}");
            Assert.NotEmpty(r.Uttrykk);
            if (r.Normform is not null)
            {
                Assert.Equal("normgivningskompetanse", r.Type);
                Assert.Contains(r.Normform, Strukturkontrakt.Normformer);
            }
            if (r.Kategori == "kompetanse")
            {
                var type = Assert.Single(Strukturkanter.Kompetansetyper, t => t.FasitType == r.Type);
                Assert.Equal(type.Familie, r.Familie);
            }
            else
            {
                Assert.Null(r.Familie);
            }
        }
        // Johanns eksempler i beslutning 3.
        Assert.Equal(("normgivningskompetanse", "forskrift"), (Kompetanseleksikon.For("forskrift-gi").Type, Kompetanseleksikon.For("forskrift-gi").Normform));
        Assert.Equal("klagekompetanse", Kompetanseleksikon.For("klage-er-klageinstans").Type);
        Assert.Equal("beslutningskompetanse", Kompetanseleksikon.For("beslutning-beslutningsmyndighet").Type);
        Assert.Equal("samordningskompetanse", Kompetanseleksikon.For("samordning-samordne").Type);
    }

    [Fact]
    public void KI_instruksen_lister_leksikonet_og_ukjent()
    {
        Assert.Contains(Kompetanseleksikon.Versjon, KiStrukturkonverterer.SystemInstruks);
        foreach (var uttrykk in Kompetanseleksikon.Regler.SelectMany(r => r.Uttrykk))
        {
            Assert.Contains(uttrykk, KiStrukturkonverterer.SystemInstruks);
        }
        Assert.Contains($"\"{Strukturkontrakt.Ukjent}\"", KiStrukturkonverterer.SystemInstruks);
    }

    [Theory]
    [InlineData("Kongen kan gi forskrift om tilsyn.", true)]
    [InlineData("Kongen i statsråd kan gi forskrift om tilsyn.", false)]
    [InlineData("Kommunestyret selv kan gi forskrift om gebyrer.", false)]
    [InlineData("Departementet kan gi forskrift om gebyrer.", null)]
    public void Delegerbar_avgjores_paa_sitatet(string tekst, bool? forventet)
    {
        var d = new MonsterStrukturkonverterer().Konverter(new Strukturkonverteringsgrunnlag("test", "https://lovdata.no/eli/lov/test/nor",
            [new Strukturnode("test", "https://lovdata.no/eli/lov/test/nor", "https://lovdata.no/eli/lov/test/nor/§1/ledd-1", "ledd", null, tekst)]));
        var u = Assert.Single(d.Utsagn, x => x.Type == "normgivningskompetanse");
        Assert.Equal(forventet, u.Delegerbar);
        Assert.Equal("forskrift", u.Normform);
    }

    [Fact]
    public void Beslutningsmyndighet_og_samordne_gir_de_nye_typene_men_passiv_samordnes_ikke()
    {
        Utsagn("Sametinget har beslutningsmyndighet når dette følger av andre bestemmelser.", "beslutningskompetanse", "Sametinget");
        Utsagn("Kommunen skal samordne tjenestetilbudet etter første ledd.", "samordningskompetanse", "Kommunen");
        var passiv = Konverter("Planen skal samordnes med kommunens øvrige beredskapsplaner.");
        Assert.DoesNotContain(passiv.Utsagn, u => u.Type == "samordningskompetanse");

        static void Utsagn(string tekst, string type, string fra)
        {
            var d = Konverter(tekst);
            var u = Assert.Single(d.Utsagn, x => x.Type == type);
            Assert.Equal(fra, d.Aktorer.Single(a => a.Id == u.Fra).Tekstform);
        }
    }

    /// <summary>[Ny, issue #352, Johanns beslutning 1] Verbene står i leksikonet, hver undertype finnes i
    /// <see cref="Strukturkanter.Undertyper"/> og omvendt, og stammene avgjør undertypen på sitatet uten å gjette.</summary>
    [Fact]
    public void Undertypene_med_verb_star_i_leksikonet_og_samsvarer_med_typemodellen()
    {
        foreach (var u in Kompetanseleksikon.Undertyper)
        {
            Assert.True(Strukturkontrakt.ErGyldigUndertype(u.Type, u.Undertype), $"{u.Type}/{u.Undertype}");
            Assert.False(string.IsNullOrWhiteSpace(u.Verb));
            Assert.NotEmpty(u.Stammer);
        }
        foreach (var (kode, undertyper) in Strukturkanter.Undertyper)
        {
            var fasitType = Strukturkanter.Kompetansetyper.Single(t => t.Kode == kode).FasitType;
            Assert.Equal(undertyper.Order(), Kompetanseleksikon.Undertyper.Where(u => u.Type == fasitType).Select(u => u.Undertype).Order());
        }
        // Johanns fire verb, [ENDRET, #355] pluss utnevner og konstituerer.
        Assert.Equal(["ansetter", "konstituerer", "oppnevner", "utnevner", "utpeker", "velger"],
            Kompetanseleksikon.Undertyper.Where(u => u.Type == "oppnevningskompetanse").Select(u => u.Verb).Order());
        // Familien heter oppnevning; vedtak-godkjennes-av er godkjenning (Johanns beslutning 3).
        Assert.Equal("oppnevning", Kompetanseleksikon.For("oppnevnt-av").Familie);
        Assert.Equal(("godkjenningskompetanse", "styring"),
            (Kompetanseleksikon.For("vedtak-godkjennes-av").Type, Kompetanseleksikon.For("vedtak-godkjennes-av").Familie));
    }

    [Theory]
    [InlineData("Forliksrådsmedlemmer med varamedlemmer velges av kommunestyret selv.", "valg")]
    [InlineData("Styret tilsetter leder for internrevisjonen", "ansettelse")]
    [InlineData("Helseinstitusjon som omfattes av denne loven, skal peke ut kontaktlege", "utpeking")]
    [InlineData("Kongen oppnevner medlemmene av Tilsynsutvalget", "oppnevning")]
    [InlineData("Dommere utnevnes som embetsmenn av Kongen", "utnevning")] // [ENDRET, #355] utnevning (embete) er en undertype
    [InlineData("Konstitusjoner med varighet inntil tre måneder kan foretas av domstollederen.", "konstitusjon")] // [Ny, #355]
    [InlineData("Sametinget konstituerende møte", null)] // [Ny, #355] «konstituerende» er ikke konstitusjon
    [InlineData("Finner han valget lovlig, utferdiger han oppnevnelse for de valgte", null)] // to verb: gjettes ikke
    public void Undertypen_avgjores_paa_sitatet(string sitat, string? forventet) =>
        Assert.Equal(forventet, Kompetanseleksikon.UndertypeFor("oppnevningskompetanse", sitat));

    /// <summary>[Ny, issue #355, Johanns beslutning 1 og 2] Avslutning speiler innsetting: avsetting/oppsigelse/avskjed på
    /// avsettingskompetanse, tilbakekall på vedtakskompetanse. To verb i samme sitat gir null — gjettes ikke (u118 i fasiten er
    /// derfor delt i to rader, L13).</summary>
    [Theory]
    [InlineData("avsettingskompetanse", "Kongen kan avsette styret dersom det ikke følger opp kritikk", "avsetting")]
    [InlineData("avsettingskompetanse", "Et styremedlem kan avsettes av den som har valgt styremedlemmet", "avsetting")]
    [InlineData("avsettingskompetanse", "Dommere kan ikke sies opp", "oppsigelse")]
    [InlineData("avsettingskompetanse", "kan bare avskjediges etter rettergang og dom", "avskjed")]
    [InlineData("avsettingskompetanse", "Styret treffer vedtak om å si opp eller avskjedige daglig leder", null)]
    [InlineData("vedtakskompetanse", "Departementet kan tilbakekalle godkjenning", "tilbakekall")]
    [InlineData("vedtakskompetanse", "Departementet eller reguleringsmyndigheten kan trekke tilbake en konsesjon", "tilbakekall")]
    [InlineData("vedtakskompetanse", "Departementet kan gi pålegg om retting", null)]
    public void Avslutningens_og_tilbakekallets_undertype_avgjores_paa_sitatet(string type, string sitat, string? forventet) =>
        Assert.Equal(forventet, Kompetanseleksikon.UndertypeFor(type, sitat));

    [Fact]
    public void Monsterlaget_setter_undertypen_paa_oppnevning()
    {
        var d = Konverter("Kongen oppnevner medlemmene av Innstillingsrådet med personlige varamedlemmer.");
        var u = Assert.Single(d.Utsagn, x => x.Type == "oppnevningskompetanse");
        Assert.Equal("oppnevning", u.Undertype);
    }

    /// <summary>[Ny, issue #353] De fem pliktuttrykkene i saken gir plikt med riktig type, retning og modalitet — og
    /// betalingsmottakeren settes aldri.</summary>
    [Theory]
    [InlineData("Reguleringsmyndigheten skal samarbeide med andre lands reguleringsmyndigheter og internasjonale institusjoner.",
        "samarbeidsplikt", "Reguleringsmyndigheten", "andre lands reguleringsmyndigheter|internasjonale institusjoner", "skal")]
    [InlineData("Kommunestyret selv skal inngå samarbeidsavtale med det regionale helseforetaket i helseregionen eller med helseforetak som det regionale helseforetaket bestemmer.",
        "avtaleplikt", "Kommunestyret", "det regionale helseforetaket i helseregionen", "skal")]
    // [ENDRET, juristgjennomgangen 2026-10-09] Genitiven sier hvem utgiftene er sine: til = det behandlende RHF-et.
    [InlineData("Det regionale helseforetakets behandlingsutgifter skal dekkes av det regionale helseforetaket i pasientens bostedsregion, jf. § 5-1.",
        "betalingsplikt", "det regionale helseforetaket i pasientens bostedsregion", "Det regionale helseforetaket", "skal")]
    // «som påføres Y» sier det også; normativ presens «dekkes» = skal.
    [InlineData("De særlige utgifter som påføres fylkeskommuner og kommuner ved valg til Sametinget dekkes av staten.",
        "betalingsplikt", "staten", "fylkeskommuner|kommuner", "skal")]
    // Formål («utgifter til …»): ingen mottaker.
    [InlineData("Utgifter til gjennomføring av tvungent psykisk helsevern skal dekkes av staten.", "betalingsplikt", "staten", null, "skal")]
    [InlineData("Folketrygden skal dekke behandlings- og forpleiningsutgifter for pasient som ikke har bosted i riket.",
        "betalingsplikt", "Folketrygden", null, "skal")]
    [InlineData("Staten dekker utgiftene til kontrollkommisjonenes virksomhet.", "betalingsplikt", "Staten", null, "skal")] // [ENDRET] presens = skal
    [InlineData("Systemansvarlig skal gi opplysninger til reguleringsmyndigheten.", "informasjonsplikt", "Systemansvarlig", "reguleringsmyndigheten", "skal")]
    [InlineData("Kommunen bør innhente uttalelse fra Sametinget før vedtak treffes.", "konsultasjonsplikt", "Kommunen", "Sametinget", "bor")]
    [InlineData("Kommunen kan samarbeide med andre kommuner om ansettelse av kommunelege.", "samarbeidsplikt", "Kommunen", "andre kommuner", "kan")]
    public void Pliktuttrykkene_gir_plikt_med_modalitet(string tekst, string type, string fra, string? til, string? modalitet)
    {
        var d = Konverter(tekst);
        var utsagn = d.Utsagn.Where(u => u.Type == type).ToList();
        Assert.NotEmpty(utsagn);
        Assert.All(utsagn, u => Assert.Equal(("plikt", modalitet), (u.Kategori, u.Modalitet)));
        Assert.All(utsagn, u => Assert.Equal(fra, d.Aktorer.Single(a => a.Id == u.Fra).Tekstform));
        var tilFormer = utsagn.Select(u => u.Til is null ? null : d.Aktorer.Single(a => a.Id == u.Til).Tekstform).ToList();
        Assert.Equal(til?.Split('|') ?? [null], tilFormer);
        // Én kant per subjekt — aldri en motsatt kant (gjensidighet sluttes ikke).
        Assert.DoesNotContain(d.Utsagn, u => u.Kategori == "plikt" && u.Fra is not null && d.Aktorer.Single(a => a.Id == u.Fra).Tekstform != fra);
    }

    [Fact]
    public void Innhente_uttalelse_uten_fra_er_saksforberedelse_og_gir_ingen_plikt()
    {
        var d = Konverter("Før det fattes vedtak, skal departementet innhente rådgivende uttalelser.");
        Assert.DoesNotContain(d.Utsagn, u => u.Kategori == "plikt");
    }

    private static Strukturdokument Konverter(string tekst) =>
        new MonsterStrukturkonverterer().Konverter(new Strukturkonverteringsgrunnlag("test", "https://lovdata.no/eli/lov/test/nor",
            [new Strukturnode("test", "https://lovdata.no/eli/lov/test/nor", "https://lovdata.no/eli/lov/test/nor/§1/ledd-1", "ledd", null, tekst)]));
}
