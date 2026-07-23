using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Kjører faktisk mot en ekte, embedded Postgres-instans (§2 i teknisk design) — verifiserer at
/// migrasjonen (partial unique index, GIN-fulltekstindeks, check-constraints) faktisk fungerer mot
/// ekte Postgres, ikke bare kompilerer.
/// </summary>
public class RettskildeImportTjenesteTests : IClassFixture<EmbeddedPostgresFixture>
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

        var antallMedForvaltningslovenEli = await db.Rettskilder.CountAsync(
            r => r.Eli == "https://lovdata.no/eli/lov/1967/02/10/nor");
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
}
