using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="BegrepsforekomstTjeneste"/> (docs/24 §1.2/§3 punkt 4) mot ekte embedded Postgres —
/// arbeidskøen for godkjenning av begreps-forekomster funnet ved deterministisk sveip, OG at
/// <see cref="BegrepsforekomstTjeneste.GodkjennAsync"/> faktisk oppretter en
/// <see cref="TekstTaggEntitet"/> OG en <see cref="BegrepEntitet"/>-rad. Selve sveipefunksjonen testes
/// separat i <see cref="BegrepsoppdagelseSveipTjenesteTests"/> — testene her dekker køens egen logikk
/// med manuelt konstruerte forekomster (samme oppdeling som <c>VirksomhetKandidatTjenesteTests</c> vs.
/// <c>VirksomhetKandidatSveipTjenesteTests</c>).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class BegrepsforekomstTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BegrepsforekomstTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static BegrepsforekomstTjeneste NyTjeneste(RegelIdeDbContext db) =>
        new(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new BegrepsregisterTjeneste(db));

    /// <summary>Fersk, syntetisk, DELT/nasjonal rettskilde med én ledd-node hvis tekst er
    /// "testbegrep: en testdefinisjon" — samme "egen syntetisk rettskilde per test" -mønster som
    /// <c>VirksomhetKandidatTjenesteTests.OpprettSyntetiskRettskildeAsync</c>.</summary>
    private static async Task<(Guid RettskildeId, RettskildeNodeEntitet Node)> OpprettSyntetiskRettskildeAsync(RegelIdeDbContext db)
    {
        var rettskildeId = Guid.NewGuid();
        var nodeEid = $"https://test/{rettskildeId:N}/§1/ledd-1/punkt-1";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testforskrift " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var node = new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid, KildeId = "punkt-1",
            NodeType = "punkt", Tekst = "testbegrep: en testdefinisjon",
        };
        db.RettskildeNoder.Add(node);
        await db.SaveChangesAsync();
        return (rettskildeId, node);
    }

    private const int TestbegrepStart = 0;
    private const int TestbegrepEnd = 10; // "testbegrep".Length

    [Fact]
    public async Task Oppretter_forekomst_og_lister_i_ventende()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);

        var kø = NyTjeneste(db);
        var forekomst = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");

        Assert.Equal("Venter", forekomst.Status);
        var ventende = await kø.ListerVentendeAsync(rettskildeId);
        Assert.Single(ventende);
    }

    [Fact]
    public async Task Gjentatt_opprettelse_gir_samme_rad_ikke_duplikat()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);
        var kø = NyTjeneste(db);

        var forste = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");
        var andre = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");

        Assert.Equal(forste.Id, andre.Id);
    }

    [Fact]
    public async Task Avvist_forekomst_dukker_ikke_opp_i_ventende_og_gjenskapes_ikke()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);
        var kø = NyTjeneste(db);

        var forekomst = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");
        await kø.AvvisAsync(forekomst.Id, "Kari Jurist");

        Assert.Empty(await kø.ListerVentendeAsync(rettskildeId));

        var etterNyttSveip = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");
        Assert.Equal(forekomst.Id, etterNyttSveip.Id);
        Assert.Equal("Avvist", etterNyttSveip.Status);
    }

    [Fact]
    public async Task Kan_ikke_avvise_forekomst_som_ikke_star_i_venter()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);
        var kø = NyTjeneste(db);
        var forekomst = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");
        await kø.AvvisAsync(forekomst.Id, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => kø.AvvisAsync(forekomst.Id, "Kari Jurist"));
    }

    [Fact]
    public async Task Godkjenn_oppretter_ekte_begrep_og_teksttagg_koblet_sammen()
    {
        // Ende-til-ende (docs/24 §3 punkt 4): en godkjent forekomst skal produsere BÅDE en ny
        // BegrepEntitet (i den angitte virksomhetens register) OG en TekstTagg (kind="begrep",
        // RefId=det nye begrepets id).
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var kø = NyTjeneste(db);
        var forekomst = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");

        var godkjent = await kø.GodkjennAsync(forekomst.Id, virksomhet.Id, "Kari Jurist");

        Assert.NotNull(godkjent);
        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.Equal("Kari Jurist", godkjent.BehandletAv);
        Assert.NotNull(godkjent.BegrepId);

        var begrep = await db.Begreper.SingleAsync(b => b.Id == godkjent.BegrepId);
        Assert.Equal("testbegrep", begrep.Term);
        Assert.Equal("en testdefinisjon", begrep.Definisjon);
        Assert.Equal(virksomhet.Id, begrep.VirksomhetId);
        Assert.Null(begrep.Begrepskategori);
        Assert.Equal("utkast", begrep.Status);
        Assert.Equal("faktabegrep", begrep.Begrepstype);
        Assert.Equal(node.Eid, begrep.LovreferanseEid);

        var tagg = await db.TekstTagger.SingleAsync(t => t.RettskildeId == rettskildeId && t.NodeEid == node.Eid);
        Assert.Equal("begrep", tagg.Kind);
        Assert.Equal(begrep.Id, tagg.RefId);
        Assert.Equal(virksomhet.Id, tagg.VirksomhetId);
        Assert.Equal("testbegrep", tagg.QuoteExact);
        Assert.Equal("gjeldende", tagg.Entitetsstatus);
    }

    [Fact]
    public async Task Godkjenn_kaster_hvis_noden_er_endret_siden_sveipet()
    {
        // Revalidering (docs/24 sitt eksplisitte testkrav) — hvis nodens tekst er endret mellom sveip
        // og godkjenning, skal godkjenning IKKE stille opprette et begrep/en tagg som ikke lenger stemmer.
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var kø = NyTjeneste(db);
        var forekomst = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");

        // Simulerer reimport — paragrafen er endret, teksten på samme node/intervall er nå noe annet.
        node.Tekst = "annetbegrep: en annen definisjon";
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => kø.GodkjennAsync(forekomst.Id, virksomhet.Id, "Kari Jurist"));
        var uendret = await db.Begrepsforekomster.FindAsync(forekomst.Id);
        Assert.Equal("Venter", uendret!.Status); // status skal IKKE flippes når revalidering feiler.
        Assert.Null(uendret.BegrepId);
        Assert.False(await db.TekstTagger.AnyAsync(t => t.RettskildeId == rettskildeId));
        Assert.False(await db.Begreper.AnyAsync(b => b.VirksomhetId == virksomhet.Id));
    }

    [Fact]
    public async Task Godkjenn_kaster_hvis_virksomheten_ikke_finnes()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);
        var kø = NyTjeneste(db);
        var forekomst = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");

        await Assert.ThrowsAsync<ArgumentException>(() => kø.GodkjennAsync(forekomst.Id, Guid.NewGuid(), "Kari Jurist"));
    }

    [Fact]
    public async Task Kan_ikke_godkjenne_forekomst_som_ikke_star_i_venter()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);
        var kø = NyTjeneste(db);
        var forekomst = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");
        await kø.AvvisAsync(forekomst.Id, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(
            () => kø.GodkjennAsync(forekomst.Id, Guid.NewGuid(), "Kari Jurist"));
    }

    [Fact]
    public async Task Kun_avviste_kan_hardslettes()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);
        var kø = NyTjeneste(db);
        var forekomst = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");

        await Assert.ThrowsAsync<ArgumentException>(() => kø.HardslettAvvistAsync(forekomst.Id));

        await kø.AvvisAsync(forekomst.Id, "Kari Jurist");
        Assert.True(await kø.HardslettAvvistAsync(forekomst.Id));
    }

    [Fact]
    public async Task HardslettAlleAvviste_med_rettskildefilter_rammer_kun_avviste_i_valgt_rettskilde()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettSyntetiskRettskildeAsync(db);
        var kø = NyTjeneste(db);
        var avvist = await kø.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, "testbegrep", "testbegrep", "en testdefinisjon",
            "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, TestbegrepStart, TestbegrepEnd, "sveip");
        await kø.AvvisAsync(avvist.Id, "Kari Jurist");

        var antallSlettet = await kø.HardslettAlleAvvisteAsync(rettskildeId: rettskildeId);

        Assert.Equal(1, antallSlettet);
        Assert.False(await db.Begrepsforekomster.AnyAsync(f => f.Id == avvist.Id));
    }
}
