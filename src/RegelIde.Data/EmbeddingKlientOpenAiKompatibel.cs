using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data;

/// <summary>
/// Ekte <see cref="IEmbeddingKlient"/>-implementasjon (byggesteg 5 runde 4, RAG-spiken) mot ETHVERT
/// OpenAI-kompatibelt <c>/v1/embeddings</c>-API — samme leverandøragnostiske mønster og feilstil som
/// <see cref="KiAgentKlientOpenAiKompatibel"/> ("ingen gjettet fallback"). Endepunkt/modell er egen
/// konfigurasjon (<c>RegelIde:KiAgent:EmbeddingBaseUrl</c>/<c>EmbeddingModell</c>) — bevisst IKKE
/// samme <c>BaseUrl</c>/<c>Modell</c> som chat-completions-klienten, siden en leverandør typisk har
/// separate URL-er/modellnavn for de to API-formene selv når den tilbyr begge. <c>ApiKey</c>
/// gjenbrukes derimot fra samme <c>RegelIde:KiAgent:ApiKey</c> — én nøkkel per leverandør er den
/// vanlige modellen.
/// </summary>
/// <remarks>
/// Byggesteg 5 runde 4, etterkant (2026-08-10, docs/14 §8.4): den rå sammenligningen mot en ekte
/// leverandør (HostYourAI) traff <c>429 Too Many Requests</c> når <see cref="RettskildeEmbeddingTjeneste"/>
/// kalte dette ETT-OG-ETT per node (~276 sekvensielle kall for en middels rettskilde). Interfacet tar
/// derfor en LISTE av tekster og sender dem i ett kall (standard OpenAI <c>input</c>-som-array-format
/// — ikke leverandørspesifikt), pluss enkel retry-med-backoff på 429 — samme "ekte, observert
/// produksjonsproblem, ikke en gjettet fremtidig bekymring"-begrunnelse som cycle-sjekk-fiksen i
/// <see cref="TjenesteavhengighetregisterTjeneste"/> runde 4.
/// </remarks>
public sealed class EmbeddingKlientOpenAiKompatibel(HttpClient http, IConfiguration config) : IEmbeddingKlient
{
    // Tre forsøk, doblende forsinkelse (300ms, 600ms) — samme "reelle, men lave" forsinkelser som
    // EmbeddedPostgresHjelper.VentTilKlarAsync bruker i tester i dag; holder testsuiten rask (< 1s
    // ekstra i verste fall) samtidig som det er en reell backoff i produksjon.
    private const int MaksAntallForsok = 3;
    private const int ForsteForsinkelseMs = 300;

    public async Task<IReadOnlyList<double[]>> EmbedAsync(IReadOnlyList<string> tekster, CancellationToken ct = default)
    {
        if (tekster.Count == 0)
        {
            return [];
        }

        var baseUrl = config["RegelIde:KiAgent:EmbeddingBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "RegelIde:KiAgent:EmbeddingBaseUrl er ikke satt. Kjør 'dotnet user-secrets set \"RegelIde:KiAgent:EmbeddingBaseUrl\" \"<leverandørens embeddings-URL>\"' fra src/RegelIde.Api. Ingen gjettet fallback.");
        }
        var modell = config["RegelIde:KiAgent:EmbeddingModell"];
        if (string.IsNullOrWhiteSpace(modell))
        {
            throw new InvalidOperationException(
                "RegelIde:KiAgent:EmbeddingModell er ikke satt. Ingen gjettet fallback — dette skal ikke defaultes til en bestemt leverandørs modell.");
        }
        var apiNokkel = config["RegelIde:KiAgent:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiNokkel))
        {
            throw new InvalidOperationException(
                "RegelIde:KiAgent:ApiKey er ikke satt. Kjør 'dotnet user-secrets set \"RegelIde:KiAgent:ApiKey\" \"<nøkkel>\"' fra src/RegelIde.Api. Ingen gjettet fallback.");
        }

        string responsBody;
        HttpStatusCode statusCode;
        var forsinkelseMs = ForsteForsinkelseMs;
        var forsok = 0;
        while (true)
        {
            forsok++;
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = JsonContent.Create(new { model = modell, input = tekster }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiNokkel);

            using var response = await http.SendAsync(request, ct);
            statusCode = response.StatusCode;
            responsBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                break;
            }
            if (statusCode != HttpStatusCode.TooManyRequests || forsok >= MaksAntallForsok)
            {
                throw new InvalidOperationException(
                    $"Embeddings-kallet mot '{baseUrl}' feilet ({(int)statusCode} {statusCode}) etter {forsok} forsøk: {responsBody}");
            }
            // 429 — leverandøren ber oss vente, ikke en varig feil. Doblende backoff, ikke ny
            // konfigurasjon nødvendig (se docs/14 §8.4).
            await Task.Delay(forsinkelseMs, ct);
            forsinkelseMs *= 2;
        }

        using var dokument = JsonDocument.Parse(responsBody);
        if (!dokument.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() != tekster.Count)
        {
            throw new InvalidOperationException(
                $"Responsen fra '{baseUrl}' inneholdt ikke like mange embeddings ({(dokument.RootElement.TryGetProperty("data", out var d) ? d.GetArrayLength() : 0)}) som tekster sendt inn ({tekster.Count}). Rå respons: {responsBody}");
        }

        // "index"-feltet (OpenAI-spesifikasjonen) forteller hvilken inputposisjon hver rad svarer på —
        // ikke alle OpenAI-kompatible leverandører garanterer at "data" kommer i samme rekkefølge som
        // "input" ble sendt i, spesielt ved batching. Faller tilbake til array-posisjon hvis feltet
        // mangler.
        var resultat = new double[tekster.Count][];
        for (var i = 0; i < data.GetArrayLength(); i++)
        {
            var element = data[i];
            var indeks = element.TryGetProperty("index", out var idxProp) && idxProp.TryGetInt32(out var idx) ? idx : i;
            var embeddingElement = element.GetProperty("embedding");
            var vektor = new double[embeddingElement.GetArrayLength()];
            for (var j = 0; j < vektor.Length; j++)
            {
                vektor[j] = embeddingElement[j].GetDouble();
            }
            resultat[indeks] = vektor;
        }
        return resultat;
    }
}
