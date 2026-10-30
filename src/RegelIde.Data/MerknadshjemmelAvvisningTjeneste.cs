using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #419] Ett forslag som avvises (eller ville blitt avvist i tørrkjøringen).</summary>
public sealed record MerknadshjemmelForslag(
    Guid KantId, string Kategori, string Typekode, string Status, string Oppdagelseskilde, string Rettskilde, string HjemmelEid);

/// <summary>[Ny, issue #419] Antall per rettskilde (hjemmelen).</summary>
public sealed record MerknadshjemmelPerRettskilde(Guid RettskildeId, string Rettskilde, int Antall);

/// <summary>
/// [Ny, issue #419] Resultatet av avvisningen. <see cref="Forslag"/> er forslagene med hjemmel i en merknadsnode (avvist når
/// <see cref="Skrevet"/>). <see cref="IkkeBerort"/> er kantene med slik hjemmel som IKKE er forslag (godkjent, faglig vurdert) — de røres ikke
/// (avgjørelse 2), men telles så de er synlige.
/// </summary>
public sealed record MerknadshjemmelAvvisningResultat(
    bool Skrevet, string Begrunnelse, int Antall, IReadOnlyList<MerknadshjemmelPerRettskilde> PerRettskilde,
    IReadOnlyDictionary<string, int> PerStatus, IReadOnlyDictionary<string, int> IkkeBerort, IReadOnlyList<MerknadshjemmelForslag> Forslag);

/// <summary>
/// [Ny, issue #419 avgjørelse 2, Johann 2026-10-29] <b>Eksisterende forslag med hjemmel i merknader eller veiledning avvises maskinelt</b>, med
/// begrunnelsen <see cref="Merknadsnoder.AvvisningBegrunnelse"/>. Bare forslag (<c>maskinelt_foreslatt</c> og <c>manuelt_foreslatt</c>);
/// godkjente og faglig vurderte kanter berøres ikke, men telles i <see cref="MerknadshjemmelAvvisningResultat.IkkeBerort"/>.
/// <para>
/// Hver kant går gjennom <see cref="StrukturkantTjeneste.AvvisAsync"/> (status <c>avvist</c>, egen proveniensrad, raden slettes ikke og
/// foreslås ikke på nytt, #374), skrevet som <see cref="Merknadsnoder.SystemBruker"/> så det synes at maskinen avviste. Uten
/// <c>skriv</c> er det en tørrkjøring som bare teller. Idempotent: en avvist kant er ikke lenger et forslag, så en ny kjøring finner 0.
/// Samme form som <c>POST /api/omrader/kommuner/kortformer?skriv=false|true</c>.
/// </para>
/// <para>
/// Forventet i den delte basen (målt 2026-10-29, kommentaren på #419): 52 forslag, alle <c>maskinelt_foreslatt</c> — tildelingsforskriften 34,
/// finansieringsforskriften 11, foreldrebetalingsforskriften 7; 0 godkjente. Forutsetter at migrasjonen <c>MerknadsnoderMerkes</c> har merket
/// nodene. [LÅST, oppdraget på #419] Kjøres mot den delte basen av hovedøkten etter fletting, ikke fra grenen.
/// </para>
/// </summary>
public sealed class MerknadshjemmelAvvisningTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanter)
{
    /// <summary>Statusene som avvises — forslag. <c>faglig_vurdert</c> er ikke med: den er vurdert av et menneske (#387).</summary>
    public static readonly string[] Avvisbare = [Strukturkanter.MaskineltForeslatt, Strukturkanter.ManueltForeslatt];

    public async Task<MerknadshjemmelAvvisningResultat> KjorAsync(bool skriv, CancellationToken ct = default)
    {
        var med = kanter.MedMerknadshjemmel(db.Strukturkanter);
        var alle = await med
            .Select(k => new { k.Id, k.Kategori, k.Typekode, k.Status, k.OppdagelsesKilde, Rettskilde = k.HjemmelRettskildeId!.Value, k.HjemmelEid })
            .OrderBy(k => k.Rettskilde).ThenBy(k => k.HjemmelEid).ThenBy(k => k.Id)
            .ToListAsync(ct);
        var ider = alle.Select(k => k.Rettskilde).Distinct().ToList();
        var titler = await db.Rettskilder.Where(r => ider.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Kortnavn ?? r.Tittel, ct);

        var forslag = alle.Where(k => Avvisbare.Contains(k.Status))
            .Select(k => new MerknadshjemmelForslag(k.Id, k.Kategori, k.Typekode, k.Status, k.OppdagelsesKilde,
                titler.GetValueOrDefault(k.Rettskilde) ?? k.Rettskilde.ToString(), k.HjemmelEid!))
            .ToList();
        var ikkeBerort = alle.Where(k => !Avvisbare.Contains(k.Status)).GroupBy(k => k.Status)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        if (skriv)
        {
            foreach (var f in forslag)
            {
                await kanter.AvvisAsync(f.KantId, Merknadsnoder.SystemBruker, Merknadsnoder.AvvisningBegrunnelse, ct);
            }
        }

        var perRettskilde = alle.Where(k => Avvisbare.Contains(k.Status)).GroupBy(k => k.Rettskilde)
            .Select(g => new MerknadshjemmelPerRettskilde(g.Key, titler.GetValueOrDefault(g.Key) ?? g.Key.ToString(), g.Count()))
            .OrderByDescending(r => r.Antall).ThenBy(r => r.Rettskilde, StringComparer.Ordinal).ToList();
        var perStatus = forslag.GroupBy(f => f.Status).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return new MerknadshjemmelAvvisningResultat(skriv, Merknadsnoder.AvvisningBegrunnelse, forslag.Count, perRettskilde, perStatus,
            ikkeBerort, forslag);
    }
}
