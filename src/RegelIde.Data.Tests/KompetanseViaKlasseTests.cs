using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #418, Johanns avgjørelser (a) og (b) 2026-10-29] Kompetanse med klasse som innehaver, og rolle med klasse som innehaver.
/// Spørsmålene: godtar skriveveien klasse som fra i K og I (og fortsatt ikke et område)? Hvilken kompetanse har en kommune fordi den er medlem
/// av klassen «kommune», eller fordi klassen har en rolle? Kan svaret vises som sterkere enn medlemskapet, rolleinnehavet eller K-kanten?
/// Kan en rolle opprettes som forslag med tekstfunn-hjemmel?
/// <para>Testbasen er delt: navn og koder er unike per test, og påstandene gjelder bare testens egne rader.</para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class KompetanseViaKlasseTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KompetanseViaKlasseTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Unik(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    private async Task<(RegelIdeDbContext Db, Guid Lov, string Eid)> OppsettAsync()
    {
        var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var eid = (await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync()).Eid;
        return (db, lovId, eid);
    }

    private static async Task<Guid> BegrepAsync(RegelIdeDbContext db, string kategori, string term, string? omradetype = null, string? kode = null)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = kategori, Term = Unik(term), Omradetype = omradetype, Omradekode = kode,
            Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        await db.SaveChangesAsync();
        return b.Id;
    }

    private static async Task<Guid> AktorAsync(RegelIdeDbContext db, string navn, string? kommunenummer = null)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik(navn), Aktortype = "rettssubjekt", Kommunenummer = kommunenummer };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    [Fact]
    public async Task K_og_I_godtar_klasse_som_innehaver_men_ikke_et_omrade()
    {
        var (db, lov, eid) = await OppsettAsync();
        await using var _ = db;
        var klasse = await BegrepAsync(db, Nodetyper.Klasse, "kommune");
        var rolle = await BegrepAsync(db, Nodetyper.Rolle, "lokal barnehagemyndighet");
        var omrade = await BegrepAsync(db, Nodetyper.Omrade, "Testområde");
        var tjeneste = new StrukturkantTjeneste(db);

        // barnehageloven § 20 første ledd: «Kommunen kan gi lokal forskrift …» — K fra klassen, lest distributivt.
        var k = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Normgivning, Kantnode.Begrep(klasse), null,
            HjemmelRettskildeId: lov, HjemmelEid: eid, Normform: "forskrift", Objekt: "lokal forskrift"), "test");
        Assert.True(k.VarNy);
        // barnehageloven § 10 første ledd: «Kommunen er lokal barnehagemyndighet» — I fra klassen.
        var i = await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Rolleinnehav, Strukturkanter.Innehar, Kantnode.Begrep(klasse),
            Kantnode.Begrep(rolle), HjemmelRettskildeId: lov, HjemmelEid: eid), "test");
        Assert.True(i.VarNy);

        var feil = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse,
            "tilsyn", Kantnode.Begrep(omrade), null, HjemmelRettskildeId: lov, HjemmelEid: eid, Objekt: "x"), "test"));
        Assert.Contains("aktør, rolle eller klasse", feil.Message);
        feil = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Rolleinnehav,
            Strukturkanter.Innehar, Kantnode.Begrep(omrade), Kantnode.Begrep(rolle), HjemmelRettskildeId: lov, HjemmelEid: eid), "test"));
        Assert.Contains("aktør eller klasse", feil.Message);
    }

    [Fact]
    public async Task Medlem_av_klassen_har_kompetansen_avledet_og_aldri_sterkere_enn_premissene()
    {
        var (db, lov, eid) = await OppsettAsync();
        await using var _ = db;
        var tjeneste = new StrukturkantTjeneste(db);
        var klasse = await BegrepAsync(db, Nodetyper.Klasse, "barnehagekommune");
        var rolle = await BegrepAsync(db, Nodetyper.Rolle, "lokal barnehagemyndighet");
        var v = await AktorAsync(db, "Testby kommune");
        var tilsynsorgan = await AktorAsync(db, "Statsforvalteren i Testfylke");

        async Task<Guid> Kant(string kat, string type, Kantnode fra, Kantnode? til, string? ks, string status = Strukturkanter.Godkjent,
            string? objekt = null, string? normform = null) =>
            (await tjeneste.OpprettAsync(new NyStrukturkant(kat, type, fra, til, HjemmelRettskildeId: lov, HjemmelEid: eid, Objekt: objekt,
                Normform: normform, Kunnskapsstatus: ks, Status: status, OppdagelsesKilde: "monster:test"), "test")).Kant.Id;

        var medlem = await Kant(Strukturkanter.Medlemskap, "medlem_av", Kantnode.Virksomhet(v), Kantnode.Begrep(klasse), Strukturkanter.Tekstfunn);
        var normgivning = await Kant(Strukturkanter.Kompetanse, Strukturkanter.Normgivning, Kantnode.Begrep(klasse), null,
            Strukturkanter.Normalisering, Strukturkanter.MaskineltForeslatt, "lokal forskrift", "forskrift");
        var godkjenning = await Kant(Strukturkanter.Kompetanse, "godkjenning", Kantnode.Begrep(klasse), null, null,
            Strukturkanter.MaskineltForeslatt, "barnehager");
        var innehar = await Kant(Strukturkanter.Rolleinnehav, Strukturkanter.Innehar, Kantnode.Begrep(klasse), Kantnode.Begrep(rolle),
            Strukturkanter.Normalisering, Strukturkanter.MaskineltForeslatt);
        var politiattest = await Kant(Strukturkanter.Kompetanse, "vedtak", Kantnode.Begrep(rolle), null, Strukturkanter.Tekstfunn,
            objekt: "kreve politiattest");
        var tilsyn = await Kant(Strukturkanter.Kompetanse, "tilsyn", Kantnode.Virksomhet(tilsynsorgan), Kantnode.Begrep(klasse),
            Strukturkanter.Uavklart);

        var svar = await new KompetanseViaKlasseTjeneste(db, tjeneste).ForVirksomhetAsync(v);
        Assert.NotNull(svar);
        KompetanseViaKlasseTreff T(Guid kant, string retning = "har") => svar.Treff.Single(t => t.Kant.Id == kant && t.Retning == retning);

        // Medlemskap (tekstfunn) + K (normalisering) → avledet; forslag fordi K-kanten ikke er godkjent.
        var a = T(normgivning);
        Assert.Equal((Strukturkanter.Avledet, true), (a.Kunnskapsstatus, a.Forslag));
        Assert.Equal([medlem, normgivning], a.Premisser);
        Assert.Equal([klasse], a.Via.Select(n => n.Id));
        // K uten vurdert kunnskapsstatus → uavklart, og den er det svakeste premisset.
        var b = T(godkjenning);
        Assert.Equal((Strukturkanter.Uavklart, godkjenning, (string?)null), (b.Kunnskapsstatus, b.SvakestePremissId, b.SvakestePremissStatus));
        // Via rollen klassen har: medlemskap → rolleinnehav → K. Rollen er i veien, og forslaget i rolleinnehavet smitter.
        var c = T(politiattest);
        Assert.Equal([medlem, innehar, politiattest], c.Premisser);
        Assert.Equal([klasse, rolle], c.Via.Select(n => n.Id));
        Assert.Equal((Strukturkanter.Avledet, true), (c.Kunnskapsstatus, c.Forslag));
        // Motsatt retning: tilsyn overfor klassen gjelder medlemmet, uavklart fordi K-kanten er det.
        Assert.Equal(Strukturkanter.Uavklart, T(tilsyn, "overfor").Kunnskapsstatus);
        // En aktør som ikke er medlem, får ingen av treffene.
        var annen = await new KompetanseViaKlasseTjeneste(db, tjeneste).ForVirksomhetAsync(tilsynsorgan);
        Assert.DoesNotContain(annen!.Treff, t => t.Kant.Id == normgivning);
        // Premissene er med i svaret, én gang hver.
        Assert.Equal(svar.Premisser.Select(p => p.Id).Distinct().Count(), svar.Premisser.Count);
        Assert.Contains(svar.Premisser, p => p.Id == innehar);
    }

    [Fact]
    public async Task Kommunen_har_kompetansen_via_utledet_medlemskap_med_territoriekanten_som_premiss()
    {
        var (db, lov, eid) = await OppsettAsync();
        await using var _ = db;
        var tjeneste = new StrukturkantTjeneste(db);
        var kommuneklasse = await new KlassemedlemskapTjeneste(db, tjeneste).KlasseForRegelAsync(Klasseregler.OmraderegisterKommune);
        Assert.NotNull(kommuneklasse); // migrasjonen KlasseKommuneOgUtledetMedlemskap
        var kode = "T" + Guid.NewGuid().ToString("N")[..7];
        var omrade = await BegrepAsync(db, Nodetyper.Omrade, $"Testby{kode}", Omradetyper.Kommune, kode);
        var v = await AktorAsync(db, $"Testby{kode} kommune", kode);
        var territorie = (await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(v),
            Kantnode.Begrep(omrade), KildeUtenforKorpusTekst: "Testkilde (register)", KildeUtenforKorpusType: Strukturkanter.Register,
            KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer, Kunnskapsstatus: Strukturkanter.EksterntDokumentert), "test")).Kant.Id;
        var k = (await tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "vedtak", Kantnode.Begrep(kommuneklasse.Id), null,
            HjemmelRettskildeId: lov, HjemmelEid: eid, Objekt: Unik("utdanningskravet"), Kunnskapsstatus: Strukturkanter.Normalisering,
            Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: "monster:test"), "test")).Kant.Id;

        var svar = await new KompetanseViaKlasseTjeneste(db, tjeneste).ForVirksomhetAsync(v);

        var t = Assert.Single(svar!.Treff, x => x.Kant.Id == k);
        Assert.Equal([territorie, k], t.Premisser);
        Assert.Equal([kommuneklasse.Id], t.Via.Select(n => n.Id));
        Assert.Equal(Strukturkanter.Avledet, t.Kunnskapsstatus);
    }

    [Fact]
    public async Task Rolle_opprettes_som_forslag_med_tekstfunn_hjemmel_og_kan_ikke_opprettes_godkjent()
    {
        var (db, lov, eid) = await OppsettAsync();
        await using var _ = db;
        var register = new VirksomhetsbegrepTjeneste(db);
        var term = Unik("lokal barnehagemyndighet");

        var rolle = await register.OpprettGruppebegrepAsync(Nodetyper.Rolle, lov, term, "test", eid, utsagn:
            new VirksomhetsbegrepTjeneste.Begrepsutsagn(Strukturkanter.Tekstfunn, "Står ordrett.", Strukturkanter.MaskineltForeslatt, "issue-418"));
        Assert.Equal((Strukturkanter.Tekstfunn, Strukturkanter.MaskineltForeslatt, eid, "issue-418"),
            (rolle.Kunnskapsstatus, rolle.Godkjenning, rolle.LovreferanseEid, rolle.OppdagelsesKilde));

        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettGruppebegrepAsync(Nodetyper.Rolle, lov, Unik("rolle"), "test", eid,
            utsagn: new VirksomhetsbegrepTjeneste.Begrepsutsagn(Strukturkanter.Tekstfunn, null, Strukturkanter.Godkjent, null)));
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettGruppebegrepAsync(Nodetyper.Rolle, lov, Unik("rolle"), "test", null,
            utsagn: new VirksomhetsbegrepTjeneste.Begrepsutsagn(Strukturkanter.Tekstfunn, null, null, null)));
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettGruppebegrepAsync(Nodetyper.Rolle, lov, Unik("rolle"), "test",
            eid + "-finnes-ikke", utsagn: new VirksomhetsbegrepTjeneste.Begrepsutsagn(Strukturkanter.Tekstfunn, null, null, null)));
    }
}
