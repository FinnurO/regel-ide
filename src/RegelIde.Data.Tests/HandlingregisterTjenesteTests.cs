using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Handlingsregister (2026-08-20), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class HandlingregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public HandlingregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<(Guid Virksomhet, Guid Tjeneste)> NyTjenesteAsync(RegelIdeDbContext db, string navn = "Testkommunen")
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = navn });
        await db.SaveChangesAsync();
        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettAsync(
            virksomhet, "Serveringsbevilling", null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist");
        return (virksomhet, tjeneste.Id);
    }

    [Fact]
    public async Task Oppretter_handling_med_kanaler_og_hjemmel_og_kan_lese_dem_tilbake()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, tjenesteId) = await NyTjenesteAsync(db);
        var register = new HandlingregisterTjeneste(db);

        var handling = await register.OpprettAsync(
            virksomhet, tjenesteId, "Søknad om serveringsbevilling", "soke", "soknad_registrering", "soker",
            kanaler: [new HandlingKanalInput("elektronisk", null)],
            behandlingstid: new HandlingBehandlingstidInput("Senest 60 dager", new HandlingHjemmelInput("serveringsloven", "§ 10")),
            kostnad: null, vedlegg: [new HandlingVedleggInput("Skatteattest", "skatteattest", new HandlingHjemmelInput("serveringsloven", "§ 8"))],
            veiledningstekst: null, arsaker: null, resultat: null, merknad: null, opprettetAv: "Kari Jurist");

        Assert.Equal("utkast", handling.Status);
        Assert.Equal(1, handling.Versjon);

        var hentet = await register.FinnAsync(handling.Id);
        Assert.NotNull(hentet);
        Assert.Contains("\"elektronisk\"", hentet!.KanalerJson);
        Assert.Contains("serveringsloven", hentet.BehandlingstidJson);
        Assert.Contains("skatteattest", hentet.VedleggJson);

        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == handling.Id);
        Assert.Equal("handling", proveniens.EntitetType);
        Assert.Equal("opprettet", proveniens.Handling);
    }

    [Fact]
    public async Task Ukjent_handlingstype_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, tjenesteId) = await NyTjenesteAsync(db);
        var register = new HandlingregisterTjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, tjenesteId, "Noe", "ukjent-type", null, null,
            null, null, null, null, null, null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_utfort_av_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, tjenesteId) = await NyTjenesteAsync(db);
        var register = new HandlingregisterTjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, tjenesteId, "Noe", "soke", null, "ukjent-aktor",
            null, null, null, null, null, null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Oppretter_handling_pa_ukjent_tjeneste_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var register = new HandlingregisterTjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, Guid.NewGuid(), "Noe", "soke", null, null,
            null, null, null, null, null, null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Oppdaterer_handling_oker_versjon()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, tjenesteId) = await NyTjenesteAsync(db);
        var register = new HandlingregisterTjeneste(db);
        var handling = await register.OpprettAsync(
            virksomhet, tjenesteId, "Klage på vedtak", "klage", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist");

        var oppdatert = await register.OppdaterAsync(
            handling.Id, virksomhet, "Klage på vedtak", "klage", null, "soker",
            null, null, null, null,
            [new HandlingVeiledningstekstInput("Hvem behandler klagen?", "Statsforvalteren.", null)],
            null, null, "Endret", "Ola Fagansvarlig");

        Assert.NotNull(oppdatert);
        Assert.Equal(2, oppdatert!.Versjon);
        Assert.Contains("Statsforvalteren", oppdatert.VeiledningstekstJson);
    }

    // ---------- Sikkerhetsscoping (2026-08-20, samme runde som TjenesteregisterTjeneste-fiksen) ----------

    [Fact]
    public async Task Annen_virksomhet_kan_ikke_oppdatere_en_handling_pa_en_tjeneste_den_ikke_eier()
    {
        await using var db = _fixture.NyDbContext();
        var (eierVirksomhet, tjenesteId) = await NyTjenesteAsync(db, "Testkommunen");
        var annenVirksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = annenVirksomhet, Navn = "Bergen kommune" });
        await db.SaveChangesAsync();

        var register = new HandlingregisterTjeneste(db);
        var handling = await register.OpprettAsync(
            eierVirksomhet, tjenesteId, "Søknad", "soke", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist");

        var resultat = await register.OppdaterAsync(
            handling.Id, annenVirksomhet, "Kapret", "soke", null, "soker",
            null, null, null, null, null, null, null, null, "Ukjent Bruker");

        Assert.Null(resultat);
        var uendret = await register.FinnAsync(handling.Id);
        Assert.Equal("Søknad", uendret!.Navn);
    }

    [Fact]
    public async Task Annen_virksomhet_kan_ikke_slette_en_handling_pa_en_tjeneste_den_ikke_eier()
    {
        await using var db = _fixture.NyDbContext();
        var (eierVirksomhet, tjenesteId) = await NyTjenesteAsync(db, "Testkommunen");
        var annenVirksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = annenVirksomhet, Navn = "Bergen kommune" });
        await db.SaveChangesAsync();

        var register = new HandlingregisterTjeneste(db);
        var handling = await register.OpprettAsync(
            eierVirksomhet, tjenesteId, "Søknad", "soke", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist");

        Assert.False(await register.SlettAsync(handling.Id, annenVirksomhet));
        Assert.NotNull(await register.FinnAsync(handling.Id));
    }

    [Fact]
    public async Task Slett_fungerer_for_den_faktiske_eieren()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, tjenesteId) = await NyTjenesteAsync(db);
        var register = new HandlingregisterTjeneste(db);
        var handling = await register.OpprettAsync(
            virksomhet, tjenesteId, "Søknad", "soke", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist");

        Assert.True(await register.SlettAsync(handling.Id, virksomhet));
        Assert.Null(await register.FinnAsync(handling.Id));
    }

    [Fact]
    public async Task Lister_handlinger_for_tjeneste_sortert_pa_navn()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, tjenesteId) = await NyTjenesteAsync(db);
        var register = new HandlingregisterTjeneste(db);
        await register.OpprettAsync(virksomhet, tjenesteId, "Åpne sak", "soke", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist");
        await register.OpprettAsync(virksomhet, tjenesteId, "Avslutte sak", "avslutte", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist");

        var liste = await register.ListerForTjenesteAsync(tjenesteId);
        Assert.Equal(2, liste.Count);
        Assert.Equal("Avslutte sak", liste[0].Navn);
    }
}
