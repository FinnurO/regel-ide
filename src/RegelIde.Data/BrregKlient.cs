using System.Text.Json;
using System.Text.Json.Serialization;

namespace RegelIde.Data;

/// <summary>
/// [Ny, 2026-08-29, docs/13-backlog.md §9] Klient mot Brønnøysundregistrenes offentlige,
/// autentiseringsfrie Enhetsregister-API (<c>data.brreg.no/enhetsregisteret/api</c>) — portert fra
/// Johanns eget `BrregService` i <c>github.com/FinnurO/kontaktlisteregisteret</c> (samme forfatter,
/// samme API), forenklet til det DENNE appen faktisk trenger: søk + ett enkeltoppslag for å opprette
/// eller berike en <see cref="Virksomhet"/>-rad. Ikke portert: massevalidering av orgnr-lister,
/// dynamiske kriterier, hierarki-visning (over- og underenheter) — ingen konkret behov for det ennå
/// her, se referanserepoets `BrregService.cs` hvis/når det trengs.
/// </summary>
public sealed class BrregKlient(HttpClient http)
{
    private const string BaseUrl = "https://data.brreg.no/enhetsregisteret/api";

    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Fritekstsøk på navn ELLER organisasjonsnummer — 9 sammenhengende sifre tolkes automatisk som et
    /// orgnr-søk i stedet for navnesøk (samme heuristikk som referanseimplementasjonen).
    /// </summary>
    public async Task<IReadOnlyList<BrregEnhet>> SokAsync(string tekst, int antall = 20, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/enheter?size={antall}&{ByggSokeparameter(tekst)}";
        var (resultat, _) = await HentAsync<BrregSokeResultat>(url, ct);
        return resultat?.Embedded?.Enheter ?? [];
    }

    /// <summary>
    /// Ett konkret oppslag på organisasjonsnummer — prøver hovedenhet først, deretter underenhet
    /// (driftsenhet/avdeling) som fallback, siden Brreg har to separate endepunkt for de to formene.
    /// </summary>
    public async Task<BrregEnhet?> HentPaOrgnrAsync(string orgnr, CancellationToken ct = default)
    {
        var rent = orgnr.Replace(" ", "");
        var (enhet, _) = await HentAsync<BrregEnhet>($"{BaseUrl}/enheter/{rent}", ct);
        if (enhet is not null) return enhet;
        var (underenhet, _) = await HentAsync<BrregEnhet>($"{BaseUrl}/underenheter/{rent}", ct);
        return underenhet;
    }

    private static string ByggSokeparameter(string tekst)
    {
        var rent = tekst.Trim().Replace(" ", "");
        return rent.Length == 9 && rent.All(char.IsDigit)
            ? $"organisasjonsnummer={rent}"
            : $"navn={Uri.EscapeDataString(tekst.Trim())}";
    }

    private async Task<(T? Verdi, string? Feil)> HentAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var svar = await http.GetAsync(url, ct);
            var innhold = await svar.Content.ReadAsStringAsync(ct);
            if (!svar.IsSuccessStatusCode)
            {
                return (default, $"HTTP {(int)svar.StatusCode} fra Brreg: {innhold[..Math.Min(300, innhold.Length)]}");
            }
            return (JsonSerializer.Deserialize<T>(innhold, JsonInnstillinger), null);
        }
        catch (TaskCanceledException)
        {
            return (default, "Tidsavbrudd ved kall til Brreg.");
        }
        catch (Exception ex)
        {
            return (default, $"Uventet feil mot Brreg: {ex.Message}");
        }
    }
}

public sealed class BrregSokeResultat
{
    [JsonPropertyName("_embedded")]
    public BrregEmbedded? Embedded { get; set; }
}

public sealed class BrregEmbedded
{
    public List<BrregEnhet> Enheter { get; set; } = [];
}

/// <summary>Kun feltene denne appen faktisk bruker — Brreg sitt fulle svar har vesentlig flere
/// (næringskoder, ansattall, stiftelsesdato osv.), bevisst utelatt til det trengs.</summary>
public sealed class BrregEnhet
{
    public string Organisasjonsnummer { get; set; } = "";
    public string Navn { get; set; } = "";
    public BrregKode? Organisasjonsform { get; set; }
    public BrregKode? InstitusjonellSektorkode { get; set; }

    /// <summary>Orgnr for overordnet enhet — Brreg returnerer kun en streng, ikke et objekt.</summary>
    public string? OverordnetEnhet { get; set; }

    public string? Hjemmeside { get; set; }
    public string? Slettedato { get; set; }
    public BrregAdresse? Forretningsadresse { get; set; }

    public bool ErAktiv => Slettedato is null;
}

public sealed class BrregKode
{
    public string Kode { get; set; } = "";
    public string Beskrivelse { get; set; } = "";
}

public sealed class BrregAdresse
{
    public List<string>? Adresse { get; set; }
    public string? Poststed { get; set; }
}
