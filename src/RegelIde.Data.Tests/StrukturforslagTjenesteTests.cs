using Microsoft.EntityFrameworkCore;
using RegelIde.Data.Strukturkonvertering;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #313 «Strukturmodell 8»] Forslagslasteren mot ekte embedded Postgres: blir et utsagn i fasit-formatet et forslag med riktige
/// felt gjennom den felles skriveveien? Løses aktører bare ved eksakt treff, og blir resten navnekandidater med årsak? Er en ny kjøring
/// idempotent, og foreslås et avvist utsagn ikke på nytt (#374)?
/// </summary>
[Collection(DataTestCollection.Navn)]
public class StrukturforslagTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public StrukturforslagTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class KasterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Forslagslasteren skal aldri gjøre eksterne oppslag.");
    }

    private static StrukturforslagTjeneste NyLaster(RegelIdeDbContext db) => new(
        db, new StrukturkantTjeneste(db), new VirksomhetOppslagTjeneste(db),
        new NavnekandidatOppdagelseTjeneste(
            db, new VirksomhetsbegrepTjeneste(db), new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)),
            new VirksomhetOppslagTjeneste(db), new EksternNavneoppslagTjeneste(new HttpClient(new KasterHandler()), db),
            new StrukturkantTjeneste(db)));

    private static StrukturAktor Aktor(string id, string tekstform) =>
        new(id, tekstform, [], [], 1, null, null, null, null, null, null);

    private static StrukturUtsagn Utsagn(string id, string eid, string kategori, string type, string? fra, string? til, string? objekt = null,
        string oppdagelseskilde = "monster:test") =>
        new(id, eid, "sitat " + id, kategori, type, fra, til, objekt, "positiv", null, null, null, null, null)
        {
            Oppdagelseskilde = oppdagelseskilde,
        };

    [Fact]
    public void Hver_avbildet_type_finnes_i_startsettet_og_ingenting_gjettes()
    {
        var startsett = Strukturkanter.Startsett.Select(t => (t.Kategori, t.Kode)).ToHashSet();
        foreach (var (kategori, typer) in Strukturkontrakt.TyperPerKategori)
        {
            foreach (var type in typer)
            {
                if (StrukturforslagTjeneste.KanttypeFor(kategori, type) is { } kt)
                {
                    Assert.True(startsett.Contains(kt), $"{kategori}/{type} → {kt} finnes ikke i startsettet");
                }
            }
        }
        Assert.Equal((Strukturkanter.Organtilhorighet, "del_av"), StrukturforslagTjeneste.KanttypeFor("relasjon", "del_av"));
        Assert.Equal((Strukturkanter.Kompetanse, "klage"), StrukturforslagTjeneste.KanttypeFor("kompetanse", "klagekompetanse"));
        Assert.Equal((Strukturkanter.Klasseniva, "skal_ha"), StrukturforslagTjeneste.KanttypeFor("konstituerende", "skal_finnes"));
        Assert.Null(StrukturforslagTjeneste.KanttypeFor("kompetanse", "ukjent"));
        Assert.Null(StrukturforslagTjeneste.KanttypeFor("kompetanse", "annet:noe"));
        Assert.Null(StrukturforslagTjeneste.KanttypeFor("sammensetning_omrade", "del_av"));
        Assert.Null(StrukturforslagTjeneste.KanttypeFor("medlemskap", "inngar_i"));
    }

    [Fact]
    public async Task Laster_forslag_loeser_bare_eksakt_er_idempotent_og_foreslaar_ikke_avviste_paa_nytt()
    {
        await using var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var node = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.Tekst != null && n.Tekst.Contains("departementet"))
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        var organ = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Forslagsorgan-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(organ);
        await db.SaveChangesAsync();

        var dok = new Strukturdokument("Alkoholloven", "eli", [], "test", 1,
            [Aktor("a1", organ.Navn.ToUpperInvariant()), Aktor("a2", "departementet")],
            [
                Utsagn("u1", node.Eid, "kompetanse", "vedtakskompetanse", "a1", null, objekt: "bevilling") with
                    { Kunnskapsstatus = "normalisering", Delegeringsadgang = "ikke_regulert" },
                Utsagn("u2", node.Eid, "kompetanse", "klagekompetanse", "a2", "a1"),
                Utsagn("u3", node.Eid, "kompetanse", "annet:noe", "a1", null),
                Utsagn("u4", node.Eid, "kompetanse", "tilsynskompetanse", null, null, objekt: "x"),
                Utsagn("u5", node.Eid, "kompetanse", "vedtakskompetanse", "a1", null, objekt: "bevilling"),
                Utsagn("u6", node.Eid, "kompetanse", "vedtakskompetanse", "a1", "a1", objekt: "bevilling"), // til = fra: skriveveien avviser
            ]);

        var laster = NyLaster(db);
        var r1 = await laster.LastAsync(lovId, dok, "monster", "monster:ukjent", "Testlaster");
        string Utfall(StrukturforslagRapport r, string id) => r.Rader.Single(x => x.UtsagnId == id).Utfall;
        Assert.Equal(Forslagsutfall.Ny, Utfall(r1, "u1"));
        Assert.Equal((Forslagsutfall.IkkeLastet, Forslagsarsak.AktorIkkeIKatalogen),
            (Utfall(r1, "u2"), r1.Rader.Single(x => x.UtsagnId == "u2").Arsak));
        Assert.Equal(Forslagsarsak.TypeUtenTypekode, r1.Rader.Single(x => x.UtsagnId == "u3").Arsak);
        Assert.Equal(Forslagsarsak.FraIkkeITeksten, r1.Rader.Single(x => x.UtsagnId == "u4").Arsak);
        Assert.Equal(Forslagsutfall.DublettIKjoringen, Utfall(r1, "u5"));
        Assert.Equal(Forslagsarsak.AvvistAvValideringen, r1.Rader.Single(x => x.UtsagnId == "u6").Arsak);
        var dep = Assert.Single(r1.UloesteAktorer);
        Assert.Equal(("departementet", Forslagsarsak.AktorIkkeIKatalogen), (dep.Tekstform, dep.Arsak));
        var kandidat = await db.Navnekandidater.SingleAsync(k => k.Id == dep.NavnekandidatId);
        Assert.Equal(("departementet", node.Eid), (kandidat.ForeslattTekst, kandidat.NodeEid));
        var k = Assert.Single(r1.PerKategori, t => t.Kategori == "K");
        Assert.Equal((5, 1, 1), (k.Utsagn, k.Nye, k.Dubletter));

        // Feltene følger med; status er forslag, aldri godkjent; hjemmelnoden er den gjeldende versjonen.
        var kantId = r1.Rader.Single(r => r.UtsagnId == "u1").KantId;
        var kant = await db.Strukturkanter.SingleAsync(x => x.Id == kantId);
        Assert.Equal(("maskinelt_foreslatt", "monster:test", "vedtak", "bevilling", "normalisering", "ikke_regulert", node.Id),
            (kant.Status, kant.OppdagelsesKilde, kant.Typekode, kant.Objekt, kant.Kunnskapsstatus, kant.Delegeringsadgang, kant.HjemmelNodeId!.Value));
        Assert.Equal("«sitat u1»", kant.Kommentar);

        // Idempotent: ingen nye rader.
        var antallFor = await db.Strukturkanter.IgnoreQueryFilters().CountAsync(x => x.HjemmelRettskildeId == lovId);
        var r2 = await laster.LastAsync(lovId, dok, "monster", "monster:ukjent", "Testlaster");
        Assert.Equal(Forslagsutfall.FantesSomForslag, Utfall(r2, "u1"));
        Assert.Equal(antallFor, await db.Strukturkanter.IgnoreQueryFilters().CountAsync(x => x.HjemmelRettskildeId == lovId));

        // Avvist → «tidligere avvist», ikke foreslått på nytt.
        await new StrukturkantTjeneste(db).AvvisAsync(kant.Id, "Kari Jurist", "Gjelder ikke denne aktøren.");
        var r3 = await laster.LastAsync(lovId, dok, "monster", "monster:ukjent", "Testlaster");
        Assert.Equal(Forslagsutfall.TidligereAvvist, Utfall(r3, "u1"));
        Assert.Equal(1, r3.PerKategori.Single(t => t.Kategori == "K").TidligereAvvist);
        Assert.Equal(antallFor, await db.Strukturkanter.IgnoreQueryFilters().CountAsync(x => x.HjemmelRettskildeId == lovId));

        // KI-laget sender ikke medlemskap (docs/33 §5.5).
        var ki = dok with { Utsagn = [Utsagn("m1", node.Eid, "medlemskap", "medlem_av", "a1", "a1", oppdagelseskilde: "ki:test")] };
        var r4 = await laster.LastAsync(lovId, ki, "ki", "ki:test", "Testlaster");
        Assert.Equal(Forslagsarsak.KiMedlemskapSendesIkke, r4.Rader.Single().Arsak);
    }

    [Fact]
    public async Task Grunnlaget_er_rettskildens_gjeldende_noder_og_monsterlaget_kan_lastes()
    {
        await using var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var laster = NyLaster(db);
        var grunnlag = await laster.ByggGrunnlagAsync(lovId);
        Assert.Equal(await db.RettskildeNoder.CountAsync(n => n.RettskildeId == lovId && n.Entitetsstatus == "gjeldende" && !n.Opphevet),
            grunnlag.Noder.Count);
        var dok = new MonsterStrukturkonverterer().Konverter(grunnlag);
        var r = await laster.LastAsync(lovId, dok, "monster", "monster:ukjent", "Testlaster");
        Assert.Equal(dok.Utsagn.Count, r.Rader.Count);
        Assert.Equal(r.UtsagnTotalt, r.PerKategori.Sum(t => t.Utsagn));
        Assert.All(r.Rader.Where(x => x.Utfall == Forslagsutfall.Ny), x => Assert.StartsWith("monster:", x.Oppdagelseskilde));
    }

    // ------------------------------------------------------------------ [Ny, issue #420] Setningen i idempotensnøkkelen, en-til-en-paring

    /// <summary>Barnehageloven § 53 andre ledd (nodeteksten i den delte basen, lest 2026-10-29): pålegg i første setning, stenging i andre.</summary>
    private const string Setning0 =
        "Kommunen kan gi pålegg til barnehagen om å rette uforsvarlige eller ulovlige forhold ved godkjente eller godkjenningspliktige virksomheter.";
    private const string Setning1 =
        "Hvis fristen for å etterkomme pålegget ikke overholdes, eller forholdet ikke lar seg rette, kan kommunen fatte vedtak om tidsbegrenset "
        + "eller varig stenging av virksomheten.";
    private const string Bhl53Ledd2 = Setning0 + " " + Setning1;

    /// <summary>En egen rettskilde med én node (§ 53 andre ledd) og et eget organ, så testene ikke deler rader med andre tester.</summary>
    private async Task<(RegelIdeDbContext Db, Guid KildeId, string Eid, Virksomhet Organ)> NyBhl53Async()
    {
        var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testlov #420 " + id,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var eid = $"https://test/{id:N}/§53/ledd-2";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = id, Eid = eid, KildeId = "ledd-2", NodeType = "ledd", Tekst = Bhl53Ledd2, Sorteringsrekkefolge = 0,
        });
        var organ = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Kommuneorgan-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(organ);
        await db.SaveChangesAsync();
        return (db, id, eid, organ);
    }

    private static StrukturUtsagn Vedtak(string id, string eid, string sitat, string? objekt, string kilde) =>
        new(id, eid, sitat, "kompetanse", "vedtakskompetanse", "a1", null, objekt, "positiv", null, null, null, null, null)
        {
            Oppdagelseskilde = kilde,
        };

    private static Strukturdokument Dok(Virksomhet organ, params StrukturUtsagn[] utsagn) =>
        new("Testlov #420", "eli", [], "test", 1, [Aktor("a1", organ.Navn)], utsagn);

    private static string UtfallFor(StrukturforslagRapport r, string id) => r.Rader.Single(x => x.UtsagnId == id).Utfall;
    private static Guid? KantIdFor(StrukturforslagRapport r, string id) => r.Rader.Single(x => x.UtsagnId == id).KantId;

    [Fact]
    public void Setningsnummer_er_setningen_sitatet_starter_i_og_sitatet_leses_fra_kommentaren()
    {
        Assert.Equal(0, MonsterStrukturkonverterer.Setningsnummer(Bhl53Ledd2, Setning0));
        Assert.Equal(1, MonsterStrukturkonverterer.Setningsnummer(Bhl53Ledd2, Setning1));
        // KI siterer ofte bare en del av setningen: samme setning.
        Assert.Equal(1, MonsterStrukturkonverterer.Setningsnummer(Bhl53Ledd2, "kan kommunen fatte vedtak om tidsbegrenset eller varig stenging"));
        Assert.Equal(0, MonsterStrukturkonverterer.Setningsnummer(Bhl53Ledd2, "Kommunen kan gi pålegg"));
        // Står ikke i teksten → ukjent, ingenting gjettes.
        Assert.Null(MonsterStrukturkonverterer.Setningsnummer(Bhl53Ledd2, "Departementet kan gi forskrift"));
        Assert.Null(MonsterStrukturkonverterer.Setningsnummer(null, Setning0));
        // Nummeret følger setningsdelingen i mønsterlaget.
        Assert.Equal([Setning0, Setning1], MonsterStrukturkonverterer.Setninger(Bhl53Ledd2));

        Assert.Equal("a", StrukturforslagTjeneste.SitatFraKommentar("«a»"));
        Assert.Equal("a «b» c", StrukturforslagTjeneste.SitatFraKommentar("«a «b» c» — KI-kommentar"));
        Assert.Null(StrukturforslagTjeneste.SitatFraKommentar("Navneregel: lagmannsretten for lagdømmet «Agder lagdømme»"));
        Assert.Null(StrukturforslagTjeneste.SitatFraKommentar(null));
        Assert.Null(StrukturforslagTjeneste.SitatFraKommentar("«»"));
    }

    /// <summary>AK 1 og 2 (#420): mønsterlaget på bhl. § 53 andre ledd gir to vedtak (pålegg og stenging). Før ble stengingen
    /// «dublett_i_kjoringen». En ny kjøring lager ingen nye rader.</summary>
    [Fact]
    public async Task Monsterlaget_laster_palegg_og_stenging_i_samme_ledd_som_to_forslag_og_er_idempotent()
    {
        var (db, kildeId, _, organ) = await NyBhl53Async();
        await using var _db = db;
        var laster = NyLaster(db);
        var dok = new MonsterStrukturkonverterer().Konverter(await laster.ByggGrunnlagAsync(kildeId));
        var vedtak = dok.Utsagn.Where(u => u.Type == "vedtakskompetanse").ToList();
        Assert.Equal([0, 1], vedtak.Select(u => MonsterStrukturkonverterer.Setningsnummer(Bhl53Ledd2, u.Sitat)).ToList());
        // Aktøren «Kommunen» peker på testorganet, så oppslaget ikke avhenger av katalogen.
        var fraId = Assert.Single(vedtak.Select(u => u.Fra).Distinct());
        dok = dok with
        {
            Aktorer = dok.Aktorer.Select(a => a.Id == fraId ? a with { Tekstform = organ.Navn, Varianter = [] } : a).ToList(),
            Utsagn = vedtak,
        };

        var r1 = await laster.LastAsync(kildeId, dok, "monster", "monster:ukjent", "Testlaster");
        Assert.All(r1.Rader, r => Assert.Equal(Forslagsutfall.Ny, r.Utfall));
        var ider = r1.Rader.Select(r => r.KantId!.Value).ToList();
        Assert.Equal(2, ider.Distinct().Count());
        var objekter = await db.Strukturkanter.Where(k => ider.Contains(k.Id)).Select(k => k.Objekt!).ToListAsync();
        Assert.Contains(objekter, o => o.Contains("pålegg"));
        Assert.Contains(objekter, o => o.Contains("stenging"));

        var antall = await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == kildeId);
        var r2 = await laster.LastAsync(kildeId, dok, "monster", "monster:ukjent", "Testlaster");
        Assert.All(r2.Rader, r => Assert.Equal(Forslagsutfall.FantesSomForslag, r.Utfall));
        Assert.Equal(ider, r2.Rader.Select(r => r.KantId!.Value).ToList());
        Assert.Equal(antall, await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == kildeId));
    }

    /// <summary>AK 3 (#420): KI omformulerer objektet og siterer en del av setningen. Det er samme utsagn, og ingen ny rad lages.</summary>
    [Fact]
    public async Task KI_som_omformulerer_objekt_og_sitat_i_samme_setning_gir_ingen_ny_rad()
    {
        var (db, kildeId, eid, organ) = await NyBhl53Async();
        await using var _db = db;
        var laster = NyLaster(db);
        var r1 = await laster.LastAsync(kildeId, Dok(organ, Vedtak("u1", eid, Setning0, "gi pålegg til barnehagen om å rette forhold", "monster:test")),
            "monster", "monster:ukjent", "Testlaster");
        var antall = await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == kildeId);

        var ki = Dok(organ, Vedtak("k1", eid, "Kommunen kan gi pålegg til barnehagen", "pålegg om retting", "ki:test"));
        var r2 = await laster.LastAsync(kildeId, ki, "ki", "ki:test", "Testlaster");
        Assert.Equal(Forslagsutfall.FantesSomForslag, UtfallFor(r2, "k1"));
        Assert.Equal(KantIdFor(r1, "u1"), KantIdFor(r2, "k1"));
        Assert.Equal(antall, await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == kildeId));
    }

    /// <summary>AK 4 (#420, § 23 a-typen): to utsagn i samme setning og én eksisterende rad gir én ny. En ny kjøring gir ingen nye.</summary>
    [Fact]
    public async Task To_utsagn_i_samme_setning_bruker_opp_en_rad_hver()
    {
        var (db, kildeId, eid, organ) = await NyBhl53Async();
        await using var _db = db;
        var laster = NyLaster(db);
        await laster.LastAsync(kildeId, Dok(organ, Vedtak("u1", eid, Setning0, "pålegg om retting", "monster:test")), "monster", "monster:ukjent", "Testlaster");

        var ki = Dok(organ,
            Vedtak("k1", eid, "Kommunen kan gi pålegg til barnehagen", "pålegg til barnehagen", "ki:test"),
            Vedtak("k2", eid, "ved godkjente eller godkjenningspliktige virksomheter", "pålegg til godkjenningspliktige virksomheter", "ki:test"));
        var r1 = await laster.LastAsync(kildeId, ki, "ki", "ki:test", "Testlaster");
        Assert.Equal([Forslagsutfall.FantesSomForslag, Forslagsutfall.Ny], r1.Rader.Select(r => r.Utfall).Order().ToList());
        Assert.Equal(1, r1.PerKategori.Single(t => t.Kategori == "K").Nye);
        var antall = await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == kildeId);

        var r2 = await laster.LastAsync(kildeId, ki, "ki", "ki:test", "Testlaster");
        Assert.All(r2.Rader, r => Assert.Equal(Forslagsutfall.FantesSomForslag, r.Utfall));
        Assert.Equal(2, r2.Rader.Select(r => r.KantId).Distinct().Count());
        Assert.Equal(antall, await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == kildeId));
    }

    /// <summary>AK 5 (#420): et avvist utsagn i én setning foreslås ikke på nytt (#374), men et utsagn i en annen setning er et nytt utsagn.</summary>
    [Fact]
    public async Task Avvist_i_en_setning_foreslaas_ikke_paa_nytt_men_en_annen_setning_er_et_nytt_utsagn()
    {
        var (db, kildeId, eid, organ) = await NyBhl53Async();
        await using var _db = db;
        var laster = NyLaster(db);
        var r1 = await laster.LastAsync(kildeId, Dok(organ, Vedtak("u1", eid, Setning0, "pålegg om retting", "monster:test")),
            "monster", "monster:ukjent", "Testlaster");
        await new StrukturkantTjeneste(db).AvvisAsync(KantIdFor(r1, "u1")!.Value, "Kari Jurist", "Test #420.");

        var ki = Dok(organ,
            Vedtak("k1", eid, "Kommunen kan gi pålegg", "pålegg", "ki:test"),
            Vedtak("k2", eid, Setning1, "vedtak om stenging", "ki:test"));
        var r2 = await laster.LastAsync(kildeId, ki, "ki", "ki:test", "Testlaster");
        Assert.Equal(Forslagsutfall.TidligereAvvist, UtfallFor(r2, "k1"));
        Assert.Equal(KantIdFor(r1, "u1"), KantIdFor(r2, "k1"));
        Assert.Equal(Forslagsutfall.Ny, UtfallFor(r2, "k2"));
    }

    /// <summary>AK 6 (#420): en rad uten sitat i kommentaren (fasit, manuell) har ukjent setning og blokkerer som før.</summary>
    [Fact]
    public async Task Rad_uten_sitat_blokkerer_som_for()
    {
        var (db, kildeId, eid, organ) = await NyBhl53Async();
        await using var _db = db;
        var manuell = await new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(
            Strukturkanter.Kompetanse, "vedtak", Kantnode.Virksomhet(organ.Id), null,
            HjemmelRettskildeId: kildeId, HjemmelEid: eid, Objekt: "stenging", Kommentar: "Registrert for hånd."), "Kari Jurist");
        var antall = await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == kildeId);

        var r = await NyLaster(db).LastAsync(kildeId, Dok(organ, Vedtak("u2", eid, Setning1, "vedtak om tidsbegrenset eller varig stenging", "monster:test")),
            "monster", "monster:ukjent", "Testlaster");
        Assert.Equal(Forslagsutfall.FantesGodkjent, UtfallFor(r, "u2"));
        Assert.Equal(manuell.Kant.Id, KantIdFor(r, "u2"));
        Assert.Equal(antall, await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == kildeId));
    }
}
