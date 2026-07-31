using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Autentisering;

/// <summary>
/// Sømmen mellom "hvem er dette" og resten av API-et. Alle skrivende endepunkter går gjennom
/// denne ene metoden (via <see cref="GjeldendeBrukerTjeneste"/>), slik at bytte av
/// autentiseringsmekanisme ikke rører kallstedene.
/// </summary>
public interface IBrukerkontekst
{
    /// <summary>Returnerer brukeren forespørselen skal attribueres til, eller null hvis den ikke kan identifiseres.</summary>
    Task<Bruker?> FinnAsync(HttpContext kontekst, CancellationToken ct = default);
}

/// <summary>
/// Dagens oppførsel, uendret: <c>X-Bruker-Id</c>-header slått opp i brukertabellen.
/// IKKE autentisering — se <see cref="Autentiseringsprofil.Testbruker"/>.
/// </summary>
public sealed class TestbrukerKontekst(RegelIdeDbContext db) : IBrukerkontekst
{
    public const string HeaderNavn = "X-Bruker-Id";

    public async Task<Bruker?> FinnAsync(HttpContext kontekst, CancellationToken ct = default)
    {
        if (!kontekst.Request.Headers.TryGetValue(HeaderNavn, out var verdi)
            || !Guid.TryParse(verdi, out var brukerId))
        {
            return null;
        }
        return await db.Brukere.FirstOrDefaultAsync(b => b.Id == brukerId, ct);
    }
}
