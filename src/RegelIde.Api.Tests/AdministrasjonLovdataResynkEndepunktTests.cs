using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Administrasjon-Lovdata-resynk (GitHub-issue #104) — manuell trigger, database-lagret
/// frekvensinnstilling, og kjøre-historikk. Samme <c>WithWebHostBuilder</c>-overstyring av
/// <see cref="LovdataBulkHenter"/>s <see cref="HttpClient"/> som <see cref="BrregEndepunktTests"/>
/// bruker for <see cref="BrregKlient"/> — INGEN ekte nettverkskall mot api.lovdata.no i denne
/// test-suiten. Stubben feiler bevisst (med en liten, kontrollert forsinkelse) i stedet for å levere
/// et ekte bulk-arkiv: det er nok til å bevise BEGGE tingene testene bryr seg om — at requesten
/// returnerer FØR arbeidet er ferdig, og at et helt mislykket forsøk faktisk havner i historikken som
/// Feilet (samme <see cref="LovdataResynkKjoringTjeneste.FullforKjoringAsync"/>-sti som en ekte feil).
/// <para>
/// <c>lovdata_resynk_kjoringer</c>/<c>lovdata_resynk_innstilling</c> er, med vilje, GLOBALE tabeller
/// (se LovdataResynkKjoringTjenesteTests/LovdataResynkInnstillingTjenesteTests i RegelIde.Data.Tests
/// for samme resonnement) — denne fixturen er delt for HELE RegelIde.Api.Tests-assemblyen, så hver
/// test her nullstiller dem selv FØR den setter opp sitt eget scenario, se <see cref="RyddAsync"/>.
/// </para>
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class AdministrasjonLovdataResynkEndepunktTests
{
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public AdministrasjonLovdataResynkEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task RyddAsync(RegelIdeDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM lovdata_resynk_kjoringer;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM lovdata_resynk_innstilling;");
    }

    /// <summary>Svarer 503 for BEGGE bulk-arkiv-URL-ene, etter en liten forsinkelse — nok til å bevise
    /// at POST-endepunktet returnerer LENGE FØR selve (mislykkede) kjøringen er ferdig.</summary>
    private sealed class TregFeilendeHandler(TimeSpan forsinkelse) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(forsinkelse, ct);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }
    }

    private HttpClient LagKlientMedTregFeilendeLovdata(TimeSpan forsinkelse)
    {
        var factoryMedStub = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient<LovdataBulkHenter>().ConfigurePrimaryHttpMessageHandler(() => new TregFeilendeHandler(forsinkelse))));
        return factoryMedStub.CreateClient();
    }

    private async Task<Guid> HentEnBrukerIdAsync(HttpClient klient)
    {
        var brukere = await klient.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.First().Id;
    }

    [Fact]
    public async Task Trigger_uten_bruker_header_gir_400()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);

        var svar = await _fixture.Factory.CreateClient().PostAsync("/api/administrasjon/lovdata-resynk", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Manuell_trigger_returnerer_umiddelbart_og_kjoringen_havner_etter_hvert_i_historikken_som_feilet()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);

        using var klient = LagKlientMedTregFeilendeLovdata(TimeSpan.FromSeconds(3));
        var brukerId = await HentEnBrukerIdAsync(klient);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/administrasjon/lovdata-resynk");
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString());

        var stoppeklokke = Stopwatch.StartNew();
        var svar = await klient.SendAsync(request);
        stoppeklokke.Stop();

        // Selve poenget (issue #104: "IKKE la selve HTTP-requesten henge og vente på hele resultatet")
        // -- den stubbede Lovdata-handleren bruker 3s, men responsen skal komme LENGE før det.
        Assert.True(stoppeklokke.Elapsed < TimeSpan.FromSeconds(1),
            $"POST skulle returnert umiddelbart, brukte {stoppeklokke.Elapsed}.");
        Assert.Equal(HttpStatusCode.Accepted, svar.StatusCode);

        var startet = await svar.Content.ReadFromJsonAsync<LovdataResynkKjoringDto>(JsonInnstillinger);
        Assert.Equal(LovdataResynkStatus.Pagar, startet!.Status);
        Assert.Equal(LovdataResynkUtlost.Manuell, startet.Utlost);

        // Kjøringen fullfører seg selv (som Feilet, siden Lovdata-stubben svarer 503) i BAKGRUNNEN --
        // poll historikken til den er ferdig i stedet for en fast Task.Delay.
        LovdataResynkKjoringDto? ferdig = null;
        for (var forsok = 0; forsok < 50 && ferdig is null; forsok++)
        {
            await Task.Delay(200);
            var historikk = await klient.GetFromJsonAsync<List<LovdataResynkKjoringDto>>(
                "/api/administrasjon/lovdata-resynk", JsonInnstillinger);
            ferdig = historikk!.SingleOrDefault(k => k.Id == startet.Id && k.Status != LovdataResynkStatus.Pagar);
        }

        Assert.NotNull(ferdig);
        Assert.Equal(LovdataResynkStatus.Feilet, ferdig!.Status);
        Assert.NotNull(ferdig.Feilmelding);
        Assert.NotNull(ferdig.FullfortTidspunkt);
    }

    [Fact]
    public async Task Trigger_mens_en_kjoring_allerede_pagar_gir_409()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);

        using var klient = LagKlientMedTregFeilendeLovdata(TimeSpan.FromSeconds(5));
        var brukerId = await HentEnBrukerIdAsync(klient);

        using var forsteRequest = new HttpRequestMessage(HttpMethod.Post, "/api/administrasjon/lovdata-resynk");
        forsteRequest.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString());
        var forsteSvar = await klient.SendAsync(forsteRequest);
        Assert.Equal(HttpStatusCode.Accepted, forsteSvar.StatusCode);

        // Den første kjøringen bruker 5s på å feile -- fortsatt trygt "Pågår" når vi trigger igjen.
        using var andreRequest = new HttpRequestMessage(HttpMethod.Post, "/api/administrasjon/lovdata-resynk");
        andreRequest.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString());
        var andreSvar = await klient.SendAsync(andreRequest);
        Assert.Equal(HttpStatusCode.Conflict, andreSvar.StatusCode);
    }

    [Fact]
    public async Task Historikk_er_tom_liste_uten_feil_nar_ingenting_er_kjort_ennaa()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);

        var klient = _fixture.Factory.CreateClient();
        var svar = await klient.GetAsync("/api/administrasjon/lovdata-resynk");
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var historikk = await svar.Content.ReadFromJsonAsync<List<LovdataResynkKjoringDto>>(JsonInnstillinger);
        Assert.NotNull(historikk);
        Assert.Empty(historikk!);
    }

    [Fact]
    public async Task Innstilling_hentes_med_standardverdi_og_kan_oppdateres()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);

        var klient = _fixture.Factory.CreateClient();

        var standard = await klient.GetFromJsonAsync<LovdataResynkInnstillingDto>(
            "/api/administrasjon/lovdata-resynk/innstilling", JsonInnstillinger);
        Assert.Null(standard!.IntervallTimer);

        var brukerId = await HentEnBrukerIdAsync(klient);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/administrasjon/lovdata-resynk/innstilling")
        {
            Content = JsonContent.Create(new OppdaterLovdataResynkInnstillingRequest(24), options: JsonInnstillinger),
        };
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString());

        var svar = await klient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var oppdatert = await svar.Content.ReadFromJsonAsync<LovdataResynkInnstillingDto>(JsonInnstillinger);
        Assert.Equal(24, oppdatert!.IntervallTimer);

        var lestTilbake = await klient.GetFromJsonAsync<LovdataResynkInnstillingDto>(
            "/api/administrasjon/lovdata-resynk/innstilling", JsonInnstillinger);
        Assert.Equal(24, lestTilbake!.IntervallTimer);
    }

    [Fact]
    public async Task Negativt_intervall_gir_400()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);

        var klient = _fixture.Factory.CreateClient();
        var brukerId = await HentEnBrukerIdAsync(klient);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/administrasjon/lovdata-resynk/innstilling")
        {
            Content = JsonContent.Create(new OppdaterLovdataResynkInnstillingRequest(-5), options: JsonInnstillinger),
        };
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString());

        var svar = await klient.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Innstilling_uten_bruker_header_gir_400()
    {
        var klient = _fixture.Factory.CreateClient();
        var svar = await klient.PutAsJsonAsync(
            "/api/administrasjon/lovdata-resynk/innstilling", new OppdaterLovdataResynkInnstillingRequest(24), JsonInnstillinger);
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    // ---------- Del A (issue #201): feillogg pr. kjøring, GET .../{kjoringId}/feilede-dokumenter ----------

    private static async Task<Guid> NyFullfortKjoringAsync(RegelIdeDbContext db)
    {
        var kjoring = new LovdataResynkKjoringEntitet
        {
            Id = Guid.NewGuid(),
            Utlost = LovdataResynkUtlost.Manuell,
            UtlostAvBruker = "Kari Saksbehandler",
            Status = LovdataResynkStatus.Fullfort,
            StartetTidspunkt = DateTimeOffset.UtcNow,
            FullfortTidspunkt = DateTimeOffset.UtcNow,
        };
        db.LovdataResynkKjoringer.Add(kjoring);
        await db.SaveChangesAsync();
        return kjoring.Id;
    }

    [Fact]
    public async Task FeiledeDokumenter_gir_404_for_ukjent_kjoringId()
    {
        var klient = _fixture.Factory.CreateClient();
        var svar = await klient.GetAsync($"/api/administrasjon/lovdata-resynk/{Guid.NewGuid()}/feilede-dokumenter");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task FeiledeDokumenter_returnerer_kun_rader_for_DENNE_kjoringen_ikke_andre()
    {
        await using var db = _fixture.NyDbContext();
        var forsteKjoringId = await NyFullfortKjoringAsync(db);
        var andreKjoringId = await NyFullfortKjoringAsync(db);
        var datokode = $"LOV-TEST-{Guid.NewGuid():N}";

        db.LovdataImportstatusHistorikk.Add(new LovdataImportstatusHistorikkEntitet
        {
            Id = Guid.NewGuid(), KjoringId = forsteKjoringId, Datokode = datokode, Type = "lov",
            Tittel = "Testlov", Eli = "https://lovdata.no/eli/lov/test", Feilmelding = "Feil i kjøring 1.",
            ForsoktTidspunkt = DateTimeOffset.UtcNow,
        });
        db.LovdataImportstatusHistorikk.Add(new LovdataImportstatusHistorikkEntitet
        {
            Id = Guid.NewGuid(), KjoringId = andreKjoringId, Datokode = datokode, Type = "lov",
            Tittel = "Testlov", Eli = "https://lovdata.no/eli/lov/test", Feilmelding = "Feil i kjøring 2.",
            ForsoktTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var klient = _fixture.Factory.CreateClient();
        var svar = await klient.GetAsync($"/api/administrasjon/lovdata-resynk/{forsteKjoringId}/feilede-dokumenter");
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var rader = await svar.Content.ReadFromJsonAsync<List<LovdataImportstatusHistorikkDto>>(JsonInnstillinger);
        var raden = Assert.Single(rader!);
        Assert.Equal("Feil i kjøring 1.", raden.Feilmelding); // IKKE kjøring 2 sin feilmelding
    }

    [Fact]
    public async Task FeiledeDokumenter_er_tom_liste_for_kjoring_uten_feil()
    {
        await using var db = _fixture.NyDbContext();
        var kjoringId = await NyFullfortKjoringAsync(db);

        var klient = _fixture.Factory.CreateClient();
        var svar = await klient.GetAsync($"/api/administrasjon/lovdata-resynk/{kjoringId}/feilede-dokumenter");
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var rader = await svar.Content.ReadFromJsonAsync<List<LovdataImportstatusHistorikkDto>>(JsonInnstillinger);
        Assert.NotNull(rader);
        Assert.Empty(rader!);
    }

    // ---------- Del B (issue #201): aksjonskrok-bekreftelse, POST .../{kjoringId}/navnekandidat-sveip ----------
    // Johanns eksplisitte valg (kommentar på #201, 2026-09-10): BEKREFTELSESSTEG før selve sveipet,
    // MOTSATT av issuets egen "fullautomatisk"-anbefaling — se Program.cs-endepunktets kommentar.

    /// <summary>Seeder én delt/nasjonal, gjeldende rettskilde med tekst som treffer det FASTE
    /// gruppe-mønsteret ("Stortinget", se NavnekandidatOppdagelseTjeneste.FasteRollesubstantiv) —
    /// "gruppe"-treff klassifiseres ALDRI mot SNL/SSR (se den klassens kommentar), så sveipet gir et
    /// deterministisk, nettverksfritt resultat i testen.</summary>
    private static async Task<Guid> NyGjeldendeRettskildeMedGruppetreffAsync(RegelIdeDbContext db)
    {
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = $"https://test/{rettskildeId:N}/§1/ledd-1",
            KildeId = "ledd-1", NodeType = "ledd", Tekst = "Loven forvaltes av Stortinget.",
        });
        await db.SaveChangesAsync();
        return rettskildeId;
    }

    private static async Task<Guid> NyFullfortKjoringMedNyeRettskilderAsync(RegelIdeDbContext db, params Guid[] nyeRettskildeIder)
    {
        var kjoring = new LovdataResynkKjoringEntitet
        {
            Id = Guid.NewGuid(),
            Utlost = LovdataResynkUtlost.Manuell,
            UtlostAvBruker = "Kari Saksbehandler",
            Status = LovdataResynkStatus.Fullfort,
            StartetTidspunkt = DateTimeOffset.UtcNow,
            FullfortTidspunkt = DateTimeOffset.UtcNow,
            Nye = nyeRettskildeIder.Length,
            NyeRettskildeIder = nyeRettskildeIder.ToList(),
        };
        db.LovdataResynkKjoringer.Add(kjoring);
        await db.SaveChangesAsync();
        return kjoring.Id;
    }

    [Fact]
    public async Task NavnekandidatSveip_uten_bruker_header_gir_400()
    {
        await using var db = _fixture.NyDbContext();
        var kjoringId = await NyFullfortKjoringAsync(db);

        var klient = _fixture.Factory.CreateClient();
        var svar = await klient.PostAsync($"/api/administrasjon/lovdata-resynk/{kjoringId}/navnekandidat-sveip", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task NavnekandidatSveip_gir_404_for_ukjent_kjoringId()
    {
        var klient = _fixture.Factory.CreateClient();
        var brukerId = await HentEnBrukerIdAsync(klient);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/administrasjon/lovdata-resynk/{Guid.NewGuid()}/navnekandidat-sveip");
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString());
        var svar = await klient.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task NavnekandidatSveip_gir_400_nar_kjoringen_ikke_oppdaget_noen_nye_kilder()
    {
        await using var db = _fixture.NyDbContext();
        var kjoringId = await NyFullfortKjoringAsync(db); // NyeRettskildeIder er tom liste (standard)

        var klient = _fixture.Factory.CreateClient();
        var brukerId = await HentEnBrukerIdAsync(klient);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/administrasjon/lovdata-resynk/{kjoringId}/navnekandidat-sveip");
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString());
        var svar = await klient.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task NavnekandidatSveip_kjorer_sveip_for_nye_rettskilder_og_markerer_kjoringen_som_behandlet()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await NyGjeldendeRettskildeMedGruppetreffAsync(db);
        var kjoringId = await NyFullfortKjoringMedNyeRettskilderAsync(db, rettskildeId);

        var klient = _fixture.Factory.CreateClient();
        var brukerId = await HentEnBrukerIdAsync(klient);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/administrasjon/lovdata-resynk/{kjoringId}/navnekandidat-sveip");
        request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString());
        var svar = await klient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);

        var resultat = await svar.Content.ReadFromJsonAsync<KjorNyeKilderSveipResultatDto>(JsonInnstillinger);
        Assert.Equal(1, resultat!.AntallRettskilderForsokt);
        Assert.Equal(0, resultat.AntallRettskilderHoppetOver);
        Assert.True(resultat.AntallTreffFunnet > 0, "'Stortinget' skulle vært funnet av det faste gruppe-mønsteret.");
        Assert.True(resultat.AntallNyeKandidater > 0);

        // GET / (historikklisten) skal nå vise varslingsraden som BEHANDLET -- se AdministrasjonLovdataResynk.tsx.
        var historikkSvar = await klient.GetFromJsonAsync<List<LovdataResynkKjoringDto>>(
            "/api/administrasjon/lovdata-resynk", JsonInnstillinger);
        var raden = historikkSvar!.Single(k => k.Id == kjoringId);
        Assert.Equal(1, raden.AntallNyeKilderOppdaget);
        Assert.NotNull(raden.NyeKilderSveipUtfortTidspunkt);
    }
}
