using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>Kodelisteregister / verdidomene (docs/03-domenemodell.md §1.4), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class KodelisteregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KodelisteregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // Kode har en ekte, global unique-indeks (ux_kodelister_kode) — og EmbeddedPostgresFixture er en
    // ÉN delt Postgres-instans på tvers av ALLE testklasser i DataTestCollection (ikke isolert per
    // testklasse/-metode). Hver test som faktisk OPPRETTER en kodeliste (til forskjell fra tester som
    // forventer at opprettelsen kastes før noe lagres) må derfor bruke en unik kode, ellers kolliderer
    // testene med hverandre avhengig av kjørerekkefølge.
    private static string NyKode(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    [Fact]
    public async Task Oppretter_juridisk_kodeliste_med_koder()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new KodelisteregisterTjeneste(db);
        var kodeliste = await register.OpprettAsync(virksomhet, NyKode("KL-TEST"), "Test", "juridisk", null, null, null, "Kari Jurist");
        await register.LeggTilKodeAsync(kodeliste.Id, "kode-a", "Kode A", "Beskrivelse", null, null);

        var funnet = await register.FinnAsync(kodeliste.Id);
        Assert.NotNull(funnet);
        Assert.Single(funnet!.Koder);
        Assert.Equal("utkast", funnet.Status);
    }

    [Fact]
    public async Task AlleAsync_inkluderer_koder_ikke_bare_kodelisten()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new KodelisteregisterTjeneste(db);
        var kodeliste = await register.OpprettAsync(virksomhet, NyKode("KL-ALLE-TEST"), "Test", "juridisk", null, null, null, "Kari Jurist");
        await register.LeggTilKodeAsync(kodeliste.Id, "kode-a", "Kode A", null, null, null);

        var alle = await register.AlleAsync();

        Assert.Single(alle.Single(k => k.Id == kodeliste.Id).Koder);
    }

    [Fact]
    public async Task Juridisk_uten_virksomhet_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var register = new KodelisteregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(null, "KL-TEST", "Test", "juridisk", null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ekstern_referanse_med_virksomhet_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new KodelisteregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "KL-EKSTERN", "Test", "ekstern-referanse", null, "https://data.norge.no/x", "1.0", "Kari Jurist"));
    }

    [Fact]
    public async Task Ekstern_referanse_uten_kilde_uri_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var register = new KodelisteregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(null, "KL-EKSTERN", "Test", "ekstern-referanse", null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Oppretter_ekstern_referanse_som_publisert()
    {
        await using var db = _fixture.NyDbContext();
        var register = new KodelisteregisterTjeneste(db);
        var kodeliste = await register.OpprettAsync(
            null, NyKode("KL-KOMMUNENUMMER"), "Kommunenummer (SSB)", "ekstern-referanse", null, "https://data.ssb.no/kommuner", "2026", "Kari Jurist");

        Assert.Equal("publisert", kodeliste.Status);
        Assert.Null(kodeliste.VirksomhetId);
    }

    [Fact]
    public async Task Ekstern_referanse_kan_ikke_endre_status()
    {
        await using var db = _fixture.NyDbContext();
        var register = new KodelisteregisterTjeneste(db);
        var kodeliste = await register.OpprettAsync(
            null, NyKode("KL-KOMMUNENUMMER"), "Kommunenummer (SSB)", "ekstern-referanse", null, "https://data.ssb.no/kommuner", "2026", "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => register.SettStatusAsync(kodeliste.Id, "arkivert", "Kari Jurist"));
    }

    [Fact]
    public async Task Juridisk_grunnlag_kun_for_juridisk_type_kastes_for_teknisk()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        var node = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == rettskildeId && n.NodeType == "paragraf");

        var register = new KodelisteregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "KL-TEKNISK", "Teknisk", "teknisk", node.Eid, null, null, "Anne Systemforvalter"));
    }

    [Fact]
    public async Task Duplisert_kode_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new KodelisteregisterTjeneste(db);
        var kode = NyKode("KL-TEST");
        await register.OpprettAsync(virksomhet, kode, "Test", "juridisk", null, null, null, "Kari Jurist");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, kode, "Duplikat", "juridisk", null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Fjerner_kode()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new KodelisteregisterTjeneste(db);
        var kodeliste = await register.OpprettAsync(virksomhet, NyKode("KL-TEST"), "Test", "juridisk", null, null, null, "Kari Jurist");
        var kode = await register.LeggTilKodeAsync(kodeliste.Id, "kode-a", "Kode A", null, null, null);

        var slettet = await register.FjernKodeAsync(kode!.Id);

        Assert.True(slettet);
        var funnet = await register.FinnAsync(kodeliste.Id);
        Assert.Empty(funnet!.Koder);
    }
}
