using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Hendelseregister (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1) — delt register av CPSV
/// Event/LifeEvent/BusinessEvent, koblet til Tjeneste som ren, symmetrisk klassifisering
/// (<c>cpsv:isClassifiedBy</c>), ikke en rettet relasjon (det er <see cref="TjenesteavhengighetregisterTjeneste"/>).
/// Samme stil som <see cref="BegrepsregisterTjeneste"/>: primary-constructor DI, <see cref="ArgumentException"/>
/// for domenevalidering, dual-write av domenerad + proveniensrad.
/// </summary>
public sealed class HendelseregisterTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeTyper = ["generell", "livshendelse", "virksomhetshendelse"];

    /// <summary>Lister nasjonale/delte hendelser (VirksomhetId==null) pluss, hvis angitt, den gitte virksomhetens egne lokale hendelser.</summary>
    public Task<List<HendelseEntitet>> ListerAsync(Guid? virksomhetId = null, CancellationToken ct = default) =>
        db.Hendelser.Where(h => h.Entitetsstatus == "gjeldende" && (h.VirksomhetId == null || h.VirksomhetId == virksomhetId))
            .OrderBy(h => h.Navn).ToListAsync(ct);

    public Task<HendelseEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Hendelser.FirstOrDefaultAsync(h => h.Id == id && h.Entitetsstatus == "gjeldende", ct);

    public Task<List<HendelseEntitet>> ListerForTjenesteAsync(Guid tjenesteId, CancellationToken ct = default) =>
        db.TjenesteHendelser.Where(th => th.TjenesteId == tjenesteId)
            .Join(db.Hendelser.Where(h => h.Entitetsstatus == "gjeldende"), th => th.HendelseId, h => h.Id, (_, h) => h)
            .OrderBy(h => h.Navn).ToListAsync(ct);

    public async Task<HendelseEntitet> OpprettAsync(
        Guid? virksomhetId, string navn, string type, string? beskrivelse, string opprettetAv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(navn))
        {
            throw new ArgumentException("Navn kan ikke være tomt. Ingen gjettet fallback.");
        }
        if (!GyldigeTyper.Contains(type))
        {
            throw new ArgumentException($"Ukjent type '{type}'. Gyldige verdier: {string.Join(", ", GyldigeTyper)}.");
        }

        var hendelse = new HendelseEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Navn = navn,
            Type = type,
            Beskrivelse = beskrivelse,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Hendelser.Add(hendelse);
        db.Proveniens.Add(ProveniensHjelper.NyRad("hendelse", hendelse.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return hendelse;
    }

    /// <summary>Klassifiserer en Tjeneste ved en Hendelse (<c>cpsv:isClassifiedBy</c>) — ingen retning, kun medlemskap.</summary>
    public async Task<TjenesteHendelseEntitet> KobleTilTjenesteAsync(Guid tjenesteId, Guid hendelseId, CancellationToken ct = default)
    {
        if (!await db.Tjenester.AnyAsync(t => t.Id == tjenesteId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{tjenesteId}'.");
        }
        if (!await db.Hendelser.AnyAsync(h => h.Id == hendelseId && h.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen hendelse med id '{hendelseId}'.");
        }
        if (await db.TjenesteHendelser.AnyAsync(th => th.TjenesteId == tjenesteId && th.HendelseId == hendelseId, ct))
        {
            throw new ArgumentException("Denne tjenesten er allerede klassifisert ved denne hendelsen.");
        }

        var kobling = new TjenesteHendelseEntitet { Id = Guid.NewGuid(), TjenesteId = tjenesteId, HendelseId = hendelseId };
        db.TjenesteHendelser.Add(kobling);
        await db.SaveChangesAsync(ct);
        return kobling;
    }

    public async Task<bool> FjernFraTjenesteAsync(Guid tjenesteId, Guid hendelseId, CancellationToken ct = default)
    {
        var kobling = await db.TjenesteHendelser.FirstOrDefaultAsync(
            th => th.TjenesteId == tjenesteId && th.HendelseId == hendelseId, ct);
        if (kobling is null) return false;
        db.TjenesteHendelser.Remove(kobling);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
