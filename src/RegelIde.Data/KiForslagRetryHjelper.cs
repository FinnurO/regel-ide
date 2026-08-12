using Microsoft.Extensions.Logging;

namespace RegelIde.Data;

/// <summary>
/// R0 (byggesteg 5 runde 5, docs/13-backlog.md §4 punkt 7 / docs/14-byggesteg5-teknisk-design.md
/// §8.4) — delt "kall + parse, ETT retry ved et tomt forslag-array"-logikk for
/// <see cref="TjenesteforslagTjeneste"/> og <see cref="BegrepsforslagTjeneste"/>, som ellers ville
/// duplisert identisk retry-håndtering. Hører HER, ETTER parsing — ikke i
/// <see cref="KiAgentKlientOpenAiKompatibel"/> selv, som ikke vet, og ikke skal vite, hva et "tomt
/// forslag" betyr for en bestemt agent (den returnerer bare rå tekst i en <see cref="KiSvar"/>). Se
/// docs/14 §8.4: et tomt <c>[]</c>-svar på identisk kontekst er observert å være modellens EGEN
/// sampling-variasjon, ikke en kodefeil — ett automatisk retry med SAMME kontekst (ikke en endret
/// prompt) er derfor en rimelig, billig motstrategi. Hvis retry-kallet OGSÅ er tomt, er det fortsatt
/// en gyldig (om uheldig) respons — IKKE en feil, ingen uendelig løkke, kun ett ekstra forsøk.
/// </summary>
internal static class KiForslagRetryHjelper
{
    /// <summary>
    /// Kaller <paramref name="kallKi"/>, parser svaret med <paramref name="parseForslag"/>, og gjør
    /// ETT nytt kall (med samme kontekst — <paramref name="kallKi"/> er en closure over konteksten,
    /// ikke noe denne hjelperen selv velger) hvis resultatet er null/tomt. Logger den rå responsteksten
    /// begge ganger et tomt svar oppstår, slik at et mønster av "tomrespons" kan observeres i
    /// produksjonslogger (R0-målet i docs/13-backlog.md: tomrespons/timeout under 5 %). En
    /// <see cref="System.Text.Json.JsonException"/> fra <paramref name="parseForslag"/> forplantes
    /// UENDRET til kalleren — ugyldig JSON er en annen feilmodus enn et gyldig, men tomt, svar, og
    /// skal ikke utløse et retry her (kalleren pakker den evt. om til en tydeligere feilmelding).
    /// </summary>
    public static async Task<(KiSvar Svar, List<T>? Forslag)> KjorMedEttRetryVedTomtSvarAsync<T>(
        Func<CancellationToken, Task<KiSvar>> kallKi,
        Func<string, List<T>?> parseForslag,
        ILogger logger,
        string agentNavn,
        CancellationToken ct)
    {
        var svar = await kallKi(ct);
        var forslag = parseForslag(svar.Innhold);
        if (forslag is { Count: > 0 })
        {
            return (svar, forslag);
        }

        logger.LogWarning(
            "{Agent}: KI-agenten returnerte et tomt forslag-array på første forsøk. Kjent " +
            "modell-samplingvariasjon på identisk kontekst er observert, ikke nødvendigvis en feil " +
            "(se docs/14-byggesteg5-teknisk-design.md §8.4) — gjør ett automatisk retry med samme " +
            "kontekst. Rå respons: {RaaRespons}", agentNavn, svar.Innhold);

        svar = await kallKi(ct);
        forslag = parseForslag(svar.Innhold);
        if (forslag is null || forslag.Count == 0)
        {
            logger.LogWarning(
                "{Agent}: KI-agenten returnerte et tomt forslag-array også etter retry — gir opp, " +
                "returnerer et tomt (men gyldig) resultat, kaster ingen feil. Rå respons: {RaaRespons}",
                agentNavn, svar.Innhold);
        }
        return (svar, forslag);
    }
}
