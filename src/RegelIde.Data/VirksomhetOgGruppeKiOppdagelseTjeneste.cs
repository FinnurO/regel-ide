using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RegelIde.Data;

/// <summary>
/// Ett behandlet KI-forslag — hva som faktisk ble gjort (eller IKKE gjort, og hvorfor). Brukt av både
/// UI-et (kø-visning) og AC1-omfangsmålingen (issue #285): «dekket KI noe regex-sveipet gikk glipp
/// av» krever at hvert enkelt forslag er etterprøvbart, ikke bare en sum.
/// </summary>
public sealed record KiOppdagelseKandidatUtfall(
    string Type, string Navn, string NodeEid,
    Guid? NavnekandidatId, string? NavnekandidatFeil,
    Guid? MyndighetstildelingId, string? RolleIkkeOpprettetGrunn,
    Guid? VirksomhetRelasjonId, string? RelasjonIkkeOpprettetGrunn,
    Guid? GruppeMedlemskapId, string? GruppeAvGruppeIkkeOpprettetGrunn);

/// <summary>Resultatet av ett <see cref="VirksomhetOgGruppeKiOppdagelseTjeneste.KjorOppdagelseAsync"/>-kall
/// — samme "svar + token-forbruk + evt. tom-melding"-form som <see cref="KiForslagResultat{T}"/>, men egen
/// record fordi denne agenten returnerer en LISTE av UTFALL (inkl. forkastede), ikke en liste av
/// opprettede entiteter av én type.</summary>
public sealed record KiOppdagelseResultat(
    IReadOnlyList<KiOppdagelseKandidatUtfall> Kandidater, int? InputTokens, int? OutputTokens, string? Melding);

/// <summary>
/// [Ny, issue #285] «KI-basert oppdagelse av virksomheter, grupper, roller og relasjoner» —
/// TILLEGGET til det deterministiske regex-sveipet (<see cref="NavnekandidatOppdagelseTjeneste.SveipAsync"/>),
/// IKKE en erstatning (issue #285, «Ikke i denne saken»). Samme "KI-forslag"-mønster som
/// <see cref="BegrepsforslagTjeneste"/>/<see cref="TjenesteforslagTjeneste"/>/<see cref="HandlingsforslagTjeneste"/>:
/// kaller <see cref="IKiAgentKlient"/> med rettskildens faktiske, allerede importerte tekst, ber om et
/// strukturert JSON-svar, og LAR ALDRI KI-en skrive direkte til databasen.
/// <para>
/// <b>Kritisk arkitekturkrav (Johann, issue #285, verbatim): «viktig at opprettelsen er api basert slik
/// at KI kan opprette utifra de mønstrene du har beskrevet nøye i api'ene»</b> — denne tjenesten kaller
/// derfor UTELUKKENDE de SAMME tjenestemetodene <c>RegelIde.Api</c> sine eksisterende HTTP-endepunkter
/// selv kaller: <see cref="NavnekandidatOppdagelseTjeneste.OpprettEllerFinnAsync"/> (samme kodesti som
/// <c>POST /api/navnekandidater/manuell</c>), <see cref="MyndighetstildelingTjeneste.OpprettAsync"/>
/// (samme kodesti som <c>POST /api/myndighetstildelinger</c> OG
/// <c>POST /api/navnekandidater/{id}/kobl-til-myndighetstildeling</c>),
/// <see cref="VirksomhetRelasjonregisterTjeneste.OpprettAsync"/> (samme kodesti som
/// <c>POST /api/virksomheter/{id}/relasjoner</c> OG <c>.../kobl-til-relasjon</c>), og
/// <see cref="GruppeMedlemskapTjeneste.OpprettAsync"/> (samme kodesti som <c>POST /api/gruppemedlemskap</c>
/// OG <c>.../kobl-til-gruppe-av-gruppe</c>). Ingen ny, parallell skrivevei til noen av disse tabellene.
/// </para>
/// <para>
/// <b>Hvorfor tjenestemetodene direkte, og ikke et ekte utgående HTTP-kall til seg selv:</b> et internt
/// loopback-HTTP-kall ville trengt sitt eget autentiseringsspor og gitt akkurat samme kodesti likevel
/// (endepunktene ER tynne wrappere rundt disse metodene, se Program.cs) — bare med et unødvendig
/// nettverkshopp og en egen feilhåndteringsvei mellom. Samme vurdering som allerede gjelder for
/// <see cref="NavnekandidatOppdagelseTjeneste.KoblTilMyndighetstildelingAsync"/> m.fl., som også kaller
/// <see cref="MyndighetstildelingTjeneste"/> direkte, ikke over HTTP.
/// </para>
/// <para>
/// <b>Aldri publisert/stolt på uten et menneske (issue #285s eksplisitte krav):</b>
/// <list type="bullet">
/// <item>Selve navneform-kandidaten opprettes med <c>Status = "Venter"</c> og
/// <see cref="NavnekandidatOppdagelseTjeneste.KiFriSveipOppdagelsesKilde"/> — UENDRET fra ethvert annet
/// sveiptreff, må gjennom nøyaktig samme godkjenn/avvis-flyt/veiviser som et menneske ville brukt.</item>
/// <item>Rolle-/relasjon-/gruppe-av-gruppe-forslagene skrives med <c>Status = "foreslatt_av_ai"</c> (issue
/// #285 AC5 — det NYE statusfeltet på <see cref="MyndighetstildelingEntitet"/>/<see cref="VirksomhetRelasjonEntitet"/>/
/// <see cref="GruppeMedlemskapEntitet"/>), ALDRI <c>"validert"</c> direkte — et menneske må eksplisitt
/// godkjenne (<c>GodkjennAsync</c> på hver av de tre tjenestene) før raden regnes som gjeldende
/// (issue #285 AC6).</item>
/// </list>
/// </para>
/// <para>
/// <b>Automatisert navneoppslag er BEVISST BEGRENSET til eksakt/navneform-match</b>
/// (<see cref="VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnEllerNavneformAsync"/>) — «ingen gjettet
/// fallback», samme prinsipp som resten av kodebasen. En rolle/relasjon der KI-en ikke kan pekes ENTYDIG
/// til en allerede kjent <see cref="Virksomhet"/>/gruppebegrep i registeret, opprettes IKKE — kun selve
/// navneform-kandidaten (som uansett krever et menneske i wizarden for å velge/opprette virksomheten).
/// Dette er en reell, dokumentert begrensning: en KI-oppdagelse av et HELT NYTT organ kan foreslå
/// navneformen, men ikke automatisk en rolle/relasjon for det FØR et menneske har koblet navnet til en
/// konkret virksomhet gjennom veiviseren.
/// </para>
/// <para>
/// <b>«Gruppe av gruppe» (issue #285 AC2s tredje mekanisme) er av samme grunn begrenset til gruppe-
/// kandidater som allerede har en godkjent, EKSISTERENDE <see cref="BegrepEntitet"/>-rad</b> (samme
/// (Term, LovkildeId)-identitet som <see cref="VirksomhetsbegrepTjeneste.OpprettGruppebegrepAsync"/>) —
/// en helt NY gruppe har ennå ingen begrep-rad å knytte medlemskapet til før et menneske har godkjent
/// selve gruppekandidaten. Se <see cref="KjorOppdagelseAsync"/>s kommentar for detaljene.
/// </para>
/// </summary>
public sealed class VirksomhetOgGruppeKiOppdagelseTjeneste(
    RegelIdeDbContext db, IKiAgentKlient kiKlient, IConfiguration config,
    NavnekandidatOppdagelseTjeneste navnekandidatOppdagelse, VirksomhetOppslagTjeneste virksomhetOppslag,
    MyndighetstildelingTjeneste myndighetstildeling, VirksomhetRelasjonregisterTjeneste virksomhetRelasjonregister,
    GruppeMedlemskapTjeneste gruppeMedlemskap,
    ILogger<VirksomhetOgGruppeKiOppdagelseTjeneste>? logger = null)
{
    private readonly ILogger<VirksomhetOgGruppeKiOppdagelseTjeneste> _logger =
        logger ?? NullLogger<VirksomhetOgGruppeKiOppdagelseTjeneste>.Instance;

    // Samme mønster som BegrepsforslagTjeneste/TjenesteforslagTjeneste — kun "stub-v1" er faktisk
    // riktig når KiAgentKlientStub kjører (utviklings-/CI-miljø uten RegelIde:KiAgent:*-secrets).
    private string AiForslagVersjon =>
        config["RegelIde:KiAgent:Leverandor"] == "OpenAiKompatibel"
            ? $"OpenAiKompatibel:{config["RegelIde:KiAgent:Modell"]}"
            : "stub-v1";

    private const string SystemInstruks =
        """
        Du er en juridisk assistent som leser norsk lovtekst FRITT (ikke bare kjente mønstre) for å finne
        virksomheter, grupper/organer, roller og relasjoner mellom organer.

        Konteksten under er lovtekst der hver paragraf/ledd er merket med en [eId]-tag foran teksten
        (f.eks. "[§1-5] Alkoholholdig drikk...").

        Svar KUN med en ren JSON-array, ingen markdown-kodeblokk (```), ingen forklaringstekst før eller
        etter. Hvert element er ETT navngitt organ/virksomhet nevnt i teksten, med NØYAKTIG disse feltene:
        - "Type": "virksomhet" (et konkret, navngitt organ/virksomhet — f.eks. "Statens vegvesen",
          "Energiklagenemnda") eller "gruppe" (en generisk juridisk-aktør-rolle uten egennavn-status —
          f.eks. "kommunen", "statsforvalteren")
        - "Navn": navnet EKSAKT slik det står i lovteksten (samme stavemåte/store-små bokstaver) — MÅ
          finnes ORDRETT i teksten til den oppgitte [eId]-taggen, ikke omskrevet/normalisert
        - "NodeEid": den eksakte [eId]-taggen (uten hakeparentesene) der navnet faktisk står
        - "Rolle": KUN hvis teksten EKSPLISITT tildeler organet en navngitt rolle/myndighet i nøyaktig
          dette leddet — et objekt {"RolleNavn": "rollebegrepet slik det brukes andre steder i loven",
          "ParagrafEid": "[eId] der rollen tildeles, eller null for samme som NodeEid"}, ellers null
        - "Relasjon": KUN hvis teksten EKSPLISITT sier at organet står i et hierarkisk/administrativt
          forhold til ET ANNET navngitt organ (underlagt/sekretariat for/klageinstans for/er en enhet i)
          — et objekt {"Type": "underlagt"|"sekretariat"|"klageinstans"|"enhet_i", "MotpartNavn": "det
          andre organets navn, eksakt som det står i teksten", "HjemletHer": true hvis DENNE paragrafen
          faktisk sier det, false hvis du utleder det fra en annen kontekst}, ellers null
        - "GruppeAvGruppe": KUN for Type="gruppe" OG kun hvis teksten sier at DENNE gruppen selv inngår
          i/er en del av en STØRRE, navngitt gruppe — et objekt {"OverordnetGruppeNavn": "den større
          gruppens navn"}, ellers null

        Dikt ikke opp organer/roller/relasjoner som ikke faktisk fremgår av teksten. Returner en tom
        array [] hvis du ikke finner noe. Vær presis — foreslå Rolle/Relasjon/GruppeAvGruppe KUN når
        teksten faktisk sier det eksplisitt, ikke ut fra alminnelig kunnskap om norsk forvaltning.
        """;

    private sealed record RolleForslagJson(string RolleNavn, string? ParagrafEid);
    private sealed record RelasjonForslagJson(string Type, string MotpartNavn, bool HjemletHer);
    private sealed record GruppeAvGruppeForslagJson(string OverordnetGruppeNavn);

    private sealed record KandidatForslagJson(
        string Type, string Navn, string NodeEid,
        RolleForslagJson? Rolle, RelasjonForslagJson? Relasjon, GruppeAvGruppeForslagJson? GruppeAvGruppe);

    /// <summary>
    /// Kjører KI-oppdagelsen mot ÉN rettskildes faktiske, allerede importerte tekst. Bevisst scopet til
    /// ÉN rettskilde av gangen (til forskjell fra <see cref="BegrepsforslagTjeneste.KjorForslagAsync"/>,
    /// som tar flere) — et <c>[eId]</c> er kun unikt INNENFOR sin egen rettskilde, og
    /// <see cref="NavnekandidatOppdagelseTjeneste.OpprettEllerFinnAsync"/> krever en eksakt
    /// <c>RettskildeId</c> for hvert treff. Flere rettskilder kjøres ved å kalle denne flere ganger (se
    /// <c>POST /api/ki-oppdagelse/kjor</c>, som gjør nøyaktig det per oppgitt id).
    /// <para>
    /// Se klassekommentaren for HVORFOR rolle-/relasjon-/gruppe-av-gruppe-forslag krever et allerede
    /// KJENT, entydig navnematch (virksomhet i registeret / gruppebegrep i databasen) for å faktisk
    /// skrive en rad — et forslag KI-en ikke kan forankre entydig blir liggende som RENT
    /// navneform-forslag (fortsatt verdifullt: et menneske ser det i navnekandidat-køen), ikke kastet
    /// bort, men heller ikke tvunget gjennom med en gjettet kobling.
    /// </para>
    /// </summary>
    public async Task<KiOppdagelseResultat> KjorOppdagelseAsync(Guid rettskildeId, string opprettetAv, CancellationToken ct = default)
    {
        var kontekst = await RettskildeKontekstHjelper.ByggKontekstAsync(db, [rettskildeId], ct);

        KiSvar svar;
        List<KandidatForslagJson>? forslag;
        try
        {
            (svar, forslag) = await KiForslagRetryHjelper.KjorMedEttRetryVedTomtSvarAsync<KandidatForslagJson>(
                kallCt => kiKlient.GenererAsync(SystemInstruks, kontekst, kallCt),
                json => JsonSerializer.Deserialize<List<KandidatForslagJson>>(
                    JsonSvarHjelper.StrimleKodeblokk(json), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
                _logger, "KI-oppdagelse virksomhet/gruppe", ct);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"KI-klienten returnerte ugyldig JSON for KI-oppdagelse: {ex.Message}", ex);
        }
        if (forslag is null || forslag.Count == 0)
        {
            return new KiOppdagelseResultat(
                [], svar.InputTokens, svar.OutputTokens,
                "KI-agenten svarte, men fant ingen virksomhet-/gruppekandidater i valgt rettskilde.");
        }

        // Rettskilden er allerede validert til å finnes/være gjeldende av ByggKontekstAsync over (den
        // kaster ArgumentException ellers) — ingen ny sjekk trengs her.
        var utfall = new List<KiOppdagelseKandidatUtfall>();
        foreach (var k in forslag)
        {
            utfall.Add(await BehandleEttForslagAsync(k, rettskildeId, opprettetAv, ct));
        }
        return new KiOppdagelseResultat(utfall, svar.InputTokens, svar.OutputTokens, null);
    }

    /// <summary>
    /// [Rettet, live-verifisering issue #285 AC1] Ekte KI-svar (verifisert mot HostYourAI/DeepSeek-V4-
    /// Flash, ekte «Forskrift om Energiklagenemnda» hentet fra Lovdata) ekkoer IKKE alltid <c>[eId]</c>-
    /// taggen ordrett — modellen returnerte konsekvent en KORTFORM ("§1/ledd-1") i stedet for hele den
    /// AKN-formede eId-en rettskilden faktisk har lagret (samme "modeller siterer ikke alltid ordrett"-
    /// erfaring som <see cref="BegrepsforslagTjeneste.KjorForslagAsync"/> allerede dokumenterer for
    /// <c>LovreferanseEid</c>). Eksakt match FØRST, deretter et suffiks-fall-tilbake — «ingen gjettet
    /// fallback» ivaretas ved at suffikset må være ENTYDIG innenfor denne ene rettskilden; er det
    /// tvetydig (usannsynlig, men mulig for et for kort/generisk suffiks), behandles det som «ikke
    /// funnet» i stedet for å gjette hvilken av flere noder som menes.
    /// </summary>
    private async Task<RettskildeNodeEntitet?> FinnNodeAsync(Guid rettskildeId, string nodeEid, CancellationToken ct)
    {
        var eksakt = await db.RettskildeNoder.FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == nodeEid, ct);
        if (eksakt is not null) return eksakt;

        // [Rettet, live-verifisering, to separate kjøringer] Ekte eId-form er ALLTID skilt med skråstrek
        // (LovdataIdentifikatorer.LeddEid: "{paragrafEid}/ledd-{i}") — men modellen har, på TVERS AV TO
        // separate kjøringer mot SAMME tekst, echoet to ULIKE kortformer av nøyaktig samme posisjon:
        // "§1/ledd-1" én gang, "§1-ledd-1" en annen gang. Ren suffiks-sammenligning fanger den FØRSTE
        // formen, ikke den andre — normaliser derfor BEGGE sider (kun for selve SAMMENLIGNINGEN, aldri
        // for hva som faktisk lagres — se ekteNodeEid i BehandleEttForslagAsync) ved å behandle '-' og
        // '/' som samme skilletegn. Henter alle noder for DENNE ene rettskilden (ikke hele korpuset) og
        // sammenligner i minnet — et lite, avgrenset sett, ikke et ytelsesproblem.
        string Normaliser(string s) => s.Replace('-', '/');
        var normalisertMål = Normaliser(nodeEid);
        var alleNoder = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).ToListAsync(ct);
        var suffiksTreff = alleNoder.Where(n => Normaliser(n.Eid).EndsWith(normalisertMål, StringComparison.Ordinal)).Take(2).ToList();
        return suffiksTreff.Count == 1 ? suffiksTreff[0] : null;
    }

    private async Task<KiOppdagelseKandidatUtfall> BehandleEttForslagAsync(
        KandidatForslagJson k, Guid rettskildeId, string opprettetAv, CancellationToken ct)
    {
        if (k.Type is not ("virksomhet" or "gruppe"))
        {
            return new KiOppdagelseKandidatUtfall(
                k.Type, k.Navn, k.NodeEid, null, $"Ukjent type '{k.Type}' fra KI-agenten.",
                null, null, null, null, null, null);
        }

        var rettskildeNode = await FinnNodeAsync(rettskildeId, k.NodeEid, ct);
        if (rettskildeNode?.Tekst is null)
        {
            return new KiOppdagelseKandidatUtfall(
                k.Type, k.Navn, k.NodeEid, null, $"Fant ingen node med eId '{k.NodeEid}' i rettskilden — ikke behandlet.",
                null, null, null, null, null, null);
        }

        // Eksakt, deretter case-insensitiv fallback (ekte modeller normaliserer av og til store/små
        // bokstaver selv om instruksen ber om ordrett sitat) — men ALDRI en fuzzy/nærmeste-treff-søk:
        // finner vi det ikke i teksten i det hele tatt, forkastes forslaget. Selve LAGREDE teksten er
        // alltid substrengen slik den FAKTISK står (bevarer ekte store/små bokstaver for "virksomhet").
        var tekst = rettskildeNode.Tekst;
        var idx = tekst.IndexOf(k.Navn, StringComparison.Ordinal);
        if (idx < 0) idx = tekst.IndexOf(k.Navn, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return new KiOppdagelseKandidatUtfall(
                k.Type, k.Navn, k.NodeEid, null,
                $"Teksten '{k.Navn}' ble ikke funnet i node '{k.NodeEid}' — KI-en siterte ikke ordrett. Ikke behandlet.",
                null, null, null, null, null, null);
        }
        var faktiskTekst = tekst.Substring(idx, k.Navn.Length);
        // Den EKTE, lagrede eId-en (kan avvike fra k.NodeEid — se FinnNodeAsync) — brukes for ALT som
        // skrives videre, aldri modellens rå (mulig forkortede) sitat.
        var ekteNodeEid = rettskildeNode.Eid;

        Guid? navnekandidatId = null;
        string? navnekandidatFeil = null;
        try
        {
            var kandidat = await navnekandidatOppdagelse.OpprettEllerFinnAsync(
                faktiskTekst, k.Type, rettskildeId, ekteNodeEid, idx, idx + faktiskTekst.Length, opprettetAv, ct,
                oppdagelsesKilde: NavnekandidatOppdagelseTjeneste.KiFriSveipOppdagelsesKilde);
            navnekandidatId = kandidat.Id;
        }
        catch (ArgumentException ex)
        {
            navnekandidatFeil = ex.Message;
        }

        Guid? myndighetstildelingId = null;
        string? rolleGrunn = k.Rolle is null ? null : "Ingen rolle foreslått av KI-agenten for dette treffet.";
        if (k.Type == "virksomhet" && k.Rolle is not null)
        {
            (myndighetstildelingId, rolleGrunn) = await ForsokRolleAsync(k.Rolle, faktiskTekst, rettskildeId, ekteNodeEid, opprettetAv, ct);
        }

        Guid? relasjonId = null;
        string? relasjonGrunn = k.Relasjon is null ? null : "Ingen relasjon foreslått av KI-agenten for dette treffet.";
        if (k.Type == "virksomhet" && k.Relasjon is not null)
        {
            (relasjonId, relasjonGrunn) = await ForsokRelasjonAsync(k.Relasjon, faktiskTekst, rettskildeId, ekteNodeEid, opprettetAv, ct);
        }

        Guid? gruppeMedlemskapId = null;
        string? gruppeAvGruppeGrunn = k.GruppeAvGruppe is null ? null : "Ingen gruppe-av-gruppe foreslått av KI-agenten for dette treffet.";
        if (k.Type == "gruppe" && k.GruppeAvGruppe is not null)
        {
            (gruppeMedlemskapId, gruppeAvGruppeGrunn) = await ForsokGruppeAvGruppeAsync(
                k.GruppeAvGruppe, faktiskTekst, rettskildeId, ekteNodeEid, opprettetAv, ct);
        }

        return new KiOppdagelseKandidatUtfall(
            k.Type, faktiskTekst, ekteNodeEid, navnekandidatId, navnekandidatFeil,
            myndighetstildelingId, rolleGrunn, relasjonId, relasjonGrunn, gruppeMedlemskapId, gruppeAvGruppeGrunn);
    }

    /// <summary>
    /// Forsøker å forankre og opprette en <see cref="MyndighetstildelingEntitet"/> for et KI-foreslått
    /// rolle-treff. «Ingen gjettet fallback» på BEGGE bindingene: rollebegrepet MÅ finnes entydig fra
    /// før (KI-en dikter ikke opp et nytt rollebegrep her — det er <see cref="NavnekandidatOppdagelseTjeneste.GodkjennAsync"/>s
    /// jobb for en <c>"gruppe"</c>-kandidat, en helt annen kjede), og virksomheten kandidatens EGET navn
    /// peker på må allerede finnes i registeret/navneformene (<see cref="VirksomhetOppslagTjeneste"/>).
    /// </summary>
    private async Task<(Guid? Id, string? IkkeOpprettetGrunn)> ForsokRolleAsync(
        RolleForslagJson rolle, string virksomhetNavn, Guid rettskildeId, string kandidatNodeEid, string opprettetAv, CancellationToken ct)
    {
        var rolleTreff = await db.Begreper
            .Where(b => b.Begrepskategori == "gruppe" && b.Entitetsstatus == "gjeldende"
                        && b.Term.ToLower() == rolle.RolleNavn.Trim().ToLower())
            .Select(b => b.Id)
            .ToListAsync(ct);
        if (rolleTreff.Count != 1)
        {
            return (null, rolleTreff.Count == 0
                ? $"Fant ingen gruppebegrep med term '{rolle.RolleNavn}' — rolletildelingen krever et allerede kjent rollebegrep."
                : $"Fant {rolleTreff.Count} gruppebegrep med term '{rolle.RolleNavn}' — ikke entydig, ingen gjettet fallback.");
        }

        var virksomhetId = await virksomhetOppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(virksomhetNavn, ct);
        if (virksomhetId is null)
        {
            return (null, $"Fant ingen virksomhet med navn/navneform '{virksomhetNavn}' i registeret — ingen gjettet fallback.");
        }

        // Null betyr "samme som NodeEid" (se system-instruksen) — kandidatens EGEN node er allerede
        // bekreftet å finnes (BehandleEttForslagAsync hentet den før dette kalles) via NØYAKTIG samme
        // eksakt-så-suffiks-oppslag som her, så vi kan trygt gjenbruke kandidatNodeEid (allerede den
        // EKTE, lagrede formen) direkte i det tilfellet. Et OPPGITT, ANNET ParagrafEid må derimot
        // løses på samme lempelige måte (se FinnNodeAsync) — MyndighetstildelingTjeneste.OpprettAsync
        // gjør sin EGEN eksakte eksistens-sjekk nedstrøms, så den EKTE, lagrede eId-formen må brukes
        // her, ikke modellens rå (mulig forkortede) sitat.
        string paragrafEid;
        if (rolle.ParagrafEid is null)
        {
            paragrafEid = kandidatNodeEid;
        }
        else
        {
            var paragrafNode = await FinnNodeAsync(rettskildeId, rolle.ParagrafEid, ct);
            if (paragrafNode is null)
            {
                return (null, $"ParagrafEid '{rolle.ParagrafEid}' fra KI-agenten finnes ikke i rettskilden — ikke behandlet.");
            }
            paragrafEid = paragrafNode.Eid;
        }

        var eksisterende = await db.Myndighetstildelinger.FirstOrDefaultAsync(
            m => m.GruppeBegrepId == rolleTreff[0] && m.VirksomhetId == virksomhetId.Value && m.HjemmelRettskildeId == rettskildeId, ct);
        if (eksisterende is not null) return (eksisterende.Id, null);

        try
        {
            var tildeling = await myndighetstildeling.OpprettAsync(
                rolleTreff[0], virksomhetId.Value, rettskildeId, [new ParagrafspennPar(paragrafEid, null)],
                vilkaar: null, opprettetAv, ct: ct, status: "foreslatt_av_ai", aiForslagVersjon: AiForslagVersjon);
            return (tildeling.Id, null);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>Se <see cref="ForsokRolleAsync"/> — samme «ingen gjettet fallback»-linje, nå for BEGGE
    /// virksomhetene en <see cref="VirksomhetRelasjonEntitet"/> trenger.</summary>
    private async Task<(Guid? Id, string? IkkeOpprettetGrunn)> ForsokRelasjonAsync(
        RelasjonForslagJson relasjon, string virksomhetNavn, Guid rettskildeId, string nodeEid, string opprettetAv, CancellationToken ct)
    {
        var relasjonsTypeFinnes = await db.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kode == relasjon.Type && t.Aktiv, ct);
        if (!relasjonsTypeFinnes)
        {
            return (null, $"Ukjent relasjonstype '{relasjon.Type}' fra KI-agenten — ingen gjettet fallback.");
        }

        var fraId = await virksomhetOppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(virksomhetNavn, ct);
        if (fraId is null)
        {
            return (null, $"Fant ingen virksomhet med navn/navneform '{virksomhetNavn}' i registeret — ingen gjettet fallback.");
        }
        var tilId = await virksomhetOppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(relasjon.MotpartNavn, ct);
        if (tilId is null)
        {
            return (null, $"Fant ingen virksomhet med navn/navneform '{relasjon.MotpartNavn}' (motpart) i registeret — ingen gjettet fallback.");
        }
        if (fraId.Value == tilId.Value)
        {
            return (null, "KI-agenten foreslo en relasjon fra en virksomhet til seg selv — forkastet.");
        }

        var eksisterende = await db.VirksomhetRelasjoner.FirstOrDefaultAsync(
            r => r.Entitetsstatus == "gjeldende" && r.FraVirksomhetId == fraId.Value
                 && r.TilVirksomhetId == tilId.Value && r.RelasjonsType == relasjon.Type, ct);
        if (eksisterende is not null) return (eksisterende.Id, null);

        try
        {
            var opprettet = await virksomhetRelasjonregister.OpprettAsync(
                fraId.Value, tilId.Value, relasjon.Type,
                relasjon.HjemletHer ? rettskildeId : null, relasjon.HjemletHer ? nodeEid : null,
                relasjon.HjemletHer ? null : "KI-forslag — ingen bekreftet hjemmel oppgitt av agenten.",
                opprettetAv, ct, status: "foreslatt_av_ai", aiForslagVersjon: AiForslagVersjon);
            return (opprettet.Id, null);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// Se klassekommentarens avsnitt om gruppe-av-gruppe: krever at BÅDE den underordnede gruppen
    /// (kandidatens EGEN term) og den overordnede gruppen allerede finnes som EKTE, godkjente
    /// <see cref="BegrepEntitet"/>-rader (<see cref="BegrepEntitet.Begrepskategori"/> = <c>"gruppe"</c>)
    /// — en helt ny gruppekandidat har ingen begrep-rad ennå, og opprettes ikke automatisk her (det er
    /// et menneskes jobb via <see cref="NavnekandidatOppdagelseTjeneste.GodkjennAsync"/>/veiviseren).
    /// </summary>
    private async Task<(Guid? Id, string? IkkeOpprettetGrunn)> ForsokGruppeAvGruppeAsync(
        GruppeAvGruppeForslagJson gruppeAvGruppe, string gruppeNavn, Guid rettskildeId, string nodeEid, string opprettetAv, CancellationToken ct)
    {
        var underordnetTreff = await db.Begreper
            .Where(b => b.Begrepskategori == "gruppe" && b.Entitetsstatus == "gjeldende" && b.LovkildeId == rettskildeId
                        && b.Term.ToLower() == gruppeNavn.Trim().ToLower())
            .Select(b => b.Id)
            .ToListAsync(ct);
        if (underordnetTreff.Count != 1)
        {
            return (null, underordnetTreff.Count == 0
                ? $"Gruppebegrepet '{gruppeNavn}' finnes ikke ennå for denne loven — kandidaten må godkjennes av et menneske først."
                : $"Fant {underordnetTreff.Count} gruppebegrep '{gruppeNavn}' for denne loven — ikke entydig, ingen gjettet fallback.");
        }

        var overordnetTreff = await db.Begreper
            .Where(b => b.Begrepskategori == "gruppe" && b.Entitetsstatus == "gjeldende"
                        && b.Term.ToLower() == gruppeAvGruppe.OverordnetGruppeNavn.Trim().ToLower())
            .Select(b => b.Id)
            .ToListAsync(ct);
        if (overordnetTreff.Count != 1)
        {
            return (null, overordnetTreff.Count == 0
                ? $"Fant ingen overordnet gruppebegrep '{gruppeAvGruppe.OverordnetGruppeNavn}' — ingen gjettet fallback."
                : $"Fant {overordnetTreff.Count} overordnede gruppebegrep '{gruppeAvGruppe.OverordnetGruppeNavn}' — ikke entydig.");
        }
        if (underordnetTreff[0] == overordnetTreff[0])
        {
            return (null, "KI-agenten foreslo at gruppen er medlem av seg selv — forkastet.");
        }

        var eksisterende = await db.GruppeMedlemskap.FirstOrDefaultAsync(
            m => m.OverordnetGruppeBegrepId == overordnetTreff[0] && m.UnderordnetGruppeBegrepId == underordnetTreff[0], ct);
        if (eksisterende is not null) return (eksisterende.Id, null);

        try
        {
            var opprettet = await gruppeMedlemskap.OpprettAsync(
                overordnetTreff[0], underordnetTreff[0], rettskildeId, [new ParagrafspennPar(nodeEid, null)],
                opprettetAv, ct: ct, status: "foreslatt_av_ai", aiForslagVersjon: AiForslagVersjon);
            return (opprettet.Id, null);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }
    }
}
