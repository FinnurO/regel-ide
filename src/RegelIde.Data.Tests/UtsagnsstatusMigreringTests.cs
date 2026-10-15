using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #367 «utsagnsstatus» + #335] Migrasjonen <c>UtsagnsstatusOgDelegeringsadgang</c>, kjørt for ekte: en fersk database migreres
/// til #355 (<see cref="HistoriskSkjema.AvslutningSpeilerInnsetting"/>), testen legger inn kanter, navneformer og tagger slik de kunne
/// ligge før #367, migrerer til siste versjon — og tilbake.
/// <para>
/// Spørsmålene (sakens AC2, AC4, AC5, AC9): Blir godkjenningen mappet én-til-én? Får ingen rad en gjettet kunnskapsstatus? Får kantene
/// den versjonerte hjemmelnoden? Blir delegerbar mappet på hjemmelens TEKST («Kongen i statsråd» → slutning, ikke tekstfunn), med
/// proveniens per rad, og står en rad som ikke kan avgjøres uten verdi? Blir taggene med teksten «Kongen» flyttet til en egen navneform?
/// Snur Down alt nøyaktig — og nekter den når noe ikke kan uttrykkes før #367?
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class UtsagnsstatusMigreringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public UtsagnsstatusMigreringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record Kantrad(Guid Id, string Status, bool? Delegerbar);

    private static async Task<Guid> KantAsync(RegelIdeDbContext db, Guid fra, Guid hjemmel, string eid, string status, bool? delegerbar)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, objekt, hjemmel_rettskilde_id, hjemmel_eid, delegerbar,
                status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
            VALUES ({id}, 'K', 'normgivning', {fra}, 'x', {hjemmel}, {eid}, {delegerbar}, {status}, 'manuell', 'test', now())
            """);
        return id;
    }

    private static async Task<Guid> TaggAsync(RegelIdeDbContext db, Guid rettskilde, string eid, string tekst, Guid navneform, int start, Guid virksomhet)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tekst_tagger ("Id", rettskilde_id, node_eid, start_offset, end_offset, quote_prefix, quote_exact, quote_suffix,
                node_tekst_hash, kind, ref_id, opprettet_av, virksomhet_id)
            VALUES ({id}, {rettskilde}, {eid}, {start}, {start + tekst.Length}, '', {tekst}, '', 'h', 'virksomhet', {navneform}, 'test', {virksomhet})
            """);
        return id;
    }

    [Fact]
    public async Task Godkjenning_hjemmelnode_delegering_og_Kongen_mappes_og_Down_snur_noyaktig()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_us367", HistoriskSkjema.AvslutningSpeilerInnsetting,
            leggTilSenereKolonner: false);
        Guid lov, kis, kisForm, kIs, kKongen, kUavklart, kForslag, tKongen, tKis;
        string[] ledd;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            lov = await new RettskildeImportTjeneste(db).ImporterAsync(
                LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
            ledd = await db.RettskildeNoder.Where(n => n.RettskildeId == lov && n.NodeType == "ledd")
                .OrderBy(n => n.Sorteringsrekkefolge).Take(4).Select(n => n.Eid).ToArrayAsync();
            // Kontrollert tekst i hjemmelnodene — mappingen avgjøres på teksten, ikke på bool-verdien alene.
            foreach (var (e, tekst) in new[] { (ledd[0], "Kongen i statsråd kan gi forskrift om dette."), (ledd[1], "Kongen kan gi forskrift om dette."),
                         (ledd[2], "Departementet kan gi forskrift om dette.") })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE rettskilde_noder SET tekst = {tekst} WHERE rettskilde_id = {lov} AND eid = {e}");
            }
            kis = await HistoriskSkjema.VirksomhetAsync(db, "Kongen i statsråd");
            kisForm = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO begreper ("Id", term, gjelder_for, status, opprettet_av, begrepskategori, virksomhet_referanse_id)
                VALUES ({kisForm}, 'Kongen i statsråd', ARRAY[]::text[], 'publisert', 'test', 'virksomhet', {kis})
                """);
            kIs = await KantAsync(db, kis, lov, ledd[0], "validert", false);
            kKongen = await KantAsync(db, kis, lov, ledd[1], "validert", true);
            kUavklart = await KantAsync(db, kis, lov, ledd[2], "validert", true);
            kForslag = await KantAsync(db, kis, lov, ledd[3], "foreslatt_av_ai", null);
            tKongen = await TaggAsync(db, lov, ledd[1], "Kongen", kisForm, 0, kis);
            tKis = await TaggAsync(db, lov, ledd[0], "Kongen i statsråd", kisForm, 0, kis);
        }

        await HistoriskSkjema.MigrerAsync(conn, null);

        Guid kongenForm;
        await using (var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            var kanter = await etter.Strukturkanter.AsNoTracking().ToDictionaryAsync(k => k.Id);
            Assert.Equal(4, kanter.Count);
            Assert.Equal(("godkjent", "maskinelt_foreslatt"), (kanter[kIs].Status, kanter[kForslag].Status));
            Assert.All(kanter.Values, k => Assert.Null(k.Kunnskapsstatus)); // ingen gjettet kunnskapsstatus
            Assert.All(kanter.Values, k => Assert.NotNull(k.HjemmelNodeId));
            Assert.Equal(("ikke_regulert", "trolig_ikke_delegerbar"), (kanter[kIs].Delegeringsadgang, kanter[kIs].DelegeringSlutning));
            Assert.Equal(("ikke_regulert", "trolig_delegerbar"), (kanter[kKongen].Delegeringsadgang, kanter[kKongen].DelegeringSlutning));
            Assert.Equal(((string?)null, (string?)null), (kanter[kUavklart].Delegeringsadgang, kanter[kUavklart].DelegeringSlutning)); // ikke avgjort
            Assert.Equal(3, await etter.Proveniens.CountAsync(p => p.EndretAv == UtsagnsstatusMigrering.EndretAv && p.EntitetType == "strukturkant"));
            var prov = await etter.Proveniens.AsNoTracking().SingleAsync(p => p.EntitetId == kIs && p.EndretAv == UtsagnsstatusMigrering.EndretAv);
            using (var j = JsonDocument.Parse(prov.KildeReferanserJson!))
            {
                Assert.False(j.RootElement.GetProperty("delegerbar").GetBoolean());
            }

            var kongen = await etter.Begreper.AsNoTracking().SingleAsync(b => b.Term == "Kongen" && b.VirksomhetReferanseId == kis);
            kongenForm = kongen.Id;
            Assert.Equal(kongen.Id, (await etter.TekstTagger.AsNoTracking().SingleAsync(t => t.Id == tKongen)).RefId);
            Assert.Equal(kisForm, (await etter.TekstTagger.AsNoTracking().SingleAsync(t => t.Id == tKis)).RefId);
        }

        // ---- Down nekter så lenge det finnes en kunnskapsstatus (kan ikke uttrykkes før #367) ----
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE strukturkanter SET kunnskapsstatus = 'tekstfunn' WHERE \"Id\" = {kIs}");
        }
        var feil = await Assert.ThrowsAnyAsync<Exception>(() => HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.AvslutningSpeilerInnsetting,
            leggTilSenereKolonner: false));
        Assert.Contains("Issue #367 Down", feil.Message);
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE strukturkanter SET kunnskapsstatus = NULL WHERE \"Id\" = {kIs}");
        }

        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.AvslutningSpeilerInnsetting, leggTilSenereKolonner: false);

        await using (var tilbake = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            var rader = await tilbake.Database.SqlQuery<Kantrad>($"SELECT \"Id\", status AS \"Status\", delegerbar AS \"Delegerbar\" FROM strukturkanter").ToListAsync();
            var forventet = new[]
            {
                new Kantrad(kIs, "validert", false), new Kantrad(kKongen, "validert", true), new Kantrad(kUavklart, "validert", true),
                new Kantrad(kForslag, "foreslatt_av_ai", null),
            };
            Assert.Equal(forventet.OrderBy(k => k.Id), rader.OrderBy(k => k.Id));
            Assert.Equal(0, await HistoriskSkjema.TellAsync(tilbake, $"SELECT count(*)::int AS \"Value\" FROM begreper WHERE \"Id\" = {kongenForm}"));
            Assert.Equal(2, await HistoriskSkjema.TellAsync(tilbake,
                $"SELECT count(*)::int AS \"Value\" FROM tekst_tagger WHERE ref_id = {kisForm}"));
            Assert.Equal(0, await HistoriskSkjema.TellAsync(tilbake,
                $"SELECT count(*)::int AS \"Value\" FROM proveniens WHERE endret_av = {UtsagnsstatusMigrering.EndretAv}"));
        }
    }
}
