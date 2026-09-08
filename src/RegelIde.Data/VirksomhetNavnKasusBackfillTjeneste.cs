using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Engangs-/gjentakbar tilbakefylling av <see cref="Virksomhet.Navn"/> for rader som ble seedet med den
/// gamle, feilaktige ledd-kasingen i <see cref="OrganisasjonsregisterSeed"/> (se
/// <c>FormaterNavnEnkelt</c>s egen <c>[FIKSET]</c>-kommentar). Den gamle varianten satte stor forbokstav
/// på KUN tegn 0 i hele strengen, så de 7 samiske dobbeltnavnene i <c>organisasjoner-norge.json</c> —
/// to LIKESTILTE navneledd skilt med <c>" / "</c> — endte med småbokstav på det andre leddet:
/// «Karasjoga gielda / karasjok kommune» i stedet for «Karasjoga gielda / Karasjok kommune».
/// Johann oppdaget dette på virksomhetssiden for Karasjok.
///
/// <para>
/// Å rette <c>FormaterNavnEnkelt</c> alene er ikke nok: seeden skriver <see cref="Virksomhet.Navn"/> KUN
/// når raden OPPRETTES (se <c>match is null</c>-grenen der — en eksisterende rad får aldri navnet
/// overskrevet, nettopp for ikke å klobre manuelle rettelser). De allerede seedede radene ville derfor
/// beholdt det gale navnet for alltid. Denne tjenesten er den målrettede motparten, samme rolle som
/// <see cref="AnsvarligDepartementBackfillTjeneste"/> har for AnsvarligDepartement-kolonnen.
/// </para>
///
/// <para>
/// **Idempotent** — den beregner den korrigerte formen og skriver KUN når den faktisk skiller seg fra det
/// lagrede navnet. Andre kjøring finner ingenting å gjøre og returnerer 0. Den lowercaser ALDRI noe (til
/// forskjell fra <c>FormaterNavnEnkelt</c>, som jobber på VERSAL-kildedata): den setter utelukkende stor
/// forbokstav på hvert <c>" / "</c>-skilte ledd, og lar alt annet i navnet stå ordrett. Dermed kan den
/// ikke ødelegge en rad noen har rettet manuelt til noe annet enn kildeformen.
/// </para>
///
/// <para>
/// **Rører ALDRI en Brreg-synkronisert rad** — avgrenset til
/// <see cref="Virksomhet.SistBrregSynkronisert"/> <c>== null</c> (altså seedede rader). Issue #158 låser
/// at et navn hentet fra Brreg beholdes uendret i REGISTERETS egen form, og Brregs form er VERSALER.
/// Den ene reelle raden dette gjelder er «SAMEDIGGI / SAMETINGET» (orgnr 974760347, synkronisert
/// 2026-08-29): den inneholder <c>" / "</c> og ville ellers vært en kandidat, men skal forbli akkurat
/// slik registeret staver den. Merk at den også er utenfor rekkevidde av selve kasus-regelen (begge ledd
/// har allerede stor forbokstav) — to uavhengige grunner som peker samme vei, men
/// <see cref="Virksomhet.SistBrregSynkronisert"/>-filteret er det bindende, siden det er policyen.
/// </para>
/// </summary>
public static class VirksomhetNavnKasusBackfillTjeneste
{
    /// <summary>Ett rettet navn — for oppstartslogging, slik at endringen er etterprøvbar i loggen.</summary>
    public sealed record RettetNavn(Guid VirksomhetId, string Fra, string Til);

    /// <summary>
    /// Retter alle berørte, ikke-Brreg-synkroniserte rader. Returnerer de faktisk endrede radene
    /// (tom liste ⇒ ingenting å gjøre, det normale ved andre og senere kjøring).
    /// </summary>
    public static async Task<IReadOnlyList<RettetNavn>> KjorAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        // Kun rader som i det hele tatt KAN være berørt: har ledd-skillet, og er ikke Brreg-synkronisert.
        // Filtrert i databasen framfor å laste hele katalogen (500+ rader) for et 7-raders treff.
        var kandidater = await db.Virksomheter
            .Where(v => v.SistBrregSynkronisert == null && v.Navn.Contains(OrganisasjonsregisterSeed.LeddSkille))
            .ToListAsync(ct);

        var rettet = new List<RettetNavn>();
        foreach (var virksomhet in kandidater)
        {
            var korrigert = OrganisasjonsregisterSeed.StorForbokstavPerLedd(virksomhet.Navn);
            if (korrigert == virksomhet.Navn) continue; // allerede riktig — idempotensen, se klassekommentaren.

            rettet.Add(new RettetNavn(virksomhet.Id, virksomhet.Navn, korrigert));
            virksomhet.Navn = korrigert;
        }

        if (rettet.Count > 0) await db.SaveChangesAsync(ct);
        return rettet;
    }
}
