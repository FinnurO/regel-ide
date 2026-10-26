namespace RegelIde.Data.Tests;

/// <summary>[Ny, Johanns avgjørelse S3 på #397] «Hovedetterfølger ukjent» regnes ut av etterfølgerkantene, og en duplikat kan erstattes og
/// angres uten at noe slettes.</summary>
[Collection(DataTestCollection.Navn)]
public class EtterfolgerHullTests(EmbeddedPostgresFixture fixture)
{
    private static async Task<Virksomhet> AktorAsync(RegelIdeDbContext db, string navn, string? livsstatus = "nedlagt")
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = $"{navn} {Guid.NewGuid():N}", Livsstatus = livsstatus, Aktiv = livsstatus is null };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    private static async Task KantAsync(RegelIdeDbContext db, Guid fra, Guid til, string kilde, string status, string? begrunnelse = null)
    {
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = Strukturkanter.Relasjon, Typekode = "etterfolger", FraVirksomhetId = fra, TilVirksomhetId = til,
            Status = status, OppdagelsesKilde = kilde, Begrunnelse = begrunnelse, OpprettetAv = "Test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            KildeUtenforKorpusTekst = "Testvedtak", KildeUtenforKorpusType = "kgl_res", KildeUtenforKorpusDokumentasjon = "primaer",
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Delvis_eller_ingen_etterfolger_gir_hull_avledet_alle_eller_vurdert_fyller_det()
    {
        await using var db = fixture.NyDbContext();
        var y = await AktorAsync(db, "Mottaker", null);
        var ingen = await AktorAsync(db, "Ingen etterfølger");
        var delvis = await AktorAsync(db, "Delvis");
        var avledet = await AktorAsync(db, "Avledet");
        var alle = await AktorAsync(db, "Alle oppgavene");
        var manuell = await AktorAsync(db, "Manuell");
        await KantAsync(db, y.Id, delvis.Id, "monster:livslop-etterfolger-tekst", Strukturkanter.MaskineltForeslatt, "Regel: monster:livslop-overtar-fra");
        await KantAsync(db, y.Id, avledet.Id, "monster:livslop-etterfolger", Strukturkanter.MaskineltForeslatt);
        await KantAsync(db, y.Id, alle.Id, "monster:livslop-etterfolger-tekst", Strukturkanter.MaskineltForeslatt, "Regel: monster:livslop-overtar-alle-oppgavene-til");
        await KantAsync(db, y.Id, manuell.Id, "manuell", Strukturkanter.FagligVurdert);

        var hull = await EtterfolgerHull.HovedetterfolgerUkjentAsync(db, [ingen.Id, delvis.Id, avledet.Id, alle.Id, manuell.Id]);
        Assert.Equal(new HashSet<Guid> { ingen.Id, delvis.Id }, hull);
    }

    [Fact]
    public async Task Duplikat_erstattes_og_kan_angres()
    {
        await using var db = fixture.NyDbContext();
        var gammel = await AktorAsync(db, "Duplikat");
        var riktig = await AktorAsync(db, "Riktig");
        Assert.Equal(riktig.Id, (await AktorErstatning.SettAsync(db, gammel.Id, riktig.Id, "Test"))!.ErstattetAvId);
        await Assert.ThrowsAsync<ArgumentException>(() => AktorErstatning.SettAsync(db, riktig.Id, gammel.Id, "Test")); // erstattet kan ikke erstatte
        Assert.Null((await AktorErstatning.SettAsync(db, gammel.Id, null, "Test"))!.ErstattetAvId);
        Assert.True(db.Virksomheter.Any(v => v.Id == gammel.Id));
    }
}
