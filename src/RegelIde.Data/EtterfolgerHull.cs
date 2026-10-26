using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, Johanns avgjørelse S3 på #397, 2026-10-27] «Hovedetterfølger ukjent»: et nedlagt departement uten en etterfølger som tar over
/// HOVEDANSVARET. Det er et synlig hull, og ingen etterfølger gjettes. Hullet regnes ut og lagres ikke. Det fylles når en av disse
/// etterfølgerkantene (R etterfolger til aktøren, ikke avvist) finnes:
/// <list type="bullet">
/// <item>en avledet etterfølger, der lovdelene gikk over (<c>monster:livslop-etterfolger</c>),</item>
/// <item>en etterfølger lest av «overtar alle arbeidsoppgavene til» eller «de resterende oppgavene til» (hele resten),</item>
/// <item>en etterfølger som er faglig vurdert eller godkjent, uansett kilde. Hullet kan fylles manuelt.</item>
/// </list>
/// Delvise etterfølgere lest av teksten, for eksempel «overtar ansvaret for kyst- og miljøsaker fra X», fyller ikke hullet
/// (Fiskeri- og kystdepartementet). Det gjør heller ingen etterfølger (Sosial- og helsedepartementet).
/// </summary>
public static class EtterfolgerHull
{
    public static async Task<HashSet<Guid>> HovedetterfolgerUkjentAsync(RegelIdeDbContext db, IEnumerable<Guid> nedlagteIder, CancellationToken ct = default)
    {
        var ider = nedlagteIder.Distinct().ToList();
        if (ider.Count == 0) return [];
        var dekket = await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Relasjon && k.Typekode == "etterfolger" && k.TilVirksomhetId != null && ider.Contains(k.TilVirksomhetId.Value)
                && (k.OppdagelsesKilde == "monster:livslop-etterfolger"
                    || (k.OppdagelsesKilde == "monster:livslop-etterfolger-tekst" && k.Begrunnelse != null
                        && k.Begrunnelse.Contains("monster:livslop-overtar-alle-oppgavene-til"))
                    || k.Status == Strukturkanter.FagligVurdert || k.Status == Strukturkanter.Godkjent))
            .Select(k => k.TilVirksomhetId!.Value).Distinct().ToListAsync(ct);
        return ider.Except(dekket).ToHashSet();
    }
}
