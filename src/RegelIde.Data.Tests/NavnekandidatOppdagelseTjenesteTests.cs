using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// Oppdagelsesmekanismen (docs/13-backlog.md §9, <see cref="NavnekandidatOppdagelseTjeneste"/>).
/// To deler: rene enhetstester av selve mønstergjenkjenningen
/// (<see cref="NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst"/>, ingen DB — raske, presise
/// feilmeldinger) og integrasjonstester av sveip/godkjenning/avvisning mot ekte embedded Postgres
/// (samme mønster som <c>VirksomhetKandidatSveipTjenesteTests</c>).
/// <para>
/// Alle DB-testene sveiper med en EKSPLISITT <c>rettskildeId</c> (aldri <c>null</c>) — DB-en er DELT
/// mellom alle tester i <see cref="DataTestCollection"/> (ICollectionFixture), og et korpus-bredt sveip
/// ville truffet ekte lovtekst fra andre testklassers fixturer (alkoholloven inneholder garantert
/// "Kongen"/"departementet" m.fl.) og gjort resultatet uforutsigbart. Snevret til én, fersk,
/// syntetisk rettskilde per test er den kontrollerte, deterministiske veien.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class NavnekandidatOppdagelseTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public NavnekandidatOppdagelseTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------- Del A: ren mønstergjenkjenning, ingen DB ----------

    [Fact]
    public void Suffiksmonster_med_stor_forbokstav_midt_i_setning_gir_virksomhet_kandidat()
    {
        const string tekst = "Vedtak kan påklages til Miljødirektoratet innen tre uker.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn);
        Assert.Equal("virksomhet", treff.Kategori);
        Assert.Equal("Miljødirektoratet", tekst.Substring(treff.Start, treff.Lengde));
    }

    [Fact]
    public void Suffiksmonster_ved_setningsstart_gir_ingen_kandidat()
    {
        // "Miljødirektoratet" er her selve setningens (og nodetekstens) FØRSTE ord — ambiguøst
        // (kunne bare være vanlig stor forbokstav ved setningsstart), gir bevisst INGEN kandidat i
        // det hele tatt, verken "virksomhet" eller "gruppe" (docs/13-backlog.md §9).
        const string tekst = "Miljødirektoratet skal føre tilsyn med at loven overholdes.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.Empty(funn);
    }

    [Fact]
    public void Suffiksmonster_etter_punktum_regnes_som_ny_setningsstart_og_gir_ingen_kandidat()
    {
        const string tekst = "Første setning er ferdig her. Miljødirektoratet skal føre tilsyn videre.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.Empty(funn);
    }

    [Fact]
    public void Suffiksmonster_med_liten_forbokstav_gir_gruppe_kandidat()
    {
        // "havnetilsynet" — suffikset "-tilsynet" med liten forbokstav er en FUNKSJONSBESKRIVELSE,
        // ikke et egennavn (docs/13-backlog.md §9, samme prinsipp som "forurensningsmyndighetene").
        const string tekst = "Alle skip skal melde fra til havnetilsynet før anløp.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn);
        Assert.Equal("gruppe", treff.Kategori);
        Assert.Equal("havnetilsynet", tekst.Substring(treff.Start, treff.Lengde));
    }

    /// <summary>
    /// [Ny, kodegjennomgang 2026-08-30] Regresjonstest for en reell falsk positiv: "fiskeriregelverket"
    /// ble tidligere foreslått som en "virksomhet"-kandidat i live data (stor forbokstav midt i
    /// setning + "verket"-suffiks), men er åpenbart ikke et egennavn — bare "regelverket for
    /// fiskeri". Et ekte "verket"-institusjonsnavn ("Patentverket") skal fortsatt gi kandidat.
    /// </summary>
    [Fact]
    public void Verket_denyliste_blokkerer_produktive_sammensetninger_men_ikke_ekte_institusjoner()
    {
        const string tekst = "Vedtak kan påklages til Fiskeriregelverket innen tre uker.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.Empty(funn);

        const string ektInstitusjon = "Vedtak kan påklages til Patentverket innen tre uker.";
        var funnEkte = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(ektInstitusjon);
        var treff = Assert.Single(funnEkte);
        Assert.Equal("virksomhet", treff.Kategori);
        Assert.Equal("Patentverket", ektInstitusjon.Substring(treff.Start, treff.Lengde));
    }

    /// <summary>
    /// [Ny, issue #150 del 1] "utvalget"/"enheten" manglet helt fra <see cref="NavnekandidatOppdagelseTjeneste"/>s
    /// suffikslister — Johann forventet vesentlig flere forslag i navnekandidat-køen (eksemplifisert med
    /// "EOS-utvalget"/"PNR-enheten", selve bindestrek-forkortelsen er bevisst IKKE dekket her, se
    /// Suffikser-feltets kommentar). Disse testene dekker den ikke-bindestrek-forkortede formen
    /// (ETT sammensmeltet ord, samme mønster som "Datatilsynet"/"Miljødirektoratet").
    /// </summary>
    [Theory]
    [InlineData("Klager fra publikum behandles av Personvernutvalget i første instans.", "Personvernutvalget")]
    [InlineData("Sluttrapporten oversendes Etterretningsenheten for videre behandling.", "Etterretningsenheten")]
    public void Suffiksmonster_fanger_utvalget_og_enheten_som_smeltet_sammensatt_ord(string tekst, string forventet)
    {
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        var treff = Assert.Single(funn, f => f.Kategori == "virksomhet");
        Assert.Equal(forventet, tekst.Substring(treff.Start, treff.Lengde));
    }

    [Fact]
    public void Fast_liste_gruppesubstantiv_gir_alltid_gruppe_uansett_store_smaa_bokstaver()
    {
        const string tekst = "Kommunen skal sørge for dette, men kommunen kan òg pålegge en frist.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        Assert.Equal(2, funn.Count);
        Assert.All(funn, f => Assert.Equal("gruppe", f.Kategori));
        Assert.Equal("Kommunen", tekst.Substring(funn[0].Start, funn[0].Lengde));
        Assert.Equal("kommunen", tekst.Substring(funn[1].Start, funn[1].Lengde));
    }

    [Fact]
    public void Fast_liste_foretrekker_lengste_frase_kongen_i_statsraad_fremfor_kongen_alene()
    {
        const string tekst = "Kongen i statsråd avgjør saken endelig.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn);
        Assert.Equal("Kongen i statsråd", tekst.Substring(treff.Start, treff.Lengde));
    }

    [Fact]
    public void Fast_liste_substantiv_uten_suffiks_klassifiseres_som_gruppe_selv_stor_forbokstav_midt_i_setning()
    {
        // "departementet" står IKKE i suffikslisten (det ER selve suffikset, ikke et sammensatt ord
        // med suffikset) — dekkes utelukkende av den faste listen, alltid "gruppe", også med stor
        // forbokstav midt i en setning (til forskjell fra suffiksmekanismens "virksomhet"-regel).
        const string tekst = "Klage sendes til Departementet innen tre uker.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn);
        Assert.Equal("gruppe", treff.Kategori);
        Assert.Equal("Departementet", tekst.Substring(treff.Start, treff.Lengde));
    }

    // ---------- Del A2: flerords-mønster (egennavn + institusjonsord), docs/13-backlog.md §9 punkt 4 ----------

    [Fact]
    public void Flerords_monster_med_ett_egennavn_gir_virksomhet_kandidat()
    {
        // Basert på live data, FOR-2019-09-30-1310 §2 andre ledd, punkt 1 ("Østfold fylkeskommune:
        // Driftsområde Ytre Oslofjord Øst") — MEN med en kort, urelatert lead-in-setning foran, slik at
        // treffet ikke lenger står ved ABSOLUTT tekststart (se
        // Flerords_monster_gir_ingen_kandidat_for_egennavn_som_star_bokstavelig_forst_i_egen_node under
        // for nøyaktig DEN varianten, og hvorfor den nå bevisst IKKE lenger gir noen kandidat, issue #149).
        const string tekst = "Fylkeskommunale driftsområder er inndelt slik: Østfold fylkeskommune: Driftsområde Ytre Oslofjord Øst";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        // Merk: bare "fylkeskommune" gir I TILLEGG en separat "gruppe"-treff (Del 2s utvidede
        // FasteRollesubstantiv, se egne tester under) — vi filtrerer her på "virksomhet" spesifikt.
        var treff = Assert.Single(funn, f => f.Kategori == "virksomhet");
        Assert.Equal("Østfold fylkeskommune", tekst.Substring(treff.Start, treff.Lengde));
    }

    [Fact]
    public void Flerords_monster_med_bindeord_og_inkluderer_bindeordet_i_fanget_tekst()
    {
        // Basert på samme rettskilde, punkt 7 ("Møre og Romsdal fylkeskommune: …") — "og" MELLOM to
        // store-forbokstav-ord skal være med i den fangede teksten, ikke bare det siste av dem. Samme
        // lead-in-tilpasning som testen over (issue #149 — se den testens kommentar).
        const string tekst = "Fylkeskommunale driftsområder er inndelt slik: Møre og Romsdal fylkeskommune: Møre og Romsdal driftsområde";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn, f => f.Kategori == "virksomhet");
        Assert.Equal("Møre og Romsdal fylkeskommune", tekst.Substring(treff.Start, treff.Lengde));
    }

    /// <summary>
    /// [Ny, issue #149] Regresjonstest for selve fiksen: det fangede egennavn-ordet FØRST i
    /// flerords-treffet skal IKKE telle som et egennavn når det står ved en (tvetydig) setningsstart —
    /// samme prinsipp som <see cref="ErSetningsstart"/> allerede håndhever for suffiksmønsteret og
    /// <see cref="FinnStorBokstavKandidaterITekst"/>. "For"/"Konkret" er her IKKE egennavn — de er kun
    /// stor forbokstav fordi de tilfeldigvis åpner sin egen setning/node, nøyaktig Johanns to rapporterte
    /// eksempler (issue #149).
    /// </summary>
    [Theory]
    [InlineData("For tilsyn med at reglene overholdes, skal Statsforvalteren føre kontroll.")]
    [InlineData("Konkret tilsyn kan gjennomføres når det er nødvendig.")]
    public void Flerords_monster_gir_ingen_virksomhet_for_ord_som_kun_er_stor_forbokstav_ved_setningsstart(string tekst)
    {
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.DoesNotContain(funn, f => f.Kategori == "virksomhet");
    }

    /// <summary>
    /// [Ny, issue #149] Dokumentert, AKSEPTERT recall-tap som direkte følge av fiksen over: et EKTE
    /// flerords-institusjonsnavn som (som i FOR-2019-09-30-1310 §2) står bokstavelig som det ALLERFØRSTE
    /// i sin egen <see cref="RettskildeNodeEntitet"/> (AKN-listepunkt-per-node-import, ingen tekstlig
    /// liste-markør i selve Tekst) fanges IKKE lenger av dette mønsteret — se
    /// <see cref="ErFlerordsKontekstTillatt"/>s metodekommentar for hvorfor dette er en bevisst,
    /// eksplisitt flagget avveining (samme mekanisme kan ikke skille "Østfold" fra "For"/"Konkret" uten
    /// en ordklasse-/NLP-analyse, som er eksplisitt forbudt i denne klassen).
    /// </summary>
    [Fact]
    public void Flerords_monster_gir_ingen_kandidat_for_egennavn_som_star_bokstavelig_forst_i_egen_node()
    {
        const string tekst = "Østfold fylkeskommune: Driftsområde Ytre Oslofjord Øst";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.DoesNotContain(funn, f => f.Kategori == "virksomhet");
    }

    [Fact]
    public void Flerords_monster_gir_ingen_virksomhet_kandidat_for_generisk_ubestemt_forekomst()
    {
        // "en fylkeskommune" — ordet RETT FØR institusjonsordet ("en") er IKKE stor forbokstav, altså
        // ikke et egennavn. Presisjonskravet docs/13-backlog.md §9 eksplisitt advarer mot.
        const string tekst = "Loven åpner for at en fylkeskommune samarbeider med andre.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        Assert.DoesNotContain(funn, f => f.Kategori == "virksomhet");
    }

    [Fact]
    public void Flerords_monster_gir_ingen_kandidat_i_det_hele_tatt_for_generisk_forekomst_uten_institusjonsord_i_fast_liste()
    {
        // "et statlig tilsyn" — "tilsyn" (ubestemt) er IKKE i FasteRollesubstantiv (kun kommune/
        // fylkeskommune/departement/statsforvalter er utvidet dit, se Del 2), og "statlig" foran er
        // ikke stor forbokstav — INGEN kandidat i det hele tatt forventes, verken virksomhet ELLER gruppe.
        const string tekst = "et statlig tilsyn kan gripe inn.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        Assert.Empty(funn);
    }

    [Fact]
    public void Flerords_monster_fanger_statens_vegvesen()
    {
        // "vegvesen" — lagt til Institusjonsord utover Johanns opprinnelige liste (se listens
        // kommentar for begrunnelsen): til forskjell fra f.eks. "tilsyn"-institusjoner skrives denne
        // reelt som to ord, og er lav-tvetydig alene.
        const string tekst = "Ansvaret for vegen ligger hos Statens vegvesen i denne saken.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn, f => f.Kategori == "virksomhet");
        Assert.Equal("Statens vegvesen", tekst.Substring(treff.Start, treff.Lengde));
    }

    /// <summary>
    /// [Ny, issue #150 del 1] "utvalg"/"enhet" — lagt til <see cref="Institusjonsord"/> som eget,
    /// mellomromsdelt ord etter et egennavn, samme flerords-form som "Statens vegvesen"/"Møre og
    /// Romsdal fylkeskommune" (til forskjell fra det MELTEDE "utvalget"/"enheten"-suffikset, se
    /// Suffiksmonster-testene over). Fanger ikke bindestrek-forkortelsen "EOS-utvalget"/"PNR-enheten"
    /// (issue #150 del 2, bevisst ikke bygget her).
    /// </summary>
    [Theory]
    [InlineData("Klagesaker om personvern behandles av Telemark utvalg innen tre uker.", "Telemark utvalg")]
    [InlineData("Bistand til politiet ytes av Vestfold enhet i denne typen saker.", "Vestfold enhet")]
    public void Flerords_monster_fanger_utvalg_og_enhet_som_eget_ord_etter_egennavn(string tekst, string forventet)
    {
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        var treff = Assert.Single(funn, f => f.Kategori == "virksomhet");
        Assert.Equal(forventet, tekst.Substring(treff.Start, treff.Lengde));
    }

    [Fact]
    public void Flerords_monster_fanger_navngitt_fagskole()
    {
        // "fagskole" — lagt til 2026-08-30 etter at Johann forventet skolenavn i navnekandidat-køen
        // ("Fagskolen Essens") og de manglet — bekreftet i live data at korpuset inneholder mange
        // navngitte fagskoler i selve rettskilde-teksten på nøyaktig denne formen (Nortrain fagskole,
        // Nordland fagskole m.fl., se klassekommentaren på Institusjonsord).
        const string tekst = "Studenter ved Nortrain fagskole har rett til klage på karaktervedtak.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn, f => f.Kategori == "virksomhet");
        Assert.Equal("Nortrain fagskole", tekst.Substring(treff.Start, treff.Lengde));
    }

    [Fact]
    public void Flerords_monster_gir_ingen_treff_for_generisk_ubestemt_fagskole()
    {
        // Samme presisjonskrav som for "en fylkeskommune" — ordet rett før institusjonsordet er ikke
        // stor forbokstav, altså ikke et egennavn.
        const string tekst = "Forskriften gjelder for enhver fagskole som tilbyr slik utdanning.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.DoesNotContain(funn, f => f.Kategori == "virksomhet");
    }

    [Fact]
    public void Flerords_monster_bevisst_utelater_bare_skole_uten_prefiks()
    {
        // "skole" alene er BEVISST utelatt fra Institusjonsord (se klassekommentaren — for produktivt
        // brukt med ethvert stedsnavn/adjektiv til at en denyliste kunne blitt uttømmende, til
        // forskjell fra "verket"). "Samisk skole" skal derfor IKKE gi noen kandidat i det hele tatt.
        const string tekst = "Denne bestemmelsen gjelder også for Samisk skole i regionen.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.DoesNotContain(funn, f => f.Kategori == "virksomhet");
    }

    [Theory]
    [InlineData("Studenten har fullført nivå 5 i NKR og fagskole 1 tidligere.")]
    [InlineData("Elevene går på SFO og skole hver dag.")]
    public void Flerords_monster_forkaster_hele_treffet_ved_dinglende_bindeord_rett_for_institusjonsordet(string tekst)
    {
        // Regresjonstest for en bug avdekket ved korpusomfattende testsveip av skole-utvidelsen
        // (2026-08-30): et bindeord ("og") UMIDDELBART FØR institusjonsordet ga tidligere en falsk
        // kandidat ("NKR og fagskole"/"SFO og skole") fordi det dinglende bindeordet der ikke kan
        // bare fjernes og falle tilbake til forrige token, slik det trygt kan i STARTEN av et treff
        // — se metodekommentaren i selve koden for hvorfor. Hele treffet forkastes nå i stedet.
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.DoesNotContain(funn, f => f.Kategori == "virksomhet");
    }

    [Fact]
    public void Flerords_monster_tillater_liste_prefiks_markor_som_ikke_er_en_ekte_setningsslutt()
    {
        // Samme reelle liste som live-data-eksempelet over, men denne varianten har bokstav-listemarkører
        // SOM DEL AV selve teksten (f.eks. fra en annen importrute enn AKN-punkt-per-node) — uten
        // ErListePrefiksVedLinjestart-unntaket ville "a. "/"b. " blitt lest som en ekte setningsslutt
        // (punktum) rett før, og begge blitt avvist som tvetydig setningsstart.
        const string tekst = "Fylkeskommunale driftsområder:\n" +
                              "a. Østfold fylkeskommune: Driftsområde Ytre Oslofjord Øst\n" +
                              "b. Akershus fylkeskommune: Driftsområde Indre Oslofjord";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var virksomhetTreff = funn.Where(f => f.Kategori == "virksomhet").ToList();
        Assert.Equal(2, virksomhetTreff.Count);
        Assert.Contains(virksomhetTreff, f => tekst.Substring(f.Start, f.Lengde) == "Østfold fylkeskommune");
        Assert.Contains(virksomhetTreff, f => tekst.Substring(f.Start, f.Lengde) == "Akershus fylkeskommune");
    }

    /// <summary>
    /// Regresjonstester for konkrete falske positiver AVDEKKET av et faktisk korpusomfattende testsveip
    /// mot den kjørende dev-databasen (se PR-beskrivelsen) — IKKE hypotetiske, alle observert i ekte
    /// rettskildetekst FØR <see cref="AldriEgennavnOrd"/>/genitiv-vernet/den innstrammede
    /// <see cref="TillatteBindeord"/> ble lagt til.
    /// </summary>
    [Theory]
    [InlineData("Enhver fylkeskommune kan søke om unntak fra dette.")]
    [InlineData("Hver kommune skal føre eget regnskap.")]
    [InlineData("En kommune kan overføre oppgaver til en annen kommune.")]
    [InlineData("Det departement som har ansvaret, avgjør saken.")]
    public void Flerords_monster_gir_ingen_virksomhet_for_determinativ_pronomen_foran_institusjonsord(string tekst)
    {
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.DoesNotContain(funn, f => f.Kategori == "virksomhet");
    }

    [Fact]
    public void Flerords_monster_gir_ingen_virksomhet_for_genitivsform_av_en_annen_institusjon()
    {
        // "Finanstilsynets tilsyn" = "tilsynet TIL Finanstilsynet", ikke et navn på en NY institusjon —
        // ordet rett før institusjonsordet ender på genitiv-"s".
        const string tekst = "Finanstilsynets tilsyn omfatter alle banker.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);
        Assert.DoesNotContain(funn, f => f.Kategori == "virksomhet");
    }

    [Fact]
    public void Flerords_monster_fanger_kun_selve_institusjonsnavnet_ikke_en_urelatert_preposisjonsfrase_foran()
    {
        // "Inntaksnemnda i Finnmark fylkeskommune" — "i" er her en EKTE preposisjon ("i [fylket]
        // Finnmark"), ikke et navneinternt bindeord — derfor fjernet fra TillatteBindeord (se dens
        // kommentar). Korrekt fanget tekst er KUN "Finnmark fylkeskommune", ikke hele frasen.
        const string tekst = "Inntaksnemnda i Finnmark fylkeskommune behandler klagen.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn, f => f.Kategori == "virksomhet");
        Assert.Equal("Finnmark fylkeskommune", tekst.Substring(treff.Start, treff.Lengde));
    }

    // ---------- Del A3: normaliserte bøyningsformer i FasteRollesubstantiv (docs/13-backlog.md §9, Del 2) ----------

    [Fact]
    public void Fast_liste_dekker_alle_fire_boyningsformer_av_kommune_og_fylkeskommune()
    {
        // Bekreftet i live data: "kommuneloven" har 71 forekomster av "kommuner" og 71 av
        // "fylkeskommuner" (ubestemt flertall) som IKKE ble fanget før denne utvidelsen.
        const string tekst = "Kommuneloven gjelder for kommune, kommunen, kommuner og kommunene, " +
                              "samt fylkeskommune, fylkeskommunen, fylkeskommuner og fylkeskommunene.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        Assert.Equal(8, funn.Count);
        Assert.All(funn, f => Assert.Equal("gruppe", f.Kategori));
        foreach (var form in new[] { "kommune", "kommunen", "kommuner", "kommunene", "fylkeskommune", "fylkeskommunen", "fylkeskommuner", "fylkeskommunene" })
        {
            Assert.Contains(funn, f => tekst.Substring(f.Start, f.Lengde) == form);
        }
    }

    [Fact]
    public void Fast_liste_dekker_alle_fire_boyningsformer_av_statsforvalter_og_departement()
    {
        const string tekst = "En statsforvalter, flere statsforvaltere, Statsforvalteren og statsforvalterne " +
                              "behandlet saken sammen med et departement, departementet, flere departementer og departementene.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        Assert.Equal(8, funn.Count);
        Assert.All(funn, f => Assert.Equal("gruppe", f.Kategori));
        foreach (var form in new[] { "statsforvalter", "statsforvaltere", "statsforvalterne", "departement", "departementet", "departementer", "departementene" })
        {
            Assert.Contains(funn, f => tekst.Substring(f.Start, f.Lengde) == form);
        }
        Assert.Contains(funn, f => tekst.Substring(f.Start, f.Lengde) == "Statsforvalteren");
    }

    // ---------- Del B: sveip/godkjenning/avvisning mot ekte embedded Postgres ----------

    private static async Task<Guid> OpprettRettskildeMedNodeAsync(
        RegelIdeDbContext db, string tekst, string? eid = null, string? ansvarligDepartement = null)
    {
        var rettskildeId = Guid.NewGuid();
        var nodeEid = eid ?? $"https://test/{Guid.NewGuid():N}/§1/ledd-1";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            // Importrolle="referanse" — samme "ingen AKN-XML nødvendig for en hentet/forfattet
            // referansekilde"-begrunnelse som RettsligStatusKontrastTests: ck_rettskilder_akn_xml
            // krever ellers akn_xml IS NOT NULL for Importrolle="primaer" (default).
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, AnsvarligDepartement = ansvarligDepartement,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid, KildeId = "ledd-1",
            NodeType = "ledd", Tekst = tekst,
        });
        await db.SaveChangesAsync();
        return rettskildeId;
    }

    [Fact]
    public async Task Sveip_finner_ny_virksomhetskandidat_for_suffiksmonster_midt_i_setning()
    {
        // Merk: hver "virksomhet"-kategori-DB-test under bruker sitt EGET, unike institusjonsnavn
        // (ikke gjenbruk av samme "Miljødirektoratet" på tvers av flere tester) — bevisst, siden
        // "allerede dekket"-filtreringen for kategori="virksomhet" er GLOBAL (uansett rettskilde,
        // docs/20 §2.3), og DB-en er DELT mellom alle tester i samlingen (se klassekommentaren). Et
        // annet testmetode som setter opp et dekkende Begrep for SAMME term ville ellers permanent
        // blokkert denne testen, uavhengig av kjørerekkefølge.
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Fiskeridirektoratet innen tre uker.");

        var tjeneste = NyTjeneste(db);
        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(1, resultat.AntallNyeKandidater);

        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("virksomhet", kandidat.Kategori);
        Assert.Equal("Fiskeridirektoratet", kandidat.ForeslattTekst);
        Assert.Equal("Venter", kandidat.Status);
    }

    [Fact]
    public async Task Allerede_dekket_av_eksisterende_virksomhetsbegrep_gir_ingen_ny_kandidat()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Miljødirektoratet innen tre uker.");

        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = "Miljødirektoratet" };
        db.Virksomheter.Add(virksomhet);
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = virksomhet.Id, VirksomhetId = null,
            Term = "Miljødirektoratet", Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var tjeneste = NyTjeneste(db);
        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.Equal(0, resultat.AntallTreffFunnet);
        Assert.Equal(0, resultat.AntallNyeKandidater);
        Assert.False(await db.Navnekandidater.AnyAsync(k => k.RettskildeId == rettskildeId));
    }

    /// <summary>
    /// [Ny, kodegjennomgang 2026-08-30] Regresjonstest for en reell kryssvirksomhet-lekkasje: et
    /// tidligere sveip søkte uskjermet gjennom ALLE virksomheters rettskilder, inkl. private/lokale —
    /// samme klasse bug som allerede ble funnet og fikset én gang i søsterklassen
    /// <see cref="VirksomhetKandidatSveipTjeneste"/> (Agder/Bergen, 2026-08-22). Et korpusomfattende
    /// sveip skal ALDRI opprette en kandidat fra en virksomhets private rettskilde.
    /// </summary>
    [Fact]
    public async Task Sveip_hopper_over_en_virksomhets_private_rettskilde_selv_ved_korpusomfattende_sok()
    {
        await using var db = _fixture.NyDbContext();
        var privatVirksomhetId = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = privatVirksomhetId, Navn = "Testkommunen (privat rettskilde-test)" });
        var privatRettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = privatRettskildeId, VirksomhetId = privatVirksomhetId, Doctype = "doc", Kildetype = "Lov",
            Status = "Gjeldende", Importrolle = "referanse", Tittel = "Privat testlov " + privatRettskildeId,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var nodeEid = $"https://test/{Guid.NewGuid():N}/§1/ledd-1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = privatRettskildeId, Eid = nodeEid, KildeId = "ledd-1",
            NodeType = "ledd", Tekst = "Vedtak kan påklages til Havnetilsynet innen tre uker.",
        });
        await db.SaveChangesAsync();

        var tjeneste = NyTjeneste(db);
        // Korpusomfattende sveip (rettskildeId=null) — den reelle bug-scenarioen, IKKE et eksplisitt
        // forsøk på å be om nettopp denne rettskilden (det dekkes av testen under). Merk: kan IKKE
        // sjekke AntallTreffFunnet==0 her — samlingen deler embedded Postgres med andre tester i samme
        // fil, som selv oppretter delte/nasjonale (VirksomhetId=null) testrettskilder DENNE sveipen
        // legitimt også finner. Assert kun at DENNE private rettskilden ikke bidro noe.
        await tjeneste.SveipAsync(null, "test");

        Assert.False(await db.Navnekandidater.AnyAsync(k => k.RettskildeId == privatRettskildeId));
    }

    [Fact]
    public async Task Sveip_nekter_eksplisitt_forespurt_privat_rettskilde()
    {
        await using var db = _fixture.NyDbContext();
        var privatVirksomhetId = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = privatVirksomhetId, Navn = "Testkommunen (privat rettskilde-test 2)" });
        var privatRettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = privatRettskildeId, VirksomhetId = privatVirksomhetId, Doctype = "doc", Kildetype = "Lov",
            Status = "Gjeldende", Importrolle = "referanse", Tittel = "Privat testlov " + privatRettskildeId,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var tjeneste = NyTjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.SveipAsync(privatRettskildeId, "test"));
    }

    /// <summary>
    /// [Ny, kodegjennomgang 2026-08-30] Regresjonstest: en rettskildes NODER forblir 'gjeldende' for
    /// alltid selv etter at selve rettskilden er reimportert og merket 'erstattet' — uten dette filteret
    /// ville sveipet opprettet en "gruppe"-kandidat som ALDRI kan godkjennes
    /// (<see cref="VirksomhetsbegrepTjeneste.OpprettGruppebegrepAsync"/> krever eksplisitt at
    /// rettskilden selv er gjeldende).
    /// </summary>
    [Fact]
    public async Task Sveip_hopper_over_noder_fra_en_erstattet_rettskilde()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Entitetsstatus = "erstattet", Tittel = "Erstattet testlov " + rettskildeId,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var nodeEid = $"https://test/{Guid.NewGuid():N}/§1/ledd-1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid, KildeId = "ledd-1",
            NodeType = "ledd", Tekst = "Klage sendes til Sjøfartsdirektoratet innen tre uker.",
        });
        await db.SaveChangesAsync();

        var tjeneste = NyTjeneste(db);
        // Samme merknad som testen over: ingen AntallTreffFunnet==0-sjekk mulig i en delt DB-samling.
        await tjeneste.SveipAsync(null, "test");

        Assert.False(await db.Navnekandidater.AnyAsync(k => k.RettskildeId == rettskildeId));
    }

    [Fact]
    public async Task Gruppekandidat_allerede_dekket_for_samme_lovkilde_filtreres_men_ikke_for_annen_lovkilde()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Alle skip skal melde fra til havnetilsynet før anløp.");
        var enAnnenRettskildeId = await OpprettRettskildeMedNodeAsync(db, "Meldeplikt gjelder også overfor havnetilsynet der.");

        // Gruppebegrep "havnetilsynet" finnes allerede — men KUN for rettskildeId, ikke for
        // enAnnenRettskildeId (gruppebegrepets identitet er (Term, LovkildeId) sammen, docs/20 §2.4).
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "gruppe", LovkildeId = rettskildeId, VirksomhetId = null,
            Term = "havnetilsynet", Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var tjeneste = NyTjeneste(db);
        var forsteResultat = await tjeneste.SveipAsync(rettskildeId, "test");
        var andreResultat = await tjeneste.SveipAsync(enAnnenRettskildeId, "test");

        Assert.Equal(0, forsteResultat.AntallTreffFunnet); // dekket for DENNE loven
        Assert.Equal(1, andreResultat.AntallTreffFunnet); // IKKE dekket for den andre loven
        Assert.Equal(1, andreResultat.AntallNyeKandidater);
    }

    [Fact]
    public async Task Gjentatt_sveip_gir_ingen_nye_kandidater()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Vegdirektoratet innen tre uker.");

        var tjeneste = NyTjeneste(db);
        var forste = await tjeneste.SveipAsync(rettskildeId, "test");
        var andre = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.True(forste.AntallNyeKandidater > 0);
        Assert.Equal(forste.AntallTreffFunnet, andre.AntallTreffFunnet);
        Assert.Equal(0, andre.AntallNyeKandidater);
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeId));
    }

    [Fact]
    public async Task Godkjenning_av_gruppekandidat_oppretter_ekte_gruppebegrep()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Alle skip skal melde fra til havnetilsynet før anløp.");

        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("gruppe", kandidat.Kategori);

        var godkjent = await tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist");
        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.Equal("Kari Jurist", godkjent.BehandletAv);

        var gruppebegrep = await db.Begreper.SingleAsync(
            b => b.Begrepskategori == "gruppe" && b.LovkildeId == rettskildeId && b.Term == "havnetilsynet");
        Assert.Equal("publisert", gruppebegrep.Status);
        // [Rettet, 2026-08-30] LovreferanseEid skal settes til NØYAKTIG kandidatens NodeEid ved
        // godkjenning, slik at gruppebegrepet kan spores tilbake til paragrafen det ble funnet i —
        // se OpprettGruppebegrepAsync sin XML-kommentar for konteksten (Johann-observert bug).
        Assert.Equal(kandidat.NodeEid, gruppebegrep.LovreferanseEid);
    }

    [Fact]
    public async Task Godkjenning_av_virksomhetskandidat_oppretter_ikke_noe_begrep()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Sjøfartsdirektoratet innen tre uker.");

        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("virksomhet", kandidat.Kategori);

        var antallBegrepFor = await db.Begreper.CountAsync();
        var godkjent = await tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist");

        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.Equal(antallBegrepFor, await db.Begreper.CountAsync()); // ingen ny rad — se metodekommentaren.
    }

    [Fact]
    public async Task Avvisning_setter_status_avvist_og_hindrer_ikke_ny_sveip_i_a_gjenskape_den()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Reindriftsdirektoratet innen tre uker.");

        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);

        var avvist = await tjeneste.AvvisAsync(kandidat.Id, "Kari Jurist");
        Assert.Equal("Avvist", avvist!.Status);

        // Sveip på nytt — skal IKKE gjenskape en ny rad for samme (rettskilde, node, start).
        var andreSveip = await tjeneste.SveipAsync(rettskildeId, "test");
        Assert.Equal(0, andreSveip.AntallNyeKandidater);
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeId));
        Assert.Equal("Avvist", (await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId)).Status);
    }

    [Fact]
    public async Task Kaster_hvis_rettskilden_ikke_finnes()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = NyTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.SveipAsync(Guid.NewGuid(), "test"));
    }

    // ---------- Del C: normalisering + term-basert dedup for "gruppe" (docs/13-backlog.md §9, Del 2) ----------

    [Fact]
    public async Task Gruppekandidat_lagres_med_normalisert_smaa_bokstaver_tekst_selv_om_treffet_var_stor_forbokstav()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Statsforvalteren skal påse at loven følges.");

        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");

        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("gruppe", kandidat.Kategori);
        // Selve teksten i noden har stor forbokstav ("Statsforvalteren") — lagret ForeslattTekst skal
        // likevel være normalisert til små bokstaver, se klassekommentarens "Normalisering før
        // lagring"-avsnitt.
        Assert.Equal("statsforvalteren", kandidat.ForeslattTekst);
    }

    /// <summary>
    /// Regresjonstest for det konkrete, bekreftede problemet: 68 forekomster av "statsforvalteren" og
    /// 45 av "Statsforvalteren" ga tidligere separate kandidater (ren posisjonell idempotens fanget
    /// ikke opp at det var samme term). Begge forekomster her er i SAMME node — dedupliseringen skjer
    /// derfor INNENFOR samme sveip-kjøring (det in-memory settet oppdateres fortløpende i løkken).
    /// </summary>
    [Fact]
    public async Task Gruppekandidat_med_ulik_store_smaa_bokstaver_i_samme_node_dedupliseres_til_en_rad()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Statsforvalteren skal føre tilsyn. I andre saker avgjør statsforvalteren selv.");

        var tjeneste = NyTjeneste(db);
        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        // Kun ÉN treff telles — den andre forekomsten er "alleredeDekketAvEksisterendeKandidat", samme
        // prinsipp (og samme plassering FØR antallTreff++) som "alleredeDekket mot Begrep".
        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(1, resultat.AntallNyeKandidater);

        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("statsforvalteren", kandidat.ForeslattTekst);
    }

    /// <summary>
    /// Samme regresjon som testen over, men på TVERS av to ulike noder i samme rettskilde — beviser at
    /// dedupliseringen ikke er avhengig av at begge forekomstene behandles i samme indre løkke-iterasjon
    /// (det forhåndslastede oppslaget ved sveipets start dekker dette like godt som den fortløpende
    /// oppdateringen dekker duplikater innenfor én og samme node).
    /// </summary>
    [Fact]
    public async Task Gruppekandidat_med_ulik_store_smaa_bokstaver_pa_tvers_av_noder_dedupliseres_til_en_rad()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Kommunen skal føre tilsyn med dette.");
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = $"https://test/{Guid.NewGuid():N}/§2/ledd-1",
            KildeId = "ledd-1", NodeType = "ledd", Tekst = "I andre saker avgjør kommunen selv.",
        });
        await db.SaveChangesAsync();

        var tjeneste = NyTjeneste(db);
        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(1, resultat.AntallNyeKandidater);
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeId));
    }

    /// <summary>
    /// Kontrasttest: "virksomhet" er bevisst IKKE del av term-dedup-utvidelsen (se klassekommentaren —
    /// case er signal, ikke støy, for et egennavn). To forekomster av SAMME virksomhetsnavn på ulike
    /// posisjoner i samme rettskilde skal derfor fortsatt gi TO separate rader (ren posisjonell
    /// idempotens, uendret oppførsel).
    /// </summary>
    [Fact]
    public async Task Virksomhetskandidat_med_samme_term_pa_ulike_posisjoner_gir_fortsatt_to_separate_rader()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Vedtak kan påklages til Sjøfartsdirektoratet, og Sjøfartsdirektoratet behandler klagen innen tre uker.");

        var tjeneste = NyTjeneste(db);
        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.Equal(2, resultat.AntallTreffFunnet);
        Assert.Equal(2, resultat.AntallNyeKandidater);
        var kandidater = await db.Navnekandidater.Where(k => k.RettskildeId == rettskildeId).ToListAsync();
        Assert.Equal(2, kandidater.Count);
        Assert.All(kandidater, k => Assert.Equal("Sjøfartsdirektoratet", k.ForeslattTekst));
        Assert.Equal(2, kandidater.Select(k => k.StartOffset).Distinct().Count());
    }

    // ---------- Del D: sletting (2026-08-30) — ytelsestest av sortering/filtrering-UI-en krever å kunne
    // tømme køen (helt eller delvis) og sveipe på nytt, se NavnekandidatOppdagelseTjeneste.SlettAsync/
    // SlettAlleAsync for hvorfor "avvis" alene ikke holder (posisjonsbasert idempotens). ----------

    [Fact]
    public async Task SlettAsync_fjerner_raden_faktisk()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Fiskeridirektoratet innen tre uker.");
        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);

        var antallFor = await db.Navnekandidater.CountAsync();
        var slettet = await tjeneste.SlettAsync(kandidat.Id);

        Assert.True(slettet);
        Assert.Equal(antallFor - 1, await db.Navnekandidater.CountAsync());
        Assert.False(await db.Navnekandidater.AnyAsync(k => k.Id == kandidat.Id));
    }

    /// <summary>Sletting skal fungere UANSETT status (til forskjell fra VirksomhetKandidatTjeneste sin
    /// hardslett-KUN-'Avvist'-begrensning) — en 'Godkjent' rad skal også kunne slettes, se
    /// SlettAsync-kommentaren for hvorfor dette er en bevisst, dokumentert forskjell.</summary>
    [Fact]
    public async Task SlettAsync_fungerer_ogsa_for_godkjent_status()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Alle skip skal melde fra til havnetilsynet før anløp.");
        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        await tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist");

        var slettet = await tjeneste.SlettAsync(kandidat.Id);

        Assert.True(slettet);
        Assert.False(await db.Navnekandidater.AnyAsync(k => k.Id == kandidat.Id));
    }

    [Fact]
    public async Task SlettAsync_med_ukjent_id_returnerer_false_uten_a_kaste()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = NyTjeneste(db);

        var slettet = await tjeneste.SlettAsync(Guid.NewGuid());

        Assert.False(slettet);
    }

    /// <summary>Bulk-sletting filtrert på rettskilde skal KUN slette raden(e) for den ene rettskilden —
    /// en annen rettskildes kandidater (opprettet i samme sveip-kjøring) skal forbli urørt.</summary>
    [Fact]
    public async Task SlettAlleAsync_med_rettskildefilter_sletter_kun_matchende_rettskildes_rader()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeA = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Reindriftsdirektoratet innen tre uker.");
        var rettskildeB = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Kystdirektoratet innen tre uker.");
        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeA, "test");
        await tjeneste.SveipAsync(rettskildeB, "test");
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeA));
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeB));

        var antallSlettet = await tjeneste.SlettAlleAsync(rettskildeId: rettskildeA);

        Assert.Equal(1, antallSlettet);
        Assert.Equal(0, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeA));
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeB)); // urørt.
    }

    /// <summary>Bulk-sletting filtrert på status skal KUN slette rader med akkurat den statusen — en
    /// 'Venter'-rad i samme rettskilde skal ikke rammes av et status='Avvist'-filter.</summary>
    [Fact]
    public async Task SlettAlleAsync_med_statusfilter_sletter_kun_matchende_status()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Alle skip skal melde fra til havnetilsynet, og vedtak kan påklages til Oljedirektoratet innen tre uker.");
        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidater = await db.Navnekandidater.Where(k => k.RettskildeId == rettskildeId).ToListAsync();
        var gruppeKandidat = kandidater.Single(k => k.Kategori == "gruppe");
        var virksomhetKandidat = kandidater.Single(k => k.Kategori == "virksomhet");
        await tjeneste.AvvisAsync(gruppeKandidat.Id, "Kari Jurist"); // virksomhetKandidat forblir 'Venter'.

        var antallSlettet = await tjeneste.SlettAlleAsync(status: "Avvist", rettskildeId: rettskildeId);

        Assert.Equal(1, antallSlettet);
        Assert.False(await db.Navnekandidater.AnyAsync(k => k.Id == gruppeKandidat.Id));
        Assert.True(await db.Navnekandidater.AnyAsync(k => k.Id == virksomhetKandidat.Id)); // urørt.
    }

    [Fact]
    public async Task SlettAlleAsync_uten_treff_returnerer_null_uten_a_kaste()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Datatilsynet innen tre uker.");
        var tjeneste = NyTjeneste(db);

        var antallSlettet = await tjeneste.SlettAlleAsync(status: "Avvist", rettskildeId: rettskildeId);

        Assert.Equal(0, antallSlettet);
    }

    // ---------- Del E: departement-eid tekst-tagg ved godkjenning (tekst-tagg-departement-eierskap, 2026-08-31) ----------

    /// <summary>
    /// [Rettet, issue #117] <see cref="EksternNavneoppslagTjeneste"/> her fikk TIDLIGERE en helt
    /// utstubbet <see cref="HttpClient"/> (ingen <see cref="HttpMessageHandler"/> overstyrt i det hele
    /// tatt) — trygt DEN gang, siden <see cref="NavnekandidatOppdagelseTjeneste.SveipAsync"/> aldri
    /// kalte <see cref="EksternNavneoppslagTjeneste"/> uansett. Nå som <c>SveipAsync</c> også kjører
    /// "virksomhet"-kandidater gjennom SNL/SSR-klassifiseringskjeden (se den metodens kommentar), ville
    /// den ekte, ustubbede klienten gjort EKTE nettverkskall mot snl.no/ws.geonorge.no fra so godt som
    /// ALLE <c>SveipAsync</c>-testene i denne klassen (mange av dem sveiper nettopp en "virksomhet"-tekst,
    /// f.eks. "Fiskeridirektoratet"/"Miljødirektoratet"). Svarer nå "ingen treff" for BEGGE kildene —
    /// samme nøytrale docs/31 §2 punkt 3-gren ("ukjent i begge" → behold, lav tillit) som resten av
    /// klassen uansett bygger for en ukjent term, så INGEN av disse testenes eksisterende assertions
    /// endres av dette — kun selve NETTVERKSAVHENGIGHETEN fjernes. Tester som faktisk vil øve på
    /// SNL/SSR-KLASSIFISERINGEN via <c>SveipAsync</c> bygger sin egen tjeneste med
    /// <see cref="NyTjenesteMedStubbetOppslag"/> (samme hjelpemetode <see cref="SveipStorBokstavAsync"/>-
    /// testene under allerede bruker), ikke denne default-hjelpemetoden.
    /// </summary>
    private static NavnekandidatOppdagelseTjeneste NyTjeneste(RegelIdeDbContext db) => NyTjenesteMedStubbetOppslag(db, req =>
        Json(req.RequestUri!.ToString().Contains("stedsnavn", StringComparison.OrdinalIgnoreCase)
            ? """{ "navn": [] }"""
            : "[]"));

    /// <summary>
    /// Kjernescenariet fra Johanns designvalg: et gruppebegrep er delt/nasjonalt (ingen egen eiende
    /// virksomhet), men når rettskildens <see cref="RettskildeEntitet.AnsvarligDepartement"/> løser til
    /// en ekte, kjent <see cref="Virksomhet"/>, skal godkjenningen OGSÅ opprette en ekte
    /// <see cref="TekstTaggEntitet"/> (kind='begrep', RefId=det nye gruppebegrepets id) eid av nettopp
    /// den virksomheten — "opprett disse med virksomheten til departementet".
    /// </summary>
    [Fact]
    public async Task Godkjenning_av_gruppekandidat_med_kjent_departement_oppretter_tekst_tagg_eid_av_departementets_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var departementNavn = "Testdepartementet " + Guid.NewGuid();
        var departementVirksomhetId = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = departementVirksomhetId, Navn = departementNavn });
        await db.SaveChangesAsync();

        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Alle skip skal melde fra til havnetilsynet før anløp.", ansvarligDepartement: departementNavn);

        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("gruppe", kandidat.Kategori);

        var godkjent = await tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist");
        Assert.Equal("Godkjent", godkjent!.Status);

        var gruppebegrep = await db.Begreper.SingleAsync(
            b => b.Begrepskategori == "gruppe" && b.LovkildeId == rettskildeId && b.Term == "havnetilsynet");

        var tagg = await db.TekstTagger.SingleAsync(t => t.RettskildeId == rettskildeId);
        Assert.Equal(departementVirksomhetId, tagg.VirksomhetId);
        Assert.Equal("begrep", tagg.Kind);
        Assert.Equal(gruppebegrep.Id, tagg.RefId);
        Assert.Equal(kandidat.NodeEid, tagg.NodeEid);
        Assert.Equal(kandidat.StartOffset, tagg.StartOffset);
        Assert.Equal(kandidat.EndOffset, tagg.EndOffset);
        Assert.Equal("gjeldende", tagg.Entitetsstatus);
    }

    /// <summary>
    /// Motstykket — en rettskilde uten noe kjent <see cref="RettskildeEntitet.AnsvarligDepartement"/> i
    /// det hele tatt (aldri satt ved import, f.eks. et rundskriv/håndbok) skal IKKE gi noen tagg. En
    /// reell, dokumentert begrensning ("ingen gjettet fallback"), ikke en feil — selve
    /// kandidatgodkjenningen (gruppebegrepet) skal likevel lykkes helt normalt.
    /// </summary>
    [Fact]
    public async Task Godkjenning_av_gruppekandidat_uten_kjent_departement_oppretter_ingen_tagg()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Kommunen skal føre tilsyn i dette tilfellet.");

        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("gruppe", kandidat.Kategori);

        var godkjent = await tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist");

        Assert.Equal("Godkjent", godkjent!.Status); // kandidaten godkjennes fortsatt normalt …
        Assert.NotNull(await db.Begreper.SingleOrDefaultAsync(
            b => b.Begrepskategori == "gruppe" && b.LovkildeId == rettskildeId)); // … gruppebegrepet opprettes fortsatt …
        Assert.False(await db.TekstTagger.AnyAsync(t => t.RettskildeId == rettskildeId)); // … men ingen tagg-siden-effekt.
    }

    /// <summary>
    /// Variant av testen over: departementstrengen ER satt (ikke null), men matcher ingen ekte
    /// <see cref="Virksomhet"/>-rad i katalogen — like reelt "ukjent" som når feltet mangler helt, se
    /// <see cref="VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnAsync"/>s "ingen gjettet fallback".
    /// </summary>
    [Fact]
    public async Task Godkjenning_av_gruppekandidat_med_uopploselig_departementstreng_oppretter_ingen_tagg()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Alle skip skal melde fra til havnetilsynet før anløp.",
            ansvarligDepartement: "Et departement som ikke finnes " + Guid.NewGuid());

        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);

        var godkjent = await tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist");

        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.False(await db.TekstTagger.AnyAsync(t => t.RettskildeId == rettskildeId));
    }

    /// <summary>
    /// Samme departement-eierskap gjelder for <c>"virksomhet"</c>-kategorien (steg 2 i oppgaven: BEGGE
    /// kategorier skal kunne gi en tagg) — men siden <see cref="GodkjennAsync"/> ALDRI oppretter noen
    /// <see cref="BegrepEntitet"/> for denne kategorien (se dens metodekommentar), er det ingen ekte id
    /// for taggen å peke på: <see cref="TekstTaggEntitet.RefId"/> forblir <c>null</c> ("ingen gjettet
    /// fallback" — ikke fabriker en Begrep-id).
    /// </summary>
    [Fact]
    public async Task Godkjenning_av_virksomhetskandidat_med_kjent_departement_oppretter_tagg_uten_ref_id()
    {
        await using var db = _fixture.NyDbContext();
        var departementNavn = "Testdepartementet " + Guid.NewGuid();
        var departementVirksomhetId = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = departementVirksomhetId, Navn = departementNavn });
        await db.SaveChangesAsync();

        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Vedtak kan påklages til Losdirektoratet innen tre uker.", ansvarligDepartement: departementNavn);

        var tjeneste = NyTjeneste(db);
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("virksomhet", kandidat.Kategori);

        var godkjent = await tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist");
        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.False(await db.Begreper.AnyAsync(b => b.LovkildeId == rettskildeId)); // fortsatt ingen Begrep-rad, se metodekommentaren.

        var tagg = await db.TekstTagger.SingleAsync(t => t.RettskildeId == rettskildeId);
        Assert.Equal(departementVirksomhetId, tagg.VirksomhetId);
        Assert.Equal("begrep", tagg.Kind);
        Assert.Null(tagg.RefId);
    }

    // ---------- Del F: "stor bokstav midt i setning" + SNL/SSR-klassifisering (docs/31) ----------

    [Fact]
    public void StorBokstav_fanger_title_case_ord_midt_i_setning()
    {
        const string tekst = "Møtet ble avholdt hos Kvirrefjord uten videre varsel.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnStorBokstavKandidaterITekst(tekst);
        var treff = Assert.Single(funn);
        Assert.Equal("Kvirrefjord", treff.RaaTekst);
    }

    [Fact]
    public void StorBokstav_ved_setningsstart_gir_ingen_treff()
    {
        const string tekst = "Kvirrefjord ligger langt unna alt annet.";
        Assert.Empty(NavnekandidatOppdagelseTjeneste.FinnStorBokstavKandidaterITekst(tekst));
    }

    [Fact]
    public void StorBokstav_ekskluderer_forkortelser_i_store_bokstaver()
    {
        const string tekst = "Nivå 5 i NKR og fagskole er noe annet enn SFO og skole.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnStorBokstavKandidaterITekst(tekst);
        Assert.DoesNotContain(funn, f => f.RaaTekst is "NKR" or "SFO");
    }

    [Fact]
    public void StorBokstav_ekskluderer_determinativer()
    {
        const string tekst = "Vedtaket gjelder Enhver Kvirrefjord som søker om dette.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnStorBokstavKandidaterITekst(tekst);
        Assert.DoesNotContain(funn, f => f.RaaTekst == "Enhver");
        Assert.Contains(funn, f => f.RaaTekst == "Kvirrefjord");
    }

    [Fact]
    public void StorBokstav_ekskluderer_ord_som_allerede_dekkes_av_suffiks_faste_rolle_og_institusjonsord()
    {
        const string tekst = "Vedtak fattes av Miljødirektoratet, deretter av Kongen, og gjelder Kommune som sådan.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnStorBokstavKandidaterITekst(tekst);
        Assert.Empty(funn); // alle tre dekkes ALLEREDE av eksisterende, mer presise mønstre.
    }

    // ---------- SveipStorBokstavAsync — DB-integrasjonstester med stubbet SNL/SSR ----------

    private sealed class RutetHandler(Func<HttpRequestMessage, HttpResponseMessage> svar) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(svar(request));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Bygger en <see cref="NavnekandidatOppdagelseTjeneste"/> der <see cref="EksternNavneoppslagTjeneste"/>
    /// er koblet til en STUBBET SNL/SSR — ingen ekte nettverkskall i disse testene.</summary>
    private static NavnekandidatOppdagelseTjeneste NyTjenesteMedStubbetOppslag(
        RegelIdeDbContext db, Func<HttpRequestMessage, HttpResponseMessage> svar) => new(
        db, new VirksomhetsbegrepTjeneste(db), new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)),
        new VirksomhetOppslagTjeneste(db), new EksternNavneoppslagTjeneste(new HttpClient(new RutetHandler(svar)), db));

    [Fact]
    public async Task SveipStorBokstav_snl_bekreftet_institusjon_gir_virksomhetskandidat_med_oppdagelseskilde()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Testrådet innen tre uker.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search"))
            {
                return Json("""
                [{ "article_type_id": 16, "taxonomy_title": "Test-taksonomi",
                   "article_url": "https://snl.no/Testradet", "article_url_json": "https://snl.no/Testradet.json" }]
                """);
            }
            if (url.EndsWith("Testradet.json"))
            {
                return Json("""
                { "headword": "Testrådet", "url": "https://snl.no/Testradet",
                  "metadata": { "organization_name": "Testrådet", "organization_number": "111222333" } }
                """);
            }
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var resultat = await tjeneste.SveipStorBokstavAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(1, resultat.AntallNyeKandidater);
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("virksomhet", kandidat.Kategori);
        Assert.Equal("Testrådet", kandidat.ForeslattTekst);
        Assert.Equal(NavnekandidatOppdagelseTjeneste.StorBokstavOppdagelsesKilde, kandidat.OppdagelsesKilde);

        var cacheRad = await db.EksternNavneoppslagCache.SingleAsync(c => c.Term == "testrådet" && c.Kilde == "snl");
        Assert.True(cacheRad.Treff);
        Assert.Equal("111222333", cacheRad.OrganisasjonsnummerFunnet);
    }

    [Fact]
    public async Task SveipStorBokstav_ssr_bekreftet_stedsnavn_uten_institusjonsord_etter_forkastes()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Reglene gjelder også i Bergsheia der lokale forhold tilsier det.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search")) return Json("[]"); // ingen SNL-treff.
            if (url.Contains("stedsnavn")) return Json("""{ "navn": [ { "skrivemåte": "Bergsheia", "navneobjekttype": "Tettsted" } ] }""");
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var resultat = await tjeneste.SveipStorBokstavAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(0, resultat.AntallNyeKandidater); // forkastet — geografisk løpetekst-referanse.
        Assert.False(await db.Navnekandidater.AnyAsync(k => k.RettskildeId == rettskildeId));
    }

    [Fact]
    public async Task SveipStorBokstav_ssr_bekreftet_stedsnavn_med_institusjonsord_etter_beholdes()
    {
        // NB: "Lindholt" -- MÅ være forskjellig fra enhver annen (Term, Kilde)-cachet term i denne delte
        // DataTestCollection-en (se EksternNavneoppslagTjenesteTests for samme forbehold) — samme
        // (Term, Kilde)-nøkkel på tvers av tester ville latt den FØRST kjørte testens cache-svar
        // feilaktig "vinne" for den andre.
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Innbyggerne i Lindholt kommune har rett til å klage.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search")) return Json("[]");
            if (url.Contains("stedsnavn")) return Json("""{ "navn": [ { "skrivemåte": "Lindholt", "navneobjekttype": "Tettsted" } ] }""");
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var resultat = await tjeneste.SveipStorBokstavAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallNyeKandidater);
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("Lindholt", kandidat.ForeslattTekst);
        Assert.Equal("virksomhet", kandidat.Kategori);
    }

    [Fact]
    public async Task SveipStorBokstav_ukjent_i_begge_beholdes_som_lav_tillit_kandidat()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Møtet ble avholdt hos Kvirrefjordsen uten videre varsel.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search")) return Json("[]");
            if (url.Contains("stedsnavn")) return Json("""{ "navn": [] }""");
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var resultat = await tjeneste.SveipStorBokstavAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallNyeKandidater); // "ingen gjettet fallback" — ikke forkastet.
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("Kvirrefjordsen", kandidat.ForeslattTekst);
    }

    /// <summary>
    /// docs/31 §3 — et sveip skal ALDRI stoppe/krasje pga. en ekstern nettverksfeil. Simulerer at
    /// SNL-kallet feiler (timeout/500) for én node, og verifiserer at sveipet likevel fullfører OG
    /// fortsatt produserer en kandidat for treffet (degraderer til "ukjent" = lav-tillit, ikke en
    /// kastet feil som stopper hele <see cref="NavnekandidatOppdagelseTjeneste.SveipStorBokstavAsync"/>).
    /// </summary>
    [Fact]
    public async Task SveipStorBokstav_fortsetter_selv_om_ett_eksternt_kall_feiler()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Møtet ble avholdt hos Kvirrefjelldalen uten videre varsel.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search")) throw new HttpRequestException("simulert 500 fra SNL");
            if (url.Contains("stedsnavn")) return Json("""{ "navn": [] }""");
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var resultat = await tjeneste.SveipStorBokstavAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(1, resultat.AntallNyeKandidater); // sveipet fullførte OG produserte kandidaten likevel.
        Assert.True(await db.Navnekandidater.AnyAsync(k => k.RettskildeId == rettskildeId && k.ForeslattTekst == "Kvirrefjelldalen"));
    }

    [Fact]
    public async Task SveipStorBokstav_er_idempotent_ved_gjentatt_kjoring()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Møtet ble avholdt hos Kvirrefjordtoppen uten videre varsel.");
        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search")) return Json("[]");
            if (url.Contains("stedsnavn")) return Json("""{ "navn": [] }""");
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var forste = await tjeneste.SveipStorBokstavAsync(rettskildeId, "test");
        var andre = await tjeneste.SveipStorBokstavAsync(rettskildeId, "test");

        Assert.Equal(1, forste.AntallNyeKandidater);
        Assert.Equal(0, andre.AntallNyeKandidater); // allerede en kandidat på nøyaktig denne posisjonen.
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeId));
    }

    // ---------- Del G: SNL/SSR-klassifisering utvidet til SveipAsync (issue #117) ----------
    //
    // Fram til denne rettelsen var SveipAsync (suffiks-/flerords-mønstrene) og SveipStorBokstavAsync
    // helt uavhengige av hverandre m.h.t. SNL/SSR — kun sistnevnte klassifiserte i det hele tatt. Disse
    // testene dekker den NYE koblingen (se SveipAsync sin metodekommentar for hele resonnementet, inkl.
    // hvorfor "gruppe" er eksplisitt utenfor scopet og hvorfor SSR-forkastingsgrenen sjelden trigges for
    // disse to mønstrene). Samme klassifiseringskjede (BeholdSomKandidatAsync) og samme cache som
    // Del F over — ingen ny, parallell mekanisme.

    [Fact]
    public async Task SveipAsync_suffiksmonster_snl_bekreftet_institusjon_beholdes_og_cacher_treff()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Kvirsetilsynet innen tre uker.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search"))
            {
                return Json("""
                [{ "article_type_id": 16, "taxonomy_title": "Test-taksonomi",
                   "article_url": "https://snl.no/Kvirsetilsynet", "article_url_json": "https://snl.no/Kvirsetilsynet.json" }]
                """);
            }
            if (url.EndsWith("Kvirsetilsynet.json"))
            {
                return Json("""
                { "headword": "Kvirsetilsynet", "url": "https://snl.no/Kvirsetilsynet",
                  "metadata": { "organization_name": "Kvirsetilsynet", "organization_number": "999888777" } }
                """);
            }
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(1, resultat.AntallNyeKandidater);
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("virksomhet", kandidat.Kategori);
        Assert.Equal("Kvirsetilsynet", kandidat.ForeslattTekst);
        // Til forskjell fra SveipStorBokstavAsync: OppdagelsesKilde forblir null — dette er FORTSATT det
        // opprinnelige, presise suffiksmønsteret som oppdaget kandidaten, kun VALIDERINGEN er ny (se
        // NavnekandidatEntitet.OppdagelsesKilde sin kommentar).
        Assert.Null(kandidat.OppdagelsesKilde);

        var cacheRad = await db.EksternNavneoppslagCache.SingleAsync(c => c.Term == "kvirsetilsynet" && c.Kilde == "snl");
        Assert.True(cacheRad.Treff);
        Assert.Equal("999888777", cacheRad.OrganisasjonsnummerFunnet);
    }

    [Fact]
    public async Task SveipAsync_ukjent_i_snl_og_ssr_beholdes_fortsatt_som_lav_tillit_kandidat()
    {
        // "Ingen gjettet fallback" (docs/31 §2 punkt 3) — en term SNL/SSR ikke kjenner igjen skal IKKE
        // forkastes bare fordi den nå faktisk BLIR slått opp (til forskjell fra før denne rettelsen, da
        // suffiksmønsteret aldri ble slått opp mot noe som helst).
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Kvirsedirektoratet innen tre uker.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search")) return Json("[]");
            if (url.Contains("stedsnavn")) return Json("""{ "navn": [] }""");
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallNyeKandidater);
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("Kvirsedirektoratet", kandidat.ForeslattTekst);
    }

    [Fact]
    public async Task SveipAsync_flerordsmonster_ssr_bekreftet_uten_institusjonsord_rett_etter_forkastes()
    {
        // Viser at gjenbruken av BeholdSomKandidatAsync FAKTISK kan forkaste noe også for flerords-
        // mønsteret, ikke bare gi ren berikelse — riktignok en sjelden situasjon i praksis (se SveipAsync
        // sin metodekommentar om hvorfor), men mekanismen skal fungere identisk uansett kilde-mønster.
        // "Myrvang kommune" er selve den fangede "virksomhet"-kandidaten (flerords: "Myrvang" + "kommune"
        // som eget institusjonsord) — ordet RETT ETTER hele kandidaten i løpeteksten ("deretter") er
        // ikke et institusjonsord, så et SSR-"treff" på nøyaktig "Myrvang kommune" skal forkastes.
        // <b>To treff totalt, ikke ett</b>: "kommune" alene gir I TILLEGG en separat "gruppe"-kandidat
        // (FasteRollesubstantiv sine bøyningsformer, se den listens kommentar og
        // Flerords_monster_med_ett_egennavn_gir_virksomhet_kandidat over for samme, allerede dokumenterte
        // dobbelttreff) — DEN sendes aldri til SNL/SSR (scopet til "virksomhet", se SveipAsync sin
        // metodekommentar) og forkastes derfor IKKE av denne testens SSR-stub.
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Alle henvendelser rettes til Myrvang kommune deretter i sakens anledning.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search")) return Json("[]"); // ingen SNL-treff.
            if (url.Contains("stedsnavn")) return Json("""{ "navn": [ { "skrivemåte": "Myrvang kommune", "navneobjekttype": "Tettsted" } ] }""");
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.Equal(2, resultat.AntallTreffFunnet); // "Myrvang kommune" (virksomhet) + "kommune" alene (gruppe).
        Assert.Equal(1, resultat.AntallNyeKandidater); // KUN "kommune"-gruppekandidaten — virksomhet-treffet ble forkastet.
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("gruppe", kandidat.Kategori);
        Assert.Equal("kommune", kandidat.ForeslattTekst);
        Assert.False(await db.Navnekandidater.AnyAsync(k => k.RettskildeId == rettskildeId && k.Kategori == "virksomhet"));
    }

    [Fact]
    public async Task SveipAsync_gruppekandidat_sendes_aldri_til_snl_ssr()
    {
        // Scoping-beslutningen (se SveipAsync sin metodekommentar) — "gruppe" (her: suffiksmønsterets
        // LITEN forbokstav-gren, "kommunen" med liten k) skal ALDRI treffe et eksternt oppslag. Stubben
        // kaster hvis den i det hele tatt kalles — testen feiler dermed tydelig (ikke stille) om scopingen
        // noensinne brytes.
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Det er kommunen som har ansvaret her.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
            throw new InvalidOperationException($"Uventet eksternt SNL/SSR-kall for en 'gruppe'-kandidat: {req.RequestUri}"));

        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallNyeKandidater);
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("gruppe", kandidat.Kategori);
    }

    [Fact]
    public async Task SveipAsync_fortsetter_selv_om_snl_oppslag_feiler_for_suffiksmonster_kandidat()
    {
        // Samme docs/31 §3-garanti ("et sveip skal ALDRI stoppe/krasje pga. en ekstern nettverksfeil")
        // som SveipStorBokstav_fortsetter_selv_om_ett_eksternt_kall_feiler over, nå bekreftet for
        // SveipAsync også.
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Kvirseombudet innen tre uker.");

        var tjeneste = NyTjenesteMedStubbetOppslag(db, req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/v1/search")) throw new HttpRequestException("simulert 500 fra SNL");
            if (url.Contains("stedsnavn")) return Json("""{ "navn": [] }""");
            throw new InvalidOperationException($"Uventet URL: {url}");
        });

        var resultat = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(1, resultat.AntallNyeKandidater); // sveipet fullførte OG produserte kandidaten likevel.
        Assert.True(await db.Navnekandidater.AnyAsync(k => k.RettskildeId == rettskildeId && k.ForeslattTekst == "Kvirseombudet"));
    }
}
