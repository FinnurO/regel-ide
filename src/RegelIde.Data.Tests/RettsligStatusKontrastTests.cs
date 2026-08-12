using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// Del A — beviser den reelle, verifiserbare KONTRASTEN mellom Bergens retningslinjer (bystyrevedtatt
/// politisk styringsdokument, binder KUN forvaltningen — <c>NormativVirkning = "bindende_forvaltning"</c>)
/// og Bergens forskrift (kunngjort norm i medhold av alkoholloven, binder BORGEREN direkte —
/// <c>NormativVirkning = "bindende_borger"</c>) fra samme dokumentbunt/bystyrevedtak
/// (docs/15-handbok-dokumentgraf-notat.md §3.3, to-akse-modellen [LÅST avklaringsrunde 1]). Ren
/// SQLite-rundtur (samme mønster som <c>SqliteProfilTests</c>) — feltene ble lagt til skjemaet i
/// forrige runde, men er ALDRI faktisk blitt populert/testet før nå.
/// </summary>
public sealed class RettsligStatusKontrastTests : IAsyncLifetime
{
    private string _filsti = "";

    public Task InitializeAsync()
    {
        _filsti = Path.Combine(Path.GetTempPath(), $"regelide-rettsligstatus-{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_filsti)) File.Delete(_filsti);
        return Task.CompletedTask;
    }

    private async Task<RegelIdeDbContext> NyBaseAsync()
    {
        var db = new RegelIdeDbContext(new DbContextOptionsBuilder<RegelIdeDbContext>().UseSqlite($"Data Source={_filsti}").Options);
        await Databaseoppsett.SorgForSkjemaAsync(db);
        return db;
    }

    [Fact]
    public async Task Retningslinjer_og_forskrift_far_ulik_NormativVirkning_selv_om_de_deler_bystyrevedtak_og_gyldighetsperiode()
    {
        await using var db = await NyBaseAsync();

        var retningslinjerId = Guid.NewGuid();
        var forskriftId = Guid.NewGuid();
        var felles = new
        {
            VedtattAv = "Bystyret",
            Vedtaksdato = new DateOnly(2024, 6, 19),
            GyldigTil = new DateOnly(2028, 7, 1),
        };

        db.Rettskilder.AddRange(
            new RettskildeEntitet
            {
                Id = retningslinjerId, Doctype = "doc", Kildetype = "Virksomhetsdokument", Status = "Gjeldende",
                Importrolle = "referanse", // AknXml er NULL — hentet/forfattet, ikke AKN-importert (§9.5)
                Tittel = "Retningslinjer for tildeling av salgs- og skjenkebevillinger i Bergen kommune for perioden 2024-2028",
                InterntDokNr = "SD-24-113", Revisjonsnr = "01",
                VedtattAv = felles.VedtattAv, Vedtaksdato = felles.Vedtaksdato, GyldigTil = felles.GyldigTil,
                NormativVirkning = "bindende_forvaltning",
                OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            },
            new RettskildeEntitet
            {
                Id = forskriftId, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende",
                Importrolle = "referanse", // samme begrunnelse — ingen AKN-eksport bygget for håndbøker denne runden
                Tittel = "Forskrift om salgs-, skjenke- og åpningstider i Bergen kommune for perioden 2024 – 2028",
                InterntDokNr = "SD-24-114", Revisjonsnr = "01",
                VedtattAv = felles.VedtattAv, Vedtaksdato = felles.Vedtaksdato, GyldigTil = felles.GyldigTil,
                NormativVirkning = "bindende_borger",
                OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var retningslinjer = await db.Rettskilder.SingleAsync(r => r.Id == retningslinjerId);
        var forskrift = await db.Rettskilder.SingleAsync(r => r.Id == forskriftId);

        // Samme bystyrevedtak, samme vedtaksdato, samme gyldighetsperiode — men FUNDAMENTALT ulik
        // rettslig kraft. Dette ER kontrasten §13/§3.3 ber om å teste, ikke et tilfeldig funn.
        Assert.Equal(retningslinjer.VedtattAv, forskrift.VedtattAv);
        Assert.Equal(retningslinjer.Vedtaksdato, forskrift.Vedtaksdato);
        Assert.Equal(retningslinjer.GyldigTil, forskrift.GyldigTil);
        Assert.NotEqual(retningslinjer.NormativVirkning, forskrift.NormativVirkning);
        Assert.Equal("bindende_forvaltning", retningslinjer.NormativVirkning);
        Assert.Equal("bindende_borger", forskrift.NormativVirkning);

        // §3.3: "InterntDokNr"/"Revisjonsnr" er en brukbar dokument-nøkkel lest rett ut av
        // dokumentet — de to instrumentene har SAMME Rev.nr men FORSKJELLIG InterntDokNr, akkurat
        // som de ekte PDF-ene faktisk viser (SD-24-113 vs. SD-24-114).
        Assert.Equal(retningslinjer.Revisjonsnr, forskrift.Revisjonsnr);
        Assert.NotEqual(retningslinjer.InterntDokNr, forskrift.InterntDokNr);
    }
}
