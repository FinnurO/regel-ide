using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #286] Integrasjonstester for (1) den KI-assisterte regelverksreferanseforslag-køen for
/// EKSISTERENDE tjenester uten koblinger, og (2) GET /api/rettskilder/{id}/statistikk. Kjører mot ekte
/// embedded Postgres inkl. Program.cs' egen oppstartsseeding (alkoholloven + FasitRunde4Seed sine
/// tjenester under Testkommunen er allerede der før noen test kjører, se AC1-tellingen i PR-beskrivelsen).
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class RegelverksreferanseforslagOgRettskildeStatistikkEndepunktTests
{
    private readonly EmbeddedPostgresApiFixture _fixture;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public RegelverksreferanseforslagOgRettskildeStatistikkEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    private async Task<Guid> HentAlkohollovenIdAsync()
    {
        var rettskilder = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        return rettskilder!.First(r => r.Tittel.Contains("alkohol", StringComparison.OrdinalIgnoreCase)).Id;
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    // ---------- GET /api/rettskilder/{id}/statistikk ----------

    [Fact]
    public async Task Statistikk_for_ukjent_rettskilde_gir_404()
    {
        var svar = await _client.GetAsync($"/api/rettskilder/{Guid.NewGuid()}/statistikk");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Statistikk_for_alkoholloven_matcher_direkte_databaseoppslag()
    {
        var rettskildeId = await HentAlkohollovenIdAsync();

        var svar = await _client.GetAsync($"/api/rettskilder/{rettskildeId}/statistikk");
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var statistikk = await svar.Content.ReadFromJsonAsync<RettskildeStatistikkDto>(JsonInnstillinger);

        // Golden cross-check mot en uavhengig, direkte spørring i stedet for hardkodede magic numbers —
        // robust mot at seed-korpuset endrer seg over tid (se RettskildeRepository.StatistikkAsync sin
        // doc-kommentar for nøyaktig samme telledefinisjon gjentatt her).
        await using var db = _fixture.NyDbContext();
        var forventetTjenester = await db.TjenesteRegelverksreferanser
            .Where(r => r.TilRettskildeId == rettskildeId).Select(r => r.TjenesteId).Distinct().CountAsync();
        var forventetBegrep = await db.TekstTagger
            .Where(t => t.RettskildeId == rettskildeId && t.Kind == "begrep" && t.RefId != null && t.Entitetsstatus == "gjeldende")
            .Select(t => t.RefId!.Value).Distinct().CountAsync();

        Assert.NotNull(statistikk);
        Assert.Equal(rettskildeId, statistikk!.RettskildeId);
        Assert.Equal(forventetTjenester, statistikk.AntallTjenester);
        Assert.Equal(forventetBegrep, statistikk.AntallBegrep);
        Assert.True(statistikk.AntallVirksomheter >= 0);
    }

    // ---------- Regelverksreferanseforslag-køen ----------

    [Fact]
    public async Task Kjor_uten_bruker_header_gir_400()
    {
        var rettskildeId = await HentAlkohollovenIdAsync();
        var svar = await _client.PostAsJsonAsync(
            "/api/tjenester/regelverksreferanse-forslag/kjor", new KjorRegelverksreferanseforslagRequest([rettskildeId]));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Kjor_med_ukjent_rettskilde_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/regelverksreferanse-forslag/kjor", bruker.Id,
            new KjorRegelverksreferanseforslagRequest([Guid.NewGuid()])));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Kjor_oppretter_ventende_forslag_som_kan_godkjennes_og_avvises()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();

        await using (var forDb = _fixture.NyDbContext())
        {
            var forventetUtenReferanse = await forDb.Tjenester
                .Where(t => t.VirksomhetId == bruker.VirksomhetId && t.Entitetsstatus == "gjeldende")
                .Where(t => !forDb.TjenesteRegelverksreferanser.Any(r => r.TjenesteId == t.Id))
                .CountAsync();
            Assert.True(forventetUtenReferanse > 0, "Testforutsetning: Testkommunen må ha minst én tjeneste uten regelverksreferanse (se AC1-tellingen i PR-beskrivelsen).");
        }

        var kjorSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/regelverksreferanse-forslag/kjor", bruker.Id,
            new KjorRegelverksreferanseforslagRequest([rettskildeId])));
        Assert.Equal(HttpStatusCode.OK, kjorSvar.StatusCode);
        var resultat = await kjorSvar.Content.ReadFromJsonAsync<KjorRegelverksreferanseforslagResponsDto>(JsonInnstillinger);
        Assert.NotNull(resultat);
        Assert.True(resultat!.AntallTjenesterUtenReferanse > 0);
        Assert.True(resultat.AntallNyeForslag > 0);

        var koSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester/regelverksreferanse-forslag", bruker.Id));
        Assert.Equal(HttpStatusCode.OK, koSvar.StatusCode);
        var ko = await koSvar.Content.ReadFromJsonAsync<List<TjenesteRegelverksreferanseForslagDto>>(JsonInnstillinger);
        Assert.NotEmpty(ko!);
        Assert.All(ko!, f => Assert.Equal("Venter", f.Status));
        Assert.All(ko!, f => Assert.Equal(bruker.VirksomhetId, f.VirksomhetId));

        // Godkjenn ett forslag — skal opprette en ekte TjenesteRegelverksreferanseEntitet-kobling.
        var forsteForslag = ko![0];
        var godkjennSvar = await _client.SendAsync(MedBruker(
            HttpMethod.Post, $"/api/tjenester/regelverksreferanse-forslag/{forsteForslag.Id}/godkjenn", bruker.Id));
        Assert.Equal(HttpStatusCode.OK, godkjennSvar.StatusCode);
        await using (var db = _fixture.NyDbContext())
        {
            Assert.True(await db.TjenesteRegelverksreferanser.AnyAsync(
                r => r.TjenesteId == forsteForslag.TjenesteId && r.TilEid == forsteForslag.TilEid));
        }

        // Et allerede godkjent forslag kan ikke godkjennes på nytt.
        var godkjennIgjenSvar = await _client.SendAsync(MedBruker(
            HttpMethod.Post, $"/api/tjenester/regelverksreferanse-forslag/{forsteForslag.Id}/godkjenn", bruker.Id));
        Assert.Equal(HttpStatusCode.BadRequest, godkjennIgjenSvar.StatusCode);

        // Kjøres køen på nytt: det nettopp godkjente forslagets tjeneste har nå en ekte referanse, og
        // skal derfor IKKE dukke opp igjen i "Venter"-listen for denne tjenesten.
        var koEtterGodkjenning = await (await _client.SendAsync(
                MedBruker(HttpMethod.Get, "/api/tjenester/regelverksreferanse-forslag", bruker.Id)))
            .Content.ReadFromJsonAsync<List<TjenesteRegelverksreferanseForslagDto>>(JsonInnstillinger);
        Assert.DoesNotContain(koEtterGodkjenning!, f => f.Id == forsteForslag.Id);

        if (koEtterGodkjenning!.Count > 0)
        {
            var toAvvise = koEtterGodkjenning[0];
            var avvisSvar = await _client.SendAsync(MedBruker(
                HttpMethod.Post, $"/api/tjenester/regelverksreferanse-forslag/{toAvvise.Id}/avvis", bruker.Id));
            Assert.Equal(HttpStatusCode.OK, avvisSvar.StatusCode);

            var koEtterAvvisning = await (await _client.SendAsync(
                    MedBruker(HttpMethod.Get, "/api/tjenester/regelverksreferanse-forslag", bruker.Id)))
                .Content.ReadFromJsonAsync<List<TjenesteRegelverksreferanseForslagDto>>(JsonInnstillinger);
            Assert.DoesNotContain(koEtterAvvisning!, f => f.Id == toAvvise.Id);

            var alleSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester/regelverksreferanse-forslag?status=Alle", bruker.Id));
            var alle = await alleSvar.Content.ReadFromJsonAsync<List<TjenesteRegelverksreferanseForslagDto>>(JsonInnstillinger);
            Assert.Contains(alle!, f => f.Id == toAvvise.Id && f.Status == "Avvist");
        }
    }

    [Fact]
    public async Task Godkjenn_ukjent_forslag_gir_404()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(
            HttpMethod.Post, $"/api/tjenester/regelverksreferanse-forslag/{Guid.NewGuid()}/godkjenn", bruker.Id));
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }
}
