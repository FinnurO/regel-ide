using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #368 «oppgave/funksjon (kategori F)», 2026-10-15] Migrasjonen <c>OppgaveFunksjon</c>, kjørt for ekte: en fersk database
/// migreres til #313 (<see cref="HistoriskSkjema.AvvistSomStatus"/>), testen legger inn R sekretariat_for-kanter med rå SQL slik de lå
/// lokalt, migrerer til siste versjon — og tilbake.
/// <para>
/// Spørsmålene (sakens AC3): finnes de åtte F-typene etterpå? Flytter migrasjonen BARE det juristrunden avgjorde (Johann: «ingenting
/// flyttes automatisk»)? Står R-kanten (utfall «begge»), og er den nye F-kanten et forslag med kunnskapsstatus, ikke godkjent? Står en
/// sekretariat_for-kant runden ikke vurderte urørt? Er tallene like før og etter, og snur <c>Down</c> nøyaktig — og nekter den når det
/// finnes en F-kant registrert etter migrasjonen?
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class OppgaveMigreringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public OppgaveMigreringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task SekretariatAsync(RegelIdeDbContext db, Guid id, Guid fra, Guid til, Guid hjemmel, string eid) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, til_virksomhet_id,
                hjemmel_rettskilde_id, hjemmel_eid, status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
            VALUES ({id}, 'R', 'sekretariat_for', {fra}, {til}, {hjemmel}, {eid}, 'godkjent', 'manuell', 'Anne Systemforvalter', now())
            """);

    [Fact]
    public async Task F_typene_legges_inn_bare_juristrundens_begge_gir_ny_F_kant_og_Down_snur()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_op368", HistoriskSkjema.AvvistSomStatus, leggTilSenereKolonner: false);
        var (begge, behold, uvurdert) = (OppgaveMigrering.Beslutninger.First(b => b.Utfall == "begge").KantId,
            OppgaveMigrering.Beslutninger.First(b => b.Utfall == "behold").KantId, Guid.NewGuid());
        Guid kns, nemnd, annen;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
                VALUES (gen_random_uuid(), 'R', 'sekretariat_for', 'er sekretariat for {{0}}', 'har sekretariat hos {{0}}', 10, true)
                ON CONFLICT (kategori, kode) DO NOTHING;
                """);
            var lov = await new RettskildeImportTjeneste(db).ImporterAsync(
                LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
            var eid = (await db.RettskildeNoder.Where(n => n.RettskildeId == lov && n.NodeType == "paragraf")
                .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync()).Eid;
            (kns, nemnd, annen) = (await HistoriskSkjema.VirksomhetAsync(db, "KNS"), await HistoriskSkjema.VirksomhetAsync(db, "Nemnda"),
                await HistoriskSkjema.VirksomhetAsync(db, "Annen nemnd"));
            await SekretariatAsync(db, begge, kns, nemnd, lov, eid);
            await SekretariatAsync(db, behold, kns, annen, lov, eid);
            await SekretariatAsync(db, uvurdert, annen, nemnd, lov, eid);
        }

        await HistoriskSkjema.MigrerAsync(conn, null);

        await using (var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(4, await etter.Strukturkanter.CountAsync()); // 3 + én F fra «begge»
            Assert.Equal(3, await etter.Strukturkanter.CountAsync(k => k.Kategori == "R" && k.Typekode == "sekretariat_for"));
            var f = await etter.Strukturkanter.AsNoTracking().SingleAsync(k => k.Kategori == Strukturkanter.Oppgave);
            Assert.Equal(("sekretariatsfunksjon", kns, nemnd, "skal"), (f.Typekode, f.FraVirksomhetId!.Value, f.TilVirksomhetId!.Value, f.Modalitet));
            Assert.Equal((Strukturkanter.MaskineltForeslatt, OppgaveMigrering.Oppdagelseskilde, Strukturkanter.Avledet),
                (f.Status, f.OppdagelsesKilde, f.Kunnskapsstatus));
            Assert.Equal("saksforberedelse", f.Objekt);
            // Avledet: premisset er R-kanten selv (KNS som sekretariat, kilde utenfor korpus).
            Assert.Equal(begge, await etter.Database.SqlQuery<Guid>($"SELECT premiss_kant_id AS \"Value\" FROM strukturkant_premisser WHERE kant_id = {f.Id}").SingleAsync());
            Assert.False(string.IsNullOrWhiteSpace(f.Begrunnelse));
            // Ingen eksisterende kant er endret.
            Assert.False(await etter.Strukturkanter.AnyAsync(k => k.Kategori == "R" && k.SistEndretAv != null));
            Assert.Equal(1, await etter.Proveniens.CountAsync(p => p.EndretAv == OppgaveMigrering.EndretAv && p.Handling == "opprettet"));
            Assert.Equal(Strukturkanter.Oppgavetyper.Count,
                await etter.RelasjonsTypeKonfigurasjoner.CountAsync(t => t.Kategori == Strukturkanter.Oppgave));
            var startsett = Strukturkanter.Startsett.Single(t => t.Kategori == Strukturkanter.Oppgave && t.Kode == "sorge_for");
            var rad = await etter.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == Strukturkanter.Oppgave && t.Kode == "sorge_for");
            Assert.Equal((startsett.FraMal, startsett.TilMal), (rad.FraVisningsmal, rad.TilVisningsmal));
        }

        // ---- Down: tilbake til #313 ----
        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.AvvistSomStatus, leggTilSenereKolonner: false);
        await using (var tilbake = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(3, await HistoriskSkjema.TellAsync(tilbake, $"SELECT count(*)::int AS \"Value\" FROM strukturkanter"));
            Assert.Equal(0, await HistoriskSkjema.TellAsync(tilbake, $"SELECT count(*)::int AS \"Value\" FROM relasjonstype_konfigurasjon WHERE kategori = 'F'"));
            Assert.Equal(0, await HistoriskSkjema.TellAsync(tilbake, $"SELECT count(*)::int AS \"Value\" FROM proveniens WHERE endret_av = 'migrasjon-368'"));
            Assert.Equal(0, await HistoriskSkjema.TellAsync(tilbake,
                $"SELECT count(*)::int AS \"Value\" FROM pg_constraint WHERE conname = 'ck_strukturkanter_oppgave_objekt'"));
        }
    }

    [Fact]
    public async Task Down_nekter_naar_det_finnes_en_oppgave_registrert_etter_migrasjonen()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_op368b", null);
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            var lov = await new RettskildeImportTjeneste(db).ImporterAsync(
                LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
            var v = new Virksomhet { Id = Guid.NewGuid(), Navn = "Beredskapsmyndigheten" };
            db.Virksomheter.Add(v);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, objekt, hjemmel_rettskilde_id,
                    status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
                VALUES ({Guid.NewGuid()}, 'F', 'samordning', {v.Id}, 'beredskapsarbeid', {lov}, 'godkjent', 'manuell', 'test', now())
                """);
        }
        var feil = await Assert.ThrowsAnyAsync<Exception>(() => HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.AvvistSomStatus,
            leggTilSenereKolonner: false));
        Assert.Contains("Issue #368 Down", feil.Message);
    }
}
