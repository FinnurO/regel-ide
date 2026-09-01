using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester: kjører hele API-et (inkl. migrasjon + førstegangs-seeding i Program.cs) mot
/// en ekte, embedded Postgres-instans og de ekte rettskilde-fixturene i data/kilder/raw-lovdata/.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class RettskilderEndepunktTests
{
    private readonly HttpClient _client;
    private const string AlkohollovenEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly EmbeddedPostgresApiFixture _fixture;

    public RettskilderEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> HentAlkohollovenIdAsync()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        return sammendrag!.Single(r => r.Eli == AlkohollovenEli).Id;
    }

    [Fact]
    public async Task Liste_inneholder_alle_tre_kildedokumenter()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);

        Assert.NotNull(sammendrag);
        // >= 3, ikke akkurat 3: denne testklassen deler databasen med ImportEndepunktTests
        // (samme Postgres-instans for hele assemblyen, se ApiTestCollection), som kan legge til
        // flere rettskilder (bl.a. en virksomhetseid kopi av forvaltningsloven).
        Assert.True(sammendrag!.Count >= 3);
        Assert.Contains(sammendrag, r => r.Eli == AlkohollovenEli);
        Assert.Contains(sammendrag, r => r.Eli == "https://lovdata.no/eli/forskrift/2005/06/08/538/nor");
        Assert.Contains(sammendrag, r => r.Eli == "https://lovdata.no/eli/lov/1967/02/10/nor");
    }

    [Fact]
    public async Task Henter_full_rettskilde_med_metadata_og_akn_xml()
    {
        var id = await HentAlkohollovenIdAsync();
        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);

        Assert.NotNull(detalj);
        Assert.Equal(AlkohollovenEli, detalj!.Eli);
        Assert.StartsWith("<akomaNtoso", detalj.AknXml);
    }

    [Fact]
    public async Task Ukjent_id_gir_404()
    {
        var svar = await _client.GetAsync($"/api/rettskilder/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Henter_nodetre_og_finner_paragraf_1_1()
    {
        var id = await HentAlkohollovenIdAsync();
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{id}/noder", JsonInnstillinger);

        Assert.NotNull(noder);
        Assert.Contains(noder!, n => n.Eid == $"{AlkohollovenEli}/§1-1");
    }

    [Fact]
    public async Task Nodetre_eksponerer_opphevet_flagg_og_dato_for_1_12()
    {
        var id = await HentAlkohollovenIdAsync();
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{id}/noder", JsonInnstillinger);

        var opphevetParagraf = noder!.Single(n => n.Eid == $"{AlkohollovenEli}/§1-12");
        Assert.True(opphevetParagraf.Opphevet);
        Assert.Equal(new DateOnly(2005, 7, 1), opphevetParagraf.OpphevetDato);

        var vanligParagraf = noder!.Single(n => n.Eid == $"{AlkohollovenEli}/§1-1");
        Assert.False(vanligParagraf.Opphevet);
    }

    [Fact]
    public async Task Henter_enkeltnode_ved_eId_med_skraastreker_og_skjema()
    {
        var id = await HentAlkohollovenIdAsync();
        var eid = $"{AlkohollovenEli}/§1-1/ledd-1";
        var node = await _client.GetFromJsonAsync<RettskildeNodeDto>(
            $"/api/rettskilder/{id}/noder/oppslag?eid={Uri.EscapeDataString(eid)}", JsonInnstillinger);

        Assert.NotNull(node);
        Assert.Equal(eid, node!.Eid);
        Assert.StartsWith("Reguleringen av innførsel", node.Tekst);
    }

    [Fact]
    public async Task Ukjent_eId_gir_404_selv_om_rettskilden_finnes()
    {
        var id = await HentAlkohollovenIdAsync();
        var svar = await _client.GetAsync($"/api/rettskilder/{id}/noder/oppslag?eid=finnes-ikke");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Henter_kryssreferanser_inkludert_intern_referanse_1_3_til_1_5()
    {
        var id = await HentAlkohollovenIdAsync();
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{id}/noder", JsonInnstillinger);
        var fraNodeId = noder!.Single(n => n.Eid == $"{AlkohollovenEli}/§1-3/ledd-1").Id;

        var referanser = await _client.GetFromJsonAsync<List<RettskildeReferanseDto>>($"/api/rettskilder/{id}/referanser", JsonInnstillinger);

        Assert.NotNull(referanser);
        Assert.Contains(referanser!, r => r.FraNodeId == fraNodeId && r.TilEid == $"{AlkohollovenEli}/§1-5");
    }

    // ---------- Hjemmel (2026-08-30) — header-metadatafeltet <dt class="basedOn">, se
    // RettskildeHjemmelEntitet-kommentaren. Startup-seedingen importerer HELE data/kilder/raw-lovdata/
    // i filnavnrekkefølge — "alkoholforskriften-..." kommer FØR "alkoholloven-..." alfabetisk, så
    // dette dekker ende-til-ende at en referanse-stub opprettet under forskrift-importen faktisk
    // forfremmes korrekt når alkoholloven importeres like etter, i samme seeding-kjøring. ----------

    [Fact]
    public async Task Hjemmel_for_alkoholforskriften_har_tjueen_referanser_til_den_virkelige_alkoholloven()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        var forskriftId = sammendrag!.Single(r => r.Eli == "https://lovdata.no/eli/forskrift/2005/06/08/538/nor").Id;
        var lovenId = await HentAlkohollovenIdAsync();

        var hjemler = await _client.GetFromJsonAsync<List<RettskildeHjemmelDto>>(
            $"/api/rettskilder/{forskriftId}/hjemmel", JsonInnstillinger);

        Assert.NotNull(hjemler);
        Assert.Equal(21, hjemler!.Count);
        Assert.All(hjemler, h => Assert.Equal(lovenId, h.HjemmelRettskildeId));
        Assert.Contains(hjemler, h => h.HjemmelEid == $"{AlkohollovenEli}/§1-2");
        // Rekkefølgen fra kilde-HTML-en er bevart (0-indeksert Sorteringsrekkefolge).
        Assert.Equal(0, hjemler.Single(h => h.HjemmelEid == $"{AlkohollovenEli}/§1-2").Sorteringsrekkefolge);
    }

    [Fact]
    public async Task Hjemmel_for_alkoholloven_selv_er_tom_liste()
    {
        var id = await HentAlkohollovenIdAsync();
        var hjemler = await _client.GetFromJsonAsync<List<RettskildeHjemmelDto>>($"/api/rettskilder/{id}/hjemmel", JsonInnstillinger);

        Assert.NotNull(hjemler);
        Assert.Empty(hjemler!);
    }

    [Fact]
    public async Task Hjemmel_for_ukjent_rettskilde_gir_404()
    {
        var svar = await _client.GetAsync($"/api/rettskilder/{Guid.NewGuid()}/hjemmel");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task HjemmelFor_pa_alkoholloven_viser_alkoholforskriften_som_hjemlet_forskrift()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        var forskriftId = sammendrag!.Single(r => r.Eli == "https://lovdata.no/eli/forskrift/2005/06/08/538/nor").Id;
        var lovenId = await HentAlkohollovenIdAsync();

        var hjemletFor = await _client.GetFromJsonAsync<List<RettskildeHjemletForDto>>(
            $"/api/rettskilder/{lovenId}/hjemmel-for", JsonInnstillinger);

        Assert.NotNull(hjemletFor);
        Assert.Equal(21, hjemletFor!.Count);
        Assert.All(hjemletFor, r => Assert.Equal(forskriftId, r.ForskriftId));
        Assert.Contains(hjemletFor, r => r.HjemmelEid == $"{AlkohollovenEli}/§1-2");
    }

    [Fact]
    public async Task HjemmelFor_pa_alkoholforskriften_selv_er_tom_liste()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        var forskriftId = sammendrag!.Single(r => r.Eli == "https://lovdata.no/eli/forskrift/2005/06/08/538/nor").Id;

        var hjemletFor = await _client.GetFromJsonAsync<List<RettskildeHjemletForDto>>(
            $"/api/rettskilder/{forskriftId}/hjemmel-for", JsonInnstillinger);

        Assert.NotNull(hjemletFor);
        Assert.Empty(hjemletFor!);
    }

    [Fact]
    public async Task HjemmelFor_for_ukjent_rettskilde_gir_404()
    {
        var svar = await _client.GetAsync($"/api/rettskilder/{Guid.NewGuid()}/hjemmel-for");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    // ---------- Åpne data: statusfilter + valgfri virksomhet-parameter (2026-07-24) ----------

    [Fact]
    public async Task Utkast_rettskilde_er_skjult_fra_listen_og_gir_404_ved_direkte_oppslag()
    {
        Guid utkastId;
        await using (var db = _fixture.NyDbContext())
        {
            utkastId = Guid.NewGuid();
            db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = utkastId,
                Doctype = "internal",
                Kildetype = "Virksomhetsdokument",
                Tittel = "Ikke ferdig verifisert kilde",
                Status = "Utkast",
                AknXml = "<akomaNtoso/>",
                OpprettetAv = "test",
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        Assert.DoesNotContain(sammendrag!, r => r.Id == utkastId);

        var svar = await _client.GetAsync($"/api/rettskilder/{utkastId}");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task VirksomhetId_parameter_snevrer_inn_til_kun_den_virksomhetens_egne_kilder()
    {
        Guid virksomhetId, egenRettskildeId;
        await using (var db = _fixture.NyDbContext())
        {
            virksomhetId = Guid.NewGuid();
            db.Virksomheter.Add(new Virksomhet { Id = virksomhetId, Navn = "Vennesla kommune" });
            egenRettskildeId = Guid.NewGuid();
            db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = egenRettskildeId,
                VirksomhetId = virksomhetId,
                Doctype = "act",
                Kildetype = "Forskrift",
                Tittel = "Lokal forskrift om skjenketider, Vennesla kommune",
                Status = "Gjeldende",
                AknXml = "<akomaNtoso/>",
                OpprettetAv = "test",
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Uten parameter: ser alt (delte kilder + virksomhetens egen) -- åpne data, ikke en tilgangssperre.
        var alt = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        Assert.Contains(alt!, r => r.Id == egenRettskildeId);
        Assert.Contains(alt!, r => r.Eli == AlkohollovenEli);

        // Med ?virksomhetId=...: kun DENNE virksomhetens egne kilder, ikke de delte/nasjonale.
        var kunEgne = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>(
            $"/api/rettskilder?virksomhetId={virksomhetId}", JsonInnstillinger);
        Assert.Single(kunEgne!);
        Assert.Equal(egenRettskildeId, kunEgne!.Single().Id);
    }

    // ---------- Metadata-oppdatering (2026-07-29, AK-3.3.6 importbekreftelse) ----------

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    [Fact]
    public async Task Oppdater_metadata_uten_bruker_id_header_gir_400()
    {
        var id = await HentAlkohollovenIdAsync();
        var svar = await _client.PatchAsJsonAsync(
            $"/api/rettskilder/{id}/metadata",
            new OppdaterRettskildeMetadataRequest("Nytt", "Ny utgiver", null, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Oppdater_metadata_lagrer_kortnavn_og_utgiver()
    {
        var id = await HentAlkohollovenIdAsync();
        var bruker = await HentTestbrukerAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/rettskilder/{id}/metadata")
        {
            Content = JsonContent.Create(new OppdaterRettskildeMetadataRequest(
                "Alkoholloven (kortnavn testet)", "Lovdata (redigert)", null, null, null, null, null, null)),
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString() } },
        };
        var svar = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var oppdatert = await svar.Content.ReadFromJsonAsync<RettskildeDetalj>(JsonInnstillinger);
        Assert.Equal("Alkoholloven (kortnavn testet)", oppdatert!.Kortnavn);
        Assert.Equal("Lovdata (redigert)", oppdatert.Utgiver);

        var hentetPaNytt = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);
        Assert.Equal("Alkoholloven (kortnavn testet)", hentetPaNytt!.Kortnavn);
    }

    /// <summary>
    /// Punkt 4 (avklaringsrunde 2026-08-13) — de seks feltene som allerede fantes på entiteten
    /// (håndbok-metadata) men manglet en skrivevei før nå. Eli forblir UTENFOR requesten (permanent
    /// skrivebeskyttet) — verifisert her ved at den er uendret etter kallet.
    /// </summary>
    [Fact]
    public async Task Oppdater_metadata_lagrer_de_nye_handbok_feltene_og_lar_eli_sta_urort()
    {
        var id = await HentAlkohollovenIdAsync();
        var bruker = await HentTestbrukerAsync();
        var forOppdatering = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/rettskilder/{id}/metadata")
        {
            Content = JsonContent.Create(new OppdaterRettskildeMetadataRequest(
                forOppdatering!.Kortnavn, forOppdatering.Utgiver,
                "SD-24-113", "01", "Bystyret", new DateOnly(2024, 6, 19), new DateOnly(2028, 7, 1), new DateOnly(2026, 1, 1))),
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString() } },
        };
        var svar = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var oppdatert = await svar.Content.ReadFromJsonAsync<RettskildeDetalj>(JsonInnstillinger);
        Assert.Equal("SD-24-113", oppdatert!.InterntDokNr);
        Assert.Equal("01", oppdatert.Revisjonsnr);
        Assert.Equal("Bystyret", oppdatert.VedtattAv);
        Assert.Equal(new DateOnly(2024, 6, 19), oppdatert.Vedtaksdato);
        Assert.Equal(new DateOnly(2028, 7, 1), oppdatert.GyldigTil);
        Assert.Equal(new DateOnly(2026, 1, 1), oppdatert.KonsolidertDato);
        Assert.Equal(forOppdatering.Eli, oppdatert.Eli); // ALDRI skrivbar via denne requesten.
    }

    [Fact]
    public async Task Oppdater_metadata_pa_ukjent_rettskilde_gir_404()
    {
        var bruker = await HentTestbrukerAsync();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/rettskilder/{Guid.NewGuid()}/metadata")
        {
            Content = JsonContent.Create(new OppdaterRettskildeMetadataRequest("X", "Y", null, null, null, null, null, null)),
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString() } },
        };
        var svar = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    // ---------- Irrelevant-markering (2026-08-30, header-nivå «irrelevant for regel-ide») ----------

    /// <summary>
    /// Egen fixture-rad per test her (ikke alkoholloven — den brukes/telles av mange andre tester i
    /// denne og andre klasser i samme <see cref="ApiTestCollection"/>, en irrelevant-markering på DEN
    /// ville vært et snikende sidesteg som ville forstyrret uavhengige tester som antar den er synlig).
    /// </summary>
    private async Task<Guid> OpprettEgenRettskildeAsync(bool erIrrelevant = false, string? irrelevantKommentar = null)
    {
        await using var db = _fixture.NyDbContext();
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id,
            Doctype = "act",
            Kildetype = "Forskrift",
            Tittel = $"Delegering av myndighet — irrelevant-test {id}",
            Status = "Gjeldende",
            AknXml = "<akomaNtoso/>",
            OpprettetAv = "test",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
            ErIrrelevant = erIrrelevant,
            IrrelevantKommentar = irrelevantKommentar,
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Oppdater_irrelevant_uten_bruker_id_header_gir_400()
    {
        var id = await OpprettEgenRettskildeAsync();
        var svar = await _client.PatchAsJsonAsync(
            $"/api/rettskilder/{id}/irrelevant",
            new OppdaterRettskildeIrrelevantRequest(true, "Ren delegeringsbeslutning, ingen realitet."));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Oppdater_irrelevant_pa_ukjent_rettskilde_gir_404()
    {
        var bruker = await HentTestbrukerAsync();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/rettskilder/{Guid.NewGuid()}/irrelevant")
        {
            Content = JsonContent.Create(new OppdaterRettskildeIrrelevantRequest(true, "X")),
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString() } },
        };
        var svar = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Setter_irrelevant_markering_med_kommentar_og_kan_hente_den_igjen()
    {
        var id = await OpprettEgenRettskildeAsync();
        var bruker = await HentTestbrukerAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/rettskilder/{id}/irrelevant")
        {
            Content = JsonContent.Create(new OppdaterRettskildeIrrelevantRequest(
                true, "Rent prosedyremessig ikrafttredelsesvedtak, ingen egen rettighet.")),
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString() } },
        };
        var svar = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var oppdatert = await svar.Content.ReadFromJsonAsync<RettskildeDetalj>(JsonInnstillinger);
        Assert.True(oppdatert!.ErIrrelevant);
        Assert.Equal("Rent prosedyremessig ikrafttredelsesvedtak, ingen egen rettighet.", oppdatert.IrrelevantKommentar);

        var hentetPaNytt = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);
        Assert.True(hentetPaNytt!.ErIrrelevant);
        Assert.Equal("Rent prosedyremessig ikrafttredelsesvedtak, ingen egen rettighet.", hentetPaNytt.IrrelevantKommentar);
    }

    /// <summary>
    /// Fjernes markeringen igjen (satt tilbake til <c>false</c>), skal kommentaren IKKE slettes
    /// automatisk noe sted i denne flyten — se <see cref="RettskildeEntitet.IrrelevantKommentar"/>s
    /// klassekommentar. Testet ved at kommentaren fortsatt kommer tilbake uendret når klienten selv
    /// sender den med (samme oppførsel som skjemaet i RettskildeDetalj.tsx: teksten forblir i boksen).
    /// </summary>
    [Fact]
    public async Task Fjerner_irrelevant_markering_lar_kommentaren_bli_staende_urort()
    {
        var id = await OpprettEgenRettskildeAsync(erIrrelevant: true, irrelevantKommentar: "Opprinnelig begrunnelse.");
        var bruker = await HentTestbrukerAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/rettskilder/{id}/irrelevant")
        {
            Content = JsonContent.Create(new OppdaterRettskildeIrrelevantRequest(false, "Opprinnelig begrunnelse.")),
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString() } },
        };
        var svar = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var oppdatert = await svar.Content.ReadFromJsonAsync<RettskildeDetalj>(JsonInnstillinger);
        Assert.False(oppdatert!.ErIrrelevant);
        Assert.Equal("Opprinnelig begrunnelse.", oppdatert.IrrelevantKommentar);
    }

    [Fact]
    public async Task Liste_ekskluderer_irrelevant_markerte_som_standard_men_viser_dem_med_eksplisitt_flagg()
    {
        var id = await OpprettEgenRettskildeAsync(erIrrelevant: true, irrelevantKommentar: "Kun administrativ, ingen realitet.");

        var standard = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        Assert.DoesNotContain(standard!, r => r.Id == id);

        var medIrrelevante = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>(
            "/api/rettskilder?inkluderIrrelevante=true", JsonInnstillinger);
        var rad = Assert.Single(medIrrelevante!, r => r.Id == id);
        Assert.True(rad.ErIrrelevant);
    }

    [Fact]
    public async Task Referert_av_tjenester_viser_alminnelig_skjenkebevilling()
    {
        // Byggesteg 4 (2026-07-30) — motsatt retning av tjenestens regelverksreferanser. Byggesteg 2-
        // seedingen kobler "Alminnelig skjenkebevilling" til alkoholloven kap. 4 (§§ 4-1 til 4-7).
        var id = await HentAlkohollovenIdAsync();

        var referanser = await _client.GetFromJsonAsync<List<TjenesteReferanseDto>>(
            $"/api/rettskilder/{id}/referert-av-tjenester", JsonInnstillinger);

        Assert.NotNull(referanser);
        Assert.Contains(referanser!, r => r.TjenesteTittel == "Alminnelig skjenkebevilling");
        Assert.True(referanser!.Count(r => r.TjenesteTittel == "Alminnelig skjenkebevilling") >= 7);
    }

    [Fact]
    public async Task Referert_av_tjenester_for_ukjent_rettskilde_gir_tom_liste()
    {
        var referanser = await _client.GetFromJsonAsync<List<TjenesteReferanseDto>>(
            $"/api/rettskilder/{Guid.NewGuid()}/referert-av-tjenester", JsonInnstillinger);

        Assert.NotNull(referanser);
        Assert.Empty(referanser!);
    }

    [Fact]
    public async Task Referert_av_dokumenter_utelater_rettskildens_egne_interne_referanser()
    {
        // Bugfiks (avklaringsrunde 2026-08-13, funn 2): §1-3 → §1-5 er alkohollovens EGEN interne
        // kryssreferanse, fanget opp med Opprinnelse="import" under selve importen (se
        // Import_referanse_har_opprinnelse_import_og_kan_ikke_fjernes under). Før fiksen ble denne
        // (og enhver annen import-referanse) talt som om et ANNET dokument refererte alkoholloven —
        // den skal IKKE dukke opp her: alkoholloven er ikke "et annet dokument" enn seg selv, og en
        // rettskildes egen interne struktur (import) er ikke det samme som en håndbok/rundskriv som
        // faktisk kobler seg til den (manuell).
        var alkoholovenId = await HentAlkohollovenIdAsync();

        var referertAv = await _client.GetFromJsonAsync<List<DokumentReferanseDto>>(
            $"/api/rettskilder/{alkoholovenId}/referert-av-dokumenter", JsonInnstillinger);

        Assert.NotNull(referertAv);
        Assert.DoesNotContain(referertAv!, r => r.DokumentId == alkoholovenId);
    }

    [Fact]
    public async Task Import_referanse_har_opprinnelse_import_og_kan_ikke_fjernes()
    {
        // 2026-07-30: kryssreferansen §1-3 → §1-5 fanges opp automatisk under import (§1-3 har en reell
        // inline Lovdata-lenke) — bekrefter at den er skrivebeskyttet i UI-flyten.
        var alkoholovenId = await HentAlkohollovenIdAsync();
        var referanser = await _client.GetFromJsonAsync<List<RettskildeReferanseDto>>(
            $"/api/rettskilder/{alkoholovenId}/referanser", JsonInnstillinger);
        var importReferanse = referanser!.First(r => r.Opprinnelse == "import");

        var fjernSvar = await _client.DeleteAsync($"/api/rettskilder/{alkoholovenId}/referanser/{importReferanse.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, fjernSvar.StatusCode);
    }

    [Fact]
    public async Task Manuell_referanse_kan_opprettes_pa_en_node_og_fjernes_igjen()
    {
        var alkoholovenId = await HentAlkohollovenIdAsync();
        var paragrafer = (await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{alkoholovenId}/noder", JsonInnstillinger))!
            .Where(n => n.NodeType == "paragraf").ToList();
        var fra = paragrafer[0];
        var til = paragrafer[1];

        var svar = await _client.PostAsJsonAsync(
            $"/api/rettskilder/{alkoholovenId}/noder/{fra.Id}/referanser",
            new KobleLovreferanseRequest(alkoholovenId, til.Eid), JsonInnstillinger);
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var referanse = await svar.Content.ReadFromJsonAsync<RettskildeReferanseDto>(JsonInnstillinger);
        Assert.Equal("manuell", referanse!.Opprinnelse);

        var fjernSvar = await _client.DeleteAsync($"/api/rettskilder/{alkoholovenId}/referanser/{referanse.Id}");
        Assert.Equal(HttpStatusCode.NoContent, fjernSvar.StatusCode);
    }
}
