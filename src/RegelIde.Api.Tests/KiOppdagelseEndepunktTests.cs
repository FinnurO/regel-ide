using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for `/api/ki-oppdagelse/*` (issue #285) mot ekte embedded Postgres og den DELTE
/// fixturens tvungne <see cref="KiAgentKlientStub"/> (se <see cref="EmbeddedPostgresApiFixture"/> —
/// aldri ekte, betalte KI-kall i denne testklassen). Stubben returnerer et svar formet for de FIRE
/// andre KI-forslags-agentene (begrep/tjeneste/handling/full), ikke <see cref="VirksomhetOgGruppeKiOppdagelseTjeneste"/>
/// sitt eget skjema — disse testene verifiserer derfor rørledningen (endepunktet svarer, autentisering
/// virker, et forslag den ikke forstår forkastes trygt) og godkjenn/avvis-køen, IKKE selve KI-ens
/// resonnering (det er <see cref="RegelIde.Data.Tests.VirksomhetOgGruppeKiOppdagelseTjenesteTests"/>
/// sin jobb, med en egen, deterministisk stub som FAKTISK matcher skjemaet — og
/// <see cref="RegelIde.Data.Tests.VirksomhetOgGruppeKiOppdagelseLiveTests"/> sin jobb for ekte
/// KI-verifisering, issue #285 AC1/AC7).
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class KiOppdagelseEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web);

    public KiOppdagelseEndepunktTests(EmbeddedPostgresApiFixture fixture)
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

    private async Task<Guid> OpprettRettskildeMedNodeAsync(string tekst)
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = $"https://test/{rettskildeId:N}/§1", KildeId = "§1",
            NodeType = "paragraf", Nummer = "§ 1", Tekst = tekst,
        });
        await db.SaveChangesAsync();
        return rettskildeId;
    }

    [Fact]
    public async Task Kjor_uten_valgte_rettskilder_gir_400()
    {
        var brukerId = await HentJuristIdAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/ki-oppdagelse/kjor", brukerId,
            new { RettskildeIder = Array.Empty<Guid>() }));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Kjor_uinnlogget_gir_ikke_innlogget_svar()
    {
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Testorganet behandler saker etter denne loven.");
        var svar = await _client.PostAsJsonAsync("/api/ki-oppdagelse/kjor", new { RettskildeIder = new[] { rettskildeId } });
        Assert.False(svar.IsSuccessStatusCode);
    }

    /// <summary>Stubben svarer med et forslag formet for en HELT ANNEN agent (se klassekommentaren) —
    /// forventet, trygt utfall er at det ene «forslaget» forkastes med en tydelig grunn, IKKE en 500.</summary>
    [Fact]
    public async Task Kjor_med_stub_som_ikke_forstar_skjemaet_forkaster_trygt_uten_a_krasje()
    {
        var brukerId = await HentJuristIdAsync();
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Testorganet behandler saker etter denne loven.");

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/ki-oppdagelse/kjor", brukerId,
            new { RettskildeIder = new[] { rettskildeId } }));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var resultat = await svar.Content.ReadFromJsonAsync<KiOppdagelseSamletResultatDto>(JsonInnstillinger);
        Assert.NotNull(resultat);
        // Ingen navnekandidat/rolle/relasjon/gruppemedlemskap ble opprettet av det ugjenkjennelige svaret.
        Assert.All(resultat!.Kandidater, k => Assert.Null(k.NavnekandidatId));
    }

    [Fact]
    public async Task Ko_lister_kun_foreslatt_av_ai_rader_pa_tvers_av_de_tre_entitetstypene()
    {
        var brukerId = await HentJuristIdAsync();
        await using var db = _fixture.NyDbContext();
        var lov = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Ko-testlov", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Rettskilder.Add(lov);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Ko-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        var gruppebegrep = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "gruppe", LovkildeId = lov.Id, Term = $"ko-rolle-{Guid.NewGuid():N}",
            Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(gruppebegrep);
        await db.SaveChangesAsync();
        db.Myndighetstildelinger.Add(new MyndighetstildelingEntitet
        {
            Id = Guid.NewGuid(), GruppeBegrepId = gruppebegrep.Id, VirksomhetId = virksomhet.Id, HjemmelRettskildeId = lov.Id,
            Status = "foreslatt_av_ai", OpprettetAv = "system-ki", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var ko = await _client.GetFromJsonAsync<List<KiForslagKoRadDto>>("/api/ki-oppdagelse/ko", JsonInnstillinger);
        Assert.Contains(ko!, r => r.Type == "myndighetstildeling" && r.Visningstekst.Contains(virksomhet.Navn));
    }
}
