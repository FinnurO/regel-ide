using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #311 «Strukturmodell 6», 2026-10-07] Migrasjonen <c>InnforStrukturkanttabell</c>, kjørt for
/// ekte: en fersk database migreres til #310 (<see cref="HistoriskSkjema.Nodetypeakse"/>), testen legger inn
/// rader i de GAMLE tabellene, og så migreres den videre til #311. Spørsmålene testene besvarer (issue #311
/// AC2 og Johanns organ-beslutning): blir hver rad i de tre gamle tabellene nøyaktig én kant med riktig
/// kategori, typekode, ender og kilde — og blir organene virksomheter og organ-begrepene navneformer?
/// <para>
/// Én database per scenario (to stk.): en migrasjon kan bare kjøres framover én gang per base. Se
/// <c>NodetypeReklassifiseringTests</c> for hvorfor antallet ferske baser holdes lavt.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class StrukturkantMigreringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public StrukturkantMigreringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static Virksomhet V(RegelIdeDbContext db, string navn, string? orgnr = null, string? aktortype = null) =>
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = navn, Organisasjonsnummer = orgnr, Aktortype = aktortype }).Entity;

    private static BegrepEntitet B(RegelIdeDbContext db, string term, string kategori, Guid? lov = null, Guid? virksomhetRef = null,
        string entitetsstatus = "gjeldende") =>
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Term = term, Begrepskategori = kategori, LovkildeId = lov, VirksomhetReferanseId = virksomhetRef,
            Status = "publisert", Entitetsstatus = entitetsstatus, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        }).Entity;

    private static RettskildeEntitet R(RegelIdeDbContext db, string tittel, string? eli) =>
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Tittel = tittel, Eli = eli, Status = "Gjeldende",
            Importrolle = "referanse", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        }).Entity;

    private static TekstTaggEntitet Tagg(RegelIdeDbContext db, Guid eier, Guid rettskilde, Guid refId, int start) =>
        db.TekstTagger.Add(new TekstTaggEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = eier, RettskildeId = rettskilde, NodeEid = "§1/ledd-1",
            StartOffset = start, EndOffset = start + 5, QuotePrefix = "", QuoteExact = "x", QuoteSuffix = "",
            NodeTekstHash = "h", Kind = "begrep", RefId = refId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        }).Entity;

    /// <summary>
    /// Alle tre tabellene, alle kombinasjoner migrasjonen skiller mellom: relasjon med/uten hjemmel og
    /// kommentar, gruppe-av-gruppe, tildeling til klasse/område/rolle, vilkår og et KI-forslag. Pluss organene.
    /// </summary>
    [Fact]
    public async Task Alle_rader_flyttes_1_til_1_og_organene_blir_virksomheter()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_sk311", HistoriskSkjema.Nodetypeakse);
        Guid relHjemlet, relKommentar, relIngenting, relBegge, gm, mtKlasse, mtOmrade, mtRolle, mtForslag;
        Guid kongenIStatsradBegrep, kongenArkivert, stortingetBegrep, ukjentOrgan, taggKongen, taggStortinget;
        Guid lov, grunnloven, eier, a, b;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            lov = R(db, "en lov", "https://lovdata.no/eli/lov/2000/01/01/1/nor").Id;
            grunnloven = R(db, "Kongeriket Norges Grunnlov", StrukturkantMigrering.GrunnlovenEli).Id;
            eier = V(db, "Eierdepartementet", "999999901").Id;
            a = V(db, "Organ A", "999999902").Id;
            b = V(db, "Organ B", "999999903").Id;
            var klasse = B(db, "språkutviklingskommuner", "klasse", lov).Id;
            var omrade = B(db, "forvaltningsområdet", "omrade", lov).Id;
            var rolle = B(db, "reguleringsmyndighet", "rolle", lov).Id;
            kongenIStatsradBegrep = B(db, "Kongen i statsråd", "organ").Id;
            kongenArkivert = B(db, "kongen", "organ", entitetsstatus: "arkivert").Id;
            stortingetBegrep = B(db, "stortinget", "organ", lov).Id;
            ukjentOrgan = B(db, "riksrevisjonen", "organ", lov).Id;
            await db.SaveChangesAsync();
            taggKongen = Tagg(db, eier, lov, kongenIStatsradBegrep, 0).Id;
            taggStortinget = Tagg(db, eier, lov, stortingetBegrep, 10).Id;
            await db.SaveChangesAsync();

            relHjemlet = (await HistoriskSkjema.RelasjonAsync(db, a, b, "klageinstans", lov, "§1", null)).Id;
            relKommentar = (await HistoriskSkjema.RelasjonAsync(db, a, b, "sekretariat", null, null, "bekreftet mot org-kartet")).Id;
            relIngenting = (await HistoriskSkjema.RelasjonAsync(db, b, a, "enhet_i", null, null, null)).Id;
            relBegge = (await HistoriskSkjema.RelasjonAsync(db, b, a, "oppgaver_overfort_til", lov, null, "Gjelder klagesaker")).Id;
            gm = (await HistoriskSkjema.MedlemskapAsync(db, omrade, klasse, lov)).Id;
            mtKlasse = (await HistoriskSkjema.TildelingAsync(db, klasse, a, lov, vilkaar: "bare kommunale vedtak")).Id;
            mtOmrade = (await HistoriskSkjema.TildelingAsync(db, omrade, a, lov)).Id;
            mtRolle = (await HistoriskSkjema.TildelingAsync(db, rolle, b, lov)).Id;
            mtForslag = (await HistoriskSkjema.TildelingAsync(db, rolle, a, lov, status: "foreslatt_av_ai")).Id;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, ai_forslag_versjon)
                VALUES ({Guid.NewGuid()}, 'myndighetstildeling', {mtForslag}, 'KI', now(), 'foreslatt_av_ai', 'OpenAiKompatibel:test')
                """);
        }

        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.Strukturkanttabell);

        await using var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn));
        // ---- Tellingen: 4 relasjoner + 1 medlemskap + 4 tildelinger = 9 kanter, med samme id-er. ----
        Assert.Equal(9, await etter.Strukturkanter.CountAsync());
        Assert.Equal(9, await etter.Proveniens.CountAsync(p => p.EntitetType == "strukturkant" && p.Handling == "migrert"));
        Assert.Equal(0, await HistoriskSkjema.TellAsync(etter, $"""
            SELECT count(*)::int AS "Value" FROM information_schema.tables
            WHERE table_name IN ('virksomhet_relasjoner', 'gruppe_medlemskap', 'myndighetstildelinger')
            """));

        async Task<StrukturkantEntitet> K(Guid id) => await etter.Strukturkanter.AsNoTracking().SingleAsync(k => k.Id == id);

        var r1 = await K(relHjemlet);
        Assert.Equal(("R", "klageinstans", a, b, lov, "§1"), (r1.Kategori, r1.Typekode, r1.FraVirksomhetId!.Value, r1.TilVirksomhetId!.Value, r1.HjemmelRettskildeId!.Value, r1.HjemmelEid));
        Assert.Equal(("positiv", "manuell", "[]"), (r1.Polaritet, r1.OppdagelsesKilde, r1.AvgrensningParagrafspennJson));
        var r2 = await K(relKommentar);
        Assert.Null(r2.HjemmelRettskildeId);
        Assert.Equal("bekreftet mot org-kartet", r2.KildeUtenforKorpusTekst);
        // Johanns beslutning 2026-10-07: relasjoner uten hjemmel migreres med nettside_annet — ingen bedre type gjettes.
        Assert.Equal(("nettside_annet", "sekundaer"), (r2.KildeUtenforKorpusType, r2.KildeUtenforKorpusDokumentasjon));
        Assert.Null(r2.Kommentar);
        var r3 = await K(relIngenting);
        Assert.StartsWith("(ingen kilde oppgitt", r3.KildeUtenforKorpusTekst);
        Assert.Equal("nettside_annet", r3.KildeUtenforKorpusType);
        Assert.Null(r1.KildeUtenforKorpusType); // hjemmel i korpus ⇒ ingen kildetype …
        Assert.Null(r1.KildeUtenforKorpusDokumentasjon); // … og ingen dokumentasjonsgrad.
        Assert.Equal(2, await etter.Strukturkanter.CountAsync(k => k.KildeUtenforKorpusType == "nettside_annet"));
        var r4 = await K(relBegge);
        Assert.Equal("Gjelder klagesaker", r4.Kommentar);
        Assert.Null(r4.KildeUtenforKorpusTekst);

        var m = await K(gm);
        Assert.Equal(("M", "medlem_av"), (m.Kategori, m.Typekode));
        Assert.NotNull(m.FraBegrepId); // underordnet (klassen)
        Assert.NotNull(m.TilBegrepId); // overordnet (området)
        Assert.Equal("""[{"FraEid":"§1","TilEid":null}]""", m.AvgrensningParagrafspennJson);

        var t1 = await K(mtKlasse);
        Assert.Equal(("M", "medlem_av", a, "bare kommunale vedtak"), (t1.Kategori, t1.Typekode, t1.FraVirksomhetId!.Value, t1.AvgrensningTekst));
        Assert.Equal("M", (await K(mtOmrade)).Kategori);
        Assert.Equal(("I", "innehar"), ((await K(mtRolle)).Kategori, (await K(mtRolle)).Typekode));
        var forslag = await K(mtForslag);
        Assert.Equal(("foreslatt_av_ai", "ki:OpenAiKompatibel:test"), (forslag.Status, forslag.OppdagelsesKilde));

        // ---- Organene ----
        var stortinget = await etter.Virksomheter.SingleAsync(v => v.Organisasjonsnummer == StrukturkantMigrering.StortingetOrgnr);
        Assert.Equal(("STORTINGET", "organ", "STAT", "6100", new DateOnly(2026, 10, 7)),
            (stortinget.Navn, stortinget.Aktortype, stortinget.OrganisasjonsformKode, stortinget.Sektorkode, stortinget.SistBrregSynkronisert!.Value));
        Assert.Null(stortinget.Forvaltningsniva); // docs/20 §7.2: aldri utledet fra Brreg.
        Assert.Equal("www.stortinget.no/", (await etter.VirksomhetNettsider.SingleAsync(n => n.VirksomhetId == stortinget.Id)).Url);

        var kongen = await etter.Virksomheter.SingleAsync(v => v.Navn == "Kongen i statsråd");
        Assert.Null(kongen.Organisasjonsnummer);
        Assert.Equal("organ", kongen.Aktortype);
        var hjemmel = await etter.Proveniens.SingleAsync(p => p.EntitetId == kongen.Id && p.Handling == "opprettet");
        using (var json = JsonDocument.Parse(hjemmel.KildeReferanserJson!))
        {
            var h = json.RootElement.GetProperty("hjemmel");
            Assert.Equal(StrukturkantMigrering.GrunnlovenEli, h.GetProperty("eli").GetString());
            Assert.Equal(grunnloven, h.GetProperty("rettskilde_id").GetGuid());
        }

        var begreper = await etter.Begreper.AsNoTracking()
            .Where(x => new[] { kongenIStatsradBegrep, kongenArkivert, stortingetBegrep, ukjentOrgan }.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        Assert.Equal(("virksomhet", kongen.Id), (begreper[kongenIStatsradBegrep].Begrepskategori, begreper[kongenIStatsradBegrep].VirksomhetReferanseId!.Value));
        Assert.Equal(("virksomhet", kongen.Id, "arkivert"), (begreper[kongenArkivert].Begrepskategori, begreper[kongenArkivert].VirksomhetReferanseId!.Value, begreper[kongenArkivert].Entitetsstatus));
        Assert.Equal(("virksomhet", stortinget.Id), (begreper[stortingetBegrep].Begrepskategori, begreper[stortingetBegrep].VirksomhetReferanseId!.Value));
        Assert.Null(begreper[stortingetBegrep].LovkildeId);
        // En organ-term uten kjent organ-virksomhet gjettes ikke på — den blir uavklart.
        Assert.Equal("gruppe", begreper[ukjentOrgan].Begrepskategori);
        Assert.False(await etter.Begreper.AnyAsync(x => x.Begrepskategori == "organ"));

        // Taggene flyttes fra begrep- til virksomhet-laget og peker fortsatt på samme (nå navneform-)begrep.
        var tagger = await etter.TekstTagger.AsNoTracking().Where(t => t.Id == taggKongen || t.Id == taggStortinget).ToListAsync();
        Assert.All(tagger, t => Assert.Equal("virksomhet", t.Kind));

        // Og databasen tar ikke lenger imot 'organ'.
        await Assert.ThrowsAnyAsync<Exception>(() => etter.Database.ExecuteSqlRawAsync(
            "UPDATE begreper SET begrepskategori = 'organ' WHERE \"Id\" = '" + ukjentOrgan + "'"));
    }

    /// <summary>
    /// Seed-vakten står på den STABILE nøkkelen (orgnr, CLAUDE.md §4): finnes Stortinget alt, gjenbrukes raden
    /// (bare en uavklart aktørtype settes til organ), og et organ-begrep med samme term som en eksisterende
    /// navneform slås sammen med den i stedet for å bli en dublett.
    /// </summary>
    [Fact]
    public async Task Eksisterende_Stortinget_gjenbrukes_og_lik_navneform_slaas_sammen()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_sk311b", HistoriskSkjema.Nodetypeakse);
        Guid stortingetId, navneform, organBegrep, tagg;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            var lov = R(db, "reindriftsloven", "https://lovdata.no/eli/lov/2007/06/15/40/nor").Id;
            var eier = V(db, "Landbruksdepartementet", "999999904").Id;
            stortingetId = V(db, "STORTINGET", StrukturkantMigrering.StortingetOrgnr).Id;
            navneform = B(db, "Stortinget", "virksomhet", virksomhetRef: stortingetId).Id;
            organBegrep = B(db, "stortinget", "organ", lov).Id;
            await db.SaveChangesAsync();
            tagg = Tagg(db, eier, lov, organBegrep, 0).Id;
            await db.SaveChangesAsync();
        }

        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.Strukturkanttabell);

        await using var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn));
        var stortinget = Assert.Single(await etter.Virksomheter.Where(v => v.Organisasjonsnummer == StrukturkantMigrering.StortingetOrgnr).ToListAsync());
        Assert.Equal(stortingetId, stortinget.Id);
        Assert.Equal("organ", stortinget.Aktortype);
        Assert.Null(stortinget.SistBrregSynkronisert); // raden er ikke skrevet om fra øyeblikksbildet.

        var arkivert = await etter.Begreper.AsNoTracking().SingleAsync(x => x.Id == organBegrep);
        Assert.Equal(("virksomhet", "arkivert"), (arkivert.Begrepskategori, arkivert.Entitetsstatus));
        var flyttet = await etter.TekstTagger.AsNoTracking().SingleAsync(t => t.Id == tagg);
        Assert.Equal((navneform, "virksomhet"), (flyttet.RefId!.Value, flyttet.Kind));
    }

    /// <summary>Migrasjonen kan ikke kalle Brreg; verdiene i SQL-en er lest av fra øyeblikksbildet i Seed/.
    /// Denne testen er det som holder de to like (issue #311: «HENT verdiene — ingen gjettede felt»).</summary>
    [Fact]
    public void OrganSql_bruker_verdiene_fra_Brreg_oyeblikksbildet()
    {
        var sti = Path.Combine(AppContext.BaseDirectory, "Seed", "brreg-971524960-stortinget.json");
        using var json = JsonDocument.Parse(File.ReadAllText(sti));
        var svar = json.RootElement.GetProperty("svar");
        Assert.Equal(StrukturkantMigrering.StortingetOrgnr, svar.GetProperty("organisasjonsnummer").GetString());
        Assert.Equal(StrukturkantMigrering.StortingetNavn, svar.GetProperty("navn").GetString());
        Assert.Equal(StrukturkantMigrering.StortingetOrganisasjonsform, svar.GetProperty("organisasjonsform").GetProperty("kode").GetString());
        Assert.Equal(StrukturkantMigrering.StortingetSektorkode, svar.GetProperty("institusjonellSektorkode").GetProperty("kode").GetString());
        Assert.Equal(StrukturkantMigrering.StortingetHjemmeside, svar.GetProperty("hjemmeside").GetString());
        Assert.False(svar.TryGetProperty("overordnetEnhet", out _)); // ingen overordnet enhet å koble til.
        Assert.Equal(StrukturkantMigrering.StortingetHentet, json.RootElement.GetProperty("_hentet").GetString());
    }
}
