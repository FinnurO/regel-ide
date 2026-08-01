using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>«Identifiser begrep» (byggesteg 5 runde 1, docs/06-veikart.md), mot ekte embedded Postgres og den ekte <see cref="KiAgentKlientStub"/>.</summary>
[Collection(DataTestCollection.Navn)]
public class BegrepsforslagTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BegrepsforslagTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Kjorer_forslag_oppretter_begrep_med_status_foreslatt_av_ai_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var forslagstjeneste = new BegrepsforslagTjeneste(db, new KiAgentKlientStub(), new BegrepsregisterTjeneste(db));
        var opprettede = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Single(opprettede);
        Assert.Equal("foreslatt_av_ai", opprettede[0].Status);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == opprettede[0].Id);
        Assert.Equal("foreslatt_av_ai", proveniens.Handling);
        Assert.NotNull(proveniens.AiForslagVersjon);
        Assert.Contains(rettskildeId.ToString(), proveniens.KildeReferanserJson);
    }

    [Fact]
    public async Task Ukjent_rettskilde_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var forslagstjeneste = new BegrepsforslagTjeneste(db, new KiAgentKlientStub(), new BegrepsregisterTjeneste(db));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(virksomhet, [Guid.NewGuid()], "system-ki"));
    }

    [Fact]
    public async Task Ingen_valgte_rettskilder_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var forslagstjeneste = new BegrepsforslagTjeneste(db, new KiAgentKlientStub(), new BegrepsregisterTjeneste(db));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(virksomhet, [], "system-ki"));
    }
}
