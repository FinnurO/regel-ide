using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// [Ny, fastsatt-av-runden, 2026-09-10, issue #215] Fyller <c>fastsatt_av</c> for rettskilder som ble
/// importert før feltet fantes.
///
/// <para>
/// Trenger ingenting eksternt og ingen rå HTML: frasen står i <see cref="RettskildeEntitet.AnnetOmDokumentet"/>,
/// som har vært lagret hele tiden. Etterfyllingen er en ren reparsing av et felt vi allerede har.
/// </para>
///
/// <para>
/// Idempotent, og trygg å kjøre på nytt: den skriver kun rader der resultatet FAKTISK er nytt, og den
/// rører <see cref="RettskildeEntitet.VirksomhetId"/> under ingen omstendighet — se
/// <see cref="Resultat.VirksomhetIdUendret"/> for hvorfor det telles og ikke bare antas.
/// </para>
/// </summary>
public sealed class FastsattAvEtterfyllingTjeneste(RegelIdeDbContext db)
{
    /// <param name="AntallRettskilder">Rader gjennomgått (alle med en hjemmelslinje).</param>
    /// <param name="Fylt">Fikk en fastsetter satt.</param>
    /// <param name="AlleredeSatt">Hadde samme verdi fra før — ingen skriving.</param>
    /// <param name="IngenFraseIKilden">Hjemmelslinja har ingen fastsettelsesfrase. Ikke en feil.</param>
    /// <param name="KongenIStatsrad">«kgl.res»/«Kronprinsreg.res» — tekst satt, men bevisst uten
    /// organnavn: Kongen i statsråd er et gruppebegrep, ikke en virksomhet (issue #215 kriterium 3).</param>
    /// <param name="VirksomhetIdUendret">Rader der <c>virksomhet_id</c> er den samme før og etter.
    /// Skal ALLTID være lik <paramref name="AntallRettskilder"/>. Issue #215 kriterium 7 ber
    /// eksplisitt om at dette verifiseres og ikke antas: å sette <c>virksomhet_id</c> på en nasjonal
    /// forskrift ville skjult den for alle andre virksomheter og for sveipene.</param>
    public sealed record Resultat(
        int AntallRettskilder,
        int Fylt,
        int AlleredeSatt,
        int IngenFraseIKilden,
        int KongenIStatsrad,
        int VirksomhetIdUendret);

    public async Task<Resultat> KjorAsync(CancellationToken ct = default)
    {
        var fylt = 0;
        var alleredeSatt = 0;
        var ingenFrase = 0;
        var kongen = 0;
        var uendret = 0;
        var antall = 0;

        // Bare radene som HAR en hjemmelslinje — resten har ingenting å parse.
        var ider = await db.Rettskilder
            .Where(r => r.AnnetOmDokumentet != null)
            .Select(r => r.Id)
            .ToListAsync(ct);

        // I bolker: 5900 rettskilder med full entitet i én ChangeTracker gir en unødvendig stor
        // sporingsgraf, og hver bolk lagres for seg slik at en avbrutt kjøring beholder det den fikk
        // gjort (og kan gjenopptas — den er idempotent).
        foreach (var bolk in ider.Chunk(500))
        {
            var rader = await db.Rettskilder.Where(r => bolk.Contains(r.Id)).ToListAsync(ct);
            foreach (var r in rader)
            {
                antall++;
                var virksomhetIdFor = r.VirksomhetId;
                var fastsatt = FastsattAvTolker.Tolk(r.AnnetOmDokumentet);

                if (fastsatt is null)
                {
                    ingenFrase++;
                }
                else if (r.FastsattAv == fastsatt.Tekst && r.FastsattAvOrgannavn == fastsatt.Organnavn)
                {
                    alleredeSatt++;
                }
                else
                {
                    r.FastsattAv = fastsatt.Tekst;
                    r.FastsattAvOrgannavn = fastsatt.Organnavn;
                    fylt++;
                }
                if (fastsatt?.ErKongenIStatsrad == true) kongen++;

                // Kriterium 7, målt per rad i stedet for antatt.
                if (r.VirksomhetId == virksomhetIdFor) uendret++;
            }
            await db.SaveChangesAsync(ct);
            // Bolken er lagret; slipp sporingen før neste.
            db.ChangeTracker.Clear();
        }

        return new Resultat(antall, fylt, alleredeSatt, ingenFrase, kongen, uendret);
    }
}
