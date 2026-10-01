using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Delt hjelper (byggesteg 5 runde 1) for å bygge en KI-kontekst-streng fra valgte rettskilders
/// faktiske, allerede importerte lovtekst — brukt av både <see cref="BegrepsforslagTjeneste"/> og
/// <see cref="TjenesteforslagTjeneste"/>. Rettskilder er allerede ekte, strukturert tekst — langt
/// bedre KI-kontekst enn et opplastet, uparset dokument ville vært (dokumentinnholds-uttrekk/OCR er
/// utenfor scope, se docs/06-veikart.md).
/// <para>
/// [Rettet, 2026-10-01, live bug mot ekte helse- og omsorgstjenesteloven] Klassekommentaren sa
/// tidligere «ingen kontekst-lengde-trimming i denne runden — en reell fremtidig bekymring når en
/// ekte, kontekstvindu-begrenset leverandør kobles til». Den bekymringen materialiserte seg: et
/// ekte KI-kall (<see cref="VirksomhetOgGruppeKiOppdagelseTjeneste"/>) mot en stor lov (143 913 tegn
/// AKN-XML) timet ut etter 100 sekunder, to forsøk, mot HostYourAI — 500 Internal Server Error i
/// produksjon. <see cref="ByggKontekstChunketAsync"/> er tillegget som løser DETTE konkrete
/// kallstedet (flere, mindre KI-kall i stedet for ett for stort). <see cref="ByggKontekstAsync"/>
/// over er UENDRET og brukes fortsatt av Begrepsforslag/Tjenesteforslag — de har ikke (ennå)
/// rapportert samme feil, og en ukoordinert endring av delt, etablert kode uten et konkret,
/// reprodusert problem der ville vært en gjetning. Se issue (navnet legges inn ved PR) for at de bør
/// vurderes separat.
/// </para>
/// </summary>
internal static class RettskildeKontekstHjelper
{
    /// <summary>
    /// Som <see cref="ByggKontekstAsync"/>, men for ÉN rettskilde og delt opp i flere kontekst-
    /// strenger à maks <paramref name="maksTegnPerDel"/> tegn — grådig akkumulering node for node i
    /// EKSISTERENDE sorteringsrekkefølge, ALDRI splitt midt i én nodes tekst (en splitt der ville gitt
    /// en agent en avkuttet paragraf å resonnere over, verre enn selve problemet dette løser). Hver
    /// del får samme "# {tittel}"-header som <see cref="ByggKontekstAsync"/>, slik at en agent som
    /// kun ser én del fortsatt vet hvilken rettskilde den leser. En enkelt node som ALENE overskrider
    /// <paramref name="maksTegnPerDel"/> (ikke observert i praksis, men ingen gjettet fallback) får
    /// stå i sin egen del uten å bli kuttet — budsjettet er en myk, ikke en hard grense per del.
    /// </summary>
    public static async Task<List<string>> ByggKontekstChunketAsync(
        RegelIdeDbContext db, Guid rettskildeId, int maksTegnPerDel, CancellationToken ct)
    {
        var rettskilde = await db.Rettskilder
            .FirstOrDefaultAsync(r => r.Id == rettskildeId && r.Entitetsstatus == "gjeldende", ct);
        if (rettskilde is null)
        {
            throw new ArgumentException("Rettskilden finnes ikke. Ingen gjettet fallback.");
        }

        var noder = await db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.Tekst != null)
            .OrderBy(n => n.Sorteringsrekkefolge)
            .ToListAsync(ct);

        var header = $"# {rettskilde.Tittel}";
        var deler = new List<string>();
        var sb = new StringBuilder(header).AppendLine();
        var tegnIDenneDelen = header.Length;
        foreach (var node in noder)
        {
            var linje = $"[{node.Eid}] {node.Tekst}";
            if (tegnIDenneDelen > header.Length && tegnIDenneDelen + linje.Length > maksTegnPerDel)
            {
                deler.Add(sb.ToString());
                sb = new StringBuilder(header).AppendLine();
                tegnIDenneDelen = header.Length;
            }
            sb.AppendLine(linje);
            tegnIDenneDelen += linje.Length;
        }
        if (tegnIDenneDelen > header.Length) deler.Add(sb.ToString());
        return deler;
    }
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
                // Eid tas med slik at en agent kan sitere PRESIST hvilket ledd/punkt et forslag kom
                // fra (f.eks. Begrep.LovreferanseEid) — uten dette finnes informasjonen ikke i det
                // agenten faktisk ser, uansett hvor godt den er instruert (byggesteg 5 runde 3).
                sb.AppendLine($"[{node.Eid}] {node.Tekst}");
            }
        }
        return sb.ToString();
    }
}
