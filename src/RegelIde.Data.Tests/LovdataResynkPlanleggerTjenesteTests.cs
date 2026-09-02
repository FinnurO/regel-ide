using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// Orkestrering av den planlagte Lovdata-resynk-sjekken (administrasjon-Lovdata-resynk, GitHub-issue
/// #104) — mot ekte embedded Postgres, med en enkel, KONTROLLERBAR <see cref="FakeKlokke"/> i stedet
/// for <see cref="TimeProvider.System"/>/ekte <c>Task.Delay</c>, og en enkel lokal lambda i stedet for
/// et ekte, tregt nettverkskall mot Lovdata — se <see cref="LovdataResynkPlanleggerTjeneste"/>s
/// klassekommentar. Begge tabellene denne testen leser (kjøringer OG innstilling) er globale/singleton
/// på tvers av hele testassemblyens delte embedded Postgres — se <see cref="RyddAsync"/> og
/// LovdataResynkKjoringTjenesteTests/LovdataResynkInnstillingTjenesteTests for samme resonnement.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class LovdataResynkPlanleggerTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public LovdataResynkPlanleggerTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task RyddAsync(RegelIdeDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM lovdata_resynk_kjoringer;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM lovdata_resynk_innstilling;");
    }

    private sealed class FakeKlokke(DateTimeOffset naa) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => naa;
    }

    private static Task<LovdataFullimportResultat> FastResultat(CancellationToken _) =>
        Task.FromResult(new LovdataFullimportResultat(Nye: 1, NyeVersjoner: 0, Uendret: 5, Feilet: 0, TotaltBehandlet: 6));

    private static LovdataResynkPlanleggerTjeneste NyPlanlegger(RegelIdeDbContext db, DateTimeOffset naa) =>
        new(new LovdataResynkInnstillingTjeneste(db), new LovdataResynkKjoringTjeneste(db), new FakeKlokke(naa));

    [Fact]
    public async Task Ingen_lagret_intervall_kjorer_aldri()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var planlegger = NyPlanlegger(db, DateTimeOffset.UtcNow);

        var kaltMedArbeid = false;
        var startet = await planlegger.KjorHvisPaaTideAsync(ct => { kaltMedArbeid = true; return FastResultat(ct); });

        Assert.False(startet);
        Assert.False(kaltMedArbeid);
        Assert.Empty(await db.LovdataResynkKjoringer.ToListAsync());
    }

    [Fact]
    public async Task Intervall_satt_og_aldri_kjort_for_kjorer_na()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        await new LovdataResynkInnstillingTjeneste(db).OppdaterAsync(24, "Kari Saksbehandler");

        var planlegger = NyPlanlegger(db, DateTimeOffset.UtcNow);
        var startet = await planlegger.KjorHvisPaaTideAsync(FastResultat);

        Assert.True(startet);
        var rad = await db.LovdataResynkKjoringer.SingleAsync();
        Assert.Equal(LovdataResynkUtlost.Planlagt, rad.Utlost);
        Assert.Equal(LovdataResynkStatus.Fullfort, rad.Status);
        Assert.Null(rad.UtlostAvBruker); // ingen menneske involvert i en planlagt kjøring
    }

    [Fact]
    public async Task Innenfor_intervallet_siden_siste_kjoring_kjorer_ikke()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        await new LovdataResynkInnstillingTjeneste(db).OppdaterAsync(24, "Kari Saksbehandler");

        var forSisteTime = DateTimeOffset.UtcNow;
        await new LovdataResynkKjoringTjeneste(db).KjorOgRegistrerAsync(LovdataResynkUtlost.Manuell, "Kari Saksbehandler", FastResultat);

        var naa = forSisteTime + TimeSpan.FromHours(1); // kun 1t senere -- intervallet er 24t
        var planlegger = NyPlanlegger(db, naa);
        var startet = await planlegger.KjorHvisPaaTideAsync(FastResultat);

        Assert.False(startet);
        Assert.Equal(1, await db.LovdataResynkKjoringer.CountAsync()); // fortsatt kun den ene manuelle kjøringen
    }

    [Fact]
    public async Task Forbi_intervallet_siden_siste_kjoring_kjorer_pa_nytt()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        await new LovdataResynkInnstillingTjeneste(db).OppdaterAsync(24, "Kari Saksbehandler");

        var forSisteTime = DateTimeOffset.UtcNow;
        await new LovdataResynkKjoringTjeneste(db).KjorOgRegistrerAsync(LovdataResynkUtlost.Oppstart, utlostAvBruker: null, FastResultat);

        var naa = forSisteTime + TimeSpan.FromHours(25); // forbi 24t-intervallet
        var planlegger = NyPlanlegger(db, naa);
        var startet = await planlegger.KjorHvisPaaTideAsync(FastResultat);

        Assert.True(startet);
        Assert.Equal(2, await db.LovdataResynkKjoringer.CountAsync());
    }

    [Fact]
    public async Task Pagaende_kjoring_hindrer_overlapp_selv_om_intervallet_tilsier_kjoring()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        await new LovdataResynkInnstillingTjeneste(db).OppdaterAsync(1, "Kari Saksbehandler");
        // En kjøring som fortsatt pågår (f.eks. en samtidig manuell trigger) -- ALDRI fullført.
        await new LovdataResynkKjoringTjeneste(db).StartKjoringAsync(LovdataResynkUtlost.Manuell, "Kari Saksbehandler");

        var naa = DateTimeOffset.UtcNow + TimeSpan.FromDays(30); // intervallet er uansett langt utløpt
        var planlegger = NyPlanlegger(db, naa);

        var kaltMedArbeid = false;
        var startet = await planlegger.KjorHvisPaaTideAsync(ct => { kaltMedArbeid = true; return FastResultat(ct); });

        Assert.False(startet);
        Assert.False(kaltMedArbeid);
    }

    [Fact]
    public async Task Feilet_arbeid_registreres_og_kastes_videre()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        await new LovdataResynkInnstillingTjeneste(db).OppdaterAsync(24, "Kari Saksbehandler");

        var planlegger = NyPlanlegger(db, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            planlegger.KjorHvisPaaTideAsync(_ => throw new InvalidOperationException("Lovdata utilgjengelig")));

        var rad = await db.LovdataResynkKjoringer.SingleAsync();
        Assert.Equal(LovdataResynkStatus.Feilet, rad.Status);
        Assert.Equal(LovdataResynkUtlost.Planlagt, rad.Utlost);
        Assert.Equal("Lovdata utilgjengelig", rad.Feilmelding);
    }
}
