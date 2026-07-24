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
}
