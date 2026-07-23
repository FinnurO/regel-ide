using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Databasebacket register over rettskilder (§2 i teknisk design) — erstatter den tidligere
/// in-memory-varianten fra denne økten. Scoped (per request), speiler DbContext sin levetid.
/// </summary>
public sealed class RettskildeRepository(RegelIdeDbContext db)
{
    public Task<List<RettskildeEntitet>> AlleRettskilderAsync() =>
        db.Rettskilder.Where(r => r.Importrolle == "primaer" && r.Entitetsstatus == "gjeldende").ToListAsync();

    public Task<RettskildeEntitet?> FinnAsync(Guid id) =>
        db.Rettskilder.FirstOrDefaultAsync(r => r.Id == id);

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
