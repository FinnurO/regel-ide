using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data.Tests;

/// <summary>
/// Byggesteg 5 runde 4 (RAG-spike) — samme stub-<see cref="HttpMessageHandler"/>-prinsipp som
/// <see cref="KiAgentKlientOpenAiKompatibelTests"/>: et ekte embeddings-kall koster penger og
/// krever en nøkkel som ikke skal ligge i CI.
/// </summary>
public class EmbeddingKlientOpenAiKompatibelTests
{
    /// <summary>Returnerer ett svar fra en sekvens per kall (siste svar gjentas hvis flere kall enn
    /// oppgitt) — brukes til å simulere "429 først, så OK" uten et ekte nettverkskall (byggesteg 5
    /// runde 4, etterkant, docs/14 §8.4).</summary>
    private sealed class SekvensStubHandler(IReadOnlyList<(HttpStatusCode Status, string Body)> svar, Action<HttpRequestMessage, string>? verifiser = null) : HttpMessageHandler
    {
        private int _kall;
        public int AntallKall => _kall;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (status, body) = svar[Math.Min(_kall, svar.Count - 1)];
            _kall++;
            var kropp = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            verifiser?.Invoke(request, kropp);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static HttpMessageHandler EnkeltSvar(HttpStatusCode status, string body, Action<HttpRequestMessage, string>? verifiser = null) =>
        new SekvensStubHandler([(status, body)], verifiser);

    private const string TestBaseUrl = "https://api.eksempel-leverandor.test/v1/embeddings";
    private const string TestModell = "en-embeddings-modell";

    private static IConfiguration LagConfig(string? baseUrl = TestBaseUrl, string? modell = TestModell, string? apiKey = "test-nøkkel")
    {
        var verdier = new Dictionary<string, string?>();
        if (baseUrl is not null) verdier["RegelIde:KiAgent:EmbeddingBaseUrl"] = baseUrl;
        if (modell is not null) verdier["RegelIde:KiAgent:EmbeddingModell"] = modell;
        if (apiKey is not null) verdier["RegelIde:KiAgent:ApiKey"] = apiKey;
        return new ConfigurationBuilder().AddInMemoryCollection(verdier).Build();
    }

    [Fact]
    public async Task Sender_flere_tekster_i_ett_kall_og_parser_vektorene_i_riktig_rekkefolge()
    {
        // "index" i svaret er BEVISST i motsatt rekkefølge av input — beviser at klienten ikke bare
        // antar samme rekkefølge som den sendte inn (se docs/14 §8.4-kommentaren i selve klienten).
        const string svarJson = """{"data": [{"index": 1, "embedding": [0.3, 0.4]}, {"index": 0, "embedding": [0.1, 0.2]}]}""";
        HttpRequestMessage? fangetRequest = null;
        string? fangetBody = null;
        var handler = EnkeltSvar(HttpStatusCode.OK, svarJson, (req, body) => { fangetRequest = req; fangetBody = body; });
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig());

        var vektorer = await klient.EmbedAsync(["tekst-en", "tekst-to"]);

        Assert.Equal([0.1, 0.2], vektorer[0]);
        Assert.Equal([0.3, 0.4], vektorer[1]);
        Assert.Equal(TestBaseUrl, fangetRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", fangetRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-nøkkel", fangetRequest.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(fangetBody!);
        Assert.Equal(TestModell, body.RootElement.GetProperty("model").GetString());
        var input = body.RootElement.GetProperty("input");
        Assert.Equal(2, input.GetArrayLength());
        Assert.Equal("tekst-en", input[0].GetString());
        Assert.Equal("tekst-to", input[1].GetString());
    }

    [Fact]
    public async Task Tom_tekstliste_gir_tom_liste_uten_a_ringe_nettverket()
    {
        var handler = EnkeltSvar(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig());

        var vektorer = await klient.EmbedAsync([]);

        Assert.Empty(vektorer);
    }

    [Fact]
    public async Task Manglende_baseurl_kaster_tydelig_feil_uten_a_ringe_nettverket()
    {
        var handler = EnkeltSvar(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig(baseUrl: null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.EmbedAsync(["t"]));
        Assert.Contains("EmbeddingBaseUrl", ex.Message);
    }

    [Fact]
    public async Task Manglende_modell_kaster_tydelig_feil_uten_gjettet_default()
    {
        var handler = EnkeltSvar(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig(modell: null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.EmbedAsync(["t"]));
        Assert.Contains("EmbeddingModell", ex.Message);
    }

    [Fact]
    public async Task Manglende_api_nokkel_kaster_tydelig_feil()
    {
        var handler = EnkeltSvar(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig(apiKey: null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.EmbedAsync(["t"]));
        Assert.Contains("ApiKey", ex.Message);
    }

    [Fact]
    public async Task Feilrespons_kaster_tydelig_feil_med_baseurl_i_meldingen()
    {
        var handler = EnkeltSvar(HttpStatusCode.Unauthorized, """{"error": "invalid key"}""");
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.EmbedAsync(["t"]));
        Assert.Contains("401", ex.Message);
        Assert.Contains(TestBaseUrl, ex.Message);
    }

    [Fact]
    public async Task Manglende_data_i_svaret_kaster_tydelig_feil()
    {
        const string svarJson = """{"usage": {"total_tokens": 3}}""";
        var handler = EnkeltSvar(HttpStatusCode.OK, svarJson);
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.EmbedAsync(["t"]));
        Assert.Contains("embeddings", ex.Message);
    }

    [Fact]
    public async Task Antall_embeddinger_som_ikke_matcher_antall_tekster_kaster_tydelig_feil()
    {
        // Kun 1 embedding i svaret, men 2 tekster ble sendt inn — en leverandør som droppet én
        // rad stille ville ellers gitt feil node en tilfeldig annen nodes vektor.
        const string svarJson = """{"data": [{"index": 0, "embedding": [0.1]}]}""";
        var handler = EnkeltSvar(HttpStatusCode.OK, svarJson);
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.EmbedAsync(["t1", "t2"]));
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public async Task Retryer_pa_429_og_lykkes_pa_neste_forsok()
    {
        // Byggesteg 5 runde 4, etterkant (docs/14 §8.4) — reell observert oppførsel fra HostYourAI.
        var handler = new SekvensStubHandler([
            (HttpStatusCode.TooManyRequests, "Too Many Requests"),
            (HttpStatusCode.OK, """{"data": [{"index": 0, "embedding": [0.5, 0.6]}]}"""),
        ]);
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig());

        var vektorer = await klient.EmbedAsync(["t"]);

        Assert.Equal([0.5, 0.6], vektorer[0]);
        Assert.Equal(2, handler.AntallKall);
    }

    [Fact]
    public async Task Gir_opp_etter_maks_antall_forsok_pa_429_og_kaster_tydelig_feil()
    {
        var handler = new SekvensStubHandler([(HttpStatusCode.TooManyRequests, "Too Many Requests")]); // alltid 429
        using var http = new HttpClient(handler);
        var klient = new EmbeddingKlientOpenAiKompatibel(http, LagConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => klient.EmbedAsync(["t"]));
        Assert.Contains("429", ex.Message);
        Assert.Equal(3, handler.AntallKall); // maks 3 forsøk totalt, se EmbeddingKlientOpenAiKompatibel
    }
}
