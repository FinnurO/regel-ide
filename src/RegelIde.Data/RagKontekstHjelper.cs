using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// RAG-spike-motstykke til <see cref="RettskildeKontekstHjelper"/> (byggesteg 5 runde 4,
/// docs/14-byggesteg5-teknisk-design.md — "RAG-spike"). I stedet for å dumpe ALL bladtekst fra
/// valgte rettskilder, embedder denne <paramref name="sporsmalTekst"/> (kunnskapsbibliotekets
/// sammenslåtte lenke-/fil-tekst) og returnerer kun de <paramref name="k"/> nodene med høyest
/// kosinuslikhet, formatert IDENTISK med <see cref="RettskildeKontekstHjelper"/>s "[eId] Tekst"-
/// format — agent-siden av koden (JSON-parsing, eId-validering) trenger ikke vite hvilken
/// kontekstbygger som ble brukt. Erstatter IKKE <see cref="RettskildeKontekstHjelper"/> — de finnes
/// side ved side for rå sammenligning denne runden, se
/// <see cref="TjenesteforslagTjeneste.KjorForslagMedRagAsync"/>.
/// </summary>
internal static class RagKontekstHjelper
{
    public static async Task<string> ByggKontekstAsync(
        RegelIdeDbContext db, IReadOnlyList<Guid> rettskildeIder, RettskildeEmbeddingTjeneste embeddingTjeneste,
        IEmbeddingKlient embeddingKlient, string sporsmalTekst, int k, CancellationToken ct)
    {
        if (rettskildeIder.Count == 0)
        {
            throw new ArgumentException("Minst én rettskilde må velges. Ingen gjettet fallback.");
        }

        var rettskilder = await db.Rettskilder
            .Where(r => rettskildeIder.Contains(r.Id) && r.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        if (rettskilder.Count != rettskildeIder.Distinct().Count())
        {
            throw new ArgumentException("En eller flere valgte rettskilder finnes ikke.");
        }

        // Lazy: sikrer embeddings for hver valgt rettskilde FØR henting — ingen eget forhåndssteg
        // brukeren må huske å kjøre selv (byggesteg 5 runde 4, se RettskildeEmbeddingTjeneste).
        foreach (var rettskildeId in rettskildeIder.Distinct())
        {
            await embeddingTjeneste.SikreEmbeddingerAsync(rettskildeId, ct);
        }

        var noder = await db.RettskildeNoder
            .Where(n => rettskildeIder.Contains(n.RettskildeId) && n.Tekst != null)
            .OrderBy(n => n.RettskildeId).ThenBy(n => n.Sorteringsrekkefolge)
            .ToListAsync(ct);
        var nodeIder = noder.Select(n => n.Id).ToList();
        var embeddinger = await db.RettskildeNodeEmbeddinger
            .Where(e => nodeIder.Contains(e.NodeId))
            .ToDictionaryAsync(e => e.NodeId, e => e.Embedding, ct);

        var sporsmalVektor = (await embeddingKlient.EmbedAsync([sporsmalTekst], ct))[0];

        var topK = noder
            .Where(n => embeddinger.ContainsKey(n.Id))
            .Select(n => new { Node = n, Likhet = KosinusLikhet(sporsmalVektor, embeddinger[n.Id]) })
            .OrderByDescending(x => x.Likhet)
            .Take(k)
            .Select(x => x.Node.Id)
            .ToHashSet();

        // Grupperes fortsatt per rettskilde og beholder Sorteringsrekkefolge (ikke ren likhets-
        // rangert rekkefølge) — samme lesbarhetsbegrunnelse som RettskildeKontekstHjelper, ellers
        // hopper konteksten usammenhengende mellom paragrafer.
        var sb = new StringBuilder();
        foreach (var rettskilde in rettskilder)
        {
            var valgteNoder = noder.Where(n => n.RettskildeId == rettskilde.Id && topK.Contains(n.Id)).ToList();
            if (valgteNoder.Count == 0) continue; // ingen av de K mest like nodene kom fra denne rettskilden
            sb.AppendLine($"# {rettskilde.Tittel}");
            foreach (var node in valgteNoder)
            {
                sb.AppendLine($"[{node.Eid}] {node.Tekst}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Standard kosinuslikhet — ren C#, ingen ny avhengighet (se docs/14 §RAG-spike for hvorfor ikke
    /// pgvector). Returnerer 0 (degraderer, kaster ikke) hvis vektorene har ulik dimensjon (f.eks.
    /// embeddings-modellen ble byttet mellom lagring og spørsmål) — en reell, udekket svakhet
    /// dokumentert som utsatt i docs/13-backlog.md, ikke noe denne spiken skal krasje på.
    /// </summary>
    internal static double KosinusLikhet(double[] a, List<double> b)
    {
        var n = Math.Min(a.Length, b.Count);
        if (n == 0) return 0;
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA == 0 || normB == 0) return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
