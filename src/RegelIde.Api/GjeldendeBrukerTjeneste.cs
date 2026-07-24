using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Løser "hvem skriver" fra en enkel <c>X-Bruker-Id</c>-header — IKKE ekte autentisering.
/// GUI-et lar brukeren velge en testbruker og sender dens Id på hvert skrivekall, slik at
/// import/senere skriving kan attribueres (opprettet_av, virksomhet_id) uten Ansattporten ennå.
/// Se Bruker-kommentaren i RegelIde.Data/Entiteter.cs for hvordan dette byttes ut senere.
/// </summary>
public static class GjeldendeBrukerTjeneste
{
    public const string HeaderNavn = "X-Bruker-Id";

    public static async Task<Bruker?> FinnAsync(HttpRequest request, RegelIdeDbContext db, CancellationToken ct = default)
    {
        if (!request.Headers.TryGetValue(HeaderNavn, out var verdi) || !Guid.TryParse(verdi, out var brukerId))
        {
            return null;
        }
        return await db.Brukere.FirstOrDefaultAsync(b => b.Id == brukerId, ct);
    }
}

public sealed record BrukerDto(Guid Id, string Navn, Guid VirksomhetId, string VirksomhetNavn, string Rolle);

public sealed record VirksomhetDto(Guid Id, string Navn, string? Organisasjonsnummer);
