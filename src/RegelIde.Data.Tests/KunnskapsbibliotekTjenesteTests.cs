namespace RegelIde.Data.Tests;

/// <summary>Kunnskapsbibliotek (byggesteg 5 runde 1, docs/06-veikart.md), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class KunnskapsbibliotekTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KunnskapsbibliotekTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Legger_til_lenke_med_gyldig_url()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        var lenke = await tjeneste.LeggTilLenkeAsync(virksomhet, "https://testkommunen.no/tjenester", "Om tjenestetilbudet", "Kari Jurist");

        Assert.Equal("https://testkommunen.no/tjenester", lenke.Url);
        var liste = await tjeneste.ListerForVirksomhetAsync(virksomhet);
        Assert.Single(liste);
    }

    [Theory]
    [InlineData("ikke-en-url")]
    [InlineData("ftp://testkommunen.no")]
    [InlineData("")]
    public async Task Ugyldig_url_kastes_ingen_gjettet_fallback(string ugyldigUrl)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tjeneste.LeggTilLenkeAsync(virksomhet, ugyldigUrl, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Sletter_lenke()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        var lenke = await tjeneste.LeggTilLenkeAsync(virksomhet, "https://testkommunen.no", null, "Kari Jurist");

        var slettet = await tjeneste.SlettAsync(lenke.Id);

        Assert.True(slettet);
        Assert.Empty(await tjeneste.ListerForVirksomhetAsync(virksomhet));
    }

    [Fact]
    public async Task Lister_kun_egen_virksomhets_lenker()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetA = Guid.NewGuid();
        var virksomhetB = Guid.NewGuid();
        db.Virksomheter.AddRange(
            new Virksomhet { Id = virksomhetA, Navn = "Testkommunen A" },
            new Virksomhet { Id = virksomhetB, Navn = "Testkommunen B" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        await tjeneste.LeggTilLenkeAsync(virksomhetA, "https://a.testkommunen.no", null, "Kari Jurist");
        await tjeneste.LeggTilLenkeAsync(virksomhetB, "https://b.testkommunen.no", null, "Kari Jurist");

        var listeA = await tjeneste.ListerForVirksomhetAsync(virksomhetA);
        Assert.Single(listeA);
        Assert.Equal("https://a.testkommunen.no", listeA[0].Url);
    }
}
