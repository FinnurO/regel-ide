using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RegelIde.Data;

/// <summary>
/// «Identifiser tjenester» (byggesteg 5 runde 1, docs/06-veikart.md) — foreslår nye Tjeneste-objekter
/// fra valgte rettskilder pluss virksomhetens registrerte kunnskapsbibliotek-lenker (nettside o.l.).
/// Bevisst IKKE avhengig av at noe Tjeneste-objekt finnes fra før — det er nettopp det denne agenten
/// finner ut. Oppretter forslag via <see cref="TjenesteregisterTjeneste.OpprettForslagFraKiAsync"/>.
/// </summary>
public sealed class TjenesteforslagTjeneste(
    RegelIdeDbContext db, IKiAgentKlient kiKlient, TjenesteregisterTjeneste tjenesteregister,
    TjenesteavhengighetregisterTjeneste tjenesteavhengighetregister, IConfiguration config,
    RettskildeEmbeddingTjeneste rettskildeEmbeddingTjeneste, IEmbeddingKlient embeddingKlient,
    ILogger<TjenesteforslagTjeneste>? logger = null)
{
    private readonly ILogger<TjenesteforslagTjeneste> _logger = logger ?? NullLogger<TjenesteforslagTjeneste>.Instance;


    // Byggesteg 5 runde 3: se samme begrunnelse i BegrepsforslagTjeneste.
    private string AiForslagVersjon =>
        config["RegelIde:KiAgent:Leverandor"] == "OpenAiKompatibel"
            ? $"OpenAiKompatibel:{config["RegelIde:KiAgent:Modell"]}"
            : "stub-v1";

    // Byggesteg 5 runde 3: utvidet fra kun Tittel/KortBeskrivelse til de øvrige CPSV-AP-NO-feltene
    // som ALLEREDE finnes på TjenesteEntitet (ingen nye entiteter/migrasjon). Tre CPSV-AP-NO-
    // konsepter er FORTSATT bevisst IKKE dekket her fordi de ikke finnes i skjemaet i det hele tatt
    // — se docs/14-byggesteg5-teknisk-design.md: cpsv:hasParticipation (organisasjon+rolle som egen
    // struktur), cpsv:hasInput (dokumentasjonskrav), dct:spatial (geografisk område). Byggesteg 5
    // runde 4 la til RegelverksreferanserEid/RelatertTil under — det fjerde konseptet
    // (dct:requires/dct:hasPart) er dermed lettet (ny "har_del"-Rel-verdi), ikke fullt løst — se
    // docs/13-backlog.md for hva som fortsatt er åpent.
    //
    // Ikke en `const string` lenger, siden Rel-listen under interpoleres inn fra
    // TjenesteavhengighetregisterTjeneste.GyldigeRel (ett sted, ikke en duplisert, driftbar kopi).
    private static string SystemInstruks =>
        """
        Du er en assistent som identifiserer offentlige tjenester (CPSV-AP-NO) fra lovtekst,
        virksomhetens nettside-lenker og opplastede dokumenter.

        Konteksten under inneholder lovtekst (hver paragraf/ledd merket med en [eId]-tag),
        kunnskapsbibliotek-lenker, opplastede dokumenter, og en liste over virksomhetens
        eksisterende tjenester (merket "E1", "E2", osv.).

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
        - "RegelverksreferanserEid": liste av eksakte [eId]-tagger (uten hakeparentesene) som er det
          konkrete rettslige grunnlaget for DENNE tjenesten — kun tagger som faktisk finnes i
          konteksten, aldri oppdiktet. Null eller tom liste hvis usikker.
        - "RelatertTil": liste av relasjoner til andre tjenester, hver med "Referanse" og "Rel".
          "Referanse" er ENTEN "E{n}" for en eksisterende tjeneste fra listen i konteksten, ELLER
          "T{n}" for tjeneste nummer n i DIN EGEN JSON-array (1-indeksert, i den rekkefølgen du
          selv returnerer dem) — bruk "T{n}" når to tjenester du foreslår i samme svar hører
          sammen. "Rel" må være en av: RELENUM. Null eller tom liste hvis ingen tydelig relasjon.

        Returner en tom array [] hvis du ikke finner noen tydelige tjenester.
        """.Replace("RELENUM", string.Join(", ", TjenesteavhengighetregisterTjeneste.GyldigeRel));

    private sealed record RelatertTjenesteJson(string Referanse, string Rel);

    private sealed record EksisterendeTjenesteRef(Guid Id, string Tittel);

    private sealed record TjenesteForslagJson(
        string Tittel, string? KortBeskrivelse, string? KompetentMyndighet, string? Output,
        string? Tjenestetype, string? Malgruppe, IReadOnlyList<string>? Kanaler, string? Kostnad,
        string? Behandlingstid, string? Kontaktpunkt, string? KonsekvensVedBrudd, IReadOnlyList<string>? Sprak,
        IReadOnlyList<string>? RegelverksreferanserEid, IReadOnlyList<RelatertTjenesteJson>? RelatertTil);

    public async Task<KiForslagResultat<TjenesteEntitet>> KjorForslagAsync(
        Guid virksomhetId, IReadOnlyList<Guid> rettskildeIder, string opprettetAv, CancellationToken ct = default)
    {
        var rettskildeKontekst = await RettskildeKontekstHjelper.ByggKontekstAsync(db, rettskildeIder, ct);
        return await KjorForslagFraKontekstAsync(virksomhetId, rettskildeIder, rettskildeKontekst, opprettetAv, ct);
    }

    /// <summary>
    /// RAG-spike-motstykke til <see cref="KjorForslagAsync"/> (byggesteg 5 runde 4, se
    /// docs/14-byggesteg5-teknisk-design.md "RAG-spike") — bruker <see cref="RagKontekstHjelper"/> i
    /// stedet for <see cref="RettskildeKontekstHjelper"/> for rettskilde-delen av konteksten;
    /// resten (kunnskapsbibliotek-dump, eksisterende tjenester, agent-kall, opprettelse,
    /// referanse-kobling) er UENDRET og delt med <see cref="KjorForslagAsync"/> via
    /// <see cref="KjorForslagFraKontekstAsync"/> — kun selve rettskilde-kontekst-byggingen skiller de
    /// to metodene. "Spørsmålet" som embeddes er kunnskapsbibliotekets sammenslåtte lenke-/fil-tekst
    /// — se docs/14 for retrieval-anker-begrunnelsen (samme anker Begrep-agenten IKKE har, derfor
    /// ikke del av spiken). Erstatter IKKE <see cref="KjorForslagAsync"/> — begge kjøres side ved
    /// side for rå sammenligning denne runden.
    /// </summary>
    public async Task<KiForslagResultat<TjenesteEntitet>> KjorForslagMedRagAsync(
        Guid virksomhetId, IReadOnlyList<Guid> rettskildeIder, int antallNoder, string opprettetAv, CancellationToken ct = default)
    {
        var sporsmalTekst = await ByggSporsmalTekstAsync(virksomhetId, ct);
        if (string.IsNullOrWhiteSpace(sporsmalTekst))
        {
            throw new ArgumentException(
                "RAG-sporet krever minst én kunnskapsbibliotek-lenke eller -fil som retrieval-anker (spørsmålet som embeddes) — se docs/14 §RAG-spike. Ingen gjettet fallback.");
        }
        var rettskildeKontekst = await RagKontekstHjelper.ByggKontekstAsync(
            db, rettskildeIder, rettskildeEmbeddingTjeneste, embeddingKlient, sporsmalTekst, antallNoder, ct);
        return await KjorForslagFraKontekstAsync(virksomhetId, rettskildeIder, rettskildeKontekst, opprettetAv, ct);
    }

    /// <summary>Sammenslått lenke-beskrivelse/fil-tittel-tekst brukt som "spørsmål" å embedde i
    /// <see cref="KjorForslagMedRagAsync"/> — se docs/14 for hvorfor kunnskapsbiblioteket er det
    /// naturlige retrieval-ankeret for denne agenten spesifikt.</summary>
    private async Task<string> ByggSporsmalTekstAsync(Guid virksomhetId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var lenker = await db.KunnskapsbibliotekLenker.Where(l => l.VirksomhetId == virksomhetId).ToListAsync(ct);
        foreach (var lenke in lenker)
        {
            sb.AppendLine(lenke.Beskrivelse is null ? lenke.Url : $"{lenke.Url} — {lenke.Beskrivelse}");
        }
        var filer = await db.KunnskapsbibliotekFiler.Where(f => f.VirksomhetId == virksomhetId)
            .Select(f => new { f.Filnavn, f.Tittel }).ToListAsync(ct);
        foreach (var fil in filer)
        {
            sb.AppendLine(fil.Tittel ?? fil.Filnavn);
        }
        return sb.ToString();
    }

    private async Task<KiForslagResultat<TjenesteEntitet>> KjorForslagFraKontekstAsync(
        Guid virksomhetId, IReadOnlyList<Guid> rettskildeIder, string rettskildeKontekst, string opprettetAv, CancellationToken ct)
    {
        var lenker = await db.KunnskapsbibliotekLenker
            .Where(l => l.VirksomhetId == virksomhetId)
            .ToListAsync(ct);
        var filer = await db.KunnskapsbibliotekFiler
            .Where(f => f.VirksomhetId == virksomhetId)
            .Select(f => new { f.Id, f.Filnavn, f.Tittel, f.UtvunnetTekst })
            .ToListAsync(ct);
        // Byggesteg 5 runde 4: eksisterende, gjeldende tjenester listes E1/E2/... så agenten kan
        // foreslå en relasjon til dem UTEN å måtte oppgi en Guid den ikke kan vite (samme "server-en
        // nummererer, agenten refererer til nummer"-prinsipp som T#-nummereringen av dens EGNE
        // forslag under).
        var eksisterendeTjenester = await db.Tjenester
            .Where(t => t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende")
            .OrderBy(t => t.Tittel)
            .Select(t => new EksisterendeTjenesteRef(t.Id, t.Tittel))
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
        if (eksisterendeTjenester.Count > 0)
        {
            sb.AppendLine("# Eksisterende tjenester");
            for (var i = 0; i < eksisterendeTjenester.Count; i++)
            {
                sb.AppendLine($"E{i + 1}: {eksisterendeTjenester[i].Tittel}");
            }
        }

        var kontekstTekst = sb.ToString();
        KiSvar svar;
        List<TjenesteForslagJson>? forslag;
        try
        {
            // R0 (docs/13-backlog.md §4 punkt 7) — ETT automatisk retry med SAMME kontekst hvis
            // agenten svarer med et tomt forslag-array, se KiForslagRetryHjelper for begrunnelsen.
            (svar, forslag) = await KiForslagRetryHjelper.KjorMedEttRetryVedTomtSvarAsync<TjenesteForslagJson>(
                kallCt => kiKlient.GenererAsync(SystemInstruks, kontekstTekst, kallCt),
                json => JsonSerializer.Deserialize<List<TjenesteForslagJson>>(
                    JsonSvarHjelper.StrimleKodeblokk(json), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
                _logger, "Identifiser tjenester", ct);
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

        // Byggesteg 5 runde 4 — koble regelverksreferanser og relaterte tjenester ETTER at alle
        // tjenestene i batchen er opprettet (T#-referanser kan peke på HVERANDRE). Samme
        // "hallusinert/uoppløselig referanse dropper stille, kaster ikke hele batchen"-prinsipp som
        // eId-fiksen i BegrepsforslagTjeneste (runde 3) — en ekte modell bommer på referanser omtrent
        // like ofte som på eId-format, og det skal ikke koste resten av forslagene.
        for (var i = 0; i < forslag.Count; i++)
        {
            var f = forslag[i];
            var tjeneste = opprettede[i];

            if (f.RegelverksreferanserEid is not null)
            {
                foreach (var eid in f.RegelverksreferanserEid)
                {
                    // Scopet til rettskilderIder (denne kjøringens faktiske kontekst), ikke et globalt
                    // oppslag — forsvarslag mot Eid-kollisjon mellom rettskilder valgt i SAMME kjøring
                    // (f.eks. håndbøker fra ulike virksomheter kan i prinsippet dele samme
                    // dokument-interne nummerering). Rot-fiksen for Brukerveiledning-kollisjonen er at
                    // Eid nå er KanoniskUrl (globalt unik) — se BrukerveiledningImportTjeneste — men
                    // denne scopingen er billig, korrekt, og reduserer blindsonen uansett kildetype.
                    var node = await db.RettskildeNoder
                        .FirstOrDefaultAsync(n => n.Eid == eid && rettskildeIder.Contains(n.RettskildeId), ct);
                    if (node is null) continue; // hallusinert/kortform-eId/utenfor kontekst — drop stille
                    try
                    {
                        await tjenesteregister.KobleRegelverksreferanseAsync(tjeneste.Id, node.RettskildeId, eid, ct);
                    }
                    catch (ArgumentException)
                    {
                        // duplikat — drop stille, samme prinsipp
                    }
                }
            }

            if (f.RelatertTil is not null)
            {
                foreach (var relasjon in f.RelatertTil)
                {
                    var motpartId = LosReferanse(relasjon.Referanse, opprettede, eksisterendeTjenester);
                    if (motpartId is null || !TjenesteavhengighetregisterTjeneste.GyldigeRel.Contains(relasjon.Rel))
                    {
                        continue; // uoppløselig T#/E# eller ukjent Rel — drop stille
                    }
                    try
                    {
                        await tjenesteavhengighetregister.OpprettAsync(
                            virksomhetId, tjeneste.Id, motpartId.Value, relasjon.Rel,
                            hendelseId: null, beskrivelse: null, opprettetAv, ct: ct);
                    }
                    catch (ArgumentException)
                    {
                        // selvreferanse/duplikat/sykel — drop stille
                    }
                }
            }
        }

        return new KiForslagResultat<TjenesteEntitet>(opprettede, svar.InputTokens, svar.OutputTokens, null);
    }

    /// <summary>
    /// Løser en KI-oppgitt "T{n}"/"E{n}"-referanse til en ekte Guid — "T" viser til n-te tjeneste i
    /// DENNE batchen (1-indeksert, samme rekkefølge som <paramref name="nyeForslag"/>), "E" til n-te
    /// eksisterende tjeneste i konteksten. Returnerer null for alt som ikke matcher formatet eller
    /// peker utenfor rekkevidde — ingen gjettet fallback, kalleren dropper referansen stille.
    /// </summary>
    private static Guid? LosReferanse(
        string referanse, List<TjenesteEntitet> nyeForslag, List<EksisterendeTjenesteRef> eksisterendeTjenester)
    {
        if (string.IsNullOrEmpty(referanse) || !int.TryParse(referanse[1..], out var n) || n < 1)
        {
            return null;
        }
        return referanse[0] switch
        {
            'T' or 't' => n <= nyeForslag.Count ? nyeForslag[n - 1].Id : null,
            'E' or 'e' => n <= eksisterendeTjenester.Count ? eksisterendeTjenester[n - 1].Id : null,
            _ => null,
        };
    }
}
