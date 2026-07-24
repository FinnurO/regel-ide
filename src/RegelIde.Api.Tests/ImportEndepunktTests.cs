using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for import-endepunktene (2026-07-24) — kjører mot ekte embedded Postgres.
/// Lovdata-testene gjør ekte nettverkskall (samme prinsipp som RegelIde.Data.Tests/LovdataBulkHenterTests).
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class ImportEndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public ImportEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    [Fact]
    public async Task Import_uten_bruker_id_header_gir_400()
    {
        var svar = await _client.PostAsJsonAsync("/api/rettskilder/lovdata", new LovdataImportRequest("LOV-1967-02-10"));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Filopplasting_importerer_som_delt_kilde_nar_ingen_virksomhet_er_angitt()
    {
        var bruker = await HentTestbrukerAsync();
        var html = Testdata.LesAlkoholloven();

        using var innhold = new MultipartFormDataContent();
        var filInnhold = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(html));
        filInnhold.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        innhold.Add(filInnhold, "fil", "alkoholloven.html");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rettskilder/fil") { Content = innhold };
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());

        var svar = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);

        var opprettet = await svar.Content.ReadFromJsonAsync<JsonElement>();
        var id = opprettet.GetProperty("id").GetGuid();

        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);
        Assert.NotNull(detalj);
        Assert.Null(detalj!.VirksomhetId); // ingen ?virksomhetId sendt -> delt/nasjonal
    }

    [Fact]
    public async Task Filopplasting_med_virksomhetId_importerer_som_virksomhetens_egen_kilde()
    {
        var bruker = await HentTestbrukerAsync();
        var html = Testdata.LesForvaltningsloven();

        using var innhold = new MultipartFormDataContent();
        var filInnhold = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(html));
        filInnhold.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        innhold.Add(filInnhold, "fil", "forvaltningsloven.html");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/rettskilder/fil?virksomhetId={bruker.VirksomhetId}")
        {
            Content = innhold,
        };
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());

        var svar = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);

        var opprettet = await svar.Content.ReadFromJsonAsync<JsonElement>();
        var id = opprettet.GetProperty("id").GetGuid();

        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);
        Assert.Equal(bruker.VirksomhetId, detalj!.VirksomhetId);
    }

    [Fact]
    public async Task Ugyldig_fil_gir_400_ikke_500()
    {
        var bruker = await HentTestbrukerAsync();

        using var innhold = new MultipartFormDataContent();
        var filInnhold = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("<html><body>ikke Lovdata-format</body></html>"));
        filInnhold.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        innhold.Add(filInnhold, "fil", "tull.html");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rettskilder/fil") { Content = innhold };
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());

        var svar = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Import_fra_lovdata_henter_og_lagrer_som_delt_kilde()
    {
        var bruker = await HentTestbrukerAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rettskilder/lovdata")
        {
            Content = JsonContent.Create(new LovdataImportRequest("LOV-1967-02-10")),
        };
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());

        var svar = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);

        var opprettet = await svar.Content.ReadFromJsonAsync<JsonElement>();
        var id = opprettet.GetProperty("id").GetGuid();

        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);
        Assert.NotNull(detalj);
        Assert.Equal("https://lovdata.no/eli/lov/1967/02/10/nor", detalj!.Eli);
        Assert.Null(detalj.VirksomhetId);
    }
}
