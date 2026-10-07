using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for `/api/gruppemedlemskap` (issue #164, «gruppe av gruppe») mot ekte embedded
/// Postgres — samme mønster som <see cref="MyndighetstildelingEndepunktTests"/>, som er den tilsvarende
/// kanten ned til en konkret virksomhet. [Ny, issue #285 AC6] utvidet med godkjenn/avvis for
/// KI-foreslåtte medlemskap.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class GruppeMedlemskapEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web);

    public GruppeMedlemskapEndepunktTests(EmbeddedPostgresApiFixture fixture)
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

    private async Task<BegrepDto> OpprettGruppebegrepAsync(Guid brukerId, Guid lovId, string term)
    {
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/gruppebegrep", brukerId, new { LovkildeId = lovId, Term = term, Nodetype = "klasse" }));
        svar.EnsureSuccessStatusCode();
        return (await svar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger))!;
    }

    [Fact]
    public async Task Oppretter_gruppemedlemskap_med_status_validert()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (hjemmelId, hjemmelEid) = await OpprettRettskildeMedParagrafAsync();
        var overordnet = await OpprettGruppebegrepAsync(brukerId, lovId, $"forvaltningsomradet-{Guid.NewGuid():N}");
        var underordnet = await OpprettGruppebegrepAsync(brukerId, lovId, $"sprakutviklingskommuner-{Guid.NewGuid():N}");

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/gruppemedlemskap", brukerId, new
        {
            OverordnetGruppeBegrepId = overordnet.Id, UnderordnetGruppeBegrepId = underordnet.Id, HjemmelRettskildeId = hjemmelId,
            Paragrafspenn = new[] { new { FraEid = hjemmelEid, TilEid = (string?)null } },
        }));
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var medlemskap = await svar.Content.ReadFromJsonAsync<GruppeMedlemskapDto>(JsonInnstillinger);
        Assert.Equal("validert", medlemskap!.Status);

        var medlemsgrupper = await _client.GetFromJsonAsync<List<GruppeMedlemskapDto>>(
            $"/api/gruppebegrep/{overordnet.Id}/medlemsgrupper", JsonInnstillinger);
        Assert.Single(medlemsgrupper!, m => m.Id == medlemskap.Id);
    }

    [Fact]
    public async Task Godkjenn_endrer_status_fra_foreslatt_av_ai_til_validert()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, _) = await OpprettRettskildeMedParagrafAsync();
        var (hjemmelId, hjemmelEid) = await OpprettRettskildeMedParagrafAsync();
        var overordnet = await OpprettGruppebegrepAsync(brukerId, lovId, $"ki-over-{Guid.NewGuid():N}");
        var underordnet = await OpprettGruppebegrepAsync(brukerId, lovId, $"ki-under-{Guid.NewGuid():N}");

        await using var db = _fixture.NyDbContext();
        var medlemskap = new GruppeMedlemskapEntitet
        {
            Id = Guid.NewGuid(), OverordnetGruppeBegrepId = overordnet.Id, UnderordnetGruppeBegrepId = underordnet.Id,
            HjemmelRettskildeId = hjemmelId, ParagrafspennJson = "[{\"FraEid\":\"" + hjemmelEid + "\",\"TilEid\":null}]",
            Status = "foreslatt_av_ai", OpprettetAv = "system-ki", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.GruppeMedlemskap.Add(medlemskap);
        await db.SaveChangesAsync();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/gruppemedlemskap/{medlemskap.Id}/godkjenn", brukerId));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var godkjent = await svar.Content.ReadFromJsonAsync<GruppeMedlemskapDto>(JsonInnstillinger);
        Assert.Equal("validert", godkjent!.Status);
    }

    [Fact]
    public async Task Avvis_sletter_kun_foreslatt_av_ai_rad()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, _) = await OpprettRettskildeMedParagrafAsync();
        var (hjemmelId, hjemmelEid) = await OpprettRettskildeMedParagrafAsync();
        var overordnet = await OpprettGruppebegrepAsync(brukerId, lovId, $"ki-avvis-over-{Guid.NewGuid():N}");
        var underordnet = await OpprettGruppebegrepAsync(brukerId, lovId, $"ki-avvis-under-{Guid.NewGuid():N}");

        await using var db = _fixture.NyDbContext();
        var medlemskap = new GruppeMedlemskapEntitet
        {
            Id = Guid.NewGuid(), OverordnetGruppeBegrepId = overordnet.Id, UnderordnetGruppeBegrepId = underordnet.Id,
            HjemmelRettskildeId = hjemmelId, ParagrafspennJson = "[{\"FraEid\":\"" + hjemmelEid + "\",\"TilEid\":null}]",
            Status = "foreslatt_av_ai", OpprettetAv = "system-ki", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.GruppeMedlemskap.Add(medlemskap);
        await db.SaveChangesAsync();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/gruppemedlemskap/{medlemskap.Id}", brukerId));
        Assert.Equal(HttpStatusCode.NoContent, svar.StatusCode);
    }
}
