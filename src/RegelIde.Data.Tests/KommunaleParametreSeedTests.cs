using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>Kommunale datasett-verdier for §5.5-testcaset (docs/12-fasit-handbok-leveranse.md dimensjon C, 2026-07-30), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class KommunaleParametreSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KommunaleParametreSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task ForberedForutsetningerAsync(RegelIdeDbContext db)
    {
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        await Byggesteg2InnholdSeed.SeedAsync(db);
        await Byggesteg4VilkarstreSeed.SeedAsync(db);
    }

    [Fact]
    public async Task Seeder_tonsberg_og_barum_med_standardverdi()
    {
        await using var db = _fixture.NyDbContext();
        await ForberedForutsetningerAsync(db);

        await KommunaleParametreSeed.SeedAsync(db);

        var klokkeslett = await db.Datasett.SingleAsync(d => d.Prop == "klokkeslett.tidspunkt");
        var verdier = await db.DatasettVerdier.Where(v => v.DatasettId == klokkeslett.Id).ToListAsync();
        Assert.Equal(3, verdier.Count); // Tønsberg, Bærum, standardverdi

        var tonsberg = await db.Virksomheter.SingleAsync(v => v.Navn == "Tønsberg kommune");
        var barum = await db.Virksomheter.SingleAsync(v => v.Navn == "Bærum kommune");
        Assert.Contains(verdier, v => v.VirksomhetId == tonsberg.Id);
        Assert.Contains(verdier, v => v.VirksomhetId == barum.Id);
        Assert.Contains(verdier, v => v.VirksomhetId == null);
    }

    [Fact]
    public async Task Seeding_er_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        await ForberedForutsetningerAsync(db);

        await KommunaleParametreSeed.SeedAsync(db);
        var antallVirksomheterForste = await db.Virksomheter.CountAsync();
        var antallVerdierForste = await db.DatasettVerdier.CountAsync();

        await KommunaleParametreSeed.SeedAsync(db);

        Assert.Equal(antallVirksomheterForste, await db.Virksomheter.CountAsync());
        Assert.Equal(antallVerdierForste, await db.DatasettVerdier.CountAsync());
    }
}
