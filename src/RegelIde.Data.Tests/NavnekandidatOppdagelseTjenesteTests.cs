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
}
