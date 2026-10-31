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
        new(Barnehageloven, "kap-veiledning-til-de-enkelte-bestemmelsene/rom-til-5/ledd-2",
            "Det opprettes et eget Utdanningsdirektoratet for rapporteringen.", true, 40),
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

    [Theory]
    [InlineData("Det opprettes et forliksråd i hver kommune.", "forliksrådet", "opprettes")]
    [InlineData("Departementet oppnevner innstillingsrådet.", "innstillingsrådet", "oppnevner")]
    [InlineData("Sametinget etableres som et eget organ.", "sametinget", "etableres")]
    public void De_tre_verbene_gir_forslag_i_samme_setning(string tekst, string navn, string uttrykk)
    {
        var noder = new[] { new HjemmelNode(Barnehageloven, "https://test/§1/ledd-1", tekst, false, 1) };
        Assert.Equal(uttrykk, Assert.Single(Aktorkoregler.FinnHjemmelforslag([navn], noder)).Uttrykk);
    }

    [Fact]
    public void Uttrykk_og_navn_i_hver_sin_setning_gir_ikke_forslag()
    {
        var noder = new[] { new HjemmelNode(Barnehageloven, "https://test/§1/ledd-1", "Det opprettes et register. Forliksrådet fører det.", false, 1) };
        Assert.Empty(Aktorkoregler.FinnHjemmelforslag(["forliksrådet"], noder));
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
