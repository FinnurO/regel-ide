using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #311, 2026-10-07] En fersk database migrert til et BESTEMT historisk punkt, pluss rå-SQL for
/// tabellene som ikke lenger har EF-entiteter (<c>myndighetstildelinger</c>, <c>gruppe_medlemskap</c>,
/// <c>virksomhet_relasjoner</c> — droppet i <c>InnforStrukturkanttabell</c>).
/// <para>
/// Hvorfor: testene for en datamigrasjon (<c>NodetypeReklassifiseringTests</c> for #310,
/// <c>StrukturkantMigreringTests</c> for #311) må kjøre mot SKJEMAET SOM DET VAR da migrasjonen kjørte. Før
/// #311 migrerte #310-testene til siste versjon og brukte EF-entitetene for de gamle tabellene; det gikk så
/// lenge siste versjon var #310. Å migrere til et navngitt punkt holder dem gyldige for alltid.
/// </para>
/// </summary>
internal static class HistoriskSkjema
{
    public const string Nodetypeakse = "20261007074037_InnforNodetypeakse";
    public const string Strukturkanttabell = "20261007202146_InnforStrukturkanttabell";
    public const string HarmoniserRelasjonskoder = "20261008064334_HarmoniserRelasjonskoder"; // [Ny, #330]

    /// <summary>Oppretter en ny, tom database og migrerer den til <paramref name="tilMigrasjon"/> (null = siste).</summary>
    public static async Task<string> NyDatabaseAsync(EmbeddedPostgresFixture fixture, string prefiks, string? tilMigrasjon)
    {
        var navn = $"{prefiks}_{Guid.NewGuid():N}";
        await using (var master = new RegelIdeDbContext(Options(ByttDatabase(fixture, "postgres"))))
        {
            // EF1003: et databasenavn er en IDENTIFIKATOR og kan ikke være en SQL-parameter i DDL.
            // Navnet er generert her, av en Guid — ingen ytre inndata er involvert.
#pragma warning disable EF1003
            await master.Database.ExecuteSqlRawAsync("CREATE DATABASE " + navn + ";");
#pragma warning restore EF1003
        }
        var connString = ByttDatabase(fixture, navn);
        await MigrerAsync(connString, tilMigrasjon);
        if (tilMigrasjon is not null) await LeggTilSenereKolonnerAsync(connString);
        return connString;
    }

    /// <summary>
    /// [Ny, issue #312, 2026-10-08] Testene skriver og leser <see cref="BegrepEntitet"/> med DAGENS EF-modell mot et
    /// HISTORISK skjema. Når en senere migrasjon legger en kolonne til en slik tabell (#312: <c>begreper.omradetype</c>/
    /// <c>omradekode</c>), ville hver INSERT/SELECT velte på «column does not exist» — 6 tester gjorde det. Kolonnene legges
    /// derfor til tomme (nullable, uten constraints) her. Det endrer ikke det testene måler: migrasjonene de tester rører
    /// ikke kolonnene. Ingen av testene migrerer videre til siste versjon etterpå (da ville AddColumn kollidert).
    /// </summary>
    private static async Task LeggTilSenereKolonnerAsync(string connString)
    {
        await using var db = new RegelIdeDbContext(Options(connString));
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE begreper ADD COLUMN IF NOT EXISTS omradetype text; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS omradekode text;");
    }

    public static async Task MigrerAsync(string connString, string? tilMigrasjon)
    {
        await using var db = new RegelIdeDbContext(Options(connString));
        await db.GetService<IMigrator>().MigrateAsync(tilMigrasjon);
    }

    public static DbContextOptions<RegelIdeDbContext> Options(string connString) =>
        new DbContextOptionsBuilder<RegelIdeDbContext>().UseNpgsql(connString).Options;

    private static string ByttDatabase(EmbeddedPostgresFixture fixture, string databasenavn) =>
        fixture.ConnectionString.Replace("Database=regelide_test", $"Database={databasenavn}");

    // ---------- Rå SQL for de droppede tabellene ----------

    public sealed record Rad(Guid Id);

    public static async Task<Rad> TildelingAsync(RegelIdeDbContext db, Guid gruppe, Guid virksomhet, Guid hjemmel,
        string paragrafspennJson = """[{"FraEid":"§1","TilEid":null}]""", string? vilkaar = null, string status = "validert")
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO myndighetstildelinger ("Id", gruppe_begrep_id, virksomhet_id, hjemmel_rettskilde_id, paragrafspenn_json,
                vilkaar, status, opprettet_av, opprettet_tidspunkt)
            VALUES ({id}, {gruppe}, {virksomhet}, {hjemmel}, {paragrafspennJson}, {vilkaar}, {status}, 'test', now())
            """);
        return new Rad(id);
    }

    public static async Task<Rad> MedlemskapAsync(RegelIdeDbContext db, Guid over, Guid under, Guid hjemmel)
    {
        var id = Guid.NewGuid();
        const string spenn = """[{"FraEid":"§1","TilEid":null}]""";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO gruppe_medlemskap ("Id", overordnet_gruppe_begrep_id, underordnet_gruppe_begrep_id, hjemmel_rettskilde_id,
                paragrafspenn_json, status, opprettet_av, opprettet_tidspunkt)
            VALUES ({id}, {over}, {under}, {hjemmel}, {spenn}, 'validert', 'test', now())
            """);
        return new Rad(id);
    }

    public static async Task<Rad> RelasjonAsync(RegelIdeDbContext db, Guid fra, Guid til, string type, Guid? hjemmel,
        string? hjemmelEid, string? kommentar, string status = "validert")
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO virksomhet_relasjoner ("Id", fra_virksomhet_id, til_virksomhet_id, relasjons_type, hjemmel_rettskilde_id,
                hjemmel_eid, kommentar, status, opprettet_av, opprettet_tidspunkt)
            VALUES ({id}, {fra}, {til}, {type}, {hjemmel}, {hjemmelEid}, {kommentar}, {status}, 'test', now())
            """);
        return new Rad(id);
    }

    /// <summary>Ett heltall fra en spørring som returnerer én kolonne kalt <c>"Value"</c>.</summary>
    public static Task<int> TellAsync(RegelIdeDbContext db, FormattableString sql) =>
        db.Database.SqlQuery<int>(sql).SingleAsync();

    public static Task<Guid> GuidAsync(RegelIdeDbContext db, FormattableString sql) =>
        db.Database.SqlQuery<Guid>(sql).SingleAsync();
}
