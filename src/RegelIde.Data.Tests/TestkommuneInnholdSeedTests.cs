using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Testkommunens egne lokale rettskilder (2026-07-29, docs/06-veikart.md), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class TestkommuneInnholdSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TestkommuneInnholdSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Seeder_lokal_forskrift_og_alkoholpolitiske_retningslinjer()
    {
        await using var db = _fixture.NyDbContext();
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        await TestkommuneInnholdSeed.SeedAsync(db);

        var forskrift = await db.Rettskilder.SingleAsync(r => r.Kildetype == "Forskrift" && r.Tittel.Contains("Testkommune"));
        Assert.Equal("act", forskrift.Doctype);
        Assert.NotNull(forskrift.VirksomhetId);
        var forskriftNoder = await db.RettskildeNoder.Where(n => n.RettskildeId == forskrift.Id).ToListAsync();
        Assert.Contains(forskriftNoder, n => n.NodeType == "kapittel" && n.Overskrift == "Salgstider");
        Assert.Contains(forskriftNoder, n => n.Tekst != null && n.Tekst.Contains("kl. 08.00 til 20.00"));

        // Tittel-scopet, samme mønster som forskrift-oppslaget over: siden hele DataTestCollection deler
        // ÉN Postgres-database, ville et uscopet Kildetype=="Virksomhetsdokument"-oppslag brutt i det
        // øyeblikket en ANNEN virksomhets håndbok med samme Kildetype dukket opp (f.eks. Bergens
        // retningslinjer via HandbokImportTjeneste/BergenKorpusSeed, 2026-08-13) — samme kollisjonsrisiko
        // AgderFylkeskommuneSeed sin egen kommentar allerede advarer om for `.Single(b => b.Rolle == ...)`.
        var retningslinjer = await db.Rettskilder.SingleAsync(r => r.Kildetype == "Virksomhetsdokument" && r.Tittel.Contains("Testkommune"));
        Assert.Equal("internal", retningslinjer.Doctype);
        var retningslinjerNoder = await db.RettskildeNoder.Where(n => n.RettskildeId == retningslinjer.Id).ToListAsync();
        Assert.Contains(retningslinjerNoder, n => n.Tekst != null && n.Tekst.Contains("aldersgrense 18 år"));

        // Ingen HandbokKommentarMetadata — dette er selve rettskilden, ikke en kommentar til noe annet.
        var nodeIder = forskriftNoder.Concat(retningslinjerNoder).Select(n => n.Id).ToList();
        Assert.False(await db.HandbokKommentarMetadata.AnyAsync(m => nodeIder.Contains(m.NodeId)));
    }

    [Fact]
    public async Task Seeding_er_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        // Tittel-scopet av samme grunn som over — uten dette teller kollisjonen inn ANDRE virksomheters
        // Forskrift/Virksomhetsdokument-rader i den delte databasen (f.eks. Bergens, se BergenKorpusSeed).
        await TestkommuneInnholdSeed.SeedAsync(db);
        var antallEtterForste = await db.Rettskilder.CountAsync(
            r => (r.Kildetype == "Forskrift" || r.Kildetype == "Virksomhetsdokument") && r.Tittel.Contains("Testkommune"));

        await TestkommuneInnholdSeed.SeedAsync(db);
        var antallEtterAndre = await db.Rettskilder.CountAsync(
            r => (r.Kildetype == "Forskrift" || r.Kildetype == "Virksomhetsdokument") && r.Tittel.Contains("Testkommune"));

        Assert.Equal(2, antallEtterForste);
        Assert.Equal(antallEtterForste, antallEtterAndre);
    }
}
