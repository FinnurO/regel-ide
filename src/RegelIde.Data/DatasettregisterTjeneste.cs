using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Kommunale/nasjonale parameterverdier for et Datasett-felt (docs/12-fasit-handbok-leveranse.md
/// dimensjon C, 2026-07-30) — <see cref="DatasettEntitet"/> er kun feltets DEFINISJON, denne
/// tjenesten forvalter de faktiske VERDIENE. <paramref name="virksomhetId"/> null i
/// <see cref="SettVerdiAsync"/> betyr den nasjonale standardverdien (§8.4-mønsteret).
/// </summary>
public sealed class DatasettregisterTjeneste(RegelIdeDbContext db)
{
    public Task<List<DatasettVerdiEntitet>> HentVerdierAsync(Guid datasettId, CancellationToken ct = default) =>
        db.DatasettVerdier.Where(v => v.DatasettId == datasettId)
            .OrderBy(v => v.VirksomhetId == null ? 0 : 1) // standardverdien alltid først
            .ToListAsync(ct);

    /// <summary>Upsert — én rad per (Datasett, Virksomhet)-par, én rad med VirksomhetId=null per Datasett.</summary>
    public async Task<DatasettVerdiEntitet> SettVerdiAsync(
        Guid datasettId, Guid? virksomhetId, string verdiJson, string? kilde, string opprettetAv, CancellationToken ct = default)
    {
        if (!await db.Datasett.AnyAsync(d => d.Id == datasettId, ct))
        {
            throw new ArgumentException($"Datasett '{datasettId}' finnes ikke.");
        }
        if (virksomhetId is not null && !await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct))
        {
            throw new ArgumentException($"Virksomhet '{virksomhetId}' finnes ikke.");
        }
        var verdi = JsonSerialiseringHjelper.ValiderJson(verdiJson, "verdi")
            ?? throw new ArgumentException("Verdi kan ikke være tom. Ingen gjettet fallback.");

        var eksisterende = await db.DatasettVerdier
            .FirstOrDefaultAsync(v => v.DatasettId == datasettId && v.VirksomhetId == virksomhetId, ct);
        if (eksisterende is not null)
        {
            eksisterende.VerdiJson = verdi;
            eksisterende.Kilde = kilde;
            await db.SaveChangesAsync(ct);
            return eksisterende;
        }

        var ny = new DatasettVerdiEntitet
        {
            Id = Guid.NewGuid(),
            DatasettId = datasettId,
            VirksomhetId = virksomhetId,
            VerdiJson = verdi,
            Kilde = kilde,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.DatasettVerdier.Add(ny);
        await db.SaveChangesAsync(ct);
        return ny;
    }

    public async Task<bool> FjernVerdiAsync(Guid verdiId, CancellationToken ct = default)
    {
        var rad = await db.DatasettVerdier.FirstOrDefaultAsync(v => v.Id == verdiId, ct);
        if (rad is null) return false;
        db.DatasettVerdier.Remove(rad);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
