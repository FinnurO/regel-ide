using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Kunnskapsbibliotek (byggesteg 5 runde 1, docs/06-veikart.md "Byggesteg 5 — AI-forslag") — CRUD for
/// <see cref="KunnskapsbibliotekLenkeEntitet"/>. Kun brukt av «Identifiser tjenester»-agenten
/// (<see cref="TjenesteforslagTjeneste"/>) som ekstra kontekst utover valgte rettskilder. Samme stil
/// som andre registertjenester: primary-constructor DI, <see cref="ArgumentException"/> for
/// domenevalidering, ingen gjettet fallback.
/// </summary>
public sealed class KunnskapsbibliotekTjeneste(RegelIdeDbContext db)
{
    public Task<List<KunnskapsbibliotekLenkeEntitet>> ListerForVirksomhetAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.KunnskapsbibliotekLenker
            .Where(l => l.VirksomhetId == virksomhetId)
            .OrderByDescending(l => l.OpprettetTidspunkt)
            .ToListAsync(ct);

    public async Task<KunnskapsbibliotekLenkeEntitet> LeggTilLenkeAsync(
        Guid virksomhetId, string url, string? beskrivelse, string opprettetAv, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || (parsed.Scheme != "http" && parsed.Scheme != "https"))
        {
            throw new ArgumentException($"Ugyldig URL '{url}'. Må være en absolutt http(s)-URL. Ingen gjettet fallback.");
        }

        var lenke = new KunnskapsbibliotekLenkeEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Url = url,
            Beskrivelse = beskrivelse,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.KunnskapsbibliotekLenker.Add(lenke);
        await db.SaveChangesAsync(ct);
        return lenke;
    }

    public async Task<bool> SlettAsync(Guid id, CancellationToken ct = default)
    {
        var lenke = await db.KunnskapsbibliotekLenker.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lenke is null) return false;
        db.KunnskapsbibliotekLenker.Remove(lenke);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
