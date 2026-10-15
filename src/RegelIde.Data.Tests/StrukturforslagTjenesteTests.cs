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
}
