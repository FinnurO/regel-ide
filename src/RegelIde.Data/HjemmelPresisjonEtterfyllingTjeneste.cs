using System.Text;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// [Ny, hjemmel-presisjon-runden, 2026-09-10, issue #217] Oppgraderer EKSISTERENDE hjemmelrader fra
/// paragrafnivå til den noden kilden faktisk presiserer.
///
/// <para>
/// Hvorfor en tjeneste og ikke en SQL-migrasjon: presisjonen finnes bare i den rå Lovdata-HTML-en
/// (<see cref="RettskildeEntitet.Innhold"/>, lagret siden PR #84), og den må parses. En migrasjon kan
/// ikke gjøre det. Til gjengjeld trengs INGEN ny henting fra Lovdata — kilden vi importerte fra ligger
/// i basen.
/// </para>
///
/// <para>
/// Idempotent: en rad som allerede peker på en ledd-node har ingen presisering å hente og hoppes
/// over. Kan derfor kjøres flere ganger, og kjøres på nytt etter at flere lover er importert (en
/// presisering som ikke kunne løses fordi loven manglet, løses da).
/// </para>
///
/// <para>
/// Rapporterer tall for hver utgang, ikke bare «ferdig» — issue #217 kriterium 6 ber om før/etter, og
/// forskjellen mellom «forble paragraf fordi kilden ikke presiserte noe» (riktig) og «presiseringen
/// kunne ikke løses» (et synlig tap) er hele poenget.
/// </para>
/// </summary>
public sealed class HjemmelPresisjonEtterfyllingTjeneste(RegelIdeDbContext db)
{
    /// <param name="AntallHjemler">Hjemmelrader gjennomgått.</param>
    /// <param name="AlleredePresise">Pekte alt på en dypere node enn paragrafen — ingenting å gjøre.</param>
    /// <param name="Oppgradert">Peker nå på den presiserte noden i stedet for paragrafen.</param>
    /// <param name="UtenPresiseringIKilden">Kilden presiserte ingenting. Paragrafnivå er da RIKTIG, ikke en mangel.</param>
    /// <param name="PresiseringKunneIkkeLoses">Kilden presiserte, men noden finnes ikke (loven er ikke importert, eller
    /// nodetreet går ikke så dypt). Lagret i <c>ulost_presisering</c> slik at tapet er synlig.</param>
    /// <param name="UtenRaaKilde">Rettskilder uten lagret rå HTML (importert før PR #84, eller referanse-stubber).</param>
    public sealed record Resultat(
        int AntallHjemler,
        int AlleredePresise,
        int Oppgradert,
        int UtenPresiseringIKilden,
        int PresiseringKunneIkkeLoses,
        int UtenRaaKilde);

    public async Task<Resultat> KjorAsync(CancellationToken ct = default)
    {
        var alleredePresise = 0;
        var oppgradert = 0;
        var utenPresisering = 0;
        var ulost = 0;
        var utenRaaKilde = 0;
        var antall = 0;

        // Gruppert per rettskilde: den rå HTML-en parses ÉN gang per dokument, ikke én gang per
        // hjemmelrad. Et dokument har typisk 1–20 hjemler.
        var rettskildeIder = await db.RettskildeHjemler
            .Select(h => h.RettskildeId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var rettskildeId in rettskildeIder)
        {
            var hjemler = await db.RettskildeHjemler.Where(h => h.RettskildeId == rettskildeId).ToListAsync(ct);
            antall += hjemler.Count;

            // En rad som alt er dypere enn paragrafnivå er ferdig. Kjennetegnet er et segment ETTER
            // paragrafen — «…/§13-1/ledd-4» har det, «…/§13-1» ikke.
            var ufullstendige = hjemler.Where(h => !HarPresisjon(h.HjemmelEid)).ToList();
            alleredePresise += hjemler.Count - ufullstendige.Count;
            if (ufullstendige.Count == 0) continue;

            var raa = await db.Rettskilder
                .Where(r => r.Id == rettskildeId)
                .Select(r => r.Innhold)
                .FirstOrDefaultAsync(ct);
            if (raa is null)
            {
                utenRaaKilde++;
                utenPresisering += ufullstendige.Count;
                continue;
            }

            var presiseringer = LovdataHtmlParser.TolkHjemmelPresiseringer(Encoding.UTF8.GetString(raa));
            foreach (var h in ufullstendige)
            {
                if (!presiseringer.TryGetValue(h.HjemmelEid, out var presisering))
                {
                    utenPresisering++;
                    continue;
                }

                var (nyEid, restUlost) = await LosAsync(h.HjemmelEid, presisering, h.HjemmelRettskildeId, ct);
                if (nyEid == h.HjemmelEid)
                {
                    h.UlostPresisering = restUlost;
                    ulost++;
                    continue;
                }
                h.HjemmelEid = nyEid;
                h.UlostPresisering = restUlost;
                oppgradert++;
            }
            await db.SaveChangesAsync(ct);
        }

        return new Resultat(antall, alleredePresise, oppgradert, utenPresisering, ulost, utenRaaKilde);
    }

    /// <summary>Har eId-en et segment etter paragrafen? «…/§13-1/ledd-4» ja, «…/§13-1» nei.</summary>
    private static bool HarPresisjon(string eid)
    {
        var idx = eid.IndexOf("/§", StringComparison.Ordinal);
        return idx >= 0 && eid.IndexOf('/', idx + 2) >= 0;
    }

    /// <summary>
    /// Samme oppløsning som <c>RettskildeImportTjeneste.LosPresiseringAsync</c> — dypest først, mot
    /// EKTE noder, aldri mot en konstruert eId. Bevisst duplisert i stedet for delt: importstien
    /// arbeider på uskrevne <c>RettskildeHjemmel</c>-records i en pågående transaksjon, denne på
    /// lagrede rader, og å tvinge dem gjennom én signatur ville gjort begge vanskeligere å lese enn de
    /// tolv linjene sparer. Endres den ene, skal den andre endres med — derfor denne merknaden.
    /// </summary>
    private async Task<(string Eid, string? Ulost)> LosAsync(
        string paragrafEid, string presisering, Guid hjemmelRettskildeId, CancellationToken ct)
    {
        var deler = presisering.Split('/');
        var segmenter = new List<string>();
        for (var i = 0; i + 1 < deler.Length; i += 2)
        {
            segmenter.Add($"{deler[i]}-{deler[i + 1]}");
        }

        for (var dybde = segmenter.Count; dybde > 0; dybde--)
        {
            var kandidat = $"{paragrafEid}/{string.Join('/', segmenter.Take(dybde))}";
            if (await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == hjemmelRettskildeId && n.Eid == kandidat, ct))
            {
                return (kandidat, dybde == segmenter.Count ? null : string.Join('/', segmenter.Skip(dybde)));
            }
        }
        return (paragrafEid, segmenter.Count > 0 ? string.Join('/', segmenter) : presisering);
    }
}
