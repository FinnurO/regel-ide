using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Delt hjelper (byggesteg 5 runde 1) for å bygge en KI-kontekst-streng fra valgte rettskilders
/// faktiske, allerede importerte lovtekst — brukt av både <see cref="BegrepsforslagTjeneste"/> og
/// <see cref="TjenesteforslagTjeneste"/>. Rettskilder er allerede ekte, strukturert tekst — langt
/// bedre KI-kontekst enn et opplastet, uparset dokument ville vært (dokumentinnholds-uttrekk/OCR er
/// utenfor scope, se docs/06-veikart.md). Ingen kontekst-lengde-trimming i denne runden — en reell
/// fremtidig bekymring når en ekte, kontekstvindu-begrenset leverandør kobles til bak
/// <see cref="IKiAgentKlient"/>, ikke noe stubben trenger å ta stilling til.
/// </summary>
internal static class RettskildeKontekstHjelper
{
    public static async Task<string> ByggKontekstAsync(RegelIdeDbContext db, IReadOnlyList<Guid> rettskildeIder, CancellationToken ct)
    {
        if (rettskildeIder.Count == 0)
        {
            throw new ArgumentException("Minst én rettskilde må velges. Ingen gjettet fallback.");
        }

        var rettskilder = await db.Rettskilder
            .Where(r => rettskildeIder.Contains(r.Id) && r.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        if (rettskilder.Count != rettskildeIder.Distinct().Count())
        {
            throw new ArgumentException("En eller flere valgte rettskilder finnes ikke.");
        }

        var noder = await db.RettskildeNoder
            .Where(n => rettskildeIder.Contains(n.RettskildeId) && n.Tekst != null)
            .OrderBy(n => n.RettskildeId).ThenBy(n => n.Sorteringsrekkefolge)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        foreach (var rettskilde in rettskilder)
        {
            sb.AppendLine($"# {rettskilde.Tittel}");
            foreach (var node in noder.Where(n => n.RettskildeId == rettskilde.Id))
            {
                sb.AppendLine(node.Tekst);
            }
        }
        return sb.ToString();
    }
}
