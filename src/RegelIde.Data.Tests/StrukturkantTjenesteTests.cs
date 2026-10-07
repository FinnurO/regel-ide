using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #311 «Strukturmodell 6: én typestyrt kanttabell», 2026-10-07] <see cref="StrukturkantTjeneste"/>
/// mot ekte embedded Postgres. Erstatter testene for de tre tjenestene som ble slått sammen
/// (<c>VirksomhetRelasjonregisterTjenesteTests</c>, <c>GruppeMedlemskapTjenesteTests</c>,
/// <c>MyndighetstildelingTjenesteTests</c> — [FJERNET, #311]); oppførselen de testet er tatt med her
/// (sykel med navngitt kjede, diamant, gyldighet arvet fra hjemmelen, visningstekst fra begge sider,
/// navneform framfor registernavn, forslag/godkjenn/avvis).
/// <para>
/// Spørsmålene testene besvarer (issue #311 AC1/AC4): kan alle åtte kategoriene lagres med de felles
/// egenskapene, og avvises en kant som ikke kan være sann (feil nodetype, sykel, uten kilde)? Typekodene
/// seedes med <see cref="Strukturkanter.SeedStartsettAsync"/> — samme lista API-et seeder ved oppstart.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class StrukturkantTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public StrukturkantTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // Alkoholloven/-forskriften importeres idempotent og deles på tvers av testene i den delte databasen —
    // alle termer og navn må derfor være unike per test (samme begrunnelse som de gamle testene).
    private static string Unik(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    private sealed record Oppsett(Guid LovId, Guid ForskriftId, string ParagrafEid, string AnnenParagrafEid, string ForskriftEid);

    private static async Task<Oppsett> NyttOppsettAsync(RegelIdeDbContext db)
    {
        await Strukturkanter.SeedStartsettAsync(db);
        var importer = new RettskildeImportTjeneste(db);
        var lovId = await importer.ImporterAsync(LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var forskriftId = await importer.ImporterAsync(LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 22)));
        var paragrafer = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge).Take(2).ToListAsync();
        var forskriftNode = await db.RettskildeNoder.Where(n => n.RettskildeId == forskriftId && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        return new Oppsett(lovId, forskriftId, paragrafer[0].Eid, paragrafer[1].Eid, forskriftNode.Eid);
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn = "Testorgan")
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik(navn) };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    private static async Task<Guid> NyttBegrepAsync(RegelIdeDbContext db, string nodetype, Guid lovId, string prefiks) =>
        (await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(nodetype, lovId, Unik(prefiks), "Kari Jurist")).Id;

    // ---------------- Én test per kategori: kan den lagres med de felles egenskapene? ----------------

    [Fact]
    public async Task R_relasjon_lagres_med_proveniens_og_ulik_visningstekst_fra_hver_side()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (fra, til) = (await NyVirksomhetAsync(db, "Lokal nemnd"), await NyVirksomhetAsync(db, "Statsforvalteren"));
        var tjeneste = new StrukturkantTjeneste(db);

        var r = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "klageinstans", Kantnode.Virksomhet(fra), Kantnode.Virksomhet(til),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Polaritet: "positiv"), "Kari Jurist");

        Assert.True(r.VarNy);
        Assert.Equal("manuell", r.Kant.OppdagelsesKilde);
        Assert.Equal("validert", r.Kant.Status);
        var prov = await db.Proveniens.SingleAsync(p => p.EntitetId == r.Kant.Id);
        Assert.Equal((StrukturkantTjeneste.ProveniensType, "opprettet"), (prov.EntitetType, prov.Handling));

        var fraSiden = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(fra)));
        var tilSiden = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(til)));
        Assert.Equal("fra", fraSiden.Retning);
        Assert.Equal("til", tilSiden.Retning);
        Assert.StartsWith("har klageinstans hos ", fraSiden.Visningstekst);
        Assert.StartsWith("er klageinstans for ", tilSiden.Visningstekst);
    }

    [Fact]
    public async Task K_kompetanse_uten_tilnode_lagres_med_objekt_og_avgrensning()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var dep = await NyVirksomhetAsync(db, "Departementet");
        var tjeneste = new StrukturkantTjeneste(db);

        var k = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Kompetanse, "forskrift", Kantnode.Virksomhet(dep), Til: null,
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Paragrafspenn: [new ParagrafspennPar(o.ParagrafEid, o.AnnenParagrafEid)],
            Objekt: "salgs- og skjenketider", Polaritet: "positiv"), "Kari Jurist");

        var v = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(dep), Strukturkanter.Kompetanse));
        Assert.Null(v.Til);
        Assert.Equal("har forskriftskompetanse: salgs- og skjenketider", v.Visningstekst);
        Assert.Equal(new ParagrafspennPar(o.ParagrafEid, o.AnnenParagrafEid), Assert.Single(v.Paragrafspenn));
        Assert.Equal(k.Kant.Id, v.Id);
    }

    [Fact]
    public async Task K_kompetanse_kan_ligge_hos_en_rolle_men_ikke_uten_gjenstand()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var rolle = await NyttBegrepAsync(db, Nodetyper.Rolle, o.LovId, "reguleringsmyndighet");
        var tjeneste = new StrukturkantTjeneste(db);

        var ok = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Kompetanse, "vedtak", Kantnode.Begrep(rolle), null,
            HjemmelRettskildeId: o.LovId, Objekt: "vedtak om tariffer", Polaritet: "positiv"), "Kari Jurist");
        Assert.True(ok.VarNy);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Kompetanse, "vedtak", Kantnode.Begrep(rolle), null, HjemmelRettskildeId: o.LovId), "Kari Jurist"));
        Assert.Contains("HVA kompetansen gjelder", ex.Message);
    }

    [Fact]
    public async Task M_medlemskap_virksomhet_i_klasse_og_klasse_i_omrade()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var kommune = await NyVirksomhetAsync(db, "Karasjok kommune");
        var klasse = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "språkutviklingskommuner");
        var omrade = await NyttBegrepAsync(db, Nodetyper.Omrade, o.LovId, "forvaltningsområdet");
        var tjeneste = new StrukturkantTjeneste(db);
        var spenn = new[] { new ParagrafspennPar(o.ForskriftEid, null) };

        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Medlemskap, Strukturkanter.MedlemAv,
            Kantnode.Virksomhet(kommune), Kantnode.Begrep(klasse), HjemmelRettskildeId: o.ForskriftId, Paragrafspenn: spenn), "Kari Jurist");
        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Medlemskap, Strukturkanter.MedlemAv,
            Kantnode.Begrep(klasse), Kantnode.Begrep(omrade), HjemmelRettskildeId: o.ForskriftId, Paragrafspenn: spenn), "Kari Jurist");

        var forKlassen = await tjeneste.HentForNodeAsync(Kantnode.Begrep(klasse), Strukturkanter.Medlemskap);
        Assert.Equal(2, forKlassen.Count);
        Assert.Contains(forKlassen, v => v.Retning == "til" && v.Fra.Id == kommune && v.Visningstekst.StartsWith("har medlem "));
        Assert.Contains(forKlassen, v => v.Retning == "fra" && v.Til!.Id == omrade && v.Til.Nodetype == Nodetyper.Omrade);
    }

    [Fact]
    public async Task M_og_I_krever_paragrafspenn_naar_hjemmelen_er_i_korpus()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var v = await NyVirksomhetAsync(db);
        var klasse = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "klasse-uten-spenn");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(
            Strukturkanter.Medlemskap, Strukturkanter.MedlemAv, Kantnode.Virksomhet(v), Kantnode.Begrep(klasse),
            HjemmelRettskildeId: o.LovId), "Kari Jurist"));
        Assert.Contains("paragrafspenn", ex.Message);
    }

    [Fact]
    public async Task Tildeling_blir_I_naar_maalet_er_en_rolle_og_M_ellers()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var nve = await NyVirksomhetAsync(db, "NVE");
        var rolle = await NyttBegrepAsync(db, Nodetyper.Rolle, o.LovId, "konsesjonsmyndigheten");
        var klasse = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "direktorater");
        var tjeneste = new StrukturkantTjeneste(db);
        var spenn = new[] { new ParagrafspennPar(o.ParagrafEid, null) };

        var i = await tjeneste.OpprettTildelingAsync(nve, rolle, o.LovId, spenn, "bare konsesjoner etter § 3-1", "Kari Jurist");
        var m = await tjeneste.OpprettTildelingAsync(nve, klasse, o.LovId, spenn, null, "Kari Jurist");

        Assert.Equal((Strukturkanter.Rolleinnehav, Strukturkanter.Innehar), (i.Kant.Kategori, i.Kant.Typekode));
        Assert.Equal("bare konsesjoner etter § 3-1", i.Kant.AvgrensningTekst);
        Assert.Equal((Strukturkanter.Medlemskap, Strukturkanter.MedlemAv), (m.Kant.Kategori, m.Kant.Typekode));

        // M til en rolle er feil kategori — rolleinnehav arves ikke (docs/33 §4.2), så det må være I.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Medlemskap, Strukturkanter.MedlemAv, Kantnode.Virksomhet(nve), Kantnode.Begrep(rolle),
            HjemmelRettskildeId: o.LovId, Paragrafspenn: spenn), "Kari Jurist"));
        Assert.Contains("rolle", ex.Message);
    }

    [Fact]
    public async Task O_omradesammensetning_bare_mellom_omrader()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var lagdomme = await NyttBegrepAsync(db, Nodetyper.Omrade, o.LovId, "Agder lagdømme");
        var rettskrets = await NyttBegrepAsync(db, Nodetyper.Omrade, o.LovId, "Agder tingretts rettskrets");
        var klasse = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "kommunene");
        var tjeneste = new StrukturkantTjeneste(db);

        var ok = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Omradesammensetning, "bestar_av",
            Kantnode.Begrep(lagdomme), Kantnode.Begrep(rettskrets), HjemmelRettskildeId: o.LovId), "Kari Jurist");
        Assert.True(ok.VarNy);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Omradesammensetning, "bestar_av", Kantnode.Begrep(klasse), Kantnode.Begrep(rettskrets),
            HjemmelRettskildeId: o.LovId), "Kari Jurist"));
    }

    [Fact]
    public async Task A_ansvarsomrade_G_organtilhorighet_og_T_klasseniva_lagres()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var statsforvalter = await NyVirksomhetAsync(db, "Statsforvalteren i Troms og Finnmark");
        var troms = await NyttBegrepAsync(db, Nodetyper.Omrade, o.LovId, "Troms");
        var rme = await NyVirksomhetAsync(db, "RME");
        var nve = await NyVirksomhetAsync(db, "NVE");
        var kommunene = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "kommunene");
        var tjeneste = new StrukturkantTjeneste(db);

        var a = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade",
            Kantnode.Virksomhet(statsforvalter), Kantnode.Begrep(troms),
            KildeUtenforKorpusTekst: "Kgl.res. om embetsområder", KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: "primaer", Polaritet: "positiv"), "Kari Jurist");
        var g = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Organtilhorighet, "del_av",
            Kantnode.Virksomhet(rme), Kantnode.Virksomhet(nve), HjemmelRettskildeId: o.LovId), "Kari Jurist");
        var t = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Klasseniva, "skal_ha",
            Kantnode.Begrep(kommunene), null, HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Objekt: "kommunestyre"), "Kari Jurist");

        Assert.All(new[] { a, g, t }, r => Assert.True(r.VarNy));
        var tVisning = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Begrep(kommunene), Strukturkanter.Klasseniva));
        Assert.Equal("skal (hvert medlem) ha kommunestyre", tVisning.Visningstekst);

        // A fra et OMRÅDE er feil retning: aktør → område.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Begrep(troms), Kantnode.Virksomhet(statsforvalter),
            HjemmelRettskildeId: o.LovId), "Kari Jurist"));
    }

    // ---------------- Felles egenskaper ----------------

    [Fact]
    public async Task Negativ_polaritet_lagres_og_er_et_annet_utsagn_enn_den_positive()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (dep, nemnd) = (await NyVirksomhetAsync(db, "Departementet"), await NyVirksomhetAsync(db, "Klagenemnda"));
        var tjeneste = new StrukturkantTjeneste(db);
        NyStrukturkant Kant(string polaritet) => new(Strukturkanter.Relasjon, "instruksjon",
            Kantnode.Virksomhet(dep), Kantnode.Virksomhet(nemnd), HjemmelRettskildeId: o.LovId, Polaritet: polaritet);

        var negativ = await tjeneste.OpprettAsync(Kant("negativ"), "Kari Jurist");
        var positiv = await tjeneste.OpprettAsync(Kant("positiv"), "Kari Jurist");

        Assert.True(negativ.VarNy);
        Assert.True(positiv.VarNy);
        Assert.Equal("negativ", (await db.Strukturkanter.SingleAsync(k => k.Id == negativ.Kant.Id)).Polaritet);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(Kant("kanskje"), "Kari Jurist"));
    }

    [Fact]
    public async Task Kilde_utenfor_korpus_erstatter_hjemmel_men_en_av_dem_er_paakrevd()
    {
        await using var db = _fixture.NyDbContext();
        await NyttOppsettAsync(db);
        var (rhf, hod) = (await NyVirksomhetAsync(db, "Helse Nord RHF"), await NyVirksomhetAsync(db, "HOD"));
        var tjeneste = new StrukturkantTjeneste(db);

        var r = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "eies_av",
            Kantnode.Virksomhet(rhf), Kantnode.Virksomhet(hod),
            KildeUtenforKorpusTekst: "Vedtekter for Helse Nord RHF § 3", KildeUtenforKorpusLenke: "https://helse-nord.no/vedtekter",
            KildeUtenforKorpusType: "vedtekter", KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer), "Kari Jurist");
        Assert.Null(r.Kant.HjemmelRettskildeId);
        Assert.Equal(("https://helse-nord.no/vedtekter", "vedtekter"), (r.Kant.KildeUtenforKorpusLenke, r.Kant.KildeUtenforKorpusType));

        // [Ny, Johanns beslutning 2026-10-07] Typen er påkrevd uten hjemmel, må være i lista, og en kant med
        // hjemmel i korpus kan ikke OGSÅ ha en kilde utenfor (da er typen NULL).
        var utenType = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "ledes_av", Kantnode.Virksomhet(rhf), Kantnode.Virksomhet(hod),
            KildeUtenforKorpusTekst: "styrevedtak 12/2025"), "Kari Jurist"));
        Assert.Contains("type", utenType.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "ledes_av", Kantnode.Virksomhet(rhf), Kantnode.Virksomhet(hod),
            KildeUtenforKorpusTekst: "styrevedtak 12/2025", KildeUtenforKorpusType: "rykte", KildeUtenforKorpusDokumentasjon: "primaer"), "Kari Jurist"));

        // [Ny, Johanns beslutning 2026-10-07] Dokumentasjonen (primær/sekundær) er påkrevd sammen med typen.
        // Tilsynsutvalget-eksempelet: opprettet ved kgl.res. 15. mai 2002, bare kjent gjennom en artikkel i Juristen.
        var utenDok = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "oppretter", Kantnode.Virksomhet(hod), Kantnode.Virksomhet(rhf),
            KildeUtenforKorpusTekst: "kgl.res. 15. mai 2002", KildeUtenforKorpusType: "kgl_res"), "Kari Jurist"));
        Assert.Contains("sekundaer", utenDok.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "oppretter", Kantnode.Virksomhet(hod), Kantnode.Virksomhet(rhf),
            KildeUtenforKorpusTekst: "kgl.res. 15. mai 2002", KildeUtenforKorpusType: "kgl_res",
            KildeUtenforKorpusDokumentasjon: "tertiaer"), "Kari Jurist"));
        var tilsynsutvalget = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "oppretter", Kantnode.Virksomhet(hod), Kantnode.Virksomhet(rhf),
            KildeUtenforKorpusTekst: "kgl.res. 15. mai 2002 — kjent gjennom artikkel i Juristen",
            KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: Strukturkanter.Sekundaer), "Kari Jurist");
        Assert.Equal(("kgl_res", "sekundaer"), (tilsynsutvalget.Kant.KildeUtenforKorpusType, tilsynsutvalget.Kant.KildeUtenforKorpusDokumentasjon));

        var utenKilde = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "ledes_av", Kantnode.Virksomhet(rhf), Kantnode.Virksomhet(hod)), "Kari Jurist"));
        Assert.Contains("kilde utenfor korpus", utenKilde.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "ledes_av", Kantnode.Virksomhet(rhf), Kantnode.Virksomhet(hod),
            KildeUtenforKorpusLenke: "https://example.org"), "Kari Jurist"));
    }

    [Fact]
    public async Task Hjemmel_i_korpus_utelukker_kilde_utenfor_og_arbeidslista_viser_bare_nettside_annet()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b, c) = (await NyVirksomhetAsync(db, "Sekretariatet"), await NyVirksomhetAsync(db, "Nemnda"), await NyVirksomhetAsync(db, "Departementet"));
        var tjeneste = new StrukturkantTjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "sekretariat_for", Kantnode.Virksomhet(a), Kantnode.Virksomhet(b),
            HjemmelRettskildeId: o.LovId, KildeUtenforKorpusTekst: "org-kart", KildeUtenforKorpusType: "nettside_annet",
            KildeUtenforKorpusDokumentasjon: "primaer"), "Kari Jurist"));

        var bareNettside = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "sekretariat_for",
            Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), KildeUtenforKorpusTekst: "organisasjonskartet",
            KildeUtenforKorpusType: Strukturkanter.NettsideAnnet, KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer), "Kari Jurist");
        var instruks = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "instruksjon",
            Kantnode.Virksomhet(c), Kantnode.Virksomhet(b), KildeUtenforKorpusTekst: "instruks for nemnda",
            KildeUtenforKorpusType: "instruks", KildeUtenforKorpusDokumentasjon: Strukturkanter.Sekundaer), "Kari Jurist");
        var hjemlet = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "klageinstans_for",
            Kantnode.Virksomhet(b), Kantnode.Virksomhet(c), HjemmelRettskildeId: o.LovId), "Kari Jurist");
        Assert.Null(hjemlet.Kant.KildeUtenforKorpusType);

        var arbeidsliste = await tjeneste.HentUtenKorpusforankringAsync(Strukturkanter.NettsideAnnet);
        Assert.Contains(arbeidsliste, v => v.Id == bareNettside.Kant.Id && v.KildeUtenforKorpusType == "nettside_annet");
        Assert.DoesNotContain(arbeidsliste, v => v.Id == instruks.Kant.Id || v.Id == hjemlet.Kant.Id);
        var alleUtenHjemmel = await tjeneste.HentUtenKorpusforankringAsync(null);
        Assert.Contains(alleUtenHjemmel, v => v.Id == instruks.Kant.Id);
        Assert.DoesNotContain(alleUtenHjemmel, v => v.Id == hjemlet.Kant.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.HentUtenKorpusforankringAsync("blogg"));
    }

    [Fact]
    public async Task Databasen_avviser_selv_en_kant_uten_kilde_og_en_kant_med_to_fra_noder()
    {
        await using var db = _fixture.NyDbContext();
        var (a, b) = (await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = "R", Typekode = "underlagt", FraVirksomhetId = a, TilVirksomhetId = b, OpprettetAv = "test",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var begrep = await db.Begreper.Select(x => x.Id).FirstAsync();
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = "R", Typekode = "underlagt", FraVirksomhetId = a, FraBegrepId = begrep,
            TilVirksomhetId = b, KildeUtenforKorpusTekst = "x", OpprettetAv = "test",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Avgrensning_med_ukjent_eid_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b) = (await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        var tjeneste = new StrukturkantTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "delegerer_til", Kantnode.Virksomhet(a), Kantnode.Virksomhet(b),
            HjemmelRettskildeId: o.LovId, Paragrafspenn: [new ParagrafspennPar("finnes/ikke", null)]), "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "delegerer_til", Kantnode.Virksomhet(a), Kantnode.Virksomhet(b),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ForskriftEid), "Kari Jurist")); // eId fra en ANNEN rettskilde
    }

    [Fact]
    public async Task Typekoden_maa_finnes_aktiv_for_kategorien()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b) = (await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        var tjeneste = new StrukturkantTjeneste(db);
        // 'forskrift' er en K-type, ikke en R-type.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "forskrift", Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId), "Kari Jurist"));

        var inaktiv = Unik("inaktiv");
        db.RelasjonsTypeKonfigurasjoner.Add(new RelasjonsTypeKonfigurasjonEntitet
        {
            Id = Guid.NewGuid(), Kategori = "R", Kode = inaktiv, FraVisningsmal = "{0}", TilVisningsmal = "{0}", Aktiv = false,
        });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, inaktiv, Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId), "Kari Jurist"));

        // Ny type = ny rad, ingen kodeendring (docs/33 §4.3).
        var ny = Unik("ny-type");
        db.RelasjonsTypeKonfigurasjoner.Add(new RelasjonsTypeKonfigurasjonEntitet
        {
            Id = Guid.NewGuid(), Kategori = "K", Kode = ny, FraVisningsmal = "kan {0}", TilVisningsmal = "{0}",
        });
        await db.SaveChangesAsync();
        var k = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, ny, Kantnode.Virksomhet(a), null,
            HjemmelRettskildeId: o.LovId, Objekt: "noe nytt"), "Kari Jurist");
        Assert.Equal(ny, k.Kant.Typekode);
    }

    [Fact]
    public async Task Selvkant_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var a = await NyVirksomhetAsync(db);
        await Assert.ThrowsAsync<ArgumentException>(() => new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "underlagt", Kantnode.Virksomhet(a), Kantnode.Virksomhet(a), HjemmelRettskildeId: o.LovId), "Kari Jurist"));
    }

    // ---------------- Sykel (bevart fra gruppe-av-gruppe, issue #164) ----------------

    [Fact]
    public async Task Sykel_i_medlemskap_avvises_og_kjeden_navngis_men_diamant_er_lov()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var a = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "A");
        var b = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "B");
        var c = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "C");
        var d = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "D");
        var tjeneste = new StrukturkantTjeneste(db);
        var spenn = new[] { new ParagrafspennPar(o.ParagrafEid, null) };
        Task<StrukturkantOpprettet> Medlem(Guid fra, Guid til) => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Medlemskap, Strukturkanter.MedlemAv, Kantnode.Begrep(fra), Kantnode.Begrep(til),
            HjemmelRettskildeId: o.LovId, Paragrafspenn: spenn), "Kari Jurist");

        await Medlem(a, b);  // A ⊂ B
        await Medlem(b, c);  // B ⊂ C
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Medlem(c, a)); // C ⊂ A lukker ringen
        var termA = (await db.Begreper.SingleAsync(x => x.Id == a)).Term;
        var termB = (await db.Begreper.SingleAsync(x => x.Id == b)).Term;
        Assert.Contains("sirkulær", ex.Message);
        Assert.Contains(termA, ex.Message);
        Assert.Contains(termB, ex.Message);

        // Diamant: D ⊂ A og D ⊂ B — to veier til samme node er ingen sykel.
        await Medlem(d, a);
        var diamant = await Medlem(d, b);
        Assert.True(diamant.VarNy);
    }

    [Fact]
    public async Task Sykel_i_omradesammensetning_avvises_men_R_har_ingen_sykelsjekk()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var x = await NyttBegrepAsync(db, Nodetyper.Omrade, o.LovId, "X");
        var y = await NyttBegrepAsync(db, Nodetyper.Omrade, o.LovId, "Y");
        var (v1, v2) = (await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        var tjeneste = new StrukturkantTjeneste(db);

        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Omradesammensetning, "bestar_av",
            Kantnode.Begrep(x), Kantnode.Begrep(y), HjemmelRettskildeId: o.LovId), "Kari Jurist");
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Omradesammensetning,
            "bestar_av", Kantnode.Begrep(y), Kantnode.Begrep(x), HjemmelRettskildeId: o.LovId), "Kari Jurist"));

        // docs/29 §C.3: A «underlagt» B og B «enhet_i» A kan begge være sanne — ingen sykelsjekk for R.
        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "underlagt",
            Kantnode.Virksomhet(v1), Kantnode.Virksomhet(v2), HjemmelRettskildeId: o.LovId), "Kari Jurist");
        var tilbake = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "enhet_i",
            Kantnode.Virksomhet(v2), Kantnode.Virksomhet(v1), HjemmelRettskildeId: o.LovId), "Kari Jurist");
        Assert.True(tilbake.VarNy);
    }

    // ---------------- Idempotens, status, sletting ----------------

    [Fact]
    public async Task Samme_utsagn_to_ganger_gir_samme_kant_men_annen_hjemmel_er_et_nytt_utsagn()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b) = (await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        var tjeneste = new StrukturkantTjeneste(db);
        NyStrukturkant Kant(Guid hjemmel) => new(Strukturkanter.Relasjon, "sekretariat_for",
            Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: hjemmel);

        var forste = await tjeneste.OpprettAsync(Kant(o.LovId), "Kari Jurist");
        var andre = await tjeneste.OpprettAsync(Kant(o.LovId), "Kari Jurist");
        var annenHjemmel = await tjeneste.OpprettAsync(Kant(o.ForskriftId), "Kari Jurist");

        Assert.False(andre.VarNy);
        Assert.Equal(forste.Kant.Id, andre.Kant.Id);
        Assert.True(annenHjemmel.VarNy);
    }

    [Fact]
    public async Task Forslag_krever_versjon_faar_ki_kilde_og_kan_godkjennes_eller_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b, c) = (await NyVirksomhetAsync(db), await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        var tjeneste = new StrukturkantTjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon,
            "radgir", Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId, Status: "foreslatt_av_ai"), "KI"));

        var forslag = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "radgir",
            Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId,
            Status: "foreslatt_av_ai", AiForslagVersjon: "OpenAiKompatibel:test"), "KI");
        Assert.Equal("ki:OpenAiKompatibel:test", forslag.Kant.OppdagelsesKilde);
        Assert.Contains(await tjeneste.HentForslagAsync(), v => v.Id == forslag.Kant.Id);
        var prov = await db.Proveniens.SingleAsync(p => p.EntitetId == forslag.Kant.Id);
        Assert.Equal(("foreslatt_av_ai", "OpenAiKompatibel:test"), (prov.Handling, prov.AiForslagVersjon));

        var godkjent = await tjeneste.GodkjennAsync(forslag.Kant.Id, "Kari Jurist");
        Assert.Equal("validert", godkjent!.Status);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.GodkjennAsync(forslag.Kant.Id, "Kari Jurist"));
        // En validert kant avvises ikke — den slettes.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.AvvisAsync(forslag.Kant.Id));

        var mønster = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "oppnevner",
            Kantnode.Virksomhet(a), Kantnode.Virksomhet(c), HjemmelRettskildeId: o.LovId,
            Status: "foreslatt_av_ai", OppdagelsesKilde: "monster:oppnevnes-av"), "mønsterlaget");
        Assert.Equal("monster:oppnevnes-av", mønster.Kant.OppdagelsesKilde);
        Assert.True(await tjeneste.AvvisAsync(mønster.Kant.Id));
        Assert.False(await db.Strukturkanter.AnyAsync(k => k.Id == mønster.Kant.Id));
    }

    [Fact]
    public async Task Validert_kant_kan_slettes_og_slettingen_logges()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b) = (await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        var tjeneste = new StrukturkantTjeneste(db);
        var kant = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "velger",
            Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId), "Kari Jurist");

        Assert.True(await tjeneste.SlettAsync(kant.Kant.Id, "Kari Jurist"));
        Assert.False(await tjeneste.SlettAsync(kant.Kant.Id, "Kari Jurist"));
        Assert.True(await db.Proveniens.AnyAsync(p => p.EntitetId == kant.Kant.Id && p.Handling == "slettet"));
    }

    // ---------------- Gyldighet (docs/29 §Del B, bevart fra myndighetstildeling) ----------------

    [Fact]
    public async Task Kun_gjeldende_utelater_kanter_med_opphevet_hjemmel_eller_utlopt_egen_periode()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var v = await NyVirksomhetAsync(db, "Vertskommune");
        var klasse1 = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "vertskommuner");
        var klasse2 = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "gamle-vertskommuner");
        var tjeneste = new StrukturkantTjeneste(db);
        var spenn = new[] { new ParagrafspennPar(o.ParagrafEid, null) };

        await tjeneste.OpprettTildelingAsync(v, klasse1, o.LovId, spenn, null, "Kari Jurist");
        await tjeneste.OpprettTildelingAsync(v, klasse2, o.LovId, spenn, null, "Kari Jurist",
            gyldigFra: new DateOnly(2020, 1, 1), gyldigTil: new DateOnly(2021, 1, 1));

        var alle = await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(v), Strukturkanter.Medlemskap);
        var gjeldende = await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(v), Strukturkanter.Medlemskap, kunGjeldende: true);
        Assert.Equal(2, alle.Count);
        Assert.Equal(klasse1, Assert.Single(gjeldende).Til!.Id);

        var kant = await db.Strukturkanter.FirstAsync(k => k.FraVirksomhetId == v && k.TilBegrepId == klasse1);
        Assert.True(StrukturkantTjeneste.ErGjeldende(kant, new RettskildeEntitet
        {
            Doctype = "act", Kildetype = "Lov", Tittel = "x", Status = "Gjeldende", OpprettetAv = "t",
        }, DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.False(StrukturkantTjeneste.ErGjeldende(kant, new RettskildeEntitet
        {
            Doctype = "act", Kildetype = "Lov", Tittel = "x", Status = "Opphevet", OpprettetAv = "t",
        }, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    // ---------------- Visning ----------------

    [Fact]
    public async Task Hjemlet_i_rettskilde_viser_bare_kanter_med_den_hjemmelen_og_bruker_navneformen()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b, c) = (await NyVirksomhetAsync(db, "KONKURRANSETILSYNET"), await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        var lesbart = Unik("Konkurransetilsynet");
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(a, lesbart, "Kari Jurist", null,
            navneformgrunn: VirksomhetVisningsnavnTjeneste.VisningsGrunn);
        var tjeneste = new StrukturkantTjeneste(db);

        var hjemlet = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "klageinstans",
            Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.ForskriftId), "Kari Jurist");
        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "sekretariat",
            Kantnode.Virksomhet(c), Kantnode.Virksomhet(b), KildeUtenforKorpusTekst: "org-kart",
            KildeUtenforKorpusType: Strukturkanter.NettsideAnnet, KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer), "Kari Jurist");

        var forForskriften = await tjeneste.HentForHjemmelRettskildeAsync(o.ForskriftId);
        var v = Assert.Single(forForskriften, x => x.Id == hjemlet.Kant.Id);
        Assert.Null(v.Retning);
        Assert.StartsWith($"{lesbart} har klageinstans hos ", v.Visningstekst);
        Assert.DoesNotContain(forForskriften, x => x.KildeUtenforKorpusTekst == "org-kart");
    }
}
