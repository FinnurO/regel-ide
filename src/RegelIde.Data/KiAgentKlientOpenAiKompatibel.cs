using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data;

/// <summary>
/// Ekte <see cref="IKiAgentKlient"/>-implementasjon (byggesteg 5 runde 3) mot ETHVERT OpenAI-
/// kompatibelt chat-completions-API — ikke bundet til én bestemt leverandør. Endepunkt/modell/nøkkel
/// er alle konfigurasjon (<c>RegelIde:KiAgent:BaseUrl</c>/<c>Modell</c>/<c>ApiKey</c>, se
/// <c>Program.cs</c>), ikke hardkodet — en fremtidig leverandør-/modellbytte (f.eks. HostYourAI,
/// OpenRouter, eller noe helt annet som snakker samme wire-format) krever kun nye konfigverdier,
/// aldri en kodeendring i denne klassen. Se docs/14-byggesteg5-teknisk-design.md for begrunnelsen.
/// </summary>
public sealed class KiAgentKlientOpenAiKompatibel(HttpClient http, IConfiguration config) : IKiAgentKlient
{
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

        using var response = await http.SendAsync(request, ct);
        var responsBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"KI-kallet mot '{baseUrl}' feilet ({(int)response.StatusCode} {response.StatusCode}): {responsBody}");
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
