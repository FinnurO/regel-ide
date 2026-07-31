using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Autentisering;

/// <summary>
/// Oversetter en innlogget Altinn-bruker til en <see cref="Bruker"/>-rad.
/// <para>
/// Selve valideringen av <c>AltinnStudioRuntime</c>-cookien gjør JwtBearer (se
/// <see cref="Autentiseringsoppsett"/>); her handler det bare om å gå fra validerte claims til
/// en rad vi kan attribuere arbeid til. Raden opprettes ved første innlogging, fordi en ekte
/// bruker ikke finnes i tabellen på forhånd.
/// </para>
/// </summary>
public sealed class AltinnBrukerkontekst(
    RegelIdeDbContext db,
    Altinninnstillinger innstillinger,
    IAltinnRolleoppslag rolleoppslag) : IBrukerkontekst
{
    /// <summary>Claim-navnene Altinn-plattformen legger i runtime-tokenet.</summary>
    public const string BrukerIdClaim = "urn:altinn:userid";
    public const string BrukernavnClaim = "urn:altinn:username";

    /// <summary>Avgiveren som er valgt — organisasjonens party når brukeren representerer en virksomhet, ellers personens egen.</summary>
    public const string PartyIdClaim = "urn:altinn:partyid";

    /// <summary>Fødselsnummer. Ikke garantert til stede i runtime-tokenet; se /api/meg/claims.</summary>
    public const string FodselsnummerClaim = "urn:altinn:ssn";

    public async Task<Bruker?> FinnAsync(HttpContext kontekst, CancellationToken ct = default)
    {
        var bruker = kontekst.User;
        if (bruker.Identity?.IsAuthenticated is not true) return null;

        var altinnBrukerId = bruker.FindFirstValue(BrukerIdClaim);
        if (string.IsNullOrWhiteSpace(altinnBrukerId)) return null;

        var eksisterende = await db.Brukere.FirstOrDefaultAsync(b => b.AltinnBrukerId == altinnBrukerId, ct);
        if (eksisterende is not null) return eksisterende;

        var rolle = await BestemRolleAsync(bruker, ct);
        var virksomhet = await FinnEllerOpprettVirksomhetAsync(ct);

        var ny = new Bruker
        {
            Id = Guid.NewGuid(),
            Navn = bruker.FindFirstValue(BrukernavnClaim)
                   ?? bruker.FindFirstValue(ClaimTypes.Name)
                   ?? $"Altinn-bruker {altinnBrukerId}",
            VirksomhetId = virksomhet.Id,
            Rolle = rolle,
            AltinnBrukerId = altinnBrukerId,
        };

        db.Brukere.Add(ny);
        await db.SaveChangesAsync(ct);
        return ny;
    }

    /// <summary>
    /// 401, ikke 400: her er det ingen header å mangle — brukeren er rett og slett ikke innlogget,
    /// eller cookien ble ikke godtatt. Statuskoden er også det klienten kan reagere på for å
    /// starte innloggingen på nytt når sesjonen løper ut i en åpen fane.
    /// </summary>
    public IResult IkkeFunnetSvar() =>
        Results.Json(
            new { feil = "Ikke innlogget. Last siden på nytt for å logge inn via Altinn." },
            statusCode: StatusCodes.Status401Unauthorized);

    /// <summary>
    /// DAGL gir Jurist, alt annet gir Saksbehandler. Saksbehandler er den minst privilegerte
    /// rollen i RBAC-matrisen (docs/03-domenemodell.md §2), så et rolleoppslag som ikke er
    /// konfigurert — eller som feiler — gir minst tilgang, ikke mest.
    /// </summary>
    private async Task<string> BestemRolleAsync(ClaimsPrincipal bruker, CancellationToken ct) =>
        await rolleoppslag.ErDagligLederAsync(bruker, ct) ? "Jurist" : "Saksbehandler";

    private async Task<Virksomhet> FinnEllerOpprettVirksomhetAsync(CancellationToken ct)
    {
        var navn = innstillinger.Virksomhet;
        var funnet = await db.Virksomheter.FirstOrDefaultAsync(v => v.Navn == navn, ct);
        if (funnet is not null)
        {
            // Seedet virksomhet har ikke organisasjonsnummer. Fyll det inn første gang vi vet
            // hvilken ekte organisasjon den representerer, men overskriv aldri et satt nummer.
            funnet.Organisasjonsnummer ??= innstillinger.Organisasjonsnummer;
            return funnet;
        }

        var ny = new Virksomhet
        {
            Id = Guid.NewGuid(),
            Navn = navn,
            Organisasjonsnummer = innstillinger.Organisasjonsnummer,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Virksomheter.Add(ny);
        return ny;
    }
}
