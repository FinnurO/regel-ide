using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for Tjeneste-/Begreps-/Kodelisteregisteret (byggesteg 2, 2026-07-29) — kjører
/// mot ekte embedded Postgres. Program.cs' egen oppstartsseeding (Byggesteg2InnholdSeed) har allerede
/// opprettet "Alminnelig skjenkebevilling" + begrepene/kodelistene før noen test kjører — testene
/// bruker unike navn/koder for eget opprettet innhold for å unngå kollisjon med den delte databasen
/// (samme prinsipp som ux_kodelister_kode i RegelIde.Data.Tests).
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class Byggesteg2EndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Byggesteg2EndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    // ---------- Tjenester ----------

    [Fact]
    public async Task Hent_tjenester_uten_bruker_header_gir_400()
    {
        var svar = await _client.GetAsync("/api/tjenester");
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Hent_tjenester_viser_seedet_skjenkebevilling()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester", bruker.Id));
        var tjenester = await svar.Content.ReadFromJsonAsync<List<TjenesteDto>>(JsonInnstillinger);

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains(tjenester!, t => t.Tittel == "Alminnelig skjenkebevilling");
    }

    [Fact]
    public async Task Seedet_skjenkebevilling_har_syv_regelverksreferanser()
    {
        var bruker = await HentTestbrukerAsync();
        var tjenester = await (await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester", bruker.Id)))
            .Content.ReadFromJsonAsync<List<TjenesteDto>>(JsonInnstillinger);
        var tjeneste = tjenester!.Single(t => t.Tittel == "Alminnelig skjenkebevilling");

        var referanser = await _client.GetFromJsonAsync<List<TjenesteRegelverksreferanseDto>>(
            $"/api/tjenester/{tjeneste.Id}/regelverksreferanser", JsonInnstillinger);

        Assert.Equal(7, referanser!.Count);
    }

    [Fact]
    public async Task Oppretter_oppdaterer_og_endrer_status_pa_tjeneste()
    {
        var bruker = await HentTestbrukerAsync();
        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester", bruker.Id,
            new TjenesteRequest("Testtjeneste", null, null, null, null, null, null, null, null, null, null, null)));
        Assert.Equal(HttpStatusCode.Created, opprettSvar.StatusCode);
        var opprettet = await opprettSvar.Content.ReadFromJsonAsync<TjenesteDto>(JsonInnstillinger);
        Assert.Equal("utkast", opprettet!.Status);

        var oppdaterSvar = await _client.SendAsync(MedBruker(HttpMethod.Put, $"/api/tjenester/{opprettet.Id}", bruker.Id,
            new TjenesteRequest("Testtjeneste v2", "Ny beskrivelse", null, null, null, null, null, null, null, null, null, null)));
        var oppdatert = await oppdaterSvar.Content.ReadFromJsonAsync<TjenesteDto>(JsonInnstillinger);
        Assert.Equal(HttpStatusCode.OK, oppdaterSvar.StatusCode);
        Assert.Equal("Testtjeneste v2", oppdatert!.Tittel);

        var statusSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{opprettet.Id}/status", bruker.Id,
            new SettStatusRequest("publisert")));
        var medNyStatus = await statusSvar.Content.ReadFromJsonAsync<TjenesteDto>(JsonInnstillinger);
        Assert.Equal("publisert", medNyStatus!.Status);
    }

    [Fact]
    public async Task Ukjent_status_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var opprettet = await (await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester", bruker.Id,
            new TjenesteRequest("Statustest", null, null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<TjenesteDto>(JsonInnstillinger);

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{opprettet!.Id}/status", bruker.Id,
            new SettStatusRequest("ukjent-status")));

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    // ---------- Begreper ----------

    [Fact]
    public async Task Hent_begreper_viser_de_tre_seedede()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/begreper", bruker.Id));
        var begreper = await svar.Content.ReadFromJsonAsync<List<BegrepDto>>(JsonInnstillinger);

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains(begreper!, b => b.Term == "uklanderlig vandel");
        Assert.Contains(begreper!, b => b.Term == "styrer og stedfortreder");
        Assert.Contains(begreper!, b => b.Term == "skjenketid");
    }

    [Fact]
    public async Task Oppretter_begrep_med_ukjent_lovreferanse_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begreper", bruker.Id,
            new BegrepRequest("test-begrep-api", "Definisjon", "finnes-ikke", null, null, null, "faktabegrep")));

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Oppretter_og_endrer_status_pa_begrep()
    {
        var bruker = await HentTestbrukerAsync();
        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begreper", bruker.Id,
            new BegrepRequest("test-begrep-endepunkt", "Definisjon", null, null, null, null, "faktabegrep")));
        Assert.Equal(HttpStatusCode.Created, opprettSvar.StatusCode);
        var begrep = await opprettSvar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger);

        var statusSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/begreper/{begrep!.Id}/status", bruker.Id,
            new SettStatusRequest("validert")));
        var oppdatert = await statusSvar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger);

        Assert.Equal("validert", oppdatert!.Status);
    }

    // ---------- Kodelister (åpne data, ingen bruker-header krevd for lesing) ----------

    [Fact]
    public async Task Hent_kodelister_viser_de_to_seedede_uten_bruker_header()
    {
        var kodelister = await _client.GetFromJsonAsync<List<KodelisteDto>>("/api/kodelister", JsonInnstillinger);

        Assert.Contains(kodelister!, k => k.Kode == "KL-VANDELSOMRADE-ALKOHOLLOV");
        Assert.Contains(kodelister!, k => k.Kode == "KL-RETTSKILDEVEKT");
    }

    [Fact]
    public async Task Vandelsomrade_kodeliste_har_fire_koder()
    {
        var kodelister = await _client.GetFromJsonAsync<List<KodelisteDto>>("/api/kodelister", JsonInnstillinger);
        var vandelsomrade = kodelister!.Single(k => k.Kode == "KL-VANDELSOMRADE-ALKOHOLLOV");

        var detalj = await _client.GetFromJsonAsync<KodelisteDto>($"/api/kodelister/{vandelsomrade.Id}", JsonInnstillinger);

        Assert.Equal(4, detalj!.Koder.Count);
    }

    [Fact]
    public async Task Oppretter_kodeliste_legger_til_kode_og_avviser_status_for_ekstern_referanse()
    {
        var bruker = await HentTestbrukerAsync();
        var unikKode = $"KL-API-TEST-{Guid.NewGuid():N}";
        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/kodelister", bruker.Id,
            new KodelisteRequest(unikKode, "API-test", "ekstern-referanse", null, null, "https://data.norge.no/x", "1.0")));
        Assert.Equal(HttpStatusCode.Created, opprettSvar.StatusCode);
        var kodeliste = await opprettSvar.Content.ReadFromJsonAsync<KodelisteDto>(JsonInnstillinger);
        Assert.Equal("publisert", kodeliste!.Status);

        var kodeSvar = await _client.PostAsJsonAsync($"/api/kodelister/{kodeliste.Id}/koder",
            new LeggTilKodeRequest("kode-a", "Kode A", null, null, null));
        Assert.Equal(HttpStatusCode.Created, kodeSvar.StatusCode);

        var statusSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/kodelister/{kodeliste.Id}/status", bruker.Id,
            new SettStatusRequest("arkivert")));
        Assert.Equal(HttpStatusCode.BadRequest, statusSvar.StatusCode);
    }

    // ---------- Tagg-kobling (låser opp TekstTaggEntitet.RefId) ----------

    [Fact]
    public async Task Kobler_tagg_til_begrep()
    {
        var bruker = await HentTestbrukerAsync();

        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        var alkoholloven = sammendrag!.Single(r => r.Eli == "https://lovdata.no/eli/lov/1989/06/02/27/nor");
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{alkoholloven.Id}/noder", JsonInnstillinger);
        var leddMedTekst = noder!.First(n => n.NodeType == "ledd" && n.Tekst != null && n.Tekst.Length > 10);

        var taggSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/rettskilder/{alkoholloven.Id}/tagger", bruker.Id,
            new OpprettTekstTaggRequest(leddMedTekst.Eid, 0, 4, "", leddMedTekst.Tekst![..4], leddMedTekst.Tekst[4..], "begrep")));
        var tagg = await taggSvar.Content.ReadFromJsonAsync<TekstTaggDto>(JsonInnstillinger);

        var begrepSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begreper", bruker.Id,
            new BegrepRequest("test-begrep-for-kobling", "Definisjon", null, null, null, null, "faktabegrep")));
        var begrep = await begrepSvar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger);

        var kobleSvar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/rettskilder/{alkoholloven.Id}/tagger/{tagg!.Id}/koble", bruker.Id,
            new KobleTaggTilEntitetRequest(begrep!.Id)));
        var koblet = await kobleSvar.Content.ReadFromJsonAsync<TekstTaggDto>(JsonInnstillinger);

        Assert.Equal(HttpStatusCode.OK, kobleSvar.StatusCode);
        Assert.Equal(begrep.Id, koblet!.RefId);
    }

    [Fact]
    public async Task Kobler_tagg_til_ukjent_entitet_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        var alkoholloven = sammendrag!.Single(r => r.Eli == "https://lovdata.no/eli/lov/1989/06/02/27/nor");
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{alkoholloven.Id}/noder", JsonInnstillinger);
        var leddMedTekst = noder!.First(n => n.NodeType == "ledd" && n.Tekst != null && n.Tekst.Length > 10);

        var taggSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/rettskilder/{alkoholloven.Id}/tagger", bruker.Id,
            new OpprettTekstTaggRequest(leddMedTekst.Eid, 0, 4, "", leddMedTekst.Tekst![..4], leddMedTekst.Tekst[4..], "vilkar")));
        var tagg = await taggSvar.Content.ReadFromJsonAsync<TekstTaggDto>(JsonInnstillinger);

        var kobleSvar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/rettskilder/{alkoholloven.Id}/tagger/{tagg!.Id}/koble", bruker.Id,
            new KobleTaggTilEntitetRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.BadRequest, kobleSvar.StatusCode);
    }
}
