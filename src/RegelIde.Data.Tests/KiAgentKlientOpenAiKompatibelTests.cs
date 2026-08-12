using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data.Tests;

/// <summary>
/// Byggesteg 5 runde 3 — klienten er leverandøragnostisk (BaseUrl/Modell/ApiKey er alle konfig), så
/// disse testene bruker en frittkonfigurert URL i stedet for en hardkodet leverandør-URL. Samme
/// stub-<see cref="HttpMessageHandler"/>-prinsipp som <see cref="LovdataBulkHenterTests"/>s
/// motstykke — et ekte KI-kall koster penger og krever en nøkkel som ikke skal ligge i CI.
/// </summary>
public class KiAgentKlientOpenAiKompatibelTests
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

    /// <summary>R0 (docs/13-backlog.md §4 punkt 7) — én "handling" per kall (siste gjentas hvis flere
    /// kall enn oppgitt), der en handling ENTEN kaster (simulerer en transient nettverksfeil/timeout)
    /// ELLER returnerer et fast svar. Samme prinsipp som SekvensStubHandler i
    /// EmbeddingKlientOpenAiKompatibelTests, men generalisert til å kunne kaste i stedet for kun å
    /// variere statuskode/kropp — 429 er en HTTP-status, en timeout er et unntak fra selve kallet.</summary>
    private sealed class UnntakEllerSvarHandler(IReadOnlyList<Func<HttpResponseMessage>> handlinger) : HttpMessageHandler
    {
        private int _kall;
        public int AntallKall => _kall;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var handling = handlinger[Math.Min(_kall, handlinger.Count - 1)];
            _kall++;
            return Task.FromResult(handling());
        }
    }

    private const string TestBaseUrl = "https://api.eksempel-leverandor.test/v1/chat/completions";
    private const string TestModell = "et-modellnavn";

    private static IConfiguration LagConfig(string? baseUrl = TestBaseUrl, string? modell = TestModell, string? apiKey = "test-nøkkel")
    {
        var verdier = new Dictionary<string, string?>();
        if (baseUrl is not null) verdier["RegelIde:KiAgent:BaseUrl"] = baseUrl;
        if (modell is not null) verdier["RegelIde:KiAgent:Modell"] = modell;
        if (apiKey is not null) verdier["RegelIde:KiAgent:ApiKey"] = apiKey;
        return new ConfigurationBuilder().AddInMemoryCollection(verdier).Build();
    }

    [Fact]
    public async Task Sender_til_konfigurert_baseurl_med_konfigurert_modell_og_parser_svaret()
    {
        const string svarJson = """{"choices": [{"message": {"content": "[{\"Term\":\"x\"}]"}}]}""";
        HttpRequestMessage? fangetRequest = null;
        string? fangetBody = null;
        var handler = new StubHandler(HttpStatusCode.OK, svarJson, (req, body) => { fangetRequest = req; fangetBody = body; });
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenAiKompatibel(http, LagConfig());

        var resultat = await klient.GenererAsync("Identifiser begrep", "kontekst-tekst");

        Assert.Equal("[{\"Term\":\"x\"}]", resultat.Innhold);
        Assert.Equal(TestBaseUrl, fangetRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", fangetRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-nøkkel", fangetRequest.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(fangetBody!);
        Assert.Equal(TestModell, body.RootElement.GetProperty("model").GetString());
        var meldinger = body.RootElement.GetProperty("messages");
        Assert.Equal("system", meldinger[0].GetProperty("role").GetString());
        Assert.Equal("Identifiser begrep", meldinger[0].GetProperty("content").GetString());
        Assert.Equal("user", meldinger[1].GetProperty("role").GetString());
        Assert.Equal("kontekst-tekst", meldinger[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Manglende_baseurl_kaster_tydelig_feil_uten_a_ringe_nettverket()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenAiKompatibel(http, LagConfig(baseUrl: null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.GenererAsync("s", "k"));
        Assert.Contains("BaseUrl", ex.Message);
    }

    [Fact]
    public async Task Manglende_modell_kaster_tydelig_feil_uten_gjettet_default()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenAiKompatibel(http, LagConfig(modell: null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.GenererAsync("s", "k"));
        Assert.Contains("Modell", ex.Message);
    }

    [Fact]
    public async Task Manglende_api_nokkel_kaster_tydelig_feil()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenAiKompatibel(http, LagConfig(apiKey: null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.GenererAsync("s", "k"));
        Assert.Contains("ApiKey", ex.Message);
    }

    [Fact]
    public async Task Feilrespons_kaster_tydelig_feil_med_baseurl_i_meldingen()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, """{"error": "invalid key"}""");
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenAiKompatibel(http, LagConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.GenererAsync("s", "k"));
        Assert.Contains("401", ex.Message);
        Assert.Contains(TestBaseUrl, ex.Message);
    }

    [Fact]
    public async Task Parser_token_forbruk_fra_usage_i_svaret()
    {
        const string svarJson = """
            {"choices": [{"message": {"content": "[]"}}], "usage": {"prompt_tokens": 49123, "completion_tokens": 2, "total_tokens": 49125}}
            """;
        var handler = new StubHandler(HttpStatusCode.OK, svarJson);
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenAiKompatibel(http, LagConfig());

        var resultat = await klient.GenererAsync("s", "k");

        Assert.Equal(49123, resultat.InputTokens);
        Assert.Equal(2, resultat.OutputTokens);
    }

    [Fact]
    public async Task Manglende_usage_i_svaret_gir_null_token_tall_ingen_gjettet_fallback()
    {
        const string svarJson = """{"choices": [{"message": {"content": "[]"}}]}""";
        var handler = new StubHandler(HttpStatusCode.OK, svarJson);
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenAiKompatibel(http, LagConfig());

        var resultat = await klient.GenererAsync("s", "k");

        Assert.Null(resultat.InputTokens);
        Assert.Null(resultat.OutputTokens);
    }

    [Fact]
    public async Task Transient_timeout_pa_forste_forsok_lykkes_pa_retry()
    {
        // R0 (docs/13-backlog.md §4 punkt 7) — reell observert oppførsel mot HostYourAI: en
        // TaskCanceledException (HttpClient sin 100-sekunders standard-timeout) på første forsøk,
        // lyktes på forsøk to. Samme feilmodus simulert her uten et ekte nettverkskall.
        const string svarJson = """{"choices": [{"message": {"content": "[]"}}]}""";
        var handler = new UnntakEllerSvarHandler([
            () => throw new TaskCanceledException("simulert HttpClient-timeout"),
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(svarJson, Encoding.UTF8, "application/json") },
        ]);
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenAiKompatibel(http, LagConfig());

        var resultat = await klient.GenererAsync("s", "k");

        Assert.Equal("[]", resultat.Innhold);
        Assert.Equal(2, handler.AntallKall);
    }

    [Fact]
    public async Task Transient_feil_pa_alle_forsok_kaster_tydelig_feil_ikke_stille_svelging()
    {
        var handler = new UnntakEllerSvarHandler([
            () => throw new TaskCanceledException("simulert HttpClient-timeout"),
        ]); // alltid timeout
        using var http = new HttpClient(handler);
        var klient = new KiAgentKlientOpenAiKompatibel(http, LagConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.GenererAsync("s", "k"));

        Assert.Contains(TestBaseUrl, ex.Message);
        Assert.Equal(2, handler.AntallKall); // maks 2 forsøk totalt, se KiAgentKlientOpenAiKompatibel
    }
}
