using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// Full Lovdata-synkronisering (docs/13-backlog.md §6 "Daglig Lovdata-synkronisering (full + delta)")
/// — ekte nettverkskall mot Lovdatas bulk-API, samme "test mot ekte data"-kultur som
/// <see cref="LovdataBulkHenterTests"/>/<see cref="LovdataKatalogTjenesteTests"/>. Kjører hele
/// korpuset (alle lover+sentrale forskrifter) TO ganger for å bevise selve delta-analysen brukeren
/// ba om — derfor trolig den tregeste enkelttesten i denne assemblyen, men det finnes ingen snarvei
/// som tester delta-egenskapen uten faktisk å kjøre en full runde to ganger.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class LovdataFullimportTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public LovdataFullimportTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Forste_kjoring_importerer_alt_andre_kjoring_finner_kun_uendret()
    {
        await using var db = _fixture.NyDbContext();
        using var http = new HttpClient();
        var tjeneste = new LovdataFullimportTjeneste(
            new LovdataBulkHenter(http), new RettskildeImportTjeneste(db), db, new LovdataImportstatusTjeneste(db));

        var forsteRunde = await tjeneste.KjorAsync();
        Assert.True(forsteRunde.TotaltBehandlet > 100, "Forventet et stort antall lover+forskrifter i bulk-arkivene.");
        Assert.True(forsteRunde.Nye + forsteRunde.NyeVersjoner > 0, "Forventet at (de fleste av) disse ikke fantes fra før.");

        var forvaltningsloven = await db.Rettskilder.SingleOrDefaultAsync(
            r => r.Eli == "https://lovdata.no/eli/lov/1967/02/10/nor" && r.VirksomhetId == null);
        Assert.NotNull(forvaltningsloven);
        Assert.Equal("primaer", forvaltningsloven!.Importrolle);
        Assert.Equal("gjeldende", forvaltningsloven.Entitetsstatus);

        // Importstatus (brukerens ekstra ønske: flagg + url/eId + metadata for de som FEILER) --
        // forvaltningsloven lar seg parse -- skal derfor stå som importert=true med RettskildeId satt.
        var forvaltningslovenStatus = await db.LovdataImportstatuser.SingleAsync(s => s.Datokode == "LOV-1967-02-10");
        Assert.True(forvaltningslovenStatus.Importert);
        Assert.Equal(forvaltningsloven.Id, forvaltningslovenStatus.RettskildeId);
        Assert.Null(forvaltningslovenStatus.Feilmelding);
        Assert.Equal("https://lovdata.no/eli/lov/1967/02/10/nor", forvaltningslovenStatus.Eli);
        Assert.Equal("lov", forvaltningslovenStatus.Type);
        Assert.NotNull(forvaltningslovenStatus.Tittel);

        // LOV-1931-06-12-1 er et EKTE, kjent tilfelle parseren i dag avviser (gammel "Første
        // kapitel."-ordvariant, bevisst utenfor scope -- se KapittelOrdvarianter i LovdataHtmlParser)
        // -- skal likevel få en importstatus-rad: url/eId avledet rent fra datokoden (uavhengig av at
        // selve strukturparsingen feilet), flagget importert=false, og en faktisk feilmelding -- akkurat
        // det brukeren ba om å kunne se. Merk: Grunnloven (LOV-1814-05-17), tidligere brukt her, ble et
        // FAKTISK positivt PARSE-resultat etter runden med gjennomgang mot https://api.lovdata.no/xmldocs
        // 2026-08-21 (kapittelfri-lov-håndteringen dekker den nå) -- testen måtte derfor byttes til et
        // dokument som fortsatt genuint feiler, samme bytte som i ImportEndepunktTests.cs.
        var grunnlovenStatus = await db.LovdataImportstatuser.SingleOrDefaultAsync(s => s.Datokode == "LOV-1931-06-12-1");
        Assert.NotNull(grunnlovenStatus);
        Assert.False(grunnlovenStatus!.Importert);
        Assert.Null(grunnlovenStatus.RettskildeId);
        Assert.NotNull(grunnlovenStatus.Feilmelding);
        Assert.Equal("https://lovdata.no/eli/lov/1931/06/12/1/nor", grunnlovenStatus.Eli);

        // Selve delta-analysen: en andre runde mot samme, nå-fylte database skal ikke opprette noe
        // nytt -- alt som ikke reelt har endret seg siden forrige runde klassifiseres som Uendret.
        var andreRunde = await tjeneste.KjorAsync();
        Assert.Equal(forsteRunde.TotaltBehandlet, andreRunde.TotaltBehandlet);
        Assert.Equal(0, andreRunde.Nye);
        Assert.Equal(0, andreRunde.NyeVersjoner);
        Assert.True(andreRunde.Uendret > 100, "Andre runde skal finne alt fra første runde uendret.");

        // Importstatus-raden oppdateres (ikke dupliseres) ved reimport -- fortsatt én rad per datokode.
        var antallStatusraderForForvaltningsloven = await db.LovdataImportstatuser.CountAsync(s => s.Datokode == "LOV-1967-02-10");
        Assert.Equal(1, antallStatusraderForForvaltningsloven);
    }
}
