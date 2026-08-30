using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="DepartementSeed"/> mot ekte embedded Postgres — samme delte DataTestCollection-database
/// som resten av seed-testene i denne mappen.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class DepartementSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public DepartementSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Seeder_kunnskapsdepartementet_med_korrekt_orgnr_og_forvaltningsniva()
    {
        await using var db = _fixture.NyDbContext();

        await DepartementSeed.SeedAsync(db);

        var kunnskap = await db.Virksomheter.SingleAsync(v => v.Organisasjonsnummer == "872417842");
        Assert.Equal("Kunnskapsdepartementet", kunnskap.Navn);
        Assert.Equal("stat", kunnskap.Forvaltningsniva);
        Assert.False(kunnskap.Aktiv);
    }

    [Fact]
    public async Task Seeder_alle_13_manglende_departementer()
    {
        await using var db = _fixture.NyDbContext();

        await DepartementSeed.SeedAsync(db);

        string[] forventedeOrgnr =
        [
            "983887457", "972417793", "932384469", "972417807", "972417823", "983887406",
            "972417882", "972417858", "972417866", "872417842", "972417874", "972417904", "972417777",
        ];
        foreach (var orgnr in forventedeOrgnr)
        {
            Assert.True(await db.Virksomheter.AnyAsync(v => v.Organisasjonsnummer == orgnr), $"Mangler orgnr {orgnr}");
        }
    }

    [Fact]
    public async Task Idempotent_ved_gjentatt_kall()
    {
        await using var db = _fixture.NyDbContext();

        await DepartementSeed.SeedAsync(db);
        await DepartementSeed.SeedAsync(db);

        var antall = await db.Virksomheter.CountAsync(v => v.Organisasjonsnummer == "972417807"); // Finansdepartementet
        Assert.Equal(1, antall);
    }

    [Fact]
    public async Task Dupliserer_ikke_departement_som_allerede_finnes_fra_organisasjonsregisteret()
    {
        // Energidepartementet (orgnr 977161630) fantes fra før via OrganisasjonsregisterSeed —
        // DepartementSeed skal gjenkjenne den på orgnr og ikke legge til en ny rad ved siden av.
        await using var db = _fixture.NyDbContext();
        db.Virksomheter.Add(new Virksomhet
        {
            Id = Guid.NewGuid(), Navn = "Energidepartementet", Organisasjonsnummer = "977161630",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        await DepartementSeed.SeedAsync(db);

        var antall = await db.Virksomheter.CountAsync(v => v.Organisasjonsnummer == "977161630");
        Assert.Equal(1, antall);
    }
}
