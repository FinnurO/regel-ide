namespace RegelIde.Api.Autentisering;

/// <summary>
/// Konfigurasjonen <see cref="Autentiseringsprofil.Altinn"/> trenger, lest under <c>RegelIde:Altinn</c>.
/// <see cref="Plattform"/> må oppgis; resten har standardverdier som er like i alle miljøer.
/// </summary>
public sealed class Altinninnstillinger
{
    public const string Seksjon = "RegelIde:Altinn";

    /// <summary>
    /// Plattformens basis-URL — <c>https://platform.at23.altinn.cloud</c>, <c>https://platform.tt02.altinn.no</c>,
    /// <c>https://platform.altinn.no</c>.
    /// <para>
    /// Har bevisst ingen standardverdi. Hvert miljø signerer runtime-cookien med sin egen nøkkel, så
    /// en standard ville betydd at deploy til et annet miljø ga en app som starter fint og avviser
    /// alle gyldige innlogginger — uten noe spor av hvorfor. Nå stopper den i stedet ved oppstart.
    /// </para>
    /// </summary>
    public required string Plattform { get; init; }

    /// <summary>Navnet på runtime-cookien. Samme verdi som skall-appens <c>RuntimeCookieName</c>.</summary>
    public required string Cookienavn { get; init; }

    /// <summary>
    /// Virksomheten innloggede Altinn-brukere havner i. I PoC-en representerer én organisasjon
    /// én kommune (se docs/autentisering.md) — vi slår ikke opp organisasjonsnummer via
    /// register-API-et, fordi det ville krevd abonnementsnøkkel for å gi oss noe vi ikke bruker.
    /// </summary>
    public required string Virksomhet { get; init; }

    /// <summary>Organisasjonsnummeret virksomheten opprettes med. Kun dokumentasjon — vi slår ikke opp noe på det.</summary>
    public string? Organisasjonsnummer { get; init; }

    /// <summary>
    /// Identifikatorer som skal regnes som daglig leder — normalt <c>urn:altinn:userid</c>.
    /// Midlertidig kilde til DAGL i PoC-en, se <see cref="KonfigurertRolleoppslag"/>.
    /// </summary>
    public required IReadOnlyCollection<string> DaglIdentifikatorer { get; init; }

    /// <summary>
    /// Eksponerer <c>/api/meg/claims</c> så første innlogging i tt02 viser hvilke claims
    /// runtime-tokenet faktisk inneholder. Av som standard; slås på ved behov.
    /// </summary>
    public bool VisClaims { get; init; }

    public string VelkjentEndepunkt =>
        $"{Plattform.TrimEnd('/')}/authentication/api/v1/openid/.well-known/openid-configuration";

    public static Altinninnstillinger Les(IConfiguration konfigurasjon)
    {
        var seksjon = konfigurasjon.GetSection(Seksjon);
        return new Altinninnstillinger
        {
            Plattform = seksjon["Plattform"]?.Trim() is { Length: > 0 } plattform
                ? plattform
                : throw new InvalidOperationException(
                    $"{Seksjon}:Plattform må settes når {Autentiseringsoppsett.Konfigurasjonsnokkel}=altinn. "
                    + "Verdien er miljøspesifikk, f.eks. https://platform.at23.altinn.cloud, "
                    + "https://platform.tt02.altinn.no eller https://platform.altinn.no."),
            Cookienavn = seksjon["Cookienavn"] ?? "AltinnStudioRuntime",
            Virksomhet = seksjon["Virksomhet"] ?? "Testkommunen",
            Organisasjonsnummer = seksjon["Organisasjonsnummer"],
            DaglIdentifikatorer = seksjon.GetSection("DaglIdentifikatorer").Get<string[]>() ?? [],
            VisClaims = seksjon.GetValue("VisClaims", false),
        };
    }
}
