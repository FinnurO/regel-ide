using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MysticMind.PostgresEmbed;
using Npgsql;
using RegelIde.Data;

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
        await VentTilPostgresErKlarAsync(masterConnString);
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

        Factory = new WebApplicationFactory<Program>();

        // Trigger host-oppstart (migrasjon + seeding i Program.cs) nå, ikke ved første test.
        using var warmup = Factory.CreateClient();
        await warmup.GetAsync("/api/rettskilder");
    }

    /// <summary>
    /// PgServer.Start() returnerer før Postgres nødvendigvis er ferdig med oppstartsfasen
    /// (57P03 "the database system is starting up") — retry med kort ventetid i stedet for å anta
    /// at porten er klar med én gang.
    /// </summary>
    private static async Task VentTilPostgresErKlarAsync(string connString)
    {
        for (var forsok = 1; forsok <= 20; forsok++)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();
                return;
            }
            catch (Npgsql.PostgresException) when (forsok < 20)
            {
                await Task.Delay(250);
            }
            catch (Npgsql.NpgsqlException) when (forsok < 20)
            {
                await Task.Delay(250);
            }
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
