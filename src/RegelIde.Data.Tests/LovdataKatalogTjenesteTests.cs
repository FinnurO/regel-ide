using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// Byggesteg 5 runde 2 (Lovdata-katalog/søk) — ekte nettverkskall mot Lovdatas bulk-API, samme kultur
/// som <see cref="LovdataBulkHenterTests"/>. Katalogtabellen er GLOBAL (ingen virksomhet-scoping,
/// siden Lovdata-innholdet er nasjonalt/delt) — hver test nullstiller derfor eksplisitt tabellen selv
/// før den setter opp sin egen kjente tilstand, i stedet for å anta en tom tabell (delt embedded
/// Postgres på tvers av hele testkjøringen, se DataTestCollection).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class LovdataKatalogTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public LovdataKatalogTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Sok_bygger_katalogen_ved_forste_kall_og_finner_alkoholloven()
    {
        await using var db = _fixture.NyDbContext();
        await db.LovdataKatalogOppforinger.ExecuteDeleteAsync();

        using var http = new HttpClient();
        var tjeneste = new LovdataKatalogTjeneste(db, new LovdataBulkHenter(http));

        var treff = await tjeneste.SokAsync("alkohol");

        Assert.NotEmpty(treff);
        Assert.Contains(treff, t => t.Type == "lov");
        Assert.All(treff, t => Assert.Contains("alkohol", t.Tittel, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Frisk_katalog_utloser_ikke_ny_bygging()
    {
        await using var db = _fixture.NyDbContext();
        await db.LovdataKatalogOppforinger.ExecuteDeleteAsync();
        db.LovdataKatalogOppforinger.Add(new LovdataKatalogOppforingEntitet
        {
            Datokode = "LOV-9999-01-01", Tittel = "Syntetisk frisk testrad", Type = "lov", SistOppdatert = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        using var http = new HttpClient();
        var tjeneste = new LovdataKatalogTjeneste(db, new LovdataBulkHenter(http));

        await tjeneste.SikreOppdatertKatalogAsync();

        var antall = await db.LovdataKatalogOppforinger.CountAsync();
        Assert.Equal(1, antall); // ingen ekte nettverkskall skjedde — katalogen var fersk
    }

    [Fact]
    public async Task Foreldet_katalog_bygges_pa_nytt_fra_ekte_data()
    {
        await using var db = _fixture.NyDbContext();
        await db.LovdataKatalogOppforinger.ExecuteDeleteAsync();
        db.LovdataKatalogOppforinger.Add(new LovdataKatalogOppforingEntitet
        {
            Datokode = "LOV-9999-01-01", Tittel = "Syntetisk foreldet testrad", Type = "lov",
            SistOppdatert = DateTimeOffset.UtcNow - TimeSpan.FromHours(25),
        });
        await db.SaveChangesAsync();

        using var http = new HttpClient();
        var tjeneste = new LovdataKatalogTjeneste(db, new LovdataBulkHenter(http));

        await tjeneste.SikreOppdatertKatalogAsync();

        var alle = await db.LovdataKatalogOppforinger.ToListAsync();
        Assert.True(alle.Count > 100, "Forventet at foreldet katalog ble erstattet med ekte Lovdata-innhold.");
        Assert.DoesNotContain(alle, o => o.Datokode == "LOV-9999-01-01");
    }
}
