using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data;

/// <summary>
/// «Identifiser tjenester» (byggesteg 5 runde 1, docs/06-veikart.md) — foreslår nye Tjeneste-objekter
/// fra valgte rettskilder pluss virksomhetens registrerte kunnskapsbibliotek-lenker (nettside o.l.).
/// Bevisst IKKE avhengig av at noe Tjeneste-objekt finnes fra før — det er nettopp det denne agenten
/// finner ut. Oppretter forslag via <see cref="TjenesteregisterTjeneste.OpprettForslagFraKiAsync"/>.
/// </summary>
public sealed class TjenesteforslagTjeneste(RegelIdeDbContext db, IKiAgentKlient kiKlient, TjenesteregisterTjeneste tjenesteregister, IConfiguration config)
{
    // Byggesteg 5 runde 3: se samme begrunnelse i BegrepsforslagTjeneste.
    private string AiForslagVersjon =>
        config["RegelIde:KiAgent:Leverandor"] == "OpenAiKompatibel"
            ? $"OpenAiKompatibel:{config["RegelIde:KiAgent:Modell"]}"
            : "stub-v1";

    // Byggesteg 5 runde 3: utvidet fra kun Tittel/KortBeskrivelse til de øvrige CPSV-AP-NO-feltene
    // som ALLEREDE finnes på TjenesteEntitet (ingen nye entiteter/migrasjon). Fire CPSV-AP-NO-
    // konsepter er bevisst IKKE dekket her fordi de ikke finnes i skjemaet i det hele tatt ennå —
    // se docs/14-byggesteg5-teknisk-design.md: cpsv:hasParticipation (organisasjon+rolle som egen
    // struktur), cpsv:hasInput (dokumentasjonskrav), dct:spatial (geografisk område), og en eksplisitt
    // dct:requires/dct:hasPart-utvidelse av Tjenesteavhengighet.Rel.
    private const string SystemInstruks =
        """
        Du er en assistent som identifiserer offentlige tjenester (CPSV-AP-NO) fra lovtekst,
        virksomhetens nettside-lenker og opplastede dokumenter.

        Konteksten under inneholder lovtekst (hver paragraf/ledd merket med en [eId]-tag),
        kunnskapsbibliotek-lenker, og ev. opplastede dokumenter.

        Svar KUN med en ren JSON-array, ingen markdown-kodeblokk (```), ingen forklaringstekst før
        eller etter. Hvert element beskriver ÉN tjeneste med disse feltene — kun "Tittel" er
        obligatorisk, resten skal være null hvis konteksten ikke gir tydelig belegg (dikt ikke opp):
        - "Tittel": tjenestens navn (streng, obligatorisk)
        - "KortBeskrivelse": en fyldig beskrivelse av hva tjenesten er og hvem den er for
        - "KompetentMyndighet": hvilken myndighet/virksomhet som er ansvarlig for tjenesten
        - "Output": resultatet av tjenesten (f.eks. et vedtak, en bevilling, et kort)
        - "Tjenestetype": en kort klassifisering av tjenestetypen
        - "Malgruppe": hvem tjenesten retter seg mot
        - "Kanaler": liste av strenger for hvordan tjenesten leveres (f.eks. "digitalt", "fysisk",
          "telefon")
        - "Kostnad": om tjenesten har en kostnad, og evt. hvor mye
        - "Behandlingstid": forventet saksbehandlingstid
        - "Kontaktpunkt": hvor brukere kan henvende seg
        - "KonsekvensVedBrudd": konsekvenser dersom vilkårene for tjenesten ikke er oppfylt
        - "Sprak": liste av strenger for hvilke språk tjenesten er tilgjengelig på

        Returner en tom array [] hvis du ikke finner noen tydelige tjenester.
        """;

    private sealed record TjenesteForslagJson(
        string Tittel, string? KortBeskrivelse, string? KompetentMyndighet, string? Output,
        string? Tjenestetype, string? Malgruppe, IReadOnlyList<string>? Kanaler, string? Kostnad,
        string? Behandlingstid, string? Kontaktpunkt, string? KonsekvensVedBrudd, IReadOnlyList<string>? Sprak);

    public async Task<KiForslagResultat<TjenesteEntitet>> KjorForslagAsync(
        Guid virksomhetId, IReadOnlyList<Guid> rettskildeIder, string opprettetAv, CancellationToken ct = default)
    {
        var rettskildeKontekst = await RettskildeKontekstHjelper.ByggKontekstAsync(db, rettskildeIder, ct);
        var lenker = await db.KunnskapsbibliotekLenker
            .Where(l => l.VirksomhetId == virksomhetId)
            .ToListAsync(ct);
        var filer = await db.KunnskapsbibliotekFiler
            .Where(f => f.VirksomhetId == virksomhetId)
            .Select(f => new { f.Id, f.Filnavn, f.Tittel, f.UtvunnetTekst })
            .ToListAsync(ct);

        var sb = new StringBuilder(rettskildeKontekst);
        if (lenker.Count > 0)
        {
            sb.AppendLine("# Kunnskapsbibliotek-lenker");
            foreach (var lenke in lenker)
            {
                sb.AppendLine(lenke.Beskrivelse is null ? lenke.Url : $"{lenke.Url} — {lenke.Beskrivelse}");
            }
        }
        if (filer.Count > 0)
        {
            sb.AppendLine("# Kunnskapsbibliotek-filer");
            foreach (var fil in filer)
            {
                sb.AppendLine($"## {fil.Tittel ?? fil.Filnavn}");
                sb.AppendLine(fil.UtvunnetTekst);
            }
        }

        var svar = await kiKlient.GenererAsync(SystemInstruks, sb.ToString(), ct);

        List<TjenesteForslagJson>? forslag;
        try
        {
            forslag = JsonSerializer.Deserialize<List<TjenesteForslagJson>>(
                JsonSvarHjelper.StrimleKodeblokk(svar.Innhold), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"KI-klienten returnerte ugyldig JSON for tjenesteforslag: {ex.Message}", ex);
        }
        if (forslag is null || forslag.Count == 0)
        {
            // Se samme begrunnelse i BegrepsforslagTjeneste — skiller "kjørte, fant ingenting" fra
            // stillhet som ellers ikke kan skilles fra en feil i UI-et.
            return new KiForslagResultat<TjenesteEntitet>(
                [], svar.InputTokens, svar.OutputTokens, "KI-agenten svarte, men fant ingen tjenester å foreslå i valgt kontekst.");
        }

        var kildeReferanserJson = JsonSerializer.Serialize(new
        {
            rettskildeIder,
            lenkeIder = lenker.Select(l => l.Id),
            filIder = filer.Select(f => f.Id),
        });
        var opprettede = new List<TjenesteEntitet>();
        foreach (var f in forslag)
        {
            var tjeneste = await tjenesteregister.OpprettForslagFraKiAsync(
                virksomhetId, f.Tittel, f.KortBeskrivelse, f.KompetentMyndighet, f.Output, f.Tjenestetype,
                f.Malgruppe, f.Kanaler, f.Kostnad, f.Behandlingstid, f.Kontaktpunkt, f.KonsekvensVedBrudd,
                f.Sprak, opprettetAv, AiForslagVersjon, kildeReferanserJson, ct);
            opprettede.Add(tjeneste);
        }
        return new KiForslagResultat<TjenesteEntitet>(opprettede, svar.InputTokens, svar.OutputTokens, null);
    }
}
