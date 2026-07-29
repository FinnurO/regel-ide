using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Vilkårregister (docs/03-domenemodell.md §1.8), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class VilkarregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VilkarregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Oppretter_vilkar_som_utkast()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);
        var vilkar = await register.OpprettAsync(virksomhet, "Aldersvilkår", "Beskrivelse", null, "materiell", null,
            [new JuridiskGrunnlagInput("alkoholloven", "§1-5")], null, "regelbasert", null, null, null, false, null,
            null, null, false, null, "Kari Jurist");

        Assert.Equal("utkast", vilkar.Status);
        Assert.Contains("§1-5", vilkar.JuridiskGrunnlagJson);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == vilkar.Id);
        Assert.Equal("opprettet", proveniens.Handling);
    }

    [Fact]
    public async Task Skjonnsbasert_uten_skjonnsgrunnlag_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "Vandelsvilkår", null, null, "materiell", null, null, null, "skjonnsbasert", null,
            null, null, false, null, null, null, false, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_skjonnsgrunnlag_begrep_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "Vandelsvilkår", null, null, "materiell", null, null, null, "skjonnsbasert", null,
            Guid.NewGuid(), null, false, null, null, null, false, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_vilkarstype_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "Test", null, null, "ukjent-type", null, null, null, "regelbasert", null,
            null, null, false, null, null, null, false, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Oppdaterer_vilkar_oker_versjon()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);
        var vilkar = await register.OpprettAsync(virksomhet, "Aldersvilkår", null, null, "materiell", null, null,
            null, "regelbasert", null, null, null, false, null, null, null, false, null, "Kari Jurist");

        var oppdatert = await register.OppdaterAsync(vilkar.Id, "Aldersvilkår v2", "Ny beskrivelse", null, "materiell",
            null, null, null, "regelbasert", null, null, null, false, null, null, null, false, null, "Ola Fagansvarlig");

        Assert.NotNull(oppdatert);
        Assert.Equal("Aldersvilkår v2", oppdatert!.Tittel);
        Assert.Equal(2, oppdatert.Versjon);
    }

    [Fact]
    public async Task Legger_til_og_fjerner_input_datasett()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        var datasett = new DatasettEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhet, Felt = "Test", Prop = $"test.{Guid.NewGuid():N}",
            Dtype = "string", Type = "brukeroppgitt", OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Datasett.Add(datasett);
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);
        var vilkar = await register.OpprettAsync(virksomhet, "Aldersvilkår", null, null, "materiell", null, null,
            null, "regelbasert", null, null, null, false, null, null, null, false, null, "Kari Jurist");

        await register.LeggTilInputAsync(vilkar.Id, datasett.Id);
        var input = await register.InputForAsync(vilkar.Id);
        Assert.Single(input);

        var fjernet = await register.FjernInputAsync(vilkar.Id, datasett.Id);
        Assert.True(fjernet);
        Assert.Empty(await register.InputForAsync(vilkar.Id));
    }

    [Fact]
    public async Task Setter_status()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);
        var vilkar = await register.OpprettAsync(virksomhet, "Aldersvilkår", null, null, "materiell", null, null,
            null, "regelbasert", null, null, null, false, null, null, null, false, null, "Kari Jurist");

        var oppdatert = await register.SettStatusAsync(vilkar.Id, "validert", "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal("validert", oppdatert!.Status);
    }

    [Fact]
    public async Task Formel_annotering_lagres()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);
        var vilkar = await register.OpprettAsync(virksomhet, "Bevillingsgebyr", null, null, "materiell", null, null,
            null, "regelbasert", null, null, null, false, null, null, null, true, "Beregnet etter alkoholforskriften § 6-2.", "Kari Jurist");

        Assert.True(vilkar.ErFormel);
        Assert.Equal("Beregnet etter alkoholforskriften § 6-2.", vilkar.FormelBeskrivelse);
    }
}
