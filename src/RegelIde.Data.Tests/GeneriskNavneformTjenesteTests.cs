using Microsoft.EntityFrameworkCore;
using RegelIde.Data.Strukturkonvertering;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #378] Generiske navneformer og bindingen per rettskilde, og aktørsveipet, mot ekte embedded Postgres. Spørsmålene:
/// betyr «styret» i rettskilde X det bindingen sier — og bare når bindingen er godkjent, med det smaleste spennet som vinner? Blir en
/// generisk omtale uten binding stående som generisk (ingen gjetting)? Gir aktørsveipet riktig status uten å skrive forslag, og bruker
/// lasteren bindingen?
/// <para>Termene er unike per test (Guid), fordi testsamlingen deler database og «departementet» brukes av lastertestene.</para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class GeneriskNavneformTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public GeneriskNavneformTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class KasterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Ingen eksterne oppslag her.");
    }

    private static NavnekandidatOppdagelseTjeneste NyeKandidater(RegelIdeDbContext db) => new(
        db, new VirksomhetsbegrepTjeneste(db), new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)),
        new VirksomhetOppslagTjeneste(db), new EksternNavneoppslagTjeneste(new HttpClient(new KasterHandler()), db), new StrukturkantTjeneste(db));

    private static StrukturforslagTjeneste NyLaster(RegelIdeDbContext db) =>
        new(db, new StrukturkantTjeneste(db), new VirksomhetOppslagTjeneste(db), NyeKandidater(db));

    private static async Task<Guid> ImporterAlkohollovenAsync(RegelIdeDbContext db) =>
        await new RettskildeImportTjeneste(db).ImporterAsync(LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));

    private static async Task<Virksomhet> NyVirksomhetAsync(RegelIdeDbContext db, string navn)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = navn };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    [Fact]
    public async Task Binding_oppløser_bare_godkjent_og_smaleste_spenn_vinner()
    {
        await using var db = _fixture.NyDbContext();
        var lovId = await ImporterAlkohollovenAsync(db);
        var paragraf = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.NodeType == "paragraf" && n.Entitetsstatus == "gjeldende")
            .OrderBy(n => n.Sorteringsrekkefolge).Skip(2).FirstAsync();
        var leddUnder = await db.RettskildeNoder.FirstAsync(n => n.ParentNodeId == paragraf.Id);
        var annenNode = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.Tekst != null && n.Sorteringsrekkefolge < paragraf.Sorteringsrekkefolge)
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        var a = await NyVirksomhetAsync(db, $"Binding A {Guid.NewGuid():N}");
        var b = await NyVirksomhetAsync(db, $"Binding B {Guid.NewGuid():N}");
        var tjeneste = new GeneriskNavneformTjeneste(db);
        var term = $"Styret{Guid.NewGuid():N}";

        var (navneform, varNy) = await tjeneste.OpprettEllerFinnAsync(term, "Test");
        Assert.True(varNy);
        Assert.Equal((term.ToLowerInvariant(), "generisk", (Guid?)null), (navneform.Term, navneform.Navneformgrunn, navneform.VirksomhetReferanseId));
        Assert.Equal(navneform.Id, (await tjeneste.OpprettEllerFinnAsync(term.ToUpperInvariant(), "Test")).Navneform.Id); // én per term

        // Uten binding: ubundet — ingen kobling.
        Assert.Equal(GeneriskOppslagstatus.Ubundet, (await tjeneste.LosOppAsync(navneform.Id, lovId, leddUnder.Eid)).Status);

        var hele = await tjeneste.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, lovId, Kantnode.Virksomhet(a.Id),
            HjemmelRettskildeId: lovId, HjemmelEid: annenNode.Eid, Kunnskapsstatus: "tekstfunn", Status: "godkjent"), "Test");
        Assert.NotNull(hele.HjemmelNodeId);
        var smal = await tjeneste.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, lovId, Kantnode.Virksomhet(b.Id),
            Paragrafspenn: [new ParagrafspennPar(paragraf.Eid, null)],
            KildeUtenforKorpusTekst: "Kgl.res. om ansvarsfordeling", KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: "primaer",
            Kunnskapsstatus: "eksternt_dokumentert", Status: "maskinelt_foreslatt", OppdagelsesKilde: "ki:test"), "Test");

        // Forslaget brukes ikke: hele-rettskilden-bindingen gjelder, og forslaget telles.
        var o1 = await tjeneste.LosOppAsync(navneform.Id, lovId, leddUnder.Eid);
        Assert.Equal((GeneriskOppslagstatus.Bundet, a.Id, 1), (o1.Status, o1.Referent!.VirksomhetId!.Value, o1.AntallForeslatteBindinger));

        await tjeneste.GodkjennAsync(smal.Id, "Kari Jurist");
        Assert.Equal(b.Id, (await tjeneste.LosOppAsync(navneform.Id, lovId, leddUnder.Eid)).Referent!.VirksomhetId);   // smaleste vinner
        Assert.Equal(b.Id, (await tjeneste.LosOppAsync(navneform.Id, lovId, paragraf.Eid)).Referent!.VirksomhetId);
        Assert.Equal(a.Id, (await tjeneste.LosOppAsync(navneform.Id, lovId, annenNode.Eid)).Referent!.VirksomhetId);    // utenfor spennet

        // Samme virkeområde to ganger er en motsigelse.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, lovId,
            Kantnode.Virksomhet(b.Id), HjemmelRettskildeId: lovId, HjemmelEid: annenNode.Eid), "Test"));
        // Fortolkning uten tolkningsargumenter, tekstfunn uten hjemmel og hjemmel + kilde samtidig avvises.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, lovId,
            Kantnode.Virksomhet(a.Id), Paragrafspenn: [new ParagrafspennPar(annenNode.Eid, null)], HjemmelRettskildeId: lovId,
            HjemmelEid: annenNode.Eid, Kunnskapsstatus: "fortolkning", Begrunnelse: "fordi"), "Test"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, lovId,
            Kantnode.Virksomhet(a.Id), Paragrafspenn: [new ParagrafspennPar(annenNode.Eid, null)],
            KildeUtenforKorpusTekst: "x", KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: "primaer", Kunnskapsstatus: "tekstfunn"), "Test"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, lovId,
            Kantnode.Virksomhet(a.Id), Paragrafspenn: [new ParagrafspennPar(annenNode.Eid, null)], HjemmelRettskildeId: lovId, HjemmelEid: annenNode.Eid,
            KildeUtenforKorpusTekst: "x", KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: "primaer"), "Test"));

        // En navneform for én virksomhet kan ikke bindes, og en term som alt er navneform for en virksomhet blir ikke generisk.
        var bundet = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(a.Id, $"Kortform{Guid.NewGuid():N}", "Test");
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettBindingAsync(new NyNavneformBinding(bundet.Id, lovId,
            Kantnode.Virksomhet(a.Id), HjemmelRettskildeId: lovId, HjemmelEid: annenNode.Eid), "Test"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettEllerFinnAsync(bundet.Term, "Test"));

        // Avvist smal binding: hele-rettskilden gjelder igjen. Raden står med hvem og hvorfor.
        await tjeneste.AvvisAsync(smal.Id, "Kari Jurist", "Feil departement.");
        Assert.Equal(a.Id, (await tjeneste.LosOppAsync(navneform.Id, lovId, leddUnder.Eid)).Referent!.VirksomhetId);
        var avvist = await db.NavneformBindinger.SingleAsync(x => x.Id == smal.Id);
        Assert.Equal(("avvist", "Kari Jurist", "Feil departement."), (avvist.Status, avvist.SistEndretAv, avvist.AvvisningBegrunnelse));

        // AC7: oversikten viser rettskilden og bindingene.
        var oversikt = Assert.Single(await tjeneste.OversiktAsync(term));
        var bruk = Assert.Single(oversikt.Rettskilder);
        Assert.Equal(lovId, bruk.RettskildeId);
        Assert.Single(bruk.Bindinger); // den avviste vises ikke
    }

    /// <summary>[Ny, issue #378, Johanns beslutning 2026-10-16 etter juristrunden B1] «departementet» følger ressortfordelingen: en binding
    /// som er utløpt, brukes ikke i dag, men på en dato innenfor perioden; en etterfølgende binding med samme virkeområde er lov, en
    /// overlappende er ikke.</summary>
    [Fact]
    public async Task Binding_brukes_bare_innenfor_sin_gyldighet()
    {
        await using var db = _fixture.NyDbContext();
        var lovId = await ImporterAlkohollovenAsync(db);
        var node = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.Tekst != null).OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        var gammelt = await NyVirksomhetAsync(db, $"Gammelt dep {Guid.NewGuid():N}");
        var nytt = await NyVirksomhetAsync(db, $"Nytt dep {Guid.NewGuid():N}");
        var tjeneste = new GeneriskNavneformTjeneste(db);
        var (navneform, _) = await tjeneste.OpprettEllerFinnAsync($"departementet{Guid.NewGuid():N}", "Test");
        var idag = DateOnly.FromDateTime(DateTime.Today);
        NyNavneformBinding Binding(Guid referent, DateOnly? fra, DateOnly? til) => new(navneform.Id, lovId, Kantnode.Virksomhet(referent),
            KildeUtenforKorpusTekst: "Kgl.res. om ressortfordeling", KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: "primaer",
            Kunnskapsstatus: "eksternt_dokumentert", Status: "godkjent", GyldigFra: fra, GyldigTil: til);

        await tjeneste.OpprettBindingAsync(Binding(gammelt.Id, null, idag.AddDays(-10)), "Test");
        Assert.Equal(GeneriskOppslagstatus.Ubundet, (await tjeneste.LosOppAsync(navneform.Id, lovId, node.Eid)).Status); // utløpt i dag
        Assert.Equal(gammelt.Id, (await tjeneste.LosOppAsync(navneform.Id, lovId, node.Eid, dato: idag.AddDays(-20))).Referent!.VirksomhetId);

        // Etterfølgende binding med samme virkeområde: lov. Overlappende: avvist. Fra etter til: avvist.
        await tjeneste.OpprettBindingAsync(Binding(nytt.Id, idag.AddDays(-9), null), "Test");
        Assert.Equal(nytt.Id, (await tjeneste.LosOppAsync(navneform.Id, lovId, node.Eid)).Referent!.VirksomhetId);
        Assert.Equal(gammelt.Id, (await tjeneste.LosOppAsync(navneform.Id, lovId, node.Eid, dato: idag.AddDays(-10))).Referent!.VirksomhetId);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettBindingAsync(Binding(gammelt.Id, idag, null), "Test"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettBindingAsync(Binding(gammelt.Id, idag.AddDays(5), idag), "Test"));

        var visning = (await tjeneste.HentBindingerAsync(navneform.Id)).OrderBy(b => b.GyldigFra ?? DateOnly.MinValue).ToList();
        Assert.Equal([(null, idag.AddDays(-10)), (idag.AddDays(-9), null)], visning.Select(b => (b.GyldigFra, b.GyldigTil)).ToList());
    }

    [Fact]
    public async Task Aktorsveip_klassifiserer_uten_aa_skrive_forslag_og_lasteren_bruker_bindingen()
    {
        await using var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var lovId = await ImporterAlkohollovenAsync(db);
        var annenLovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 8, 22)));
        var node = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.Tekst != null && n.Tekst.Contains("departementet"))
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        var g = Guid.NewGuid().ToString("N")[..8];
        var kjent = await NyVirksomhetAsync(db, $"Sveiporgan {g}");
        var dep = await NyVirksomhetAsync(db, $"Sveipdepartementet {g}");
        var kandidatOrgan = await NyVirksomhetAsync(db, $"Norges Sveipretten{g}");
        foreach (var v in new[] { await NyVirksomhetAsync(db, $"Fler A {g}"), await NyVirksomhetAsync(db, $"Fler B {g}") })
        {
            await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(v.Id, $"flertydig{g}", "Test");
        }
        // Lovspesifikt rollebegrep i en ANNEN lov skal ikke treffe i alkoholloven (#378).
        db.Begreper.Add(new BegrepEntitet { Id = Guid.NewGuid(), Begrepskategori = "rolle", LovkildeId = annenLovId, Term = $"annenlovsrolle{g}",
            Status = "publisert", OpprettetAv = "Test" });
        await db.SaveChangesAsync();

        var generiske = new GeneriskNavneformTjeneste(db);
        var (bundet, _) = await generiske.OpprettEllerFinnAsync($"departementet{g}", "Test");
        await generiske.OpprettBindingAsync(new NyNavneformBinding(bundet.Id, lovId, Kantnode.Virksomhet(dep.Id),
            KildeUtenforKorpusTekst: "Kgl.res. om ansvarsfordeling", KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: "primaer",
            Kunnskapsstatus: "eksternt_dokumentert", Status: "godkjent"), "Test");
        await generiske.OpprettEllerFinnAsync($"styret{g}", "Test");

        StrukturAktor A(string id, string t) => new(id, t, [], [], 1, null, null, null, null, null, null);
        StrukturUtsagn U(string id, string fra) => new(id, node.Eid, "sitat " + id, "kompetanse", "vedtakskompetanse", fra, null, "bevilling",
            "positiv", null, null, null, null, null) { Oppdagelseskilde = "monster:test" };
        var dok = new Strukturdokument("Alkoholloven", "eli", [], "test", 1,
            [A("a1", kjent.Navn), A("a2", $"Departementet{g}"), A("a3", $"styret{g}"), A("a4", $"Sveipretten{g}"), A("a5", $"Ukjent{g}"),
             A("a6", $"flertydig{g}"), A("a7", $"annenlovsrolle{g}")],
            [U("u1", "a1"), U("u2", "a2"), U("u3", "a3"), U("u4", "a4"), U("u5", "a5"), U("u6", "a6"), U("u7", "a7")]);

        var kanterFor = await db.Strukturkanter.IgnoreQueryFilters().CountAsync();
        var kandidaterFor = await db.Navnekandidater.CountAsync();
        var laster = NyLaster(db);
        var sveip = await laster.AktorsveipAsync(lovId, dok, "monster", skrivNavnekandidater: false, "Test");
        string Status(string id) => sveip.Aktorer.Single(r => r.AktorId == id).Status;
        Assert.Equal(Aktorstatus.Kjent, Status("a1"));
        Assert.Equal(Aktorstatus.Kjent, Status("a2"));             // generisk, bundet for rettskilden
        Assert.Equal(dep.Id, sveip.Aktorer.Single(r => r.AktorId == "a2").Referent!.Id);
        Assert.Equal(Aktorstatus.Generisk, Status("a3"));           // generisk uten binding
        Assert.Equal(Aktorstatus.NyNavneformForKjentAktor, Status("a4"));
        Assert.Contains(kandidatOrgan.Navn, sveip.Aktorer.Single(r => r.AktorId == "a4").Kandidater);
        Assert.Equal(Aktorstatus.NyAktor, Status("a5"));
        Assert.Equal(Aktorstatus.Flertydig, Status("a6"));
        Assert.Equal(Aktorstatus.NyAktor, Status("a7"));            // rolle i en annen lov treffer ikke
        Assert.Equal(7, sveip.PerStatus.Sum(p => p.Antall));
        Assert.Equal(kanterFor, await db.Strukturkanter.IgnoreQueryFilters().CountAsync());   // ingen forslag
        Assert.Equal(kandidaterFor, await db.Navnekandidater.CountAsync());                   // tørrkjøring

        // Med navnekandidater: de som ikke er kjent og ikke flertydige (a3, a4, a5, a7) — bare der tekstformen står i noden.
        var medKandidater = await laster.AktorsveipAsync(lovId, dok, "monster", skrivNavnekandidater: true, "Test");
        Assert.All(medKandidater.Aktorer.Where(r => r.Status is Aktorstatus.Kjent or Aktorstatus.Flertydig), r => Assert.Null(r.NavnekandidatId));
        Assert.Equal(kanterFor, await db.Strukturkanter.IgnoreQueryFilters().CountAsync());

        // Steg 2: lasteren bruker bindingen (u2 → forslag fra departementet), og den generiske uten binding faller bort med egen årsak.
        var rapport = await laster.LastAsync(lovId, dok, "monster", "monster:ukjent", "Test");
        var u2 = rapport.Rader.Single(r => r.UtsagnId == "u2");
        Assert.Equal(Forslagsutfall.Ny, u2.Utfall);
        Assert.Equal(dep.Id, (await db.Strukturkanter.SingleAsync(k => k.Id == u2.KantId)).FraVirksomhetId);
        Assert.Equal(Forslagsarsak.AktorGeneriskUtenBinding, rapport.Rader.Single(r => r.UtsagnId == "u3").Arsak);
        Assert.Equal(Forslagsarsak.AktorFlertydig, rapport.Rader.Single(r => r.UtsagnId == "u6").Arsak);
        Assert.Equal(2, rapport.AktorerPerStatus.Single(p => p.Status == Aktorstatus.Kjent).Antall);
    }

    [Fact]
    public async Task Marker_som_generisk_med_binding_godkjenner_kandidaten()
    {
        await using var db = _fixture.NyDbContext();
        var lovId = await ImporterAlkohollovenAsync(db);
        var node = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.Tekst != null && n.Tekst.Length > 20)
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        var dep = await NyVirksomhetAsync(db, $"Markerdep {Guid.NewGuid():N}");
        var term = $"retten{Guid.NewGuid():N}";
        var kandidater = NyeKandidater(db);
        var kandidat = await kandidater.OpprettEllerFinnAsync(term, "gruppe", lovId, node.Eid, 0, 5, "Test");

        // En binding skriveveien avviser, endrer ingenting.
        await Assert.ThrowsAsync<ArgumentException>(() => kandidater.MarkerSomGeneriskAsync(kandidat.Id,
            new NyNavneformBinding(Guid.Empty, Guid.Empty, Kantnode.Virksomhet(dep.Id), Kunnskapsstatus: "tekstfunn"), "Kari"));
        Assert.Null(await new GeneriskNavneformTjeneste(db).FinnAsync(term));
        Assert.Equal("Venter", (await db.Navnekandidater.AsNoTracking().SingleAsync(k => k.Id == kandidat.Id)).Status);

        await using var db2 = _fixture.NyDbContext();
        var r = await NyeKandidater(db2).MarkerSomGeneriskAsync(kandidat.Id,
            new NyNavneformBinding(Guid.Empty, Guid.Empty, Kantnode.Virksomhet(dep.Id), HjemmelRettskildeId: lovId, HjemmelEid: node.Eid,
                Kunnskapsstatus: "fortolkning", Begrunnelse: "Ordlyd og kontekst.", Tolkningsargumenter: ["ordlyd", "kontekst_system"], Status: "godkjent"),
            "Kari");
        Assert.NotNull(r);
        Assert.Equal(("Godkjent", "generisk", lovId), (r!.Kandidat.Status, r.Navneform.Navneformgrunn, r.Binding!.RettskildeId));
        Assert.Equal(dep.Id, (await new GeneriskNavneformTjeneste(db2).LosOppAsync(r.Navneform.Id, lovId, node.Eid)).Referent!.VirksomhetId);

        // Taggen eies av alkohollovens ansvarlige departement og ville vist seg i TekstTaggTjenesteTests (samme database, samme lov) —
        // fjernes etter testen.
        db2.TekstTagger.RemoveRange(await db2.TekstTagger.Where(t => t.RefId == r.Navneform.Id).ToListAsync());
        await db2.SaveChangesAsync();
    }
}
