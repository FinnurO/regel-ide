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
    private const long MaksFilstorrelseBytes = 20 * 1024 * 1024;

    public Task<List<KunnskapsbibliotekLenkeEntitet>> ListerForVirksomhetAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.KunnskapsbibliotekLenker
            .Where(l => l.VirksomhetId == virksomhetId)
            .OrderByDescending(l => l.OpprettetTidspunkt)
            .ToListAsync(ct);

    public Task<List<KunnskapsbibliotekFilEntitet>> ListerFilerForVirksomhetAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.KunnskapsbibliotekFiler
            .Where(f => f.VirksomhetId == virksomhetId)
            .OrderByDescending(f => f.OpprettetTidspunkt)
            .Select(f => new KunnskapsbibliotekFilEntitet
            {
                Id = f.Id, VirksomhetId = f.VirksomhetId, Filnavn = f.Filnavn, Filtype = f.Filtype,
                Innhold = Array.Empty<byte>(), UtvunnetTekst = f.UtvunnetTekst, OpprettetAv = f.OpprettetAv, OpprettetTidspunkt = f.OpprettetTidspunkt,
            })
            .ToListAsync(ct);

    /// <exception cref="ArgumentException">For stor fil, ukjent filtype, eller ugyldig PDF/Word.</exception>
    /// <exception cref="InvalidOperationException">Filen mangler tekstlag (sannsynligvis et skann).</exception>
    public async Task<KunnskapsbibliotekFilEntitet> LeggTilFilAsync(
        Guid virksomhetId, string filnavn, byte[] innhold, string opprettetAv, CancellationToken ct = default)
    {
        if (innhold.LongLength > MaksFilstorrelseBytes)
        {
            throw new ArgumentException(
                $"'{filnavn}' er {innhold.LongLength / (1024 * 1024)} MB — maks tillatt størrelse er 20 MB.");
        }

        var utvunnetTekst = KunnskapsbibliotekTekstUtvinner.PrøvUtvinnTekst(innhold, filnavn);
        var filtype = Path.GetExtension(filnavn).TrimStart('.').ToLowerInvariant();

        var fil = new KunnskapsbibliotekFilEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Filnavn = filnavn,
            Filtype = filtype,
            Innhold = innhold,
            UtvunnetTekst = utvunnetTekst,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.KunnskapsbibliotekFiler.Add(fil);
        await db.SaveChangesAsync(ct);
        return fil;
    }

    public async Task<bool> SlettFilAsync(Guid id, CancellationToken ct = default)
    {
        var fil = await db.KunnskapsbibliotekFiler.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fil is null) return false;
        db.KunnskapsbibliotekFiler.Remove(fil);
        await db.SaveChangesAsync(ct);
        return true;
    }

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
