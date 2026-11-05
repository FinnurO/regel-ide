using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #440 PR 1] De rene reglene bak aktørkøen (<see cref="Aktorkoregler"/>), uten database. Akseptansekriteriene fra saken, som
/// spørsmål modellen skal kunne besvare:
/// <list type="number">
/// <item><b>AK1</b> «Hvor mange ukjente aktører venter?» gir antall NAVN, ikke antall forekomster, og Utdanningsdirektoratet står én gang
/// med 9 forekomster i 5 rettskilder.</item>
/// <item><b>AK2</b> Grupperingen gir samme resultat hver gang. «domstolens leder» slås aldri sammen med «leder» (bare forslag).</item>
/// <item><b>AK3</b> Summen av rutene er antall ventende. Ingen kandidat står i to ruter. En bøyningsform arver aldri regelen.</item>
/// <item><b>AK4</b> Hjemmelforslaget for foreldrerådet er bhl. § 4 første ledd. Utdanningsdirektoratet viser hullet, og veiledning tilbys
/// aldri som hjemmel.</item>
/// </list>
/// Tallene mot den delte basen (192 navn av 1 211, 0 hos Agder) er målt i PR-en; her er reglene bak dem.
/// </summary>
public class AktorkoreglerTests
{
    private static readonly Guid Barnehageloven = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Reindriftsloven = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid ForskriftUnderReindrift = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid KongenId = Guid.Parse("00000000-0000-0000-0000-00000000c001");

    /// <summary>En tabell som ligner den delte basen: tre generiske ord, Kongen i statsråd, klassen kommune og den lovspesifikke klassen
    /// «kommunene» i reindriftsloven.</summary>
    private static Rutingstabell Tabell() => new(
        new Dictionary<string, KjentOrd>
        {
            ["departementet"] = new("departementet", "generisk", "departementet", null, Guid.NewGuid(), null, "Ansvarlig departement"),
            ["kommunen"] = new("kommunen", "generisk", "kommunen", null, Guid.NewGuid(), null, "Klassen kommune"),
            ["statsforvalteren"] = new("statsforvalteren", "generisk", "statsforvalteren", null, Guid.NewGuid(), null, "Klassen statsforvalter"),
        },
        new Dictionary<string, IReadOnlyList<KjentOrd>>
        {
            ["kongen"] = [new("Kongen", "navneform", "Kongen i statsråd", KongenId, Guid.NewGuid(), null, null)],
            ["kongen i statsråd"] = [new("Kongen i statsråd", "registernavn", "Kongen i statsråd", KongenId, null, null, null)],
            ["stortinget"] = [new("STORTINGET", "registernavn", "STORTINGET", Guid.NewGuid(), null, null, null)],
        },
        new Dictionary<string, IReadOnlyList<KjentOrd>>
        {
            ["kommune"] = [new("kommune", "klasse", "kommune", null, Guid.NewGuid(), null, null)],
            ["kommunene"] = [new("kommunene", "klasse", "kommunene", null, Guid.NewGuid(), Reindriftsloven, null)],
        },
        new Dictionary<Guid, IReadOnlySet<Guid>> { [ForskriftUnderReindrift] = new HashSet<Guid> { Reindriftsloven } });

    private static int _teller;

    private static KoForekomst F(string tekst, Guid? rettskilde = null, string? eid = null) =>
        new(Guid.NewGuid(), tekst, rettskilde ?? Barnehageloven, eid ?? $"https://test/§{Interlocked.Increment(ref _teller)}/ledd-1", 0,
            tekst.Length, "gruppe", null, null);

    // ------------------------------------------------------------------ Ruting (AK3)

    [Theory]
    [InlineData("departementet", Aktorkoruter.Generisk)]
    [InlineData("Departementet", Aktorkoruter.Generisk)]
    [InlineData("  kommunen ", Aktorkoruter.Generisk)]
    [InlineData("Kongen", Aktorkoruter.KjentAktor)]
    [InlineData("Kongen  i statsråd", Aktorkoruter.KjentAktor)]
    [InlineData("Stortinget", Aktorkoruter.KjentAktor)]
    [InlineData("kommune", Aktorkoruter.KjentBegrep)]
    [InlineData("departement", Aktorkoruter.Boyningsform)]
    [InlineData("kommuner", Aktorkoruter.Boyningsform)]
    [InlineData("Kongens", Aktorkoruter.Boyningsform)]
    [InlineData("Utdanningsdirektoratet", Aktorkoruter.Aktorko)]
    [InlineData("departementene", Aktorkoruter.Aktorko)] // -ene mot -et: to suffikser på samme stamme, ikke en bøyning (bevisst)
    [InlineData("fylkeskommunen", Aktorkoruter.Aktorko)]
    public void Ruter_prover_reglene_i_rekkefolge(string tekst, string forventet) =>
        Assert.Equal(forventet, Aktorkoregler.Ruter(Tabell(), tekst, Barnehageloven).Rute);

    [Fact]
    public void En_boyningsform_arver_aldri_regelen_til_ordet_den_ligner()
    {
        var eksakt = Aktorkoregler.Ruter(Tabell(), "departementet", Barnehageloven);
        Assert.Equal("Ansvarlig departement", eksakt.Standardregel);

        var boyning = Aktorkoregler.Ruter(Tabell(), "departement", Barnehageloven);
        Assert.Equal(Aktorkoruter.Boyningsform, boyning.Rute);
        Assert.Equal((Aktorkoruter.Generisk, "departementet", "−et"), (boyning.BoyningAvRute, boyning.Ligner, boyning.Suffiks));
        Assert.Null(boyning.Standardregel); // «et departement» er ikke «departementet»
    }

    [Fact]
    public void Lovspesifikt_begrep_gjelder_bare_i_loven_og_rettskilder_med_hjemmel_i_den()
    {
        Assert.Equal(Aktorkoruter.KjentBegrep, Aktorkoregler.Ruter(Tabell(), "kommunene", Reindriftsloven).Rute);
        Assert.Equal(Aktorkoruter.KjentBegrep, Aktorkoregler.Ruter(Tabell(), "kommunene", ForskriftUnderReindrift).Rute);
        // I barnehageloven er «kommunene» ikke klassen fra reindriftsloven (#378: det ville vært en gjetning), men en bøyningsform av det
        // generiske «kommunen». Den havner fortsatt ikke i aktørkøen.
        var bhl = Aktorkoregler.Ruter(Tabell(), "kommunene", Barnehageloven);
        Assert.Equal((Aktorkoruter.Boyningsform, Aktorkoruter.Generisk), (bhl.Rute, bhl.BoyningAvRute));
    }

    [Fact]
    public void Stammen_ma_ha_minst_fire_tegn_og_suffikset_ma_sta_i_den_lukkede_lista()
    {
        Assert.Equal("+r", Aktorkoregler.BoyningSuffiks("kommune", "kommuner"));
        Assert.Equal("−et", Aktorkoregler.BoyningSuffiks("departementet", "departement"));
        Assert.Null(Aktorkoregler.BoyningSuffiks("rad", "rads"));            // stamme på 3 tegn
        Assert.Null(Aktorkoregler.BoyningSuffiks("leder", "lederskap"));     // «skap» står ikke i lista
        Assert.Null(Aktorkoregler.BoyningSuffiks("departementet", "departementene"));
    }

    [Fact]
    public void Summen_av_rutene_er_antall_ventende_og_ingen_kandidat_star_i_to_ruter()
    {
        var tabell = Tabell();
        var ventende = new[] { "departementet", "Kongen", "kommune", "kommuner", "departement", "Utdanningsdirektoratet", "foreldrerådet",
            "Stortinget", "kommunen", "leder" }.Select(t => F(t)).ToList();

        var svar = ventende.Select(f => (f.NavnekandidatId, Aktorkoregler.Ruter(tabell, f.Tekst, f.RettskildeId).Rute)).ToList();

        Assert.Equal(ventende.Count, svar.Count);
        Assert.Equal(ventende.Count, svar.Select(s => s.NavnekandidatId).Distinct().Count()); // én rute per kandidat
        Assert.All(svar, s => Assert.Contains(s.Rute, Aktorkoruter.Alle));
        Assert.Equal(ventende.Count, Aktorkoruter.Alle.Sum(r => svar.Count(s => s.Rute == r)));
    }

    // ------------------------------------------------------------------ Gruppering (AK1, AK2)

    private static List<KoForekomst> Udir()
    {
        var kilder = Enumerable.Range(1, 5).Select(i => Guid.Parse($"00000000-0000-0000-0000-00000000d00{i}")).ToArray();
        // 3 + 2 + 2 + 1 + 1 = 9 forekomster i 5 rettskilder, som i den delte basen.
        return new[] { 0, 0, 0, 1, 1, 2, 2, 3, 4 }.Select(i => F("Utdanningsdirektoratet", kilder[i])).ToList();
    }

    [Fact]
    public void AK1_hvor_mange_ukjente_aktorer_venter_teller_navn_og_Utdanningsdirektoratet_star_en_gang()
    {
        var forekomster = Udir()
            .Concat([F("Foreldrerådet"), F("Foreldrerådet"), F("foreldrerådet"), F("foreldrerådet")])
            .Concat([F("fylkeskommunen"), F("fylkeskommuner"), F("fylkeskommune"), F("fylkeskommunene"), F("fylkeskommunens")])
            .ToList();

        var grupper = Aktorkoregler.Grupper(forekomster);

        Assert.Equal(3, grupper.Count); // tre navn, ikke 18 forekomster
        var udir = Assert.Single(grupper, g => g.Nokkel == "utdanningsdirektoratet");
        Assert.Equal((9, 5, "Utdanningsdirektoratet"), (udir.Forekomster.Count, udir.Rettskilder, udir.Navn));

        var fk = Assert.Single(grupper, g => g.Nokkel.StartsWith("fylkeskommune", StringComparison.Ordinal));
        Assert.Equal(5, fk.Varianter.Count); // regel 2: fem bøyningsformer, ett navn
        Assert.All(fk.Varianter.Skip(1), v => Assert.Equal("boyning", v.Regel));

        var fr = Assert.Single(grupper, g => g.Nokkel == "foreldrerådet");
        var variant = Assert.Single(fr.Varianter); // regel 1: «Foreldrerådet» og «foreldrerådet» er samme ord
        Assert.Equal(("samme_ord", 2), (variant.Regel, variant.Skrivemater.Count));
        Assert.Equal("foreldrerådet", fr.Navn); // liten forbokstav ved likt antall (stor er ofte bare setningsstart)
    }

    [Fact]
    public void AK2_genitivforledd_blir_bare_forslag_og_domstolens_leder_slas_aldri_sammen_med_leder()
    {
        var forekomster = new[] { F("domstolens leder"), F("leder"), F("leder"), F("nemndas leder"), F("barnehagens foreldreråd"),
            F("foreldrerådet"), F("Foreldrerådet") }.ToList();

        var grupper = Aktorkoregler.Grupper(forekomster);

        Assert.Contains(grupper, g => g.Nokkel == "domstolens leder" && g.Varianter.Count == 1);
        Assert.Contains(grupper, g => g.Nokkel == "leder" && g.Varianter.Count == 1);
        var forslag = Aktorkoregler.FinnGenitivforslag(grupper);
        Assert.Contains(forslag, f => f is { FraNokkel: "domstolens leder", TilNokkel: "leder", Etterledd: "leder" });
        Assert.Contains(forslag, f => f is { FraNokkel: "nemndas leder", TilNokkel: "leder" });
        Assert.Contains(forslag, f => f is { FraNokkel: "barnehagens foreldreråd", TilNokkel: "foreldrerådet", Etterledd: "foreldreråd" });
    }

    [Fact]
    public void AK2_grupperingen_gir_samme_resultat_hver_gang_uansett_rekkefolge()
    {
        var forekomster = Udir()
            .Concat(new[] { "barnehagen", "barnehagene", "barnehage", "barnehager", "Barnehagen", "styrer", "styreren", "domstolens leder",
                "leder", "samarbeidsutvalget", "samarbeidsutvalg", "Samarbeidsutvalget" }.Select(t => F(t)))
            .ToList();

        static string Signatur(IReadOnlyList<Kogruppe> g) => string.Join("|", g.Select(x =>
            $"{x.Nokkel}:{x.Navn}:{x.Forekomster.Count}:{string.Join(",", x.Varianter.Select(v => $"{v.Normalisert}/{v.Regel}/{v.BoyningAv}/{v.Suffiks}"))}"));

        var forste = Signatur(Aktorkoregler.Grupper(forekomster));
        var tilfeldig = new Random(440);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(forste, Signatur(Aktorkoregler.Grupper(forekomster.OrderBy(_ => tilfeldig.Next()).ToList())));
        }
        Assert.Contains("barnehagen:barnehagen:5:barnehagen/samme_ord//,barnehage/boyning/barnehagen/−n", forste);
    }

    // ------------------------------------------------------------------ Hjemmelforslag (AK4)

    private const string BhlParagraf4Ledd1 = "https://lovdata.no/eli/lov/2005/06/17/64/nor/§4/ledd-1";

    private static readonly HjemmelNode[] Barnehagelovnoder =
    [
        new(Barnehageloven, BhlParagraf4Ledd1,
            "For å sikre samarbeidet med barnas hjem, skal hver barnehage ha et foreldreråd og et samarbeidsutvalg.", false, 10),
        new(Barnehageloven, "https://lovdata.no/eli/lov/2005/06/17/64/nor/§13/ledd-2",
            "Utdanningsdirektoratet skal veilede kommuner og barnehager om denne loven kapittel V med forskrifter.", false, 20),
        new(Barnehageloven, "https://lovdata.no/eli/lov/2005/06/17/64/nor/§56/ledd-1",
            "Utdanningsdirektoratet fører tilsyn med at de private barnehagene drives i samsvar med bestemmelsene.", false, 30),
        // Veiledning (#419): selv med opprettelsesuttrykk og navnet blir den aldri hjemmel.
        // [ENDRET, #440 PR 2] «Det opprettes» er ikke lenger et uttrykk; «det skal være et» er det.
        new(Barnehageloven, "kap-veiledning-til-de-enkelte-bestemmelsene/rom-til-5/ledd-2",
            "For rapporteringen skal det være et Utdanningsdirektoratet med eget kontor.", true, 40),
    ];

    [Fact]
    public void AK4_hjemmelforslaget_for_foreldraadet_er_bhl_paragraf_4_forste_ledd()
    {
        var forslag = Aktorkoregler.FinnHjemmelforslag(["foreldrerådet"], Barnehagelovnoder);

        var f = Assert.Single(forslag);
        Assert.Equal((BhlParagraf4Ledd1, "skal_ha", "foreldreråd"), (f.Eid, f.Uttrykk, f.Treff));
        Assert.Equal(Strukturkanter.Normalisering, f.Kunnskapsstatus); // teksten har ubestemt form
        Assert.Equal("§ 4 første ledd", AktorkoTjeneste.Bestemmelse(f.Eid));
        Assert.Equal("foreldreråd", f.Setning.Substring(f.TreffStart, f.Treff.Length));
    }

    [Fact]
    public void AK4_Utdanningsdirektoratet_viser_hullet_og_veiledning_tilbys_aldri_som_hjemmel()
    {
        Assert.Empty(Aktorkoregler.FinnHjemmelforslag(["utdanningsdirektoratet"], Barnehagelovnoder));
        // Kontroll: uten merknadsflagget ville veiledningsnoden gitt et forslag. Det er flagget som stopper den, ikke uttrykket.
        var somNormtekst = Barnehagelovnoder.Select(n => n with { ErMerknad = false });
        Assert.Single(Aktorkoregler.FinnHjemmelforslag(["utdanningsdirektoratet"], somNormtekst));
    }

    [Fact]
    public void Skal_ha_krever_navnet_etter_uttrykket()
    {
        // «Barnehagen skal ha en …»: barnehagen er den som har noe, ikke det som opprettes.
        var noder = new[] { new HjemmelNode(Barnehageloven, "https://test/§1/ledd-1", "Barnehagen skal ha en styrer.", false, 1) };
        Assert.Empty(Aktorkoregler.FinnHjemmelforslag(["barnehagen"], noder));
        Assert.Equal("styrer", Assert.Single(Aktorkoregler.FinnHjemmelforslag(["styrer"], noder)).Treff);
    }

    /// <summary>[ENDRET, #440 PR 2, juristrunde-440 punkt 2 og 4] «oppnevner», «opprettes» og «etableres» med navnet hvor som helst er tatt ut
    /// (0 av 10 holdt), og oppnevning, valg, utnevnelse og tilsetting gir aldri forslag. Før ga disse tre setningene forslag.</summary>
    [Theory]
    [InlineData("Det opprettes et forliksråd i hver kommune.", "forliksrådet")]
    [InlineData("Departementet oppnevner innstillingsrådet.", "innstillingsrådet")]
    [InlineData("Sametinget etableres som et eget organ.", "sametinget")]
    [InlineData("Kongen oppnevner medlemmene av Innstillingsrådet.", "innstillingsrådet")]
    [InlineData("Stortinget velger to medlemmer til styret.", "styret")]
    [InlineData("Kongen utnevner en direktør.", "direktøren")]
    [InlineData("Kommunen tilsetter en kommunelege.", "kommunelegen")]
    public void Oppnevning_og_de_tre_verbene_gir_aldri_forslag(string tekst, string navn)
    {
        var noder = new[] { new HjemmelNode(Barnehageloven, "https://test/§1/ledd-1", tekst, false, 1) };
        Assert.Empty(Aktorkoregler.FinnHjemmeltreff([navn], noder));
    }

    [Fact]
    public void Uttrykk_og_navn_i_hver_sin_setning_gir_ikke_forslag()
    {
        var noder = new[] { new HjemmelNode(Barnehageloven, "https://test/§1/ledd-1", "Det skal være et register. Forliksrådet fører det.", false, 1) };
        Assert.Empty(Aktorkoregler.FinnHjemmelforslag(["forliksrådet"], noder));
    }

    /// <summary>[Ny, #440 PR 2, juristrunde-440 punkt 1] Navnet må være kjernen i nominalfrasen uttrykket styrer: frasen stopper ved
    /// preposisjon, «som», «at», «enn» og komma, har høyst tre ord per ledd, og kan ha «og/eller» med ny artikkel.</summary>
    [Theory]
    [InlineData("Lagmannsrettene skal ha en førstelagmann som leder og så mange lagmenn som bestemt.", "lagmennene", false)]
    [InlineData("Lagmannsrettene skal ha en førstelagmann som leder og så mange lagmenn som bestemt.", "førstelagmannen", true)]
    [InlineData("Leken skal ha en sentral plass i barnehagen.", "barnehagen", false)]
    [InlineData("Kommunen skal ha en eller flere kommuneleger som skal utføre de oppgaver kommunelegen er tillagt.", "kommunelegen", false)]
    [InlineData("Kommunen skal ha en eller flere kommuneleger som skal utføre de oppgaver kommunelegen er tillagt.", "kommunelegene", false)]
    [InlineData("Kommunen skal ha en eller flere kommuneleger som skal utføre oppgaver.", "kommuneleger", true)]
    [InlineData("Høyesterett skal ha en annen sammensetning enn den som følger av § 5, treffes av domstollederen.", "domstollederen", false)]
    [InlineData("Retten skal ha en justitiarius og nitten andre dommere.", "dommere", true)]
    [InlineData("For å sikre samarbeidet skal hver barnehage ha et foreldreråd og et samarbeidsutvalg.", "samarbeidsutvalget", true)]
    [InlineData("Virksomheten skal ha en oppdatert og langsiktig vedlikeholdsplan som sikrer at virksomhetens lokaler vedlikeholdes.", "virksomheten", false)]
    [InlineData("I hver kommune skal det være et forliksråd.", "forliksrådet", true)]
    [InlineData("I hver kommune skal det i tillegg være et forliksråd.", "forliksrådet", true)]
    [InlineData("Det skal være et forliksråd for hver kommune.", "forliksrådet", true)]
    [InlineData("Det skal være mulig å klage til forliksrådet.", "forliksrådet", false)]
    [InlineData("Barnehagen skal ha medlemmer fra foreldrerådet.", "medlemmer", false)] // «skal ha» uten artikkel er ikke med
    public void Navnet_ma_vaere_kjernen_i_nominalfrasen(string tekst, string navn, bool forslag)
    {
        var noder = new[] { new HjemmelNode(Barnehageloven, "https://test/§1/ledd-1", tekst, false, 1) };
        Assert.Equal(forslag, Aktorkoregler.FinnHjemmelforslag([navn], noder).Count == 1);
    }

    /// <summary>[Ny, #440 PR 2, juristrunde-440 punkt 3] Forleddssperren: «felles forliksråd» er et annet organ. Treffet står, men er ikke
    /// forslag, og grunnen sier hvorfor.</summary>
    [Theory]
    [InlineData("For to kommuner skal det være et felles forliksråd.", "forliksrådet")]
    [InlineData("Kommunen kan bestemme at det skal være felles samarbeidsutvalg for kommunal barnehage og grunnskole.", "samarbeidsutvalget")]
    [InlineData("Departementet skal ha et regionalt kontor.", "kontoret")]
    public void Forleddssperren_stopper_et_annet_organ(string tekst, string navn)
    {
        var noder = new[] { new HjemmelNode(Barnehageloven, "https://test/§1/ledd-1", tekst, false, 1) };
        var treff = Assert.Single(Aktorkoregler.FinnHjemmeltreff([navn], noder));
        Assert.StartsWith("forleddssperre", treff.Sperret);
        Assert.Empty(Aktorkoregler.FinnHjemmelforslag([navn], noder));
    }

    // ------------------------------------------------------------------ Forleddssperren i grupperingen (Johanns avgjørelse 2026-11-05)

    private const string BhlParagraf5 =
        "Kommunen kan bestemme at det skal være felles samarbeidsutvalg for kommunal barnehage og grunnskole. Der begge parter ønsker det, "
        + "kan det etableres tilsvarende ordning for privat barnehage og kommunal eller privat skole.";

    /// <summary>Forekomsten av <paramref name="navn"/> (første treff) i <paramref name="tekst"/>, med frasen sperren gir.</summary>
    private static string? Sperret(string tekst, string navn)
    {
        var start = tekst.IndexOf(navn, StringComparison.Ordinal);
        Assert.True(start >= 0, $"«{navn}» står ikke i teksten");
        return Aktorkoregler.SperretFrase(tekst, start, start + navn.Length);
    }

    /// <summary>«Hvilket navn står forekomsten for?» Samme regel som hjemmelregelen: et ord foran kjernen som ikke er tall- eller mengdeord,
    /// gjør det til et annet navn. Svaret er frasen slik den står i teksten, fra det første ordet som sperrer.</summary>
    [Theory]
    [InlineData(BhlParagraf5, "samarbeidsutvalg", "felles samarbeidsutvalg")]
    [InlineData("For to kommuner skal det være et felles forliksråd.", "forliksråd", "felles forliksråd")]
    [InlineData("I hver kommune skal det være to felles forliksråd.", "forliksråd", "felles forliksråd")] // tallordet foran tas ikke med
    [InlineData("Departementet skal ha et regionalt kontor.", "kontor", "regionalt kontor")]
    [InlineData("Retten skal ha en justitiarius og nitten andre dommere.", "dommere", null)] // tall- og mengdeord sperrer ikke
    [InlineData("Barnehagen skal ha et eget samarbeidsutvalg.", "samarbeidsutvalg", null)]
    [InlineData("For å sikre samarbeidet skal hver barnehage ha et foreldreråd og et samarbeidsutvalg.", "samarbeidsutvalg", null)] // ingen forledd
    [InlineData("Samarbeidsutvalget skal være et rådgivende organ.", "Samarbeidsutvalget", null)] // ikke i frasen uttrykket styrer
    [InlineData("Kommunen skal legge saken fram for felles samarbeidsutvalg.", "samarbeidsutvalg", null)] // ingen frase: ikke gjettet (§8)
    [InlineData("Det skal være felles samarbeidsutvalg.", "felles samarbeidsutvalg", null)] // forleddet står alt i forekomsten
    public void Forleddssperren_i_grupperingen_folger_hjemmelregelen(string tekst, string navn, string? forventet) =>
        Assert.Equal(forventet, Sperret(tekst, navn));

    [Fact]
    public void Felles_samarbeidsutvalg_blir_eget_navn_og_ikke_en_variant_av_samarbeidsutvalget()
    {
        var felles = F("samarbeidsutvalg") with { Frase = Sperret(BhlParagraf5, "samarbeidsutvalg") };
        var forekomster = new[] { F("samarbeidsutvalget"), F("Samarbeidsutvalget"), felles };

        var grupper = Aktorkoregler.Grupper(forekomster);

        Assert.Equal(2, grupper.Count);
        var su = Assert.Single(grupper, g => g.Nokkel == "samarbeidsutvalget");
        Assert.Equal(["samarbeidsutvalget"], su.Varianter.Select(v => v.Normalisert));
        Assert.Equal(2, su.Forekomster.Count);
        var fs = Assert.Single(grupper, g => g.Nokkel == "felles samarbeidsutvalg");
        Assert.Equal(("felles samarbeidsutvalg", "samme_ord"), (fs.Navn, Assert.Single(fs.Varianter).Regel));
        Assert.Equal("felles samarbeidsutvalg", Assert.Single(fs.Varianter[0].Skrivemater).Tekst); // kan godkjennes slik det står
        Assert.Equal(felles.NavnekandidatId, Assert.Single(fs.Forekomster).NavnekandidatId);
        // «felles» ender på «s», men er ikke genitiv: sperren har avgjort det, og det blir ikke et forslag om å slå dem sammen igjen.
        Assert.DoesNotContain(Aktorkoregler.FinnGenitivforslag(grupper), f => f.FraNokkel == "felles samarbeidsutvalg");
        // Hjemmelregelen på det nye navnet: «det skal være felles samarbeidsutvalg» er et forslag (tekstfunn), ikke lenger sperret.
        var noder = new[] { new HjemmelNode(Barnehageloven, "https://lovdata.no/eli/lov/2005/06/17/64/nor/§5/ledd-1", BhlParagraf5, false, 1) };
        var h = Assert.Single(Aktorkoregler.FinnHjemmelforslag(fs.Varianter.Select(v => v.Normalisert), noder));
        Assert.Equal(("skal_vaere", "felles samarbeidsutvalg", Strukturkanter.Tekstfunn), (h.Uttrykk, h.Treff, h.Kunnskapsstatus));
        Assert.Empty(Aktorkoregler.FinnHjemmelforslag(su.Varianter.Select(v => v.Normalisert), noder));
    }

    [Fact]
    public void Uten_sperre_er_grupperingen_uendret()
    {
        // Samme forekomster uten frase: «samarbeidsutvalg» er en bøyning av «samarbeidsutvalget», som før.
        var grupper = Aktorkoregler.Grupper([F("samarbeidsutvalget"), F("Samarbeidsutvalget"), F("samarbeidsutvalg")]);
        var su = Assert.Single(grupper);
        Assert.Equal("samarbeidsutvalget", su.Nokkel);
        Assert.Equal(("samarbeidsutvalg", "boyning", "−et"), (su.Varianter[1].Normalisert, su.Varianter[1].Regel, su.Varianter[1].Suffiks));

        // To forekomster med samme sperrede frase er samme ord, og bøyning med like forledd gjelder frasene imellom (regel 2).
        var fraser = Aktorkoregler.Grupper([F("forliksråd") with { Frase = "felles forliksråd" }, F("forliksrådet") with { Frase = "felles forliksrådet" },
            F("forliksrådet")]);
        Assert.Equal(2, fraser.Count);
        Assert.Contains(fraser, g => g.Nokkel == "forliksrådet" && g.Forekomster.Count == 1);
        Assert.Contains(fraser, g => g.Varianter.Select(v => v.Normalisert).Order().SequenceEqual(["felles forliksråd", "felles forliksrådet"]));
    }

    // ------------------------------------------------------------------ Forleddssperren i rutingen (#456, Johanns klarsignal 2026-11-05)

    private static readonly Guid SamarbeidsutvalgetId = Guid.Parse("00000000-0000-0000-0000-00000000c456");

    /// <summary><see cref="Tabell"/> med «samarbeidsutvalget» som kjent aktør (navneform) eller kjent klasse, slik det blir når Johann godkjenner
    /// det i køen.</summary>
    private static Rutingstabell TabellMedSamarbeidsutvalget(string art)
    {
        var t = Tabell();
        var aktorer = t.Aktorer.ToDictionary(x => x.Key, x => x.Value);
        var begreper = t.Begreper.ToDictionary(x => x.Key, x => x.Value);
        if (art == "navneform")
            aktorer["samarbeidsutvalget"] = [new("samarbeidsutvalget", "navneform", "Samarbeidsutvalget", SamarbeidsutvalgetId, Guid.NewGuid(), null, null)];
        else
            begreper["samarbeidsutvalget"] = [new("samarbeidsutvalget", art, "samarbeidsutvalget", null, Guid.NewGuid(), Barnehageloven, null)];
        return new Rutingstabell(t.Generiske, aktorer, begreper, t.HjemmelLover);
    }

    /// <summary>Forekomsten av <paramref name="navn"/> (første treff) i <paramref name="tekst"/>, med posisjonen sveipet fant, og med frasen
    /// sperren gir (<see cref="Aktorkoregler.MedForleddssperre"/>, samme vei som tjenesten).</summary>
    private static KoForekomst Lest(string tekst, string navn)
    {
        var start = tekst.IndexOf(navn, StringComparison.Ordinal);
        Assert.True(start >= 0, $"«{navn}» står ikke i teksten");
        var f = new KoForekomst(Guid.NewGuid(), navn, Barnehageloven, $"https://test/§{Interlocked.Increment(ref _teller)}/ledd-1", start,
            start + navn.Length, "gruppe", null, null);
        return Aktorkoregler.MedForleddssperre(f, tekst);
    }

    /// <summary>«Står ‹felles samarbeidsutvalg› fortsatt i køen når ‹samarbeidsutvalget› er godkjent?» Ja: forleddssperren gjelder i rutingen
    /// som i grupperingen, og forekomsten er et eget navn, ikke en bøyningsform av det kjente ordet.</summary>
    [Theory]
    [InlineData("navneform", Aktorkoruter.KjentAktor)]
    [InlineData(Nodetyper.Klasse, Aktorkoruter.KjentBegrep)]
    public void Felles_samarbeidsutvalg_rutes_til_koen_selv_om_samarbeidsutvalget_er_kjent(string art, string kjentRute)
    {
        var tabell = TabellMedSamarbeidsutvalget(art);
        var felles = Lest(BhlParagraf5, "samarbeidsutvalg");
        Assert.Equal("felles samarbeidsutvalg", felles.Frase);

        var svar = Aktorkoregler.Ruter(tabell, felles);
        Assert.Equal(Aktorkoruter.Aktorko, svar.Rute);
        Assert.Equal("felles samarbeidsutvalg", svar.Normalisert);
        Assert.Empty(svar.Treff);

        // Kontroll: uten sperren (teksten alene, slik rutingen var før #456) er det en bøyningsform av det kjente ordet.
        var uten = Aktorkoregler.Ruter(tabell, felles.Tekst, felles.RettskildeId);
        Assert.Equal((Aktorkoruter.Boyningsform, kjentRute, "samarbeidsutvalget", "−et"), (uten.Rute, uten.BoyningAvRute, uten.Ligner, uten.Suffiks));

        // Og i køen står den som eget navn, ved siden av de andre forekomstene av «samarbeidsutvalget» som (før godkjenningen) var der.
        var grupper = Aktorkoregler.Grupper([F("samarbeidsutvalget"), felles]);
        Assert.Contains(grupper, g => g.Nokkel == "felles samarbeidsutvalg" && g.Forekomster.Single().NavnekandidatId == felles.NavnekandidatId);
    }

    [Fact]
    public void Blir_frasen_selv_kjent_rutes_forekomsten_ut_av_koen()
    {
        // Når «felles samarbeidsutvalg» er godkjent som aktør, er forekomsten kjent: samme ord, eller en bøyningsform med like forledd.
        var t = TabellMedSamarbeidsutvalget("navneform");
        var aktorer = t.Aktorer.ToDictionary(x => x.Key, x => x.Value);
        aktorer["felles samarbeidsutvalg"] = [new("felles samarbeidsutvalg", "navneform", "Felles samarbeidsutvalg", Guid.NewGuid(), Guid.NewGuid(), null, null)];
        var tabell = new Rutingstabell(t.Generiske, aktorer, t.Begreper, t.HjemmelLover);

        Assert.Equal(Aktorkoruter.KjentAktor, Aktorkoregler.Ruter(tabell, Lest(BhlParagraf5, "samarbeidsutvalg")).Rute);
        var boyning = Aktorkoregler.Ruter(tabell, Lest("Det skal være felles samarbeidsutvalget for skolene.", "samarbeidsutvalget"));
        Assert.Equal((Aktorkoruter.Boyningsform, "felles samarbeidsutvalg"), (boyning.Rute, boyning.Ligner));
    }

    /// <summary>«Rutes alt annet som før?» Uten sperre gir forekomsten nøyaktig samme svar som teksten alene, også der en frase er lest
    /// (tall- og mengdeord, ingen forledd) og der regelen ikke vet hvor frasen begynner.</summary>
    [Theory]
    [InlineData("departementet")]
    [InlineData("Departementet")]
    [InlineData("  kommunen ")]
    [InlineData("Kongen")]
    [InlineData("Kongen  i statsråd")]
    [InlineData("Stortinget")]
    [InlineData("kommune")]
    [InlineData("departement")]
    [InlineData("kommuner")]
    [InlineData("Kongens")]
    [InlineData("Utdanningsdirektoratet")]
    [InlineData("departementene")]
    [InlineData("fylkeskommunen")]
    [InlineData("samarbeidsutvalg")]
    public void Uten_sperre_er_rutingen_uendret(string tekst)
    {
        var tabell = TabellMedSamarbeidsutvalget("navneform");
        var f = F(tekst);
        Assert.Null(f.Frase);
        Assert.Equal(Aktorkoregler.Ruter(tabell, tekst, Barnehageloven), Aktorkoregler.Ruter(tabell, f), RutingssvarLikhet.Instans);
    }

    [Theory]
    [InlineData("For å sikre samarbeidet skal hver barnehage ha et foreldreråd og et samarbeidsutvalg.", "samarbeidsutvalg")] // ingen forledd
    [InlineData("Barnehagen skal ha et eget samarbeidsutvalg.", "samarbeidsutvalg")]                                         // mengdeord
    [InlineData("Kommunen skal legge saken fram for felles samarbeidsutvalg.", "samarbeidsutvalg")]                          // ingen frase lest
    public void En_lest_forekomst_uten_sperre_er_fortsatt_en_boyningsform(string tekst, string navn)
    {
        var f = Lest(tekst, navn);
        Assert.Null(f.Frase);
        var svar = Aktorkoregler.Ruter(TabellMedSamarbeidsutvalget("navneform"), f);
        Assert.Equal((Aktorkoruter.Boyningsform, "samarbeidsutvalget"), (svar.Rute, svar.Ligner));
    }

    [Fact]
    public void Sperren_settes_bare_naar_teksten_fortsatt_har_navnet_paa_posisjonen()
    {
        var lest = Lest(BhlParagraf5, "samarbeidsutvalg");
        Assert.NotNull(lest.Frase);
        // Teksten er endret siden sveipet: posisjonen peker ikke lenger på navnet. Da gjettes ikke en frase (CLAUDE.md §8), og forekomsten
        // rutes på teksten som før.
        var endret = "Kort. " + BhlParagraf5;
        var f = lest with { Frase = null };
        Assert.Null(Aktorkoregler.MedForleddssperre(f, endret).Frase);
        Assert.Null(Aktorkoregler.MedForleddssperre(f, null).Frase);
        Assert.Null(Aktorkoregler.MedForleddssperre(f with { Start = -1 }, BhlParagraf5).Frase);
        Assert.Equal(Aktorkoruter.Boyningsform, Aktorkoregler.Ruter(TabellMedSamarbeidsutvalget("navneform"), Aktorkoregler.MedForleddssperre(f, endret)).Rute);
    }

    /// <summary>AK3 med sperren: summen av rutene er antall ventende, og ingen kandidat står i to ruter, også når noen forekomster har frase.</summary>
    [Fact]
    public void Med_sperre_er_summen_av_rutene_antall_ventende_og_ingen_kandidat_star_i_to_ruter()
    {
        var tabell = TabellMedSamarbeidsutvalget("navneform");
        var ventende = new[] { "departementet", "Kongen", "kommune", "kommuner", "departement", "Utdanningsdirektoratet", "samarbeidsutvalget",
                "samarbeidsutvalg", "Stortinget", "kommunen" }.Select(t => F(t))
            .Concat([Lest(BhlParagraf5, "samarbeidsutvalg"), Lest("For to kommuner skal det være et felles forliksråd.", "forliksråd"),
                Lest("Departementet skal ha et regionalt kontor.", "kontor"), Lest("Retten skal ha en justitiarius og nitten andre dommere.", "dommere")])
            .ToList();

        var svar = ventende.Select(f => (f.NavnekandidatId, Aktorkoregler.Ruter(tabell, f).Rute)).ToList();

        Assert.Equal(ventende.Count, svar.Select(s => s.NavnekandidatId).Distinct().Count()); // én rute per kandidat
        Assert.All(svar, s => Assert.Contains(s.Rute, Aktorkoruter.Alle));
        Assert.Equal(ventende.Count, Aktorkoruter.Alle.Sum(r => svar.Count(s => s.Rute == r)));
        // Sperren flytter bare den ene: «samarbeidsutvalg» uten frase er fortsatt en bøyningsform, med frasen er den i køen.
        Assert.Equal(Aktorkoruter.Boyningsform, svar[7].Rute);
        Assert.Equal(Aktorkoruter.Aktorko, svar[10].Rute);
    }

    /// <summary>Likhet på det rutingen svarer (recorden har lister, så standardlikheten sammenligner referanser).</summary>
    private sealed class RutingssvarLikhet : IEqualityComparer<Rutingssvar>
    {
        public static readonly RutingssvarLikhet Instans = new();
        public bool Equals(Rutingssvar? a, Rutingssvar? b) => a is not null && b is not null
            && (a.Rute, a.Normalisert, a.BoyningAvRute, a.Ligner, a.Suffiks, a.Standardregel) == (b.Rute, b.Normalisert, b.BoyningAvRute, b.Ligner, b.Suffiks, b.Standardregel)
            && a.Treff.SequenceEqual(b.Treff);
        public int GetHashCode(Rutingssvar s) => HashCode.Combine(s.Rute, s.Normalisert);
    }

    [Fact]
    public void Normalisert_folger_frasen_ogsa_etter_with()
    {
        var f = F("Samarbeidsutvalg");
        Assert.Equal("samarbeidsutvalg", f.Normalisert);
        Assert.Equal("felles samarbeidsutvalg", (f with { Frase = "Felles  samarbeidsutvalg" }).Normalisert);
    }

    [Fact]
    public void Ordrett_variant_gir_tekstfunn_selv_om_en_annen_variant_er_en_boyning()
    {
        var noder = new[] { new HjemmelNode(Barnehageloven, "https://test/§1/ledd-1", "Hver barnehage skal ha en styrer.", false, 1) };
        Assert.Equal(Strukturkanter.Tekstfunn, Assert.Single(Aktorkoregler.FinnHjemmelforslag(["styreren", "styrer"], noder)).Kunnskapsstatus);
        Assert.Equal(Strukturkanter.Normalisering, Assert.Single(Aktorkoregler.FinnHjemmelforslag(["styreren"], noder)).Kunnskapsstatus);
    }

    // ------------------------------------------------------------------ Annen paragraf (Johanns avgjørelse 2026-11-01)

    [Fact]
    public void Annen_paragraf_vises_bare_naar_hjemmelen_star_i_en_annen_paragraf_enn_alle_forekomstene()
    {
        var titler = new Dictionary<Guid, (string Tittel, string? Kortnavn)> { [Barnehageloven] = ("Lov om domstolene", "dl") };
        var h = new Hjemmelforslag(Barnehageloven, "https://lovdata.no/eli/lov/1915/08/13/5/nor/§9/ledd-1", "skal_ha", "s", "direktør", 0,
            Strukturkanter.Normalisering);
        var annen = new[] { F("direktøren", Barnehageloven, "https://lovdata.no/eli/lov/1915/08/13/5/nor/§33b/ledd-2") };
        Assert.Equal("hjemmelen står i § 9; forekomstene i § 33b", AktorkoTjeneste.AnnenParagraf(h, annen, titler));

        var samme = annen.Append(F("direktøren", Barnehageloven, "https://lovdata.no/eli/lov/1915/08/13/5/nor/§9/ledd-3"));
        Assert.Null(AktorkoTjeneste.AnnenParagraf(h, samme, titler));
    }

    // ------------------------------------------------------------------ Skill ut og ta med (#440 PR 2)

    private static KoForekomst G(string tekst, string? gruppe) => F(tekst) with { Gruppe = gruppe };

    [Fact]
    public void Skill_ut_gir_varianten_eget_navn_og_nye_forekomster_av_samme_ord_folger_med()
    {
        var beregnet = Aktorkoregler.Grupper([F("styret"), F("styret"), F("styre")]);
        Assert.Single(beregnet);

        // «styre» skilt ut (aktorko_gruppe = «styre» på forekomsten), og en ny, ventende forekomst av «styre» uten verdi.
        var grupper = Aktorkoregler.Grupper([F("styret"), F("styret"), G("styre", "styre"), F("Styre")]);
        Assert.Equal(2, grupper.Count);
        var utskilt = grupper.Single(g => g.Nokkel == "styre");
        Assert.True(utskilt.Manuell);
        Assert.Equal(2, utskilt.Forekomster.Count);
        Assert.False(grupper.Single(g => g.Nokkel == "styret").Manuell);
    }

    [Fact]
    public void Ta_med_legger_et_ord_uten_boyning_under_navnet_og_merker_det_manuelt()
    {
        var grupper = Aktorkoregler.Grupper([G("foreldrerådet", "foreldrerådet"), G("barnehagens foreldreråd", "foreldrerådet"), F("domstolens leder")]);
        var navn = grupper.Single(g => g.Nokkel == "foreldrerådet");
        Assert.True(navn.Manuell);
        Assert.Equal(["samme_ord", "manuell"], navn.Varianter.Select(v => v.Regel));
        Assert.Contains(grupper, g => g.Nokkel == "domstolens leder" && !g.Manuell);
    }

    [Fact]
    public void Manuell_gruppering_er_uavhengig_av_rekkefolgen()
    {
        var inn = new[] { F("styret"), G("styre", "styre"), F("styre"), G("styrets leder", "styret"), F("Styret") };
        string Nokler(IEnumerable<KoForekomst> f) =>
            string.Join("|", Aktorkoregler.Grupper(f).Select(g => g.Nokkel + ":" + g.Forekomster.Count + ":" + string.Join(",", g.Varianter.Select(v => v.Regel))));
        Assert.Equal(Nokler(inn), Nokler(inn.Reverse()));
    }

    /// <summary>
    /// [Ny, issue #440, Johanns avgjørelse 2026-10-31] Aktørsveipet: bare navn aktørkøen ikke kjenner blir navnekandidater, med ÉTT unntak —
    /// et generisk ord uten binding («kommunen»), så «Behandle»-lenken til én-skjermen for generisk omtale (docs/09 §42) står igjen.
    /// Spørsmålet: «Kan et generisk ord uten binding fortsatt behandles fra aktørsveipet, uten at andre kjente ord slipper inn?»
    /// </summary>
    [Theory]
    [InlineData(Aktorstatus.Generisk, Aktorkoruter.Generisk, true)]                     // unntaket: «kommunen» uten binding
    [InlineData(Aktorstatus.NyAktor, Aktorkoruter.Aktorko, true)]
    [InlineData(Aktorstatus.NyNavneformForKjentAktor, Aktorkoruter.Aktorko, true)]
    [InlineData(Aktorstatus.Generisk, Aktorkoruter.Aktorko, true)]
    [InlineData(Aktorstatus.Generisk, null, false)]                                     // ingen rute, ingen gjetting
    [InlineData(Aktorstatus.NyAktor, Aktorkoruter.Generisk, false)]                    // bare status OG rute sammen gir unntaket
    [InlineData(Aktorstatus.Generisk, Aktorkoruter.Boyningsform, false)]              // bøyningsform arver aldri regelen
    [InlineData(Aktorstatus.NyAktor, Aktorkoruter.Boyningsform, false)]               // «kommunene» utenfor
    [InlineData(Aktorstatus.NyNavneformForKjentAktor, Aktorkoruter.KjentAktor, false)]
    [InlineData(Aktorstatus.NyAktor, Aktorkoruter.KjentBegrep, false)]
    [InlineData(Aktorstatus.Kjent, Aktorkoruter.Aktorko, false)]
    [InlineData(Aktorstatus.Kjent, Aktorkoruter.Generisk, false)]                      // bundet: kjent, ingen kandidat
    [InlineData(Aktorstatus.Flertydig, Aktorkoruter.Generisk, false)]
    [InlineData(Aktorstatus.Flertydig, Aktorkoruter.Aktorko, false)]
    public void Aktorsveipet_legger_bare_ukjente_navn_og_generiske_ord_uten_binding_i_navnekandidatkoen(string status, string? rute, bool forventet)
    {
        Assert.Equal(forventet, AktorsveipRad.BlirNavnekandidat(status, rute));
        Assert.Equal(status == Aktorstatus.Generisk && rute == Aktorkoruter.Generisk, AktorsveipRad.ErGeneriskOrdUtenBinding(status, rute));
    }
}
