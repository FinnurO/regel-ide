using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #353 «pliktrelasjoner mellom parter (kategori P) og nodetypen ordning», 2026-10-09] Migrasjonen
/// <c>PliktOgOrdning</c>, kjørt for ekte: en fersk database migreres til #352
/// (<see cref="HistoriskSkjema.OppnevningsfamilienOgRester"/>), testen legger inn kanter med rå SQL slik de kunne ligge før #353,
/// migrerer til siste versjon — og tilbake.
/// <para>
/// Spørsmålene (sakens AC1/AC2): finnes P med de seks typene, R forvaltes_av og G tilhorer etterpå? Blir en håndlagt R
/// samarbeider_med/bistar P samarbeid/bistand i SAMME retning, uten gjettet modalitet? Står andre R-kanter urørt? Er tallene like
/// før og etter, og snur <c>Down</c> nøyaktig — og nekter den når det finnes opplysninger som ikke kan uttrykkes før #353?
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class PliktMigreringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public PliktMigreringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>En kant slik den lå før #353 — rå SQL, fordi skjemaet ennå ikke har <c>modalitet</c>.</summary>
    private static async Task<Guid> KantAsync(RegelIdeDbContext db, string kategori, string kode, Guid fraV, Guid tilV, Guid hjemmel,
        string hjemmelEid, string? avgrensningTekst = null)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, til_virksomhet_id,
                hjemmel_rettskilde_id, hjemmel_eid, avgrensning_tekst, status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
            VALUES ({id}, {kategori}, {kode}, {fraV}, {tilV}, {hjemmel}, {hjemmelEid}, {avgrensningTekst},
                'validert', 'manuell', 'test', now())
            """);
        return id;
    }

    private sealed record KantRad(string Kategori, string Typekode, Guid? Fra, Guid? Til, string? HjemmelEid, string? Avgrensning);

    private static async Task<KantRad> LesAsync(RegelIdeDbContext db, Guid id)
    {
        var k = await db.Strukturkanter.AsNoTracking().SingleAsync(x => x.Id == id);
        return new KantRad(k.Kategori, k.Typekode, k.FraVirksomhetId, k.TilVirksomhetId, k.HjemmelEid, k.AvgrensningTekst);
    }

    [Fact]
    public async Task Plikttypene_legges_inn_handlagde_R_koder_blir_P_og_Down_snur_bare_de_radene()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_pl353", HistoriskSkjema.OppnevningsfamilienOgRester,
            leggTilSenereKolonner: false);
        Guid kommunen, rhf, politiet, nemnd, lov;
        string eid;
        Guid samarbeid, bistand, sekretariat;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            // To R-koder som aldri har stått i startsettet, men som et miljø kan ha lagt inn for hånd (fasitens relasjonstyper).
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
                VALUES (gen_random_uuid(), 'R', 'samarbeider_med', 'samarbeider med {{0}}', 'samarbeider med {{0}}', 90, true),
                       (gen_random_uuid(), 'R', 'bistar', 'bistår {{0}}', 'får bistand fra {{0}}', 91, true),
                       (gen_random_uuid(), 'R', 'sekretariat_for', 'er sekretariat for {{0}}', 'har sekretariat hos {{0}}', 10, true)
                ON CONFLICT (kategori, kode) DO NOTHING;
                """);
            lov = await new RettskildeImportTjeneste(db).ImporterAsync(
                LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
            eid = (await db.RettskildeNoder.Where(n => n.RettskildeId == lov && n.NodeType == "paragraf")
                .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync()).Eid;
            (kommunen, rhf) = (await HistoriskSkjema.VirksomhetAsync(db, "Kommunen"), await HistoriskSkjema.VirksomhetAsync(db, "Helse Nord RHF"));
            (politiet, nemnd) = (await HistoriskSkjema.VirksomhetAsync(db, "Politiet"), await HistoriskSkjema.VirksomhetAsync(db, "Nemnda"));

            samarbeid = await KantAsync(db, "R", "samarbeider_med", kommunen, rhf, lov, eid, "utskrivningsklare pasienter");
            bistand = await KantAsync(db, "R", "bistar", politiet, kommunen, lov, eid);
            sekretariat = await KantAsync(db, "R", "sekretariat_for", kommunen, nemnd, lov, eid);
        }

        await HistoriskSkjema.MigrerAsync(conn, null);

        await using (var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(3, await etter.Strukturkanter.CountAsync()); // ingenting tapt, ingenting nytt

            // Samme retning (fra = pliktsubjektet) og avgrensning; modaliteten gjettes ikke (basen har ikke sitatet).
            Assert.Equal(new KantRad("P", "samarbeid", kommunen, rhf, eid, "utskrivningsklare pasienter"), await LesAsync(etter, samarbeid));
            Assert.Equal(new KantRad("P", "bistand", politiet, kommunen, eid, null), await LesAsync(etter, bistand));
            Assert.Equal(("R", "sekretariat_for"), ((await LesAsync(etter, sekretariat)).Kategori, (await LesAsync(etter, sekretariat)).Typekode));
            Assert.False(await etter.Strukturkanter.AnyAsync(k => k.Modalitet != null));
            Assert.Null((await etter.Strukturkanter.AsNoTracking().SingleAsync(k => k.Id == sekretariat)).SistEndretAv);

            // Én proveniensrad per flyttet kant (2), med de gamle verdiene.
            var prov = await etter.Proveniens.AsNoTracking()
                .Where(p => p.EndretAv == PliktMigrering.EndretAv && p.Handling == "endret").ToListAsync();
            Assert.Equal(2, prov.Count);
            using (var j = JsonDocument.Parse(prov.Single(p => p.EntitetId == samarbeid).KildeReferanserJson!))
            {
                Assert.Equal(("R", "samarbeider_med", "samarbeid"), (j.RootElement.GetProperty("fra_kategori").GetString(),
                    j.RootElement.GetProperty("fra_typekode").GetString(), j.RootElement.GetProperty("til_typekode").GetString()));
            }

            // Konfigurasjonen: de åtte nye typene finnes, de håndlagde R-kodene er borte.
            foreach (var (kategori, kode) in PliktMigrering.NyeTyper)
            {
                Assert.True(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == kategori && t.Kode == kode), $"{kategori} {kode}");
            }
            Assert.Equal(6, await etter.RelasjonsTypeKonfigurasjoner.CountAsync(t => t.Kategori == Strukturkanter.Plikt));
            foreach (var (gammel, _) in PliktMigrering.Flytting)
            {
                Assert.False(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == "R" && t.Kode == gammel), gammel);
            }
            // Malene er de samme som oppstartsseeden ville lagt inn (Strukturkanter.Startsett).
            var startsett = Strukturkanter.Startsett.Single(t => t.Kategori == Strukturkanter.Plikt && t.Kode == "samarbeid");
            var rad = await etter.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == Strukturkanter.Plikt && t.Kode == "samarbeid");
            Assert.Equal((startsett.FraMal, startsett.TilMal), (rad.FraVisningsmal, rad.TilVisningsmal));
        }

        // ---- Down: tilbake til #352 ----
        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.OppnevningsfamilienOgRester, leggTilSenereKolonner: false);

        await using (var tilbake = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            // Rå SQL: modalitet/ordningstype finnes ikke lenger i skjemaet.
            Assert.Equal("R|samarbeider_med", await tilbake.Database.SqlQuery<string>(
                $"SELECT kategori || '|' || typekode AS \"Value\" FROM strukturkanter WHERE \"Id\" = {samarbeid}").SingleAsync());
            Assert.Equal("R|bistar", await tilbake.Database.SqlQuery<string>(
                $"SELECT kategori || '|' || typekode AS \"Value\" FROM strukturkanter WHERE \"Id\" = {bistand}").SingleAsync());
            Assert.Equal(0, await HistoriskSkjema.TellAsync(tilbake, $"SELECT count(*)::int AS \"Value\" FROM proveniens WHERE endret_av = 'migrasjon-353'"));
            Assert.Equal(0, await HistoriskSkjema.TellAsync(tilbake,
                $"SELECT count(*)::int AS \"Value\" FROM relasjonstype_konfigurasjon WHERE kategori = 'P' OR kode IN ('forvaltes_av', 'tilhorer')"));
            Assert.Equal(2, await HistoriskSkjema.TellAsync(tilbake,
                $"SELECT count(*)::int AS \"Value\" FROM relasjonstype_konfigurasjon WHERE kategori = 'R' AND kode IN ('samarbeider_med', 'bistar')"));
            Assert.Equal(0, await HistoriskSkjema.TellAsync(tilbake,
                $"SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name = 'strukturkanter' AND column_name = 'modalitet'"));
        }
    }

    [Fact]
    public async Task Down_nekter_naar_det_finnes_en_ordning_eller_en_plikt_registrert_etter_migrasjonen()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_pl353b", null);
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = "Folketrygden", Aktortype = Nodetyper.Ordning, Ordningstype = "trygdeordning" });
            await db.SaveChangesAsync();
        }
        var feil = await Assert.ThrowsAnyAsync<Exception>(() => HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.OppnevningsfamilienOgRester,
            leggTilSenereKolonner: false));
        Assert.Contains("Issue #353 Down", feil.Message);
    }
}
