using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Databasebacket register over rettskilder (§2 i teknisk design). Scoped (per request), speiler
/// DbContext sin levetid.
///
/// Åpne data, ikke virksomhets-lukket (2026-07-24, jf. eksisterende publiseringsfilosofi i
/// 05-arkitektur-og-nfk.md §1.2 — "publisert" har alltid betydd "gjøres tilgjengelig i regel-IDEs
/// eget høstbare/lesbare endepunkt"): alle metoder viser kun rettskilder med
/// <c>Status != "Utkast"</c> — kladder (ikke menneskelig verifisert ennå, §3.1 steg 10 i teknisk
/// design) er aldri offentlig synlige, uansett virksomhet. <c>virksomhetId</c> er en valgfri
/// filtrering for å snevre inn til én virksomhets bidrag, ikke en tilgangssperre — utelates den,
/// vises alt som er synlig (delte/nasjonale kilder + alle virksomheters publiserte lokale kilder),
/// akkurat som en nasjonal åpne-data-katalog aggregerer på tvers av alle bidragsytere.
/// </summary>
public sealed class RettskildeRepository(RegelIdeDbContext db)
{
    private const string UtkastStatus = "Utkast";

    public Task<List<RettskildeEntitet>> AlleRettskilderAsync(Guid? virksomhetId = null) =>
        db.Rettskilder
            .Where(r => r.Importrolle == "primaer" && r.Entitetsstatus == "gjeldende" && r.Status != UtkastStatus)
            .Where(r => virksomhetId == null || r.VirksomhetId == virksomhetId)
            .ToListAsync();

    public Task<RettskildeEntitet?> FinnAsync(Guid id) =>
        db.Rettskilder.FirstOrDefaultAsync(r => r.Id == id && r.Status != UtkastStatus);

    public Task<List<RettskildeNodeEntitet>> NoderForAsync(Guid rettskildeId) =>
        db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).OrderBy(n => n.Sorteringsrekkefolge).ToListAsync();

    public Task<RettskildeNodeEntitet?> FinnNodeAsync(Guid rettskildeId, string eid) =>
        db.RettskildeNoder.FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == eid);

    public async Task<List<RettskildeReferanseEntitet>> ReferanserForAsync(Guid rettskildeId)
    {
        var nodeIder = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).Select(n => n.Id).ToListAsync();
        return await db.RettskildeReferanser.Where(r => nodeIder.Contains(r.FraNodeId)).ToListAsync();
    }
}
