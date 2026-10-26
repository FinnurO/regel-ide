using Microsoft.EntityFrameworkCore;
using RegelIde.Data.Strukturkonvertering;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #387] Fasiten inn i appen, mot ekte embedded Postgres. Spørsmålene AK 1–5 gjør besvarbare:
/// blir et verifisert fasitutsagn en faglig vurdert kant med opphav fasit:&lt;fil&gt;:&lt;id&gt; og fasitens kunnskapsstatus (AK 1)? Løses
/// aktørene uten gjetting, og listes resten (AK 2)? Kan importen kjøres på nytt — lik rad uendret, endret rad revidert, avvist respektert
/// (AK 3)? Overskrives aldri et godkjent utsagn fra en annen kilde? Blir en rad aldri sterkere enn det svakeste premisset (docs/33 §4.6)?
/// </summary>
[Collection(DataTestCollection.Navn)]
public class FasitimportTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public FasitimportTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class KasterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Fasitimporten skal aldri gjøre eksterne oppslag.");
    }

    private static StrukturforslagTjeneste NyLaster(RegelIdeDbContext db) => new(
        db, new StrukturkantTjeneste(db), new VirksomhetOppslagTjeneste(db),
        new NavnekandidatOppdagelseTjeneste(
            db, new VirksomhetsbegrepTjeneste(db), new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)),
            new VirksomhetOppslagTjeneste(db), new EksternNavneoppslagTjeneste(new HttpClient(new KasterHandler()), db),
            new StrukturkantTjeneste(db)));

    private static StrukturAktor A(string id, string tekstform, string? entitetstype = null) =>
        new(id, tekstform, [], [], 1, entitetstype, null, null, null, null, null);

    private async Task<(Guid LovId, string Eli, RettskildeNodeEntitet Node, string Sitat)> NyLovAsync(RegelIdeDbContext db)
    {
        await Strukturkanter.SeedStartsettAsync(db);
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var eli = (await db.Rettskilder.SingleAsync(r => r.Id == lovId)).Eli!;
        var node = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.Tekst != null && n.Tekst.Length > 40)
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        return (lovId, eli, node, node.Tekst!.Substring(0, 30));
    }

    private static Fasitfil Fil(string eli, IReadOnlyList<StrukturAktor> aktorer, IReadOnlyList<StrukturUtsagn> utsagn,
        IReadOnlyList<FasitUtsagnUtenforKorpus>? utenfor = null) =>
        new("test", new Strukturdokument("Alkoholloven", eli, [], "test", 1, aktorer, utsagn), utenfor ?? []);

    private static StrukturUtsagn U(string id, string eid, string sitat, string kategori, string type, string? fra, string? til,
        string? objekt = null, string? avgrensning = null) =>
        new(id, eid, sitat, kategori, type, fra, til, objekt, "positiv", avgrensning, false, false, "hoy", "");

    [Fact]
    public async Task Fasiten_lastes_som_faglig_vurdert_med_opphav_og_kan_kjoeres_paa_nytt()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, eli, node, sitat) = await NyLovAsync(db);
        var g = Guid.NewGuid().ToString("N")[..8];
        var organ = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Fasitorgan {g}" };
        var annet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Fasitannet {g}" };
        db.Virksomheter.AddRange(organ, annet);
        await db.SaveChangesAsync();
        var aktorer = new[] { A("a1", organ.Navn), A("a2", annet.Navn), A("a3", $"ukjentaktør{g}") };
        var u1 = U("u1", node.Eid, sitat, "kompetanse", "vedtakskompetanse", "a1", null, objekt: "bevilling") with
            { Kunnskapsstatus = "normalisering", Begrunnelse = "regel x", Delegeringsadgang = "ikke_regulert" };
        var utsagn = new List<StrukturUtsagn>
        {
            u1,
            U("u2", node.Eid, sitat, "kompetanse", "tilsynskompetanse", "a3", null, objekt: "y"),           // aktør ukjent → listet
            U("u3", node.Eid, "dette står ikke i teksten", "kompetanse", "tilsynskompetanse", "a1", null, objekt: "z"), // eId-drift
            U("u4", node.Eid + "/finnes-ikke", sitat, "kompetanse", "tilsynskompetanse", "a1", null, objekt: "z"),      // eId-drift
            U("u5", node.Eid, sitat, "kompetanse", "annet:noe", "a1", null),                                // ingen databasetype
            U("u6", node.Eid, sitat, "relasjon", "ledes_av", "a1", "a2"),
        };
        var laster = NyLaster(db);

        // Tørrkjøring skriver ingenting.
        var antall0 = await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == lovId);
        var torr = await laster.ImporterFasitAsync(Fil(eli, aktorer, utsagn), torrkjoring: true, navnekandidater: false, "Test");
        Assert.Equal(Fasitutfall.Ny, torr.Rader.Single(r => r.UtsagnId == "u1").Utfall);
        Assert.Equal(antall0, await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == lovId));

        var r1 = await laster.ImporterFasitAsync(Fil(eli, aktorer, utsagn), false, false, "Test");
        FasitimportRad Rad(FasitimportRapport r, string id) => r.Rader.Single(x => x.UtsagnId == id);
        Assert.Equal(Fasitutfall.Ny, Rad(r1, "u1").Utfall);
        Assert.Equal(Forslagsarsak.AktorIkkeIKatalogen, Rad(r1, "u2").Arsak);
        Assert.Equal(Fasitarsak.EidDrift, Rad(r1, "u3").Arsak);
        Assert.Equal(Fasitarsak.EidDrift, Rad(r1, "u4").Arsak);
        Assert.Equal(Forslagsarsak.TypeUtenTypekode, Rad(r1, "u5").Arsak);
        var uloest = Assert.Single(r1.UloesteAktorer);
        Assert.Equal(("a3", null), (uloest.AktorId, uloest.NavnekandidatId)); // ingen navnekandidat uten ?navnekandidater, ingen aktør opprettet
        Assert.False(await db.Virksomheter.AnyAsync(v => v.Navn == $"ukjentaktør{g}"));

        var kantId1 = Rad(r1, "u1").KantId;
        var kant = await db.Strukturkanter.SingleAsync(k => k.Id == kantId1);
        Assert.Equal(("faglig_vurdert", "fasit:test:u1", "normalisering", "regel x", "bevilling", node.Id),
            (kant.Status, kant.OppdagelsesKilde, kant.Kunnskapsstatus, kant.Begrunnelse, kant.Objekt, kant.HjemmelNodeId!.Value));
        Assert.StartsWith($"«{sitat}»", kant.Kommentar);

        // Ny kjøring: uendret, ingen nye rader.
        var antall1 = await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == lovId);
        var r2 = await laster.ImporterFasitAsync(Fil(eli, aktorer, utsagn), false, false, "Test");
        Assert.Equal(Fasitutfall.Uendret, Rad(r2, "u1").Utfall);
        Assert.Equal(antall1, await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.HjemmelRettskildeId == lovId));

        // Fasiten endret → revisjon: ny rad, den gamle foreldet (slettes ikke).
        utsagn[0] = u1 with { Avgrensning = "bare salgsbevilling" };
        var r3 = await laster.ImporterFasitAsync(Fil(eli, aktorer, utsagn), false, false, "Test");
        Assert.Equal(Fasitutfall.Oppdatert, Rad(r3, "u1").Utfall);
        var gammel = await db.Strukturkanter.IgnoreQueryFilters().SingleAsync(k => k.Id == kant.Id);
        Assert.Equal("foreldet", gammel.Entitetsstatus);
        var kantId3 = Rad(r3, "u1").KantId;
        var nyKant = await db.Strukturkanter.SingleAsync(k => k.Id == kantId3);
        Assert.Equal(("bare salgsbevilling", kant.Id), (nyKant.AvgrensningTekst, nyKant.ErstatterKantId!.Value));

        // Avvist → respektert ved neste import.
        await new StrukturkantTjeneste(db).AvvisAsync(nyKant.Id, "Kari Jurist", "Ikke riktig.");
        var r4 = await laster.ImporterFasitAsync(Fil(eli, aktorer, utsagn), false, false, "Test");
        Assert.Equal(Fasitutfall.TidligereAvvist, Rad(r4, "u1").Utfall);
        Assert.Equal(0, await db.Strukturkanter.CountAsync(k => k.OppdagelsesKilde == "fasit:test:u1"));
    }

    [Fact]
    public async Task Godkjent_fra_annen_kilde_overskrives_aldri_og_ventende_forslag_erstattes()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, eli, node, sitat) = await NyLovAsync(db);
        var g = Guid.NewGuid().ToString("N")[..8];
        var (x, y, z) = (new Virksomhet { Id = Guid.NewGuid(), Navn = $"Konfliktorgan {g}" }, new Virksomhet { Id = Guid.NewGuid(), Navn = $"Konfliktleder {g}" },
            new Virksomhet { Id = Guid.NewGuid(), Navn = $"Forslagsorgan {g}" });
        db.Virksomheter.AddRange(x, y, z);
        await db.SaveChangesAsync();
        var kanter = new StrukturkantTjeneste(db);
        var godkjent = (await kanter.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "ledes_av", Kantnode.Virksomhet(x.Id),
            Kantnode.Virksomhet(y.Id), HjemmelRettskildeId: lovId, HjemmelEid: node.Eid, AvgrensningTekst: "A"), "Kari Jurist")).Kant;
        var forslag = (await kanter.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "ledes_av", Kantnode.Virksomhet(z.Id),
            Kantnode.Virksomhet(y.Id), HjemmelRettskildeId: lovId, HjemmelEid: node.Eid, Status: Strukturkanter.MaskineltForeslatt,
            OppdagelsesKilde: "monster:test"), "Laster")).Kant;

        var fil = Fil(eli, [A("x", x.Navn), A("y", y.Navn), A("z", z.Navn)],
        [
            U("k1", node.Eid, sitat, "relasjon", "ledes_av", "x", "y", avgrensning: "B"),
            U("k2", node.Eid, sitat, "relasjon", "ledes_av", "z", "y"),
        ]);
        var r = await NyLaster(db).ImporterFasitAsync(fil, false, false, "Test");
        var k1 = r.Rader.Single(x => x.UtsagnId == "k1");
        Assert.Equal((Fasitutfall.Konflikt, godkjent.Id), (k1.Utfall, k1.KantId!.Value));
        Assert.Contains("avgrensning", k1.Detalj);
        Assert.Single(r.Konflikter);
        Assert.Equal("A", (await db.Strukturkanter.SingleAsync(k => k.Id == godkjent.Id)).AvgrensningTekst); // ikke overskrevet

        var k2 = r.Rader.Single(x => x.UtsagnId == "k2");
        Assert.Equal(Fasitutfall.ErstattetForslag, k2.Utfall);
        Assert.Equal("foreldet", (await db.Strukturkanter.IgnoreQueryFilters().SingleAsync(k => k.Id == forslag.Id)).Entitetsstatus);
        var ny = await db.Strukturkanter.SingleAsync(k => k.Id == k2.KantId);
        Assert.Equal(("faglig_vurdert", "fasit:test:k2", forslag.Id), (ny.Status, ny.OppdagelsesKilde, ny.ErstatterKantId!.Value));
    }

    /// <summary>[Ny, Johanns avgjørelse 2026-10-26 på #387] Fasiten bekrefter et godkjent utsagn med samme innhold: ett ekstra grunnlag
    /// (S1, <c>strukturkant_bekreftelser</c>), statusen og opphavet står, og ny kjøring lager ingen ny bekreftelse.</summary>
    [Fact]
    public async Task Godkjent_med_samme_innhold_faar_fasiten_som_bekreftelse()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, eli, node, sitat) = await NyLovAsync(db);
        var g = Guid.NewGuid().ToString("N")[..8];
        var (x, y) = (new Virksomhet { Id = Guid.NewGuid(), Navn = $"Bekreftet {g}" }, new Virksomhet { Id = Guid.NewGuid(), Navn = $"Bekreftleder {g}" });
        db.Virksomheter.AddRange(x, y);
        await db.SaveChangesAsync();
        var godkjent = (await new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "ledes_av", Kantnode.Virksomhet(x.Id),
            Kantnode.Virksomhet(y.Id), HjemmelRettskildeId: lovId, HjemmelEid: node.Eid), "Kari Jurist")).Kant;
        var fil = Fil(eli, [A("x", x.Navn), A("y", y.Navn)], [U("b1", node.Eid, sitat, "relasjon", "ledes_av", "x", "y")]);
        var laster = NyLaster(db);

        var torr = await laster.ImporterFasitAsync(fil, true, false, "Test");
        Assert.Equal((1, 0), (torr.BekreftelserNye, await db.StrukturkantBekreftelser.CountAsync(b => b.KantId == godkjent.Id)));

        var r = await laster.ImporterFasitAsync(fil, false, false, "Test");
        Assert.Equal((Fasitutfall.FantesVurdert, godkjent.Id), (r.Rader.Single().Utfall, r.Rader.Single().KantId!.Value));
        var b = await db.StrukturkantBekreftelser.SingleAsync(b => b.KantId == godkjent.Id);
        Assert.Equal(("fasit:test:b1", lovId, node.Eid), (b.OppdagelsesKilde, b.HjemmelRettskildeId, b.HjemmelEid));
        var kant = await db.Strukturkanter.SingleAsync(k => k.Id == godkjent.Id);
        Assert.Equal(("godkjent", "manuell"), (kant.Status, kant.OppdagelsesKilde));

        var r2 = await laster.ImporterFasitAsync(fil, false, false, "Test");
        Assert.Equal((0, 1), (r2.BekreftelserNye, r2.BekreftelserFantes));
        Assert.Equal(1, await db.StrukturkantBekreftelser.CountAsync(b => b.KantId == godkjent.Id));
    }

    /// <summary>[Ny, Johanns avgjørelse 2026-10-26 på #387] Lav sikkerhet i fasiten gir manuelt forslag med kunnskapsstatus uavklart — også når
    /// raden alt ligger som faglig vurdert fra en tidligere import (revisjon: den gamle raden blir foreldet, ikke slettet).</summary>
    [Fact]
    public async Task Lav_sikkerhet_blir_manuelt_forslag_uavklart_ogsaa_for_tidligere_lastet_rad()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, eli, node, sitat) = await NyLovAsync(db);
        var g = Guid.NewGuid().ToString("N")[..8];
        var organ = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Usikkert organ {g}" };
        db.Virksomheter.Add(organ);
        await db.SaveChangesAsync();
        // Slik importen før oppfølgingen skrev raden: faglig vurdert, fasitens normalisering.
        var gammel = (await new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "tilsyn", Kantnode.Virksomhet(organ.Id),
            null, HjemmelRettskildeId: lovId, HjemmelEid: node.Eid, Objekt: "salg", Status: Strukturkanter.FagligVurdert,
            OppdagelsesKilde: "fasit:test:l1", Kunnskapsstatus: "normalisering", Begrunnelse: "regel z"), "Test")).Kant;
        var l1 = new StrukturUtsagn("l1", node.Eid, sitat, "kompetanse", "tilsynskompetanse", "o", null, "salg", "positiv", null, false, false, "lav", "")
            { Kunnskapsstatus = "normalisering", Begrunnelse = "regel z" };
        var fil = Fil(eli, [A("o", organ.Navn)], [l1]);
        var laster = NyLaster(db);

        var r = await laster.ImporterFasitAsync(fil, false, false, "Test");
        var rad = r.Rader.Single();
        Assert.Equal((Fasitutfall.Oppdatert, "uavklart", 1), (rad.Utfall, rad.Kunnskapsstatus, r.LavSikkerhetSomForslag));
        var ny = await db.Strukturkanter.SingleAsync(k => k.Id == rad.KantId);
        Assert.Equal(("manuelt_foreslatt", "uavklart", "fasit:test:l1", gammel.Id), (ny.Status, ny.Kunnskapsstatus, ny.OppdagelsesKilde, ny.ErstatterKantId!.Value));
        Assert.Contains("sikkerhet lav", ny.Begrunnelse);
        Assert.Equal("foreldet", (await db.Strukturkanter.IgnoreQueryFilters().SingleAsync(k => k.Id == gammel.Id)).Entitetsstatus);

        Assert.Equal(Fasitutfall.Uendret, (await laster.ImporterFasitAsync(fil, false, false, "Test")).Rader.Single().Utfall);
    }

    [Fact]
    public async Task Flertydig_navn_skilles_paa_entitetstype_og_premisset_svekker_kunnskapsstatusen()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, eli, node, sitat) = await NyLovAsync(db);
        var g = Guid.NewGuid().ToString("N")[..8];
        var organ = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Sted{g} tingrett" };
        var kommune = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Sted{g}" };
        var dep = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Fasitdepartementet {g}" };
        db.Virksomheter.AddRange(organ, kommune, dep);
        var omrade = new BegrepEntitet { Id = Guid.NewGuid(), Begrepskategori = "omrade", Term = $"Sted{g}", Status = "publisert", OpprettetAv = "Test" };
        db.Begreper.Add(omrade);
        await db.SaveChangesAsync();
        // Generisk omtale med en faglig vurdert binding som selv bare er «uavklart».
        var generiske = new GeneriskNavneformTjeneste(db);
        var (nf, _) = await generiske.OpprettEllerFinnAsync($"departementet{g}", "Test");
        await generiske.OpprettBindingAsync(new NyNavneformBinding(nf.Id, lovId, Kantnode.Virksomhet(dep.Id),
            KildeUtenforKorpusTekst: "Notat", KildeUtenforKorpusType: "nettside_annet", KildeUtenforKorpusDokumentasjon: "sekundaer",
            Kunnskapsstatus: "uavklart", Status: "faglig_vurdert"), "Test");

        var fil = Fil(eli, [A("tr", organ.Navn, "organ"), A("sted", $"Sted{g}", "omrade"), A("dep", $"departementet{g}", "organ"),
                A("flert", $"Sted{g}", null)],
        [
            U("f1", node.Eid, sitat, "ansvarsomrade", "har_ansvarsomrade", "tr", "sted"),
            U("f2", node.Eid, sitat, "ansvarsomrade", "har_sete_i", "tr", "flert"), // uten entitetstype: flertydig, ikke lastet
            U("p1", node.Eid, sitat, "kompetanse", "tilsynskompetanse", "dep", null, objekt: "salg") with
                { Kunnskapsstatus = "normalisering", Begrunnelse = "regel y" },
        ]);
        var r = await NyLaster(db).ImporterFasitAsync(fil, false, false, "Test");
        var f1 = r.Rader.Single(x => x.UtsagnId == "f1");
        Assert.Equal((Fasitutfall.Ny, "entitetstype"), (f1.Utfall, f1.Aktoroppslag));
        Assert.Equal(omrade.Id, (await db.Strukturkanter.SingleAsync(k => k.Id == f1.KantId)).TilBegrepId);
        Assert.Equal(Forslagsarsak.AktorFlertydig, r.Rader.Single(x => x.UtsagnId == "f2").Arsak);

        var p1 = r.Rader.Single(x => x.UtsagnId == "p1");
        Assert.Equal(("normalisering", "uavklart"), (p1.FasitKunnskapsstatus, p1.Kunnskapsstatus));
        var kant = await db.Strukturkanter.SingleAsync(k => k.Id == p1.KantId);
        Assert.Equal((dep.Id, "uavklart"), (kant.FraVirksomhetId!.Value, kant.Kunnskapsstatus));
        Assert.Contains("svakeste premisset", kant.Begrunnelse);
        Assert.Equal(1, r.SvektEtterPremiss);
    }

    [Fact]
    public async Task Utsagn_med_kilde_utenfor_korpus_faar_kilden_og_eksternt_dokumentert()
    {
        await using var db = _fixture.NyDbContext();
        var (_, eli, _, _) = await NyLovAsync(db);
        var g = Guid.NewGuid().ToString("N")[..8];
        var (da, tu) = (new Virksomhet { Id = Guid.NewGuid(), Navn = $"Administrasjonen {g}" }, new Virksomhet { Id = Guid.NewGuid(), Navn = $"Utvalget {g}" });
        db.Virksomheter.AddRange(da, tu);
        await db.SaveChangesAsync();
        var fil = Fil(eli, [A("da", da.Navn), A("tu", tu.Navn)], [],
        [
            new FasitUtsagnUtenforKorpus("x1", "relasjon", "sekretariat_for", "da", "tu", null, "positiv", null,
                new FasitKildeUtenforKorpus("Ot.prp. nr. 44 (2000–2001) kap. 11.5.12", "https://www.regjeringen.no/", "forarbeider", "primaer"),
                "eksternt_dokumentert", "Forarbeidene.", null, "Johann"),
        ]);
        var laster = NyLaster(db);
        var r = await laster.ImporterFasitAsync(fil, false, false, "Test");
        var x1 = Assert.Single(r.Rader);
        Assert.Equal(Fasitutfall.Ny, x1.Utfall);
        var kant = await db.Strukturkanter.SingleAsync(k => k.Id == x1.KantId);
        Assert.Equal(("faglig_vurdert", "fasit:test:x1", "forarbeider", "primaer", "eksternt_dokumentert", (Guid?)null),
            (kant.Status, kant.OppdagelsesKilde, kant.KildeUtenforKorpusType, kant.KildeUtenforKorpusDokumentasjon, kant.Kunnskapsstatus, kant.HjemmelRettskildeId));
        Assert.Equal(Fasitutfall.Uendret, Assert.Single((await laster.ImporterFasitAsync(fil, false, false, "Test")).Rader).Utfall);
    }

    [Fact]
    public void Rangeringen_foelger_docs_33_4_6()
    {
        var rekke = new[] { "tekstfunn", "normalisering", "avledet", "fortolkning", "uavklart", "omstridt" };
        for (var i = 1; i < rekke.Length; i++) Assert.True(Svakestepremiss.Rang(rekke[i - 1]) < Svakestepremiss.Rang(rekke[i]), rekke[i]);
        Assert.Equal(Svakestepremiss.Rang("tekstfunn"), Svakestepremiss.Rang("eksternt_dokumentert"));
        Assert.Equal(Svakestepremiss.Rang("uavklart"), Svakestepremiss.Rang(null)); // ikke vurdert er ikke sterkere enn uavklart
    }

    [Fact]
    public void Fasitfilene_kan_leses_og_domstolloven_har_sekretariatet_for_tilsynsutvalget()
    {
        var mappe = Path.Combine(FinnRepoRot(), "data", "fasit", "strukturmodell");
        var alle = Fasitfil.Filer.Select(f => Fasitfil.Les(mappe, f)).ToList();
        Assert.Equal(1825, alle.Sum(f => f.Dokument.Utsagn.Count));
        var x1 = Assert.Single(alle.Single(f => f.Navn == "domstolloven").UtenforKorpus);
        Assert.Equal(("sekretariat_for", "da", "tilsyn", "forarbeider", "primaer"), (x1.Type, x1.Fra, x1.Til, x1.Kilde.Type, x1.Kilde.Dokumentasjon));
        Assert.Throws<ArgumentException>(() => Fasitfil.Les(mappe, "juristrunde-368-oppgave"));
    }

    private static string FinnRepoRot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "data", "fasit"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Fant ikke repo-roten (data/fasit).");
    }
}
