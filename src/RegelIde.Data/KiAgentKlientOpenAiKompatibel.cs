using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RegelIde.Data;

/// <summary>
/// Ekte <see cref="IKiAgentKlient"/>-implementasjon (byggesteg 5 runde 3) mot ETHVERT OpenAI-
/// kompatibelt chat-completions-API — ikke bundet til én bestemt leverandør. Endepunkt/modell/nøkkel
/// er alle konfigurasjon (<c>RegelIde:KiAgent:BaseUrl</c>/<c>Modell</c>/<c>ApiKey</c>, se
/// <c>Program.cs</c>), ikke hardkodet — en fremtidig leverandør-/modellbytte (f.eks. HostYourAI,
/// OpenRouter, eller noe helt annet som snakker samme wire-format) krever kun nye konfigverdier,
/// aldri en kodeendring i denne klassen. Se docs/14-byggesteg5-teknisk-design.md for begrunnelsen.
/// </summary>
/// <remarks>
/// R0 (byggesteg 5 runde 5, docs/13-backlog.md §4 punkt 7): ett automatisk retry ved en TRANSIENT
/// HTTP-feil (nettverksfeil/timeout på selve kallet — en annen feilmodus enn et tomt, men gyldig,
/// JSON-svar, se retry-logikken i <see cref="TjenesteforslagTjeneste"/>/<see cref="BegrepsforslagTjeneste"/>
/// via <see cref="KiForslagRetryHjelper"/> for DEN feilmodusen). Observert live mot HostYourAI: en
/// <see cref="TaskCanceledException"/> (HttpClient sin 100-sekunders standard-timeout) på første
/// forsøk, lyktes på forsøk to. Én fast, kort forsinkelse (300ms) — IKKE doblende backoff som
/// 429-fiksen i <see cref="EmbeddingKlientOpenAiKompatibel"/>: dette er en engangs-timeout/
/// nettverksglipp, ikke rate-limiting, så det er ingen grunn til å vente lenger og lenger.
/// </remarks>
public sealed class KiAgentKlientOpenAiKompatibel(
    HttpClient http, IConfiguration config, ILogger<KiAgentKlientOpenAiKompatibel>? logger = null) : IKiAgentKlient
{
    private readonly ILogger<KiAgentKlientOpenAiKompatibel> _logger =
        logger ?? NullLogger<KiAgentKlientOpenAiKompatibel>.Instance;

    // Kun ETT ekstra forsøk (ikke en rate-limit-backoff) — se klasse-doc-kommentaren over.
    private const int MaksAntallForsok = 2;
    private const int ForsinkelseMs = 300;

    public async Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default)
    {
        var baseUrl = config["RegelIde:KiAgent:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "RegelIde:KiAgent:BaseUrl er ikke satt. Kjør 'dotnet user-secrets set \"RegelIde:KiAgent:BaseUrl\" \"<leverandørens chat-completions-URL>\"' fra src/RegelIde.Api. Ingen gjettet fallback.");
        }
        var modell = config["RegelIde:KiAgent:Modell"];
        if (string.IsNullOrWhiteSpace(modell))
        {
            throw new InvalidOperationException(
                "RegelIde:KiAgent:Modell er ikke satt. Ingen gjettet fallback — dette skal ikke defaultes til en bestemt leverandørs modell.");
        }
        var apiNokkel = config["RegelIde:KiAgent:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiNokkel))
        {
            throw new InvalidOperationException(
                "RegelIde:KiAgent:ApiKey er ikke satt. Kjør 'dotnet user-secrets set \"RegelIde:KiAgent:ApiKey\" \"<nøkkel>\"' fra src/RegelIde.Api. Ingen gjettet fallback.");
        }

        string responsBody;
        var forsok = 0;
        while (true)
        {
            forsok++;
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = JsonContent.Create(new
                {
                    model = modell,
                    messages = new[]
                    {
                        new { role = "system", content = systemInstruks },
                        new { role = "user", content = kontekst },
                    },
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiNokkel);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Et ekte avbrutt kall fra KALLEREN (ikke HttpClient sin egen interne timeout) skal
                // forplante seg uendret, ikke tolkes som en transient feil å prøve igjen på.
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
                if (forsok >= MaksAntallForsok)
                {
                    _logger.LogError(ex,
                        "KI-kallet mot '{BaseUrl}' feilet med en transient nettverksfeil/timeout etter {Forsok} forsøk — gir opp.",
                        baseUrl, forsok);
                    throw new InvalidOperationException(
                        $"KI-kallet mot '{baseUrl}' feilet etter {forsok} forsøk (transient nettverksfeil/timeout): {ex.Message}", ex);
                }
                _logger.LogWarning(ex,
                    "KI-kallet mot '{BaseUrl}' feilet med en transient nettverksfeil/timeout på forsøk {Forsok} — prøver igjen om {ForsinkelseMs}ms.",
                    baseUrl, forsok, ForsinkelseMs);
                await Task.Delay(ForsinkelseMs, ct);
                continue;
            }

            using (response)
            {
                responsBody = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "KI-kallet mot '{BaseUrl}' feilet ({StatusCode}). Rå respons: {RaaRespons}",
                        baseUrl, (int)response.StatusCode, responsBody);
                    throw new InvalidOperationException(
                        $"KI-kallet mot '{baseUrl}' feilet ({(int)response.StatusCode} {response.StatusCode}): {responsBody}");
                }
            }
            break;
        }

        using var dokument = JsonDocument.Parse(responsBody);
        var innhold = dokument.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        if (innhold is null)
        {
            throw new InvalidOperationException(
                $"Responsen fra '{baseUrl}' manglet 'choices[0].message.content'. Rå respons: {responsBody}");
        }

        // Byggesteg 5 runde 3: OpenAI-kompatible leverandører rapporterer normalt token-forbruk i et
        // "usage"-felt på toppnivå — ikke alle gjør det, og feltet er ikke del av selve
        // chat-completions-spesifikasjonen, derfor `TryGetProperty` + null i stedet for å kreve det.
        int? inputTokens = null;
        int? outputTokens = null;
        if (dokument.RootElement.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var promptTokens) && promptTokens.TryGetInt32(out var pt))
            {
                inputTokens = pt;
            }
            if (usage.TryGetProperty("completion_tokens", out var completionTokens) && completionTokens.TryGetInt32(out var comp))
            {
                outputTokens = comp;
            }
        }

        return new KiSvar(innhold, inputTokens, outputTokens);
    }
}
