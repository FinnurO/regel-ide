using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>Tjenesteregister (CPSV-AP-NO, docs/03-domenemodell.md §1.5), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class TjenesteregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TjenesteregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> ImporterAlkohollovenAsync(RegelIdeDbContext db) =>
        await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

    [Fact]
    public async Task Oppretter_tjeneste_som_utkast()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(
            virksomhet, "Alminnelig skjenkebevilling", "Beskrivelse", "Testkommunen", "Vedtak", "Enkeltvedtak",
            "Virksomheter", ["Digitalt"], "Gebyr", "3 måneder", "Skjenkekontoret", "Inndragning", ["nb"],
            "Kari Jurist");

        Assert.Equal("utkast", tjeneste.Status);
        Assert.Equal(1, tjeneste.Versjon);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == tjeneste.Id);
        Assert.Equal("opprettet", proveniens.Handling);
    }

    [Fact]
    public async Task Tom_tittel_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "  ", null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Oppdaterer_tjeneste_oker_versjon_og_setter_sist_endret()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(virksomhet, "Skjenkebevilling", null, null, null, null, null,
            null, null, null, null, null, null, "Kari Jurist");

        var oppdatert = await register.OppdaterAsync(tjeneste.Id, "Alminnelig skjenkebevilling", "Ny beskrivelse",
            null, null, null, null, null, null, null, null, null, null, "Ola Fagansvarlig");

        Assert.NotNull(oppdatert);
        Assert.Equal("Alminnelig skjenkebevilling", oppdatert!.Tittel);
        Assert.Equal(2, oppdatert.Versjon);
        Assert.Equal("Ola Fagansvarlig", oppdatert.SistEndretAv);
    }

    [Fact]
    public async Task Kobler_regelverksreferanse_til_gyldig_node()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await ImporterAlkohollovenAsync(db);
        var node = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == rettskildeId && n.NodeType == "paragraf");

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(virksomhet, "Skjenkebevilling", null, null, null, null, null,
            null, null, null, null, null, null, "Kari Jurist");

        var referanse = await register.KobleRegelverksreferanseAsync(tjeneste.Id, rettskildeId, node.Eid);

        Assert.Equal(tjeneste.Id, referanse.TjenesteId);
        var referanser = await register.RegelverksreferanserForAsync(tjeneste.Id);
        Assert.Single(referanser);
    }

    [Fact]
    public async Task Kobler_regelverksreferanse_til_ukjent_node_kaster()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await ImporterAlkohollovenAsync(db);

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(virksomhet, "Skjenkebevilling", null, null, null, null, null,
            null, null, null, null, null, null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.KobleRegelverksreferanseAsync(tjeneste.Id, rettskildeId, "ukjent-eid"));
    }

    [Fact]
    public async Task Setter_status_til_publisert()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(virksomhet, "Skjenkebevilling", null, null, null, null, null,
            null, null, null, null, null, null, "Kari Jurist");

        var oppdatert = await register.SettStatusAsync(tjeneste.Id, "publisert", "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal("publisert", oppdatert!.Status);
    }

    [Fact]
    public async Task Ugyldig_status_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(virksomhet, "Skjenkebevilling", null, null, null, null, null,
            null, null, null, null, null, null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => register.SettStatusAsync(tjeneste.Id, "ukjent", "Kari Jurist"));
    }

    [Fact]
    public async Task Setter_status_med_godkjentAv_logges_i_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(virksomhet, "Skjenkebevilling", null, null, null, null, null,
            null, null, null, null, null, null, "Kari Jurist");

        await register.SettStatusAsync(tjeneste.Id, "validert", "Kari Jurist", godkjentAv: "Ola Fagansvarlig");

        var proveniens = await db.Proveniens
            .Where(p => p.EntitetId == tjeneste.Id && p.Handling == "validert")
            .SingleAsync();
        Assert.Equal("Ola Fagansvarlig", proveniens.GodkjentAv);
    }

    [Fact]
    public async Task Byggesteg5_oppretter_forslag_fra_ki_med_status_foreslatt_av_ai()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraKiAsync(
            virksomhet, "Stub-tjeneste (KI-forslag)", "Beskrivelse fra KI",
            kompetentMyndighet: null, output: null, tjenestetype: null, malgruppe: null, kanaler: null,
            kostnad: null, behandlingstid: null, kontaktpunkt: null, konsekvensVedBrudd: null, sprak: null,
            "system-ki", "stub-v1", """{"rettskildeIder":[],"lenkeIder":[]}""");

        Assert.Equal("foreslatt_av_ai", tjeneste.Status);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == tjeneste.Id);
        Assert.Equal("foreslatt_av_ai", proveniens.Handling);
        Assert.Equal("stub-v1", proveniens.AiForslagVersjon);
        Assert.NotNull(proveniens.KildeReferanserJson);
    }
}
