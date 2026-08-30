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
        // det hele tatt, verken "virksomhet" eller "rolle" (docs/13-backlog.md §9).
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
    public void Suffiksmonster_med_liten_forbokstav_gir_rolle_kandidat()
    {
        // "havnetilsynet" — suffikset "-tilsynet" med liten forbokstav er en FUNKSJONSBESKRIVELSE,
        // ikke et egennavn (docs/13-backlog.md §9, samme prinsipp som "forurensningsmyndighetene").
        const string tekst = "Alle skip skal melde fra til havnetilsynet før anløp.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn);
        Assert.Equal("rolle", treff.Kategori);
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

    [Fact]
    public void Fast_liste_rollesubstantiv_gir_alltid_rolle_uansett_store_smaa_bokstaver()
    {
        const string tekst = "Kommunen skal sørge for dette, men kommunen kan òg pålegge en frist.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        Assert.Equal(2, funn.Count);
        Assert.All(funn, f => Assert.Equal("rolle", f.Kategori));
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
    public void Fast_liste_substantiv_uten_suffiks_klassifiseres_som_rolle_selv_stor_forbokstav_midt_i_setning()
    {
        // "departementet" står IKKE i suffikslisten (det ER selve suffikset, ikke et sammensatt ord
        // med suffikset) — dekkes utelukkende av den faste listen, alltid "rolle", også med stor
        // forbokstav midt i en setning (til forskjell fra suffiksmekanismens "virksomhet"-regel).
        const string tekst = "Klage sendes til Departementet innen tre uker.";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn);
        Assert.Equal("rolle", treff.Kategori);
        Assert.Equal("Departementet", tekst.Substring(treff.Start, treff.Lengde));
    }

    // ---------- Del A2: flerords-mønster (egennavn + institusjonsord), docs/13-backlog.md §9 punkt 4 ----------

    [Fact]
    public void Flerords_monster_med_ett_egennavn_gir_virksomhet_kandidat()
    {
        // Ordrett fra live data, FOR-2019-09-30-1310 §2 andre ledd, punkt 1 — importert som SIN EGEN
        // RettskildeNode uten noen tekstlig liste-markør (bokstav-/tallmarkøren er strukturell metadata,
        // ikke en del av Tekst) — "Østfold fylkeskommune: …" står derfor bokstavelig på posisjon 0.
        const string tekst = "Østfold fylkeskommune: Driftsområde Ytre Oslofjord Øst";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        // Merk: bare "fylkeskommune" gir I TILLEGG en separat "rolle"-treff (Del 2s utvidede
        // FasteRollesubstantiv, se egne tester under) — vi filtrerer her på "virksomhet" spesifikt.
        var treff = Assert.Single(funn, f => f.Kategori == "virksomhet");
        Assert.Equal("Østfold fylkeskommune", tekst.Substring(treff.Start, treff.Lengde));
    }

    [Fact]
    public void Flerords_monster_med_bindeord_og_inkluderer_bindeordet_i_fanget_tekst()
    {
        // Ordrett fra samme rettskilde, punkt 7 — "og" MELLOM to store-forbokstav-ord skal være med i
        // den fangede teksten, ikke bare det siste av dem.
        const string tekst = "Møre og Romsdal fylkeskommune: Møre og Romsdal driftsområde";
        var funn = NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(tekst);

        var treff = Assert.Single(funn, f => f.Kategori == "virksomhet");
        Assert.Equal("Møre og Romsdal fylkeskommune", tekst.Substring(treff.Start, treff.Lengde));
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
        // ikke stor forbokstav — INGEN kandidat i det hele tatt forventes, verken virksomhet ELLER rolle.
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
        Assert.All(funn, f => Assert.Equal("rolle", f.Kategori));
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
        Assert.All(funn, f => Assert.Equal("rolle", f.Kategori));
        foreach (var form in new[] { "statsforvalter", "statsforvaltere", "statsforvalterne", "departement", "departementet", "departementer", "departementene" })
        {
            Assert.Contains(funn, f => tekst.Substring(f.Start, f.Lengde) == form);
        }
        Assert.Contains(funn, f => tekst.Substring(f.Start, f.Lengde) == "Statsforvalteren");
    }

    // ---------- Del B: sveip/godkjenning/avvisning mot ekte embedded Postgres ----------

    private static async Task<Guid> OpprettRettskildeMedNodeAsync(RegelIdeDbContext db, string tekst, string? eid = null)
    {
        var rettskildeId = Guid.NewGuid();
        var nodeEid = eid ?? $"https://test/{Guid.NewGuid():N}/§1/ledd-1";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            // Importrolle="referanse" — samme "ingen AKN-XML nødvendig for en hentet/forfattet
            // referansekilde"-begrunnelse som RettsligStatusKontrastTests: ck_rettskilder_akn_xml
            // krever ellers akn_xml IS NOT NULL for Importrolle="primaer" (default).
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
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

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.SveipAsync(privatRettskildeId, "test"));
    }

    /// <summary>
    /// [Ny, kodegjennomgang 2026-08-30] Regresjonstest: en rettskildes NODER forblir 'gjeldende' for
    /// alltid selv etter at selve rettskilden er reimportert og merket 'erstattet' — uten dette filteret
    /// ville sveipet opprettet en "rolle"-kandidat som ALDRI kan godkjennes
    /// (<see cref="VirksomhetsbegrepTjeneste.OpprettRollebegrepAsync"/> krever eksplisitt at
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

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
        // Samme merknad som testen over: ingen AntallTreffFunnet==0-sjekk mulig i en delt DB-samling.
        await tjeneste.SveipAsync(null, "test");

        Assert.False(await db.Navnekandidater.AnyAsync(k => k.RettskildeId == rettskildeId));
    }

    [Fact]
    public async Task Rollekandidat_allerede_dekket_for_samme_lovkilde_filtreres_men_ikke_for_annen_lovkilde()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Alle skip skal melde fra til havnetilsynet før anløp.");
        var enAnnenRettskildeId = await OpprettRettskildeMedNodeAsync(db, "Meldeplikt gjelder også overfor havnetilsynet der.");

        // Rollebegrep "havnetilsynet" finnes allerede — men KUN for rettskildeId, ikke for
        // enAnnenRettskildeId (rollebegrepets identitet er (Term, LovkildeId) sammen, docs/20 §2.4).
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "rolle", LovkildeId = rettskildeId, VirksomhetId = null,
            Term = "havnetilsynet", Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
        var forste = await tjeneste.SveipAsync(rettskildeId, "test");
        var andre = await tjeneste.SveipAsync(rettskildeId, "test");

        Assert.True(forste.AntallNyeKandidater > 0);
        Assert.Equal(forste.AntallTreffFunnet, andre.AntallTreffFunnet);
        Assert.Equal(0, andre.AntallNyeKandidater);
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeId));
    }

    [Fact]
    public async Task Godkjenning_av_rollekandidat_oppretter_ekte_rollebegrep()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Alle skip skal melde fra til havnetilsynet før anløp.");

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("rolle", kandidat.Kategori);

        var godkjent = await tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist");
        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.Equal("Kari Jurist", godkjent.BehandletAv);

        var rollebegrep = await db.Begreper.SingleAsync(
            b => b.Begrepskategori == "rolle" && b.LovkildeId == rettskildeId && b.Term == "havnetilsynet");
        Assert.Equal("publisert", rollebegrep.Status);
        // [Rettet, 2026-08-30] LovreferanseEid skal settes til NØYAKTIG kandidatens NodeEid ved
        // godkjenning, slik at rollebegrepet kan spores tilbake til paragrafen det ble funnet i —
        // se OpprettRollebegrepAsync sin XML-kommentar for konteksten (Johann-observert bug).
        Assert.Equal(kandidat.NodeEid, rollebegrep.LovreferanseEid);
    }

    [Fact]
    public async Task Godkjenning_av_virksomhetskandidat_oppretter_ikke_noe_begrep()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Sjøfartsdirektoratet innen tre uker.");

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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
        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.SveipAsync(Guid.NewGuid(), "test"));
    }

    // ---------- Del C: normalisering + term-basert dedup for "rolle" (docs/13-backlog.md §9, Del 2) ----------

    [Fact]
    public async Task Rollekandidat_lagres_med_normalisert_smaa_bokstaver_tekst_selv_om_treffet_var_stor_forbokstav()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Statsforvalteren skal påse at loven følges.");

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
        await tjeneste.SveipAsync(rettskildeId, "test");

        var kandidat = await db.Navnekandidater.SingleAsync(k => k.RettskildeId == rettskildeId);
        Assert.Equal("rolle", kandidat.Kategori);
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
    public async Task Rollekandidat_med_ulik_store_smaa_bokstaver_i_samme_node_dedupliseres_til_en_rad()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Statsforvalteren skal føre tilsyn. I andre saker avgjør statsforvalteren selv.");

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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
    public async Task Rollekandidat_med_ulik_store_smaa_bokstaver_pa_tvers_av_noder_dedupliseres_til_en_rad()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Kommunen skal føre tilsyn med dette.");
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = $"https://test/{Guid.NewGuid():N}/§2/ledd-1",
            KildeId = "ledd-1", NodeType = "ledd", Tekst = "I andre saker avgjør kommunen selv.",
        });
        await db.SaveChangesAsync();

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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

        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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
        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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
        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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
        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));

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
        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
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
        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));
        await tjeneste.SveipAsync(rettskildeId, "test");
        var kandidater = await db.Navnekandidater.Where(k => k.RettskildeId == rettskildeId).ToListAsync();
        var rolleKandidat = kandidater.Single(k => k.Kategori == "rolle");
        var virksomhetKandidat = kandidater.Single(k => k.Kategori == "virksomhet");
        await tjeneste.AvvisAsync(rolleKandidat.Id, "Kari Jurist"); // virksomhetKandidat forblir 'Venter'.

        var antallSlettet = await tjeneste.SlettAlleAsync(status: "Avvist", rettskildeId: rettskildeId);

        Assert.Equal(1, antallSlettet);
        Assert.False(await db.Navnekandidater.AnyAsync(k => k.Id == rolleKandidat.Id));
        Assert.True(await db.Navnekandidater.AnyAsync(k => k.Id == virksomhetKandidat.Id)); // urørt.
    }

    [Fact]
    public async Task SlettAlleAsync_uten_treff_returnerer_null_uten_a_kaste()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak kan påklages til Datatilsynet innen tre uker.");
        var tjeneste = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db));

        var antallSlettet = await tjeneste.SlettAlleAsync(status: "Avvist", rettskildeId: rettskildeId);

        Assert.Equal(0, antallSlettet);
    }
}
