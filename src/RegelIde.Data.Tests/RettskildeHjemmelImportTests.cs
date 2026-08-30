using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Persistering av Hjemmel-referanser (header-metadatafeltet &lt;dt class="basedOn"&gt;, 2026-08-30,
/// se RettskildeHjemmelEntitet-kommentaren) — dokumentnivå, atskilt fra de per-node løpetekst-
/// kryssreferansene <see cref="RettskildeImportTjenesteTests"/> allerede dekker.
/// <para>
/// HVER test bruker sin EGEN isolerte lov-datokode (samme mønster som
/// RettskildeImportTjenesteTests.EgenIsolertDatokode, men én per testmetode, ikke delt for hele
/// klassen): DataTestCollection deler én embedded Postgres-instans for HELE assemblyen, og import er
/// idempotent på Eli (§2.1) — to tester som brukte SAMME isolerte lov-Eli ville gjort den andres
/// resultat (stub vs. primær, ny rad vs. "Uendret") avhengig av xUnit sin (ikke-garanterte)
/// kjøringsrekkefølge på tvers av testmetoder i klassen. Én egen datokode per test fjerner denne
/// risikoen helt, uavhengig av rekkefølge.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class RettskildeHjemmelImportTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public RettskildeHjemmelImportTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string LesIsolertAlkoholloven(string datokodeSuffiks) =>
        Testdata.LesAlkoholloven().Replace("LOV-1989-06-02-27", $"LOV-{datokodeSuffiks}");

    /// <summary>
    /// Alkoholforskriften sitt Hjemmel-felt referer alle 21 til "lov/1989-06-02-27/§…" — pekes om til
    /// en isolert lov-datokode. <paramref name="egenDatokodeSuffiks"/> erstatter i TILLEGG
    /// forskriftens EGEN datokode (2005-06-08-538, 141 forekomster — data-lovdata-URL på hver
    /// paragraf, men kun siste stisegment brukes til paragrafnummer, se ParseParagraf, så dette er
    /// trygt): AknXmlSkriver serialiserer KUN Metadata+Noder, ikke Hjemler (Hjemler flyter bevisst
    /// utenfor AKN-XML-en, se RettskildeHjemmel-kommentaren) — uten en egen forskrift-Eli PER test ville
    /// «Uendret»-idempotensen (§2.1) latt kun DEN FØRSTE av flere tester som importerer «samme»
    /// forskrift faktisk sette inn hjemmelrader, og de andre stille gjenbrukt akkurat DENS rader.
    /// </summary>
    private static string LesAlkoholforskriftMedIsolertHjemmel(string lovDatokodeSuffiks, string egenDatokodeSuffiks) =>
        Testdata.LesAlkoholforskriften()
            .Replace("2005-06-08-538", egenDatokodeSuffiks)
            .Replace("1989-06-02-27", lovDatokodeSuffiks);

    [Fact]
    public async Task Import_av_forskrift_lagrer_hjemmelrader_og_oppretter_stub_for_uimportert_lov()
    {
        const string datokodeSuffiks = "2097-05-20-741";
        const string lovEli = "https://lovdata.no/eli/lov/2097/05/20/741/nor";

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(
            LesAlkoholforskriftMedIsolertHjemmel(datokodeSuffiks, "2098-01-11-901"), new DateOnly(2026, 8, 30));

        var forskriftId = await tjeneste.ImporterAsync(resultat);

        var hjemler = await db.RettskildeHjemler.Where(h => h.RettskildeId == forskriftId).ToListAsync();
        Assert.Equal(21, hjemler.Count);
        Assert.All(hjemler, h => Assert.StartsWith(lovEli + "/§", h.HjemmelEid));

        // Loven er ikke importert i denne testen -- «ingen gjettet fallback» (§3.3): fortsatt en ekte
        // HjemmelRettskildeId, men til en referanse-STUB, samme mekanisme som eksterne løpetekst-
        // referanser (§3.1 steg 6, se RettskildeImportTjenesteTests.
        // Ekstern_kryssreferanse_oppretter_referanse_stub_med_riktig_kildetype).
        var stub = await db.Rettskilder.SingleAsync(r => r.Eli == lovEli);
        Assert.Equal("referanse", stub.Importrolle);
        Assert.Equal("Utkast", stub.Status);
        Assert.All(hjemler, h => Assert.Equal(stub.Id, h.HjemmelRettskildeId));
    }

    [Fact]
    public async Task Hjemmelrad_peker_til_allerede_forfremmet_primaerkilde_nar_loven_importeres_forst()
    {
        const string datokodeSuffiks = "2097-06-21-742";

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);

        var lovenId = await tjeneste.ImporterAsync(
            LovdataKonverterer.Konverter(LesIsolertAlkoholloven(datokodeSuffiks), new DateOnly(2026, 8, 30)));

        var forskriftId = await tjeneste.ImporterAsync(
            LovdataKonverterer.Konverter(
                LesAlkoholforskriftMedIsolertHjemmel(datokodeSuffiks, "2098-02-12-902"), new DateOnly(2026, 8, 30)));

        var hjemler = await db.RettskildeHjemler.Where(h => h.RettskildeId == forskriftId).ToListAsync();
        Assert.Equal(21, hjemler.Count);
        Assert.All(hjemler, h => Assert.Equal(lovenId, h.HjemmelRettskildeId));

        var loven = await db.Rettskilder.FindAsync(lovenId);
        Assert.Equal("primaer", loven!.Importrolle);
    }

    [Fact]
    public async Task Lov_uten_basedOn_felt_lagrer_ingen_hjemmelrader()
    {
        const string datokodeSuffiks = "2097-07-22-743";

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(LesIsolertAlkoholloven(datokodeSuffiks), new DateOnly(2026, 8, 30));

        var lovenId = await tjeneste.ImporterAsync(resultat);

        var antall = await db.RettskildeHjemler.CountAsync(h => h.RettskildeId == lovenId);
        Assert.Equal(0, antall);
    }

    [Fact]
    public async Task Hjemmelrekkefolge_bevares_i_lagrede_rader()
    {
        const string datokodeSuffiks = "2097-08-23-744";
        const string lovEli = "https://lovdata.no/eli/lov/2097/08/23/744/nor";

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var resultat = LovdataKonverterer.Konverter(
            LesAlkoholforskriftMedIsolertHjemmel(datokodeSuffiks, "2098-03-13-903"), new DateOnly(2026, 8, 30));

        var forskriftId = await tjeneste.ImporterAsync(resultat);

        var forste = await db.RettskildeHjemler.SingleAsync(h => h.RettskildeId == forskriftId && h.Sorteringsrekkefolge == 0);
        Assert.Equal($"{lovEli}/§1-2", forste.HjemmelEid);

        var siste = await db.RettskildeHjemler.SingleAsync(h => h.RettskildeId == forskriftId && h.Sorteringsrekkefolge == 20);
        Assert.Equal($"{lovEli}/§10-5", siste.HjemmelEid);
    }
}
