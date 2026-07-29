using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>Byggesteg 2-testcaseinnhold (2026-07-29, docs/06-veikart.md), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class Byggesteg2InnholdSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public Byggesteg2InnholdSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task ImporterAlkohollovenAsync(RegelIdeDbContext db) =>
        await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

    [Fact]
    public async Task Seeder_tjeneste_begreper_og_kodelister()
    {
        await using var db = _fixture.NyDbContext();
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        await ImporterAlkohollovenAsync(db);

        await Byggesteg2InnholdSeed.SeedAsync(db);

        var tjeneste = await db.Tjenester.SingleAsync(t => t.Tittel == "Alminnelig skjenkebevilling");
        var referanser = await db.TjenesteRegelverksreferanser.Where(r => r.TjenesteId == tjeneste.Id).ToListAsync();
        Assert.Equal(7, referanser.Count);

        var begreper = await db.Begreper.Select(b => b.Term).ToListAsync();
        Assert.Contains("uklanderlig vandel", begreper);
        Assert.Contains("styrer og stedfortreder", begreper);
        Assert.Contains("skjenketid", begreper);

        var vandel = await db.Begreper.SingleAsync(b => b.Term == "uklanderlig vandel");
        Assert.NotNull(vandel.LovreferanseEid);
        Assert.NotNull(vandel.KodelisteReferanseId);

        var kodelistekoder = await db.Kodelister.Select(k => k.Kode).ToListAsync();
        Assert.Contains("KL-VANDELSOMRADE-ALKOHOLLOV", kodelistekoder);
        Assert.Contains("KL-RETTSKILDEVEKT", kodelistekoder);

        var vandelsomrade = await db.Kodelister
            .Include(k => k.Koder)
            .SingleAsync(k => k.Kode == "KL-VANDELSOMRADE-ALKOHOLLOV");
        Assert.Equal(4, vandelsomrade.Koder.Count);
    }

    [Fact]
    public async Task Seeding_er_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        await ImporterAlkohollovenAsync(db);

        await Byggesteg2InnholdSeed.SeedAsync(db);
        var antallTjenesterForste = await db.Tjenester.CountAsync();
        var antallBegreperForste = await db.Begreper.CountAsync();
        var antallKodelisterForste = await db.Kodelister.CountAsync();

        await Byggesteg2InnholdSeed.SeedAsync(db);

        Assert.Equal(antallTjenesterForste, await db.Tjenester.CountAsync());
        Assert.Equal(antallBegreperForste, await db.Begreper.CountAsync());
        Assert.Equal(antallKodelisterForste, await db.Kodelister.CountAsync());
    }
}
