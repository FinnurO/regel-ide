using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RegelIde.Data;

/// <summary>Hvilken databasemotor som brukes. Velges med konfigurasjonsnøkkelen <c>RegelIde:Database</c>.</summary>
public enum Databaseprofil
{
    /// <summary>Standard. Lokal utvikling (docker-compose), alle tester, og målbildet i drift.</summary>
    Postgres,

    /// <summary>
    /// Én fil, ingen egen databaseprosess. Finnes for at containeren skal kunne kjøre i et
    /// app-cluster som ikke gir oss verken root eller et persistent volum — se docker/README.md.
    /// Databasen bygges fra modellen ved hver oppstart og forsvinner med containeren.
    /// </summary>
    Sqlite,
}

/// <summary>
/// Ett sted å velge og sette opp databasen, slik at valget ikke ligger spredt som
/// <c>UseNpgsql</c>-kall i Program.cs. Postgres er og blir standard; SQLite er en deploy-profil,
/// ikke en likeverdig motor — se <see cref="Databaseprofil"/>.
/// </summary>
public static class Databaseoppsett
{
    public const string Konfigurasjonsnokkel = "RegelIde:Database";

    private const string PostgresStandard =
        "Host=localhost;Port=5432;Database=regelide;Username=postgres;Password=postgres";

    public static Databaseprofil LesProfil(IConfiguration konfigurasjon) =>
        (konfigurasjon[Konfigurasjonsnokkel] ?? "postgres").Trim().ToLowerInvariant() switch
        {
            "postgres" or "postgresql" => Databaseprofil.Postgres,
            "sqlite" => Databaseprofil.Sqlite,
            var ukjent => throw new InvalidOperationException(
                $"Ukjent {Konfigurasjonsnokkel}='{ukjent}'. Gyldige verdier: postgres | sqlite."),
        };

    public static IServiceCollection LeggTilRegelIdeDatabase(
        this IServiceCollection tjenester, IConfiguration konfigurasjon)
    {
        var profil = LesProfil(konfigurasjon);
        var tilkobling = konfigurasjon.GetConnectionString("RegelIdeDb") ?? profil switch
        {
            Databaseprofil.Sqlite => "Data Source=regelide.db",
            _ => PostgresStandard,
        };

        return tjenester.AddDbContext<RegelIdeDbContext>(o =>
        {
            if (profil is Databaseprofil.Sqlite) o.UseSqlite(tilkobling);
            else o.UseNpgsql(tilkobling);
        });
    }

    /// <summary>
    /// Sørger for at skjemaet finnes.
    /// <para>
    /// Postgres kjører migrasjonene. SQLite bygger skjemaet rett fra modellen med
    /// <c>EnsureCreated</c>, fordi migrasjonene i <c>Migrasjoner/</c> er generert for Npgsql og
    /// inneholder typenavn SQLite ikke kjenner (<c>uuid</c>, <c>jsonb</c>, <c>text[]</c>,
    /// <c>timestamp with time zone</c>, <c>now()</c>) — første CREATE TABLE ville feilet.
    /// </para>
    /// <para>
    /// Et eget migrasjonssett for SQLite ville kostet et nytt prosjekt og dobbelt vedlikehold ved
    /// hver skjemaendring, uten å gi noe: SQLite-basen er efemer og har aldri data å migrere.
    /// Prisen er at SQLite-skjemaet kan drive fra migrasjonene uten at noen merker det —
    /// akseptabelt så lenge profilen kun brukes til demo/test, og en grunn til at den ikke skal
    /// ta imot data som skal overleve.
    /// </para>
    /// </summary>
    public static async Task SorgForSkjemaAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlite())
        {
            await db.Database.MigrateAsync(ct);
            return;
        }

        await db.Database.EnsureCreatedAsync(ct);

        // WAL lar lesere og skriveren jobbe samtidig. Uten den serialiserer SQLite alt og gir
        // "database is locked" så snart to forespørsler treffer basen samtidig. Innstillingen
        // lagres i selve filen, så den settes én gang her og gjelder alle tilkoblinger etterpå.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
