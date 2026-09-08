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

/// <summary>[Utvidet, virksomhetskatalog-runden, docs/20 §2.1] De nye feltene har defaultverdier slik
/// at eksisterende konstruksjonssteder ikke må endres — kun HentVirksomhetskatalog-endepunktet fyller
/// dem inn.</summary>
/// <param name="Visningsnavn">
/// [Ny, registernavn-runden, 2026-09-08] Navnet UI-et skal VISE: virksomhetens navneform med grunn
/// <c>'gjeldende'</c> når den finnes, ellers <paramref name="Navn"/>. Se
/// <see cref="VirksomhetVisningsnavnTjeneste"/> for hvorfor de to er forskjellige — kort: fra denne
/// runden er <paramref name="Navn"/> registerets egen form (VERSALER fra Brreg, og for tospråklige
/// kommuner en konkatenering uten skilletegn), mens visningsnavnet er den lesbare formen hentet fra
/// Kartverkets SSR / SNL / en godkjent liste.
/// <para>
/// Aldri <c>null</c>: faller tilbake på <paramref name="Navn"/>. Klienter som ikke kjenner feltet
/// fortsetter derfor å virke uendret ved å lese <paramref name="Navn"/> — men BØR bytte, ellers viser
/// de «GAIVUONA SUOHKAN KÅFJORD KOMMUNE KAIVUONON KOMUUNI» der brukeren forventer «Kåfjord kommune».
/// </para>
/// <para>
/// Defaultverdien <c>null</c> i konstruktøren er en KONSTRUKSJONS-bekvemmelighet for de mange
/// kallstedene som lager en DTO uten å ha navneformene for hånden (<see cref="FraEntitet"/>); den
/// serialiserte verdien er da <paramref name="Navn"/>, satt av fabrikkmetodene under.
/// </para>
/// </param>
public sealed record VirksomhetDto(
    Guid Id, string Navn, string? Organisasjonsnummer, bool Aktiv,
    string? Forvaltningsniva = null, string? OrganisasjonsformKode = null, string? Sektorkode = null,
    Guid? OverordnetEnhetId = null, DateOnly? SistBrregSynkronisert = null, string? Visningsnavn = null)
{
    /// <summary>Uten navneformer for hånden — <c>visningsnavn</c> settes da lik <c>navn</c>, aldri
    /// null. Brukes av endepunkt som returnerer ÉN nyopprettet/nyendret rad, der en ekstra spørring
    /// for å hente navneformen ikke er verdt det.</summary>
    public static VirksomhetDto FraEntitet(Virksomhet v) => new(
        v.Id, v.Navn, v.Organisasjonsnummer, v.Aktiv, v.Forvaltningsniva, v.OrganisasjonsformKode,
        v.Sektorkode, v.OverordnetEnhetId, v.SistBrregSynkronisert, v.Navn);

    /// <summary>Med visningsnavn slått opp — brukes av katalogendepunktet, som henter alle
    /// navneformene i ett spørsmål (<see cref="VirksomhetVisningsnavnTjeneste.AlleAsync"/>).
    /// Faller tilbake på <see cref="Virksomhet.Navn"/> for rader uten
    /// <c>'gjeldende'</c>-navneform.</summary>
    public static VirksomhetDto FraEntitet(Virksomhet v, IReadOnlyDictionary<Guid, string> visningsnavn) => new(
        v.Id, v.Navn, v.Organisasjonsnummer, v.Aktiv, v.Forvaltningsniva, v.OrganisasjonsformKode,
        v.Sektorkode, v.OverordnetEnhetId, v.SistBrregSynkronisert,
        visningsnavn.GetValueOrDefault(v.Id) ?? v.Navn);
}

/// <summary>Brukerhåndteringssiden — se BrukerregisterTjeneste.GyldigeRoller for gyldige verdier.</summary>
public sealed record OpprettBrukerRequest(string Navn, string Rolle, Guid VirksomhetId);

/// <summary>Navnet endres ikke via dette endepunktet, se BrukerregisterTjeneste.OppdaterAsync.</summary>
public sealed record OppdaterBrukerRequest(string Rolle, Guid VirksomhetId);
