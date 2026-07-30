using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Kommunale/nasjonale parameterverdier (docs/12-fasit-handbok-leveranse.md dimensjon C), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class DatasettregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public DatasettregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyttVirksomhetAsync(RegelIdeDbContext db, string navn = "Testkommunen")
    {
        var id = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = id, Navn = navn });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> NyttDatasettAsync(RegelIdeDbContext db, Guid virksomhet)
    {
        // Prop må være unik i denne testens egen scope — "klokkeslett.tidspunkt" er allerede brukt av
        // Byggesteg4VilkarstreSeed sine egne datasett-rader i den delte embedded Postgres-basen
        // (DataTestCollection deler ÉN database på tvers av hele assemblyen), og NyttDatasettAsync sin
        // egen dedup-logikk i den kilden slår opp på nøyaktig denne Prop-strengen globalt.
        var id = Guid.NewGuid();
        db.Datasett.Add(new DatasettEntitet
        {
            Id = id, VirksomhetId = virksomhet, Felt = "Tidspunkt for skjenking", Prop = $"test.klokkeslett.{id}",
            Dtype = "string", Type = "oppslagbart", OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Setter_kommunal_verdi()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var datasett = await NyttDatasettAsync(db, virksomhet);

        var register = new DatasettregisterTjeneste(db);
        var verdi = await register.SettVerdiAsync(datasett, virksomhet, JsonSerializer.Serialize("07:00–03:00"), "Retningslinjer 2024–2028", "Kari Jurist");

        Assert.Equal(virksomhet, verdi.VirksomhetId);
        Assert.Equal("Retningslinjer 2024–2028", verdi.Kilde);
    }

    [Fact]
    public async Task Setter_standardverdi_med_null_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var datasett = await NyttDatasettAsync(db, virksomhet);

        var register = new DatasettregisterTjeneste(db);
        var verdi = await register.SettVerdiAsync(datasett, null, JsonSerializer.Serialize("08:00–01:00"), "Nasjonal norm", "Kari Jurist");

        Assert.Null(verdi.VirksomhetId);
    }

    [Fact]
    public async Task Ny_verdi_for_samme_datasett_og_virksomhet_oppdaterer_i_stedet_for_a_duplisere()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var datasett = await NyttDatasettAsync(db, virksomhet);

        var register = new DatasettregisterTjeneste(db);
        var forste = await register.SettVerdiAsync(datasett, virksomhet, JsonSerializer.Serialize("07:00–03:00"), null, "Kari Jurist");
        var andre = await register.SettVerdiAsync(datasett, virksomhet, JsonSerializer.Serialize("08:00–02:00"), null, "Kari Jurist");

        Assert.Equal(forste.Id, andre.Id);
        var alle = await register.HentVerdierAsync(datasett);
        Assert.Single(alle);
        Assert.Equal(JsonSerializer.Serialize("08:00–02:00"), alle[0].VerdiJson);
    }

    [Fact]
    public async Task Ugyldig_json_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var datasett = await NyttDatasettAsync(db, virksomhet);

        var register = new DatasettregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.SettVerdiAsync(datasett, virksomhet, "{ugyldig", null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_datasett_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);

        var register = new DatasettregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(
            () => register.SettVerdiAsync(Guid.NewGuid(), virksomhet, JsonSerializer.Serialize("x"), null, "Kari Jurist"));
    }

    [Fact]
    public async Task Fjerner_verdi()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyttVirksomhetAsync(db);
        var datasett = await NyttDatasettAsync(db, virksomhet);

        var register = new DatasettregisterTjeneste(db);
        var verdi = await register.SettVerdiAsync(datasett, virksomhet, JsonSerializer.Serialize("07:00–03:00"), null, "Kari Jurist");

        Assert.True(await register.FjernVerdiAsync(verdi.Id));
        Assert.Empty(await register.HentVerdierAsync(datasett));
    }
}
