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

    /// <summary>
    /// [Ny, navnekandidat-wizard-runden, 2026-09-07] Setter opp en KOMPLETT, isolert scene for
    /// wizard-testene og setter inn kandidatraden DIREKTE — uten å gå gjennom sveipet.
    ///
    /// <para>
    /// Dette er ikke en snarvei, det er nødvendig isolasjon. <see cref="EmbeddedPostgresApiFixture"/>
    /// deles av HELE testkollektivet, og <c>SveipAsync</c> filtrerer bort treff som allerede er
    /// «dekket» av et eksisterende <see cref="BegrepEntitet"/>. Wizard-testene her OPPRETTER nettopp
    /// slike navneformer som en del av det de verifiserer — bruker de samme navn som sveip-testene
    /// (f.eks. «Fiskeridirektoratet»), forsvinner kandidaten for alle tester som kjører ETTERPÅ, og
    /// fire eksisterende sveip-tester begynner å feile av en grunn som ikke har noe med dem å gjøre.
    /// (Nøyaktig det skjedde første gang disse testene ble skrevet.)
    /// </para>
    /// <para>
    /// Løsningen: et UNIKT navn per test (<paramref name="navn"/> får en GUID-hale), og ingen
    /// avhengighet til mønstergjenkjenningen i det hele tatt — hva sveipet FINNER er grundig dekket
    /// av de eksisterende testene over, og er ikke det disse testene handler om.
    /// </para>
    /// </summary>
    /// <param name="medDepartement">
    /// Når <c>true</c> får rettskilden et <c>AnsvarligDepartement</c> som faktisk finnes i katalogen,
    /// slik at en tagg kan opprettes (taggens <c>VirksomhetId</c> er ikke-nullbar). Når <c>false</c>
    /// testes den dokumenterte degraderingen «ingen tagg, men navneformen kobles likevel».
    /// </param>
    private async Task<Scene> OpprettSceneAsync(
        string navn, string kategori = "virksomhet", string status = "Venter", bool medDepartement = true)
    {
        var unikt = $"{navn}{Guid.NewGuid():N}";
        await using var db = _fixture.NyDbContext();

        string? departementNavn = null;
        if (medDepartement)
        {
            departementNavn = $"Testdepartementet {Guid.NewGuid():N}";
            db.Virksomheter.Add(new Virksomhet
            {
                Id = Guid.NewGuid(), Navn = departementNavn, OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        }

        // Teksten bygges rundt navnet, slik at offsetene er eksakte og QuoteExact blir nøyaktig navnet.
        const string foran = "Vedtak kan påklages til ";
        var tekst = $"{foran}{unikt} innen tre uker.";
        var startOffset = foran.Length;
        var endOffset = startOffset + unikt.Length;

        var rettskildeId = Guid.NewGuid();
        var nodeEid = $"https://test/{rettskildeId:N}/§1/ledd-1";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            // Importrolle="referanse" — se OpprettRettskildeMedNodeAsync for hvorfor (ck_rettskilder_akn_xml).
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId,
            AnsvarligDepartement = departementNavn is null ? null : [departementNavn],
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid,
            KildeId = "ledd-1", NodeType = "ledd", Tekst = tekst,
        });

        var kandidatId = Guid.NewGuid();
        db.Navnekandidater.Add(new NavnekandidatEntitet
        {
            Id = kandidatId, ForeslattTekst = unikt, Kategori = kategori, RettskildeId = rettskildeId,
            NodeEid = nodeEid, StartOffset = startOffset, EndOffset = endOffset, Status = status,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            BehandletAv = status == "Venter" ? null : "test",
            BehandletTidspunkt = status == "Venter" ? null : DateTimeOffset.UtcNow,
        });

        // Målvirksomheten navneformen skal peke PÅ. Eget, unikt navn — den skal ikke kunne forveksles
        // med departementet over.
        var maalId = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet
        {
            Id = maalId, Navn = $"Maalvirksomhet {Guid.NewGuid():N}", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
        return new Scene(kandidatId, rettskildeId, nodeEid, maalId, unikt);
    }

    private sealed record Scene(
        Guid KandidatId, Guid RettskildeId, string NodeEid, Guid MaalVirksomhetId, string Navn);

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
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Vedtak kan påklages til Fiskeridirektoratet innen tre uker.");

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
            "Kommunen skal føre tilsyn, og vedtak kan påklages til Fiskeridirektoratet innen tre uker.");
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
        var rettskildeId = await OpprettRettskildeMedNodeAsync("Vedtak kan påklages til Fiskeridirektoratet innen tre uker.");
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
    // ==================================================================================
    // [Ny, navnekandidat-wizard-runden, 2026-09-07]
    // PATCH (issue #203 pkt. 1) + den lukkede virksomhetskjeden (navneform + navneformgrunn + tagg).
    // Alle scener settes opp med OpprettSceneAsync (direkte innsetting, unike navn) — se dens
    // kommentar for hvorfor disse testene ALDRI skal gå gjennom sveipet.
    // ==================================================================================

    /// <summary>AK3: PATCH på en AVVIST rad endrer teksten OG setter status tilbake til «Venter».
    /// Dette er den eneste veien tilbake fra «Avvist» — se OppdaterAsync sin metodekommentar.</summary>
    [Fact]
    public async Task Patch_paa_avvist_rad_endrer_tekst_og_setter_status_tilbake_til_venter()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Avvistetaten", status: "Avvist");

        var patchSvar = await _client.SendAsync(MedBruker(HttpMethod.Patch, $"/api/navnekandidater/{scene.KandidatId}",
            brukerId, new { ForeslattTekst = "Rettet etatsnavn" }));
        Assert.Equal(HttpStatusCode.OK, patchSvar.StatusCode);
        var patchet = await patchSvar.Content.ReadFromJsonAsync<NavnekandidatDto>(JsonInnstillinger);

        Assert.Equal("Rettet etatsnavn", patchet!.ForeslattTekst);
        Assert.Equal("Venter", patchet.Status);
        // BehandletAv/-Tidspunkt nullstilles — raden er reelt ubehandlet igjen.
        Assert.Null(patchet.BehandletAv);
        Assert.Null(patchet.BehandletTidspunkt);

        // Og den kan nå faktisk behandles på nytt (hele poenget med Avvist -> Venter): avvis-veien
        // krever "Venter", og ville gitt 400 hvis statusen ikke reelt var tilbakestilt i databasen.
        var avvisSvar = await _client.SendAsync(
            MedBruker(HttpMethod.Post, $"/api/navnekandidater/{scene.KandidatId}/avvis", brukerId));
        Assert.Equal(HttpStatusCode.OK, avvisSvar.StatusCode);
    }

    /// <summary>Regex-artefakt-tilfellet ordrett fra bestillingen: «Ø Suldal kommune» -> «Suldal
    /// kommune». Verifiserer at teksten lagres trimmet.</summary>
    [Fact]
    public async Task Patch_retter_regex_artefakt_i_foreslatt_tekst()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Ø Suldal kommune");

        var patchSvar = await _client.SendAsync(MedBruker(HttpMethod.Patch, $"/api/navnekandidater/{scene.KandidatId}",
            brukerId, new { ForeslattTekst = "  Suldal kommune  " }));
        Assert.Equal(HttpStatusCode.OK, patchSvar.StatusCode);
        var patchet = await patchSvar.Content.ReadFromJsonAsync<NavnekandidatDto>(JsonInnstillinger);
        Assert.Equal("Suldal kommune", patchet!.ForeslattTekst);
        Assert.Equal("Venter", patchet.Status); // var Venter, forblir Venter.
    }

    /// <summary>En GODKJENT rad beholder bevisst sin status ved en tekstretting — den har allerede
    /// fått følge-entiteter, og et stille tilbakefall til «Venter» ville skjult at de finnes.</summary>
    [Fact]
    public async Task Patch_paa_godkjent_rad_endrer_tekst_men_beholder_godkjent_status()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Godkjentetaten", status: "Godkjent");

        var patchSvar = await _client.SendAsync(MedBruker(HttpMethod.Patch, $"/api/navnekandidater/{scene.KandidatId}",
            brukerId, new { ForeslattTekst = "Et annet navn" }));
        Assert.Equal(HttpStatusCode.OK, patchSvar.StatusCode);
        var patchet = await patchSvar.Content.ReadFromJsonAsync<NavnekandidatDto>(JsonInnstillinger);
        Assert.Equal("Et annet navn", patchet!.ForeslattTekst);
        Assert.Equal("Godkjent", patchet.Status);
    }

    [Fact]
    public async Task Patch_kan_endre_kategori_og_avviser_ukjent_kategori()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Kategorietaten");

        var okSvar = await _client.SendAsync(MedBruker(HttpMethod.Patch, $"/api/navnekandidater/{scene.KandidatId}",
            brukerId, new { Kategori = "gruppe" }));
        Assert.Equal(HttpStatusCode.OK, okSvar.StatusCode);
        Assert.Equal("gruppe", (await okSvar.Content.ReadFromJsonAsync<NavnekandidatDto>(JsonInnstillinger))!.Kategori);

        var feilSvar = await _client.SendAsync(MedBruker(HttpMethod.Patch, $"/api/navnekandidater/{scene.KandidatId}",
            brukerId, new { Kategori = "administrativ inndeling" })); // bevisst utenfor denne runden.
        Assert.Equal(HttpStatusCode.BadRequest, feilSvar.StatusCode);
    }

    [Fact]
    public async Task Patch_med_tom_tekst_gir_400_og_ukjent_id_gir_404()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Tomtekstetaten");

        var tomSvar = await _client.SendAsync(MedBruker(HttpMethod.Patch, $"/api/navnekandidater/{scene.KandidatId}",
            brukerId, new { ForeslattTekst = "   " }));
        Assert.Equal(HttpStatusCode.BadRequest, tomSvar.StatusCode);

        var ukjentSvar = await _client.SendAsync(MedBruker(HttpMethod.Patch, $"/api/navnekandidater/{Guid.NewGuid()}",
            brukerId, new { ForeslattTekst = "Noe" }));
        Assert.Equal(HttpStatusCode.NotFound, ukjentSvar.StatusCode);
    }

    [Fact]
    public async Task Patch_uten_bruker_id_header_gir_400()
    {
        var scene = await OpprettSceneAsync("Uinnloggetetaten");
        var svar = await _client.PatchAsJsonAsync(
            $"/api/navnekandidater/{scene.KandidatId}", new { ForeslattTekst = "Noe" });
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    /// <summary>
    /// AK1 + AK2 samlet — HELE kjeden for en 'virksomhet'-kandidat, verifisert i DB:
    /// navneform med Navneformgrunn satt, kandidat 'Godkjent', og en TekstTagg med
    /// Kind='virksomhet' og RefId = NAVNEFORMEN (ikke null).
    ///
    /// <para>
    /// [ENDRET, navneform-kjede-runden, 2026-09-08] Asserterte tidligere RefId = VIRKSOMHETEN. Se
    /// <see cref="TekstTaggEntitet.RefId"/>: kjeden går nå tagg → navneform → virksomhet, fordi det er
    /// navneformen som bærer Navneformgrunn.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Kobl_til_virksomhet_lukker_kjeden_med_navneformgrunn_og_koblet_tagg()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Arkivverket");

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{scene.KandidatId}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = scene.MaalVirksomhetId, Navneformgrunn = "utgatt" }));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var resultat = await svar.Content.ReadFromJsonAsync<NavnekandidatKoblingResultatDto>(JsonInnstillinger);

        Assert.Equal("Godkjent", resultat!.Kandidat.Status);
        Assert.Equal("utgatt", resultat.Navneform.Navneformgrunn);
        Assert.Equal("virksomhet", resultat.Navneform.Begrepskategori);
        Assert.Equal(scene.MaalVirksomhetId, resultat.Navneform.VirksomhetReferanseId);
        Assert.NotNull(resultat.TaggId); // departementet er oppløsbart, så taggen SKAL finnes.
        Assert.Equal(scene.NodeEid, resultat.NodeEid);

        await using var db = _fixture.NyDbContext();

        // AK1: navneformen finnes med grunn satt, og peker på virksomheten.
        var navneform = await db.Begreper.SingleAsync(b => b.Id == resultat.Navneform.Id);
        Assert.Equal("utgatt", navneform.Navneformgrunn);
        Assert.Equal(scene.MaalVirksomhetId, navneform.VirksomhetReferanseId);
        Assert.Equal(scene.Navn, navneform.Term);
        Assert.Null(navneform.VirksomhetId); // delt/nasjonal referansedata (docs/20 §2.3).

        // AK2: taggen har Kind='virksomhet' og RefId = NAVNEFORMEN, ikke virksomheten og ikke null.
        var tagg = await db.TekstTagger.SingleAsync(t => t.Id == resultat.TaggId!.Value);
        Assert.Equal("virksomhet", tagg.Kind);
        Assert.Equal(resultat.Navneform.Id, tagg.RefId);
        Assert.NotEqual(scene.MaalVirksomhetId, tagg.RefId); // eksplisitt: IKKE lenger virksomheten.
        Assert.Equal(scene.RettskildeId, tagg.RettskildeId);
        Assert.Equal(scene.NodeEid, tagg.NodeEid);
        // Taggen dekker NØYAKTIG navnet i teksten, som ER navneformens Term.
        Assert.Equal(scene.Navn, tagg.QuoteExact);
        Assert.Equal(navneform.Term, tagg.QuoteExact);

        // Kjeden videre: navneformen peker på virksomheten, altså tagg → navneform → virksomhet.
        var viaNavneformen = await db.Begreper.SingleAsync(b => b.Id == tagg.RefId!.Value);
        Assert.Equal("virksomhet", viaNavneformen.Begrepskategori);
        Assert.Equal(scene.MaalVirksomhetId, viaNavneformen.VirksomhetReferanseId);

        var lagretKandidat = await db.Navnekandidater.SingleAsync(k => k.Id == scene.KandidatId);
        Assert.Equal("Godkjent", lagretKandidat.Status);
        Assert.NotNull(lagretKandidat.BehandletAv);
    }

    /// <summary>
    /// AK2, gjenbruksgrenen: en rad som ALT har en UBUNDET tagg (Kind='begrep', RefId=null) fra den
    /// gamle godkjenn-veien. Wizarden skal KOBLE den, ikke lage en ny ved siden av — og nettopp slike
    /// fastlåste rader skal kunne redde selv om status alt er «Godkjent».
    /// </summary>
    [Fact]
    public async Task Kobl_til_virksomhet_gjenbruker_ubundet_tagg_fra_en_tidligere_godkjenning()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Blindveietaten", status: "Godkjent");

        // Gjenskap blindveien slik den ser ut i dag: en tagg på forekomsten med RefId = null,
        // eid av en virksomhet, Kind='begrep' (det GodkjennAsync etterlater for 'virksomhet').
        Guid ubundetTaggId;
        await using (var db = _fixture.NyDbContext())
        {
            var kandidat = await db.Navnekandidater.SingleAsync(k => k.Id == scene.KandidatId);
            var node = await db.RettskildeNoder.SingleAsync(
                n => n.RettskildeId == scene.RettskildeId && n.Eid == scene.NodeEid);
            var eier = await db.Virksomheter.FirstAsync();
            ubundetTaggId = Guid.NewGuid();
            db.TekstTagger.Add(new TekstTaggEntitet
            {
                Id = ubundetTaggId, VirksomhetId = eier.Id, RettskildeId = scene.RettskildeId,
                NodeEid = scene.NodeEid, StartOffset = kandidat.StartOffset, EndOffset = kandidat.EndOffset,
                QuotePrefix = node.Tekst![..kandidat.StartOffset],
                QuoteExact = node.Tekst[kandidat.StartOffset..kandidat.EndOffset],
                QuoteSuffix = node.Tekst[kandidat.EndOffset..],
                NodeTekstHash = "test-hash", Kind = "begrep", RefId = null,
                OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{scene.KandidatId}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = scene.MaalVirksomhetId, Navneformgrunn = "gjeldende" }));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var resultat = await svar.Content.ReadFromJsonAsync<NavnekandidatKoblingResultatDto>(JsonInnstillinger);

        // SAMME tagg-id — den ble gjenbrukt, ikke duplisert.
        Assert.Equal(ubundetTaggId, resultat!.TaggId);

        await using (var db = _fixture.NyDbContext())
        {
            var tagger = await db.TekstTagger
                .Where(t => t.RettskildeId == scene.RettskildeId && t.Entitetsstatus == "gjeldende")
                .ToListAsync();
            var tagg = Assert.Single(tagger); // ingen ny, overlappende tagg ved siden av den gamle.
            Assert.Equal(ubundetTaggId, tagg.Id);
            Assert.Equal("virksomhet", tagg.Kind); // flippet fra 'begrep'.
            Assert.Equal(resultat.Navneform.Id, tagg.RefId); // navneformen, ikke virksomheten.
        }
    }

    /// <summary>Gjentatt kall skal være idempotent, ikke bryte den unike tagg-indeksen
    /// (tekst_tagger_unik_tagg) med en ufanget DbUpdateException.</summary>
    [Fact]
    public async Task Kobl_til_virksomhet_er_idempotent_ved_gjentatt_kall()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Idempotentetaten");

        var forste = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{scene.KandidatId}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = scene.MaalVirksomhetId, Navneformgrunn = "kortform" }));
        Assert.Equal(HttpStatusCode.OK, forste.StatusCode);
        var forsteResultat = await forste.Content.ReadFromJsonAsync<NavnekandidatKoblingResultatDto>(JsonInnstillinger);

        var andre = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{scene.KandidatId}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = scene.MaalVirksomhetId, Navneformgrunn = "kortform" }));
        Assert.Equal(HttpStatusCode.OK, andre.StatusCode);
        var andreResultat = await andre.Content.ReadFromJsonAsync<NavnekandidatKoblingResultatDto>(JsonInnstillinger);

        Assert.Equal(forsteResultat!.TaggId, andreResultat!.TaggId);
        Assert.Equal(forsteResultat.Navneform.Id, andreResultat.Navneform.Id);

        await using var db = _fixture.NyDbContext();
        Assert.Equal(1, await db.TekstTagger.CountAsync(
            t => t.RettskildeId == scene.RettskildeId && t.Entitetsstatus == "gjeldende"));
        Assert.Equal(1, await db.Begreper.CountAsync(
            b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == scene.MaalVirksomhetId));
    }

    [Fact]
    public async Task Kobl_til_virksomhet_med_ugyldig_navneformgrunn_gir_400()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Ugyldiggrunnetaten");

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{scene.KandidatId}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = scene.MaalVirksomhetId, Navneformgrunn = "utdatert" })); // nær 'utgatt', men ugyldig.
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);

        // Ingenting skal være opprettet av det mislykkede kallet — verken navneform eller statusendring.
        await using var db = _fixture.NyDbContext();
        Assert.Equal(0, await db.Begreper.CountAsync(b => b.VirksomhetReferanseId == scene.MaalVirksomhetId));
        Assert.Equal("Venter", (await db.Navnekandidater.SingleAsync(k => k.Id == scene.KandidatId)).Status);
        Assert.Equal(0, await db.TekstTagger.CountAsync(t => t.RettskildeId == scene.RettskildeId));
    }

    /// <summary>Navneformgrunn er VALGFRI — NULL (uspesifisert) skal gå gjennom.</summary>
    [Fact]
    public async Task Kobl_til_virksomhet_uten_navneformgrunn_gir_navneform_med_null_grunn()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Utenlgrunnetaten");

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{scene.KandidatId}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = scene.MaalVirksomhetId }));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var resultat = await svar.Content.ReadFromJsonAsync<NavnekandidatKoblingResultatDto>(JsonInnstillinger);
        Assert.Null(resultat!.Navneform.Navneformgrunn);
    }

    /// <summary>En 'gruppe'-kandidat hører ikke til i denne veien — den har sin egen, fungerende
    /// godkjenn-vei (AK5: ingen regresjon der).</summary>
    [Fact]
    public async Task Kobl_til_virksomhet_avviser_gruppe_kategori()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("gruppeetaten", kategori: "gruppe");

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{scene.KandidatId}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = scene.MaalVirksomhetId, Navneformgrunn = "gjeldende" }));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Kobl_til_virksomhet_med_ukjent_virksomhet_gir_400_og_ukjent_kandidat_gir_404()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Ukjentmaaletaten");

        var ukjentVirksomhet = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{scene.KandidatId}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = Guid.NewGuid(), Navneformgrunn = "gjeldende" }));
        Assert.Equal(HttpStatusCode.BadRequest, ukjentVirksomhet.StatusCode);

        var ukjentKandidat = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{Guid.NewGuid()}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = scene.MaalVirksomhetId, Navneformgrunn = "gjeldende" }));
        Assert.Equal(HttpStatusCode.NotFound, ukjentKandidat.StatusCode);
    }

    /// <summary>Uten et oppløsbart ansvarlig departement kan ingen tagg opprettes (taggens
    /// VirksomhetId er ikke-nullbar) — men navneformkoblingen skal LYKKES uansett, og TaggId være
    /// null slik at klienten kan vise begrensningen i stedet for å skjule den.</summary>
    [Fact]
    public async Task Kobl_til_virksomhet_uten_oppslabart_departement_kobler_navneform_men_gir_ingen_tagg()
    {
        var brukerId = await HentJuristIdAsync();
        var scene = await OpprettSceneAsync("Udepartementetaten", medDepartement: false);

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post,
            $"/api/navnekandidater/{scene.KandidatId}/kobl-til-virksomhet", brukerId,
            new { VirksomhetId = scene.MaalVirksomhetId, Navneformgrunn = "feilskriving" }));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var resultat = await svar.Content.ReadFromJsonAsync<NavnekandidatKoblingResultatDto>(JsonInnstillinger);

        Assert.Null(resultat!.TaggId); // den dokumenterte degraderingen.
        Assert.Equal("feilskriving", resultat.Navneform.Navneformgrunn); // men navneformen er der.
        Assert.Equal("Godkjent", resultat.Kandidat.Status);
    }

    /// <summary>POST /api/virksomhetsbegrep skal håndheve samme vokabular som wizarden — samme
    /// tjenestelagsvalidering, ikke bare CHECK-constrainten.</summary>
    [Fact]
    public async Task Opprett_virksomhetsbegrep_avviser_ugyldig_navneformgrunn_og_lagrer_gyldig()
    {
        var brukerId = await HentJuristIdAsync();
        // Unikt navn, av samme «ikke forurens sveip-testene»-grunn som OpprettSceneAsync forklarer.
        var virksomhetId = Guid.NewGuid();
        var term = $"Matilsynet{Guid.NewGuid():N}";
        await using (var db = _fixture.NyDbContext())
        {
            db.Virksomheter.Add(new Virksomhet
            {
                Id = virksomhetId, Navn = $"Mattilsynet {virksomhetId:N}", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var feilSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/virksomhetsbegrep", brukerId,
            new { VirksomhetId = virksomhetId, Term = term, SkosUrl = (string?)null, Navneformgrunn = "tulleverdi" }));
        Assert.Equal(HttpStatusCode.BadRequest, feilSvar.StatusCode);

        var okSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/virksomhetsbegrep", brukerId,
            new { VirksomhetId = virksomhetId, Term = term, SkosUrl = (string?)null, Navneformgrunn = "feilskriving" }));
        Assert.Equal(HttpStatusCode.Created, okSvar.StatusCode);
        var opprettet = await okSvar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger);
        Assert.Equal("feilskriving", opprettet!.Navneformgrunn);

        // Og feltet kommer tilbake via virksomhetens navneform-liste (det UI-et faktisk leser).
        var liste = await _client.GetFromJsonAsync<List<BegrepDto>>(
            $"/api/virksomheter/{virksomhetId}/begrep", JsonInnstillinger);
        Assert.Equal("feilskriving", Assert.Single(liste!).Navneformgrunn);
    }

}
