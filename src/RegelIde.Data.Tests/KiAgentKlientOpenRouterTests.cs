using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data.Tests;

/// <summary>
/// I motsetning til <see cref="LovdataBulkHenterTests"/> (gratis, uautentisert offentlig data) koster
/// et ekte OpenRouter-kall penger og krever en API-nøkkel som ikke skal ligge i CI — testene her
/// stubber derfor <see cref="HttpMessageHandler"/> i stedet for å ringe det ekte API-et.
/// </summary>
public class KiAgentKlientOpenRouterTests
{
    private sealed class StubHandler(HttpStatusCode status, string responsBody, Action<HttpRequestMessage, string>? verifiser = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var kropp = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            verifiser?.Invoke(request, kropp);
            return new HttpResponseMessage(status) { Content = new StringContent(responsBody, Encoding.UTF8, "application/json") };
        }
    }

    private static IConfiguration LagConfig(string? apiKey = "test-nøkkel", string? modell = null)
    {
        var verdier = new Dictionary<string, string?>
        {
            ["RegelIde:KiAgent:OpenRouter:ApiKey"] = apiKey,
        };
        if (modell is not null) verdier["RegelIde:KiAgent:OpenRouter:Modell"] = modell;
        return new ConfigurationBuilder().AddInMemoryCollection(verdier).Build();
    }

    [Fact]
    public async Task Sender_riktig_url_headere_og_body_og_parser_svaret()
    {
        const string svarJson = """{"choices": [{"message": {"content": "[{\"Term\":\"x\"}]"}}]}""";
        HttpRequestMessage? fangetRequest = null;
        string? fangetBody = null;
        var handler = new StubHandler(HttpStatusCode.OK, svarJson, (req, body) => { fangetRequest = req; fangetBody = body; });
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenRouter(http, LagConfig());

        var resultat = await klient.GenererAsync("Identifiser begrep", "kontekst-tekst");

        Assert.Equal("[{\"Term\":\"x\"}]", resultat);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", fangetRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", fangetRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-nøkkel", fangetRequest.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(fangetBody!);
        Assert.Equal("deepseek/deepseek-v4-flash-0731", body.RootElement.GetProperty("model").GetString());
        var meldinger = body.RootElement.GetProperty("messages");
        Assert.Equal("system", meldinger[0].GetProperty("role").GetString());
        Assert.Equal("Identifiser begrep", meldinger[0].GetProperty("content").GetString());
        Assert.Equal("user", meldinger[1].GetProperty("role").GetString());
        Assert.Equal("kontekst-tekst", meldinger[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Bruker_konfigurert_modell_i_stedet_for_default()
    {
        const string svarJson = """{"choices": [{"message": {"content": "ok"}}]}""";
        string? fangetBody = null;
        var handler = new StubHandler(HttpStatusCode.OK, svarJson, (_, body) => fangetBody = body);
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenRouter(http, LagConfig(modell: "deepseek/annen-modell"));

        await klient.GenererAsync("systeminstruks", "kontekst");

        using var body = JsonDocument.Parse(fangetBody!);
        Assert.Equal("deepseek/annen-modell", body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task Manglende_api_nokkel_kaster_tydelig_feil_uten_a_ringe_nettverket()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenRouter(http, LagConfig(apiKey: null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.GenererAsync("s", "k"));
        Assert.Contains("user-secrets", ex.Message);
    }

    [Fact]
    public async Task Feilrespons_fra_openrouter_kaster_tydelig_feil()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, """{"error": "invalid key"}""");
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenRouter(http, LagConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.GenererAsync("s", "k"));
        Assert.Contains("401", ex.Message);
    }
}
