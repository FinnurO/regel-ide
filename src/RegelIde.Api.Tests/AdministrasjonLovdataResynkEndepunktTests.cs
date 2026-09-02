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
}
