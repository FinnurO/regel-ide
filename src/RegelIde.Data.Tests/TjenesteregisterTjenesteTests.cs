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
            ["Virksomheter"], ["Digitalt"], "Gebyr", "3 måneder", "Skjenkekontoret", "Inndragning", ["nb"],
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

        var oppdatert = await register.OppdaterAsync(tjeneste.Id, virksomhet, "Alminnelig skjenkebevilling", "Ny beskrivelse",
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

        var oppdatert = await register.SettStatusAsync(tjeneste.Id, virksomhet, "publisert", "Kari Jurist");

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

        await Assert.ThrowsAsync<ArgumentException>(() => register.SettStatusAsync(tjeneste.Id, virksomhet, "ukjent", "Kari Jurist"));
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

        await register.SettStatusAsync(tjeneste.Id, virksomhet, "validert", "Kari Jurist", godkjentAv: "Ola Fagansvarlig");

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

    // ---------- Cross-tenant søk (2026-08-19, feature/tjenesteavhengighet-ekstern-referanse) ----------

    [Fact]
    public async Task SokTverrTenant_finner_kun_publiserte_tjenester_fra_andre_virksomheter()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetA = Guid.NewGuid();
        var virksomhetB = Guid.NewGuid();
        db.Virksomheter.AddRange(
            new Virksomhet { Id = virksomhetA, Navn = "Testkommunen A" },
            new Virksomhet { Id = virksomhetB, Navn = "Mattilsynet" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var publisert = await register.OpprettAsync(
            virksomhetB, "Registrer matbedriften din", null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist");
        await register.SettStatusAsync(publisert.Id, virksomhetB, "publisert", "Kari Jurist");

        var utkast = await register.OpprettAsync(
            virksomhetB, "Registrer et internt utkast", null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist");
        // utkast forblir Status="utkast" — skal IKKE være søkbar tverr-tenant.

        var treffPublisert = await register.SokTverrTenantAsync("Registrer matbedriften");
        var funnet = Assert.Single(treffPublisert);
        Assert.Equal(publisert.Id, funnet.Id);
        Assert.Equal("Mattilsynet", funnet.VirksomhetNavn);

        var treffUtkast = await register.SokTverrTenantAsync("internt utkast");
        Assert.Empty(treffUtkast);
    }

    [Fact]
    public async Task SokTverrTenant_er_case_insensitiv_substring_pa_tittel()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Mattilsynet" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(
            virksomhet, "Vandelskontroll fra Politiet", null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist");
        await register.SettStatusAsync(tjeneste.Id, virksomhet, "publisert", "Kari Jurist");

        var treff = await register.SokTverrTenantAsync("vandelskontroll");
        Assert.Contains(treff, t => t.Id == tjeneste.Id);
    }

    [Fact]
    public async Task SokTverrTenant_tom_sokestreng_gir_ingen_treff()
    {
        await using var db = _fixture.NyDbContext();
        var register = new TjenesteregisterTjeneste(db);
        Assert.Empty(await register.SokTverrTenantAsync("   "));
    }

    // ---------- Sikkerhetsfiks 2026-08-20 (docs/17 §2.2, docs/18 §D.7) ----------

    [Fact]
    public async Task Annen_virksomhet_kan_ikke_oppdatere_en_tjeneste_den_ikke_eier()
    {
        await using var db = _fixture.NyDbContext();
        var eier = Guid.NewGuid();
        var annen = Guid.NewGuid();
        db.Virksomheter.AddRange(
            new Virksomhet { Id = eier, Navn = "Testkommunen" },
            new Virksomhet { Id = annen, Navn = "Bergen kommune" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(eier, "Serveringsbevilling", null, null, null, null, null,
            null, null, null, null, null, null, "Kari Jurist");

        var resultat = await register.OppdaterAsync(tjeneste.Id, annen, "Kapret tittel", null, null, null, null,
            null, null, null, null, null, null, null, "Ukjent Bruker");

        Assert.Null(resultat);
        var uendret = await register.FinnAsync(tjeneste.Id);
        Assert.Equal("Serveringsbevilling", uendret!.Tittel);
    }

    [Fact]
    public async Task Annen_virksomhet_kan_ikke_sette_status_pa_en_tjeneste_den_ikke_eier()
    {
        await using var db = _fixture.NyDbContext();
        var eier = Guid.NewGuid();
        var annen = Guid.NewGuid();
        db.Virksomheter.AddRange(
            new Virksomhet { Id = eier, Navn = "Testkommunen" },
            new Virksomhet { Id = annen, Navn = "Bergen kommune" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(eier, "Serveringsbevilling", null, null, null, null, null,
            null, null, null, null, null, null, "Kari Jurist");

        var resultat = await register.SettStatusAsync(tjeneste.Id, annen, "publisert", "Ukjent Bruker");

        Assert.Null(resultat);
        var uendret = await register.FinnAsync(tjeneste.Id);
        Assert.Equal("utkast", uendret!.Status);
    }

    [Fact]
    public async Task Eieren_selv_kan_fortsatt_oppdatere_og_sette_status()
    {
        await using var db = _fixture.NyDbContext();
        var eier = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = eier, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettAsync(eier, "Serveringsbevilling", null, null, null, null, null,
            null, null, null, null, null, null, "Kari Jurist");

        var oppdatert = await register.OppdaterAsync(tjeneste.Id, eier, "Ny tittel", null, null, null, null,
            null, null, null, null, null, null, null, "Kari Jurist");
        Assert.NotNull(oppdatert);
        Assert.Equal("Ny tittel", oppdatert!.Tittel);

        var medStatus = await register.SettStatusAsync(tjeneste.Id, eier, "publisert", "Kari Jurist");
        Assert.NotNull(medStatus);
        Assert.Equal("publisert", medStatus!.Status);
    }
}
