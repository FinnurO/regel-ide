using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, kandidatside-runden, 2026-09-09, issue #216] <c>POST /api/begrepsforekomster/godkjenn-batch</c>
/// og <c>/avvis-batch</c> — massehandlingen begrepskandidatsiden manglet, mens de to andre
/// kandidatkøene har hatt den siden §4.2 pkt. 4.
///
/// <para>Det som faktisk testes er per-rad-feilhåndteringen: én ugyldig id skal IKKE rulle tilbake de
/// gyldige radene. Det er hele poenget med en server-side batch fremfor N separate kall, og det er
/// den egenskapen en løkke i frontend ikke gir. Selve godkjenningslogikken (begrep + tagg opprettes,
/// revalidering mot nodens dåværende tekst) er dekket i <c>BegrepsforekomstTjenesteTests</c> — her
/// verifiseres at batchen faktisk kaller den, ikke bare ruller status.</para>
///
/// <para>Forekomstradene settes inn DIREKTE i basen i stedet for via sveipet. Sveipet krever en tekst
/// som treffer M1/M11, og fikstursen deles av hele testkollektivet — en syntetisk rad med egen
/// rettskilde per test er den isolasjonen resten av API-testene også bruker.</para>
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class BegrepsforekomsterBatchEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public BegrepsforekomsterBatchEndepunktTests(EmbeddedPostgresApiFixture fixture)
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

    /// <summary>Én rettskilde med én punkt-node som inneholder to definisjoner, og en ventende
    /// forekomst per definisjon — formen et M1-sveip på en definisjonsparagraf produserer, og den
    /// situasjonen massehandlingen finnes for.</summary>
    private async Task<(Guid RettskildeId, Guid Virksomhet, Guid Forste, Guid Andre)> OpprettSceneAsync()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = Guid.NewGuid();
        // Unike termer per test — fikstursen deles, og BegrepsregisterTjeneste avviser samme term
        // to ganger i samme virksomhets register.
        var stump = Guid.NewGuid().ToString("N")[..8];
        var forsteTerm = $"alfa{stump}";
        var andreTerm = $"beta{stump}";
        var tekst = $"{forsteTerm}: den første testdefinisjonen, {andreTerm}: den andre testdefinisjonen";
        var nodeEid = $"https://test/{rettskildeId:N}/§1/ledd-1/punkt-1";

        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testforskrift " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid, KildeId = "punkt-1",
            NodeType = "punkt", Tekst = tekst,
        });
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);

        var forste = NyForekomst(rettskildeId, nodeEid, forsteTerm, "den første testdefinisjonen",
            tekst.IndexOf(forsteTerm, StringComparison.Ordinal));
        var andre = NyForekomst(rettskildeId, nodeEid, andreTerm, "den andre testdefinisjonen",
            tekst.IndexOf(andreTerm, StringComparison.Ordinal));
        db.Begrepsforekomster.AddRange(forste, andre);
        await db.SaveChangesAsync();
        return (rettskildeId, virksomhet.Id, forste.Id, andre.Id);
    }

    private static BegrepsforekomstEntitet NyForekomst(
        Guid rettskildeId, string nodeEid, string term, string definisjon, int start) =>
        new()
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, NodeEid = nodeEid,
            Begrep = term, BegrepOriginal = term, Definisjon = definisjon,
            StartOffset = start, EndOffset = start + term.Length,
            Kildetype = "eksplisitt_liste", MonsterId = "M1", Konfidens = "hoy", Scope = "hele_dokumentet",
            Status = "Venter", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public async Task Godkjenn_batch_oppretter_begrep_og_tagg_per_rad_og_rapporterer_ukjent_id_som_feilet()
    {
        var brukerId = await HentJuristIdAsync();
        var (_, virksomhetId, forste, andre) = await OpprettSceneAsync();
        var ukjent = Guid.NewGuid();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begrepsforekomster/godkjenn-batch", brukerId,
            new { Ider = new[] { forste, ukjent, andre }, VirksomhetId = virksomhetId }));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var resultat = await svar.Content.ReadFromJsonAsync<BegrepsforekomstBatchResultatDto>(JsonInnstillinger);
        Assert.Equal(3, resultat!.Rader.Count);

        // Den ugyldige raden feiler ALENE — de to gyldige er godkjent i samme kall.
        var feilet = Assert.Single(resultat.Rader, r => !r.Ok);
        Assert.Equal(ukjent, feilet.Id);
        Assert.Contains(ukjent.ToString(), feilet.Feil);
        Assert.All(resultat.Rader.Where(r => r.Ok), r => Assert.Equal("Godkjent", r.Resultat!.Status));

        // Batchen kaller den faktiske godkjenningen, den ruller ikke bare status: ett ekte begrep i
        // den ANGITTE virksomhetens register per rad, og én tekst-tagg som peker på det.
        await using var db = _fixture.NyDbContext();
        var begreper = await db.Begreper.Where(b => b.VirksomhetId == virksomhetId).ToListAsync();
        Assert.Equal(2, begreper.Count);
        foreach (var begrep in begreper)
        {
            Assert.True(await db.TekstTagger.AnyAsync(t => t.Kind == "begrep" && t.RefId == begrep.Id));
        }
        Assert.All(await db.Begrepsforekomster.Where(f => f.Id == forste || f.Id == andre).ToListAsync(),
            f => Assert.Equal("Godkjent", f.Status));
    }

    [Fact]
    public async Task Avvis_batch_avviser_de_gyldige_selv_om_en_rad_alt_er_behandlet()
    {
        var brukerId = await HentJuristIdAsync();
        var (_, virksomhetId, forste, andre) = await OpprettSceneAsync();

        // Første rad godkjennes enkeltvis først — den kan da IKKE avvises etterpå (kun 'Venter' kan).
        var enkelt = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/begrepsforekomster/{forste}/godkjenn", brukerId,
            new { VirksomhetId = virksomhetId }));
        Assert.Equal(HttpStatusCode.OK, enkelt.StatusCode);

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begrepsforekomster/avvis-batch", brukerId,
            new { Ider = new[] { forste, andre } }));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var resultat = await svar.Content.ReadFromJsonAsync<BegrepsforekomstBatchResultatDto>(JsonInnstillinger);
        var feilet = Assert.Single(resultat!.Rader, r => !r.Ok);
        Assert.Equal(forste, feilet.Id);
        Assert.Contains("Godkjent", feilet.Feil);

        var ok = Assert.Single(resultat.Rader, r => r.Ok);
        Assert.Equal(andre, ok.Id);
        Assert.Equal("Avvist", ok.Resultat!.Status);
    }
}
