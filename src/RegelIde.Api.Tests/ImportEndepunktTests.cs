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

    /// <summary>
    /// Konsistensrunde (2026-08-20): en vellykket enkeltimport via dette endepunktet skal ALLTID
    /// gjenspeiles i lovdata_importstatus også, ikke bare fullimport-rundens egen skriving — se
    /// LovdataImportstatusTjeneste. Uten dette ville raden (dersom den fantes fra en tidligere
    /// fullimport-runde med importert=false) stå igjen med en utdatert feilmelding etter denne
    /// vellykkede enkeltimporten.
    /// </summary>
    [Fact]
    public async Task Import_fra_lovdata_oppdaterer_importstatus_ved_suksess()
    {
        var bruker = await HentTestbrukerAsync();
        const string datokode = "LOV-1967-02-10";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rettskilder/lovdata")
        {
            Content = JsonContent.Create(new LovdataImportRequest(datokode)),
        };
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());

        var svar = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var opprettet = await svar.Content.ReadFromJsonAsync<JsonElement>();
        var id = opprettet.GetProperty("id").GetGuid();

        var statusListe = await _client.GetFromJsonAsync<List<LovdataImportstatusDto>>(
            "/api/lovdata-importstatus", JsonInnstillinger);
        var status = statusListe!.SingleOrDefault(s => s.Datokode == datokode);
        Assert.NotNull(status);
        Assert.True(status!.Importert);
        Assert.Equal(id, status.RettskildeId);
        Assert.Null(status.Feilmelding);
        Assert.Equal("https://lovdata.no/eli/lov/1967/02/10/nor", status.Eli);
    }

    /// <summary>
    /// Samme konsistensrunde — en enkeltimport som FEILER skal oppdatere lovdata_importstatus med den
    /// FERSKE feilmeldingen (ikke la en eventuell gammel stå), men skal fortsatt returnere den vanlige
    /// tydelige feilresponsen til brukeren (422, uendret oppførsel). LOV-1931-06-12-1 er et kjent, reelt
    /// tilfelle parseren i dag avviser (gammel "Første kapitel."-ordvariant, se KapittelOrdvarianter i
    /// LovdataHtmlParser — bevisst utenfor scope, se docs/13-backlog.md). Merk: Grunnloven
    /// (LOV-1814-05-17), tidligere brukt her, ble et FAKTISK positivt PARSE-resultat etter runden med
    /// gjennomgang mot https://api.lovdata.no/xmldocs 2026-08-21 (kapittelfri-lov-håndteringen dekker
    /// den nå) — testen måtte derfor byttes til et dokument som fortsatt genuint feiler.
    /// </summary>
    [Fact]
    public async Task Import_fra_lovdata_oppdaterer_importstatus_med_fersk_feilmelding_ved_feil()
    {
        var bruker = await HentTestbrukerAsync();
        const string datokode = "LOV-1931-06-12-1";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rettskilder/lovdata")
        {
            Content = JsonContent.Create(new LovdataImportRequest(datokode)),
        };
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());

        var svar = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, svar.StatusCode);

        var statusListe = await _client.GetFromJsonAsync<List<LovdataImportstatusDto>>(
            "/api/lovdata-importstatus?importert=false", JsonInnstillinger);
        var status = statusListe!.SingleOrDefault(s => s.Datokode == datokode);
        Assert.NotNull(status);
        Assert.False(status!.Importert);
        Assert.Null(status.RettskildeId);
        Assert.NotNull(status.Feilmelding);
        Assert.Equal("https://lovdata.no/eli/lov/1931/06/12/1/nor", status.Eli);
    }
}
