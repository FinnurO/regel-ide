using Microsoft.EntityFrameworkCore;
using MysticMind.PostgresEmbed;

namespace RegelIde.Data.Tests;

/// <summary>
/// Kjører migrasjonene mot en ekte, engangs Postgres-instans (ingen Docker/Podman nødvendig i
/// dette miljøet — se src/README.md). Delt per testklasse via IClassFixture, ikke per test,
/// siden oppstart av embedded Postgres tar noen sekunder.
/// </summary>
public sealed class EmbeddedPostgresFixture : IAsyncLifetime
{
    private PgServer? _server;
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        // Eksplisitt instanceId OG fast port: unngår kollisjon med andre embedded Postgres-instanser
        // (f.eks. RegelIde.Api.Tests, som bruker port 55433) når `dotnet test` kjøres for hele
        // løsningen og flere testprosjekter starter embedded Postgres samtidig — PgServers
        // auto-portvalg (port: 0) viste seg upålitelig under akkurat denne samtidigheten.
        _server = new PgServer("15.4.0", instanceId: Guid.NewGuid(), port: 55432, clearInstanceDirOnStop: true);
        await Task.Run(() => _server.Start());
        ConnectionString = $"Host=localhost;Port={_server.PgPort};Username=postgres;Password=postgres;Database=regelide_test";

        await using var master = new RegelIdeDbContext(NyOptions("Host=localhost;Port=" + _server.PgPort + ";Username=postgres;Password=postgres;Database=postgres"));
        await master.Database.ExecuteSqlRawAsync("CREATE DATABASE regelide_test;");

        await using var db = new RegelIdeDbContext(NyOptions(ConnectionString));
        await db.Database.MigrateAsync();
    }

    public RegelIdeDbContext NyDbContext() => new(NyOptions(ConnectionString));

    private static DbContextOptions<RegelIdeDbContext> NyOptions(string connString) =>
        new DbContextOptionsBuilder<RegelIdeDbContext>().UseNpgsql(connString).Options;

    public Task DisposeAsync()
    {
        try
        {
            _server?.Stop();
        }
        catch (UnauthorizedAccessException)
        {
            // Kjent Windows-spesifikt opprydningsproblem i MysticMind.PostgresEmbed — en fillås
            // (f.eks. icudt*.dll) kan henge igjen kort tid etter at Postgres-prosessen er stoppet.
            // Ufarlig for testresultatet (selve testen er allerede ferdig når dette skjer); scratch-
            // mappen ryddes uansett bort av OS-et over tid.
        }
        _server?.Dispose();
        return Task.CompletedTask;
    }
}
