using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester: kjører hele API-et (inkl. migrasjon + førstegangs-seeding i Program.cs) mot
/// en ekte, embedded Postgres-instans og de ekte rettskilde-fixturene i data/kilder/raw-lovdata/.
/// </summary>
public class RettskilderEndepunktTests : IClassFixture<EmbeddedPostgresApiFixture>
{
    private readonly HttpClient _client;
    private const string AlkohollovenEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public RettskilderEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> HentAlkohollovenIdAsync()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        return sammendrag!.Single(r => r.Eli == AlkohollovenEli).Id;
    }

    [Fact]
    public async Task Liste_inneholder_alle_tre_kildedokumenter()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);

        Assert.NotNull(sammendrag);
        Assert.Equal(3, sammendrag!.Count);
        Assert.Contains(sammendrag, r => r.Eli == AlkohollovenEli);
        Assert.Contains(sammendrag, r => r.Eli == "https://lovdata.no/eli/forskrift/2005/06/08/538/nor");
        Assert.Contains(sammendrag, r => r.Eli == "https://lovdata.no/eli/lov/1967/02/10/nor");
    }

    [Fact]
    public async Task Henter_full_rettskilde_med_metadata_og_akn_xml()
    {
        var id = await HentAlkohollovenIdAsync();
        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);

        Assert.NotNull(detalj);
        Assert.Equal(AlkohollovenEli, detalj!.Eli);
        Assert.StartsWith("<akomaNtoso", detalj.AknXml);
    }

    [Fact]
    public async Task Ukjent_id_gir_404()
    {
        var svar = await _client.GetAsync($"/api/rettskilder/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Henter_nodetre_og_finner_paragraf_1_1()
    {
        var id = await HentAlkohollovenIdAsync();
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{id}/noder", JsonInnstillinger);

        Assert.NotNull(noder);
        Assert.Contains(noder!, n => n.Eid == $"{AlkohollovenEli}/§1-1");
    }

    [Fact]
    public async Task Henter_enkeltnode_ved_eId_med_skraastreker_og_skjema()
    {
        var id = await HentAlkohollovenIdAsync();
        var eid = $"{AlkohollovenEli}/§1-1/ledd-1";
        var node = await _client.GetFromJsonAsync<RettskildeNodeDto>(
            $"/api/rettskilder/{id}/noder/oppslag?eid={Uri.EscapeDataString(eid)}", JsonInnstillinger);

        Assert.NotNull(node);
        Assert.Equal(eid, node!.Eid);
        Assert.StartsWith("Reguleringen av innførsel", node.Tekst);
    }

    [Fact]
    public async Task Ukjent_eId_gir_404_selv_om_rettskilden_finnes()
    {
        var id = await HentAlkohollovenIdAsync();
        var svar = await _client.GetAsync($"/api/rettskilder/{id}/noder/oppslag?eid=finnes-ikke");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Henter_kryssreferanser_inkludert_intern_referanse_1_3_til_1_5()
    {
        var id = await HentAlkohollovenIdAsync();
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{id}/noder", JsonInnstillinger);
        var fraNodeId = noder!.Single(n => n.Eid == $"{AlkohollovenEli}/§1-3/ledd-1").Id;

        var referanser = await _client.GetFromJsonAsync<List<RettskildeReferanseDto>>($"/api/rettskilder/{id}/referanser", JsonInnstillinger);

        Assert.NotNull(referanser);
        Assert.Contains(referanser!, r => r.FraNodeId == fraNodeId && r.TilEid == $"{AlkohollovenEli}/§1-5");
    }
}
