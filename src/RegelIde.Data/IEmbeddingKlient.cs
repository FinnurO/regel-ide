namespace RegelIde.Data;

/// <summary>
/// Abstraksjon over en ekstern embeddings-tjeneste (byggesteg 5 runde 4, RAG-spiken —
/// docs/14-byggesteg5-teknisk-design.md). Samme leverandøragnostiske mønster som
/// <see cref="IKiAgentKlient"/>: et fremtidig leverandørvalg krever kun en ny klasse bak dette
/// interfacet + en konfigurasjonsendring, ALDRI en endring i koden som konsumerer det
/// (<see cref="RettskildeEmbeddingTjeneste"/>/<see cref="RagKontekstHjelper"/>). Bevisst eget
/// interface, ikke en utvidelse av <see cref="IKiAgentKlient"/> — embeddings og chat-completions er
/// to ulike API-former (ett tall-array vs. fritekst) selv når samme leverandør tilbyr begge.
/// </summary>
/// <remarks>
/// Tar en LISTE av tekster, ikke én streng — bevisst, se docs/14 §8.4: en rå sammenligning mot en
/// ekte leverandør (HostYourAI) traff <c>429 Too Many Requests</c> når kall gikk ett-og-ett per node.
/// Batching (standard OpenAI <c>input</c>-som-array-format) er hovedgrunnen til denne signaturen.
/// Returverdien er i SAMME rekkefølge som <paramref name="tekster"/> var i input.
/// </remarks>
public interface IEmbeddingKlient
{
    Task<IReadOnlyList<double[]>> EmbedAsync(IReadOnlyList<string> tekster, CancellationToken ct = default);
}

/// <summary>
/// STUB (byggesteg 5 runde 4) — samme rolle som <see cref="KiAgentKlientStub"/>: beviser rørledningen
/// (RettskildeEmbeddingTjeneste/RagKontekstHjelper kan lagre/hente/rangere embeddings) UTEN en ekte,
/// betalt leverandør. Deterministisk, ren hash-basert bag-of-words-vektor — ikke en ekte semantisk
/// embedding, men to like/overlappende tekster gir likevel høyere kosinuslikhet enn to helt
/// forskjellige, nok til å bevise at retrieval-mekanismen fungerer. Skal erstattes av
/// <see cref="EmbeddingKlientOpenAiKompatibel"/> for et reelt resultat.
/// </summary>
public sealed class EmbeddingKlientStub : IEmbeddingKlient
{
    private const int Dimensjoner = 32;

    public Task<IReadOnlyList<double[]>> EmbedAsync(IReadOnlyList<string> tekster, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<double[]>>(tekster.Select(EmbedEn).ToList());

    private static double[] EmbedEn(string tekst)
    {
        var vektor = new double[Dimensjoner];
        foreach (var ord in tekst.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var indeks = (ord.GetHashCode() & int.MaxValue) % Dimensjoner;
            vektor[indeks] += 1;
        }
        return vektor;
    }
}
