using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Byggesteg 5 runde 3 — testcase-virksomhet, mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class AgderFylkeskommuneSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AgderFylkeskommuneSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Seeder_virksomhet_og_en_bruker()
    {
        await using var db = _fixture.NyDbContext();

        await AgderFylkeskommuneSeed.SeedAsync(db);

        var virksomhet = await db.Virksomheter.SingleAsync(v => v.Navn == "Agder fylkeskommune");
        var bruker = await db.Brukere.SingleAsync(b => b.VirksomhetId == virksomhet.Id);
        Assert.Equal("Silje Jurist", bruker.Navn);
        Assert.Equal("Fagansvarlig", bruker.Rolle);
    }

    [Fact]
    public async Task Idempotent_ved_gjentatt_kall()
    {
        await using var db = _fixture.NyDbContext();

        await AgderFylkeskommuneSeed.SeedAsync(db);
        await AgderFylkeskommuneSeed.SeedAsync(db);

        var antallVirksomheter = await db.Virksomheter.CountAsync(v => v.Navn == "Agder fylkeskommune");
        Assert.Equal(1, antallVirksomheter);
    }
}
