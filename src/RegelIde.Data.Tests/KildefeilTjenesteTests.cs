using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #249, 2026-09-10] <see cref="KildefeilTjeneste"/> — det varige, GENERISKE
/// kildefeil-registeret. Testene her dekker køens egen logikk (opprett-eller-finn, listing/filtrering)
/// med en syntetisk <c>type</c>/<c>funnetAvMekanisme</c> som INGEN ekte validering bruker ennå — nettopp
/// for å bevise at tjenesten ikke er hardkodet mot hjemmel-valideringen (akseptansekriterium 3). Selve
/// hookup-en mot <see cref="HjemmelValideringTjeneste"/> testes i <see cref="HjemmelValideringTjenesteTests"/>.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class KildefeilTjenesteTests(EmbeddedPostgresFixture fixture)
{
    private static int _lopenummer = 20_000;

    /// <summary>Fersk, syntetisk rettskilde med unik ELI — samme "unik per test"-begrunnelse som andre
    /// kø-tester i denne collection-en (delt database på tvers av testklasser).</summary>
    private static async Task<Guid> NyRettskildeAsync(RegelIdeDbContext db)
    {
        var nr = Interlocked.Increment(ref _lopenummer);
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "act", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = $"Testforskrift {nr}", Eli = $"https://lovdata.no/eli/forskrift/1980/05/23/{nr}/nor",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task OpprettEllerFinnAsync_oppretter_ny_rad_med_status_Ny()
    {
        await using var db = fixture.NyDbContext();
        var rettskildeId = await NyRettskildeAsync(db);
        var tjeneste = new KildefeilTjeneste(db);

        var rad = await tjeneste.OpprettEllerFinnAsync(
            rettskildeId, "eid-1", "test_type", "En syntetisk beskrivelse.", "test-sveip", "test");

        Assert.Equal("Ny", rad.Status);
        Assert.Equal(rettskildeId, rad.RettskildeId);
        Assert.Equal("eid-1", rad.RettskildeEid);
    }

    [Fact]
    public async Task OpprettEllerFinnAsync_er_idempotent_ingen_duplikat_ved_gjentatt_kall()
    {
        await using var db = fixture.NyDbContext();
        var rettskildeId = await NyRettskildeAsync(db);
        var tjeneste = new KildefeilTjeneste(db);

        var forste = await tjeneste.OpprettEllerFinnAsync(
            rettskildeId, "eid-1", "test_type", "Beskrivelse v1.", "test-sveip", "test");
        var andre = await tjeneste.OpprettEllerFinnAsync(
            rettskildeId, "eid-1", "test_type", "Beskrivelse v2 — skal IKKE overskrive.", "test-sveip", "test");

        Assert.Equal(forste.Id, andre.Id);
        var antall = await db.Kildefeil.CountAsync(
            k => k.RettskildeId == rettskildeId && k.RettskildeEid == "eid-1" && k.Type == "test_type");
        Assert.Equal(1, antall);
        // Beskrivelsen på den EKSISTERENDE raden rører seg ikke — en triagert rad skal ikke få teksten
        // sin overskrevet av at samme funn dukker opp igjen i et senere sveip.
        Assert.Equal("Beskrivelse v1.", andre.Beskrivelse);
    }

    [Fact]
    public async Task Ulik_type_eller_mekanisme_pa_samme_node_gir_separate_rader()
    {
        // Samme (rettskilde, eId) kan ha FLERE ulike kildefeil-typer, eller samme type funnet av to
        // ulike mekanismer — ingen av delene skal kollapses til én rad.
        await using var db = fixture.NyDbContext();
        var rettskildeId = await NyRettskildeAsync(db);
        var tjeneste = new KildefeilTjeneste(db);

        await tjeneste.OpprettEllerFinnAsync(rettskildeId, "eid-1", "type_a", "A", "sveip-a", "test");
        await tjeneste.OpprettEllerFinnAsync(rettskildeId, "eid-1", "type_b", "B", "sveip-a", "test");
        await tjeneste.OpprettEllerFinnAsync(rettskildeId, "eid-1", "type_a", "A igjen", "sveip-b", "test");

        var antall = await db.Kildefeil.CountAsync(k => k.RettskildeId == rettskildeId && k.RettskildeEid == "eid-1");
        Assert.Equal(3, antall);
    }

    [Fact]
    public async Task OpprettEllerFinnAsync_kaster_for_ukjent_rettskilde_ingen_gjettet_fallback()
    {
        await using var db = fixture.NyDbContext();
        var tjeneste = new KildefeilTjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettEllerFinnAsync(
            Guid.NewGuid(), "eid-1", "test_type", "Beskrivelse.", "test-sveip", "test"));
    }

    [Fact]
    public async Task ListerAsync_filtrerer_pa_status_og_rettskilde()
    {
        await using var db = fixture.NyDbContext();
        var rettskildeA = await NyRettskildeAsync(db);
        var rettskildeB = await NyRettskildeAsync(db);
        var tjeneste = new KildefeilTjeneste(db);

        var radA = await tjeneste.OpprettEllerFinnAsync(rettskildeA, "eid-1", "test_type", "A", "sveip", "test");
        await tjeneste.OpprettEllerFinnAsync(rettskildeB, "eid-2", "test_type", "B", "sveip", "test");

        radA.Status = "Kjent";
        await db.SaveChangesAsync();

        var kunA = await tjeneste.ListerAsync(rettskildeId: rettskildeA);
        Assert.Single(kunA);
        Assert.Equal(radA.Id, kunA[0].Id);

        var kunKjent = await tjeneste.ListerAsync(status: "Kjent");
        Assert.Contains(kunKjent, k => k.Id == radA.Id);
        Assert.DoesNotContain(kunKjent, k => k.RettskildeId == rettskildeB);
    }

    [Fact]
    public async Task AntallAsync_filtrerer_pa_mekanisme()
    {
        await using var db = fixture.NyDbContext();
        var rettskildeId = await NyRettskildeAsync(db);
        var tjeneste = new KildefeilTjeneste(db);

        await tjeneste.OpprettEllerFinnAsync(rettskildeId, "eid-1", "test_type", "A", "mekanisme-a", "test");
        await tjeneste.OpprettEllerFinnAsync(rettskildeId, "eid-2", "test_type", "B", "mekanisme-b", "test");

        Assert.True(await tjeneste.AntallAsync("mekanisme-a") >= 1);
        var totalA = await db.Kildefeil.CountAsync(k => k.FunnetAvMekanisme == "mekanisme-a");
        Assert.Equal(totalA, await tjeneste.AntallAsync("mekanisme-a"));
    }
}
