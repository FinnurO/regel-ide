using System.Security.Claims;

namespace RegelIde.Api.Autentisering;

/// <summary>
/// Avgjør om en innlogget Altinn-bruker er daglig leder. Egen abstraksjon fordi kilden til svaret
/// er det eneste som skiller PoC fra ferdig løsning: PoC-en leser en liste fra konfigurasjon,
/// mens en ekte løsning spør Altinns autorisasjons-API.
/// </summary>
public interface IAltinnRolleoppslag
{
    /// <summary>
    /// Tar hele prinsipalen, ikke en enkelt id, fordi hvilke identifikatorer runtime-tokenet
    /// faktisk inneholder er noe vi må kunne forholde oss til uten å endre grensesnittet.
    /// </summary>
    Task<bool> ErDagligLederAsync(ClaimsPrincipal bruker, CancellationToken ct = default);
}

/// <summary>
/// PoC-kilden: en liste med identifikatorer som skal regnes som DAGL, satt i
/// <c>RegelIde:Altinn:DaglIdentifikatorer</c>.
/// <para>
/// Dette er bevisst *ikke* et kall til Altinns rolle-API. Det API-et krever en
/// abonnementsnøkkel (<c>Ocp-Apim-Subscription-Key</c>) og et plattform-token som Altinn-apper
/// får montert inn som secret — se <c>accesstoken</c>-volumet i skall-appens values.yaml. Vi er
/// ikke en Altinn-app og har ikke de secretene. Så lenge PoC-en bare skal demonstrere at rollen
/// styrer tilgangen, gir konfigurasjonslista samme observerbare oppførsel til en brøkdel av
/// koblingen — og byttes ut ved å implementere <see cref="IAltinnRolleoppslag"/> på nytt.
/// </para>
/// </summary>
public sealed class KonfigurertRolleoppslag(IReadOnlyCollection<string> daglIdentifikatorer) : IAltinnRolleoppslag
{
    /// <summary>
    /// Claims som identifiserer *personen*. Tenor oppgir party-id-er, mens runtime-tokenet
    /// primært identifiserer med <c>urn:altinn:userid</c> — og de to er ikke samme nummer.
    /// Vi sammenligner derfor mot alle identifikatorene tokenet faktisk inneholder, slik at
    /// konfigurasjonen virker uansett hvilket av numrene som ble lagt inn.
    /// <para>
    /// <c>urn:altinn:partyid</c> er med vilje utelatt: den peker på avgiveren som er valgt, ikke
    /// på den innloggede personen. Representerer brukeren organisasjonen, står organisasjonens
    /// party der — og da ville alle som representerer den samme organisasjonen fått DAGL.
    /// </para>
    /// </summary>
    private static readonly string[] Identitetsclaims =
    [
        AltinnBrukerkontekst.BrukerIdClaim,
        AltinnBrukerkontekst.FodselsnummerClaim,
    ];

    public Task<bool> ErDagligLederAsync(ClaimsPrincipal bruker, CancellationToken ct = default)
    {
        if (daglIdentifikatorer.Count == 0) return Task.FromResult(false);

        var treff = Identitetsclaims
            .SelectMany(bruker.FindAll)
            .Any(c => daglIdentifikatorer.Contains(c.Value));

        return Task.FromResult(treff);
    }
}
