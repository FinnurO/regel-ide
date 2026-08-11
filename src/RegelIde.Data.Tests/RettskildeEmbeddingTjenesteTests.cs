using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data.Tests;

/// <summary>Byggesteg 5 runde 4 (RAG-spike), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class RettskildeEmbeddingTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public RettskildeEmbeddingTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Seeder direkte (uten LovdataKonverterer/Testdata) med UNIK, tilfeldig tekst per rad — i
    /// motsetning til de andre testene i denne test-suiten som deler samme, deterministisk
    /// gjenbrukte "alkoholloven"-fixture (RettskildeImportTjeneste gjenbruker eksisterende rettskilde/
    /// noder ved uendret reimport, se docs/08 "reimport-versionering"). Disse embedding-testene bryr
    /// seg om nøyaktig antall embedding-rader og deres innhold, og ville false positive/negative på
    /// delt state — derfor helt egne, garantert unike node-Id-er per test.
    /// </summary>
    private static async Task<Guid> NyRettskildeMedNoderAsync(RegelIdeDbContext db, int antallNoderMedTekst)
    {
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            // Importrolle "referanse" — slipper unna ck_rettskilder_akn_xml-sjekken (krever ellers
            // ekte AknXml-innhold, som denne testen ikke bryr seg om).
            Id = rettskildeId, Doctype = "act", Kildetype = "Lov", Importrolle = "referanse",
            Tittel = $"Testlov {rettskildeId}", Status = "Gjeldende", OpprettetAv = "system-test",
        });
        for (var i = 0; i < antallNoderMedTekst; i++)
        {
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = $"test/{Guid.NewGuid()}",
                KildeId = $"k-{Guid.NewGuid()}", NodeType = "ledd", Tekst = $"Unik tekst {Guid.NewGuid()}",
            });
        }
        await db.SaveChangesAsync();
        return rettskildeId;
    }

    /// <summary>Teller antall underliggende EmbedAsync-KALL (batcher) og antall TEKSTER embeddet
    /// totalt — brukes både til å bevise at SikreEmbeddingerAsync er lazy (ikke embedder på nytt for
    /// noder som allerede har en rad) og at den faktisk batcher (byggesteg 5 runde 4, etterkant,
    /// docs/14 §8.4 — en rå sammenligning mot en ekte leverandør traff 429 Too Many Requests da hvert
    /// kall kun embeddet én node).</summary>
    private sealed class TellendeEmbeddingKlient : IEmbeddingKlient
    {
        public int AntallKall { get; private set; }
        public int AntallTeksterEmbeddet { get; private set; }

        public Task<IReadOnlyList<double[]>> EmbedAsync(IReadOnlyList<string> tekster, CancellationToken ct = default)
        {
            AntallKall++;
            AntallTeksterEmbeddet += tekster.Count;
            return Task.FromResult<IReadOnlyList<double[]>>(tekster.Select(t => new double[] { t.Length }).ToList());
        }
    }

    [Fact]
    public async Task SikreEmbeddingerAsync_embedder_alle_noder_med_tekst_og_lagrer_modellnavn()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await NyRettskildeMedNoderAsync(db, antallNoderMedTekst: 3);

        var klient = new TellendeEmbeddingKlient();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RegelIde:KiAgent:EmbeddingModell"] = "test-modell" })
            .Build();
        var tjeneste = new RettskildeEmbeddingTjeneste(db, klient, config);

        await tjeneste.SikreEmbeddingerAsync(rettskildeId);

        Assert.Equal(3, klient.AntallTeksterEmbeddet);
        var embeddinger = await db.RettskildeNodeEmbeddinger
            .Where(e => db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).Select(n => n.Id).Contains(e.NodeId))
            .ToListAsync();
        Assert.Equal(3, embeddinger.Count);
        Assert.All(embeddinger, e => Assert.Equal("test-modell", e.Modell));
    }

    [Fact]
    public async Task SikreEmbeddingerAsync_er_lazy_embedder_ikke_pa_nytt_for_noder_som_allerede_har_en()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await NyRettskildeMedNoderAsync(db, antallNoderMedTekst: 2);

        var klient = new TellendeEmbeddingKlient();
        var tjeneste = new RettskildeEmbeddingTjeneste(db, klient, new ConfigurationBuilder().Build());

        await tjeneste.SikreEmbeddingerAsync(rettskildeId);
        Assert.Equal(2, klient.AntallTeksterEmbeddet);

        await tjeneste.SikreEmbeddingerAsync(rettskildeId);

        Assert.Equal(2, klient.AntallTeksterEmbeddet); // ingen nye kall andre gang
    }

    [Fact]
    public async Task SikreEmbeddingerAsync_batcher_flere_noder_per_underliggende_kall()
    {
        await using var db = _fixture.NyDbContext();
        // 20 noder, batchstørrelse 16 (se RettskildeEmbeddingTjeneste) — skal gi 2 underliggende
        // EmbedAsync-kall (16 + 4), ikke 20, uavhengig av kravet om at alle 20 faktisk blir embeddet.
        var rettskildeId = await NyRettskildeMedNoderAsync(db, antallNoderMedTekst: 20);

        var klient = new TellendeEmbeddingKlient();
        var tjeneste = new RettskildeEmbeddingTjeneste(db, klient, new ConfigurationBuilder().Build());

        await tjeneste.SikreEmbeddingerAsync(rettskildeId);

        Assert.Equal(20, klient.AntallTeksterEmbeddet);
        Assert.Equal(2, klient.AntallKall);
    }
}
