using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RegelIde.Data;

/// <summary>
/// KI-forslagsformen til en Handling — brukt BÅDE av <see cref="HandlingsforslagTjeneste"/> (omfang
/// "handling", handlinger for en EKSISTERENDE tjeneste) og <see cref="TjenesteforslagTjeneste"/>
/// (omfang "full", Tjeneste+Handlinger i samme svar) — definert ETT sted, ikke duplisert i begge
/// klassene (handlingsforslag-ki-omfang-runden). Feltnavnene er identiske med de allerede eksisterende
/// verdiobjektene i <see cref="HandlingregisterTjeneste"/> (<see cref="HandlingKanalInput"/> osv.) —
/// KI-en fyller ut PRESIST samme skjema som <see cref="HandlingregisterTjeneste.OpprettAsync"/> selv
/// tar imot, ingen egen oversettelseslag trengs.
/// </summary>
internal sealed record HandlingForslagJson(
    string Navn, string Handlingstype, string? Bruksomraade, string? UtfortAv,
    IReadOnlyList<HandlingKanalInput>? Kanaler, HandlingBehandlingstidInput? Behandlingstid,
    HandlingKostnadInput? Kostnad, IReadOnlyList<HandlingVedleggInput>? Vedlegg,
    IReadOnlyList<HandlingVeiledningstekstInput>? Veiledningstekst, IReadOnlyList<HandlingArsakInput>? Arsaker,
    HandlingResultatInput? Resultat, string? Merknad);

/// <summary>
/// Delt "her er nøyaktig skjemaet"-tekst for en Handling-forslag-JSON — samme stil som
/// <see cref="TjenesteforslagTjeneste"/>s egen system-instruks, trukket ut hit slik at
/// <see cref="HandlingsforslagTjeneste"/> og <see cref="TjenesteforslagTjeneste"/> (omfang "full")
/// beskriver EKSAKT samme Handling-skjema for agenten, ordrett, i stedet for to drivende kopier.
/// </summary>
internal static class HandlingForslagSkjemaHjelper
{
    public static string SkjemaBeskrivelse =>
        """
        - "Navn": handlingens navn (streng, obligatorisk, f.eks. "Søke om skjenkebevilling")
        - "Handlingstype": obligatorisk, én av: HANDLINGSTYPEENUM
        - "Bruksomraade": grov kategori hvis konteksten gir tydelig belegg, ellers null
        - "UtfortAv": én av: UTFORTAVENUM — hvem som utfører handlingen, eller null hvis usikker
        - "Kanaler": liste av {"Kanal": streng, "Adresse": streng eller null} — hvordan handlingen utføres
        - "Behandlingstid": {"Frist": streng eller null, "Hjemmel": HJEMMELFORM eller null} eller null
        - "Kostnad": {"Belop": streng eller null, "Hjemmel": liste av HJEMMELFORM} eller null
        - "Vedlegg": liste av {"Navn": streng, "Kategori": streng eller null, "Hjemmel": HJEMMELFORM eller null}
        - "Veiledningstekst": liste av {"Overskrift": streng, "Innhold": streng eller null, "Hjemmel": HJEMMELFORM eller null}
        - "Arsaker": liste av {"Arsak": streng, "Hjemmel": HJEMMELFORM} — kun relevant for handlinger som
          gjelder bortfall/opphør av en rettighet, tom liste ellers
        - "Resultat": {"Hva": streng eller null, "BevisKanaler": liste av {"Kanal": streng}} eller null
        - "Merknad": fritekst eller null

        HJEMMELFORM er {"Lov": streng (kortnavn, f.eks. "serveringsloven"), "Henvisning": streng eller null
        (f.eks. "§ 5")} — ALDRI en [eId]-tag, kun et lesbart kortnavn+henvisning et menneske kjenner igjen.
        """
            .Replace("HANDLINGSTYPEENUM", string.Join(", ", HandlingregisterTjeneste.GyldigeHandlingstyper))
            .Replace("UTFORTAVENUM", string.Join(", ", HandlingregisterTjeneste.GyldigeUtfortAv));
}

/// <summary>
/// «Foreslå handlinger» (handlingsforslag-ki-omfang-runden) — speiler <see cref="TjenesteforslagTjeneste"/>s
/// struktur, men for omfang "handling": foreslår Handling-rader under en ALLEREDE EKSISTERENDE
/// <see cref="TjenesteEntitet"/> i stedet for å identifisere nye tjenester. Konkret use case:
/// Oppgaveregisterets grove "Oppgaveregisteret — X"-samletjenester (se
/// <see cref="OppgaveregisterHandlingSeed"/>) — denne agenten deler dem opp/beriker handlingene under
/// en slik samletjeneste basert på rettskilde-teksten. Oppretter forslag via
/// <see cref="HandlingregisterTjeneste.OpprettForslagFraKiAsync"/>.
/// </summary>
public sealed class HandlingsforslagTjeneste(
    RegelIdeDbContext db, IKiAgentKlient kiKlient, HandlingregisterTjeneste handlingregister, IConfiguration config,
    ILogger<HandlingsforslagTjeneste>? logger = null)
{
    private readonly ILogger<HandlingsforslagTjeneste> _logger = logger ?? NullLogger<HandlingsforslagTjeneste>.Instance;

    // Samme begrunnelse som TjenesteforslagTjeneste/BegrepsforslagTjeneste.
    private string AiForslagVersjon =>
        config["RegelIde:KiAgent:Leverandor"] == "OpenAiKompatibel"
            ? $"OpenAiKompatibel:{config["RegelIde:KiAgent:Modell"]}"
            : "stub-v1";

    private static string SystemInstruks =>
        $$"""
        Du er en assistent som identifiserer og beriker konkrete Handlinger (søke, endre, si opp,
        melde, registrere, rapportere, klage, kontrolleres, ...) under ÉN EKSISTERENDE tjeneste, fra
        lovtekst.

        Konteksten under starter med tjenesten disse handlingene skal foreslås FOR (tittel og
        beskrivelse), etterfulgt av lovtekst der hver paragraf/ledd er merket med en [eId]-tag foran
        teksten.

        Svar KUN med en ren JSON-array, ingen markdown-kodeblokk (```), ingen forklaringstekst før
        eller etter. Hvert element beskriver ÉN handling med disse feltene — kun "Navn" og
        "Handlingstype" er obligatoriske, resten skal være null/tom liste hvis konteksten ikke gir
        tydelig belegg (dikt ikke opp):
        {{HandlingForslagSkjemaHjelper.SkjemaBeskrivelse}}

        Returner en tom array [] hvis du ikke finner noen tydelige handlinger for denne tjenesten.
        """;

    public async Task<KiForslagResultat<HandlingEntitet>> KjorForslagAsync(
        Guid virksomhetId, Guid tjenesteId, IReadOnlyList<Guid> rettskildeIder, string opprettetAv, CancellationToken ct = default)
    {
        // Ingen gjettet fallback — en KI-agent kan ikke finne opp hvilken tjeneste handlingene hører
        // til, og et forsøk på å foreslå handlinger for en tjeneste som ikke finnes (eller tilhører en
        // annen virksomhet) er en brukerfeil å rapportere, ikke noe å late som fungerer.
        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(
            t => t.Id == tjenesteId && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null)
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{tjenesteId}' for denne virksomheten. Ingen gjettet fallback.");
        }

        var rettskildeKontekst = await RettskildeKontekstHjelper.ByggKontekstAsync(db, rettskildeIder, ct);
        var kontekstTekst =
            $"""
            # Tjeneste handlingene skal foreslås for
            {tjeneste.Tittel}
            {tjeneste.Beskrivelse}

            """ + rettskildeKontekst;

        KiSvar svar;
        List<HandlingForslagJson>? forslag;
        try
        {
            (svar, forslag) = await KiForslagRetryHjelper.KjorMedEttRetryVedTomtSvarAsync<HandlingForslagJson>(
                kallCt => kiKlient.GenererAsync(SystemInstruks, kontekstTekst, kallCt),
                json => JsonSerializer.Deserialize<List<HandlingForslagJson>>(
                    JsonSvarHjelper.StrimleKodeblokk(json), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
                _logger, "Foreslå handlinger", ct);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"KI-klienten returnerte ugyldig JSON for handlingsforslag: {ex.Message}", ex);
        }
        if (forslag is null || forslag.Count == 0)
        {
            return new KiForslagResultat<HandlingEntitet>(
                [], svar.InputTokens, svar.OutputTokens, "KI-agenten svarte, men fant ingen handlinger å foreslå i valgt kontekst.");
        }

        var kildeReferanserJson = JsonSerializer.Serialize(new { rettskildeIder, tjenesteId });
        var opprettede = new List<HandlingEntitet>();
        foreach (var f in forslag)
        {
            var handling = await handlingregister.OpprettForslagFraKiAsync(
                virksomhetId, tjenesteId, f.Navn, f.Handlingstype, f.Bruksomraade, f.UtfortAv, f.Kanaler,
                f.Behandlingstid, f.Kostnad, f.Vedlegg, f.Veiledningstekst, f.Arsaker, f.Resultat, f.Merknad,
                opprettetAv, AiForslagVersjon, kildeReferanserJson, ct);
            opprettede.Add(handling);
        }
        return new KiForslagResultat<HandlingEntitet>(opprettede, svar.InputTokens, svar.OutputTokens, null);
    }
}
