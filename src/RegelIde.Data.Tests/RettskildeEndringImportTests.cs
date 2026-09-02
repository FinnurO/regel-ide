using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Persistering av Endring-referanser (header-metadatafeltet &lt;dt class="changesToDocuments"&gt;
/// Endrer&lt;/dt&gt;, 2026-09-02, se RettskildeEndringEntitet-kommentaren) — dokumentnivå, semantisk
/// MOTSATT av Hjemmel (RettskildeHjemmelImportTests), men strukturelt identisk import-/stub-mekanisme.
/// <para>
/// HVER test bruker sine EGNE, Guid-avledede isolerte datokoder (samme begrunnelse som
/// RettskildeRaaInnholdImportTests.NyIsolertDatokode).
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class RettskildeEndringImportTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public RettskildeEndringImportTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NyIsolertDatokode()
    {
        var b = Guid.NewGuid().ToByteArray();
        var aar = 2100 + (b[0] % 90);
        var maaned = 1 + (b[1] % 12);
        var dag = 1 + (b[2] % 28);
        var lopenummer = BitConverter.ToUInt16(b, 3) + 1;
        return $"LOV-{aar:D4}-{maaned:D2}-{dag:D2}-{lopenummer}";
    }

    [Fact]
    public async Task Import_lagrer_endringsrader_og_oppretter_stub_for_uimportert_endret_dokument()
    {
        var egenDatokode = NyIsolertDatokode();
        var maalDatokode1 = NyIsolertDatokode();
        var maalDatokode2 = NyIsolertDatokode();
        var maalEli1 = LovdataIdentifikatorer.AvledEliFraDatokode(maalDatokode1, out _);
        var maalEli2 = LovdataIdentifikatorer.AvledEliFraDatokode(maalDatokode2, out _);

        // Ekte innhold i alkohollovens <dd class="changesToDocuments">: "lov/1927-04-05" og
        // "lov/1900-05-31-5" — pekes om til to egne, isolerte mål-datokoder.
        var html = Testdata.LesAlkoholloven()
            .Replace("LOV-1989-06-02-27", egenDatokode)
            .Replace("lov/1927-04-05", "lov/" + maalDatokode1[4..])
            .Replace("lov/1900-05-31-5", "lov/" + maalDatokode2[4..]);

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2));
        var id = await tjeneste.ImporterAsync(resultat);

        var endringer = await db.RettskildeEndringer
            .Where(e => e.RettskildeId == id).OrderBy(e => e.Sorteringsrekkefolge).ToListAsync();
        Assert.Equal(2, endringer.Count);
        Assert.Equal(maalEli1, endringer[0].EndringEid);
        Assert.Equal(0, endringer[0].Sorteringsrekkefolge);
        Assert.Equal(maalEli2, endringer[1].EndringEid);
        Assert.Equal(1, endringer[1].Sorteringsrekkefolge);

        // Verken mål-dokumentet er importert i denne testen -- «ingen gjettet fallback» (§3.3):
        // fortsatt en ekte EndringRettskildeId, men til en referanse-STUB, samme mekanisme som
        // Hjemmel (RettskildeHjemmelImportTests) og eksterne løpetekst-referanser.
        var stub1 = await db.Rettskilder.SingleAsync(r => r.Eli == maalEli1);
        Assert.Equal("referanse", stub1.Importrolle);
        Assert.Equal("Utkast", stub1.Status);
        Assert.Equal(stub1.Id, endringer[0].EndringRettskildeId);

        var stub2 = await db.Rettskilder.SingleAsync(r => r.Eli == maalEli2);
        Assert.Equal(stub2.Id, endringer[1].EndringRettskildeId);
    }

    [Fact]
    public async Task Endringsrad_peker_til_allerede_forfremmet_primaerkilde_nar_maldokumentet_importeres_forst()
    {
        var egenDatokode = NyIsolertDatokode();
        var maalDatokode1 = NyIsolertDatokode();
        var maalDatokode2 = NyIsolertDatokode();

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);

        var maalId1 = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(
            Testdata.LesAlkoholloven().Replace("LOV-1989-06-02-27", maalDatokode1), new DateOnly(2026, 9, 2)));
        var maalId2 = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(
            Testdata.LesAlkoholloven().Replace("LOV-1989-06-02-27", maalDatokode2), new DateOnly(2026, 9, 2)));

        var html = Testdata.LesAlkoholloven()
            .Replace("LOV-1989-06-02-27", egenDatokode)
            .Replace("lov/1927-04-05", "lov/" + maalDatokode1[4..])
            .Replace("lov/1900-05-31-5", "lov/" + maalDatokode2[4..]);

        var id = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)));

        var endringer = await db.RettskildeEndringer
            .Where(e => e.RettskildeId == id).OrderBy(e => e.Sorteringsrekkefolge).ToListAsync();
        Assert.Equal(2, endringer.Count);
        Assert.Equal(maalId1, endringer[0].EndringRettskildeId);
        Assert.Equal(maalId2, endringer[1].EndringRettskildeId);

        var maal1 = await db.Rettskilder.SingleAsync(r => r.Id == maalId1);
        Assert.Equal("primaer", maal1.Importrolle);
    }

    [Fact]
    public async Task Dokument_uten_changesToDocuments_innhold_lagrer_ingen_endringsrader()
    {
        var egenDatokode = NyIsolertDatokode();
        // forvaltningsloven har ingen ekte innhold i changesToDocuments -- bekreftet mot fixturen
        // (data/kilder/raw-lovdata/forvaltningsloven-LOV-1967-02-10.html).
        var html = Testdata.LesForvaltningsloven().Replace("LOV-1967-02-10", egenDatokode);

        await using var db = _fixture.NyDbContext();
        var id = await new RettskildeImportTjeneste(db)
            .ImporterAsync(LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)));

        var antall = await db.RettskildeEndringer.CountAsync(e => e.RettskildeId == id);
        Assert.Equal(0, antall);
    }
}
