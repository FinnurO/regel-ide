using System.Linq;
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

        // [Ny, issue #117; restrukturert 2026-09-03] Standard, DEFAULT-stubbet SNL/SSR-oppslag for HELE
        // denne DELTE fixturen (samme "aldri ekte, utilsiktede nettverkskall i en testkjøring"-hensyn som
        // Stub-KI-leverandøren og deaktivert Lovdata-fullimport over). Uten dette ville
        // NavnekandidatOppdagelseTjeneste.SveipAsync (som kjører ALLE "virksomhet"-kandidater — flerords-
        // OG det brede stor-bokstav-mønsteret, se den klassens kommentar — gjennom EksternNavneoppslagTjeneste)
        // gjort EKTE, langsomme/uforutsigbare HTTP-kall mot snl.no/ws.geonorge.no fra ENHVER test i denne
        // collection-en som (utilsiktet) treffer et "virksomhet"-mønster (flere gjør det allerede, f.eks.
        // NavnekandidaterEndepunktTests' "Miljødirektoratet"/"Fiskeridirektoratet"-tekster).
        // <para>
        // [Restrukturert, 2026-09-03] Svarte TIDLIGERE "ingen treff" for BEGGE kildene — under den NYE
        // to-utfalls klassifiseringen (se NavnekandidatOppdagelseTjeneste.KlassifiserAsync sin kommentar)
        // ville det latt ENHVER "virksomhet"-kandidat i denne DELTE fixturen opprettes DIREKTE som
        // Status="Avvist" i stedet for "Venter", og brutt praktisk talt ALLE eksisterende
        // NavnekandidaterEndepunktTests-tester som forutsetter en godkjennbar/avvisbar "virksomhet"-kandidat
        // (default-listingen viser kun 'Venter'). Denne delte standard-stubben SNL-BEKREFTER derfor nå
        // ETHVERT søkt navn (ekko av selve søketermen tilbake som artikkelens headword/organisasjonsnavn)
        // — en bevisst forenkling KUN for denne delte fixturen (selve SNL/SSR-KLASSIFISERINGSLOGIKKEN
        // testes presist, med spesifikke treff/ikke-treff-scenarioer, i RegelIde.Data.Tests'
        // NavnekandidatOppdagelseTjenesteTests, ikke her). SSR svarer fortsatt "ingen treff" (ubrukt av
        // disse API-testene). Enkelttester som faktisk vil teste SNL/SSR-KLASSIFISERINGEN via API-et
        // overstyrer dette per test med sin EGEN WithWebHostBuilder-avledede klient (samme mønster som
        // BrregEndepunktTests bruker for BrregKlient) — de rører ikke denne delte standarden.
        // </para>
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient<EksternNavneoppslagTjeneste>()
                    .ConfigurePrimaryHttpMessageHandler(() => new AltErInstitusjonHandler())));

        // Trigger host-oppstart (migrasjon + seeding i Program.cs) nå, ikke ved første test.
        using var warmup = Factory.CreateClient();
        await warmup.GetAsync("/api/rettskilder");
    }

    /// <summary>
    /// [Restrukturert, 2026-09-03, tidligere "IngenEksternTreffHandler"] Default-stub for
    /// <see cref="EksternNavneoppslagTjeneste"/> i denne DELTE fixturen — se <see cref="InitializeAsync"/>
    /// for hvorfor dette må stå på selve <see cref="Factory"/>, ikke bare per-test (denne fixturen deles
    /// av HELE RegelIde.Api.Tests-samlingen, se <see cref="ApiTestCollection"/>).
    /// <para>
    /// SNL-søket (<c>api/v1/search</c>) svarer med ETT treff som SNL-BEKREFTER den faktisk spurte termen
    /// (leser <c>query</c>-parameteren fra selve forespørselen og ekkoer den tilbake som artikkelens
    /// <c>headword</c>/<c>organization_name</c> — via en artikkel-URL som selv koder inn termen, slik at
    /// det andre kallet (artikkel-JSON-oppslaget) kan dekode den ut igjen). SSR-stedsnavn-søket svarer
    /// fortsatt "ingen treff" (tomt).
    /// </para>
    /// </summary>
    private sealed class AltErInstitusjonHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("stedsnavn", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("""{ "navn": [] }"""));
            }
            if (url.Contains("api/v1/search", StringComparison.OrdinalIgnoreCase))
            {
                var term = HentSpørreparameter(request.RequestUri!, "query");
                var kodetTerm = Uri.EscapeDataString(term);
                return Task.FromResult(Json($$"""
                [{ "article_type_id": 16, "taxonomy_title": "Test-taksonomi",
                   "article_url": "https://snl.no/{{kodetTerm}}",
                   "article_url_json": "https://snl.no/{{kodetTerm}}.json" }]
                """));
            }
            // Artikkel-JSON-oppslaget (article_url_json over) — termen er kodet inn i selve URL-en.
            var termFraUrl = Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..^".json".Length]);
            return Task.FromResult(Json($$"""
            { "headword": "{{termFraUrl}}", "url": "{{url[..^".json".Length]}}",
              "metadata": { "organization_name": "{{termFraUrl}}" } }
            """));
        }

        private static string HentSpørreparameter(Uri uri, string navn)
        {
            foreach (var par in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var deler = par.Split('=', 2);
                if (deler[0] == navn) return Uri.UnescapeDataString(deler.ElementAtOrDefault(1) ?? "");
            }
            return "";
        }

        private static HttpResponseMessage Json(string body) => new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
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
