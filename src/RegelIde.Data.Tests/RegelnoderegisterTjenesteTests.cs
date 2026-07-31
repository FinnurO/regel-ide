using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Regelnoderegister (docs/03-domenemodell.md §1.9), inkl. DAG-validering (INV-7), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class RegelnoderegisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public RegelnoderegisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyttVirksomhetAsync(RegelIdeDbContext db)
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        return virksomhet;
    }

    private static Task<VilkarEntitet> NyttVilkarAsync(RegelIdeDbContext db, Guid virksomhet, string tittel) =>
        new VilkarregisterTjeneste(db).OpprettAsync(virksomhet, tittel, null, null, "materiell", null, null,
            null, "regelbasert", null, null, null, false, null, null, null, false, null, null, "Kari Jurist");

    [Fact]
    public async Task Oppretter_regelnode_som_utkast()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);

        var register = new RegelnoderegisterTjeneste(db);
        var regelnode = await register.OpprettAsync(virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean",
            true, null, null, null, "Kari Jurist");

        Assert.Equal("utkast", regelnode.Status);
        Assert.True(regelnode.ErRotnode);
    }

    [Fact]
    public async Task Ugyldig_barn_operator_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);

        var register = new RegelnoderegisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "Vedtak", null, null, "XOR", "Utfall", "boolean", true, null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Kobler_vilkar_som_barn()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var vilkar = await NyttVilkarAsync(db, virksomhet, "Aldersvilkår");

        var register = new RegelnoderegisterTjeneste(db);
        var regelnode = await register.OpprettAsync(virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean",
            true, null, null, null, "Kari Jurist");

        var barn = await register.KobleBarnAsync(regelnode.Id, "vilkar", vilkar.Id);

        Assert.Equal("vilkar", barn.BarnType);
        Assert.Single(await register.BarnForAsync(regelnode.Id));
    }

    [Fact]
    public async Task Kobler_regelnode_som_barn_av_annen_regelnode()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);

        var register = new RegelnoderegisterTjeneste(db);
        var indre = await register.OpprettAsync(virksomhet, "Skjenketid", null, null, "OG", "Utfall", "boolean",
            false, null, null, null, "Kari Jurist");
        var ytre = await register.OpprettAsync(virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean",
            true, null, null, null, "Kari Jurist");

        var barn = await register.KobleBarnAsync(ytre.Id, "regelnode", indre.Id);

        Assert.Equal("regelnode", barn.BarnType);
        Assert.Equal(indre.Id, barn.BarnId);
    }

    [Fact]
    public async Task Duplisert_barn_kobling_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var vilkar = await NyttVilkarAsync(db, virksomhet, "Aldersvilkår");

        var register = new RegelnoderegisterTjeneste(db);
        var regelnode = await register.OpprettAsync(virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean",
            true, null, null, null, "Kari Jurist");
        await register.KobleBarnAsync(regelnode.Id, "vilkar", vilkar.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => register.KobleBarnAsync(regelnode.Id, "vilkar", vilkar.Id));
    }

    [Fact]
    public async Task Ukjent_barn_id_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);

        var register = new RegelnoderegisterTjeneste(db);
        var regelnode = await register.OpprettAsync(virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean",
            true, null, null, null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => register.KobleBarnAsync(regelnode.Id, "vilkar", Guid.NewGuid()));
    }

    [Fact]
    public async Task Sykel_via_direkte_selvreferanse_avvises()
    {
        // Regelnode A -> barn B (regelnode). Forsøk deretter å koble A som barn av B — direkte sykel.
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);

        var register = new RegelnoderegisterTjeneste(db);
        var a = await register.OpprettAsync(virksomhet, "A", null, null, "OG", "Utfall", "boolean", false, null, null, null, "Kari Jurist");
        var b = await register.OpprettAsync(virksomhet, "B", null, null, "OG", "Utfall", "boolean", false, null, null, null, "Kari Jurist");
        await register.KobleBarnAsync(a.Id, "regelnode", b.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => register.KobleBarnAsync(b.Id, "regelnode", a.Id));
        Assert.Contains("sykel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sykel_via_transitiv_kjede_avvises()
    {
        // A -> B -> C (regelnoder). Forsøk å koble A som barn av C — transitiv sykel over to hopp.
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);

        var register = new RegelnoderegisterTjeneste(db);
        var a = await register.OpprettAsync(virksomhet, "A", null, null, "OG", "Utfall", "boolean", false, null, null, null, "Kari Jurist");
        var b = await register.OpprettAsync(virksomhet, "B", null, null, "OG", "Utfall", "boolean", false, null, null, null, "Kari Jurist");
        var c = await register.OpprettAsync(virksomhet, "C", null, null, "OG", "Utfall", "boolean", false, null, null, null, "Kari Jurist");
        await register.KobleBarnAsync(a.Id, "regelnode", b.Id);
        await register.KobleBarnAsync(b.Id, "regelnode", c.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => register.KobleBarnAsync(c.Id, "regelnode", a.Id));
    }

    [Fact]
    public async Task Fjerner_barn()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var vilkar = await NyttVilkarAsync(db, virksomhet, "Aldersvilkår");

        var register = new RegelnoderegisterTjeneste(db);
        var regelnode = await register.OpprettAsync(virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean",
            true, null, null, null, "Kari Jurist");
        await register.KobleBarnAsync(regelnode.Id, "vilkar", vilkar.Id);

        var fjernet = await register.FjernBarnAsync(regelnode.Id, "vilkar", vilkar.Id);

        Assert.True(fjernet);
        Assert.Empty(await register.BarnForAsync(regelnode.Id));
    }

    [Fact]
    public async Task Setter_operator()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);

        var register = new RegelnoderegisterTjeneste(db);
        var regelnode = await register.OpprettAsync(virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean",
            true, null, null, null, "Kari Jurist");

        var oppdatert = await register.SettOperatorAsync(regelnode.Id, "ELLER", "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal("ELLER", oppdatert!.BarnOperator);
    }

    [Fact]
    public async Task Koblede_barn_far_stigende_rekkefolge()
    {
        // 2026-07-30 (docs/12-fasit-handbok-leveranse.md "Hovedfunn") — nødvendig for en stabil
        // beslutnings-ordnet traversering i veiledningsvisningen.
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var vilkarA = await NyttVilkarAsync(db, virksomhet, "A");
        var vilkarB = await NyttVilkarAsync(db, virksomhet, "B");
        var vilkarC = await NyttVilkarAsync(db, virksomhet, "C");

        var register = new RegelnoderegisterTjeneste(db);
        var regelnode = await register.OpprettAsync(virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean",
            true, null, null, null, "Kari Jurist");
        await register.KobleBarnAsync(regelnode.Id, "vilkar", vilkarA.Id);
        await register.KobleBarnAsync(regelnode.Id, "vilkar", vilkarB.Id);
        await register.KobleBarnAsync(regelnode.Id, "vilkar", vilkarC.Id);

        var barn = await register.BarnForAsync(regelnode.Id);

        Assert.Equal(vilkarA.Id, barn.Single(b => b.Rekkefolge == 0).BarnId);
        Assert.Equal(vilkarB.Id, barn.Single(b => b.Rekkefolge == 1).BarnId);
        Assert.Equal(vilkarC.Id, barn.Single(b => b.Rekkefolge == 2).BarnId);
    }

    [Fact]
    public async Task Setter_status()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);

        var register = new RegelnoderegisterTjeneste(db);
        var regelnode = await register.OpprettAsync(virksomhet, "Vedtak", null, null, "OG", "Utfall", "boolean",
            true, null, null, null, "Kari Jurist");

        var oppdatert = await register.SettStatusAsync(regelnode.Id, "validert", "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal("validert", oppdatert!.Status);
    }
}
