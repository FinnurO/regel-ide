namespace RegelIde.Data;

/// <summary>
/// Delt upsert-logikk for <c>lovdata_importstatus</c> (<see cref="LovdataImportstatusEntitet"/>) —
/// trukket ut av <see cref="LovdataFullimportTjeneste"/> (2026-08-20, konsistensrunde) slik at BÅDE
/// den automatiske bakgrunnsjobben OG en brukerutløst enkeltimport (<c>POST /api/rettskilder/lovdata</c>
/// i RegelIde.Api/Program.cs) kan holde denne tabellen konsistent med det faktiske importutfallet.
/// Uten dette ville en enkeltimport av et dokument som tidligere stod som <c>importert=false</c> (fra
/// forrige fullimport-runde) latt raden stå igjen med den GAMLE feilmeldingen selv etter en vellykket
/// enkeltimport — først rettet ved neste app-restart/fullimport-runde. Se klassekommentaren på
/// <see cref="LovdataImportstatusEntitet"/> for hvorfor dette er en upsert (én rad per kjent
/// dokument, ikke historikk).
/// </summary>
public sealed class LovdataImportstatusTjeneste(RegelIdeDbContext db)
{
    /// <summary>
    /// Upsert på <see cref="LovdataImportstatusEntitet.Datokode"/> — kalles etter ETT importforsøk
    /// (enten fra <see cref="LovdataFullimportTjeneste"/>s runde over ALLE dokumenter, eller fra en
    /// enkeltimport av nøyaktig ett), uansett om forsøket lyktes eller ikke.
    /// <para>
    /// <paramref name="kjoringId"/> [Ny, feillogg-runden, 2026-09-10, issue #201 del A] — når oppgitt
    /// OG <paramref name="importert"/> er <c>false</c>, skriver denne i SAMME <c>SaveChangesAsync</c>-
    /// kall også en rad til <c>lovdata_importstatus_historikk</c> (se
    /// <see cref="LovdataImportstatusHistorikkEntitet"/> for hvorfor det trengs ved siden av selve
    /// upsert-raden over). <c>null</c> (standardverdien) for enkeltimport via
    /// <c>POST /api/rettskilder/lovdata</c> — den har ingen Lovdata-resynk-kjøring å knytte forsøket
    /// til, og skal derfor ALDRI havne i historikk-tabellen (som eksplisitt er "per kjøring").
    /// </para>
    /// </summary>
    public async Task OppdaterAsync(
        string datokode, string type, string? tittel, string eli, bool importert, Guid? rettskildeId,
        string? feilmelding, CancellationToken ct = default, Guid? kjoringId = null)
    {
        var rad = await db.LovdataImportstatuser.FindAsync([datokode], ct);
        if (rad is null)
        {
            rad = new LovdataImportstatusEntitet { Datokode = datokode, Type = type, Eli = eli, Importert = importert };
            db.LovdataImportstatuser.Add(rad);
        }

        rad.Type = type;
        rad.Tittel = tittel;
        rad.Eli = eli;
        rad.Importert = importert;
        rad.RettskildeId = rettskildeId;
        rad.Feilmelding = feilmelding;
        rad.SistForsoktTidspunkt = DateTimeOffset.UtcNow;

        if (!importert && kjoringId is not null)
        {
            db.LovdataImportstatusHistorikk.Add(new LovdataImportstatusHistorikkEntitet
            {
                Id = Guid.NewGuid(),
                KjoringId = kjoringId.Value,
                Datokode = datokode,
                Type = type,
                Tittel = tittel,
                Eli = eli,
                Feilmelding = feilmelding,
                ForsoktTidspunkt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
