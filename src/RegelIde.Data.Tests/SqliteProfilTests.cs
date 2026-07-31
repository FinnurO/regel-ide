using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// SQLite-profilen (<c>RegelIde:Database=sqlite</c>) — deploy-profilen for app-cluster uten root
/// eller persistent volum. Kjører mot en ekte SQLite-fil, ikke Postgres, og trenger derfor ikke
/// den delte embedded-Postgres-fixturen.
/// <para>
/// Poenget med disse testene er å fange opp der de to motorene faktisk oppfører seg ulikt —
/// <c>jsonb</c> mot TEXT, <c>text[]</c> mot JSON-kolonne, <c>now()</c> som ikke finnes, og
/// <see cref="DateTimeOffset"/> som SQLite ikke kan sortere på uten konvertering.
/// </para>
/// </summary>
public sealed class SqliteProfilTests : IAsyncLifetime
{
    private string _filsti = "";

    public Task InitializeAsync()
    {
        _filsti = Path.Combine(Path.GetTempPath(), $"regelide-sqlitetest-{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_filsti)) File.Delete(_filsti);
        return Task.CompletedTask;
    }

    private RegelIdeDbContext NyDbContext() =>
        new(new DbContextOptionsBuilder<RegelIdeDbContext>().UseSqlite($"Data Source={_filsti}").Options);

    private async Task<RegelIdeDbContext> NyBaseAsync()
    {
        var db = NyDbContext();
        await Databaseoppsett.SorgForSkjemaAsync(db);
        return db;
    }

    private static Virksomhet NyVirksomhet() => new()
    {
        Id = Guid.NewGuid(), Navn = "Testkommunen", OpprettetTidspunkt = DateTimeOffset.UtcNow,
    };

    /* ------------------------------- profilvalg ------------------------------- */

    [Theory]
    [InlineData("sqlite", Databaseprofil.Sqlite)]
    [InlineData("postgres", Databaseprofil.Postgres)]
    [InlineData("postgresql", Databaseprofil.Postgres)]
    [InlineData("SQLite", Databaseprofil.Sqlite)]
    [InlineData(null, Databaseprofil.Postgres)]
    public void Leser_profil_fra_konfigurasjon(string? verdi, Databaseprofil forventet)
    {
        var konfigurasjon = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Databaseoppsett.Konfigurasjonsnokkel] = verdi })
            .Build();

        Assert.Equal(forventet, Databaseoppsett.LesProfil(konfigurasjon));
    }

    [Fact]
    public void Ukjent_profil_feiler_hoylytt_i_stedet_for_a_falle_tilbake()
    {
        var konfigurasjon = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Databaseoppsett.Konfigurasjonsnokkel] = "mysql" })
            .Build();

        var feil = Assert.Throws<InvalidOperationException>(() => { Databaseoppsett.LesProfil(konfigurasjon); });
        Assert.Contains("postgres | sqlite", feil.Message);
    }

    /* -------------------------------- skjemaet -------------------------------- */

    [Fact]
    public async Task Skjemaet_bygges_med_alle_tabeller()
    {
        await using var db = await NyBaseAsync();

        var tabeller = await db.Database
            .SqlQueryRaw<string>("select name from sqlite_master where type = 'table' and name not like 'sqlite_%'")
            .ToListAsync();

        // Ett navn per DbSet i konteksten.
        Assert.Contains("virksomheter", tabeller);
        Assert.Contains("rettskilder", tabeller);
        Assert.Contains("rettskilde_noder", tabeller);
        Assert.Contains("vilkar", tabeller);
        Assert.Contains("regelnoder", tabeller);
        Assert.Contains("proveniens", tabeller);
        Assert.Contains("kodelister", tabeller);
        Assert.True(tabeller.Count >= 20, $"Forventet minst 20 tabeller, fant {tabeller.Count}.");
    }

    [Fact]
    public async Task Partial_unique_index_opprettes_og_handheves()
    {
        await using var db = await NyBaseAsync();

        var indekser = await db.Database
            .SqlQueryRaw<string>("select sql from sqlite_master where type = 'index' and sql is not null")
            .ToListAsync();

        // WHERE-klausulen er det som gjør indeksen partial — den må ha overlevd til SQLite.
        Assert.Contains(indekser, i => i.Contains("ux_virksomheter_organisasjonsnummer") && i.Contains("WHERE"));

        db.Virksomheter.Add(new Virksomhet
        {
            Id = Guid.NewGuid(), Navn = "A", Organisasjonsnummer = "123456789", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        db.Virksomheter.Add(new Virksomhet
        {
            Id = Guid.NewGuid(), Navn = "B", Organisasjonsnummer = "123456789", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Check_constraint_handheves()
    {
        await using var db = await NyBaseAsync();

        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Tittel = "Ugyldig importrolle", Kildetype = "Lov", Doctype = "act",
            Status = "Gjeldende", OpprettetAv = "Kari Jurist",
            Importrolle = "tullball", // check-constraint tillater kun 'primaer' | 'referanse'
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /* ------------------- de faktiske forskjellene mellom motorene ------------------- */

    [Fact]
    public async Task DateTimeOffset_kan_sorteres_og_sammenlignes_i_databasen()
    {
        await using var db = await NyBaseAsync();
        var virksomhet = NyVirksomhet();
        db.Virksomheter.Add(virksomhet);

        // Bevisst blandede offsets: uten UtcTicks-konverteringen sorterer dette på lokal
        // veggklokke i stedet for faktisk tidspunkt. Kronologisk: eldst -> nyest er A, B, C.
        var a = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);            // 10:00 UTC
        var b = new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.FromHours(2));    // 12:00 UTC
        var c = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5));    // 14:00 UTC
        db.Proveniens.AddRange(
            ProveniensMed(virksomhet.Id, "A", a),
            ProveniensMed(virksomhet.Id, "B", b),
            ProveniensMed(virksomhet.Id, "C", c));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Samme mønster som proveniens-/historikk-endepunktene i Program.cs.
        var sortert = await db.Proveniens.OrderByDescending(p => p.Dato).Select(p => p.Handling).ToListAsync();
        Assert.Equal(["C", "B", "A"], sortert);

        var grense = new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero);
        var etterGrensen = await db.Proveniens.Where(p => p.Dato > grense).Select(p => p.Handling).OrderBy(h => h).ToListAsync();
        Assert.Equal(["B", "C"], etterGrensen);
    }

    [Fact]
    public async Task DateTimeOffset_leses_tilbake_som_samme_tidspunkt()
    {
        await using var db = await NyBaseAsync();
        var virksomhet = NyVirksomhet();
        db.Virksomheter.Add(virksomhet);
        var tidspunkt = new DateTimeOffset(2026, 7, 30, 12, 34, 56, TimeSpan.FromHours(2));
        db.Proveniens.Add(ProveniensMed(virksomhet.Id, "opprettet", tidspunkt));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var lest = await db.Proveniens.SingleAsync();

        // Offset går tapt (alt leses som UTC), men tidspunktet er det samme.
        Assert.Equal(tidspunkt.UtcDateTime, lest.Dato.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, lest.Dato.Offset);
    }

    [Fact]
    public async Task Liste_av_strenger_lagres_og_leses_tilbake()
    {
        await using var db = await NyBaseAsync();
        var virksomhet = NyVirksomhet();
        db.Virksomheter.Add(virksomhet);
        var tjeneste = new TjenesteEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhet.Id, Tittel = "Skjenkebevilling", Status = "utkast",
            Kanaler = ["digital", "papir"],
            Sprak = ["nb", "nn"],
            OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Tjenester.Add(tjeneste);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // På Postgres er dette text[]; SQLite har ingen array-type, så EF mapper det til en
        // JSON-kolonne. Det er den ene antakelsen i profilen som måtte verifiseres kjørende.
        var lest = await db.Tjenester.SingleAsync(t => t.Id == tjeneste.Id);
        Assert.Equal(["digital", "papir"], lest.Kanaler);
        Assert.Equal(["nb", "nn"], lest.Sprak);
    }

    [Fact]
    public async Task Json_kolonne_lagres_som_tekst_og_beholder_innholdet()
    {
        await using var db = await NyBaseAsync();
        var virksomhet = NyVirksomhet();
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);
        var vilkar = await register.OpprettAsync(virksomhet.Id, "Aldersvilkår", null, null, "materiell", null,
            [new JuridiskGrunnlagInput("alkoholloven", "§1-5")], null, "regelbasert", """{"aldersgrense":18}""",
            null, null, false, null, null, null, false, null, null, "Kari Jurist");
        db.ChangeTracker.Clear();

        var lest = await db.Vilkar.SingleAsync(v => v.Id == vilkar.Id);
        using var parametre = JsonDocument.Parse(lest.ParametreJson);
        Assert.Equal(18, parametre.RootElement.GetProperty("aldersgrense").GetInt32());
        Assert.Contains("§1-5", lest.JuridiskGrunnlagJson);
    }

    [Fact]
    public async Task Ugyldig_json_avvises_ogsa_her_selv_om_SQLite_ikke_validerer()
    {
        await using var db = await NyBaseAsync();
        var virksomhet = NyVirksomhet();
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = new VilkarregisterTjeneste(db);

        // Postgres' jsonb ville avvist dette selv. SQLite lagrer TEXT og validerer ingenting,
        // så applikasjonsvalideringen er eneste vern mot at søppel blir liggende.
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet.Id, "Aldersvilkår", null, null, "materiell", null, null, null, "regelbasert",
            "{ugyldig}", null, null, false, null, null, null, false, null, null, "Kari Jurist"));
    }

    /* --------------------------- ekte innhold ende-til-ende --------------------------- */

    [Fact]
    public async Task Importerer_ekte_lovdatadokument()
    {
        await using var db = await NyBaseAsync();
        var fil = Path.Combine(AppContext.BaseDirectory, "Testdata", "alkoholloven-LOV-1989-06-02-27.html");
        var resultat = LovdataKonverterer.Konverter(await File.ReadAllTextAsync(fil));

        var importert = await new RettskildeImportTjeneste(db).ImporterAsync(resultat);

        Assert.NotEqual(Guid.Empty, importert);
        Assert.True(await db.RettskildeNoder.CountAsync() > 100);
    }

    [Fact]
    public async Task Alle_seedene_kjorer_gjennom()
    {
        await using var db = await NyBaseAsync();
        var fil = Path.Combine(AppContext.BaseDirectory, "Testdata", "alkoholloven-LOV-1989-06-02-27.html");
        await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(await File.ReadAllTextAsync(fil)));

        // Seedene forutsetter oppsett som gjøres i Program.cs, ikke av seed-klassene selv:
        // Testkommunen (fra bruker-seedingen) og tagg-kind-konfigurasjonen. Uten den første blir
        // alt en stille no-op; uten den andre kaster Byggesteg4 på ukjent tag-type.
        db.Virksomheter.Add(new Virksomhet
        {
            Id = Guid.NewGuid(), Navn = "Testkommunen", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.TaggKindKonfigurasjoner.AddRange(
            new TaggKindKonfigurasjonEntitet { Id = Guid.NewGuid(), Kode = "begrep", Navn = "Begrep", Farge = "accent", Sorteringsrekkefolge = 0 },
            new TaggKindKonfigurasjonEntitet { Id = Guid.NewGuid(), Kode = "tjeneste", Navn = "Tjeneste", Farge = "info", Sorteringsrekkefolge = 1 },
            new TaggKindKonfigurasjonEntitet { Id = Guid.NewGuid(), Kode = "vilkar", Navn = "Vilkår", Farge = "warning", Sorteringsrekkefolge = 2 },
            new TaggKindKonfigurasjonEntitet { Id = Guid.NewGuid(), Kode = "regel", Navn = "Regel", Farge = "success", Sorteringsrekkefolge = 3 });
        await db.SaveChangesAsync();

        // Rekkefølgen er den samme som i Program.cs' oppstartsseeding.
        await TestkommuneInnholdSeed.SeedAsync(db);
        await Byggesteg2InnholdSeed.SeedAsync(db);
        await Byggesteg4VilkarstreSeed.SeedAsync(db);

        Assert.True(await db.Tjenester.AnyAsync());
        Assert.True(await db.Begreper.AnyAsync());
        Assert.True(await db.Vilkar.AnyAsync());
        Assert.True(await db.Regelnoder.AnyAsync(r => r.ErRotnode));
    }

    private static ProveniensEntitet ProveniensMed(Guid virksomhetId, string handling, DateTimeOffset dato) => new()
    {
        Id = Guid.NewGuid(),
        EntitetType = "vilkar",
        EntitetId = Guid.NewGuid(),
        VirksomhetId = virksomhetId,
        Handling = handling,
        EndretAv = "Kari Jurist",
        Dato = dato,
    };
}
