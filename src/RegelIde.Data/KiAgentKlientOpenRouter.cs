using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data;

/// <summary>
/// Ekte <see cref="IKiAgentKlient"/>-implementasjon (byggesteg 5 runde 2) mot OpenRouters
/// OpenAI-kompatible chat-completions-API (<c>https://openrouter.ai/api/v1/chat/completions</c>),
/// brukt til å nå DeepSeek V4 Flash 0731 (<c>deepseek/deepseek-v4-flash-0731</c>) uten Kina-hostet
/// direktetilgang — se docs/14-byggesteg5-teknisk-design.md for begrunnelsen. Leverandør/modell
/// styres av konfig (<c>RegelIde:KiAgent:...</c>, se <c>Program.cs</c>), ikke av denne klassen —
/// bytte av modell er en konfigurasjonsendring, ikke en kodeendring.
/// </summary>
public sealed class KiAgentKlientOpenRouter(HttpClient http, IConfiguration config) : IKiAgentKlient
{
    private const string Endepunkt = "https://openrouter.ai/api/v1/chat/completions";

    public async Task<string> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default)
    {
        var apiNokkel = config["RegelIde:KiAgent:OpenRouter:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiNokkel))
        {
            throw new InvalidOperationException(
                "RegelIde:KiAgent:OpenRouter:ApiKey er ikke satt. Kjør 'dotnet user-secrets set \"RegelIde:KiAgent:OpenRouter:ApiKey\" \"<nøkkel>\"' fra src/RegelIde.Api. Ingen gjettet fallback.");
        }
        var modell = config["RegelIde:KiAgent:OpenRouter:Modell"] ?? "deepseek/deepseek-v4-flash-0731";

        using var request = new HttpRequestMessage(HttpMethod.Post, Endepunkt)
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
        // OpenRouters egen (valgfrie) attribusjons-headere for ranking på openrouter.ai/rankings.
        request.Headers.Add("HTTP-Referer", "https://regel-ide.local");
        request.Headers.Add("X-Title", "Regel-IDE");

        using var response = await http.SendAsync(request, ct);
        var responsBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenRouter-kallet feilet ({(int)response.StatusCode} {response.StatusCode}): {responsBody}");
        }

        using var dokument = JsonDocument.Parse(responsBody);
        var innhold = dokument.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return innhold ?? throw new InvalidOperationException(
            $"OpenRouter-responsen manglet 'choices[0].message.content'. Rå respons: {responsBody}");
    }
}
