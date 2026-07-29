using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// DAG-validering for vilkårstreet (INV-7, docs/01-referansemodell.md §5.4). Grafen for sykel-sjekk
/// spenner kun over <c>{vilkar, regelnode}</c>-noder: en Regelnode har utgående kanter til hvert av
/// sine <c>barn[]</c> OG til betingelsen til hvert av sine <c>unntak[]</c> — Unntak selv er kun en
/// kant-bærer, ikke en egen graf-node, som matcher hvordan INV-7 er formulert ("tilbake til en node
/// som selv er forelder til Regelen unntaket gjelder"). Et Vilkår har ingen utgående kanter (alltid
/// blad, INV-1).
/// </summary>
internal static class VilkarstreGrafHjelper
{
    /// <summary>
    /// BFS fra <c>(fraType,fraId)</c> — returnerer stien til <c>(tilType,tilId)</c> hvis den er nåbar
    /// via eksisterende barn-/unntak-betingelse-kanter, ellers <c>null</c>. Brukes både til å avgjøre
    /// om en ny kobling ville lukket en sykel, og til å bygge feilmeldingen som viser sykelen (AK-3.4.6).
    /// </summary>
    public static async Task<IReadOnlyList<(string Type, Guid Id)>?> FinnStiAsync(
        RegelIdeDbContext db, string fraType, Guid fraId, string tilType, Guid tilId, CancellationToken ct = default)
    {
        var start = (Type: fraType, Id: fraId);
        var mal = (Type: tilType, Id: tilId);
        if (start == mal) return [start];

        var forelder = new Dictionary<(string Type, Guid Id), (string Type, Guid Id)?> { [start] = null };
        var ko = new Queue<(string Type, Guid Id)>();
        ko.Enqueue(start);

        while (ko.Count > 0)
        {
            var gjeldende = ko.Dequeue();
            if (gjeldende.Type != "regelnode") continue; // vilkår er alltid blad (INV-1) — ingen utgående kanter

            var barn = await db.RegelnodeBarn.Where(b => b.RegelnodeId == gjeldende.Id)
                .Select(b => new { b.BarnType, b.BarnId }).ToListAsync(ct);
            var unntak = await db.Unntak.Where(u => u.GjelderRegelId == gjeldende.Id && u.Entitetsstatus == "gjeldende")
                .Select(u => new { u.BetingelseType, u.BetingelseId }).ToListAsync(ct);

            var naboer = barn.Select(b => (Type: b.BarnType, Id: b.BarnId))
                .Concat(unntak.Select(u => (Type: u.BetingelseType, Id: u.BetingelseId)));

            foreach (var nabo in naboer)
            {
                if (forelder.ContainsKey(nabo)) continue;
                forelder[nabo] = gjeldende;
                if (nabo == mal)
                {
                    var sti = new List<(string Type, Guid Id)> { nabo };
                    (string Type, Guid Id)? p = gjeldende;
                    while (p is not null)
                    {
                        sti.Insert(0, p.Value);
                        p = forelder[p.Value];
                    }
                    return sti;
                }
                ko.Enqueue(nabo);
            }
        }
        return null;
    }

    /// <summary>
    /// Sant hvis <c>(tilType,tilId)</c> er nåbar fra <c>(fraType,fraId)</c>. Kall før en ny
    /// barn-/betingelse-kobling: hvis MÅLET for den nye koblingen allerede kan nå KILDEN, ville
    /// koblingen lukket en sykel.
    /// </summary>
    public static async Task<bool> KanNaAsync(
        RegelIdeDbContext db, string fraType, Guid fraId, string tilType, Guid tilId, CancellationToken ct = default) =>
        await FinnStiAsync(db, fraType, fraId, tilType, tilId, ct) is not null;

    public static string FormaterSti(IReadOnlyList<(string Type, Guid Id)> sti) =>
        string.Join(" → ", sti.Select(s => $"{s.Type}:{s.Id}"));
}
