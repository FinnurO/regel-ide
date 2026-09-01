using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for hardsletting av <c>/api/virksomhet-kandidater</c> (enkeltrad + massehardsletting)
/// mot ekte embedded Postgres, samme mønster som <c>NavnekandidaterEndepunktTests</c>. Den definerende
/// forskjellen fra navnekandidater testes eksplisitt her: KUN 'Avvist'-rader er slettbare — se
/// <see cref="VirksomhetKandidatTjeneste.HardslettAvvistAsync"/>/<see cref="VirksomhetKandidatTjeneste.HardslettAlleAvvisteAsync"/>
/// for hvorfor 'Godkjent' (en ekte tekst-tagg som ikke kan fjernes i etterkant) og 'Venter' (skal
/// behandles, ikke forsvinne) begge er utelukket.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class VirksomhetKandidaterSlettEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web);

    public VirksomhetKandidaterSlettEndepunktTests(EmbeddedPostgresApiFixture fixture)
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

    /// <summary>Setter opp en rettskilde+node med kontrollert tekst, en virksomhet, og (valgfritt) en
    /// navneform-Begrep som matcher de FØRSTE 4 tegnene i teksten — nok til at GodkjennAsync-sporet
    /// (kravspek §4.2 pkt. 5) faktisk lykkes i testene som trenger det.</summary>
    private async Task<(Guid RettskildeId, string NodeEid, Guid VirksomhetId)> OpprettRettskildeVirksomhetOgNavneformAsync(
        string tekst, bool medNavneform)
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = Guid.NewGuid();
        var nodeEid = $"https://test/{rettskildeId:N}/§1/ledd-1";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid, KildeId = "ledd-1", NodeType = "ledd", Tekst = tekst,
        });
        var virksomhetId = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhetId, Navn = $"Test-virksomhet-{virksomhetId:N}" });
        if (medNavneform)
        {
            db.Begreper.Add(new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = virksomhetId, VirksomhetId = null,
                Term = tekst[..4], Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();
        return (rettskildeId, nodeEid, virksomhetId);
    }

    private async Task<VirksomhetKandidatDto> OpprettKandidatAsync(Guid brukerId, Guid virksomhetId, Guid rettskildeId, string nodeEid, int start, int end)
    {
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/virksomhet-kandidater", brukerId,
            new { VirksomhetId = virksomhetId, RettskildeId = rettskildeId, NodeEid = nodeEid, StartOffset = start, EndOffset = end }));
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        return (await svar.Content.ReadFromJsonAsync<VirksomhetKandidatDto>(JsonInnstillinger))!;
    }

    [Fact]
    public async Task Slett_enkeltrad_avvist_via_delete_fjerner_raden_faktisk()
    {
        var brukerId = await HentJuristIdAsync();
        var (rettskildeId, nodeEid, virksomhetId) = await OpprettRettskildeVirksomhetOgNavneformAsync(
            "Vedtak kan påklages til Fiskeridirektoratet innen tre uker.", medNavneform: false);
        var kandidat = await OpprettKandidatAsync(brukerId, virksomhetId, rettskildeId, nodeEid, 0, 4);
        await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/virksomhet-kandidater/{kandidat.Id}/avvis", brukerId));

        var slettSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/virksomhet-kandidater/{kandidat.Id}", brukerId));
        Assert.Equal(HttpStatusCode.NoContent, slettSvar.StatusCode);

        await using var db = _fixture.NyDbContext();
        Assert.False(await db.VirksomhetKandidater.AnyAsync(k => k.Id == kandidat.Id));
    }

    [Fact]
    public async Task Slett_enkeltrad_som_star_i_venter_gir_400_ikke_ufanget_feil()
    {
        var brukerId = await HentJuristIdAsync();
        var (rettskildeId, nodeEid, virksomhetId) = await OpprettRettskildeVirksomhetOgNavneformAsync(
            "Vedtak kan påklages til Reindriftsdirektoratet innen tre uker.", medNavneform: false);
        var kandidat = await OpprettKandidatAsync(brukerId, virksomhetId, rettskildeId, nodeEid, 0, 4);

        var slettSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/virksomhet-kandidater/{kandidat.Id}", brukerId));
        Assert.Equal(HttpStatusCode.BadRequest, slettSvar.StatusCode);

        await using var db = _fixture.NyDbContext();
        Assert.True(await db.VirksomhetKandidater.AnyAsync(k => k.Id == kandidat.Id)); // ikke slettet.
    }

    [Fact]
    public async Task Slett_enkeltrad_med_ukjent_id_gir_404()
    {
        var brukerId = await HentJuristIdAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/virksomhet-kandidater/{Guid.NewGuid()}", brukerId));
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    /// <summary>Bulk-sletting filtrert på rettskilde skal KUN slette den ene rettskildens avviste rad —
    /// en avvist rad i en ANNEN rettskilde skal forbli urørt.</summary>
    [Fact]
    public async Task Bulk_slett_med_rettskildefilter_rammer_kun_avviste_rader_i_valgt_rettskilde()
    {
        var brukerId = await HentJuristIdAsync();
        var (rettskildeA, nodeA, virksomhetA) = await OpprettRettskildeVirksomhetOgNavneformAsync(
            "Vedtak kan påklages til Kystdirektoratet innen tre uker.", medNavneform: false);
        var (rettskildeB, nodeB, virksomhetB) = await OpprettRettskildeVirksomhetOgNavneformAsync(
            "Vedtak kan påklages til Oljedirektoratet innen tre uker.", medNavneform: false);
        var kandidatA = await OpprettKandidatAsync(brukerId, virksomhetA, rettskildeA, nodeA, 0, 4);
        var kandidatB = await OpprettKandidatAsync(brukerId, virksomhetB, rettskildeB, nodeB, 0, 4);
        await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/virksomhet-kandidater/{kandidatA.Id}/avvis", brukerId));
        await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/virksomhet-kandidater/{kandidatB.Id}/avvis", brukerId));

        var slettSvar = await _client.SendAsync(
            MedBruker(HttpMethod.Delete, $"/api/virksomhet-kandidater?rettskildeId={rettskildeA}", brukerId));
        Assert.Equal(HttpStatusCode.OK, slettSvar.StatusCode);
        var resultat = await slettSvar.Content.ReadFromJsonAsync<HardslettVirksomhetKandidaterResultatDto>(JsonInnstillinger);
        Assert.Equal(1, resultat!.AntallSlettet);

        await using var db = _fixture.NyDbContext();
        Assert.False(await db.VirksomhetKandidater.AnyAsync(k => k.Id == kandidatA.Id));
        Assert.True(await db.VirksomhetKandidater.AnyAsync(k => k.Id == kandidatB.Id)); // urørt — annen rettskilde.
    }

    /// <summary>Den definerende forskjellen fra navnekandidater: et eksplisitt status='Godkjent'-filter
    /// på bulk-endepunktet skal gi 400, IKKE stille slette 0 rader — se
    /// VirksomhetKandidatTjeneste.HardslettAlleAvvisteAsync for hvorfor 'Godkjent' er utelukket (en ekte
    /// tekst-tagg som ikke kan fjernes i etterkant).</summary>
    [Fact]
    public async Task Bulk_slett_med_statusGodkjent_gir_400_og_sletter_ingenting()
    {
        var brukerId = await HentJuristIdAsync();
        var tekst = "Vedtak kan påklages til Sjøfartsdirektoratet innen tre uker.";
        var (rettskildeId, nodeEid, virksomhetId) = await OpprettRettskildeVirksomhetOgNavneformAsync(tekst, medNavneform: true);
        var kandidat = await OpprettKandidatAsync(brukerId, virksomhetId, rettskildeId, nodeEid, 0, 4);
        var godkjennSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/virksomhet-kandidater/{kandidat.Id}/godkjenn", brukerId));
        Assert.Equal(HttpStatusCode.OK, godkjennSvar.StatusCode);

        var slettSvar = await _client.SendAsync(
            MedBruker(HttpMethod.Delete, $"/api/virksomhet-kandidater?rettskildeId={rettskildeId}&status=Godkjent", brukerId));
        Assert.Equal(HttpStatusCode.BadRequest, slettSvar.StatusCode);

        await using var db = _fixture.NyDbContext();
        Assert.True(await db.VirksomhetKandidater.AnyAsync(k => k.Id == kandidat.Id)); // ikke slettet.
        Assert.True(await db.TekstTagger.AnyAsync(t => t.RettskildeId == rettskildeId && t.VirksomhetId == virksomhetId)); // taggen består.
    }
}
