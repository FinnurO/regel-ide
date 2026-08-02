using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Søkbar katalog over Lovdatas bulk-datasett (byggesteg 5 runde 2, docs/14-byggesteg5-teknisk-design.md)
/// — løser at <c>Importer.tsx</c> tidligere krevde at brukeren allerede kjente den eksakte datokoden.
/// Katalogen inneholder KUN tittel/type, aldri full tekst — selve rettskilde-importen skjer fortsatt
/// via det uendrede <c>POST /api/rettskilder/lovdata</c> når brukeren velger et søketreff.
/// </summary>
public sealed class LovdataKatalogTjeneste(RegelIdeDbContext db, LovdataBulkHenter bulkHenter)
{
    private static readonly TimeSpan Foreldelsesgrense = TimeSpan.FromHours(24);

    public async Task<List<LovdataKatalogOppforingEntitet>> SokAsync(string sokestreng, CancellationToken ct = default)
    {
        await SikreOppdatertKatalogAsync(ct);

        if (string.IsNullOrWhiteSpace(sokestreng)) return [];

        var lavSokestreng = sokestreng.ToLower();
        return await db.LovdataKatalogOppforinger
            .Where(o => o.Tittel.ToLower().Contains(lavSokestreng))
            .OrderBy(o => o.Tittel)
            .Take(50)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Bygger katalogen på nytt hvis den er tom eller eldre enn <see cref="Foreldelsesgrense"/> — matcher
    /// Lovdatas egen nattlige oppdateringssyklus for bulk-datasettet. Laster ned og itererer BEGGE
    /// arkiv via <see cref="LovdataBulkHenter.HentAlleOppforingerAsync"/> (kan ta noen sekunder).
    /// </summary>
    public async Task SikreOppdatertKatalogAsync(CancellationToken ct = default)
    {
        var sistOppdatert = await db.LovdataKatalogOppforinger.MaxAsync(o => (DateTimeOffset?)o.SistOppdatert, ct);
        if (sistOppdatert is not null && DateTimeOffset.UtcNow - sistOppdatert < Foreldelsesgrense) return;

        var nyTidspunkt = DateTimeOffset.UtcNow;
        var nyeRader = new List<LovdataKatalogOppforingEntitet>();
        await foreach (var (datokode, tittel, type) in bulkHenter.HentAlleOppforingerAsync(ct))
        {
            nyeRader.Add(new LovdataKatalogOppforingEntitet { Datokode = datokode, Tittel = tittel, Type = type, SistOppdatert = nyTidspunkt });
        }

        await using var transaksjon = await db.Database.BeginTransactionAsync(ct);
        await db.LovdataKatalogOppforinger.ExecuteDeleteAsync(ct);
        db.LovdataKatalogOppforinger.AddRange(nyeRader);
        await db.SaveChangesAsync(ct);
        await transaksjon.CommitAsync(ct);
    }
}
