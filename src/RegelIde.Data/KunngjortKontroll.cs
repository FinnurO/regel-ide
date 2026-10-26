using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, Johanns avgjørelse P4 på #397, 2026-10-26] Konsoliderte tekster kan bruke departementsnavn Lovdata har oppdatert i ettertid
/// (FOR-1989-12-22-1273 har både «Kirke- og kulturdepartementet» og «Kultur- og kirkedepartementet»). Kunngjort tekst før 2001 finnes ikke
/// i åpne data. Et navn med hjemmel i en konsolidert <c>DEL/</c>-tekst fra før 2001 merkes derfor «ikke kontrollert mot kunngjort tekst».
/// Merket hindrer ikke godkjenning, og det lagres ikke: det følger av hjemmelen.
/// </summary>
public static class KunngjortKontroll
{
    public static readonly DateOnly Grense = new(2001, 1, 1);

    /// <summary>Rettskildene blant <paramref name="ider"/> som er konsolidert DEL-tekst med vedtak (ikrafttredelse, ellers vedtaksdato) før 2001.</summary>
    public static async Task<HashSet<Guid>> UkontrollerteAsync(RegelIdeDbContext db, IEnumerable<Guid> ider, CancellationToken ct = default)
    {
        var liste = ider.Distinct().ToList();
        if (liste.Count == 0) return [];
        return (await db.Rettskilder.Where(r => liste.Contains(r.Id) && r.Kildeform == "konsolidert" && r.DokumentId != null
                && r.DokumentId.StartsWith("DEL/") && (r.Ikrafttredelse ?? r.Vedtaksdato) < Grense)
            .Select(r => r.Id).ToListAsync(ct)).ToHashSet();
    }
}
