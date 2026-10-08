using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #330 «harmoniser gamle og nye relasjonskoder», 2026-10-08] Migrasjonen <c>HarmoniserRelasjonskoder</c>,
/// kjørt for ekte: en fersk database migreres til #311 (<see cref="HistoriskSkjema.Strukturkanttabell"/>), testen
/// legger inn én kant per gammel kode (pluss en kant som alt bruker en ny kode, og en kant i en annen kategori), og
/// så migreres den til siste versjon — og tilbake.
/// <para>
/// Spørsmålene (issue #330 AC1/AC2): blir hver gammel kode den nye koden i sakens tabell, med fra/til byttet NØYAKTIG
/// der det står «byttes» — og ingenting annet endret (id, hjemmel, kilde, status)? Er de gamle kodene borte fra
/// konfigurasjonen? Og snur <c>Down</c> nøyaktig de radene migrasjonen rørte, og ingen andre?
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class RelasjonskodeHarmoniseringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public RelasjonskodeHarmoniseringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static Guid V(RegelIdeDbContext db, string navn) =>
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = navn }).Entity.Id;

    private static StrukturkantEntitet Kant(RegelIdeDbContext db, string kategori, string kode, Guid fra, Guid til, Guid? hjemmel,
        string? hjemmelEid = null) =>
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = kategori, Typekode = kode, FraVirksomhetId = fra, TilVirksomhetId = til,
            HjemmelRettskildeId = hjemmel, HjemmelEid = hjemmelEid,
            KildeUtenforKorpusTekst = hjemmel is null ? "org-kart" : null,
            KildeUtenforKorpusType = hjemmel is null ? Strukturkanter.NettsideAnnet : null,
            KildeUtenforKorpusDokumentasjon = hjemmel is null ? Strukturkanter.Sekundaer : null,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        }).Entity;

    [Fact]
    public async Task Hver_gammel_kode_konverteres_etter_tabellen_og_Down_snur_bare_de_radene()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_hk330", HistoriskSkjema.Strukturkanttabell);
        Guid a, b, c, lov;
        StrukturkantEntitet klageinstans, sekretariat, overfort, enhetI, underlagt, alleredeNy, annenKategori;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            // Konfigurasjonen slik en kjørende base hadde den før #330: de fem gamle R-kodene (oppstartsseeden).
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
                VALUES (gen_random_uuid(), 'R', 'underlagt', 'er underlagt {0}', 'er eier/overordnet for {0}', 0, true),
                       (gen_random_uuid(), 'R', 'sekretariat', 'har sekretariat hos {0}', 'er sekretariat for {0}', 1, true),
                       (gen_random_uuid(), 'R', 'klageinstans', 'har klageinstans hos {0}', 'er klageinstans for {0}', 2, true),
                       (gen_random_uuid(), 'R', 'enhet_i', 'er enhet i {0}', 'har enhet {0}', 3, true),
                       (gen_random_uuid(), 'R', 'oppgaver_overfort_til', 'fikk oppgavene overført til {0}', 'overtok oppgavene til {0}', 4, true)
                ON CONFLICT (kategori, kode) DO NOTHING;
                """);
            lov = db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Forskrift", Tittel = "forskrift om en nemnd", Status = "Gjeldende",
                Importrolle = "referanse", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            }).Entity.Id;
            a = V(db, "Nemnda");
            b = V(db, "Departementet");
            c = V(db, "Sekretariatet");
            await db.SaveChangesAsync();

            // «Nemnda har klageinstans hos Departementet» → «Departementet er klageinstans for Nemnda».
            klageinstans = Kant(db, "R", "klageinstans", a, b, lov, "§1/ledd-2");
            sekretariat = Kant(db, "R", "sekretariat", a, c, null);
            overfort = Kant(db, "R", "oppgaver_overfort_til", a, b, lov);
            enhetI = Kant(db, "R", "enhet_i", c, b, null);
            underlagt = Kant(db, "R", "underlagt", a, b, lov);
            alleredeNy = Kant(db, "R", "klageinstans_for", b, c, lov); // registrert med ny kode fra før — skal ikke røres
            annenKategori = Kant(db, "G", "har_organ", a, b, lov); // ikke en gammel kode
            await db.SaveChangesAsync();
        }

        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.HarmoniserRelasjonskoder);

        await using (var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(7, await etter.Strukturkanter.CountAsync()); // ingenting tapt, ingenting nytt
            async Task<StrukturkantEntitet> K(Guid id) => await etter.Strukturkanter.AsNoTracking().SingleAsync(k => k.Id == id);

            // Retning byttes: klageinstans, sekretariat, oppgaver_overfort_til.
            var k1 = await K(klageinstans.Id);
            Assert.Equal(("R", "klageinstans_for", b, a), (k1.Kategori, k1.Typekode, k1.FraVirksomhetId!.Value, k1.TilVirksomhetId!.Value));
            Assert.Equal((lov, "§1/ledd-2", "validert"), (k1.HjemmelRettskildeId!.Value, k1.HjemmelEid, k1.Status)); // resten urørt
            Assert.Equal(RelasjonskodeHarmonisering.EndretAv, k1.SistEndretAv);
            var k2 = await K(sekretariat.Id);
            Assert.Equal(("R", "sekretariat_for", c, a), (k2.Kategori, k2.Typekode, k2.FraVirksomhetId!.Value, k2.TilVirksomhetId!.Value));
            Assert.Equal(("org-kart", Strukturkanter.NettsideAnnet), (k2.KildeUtenforKorpusTekst, k2.KildeUtenforKorpusType));
            var k3 = await K(overfort.Id);
            Assert.Equal(("R", "etterfolger", b, a), (k3.Kategori, k3.Typekode, k3.FraVirksomhetId!.Value, k3.TilVirksomhetId!.Value));
            // Samme retning: enhet_i (blir G), underlagt.
            var k4 = await K(enhetI.Id);
            Assert.Equal(("G", "del_av", c, b), (k4.Kategori, k4.Typekode, k4.FraVirksomhetId!.Value, k4.TilVirksomhetId!.Value));
            var k5 = await K(underlagt.Id);
            Assert.Equal(("R", "administrativt_underordnet", a, b), (k5.Kategori, k5.Typekode, k5.FraVirksomhetId!.Value, k5.TilVirksomhetId!.Value));
            // Urørt.
            var k6 = await K(alleredeNy.Id);
            Assert.Equal(("klageinstans_for", b, c), (k6.Typekode, k6.FraVirksomhetId!.Value, k6.TilVirksomhetId!.Value));
            Assert.Null(k6.SistEndretAv);
            Assert.Equal("har_organ", (await K(annenKategori.Id)).Typekode);
            Assert.Equal(2, await etter.Strukturkanter.CountAsync(k => k.Typekode == "klageinstans_for")); // 1 før + 1 konvertert

            // Én proveniensrad per konvertert kant, med gammel kode og de opprinnelige endene.
            var prov = await etter.Proveniens.AsNoTracking()
                .Where(p => p.EndretAv == RelasjonskodeHarmonisering.EndretAv && p.Handling == "endret").ToListAsync();
            Assert.Equal(5, prov.Count);
            using (var j = JsonDocument.Parse(prov.Single(p => p.EntitetId == klageinstans.Id).KildeReferanserJson!))
            {
                var r = j.RootElement;
                Assert.Equal(("klageinstans", "klageinstans_for", true), (r.GetProperty("fra_typekode").GetString(),
                    r.GetProperty("til_typekode").GetString(), r.GetProperty("retning_byttet").GetBoolean()));
                Assert.Equal(a, r.GetProperty("opprinnelig_fra_virksomhet_id").GetGuid());
            }

            // Konfigurasjonen: ingen gamle koder igjen; målkodene finnes.
            Assert.False(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => RelasjonskodeHarmonisering.GamleKoder.Contains(t.Kode)));
            foreach (var (_, nyKategori, nyKode, _) in RelasjonskodeHarmonisering.Mapping)
            {
                Assert.True(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == nyKategori && t.Kode == nyKode));
            }
        }

        // ---- Down: tilbake til #311 ----
        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.Strukturkanttabell);

        await using (var tilbake = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            async Task<StrukturkantEntitet> K(Guid id) => await tilbake.Strukturkanter.AsNoTracking().SingleAsync(k => k.Id == id);
            foreach (var original in new[] { klageinstans, sekretariat, overfort, enhetI, underlagt, alleredeNy, annenKategori })
            {
                var k = await K(original.Id);
                Assert.Equal((original.Kategori, original.Typekode, original.FraVirksomhetId, original.TilVirksomhetId),
                    (k.Kategori, k.Typekode, k.FraVirksomhetId, k.TilVirksomhetId));
            }
            Assert.False(await tilbake.Proveniens.AnyAsync(p => p.EndretAv == RelasjonskodeHarmonisering.EndretAv));
            Assert.Equal(5, await tilbake.RelasjonsTypeKonfigurasjoner.CountAsync(
                t => t.Kategori == "R" && RelasjonskodeHarmonisering.GamleKoder.Contains(t.Kode)));
        }
    }

    // En FERSK base (ingen kanter; bare oppgaver_overfort_til fra migrasjonen LeggTilRelasjonstypeOppgaverOverfortTil)
    // dekkes av fixturen selv, som migrerer test-databasen til siste versjon — og av
    // StrukturkantTjenesteTests.De_gamle_relasjonskodene_finnes_ikke_og_kan_ikke_brukes. Ingen egen base her (se
    // NodetypeReklassifiseringTests for hvorfor antallet ferske baser holdes lavt).
}
