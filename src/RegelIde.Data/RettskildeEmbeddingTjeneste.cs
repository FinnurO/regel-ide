using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data;

/// <summary>
/// Sikrer embeddings for en rettskildes noder (byggesteg 5 runde 4, RAG-spiken) — lazy: beregner KUN
/// for noder som mangler en rad i <see cref="RettskildeNodeEmbeddingEntitet"/>, kalt fra
/// <see cref="RagKontekstHjelper"/> ved behov, ingen egen bakgrunnsjobb/oppstartsjobb denne runden.
/// Ingen invalidering hvis nodetekst endres ved reimport eller embeddings-modell byttes — se
/// docs/13-backlog.md, en av spikens eksplisitt utsatte punkter.
/// </summary>
public sealed class RettskildeEmbeddingTjeneste(RegelIdeDbContext db, IEmbeddingKlient embeddingKlient, IConfiguration config)
{
    // Byggesteg 5 runde 4, etterkant (docs/14 §8.4) — batcher flere node-tekster per embeddings-kall
    // i stedet for ett kall per node. En rå sammenligning mot en ekte leverandør (HostYourAI) traff
    // 429 Too Many Requests ved ~276 sekvensielle enkelt-node-kall for én rettskilde; batching
    // reduserer antall HTTP-kall med samme faktor. Ingen bekreftet grense fra HostYourAI selv på
    // hvor mange tekster per kall — 16 er et konservativt, ikke-tunet, forsiktig valg.
    private const int BatchStorrelse = 16;

    private string Modell => config["RegelIde:KiAgent:EmbeddingModell"] ?? "ukjent";

    public async Task SikreEmbeddingerAsync(Guid rettskildeId, CancellationToken ct = default)
    {
        // Kun ledd/punkt har Tekst (bladtekst, se RettskildeNodeEntitet-kommentaren) — kapittel/
        // paragraf-noder har ingenting å embedde og skal ikke ha en rad her.
        var noderUtenEmbedding = await db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.Tekst != null && n.Entitetsstatus == "gjeldende")
            .Where(n => !db.RettskildeNodeEmbeddinger.Any(e => e.NodeId == n.Id))
            .ToListAsync(ct);
        if (noderUtenEmbedding.Count == 0)
        {
            return;
        }

        for (var i = 0; i < noderUtenEmbedding.Count; i += BatchStorrelse)
        {
            var batch = noderUtenEmbedding.Skip(i).Take(BatchStorrelse).ToList();
            var vektorer = await embeddingKlient.EmbedAsync(batch.Select(n => n.Tekst!).ToList(), ct);
            for (var j = 0; j < batch.Count; j++)
            {
                db.RettskildeNodeEmbeddinger.Add(new RettskildeNodeEmbeddingEntitet
                {
                    NodeId = batch[j].Id,
                    Embedding = [.. vektorer[j]],
                    Modell = Modell,
                    OpprettetTidspunkt = DateTimeOffset.UtcNow,
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
