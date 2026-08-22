using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Arbeidskø for godkjenning av virksomhetsforekomster funnet ved tekstsøk (docs/20 §2.6). Denne
/// klassen er selve KØEN (opprett/liste/godkjenn/avvis/hardslett) — IKKE selve sveipefunksjonen
/// (tekstsøk gjennom alle rettskilder etter <see cref="BegrepEntitet"/>-strenger, docs/20 §5/kravspek
/// §4.2 pkt. 1) — den er et eget, større stykke arbeid (må finne og retunere presis nodeposisjon for
/// hvert treff) og bygges i en senere runde.
/// <para>
/// <see cref="GodkjennAsync"/> setter i DAG bare status til `'Godkjent'` — å opprette den faktiske
/// <see cref="TekstTaggEntitet"/>-forekomsten (kravspek §4.2 pkt. 5) krever et fullt quoteSelector
/// (start/slutt-offset, sitatkontekst, tekst-hash), som denne entiteten ikke lagrer i denne runden.
/// Legges til når sveipefunksjonen bygges (den har allerede denne informasjonen på treff-tidspunktet).
/// </para>
/// </summary>
public sealed class VirksomhetKandidatTjeneste(RegelIdeDbContext db)
{
    /// <summary>Idempotent — samme (virksomhet, rettskilde, node) gir samme rad tilbake i stedet for
    /// et duplikat, uansett status (docs/20 §2.6: en Avvist-rad skal IKKE dukke opp igjen ved neste
    /// sveip; se den unike indeksen i RegelIdeDbContext).</summary>
    public async Task<VirksomhetKandidatEntitet> OpprettEllerFinnAsync(
        Guid virksomhetId, Guid rettskildeId, string nodeEid, string opprettetAv, CancellationToken ct = default)
    {
        var eksisterende = await db.VirksomhetKandidater.FirstOrDefaultAsync(
            k => k.VirksomhetId == virksomhetId && k.RettskildeId == rettskildeId && k.NodeEid == nodeEid, ct);
        if (eksisterende is not null) return eksisterende;

        if (!await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{virksomhetId}'. Ingen gjettet fallback.");
        }
        if (!await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == rettskildeId && n.Eid == nodeEid, ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde-node med eId '{nodeEid}' i rettskilde '{rettskildeId}'. Ingen gjettet fallback.");
        }

        var kandidat = new VirksomhetKandidatEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            RettskildeId = rettskildeId,
            NodeEid = nodeEid,
            Status = "Venter",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.VirksomhetKandidater.Add(kandidat);
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>Kun `'Venter'`-rader (docs/20 §2.6) — det andre statusene er nettopp for å IKKE vises
    /// igjen. Valgfritt filtrert til én virksomhet og/eller én rettskilde (kravspek §4.2 pkt. 3/4).</summary>
    public Task<List<VirksomhetKandidatEntitet>> ListerVentendeAsync(
        Guid? virksomhetId = null, Guid? rettskildeId = null, CancellationToken ct = default)
    {
        var spørring = db.VirksomhetKandidater.Where(k => k.Status == "Venter");
        if (virksomhetId is not null) spørring = spørring.Where(k => k.VirksomhetId == virksomhetId);
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        return spørring.ToListAsync(ct);
    }

    public async Task<VirksomhetKandidatEntitet?> GodkjennAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.VirksomhetKandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        kandidat.Status = "Godkjent";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    public async Task<VirksomhetKandidatEntitet?> AvvisAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.VirksomhetKandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        kandidat.Status = "Avvist";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>Hardsletting — kun for `'Avvist'`-rader (docs/20 §2.6: "kan hardslettes manuelt", et
    /// eksplisitt unntak fra husstilens vanlige mykslette/Entitetsstatus-mønster, se klassekommentaren
    /// på <see cref="VirksomhetKandidatEntitet"/>). `'Venter'`/`'Godkjent'` skal IKKE kunne hardslettes
    /// herfra — en Venter-rad skal behandles (godkjennes/avvises), ikke bare forsvinne.</summary>
    public async Task<bool> HardslettAvvistAsync(Guid id, CancellationToken ct = default)
    {
        var kandidat = await db.VirksomhetKandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return false;
        if (kandidat.Status != "Avvist")
        {
            throw new ArgumentException("Kun avviste kandidater kan hardslettes. Godkjenn eller avvis først.");
        }
        db.VirksomhetKandidater.Remove(kandidat);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
