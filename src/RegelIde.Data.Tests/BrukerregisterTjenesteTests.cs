namespace RegelIde.Data.Tests;

/// <summary>Brukerhåndtering (opprett/rediger + tilordning til virksomhet), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class BrukerregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BrukerregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Oppretter_bruker_og_tilordner_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BrukerregisterTjeneste(db);
        var bruker = await register.OpprettAsync("Ola Testbruker", "Saksbehandler", virksomhet);

        Assert.Equal("Ola Testbruker", bruker.Navn);
        Assert.Equal("Saksbehandler", bruker.Rolle);
        Assert.Equal(virksomhet, bruker.VirksomhetId);
        Assert.Null(bruker.AltinnBrukerId);
    }

    [Fact]
    public async Task Tomt_navn_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BrukerregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync("   ", "Saksbehandler", virksomhet));
    }

    [Fact]
    public async Task Ukjent_rolle_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BrukerregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync("Ola Testbruker", "Direktør", virksomhet));
    }

    [Fact]
    public async Task Ukjent_virksomhet_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var register = new BrukerregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync("Ola Testbruker", "Saksbehandler", Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Fagansvarlig")]
    [InlineData("Jurist")]
    [InlineData("Systemforvalter")]
    [InlineData("Saksbehandler")]
    public async Task Alle_fire_roller_fra_rbac_matrisen_er_gyldige(string rolle)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BrukerregisterTjeneste(db);
        var bruker = await register.OpprettAsync("Testbruker", rolle, virksomhet);
        Assert.Equal(rolle, bruker.Rolle);
    }

    [Fact]
    public async Task Oppdaterer_rolle_og_virksomhet_pa_eksisterende_bruker()
    {
        await using var db = _fixture.NyDbContext();
        var forsteVirksomhet = Guid.NewGuid();
        var andreVirksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = forsteVirksomhet, Navn = "Første kommune" });
        db.Virksomheter.Add(new Virksomhet { Id = andreVirksomhet, Navn = "Andre kommune" });
        await db.SaveChangesAsync();

        var register = new BrukerregisterTjeneste(db);
        var bruker = await register.OpprettAsync("Ola Testbruker", "Saksbehandler", forsteVirksomhet);

        var oppdatert = await register.OppdaterAsync(bruker.Id, "Systemforvalter", andreVirksomhet);

        Assert.NotNull(oppdatert);
        Assert.Equal("Systemforvalter", oppdatert!.Rolle);
        Assert.Equal(andreVirksomhet, oppdatert.VirksomhetId);
        // Navnet endres ikke av OppdaterAsync — se kommentaren på metoden.
        Assert.Equal("Ola Testbruker", oppdatert.Navn);
    }

    [Fact]
    public async Task Oppdaterer_ukjent_bruker_gir_null()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BrukerregisterTjeneste(db);
        var resultat = await register.OppdaterAsync(Guid.NewGuid(), "Saksbehandler", virksomhet);

        Assert.Null(resultat);
    }

    [Fact]
    public async Task Oppdatering_med_ukjent_rolle_kastes_og_endrer_ikke_bruker()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BrukerregisterTjeneste(db);
        var bruker = await register.OpprettAsync("Ola Testbruker", "Saksbehandler", virksomhet);

        await Assert.ThrowsAsync<ArgumentException>(() => register.OppdaterAsync(bruker.Id, "Direktør", virksomhet));

        var uendret = await register.FinnAsync(bruker.Id);
        Assert.Equal("Saksbehandler", uendret!.Rolle);
    }

    [Fact]
    public async Task Lister_alle_brukere_sortert_pa_navn()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BrukerregisterTjeneste(db);
        var unikPrefiks = Guid.NewGuid().ToString("N")[..8];
        await register.OpprettAsync($"{unikPrefiks}-Yngve", "Saksbehandler", virksomhet);
        await register.OpprettAsync($"{unikPrefiks}-Bjørn", "Fagansvarlig", virksomhet);

        var alle = await register.ListerAlleAsync();
        var navnMedPrefiks = alle.Where(b => b.Navn.StartsWith(unikPrefiks)).Select(b => b.Navn).ToList();

        Assert.Equal([$"{unikPrefiks}-Bjørn", $"{unikPrefiks}-Yngve"], navnMedPrefiks);
    }
}
