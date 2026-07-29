using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>Integrasjonstester for håndbok/rundskriv-forfatterendepunktene (2026-07-26, AK-3.3.8–3.3.12).</summary>
[Collection(ApiTestCollection.Navn)]
public class HandbokEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public HandbokEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    private HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<(Guid HandbokId, Guid KapittelId, Guid BrukerId)> OpprettHandbokMedKapittelAsync()
    {
        var bruker = await HentTestbrukerAsync();
        var handbokSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/handboker", bruker.Id,
            new OpprettHandbokRequest($"Håndbok {Guid.NewGuid()}")));
        Assert.Equal(HttpStatusCode.Created, handbokSvar.StatusCode);
        var handbokId = (await handbokSvar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var kapittelSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{handbokId}/kapitler", bruker.Id,
            new OpprettKapittelNodeRequest(null, "1", "Alminnelige bestemmelser")));
        Assert.Equal(HttpStatusCode.Created, kapittelSvar.StatusCode);
        var kapittel = await kapittelSvar.Content.ReadFromJsonAsync<RettskildeNodeDto>(JsonInnstillinger);

        return (handbokId, kapittel!.Id, bruker.Id);
    }

    [Fact]
    public async Task Opprett_handbok_uten_bruker_id_header_gir_400()
    {
        var svar = await _client.PostAsJsonAsync("/api/handboker", new OpprettHandbokRequest("Test"));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Opprett_handbok_gir_201_og_er_lesbar_via_rettskilder_endepunktet()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/handboker", bruker.Id, new OpprettHandbokRequest("Alkoholloven med kommentarer")));

        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var id = (await svar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);
        Assert.Equal("Rundskriv", detalj!.Kildetype);
        Assert.Equal("Gjeldende", detalj.Status);
    }

    [Fact]
    public async Task Opprett_kapittel_pa_ukjent_handbok_gir_404()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{Guid.NewGuid()}/kapitler", bruker.Id,
            new OpprettKapittelNodeRequest(null, "1", null)));

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Opprett_kommentar_gir_201_med_utledet_bindende_i_dto()
    {
        var (handbokId, kapittelId, brukerId) = await OpprettHandbokMedKapittelAsync();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{handbokId}/kommentarer", brukerId,
            new OpprettKommentarNodeRequest(kapittelId, "1.1", "Om vandel", "<p>Kommentartekst</p>", "instruks", "ledd", ["vandel"])));

        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var dto = await svar.Content.ReadFromJsonAsync<RettskildeNodeDto>(JsonInnstillinger);
        Assert.NotNull(dto!.HandbokMetadata);
        Assert.True(dto.HandbokMetadata!.Bindende);
        Assert.Equal("instruks", dto.HandbokMetadata.Dokumenttype);
        Assert.Equal("under_arbeid", dto.HandbokMetadata.Status);
    }

    [Fact]
    public async Task Opprett_kommentar_med_ugyldig_dokumenttype_gir_400()
    {
        var (handbokId, kapittelId, brukerId) = await OpprettHandbokMedKapittelAsync();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{handbokId}/kommentarer", brukerId,
            new OpprettKommentarNodeRequest(kapittelId, "1.1", null, "<p>Tekst</p>", "veiledning", "ledd", null)));

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Noder_endepunktet_viser_handbok_metadata_for_kommentarnoder()
    {
        var (handbokId, kapittelId, brukerId) = await OpprettHandbokMedKapittelAsync();
        await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{handbokId}/kommentarer", brukerId,
            new OpprettKommentarNodeRequest(kapittelId, "1.1", null, "<p>Tekst</p>", "kommentar", "ledd", null)));

        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{handbokId}/noder", JsonInnstillinger);

        var kommentarNode = noder!.Single(n => n.Nummer == "1.1");
        Assert.NotNull(kommentarNode.HandbokMetadata);
        Assert.False(kommentarNode.HandbokMetadata!.Bindende);
    }

    [Fact]
    public async Task Rediger_kommentar_oppretter_ny_versjon_og_gammel_forsvinner_fra_noder()
    {
        var (handbokId, kapittelId, brukerId) = await OpprettHandbokMedKapittelAsync();
        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{handbokId}/kommentarer", brukerId,
            new OpprettKommentarNodeRequest(kapittelId, "1.1", "Tittel v1", "<p>Versjon 1</p>", "kommentar", "ledd", null)));
        var v1 = await opprettSvar.Content.ReadFromJsonAsync<RettskildeNodeDto>(JsonInnstillinger);

        var redigerSvar = await _client.SendAsync(MedBruker(HttpMethod.Put, $"/api/handboker/{handbokId}/kommentarer/{v1!.Id}", brukerId,
            new RedigerKommentarNodeRequest("<p>Versjon 2</p>", "Tittel v2", "retningslinje", "ledd", ["nytt"])));

        Assert.Equal(HttpStatusCode.OK, redigerSvar.StatusCode);
        var v2 = await redigerSvar.Content.ReadFromJsonAsync<RettskildeNodeDto>(JsonInnstillinger);
        Assert.Equal(2, v2!.Versjon);
        Assert.True(v2.HandbokMetadata!.Bindende);

        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{handbokId}/noder", JsonInnstillinger);
        Assert.DoesNotContain(noder!, n => n.Id == v1.Id); // gammel versjon er 'erstattet', ikke lenger i gjeldende-treet
        Assert.Contains(noder!, n => n.Id == v2.Id);

        var versjoner = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>(
            $"/api/handboker/{handbokId}/kommentarer/versjoner?eid={Uri.EscapeDataString(v1.Eid)}", JsonInnstillinger);
        Assert.Equal(2, versjoner!.Count);
    }

    [Fact]
    public async Task Lovreferanse_koblet_og_deretter_fjernet()
    {
        var (handbokId, kapittelId, brukerId) = await OpprettHandbokMedKapittelAsync();
        var kommentarSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{handbokId}/kommentarer", brukerId,
            new OpprettKommentarNodeRequest(kapittelId, "1.1", null, "<p>Tekst</p>", "kommentar", "bestemmelse", null)));
        var kommentar = await kommentarSvar.Content.ReadFromJsonAsync<RettskildeNodeDto>(JsonInnstillinger);

        var alkoholoven = (await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger))!
            .Single(r => r.Eli == "https://lovdata.no/eli/lov/1989/06/02/27/nor");
        var paragraf = (await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{alkoholoven.Id}/noder", JsonInnstillinger))!
            .First(n => n.NodeType == "paragraf");

        var kobleSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{handbokId}/kommentarer/{kommentar!.Id}/lovreferanser", brukerId,
            new KobleLovreferanseRequest(alkoholoven.Id, paragraf.Eid)));
        Assert.Equal(HttpStatusCode.Created, kobleSvar.StatusCode);
        var referanse = await kobleSvar.Content.ReadFromJsonAsync<RettskildeReferanseDto>(JsonInnstillinger);

        var fjernSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete,
            $"/api/handboker/{handbokId}/kommentarer/{kommentar.Id}/lovreferanser/{referanse!.Id}", brukerId));
        Assert.Equal(HttpStatusCode.NoContent, fjernSvar.StatusCode);

        var ukjentSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete,
            $"/api/handboker/{handbokId}/kommentarer/{kommentar.Id}/lovreferanser/{referanse.Id}", brukerId));
        Assert.Equal(HttpStatusCode.NotFound, ukjentSvar.StatusCode); // allerede fjernet
    }

    [Fact]
    public async Task Revisjonsmerke_med_tom_grunn_gir_400_med_gyldig_grunn_gir_204()
    {
        var (handbokId, kapittelId, brukerId) = await OpprettHandbokMedKapittelAsync();
        var kommentarSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{handbokId}/kommentarer", brukerId,
            new OpprettKommentarNodeRequest(kapittelId, "1.1", null, "<p>Tekst</p>", "kommentar", "ledd", null)));
        var kommentar = await kommentarSvar.Content.ReadFromJsonAsync<RettskildeNodeDto>(JsonInnstillinger);

        var tomSvar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/handboker/{handbokId}/kommentarer/{kommentar!.Id}/revisjonsmerke", brukerId, new SettRevisjonsmerkeRequest("")));
        Assert.Equal(HttpStatusCode.BadRequest, tomSvar.StatusCode);

        var gyldigSvar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/handboker/{handbokId}/kommentarer/{kommentar.Id}/revisjonsmerke", brukerId,
            new SettRevisjonsmerkeRequest("Loven er endret.")));
        Assert.Equal(HttpStatusCode.NoContent, gyldigSvar.StatusCode);
    }

    [Fact]
    public async Task Publiser_bindende_uten_godkjenner_gir_400_med_godkjenner_gir_204()
    {
        var (handbokId, kapittelId, brukerId) = await OpprettHandbokMedKapittelAsync();
        var kommentarSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/handboker/{handbokId}/kommentarer", brukerId,
            new OpprettKommentarNodeRequest(kapittelId, "1.1", null, "<p>Tekst</p>", "instruks", "ledd", null)));
        var kommentar = await kommentarSvar.Content.ReadFromJsonAsync<RettskildeNodeDto>(JsonInnstillinger);

        var utenGodkjennerSvar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/handboker/{handbokId}/kommentarer/{kommentar!.Id}/publiser", brukerId, new PubliserKommentarRequest(null)));
        Assert.Equal(HttpStatusCode.BadRequest, utenGodkjennerSvar.StatusCode);

        var medGodkjennerSvar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/handboker/{handbokId}/kommentarer/{kommentar.Id}/publiser", brukerId, new PubliserKommentarRequest("Ola Fagansvarlig")));
        Assert.Equal(HttpStatusCode.NoContent, medGodkjennerSvar.StatusCode);
    }
}
