using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #352 «Kompetansemodellen: rester etter #341», 2026-10-08] Migrasjonen <c>OppnevningsfamilienOgRester</c>,
/// kjørt for ekte: en fersk database migreres til #341 (<see cref="HistoriskSkjema.KompetanseMedMotpart"/>), testen legger
/// inn kanter med rå SQL slik de lå før #352, migrerer til siste versjon — og tilbake.
/// <para>
/// Spørsmålene (sakens AC1/AC2): blir R <c>velger</c>, K <c>utpeking</c> og K <c>ansettelse</c> K <c>oppnevning</c> med
/// SAMME retning og verbet bevart som undertype? Blir R <c>ankeinstans_for</c> K <c>overproving</c> med undertype anke?
/// Heter familien <c>oppnevning</c>, og er forelegging <c>kontroll</c>? Står struktur-R (beslutning 4) og en K oppnevning
/// uten kjent verb urørt? Er tallene like før og etter, og snur <c>Down</c> nøyaktig de radene migrasjonen rørte?
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class OppnevningMigreringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public OppnevningMigreringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>En kant slik den lå før #352 — rå SQL, fordi skjemaet ennå ikke har <c>undertype</c>.</summary>
    private static async Task<Guid> KantAsync(RegelIdeDbContext db, string kategori, string kode, Guid fraV, Guid? tilV,
        Guid hjemmel, string? hjemmelEid, string? objekt = null, string? avgrensningTekst = null)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, til_virksomhet_id,
                hjemmel_rettskilde_id, hjemmel_eid, avgrensning_tekst, objekt, status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
            VALUES ({id}, {kategori}, {kode}, {fraV}, {tilV}, {hjemmel}, {hjemmelEid}, {avgrensningTekst}, {objekt},
                'validert', 'manuell', 'test', now())
            """);
        return id;
    }

    private sealed record KantRad(string Kategori, string Typekode, Guid? Fra, Guid? Til, string? HjemmelEid, string? Avgrensning,
        string? Undertype);

    private static async Task<KantRad> LesAsync(RegelIdeDbContext db, Guid id, bool medUndertype = true)
    {
        var k = await db.Strukturkanter.AsNoTracking().SingleAsync(x => x.Id == id);
        return new KantRad(k.Kategori, k.Typekode, k.FraVirksomhetId, k.TilVirksomhetId, k.HjemmelEid, k.AvgrensningTekst,
            medUndertype ? k.Undertype : null);
    }

    [Fact]
    public async Task Oppnevningsfamilien_og_anke_blir_K_med_undertype_og_Down_snur_bare_de_radene()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_op352", HistoriskSkjema.KompetanseMedMotpart,
            leggTilSenereKolonner: false);
        Guid kommunestyret, forliksradet, styret, direktor, lagmannsrett, tingrett, dep, lov;
        string eid;
        Guid velger, utpeking, ansettelse, anke, oppnevning, radgir, oppretter;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            // Konfigurasjonen slik oppstartsseeden fra #341 la den: R velger/ankeinstans_for/radgir/oppretter står (de var
            // UAVKLART); K-typene fra #341 er lagt inn av migrasjonen KompetanseMedMotpart.
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
                VALUES (gen_random_uuid(), 'R', 'velger', 'velger {{0}}', 'velges av {{0}}', 13, true),
                       (gen_random_uuid(), 'R', 'radgir', 'gir råd til {{0}}', 'får råd fra {{0}}', 17, true),
                       (gen_random_uuid(), 'R', 'ankeinstans_for', 'er ankeinstans for {{0}}', 'har ankeinstans hos {{0}}', 20, true),
                       (gen_random_uuid(), 'R', 'oppretter', 'oppretter {{0}}', 'er opprettet av {{0}}', 21, true)
                ON CONFLICT (kategori, kode) DO NOTHING;
                """);
            Assert.Equal("personell", (await db.Database.SqlQueryRaw<string>(
                "SELECT familie AS \"Value\" FROM relasjonstype_konfigurasjon WHERE kategori = 'K' AND kode = 'ansettelse'").SingleAsync()));

            lov = await new RettskildeImportTjeneste(db).ImporterAsync(
                LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
            eid = (await db.RettskildeNoder.Where(n => n.RettskildeId == lov && n.NodeType == "paragraf")
                .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync()).Eid;
            // [ENDRET, #353] Rå SQL: dagens Virksomhet har ordningstype, som skjemaet ved #341 ikke har.
            Task<Guid> Ny(string navn) => HistoriskSkjema.VirksomhetAsync(db, navn);
            (kommunestyret, forliksradet, styret, direktor) = (await Ny("Kommunestyret"), await Ny("Forliksrådet"), await Ny("Styret"), await Ny("Direktøren"));
            (lagmannsrett, tingrett, dep) = (await Ny("Lagmannsretten"), await Ny("Tingretten"), await Ny("Departementet"));

            velger = await KantAsync(db, "R", "velger", kommunestyret, forliksradet, lov, eid, avgrensningTekst: "hvert fjerde år");
            utpeking = await KantAsync(db, "K", "utpeking", dep, tingrett, lov, eid, objekt: "rettssted");
            ansettelse = await KantAsync(db, "K", "ansettelse", styret, direktor, lov, eid);
            anke = await KantAsync(db, "R", "ankeinstans_for", lagmannsrett, tingrett, lov, eid);
            // Allerede oppnevning: verbet er ukjent for basen (ingen sitat) — undertypen gjettes ikke.
            oppnevning = await KantAsync(db, "K", "oppnevning", dep, styret, lov, eid);
            // Beslutning 4: struktur, ikke myndighet — urørt.
            radgir = await KantAsync(db, "R", "radgir", forliksradet, kommunestyret, lov, eid);
            oppretter = await KantAsync(db, "R", "oppretter", dep, styret, lov, eid);
        }

        await HistoriskSkjema.MigrerAsync(conn, null);

        await using (var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(7, await etter.Strukturkanter.CountAsync()); // ingenting tapt, ingenting nytt

            // Samme retning og avgrensning; verbet er undertypen.
            Assert.Equal(new KantRad("K", "oppnevning", kommunestyret, forliksradet, eid, "hvert fjerde år", "valg"), await LesAsync(etter, velger));
            Assert.Equal(new KantRad("K", "oppnevning", dep, tingrett, eid, null, "utpeking"), await LesAsync(etter, utpeking));
            Assert.Equal("rettssted", (await etter.Strukturkanter.AsNoTracking().SingleAsync(k => k.Id == utpeking)).Objekt);
            Assert.Equal(new KantRad("K", "oppnevning", styret, direktor, eid, null, "ansettelse"), await LesAsync(etter, ansettelse));
            Assert.Equal(new KantRad("K", "overproving", lagmannsrett, tingrett, eid, null, "anke"), await LesAsync(etter, anke));
            Assert.Equal(new KantRad("K", "oppnevning", dep, styret, eid, null, null), await LesAsync(etter, oppnevning));
            Assert.Equal(("R", "radgir"), ((await LesAsync(etter, radgir)).Kategori, (await LesAsync(etter, radgir)).Typekode));
            Assert.Equal(("R", "oppretter"), ((await LesAsync(etter, oppretter)).Kategori, (await LesAsync(etter, oppretter)).Typekode));
            Assert.Null((await etter.Strukturkanter.AsNoTracking().SingleAsync(k => k.Id == oppnevning)).SistEndretAv);

            // Én proveniensrad per flyttet kant (4), med de gamle verdiene.
            var prov = await etter.Proveniens.AsNoTracking()
                .Where(p => p.EndretAv == OppnevningMigrering.EndretAv && p.Handling == "endret").ToListAsync();
            Assert.Equal(4, prov.Count);
            using (var j = JsonDocument.Parse(prov.Single(p => p.EntitetId == velger).KildeReferanserJson!))
            {
                Assert.Equal(("R", "velger", "oppnevning", "valg"), (j.RootElement.GetProperty("fra_kategori").GetString(),
                    j.RootElement.GetProperty("fra_typekode").GetString(), j.RootElement.GetProperty("til_typekode").GetString(),
                    j.RootElement.GetProperty("til_undertype").GetString()));
            }

            // Konfigurasjonen: flyttede koder borte, familien heter oppnevning, forelegging er kontroll.
            foreach (var (kategori, kode, _, _) in OppnevningMigrering.Flytting)
            {
                Assert.False(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == kategori && t.Kode == kode), $"{kategori} {kode}");
            }
            Assert.False(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Familie == "personell"));
            Assert.Equal("oppnevning", (await etter.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == "K" && t.Kode == "avsetting")).Familie);
            Assert.Equal("kontroll", (await etter.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == "K" && t.Kode == "forelegging")).Familie);
            Assert.True(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == "R" && t.Kode == "radgir"));
            // [Ny, #352-tillegg] Sammensetningen i den enkelte sak: G settes_med, merket saksavhengig — og bare den.
            Assert.True((await etter.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == "G" && t.Kode == Strukturkanter.SettesMed)).Saksavhengig);
            Assert.Equal(1, await etter.RelasjonsTypeKonfigurasjoner.CountAsync(t => t.Saksavhengig));
        }

        // ---- Down: tilbake til #341 ----
        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.KompetanseMedMotpart);

        await using (var tilbake = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(new KantRad("R", "velger", kommunestyret, forliksradet, eid, "hvert fjerde år", null), await LesAsync(tilbake, velger, false));
            Assert.Equal(("K", "utpeking"), ((await LesAsync(tilbake, utpeking, false)).Kategori, (await LesAsync(tilbake, utpeking, false)).Typekode));
            Assert.Equal(("K", "ansettelse"), ((await LesAsync(tilbake, ansettelse, false)).Kategori, (await LesAsync(tilbake, ansettelse, false)).Typekode));
            Assert.Equal(("R", "ankeinstans_for"), ((await LesAsync(tilbake, anke, false)).Kategori, (await LesAsync(tilbake, anke, false)).Typekode));
            Assert.Equal("oppnevning", (await LesAsync(tilbake, oppnevning, false)).Typekode);
            Assert.False(await tilbake.Proveniens.AnyAsync(p => p.EndretAv == OppnevningMigrering.EndretAv));
            Assert.True(await tilbake.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == "R" && t.Kode == "velger"));
            Assert.Equal("personell", (await tilbake.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == "K" && t.Kode == "ansettelse")).Familie);
            Assert.Null((await tilbake.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == "K" && t.Kode == "forelegging")).Familie);
            Assert.False(await tilbake.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kode == Strukturkanter.SettesMed));
        }
    }
}
