using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for `/api/virksomheter/{id}/relasjoner`, `/api/virksomhet-relasjoner/{id}` og
/// `/api/konfigurasjon/relasjonstyper` (docs/28, docs/29 §Del C) mot ekte embedded Postgres.
/// `RelasjonsTypeKonfigurasjonEntitet` er allerede seedet av Program.cs' oppstartsseeding (samme fixture
/// kjører hele API-et), så testene bruker de fire kjente kodene direkte.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class VirksomhetRelasjonEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web);

    public VirksomhetRelasjonEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> HentJuristIdAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist").Id;
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<Guid> OpprettVirksomhetAsync(Guid brukerId, string navn)
    {
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/virksomheter", brukerId,
            new { Navn = navn, OverordnetEnhetId = (Guid?)null }));
        svar.EnsureSuccessStatusCode();
        var virksomhet = await svar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger);
        return virksomhet!.Id;
    }

    [Fact]
    public async Task Henter_de_fire_kjente_relasjonstypene_med_riktige_plassholdere()
    {
        var typer = await _client.GetFromJsonAsync<List<RelasjonsTypeKonfigurasjonDto>>("/api/konfigurasjon/relasjonstyper", JsonInnstillinger);
        Assert.NotNull(typer);
        Assert.True(typer!.Count >= 4);

        var underlagt = Assert.Single(typer, t => t.Kode == "underlagt");
        Assert.Equal("er underlagt {0}", underlagt.FraVisningsmal);
        Assert.Equal("er eier/overordnet for {0}", underlagt.TilVisningsmal);

        var sekretariat = Assert.Single(typer, t => t.Kode == "sekretariat");
        Assert.Equal("har sekretariat hos {0}", sekretariat.FraVisningsmal);
        Assert.Equal("er sekretariat for {0}", sekretariat.TilVisningsmal);

        var klageinstans = Assert.Single(typer, t => t.Kode == "klageinstans");
        Assert.Equal("har klageinstans hos {0}", klageinstans.FraVisningsmal);
        Assert.Equal("er klageinstans for {0}", klageinstans.TilVisningsmal);

        var enhetI = Assert.Single(typer, t => t.Kode == "enhet_i");
        Assert.Equal("er enhet i {0}", enhetI.FraVisningsmal);
        Assert.Equal("har enhet {0}", enhetI.TilVisningsmal);
    }

    /// <summary>docs/28s merkenemnd-eksempel: «Lokal merkenemnd — sekretariat → Statsforvalteren».
    /// Bekrefter visningstekstene på begge sider ordrett matcher tabellen i docs/28/docs/29 §C.2.</summary>
    [Fact]
    public async Task Oppretter_relasjon_og_henter_fra_begge_sider_med_korrekt_visningstekst()
    {
        var juristId = await HentJuristIdAsync();
        var merkenemndNavn = $"Lokal merkenemnd {Guid.NewGuid():N}";
        var statsforvalterenNavn = $"Statsforvalteren {Guid.NewGuid():N}";
        var merkenemndId = await OpprettVirksomhetAsync(juristId, merkenemndNavn);
        var statsforvalterenId = await OpprettVirksomhetAsync(juristId, statsforvalterenNavn);

        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/virksomheter/{merkenemndId}/relasjoner", juristId,
            new { TilVirksomhetId = statsforvalterenId, RelasjonsType = "sekretariat", HjemmelRettskildeId = (Guid?)null, HjemmelEid = (string?)null, Kommentar = "docs/28-eksempel" }));
        Assert.Equal(HttpStatusCode.OK, opprettSvar.StatusCode);

        var fraSiden = await _client.GetFromJsonAsync<List<VirksomhetRelasjonDto>>($"/api/virksomheter/{merkenemndId}/relasjoner", JsonInnstillinger);
        var visningFra = Assert.Single(fraSiden!);
        Assert.Equal("fra", visningFra.Retning);
        Assert.Equal($"har sekretariat hos {statsforvalterenNavn}", visningFra.Visningstekst);
        Assert.Equal(statsforvalterenId, visningFra.MotpartVirksomhetId);
        Assert.Equal("docs/28-eksempel", visningFra.Kommentar);

        var tilSiden = await _client.GetFromJsonAsync<List<VirksomhetRelasjonDto>>($"/api/virksomheter/{statsforvalterenId}/relasjoner", JsonInnstillinger);
        var visningTil = Assert.Single(tilSiden!);
        Assert.Equal("til", visningTil.Retning);
        Assert.Equal($"er sekretariat for {merkenemndNavn}", visningTil.Visningstekst);
        Assert.Equal(merkenemndId, visningTil.MotpartVirksomhetId);
        Assert.Equal(visningFra.Id, visningTil.Id);
    }

    [Fact]
    public async Task Ukjent_relasjonstype_gir_400()
    {
        var juristId = await HentJuristIdAsync();
        var fraId = await OpprettVirksomhetAsync(juristId, $"Fra-virksomhet {Guid.NewGuid():N}");
        var tilId = await OpprettVirksomhetAsync(juristId, $"Til-virksomhet {Guid.NewGuid():N}");

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/virksomheter/{fraId}/relasjoner", juristId,
            new { TilVirksomhetId = tilId, RelasjonsType = "ukjent_type_finnes_ikke", HjemmelRettskildeId = (Guid?)null, HjemmelEid = (string?)null, Kommentar = (string?)null }));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Sletter_relasjon()
    {
        var juristId = await HentJuristIdAsync();
        var fraId = await OpprettVirksomhetAsync(juristId, $"Fra-virksomhet-slett {Guid.NewGuid():N}");
        var tilId = await OpprettVirksomhetAsync(juristId, $"Til-virksomhet-slett {Guid.NewGuid():N}");

        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/virksomheter/{fraId}/relasjoner", juristId,
            new { TilVirksomhetId = tilId, RelasjonsType = "underlagt", HjemmelRettskildeId = (Guid?)null, HjemmelEid = (string?)null, Kommentar = (string?)null }));
        var liste = await opprettSvar.Content.ReadFromJsonAsync<List<VirksomhetRelasjonDto>>(JsonInnstillinger);
        var relasjonId = liste!.Single().Id;

        var slettSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/virksomhet-relasjoner/{relasjonId}", juristId));
        Assert.Equal(HttpStatusCode.NoContent, slettSvar.StatusCode);

        var etterSlett = await _client.GetFromJsonAsync<List<VirksomhetRelasjonDto>>($"/api/virksomheter/{fraId}/relasjoner", JsonInnstillinger);
        Assert.Empty(etterSlett!);
    }
}
