using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for tekst-tagg-endepunktene (2026-07-24, AK-3.3.1–3.3.4) — kjører mot ekte
/// embedded Postgres og de ekte rettskilde-fixturene.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class TaggEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;
    private const string AlkohollovenEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";
    private const string ForsteLeddEid = $"{AlkohollovenEli}/§1-1/ledd-1";

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public TaggEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> HentAlkohollovenIdAsync()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        return sammendrag!.Single(r => r.Eli == AlkohollovenEli).Id;
    }

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    private HttpRequestMessage OpprettTaggRequest(Guid rettskildeId, Guid brukerId, string nodeEid, string kind, string quoteExact) =>
        new(HttpMethod.Post, $"/api/rettskilder/{rettskildeId}/tagger")
        {
            Content = JsonContent.Create(new OpprettTekstTaggRequest(nodeEid, 0, quoteExact.Length, "", quoteExact, "", kind)),
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } },
        };

    [Fact]
    public async Task Opprett_uten_bruker_id_header_gir_400()
    {
        var id = await HentAlkohollovenIdAsync();
        var svar = await _client.PostAsJsonAsync($"/api/rettskilder/{id}/tagger",
            new OpprettTekstTaggRequest(ForsteLeddEid, 0, 4, "", "Regu", "", "begrep"));

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Opprett_gyldig_tagg_gir_201_med_riktig_dto()
    {
        var id = await HentAlkohollovenIdAsync();
        var bruker = await HentTestbrukerAsync();

        var svar = await _client.SendAsync(OpprettTaggRequest(id, bruker.Id, ForsteLeddEid, "tjeneste", "Reguleringen"));

        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var tagg = await svar.Content.ReadFromJsonAsync<TekstTaggDto>(JsonInnstillinger);
        Assert.Equal("tjeneste", tagg!.Kind);
        Assert.Equal(ForsteLeddEid, tagg.NodeEid);
        Assert.Null(tagg.RefId);
    }

    [Fact]
    public async Task Opprett_ugyldig_kind_gir_400()
    {
        var id = await HentAlkohollovenIdAsync();
        var bruker = await HentTestbrukerAsync();

        var svar = await _client.SendAsync(OpprettTaggRequest(id, bruker.Id, ForsteLeddEid, "tjenest", "Reguleringen"));

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Opprett_ukjent_node_eid_gir_404()
    {
        var id = await HentAlkohollovenIdAsync();
        var bruker = await HentTestbrukerAsync();

        var svar = await _client.SendAsync(OpprettTaggRequest(id, bruker.Id, "finnes-ikke", "begrep", "abcd"));

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Opprett_pa_ukjent_rettskilde_gir_404()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(OpprettTaggRequest(Guid.NewGuid(), bruker.Id, ForsteLeddEid, "begrep", "Reguleringen"));

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Hent_tagger_viser_kun_egen_virksomhets_tagger()
    {
        var id = await HentAlkohollovenIdAsync();
        var bruker = await HentTestbrukerAsync();

        Guid annenVirksomhetId, annenBrukerId;
        await using (var db = _fixture.NyDbContext())
        {
            annenVirksomhetId = Guid.NewGuid();
            db.Virksomheter.Add(new Virksomhet { Id = annenVirksomhetId, Navn = "Annen kommune (tagg-test)" });
            annenBrukerId = Guid.NewGuid();
            db.Brukere.Add(new Bruker { Id = annenBrukerId, Navn = "Annen bruker", VirksomhetId = annenVirksomhetId, Rolle = "Testrolle" });
            await db.SaveChangesAsync();
        }

        var mittSvar = await _client.SendAsync(OpprettTaggRequest(id, bruker.Id, ForsteLeddEid, "begrep", "Reguleringen"));
        Assert.Equal(HttpStatusCode.Created, mittSvar.StatusCode);
        var annetSvar = await _client.SendAsync(OpprettTaggRequest(id, annenBrukerId, ForsteLeddEid, "vilkar", "Reguleringen"));
        Assert.Equal(HttpStatusCode.Created, annetSvar.StatusCode);

        using var listeRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/rettskilder/{id}/tagger");
        listeRequest.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());
        var mineTagger = await (await _client.SendAsync(listeRequest)).Content.ReadFromJsonAsync<List<TekstTaggDto>>(JsonInnstillinger);

        Assert.NotNull(mineTagger);
        Assert.Contains(mineTagger!, t => t.Kind == "begrep");
        Assert.DoesNotContain(mineTagger!, t => t.Kind == "vilkar" && t.NodeEid == ForsteLeddEid && t.OpprettetAv == "Annen bruker");
    }

    [Fact]
    public async Task Slett_fjerner_taggen_fra_listen()
    {
        var id = await HentAlkohollovenIdAsync();
        var bruker = await HentTestbrukerAsync();
        var opprettSvar = await _client.SendAsync(OpprettTaggRequest(id, bruker.Id, ForsteLeddEid, "regel", "Reguleringen"));
        var tagg = await opprettSvar.Content.ReadFromJsonAsync<TekstTaggDto>(JsonInnstillinger);

        using var slettRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/rettskilder/{id}/tagger/{tagg!.Id}");
        slettRequest.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());
        var slettSvar = await _client.SendAsync(slettRequest);

        Assert.Equal(HttpStatusCode.NoContent, slettSvar.StatusCode);

        using var listeRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/rettskilder/{id}/tagger");
        listeRequest.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());
        var tagger = await (await _client.SendAsync(listeRequest)).Content.ReadFromJsonAsync<List<TekstTaggDto>>(JsonInnstillinger);
        Assert.DoesNotContain(tagger!, t => t.Id == tagg.Id);
    }

    [Fact]
    public async Task Slett_annen_virksomhets_tagg_gir_403()
    {
        var id = await HentAlkohollovenIdAsync();
        var bruker = await HentTestbrukerAsync();
        var opprettSvar = await _client.SendAsync(OpprettTaggRequest(id, bruker.Id, ForsteLeddEid, "begrep", "Reguleringen"));
        var tagg = await opprettSvar.Content.ReadFromJsonAsync<TekstTaggDto>(JsonInnstillinger);

        Guid annenBrukerId;
        await using (var db = _fixture.NyDbContext())
        {
            var annenVirksomhetId = Guid.NewGuid();
            db.Virksomheter.Add(new Virksomhet { Id = annenVirksomhetId, Navn = "Enda en annen kommune" });
            annenBrukerId = Guid.NewGuid();
            db.Brukere.Add(new Bruker { Id = annenBrukerId, Navn = "Uvedkommende", VirksomhetId = annenVirksomhetId, Rolle = "Testrolle" });
            await db.SaveChangesAsync();
        }

        using var slettRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/rettskilder/{id}/tagger/{tagg!.Id}");
        slettRequest.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, annenBrukerId.ToString());
        var slettSvar = await _client.SendAsync(slettRequest);

        Assert.Equal(HttpStatusCode.Forbidden, slettSvar.StatusCode);
    }

    [Fact]
    public async Task Slett_ukjent_tagg_gir_404()
    {
        var bruker = await HentTestbrukerAsync();
        using var slettRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/rettskilder/{Guid.NewGuid()}/tagger/{Guid.NewGuid()}");
        slettRequest.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString());

        var svar = await _client.SendAsync(slettRequest);

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }
}
