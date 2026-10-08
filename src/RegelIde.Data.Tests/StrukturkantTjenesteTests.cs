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
            Strukturkanter.Kompetanse, "klage", Kantnode.Virksomhet(fra), Kantnode.Virksomhet(til),
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
        // [ENDRET, issue #341] Klageinstans er K klage med motpart: «har klagekompetanse overfor B» / «A har … overfor denne».
        Assert.StartsWith("har klagekompetanse overfor Statsforvalteren", fraSiden.Visningstekst);
        Assert.StartsWith("Lokal nemnd", tilSiden.Visningstekst);
        Assert.EndsWith(" har klagekompetanse overfor denne", tilSiden.Visningstekst);
    }

    [Fact]
    public async Task K_kompetanse_uten_tilnode_lagres_med_objekt_og_avgrensning()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var dep = await NyVirksomhetAsync(db, "Departementet");
        var tjeneste = new StrukturkantTjeneste(db);

        var k = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Kompetanse, Strukturkanter.Normgivning, Kantnode.Virksomhet(dep), Til: null, Normform: "forskrift",
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Paragrafspenn: [new ParagrafspennPar(o.ParagrafEid, o.AnnenParagrafEid)],
            Objekt: "salgs- og skjenketider", Polaritet: "positiv"), "Kari Jurist");

        var v = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(dep), Strukturkanter.Kompetanse));
        Assert.Null(v.Til);
        // [ENDRET, issue #341] Forskriftskompetanse er normgivning med normform forskrift.
        Assert.Equal("har normgivningskompetanse (forskrift) salgs- og skjenketider", v.Visningstekst);
        Assert.Equal("forskrift", v.Normform);
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
        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Medlemskap, Strukturkanter.MedlemAv,
            Kantnode.Virksomhet(kommune), Kantnode.Begrep(klasse), HjemmelRettskildeId: o.ForskriftId, HjemmelEid: o.ForskriftEid), "Kari Jurist");
        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Medlemskap, Strukturkanter.MedlemAv,
            Kantnode.Begrep(klasse), Kantnode.Begrep(omrade), HjemmelRettskildeId: o.ForskriftId, HjemmelEid: o.ForskriftEid), "Kari Jurist");

        var forKlassen = await tjeneste.HentForNodeAsync(Kantnode.Begrep(klasse), Strukturkanter.Medlemskap);
        Assert.Equal(2, forKlassen.Count);
        Assert.Contains(forKlassen, v => v.Retning == "til" && v.Fra.Id == kommune && v.Visningstekst.StartsWith("har medlem "));
        Assert.Contains(forKlassen, v => v.Retning == "fra" && v.Til!.Id == omrade && v.Til.Nodetype == Nodetyper.Omrade);
    }

    [Fact]
    public async Task M_og_I_krever_hjemmelssted_naar_hjemmelen_er_i_korpus() // [ENDRET, #341] var «…krever_paragrafspenn…»
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var v = await NyVirksomhetAsync(db);
        var klasse = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "klasse-uten-spenn");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(
            Strukturkanter.Medlemskap, Strukturkanter.MedlemAv, Kantnode.Virksomhet(v), Kantnode.Begrep(klasse),
            HjemmelRettskildeId: o.LovId), "Kari Jurist"));
        Assert.Contains("hjemmel-eId", ex.Message);
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
        // [ENDRET, issue #341] Hjemmelsstedet er hjemmel-eId; avgrensningen er «hvilke paragrafer den gjelder for».
        var avgrensning = new[] { new ParagrafspennPar(o.AnnenParagrafEid, null) };

        var i = await tjeneste.OpprettTildelingAsync(nve, rolle, o.LovId, o.ParagrafEid, avgrensning, "bare konsesjoner etter § 3-1", "Kari Jurist");
        var m = await tjeneste.OpprettTildelingAsync(nve, klasse, o.LovId, o.ParagrafEid, null, null, "Kari Jurist");

        Assert.Equal((Strukturkanter.Rolleinnehav, Strukturkanter.Innehar), (i.Kant.Kategori, i.Kant.Typekode));
        Assert.Equal(o.ParagrafEid, i.Kant.HjemmelEid);
        Assert.Equal(o.AnnenParagrafEid, Assert.Single(StrukturkantTjeneste.LesParagrafspenn(i.Kant)).FraEid);
        Assert.Equal((o.ParagrafEid, "[]"), (m.Kant.HjemmelEid, m.Kant.AvgrensningParagrafspennJson));
        Assert.Equal("bare konsesjoner etter § 3-1", i.Kant.AvgrensningTekst);
        Assert.Equal((Strukturkanter.Medlemskap, Strukturkanter.MedlemAv), (m.Kant.Kategori, m.Kant.Typekode));

        // M til en rolle er feil kategori — rolleinnehav arves ikke (docs/33 §4.2), så det må være I.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Medlemskap, Strukturkanter.MedlemAv, Kantnode.Virksomhet(nve), Kantnode.Begrep(rolle),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid), "Kari Jurist"));
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
        NyStrukturkant Kant(string polaritet) => new(Strukturkanter.Kompetanse, "instruksjon",
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

        // [Ny, Johanns beslutning 2026-10-07] Dokumentasjonen (primær/sekundær) er påkrevd sammen med typen,
        // og må være en av de to verdiene.
        var utenDok = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "radgir", Kantnode.Virksomhet(hod), Kantnode.Virksomhet(rhf),
            KildeUtenforKorpusTekst: "styrevedtak 12/2025", KildeUtenforKorpusType: "styrevedtak"), "Kari Jurist"));
        Assert.Contains("sekundaer", utenDok.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "radgir", Kantnode.Virksomhet(hod), Kantnode.Virksomhet(rhf),
            KildeUtenforKorpusTekst: "styrevedtak 12/2025", KildeUtenforKorpusType: "styrevedtak",
            KildeUtenforKorpusDokumentasjon: "tertiaer"), "Kari Jurist"));

        var utenKilde = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "ledes_av", Kantnode.Virksomhet(rhf), Kantnode.Virksomhet(hod)), "Kari Jurist"));
        Assert.Contains("kilde utenfor korpus", utenKilde.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, "ledes_av", Kantnode.Virksomhet(rhf), Kantnode.Virksomhet(hod),
            KildeUtenforKorpusLenke: "https://example.org"), "Kari Jurist"));
    }

    /// <summary>
    /// Testtilfellene fra #311 (kommentarene 2026-10-07, med RETTELSEN): Tilsynsutvalget for dommere.
    /// <list type="bullet">
    /// <item>Sekretariatet: Domstoladministrasjonen <c>sekretariat_for</c> Tilsynsutvalget, forankret i Ot.prp. nr. 44
    /// (2000–2001) kap. 11.5.12 ⇒ <c>forarbeider</c> + <c>primaer</c> (ikke <c>nettside_annet</c>, som før kilden var funnet).</item>
    /// <item>Kongen i statsråd <c>oppnevner</c> Tilsynsutvalget, hjemlet i domstolloven (i korpus) — ingen kildetype. Selve
    /// oppnevningen av de første medlemmene ved kgl.res. 15. mai 2002 er en egen kant med <c>kgl_res</c> + <c>primaer</c>
    /// og den perioden oppnevningen gjaldt.</item>
    /// <item>INGEN <c>oppretter</c>-kant med kgl.res. som kilde — det ville gjentatt sekundærkildens (artikkelens) feil.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Tilsynsutvalget_sekretariat_i_forarbeider_og_oppnevning_hjemlet_i_loven()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db); // alkoholloven står for domstolloven: testen trenger bare EN lov i korpus.
        var (da, tilsyn, kongen) = (await NyVirksomhetAsync(db, "Domstoladministrasjonen"),
            await NyVirksomhetAsync(db, "Tilsynsutvalget for dommere"), await NyVirksomhetAsync(db, "Kongen i statsråd"));
        var tjeneste = new StrukturkantTjeneste(db);

        var sekretariat = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "sekretariat_for",
            Kantnode.Virksomhet(da), Kantnode.Virksomhet(tilsyn),
            KildeUtenforKorpusTekst: "Ot.prp. nr. 44 (2000–2001) kap. 11.5.12",
            KildeUtenforKorpusLenke: "https://www.regjeringen.no/no/dokumenter/otprp-nr-44-2000-2001-/id164074/?ch=11",
            KildeUtenforKorpusType: "forarbeider", KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer, Polaritet: "positiv"), "Kari Jurist");
        Assert.Equal(("forarbeider", "primaer"), (sekretariat.Kant.KildeUtenforKorpusType, sekretariat.Kant.KildeUtenforKorpusDokumentasjon));

        var oppnevnerEtterLoven = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "oppnevning",
            Kantnode.Virksomhet(kongen), Kantnode.Virksomhet(tilsyn), HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Polaritet: "positiv"), "Kari Jurist");
        Assert.Null(oppnevnerEtterLoven.Kant.KildeUtenforKorpusType);
        Assert.Null(oppnevnerEtterLoven.Kant.KildeUtenforKorpusDokumentasjon);

        var forsteOppnevning = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "oppnevning",
            Kantnode.Virksomhet(kongen), Kantnode.Virksomhet(tilsyn),
            KildeUtenforKorpusTekst: "Kgl.res. 15. mai 2002 (Offisielt fra statsråd) — de første medlemmene og lederen",
            KildeUtenforKorpusLenke: "https://www.regjeringen.no/no/aktuelt/offisielt-fra-statsrad-15-mai-2002-/id101763/",
            KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer,
            GyldigFra: new DateOnly(2002, 11, 1), GyldigTil: new DateOnly(2006, 10, 31), Polaritet: "positiv"), "Kari Jurist");
        Assert.True(forsteOppnevning.VarNy); // ulik kilde ⇒ eget utsagn ved siden av den lovhjemlede kanten.

        var forUtvalget = await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(tilsyn));
        Assert.DoesNotContain(forUtvalget, k => k.Typekode == "oppretter");
        Assert.Equal(2, forUtvalget.Count(k => k.Typekode == "oppnevning"));
        // Forarbeidene er en anerkjent rettskildetype — kanten havner IKKE på nettside-arbeidslista.
        Assert.DoesNotContain(await tjeneste.HentUtenKorpusforankringAsync(Strukturkanter.NettsideAnnet), k => k.Id == sekretariat.Kant.Id);
        Assert.Contains(await tjeneste.HentUtenKorpusforankringAsync("forarbeider"), k => k.Id == sekretariat.Kant.Id);
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
        var instruks = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "instruksjon",
            Kantnode.Virksomhet(c), Kantnode.Virksomhet(b), KildeUtenforKorpusTekst: "instruks for nemnda",
            KildeUtenforKorpusType: "instruks", KildeUtenforKorpusDokumentasjon: Strukturkanter.Sekundaer), "Kari Jurist");
        var hjemlet = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "klage",
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
            Id = Guid.NewGuid(), Kategori = "R", Typekode = "administrativt_underordnet", FraVirksomhetId = a, TilVirksomhetId = b, OpprettetAv = "test",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var begrep = await db.Begreper.Select(x => x.Id).FirstAsync();
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = "R", Typekode = "administrativt_underordnet", FraVirksomhetId = a, FraBegrepId = begrep,
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
            Strukturkanter.Relasjon, Strukturkanter.HarDelegertTil, Kantnode.Virksomhet(a), Kantnode.Virksomhet(b),
            HjemmelRettskildeId: o.LovId, Paragrafspenn: [new ParagrafspennPar("finnes/ikke", null)]), "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, Strukturkanter.HarDelegertTil, Kantnode.Virksomhet(a), Kantnode.Virksomhet(b),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ForskriftEid), "Kari Jurist")); // eId fra en ANNEN rettskilde
    }

    [Fact]
    public async Task Typekoden_maa_finnes_aktiv_for_kategorien()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b) = (await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        var tjeneste = new StrukturkantTjeneste(db);
        // 'normgivning' er en K-type, ikke en R-type. [ENDRET, #341: var 'forskrift', som ikke finnes lenger.]
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, Strukturkanter.Normgivning, Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId), "Kari Jurist"));

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
            Strukturkanter.Relasjon, "administrativt_underordnet", Kantnode.Virksomhet(a), Kantnode.Virksomhet(a), HjemmelRettskildeId: o.LovId), "Kari Jurist"));
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
        Task<StrukturkantOpprettet> Medlem(Guid fra, Guid til) => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Medlemskap, Strukturkanter.MedlemAv, Kantnode.Begrep(fra), Kantnode.Begrep(til),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid), "Kari Jurist");

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
        // [ENDRET, #330] Kodene er nå administrativt_underordnet og rapporterer_til (enhet_i ble G del_av).
        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "administrativt_underordnet",
            Kantnode.Virksomhet(v1), Kantnode.Virksomhet(v2), HjemmelRettskildeId: o.LovId), "Kari Jurist");
        var tilbake = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "rapporterer_til",
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

        var mønster = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "oppnevning",
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
        // [ENDRET, issue #352] var R velger — den er nå K oppnevning med undertype valg; radgir er fortsatt R.
        var kant = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "radgir",
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
        await tjeneste.OpprettTildelingAsync(v, klasse1, o.LovId, o.ParagrafEid, null, null, "Kari Jurist");
        await tjeneste.OpprettTildelingAsync(v, klasse2, o.LovId, o.ParagrafEid, null, null, "Kari Jurist",
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

        var hjemlet = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "klage",
            Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.ForskriftId), "Kari Jurist");
        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "sekretariat_for",
            Kantnode.Virksomhet(c), Kantnode.Virksomhet(b), KildeUtenforKorpusTekst: "org-kart",
            KildeUtenforKorpusType: Strukturkanter.NettsideAnnet, KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer), "Kari Jurist");

        var forForskriften = await tjeneste.HentForHjemmelRettskildeAsync(o.ForskriftId);
        var v = Assert.Single(forForskriften, x => x.Id == hjemlet.Kant.Id);
        Assert.Null(v.Retning);
        Assert.StartsWith($"{lesbart} har klagekompetanse overfor ", v.Visningstekst);
        Assert.DoesNotContain(forForskriften, x => x.KildeUtenforKorpusTekst == "org-kart");
    }

    // ---------------- Issue #330: gamle koder borte, avgrensning kan oppdateres ----------------

    /// <summary>[Ny, issue #330 AC2] Seeden legger ikke de gamle kodene inn igjen, og tjenesten avviser dem —
    /// mens den nye koden i samme betydning virker.</summary>
    [Fact]
    public async Task De_gamle_relasjonskodene_finnes_ikke_og_kan_ikke_brukes()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db); // seeder Startsett
        var (a, b) = (await NyVirksomhetAsync(db), await NyVirksomhetAsync(db));
        var tjeneste = new StrukturkantTjeneste(db);

        Assert.DoesNotContain(Strukturkanter.Startsett, s => RelasjonskodeHarmonisering.GamleKoder.Contains(s.Kode));
        Assert.False(await db.RelasjonsTypeKonfigurasjoner.AnyAsync(t => RelasjonskodeHarmonisering.GamleKoder.Contains(t.Kode)));
        foreach (var (gammel, nyKategori, nyKode, _) in RelasjonskodeHarmonisering.Mapping)
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
                Strukturkanter.Relasjon, gammel, Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId), "Kari Jurist"));
            Assert.Contains($"Ukjent typekode '{gammel}'", ex.Message);
            // Målkoden finnes og er aktiv i riktig kategori — med mindre #341 har flyttet den videre (klageinstans_for → K klage).
            if (KompetanseMigrering.FjernedeKoder.Contains((nyKategori, nyKode))) continue;
            Assert.True(await db.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == nyKategori && t.Kode == nyKode && t.Aktiv));
        }
    }

    /// <summary>[Ny, issue #330] <see cref="StrukturkantTjeneste.OppdaterAvgrensningAsync"/>: setter spenn + tekst
    /// på samme kant, logger før/etter, er en no-op ved samme verdi, og validerer som opprettelsen.</summary>
    [Fact]
    public async Task Avgrensning_oppdateres_paa_samme_kant_med_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (dep, nemnd) = (await NyVirksomhetAsync(db, "Departementet"), await NyVirksomhetAsync(db, "Klagenemnda"));
        var tjeneste = new StrukturkantTjeneste(db);
        var kant = (await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "klage",
            Kantnode.Virksomhet(dep), Kantnode.Virksomhet(nemnd), HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid), "Kari Jurist")).Kant;

        var oppdatert = await tjeneste.OppdaterAvgrensningAsync(kant.Id, [new ParagrafspennPar(o.ParagrafEid, null)],
            " enkeltvedtak nemnda treffer i første instans ", "Johann");
        Assert.NotNull(oppdatert);
        db.ChangeTracker.Clear();
        var lagret = await db.Strukturkanter.SingleAsync(k => k.Id == kant.Id);
        Assert.Equal("enkeltvedtak nemnda treffer i første instans", lagret.AvgrensningTekst);
        Assert.Equal(o.ParagrafEid, Assert.Single(StrukturkantTjeneste.LesParagrafspenn(lagret)).FraEid);
        Assert.Equal(("klage", dep, nemnd), (lagret.Typekode, lagret.FraVirksomhetId!.Value, lagret.TilVirksomhetId!.Value));
        Assert.Equal("Johann", lagret.SistEndretAv);
        var prov = await db.Proveniens.SingleAsync(p => p.EntitetId == kant.Id && p.Handling == "endret");
        using (var refs = System.Text.Json.JsonDocument.Parse(prov.KildeReferanserJson!))
        {
            Assert.Equal(0, refs.RootElement.GetProperty("forParagrafspenn").GetArrayLength());
            Assert.Equal("enkeltvedtak nemnda treffer i første instans", refs.RootElement.GetProperty("nyAvgrensningTekst").GetString());
        }

        // Samme verdi en gang til: ingen ny proveniensrad.
        await tjeneste.OppdaterAvgrensningAsync(kant.Id, [new ParagrafspennPar(o.ParagrafEid, null)],
            "enkeltvedtak nemnda treffer i første instans", "Johann");
        Assert.Equal(1, await db.Proveniens.CountAsync(p => p.EntitetId == kant.Id && p.Handling == "endret"));

        // Ukjent eId avvises; ukjent kant gir null.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OppdaterAvgrensningAsync(kant.Id,
            [new ParagrafspennPar("finnes/ikke", null)], null, "Johann"));
        Assert.Null(await tjeneste.OppdaterAvgrensningAsync(Guid.NewGuid(), [], null, "Johann"));

        // En endring som ville gjort kanten identisk med en annen, avvises (idempotensnøkkelen).
        var tvilling = (await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "klage",
            Kantnode.Virksomhet(dep), Kantnode.Virksomhet(nemnd), HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Paragrafspenn: [new ParagrafspennPar(o.AnnenParagrafEid, null)]), "Kari Jurist")).Kant;
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OppdaterAvgrensningAsync(tvilling.Id,
            [new ParagrafspennPar(o.ParagrafEid, null)], null, "Johann"));
    }

    /// <summary>[ENDRET, issue #341] Het «Avgrensning_kan_ikke_fjerne_paakrevd_spenn_paa_medlemskap». Kravet på M/I gjelder nå
    /// HVOR det står (hjemmel-eId), ikke avgrensningen — så avgrensningen kan tømmes, og hjemmelsstedet står.</summary>
    [Fact]
    public async Task Avgrensning_kan_tommes_paa_medlemskap_fordi_hjemmelsstedet_er_hjemmel_eid()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var v = await NyVirksomhetAsync(db);
        var klasse = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "klasse");
        var tjeneste = new StrukturkantTjeneste(db);
        var m = await tjeneste.OpprettTildelingAsync(v, klasse, o.LovId, o.ParagrafEid, [new ParagrafspennPar(o.AnnenParagrafEid, null)], null, "Kari Jurist");
        var etter = await tjeneste.OppdaterAvgrensningAsync(m.Kant.Id, [], null, "Johann");
        Assert.Equal(("[]", o.ParagrafEid), (etter!.AvgrensningParagrafspennJson, etter.HjemmelEid));
    }

    [Fact]
    public async Task M_og_I_med_hjemmel_krever_hjemmelssted_ikke_avgrensning()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var v = await NyVirksomhetAsync(db);
        var klasse = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "klasse");
        var tjeneste = new StrukturkantTjeneste(db);
        // Bare et avgrensningsspenn (slik #311 lagret hjemmelsstedet) holder ikke lenger.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Medlemskap, Strukturkanter.MedlemAv, Kantnode.Virksomhet(v), Kantnode.Begrep(klasse),
            HjemmelRettskildeId: o.LovId, Paragrafspenn: [new ParagrafspennPar(o.ParagrafEid, null)]), "Kari Jurist"));
        Assert.Contains("hjemmel-eId", ex.Message);
    }

    // ---------------- Issue #341: kompetanse med motpart og typologi ----------------

    /// <summary>[Ny, issue #341 AC1] R tar ikke lenger myndighetstyper: de flyttede kodene finnes ikke i startsettet eller i
    /// konfigurasjonen og avvises — mens K-typen med samme betydning tar motparten som til-node.</summary>
    [Fact]
    public async Task R_tar_ikke_lenger_myndighetstyper_de_er_K_med_motpart()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b) = (await NyVirksomhetAsync(db, "Klagenemnda"), await NyVirksomhetAsync(db, "Direktoratet"));
        var tjeneste = new StrukturkantTjeneste(db);

        foreach (var kode in new[] { "klageinstans_for", "instruksjon", "omgjoring", "oppnevner", "delegerer_til" })
        {
            Assert.DoesNotContain(Strukturkanter.Startsett, t => t.Kategori == Strukturkanter.Relasjon && t.Kode == kode);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
                Strukturkanter.Relasjon, kode, Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId), "Kari Jurist"));
            Assert.Contains("Ukjent typekode", ex.Message);
        }
        // R er struktur uten myndighet (P1) + den gjennomførte delegeringen.
        foreach (var kode in new[] { "eies_av", "ledes_av", "sekretariat_for", "rapporterer_til", "etterfolger", "representerer", Strukturkanter.HarDelegertTil })
        {
            Assert.Contains(Strukturkanter.Startsett, t => t.Kategori == Strukturkanter.Relasjon && t.Kode == kode);
        }

        var klage = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "klage",
            Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Objekt: "enkeltvedtak i første instans"), "Kari Jurist");
        var v = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(a), Strukturkanter.Kompetanse));
        Assert.Equal(klage.Kant.Id, v.Id);
        Assert.StartsWith("har klagekompetanse overfor Direktoratet", v.Visningstekst);
        Assert.EndsWith("— enkeltvedtak i første instans", v.Visningstekst);
        Assert.Equal("klage_overproving", v.Familie);
    }

    /// <summary>[Ny, issue #341] Normform bare på normgivning (lukket liste), grunnlag og delegerbar bare på K — og ingen
    /// standardverdi.</summary>
    [Fact]
    public async Task Normform_grunnlag_og_delegerbar_valideres_og_lagres()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (kongen, morselskap, nett) = (await NyVirksomhetAsync(db, "Kongen i statsråd"), await NyVirksomhetAsync(db, "Morselskapet"),
            await NyVirksomhetAsync(db, "Nettforetaket"));
        var tjeneste = new StrukturkantTjeneste(db);

        var forskrift = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Normgivning,
            Kantnode.Virksomhet(kongen), null, HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Objekt: "skjenketider",
            Normform: "forskrift", Delegerbar: false), "Kari Jurist");
        Assert.Equal(("forskrift", (bool?)false), (forskrift.Kant.Normform, forskrift.Kant.Delegerbar));
        var v = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(kongen)));
        Assert.Equal(("normgivning", "forskrift"), (v.Familie, v.FvlKategori)); // fvl-kategorien følger normformen

        var privat = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "instruksjon",
            Kantnode.Virksomhet(morselskap), Kantnode.Virksomhet(nett), HjemmelRettskildeId: o.LovId, Polaritet: "negativ",
            Grunnlag: "privatrettslig"), "Kari Jurist");
        Assert.Equal("privatrettslig", privat.Kant.Grunnlag);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "vedtak",
            Kantnode.Virksomhet(kongen), null, HjemmelRettskildeId: o.LovId, Objekt: "x", Normform: "forskrift"), "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Normgivning,
            Kantnode.Virksomhet(kongen), null, HjemmelRettskildeId: o.LovId, Objekt: "x", Normform: "rundskriv"), "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "instruksjon",
            Kantnode.Virksomhet(morselskap), Kantnode.Virksomhet(nett), HjemmelRettskildeId: o.LovId, Grunnlag: "kontrakt"), "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "eies_av",
            Kantnode.Virksomhet(nett), Kantnode.Virksomhet(morselskap), HjemmelRettskildeId: o.LovId, Grunnlag: "privatrettslig"), "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "eies_av",
            Kantnode.Virksomhet(nett), Kantnode.Virksomhet(morselskap), HjemmelRettskildeId: o.LovId, Delegerbar: true), "Kari Jurist"));

        // Samme utsagn med motsatt delegerbar er en motsigelse, ikke et nytt utsagn — og slås ikke stille sammen.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Normgivning,
            Kantnode.Virksomhet(kongen), null, HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Objekt: "skjenketider",
            Normform: "forskrift", Delegerbar: true), "Kari Jurist"));
        // Uten delegerbar: samme kant returneres.
        Assert.False((await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Normgivning,
            Kantnode.Virksomhet(kongen), null, HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Objekt: "skjenketider",
            Normform: "forskrift"), "Kari Jurist")).VarNy);
    }

    /// <summary>[Ny, issue #352, Johanns beslutning 1] S9 «hvem kan velge medlemmene av forliksrådet?»: oppnevningskompetanse
    /// med undertype valg, med motpart. Undertypen er en lukket liste per type, bare på K, del av utsagnets identitet og vises
    /// i parentes; ansettelse gir fvl-kategori enkeltvedtak.</summary>
    [Fact]
    public async Task Undertype_paa_oppnevning_valideres_lagres_og_vises()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (kommunestyret, forliksradet, styret) = (await NyVirksomhetAsync(db, "Kommunestyret"), await NyVirksomhetAsync(db, "Forliksrådet"),
            await NyVirksomhetAsync(db, "Styret"));
        var tjeneste = new StrukturkantTjeneste(db);

        var valg = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Oppnevning,
            Kantnode.Virksomhet(kommunestyret), Kantnode.Virksomhet(forliksradet), HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Undertype: "valg"), "Kari Jurist");
        Assert.Equal("valg", valg.Kant.Undertype);
        var v = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(kommunestyret)));
        Assert.Equal(("valg", "oppnevning", (string?)null), (v.Undertype, v.Familie, v.FvlKategori));
        Assert.StartsWith("har oppnevningskompetanse (valg) overfor Forliksrådet", v.Visningstekst); // navnet har et unikt suffiks

        // Samme motpart, annet verb: et annet utsagn (undertypen er identitet), og ansettelse er enkeltvedtak.
        var ansettelse = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Oppnevning,
            Kantnode.Virksomhet(kommunestyret), Kantnode.Virksomhet(forliksradet), HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Undertype: "ansettelse"), "Kari Jurist");
        Assert.True(ansettelse.VarNy);
        Assert.Equal("enkeltvedtak", (await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(kommunestyret)))
            .Single(k => k.Undertype == "ansettelse").FvlKategori);

        // Anke under overprøving.
        Assert.Equal("anke", (await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Overproving,
            Kantnode.Virksomhet(styret), Kantnode.Virksomhet(forliksradet), HjemmelRettskildeId: o.LovId, Undertype: "anke"), "Kari Jurist")).Kant.Undertype);

        // Feil undertype for typen, ukjent undertype, undertype på en type uten undertyper og på R: avvist, ingen gjetning.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Oppnevning,
            Kantnode.Virksomhet(kommunestyret), Kantnode.Virksomhet(styret), HjemmelRettskildeId: o.LovId, Undertype: "anke"), "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Oppnevning,
            Kantnode.Virksomhet(kommunestyret), Kantnode.Virksomhet(styret), HjemmelRettskildeId: o.LovId, Undertype: "utnevning"), "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "klage",
            Kantnode.Virksomhet(kommunestyret), Kantnode.Virksomhet(styret), HjemmelRettskildeId: o.LovId, Undertype: "valg"), "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "radgir",
            Kantnode.Virksomhet(kommunestyret), Kantnode.Virksomhet(styret), HjemmelRettskildeId: o.LovId, Undertype: "valg"), "Kari Jurist"));

        // Databasen holder samme grense som tjenesten (ck_strukturkanter_undertype).
        db.ChangeTracker.Clear();
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = "K", Typekode = "klage", FraVirksomhetId = kommunestyret, TilVirksomhetId = styret,
            HjemmelRettskildeId = o.LovId, Undertype = "valg", OpprettetAv = "test",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>[Ny, issue #341, Johanns beslutning P2] Selvregulering er normgivning der motparten er innehaveren selv — den
    /// eneste selvkanten som er lov, i tjenesten og i databasen.</summary>
    [Fact]
    public async Task Selvregulering_er_normgivning_overfor_seg_selv_og_ingen_andre_selvkanter_er_lov()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var sameting = await NyVirksomhetAsync(db, "Sametinget");
        var tjeneste = new StrukturkantTjeneste(db);

        var selv = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Normgivning,
            Kantnode.Virksomhet(sameting), Kantnode.Virksomhet(sameting), HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Normform: "arbeidsordning"), "Kari Jurist");
        Assert.True(Strukturkanter.ErSelvregulering(selv.Kant));
        var v = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(sameting)));
        Assert.True(v.Selvregulering);
        Assert.Equal("har normgivningskompetanse (arbeidsordning) overfor seg selv (selvregulering)", v.Visningstekst);
        Assert.Null(v.FvlKategori); // en arbeidsordning er ikke automatisk en forskrift

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "klage",
            Kantnode.Virksomhet(sameting), Kantnode.Virksomhet(sameting), HjemmelRettskildeId: o.LovId), "Kari Jurist"));
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = "K", Typekode = "klage", FraVirksomhetId = sameting, TilVirksomhetId = sameting,
            HjemmelRettskildeId = o.LovId, OpprettetAv = "test",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); // ck_strukturkanter_ikke_selv
    }

    /// <summary>[Ny, issue #341, Johanns hierarkibeslutning] Hver K-type har familien sin i konfigurasjonen (beslutning står
    /// øverst uten familie; forelegging er ikke plassert), og kantene kan filtreres på familie.</summary>
    [Fact]
    public async Task Kompetansetypene_har_familie_og_kan_filtreres_paa_den()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        foreach (var t in Strukturkanter.Kompetansetyper)
        {
            var rad = await db.RelasjonsTypeKonfigurasjoner.SingleAsync(k => k.Kategori == "K" && k.Kode == t.Kode);
            Assert.Equal(t.Familie, rad.Familie);
            Assert.Equal(t.FvlKategori, rad.FvlKategori);
            // [ENDRET, issue #352] Bare beslutning står uten familie: forelegging er kontroll (Johanns beslutning 3).
            Assert.Equal(t.Kode is Strukturkanter.Beslutning, rad.Familie is null);
        }
        Assert.Equal("kontroll", (await db.RelasjonsTypeKonfigurasjoner.SingleAsync(k => k.Kategori == "K" && k.Kode == "forelegging")).Familie);
        Assert.False(await db.RelasjonsTypeKonfigurasjoner.AnyAsync(k => k.Familie == "personell")); // [Ny, #352] heter oppnevning
        Assert.Equal("normgivning", Strukturkanter.KompetansetypeFraFasit["normgivningskompetanse"]);
        Assert.Equal("enkeltvedtak", Strukturkanter.FvlKategoriFor("vedtak", null, null, "enkeltvedtak"));
        Assert.Equal("forskrift", Strukturkanter.FvlKategoriFor(Strukturkanter.Normgivning, "forskrift", null, null));
        Assert.Null(Strukturkanter.FvlKategoriFor(Strukturkanter.Normgivning, "instruks", null, null));
        // [Ny, issue #352] Ansettelse var enkeltvedtak som egen type; som undertype av oppnevning er den det fortsatt.
        Assert.Equal("enkeltvedtak", Strukturkanter.FvlKategoriFor(Strukturkanter.Oppnevning, null, "ansettelse", null));
        Assert.Null(Strukturkanter.FvlKategoriFor(Strukturkanter.Oppnevning, null, "valg", null));

        var (dep, dir) = (await NyVirksomhetAsync(db, "Departementet"), await NyVirksomhetAsync(db, "Direktoratet"));
        var tjeneste = new StrukturkantTjeneste(db);
        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "instruksjon", Kantnode.Virksomhet(dep),
            Kantnode.Virksomhet(dir), HjemmelRettskildeId: o.LovId), "Kari Jurist");
        await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "tilsyn", Kantnode.Virksomhet(dep),
            Kantnode.Virksomhet(dir), HjemmelRettskildeId: o.LovId), "Kari Jurist");
        var styring = await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(dep), familie: "styring");
        Assert.Equal("instruksjon", Assert.Single(styring).Typekode);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.HentForNodeAsync(Kantnode.Virksomhet(dep), familie: "alt"));
    }
}
