using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>[Ny, issue #286] Se TjenesteRegelverksreferanseforslagTjeneste sin klassekommentar — samme
/// "ekte embedded Postgres + ekte KiAgentKlientStub/EmbeddingKlientStub"-mønster som
/// TjenesteforslagTjenesteTests.</summary>
[Collection(DataTestCollection.Navn)]
public class TjenesteRegelverksreferanseforslagTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TjenesteRegelverksreferanseforslagTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static TjenesteRegelverksreferanseforslagTjeneste NyTjeneste(RegelIdeDbContext db) =>
        new(db, new KiAgentKlientStub(), new TjenesteregisterTjeneste(db),
            new RettskildeEmbeddingTjeneste(db, new EmbeddingKlientStub(), new ConfigurationBuilder().Build()),
            new EmbeddingKlientStub(), new ConfigurationBuilder().Build());

    private static async Task<(Guid VirksomhetId, Guid RettskildeId, Guid TjenesteId)> ByggTestoppsettAsync(RegelIdeDbContext db)
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        var tjeneste = new TjenesteEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhet, Tittel = "Skjenkebevilling", Beskrivelse = "Søknad om skjenkebevilling",
            Status = "utkast", OpprettetAv = "system-ki",
        };
        db.Tjenester.Add(tjeneste);
        await db.SaveChangesAsync();
        return (virksomhet, rettskildeId, tjeneste.Id);
    }

    [Fact]
    public async Task Kjorer_forslag_oppretter_ventende_forslagsrad_for_tjeneste_uten_referanse()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, tjenesteId) = await ByggTestoppsettAsync(db);

        var forslagstjeneste = NyTjeneste(db);
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Equal(1, resultat.AntallTjenesterUtenReferanse);
        Assert.Equal(1, resultat.AntallVurdert);
        Assert.Equal(1, resultat.AntallNyeForslag);

        var forslag = await db.TjenesteRegelverksreferanseForslag.SingleAsync(f => f.TjenesteId == tjenesteId);
        Assert.Equal("Venter", forslag.Status);
        Assert.Equal(rettskildeId, forslag.TilRettskildeId);
        Assert.NotNull(forslag.Begrunnelse);
        Assert.Equal("stub-v1", forslag.AiForslagVersjon);
        Assert.Contains(rettskildeId.ToString(), forslag.KildeReferanserJson);
    }

    [Fact]
    public async Task Tjeneste_med_eksisterende_regelverksreferanse_foreslas_ikke()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, tjenesteId) = await ByggTestoppsettAsync(db);
        var node = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == rettskildeId && n.Tekst != null);
        await new TjenesteregisterTjeneste(db).KobleRegelverksreferanseAsync(tjenesteId, rettskildeId, node.Eid);

        var forslagstjeneste = NyTjeneste(db);
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Equal(0, resultat.AntallTjenesterUtenReferanse);
        Assert.Equal(0, resultat.AntallNyeForslag);
        Assert.False(await db.TjenesteRegelverksreferanseForslag.AnyAsync(f => f.TjenesteId == tjenesteId));
    }

    [Fact]
    public async Task Kjort_forslag_pa_nytt_oppretter_ingen_dublett()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, _) = await ByggTestoppsettAsync(db);

        var forslagstjeneste = NyTjeneste(db);
        await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");
        var andreKjoring = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Equal(0, andreKjoring.AntallNyeForslag); // fortsatt "uten referanse" (forslag != ekte kobling), men allerede foreslått
        Assert.Single(await db.TjenesteRegelverksreferanseForslag.ToListAsync());
    }

    [Fact]
    public async Task Godkjenn_oppretter_ekte_regelverksreferanse_og_setter_status_godkjent()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, tjenesteId) = await ByggTestoppsettAsync(db);
        var forslagstjeneste = NyTjeneste(db);
        await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");
        var forslag = await db.TjenesteRegelverksreferanseForslag.SingleAsync(f => f.TjenesteId == tjenesteId);

        var godkjent = await forslagstjeneste.GodkjennAsync(forslag.Id, "Kari Jurist");

        Assert.NotNull(godkjent);
        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.Equal("Kari Jurist", godkjent.BehandletAv);
        Assert.NotNull(godkjent.BehandletTidspunkt);
        var ekteReferanse = await db.TjenesteRegelverksreferanser.SingleAsync(r => r.TjenesteId == tjenesteId);
        Assert.Equal(forslag.TilEid, ekteReferanse.TilEid);
    }

    [Fact]
    public async Task Avvis_setter_status_avvist_uten_a_opprette_ekte_kobling()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, tjenesteId) = await ByggTestoppsettAsync(db);
        var forslagstjeneste = NyTjeneste(db);
        await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");
        var forslag = await db.TjenesteRegelverksreferanseForslag.SingleAsync(f => f.TjenesteId == tjenesteId);

        var avvist = await forslagstjeneste.AvvisAsync(forslag.Id, "Kari Jurist");

        Assert.Equal("Avvist", avvist!.Status);
        Assert.False(await db.TjenesteRegelverksreferanser.AnyAsync(r => r.TjenesteId == tjenesteId));
    }

    [Fact]
    public async Task Godkjenn_av_allerede_behandlet_forslag_kaster()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, tjenesteId) = await ByggTestoppsettAsync(db);
        var forslagstjeneste = NyTjeneste(db);
        await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");
        var forslag = await db.TjenesteRegelverksreferanseForslag.SingleAsync(f => f.TjenesteId == tjenesteId);
        await forslagstjeneste.AvvisAsync(forslag.Id, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => forslagstjeneste.GodkjennAsync(forslag.Id, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_rettskilde_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var forslagstjeneste = NyTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(virksomhet, [Guid.NewGuid()], "system-ki"));
    }

    [Fact]
    public async Task ListerMedTjenesteAsync_default_viser_kun_ventende()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, tjenesteId) = await ByggTestoppsettAsync(db);
        var forslagstjeneste = NyTjeneste(db);
        await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");
        var forslag = await db.TjenesteRegelverksreferanseForslag.SingleAsync(f => f.TjenesteId == tjenesteId);
        await forslagstjeneste.AvvisAsync(forslag.Id, "Kari Jurist");

        var kun_venter = await forslagstjeneste.ListerMedTjenesteAsync(virksomhet);
        var alle = await forslagstjeneste.ListerMedTjenesteAsync(virksomhet, status: null); // "Alle" er en Program.cs-endepunkt-konvensjon, tjenestelaget selv bruker null for "ingen statusfilter"

        Assert.Empty(kun_venter);
        Assert.Single(alle);
        Assert.Equal("Skjenkebevilling", alle[0].Tjeneste.Tittel);
    }
}
