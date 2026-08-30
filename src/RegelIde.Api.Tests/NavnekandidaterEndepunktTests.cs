using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for <c>/api/navnekandidater</c> (docs/13-backlog.md §9) — sveip, godkjenn, avvis,
/// mot ekte embedded Postgres. Samme mønster som <c>HandlingEndepunktTests</c>. Rettskilde+node settes
/// opp direkte i DB-en (samme "kontrollert syntetisk tekst" som Data.Tests-varianten) i stedet for en
/// ekte Lovdata-import — enklere å styre presist HVILKE mønstre teksten skal treffe.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class NavnekandidaterEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public NavnekandidaterEndepunktTests(EmbeddedPostgresApiFixture fixture)
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
            // Importrolle="referanse" — unngår ck_rettskilder_akn_xml (krever akn_xml IS NOT NULL for
            // Importrolle="primaer", default), samme mønster som RettsligStatusKontrastTests.
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = $"https://test/{rettskildeId:N}/§1/ledd-1",
            KildeId = "ledd-1", NodeType = "ledd", Tekst = tekst,
        });
        await db.SaveChangesAsync();
        return rettskildeId;
    }

    [Fact]
    public async Task Sveip_uten_bruker_id_header_gir_400()
    {
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Alle skip skal melde fra til havnetilsynet før anløp.");
        var svar = await _client.PostAsJsonAsync("/api/navnekandidater/sveip", new { RettskildeId = rettskildeId });
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Sveip_godkjenning_og_avvisning_ende_til_ende_for_rollekandidat()
    {
        var brukerId = await HentJuristIdAsync();
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Alle skip skal melde fra til havnetilsynet før anløp.");

        var sveipSvar = await _client.SendAsync(
            MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeId }));
        Assert.Equal(HttpStatusCode.OK, sveipSvar.StatusCode);
        var sveipResultat = await sveipSvar.Content.ReadFromJsonAsync<SveipNavnekandidaterResultatDto>(JsonInnstillinger);
        Assert.Equal(1, sveipResultat!.AntallNyeKandidater);

        var listeSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}", JsonInnstillinger);
        var kandidat = Assert.Single(listeSvar!);
        Assert.Equal("rolle", kandidat.Kategori);
        Assert.Equal("havnetilsynet", kandidat.ForeslattTekst);
        Assert.Equal("Venter", kandidat.Status);

        var godkjennSvar = await _client.SendAsync(
            MedBruker(HttpMethod.Post, $"/api/navnekandidater/{kandidat.Id}/godkjenn", brukerId));
        Assert.Equal(HttpStatusCode.OK, godkjennSvar.StatusCode);
        var godkjent = await godkjennSvar.Content.ReadFromJsonAsync<NavnekandidatDto>(JsonInnstillinger);
        Assert.Equal("Godkjent", godkjent!.Status);

        // Rollebegrepet skal nå faktisk finnes i databasen (docs/20 §2.4-identitet: Term+LovkildeId).
        await using var db = _fixture.NyDbContext();
        var rollebegrep = await db.Begreper.SingleOrDefaultAsync(
            b => b.Begrepskategori == "rolle" && b.LovkildeId == rettskildeId && b.Term == "havnetilsynet");
        Assert.NotNull(rollebegrep);

        // Kandidaten er allerede Godkjent — et nytt godkjenn-forsøk skal feile (kun 'Venter' kan behandles).
        var andreGodkjennSvar = await _client.SendAsync(
            MedBruker(HttpMethod.Post, $"/api/navnekandidater/{kandidat.Id}/godkjenn", brukerId));
        Assert.Equal(HttpStatusCode.BadRequest, andreGodkjennSvar.StatusCode);
    }

    [Fact]
    public async Task Sveip_og_avvisning_av_virksomhetskandidat_oppretter_intet_begrep()
    {
        var brukerId = await HentJuristIdAsync();
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Vedtak kan påklages til Miljødirektoratet innen tre uker.");

        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeId }));
        var listeSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}", JsonInnstillinger);
        var kandidat = Assert.Single(listeSvar!);
        Assert.Equal("virksomhet", kandidat.Kategori);

        var avvisSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/navnekandidater/{kandidat.Id}/avvis", brukerId));
        Assert.Equal(HttpStatusCode.OK, avvisSvar.StatusCode);
        var avvist = await avvisSvar.Content.ReadFromJsonAsync<NavnekandidatDto>(JsonInnstillinger);
        Assert.Equal("Avvist", avvist!.Status);

        // Med status='Alle' skal den avviste raden fortsatt vises i lista (docs/20 §2.6-ekvivalent).
        var alleSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}&status=Alle", JsonInnstillinger);
        Assert.Single(alleSvar!, k => k.Status == "Avvist");

        // Uten ?status= (default) vises den IKKE lenger (kun 'Venter').
        var ventendeSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}", JsonInnstillinger);
        Assert.Empty(ventendeSvar!);
    }

    // ---------- Massehandling (2026-08-30) — se docs-kommentaren i NavnekandidaterListe.tsx ----------

    [Fact]
    public async Task Godkjenn_batch_behandler_bade_rolle_og_virksomhet_kategori_i_samme_kall()
    {
        var brukerId = await HentJuristIdAsync();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            "Alle skip skal melde fra til havnetilsynet, og vedtak kan påklages til Miljødirektoratet innen tre uker.");
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeId }));

        var listeSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}", JsonInnstillinger);
        var kandidater = listeSvar!;
        Assert.Contains(kandidater, k => k.Kategori == "rolle");
        Assert.Contains(kandidater, k => k.Kategori == "virksomhet");

        var batchSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/godkjenn-batch", brukerId,
            new { Ider = kandidater.Select(k => k.Id) }));
        Assert.Equal(HttpStatusCode.OK, batchSvar.StatusCode);
        var resultat = await batchSvar.Content.ReadFromJsonAsync<NavnekandidatBatchResultatDto>(JsonInnstillinger);
        Assert.Equal(kandidater.Count, resultat!.Rader.Count);
        Assert.All(resultat.Rader, r => Assert.True(r.Ok, r.Feil));
        Assert.All(resultat.Rader, r => Assert.Equal("Godkjent", r.Resultat!.Status));

        // 'rolle'-raden skal ha opprettet et ekte rollebegrep (samme sjekk som enkeltrad-testen over) —
        // batchen ruller IKKE bare status, den kaller den faktiske GodkjennAsync-forgreiningen per rad.
        await using var db = _fixture.NyDbContext();
        var rollebegrep = await db.Begreper.SingleOrDefaultAsync(
            b => b.Begrepskategori == "rolle" && b.LovkildeId == rettskildeId && b.Term == "havnetilsynet");
        Assert.NotNull(rollebegrep);
    }

    [Fact]
    public async Task Avvis_batch_rapporterer_ukjent_id_som_feilet_rad_uten_a_rulle_tilbake_den_gyldige()
    {
        var brukerId = await HentJuristIdAsync();
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Vedtak kan påklages til Miljødirektoratet innen tre uker.");
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeId }));

        var listeSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}", JsonInnstillinger);
        var gyldigId = Assert.Single(listeSvar!).Id;
        var ukjentId = Guid.NewGuid();

        var batchSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/avvis-batch", brukerId,
            new { Ider = new[] { gyldigId, ukjentId } }));
        Assert.Equal(HttpStatusCode.OK, batchSvar.StatusCode);
        var resultat = await batchSvar.Content.ReadFromJsonAsync<NavnekandidatBatchResultatDto>(JsonInnstillinger);
        Assert.Equal(2, resultat!.Rader.Count);

        var gyldigRad = resultat.Rader.Single(r => r.Id == gyldigId);
        Assert.True(gyldigRad.Ok);
        Assert.Equal("Avvist", gyldigRad.Resultat!.Status);

        var ukjentRad = resultat.Rader.Single(r => r.Id == ukjentId);
        Assert.False(ukjentRad.Ok);
        Assert.Contains(ukjentId.ToString(), ukjentRad.Feil);

        // Bekreft at den gyldige raden faktisk ble oppdatert i databasen (ikke rullet tilbake pga.
        // den andre radens feil) — nøyaktig den per-rad-, ikke-alt-eller-ingenting-oppførselen batchen skal ha.
        var etterBatchSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}&status=Alle", JsonInnstillinger);
        Assert.Single(etterBatchSvar!, k => k.Id == gyldigId && k.Status == "Avvist");
    }
}
