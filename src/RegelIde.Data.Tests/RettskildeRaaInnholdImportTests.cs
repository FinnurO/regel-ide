using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Del B (lovdata-raa-metadata-runden, 2026-09-02) — den rå kilde-HTML-en flyter nå gjennom hele
/// pipelinen (<see cref="KonverteringResultat.RaaHtml"/>) og populerer de allerede eksisterende, men
/// til nå Lovdata-bevisst-NULL-holdte feltene <c>Url</c>/<c>Innhold</c>/<c>InnholdsHash</c>/<c>Hentet</c>
/// på <see cref="RettskildeEntitet"/>, samt del A sine nye rå metadatafelt
/// (<c>IkrafttredelseRaa</c>/<c>KonsolidertDatoRaa</c>/<c>SistEndretVed</c>).
/// <para>
/// HVER test bruker sin EGEN, Guid-avledede isolerte datokode/Eli (se <see cref="NyIsolertDatokode"/>)
/// — samme "delt embedded Postgres per assembly, ingen opprydning mellom tester"-begrunnelse som
/// RettskildeHjemmelImportTests, men generert fra en fersk Guid i stedet for et manuelt bokført tall,
/// slik at det er umulig å kollidere med noen annen testklasses hardkodede isolasjons-datokode,
/// uavhengig av kjøringsrekkefølge.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class RettskildeRaaInnholdImportTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public RettskildeRaaInnholdImportTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Guid-avledet, garantert-unik datokode. Årstallet (2100-2189) ligger bevisst utenfor ALT annet
    /// isolasjons-datokode-mønster brukt ellers i testsuiten (som topper på 2099, se
    /// RettskildeImportTjenesteTests/RettskildeHjemmelImportTests) — ingen manuell "neste ledige
    /// tall"-bokføring trengs.
    /// </summary>
    private static string NyIsolertDatokode(string prefiks = "LOV")
    {
        var b = Guid.NewGuid().ToByteArray();
        var aar = 2100 + (b[0] % 90);
        var maaned = 1 + (b[1] % 12);
        var dag = 1 + (b[2] % 28); // gyldig i alle måneder
        var lopenummer = BitConverter.ToUInt16(b, 3) + 1; // aldri 0
        return $"{prefiks}-{aar:D4}-{maaned:D2}-{dag:D2}-{lopenummer}";
    }

    private static string LesIsolertAlkoholloven(string datokode) =>
        Testdata.LesAlkoholloven().Replace("LOV-1989-06-02-27", datokode);

    [Fact]
    public async Task Ny_import_populerer_url_innhold_innholdshash_hentet_og_ra_metadatafelt()
    {
        var html = LesIsolertAlkoholloven(NyIsolertDatokode());
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2));

        await using var db = _fixture.NyDbContext();
        var id = await new RettskildeImportTjeneste(db).ImporterAsync(resultat);

        var rad = await db.Rettskilder.SingleAsync(r => r.Id == id);
        Assert.Equal(resultat.Metadata.Eli, rad.Url);
        Assert.NotNull(rad.Innhold);
        Assert.Equal(resultat.RaaHtml, System.Text.Encoding.UTF8.GetString(rad.Innhold!));
        Assert.Equal(LovdataIdentifikatorer.BeregnTekstHash(resultat.RaaHtml), rad.InnholdsHash);
        Assert.NotNull(rad.Hentet);
        Assert.True(rad.Hentet > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Equal(resultat.Metadata.IkrafttredelseRaa, rad.IkrafttredelseRaa);
        Assert.Equal(resultat.Metadata.KonsolidertDatoRaa, rad.KonsolidertDatoRaa);
        Assert.Equal(resultat.Metadata.SistEndretVed, rad.SistEndretVed);

        // Legitimt N/A for bulk-arkiv-kilden -- ikke oppfunnet, se RettskildeEntitet-kommentaren.
        Assert.Null(rad.HttpEtag);
        Assert.Null(rad.HttpLastModified);
    }

    [Fact]
    public async Task Referanse_stub_har_fortsatt_null_innhold_url_og_hash()
    {
        var eksternMalDatokode = NyIsolertDatokode(); // egen, garantert ubrukt referanse-mål
        var eksternMalEli = LovdataIdentifikatorer.AvledEliFraDatokode(eksternMalDatokode, out _);
        var html = LesIsolertAlkoholloven(NyIsolertDatokode())
            .Replace("lov/2009-01-09-2", "lov/" + eksternMalDatokode[4..]);

        await using var db = _fixture.NyDbContext();
        await new RettskildeImportTjeneste(db).ImporterAsync(LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)));

        var stub = await db.Rettskilder.SingleAsync(r => r.Eli == eksternMalEli);
        Assert.Equal("referanse", stub.Importrolle);
        Assert.Null(stub.Innhold);
        Assert.Null(stub.Url);
        Assert.Null(stub.InnholdsHash);
        Assert.Null(stub.Hentet);
        Assert.Null(stub.IkrafttredelseRaa);
        Assert.Null(stub.KonsolidertDatoRaa);
        Assert.Null(stub.SistEndretVed);
    }

    [Fact]
    public async Task Forfremmet_stub_far_url_innhold_innholdshash_hentet_satt_forste_gang()
    {
        var eksternMalDatokode = NyIsolertDatokode();
        var eksternMalEli = LovdataIdentifikatorer.AvledEliFraDatokode(eksternMalDatokode, out _);
        var htmlMedReferanse = LesIsolertAlkoholloven(NyIsolertDatokode())
            .Replace("lov/2009-01-09-2", "lov/" + eksternMalDatokode[4..]);

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(htmlMedReferanse, new DateOnly(2026, 9, 2)));

        var stubFor = await db.Rettskilder.SingleAsync(r => r.Eli == eksternMalEli);
        Assert.Null(stubFor.Innhold); // før forfremmelse -- ingen ekte HTML lagret for en ren stub.

        // Forfremmer stubben -- gjenbruker isolert alkoholloven-HTML som stand-in (kun ELI-en teller
        // her), samme mønster som RettskildeImportTjenesteTests
        // .ImporterMedUtfallAsync_klassifiserer_alle_fire_utfall_korrekt.
        var eksternResultat = LovdataKonverterer.Konverter(
            LesIsolertAlkoholloven(eksternMalDatokode), new DateOnly(2026, 9, 2));
        var forfremmetId = await tjeneste.ImporterAsync(eksternResultat);
        Assert.Equal(stubFor.Id, forfremmetId);

        var forfremmet = await db.Rettskilder.SingleAsync(r => r.Id == forfremmetId);
        Assert.Equal("primaer", forfremmet.Importrolle);
        Assert.Equal(eksternMalEli, forfremmet.Url);
        Assert.NotNull(forfremmet.Innhold);
        Assert.Equal(LovdataIdentifikatorer.BeregnTekstHash(eksternResultat.RaaHtml), forfremmet.InnholdsHash);
        Assert.NotNull(forfremmet.Hentet);
    }

    [Fact]
    public async Task Uendret_reimport_bakfyller_felt_uten_a_oke_versjon_eller_lage_ny_proveniensrad()
    {
        var html = LesIsolertAlkoholloven(NyIsolertDatokode());

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var forsteId = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)));

        // Simuler at raden ble importert FØR del B (ingen rå-felt utfylt ennå) -- den situasjonen
        // ALLE eksisterende rader fra før denne runden faktisk er i, inntil neste fulle resynk.
        var rad = await db.Rettskilder.SingleAsync(r => r.Id == forsteId);
        rad.Url = null;
        rad.Innhold = null;
        rad.InnholdsHash = null;
        rad.Hentet = null;
        rad.IkrafttredelseRaa = null;
        rad.KonsolidertDatoRaa = null;
        rad.SistEndretVed = null;
        var versjonForResynk = rad.Versjon;
        await db.SaveChangesAsync();

        var antallProveniensForResynk = await db.Proveniens.CountAsync(p => p.EntitetId == forsteId);

        // Bit-identisk resynk (samme HTML) -- «Uendret»-utfallet, men skal FORTSATT bakfylle de nå
        // tomme feltene (del B punkt 3) -- ellers ville en helt vanlig full-resynk ALDRI bakfylt
        // eksisterende rader uten en egen engangs-backfill-tjeneste.
        var resynkResultat = await tjeneste.ImporterMedUtfallAsync(
            LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 3)));
        Assert.Equal(RettskildeImportUtfall.Uendret, resynkResultat.Utfall);
        Assert.Equal(forsteId, resynkResultat.RettskildeId);

        var etterResynk = await db.Rettskilder.SingleAsync(r => r.Id == forsteId);
        Assert.Equal(etterResynk.Eli, etterResynk.Url);
        Assert.NotNull(etterResynk.Innhold);
        Assert.NotNull(etterResynk.InnholdsHash);
        Assert.NotNull(etterResynk.Hentet);
        Assert.NotNull(etterResynk.IkrafttredelseRaa);
        Assert.NotNull(etterResynk.KonsolidertDatoRaa);
        Assert.NotNull(etterResynk.SistEndretVed);

        // IKKE en reell endring -- Versjon uendret, ingen ny Proveniens-rad opprettet.
        Assert.Equal(versjonForResynk, etterResynk.Versjon);
        var antallProveniensEtterResynk = await db.Proveniens.CountAsync(p => p.EntitetId == forsteId);
        Assert.Equal(antallProveniensForResynk, antallProveniensEtterResynk);
    }

    [Fact]
    public async Task Uendret_reimport_bakfyller_de_ti_resterende_metadatafeltene_og_retter_feil_ansvarligDepartement()
    {
        // Issue #127 (10 nye felt) + issue #152 (AnsvarligDepartement-sammenlimingsbug) — begge deler
        // samme "Uendret-grenen backfiller ALLTID fra fersk parsing"-mekanisme som del B-feltene over
        // (samme test-scenario, se den forrige testens kommentar).
        var html = LesIsolertAlkoholloven(NyIsolertDatokode());

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var forsteId = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)));

        // Simuler en rad importert med (a) den gamle parseren, som ALDRI fylte ut de ti nye feltene, og
        // (b) den bekreftede #152-bugen -- et AnsvarligDepartement som ble feilaktig sammenlimt uten
        // skilletegn ved en tidligere import (rammer aldri alkoholloven selv, kun et flere-departement-
        // dokument -- men Uendret-grenen skal rette VERDIEN uansett hva den tilfeldigvis var før).
        var rad = await db.Rettskilder.SingleAsync(r => r.Id == forsteId);
        rad.Kunngjort = null;
        rad.Rettsomrade = null;
        rad.EuEosHenvisning = null;
        rad.DokumentId = null;
        rad.RefId = null;
        rad.GjelderFor = null;
        rad.Etat = null;
        rad.PublisertI = null;
        rad.AnnetOmDokumentet = null;
        rad.SisteRettelse = null;
        rad.AnsvarligDepartement = ["EtSammenlimtFeilNavnSomAldriSkalOverleveEnResynk"];
        await db.SaveChangesAsync();

        var resynkResultat = await tjeneste.ImporterMedUtfallAsync(
            LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 3)));
        Assert.Equal(RettskildeImportUtfall.Uendret, resynkResultat.Utfall);

        var etterResynk = await db.Rettskilder.SingleAsync(r => r.Id == forsteId);
        // Kun de feltene alkoholloven-fixturen FAKTISK har (se ResterendeMetadatafeltKonverteringTests
        // sin klassekommentar for hvilke 6 av 10 det er) forventes non-null her.
        Assert.NotNull(etterResynk.DokumentId);
        Assert.NotNull(etterResynk.EuEosHenvisning);
        Assert.NotNull(etterResynk.Rettsomrade);
        Assert.NotNull(etterResynk.SisteRettelse);
        Assert.NotNull(etterResynk.AnnetOmDokumentet);
        Assert.NotNull(etterResynk.RefId);
        Assert.Equal(["Helse- og omsorgsdepartementet"], etterResynk.AnsvarligDepartement);
    }

    [Fact]
    public async Task InnholdsHash_er_deterministisk_for_samme_html()
    {
        var html = LesIsolertAlkoholloven(NyIsolertDatokode());
        var forventetHash = LovdataIdentifikatorer.BeregnTekstHash(html);
        Assert.Equal(forventetHash, LovdataIdentifikatorer.BeregnTekstHash(html));

        await using var db = _fixture.NyDbContext();
        var id = await new RettskildeImportTjeneste(db)
            .ImporterAsync(LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)));

        var rad = await db.Rettskilder.SingleAsync(r => r.Id == id);
        Assert.Equal(forventetHash, rad.InnholdsHash);
    }

    [Fact]
    public async Task Ny_versjon_ved_reell_endring_far_egne_url_innhold_og_innholdshash()
    {
        var datokode = NyIsolertDatokode();
        var html = LesIsolertAlkoholloven(datokode);
        var endretHtml = html.Replace(
            "begrense forbruket av alkoholholdige drikkevarer.", "begrense forbruket av alkoholholdige drikkevarer betydelig.");

        await using var db = _fixture.NyDbContext();
        var tjeneste = new RettskildeImportTjeneste(db);
        var forsteId = await tjeneste.ImporterAsync(LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)));
        var andreResultat = LovdataKonverterer.Konverter(endretHtml, new DateOnly(2026, 9, 3));
        var andreId = await tjeneste.ImporterAsync(andreResultat);

        Assert.NotEqual(forsteId, andreId);
        var nyRad = await db.Rettskilder.SingleAsync(r => r.Id == andreId);
        Assert.NotNull(nyRad.Innhold);
        Assert.Equal(andreResultat.RaaHtml, System.Text.Encoding.UTF8.GetString(nyRad.Innhold!));
        Assert.Equal(LovdataIdentifikatorer.BeregnTekstHash(andreResultat.RaaHtml), nyRad.InnholdsHash);
        Assert.NotNull(nyRad.Hentet);
    }
}
