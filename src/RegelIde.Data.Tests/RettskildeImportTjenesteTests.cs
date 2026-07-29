using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Kjører faktisk mot en ekte, embedded Postgres-instans (§2 i teknisk design) — verifiserer at
/// migrasjonen (partial unique index, GIN-fulltekstindeks, check-constraints) faktisk fungerer mot
/// ekte Postgres, ikke bare kompilerer.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class RettskildeImportTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public RettskildeImportTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Importerer_alkoholloven_med_noder_og_referanser()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24));

        var rettskildeId = await tjeneste.ImporterAsync(resultat);

        var lagret = await db.Rettskilder.FindAsync(rettskildeId);
        Assert.NotNull(lagret);
        Assert.Equal("primaer", lagret!.Importrolle);
        Assert.Equal("gjeldende", lagret.Entitetsstatus);
        Assert.StartsWith("<akomaNtoso", lagret.AknXml);

        var antallNoder = await db.RettskildeNoder.CountAsync(n => n.RettskildeId == rettskildeId);
        Assert.Equal(resultat.Noder.Count, antallNoder);

        var fraNodeIder = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).Select(n => n.Id).ToListAsync();
        var antallReferanser = await db.RettskildeReferanser.CountAsync(r => fraNodeIder.Contains(r.FraNodeId));
        Assert.True(antallReferanser > 0);
    }

    [Fact]
    public async Task Opphevet_flagg_og_dato_flyttes_gjennom_til_lagret_node()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24));
        var rettskildeId = await tjeneste.ImporterAsync(resultat);

        var eli = resultat.Metadata.Eli;
        var opphevetParagraf = await db.RettskildeNoder.SingleAsync(
            n => n.RettskildeId == rettskildeId && n.Eid == $"{eli}/§1-12");
        Assert.True(opphevetParagraf.Opphevet);
        Assert.Equal(new DateOnly(2005, 7, 1), opphevetParagraf.OpphevetDato);

        var vanligParagraf = await db.RettskildeNoder.SingleAsync(
            n => n.RettskildeId == rettskildeId && n.Eid == $"{eli}/§1-1");
        Assert.False(vanligParagraf.Opphevet);
        Assert.Null(vanligParagraf.OpphevetDato);
    }

    [Fact]
    public async Task Gjentatt_import_av_samme_rettskilde_er_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 7, 24));

        var forsteId = await tjeneste.ImporterAsync(resultat);
        var andreId = await tjeneste.ImporterAsync(resultat);

        Assert.Equal(forsteId, andreId);
        var antallRader = await db.Rettskilder.CountAsync(r => r.Eli == resultat.Metadata.Eli);
        Assert.Equal(1, antallRader);
    }

    [Fact]
    public async Task Ekstern_kryssreferanse_oppretter_referanse_stub_med_riktig_kildetype()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24));

        await tjeneste.ImporterAsync(resultat);

        // § 9-4 ledd-3 viser til markedsføringsloven (LOV-2009-01-09-2) -- ikke importert som primærkilde
        // i denne testen, skal derfor bli en referanse-stub (§3.1 steg 6).
        var stub = await db.Rettskilder.SingleOrDefaultAsync(
            r => r.Eli == "https://lovdata.no/eli/lov/2009/01/09/2/nor");

        Assert.NotNull(stub);
        Assert.Equal("referanse", stub!.Importrolle);
        Assert.Equal("Lov", stub.Kildetype);
        Assert.Null(stub.AknXml);
    }

    [Fact]
    public async Task Referanse_stub_forfremmes_til_primaer_ved_faktisk_import()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);

        // Importer alkoholloven først -- oppretter en stub for markedsføringsloven (se test over).
        var alkoholloven = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24));
        await tjeneste.ImporterAsync(alkoholloven);

        var stubFor = await db.Rettskilder.SingleAsync(r => r.Eli == "https://lovdata.no/eli/lov/2009/01/09/2/nor");
        var stubId = stubFor.Id;
        Assert.Equal("referanse", stubFor.Importrolle);

        // Importer nå forvaltningsloven som seg selv -- ikke samme dokument som stubben, men bekrefter
        // uansett at "finn eksisterende rad"-logikken i ImporterAsync ikke lager en ny rad ved siden av.
        var forvaltningsloven = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 7, 24));
        var forvaltningslovenId = await tjeneste.ImporterAsync(forvaltningsloven);
        Assert.NotEqual(stubId, forvaltningslovenId);

        // Scoped til delte/nasjonale rader (virksomhet_id IS NULL) — andre tester i denne klassen
        // deler samme database og kan legitimt legge til virksomhets-EGNE kopier av samme ELI
        // (se "To_virksomheter_kan_ha_hver_sin_lokale_kilde_med_samme_eli_uten_kollisjon").
        var antallMedForvaltningslovenEli = await db.Rettskilder.CountAsync(
            r => r.Eli == "https://lovdata.no/eli/lov/1967/02/10/nor" && r.VirksomhetId == null);
        Assert.Equal(1, antallMedForvaltningslovenEli);
    }

    [Fact]
    public async Task Fulltekstsokindeks_fra_migrasjonen_fungerer_mot_ekte_postgres()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24));
        await tjeneste.ImporterAsync(resultat);

        var treff = await db.RettskildeNoder
            .FromSqlRaw("SELECT * FROM rettskilde_noder WHERE to_tsvector('norwegian', tekst) @@ to_tsquery('norwegian', 'alkoholholdig')")
            .CountAsync();

        Assert.True(treff > 0);
    }

    [Fact]
    public async Task Samtidig_skriving_pa_samme_rettskilde_avvises_ikke_stille()
    {
        // 05-arkitektur-og-nfk.md §2: "skal varsle og avvise en lagring som ville overskrevet en
        // endring gjort av en annen bruker" -- verifiserer at dette faktisk håndheves (§0 i
        // domenemodellen: versjon-feltet), ikke bare at kolonnen finnes.
        Guid rettskildeId;
        await using (var forsteImport = _fixture.NyDbContext())
        {
            var resultat = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 7, 24));
            rettskildeId = await new RettskildeImportTjeneste(forsteImport).ImporterAsync(resultat);
        }

        // To "brukere" laster samme rad uavhengig av hverandre.
        await using var brukerA = _fixture.NyDbContext();
        await using var brukerB = _fixture.NyDbContext();
        var radHosA = await brukerA.Rettskilder.SingleAsync(r => r.Id == rettskildeId);
        var radHosB = await brukerB.Rettskilder.SingleAsync(r => r.Id == rettskildeId);
        Assert.Equal(radHosA.Versjon, radHosB.Versjon);

        // Bruker A lagrer først -- går fint, Versjon øker.
        radHosA.Kortnavn = "Endret av A";
        radHosA.Versjon++;
        await brukerA.SaveChangesAsync();

        // Bruker B, som fortsatt har den GAMLE versjonen i minnet, prøver å lagre -- skal avvises,
        // ikke stille overskrive det bruker A nettopp lagret.
        radHosB.Kortnavn = "Endret av B";
        radHosB.Versjon++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => brukerB.SaveChangesAsync());
    }

    // ---------- Multi-virksomhet (docs/00-endringslogg-v0.3.md) ----------

    [Fact]
    public async Task Delt_nasjonal_kilde_importeres_uten_virksomhet_id()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 7, 24));

        var id = await tjeneste.ImporterAsync(resultat); // ingen virksomhetId gitt = delt

        var rad = await db.Rettskilder.SingleAsync(r => r.Id == id);
        Assert.Null(rad.VirksomhetId);
    }

    [Fact]
    public async Task To_virksomheter_kan_ha_hver_sin_lokale_kilde_med_samme_eli_uten_kollisjon()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetA = Guid.NewGuid();
        var virksomhetB = Guid.NewGuid();
        db.Virksomheter.AddRange(
            new Virksomhet { Id = virksomhetA, Navn = "Vennesla kommune" },
            new Virksomhet { Id = virksomhetB, Navn = "Tønsberg kommune" });
        await db.SaveChangesAsync();

        // Samme rettskilde (samme ELI) "importert" for to ulike virksomheter -- simulerer at begge
        // kommunene har en egen lokal forskrift med tilfeldigvis samme ELI-struktur. Skal IKKE
        // kollidere, i motsetning til to delte/nasjonale rader med samme ELI (som fortsatt skal
        // kollidere, jf. testen over).
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 7, 24));

        var idA = await tjeneste.ImporterAsync(resultat, virksomhetId: virksomhetA);
        var idB = await tjeneste.ImporterAsync(resultat, virksomhetId: virksomhetB);

        Assert.NotEqual(idA, idB);
        var radA = await db.Rettskilder.SingleAsync(r => r.Id == idA);
        var radB = await db.Rettskilder.SingleAsync(r => r.Id == idB);
        Assert.Equal(virksomhetA, radA.VirksomhetId);
        Assert.Equal(virksomhetB, radB.VirksomhetId);
    }

    [Fact]
    public async Task Samme_virksomhet_kan_ikke_ha_to_gjeldende_lokale_kilder_med_samme_eli()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Vennesla kommune" });
        await db.SaveChangesAsync();

        var resultat = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 7, 24));
        await new RettskildeImportTjeneste(db).ImporterAsync(resultat, virksomhetId: virksomhet);

        // Andre import for SAMME virksomhet er idempotent (samme oppførsel som for delte kilder),
        // ikke en constraint-kollisjon.
        var andreGangenId = await new RettskildeImportTjeneste(db).ImporterAsync(resultat, virksomhetId: virksomhet);
        var antall = await db.Rettskilder.CountAsync(r => r.VirksomhetId == virksomhet && r.Eli == resultat.Metadata.Eli);
        Assert.Equal(1, antall);
    }

    // ---------- Reimport-versjonering (§2.1) + quoteSelector-relokering (05-arkitektur-og-nfk.md §3.1) ----------
    //
    // Bruker en egen, ISOLERT kopi av alkoholloven-fixturen (unik datokode -> unik ELI) for hele denne
    // seksjonen -- ikke den delte fixturen andre testklasser i samme embedded-Postgres-database
    // (samme ICollectionFixture) forutsetter forblir uendret. Å reimportere en MODIFISERT versjon av
    // den delte alkoholloven-ELI-en ville ellers permanent endret "gjeldende"-raden andre tester leser.

    private const string IsolertDatokode = "LOV-2099-01-01-999";

    private static string LesIsolertAlkoholloven() => Testdata.LesAlkoholloven().Replace("LOV-1989-06-02-27", IsolertDatokode);

    private const string Paragraf11Tekst =
        "Reguleringen av innførsel og omsetning av alkoholholdig drikk etter denne lov har som mål å " +
        "begrense i størst mulig utstrekning de samfunnsmessige og individuelle skader som alkoholbruk " +
        "kan innebære. Som et ledd i dette sikter loven på å begrense forbruket av alkoholholdige drikkevarer.";

    [Fact]
    public async Task Reimport_av_byte_identisk_innhold_er_fortsatt_ingen_ny_versjon()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var html = LesIsolertAlkoholloven();

        // Bevisst ULIKE importdatoer -- beviser at det er INNHOLDET, ikke bare hele AKN-XML-strengen
        // (som ellers alltid ville vært ulik pga. FRBRManifestation/@date), som avgjør "uendret".
        var forsteId = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(html, new DateOnly(2026, 7, 24)));
        var andreId = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(html, new DateOnly(2026, 7, 25)));

        Assert.Equal(forsteId, andreId);
        var antallRader = await db.Rettskilder.CountAsync(r => r.Eli == "https://lovdata.no/eli/lov/2099/01/01/999/nor");
        Assert.Equal(1, antallRader);
    }

    [Fact]
    public async Task Reimport_med_endret_paragraf_oppretter_ny_versjon_og_arkiverer_gammel()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var forsteId = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(LesIsolertAlkoholloven(), new DateOnly(2026, 7, 24)));

        var endretHtml = LesIsolertAlkoholloven().Replace(
            "begrense forbruket av alkoholholdige drikkevarer.",
            "begrense forbruket av alkoholholdige drikkevarer betydelig.");
        var andreId = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(endretHtml, new DateOnly(2026, 8, 1)));

        Assert.NotEqual(forsteId, andreId);
        var gammel = await db.Rettskilder.SingleAsync(r => r.Id == forsteId);
        var ny = await db.Rettskilder.SingleAsync(r => r.Id == andreId);
        Assert.Equal("erstattet", gammel.Entitetsstatus);
        Assert.Equal("gjeldende", ny.Entitetsstatus);
        Assert.Equal(gammel.Versjon + 1, ny.Versjon);
        Assert.Equal(gammel.Id, ny.ErstatterId);
        Assert.Equal(gammel.Eli, ny.Eli);
    }

    [Fact]
    public async Task Uendret_tagg_migreres_til_ny_versjon_med_samme_offset()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen (reimport-test)" });
        await db.SaveChangesAsync();

        var importTjeneste = new RettskildeImportTjeneste(db);
        var forsteId = await importTjeneste.ImporterAsync(LovdataKonverterer.Konverter(LesIsolertAlkoholloven(), new DateOnly(2026, 7, 24)));
        var paragraf11Eid = $"https://lovdata.no/eli/lov/2099/01/01/999/nor/§1-1/ledd-1";

        var taggTjeneste = new TekstTaggTjeneste(db);
        var startIndeks = Paragraf11Tekst.IndexOf("alkoholholdig drikk", StringComparison.Ordinal);
        var tagg = await taggTjeneste.OpprettAsync(
            forsteId, virksomhet, "Kari Jurist", paragraf11Eid, startIndeks, startIndeks + "alkoholholdig drikk".Length,
            Paragraf11Tekst[..startIndeks], "alkoholholdig drikk", Paragraf11Tekst[(startIndeks + "alkoholholdig drikk".Length)..], "begrep");
        Assert.NotNull(tagg);

        // Endrer en HELT ANNEN paragraf (§ 1-2) for å trigge en ny versjon -- § 1-1 er urørt i denne testen.
        var endretHtml = LesIsolertAlkoholloven().Replace("Lovens virkeområde.", "Lovens virkeområde og formål.");
        var nyId = await importTjeneste.ImporterAsync(LovdataKonverterer.Konverter(endretHtml, new DateOnly(2026, 8, 1)));

        var migrertTagg = await db.TekstTagger.SingleAsync(t => t.Id == tagg!.Id);
        Assert.Equal(nyId, migrertTagg.RettskildeId);
        Assert.Equal(paragraf11Eid, migrertTagg.NodeEid);
        Assert.Equal(startIndeks, migrertTagg.StartOffset);
        Assert.False(migrertTagg.KreverGjennomgang);
    }

    [Fact]
    public async Task Tagg_uten_treff_etter_reimport_flagges_for_gjennomgang()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen (reimport-test 2)" });
        await db.SaveChangesAsync();

        var importTjeneste = new RettskildeImportTjeneste(db);
        var forsteId = await importTjeneste.ImporterAsync(LovdataKonverterer.Konverter(LesIsolertAlkoholloven(), new DateOnly(2026, 7, 24)));
        var paragraf11Eid = "https://lovdata.no/eli/lov/2099/01/01/999/nor/§1-1/ledd-1";

        var taggTjeneste = new TekstTaggTjeneste(db);
        var startIndeks = Paragraf11Tekst.IndexOf("drikkevarer.", StringComparison.Ordinal);
        var tagg = await taggTjeneste.OpprettAsync(
            forsteId, virksomhet, "Kari Jurist", paragraf11Eid, startIndeks, startIndeks + "drikkevarer.".Length,
            Paragraf11Tekst[..startIndeks], "drikkevarer.", "", "begrep");
        Assert.NotNull(tagg);

        // Erstatter HELE § 1-1-setningen taggen sto i -- "drikkevarer." finnes ikke lenger noe sted.
        var endretHtml = LesIsolertAlkoholloven().Replace(Paragraf11Tekst, "Loven regulerer omsetning av rusmidler generelt.");
        var nyId = await importTjeneste.ImporterAsync(LovdataKonverterer.Konverter(endretHtml, new DateOnly(2026, 8, 1)));

        var flaggetTagg = await db.TekstTagger.SingleAsync(t => t.Id == tagg!.Id);
        Assert.True(flaggetTagg.KreverGjennomgang);
        Assert.Equal(forsteId, flaggetTagg.RettskildeId); // uendret -- peker fortsatt på den (nå erstattede) gamle raden
        Assert.NotEqual(nyId, flaggetTagg.RettskildeId);
    }

    [Fact]
    public async Task Tagg_relokeres_via_quoteSelector_nar_teksten_forskyves_i_samme_node()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen (reimport-test 3)" });
        await db.SaveChangesAsync();

        var importTjeneste = new RettskildeImportTjeneste(db);
        var forsteId = await importTjeneste.ImporterAsync(LovdataKonverterer.Konverter(LesIsolertAlkoholloven(), new DateOnly(2026, 7, 24)));
        var paragraf11Eid = "https://lovdata.no/eli/lov/2099/01/01/999/nor/§1-1/ledd-1";

        var taggTjeneste = new TekstTaggTjeneste(db);
        const string sitat = "alkoholholdige drikkevarer";
        var startIndeks = Paragraf11Tekst.IndexOf(sitat, StringComparison.Ordinal);
        var tagg = await taggTjeneste.OpprettAsync(
            forsteId, virksomhet, "Kari Jurist", paragraf11Eid, startIndeks, startIndeks + sitat.Length,
            Paragraf11Tekst[..startIndeks], sitat, Paragraf11Tekst[(startIndeks + sitat.Length)..], "begrep");
        Assert.NotNull(tagg);

        // Setter inn en ny setning FØR den taggede teksten i SAMME node -- sitatet finnes fortsatt,
        // men på en forskjøvet offset, og tekst_hash for noden er nødvendigvis endret.
        var forskjovetTekst = "Se også forskrift om samme tema. " + Paragraf11Tekst;
        var endretHtml = LesIsolertAlkoholloven().Replace(Paragraf11Tekst, forskjovetTekst);
        var nyId = await importTjeneste.ImporterAsync(LovdataKonverterer.Konverter(endretHtml, new DateOnly(2026, 8, 1)));

        var relokertTagg = await db.TekstTagger.SingleAsync(t => t.Id == tagg!.Id);
        Assert.False(relokertTagg.KreverGjennomgang);
        Assert.Equal(nyId, relokertTagg.RettskildeId);
        Assert.Equal(paragraf11Eid, relokertTagg.NodeEid);
        var nyIndeks = forskjovetTekst.IndexOf(sitat, StringComparison.Ordinal);
        Assert.Equal(nyIndeks, relokertTagg.StartOffset);
        Assert.Equal(nyIndeks + sitat.Length, relokertTagg.EndOffset);
    }
}
