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

        var tjeneste = new TekstTaggTjeneste(db);
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
        var tjeneste = new TekstTaggTjeneste(db);

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
        var tjeneste = new TekstTaggTjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(
            rettskildeId, virksomhet, "Kari Jurist", node.Eid, 0, 4, "", node.Tekst![..4], node.Tekst[4..], "tjenest"));
    }

    [Fact]
    public async Task Utdatert_quoteExact_som_ikke_matcher_faktisk_tekst_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var (rettskildeId, node) = await ImporterAlkoholovenOgFinnForsteLeddAsync(db);
        var tjeneste = new TekstTaggTjeneste(db);

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
        var tjeneste = new TekstTaggTjeneste(db);

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
        var tjeneste = new TekstTaggTjeneste(db);

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
        var tjeneste = new TekstTaggTjeneste(db);

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
        var tjeneste = new TekstTaggTjeneste(db);
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
        var tjeneste = new TekstTaggTjeneste(db);
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
        var tjeneste = new TekstTaggTjeneste(db);
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
        var tjeneste = new TekstTaggTjeneste(db);

        var resultat = await tjeneste.SlettAsync(rettskildeId, Guid.NewGuid(), virksomhet, "Noen");

        Assert.Equal(SlettResultat.IkkeFunnet, resultat);
    }
}
