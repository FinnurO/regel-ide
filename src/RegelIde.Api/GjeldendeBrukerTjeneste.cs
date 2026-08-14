using RegelIde.Api.Autentisering;
using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Løser "hvem skriver" for skrivende endepunkter. Selve mekanismen ligger i
/// <see cref="IBrukerkontekst"/> og velges med <c>RegelIde:Autentisering</c> — se
/// <see cref="Autentiseringsoppsett"/> og docs/autentisering.md.
/// <para>
/// Denne klassen er bevisst beholdt som en tynn statisk inngang. Rundt 36 endepunkter kaller
/// <see cref="FinnAsync"/> med nøyaktig samme mønster; å injisere grensesnittet i hver enkelt
/// lambda ville gitt en stor diff uten å endre oppførsel, og gjort det vanskeligere å se hva som
/// faktisk er nytt. Oppslaget mot request-scopet er prisen for det.
/// </para>
/// </summary>
public static class GjeldendeBrukerTjeneste
{
    /// <summary>
    /// Navnet på headeren testbruker-profilen bruker. Beholdt her fordi feilmeldingene i
    /// endepunktene refererer til den.
    /// </summary>
    public const string HeaderNavn = TestbrukerKontekst.HeaderNavn;

    public static Task<Bruker?> FinnAsync(HttpRequest request, RegelIdeDbContext db, CancellationToken ct = default)
    {
        var kontekst = request.HttpContext.RequestServices.GetRequiredService<IBrukerkontekst>();
        return kontekst.FinnAsync(request.HttpContext, ct);
    }

    /// <summary>
    /// Svaret når <see cref="FinnAsync"/> ga null. Selve meldingen og statuskoden kommer fra
    /// profilen (<see cref="IBrukerkontekst.IkkeFunnetSvar"/>) — endepunktene skrev tidligere
    /// «Mangler eller ukjent X-Bruker-Id-header» uansett profil, som er direkte villedende under
    /// Altinn-innlogging der ingen slik header finnes.
    /// </summary>
    public static IResult IkkeInnloggetSvar(HttpRequest request) =>
        request.HttpContext.RequestServices.GetRequiredService<IBrukerkontekst>().IkkeFunnetSvar();
}

/// <summary>
/// <paramref name="ErAltinnBruker"/> speiler <c>Bruker.AltinnBrukerId != null</c> — GUI-et bruker
/// dette til å skille ekte innloggede identiteter fra testbrukere (brukerhåndteringssiden), IKKE
/// til noe autorisasjonsformål.
/// </summary>
public sealed record BrukerDto(Guid Id, string Navn, Guid VirksomhetId, string VirksomhetNavn, string Rolle, bool ErAltinnBruker);

public sealed record VirksomhetDto(Guid Id, string Navn, string? Organisasjonsnummer);

/// <summary>Brukerhåndteringssiden — se BrukerregisterTjeneste.GyldigeRoller for gyldige verdier.</summary>
public sealed record OpprettBrukerRequest(string Navn, string Rolle, Guid VirksomhetId);

/// <summary>Navnet endres ikke via dette endepunktet, se BrukerregisterTjeneste.OppdaterAsync.</summary>
public sealed record OppdaterBrukerRequest(string Rolle, Guid VirksomhetId);
