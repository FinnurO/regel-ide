using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Tekstmerking → tagging (§1.2 i domenemodellen, AK-3.3.1–3.3.4), mot ekte embedded Postgres.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class TekstTaggTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TekstTaggTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<(Guid RettskildeId, RettskildeNodeEntitet Node)> ImporterAlkoholovenOgFinnForsteLeddAsync(RegelIdeDbContext db)
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24));
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(resultat);
        var node = await db.RettskildeNoder.FirstAsync(
            n => n.RettskildeId == rettskildeId && n.NodeType == "ledd" && n.Tekst != null && n.Tekst.Length > 10);
        return (rettskildeId, node);
    }

    [Fact]
    public async Task Oppretter_tagg_med_riktig_hash_og_ref_null()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var utdrag = node.Tekst![..8];

        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 8, "", utdrag, node.Tekst[8..],  "begrep");

        Assert.NotNull(tagg);
        Assert.Null(tagg!.RefId);
        Assert.Equal(LovdataIdentifikatorer.BeregnTekstHash(node.Tekst), tagg.NodeTekstHash);
        Assert.Equal("gjeldende", tagg.Entitetsstatus);

        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == tagg.Id);
        Assert.Equal("opprettet", proveniens.Handling);
        Assert.Equal("tekst_tagg", proveniens.EntitetType);
    }

    [Theory]
    [InlineData("tjeneste")]
    [InlineData("vilkar")]
    [InlineData("regel")]
    public async Task Godtar_alle_fire_gyldige_kinds(string kind)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));

        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], kind);

        Assert.NotNull(tagg);
        Assert.Equal(kind, tagg!.Kind);
    }

    [Fact]
    public async Task Ugyldig_kind_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "tjenest"));
    }

    [Fact]
    public async Task Inaktiv_kind_avvises_som_ugyldig()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        db.TaggKindKonfigurasjoner.Add(new TaggKindKonfigurasjonEntitet
        {
            Id = Guid.NewGuid(), Kode = "utgatt-kind", Navn = "Utgått", Farge = "neutral", Sorteringsrekkefolge = 99, Aktiv = false,
        });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "utgatt-kind"));
    }

    [Fact]
    public async Task Utdatert_quoteExact_som_ikke_matcher_faktisk_tekst_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", "helt feil tekst", node.Tekst![4..], "begrep"));
    }

    [Fact]
    public async Task Offset_utenfor_teksten_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, node.Tekst!.Length + 100, "", "uansett", "", "begrep"));
    }

    [Fact]
    public async Task Ukjent_node_eid_returnerer_null()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, _) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));

        var resultat = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", "finnes-ikke", 0, 4, "", "abcd", "", "begrep");

        Assert.Null(resultat);
    }

    [Fact]
    public async Task Lister_kun_egen_virksomhets_tagger_ikke_andre_virksomheters()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetA = Guid.NewGuid();
        var virksomhetB = Guid.NewGuid();
        db.Virksomheter.AddRange(
            new Virksomhet { Id = virksomhetA, Navn = "Vennesla kommune" },
            new Virksomhet { Id = virksomhetB, Navn = "Tønsberg kommune" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));

        await tjeneste.OpprettAsync(rettskildeId, virksomhetA, "Bruker A", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "begrep");
        await tjeneste.OpprettAsync(rettskildeId, virksomhetB, "Bruker B", node.Eid, 0, 4, "", node.Tekst[..4], node.Tekst[4..], "vilkar");

        var taggerForA = await tjeneste.ListerForAsync(rettskildeId, virksomhetA);

        Assert.Single(taggerForA);
        Assert.Equal("begrep", taggerForA[0].Kind);
    }

    [Fact]
    public async Task Sletting_arkiverer_i_stedet_for_a_slette_raden()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "begrep");

        var resultat = await tjeneste.SlettAsync(rettskildeId, tagg!.Id, virksomhet, "Kari Jurist");

        Assert.Equal(SlettResultat.Ok, resultat);
        var radI_db = await db.TekstTagger.SingleAsync(t => t.Id == tagg.Id); // fortsatt i tabellen
        Assert.Equal("arkivert", radI_db.Entitetsstatus);
        var egneTagger = await tjeneste.ListerForAsync(rettskildeId, virksomhet);
        Assert.Empty(egneTagger); // men filtreres bort av lesing (kun 'gjeldende')
    }

    [Fact]
    public async Task Sletting_avvises_for_annen_virksomhets_tagg()
    {
        await using var db = _fixture.NyDbContext();
        var eier = Guid.NewGuid();
        var andre = Guid.NewGuid();
        db.Virksomheter.AddRange(
            new Virksomhet { Id = eier, Navn = "Eier kommune" },
            new Virksomhet { Id = andre, Navn = "Annen kommune" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, eier, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "begrep");

        var resultat = await tjeneste.SlettAsync(rettskildeId, tagg!.Id, andre, "Noen andre");

        Assert.Equal(SlettResultat.TilhorerAnnenVirksomhet, resultat);
    }

    [Fact]
    public async Task Sletting_avvises_nar_taggen_har_faatt_en_referanse()
    {
        // ref_id settes aldri av OpprettAsync i byggesteg 1 (§1.2), men AK-3.3.4 ("kun tagger uten
        // publiserte referanser kan fjernes") skal likevel holde straks byggesteg 2/4 knytter en tagg
        // til et faktisk begrep/vilkår -- simulerer den fremtidige tilstanden direkte mot databasen.
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "begrep");

        tagg!.RefId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var resultat = await tjeneste.SlettAsync(rettskildeId, tagg.Id, virksomhet, "Kari Jurist");

        Assert.Equal(SlettResultat.HarPublisertReferanse, resultat);
    }

    [Fact]
    public async Task Sletting_av_ukjent_tagg_gir_ikke_funnet()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, _) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));

        var resultat = await tjeneste.SlettAsync(rettskildeId, Guid.NewGuid(), virksomhet, "Noen");

        Assert.Equal(SlettResultat.IkkeFunnet, resultat);
    }

    [Fact]
    public async Task Kobler_begrep_tagg_til_eksisterende_begrep()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "begrep");

        var begrep = await new BegrepsregisterTjeneste(db).OpprettAsync(
            virksomhet, "eksempelbegrep", "Definisjon", null, null, null, null, "handlingsbegrep", "Kari Jurist");

        var oppdatert = await tjeneste.KobleTilEntitetAsync(tagg!.Id, begrep.Id, "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal(begrep.Id, oppdatert!.RefId);
    }

    [Fact]
    public async Task Kobling_til_feil_entitetstype_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "begrep");

        var enTjeneste = await new TjenesteregisterTjeneste(db).OpprettAsync(
            virksomhet, "Skjenkebevilling", null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist");

        // Taggen er kind='begrep' — kan ikke peke på en Tjeneste-id, selv om Tjenesten faktisk finnes.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.KobleTilEntitetAsync(tagg!.Id, enTjeneste.Id, "Kari Jurist"));
    }

    [Fact]
    public async Task Kobler_vilkar_tagg_til_eksisterende_vilkar()
    {
        // Byggesteg 4 (2026-07-30) — lukker gapet der 'vilkar'-tagger ikke kunne kobles til noe.
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "vilkar");

        var vilkar = await new VilkarregisterTjeneste(db).OpprettAsync(
            virksomhet, "Aldersvilkår", null, null, "materiell", null, null, null, "regelbasert", null,
            null, null, false, null, null, null, false, null, null, "Kari Jurist");

        var oppdatert = await tjeneste.KobleTilEntitetAsync(tagg!.Id, vilkar.Id, "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal(vilkar.Id, oppdatert!.RefId);
    }

    [Fact]
    public async Task Kobler_regel_tagg_til_eksisterende_regelnode()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "regel");

        var regelnode = await new RegelnoderegisterTjeneste(db).OpprettAsync(
            virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean", true, null, null, null, "Kari Jurist");

        var oppdatert = await tjeneste.KobleTilEntitetAsync(tagg!.Id, regelnode.Id, "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal(regelnode.Id, oppdatert!.RefId);
    }

    // ── RefId-validering per Kind for 'virksomhet' (navneform-kjede-runden, 2026-09-08) ──────────
    // Se TekstTaggEntitet.RefId: en 'virksomhet'-tagg peker på NAVNEFORMEN, ikke på Virksomhet-raden.

    /// <summary>Kjernen i Del 1: en 'virksomhet'-tagg kan kobles til en NAVNEFORM.</summary>
    [Fact]
    public async Task Kobler_virksomhet_tagg_til_navneformen()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Kjedetest kommune" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "virksomhet");

        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhet, node.Tekst[..4], "Kari Jurist", skosUrl: null, navneformgrunn: "kortform");

        var oppdatert = await tjeneste.KobleTilEntitetAsync(tagg!.Id, navneform.Id, "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal(navneform.Id, oppdatert!.RefId);

        // Kjeden videre: navneformen bærer grunnen OG peker på virksomheten.
        var lagret = await db.Begreper.SingleAsync(b => b.Id == oppdatert.RefId!.Value);
        Assert.Equal("kortform", lagret.Navneformgrunn);
        Assert.Equal(virksomhet, lagret.VirksomhetReferanseId);
    }

    /// <summary>
    /// Regresjonsvernet for selve skiftet: en VIRKSOMHET-id er nå et UGYLDIG referansemål for en
    /// 'virksomhet'-tagg. Uten denne testen kunne valideringen stille falt tilbake til den gamle
    /// oppførselen uten at noe slo ut.
    /// </summary>
    [Fact]
    public async Task Virksomhet_tagg_kan_IKKE_lenger_kobles_direkte_til_en_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Kjedetest direkte kommune" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "virksomhet");

        // Virksomheten FINNES — det er referansemålets TYPE som avvises, ikke en manglende rad.
        await Assert.ThrowsAsync<ArgumentException>(
            () => tjeneste.KobleTilEntitetAsync(tagg!.Id, virksomhet, "Kari Jurist"));
    }

    /// <summary>Et vanlig begrep (kategori NULL) er ikke en navneform, og skal avvises for
    /// 'virksomhet'-laget — ellers ville «navneform» i praksis betydd «hvilket som helst begrep».</summary>
    [Fact]
    public async Task Virksomhet_tagg_kan_ikke_kobles_til_et_vanlig_begrep()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Kjedetest vanlig begrep kommune" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "virksomhet");

        var vanligBegrep = await new BegrepsregisterTjeneste(db).OpprettAsync(
            virksomhet, "kjedetest vanlig begrep", "Definisjon", null, null, null, null, "faktabegrep", "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(
            () => tjeneste.KobleTilEntitetAsync(tagg!.Id, vanligBegrep.Id, "Kari Jurist"));
    }

    /// <summary>Motsatt retning: en 'begrep'-tagg skal fortsatt kunne peke på en navneform-rad
    /// (den ER et BegrepEntitet). Bekrefter at endringen ikke strammet inn 'begrep'-laget.</summary>
    [Fact]
    public async Task Begrep_tagg_kan_fortsatt_kobles_til_en_navneform()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Kjedetest begreplag kommune" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "begrep");

        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhet, "Kjedetest begreplag navneform", "Kari Jurist");

        var oppdatert = await tjeneste.KobleTilEntitetAsync(tagg!.Id, navneform.Id, "Kari Jurist");

        Assert.Equal(navneform.Id, oppdatert!.RefId);
    }

    /// <summary>
    /// <see cref="TekstTaggTjeneste.ListerForRefIdAsync"/> — «hvor er denne entiteten tagget».
    ///
    /// <para>
    /// Denne metoden hadde INGEN test, og var derfor i praksis ødelagt: OrderBy lå over en konstruert
    /// record, EF klarte ikke oversette spørringen, og <c>GET /api/begreper/{id}/taggede-forekomster</c>
    /// svarte 500 for ALLE begreper. <c>BegrepDetalj.tsx</c> svelger feilen og viser «Ingen andre
    /// taggkoblede forekomster funnet», så feilen var usynlig i GUI-et. Testen kjører spørringen mot
    /// EKTE Postgres — det er nettopp oversettingen som må bevises, og den kan ikke fanges uten en
    /// faktisk database. Sorteringen asserteres også, siden det var sorteringen som veltet.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Lister_forekomster_for_refId_sortert_og_med_rettskildetittel()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Refid-liste kommune" });
        await db.SaveChangesAsync();

        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24));
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(resultat);
        // TO ulike ledd, slik at sorteringen på NodeEid faktisk har noe å sortere.
        var noder = await db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.NodeType == "ledd" && n.Tekst != null && n.Tekst.Length > 10)
            .OrderBy(n => n.Eid).Take(2).ToListAsync();
        Assert.Equal(2, noder.Count);

        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhet, "Refid-liste navneform", "Kari Jurist", skosUrl: null, navneformgrunn: "kortform");

        foreach (var node in noder)
        {
            var tagg = await tjeneste.OpprettAsync(
                rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4,
                "", node.Tekst![..4], node.Tekst[4..], "virksomhet");
            await tjeneste.KobleTilEntitetAsync(tagg!.Id, navneform.Id, "Kari Jurist");
        }

        var forekomster = await tjeneste.ListerForRefIdAsync("virksomhet", navneform.Id);

        Assert.Equal(2, forekomster.Count);
        Assert.All(forekomster, f => Assert.Equal(navneform.Id, f.Tagg.RefId));
        // Rettskildetittelen er JOINet inn — det var joinen sorteringen ikke overlevde.
        Assert.All(forekomster, f => Assert.False(string.IsNullOrWhiteSpace(f.RettskildeTittel)));
        Assert.Equal(
            forekomster.Select(f => f.Tagg.NodeEid).OrderBy(e => e, StringComparer.Ordinal).ToArray(),
            forekomster.Select(f => f.Tagg.NodeEid).ToArray());
    }

    /// <summary>Et annet <c>kind</c> med samme RefId skal ikke lekke inn — kind-et er en del av
    /// oppslaget, ikke bare pynt (det er nettopp derfor endepunktet må velge kind fra begrepets
    /// kategori, se HentBegrepTaggedeForekomster).</summary>
    [Fact]
    public async Task Lister_for_refId_skiller_paa_kind()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Refid-kind kommune" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhet, "Refid-kind navneform", "Kari Jurist");

        // Samme RefId, men lagt i 'begrep'-laget.
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "begrep");
        await tjeneste.KobleTilEntitetAsync(tagg!.Id, navneform.Id, "Kari Jurist");

        Assert.Single(await tjeneste.ListerForRefIdAsync("begrep", navneform.Id));
        Assert.Empty(await tjeneste.ListerForRefIdAsync("virksomhet", navneform.Id));
    }
}
