using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MysticMind.PostgresEmbed;
using Npgsql;
using RegelIde.Data;
using RegelIde.TestInfrastruktur;

namespace RegelIde.Api.Tests;

/// <summary>
/// Kjører hele API-et (inkl. Program.cs sin migrasjon+førstegangs-seeding) mot en ekte, engangs
/// Postgres-instans — ingen Docker/Podman nødvendig i dette miljøet (se src/README.md).
/// Merk: overstyrer tilkoblingsstrengen via en PROSESS-global miljøvariabel (se InitializeAsync),
/// siden Program.cs (minimal hosting) leser konfigurasjon før WebApplicationFactorys egne hooks
/// rekker å virke. Dette er trygt så lenge kun én testklasse bruker denne fixturen samtidig —
/// blir det flere, må de enten dele én instans eller unngå parallell kjøring (xunit-collection).
/// </summary>
public sealed class EmbeddedPostgresApiFixture : IAsyncLifetime
{
    private PgServer? _server;
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    private string _connString = "";

    /// <summary>Direkte DB-tilgang for testoppsett (seede data Program.cs' egen seeding ikke dekker, f.eks. Utkast-rader).</summary>
    public RegelIdeDbContext NyDbContext() =>
        new(new DbContextOptionsBuilder<RegelIdeDbContext>().UseNpgsql(_connString).Options);

    public async Task InitializeAsync()
    {
        // Eksplisitt instanceId, men AUTO-portvalg (port: 0): to testklasser i denne assemblyen
        // (RettskilderEndepunktTests, ImportEndepunktTests) har hver sin instans av denne fixturen
        // og startet tidligere embedded Postgres på samme FASTE port — trygt sekvensielt (se
        // AssemblyInfo.cs: DisableTestParallelization), men en fast port delt mellom dem kolliderte
        // uansett pga. SharpCompress/Windows sin porttildeling-timing. Auto-portvalg unngår dette,
        // og er trygt mot RegelIde.Data.Tests (egen prosess, fast port 55432) siden OS-en uansett
        // ikke gir ut en port som allerede er bundet av en annen prosess.
        _server = new PgServer("15.4.0", instanceId: Guid.NewGuid(), clearInstanceDirOnStop: true);
        await Task.Run(() => _server.Start());

        var masterConnString = $"Host=localhost;Port={_server.PgPort};Username=postgres;Password=postgres;Database=postgres";
        await EmbeddedPostgresHjelper.VentTilKlarAsync(masterConnString);
        await using (var conn = new NpgsqlConnection(masterConnString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("CREATE DATABASE regelide_api_test;", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        var testConnString = $"Host=localhost;Port={_server.PgPort};Username=postgres;Password=postgres;Database=regelide_api_test";
        _connString = testConnString;

        // Program.cs leser ConnectionStrings:RegelIdeDb via builder.Configuration rett etter
        // WebApplication.CreateBuilder(args) — FØR WebApplicationFactorys egne ConfigureAppConfiguration-
        // hooks rekker å legge seg inn i konfigurasjonen for en minimal-hosting-app i prosess. En
        // miljøvariabel derimot leses av CreateBuilder sine innebygde standardkilder med én gang,
        // og er derfor den pålitelige veien å overstyre tilkoblingsstrengen i denne testprosessen.
        Environment.SetEnvironmentVariable("ConnectionStrings__RegelIdeDb", testConnString);

        // Samme resonnement gjelder RegelIde:KiAgent:Leverandor (Program.cs:54, avgjør DI rett etter
        // CreateBuilder). Uten dette ville en ekte appsettings.Local.json/user-secrets på
        // utviklermaskinen (satt for byggesteg 5 runde 3-testing mot HostYourAI) blitt lest av DENNE
        // testprosessen også — testene som forventer et fast, deterministisk KiAgentKlientStub-svar
        // ville i stedet gjort ekte, betalte nettverkskall og fått ekte (varierende) KI-svar. Tvungen
        // Stub her, uavhengig av hva som ligger i utviklerens lokale config.
        Environment.SetEnvironmentVariable("RegelIde__KiAgent__Leverandor", "Stub");

        // Samme resonnement: uten dette ville HVER test som bruker denne fixturen (og dermed
        // WebApplicationFactory<Program>, som kjører Program.cs' faktiske DI/oppstart) trigget en
        // ekte Lovdata-fullimport av tusenvis av dokumenter i bakgrunnen — trege, nettverksavhengige
        // og helt uten poeng for det disse testene faktisk verifiserer. Se
        // LovdataFullimportBakgrunnstjeneste i RegelIde.Api.
        Environment.SetEnvironmentVariable("RegelIde__LovdataFullimport__AktivVedOppstart", "false");

        // Samme resonnement som over, for den NYE periodiske planlagt-resynk-sjekken (administrasjon-
        // Lovdata-resynk, GitHub-issue #104, LovdataResynkPlanleggerBakgrunnstjeneste) — uten dette
        // ville hver test-kjøring av denne fixturen (levetid: en hel testklasse) risikert å treffe den
        // første timelige sjekken og trigge en ekte, utilsiktet Lovdata-fullimport i bakgrunnen.
        Environment.SetEnvironmentVariable("RegelIde__LovdataFullimport__PlanlagtResynkAktiv", "false");

        // [Ny, issue #117] Standard, DEFAULT-stubbet SNL/SSR-oppslag for HELE denne DELTE fixturen
        // (samme "aldri ekte, utilsiktede nettverkskall i en testkjøring"-hensyn som Stub-KI-leverandøren
        // og deaktivert Lovdata-fullimport over). Uten dette ville NavnekandidatOppdagelseTjeneste.SveipAsync
        // (nå utvidet til å kjøre "virksomhet"-kandidater fra suffiks-/flerords-mønstrene gjennom
        // EksternNavneoppslagTjeneste, se den klassens kommentar) gjort EKTE, langsomme/uforutsigbare
        // HTTP-kall mot snl.no/ws.geonorge.no fra ENHVER test i denne collection-en som (utilsiktet)
        // treffer et "virksomhet"-mønster (flere gjør det allerede, f.eks.
        // NavnekandidaterEndepunktTests' "Miljødirektoratet"/"Fiskeridirektoratet"-tekster). Svarer "ingen
        // treff" for BEGGE kildene — samme nøytrale, IKKE-forkastende "ukjent i begge"-gren (docs/31 §2
        // punkt 3) som resten av klassen allerede bygger for en term som verken bekreftes eller avkreftes.
        // Enkelttester som faktisk vil teste SNL/SSR-klassifiseringen via API-et overstyrer dette per test
        // med sin EGEN WithWebHostBuilder-avledede klient (samme mønster som BrregEndepunktTests bruker
        // for BrregKlient) — de rører ikke denne delte standarden.
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient<EksternNavneoppslagTjeneste>()
                    .ConfigurePrimaryHttpMessageHandler(() => new IngenEksternTreffHandler())));

        // Trigger host-oppstart (migrasjon + seeding i Program.cs) nå, ikke ved første test.
        using var warmup = Factory.CreateClient();
        await warmup.GetAsync("/api/rettskilder");
    }

    /// <summary>Default-stub for <see cref="EksternNavneoppslagTjeneste"/> i denne DELTE fixturen — svarer
    /// "ingen treff" (tomt JSON-svar i riktig form) uansett hvilken av de to kildene (SNL-søk eller
    /// SSR-stedsnavn-søk) som spørres. Se <see cref="InitializeAsync"/> for hvorfor dette må stå på selve
    /// <see cref="Factory"/>, ikke bare per-test (denne fixturen deles av HELE RegelIde.Api.Tests-samlingen,
    /// se <see cref="ApiTestCollection"/>).</summary>
    private sealed class IngenEksternTreffHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            var body = url.Contains("stedsnavn", StringComparison.OrdinalIgnoreCase)
                ? """{ "navn": [] }"""
                : "[]"; // SNL-søket (api/v1/search) svarer med en flat treffliste.
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    public Task DisposeAsync()
    {
        Factory?.Dispose();
        try
        {
            _server?.Stop();
        }
        catch (UnauthorizedAccessException)
        {
            // Kjent Windows-spesifikt opprydningsproblem i MysticMind.PostgresEmbed, se
            // RegelIde.Data.Tests/EmbeddedPostgresFixture.cs — ufarlig for testresultatet.
        }
        _server?.Dispose();
        return Task.CompletedTask;
    }
}
