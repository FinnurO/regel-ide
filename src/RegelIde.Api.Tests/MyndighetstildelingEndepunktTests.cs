using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for `/api/rollebegrep` og `/api/myndighetstildelinger` (docs/20 §2.4/§2.5,
/// docs/13-backlog.md §8.1 punkt 1) mot ekte embedded Postgres. Samme mønster som
/// <see cref="NavnekandidaterEndepunktTests"/> — rettskilde+node settes opp direkte i DB-en i stedet
/// for en ekte Lovdata-import, enklere å styre presist hvilken paragraf-eId testen refererer.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class MyndighetstildelingEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web);

    public MyndighetstildelingEndepunktTests(EmbeddedPostgresApiFixture fixture)
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

    /// <summary>Rettskilde med ÉN paragraf-node — brukt som både rollebegrepets lov og hjemmelen
    /// (en ekte tildeling ville typisk hatt to ulike rettskilder, men det er irrelevant for det
    /// endepunktet her verifiserer — kun at kalleren MÅ oppgi ekte, eksisterende referanser).</summary>
    private async Task<(Guid RettskildeId, string ParagrafEid)> OpprettRettskildeMedParagrafAsync()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var eid = $"https://test/{rettskildeId:N}/§1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = eid, KildeId = "§1", NodeType = "paragraf", Nummer = "§ 1",
        });
        await db.SaveChangesAsync();
        return (rettskildeId, eid);
    }

    private async Task<Guid> OpprettVirksomhetAsync()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        return virksomhet.Id;
    }

    [Fact]
    public async Task Hent_alle_rollebegrep_inkluderer_nyopprettet_rollebegrep()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, _) = await OpprettRettskildeMedParagrafAsync();
        var term = $"kontrollmyndighet-{Guid.NewGuid():N}";

        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/rollebegrep", brukerId,
            new { LovkildeId = lovId, Term = term }));
        Assert.Equal(HttpStatusCode.Created, opprettSvar.StatusCode);

        var alle = await _client.GetFromJsonAsync<List<BegrepDto>>("/api/rollebegrep", JsonInnstillinger);
        var rollebegrep = Assert.Single(alle!, b => b.Term == term);
        Assert.Equal("rolle", rollebegrep.Begrepskategori);
        Assert.Equal(lovId, rollebegrep.LovkildeId);
    }

    [Fact]
    public async Task Oppretter_myndighetstildeling_med_gyldige_referanser()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (hjemmelId, _) = await OpprettRettskildeMedParagrafAsync();
        var virksomhetId = await OpprettVirksomhetAsync();

        var rollebegrepSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/rollebegrep", brukerId,
            new { LovkildeId = lovId, Term = $"forurensningsmyndighet-{Guid.NewGuid():N}" }));
        var rollebegrep = await rollebegrepSvar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger);

        var tildelingSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/myndighetstildelinger", brukerId, new
        {
            RolleBegrepId = rollebegrep!.Id,
            VirksomhetId = virksomhetId,
            HjemmelRettskildeId = hjemmelId,
            Paragrafspenn = new[] { new { FraEid = paragrafEid, TilEid = (string?)null } },
            Vilkaar = "kommunale avløpsanlegg",
        }));
        Assert.Equal(HttpStatusCode.Created, tildelingSvar.StatusCode);
        var tildeling = await tildelingSvar.Content.ReadFromJsonAsync<MyndighetstildelingDto>(JsonInnstillinger);
        Assert.Equal(virksomhetId, tildeling!.VirksomhetId);
        Assert.Equal("kommunale avløpsanlegg", tildeling.Vilkaar);
        Assert.Single(tildeling.Paragrafspenn);
        Assert.Equal(paragrafEid, tildeling.Paragrafspenn[0].FraEid);

        var forVirksomhet = await _client.GetFromJsonAsync<List<MyndighetstildelingDto>>(
            $"/api/virksomheter/{virksomhetId}/myndighetstildelinger", JsonInnstillinger);
        Assert.Single(forVirksomhet!, t => t.Id == tildeling.Id);
    }

    [Fact]
    public async Task Avvises_med_ikke_eksisterende_rollebegrep_id()
    {
        var brukerId = await HentJuristIdAsync();
        var (_, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (hjemmelId, _) = await OpprettRettskildeMedParagrafAsync();
        var virksomhetId = await OpprettVirksomhetAsync();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/myndighetstildelinger", brukerId, new
        {
            RolleBegrepId = Guid.NewGuid(), // finnes ikke — ingen gjettet fallback
            VirksomhetId = virksomhetId,
            HjemmelRettskildeId = hjemmelId,
            Paragrafspenn = new[] { new { FraEid = paragrafEid, TilEid = (string?)null } },
            Vilkaar = (string?)null,
        }));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);

        var db = await Task.FromResult(_fixture.NyDbContext());
        await using (db)
        {
            Assert.False(await db.Myndighetstildelinger.AnyAsync(t => t.VirksomhetId == virksomhetId));
        }
    }

    [Fact]
    public async Task Avvises_med_ikke_eksisterende_virksomhet_id()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (hjemmelId, _) = await OpprettRettskildeMedParagrafAsync();

        var rollebegrepSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/rollebegrep", brukerId,
            new { LovkildeId = lovId, Term = $"kontrollmyndighet-{Guid.NewGuid():N}" }));
        var rollebegrep = await rollebegrepSvar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger);

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/myndighetstildelinger", brukerId, new
        {
            RolleBegrepId = rollebegrep!.Id,
            VirksomhetId = Guid.NewGuid(), // finnes ikke — ingen gjettet fallback
            HjemmelRettskildeId = hjemmelId,
            Paragrafspenn = new[] { new { FraEid = paragrafEid, TilEid = (string?)null } },
            Vilkaar = (string?)null,
        }));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }
}
