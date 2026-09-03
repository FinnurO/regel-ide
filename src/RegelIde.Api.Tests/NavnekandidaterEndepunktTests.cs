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

    /// <summary>[Restrukturert, 2026-09-03] Brukte TIDLIGERE "havnetilsynet" (det nå slettede
    /// suffiksmønsterets liten-forbokstav-gren) som "gruppe"-kandidat-kilde — under DENNE arkitekturen
    /// gir en liten-forbokstav-forekomst uten suffiksmønster INGEN kandidat i det hele tatt (se
    /// NavnekandidatOppdagelseTjeneste sin klassekommentar). Bruker i stedet "Kommunen" — en
    /// FasteRollesubstantiv-bøyningsform, UENDRET mekanisme.</summary>
    [Fact]
    public async Task Sveip_godkjenning_og_avvisning_ende_til_ende_for_gruppekandidat()
    {
        var brukerId = await HentJuristIdAsync();
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Kommunen skal føre tilsyn med dette.");

        var sveipSvar = await _client.SendAsync(
            MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeId }));
        Assert.Equal(HttpStatusCode.OK, sveipSvar.StatusCode);
        var sveipResultat = await sveipSvar.Content.ReadFromJsonAsync<SveipNavnekandidaterResultatDto>(JsonInnstillinger);
        Assert.Equal(1, sveipResultat!.AntallNyeKandidater);

        var listeSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}", JsonInnstillinger);
        var kandidat = Assert.Single(listeSvar!);
        Assert.Equal("gruppe", kandidat.Kategori);
        Assert.Equal("kommunen", kandidat.ForeslattTekst);
        Assert.Equal("Venter", kandidat.Status);

        var godkjennSvar = await _client.SendAsync(
            MedBruker(HttpMethod.Post, $"/api/navnekandidater/{kandidat.Id}/godkjenn", brukerId));
        Assert.Equal(HttpStatusCode.OK, godkjennSvar.StatusCode);
        var godkjent = await godkjennSvar.Content.ReadFromJsonAsync<NavnekandidatDto>(JsonInnstillinger);
        Assert.Equal("Godkjent", godkjent!.Status);

        // Gruppebegrepet skal nå faktisk finnes i databasen (docs/20 §2.4-identitet: Term+LovkildeId).
        await using var db = _fixture.NyDbContext();
        var gruppebegrep = await db.Begreper.SingleOrDefaultAsync(
            b => b.Begrepskategori == "gruppe" && b.LovkildeId == rettskildeId && b.Term == "kommunen");
        Assert.NotNull(gruppebegrep);

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
    public async Task Godkjenn_batch_behandler_bade_gruppe_og_virksomhet_kategori_i_samme_kall()
    {
        var brukerId = await HentJuristIdAsync();
        // [Restrukturert, 2026-09-03] "Kommunen" i stedet for det gamle suffiksmønsterets "havnetilsynet"
        // — se Sveip_godkjenning_og_avvisning_ende_til_ende_for_gruppekandidat sin kommentar.
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            "Kommunen skal føre tilsyn, og vedtak kan påklages til Miljødirektoratet innen tre uker.");
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeId }));

        var listeSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}", JsonInnstillinger);
        var kandidater = listeSvar!;
        Assert.Contains(kandidater, k => k.Kategori == "gruppe");
        Assert.Contains(kandidater, k => k.Kategori == "virksomhet");

        var batchSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/godkjenn-batch", brukerId,
            new { Ider = kandidater.Select(k => k.Id) }));
        Assert.Equal(HttpStatusCode.OK, batchSvar.StatusCode);
        var resultat = await batchSvar.Content.ReadFromJsonAsync<NavnekandidatBatchResultatDto>(JsonInnstillinger);
        Assert.Equal(kandidater.Count, resultat!.Rader.Count);
        Assert.All(resultat.Rader, r => Assert.True(r.Ok, r.Feil));
        Assert.All(resultat.Rader, r => Assert.Equal("Godkjent", r.Resultat!.Status));

        // 'gruppe'-raden skal ha opprettet et ekte gruppebegrep (samme sjekk som enkeltrad-testen over) —
        // batchen ruller IKKE bare status, den kaller den faktiske GodkjennAsync-forgreiningen per rad.
        await using var db = _fixture.NyDbContext();
        var gruppebegrep = await db.Begreper.SingleOrDefaultAsync(
            b => b.Begrepskategori == "gruppe" && b.LovkildeId == rettskildeId && b.Term == "kommunen");
        Assert.NotNull(gruppebegrep);
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

    // ---------- Sletting (2026-08-30) — se docs-kommentaren i NavnekandidatOppdagelseTjeneste.SlettAsync/
    // SlettAlleAsync for hvorfor "avvis" alene ikke holder for ytelsestest-scenarioet (posisjonsbasert
    // idempotens). ----------

    [Fact]
    public async Task Slett_enkeltrad_fjerner_kandidaten_faktisk()
    {
        var brukerId = await HentJuristIdAsync();
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Vedtak kan påklages til Fiskeridirektoratet innen tre uker.");
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeId }));
        var listeSvar = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeId}", JsonInnstillinger);
        var kandidat = Assert.Single(listeSvar!);

        await using (var db = _fixture.NyDbContext())
        {
            Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeId));
        }

        var slettSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/navnekandidater/{kandidat.Id}", brukerId));
        Assert.Equal(HttpStatusCode.NoContent, slettSvar.StatusCode);

        await using (var db = _fixture.NyDbContext())
        {
            Assert.Equal(0, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeId));
        }
    }

    [Fact]
    public async Task Slett_enkeltrad_med_ukjent_id_gir_404_ikke_ufanget_feil()
    {
        var brukerId = await HentJuristIdAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/navnekandidater/{Guid.NewGuid()}", brukerId));
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    /// <summary>Bulk-sletting filtrert på én rettskilde skal KUN slette den rettskildens kandidater —
    /// en annen rettskildes kandidat (opprettet i samme testkjøring) skal forbli urørt.</summary>
    [Fact]
    public async Task Slett_alle_med_rettskildefilter_sletter_kun_matchende_rettskildes_rader()
    {
        var brukerId = await HentJuristIdAsync();
        var rettskildeA = await OpprettRettskildeMedNodeAsync("Vedtak kan påklages til Reindriftsdirektoratet innen tre uker.");
        var rettskildeB = await OpprettRettskildeMedNodeAsync("Vedtak kan påklages til Kystdirektoratet innen tre uker.");
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeA }));
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeB }));

        var slettSvar = await _client.SendAsync(
            MedBruker(HttpMethod.Delete, $"/api/navnekandidater?rettskildeId={rettskildeA}", brukerId));
        Assert.Equal(HttpStatusCode.OK, slettSvar.StatusCode);
        var resultat = await slettSvar.Content.ReadFromJsonAsync<SlettNavnekandidaterResultatDto>(JsonInnstillinger);
        Assert.Equal(1, resultat!.AntallSlettet);

        await using var db = _fixture.NyDbContext();
        Assert.Equal(0, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeA));
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeB)); // urørt.
    }

    /// <summary>[Ny, «flytt Slett inn i massehandling-raden», 2026-09-02] Sletting av et PRESIST
    /// avkrysset utvalg (POST /slett-batch) — komplementær til filter-baserte Slett_alle-testen over.
    /// Samme "ukjent id rapporteres som feilet rad, gyldig rad rulles IKKE tilbake"-mønster som
    /// Avvis_batch-testen over.</summary>
    [Fact]
    public async Task Slett_batch_sletter_presist_valgte_rader_og_rapporterer_ukjent_id_som_feilet()
    {
        var brukerId = await HentJuristIdAsync();
        var rettskildeA = await OpprettRettskildeMedNodeAsync("Vedtak kan påklages til Reindriftsdirektoratet innen tre uker.");
        var rettskildeB = await OpprettRettskildeMedNodeAsync("Vedtak kan påklages til Kystdirektoratet innen tre uker.");
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeA }));
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/sveip", brukerId, new { RettskildeId = rettskildeB }));

        var listeA = await _client.GetFromJsonAsync<List<NavnekandidatDto>>(
            $"/api/navnekandidater?rettskildeId={rettskildeA}", JsonInnstillinger);
        var kandidatA = Assert.Single(listeA!);
        var ukjentId = Guid.NewGuid();

        var batchSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/navnekandidater/slett-batch", brukerId,
            new { Ider = new[] { kandidatA.Id, ukjentId } }));
        Assert.Equal(HttpStatusCode.OK, batchSvar.StatusCode);
        var resultat = await batchSvar.Content.ReadFromJsonAsync<NavnekandidatSlettBatchResultatDto>(JsonInnstillinger);
        Assert.Equal(2, resultat!.Rader.Count);

        var gyldigRad = resultat.Rader.Single(r => r.Id == kandidatA.Id);
        Assert.True(gyldigRad.Ok);
        var ukjentRad = resultat.Rader.Single(r => r.Id == ukjentId);
        Assert.False(ukjentRad.Ok);
        Assert.Contains(ukjentId.ToString(), ukjentRad.Feil);

        // Kun den valgte raden (rettskilde A) er faktisk borte — rettskilde B, som ALDRI var med i
        // batchen, skal forbli urørt (nøyaktig det som skiller "Slett valgte" fra det filter-baserte
        // "Slett alle kandidater").
        await using var db = _fixture.NyDbContext();
        Assert.Equal(0, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeA));
        Assert.Equal(1, await db.Navnekandidater.CountAsync(k => k.RettskildeId == rettskildeB));
    }
}
