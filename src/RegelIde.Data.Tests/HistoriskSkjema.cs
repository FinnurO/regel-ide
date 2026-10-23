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
    public const string Omraderegister = "20261008080640_InnforOmraderegister"; // [Ny, #341]
    public const string KompetanseMedMotpart = "20261008174215_KompetanseMedMotpart"; // [Ny, #352]
    public const string OppnevningsfamilienOgRester = "20261008194308_OppnevningsfamilienOgRester"; // [Ny, #353]
    public const string PliktOgOrdning = "20261009000135_PliktOgOrdning"; // [Ny, #355]
    public const string AvslutningSpeilerInnsetting = "20261009093903_AvslutningSpeilerInnsetting"; // [Ny, #367]
    public const string AvvistSomStatus = "20261015173308_AvvistSomStatus"; // [Ny, #368]
    public const string GeneriskNavneformOgBinding = "20261016072936_GeneriskNavneformOgBinding"; // [Ny, #384]

    /// <summary>Oppretter en ny, tom database og migrerer den til <paramref name="tilMigrasjon"/> (null = siste).</summary>
    /// <param name="leggTilSenereKolonner">[Ny, #341] false for en test som selv migrerer videre til siste versjon
    /// (da ville kolonnene <see cref="LeggTilSenereKolonnerAsync"/> legger til, kollidert med migrasjonens AddColumn) —
    /// den må skrive radene med rå SQL.</param>
    public static async Task<string> NyDatabaseAsync(EmbeddedPostgresFixture fixture, string prefiks, string? tilMigrasjon,
        bool leggTilSenereKolonner = true)
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
        await MigrerAsync(connString, tilMigrasjon, leggTilSenereKolonner);
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
        // [Ny, issue #341] strukturkanter.normform/grunnlag/delegerbar (KompetanseMedMotpart). IF EXISTS: før #311 finnes
        // ikke tabellen.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS normform text; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS grunnlag text; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS delegerbar boolean; "
            + "ALTER TABLE relasjonstype_konfigurasjon ADD COLUMN IF NOT EXISTS familie text; "
            + "ALTER TABLE relasjonstype_konfigurasjon ADD COLUMN IF NOT EXISTS fvl_kategori text;");
        // [Ny, issue #352] strukturkanter.undertype (OppnevningsfamilienOgRester).
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS undertype text; "
            + "ALTER TABLE relasjonstype_konfigurasjon ADD COLUMN IF NOT EXISTS saksavhengig boolean NOT NULL DEFAULT false;");
        // [Ny, issue #353] strukturkanter.modalitet og virksomheter.ordningstype (PliktOgOrdning).
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS modalitet text; "
            + "ALTER TABLE virksomheter ADD COLUMN IF NOT EXISTS ordningstype text;");
        // [Ny, issue #367] Utsagnsstatus (UtsagnsstatusOgDelegeringsadgang). delegerbar står (lagt til over) — den gamle kolonnen
        // leses ikke av dagens modell.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS delegeringsadgang text; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS delegering_slutning text; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS kunnskapsstatus text; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS begrunnelse text; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS tolkningsargumenter text[]; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS entitetsstatus text NOT NULL DEFAULT 'gjeldende'; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS erstatter_kant_id uuid; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS profilversjon text; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS hjemmel_node_id uuid; "
            + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS hjemmel_endret_tidspunkt timestamptz; "
            + "ALTER TABLE relasjonstype_konfigurasjon ADD COLUMN IF NOT EXISTS fvl_kategori_hjemmel text; "
            + "ALTER TABLE relasjonstype_konfigurasjon ADD COLUMN IF NOT EXISTS fvl_kategori_gjelder_fra date; "
            + "ALTER TABLE relasjonstype_konfigurasjon ADD COLUMN IF NOT EXISTS fvl_kategori_gjelder_til date;");
        // [Ny, issue #313 / #374] strukturkanter.avvisning_begrunnelse (AvvistSomStatus).
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS avvisning_begrunnelse text;");
        // [Ny, issue #384] begreper.medlemskapsregel og begreper.kunnskapsstatus (KlasseKommuneOgUtledetMedlemskap).
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE begreper ADD COLUMN IF NOT EXISTS medlemskapsregel text; "
            + "ALTER TABLE begreper ADD COLUMN IF NOT EXISTS kunnskapsstatus text;");
        // [Ny, issue #385] Aktoridentitet: navneformen som modellutsagn, lenkelister, hjemmel endret på bindinger, avvisningsbegrunnelse.
        await db.Database.ExecuteSqlRawAsync(Begrepskolonner385Legg
            + "; ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS kilde_utenfor_korpus_lenker text[]"
            + "; ALTER TABLE IF EXISTS navneform_bindinger ADD COLUMN IF NOT EXISTS kilde_utenfor_korpus_lenker text[]"
            + "; ALTER TABLE IF EXISTS navneform_bindinger ADD COLUMN IF NOT EXISTS hjemmel_endret_tidspunkt timestamptz"
            + "; ALTER TABLE navnekandidater ADD COLUMN IF NOT EXISTS avvisning_begrunnelse text;");
    }

    private const string Begrepskolonner385Legg = "ALTER TABLE begreper ADD COLUMN IF NOT EXISTS godkjenning text; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS kilde_utenfor_korpus_tekst text; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS kilde_utenfor_korpus_type text; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS kilde_utenfor_korpus_dokumentasjon text; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS kilde_utenfor_korpus_lenker text[]; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS begrunnelse text; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS tolkningsargumenter text[]; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS oppdagelses_kilde text; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS avvisning_begrunnelse text";
    private const string Begrepskolonner385Fjern = "ALTER TABLE begreper DROP COLUMN IF EXISTS godkjenning; ALTER TABLE begreper DROP COLUMN IF EXISTS kilde_utenfor_korpus_tekst; ALTER TABLE begreper DROP COLUMN IF EXISTS kilde_utenfor_korpus_type; ALTER TABLE begreper DROP COLUMN IF EXISTS kilde_utenfor_korpus_dokumentasjon; ALTER TABLE begreper DROP COLUMN IF EXISTS kilde_utenfor_korpus_lenker; ALTER TABLE begreper DROP COLUMN IF EXISTS begrunnelse; ALTER TABLE begreper DROP COLUMN IF EXISTS tolkningsargumenter; ALTER TABLE begreper DROP COLUMN IF EXISTS oppdagelses_kilde; ALTER TABLE begreper DROP COLUMN IF EXISTS avvisning_begrunnelse";

    /// <summary>
    /// [Ny, issue #384] For en test som skriver begreper med DAGENS EF-modell mot et historisk skjema og så migrerer VIDERE til siste
    /// versjon (<c>leggTilSenereKolonner: false</c>): kolonnene fra <c>KlasseKommuneOgUtledetMedlemskap</c> legges til før skrivingen
    /// (<paramref name="leggTil"/> = true) og fjernes igjen før migreringen (false) — ellers ville migrasjonens AddColumn kollidert.
    /// </summary>
    public static async Task BegrepskolonnerFra384Async(string connString, bool leggTil)
    {
        await using var db = new RegelIdeDbContext(Options(connString));
        // [ENDRET, issue #385] + kolonnene fra Aktoridentitet.
        await db.Database.ExecuteSqlRawAsync(leggTil
            ? "ALTER TABLE begreper ADD COLUMN IF NOT EXISTS medlemskapsregel text; ALTER TABLE begreper ADD COLUMN IF NOT EXISTS kunnskapsstatus text; "
              + Begrepskolonner385Legg
            : "ALTER TABLE begreper DROP COLUMN IF EXISTS medlemskapsregel; ALTER TABLE begreper DROP COLUMN IF EXISTS kunnskapsstatus; "
              + Begrepskolonner385Fjern);
    }

    /// <summary>
    /// [Ny, issue #353, 2026-10-09] En virksomhet med rå SQL, for testene som migrerer VIDERE til siste versjon
    /// (<c>leggTilSenereKolonner: false</c>): fra #353 har <see cref="Virksomhet"/> kolonnen <c>ordningstype</c>, som det historiske
    /// skjemaet ikke har, så en EF-innsetting med dagens modell velter («column does not exist»). Samme grunn som kantene i de
    /// testene alltid er skrevet med rå SQL.
    /// </summary>
    public static async Task<Guid> VirksomhetAsync(RegelIdeDbContext db, string navn)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO virksomheter (\"Id\", navn) VALUES ({id}, {navn})");
        return id;
    }

    /// <param name="leggTilSenereKolonner">[Ny, #341] Etter en migrering til et HISTORISK punkt legges senere kolonner til
    /// (som i <see cref="NyDatabaseAsync"/>) — StrukturkantMigreringTests leser strukturkanter med dagens EF-modell rett
    /// etter at #311 opprettet tabellen. Aldri når <paramref name="tilMigrasjon"/> er null (siste versjon har dem).</param>
    public static async Task MigrerAsync(string connString, string? tilMigrasjon, bool leggTilSenereKolonner = true)
    {
        await using (var db = new RegelIdeDbContext(Options(connString)))
        {
            await db.GetService<IMigrator>().MigrateAsync(tilMigrasjon);
        }
        if (tilMigrasjon is not null && leggTilSenereKolonner) await LeggTilSenereKolonnerAsync(connString);
        // [Ny, issue #395] rettskilder.kildeform og strukturkanter.objekt_eli (Organiseringskilder) legges til i BEGGE modusene: testene
        // importerer rettskilder med dagens modell (RettskildeImportTjeneste) også når de migrerer videre etterpå, og migrasjonen bruker
        // ADD COLUMN IF NOT EXISTS, så den kolliderer ikke.
        if (tilMigrasjon is not null)
        {
            await using var db = new RegelIdeDbContext(Options(connString));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS kildeform text; "
                + "ALTER TABLE IF EXISTS strukturkanter ADD COLUMN IF NOT EXISTS objekt_eli text; "
                // NedlagteAktorerOgEndringsmarkor (samme IF NOT EXISTS-mønster).
                + "ALTER TABLE virksomheter ADD COLUMN IF NOT EXISTS livsstatus text; "
                + "ALTER TABLE virksomheter ADD COLUMN IF NOT EXISTS gyldig_fra date; "
                + "ALTER TABLE virksomheter ADD COLUMN IF NOT EXISTS gyldig_til date; "
                + "ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS rent_endringsvedtak boolean; "
                + "ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS rent_endringsvedtak_grunnlag text;");
        }
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
