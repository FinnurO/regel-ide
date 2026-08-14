using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Brukerhåndtering (opprett/rediger + tilordning til virksomhet) — docs/13-backlog.md,
/// "identitet/brukerhåndtering"-runden (2026-08-13). Samme stil som
/// <see cref="TjenesteregisterTjeneste"/>/<see cref="BegrepsregisterTjeneste"/>.
/// <para>
/// Dekker BÅDE testbrukere og ekte Altinn-brukere — radene i <see cref="Bruker"/> skiller seg kun
/// på <see cref="Bruker.AltinnBrukerId"/> (satt for ekte innlogginger, null for testbrukere). Denne
/// tjenesten oppretter ALDRI en Altinn-tilknyttet rad — det skjer kun via selve innloggingsflyten
/// (se GjeldendeBrukerTjeneste.cs/AltinnBrukerkontekst) — men en admin kan endre rolle/virksomhet
/// på en rad uansett hvordan den ble opprettet.
/// </para>
/// </summary>
public sealed class BrukerregisterTjeneste(RegelIdeDbContext db)
{
    /// <summary>RBAC-matrisen, docs/03-domenemodell.md §2.</summary>
    public static readonly string[] GyldigeRoller = ["Fagansvarlig", "Jurist", "Systemforvalter", "Saksbehandler"];

    public Task<List<Bruker>> ListerAlleAsync(CancellationToken ct = default) =>
        db.Brukere.OrderBy(b => b.Navn).ToListAsync(ct);

    public Task<Bruker?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Brukere.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<Bruker> OpprettAsync(string navn, string rolle, Guid virksomhetId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(navn))
        {
            throw new ArgumentException("Navn kan ikke være tomt. Ingen gjettet fallback.");
        }
        ValiderRolle(rolle);
        await ValiderVirksomhetAsync(virksomhetId, ct);

        var bruker = new Bruker
        {
            Id = Guid.NewGuid(),
            Navn = navn.Trim(),
            Rolle = rolle,
            VirksomhetId = virksomhetId,
        };
        db.Brukere.Add(bruker);
        await db.SaveChangesAsync(ct);
        return bruker;
    }

    /// <summary>
    /// Endrer rolle og virksomhetstilordning. Navnet endres IKKE her — for en ekte Altinn-bruker er
    /// navnet gitt av innloggingen, og å la admin endre det ville sett ut som en tillatt handling
    /// uten faktisk å ha noen effekt neste gang personen logger inn.
    /// </summary>
    public async Task<Bruker?> OppdaterAsync(Guid id, string rolle, Guid virksomhetId, CancellationToken ct = default)
    {
        ValiderRolle(rolle);
        await ValiderVirksomhetAsync(virksomhetId, ct);

        var bruker = await db.Brukere.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bruker is null) return null;

        bruker.Rolle = rolle;
        bruker.VirksomhetId = virksomhetId;
        await db.SaveChangesAsync(ct);
        return bruker;
    }

    private static void ValiderRolle(string rolle)
    {
        if (!GyldigeRoller.Contains(rolle))
        {
            throw new ArgumentException($"Ukjent rolle '{rolle}'. Gyldige verdier: {string.Join(", ", GyldigeRoller)}.");
        }
    }

    private async Task ValiderVirksomhetAsync(Guid virksomhetId, CancellationToken ct)
    {
        if (!await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{virksomhetId}'. Ingen gjettet fallback.");
        }
    }
}
