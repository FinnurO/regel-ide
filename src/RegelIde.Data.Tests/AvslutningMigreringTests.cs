using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #355 «avslutning speiler innsetting», 2026-10-09] Migrasjonen <c>AvslutningSpeilerInnsetting</c>, kjørt for ekte: en
/// fersk database migreres til #353 (<see cref="HistoriskSkjema.PliktOgOrdning"/>), testen legger inn kanter slik de kunne ligge før
/// #355, migrerer til siste versjon — og tilbake.
/// <para>
/// Spørsmålene (sakens AC1): Er K forelegging ute av typekonfigurasjonen etterpå, med én proveniensrad som Down legger inn igjen
/// NØYAKTIG (samme Id)? Er tallene like før og etter, og står andre kanter (med undertype fra #352) urørt? Godtar databasen de nye
/// undertypene etterpå? Nekter Up når det finnes K forelegging-kanter (rettsvirkningen kan ikke avgjøres uten teksten), og Down når
/// det finnes en undertype som ikke kan uttrykkes før #355?
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AvslutningMigreringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AvslutningMigreringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<(Guid Lov, string Eid)> LovAsync(RegelIdeDbContext db)
    {
        var lov = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var eid = (await db.RettskildeNoder.Where(n => n.RettskildeId == lov && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync()).Eid;
        return (lov, eid);
    }

    private static async Task<Guid> KantAsync(RegelIdeDbContext db, string kategori, string kode, Guid fraV, Guid tilV, Guid hjemmel,
        string hjemmelEid, string? undertype = null)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, til_virksomhet_id,
                hjemmel_rettskilde_id, hjemmel_eid, undertype, status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
            VALUES ({id}, {kategori}, {kode}, {fraV}, {tilV}, {hjemmel}, {hjemmelEid}, {undertype}, 'validert', 'manuell', 'test', now())
            """);
        return id;
    }

    [Fact]
    public async Task Forelegging_ut_av_konfigurasjonen_med_proveniens_nye_undertyper_lov_og_Down_snur_noyaktig()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_av355", HistoriskSkjema.PliktOgOrdning,
            leggTilSenereKolonner: false);
        Guid kommunestyret, forliksradet, lov, valg, foreleggingId;
        string eid;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            (lov, eid) = await LovAsync(db);
            (kommunestyret, forliksradet) = (await HistoriskSkjema.VirksomhetAsync(db, "Kommunestyret"), await HistoriskSkjema.VirksomhetAsync(db, "Forliksrådet"));
            valg = await KantAsync(db, "K", "oppnevning", kommunestyret, forliksradet, lov, eid, "valg");
            foreleggingId = await HistoriskSkjema.GuidAsync(db,
                $"SELECT \"Id\" AS \"Value\" FROM relasjonstype_konfigurasjon WHERE kategori = 'K' AND kode = 'forelegging'");
            // Før #355 avviser databasen de nye undertypene.
            await Assert.ThrowsAnyAsync<Exception>(() => KantAsync(db, "K", "avsetting", kommunestyret, forliksradet, lov, eid, "avskjed"));
        }

        await HistoriskSkjema.MigrerAsync(conn, null);

        await using (var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(1, await etter.Strukturkanter.CountAsync()); // ingenting tapt, ingenting nytt
            var k = await etter.Strukturkanter.AsNoTracking().SingleAsync(x => x.Id == valg);
            Assert.Equal(("K", "oppnevning", "valg"), (k.Kategori, k.Typekode, k.Undertype));
            Assert.Null(k.SistEndretAv);

            Assert.False(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == "K" && t.Kode == "forelegging"));
            var prov = Assert.Single(await etter.Proveniens.AsNoTracking().Where(p => p.EndretAv == AvslutningMigrering.EndretAv).ToListAsync());
            Assert.Equal(("slettet", foreleggingId), (prov.Handling, prov.EntitetId));
            using (var j = JsonDocument.Parse(prov.KildeReferanserJson!))
            {
                Assert.Equal(("K", "forelegging", "kontroll"), (j.RootElement.GetProperty("kategori").GetString(),
                    j.RootElement.GetProperty("kode").GetString(), j.RootElement.GetProperty("familie").GetString()));
            }

            // De nye undertypene er lov etterpå (CHECK-en), en undertype typen ikke har er det ikke.
            await KantAsync(etter, "K", "avsetting", kommunestyret, forliksradet, lov, eid, "avskjed");
            await KantAsync(etter, "K", "oppnevning", kommunestyret, forliksradet, lov, eid, "utnevning");
            await Assert.ThrowsAnyAsync<Exception>(() => KantAsync(etter, "K", "avsetting", kommunestyret, forliksradet, lov, eid, "anke"));
        }

        // ---- Down nekter så lenge undertypene fra #355 finnes ----
        var feil = await Assert.ThrowsAnyAsync<Exception>(() => HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.PliktOgOrdning,
            leggTilSenereKolonner: false));
        Assert.Contains("Issue #355 Down", feil.Message);

        await using (var rydd = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            await rydd.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM strukturkanter WHERE \"Id\" <> {valg}");
        }
        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.PliktOgOrdning, leggTilSenereKolonner: false);

        await using (var tilbake = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            var rad = await tilbake.RelasjonsTypeKonfigurasjoner.AsNoTracking().SingleAsync(t => t.Kategori == "K" && t.Kode == "forelegging");
            Assert.Equal((foreleggingId, "kontroll"), (rad.Id, rad.Familie)); // nøyaktig den raden, med sin egen Id
            Assert.False(await tilbake.Proveniens.AnyAsync(p => p.EndretAv == AvslutningMigrering.EndretAv));
            Assert.Equal("valg", (await tilbake.Strukturkanter.AsNoTracking().SingleAsync(x => x.Id == valg)).Undertype);
            await Assert.ThrowsAnyAsync<Exception>(() => KantAsync(tilbake, "K", "avsetting", kommunestyret, forliksradet, lov, eid, "avskjed"));
        }
    }

    [Fact]
    public async Task Up_nekter_naar_det_finnes_K_forelegging_kanter()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_av355b", HistoriskSkjema.PliktOgOrdning,
            leggTilSenereKolonner: false);
        Guid forelegging;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            var (lov, eid) = await LovAsync(db);
            forelegging = await KantAsync(db, "K", "forelegging", await HistoriskSkjema.VirksomhetAsync(db, "Domstolen"),
                await HistoriskSkjema.VirksomhetAsync(db, "EFTA-domstolen"), lov, eid);
        }
        var feil = await Assert.ThrowsAnyAsync<Exception>(() => HistoriskSkjema.MigrerAsync(conn, null));
        Assert.Contains("Issue #355", feil.Message);
        Assert.Contains(forelegging.ToString(), feil.Message);
    }
}
