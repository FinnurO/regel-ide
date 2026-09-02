using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// Singleton-innstillingsraden for planlagt Lovdata-resynk (administrasjon-Lovdata-resynk, GitHub-issue
/// #104) — mot ekte embedded Postgres. Raden er, med vilje, en GLOBAL singleton (Id alltid 1, se
/// <see cref="LovdataResynkInnstillingEntitet"/>s klassekommentar) — delt embedded Postgres for HELE
/// assemblyen (EmbeddedPostgresFixture) betyr derfor at hver test her MÅ nullstille raden selv FØR den
/// setter opp sitt eget scenario, se <see cref="RyddAsync"/> (samme resonnement som
/// LovdataResynkKjoringTjenesteTests).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class LovdataResynkInnstillingTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public LovdataResynkInnstillingTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static Task RyddAsync(RegelIdeDbContext db) =>
        db.Database.ExecuteSqlRawAsync("DELETE FROM lovdata_resynk_innstilling;");

    [Fact]
    public async Task HentAsync_oppretter_standardrad_lazily_med_intervall_null()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkInnstillingTjeneste(db);

        var innstilling = await tjeneste.HentAsync();
        Assert.Null(innstilling.IntervallTimer);
        Assert.Null(innstilling.SistEndretAv);

        // Idempotent -- andre kall skal IKKE opprette en ny/ekstra rad.
        await tjeneste.HentAsync();
        Assert.Equal(1, await db.LovdataResynkInnstillinger.CountAsync());
    }

    [Fact]
    public async Task OppdaterAsync_lagrer_intervall_og_hvem_som_endret()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkInnstillingTjeneste(db);

        var oppdatert = await tjeneste.OppdaterAsync(24, "Kari Saksbehandler");
        Assert.Equal(24, oppdatert.IntervallTimer);
        Assert.Equal("Kari Saksbehandler", oppdatert.SistEndretAv);

        // Lest tilbake fra en FERSK DbContext -- faktisk persistert, ikke bare in-memory-tilstanden.
        await using var friskDb = _fixture.NyDbContext();
        var lest = await new LovdataResynkInnstillingTjeneste(friskDb).HentAsync();
        Assert.Equal(24, lest.IntervallTimer);
        Assert.Equal("Kari Saksbehandler", lest.SistEndretAv);
    }

    [Fact]
    public async Task OppdaterAsync_til_null_betyr_aldri_automatisk()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkInnstillingTjeneste(db);

        await tjeneste.OppdaterAsync(168, "Kari Saksbehandler");
        var tilbakestilt = await tjeneste.OppdaterAsync(null, "Kari Saksbehandler");
        Assert.Null(tilbakestilt.IntervallTimer);
    }

    [Fact]
    public async Task Negativt_intervall_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkInnstillingTjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OppdaterAsync(-1, "Kari Saksbehandler"));
    }
}
