using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, feillogg-runden, 2026-09-10, GitHub-issue #201 del A] <see cref="LovdataImportstatusTjeneste.OppdaterAsync"/>s
/// nye <c>kjoringId</c>-parameter — verifiserer isolert (ingen ekte Lovdata-nettverkskall, til forskjell
/// fra <see cref="LovdataFullimportTjenesteTests"/>s <c>LiveIntegration</c>-test) at
/// <c>lovdata_importstatus_historikk</c> KUN skrives ved feil OG når en kjøring faktisk er oppgitt, og
/// at den er en ekte LOGG (én rad per forsøk) mens <c>lovdata_importstatus</c> selv forblir en UPSERT
/// (én rad per datokode, siste forsøk) — selve poenget med hvorfor de to tabellene finnes side om side.
/// <para>
/// <c>lovdata_resynk_kjoringer</c>/<c>lovdata_importstatus</c>/<c>lovdata_importstatus_historikk</c> er
/// globale tabeller delt for hele testassemblyens embedded Postgres (se
/// LovdataResynkKjoringTjenesteTests for samme resonnement) — hver test her rydder derfor selv FØR den
/// setter opp sitt eget scenario, og bruker unike datokoder (Guid-suffiks) for å unngå kollisjon med
/// andre tester i samme collection som IKKE rydder (f.eks. LovdataFullimportTjenesteTests' egen
/// LiveIntegration-test, ekskludert fra standard kjøring, men deler likevel basen når den kjøres).
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class LovdataImportstatusTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public LovdataImportstatusTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyKjoringAsync(RegelIdeDbContext db) =>
        await new LovdataResynkKjoringTjeneste(db).StartKjoringAsync(LovdataResynkUtlost.Manuell, "Kari Saksbehandler");

    [Fact]
    public async Task OppdaterAsync_ved_feil_med_kjoringId_skriver_bade_upsert_og_historikk()
    {
        await using var db = _fixture.NyDbContext();
        var kjoringId = await NyKjoringAsync(db);
        var datokode = $"LOV-TEST-{Guid.NewGuid():N}";
        var tjeneste = new LovdataImportstatusTjeneste(db);

        await tjeneste.OppdaterAsync(
            datokode, "lov", "Test-lov", "https://lovdata.no/eli/lov/test", importert: false,
            rettskildeId: null, feilmelding: "Uventet parse-feil.", kjoringId: kjoringId);

        var statusRad = await db.LovdataImportstatuser.SingleAsync(s => s.Datokode == datokode);
        Assert.False(statusRad.Importert);
        Assert.Equal("Uventet parse-feil.", statusRad.Feilmelding);

        var historikkRad = await db.LovdataImportstatusHistorikk.SingleAsync(h => h.Datokode == datokode);
        Assert.Equal(kjoringId, historikkRad.KjoringId);
        Assert.Equal("Uventet parse-feil.", historikkRad.Feilmelding);
        Assert.Equal("https://lovdata.no/eli/lov/test", historikkRad.Eli);
        Assert.Equal("Test-lov", historikkRad.Tittel);
    }

    [Fact]
    public async Task OppdaterAsync_ved_feil_uten_kjoringId_skriver_KUN_upsert_ikke_historikk()
    {
        await using var db = _fixture.NyDbContext();
        var datokode = $"LOV-TEST-{Guid.NewGuid():N}";
        var tjeneste = new LovdataImportstatusTjeneste(db);

        // Samme sti som enkeltimport via POST /api/rettskilder/lovdata -- ingen kjøring å knytte til.
        await tjeneste.OppdaterAsync(
            datokode, "lov", "Test-lov", "https://lovdata.no/eli/lov/test", importert: false,
            rettskildeId: null, feilmelding: "Uventet parse-feil.");

        Assert.True(await db.LovdataImportstatuser.AnyAsync(s => s.Datokode == datokode));
        Assert.False(await db.LovdataImportstatusHistorikk.AnyAsync(h => h.Datokode == datokode));
    }

    [Fact]
    public async Task OppdaterAsync_ved_suksess_med_kjoringId_skriver_ALDRI_historikk()
    {
        await using var db = _fixture.NyDbContext();
        var kjoringId = await NyKjoringAsync(db);
        var datokode = $"LOV-TEST-{Guid.NewGuid():N}";
        var tjeneste = new LovdataImportstatusTjeneste(db);

        await tjeneste.OppdaterAsync(
            datokode, "lov", "Test-lov", "https://lovdata.no/eli/lov/test", importert: true,
            rettskildeId: Guid.NewGuid(), feilmelding: null, kjoringId: kjoringId);

        Assert.False(await db.LovdataImportstatusHistorikk.AnyAsync(h => h.Datokode == datokode));
    }

    /// <summary>Selve grunnen til at historikk-tabellen finnes (issue #201): et dokument som feiler i
    /// kjøring N og feiler IGJEN i kjøring N+1 skal ha SYNLIG historikk for BEGGE kjøringene, selv om
    /// UPSERT-raden i lovdata_importstatus kun husker den siste.</summary>
    [Fact]
    public async Task Samme_dokument_feiler_i_to_kjoringer_gir_to_historikkrader_men_kun_en_upsertrad()
    {
        await using var db = _fixture.NyDbContext();
        var forsteKjoringId = await NyKjoringAsync(db);
        var andreKjoringId = await NyKjoringAsync(db);
        var datokode = $"LOV-TEST-{Guid.NewGuid():N}";
        var tjeneste = new LovdataImportstatusTjeneste(db);

        await tjeneste.OppdaterAsync(
            datokode, "lov", "Test-lov", "https://lovdata.no/eli/lov/test", importert: false,
            rettskildeId: null, feilmelding: "Feil i kjøring 1.", kjoringId: forsteKjoringId);
        await tjeneste.OppdaterAsync(
            datokode, "lov", "Test-lov", "https://lovdata.no/eli/lov/test", importert: false,
            rettskildeId: null, feilmelding: "Feil i kjøring 2.", kjoringId: andreKjoringId);

        Assert.Equal(1, await db.LovdataImportstatuser.CountAsync(s => s.Datokode == datokode));
        var sisteStatus = await db.LovdataImportstatuser.SingleAsync(s => s.Datokode == datokode);
        Assert.Equal("Feil i kjøring 2.", sisteStatus.Feilmelding); // upsert husker kun SISTE forsøk

        Assert.Equal(2, await db.LovdataImportstatusHistorikk.CountAsync(h => h.Datokode == datokode));
        var forsteHistorikk = await db.LovdataImportstatusHistorikk.SingleAsync(h => h.KjoringId == forsteKjoringId && h.Datokode == datokode);
        Assert.Equal("Feil i kjøring 1.", forsteHistorikk.Feilmelding); // men historikken husker BEGGE
    }
}
