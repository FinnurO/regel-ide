using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Sveipefunksjonen (<see cref="VirksomhetKandidatSveipTjeneste"/>, docs/20 §5/kravspek §4.2 pkt. 1/2)
/// mot ekte embedded Postgres og EKTE Lovdata-tekst (advokatloven, se <see cref="Testdata.LesAdvokatloven"/>).
/// Testcaset følger oppgavens eget eksempel: sveip for Advokattilsynet skal finne treffet i § 4 første
/// ledd ("Advokattilsynet utsteder advokatbevilling").
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetKandidatSveipTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetKandidatSveipTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> ImporterAdvokatlovenAsync(RegelIdeDbContext db) =>
        await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAdvokatloven(), new DateOnly(2026, 8, 22)));

    private static async Task<Virksomhet> OpprettAdvokattilsynetMedNavneformerAsync(RegelIdeDbContext db)
    {
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = "Advokattilsynet" };
        db.Virksomheter.Add(virksomhet);
        db.Begreper.AddRange(
            new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = virksomhet.Id, VirksomhetId = null,
                Term = "Advokattilsynet", Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            },
            new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = virksomhet.Id, VirksomhetId = null,
                Term = "Tilsynsrådet for advokatvirksomhet", Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        await db.SaveChangesAsync();
        return virksomhet;
    }

    [Fact]
    public async Task Finner_treffet_i_paragraf_4_forste_ledd()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await ImporterAdvokatlovenAsync(db);
        var virksomhet = await OpprettAdvokattilsynetMedNavneformerAsync(db);

        var sveip = new VirksomhetKandidatSveipTjeneste(db, new VirksomhetKandidatTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db))));
        var resultat = await sveip.SveipAsync(virksomhet.Id, "sveip");

        Assert.True(resultat.AntallTreffFunnet > 0);
        Assert.Equal(resultat.AntallTreffFunnet, resultat.AntallNyeKandidater); // Første kjøring: alle er nye.

        // Bladteksten bærer med seg Lovdatas leddnummerering som løpetekst, f.eks. "(1) Advokattilsynet
        // utsteder advokatbevilling. …" — treffet starter derfor IKKE på tegn 0, men rett etter "(1) ".
        var paragraf4Ledd1 = await db.RettskildeNoder.FirstAsync(
            n => n.RettskildeId == rettskildeId && n.Tekst != null && n.Tekst.Contains("Advokattilsynet utsteder advokatbevilling"));
        var kandidat = await db.VirksomhetKandidater.SingleAsync(
            k => k.VirksomhetId == virksomhet.Id && k.RettskildeId == rettskildeId && k.NodeEid == paragraf4Ledd1.Eid);

        Assert.Equal("Venter", kandidat.Status);
        Assert.Equal(paragraf4Ledd1.Tekst!.IndexOf("Advokattilsynet", StringComparison.Ordinal), kandidat.StartOffset);
        Assert.Equal("Advokattilsynet", paragraf4Ledd1.Tekst[kandidat.StartOffset..kandidat.EndOffset]);
    }

    [Fact]
    public async Task Begge_navneformer_gir_treff()
    {
        // "Advokattilsynet" og den historiske navneformen "Tilsynsrådet for advokatvirksomhet" skal
        // BEGGE trigge kandidater — presiseringen i oppgaveteksten om at sveipet må lete etter ALLE
        // navneform-Begrep-rader, ikke bare Virksomhet.Navn.
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await ImporterAdvokatlovenAsync(db);
        var virksomhet = await OpprettAdvokattilsynetMedNavneformerAsync(db);

        var sveip = new VirksomhetKandidatSveipTjeneste(db, new VirksomhetKandidatTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db))));
        await sveip.SveipAsync(virksomhet.Id, "sveip");

        var kandidater = await db.VirksomhetKandidater
            .Where(k => k.VirksomhetId == virksomhet.Id && k.RettskildeId == rettskildeId)
            .ToListAsync();
        var noderPerEid = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).ToDictionaryAsync(n => n.Eid);

        var funnedeTekster = kandidater.Select(k => noderPerEid[k.NodeEid].Tekst![k.StartOffset..k.EndOffset]).Distinct().ToList();
        Assert.Contains("Advokattilsynet", funnedeTekster);
        Assert.Contains("Tilsynsrådet for advokatvirksomhet", funnedeTekster);
    }

    [Fact]
    public async Task Gjentatt_sveip_gir_ingen_nye_kandidater()
    {
        await using var db = _fixture.NyDbContext();
        await ImporterAdvokatlovenAsync(db);
        var virksomhet = await OpprettAdvokattilsynetMedNavneformerAsync(db);

        var sveip = new VirksomhetKandidatSveipTjeneste(db, new VirksomhetKandidatTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db))));
        var forste = await sveip.SveipAsync(virksomhet.Id, "sveip");
        var andre = await sveip.SveipAsync(virksomhet.Id, "sveip");

        Assert.True(forste.AntallNyeKandidater > 0);
        Assert.Equal(forste.AntallTreffFunnet, andre.AntallTreffFunnet); // Samme treff finnes fortsatt i teksten.
        Assert.Equal(0, andre.AntallNyeKandidater); // Men ingen av dem er NYE andre gang.
    }

    [Fact]
    public async Task Kandidat_fra_sveip_kan_godkjennes_til_ekte_tagg_pa_riktig_intervall()
    {
        // End-to-end-testcaset fra oppgaveteksten: sveip → Venter-kandidat i § 4 første ledd →
        // godkjenning → ekte TekstTagg (kind="begrep", RefId=navneform-Begrep) på RIKTIG tegn-intervall.
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await ImporterAdvokatlovenAsync(db);
        var virksomhet = await OpprettAdvokattilsynetMedNavneformerAsync(db);

        var kø = new VirksomhetKandidatTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)));
        var sveip = new VirksomhetKandidatSveipTjeneste(db, kø);
        await sveip.SveipAsync(virksomhet.Id, "sveip");

        var paragraf4Ledd1 = await db.RettskildeNoder.FirstAsync(
            n => n.RettskildeId == rettskildeId && n.Tekst != null && n.Tekst.Contains("Advokattilsynet utsteder advokatbevilling"));
        var forventetStart = paragraf4Ledd1.Tekst!.IndexOf("Advokattilsynet", StringComparison.Ordinal);
        var kandidat = await db.VirksomhetKandidater.SingleAsync(
            k => k.VirksomhetId == virksomhet.Id && k.NodeEid == paragraf4Ledd1.Eid && k.StartOffset == forventetStart);

        var godkjent = await kø.GodkjennAsync(kandidat.Id, "Kari Jurist");
        Assert.Equal("Godkjent", godkjent!.Status);

        // Filtrert på VirksomhetId (fersk Guid per test) — DB-en er DELT mellom alle tester i
        // samlingen (ICollectionFixture), og advokatloven-importen er idempotent per ELI, så samme
        // node kan ha tagger fra andre testmetoder (f.eks. andre virksomheter) liggende fra før.
        var tagg = await db.TekstTagger.SingleAsync(
            t => t.RettskildeId == rettskildeId && t.NodeEid == paragraf4Ledd1.Eid && t.VirksomhetId == virksomhet.Id);
        Assert.Equal("begrep", tagg.Kind);
        Assert.Equal("Advokattilsynet", tagg.QuoteExact);
        Assert.NotNull(tagg.RefId);
        var navneform = await db.Begreper.SingleAsync(b => b.Id == tagg.RefId);
        Assert.Equal("virksomhet", navneform.Begrepskategori);
        Assert.Equal(virksomhet.Id, navneform.VirksomhetReferanseId);
        Assert.Equal("Advokattilsynet", navneform.Term);
    }

    [Fact]
    public async Task Sveip_hopper_over_en_ANNEN_virksomhets_lokale_rettskilde()
    {
        // Johanns tilbakemelding 2026-08-22 (bekreftet reelt i produksjonsdata): sveip for Agder
        // fylkeskommune traff en rettskilde eid av Bergen kommune. Sveipet skal KUN gjelde delte/
        // nasjonale rettskilder (VirksomhetId == null) pluss virksomhetens EGNE — en annen virksomhets
        // lokale rettskilde skal ALDRI gi treff, selv om navneformen faktisk forekommer i teksten.
        await using var db = _fixture.NyDbContext();
        var enAnnenVirksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = "En annen virksomhet AS" };
        db.Virksomheter.Add(enAnnenVirksomhet);
        await db.SaveChangesAsync();

        // Importert som EN ANNEN virksomhets EGEN, lokale rettskilde — ikke delt/nasjonal.
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAdvokatloven(), new DateOnly(2026, 8, 22)), enAnnenVirksomhet.Id);
        var virksomhet = await OpprettAdvokattilsynetMedNavneformerAsync(db);

        var sveip = new VirksomhetKandidatSveipTjeneste(db, new VirksomhetKandidatTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db))));
        await sveip.SveipAsync(virksomhet.Id, "sveip");

        var kandidaterIDenLokaleRettskilden = await db.VirksomhetKandidater
            .Where(k => k.VirksomhetId == virksomhet.Id && k.RettskildeId == rettskildeId)
            .ToListAsync();
        Assert.Empty(kandidaterIDenLokaleRettskilden);
    }

    [Fact]
    public async Task Sveip_finner_treff_i_egen_lokal_rettskilde()
    {
        // Samme scoping-regel motsatt vei: EGNE lokale rettskilder skal fortsatt gi treff.
        await using var db = _fixture.NyDbContext();
        var virksomhet = await OpprettAdvokattilsynetMedNavneformerAsync(db);

        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAdvokatloven(), new DateOnly(2026, 8, 22)), virksomhet.Id);

        var sveip = new VirksomhetKandidatSveipTjeneste(db, new VirksomhetKandidatTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db))));
        await sveip.SveipAsync(virksomhet.Id, "sveip");

        var kandidaterIEgenRettskilde = await db.VirksomhetKandidater
            .Where(k => k.VirksomhetId == virksomhet.Id && k.RettskildeId == rettskildeId)
            .ToListAsync();
        Assert.NotEmpty(kandidaterIEgenRettskilde);
    }

    [Fact]
    public async Task Kaster_hvis_virksomheten_ikke_har_noen_navneform()
    {
        await using var db = _fixture.NyDbContext();
        await ImporterAdvokatlovenAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = "Uten navneform AS" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var sveip = new VirksomhetKandidatSveipTjeneste(db, new VirksomhetKandidatTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db))));
        await Assert.ThrowsAsync<ArgumentException>(() => sveip.SveipAsync(virksomhet.Id, "sveip"));
    }

    [Fact]
    public async Task Kaster_hvis_virksomheten_ikke_finnes()
    {
        await using var db = _fixture.NyDbContext();
        await ImporterAdvokatlovenAsync(db);

        var sveip = new VirksomhetKandidatSveipTjeneste(db, new VirksomhetKandidatTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db))));
        await Assert.ThrowsAsync<ArgumentException>(() => sveip.SveipAsync(Guid.NewGuid(), "sveip"));
    }
}
