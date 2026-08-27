using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, 2026-08-27, Tjenestedetalj-redesignrunden] Sekundær "også brukt av"-kobling mellom
/// <see cref="HandlingEntitet"/> og <see cref="TjenesteEntitet"/> — se
/// <see cref="HandlingTjenesteEntitet"/>s klassekommentar for hvorfor dette IKKE er eierskap.
/// Sideordnet <see cref="HandlingregisterTjeneste"/>, samme "ingen gjettet fallback"-stil.
/// <para>
/// Sikkerhetsscoping: <see cref="SokRegisterAsync"/> (søket bak "koble eksisterende handling")
/// begrenser seg til handlinger EGEN virksomhet eier — samme lukkede utgangspunkt som Tjeneste
/// selv har for skriving, IKKE det åpne-data-mønsteret rettskilder/kodelister/hendelser bruker.
/// En handling er forfattet innhold tilhørende én virksomhet; å la enhver virksomhet søke opp og
/// lenke inn EN ANNEN virksomhets handlinger ville vært en ny, ikke bedt om datalekkasje.
/// </para>
/// </summary>
public sealed class HandlingTjenesteregisterTjeneste(RegelIdeDbContext db)
{
    /// <summary>
    /// Handlinger en tjeneste faktisk skal vise i sin Handlinger-fane: de den EIER
    /// (<see cref="HandlingEntitet.TjenesteId"/>) UNIONERT med de den er sekundært KOBLET til via
    /// <see cref="HandlingTjenesteEntitet"/>. Ingen duplikater — en handling kan i prinsippet være
    /// koblet til en tjeneste den også eier (meningsløst, men ufarlig); <c>Distinct</c> på id
    /// håndterer det uten en egen valideringsregel mot det.
    /// </summary>
    public async Task<List<HandlingEntitet>> HentForTjenesteAsync(Guid tjenesteId, CancellationToken ct = default)
    {
        var eide = db.Handlinger.Where(h => h.TjenesteId == tjenesteId && h.Entitetsstatus == "gjeldende");
        var koblede = db.HandlingTjenester
            .Where(k => k.TjenesteId == tjenesteId)
            .Join(db.Handlinger.Where(h => h.Entitetsstatus == "gjeldende"), k => k.HandlingId, h => h.Id, (k, h) => h);

        var handlinger = await eide.Concat(koblede).Distinct().ToListAsync(ct);
        return handlinger.OrderBy(h => h.Navn).ToList();
    }

    /// <summary>
    /// Søk blant EGEN virksomhets handlinger (uansett hvilken tjeneste de er eid av) — kandidatlisten
    /// for "koble eksisterende handling". Enkelt <c>ToLower().Contains()</c>-søk, samme presedens som
    /// <see cref="TjenesteregisterTjeneste.SokTverrTenantAsync"/> — antallet handlinger er i dag i
    /// beste fall noen hundre, ingen paginering/skala-teknikk nødvendig ennå (se docs/09 §10 for når
    /// den grensen faktisk ble reell for rettskilder, til sammenligning).
    /// </summary>
    public async Task<List<HandlingEntitet>> SokRegisterAsync(Guid virksomhetId, string sokestreng, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sokestreng)) return [];
        var lav = sokestreng.ToLower();
        return await db.Handlinger
            .Join(db.Tjenester, h => h.TjenesteId, t => t.Id, (h, t) => new { Handling = h, t.VirksomhetId })
            .Where(x => x.VirksomhetId == virksomhetId && x.Handling.Entitetsstatus == "gjeldende"
                        && x.Handling.Navn.ToLower().Contains(lav))
            .OrderBy(x => x.Handling.Navn)
            .Take(50)
            .Select(x => x.Handling)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Kobler en EKSISTERENDE handling (som virksomheten selv eier, se <see cref="SokRegisterAsync"/>)
    /// til en ANNEN av virksomhetens tjenester. Idempotent — no-op (returnerer eksisterende rad om
    /// den finnes) i stedet for å kaste, siden brukeren i praksis bare ønsker "denne skal være
    /// koblet", ikke en feilmelding om den allerede er det. Kaster fortsatt hvis <paramref name="tjenesteId"/>
    /// FAKTISK eier handlingen — «koble til seg selv» er meningsløst og et tegn på en klientfeil.
    /// </summary>
    public async Task<HandlingTjenesteEntitet> KobleAsync(Guid tjenesteId, Guid virksomhetId, Guid handlingId, CancellationToken ct = default)
    {
        if (!await db.Tjenester.AnyAsync(t => t.Id == tjenesteId && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{tjenesteId}' for denne virksomheten. Ingen gjettet fallback.");
        }
        var handling = await db.Handlinger
            .Join(db.Tjenester, h => h.TjenesteId, t => t.Id, (h, t) => new { Handling = h, t.VirksomhetId })
            .Where(x => x.Handling.Id == handlingId && x.VirksomhetId == virksomhetId && x.Handling.Entitetsstatus == "gjeldende")
            .Select(x => x.Handling)
            .FirstOrDefaultAsync(ct);
        if (handling is null)
        {
            throw new ArgumentException($"Fant ingen handling med id '{handlingId}' som denne virksomheten eier. Ingen gjettet fallback.");
        }
        if (handling.TjenesteId == tjenesteId)
        {
            throw new ArgumentException("Tjenesten eier allerede denne handlingen — kobling til seg selv gir ikke mening.");
        }

        var eksisterende = await db.HandlingTjenester.FirstOrDefaultAsync(
            k => k.HandlingId == handlingId && k.TjenesteId == tjenesteId, ct);
        if (eksisterende is not null) return eksisterende;

        var kobling = new HandlingTjenesteEntitet { Id = Guid.NewGuid(), HandlingId = handlingId, TjenesteId = tjenesteId };
        db.HandlingTjenester.Add(kobling);
        await db.SaveChangesAsync(ct);
        return kobling;
    }

    /// <summary>Fjerner KUN koblingsraden — selve Handlingen (og eierskapet) er urørt, se klassekommentaren.</summary>
    public async Task<bool> FjernKoblingAsync(Guid koblingId, Guid virksomhetId, CancellationToken ct = default)
    {
        var kobling = await db.HandlingTjenester
            .Join(db.Tjenester, k => k.TjenesteId, t => t.Id, (k, t) => new { Kobling = k, t.VirksomhetId })
            .Where(x => x.Kobling.Id == koblingId && x.VirksomhetId == virksomhetId)
            .Select(x => x.Kobling)
            .FirstOrDefaultAsync(ct);
        if (kobling is null) return false;

        db.HandlingTjenester.Remove(kobling);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
