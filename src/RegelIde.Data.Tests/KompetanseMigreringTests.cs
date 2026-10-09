using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #341 «kompetanse med motpart og typologi», 2026-10-08] Migrasjonen <c>KompetanseMedMotpart</c>, kjørt for
/// ekte: en fersk database migreres til #312 (<see cref="HistoriskSkjema.Omraderegister"/>), testen legger inn kanter med
/// rå SQL slik de lå før #341, migrerer til siste versjon — og tilbake.
/// <para>
/// Spørsmålene (issue #341 AC1/AC2): blir R <c>klageinstans_for</c> til K <c>klage</c> med SAMME retning (fra =
/// klageinstansen, til = den hvis vedtak påklages) og uendret avgrensning? Blir K <c>forskrift</c> normgivning med
/// normform forskrift? Får M/I-kantene fra #311 hjemmelsstedet flyttet fra avgrensningen til <c>hjemmel_eid</c> — og
/// står et spenn i en ANNEN rettskilde (en ekte avgrensning) urørt? Er tallene like før og etter, og snur <c>Down</c>
/// nøyaktig de radene migrasjonen rørte?
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class KompetanseMigreringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KompetanseMigreringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Spenn(string eid) => $$"""[{"FraEid":"{{eid}}","TilEid":null}]""";

    /// <summary>En kant slik den lå før #341 — rå SQL, fordi skjemaet ennå ikke har kolonnene dagens EF-modell kjenner.</summary>
    private static async Task<Guid> KantAsync(RegelIdeDbContext db, string kategori, string kode, Guid? fraV, Guid? tilV,
        Guid? tilB, Guid hjemmel, string? hjemmelEid, string spenn = "[]", string? avgrensningTekst = null,
        string polaritet = "positiv", string? objekt = null)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, til_virksomhet_id, til_begrep_id,
                hjemmel_rettskilde_id, hjemmel_eid, avgrensning_paragrafspenn_json, avgrensning_tekst, polaritet, objekt,
                status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
            VALUES ({id}, {kategori}, {kode}, {fraV}, {tilV}, {tilB}, {hjemmel}, {hjemmelEid}, {spenn}, {avgrensningTekst},
                {polaritet}, {objekt}, 'validert', 'manuell', 'test', now())
            """);
        return id;
    }

    private sealed record KantRad(string Kategori, string Typekode, Guid? Fra, Guid? Til, Guid? TilBegrep, string? HjemmelEid,
        string Spenn, string? AvgrensningTekst, string Polaritet, string? Normform);

    private static async Task<KantRad> LesAsync(RegelIdeDbContext db, Guid id, bool medNormform = true)
    {
        var k = await db.Strukturkanter.AsNoTracking().SingleAsync(x => x.Id == id);
        return new KantRad(k.Kategori, k.Typekode, k.FraVirksomhetId, k.TilVirksomhetId, k.TilBegrepId, k.HjemmelEid,
            k.AvgrensningParagrafspennJson, k.AvgrensningTekst, k.Polaritet, medNormform ? k.Normform : null);
    }

    [Fact]
    public async Task Myndighetsrelasjoner_blir_K_med_motpart_og_hjemmelssted_flyttes_og_Down_snur_bare_de_radene()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_km341", HistoriskSkjema.Omraderegister,
            leggTilSenereKolonner: false);
        Guid dep, nemnd, tilsyn, lov, forskrift, klasse, rolle;
        string lovEid, forskriftEid, forskriftEid2;
        Guid klage, instruks, forskriftK, sekretariat, mHjemmel, mAnnenLov, iHjemmel, mUtenSpenn;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            // Konfigurasjonen slik en kjørende base hadde den før #341 (oppstartsseeden fra #311/#330).
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
                VALUES (gen_random_uuid(), 'R', 'klageinstans_for', 'er klageinstans for {{0}}', 'har klageinstans hos {{0}}', 6, true),
                       (gen_random_uuid(), 'R', 'instruksjon', 'kan instruere {{0}}', 'kan instrueres av {{0}}', 8, true),
                       (gen_random_uuid(), 'R', 'sekretariat_for', 'er sekretariat for {{0}}', 'har sekretariat hos {{0}}', 10, true),
                       (gen_random_uuid(), 'R', 'delegerer_til', 'delegerer myndighet til {{0}}', 'har fått delegert myndighet fra {{0}}', 18, true),
                       (gen_random_uuid(), 'K', 'forskrift', 'har forskriftskompetanse: {{0}}', 'forskriftskompetanse ligger hos {{0}}', 23, true),
                       (gen_random_uuid(), 'K', 'klage', 'har klagekompetanse: {{0}}', 'klagekompetanse ligger hos {{0}}', 25, true),
                       (gen_random_uuid(), 'K', 'tilsyn', 'kan føre tilsyn: {{0}}', 'tilsyn ligger hos {{0}}', 26, true),
                       (gen_random_uuid(), 'K', 'iverksetting', 'har iverksettingskompetanse: {{0}}', 'iverksettingskompetanse ligger hos {{0}}', 32, true),
                       (gen_random_uuid(), 'M', 'medlem_av', 'er medlem av {{0}}', 'har medlem {{0}}', 34, true),
                       (gen_random_uuid(), 'I', 'innehar', 'innehar rollen {{0}}', 'innehas av {{0}}', 43, true)
                ON CONFLICT (kategori, kode) DO NOTHING;
                """);
            var importer = new RettskildeImportTjeneste(db);
            lov = await importer.ImporterAsync(LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
            forskrift = await importer.ImporterAsync(LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 22)));
            lovEid = (await db.RettskildeNoder.Where(n => n.RettskildeId == lov && n.NodeType == "paragraf")
                .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync()).Eid;
            var fparagrafer = await db.RettskildeNoder.Where(n => n.RettskildeId == forskrift && n.NodeType == "paragraf")
                .OrderBy(n => n.Sorteringsrekkefolge).Take(2).ToListAsync();
            (forskriftEid, forskriftEid2) = (fparagrafer[0].Eid, fparagrafer[1].Eid);

            // [ENDRET, #353] Rå SQL: dagens Virksomhet har ordningstype, som skjemaet ved #341 ikke har.
            dep = await HistoriskSkjema.VirksomhetAsync(db, "Departementet");
            nemnd = await HistoriskSkjema.VirksomhetAsync(db, "Klagenemnda");
            tilsyn = await HistoriskSkjema.VirksomhetAsync(db, "Tilsynet");
            var begreper = new VirksomhetsbegrepTjeneste(db);
            klasse = (await begreper.OpprettGruppebegrepAsync(Nodetyper.Klasse, lov, $"kommuner-{Guid.NewGuid():N}", "test")).Id;
            rolle = (await begreper.OpprettGruppebegrepAsync(Nodetyper.Rolle, lov, $"myndighet-{Guid.NewGuid():N}", "test")).Id;

            // «Departementet er klageinstans for Klagenemnda», avgrenset (Energiklagenemnda-raden har samme form).
            klage = await KantAsync(db, "R", "klageinstans_for", dep, nemnd, null, forskrift, forskriftEid,
                Spenn(forskriftEid), "enkeltvedtak nemnda treffer i første instans");
            instruks = await KantAsync(db, "R", "instruksjon", dep, tilsyn, null, lov, lovEid, polaritet: "negativ");
            forskriftK = await KantAsync(db, "K", "forskrift", dep, null, null, lov, lovEid, objekt: "salgstider");
            sekretariat = await KantAsync(db, "R", "sekretariat_for", tilsyn, nemnd, null, lov, lovEid);
            // M/I fra #311: hjemmelsstedet ligger i avgrensningen.
            mHjemmel = await KantAsync(db, "M", "medlem_av", nemnd, null, klasse, forskrift, null, Spenn(forskriftEid));
            iHjemmel = await KantAsync(db, "I", "innehar", tilsyn, null, rolle, forskrift, null, Spenn(forskriftEid2));
            // Et spenn i en ANNEN rettskilde enn hjemmelen er en ekte avgrensning — skal stå.
            mAnnenLov = await KantAsync(db, "M", "medlem_av", tilsyn, null, klasse, forskrift, null, Spenn(lovEid));
            // Hverken sted eller spenn: et synlig hull, røres ikke.
            mUtenSpenn = await KantAsync(db, "M", "medlem_av", dep, null, klasse, forskrift, null);
        }

        await HistoriskSkjema.MigrerAsync(conn, null);

        await using (var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(8, await etter.Strukturkanter.CountAsync()); // ingenting tapt, ingenting nytt

            // Klageinstans → K klage, SAMME retning og avgrensning.
            Assert.Equal(new KantRad("K", "klage", dep, nemnd, null, forskriftEid, Spenn(forskriftEid),
                "enkeltvedtak nemnda treffer i første instans", "positiv", null), await LesAsync(etter, klage));
            Assert.Equal(new KantRad("K", "instruksjon", dep, tilsyn, null, lovEid, "[]", null, "negativ", null),
                await LesAsync(etter, instruks));
            Assert.Equal(new KantRad("K", "normgivning", dep, null, null, lovEid, "[]", null, "positiv", "forskrift"),
                await LesAsync(etter, forskriftK));
            Assert.Equal("sekretariat_for", (await LesAsync(etter, sekretariat)).Typekode); // struktur — urørt
            Assert.Null((await etter.Strukturkanter.AsNoTracking().SingleAsync(k => k.Id == sekretariat)).SistEndretAv);

            // Hjemmelssted: flyttet til hjemmel_eid, avgrensningen tømt.
            Assert.Equal((forskriftEid, "[]"), ((await LesAsync(etter, mHjemmel)).HjemmelEid, (await LesAsync(etter, mHjemmel)).Spenn));
            Assert.Equal((forskriftEid2, "[]"), ((await LesAsync(etter, iHjemmel)).HjemmelEid, (await LesAsync(etter, iHjemmel)).Spenn));
            Assert.Equal(((string?)null, Spenn(lovEid)), ((await LesAsync(etter, mAnnenLov)).HjemmelEid, (await LesAsync(etter, mAnnenLov)).Spenn));
            Assert.Equal(((string?)null, "[]"), ((await LesAsync(etter, mUtenSpenn)).HjemmelEid, (await LesAsync(etter, mUtenSpenn)).Spenn));

            // Ingen grunnlag eller delegerbar er gjettet.
            Assert.False(await etter.Strukturkanter.AnyAsync(k => k.Grunnlag != null || k.Delegerbar != null));

            // Én proveniensrad per endret kant (3 flyttet + 2 hjemmelssted), med de gamle verdiene.
            var prov = await etter.Proveniens.AsNoTracking()
                .Where(p => p.EndretAv == KompetanseMigrering.EndretAv && p.Handling == "endret").ToListAsync();
            Assert.Equal(5, prov.Count);
            using (var j = JsonDocument.Parse(prov.Single(p => p.EntitetId == klage).KildeReferanserJson!))
            {
                Assert.Equal(("R", "klageinstans_for", "klage"), (j.RootElement.GetProperty("fra_kategori").GetString(),
                    j.RootElement.GetProperty("fra_typekode").GetString(), j.RootElement.GetProperty("til_typekode").GetString()));
            }
            using (var j = JsonDocument.Parse(prov.Single(p => p.EntitetId == mHjemmel).KildeReferanserJson!))
            {
                Assert.Equal(Spenn(forskriftEid), j.RootElement.GetProperty("gammelt_avgrensningsspenn").GetString());
            }

            // Konfigurasjonen: flyttede/fjernede koder borte, nye på plass, K-malene med «overfor»-formen der de var urørt.
            foreach (var (kategori, kode) in KompetanseMigrering.FjernedeKoder)
            {
                Assert.False(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == kategori && t.Kode == kode), $"{kategori} {kode}");
            }
            // [ENDRET, issue #355] «forelegging» er tatt ut av lista: testen migrerer til siste versjon, og migrasjonen
            // AvslutningSpeilerInnsetting sletter koden (forelegging klassifiseres etter rettsvirkningen).
            foreach (var kode in new[] { "klage", "normgivning", "avsetting", "palegg" })
            {
                Assert.True(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == "K" && t.Kode == kode), kode);
            }
            Assert.True(await etter.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == "R" && t.Kode == Strukturkanter.HarDelegertTil));
            var klagemal = await etter.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == "K" && t.Kode == "klage");
            Assert.Equal("har klagekompetanse {0}", klagemal.FraVisningsmal);
            // En mal endret bevisst i drift røres ikke.
            Assert.Equal("kan føre tilsyn: {0}", (await etter.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == "K" && t.Kode == "tilsyn")).FraVisningsmal);
        }

        // ---- Down: tilbake til #312 ----
        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.Omraderegister);

        await using (var tilbake = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(new KantRad("R", "klageinstans_for", dep, nemnd, null, forskriftEid, Spenn(forskriftEid),
                "enkeltvedtak nemnda treffer i første instans", "positiv", null), await LesAsync(tilbake, klage, medNormform: false));
            Assert.Equal(("K", "forskrift"), ((await LesAsync(tilbake, forskriftK, false)).Kategori, (await LesAsync(tilbake, forskriftK, false)).Typekode));
            Assert.Equal(((string?)null, Spenn(forskriftEid)), ((await LesAsync(tilbake, mHjemmel, false)).HjemmelEid, (await LesAsync(tilbake, mHjemmel, false)).Spenn));
            Assert.Equal(Spenn(lovEid), (await LesAsync(tilbake, mAnnenLov, false)).Spenn);
            Assert.False(await tilbake.Proveniens.AnyAsync(p => p.EndretAv == KompetanseMigrering.EndretAv));
            Assert.True(await tilbake.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == "R" && t.Kode == "klageinstans_for"));
            Assert.Equal("har klagekompetanse: {0}",
                (await tilbake.RelasjonsTypeKonfigurasjoner.SingleAsync(t => t.Kategori == "K" && t.Kode == "klage")).FraVisningsmal);
        }
    }

    [Theory]
    [InlineData("R", "delegerer_til")]
    [InlineData("K", "iverksetting")]
    public async Task Migrasjonen_avbryter_i_stedet_for_aa_gjette_typen(string kategori, string kode)
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_km341b", HistoriskSkjema.Omraderegister,
            leggTilSenereKolonner: false);
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            var lov = await new RettskildeImportTjeneste(db).ImporterAsync(
                LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
            var a = await HistoriskSkjema.VirksomhetAsync(db, "A"); // [ENDRET, #353] rå SQL, se over
            var b = await HistoriskSkjema.VirksomhetAsync(db, "B");
            await KantAsync(db, kategori, kode, a, kategori == "R" ? b : null, null, lov, null, objekt: kategori == "K" ? "noe" : null);
        }

        var feil = await Assert.ThrowsAnyAsync<Exception>(() => HistoriskSkjema.MigrerAsync(conn, null));
        Assert.Contains("Issue #341", feil.Message);
    }
}
