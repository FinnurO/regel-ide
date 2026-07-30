using System.Text.Json;
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

    /* ---------- parametre-kolonnen: eneste jsonb-feltet som tar rå klientstreng ---------- */

    private async Task<(RegelIdeDbContext Db, VilkarregisterTjeneste Register, Guid Virksomhet)> NyttOppsettAsync()
    {
        var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        return (db, new VilkarregisterTjeneste(db), virksomhet);
    }

    private Task<VilkarEntitet> OpprettMedParametreAsync(
        VilkarregisterTjeneste register, Guid virksomhet, string? parametre) =>
        register.OpprettAsync(virksomhet, "Aldersvilkår", null, null, "materiell", null, null, null,
            "regelbasert", parametre, null, null, false, null, null, null, false, null, "Kari Jurist");

    [Theory]
    [InlineData("{ikke json}")]
    [InlineData("{\"mangler\": }")]
    [InlineData("{\"uavsluttet\": 1")]
    [InlineData("")]           // tom streng er ikke gyldig JSON — men behandles som "utelatt", se egen test
    public async Task Ugyldig_parametre_json_gir_ArgumentException_ikke_databasefeil(string parametre)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var register = new VilkarregisterTjeneste(db);

        if (parametre.Length == 0)
        {
            // Tom streng er en utelatt verdi, ikke en feil.
            var vilkar = await OpprettMedParametreAsync(register, virksomhet, parametre);
            Assert.Equal("{}", vilkar.ParametreJson);
            return;
        }

        // Poenget: ArgumentException (-> 400) i stedet for DbUpdateException (-> ubehandlet 500).
        var feil = await Assert.ThrowsAsync<ArgumentException>(
            () => OpprettMedParametreAsync(register, virksomhet, parametre));
        Assert.Contains("parametre", feil.Message);
        Assert.Empty(await db.Vilkar.Where(v => v.VirksomhetId == virksomhet).ToListAsync());
    }

    [Theory]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"bare en streng\"")]
    [InlineData("42")]
    [InlineData("null")]
    public async Task Gyldig_json_som_ikke_er_objekt_avvises(string parametre)
    {
        var (db, register, virksomhet) = await NyttOppsettAsync();
        await using var _ = db;

        var feil = await Assert.ThrowsAsync<ArgumentException>(
            () => OpprettMedParametreAsync(register, virksomhet, parametre));
        Assert.Contains("JSON-objekt", feil.Message);
    }

    [Fact]
    public async Task Gyldig_parametre_objekt_lagres_og_kan_leses_tilbake()
    {
        var (db, register, virksomhet) = await NyttOppsettAsync();
        await using var _ = db;

        var vilkar = await OpprettMedParametreAsync(
            register, virksomhet, """{"aldersgrense":18,"gjelderØl":true}""");

        db.ChangeTracker.Clear();
        var lagret = await db.Vilkar.SingleAsync(v => v.Id == vilkar.Id);

        // Sammenlignes semantisk, ikke tegn for tegn: Postgres' jsonb normaliserer verdien ved
        // lagring (her legges det inn mellomrom etter kolon). SQLite lagrer JSON som ren TEXT og
        // gjør ingen slik normalisering — en tegn-for-tegn-assert ville altså gitt ulikt svar på
        // de to profilene, og testet lagringsformatet i stedet for oppførselen vi bryr oss om.
        using var dokument = JsonDocument.Parse(lagret.ParametreJson);
        Assert.Equal(18, dokument.RootElement.GetProperty("aldersgrense").GetInt32());
        Assert.True(dokument.RootElement.GetProperty("gjelderØl").GetBoolean());
    }

    [Fact]
    public async Task Utelatt_parametre_blir_tomt_objekt()
    {
        var (db, register, virksomhet) = await NyttOppsettAsync();
        await using var _ = db;

        var vilkar = await OpprettMedParametreAsync(register, virksomhet, null);

        Assert.Equal("{}", vilkar.ParametreJson);
    }

    [Fact]
    public async Task Oppdatering_validerer_parametre_og_lar_raden_sta_urort()
    {
        var (db, register, virksomhet) = await NyttOppsettAsync();
        await using var _ = db;

        var vilkar = await OpprettMedParametreAsync(register, virksomhet, """{"aldersgrense":18}""");

        await Assert.ThrowsAsync<ArgumentException>(() => register.OppdaterAsync(
            vilkar.Id, "Endret tittel", null, null, "materiell", null, null, null, "regelbasert",
            "{ugyldig}", null, null, false, null, null, null, false, null, "Kari Jurist"));

        // Verifiser mot databasen, ikke mot sporet entitet.
        db.ChangeTracker.Clear();
        var uendret = await db.Vilkar.SingleAsync(v => v.Id == vilkar.Id);
        Assert.Equal("Aldersvilkår", uendret.Tittel);
        using var dokument = JsonDocument.Parse(uendret.ParametreJson);
        Assert.Equal(18, dokument.RootElement.GetProperty("aldersgrense").GetInt32());
    }
}
