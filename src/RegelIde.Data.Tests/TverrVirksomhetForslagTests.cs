using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, 2026-08-28, import-wizard-runden] Dekker `OpprettForslagFraAnnenVirksomhetAsync` (både
/// Tjeneste- og Handling-varianten) — kjernen i "tverr-virksomhet import-forslag"-mekanismen.
/// End-til-ende-verifisering av selve import-wizard-flyten er gjort manuelt mot en kjørende server
/// (se sesjonsnotatet); disse testene dekker service-laget isolert.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class TverrVirksomhetForslagTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TverrVirksomhetForslagTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn)
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = navn });
        await db.SaveChangesAsync();
        return virksomhet;
    }

    [Fact]
    public async Task OpprettForslagFraAnnenVirksomhetAsync_setter_riktig_eierskap_status_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");

        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", "Skatteetaten", ["Personer som skal gifte seg"],
            null, "Kari Jurist");

        Assert.Equal(mal, tjeneste.VirksomhetId);
        Assert.Equal("foreslatt_av_annen_virksomhet", tjeneste.Status);

        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetType == "tjeneste" && p.EntitetId == tjeneste.Id);
        Assert.Equal("foreslatt_av_annen_virksomhet", proveniens.Handling);
        Assert.Equal(importor, proveniens.ForeslattAvVirksomhetId);
        Assert.Equal(mal, proveniens.VirksomhetId);
    }

    [Fact]
    public async Task Handling_OpprettForslagFraAnnenVirksomhetAsync_setter_riktig_status_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        var handling = await new HandlingregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, tjeneste.Id, "Søke om prøvingsattest", "soke", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist", importor);

        Assert.Equal("foreslatt_av_annen_virksomhet", handling.Status);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetType == "handling" && p.EntitetId == handling.Id);
        Assert.Equal(importor, proveniens.ForeslattAvVirksomhetId);
    }

    [Fact]
    public async Task Forslagskoen_filtrerer_pa_riktig_malvirksomhet_ikke_importorens_egen()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        await new TjenesteregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        var iMal = await db.Tjenester.Where(t => t.VirksomhetId == mal && t.Status == "foreslatt_av_annen_virksomhet").ToListAsync();
        var iImportor = await db.Tjenester.Where(t => t.VirksomhetId == importor && t.Status == "foreslatt_av_annen_virksomhet").ToListAsync();

        Assert.Single(iMal);
        Assert.Empty(iImportor);
    }

    /// <summary>
    /// [Ny, 2026-08-28] Selve gapet som utløste `SlettForslagAsync`: en importerende virksomhet kunne
    /// ikke rydde opp sine egne tverr-virksomhet-testforslag, siden `SettStatusAsync`s eierskapssjekk
    /// alene (VirksomhetId == bruker.VirksomhetId) blokkerer alt annet enn MÅL-virksomheten selv —
    /// oppdaget live under opprydding etter vielsesreise-importtesten.
    /// </summary>
    [Fact]
    public async Task SlettForslagAsync_lar_importoren_slette_sitt_eget_tverr_virksomhet_forslag()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        var slettet = await register.SlettForslagAsync(tjeneste.Id, importor);

        Assert.True(slettet);
        Assert.False(await db.Tjenester.AnyAsync(t => t.Id == tjeneste.Id));
    }

    [Fact]
    public async Task SlettForslagAsync_lar_ogsa_maal_virksomheten_selv_slette()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        var slettet = await register.SlettForslagAsync(tjeneste.Id, mal);

        Assert.True(slettet);
    }

    [Fact]
    public async Task SlettForslagAsync_nekter_en_tredje_virksomhet_uten_tilknytning()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var utenforstaende = await NyVirksomhetAsync(db, "UDI");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => register.SlettForslagAsync(tjeneste.Id, utenforstaende));
        Assert.True(await db.Tjenester.AnyAsync(t => t.Id == tjeneste.Id));
    }

    [Fact]
    public async Task SlettForslagAsync_nekter_en_tjeneste_som_ikke_lenger_er_et_ubehandlet_forslag()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");
        // Mål-virksomheten har godkjent forslaget — det er ikke lenger "ubehandlet".
        await register.SettStatusAsync(tjeneste.Id, mal, "validert", "Skatteetaten-saksbehandler");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => register.SlettForslagAsync(tjeneste.Id, importor));
        Assert.Contains("ubehandlet forslag", ex.Message);
        Assert.True(await db.Tjenester.AnyAsync(t => t.Id == tjeneste.Id));
    }

    [Fact]
    public async Task SlettForslagAsync_sletter_avhengigheter_og_handlinger_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");
        var handling = await new HandlingregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, tjeneste.Id, "Søke om prøvingsattest", "soke", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist", importor);
        var annenTjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Ekteskap (vigsel)", null, null, null, "Kari Jurist");
        await new TjenesteavhengighetregisterTjeneste(db).OpprettAsync(
            mal, tjeneste.Id, annenTjeneste.Id, "forutsetning_for", null, null, "Kari Jurist");

        var slettet = await register.SlettForslagAsync(tjeneste.Id, importor);

        Assert.True(slettet);
        Assert.False(await db.Handlinger.AnyAsync(h => h.Id == handling.Id));
        Assert.False(await db.Tjenesteavhengigheter.AnyAsync(a => a.FraTjenesteId == tjeneste.Id || a.TilTjenesteId == tjeneste.Id));
        Assert.False(await db.Proveniens.AnyAsync(p => p.EntitetType == "tjeneste" && p.EntitetId == tjeneste.Id));
        Assert.False(await db.Proveniens.AnyAsync(p => p.EntitetType == "handling" && p.EntitetId == handling.Id));
        // Motparten i avhengigheten selv skal IKKE bli slettet — kun tjenesten vi ba om å slette.
        Assert.True(await db.Tjenester.AnyAsync(t => t.Id == annenTjeneste.Id));
    }
}
