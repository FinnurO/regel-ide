using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// «Brukt i rettskilder» — et EKTE reverse-oppslag for et <see cref="BegrepEntitet"/>, analogt til den
/// eksisterende «Brukt i vilkår»-seksjonen i <c>BegrepDetalj.tsx</c> (som lister
/// <see cref="VilkarEntitet"/> som refererer begrepet via <c>BegrepId</c>/<c>SkjonnsgrunnlagBegrepId</c>),
/// men for selve lovteksten: søker gjennom <see cref="RettskildeNodeEntitet.Tekst"/> etter FAKTISKE
/// forekomster av begrepets <see cref="BegrepEntitet.Term"/>.
/// <para>
/// <b>IKKE det samme som <see cref="BegrepEntitet.LovreferanseEid"/></b> — det feltet er én enkelt,
/// manuelt satt referanse (juristens «dette begrepet stammer fra HER»). Denne tjenesten er derimot et
/// upartisk søk: ALLE steder i korpuset der Termen faktisk forekommer, uansett om noen har koblet den
/// dit manuelt.
/// </para>
/// <para>
/// <b>Ordgrense, ikke substreng</b> — samme presisjonsprinsipp og samme regex-mønster
/// (<c>\b...\b</c>, <see cref="RegexOptions.IgnoreCase"/>) som allerede brukt i
/// <see cref="VirksomhetKandidatSveipTjeneste"/>/<see cref="NavnekandidatOppdagelseTjeneste"/> —
/// gjenbrukt her, IKKE reimplementert fra bunnen av. Uten ordgrense ville f.eks. et faktabegrep
/// "bil" gitt falske treff inni "mobil".
/// </para>
/// <para>
/// <b>To-stegs søk</b>: (1) en billig <c>ILIKE '%term%'</c>-forspørring i databasen henter
/// KANDIDATNODER (kan inneholde falske positiver — ILIKE har ingen ordgrense, så et substreng-treff i
/// et lengre ord slipper gjennom dette steget), (2) presis ordgrense-regex filtrerer disse i minnet
/// akkurat som sveipenes egne mønstre. Forskjellen fra sveipene: her er selve søkeordet allerede kjent
/// PRESIST på forhånd (ikke et ukjent mønster over hele korpuset), så en DB-side ILIKE er en fornuftig
/// forhåndsfiltrering fremfor å hente og regex-skanne HVER ENESTE node i korpuset.
/// </para>
/// <para>
/// <b>Scope</b>: samme «node OG eiende rettskilde må begge være <c>Entitetsstatus == "gjeldende"</c>»-
/// filter som søsterklassene (se <see cref="NavnekandidatOppdagelseTjeneste"/> sin klassekommentar for
/// hvorfor BEGGE trengs — en reimportert rettskildes GAMLE <see cref="RettskildeEntitet"/>-rad blir
/// «erstattet», men dens <see cref="RettskildeNodeEntitet"/>-rader forblir for alltid «gjeldende»).
/// Bevisst IKKE begrenset til delt/nasjonal (<c>VirksomhetId == null</c>) rettskilde slik sveipene er —
/// den begrensningen der beskytter en VIRKSOMHETSSPESIFIKK godkjenningskø mot kryssvirksomhet-lekkasje;
/// her er formålet en ren, allment lesbar visning av et begrep (samme åpenhetsprinsipp som
/// <c>RettskildeRepository</c> allerede har for publiserte rettskilder generelt).
/// </para>
/// <para>
/// <b>Antallsgrense</b> (<see cref="MaksAntallTreff"/>): et faktabegrep sin Term kan i prinsippet være
/// et helt vanlig ord/uttrykk brukt svært mange steder i korpuset — uten en grense kunne ett enkelt
/// begrep gitt et enormt svar. Søket stopper så snart grensen er nådd (ingen fullstendig telling utover
/// det), en dokumentert begrensning, ikke en skjult trunkering.
/// </para>
/// </summary>
public sealed class BegrepBruktIRettskilderTjeneste(RegelIdeDbContext db)
{
    /// <summary>Maks antall treff returnert per kall — se klassekommentaren.</summary>
    public const int MaksAntallTreff = 50;

    /// <summary>Antall tegn kontekst vist på hver side av selve treffet i <see cref="BegrepBruktIRettskildeTreff.Snippet"/>.</summary>
    private const int SnippetKontekst = 40;

    public async Task<List<BegrepBruktIRettskildeTreff>> FinnAsync(Guid begrepId, CancellationToken ct = default)
    {
        var begrep = await db.Begreper.FirstOrDefaultAsync(b => b.Id == begrepId && b.Entitetsstatus == "gjeldende", ct);
        if (begrep is null) return [];

        var mønster = new Regex(@"\b" + Regex.Escape(begrep.Term) + @"\b", RegexOptions.IgnoreCase);

        var likeMønster = $"%{begrep.Term}%";
        var kandidatNoder = await db.RettskildeNoder
            .Join(db.Rettskilder, n => n.RettskildeId, r => r.Id, (n, r) => new { Node = n, Rettskilde = r })
            .Where(x => x.Node.Tekst != null && !x.Node.Opphevet && x.Node.Entitetsstatus == "gjeldende"
                        && x.Rettskilde.Entitetsstatus == "gjeldende"
                        && EF.Functions.ILike(x.Node.Tekst!, likeMønster))
            .OrderBy(x => x.Rettskilde.Tittel).ThenBy(x => x.Node.Eid)
            .Select(x => new { x.Node.RettskildeId, x.Node.Eid, x.Node.Tekst, x.Rettskilde.Tittel, x.Rettskilde.Kortnavn })
            .ToListAsync(ct);

        var treff = new List<BegrepBruktIRettskildeTreff>();
        foreach (var node in kandidatNoder)
        {
            foreach (Match m in mønster.Matches(node.Tekst!))
            {
                treff.Add(new BegrepBruktIRettskildeTreff(
                    node.RettskildeId, node.Eid, node.Kortnavn ?? node.Tittel, LagSnippet(node.Tekst!, m.Index, m.Length)));
                if (treff.Count >= MaksAntallTreff) return treff;
            }
        }
        return treff;
    }

    /// <summary>Enkel <c>Substring</c>-basert utsnitt rundt treffet (ikke <see cref="NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst"/>-
    /// mønsteret — dette er bare visning, ikke en købar kandidat med sitat-relokering ved reimport).</summary>
    private static string LagSnippet(string tekst, int start, int lengde)
    {
        var fra = Math.Max(0, start - SnippetKontekst);
        var til = Math.Min(tekst.Length, start + lengde + SnippetKontekst);
        var prefiks = fra > 0 ? "…" : "";
        var suffiks = til < tekst.Length ? "…" : "";
        return prefiks + tekst[fra..til] + suffiks;
    }
}

/// <summary>Ett treff fra <see cref="BegrepBruktIRettskilderTjeneste.FinnAsync"/>. <see cref="RettskildeTittel"/>
/// er kortnavnet hvis satt, ellers full tittel — samme fallback som ellers i appen (f.eks.
/// <c>eidLenker.ts</c> sin <c>eidVisningstekst</c>).</summary>
public sealed record BegrepBruktIRettskildeTreff(Guid RettskildeId, string NodeEid, string RettskildeTittel, string Snippet);
