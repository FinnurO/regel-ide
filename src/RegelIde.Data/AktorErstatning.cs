using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #397, 2026-10-27] En aktør som er en duplikat, markeres som ERSTATTET av en annen (<see cref="Virksomhet.ErstattetAvId"/>).
/// Ingenting slettes, og det kan angres ved å sette null. Proveniens skrives.</summary>
public static class AktorErstatning
{
    /// <returns>Aktøren, eller null når den ikke finnes. Kaster <see cref="ArgumentException"/> når den erstattende ikke kan brukes.</returns>
    public static async Task<Virksomhet?> SettAsync(RegelIdeDbContext db, Guid id, Guid? erstattetAvId, string bruker, CancellationToken ct = default)
    {
        var v = await db.Virksomheter.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return null;
        if (erstattetAvId is { } ny && (ny == id || !await db.Virksomheter.AnyAsync(x => x.Id == ny && x.ErstattetAvId == null, ct)))
        {
            throw new ArgumentException("Den erstattende aktøren må finnes, være en annen og ikke selv være erstattet.");
        }
        v.ErstattetAvId = erstattetAvId;
        db.Proveniens.Add(ProveniensHjelper.NyRad("virksomhet", v.Id, virksomhetId: null, erstattetAvId is null ? "erstatning_angret" : "erstattet", bruker));
        await db.SaveChangesAsync(ct);
        return v;
    }
}
