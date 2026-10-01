using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #290] Integrasjonstester for (1) Handling-regelverksreferanseforslag-køen (KI-assistert
/// paragrafnivå-oppgradering av OppgaveregisterHandlingSeed sine dokumentnivå-koblinger), og (2) den
/// utvidede GET /api/rettskilder/{id}/statistikk (antallTjenester teller nå med Handling-koblede
/// tjenester også, ikke bare direkte TjenesteRegelverksreferanse). Kjører mot samme delte embedded
/// Postgres/Program.cs-oppstartsseeding som RegelverksreferanseforslagOgRettskildeStatistikkEndepunktTests
/// (issue #286) — setter selv opp Handling-nivå testdata direkte via db (Program.cs' egen seeding dekker
/// den ikke, samme "direkte DB-tilgang for testoppsett"-mønster som EmbeddedPostgresApiFixture selv
/// dokumenterer).
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class HandlingRegelverksreferanseforslagEndepunktTests
{
    private readonly EmbeddedPostgresApiFixture _fixture;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public HandlingRegelverksreferanseforslagEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    /// <summary>
    /// Henter DEN EKTE alkoholloven (ikke en av de flere andre "alkohol"-tittel-treffene i seed-korpuset —
    /// bl.a. en lokal forskrift/retningslinjer UTEN noen "paragraf"-nivå-node i det hele tatt, kun rene
    /// kapittel→ledd-trær — bekreftet empirisk 2026-09-30). Denne testen trenger spesifikt EN rettskilde
    /// med ekte paragraf-struktur (for ParagrafEid-bekreftelse), så matcher presist på tittelens eget
    /// "(alkoholloven)"-parentetiske kortnavn i stedet for det generiske "alkohol"-søket
    /// RegelverksreferanseforslagOgRettskildeStatistikkEndepunktTests bruker (der ETHVERT "alkohol"-treff
    /// duger, siden den kun gjør en golden-tallsjekk, ikke krever paragrafstruktur).
    /// </summary>
    private async Task<Guid> HentAlkohollovenIdAsync()
    {
        var rettskilder = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        return rettskilder!.First(r => r.Tittel.Contains("(alkoholloven)", StringComparison.OrdinalIgnoreCase)).Id;
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    /// <summary>Setter opp én Oppgaveregister-lignende Handling (under bruker sin egen virksomhet) med
    /// en DOKUMENTNIVÅ HandlingRegelverksreferanseEntitet mot alkoholloven, med en fritekst-henvisning
    /// som nevner TO ekte paragrafnumre — samme "§§ X, Y"-form som det virkelige korpuset. Returnerer
    /// handlingId + de to paragraf-eId-ene (for assertions).</summary>
    private async Task<(Guid HandlingId, Guid RettskildeId, string ForsteParagrafEid, string AndreParagrafEid)> ByggHandlingMedDokumentnivaReferanseAsync(Guid virksomhetId)
    {
        var rettskildeId = await HentAlkohollovenIdAsync();
        await using var db = _fixture.NyDbContext();
        var eli = await db.Rettskilder.Where(r => r.Id == rettskildeId).Select(r => r.Eli!).SingleAsync();

        // Paragraf-NODEN selv har aldri egen Tekst (kun ledd/punkt har, se RettskildeNodeEntitet.Tekst
        // sin doc-kommentar) — plukk to paragrafer som FAKTISK har en tekstbærende ledd-etterkommer,
        // samme mønster som HandlingRegelverksreferanseforslagTjenesteTests i RegelIde.Data.Tests.
        var alleNoder = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).ToListAsync();
        var paragrafPerId = alleNoder.Where(n => n.NodeType == "paragraf").ToDictionary(n => n.Id);
        var paragrafNoder = alleNoder
            .Where(n => n.Tekst != null && n.ParentNodeId != null && paragrafPerId.ContainsKey(n.ParentNodeId.Value))
            .OrderBy(n => n.Sorteringsrekkefolge)
            .Select(n => paragrafPerId[n.ParentNodeId!.Value])
            .DistinctBy(n => n.Id)
            .Take(2)
            .ToList();
        Assert.True(paragrafNoder.Count == 2, "Testforutsetning: alkoholloven må ha minst to paragrafer med tekstbærende etterkommere.");

        var tjeneste = new TjenesteEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhetId, Tittel = $"Oppgaveregisteret — test {Guid.NewGuid():N}",
            Status = "utkast", OpprettetAv = "oppgaveregister-import",
        };
        db.Tjenester.Add(tjeneste);
        var handling = new HandlingEntitet
        {
            Id = Guid.NewGuid(), TjenesteId = tjeneste.Id, Navn = "Søknad om skjenkebevilling (test)", Handlingstype = "soke",
            Status = "utkast", OpprettetAv = "oppgaveregister-import",
        };
        db.Handlinger.Add(handling);
        var forsteNr = paragrafNoder[0].Eid[(eli.Length + 1)..];
        var andreNr = paragrafNoder[1].Eid[(eli.Length + 1)..];
        db.HandlingRegelverksreferanser.Add(new HandlingRegelverksreferanseEntitet
        {
            Id = Guid.NewGuid(), HandlingId = handling.Id, TilRettskildeId = rettskildeId, TilEid = eli,
            KildeHenvisningFritekst = $"§§ {forsteNr[1..]}, {andreNr[1..]}",
        });
        await db.SaveChangesAsync();

        return (handling.Id, rettskildeId, paragrafNoder[0].Eid, paragrafNoder[1].Eid);
    }

    // ---------- GET /api/rettskilder/{id}/statistikk — utvidet antallTjenester (issue #290) ----------

    [Fact]
    public async Task Statistikk_teller_med_tjeneste_koblet_kun_via_handling()
    {
        var bruker = await HentTestbrukerAsync();
        var (_, rettskildeId, _, _) = await ByggHandlingMedDokumentnivaReferanseAsync(bruker.VirksomhetId);

        var svar = await _client.GetAsync($"/api/rettskilder/{rettskildeId}/statistikk");
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var statistikk = await svar.Content.ReadFromJsonAsync<RettskildeStatistikkDto>(JsonInnstillinger);

        // Golden cross-check mot samme telledefinisjon som RettskildeRepository.StatistikkAsync — nå
        // UNION av direkte TjenesteRegelverksreferanse OG Handling-koblede tjenester (issue #290).
        await using var db = _fixture.NyDbContext();
        var viaDirekte = db.TjenesteRegelverksreferanser.Where(r => r.TilRettskildeId == rettskildeId).Select(r => r.TjenesteId);
        var viaHandling = db.HandlingRegelverksreferanser.Where(r => r.TilRettskildeId == rettskildeId)
            .Join(db.Handlinger, r => r.HandlingId, h => h.Id, (r, h) => h.TjenesteId);
        var forventetTjenester = await viaDirekte.Union(viaHandling).Distinct().CountAsync();

        Assert.Equal(forventetTjenester, statistikk!.AntallTjenester);
        Assert.True(statistikk.AntallTjenester >= 1, "Testforutsetning: den nyopprettede tjenesten (kun koblet via Handling) skal telles med.");
    }

    // ---------- Handling-regelverksreferanseforslag-køen ----------

    [Fact]
    public async Task Kjor_uten_bruker_header_gir_400()
    {
        var rettskildeId = await HentAlkohollovenIdAsync();
        var svar = await _client.PostAsJsonAsync(
            "/api/tjenester/handlinger/regelverksreferanse-forslag/kjor", new KjorHandlingRegelverksreferanseforslagRequest([rettskildeId]));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Kjor_med_ukjent_rettskilde_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/handlinger/regelverksreferanse-forslag/kjor", bruker.Id,
            new KjorHandlingRegelverksreferanseforslagRequest([Guid.NewGuid()])));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Kjor_oppretter_ventende_forslag_som_kan_godkjennes_og_avvises()
    {
        var bruker = await HentTestbrukerAsync();
        var (handlingId, rettskildeId, forsteParagrafEid, _) = await ByggHandlingMedDokumentnivaReferanseAsync(bruker.VirksomhetId);

        var kjorSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/handlinger/regelverksreferanse-forslag/kjor", bruker.Id,
            new KjorHandlingRegelverksreferanseforslagRequest([rettskildeId])));
        Assert.Equal(HttpStatusCode.OK, kjorSvar.StatusCode);
        var resultat = await kjorSvar.Content.ReadFromJsonAsync<KjorHandlingRegelverksreferanseforslagResponsDto>(JsonInnstillinger);
        Assert.NotNull(resultat);
        Assert.True(resultat!.AntallKandidatrader >= 1);
        Assert.True(resultat.AntallNyeForslag >= 1);

        var koSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester/handlinger/regelverksreferanse-forslag", bruker.Id));
        Assert.Equal(HttpStatusCode.OK, koSvar.StatusCode);
        var ko = await koSvar.Content.ReadFromJsonAsync<List<HandlingRegelverksreferanseForslagDto>>(JsonInnstillinger);
        Assert.NotEmpty(ko!);
        Assert.Contains(ko!, f => f.HandlingId == handlingId && f.TilEid == forsteParagrafEid);
        Assert.All(ko!.Where(f => f.HandlingId == handlingId), f => Assert.Equal("Venter", f.Status));

        var forsteForslag = ko!.First(f => f.HandlingId == handlingId);
        var godkjennSvar = await _client.SendAsync(MedBruker(
            HttpMethod.Post, $"/api/tjenester/handlinger/regelverksreferanse-forslag/{forsteForslag.Id}/godkjenn", bruker.Id));
        Assert.Equal(HttpStatusCode.OK, godkjennSvar.StatusCode);
        await using (var db = _fixture.NyDbContext())
        {
            var ekteReferanse = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == handlingId);
            Assert.Equal(forsteForslag.TilEid, ekteReferanse.TilEid);
        }

        // Et allerede godkjent forslag kan ikke godkjennes på nytt.
        var godkjennIgjenSvar = await _client.SendAsync(MedBruker(
            HttpMethod.Post, $"/api/tjenester/handlinger/regelverksreferanse-forslag/{forsteForslag.Id}/godkjenn", bruker.Id));
        Assert.Equal(HttpStatusCode.BadRequest, godkjennIgjenSvar.StatusCode);

        // Avvis et evt. gjenværende ventende forslag for samme handling (den andre paragrafkandidaten).
        var koEtterGodkjenning = await (await _client.SendAsync(
                MedBruker(HttpMethod.Get, "/api/tjenester/handlinger/regelverksreferanse-forslag", bruker.Id)))
            .Content.ReadFromJsonAsync<List<HandlingRegelverksreferanseForslagDto>>(JsonInnstillinger);
        Assert.DoesNotContain(koEtterGodkjenning!, f => f.Id == forsteForslag.Id);

        if (koEtterGodkjenning!.Any(f => f.HandlingId == handlingId))
        {
            var toAvvise = koEtterGodkjenning.First(f => f.HandlingId == handlingId);
            var avvisSvar = await _client.SendAsync(MedBruker(
                HttpMethod.Post, $"/api/tjenester/handlinger/regelverksreferanse-forslag/{toAvvise.Id}/avvis", bruker.Id));
            Assert.Equal(HttpStatusCode.OK, avvisSvar.StatusCode);

            var alleSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester/handlinger/regelverksreferanse-forslag?status=Alle", bruker.Id));
            var alle = await alleSvar.Content.ReadFromJsonAsync<List<HandlingRegelverksreferanseForslagDto>>(JsonInnstillinger);
            Assert.Contains(alle!, f => f.Id == toAvvise.Id && f.Status == "Avvist");
        }
    }

    [Fact]
    public async Task Godkjenn_ukjent_forslag_gir_404()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(
            HttpMethod.Post, $"/api/tjenester/handlinger/regelverksreferanse-forslag/{Guid.NewGuid()}/godkjenn", bruker.Id));
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }
}
