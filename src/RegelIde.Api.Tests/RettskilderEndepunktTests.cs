using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using RegelIde.Api;
using RegelIde.Kildekonvertering;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester: kjører hele API-et in-process (WebApplicationFactory) mot de ekte
/// rettskilde-fixturene i data/kilder/raw-lovdata/, ingen mocking av repositoryet.
/// </summary>
public class RettskilderEndepunktTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private const string AlkohollovenDatokode = "LOV-1989-06-02-27";
    private const string AlkohollovenEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";

    // Må matche serverens JSON-oppsett (Program.cs: ConfigureHttpJsonOptions) — enums serialiseres
    // som tekst der, så klienten må lese dem på samme måte.
    // Må matche ASP.NET Cores standard HTTP-JSON-oppsett (camelCase, case-insensitiv lesing)
    // i tillegg til enum-som-tekst konfigurert i Program.cs.
    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public RettskilderEndepunktTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Liste_inneholder_alle_tre_kildedokumenter()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);

        Assert.NotNull(sammendrag);
        Assert.Equal(3, sammendrag!.Count);
        Assert.Contains(sammendrag, r => r.Datokode == AlkohollovenDatokode);
        Assert.Contains(sammendrag, r => r.Datokode == "FOR-2005-06-08-538");
        Assert.Contains(sammendrag, r => r.Datokode == "LOV-1967-02-10");
    }

    [Fact]
    public async Task Henter_full_rettskilde_med_metadata_og_akn_xml()
    {
        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{AlkohollovenDatokode}", JsonInnstillinger);

        Assert.NotNull(detalj);
        Assert.Equal(AlkohollovenEli, detalj!.Metadata.Eli);
        Assert.StartsWith("<akomaNtoso", detalj.AknXml);
    }

    [Fact]
    public async Task Ukjent_datokode_gir_404()
    {
        var svar = await _client.GetAsync("/api/rettskilder/LOV-0000-01-01-1");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Henter_nodetre_og_finner_paragraf_1_1()
    {
        var noder = await _client.GetFromJsonAsync<List<RettskildeNode>>($"/api/rettskilder/{AlkohollovenDatokode}/noder", JsonInnstillinger);

        Assert.NotNull(noder);
        Assert.Contains(noder!, n => n.Eid == $"{AlkohollovenEli}/§1-1");
    }

    [Fact]
    public async Task Henter_enkeltnode_ved_eId_med_skraastreker_og_skjema()
    {
        // eId-en er en full ELI-URI (både "://" og flere skråstreker: .../§1-1/ledd-1) — gis derfor
        // som query-parameter, ikke rutesegment (se kommentar i Program.cs).
        var eid = $"{AlkohollovenEli}/§1-1/ledd-1";
        var node = await _client.GetFromJsonAsync<RettskildeNode>(
            $"/api/rettskilder/{AlkohollovenDatokode}/noder/oppslag?eid={Uri.EscapeDataString(eid)}", JsonInnstillinger);

        Assert.NotNull(node);
        Assert.Equal(eid, node!.Eid);
        Assert.StartsWith("Reguleringen av innførsel", node.Tekst);
    }

    [Fact]
    public async Task Ukjent_eId_gir_404_selv_om_rettskilden_finnes()
    {
        var svar = await _client.GetAsync($"/api/rettskilder/{AlkohollovenDatokode}/noder/oppslag?eid=finnes-ikke");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Henter_kryssreferanser_inkludert_intern_referanse_1_3_til_1_5()
    {
        var referanser = await _client.GetFromJsonAsync<List<RettskildeReferanse>>($"/api/rettskilder/{AlkohollovenDatokode}/referanser", JsonInnstillinger);

        Assert.NotNull(referanser);
        Assert.Contains(referanser!, r =>
            r.FraNodeEid == $"{AlkohollovenEli}/§1-3/ledd-1" &&
            r.TilEid == $"{AlkohollovenEli}/§1-5" &&
            r.ErInternReferanse);
    }
}
