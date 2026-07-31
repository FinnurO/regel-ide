using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Unntaksregister (docs/03-domenemodell.md §1.10), inkl. DAG-validering (INV-7), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class UnntaksregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public UnntaksregisterTjenesteTests(EmbeddedPostgresFixture fixture)
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

    private static Task<RegelnodeEntitet> NyRegelnodeAsync(RegelIdeDbContext db, Guid virksomhet, string tittel, bool erRotnode = false) =>
        new RegelnoderegisterTjeneste(db).OpprettAsync(virksomhet, tittel, null, null, "OG", "Utfall", "boolean",
            erRotnode, null, null, null, "Kari Jurist");

    [Fact]
    public async Task Oppretter_unntak_med_vilkar_betingelse()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var regel = await NyRegelnodeAsync(db, virksomhet, "Skjenketid");
        var vilkar = await NyttVilkarAsync(db, virksomhet, "Er lukket selskap");

        var register = new UnntaksregisterTjeneste(db);
        var unntak = await register.OpprettAsync(virksomhet, "Unntak for lukket selskap", null, regel.Id, "vilkar", vilkar.Id, null, "Kari Jurist");

        Assert.Equal(regel.Id, unntak.GjelderRegelId);
        Assert.Equal(vilkar.Id, unntak.BetingelseId);
        Assert.Equal("utkast", unntak.Status);
    }

    [Fact]
    public async Task Ukjent_betingelse_type_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var regel = await NyRegelnodeAsync(db, virksomhet, "Skjenketid");

        var register = new UnntaksregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, "Unntak", null, regel.Id, "ukjent-type", Guid.NewGuid(), null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_gjelder_regel_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var vilkar = await NyttVilkarAsync(db, virksomhet, "Er lukket selskap");

        var register = new UnntaksregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, "Unntak", null, Guid.NewGuid(), "vilkar", vilkar.Id, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_betingelse_id_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var regel = await NyRegelnodeAsync(db, virksomhet, "Skjenketid");

        var register = new UnntaksregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, "Unntak", null, regel.Id, "vilkar", Guid.NewGuid(), null, "Kari Jurist"));
    }

    [Fact]
    public async Task Unntak_som_skaper_sykel_avvises()
    {
        // Ytre -> barn Indre (regelnode). Forsøk unntak: gjelderRegel=Indre, betingelse=Ytre — Ytre kan
        // allerede nå Indre via barn-kanten, så dette ville lukket en sykel (INV-7).
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var regelnoderegister = new RegelnoderegisterTjeneste(db);
        var ytre = await NyRegelnodeAsync(db, virksomhet, "Ytre");
        var indre = await NyRegelnodeAsync(db, virksomhet, "Indre");
        await regelnoderegister.KobleBarnAsync(ytre.Id, "regelnode", indre.Id);

        var register = new UnntaksregisterTjeneste(db);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, "Sykel-unntak", null, indre.Id, "regelnode", ytre.Id, null, "Kari Jurist"));
        Assert.Contains("sykel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Oppdaterer_unntak_oker_versjon()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var regel = await NyRegelnodeAsync(db, virksomhet, "Skjenketid");
        var vilkar = await NyttVilkarAsync(db, virksomhet, "Er lukket selskap");

        var register = new UnntaksregisterTjeneste(db);
        var unntak = await register.OpprettAsync(virksomhet, "Unntak", null, regel.Id, "vilkar", vilkar.Id, null, "Kari Jurist");

        var oppdatert = await register.OppdaterAsync(unntak.Id, "Unntak v2", "Ny beskrivelse", null, "Ola Fagansvarlig");

        Assert.NotNull(oppdatert);
        Assert.Equal("Unntak v2", oppdatert!.Tittel);
        Assert.Equal(2, oppdatert.Versjon);
    }

    [Fact]
    public async Task Setter_status()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var regel = await NyRegelnodeAsync(db, virksomhet, "Skjenketid");
        var vilkar = await NyttVilkarAsync(db, virksomhet, "Er lukket selskap");

        var register = new UnntaksregisterTjeneste(db);
        var unntak = await register.OpprettAsync(virksomhet, "Unntak", null, regel.Id, "vilkar", vilkar.Id, null, "Kari Jurist");

        var oppdatert = await register.SettStatusAsync(unntak.Id, "validert", "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal("validert", oppdatert!.Status);
    }
}
