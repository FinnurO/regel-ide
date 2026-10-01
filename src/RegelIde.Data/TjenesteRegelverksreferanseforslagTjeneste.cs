using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #286] KI-assistert bulk-forslag av regelverksreferanser for EKSISTERENDE tjenester som i
/// dag har null <see cref="TjenesteRegelverksreferanseEntitet"/>-rader (typisk importerte tjenester —
/// se AC1-tellingen i PR-beskrivelsen: 14 av 15 seedede tjenester manglet koblingen ved måling
/// 2026-09-30). Til forskjell fra <see cref="TjenesteforslagTjeneste"/> (som foreslår HELT NYE
/// Tjeneste-rader) er tjenesten her allerede en ekte, gjeldende rad — det som foreslås er BARE selve
/// KOBLINGEN, lagt i samme kandidatkø-mønster som <see cref="BegrepDefinisjonRelasjonTjeneste"/>
/// (issue #212): se <see cref="TjenesteRegelverksreferanseForslagEntitet"/> i Entiteter.cs for hele
/// resonnementet bak entitetsvalget (egen kandidattabell, ikke <see cref="ProveniensEntitet"/>/
/// <c>TjenesteEntitet.Status</c>).
///
/// <para>
/// Kandidat-innsnevring er EMBEDDING-basert (<see cref="RettskildeEmbeddingTjeneste"/>/
/// <see cref="IEmbeddingKlient"/>, samme infrastruktur som <see cref="TjenesteforslagTjeneste.KjorForslagMedRagAsync"/>s
/// RAG-spike — se <see cref="RagKontekstHjelper.RangerEtterLikhet"/>) i stedet for å dumpe hele det
/// valgte rettskilde-korpuset som fritekst-kontekst (<see cref="RettskildeKontekstHjelper"/>, mønsteret
/// <see cref="TjenesteforslagTjeneste"/> selv bruker som standard). Vurdert eksplisitt (issue #286 ber
/// om akkurat denne vurderingen): for ÉN tjeneste av gangen er spørsmålet "hvilken paragraf i DETTE
/// korpuset ligner mest på denne tjenestens tittel/beskrivelse" — nøyaktig det embedding-kosinuslikhet
/// allerede løser billig, og et korpus i praktisk skala (tusenvis av noder på tvers av flere lover) er
/// for stort til å sende som fritekst-kontekst per tjeneste uten en slik innsnevring først.
/// </para>
/// </summary>
public sealed class TjenesteRegelverksreferanseforslagTjeneste(
    RegelIdeDbContext db, IKiAgentKlient kiKlient, TjenesteregisterTjeneste tjenesteregister,
    RettskildeEmbeddingTjeneste rettskildeEmbeddingTjeneste, IEmbeddingKlient embeddingKlient,
    IConfiguration config, ILogger<TjenesteRegelverksreferanseforslagTjeneste>? logger = null)
{
    private readonly ILogger<TjenesteRegelverksreferanseforslagTjeneste> _logger =
        logger ?? NullLogger<TjenesteRegelverksreferanseforslagTjeneste>.Instance;

    /// <summary>Antall kandidatparagrafer (embedding-nærmest) som sendes til KI-en PER tjeneste — bevisst
    /// lite: konteksten skal være billig å lese for både KI-en og (via Begrunnelse) saksbehandleren.</summary>
    private const int AntallKandidaterPerTjeneste = 8;

    // Samme begrunnelse for AiForslagVersjon-formen som TjenesteforslagTjeneste/BegrepsforslagTjeneste.
    private string AiForslagVersjon =>
        config["RegelIde:KiAgent:Leverandor"] == "OpenAiKompatibel"
            ? $"OpenAiKompatibel:{config["RegelIde:KiAgent:Modell"]}"
            : "stub-v1";

    // MERK for KiAgentKlientStub (IKiAgentKlient.cs): denne teksten inneholder BEVISST den eksakte
    // frasen "en gitt offentlig tjeneste" — stubben kjenner igjen akkurat denne frasen for å dispatche
    // til sitt eget, kontekst-lesende svar (finner første [eId] i konteksten), se stubbens kommentar.
    // Ikke fjern/omformuler frasen uten å oppdatere stubben tilsvarende.
    private const string SystemInstruks =
        """
        Du er en assistent som avgjør om noen av et sett kandidatparagrafer er det rettslige grunnlaget
        for en gitt offentlig tjeneste.

        Konteksten under starter med en beskrivelse av TJENESTEN (tittel, kort beskrivelse, kompetent
        myndighet), etterfulgt av et sett KANDIDATPARAGRAFER fra rettskilde-korpuset (hver merket med en
        [eId]-tag), funnet ved semantisk søk mot tjenestebeskrivelsen.

        Svar KUN med en ren JSON-array, ingen markdown-kodeblokk (```), ingen forklaringstekst før eller
        etter. Hvert element beskriver ÉN paragraf du mener FAKTISK er et rettslig grunnlag for tjenesten,
        med feltene:
        - "Eid": nøyaktig [eId]-tag (uten hakeparentesene) for kandidatparagrafen — MÅ være én av
          kandidatene i konteksten, aldri oppdiktet.
        - "Begrunnelse": kort begrunnelse (1-2 setninger) for hvorfor akkurat denne paragrafen er
          grunnlaget for tjenesten.

        Returner en tom array [] hvis INGEN av kandidatene tydelig er et rettslig grunnlag for tjenesten
        — ikke tving frem et svar, en tom liste er et fullt gyldig og nyttig svar.
        """;

    private sealed record RegelverksreferanseForslagJson(string Eid, string? Begrunnelse);

    /// <summary>
    /// Kjører forslag for alle <paramref name="virksomhetId"/> sine tjenester som i dag har null
    /// regelverksreferanser, mot kandidatparagrafer hentet fra <paramref name="rettskildeIder"/>. Ett
    /// KI-kall PER tjeneste (kandidatsettet er individuelt for hver tjenestes tittel/beskrivelse) — se
    /// klassekommentaren for hvorfor embedding-innsnevring gjør dette rimelig selv i praktisk skala.
    /// Idempotent (samme (tjeneste, rettskilde, eId)-forslag opprettes ikke to ganger, se
    /// <see cref="OpprettEllerFinnForslagAsync"/>) — trygt å kjøre på nytt, f.eks. etter at flere
    /// rettskilder er lagt til.
    /// </summary>
    public async Task<TjenesteRegelverksreferanseforslagResultat> KjorForslagAsync(
        Guid virksomhetId, IReadOnlyList<Guid> rettskildeIder, string opprettetAv, CancellationToken ct = default)
    {
        if (rettskildeIder.Count == 0)
        {
            throw new ArgumentException("Minst én rettskilde må velges. Ingen gjettet fallback.");
        }
        var rettskilder = await db.Rettskilder
            .Where(r => rettskildeIder.Contains(r.Id) && r.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        if (rettskilder.Count != rettskildeIder.Distinct().Count())
        {
            throw new ArgumentException("En eller flere valgte rettskilder finnes ikke.");
        }

        var tjenester = await db.Tjenester
            .Where(t => t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende")
            .Where(t => !db.TjenesteRegelverksreferanser.Any(r => r.TjenesteId == t.Id))
            .OrderBy(t => t.Tittel)
            .ToListAsync(ct);

        // Lazy: sikrer embeddings for hver valgte rettskilde FØR henting — samme mønster som
        // RagKontekstHjelper.ByggKontekstAsync.
        foreach (var rettskildeId in rettskildeIder.Distinct())
        {
            await rettskildeEmbeddingTjeneste.SikreEmbeddingerAsync(rettskildeId, ct);
        }
        var noder = await db.RettskildeNoder
            .Where(n => rettskildeIder.Contains(n.RettskildeId) && n.Tekst != null)
            .ToListAsync(ct);
        var nodeIder = noder.Select(n => n.Id).ToList();
        var embeddinger = await db.RettskildeNodeEmbeddinger
            .Where(e => nodeIder.Contains(e.NodeId))
            .ToDictionaryAsync(e => e.NodeId, e => e.Embedding, ct);

        var antallVurdert = 0;
        var antallNyeForslag = 0;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        foreach (var tjeneste in tjenester)
        {
            var sporsmalTekst = ByggSporsmalTekst(tjeneste);
            if (string.IsNullOrWhiteSpace(sporsmalTekst)) continue; // ingen tittel/beskrivelse å søke med

            var sporsmalVektor = (await embeddingKlient.EmbedAsync([sporsmalTekst], ct))[0];
            var kandidater = RagKontekstHjelper.RangerEtterLikhet(noder, embeddinger, sporsmalVektor, AntallKandidaterPerTjeneste);
            if (kandidater.Count == 0) continue; // ingen embeddede noder i valgt korpus — ingenting å vurdere

            antallVurdert++;
            var kontekst = ByggKontekstTekst(tjeneste, kandidater);

            KiSvar svar;
            List<RegelverksreferanseForslagJson>? forslag;
            try
            {
                (svar, forslag) = await KiForslagRetryHjelper.KjorMedEttRetryVedTomtSvarAsync<RegelverksreferanseForslagJson>(
                    kallCt => kiKlient.GenererAsync(SystemInstruks, kontekst, kallCt),
                    json => JsonSerializer.Deserialize<List<RegelverksreferanseForslagJson>>(
                        JsonSvarHjelper.StrimleKodeblokk(json), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
                    _logger, "Foreslå regelverksreferanse", ct);
            }
            catch (JsonException ex)
            {
                // Samme "hopp over denne, ikke krasj hele batchen"-holdning som resten av forslags-
                // tjenestene har for hallusinerte referanser — her løftet til hele KI-svaret for ÉN
                // tjeneste, siden resten av batchen (de andre tjenestene) er uavhengige.
                _logger.LogWarning(ex,
                    "KI-klienten returnerte ugyldig JSON for regelverksreferanseforslag på tjeneste {TjenesteId} — hopper over.",
                    tjeneste.Id);
                continue;
            }
            totalInputTokens += svar.InputTokens ?? 0;
            totalOutputTokens += svar.OutputTokens ?? 0;
            if (forslag is null || forslag.Count == 0) continue;

            var kandidatEider = kandidater.ToDictionary(n => n.Eid, n => n);
            var kildeReferanserJson = JsonSerializer.Serialize(new
            {
                rettskildeIder,
                kandidatEider = kandidater.Select(n => n.Eid),
            });
            foreach (var f in forslag)
            {
                // Null/tomt Eid (uventet, men en ekte KI-leverandør kan i prinsippet levere et ufullstendig
                // element) behandles som enhver annen uoppløselig referanse — droppes stille, kaster ikke
                // hele batchen. Samme prinsipp som eId-hallusinasjonshåndteringen i TjenesteforslagTjeneste.
                if (string.IsNullOrEmpty(f.Eid) || !kandidatEider.TryGetValue(f.Eid, out var node)) continue;
                var (nyRad, _) = await OpprettEllerFinnForslagAsync(
                    tjeneste.Id, node.RettskildeId, node.Eid, f.Begrunnelse, opprettetAv, kildeReferanserJson, ct);
                if (nyRad) antallNyeForslag++;
            }
        }

        return new TjenesteRegelverksreferanseforslagResultat(
            tjenester.Count, antallVurdert, antallNyeForslag,
            totalInputTokens == 0 ? null : totalInputTokens, totalOutputTokens == 0 ? null : totalOutputTokens);
    }

    private static string ByggSporsmalTekst(TjenesteEntitet t) =>
        string.Join(" — ", new[] { t.Tittel, t.Beskrivelse, t.KompetentMyndighet, t.Output }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string ByggKontekstTekst(TjenesteEntitet t, List<RettskildeNodeEntitet> kandidater)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Tjeneste");
        sb.AppendLine($"Tittel: {t.Tittel}");
        if (!string.IsNullOrWhiteSpace(t.Beskrivelse)) sb.AppendLine($"Beskrivelse: {t.Beskrivelse}");
        if (!string.IsNullOrWhiteSpace(t.KompetentMyndighet)) sb.AppendLine($"Kompetent myndighet: {t.KompetentMyndighet}");
        sb.AppendLine();
        sb.AppendLine("# Kandidatparagrafer");
        foreach (var node in kandidater)
        {
            sb.AppendLine($"[{node.Eid}] {node.Tekst}");
        }
        return sb.ToString();
    }

    /// <summary>Idempotent — se <see cref="TjenesteRegelverksreferanseForslagEntitet"/> sin unik-indeks
    /// (<c>ux_tjeneste_regelverksreferanse_forslag_par</c>). Samme racy-sveip-vern (<see cref="DbUpdateException"/>-
    /// fallback) som <see cref="BegrepDefinisjonRelasjonTjeneste.OpprettEllerFinnKandidatAsync"/>.</summary>
    private async Task<(bool NyRad, TjenesteRegelverksreferanseForslagEntitet Rad)> OpprettEllerFinnForslagAsync(
        Guid tjenesteId, Guid tilRettskildeId, string tilEid, string? begrunnelse, string opprettetAv,
        string kildeReferanserJson, CancellationToken ct)
    {
        var eksisterende = await db.TjenesteRegelverksreferanseForslag.FirstOrDefaultAsync(
            f => f.TjenesteId == tjenesteId && f.TilRettskildeId == tilRettskildeId && f.TilEid == tilEid, ct);
        if (eksisterende is not null) return (false, eksisterende);

        var rad = new TjenesteRegelverksreferanseForslagEntitet
        {
            Id = Guid.NewGuid(),
            TjenesteId = tjenesteId,
            TilRettskildeId = tilRettskildeId,
            TilEid = tilEid,
            Begrunnelse = begrunnelse,
            Status = "Venter",
            AiForslagVersjon = AiForslagVersjon,
            KildeReferanserJson = kildeReferanserJson,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.TjenesteRegelverksreferanseForslag.Add(rad);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(rad).State = EntityState.Detached;
            var vantLopet = await db.TjenesteRegelverksreferanseForslag.FirstOrDefaultAsync(
                f => f.TjenesteId == tjenesteId && f.TilRettskildeId == tilRettskildeId && f.TilEid == tilEid, ct);
            if (vantLopet is not null) return (false, vantLopet);
            throw;
        }
        return (true, rad);
    }

    /// <summary>
    /// Scopet til ÉN virksomhet (<paramref name="virksomhetId"/>) — til forskjell fra
    /// <see cref="BegrepDefinisjonRelasjonTjeneste.ListerAsync"/> (delt/nasjonalt korpus på tvers av
    /// virksomheter) er en <see cref="TjenesteEntitet"/> ALLTID én bestemt virksomhets eget
    /// arbeidsprodukt (§0.1), så denne køen skal aldri vise en annen virksomhets kandidater — samme
    /// isolasjonsprinsipp som resten av virksomhets-scopede kandidatkøer i appen.
    /// <paramref name="status"/> = <c>null</c> betyr ALLE statuser (samme eksplisitte
    /// "ingen stille standard"-mønster som <see cref="BegrepDefinisjonRelasjonTjeneste.ListerAsync"/>).
    /// Default her er <c>"Venter"</c> (arbeidskøen), ikke <c>null</c> — samme standard som
    /// GET /api/begrep-definisjon-relasjoner.
    /// </summary>
    public Task<List<TjenesteRegelverksreferanseForslagEntitet>> ListerAsync(
        Guid virksomhetId, string? status = "Venter", CancellationToken ct = default)
    {
        var tjenesteIderForVirksomhet = db.Tjenester.Where(t => t.VirksomhetId == virksomhetId).Select(t => t.Id);
        var sporring = db.TjenesteRegelverksreferanseForslag.Where(f => tjenesteIderForVirksomhet.Contains(f.TjenesteId));
        if (status is not null) sporring = sporring.Where(f => f.Status == status);
        return sporring.OrderByDescending(f => f.OpprettetTidspunkt).ToListAsync(ct);
    }

    /// <summary>Samme "join inn det underliggende, klienten trenger ikke ett kall per rad"-mønster som
    /// <see cref="BegrepDefinisjonRelasjonTjeneste.ListerMedForekomsterAsync"/> — join'er inn tjenestens
    /// egen tittel (ett batch-lastet oppslag, ikke N+1).</summary>
    public async Task<List<(TjenesteRegelverksreferanseForslagEntitet Forslag, TjenesteEntitet Tjeneste)>> ListerMedTjenesteAsync(
        Guid virksomhetId, string? status = "Venter", CancellationToken ct = default)
    {
        var forslag = await ListerAsync(virksomhetId, status, ct);
        var tjenesteIder = forslag.Select(f => f.TjenesteId).Distinct().ToList();
        var tjenester = await db.Tjenester.Where(t => tjenesteIder.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        // En tjeneste kan i prinsippet være hard-slettet siden forslaget ble opprettet — dropp stille i
        // stedet for å kaste, samme holdning som resten av kodebasen til "forsvunnet referanse".
        return forslag.Where(f => tjenester.ContainsKey(f.TjenesteId)).Select(f => (f, tjenester[f.TjenesteId])).ToList();
    }

    /// <summary>
    /// Bekrefter forslaget: oppretter en ekte <see cref="TjenesteRegelverksreferanseEntitet"/> via den
    /// allerede eksisterende <see cref="TjenesteregisterTjeneste.KobleRegelverksreferanseAsync"/> — denne
    /// tjenesten dupliserer ALDRI den koblingslogikken (målnode-validering, duplikatsjekk), kun selve
    /// arbeidskø-statusen.
    /// </summary>
    public async Task<TjenesteRegelverksreferanseForslagEntitet?> GodkjennAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var forslag = await db.TjenesteRegelverksreferanseForslag.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (forslag is null) return null;
        if (forslag.Status != "Venter")
        {
            throw new ArgumentException($"Forslaget har status '{forslag.Status}' — kan kun godkjenne forslag med status 'Venter'.");
        }
        try
        {
            await tjenesteregister.KobleRegelverksreferanseAsync(forslag.TjenesteId, forslag.TilRettskildeId, forslag.TilEid, ct, forslag.Felt);
        }
        catch (ArgumentException)
        {
            // Allerede koblet (f.eks. manuelt i mellomtiden, eller av et annet forslag om nøyaktig samme
            // paragraf) — ikke en feil for GODKJENNINGEN av DETTE forslaget, koblingen finnes uansett.
        }
        forslag.Status = "Godkjent";
        forslag.BehandletAv = behandletAv;
        forslag.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return forslag;
    }

    public async Task<TjenesteRegelverksreferanseForslagEntitet?> AvvisAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var forslag = await db.TjenesteRegelverksreferanseForslag.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (forslag is null) return null;
        if (forslag.Status != "Venter")
        {
            throw new ArgumentException($"Forslaget har status '{forslag.Status}' — kan kun avvise forslag med status 'Venter'.");
        }
        forslag.Status = "Avvist";
        forslag.BehandletAv = behandletAv;
        forslag.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return forslag;
    }
}

/// <summary>Sammendraget POST /api/tjenester/regelverksreferanse-forslag/kjor svarer med —
/// <see cref="AntallTjenesterUtenReferanse"/> er hele omfanget (AC1), <see cref="AntallVurdert"/> hvor
/// mange av dem som faktisk fikk kandidatparagrafer (og dermed et KI-kall), <see cref="AntallNyeForslag"/>
/// hvor mange NYE kø-rader dette konkrete kjøret la til (idempotent — kan være 0 ved et gjentatt kjør).</summary>
public sealed record TjenesteRegelverksreferanseforslagResultat(
    int AntallTjenesterUtenReferanse, int AntallVurdert, int AntallNyeForslag, int? InputTokens, int? OutputTokens);
