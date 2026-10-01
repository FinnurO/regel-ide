using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #290] KI-assistert oppgradering av <see cref="HandlingRegelverksreferanseEntitet"/>-rader
/// fra dokumentnivå til paragrafnivå, for de skjemaene fra <see cref="OppgaveregisterHandlingSeed"/> der
/// (1) rettskilden ER importert og allerede matchet på DOKUMENTNIVÅ, MEN (2) seedens egen enkle regex
/// (<c>OppgaveregisterHandlingSeed.TrekkUtEnkeltParagraf</c>, kun "§ 42"/"§4-1"-formen) ikke klarte å
/// bekrefte noen paragrafnivå-node — typisk komma-/spenn-lister ("§§ 21-4, 22-3", "§§ 1 til 5") eller
/// "jf."-kryssreferanser. Samme kø/godkjenn/avvis/proveniens-mønster (egen kandidatentitet med sitt eget
/// Status/AiForslagVersjon/BehandletAv, IKKE <see cref="ProveniensEntitet"/>) som
/// <see cref="TjenesteRegelverksreferanseforslagTjeneste"/> (issue #286) — se
/// <see cref="HandlingRegelverksreferanseForslagEntitet"/> for hele resonnementet bak entitetsvalget.
///
/// <para>
/// KJØRES ALDRI for <c>RettskildematcherIkkeFunnet</c>-tilfeller (rettskilden finnes ikke i korpuset i
/// det hele tatt) — det er et IMPORTBEHOV, ikke et tolkningsbehov, og filtreres bort AV KONSTRUKSJON her:
/// <see cref="HandlingRegelverksreferanseEntitet"/>-rader opprettes av seeden KUN når rettskilden faktisk
/// ble funnet (se dens klassekommentar punkt (c)), så kandidatspørringen under ser aldri disse i det
/// hele tatt — det finnes ingen rad å iterere over for dem.
/// </para>
///
/// <para>
/// **Kandidat-innsnevring er BEVISST IKKE embedding-basert**, til forskjell fra
/// <see cref="TjenesteRegelverksreferanseforslagTjeneste"/>. Der fantes ingen fritekst-hint i det hele
/// tatt (en Tjeneste-tittel/beskrivelse må søkes semantisk mot HELE det valgte lovkorpuset), så
/// embedding-kosinuslikhet var den eneste rimelige måten å snevre inn et stort korpus på. Her har vi noe
/// mye bedre: <see cref="HandlingRegelverksreferanseEntitet.KildeHenvisningFritekst"/> NEVNER allerede de
/// faktiske paragrafnumrene (bare i en form regex-en i seeden ikke tolker), OG vi vet nøyaktig HVILKEN
/// ene rettskilde (dokumentnivå-matchen er allerede gjort). Løsningen er derfor en UTVIDET, permissiv
/// regex (<see cref="TrekkUtFlereParagrafnumre"/>) som trekker ut ALLE tallkandidater etter FØRSTE "§" i
/// fritekst-strengen (håndterer "§§ 21-4, 22-3" — kun første tall har sitt eget "§" — OG "jf. § 3", der
/// en senere "§" introduserer enda et tall), hver BEKREFTET mot en faktisk importert paragraf-node i
/// AKKURAT denne rettskilden (samme "aldri gjett, alltid bekreft mot ekte struktur"-prinsipp som seeden
/// selv). KI-ens jobb er IKKE å finne kandidatene (det gjør regex+bekreftelse allerede, deterministisk),
/// men å AVGJØRE hvilke av de bekreftede kandidatene som faktisk ER et rettslig grunnlag for handlingen —
/// f.eks. kan én være en ren "jf."-kryssreferanse, ikke selve hjemmelen — nøyaktig samme relevans-
/// vurdering som <see cref="TjenesteRegelverksreferanseforslagTjeneste"/>s KI gjør, nå anvendt på et
/// lite, DETERMINISTISK bekreftet kandidatsett i stedet for et embedding-snevret ett. En henvisning uten
/// noen "§" i det hele tatt (Kapittel-betegnelser, romertall) gir ingen kandidater og hopper derfor
/// automatisk over — samme grense som seedens egen <c>TrekkUtEnkeltParagraf</c> allerede har, kun utvidet
/// til flere paragrafer per henvisning, ikke til nye henvisningsformer.
/// </para>
/// </summary>
public sealed partial class HandlingRegelverksreferanseforslagTjeneste(
    RegelIdeDbContext db, IKiAgentKlient kiKlient, HandlingregisterTjeneste handlingregister,
    IConfiguration config, ILogger<HandlingRegelverksreferanseforslagTjeneste>? logger = null)
{
    private readonly ILogger<HandlingRegelverksreferanseforslagTjeneste> _logger =
        logger ?? NullLogger<HandlingRegelverksreferanseforslagTjeneste>.Instance;

    private string AiForslagVersjon =>
        config["RegelIde:KiAgent:Leverandor"] == "OpenAiKompatibel"
            ? $"OpenAiKompatibel:{config["RegelIde:KiAgent:Modell"]}"
            : "stub-v1";

    // MERK for KiAgentKlientStub (IKiAgentKlient.cs): denne teksten inneholder BEVISST frasen "rettslig
    // grunnlag for handlingen" — stubben kjenner igjen akkurat denne frasen (skilt fra
    // TjenesteRegelverksreferanseforslagTjenestes "en gitt offentlig tjeneste") for å dispatche til SAMME
    // gjenbrukbare kontekst-lesende svar (finner første [eId] i konteksten) — begge agentene svarer i
    // identisk JSON-form, se RegelverksreferanseForslagJson under. Ikke fjern/omformuler frasen uten å
    // oppdatere stubben tilsvarende.
    private const string SystemInstruks =
        """
        Du er en assistent som tolker en Oppgaveregister-lovhjemmel-henvisning (fritekst, f.eks.
        "§§ 21-4, 22-3" eller "jf. § 5") og avgjør hvilke av et sett BEKREFTEDE kandidatparagrafer (funnet
        direkte i rettskildens egen struktur — paragrafnumrene er allerede trukket ut og bekreftet, ikke
        noe DU skal finne) som faktisk ER et rettslig grunnlag for handlingen — til forskjell fra f.eks.
        en ren "jf."-kryssreferanse til noe beslektet, men ikke selve hjemmelen.

        Konteksten under starter med en beskrivelse av HANDLINGEN (navn, evt. merknad) og selve den
        opprinnelige fritekst-henvisningen, etterfulgt av et sett BEKREFTEDE KANDIDATPARAGRAFER (hver
        merket med en [eId]-tag og selve paragrafteksten).

        Svar KUN med en ren JSON-array, ingen markdown-kodeblokk (```), ingen forklaringstekst før eller
        etter. Hvert element beskriver ÉN paragraf du mener FAKTISK er et rettslig grunnlag for
        handlingen, med feltene:
        - "Eid": nøyaktig [eId]-tag (uten hakeparentesene) for kandidatparagrafen — MÅ være én av
          kandidatene i konteksten, aldri oppdiktet.
        - "Begrunnelse": kort begrunnelse (1-2 setninger) for hvorfor akkurat denne paragrafen er et
          rettslig grunnlag for handlingen.

        Returner en tom array [] hvis INGEN av kandidatene tydelig er et rettslig grunnlag — ikke tving
        frem et svar, en tom liste er et fullt gyldig og nyttig svar. Det er også fullt gyldig å returnere
        FLERE paragrafer (f.eks. hvis fritekst-henvisningen faktisk nevner flere reelle hjemler side om
        side, ikke bare én pluss en kryssreferanse).
        """;

    private sealed record RegelverksreferanseForslagJson(string Eid, string? Begrunnelse);

    /// <summary>
    /// [Ny, issue #290, tiltak KI-fallback] Utvidet, permissiv variant av
    /// <see cref="OppgaveregisterHandlingSeed"/>s <c>TrekkUtEnkeltParagraf</c> — trekker ut ALLE
    /// tallkandidater (rent tall, evt. med én bindestrek-del, f.eks. "21-4") som opptrer PÅ ELLER ETTER
    /// den FØRSTE "§" i strengen. "På eller etter" (ikke "rett etter et eget §") er bevisst: en
    /// komma-/"og"-liste som "§§ 21-4, 22-3" har bare ÉN "§" foran HELE listen, ikke ett foran hvert tall
    /// — og en "jf."-kryssreferanse som "§ 5, jf. § 3" introduserer et NYTT tall via sin egen, senere "§".
    /// Begge formene fanges av samme enkle regel. Kapittel-/romertall-former uten NOEN "§" i det hele
    /// tatt gir tom liste (samme grense som seedens egen enkle ekstraksjon, kun utvidet til FLERE
    /// paragrafer per henvisning — ikke til nye henvisningsformer). Returnerer paragrafnumrene i
    /// "§"-prefikset form (<c>LovdataIdentifikatorer.ParagrafEid</c> sin forventede input), IKKE bekreftet
    /// mot noen node her — det er kallerens ansvar, samme todeling som seedens egen
    /// TrekkUtEnkeltParagraf/paragrafEidSett-bekreftelse.
    /// </summary>
    [GeneratedRegex(@"\d+(?:-\d+)?")]
    private static partial Regex TallMønster();

    internal static List<string> TrekkUtFlereParagrafnumre(string henvisning)
    {
        var forsteParagraftegn = henvisning.IndexOf('§');
        if (forsteParagraftegn < 0) return []; // Kapittel/romertall — utenfor rekkevidde, se klassekommentaren.

        var restenAvStrengen = henvisning[forsteParagraftegn..];
        return TallMønster().Matches(restenAvStrengen).Select(m => "§" + m.Value).Distinct().ToList();
    }

    /// <summary>
    /// Kjører oppgraderingsforslag for <paramref name="virksomhetId"/> sine handlinger med en
    /// dokumentnivå-regelverksreferanse (mot en av <paramref name="rettskildeIder"/>) som ennå ikke er
    /// bekreftet til paragrafnivå. Ett KI-kall PER kandidatrad — se klassekommentaren for hvorfor
    /// kandidatsettet allerede er lite og deterministisk (ingen embedding-infrastruktur trengs her).
    /// Idempotent — samme (handling, rettskilde, foreslått eId) foreslås ikke to ganger (unik indeks
    /// <c>ux_handling_regelverksreferanse_forslag_par</c>), og en rad som allerede er oppgradert til
    /// paragrafnivå (av et tidligere godkjent forslag, eller en ny seed-kjøring) faller automatisk ut av
    /// kandidatsettet (den er ikke lenger på dokumentnivå) — trygt å kjøre på nytt.
    /// </summary>
    public async Task<HandlingRegelverksreferanseforslagResultat> KjorForslagAsync(
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
        var eliPerRettskilde = rettskilder.ToDictionary(r => r.Id, r => r.Eli);

        // Kandidat-innsnevring, steg 1: Handling-nivå referanser mot en av de VALGTE rettskildene, som
        // fortsatt STÅR PÅ DOKUMENTNIVÅ (TilEid == rettskildens egen Eli — ingen paragrafnivå-node
        // bekreftet ennå av verken seeden eller et tidligere godkjent forslag) og faktisk HAR en
        // fritekst-henvisning å tolke (uten fritekst er det ingenting for KI-en å jobbe med). Scopet til
        // EGEN virksomhet — en Handling arves fra sin eiende Tjenestes VirksomhetId, samme isolasjons-
        // prinsipp som TjenesteRegelverksreferanseforslagTjeneste.
        var handlingIderForVirksomhet = db.Tjenester
            .Where(t => t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende")
            .Join(db.Handlinger.Where(h => h.Entitetsstatus == "gjeldende"), t => t.Id, h => h.TjenesteId, (t, h) => h.Id);

        var alleKandidatrader = await db.HandlingRegelverksreferanser
            .Where(r => rettskildeIder.Contains(r.TilRettskildeId) && r.KildeHenvisningFritekst != null)
            .Where(r => handlingIderForVirksomhet.Contains(r.HandlingId))
            .ToListAsync(ct);
        var kandidatrader = alleKandidatrader.Where(r => r.TilEid == eliPerRettskilde[r.TilRettskildeId]).ToList();

        if (kandidatrader.Count == 0)
        {
            return new HandlingRegelverksreferanseforslagResultat(0, 0, 0, null, null);
        }

        var handlingIder = kandidatrader.Select(r => r.HandlingId).Distinct().ToList();
        var handlinger = await db.Handlinger.Where(h => handlingIder.Contains(h.Id)).ToDictionaryAsync(h => h.Id, ct);

        // ALLE noder (ikke kun "paragraf"-typen, og UTEN Tekst-filter) for de INVOLVERTE rettskildene —
        // se <see cref="RettskildeNodeEntitet.Tekst"/> sin egen doc-kommentar: "kun ledd/punkt
        // (bladtekst)". Et "paragraf"-nivå-node er en ren STRUKTURELL header UTEN egen tekst (samme
        // grunn til at OppgaveregisterHandlingSeed sin paragrafEidSett-bekreftelse ALDRI filtrerer på
        // Tekst — den bekrefter kun at paragrafen FINNES, viser aldri innholdet). For at KI-en skal ha
        // noe å faktisk LESE må paragrafens tekst SAMLES fra dens ledd/punkt-etterkommere (se
        // SamleParagrafTekst under) — bekreftelsen (finnes paragrafen) og lesbarheten (hva står i den)
        // er to forskjellige spørsmål, løst med to forskjellige oppslag over samme nodesett.
        var involverteRettskilder = kandidatrader.Select(r => r.TilRettskildeId).Distinct().ToList();
        var alleNoder = await db.RettskildeNoder
            .Where(n => involverteRettskilder.Contains(n.RettskildeId))
            .ToListAsync(ct);
        var paragrafNoderPerRettskilde = alleNoder.Where(n => n.NodeType == "paragraf")
            .GroupBy(n => n.RettskildeId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(n => n.Eid, n => n));
        var barnPerForelder = alleNoder.Where(n => n.ParentNodeId is not null)
            .GroupBy(n => n.ParentNodeId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Sorteringsrekkefolge).ToList());

        var antallVurdert = 0;
        var antallNyeForslag = 0;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;

        foreach (var rad in kandidatrader)
        {
            var fritekst = rad.KildeHenvisningFritekst!;
            var paragrafnumre = TrekkUtFlereParagrafnumre(fritekst);
            if (paragrafnumre.Count == 0) continue; // ingen "§"-form i teksten — kapittel/romertall, utenfor rekkevidde.

            var eli = eliPerRettskilde[rad.TilRettskildeId];
            var noderForRettskilde = paragrafNoderPerRettskilde.GetValueOrDefault(rad.TilRettskildeId);
            var kandidater = new List<ParagrafKandidat>();
            foreach (var paragrafnummer in paragrafnumre)
            {
                var kandidatEid = LovdataIdentifikatorer.ParagrafEid(eli, paragrafnummer);
                if (noderForRettskilde is not null && noderForRettskilde.TryGetValue(kandidatEid, out var node))
                {
                    var tekst = SamleParagrafTekst(node, barnPerForelder);
                    if (!string.IsNullOrWhiteSpace(tekst)) kandidater.Add(new ParagrafKandidat(node.Eid, tekst));
                    // Paragraf uten noen tekstbærende etterkommer (f.eks. opphevet, kun overskrift) —
                    // ingenting for KI-en å vurdere relevans mot, samme "ingen kandidat" som et
                    // ubekreftet paragrafnummer. Bevisst IKKE inkludert med tom tekst.
                }
            }
            if (kandidater.Count == 0) continue; // ingen av de nevnte paragrafnumrene finnes som ekte, lesbar node her.
            if (!handlinger.TryGetValue(rad.HandlingId, out var handling)) continue; // forsvarslag, bør ikke skje.

            antallVurdert++;
            var kontekst = ByggKontekstTekst(handling, fritekst, kandidater);

            KiSvar svar;
            List<RegelverksreferanseForslagJson>? forslag;
            try
            {
                (svar, forslag) = await KiForslagRetryHjelper.KjorMedEttRetryVedTomtSvarAsync<RegelverksreferanseForslagJson>(
                    kallCt => kiKlient.GenererAsync(SystemInstruks, kontekst, kallCt),
                    json => JsonSerializer.Deserialize<List<RegelverksreferanseForslagJson>>(
                        JsonSvarHjelper.StrimleKodeblokk(json), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
                    _logger, "Foreslå handling-regelverksreferanse", ct);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "KI-klienten returnerte ugyldig JSON for handling-regelverksreferanseforslag på handling {HandlingId} — hopper over.",
                    rad.HandlingId);
                continue;
            }
            totalInputTokens += svar.InputTokens ?? 0;
            totalOutputTokens += svar.OutputTokens ?? 0;
            if (forslag is null || forslag.Count == 0) continue;

            var kandidatEider = kandidater.ToDictionary(n => n.Eid, n => n);
            var kildeReferanserJson = JsonSerializer.Serialize(new
            {
                rad.TilRettskildeId,
                fritekst,
                kandidatEider = kandidater.Select(n => n.Eid),
            });
            foreach (var f in forslag)
            {
                if (string.IsNullOrEmpty(f.Eid) || !kandidatEider.ContainsKey(f.Eid)) continue;
                var (nyRad, _) = await OpprettEllerFinnForslagAsync(
                    rad.HandlingId, rad.TilRettskildeId, f.Eid, f.Begrunnelse, opprettetAv, kildeReferanserJson, ct);
                if (nyRad) antallNyeForslag++;
            }
        }

        return new HandlingRegelverksreferanseforslagResultat(
            kandidatrader.Count, antallVurdert, antallNyeForslag,
            totalInputTokens == 0 ? null : totalInputTokens, totalOutputTokens == 0 ? null : totalOutputTokens);
    }

    private static string ByggKontekstTekst(HandlingEntitet handling, string fritekst, List<ParagrafKandidat> kandidater)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Handling");
        sb.AppendLine($"Navn: {handling.Navn}");
        if (!string.IsNullOrWhiteSpace(handling.Merknad)) sb.AppendLine($"Merknad: {handling.Merknad}");
        sb.AppendLine($"Opprinnelig fritekst-henvisning (Oppgaveregisteret): {fritekst}");
        sb.AppendLine();
        sb.AppendLine("# Bekreftede kandidatparagrafer");
        foreach (var kandidat in kandidater)
        {
            sb.AppendLine($"[{kandidat.Eid}] {kandidat.Tekst}");
        }
        return sb.ToString();
    }

    /// <summary>Én bekreftet, LESBAR kandidatparagraf — se KjorForslagAsync sin kommentar for hvorfor
    /// <see cref="Tekst"/> må SAMLES fra ledd/punkt-etterkommere i stedet for lest direkte av selve
    /// paragraf-noden.</summary>
    private sealed record ParagrafKandidat(string Eid, string Tekst);

    /// <summary>
    /// Samler all bladtekst (<see cref="RettskildeNodeEntitet.Tekst"/>, kun satt på ledd/punkt-noder)
    /// under EN paragraf-node, i dokumentets egen LESEREKKEFØLGE — en PRE-ORDER traversering av
    /// <paramref name="barnPerForelder"/> (hvert barns egne barn samles FØR neste søsken, ikke
    /// bredde-først, ellers ville et ledds punkt-underliste havnet EI'ER alle paragrafens ledd i
    /// stedet for rett under sitt eget ledd) — paragraf-noden SELV har aldri egen tekst (kun ledd/punkt
    /// har, se <see cref="RettskildeNodeEntitet.Tekst"/> sin doc-kommentar). Returnerer tom streng
    /// (ikke null) for en paragraf uten noen tekstbærende etterkommer i det hele tatt — kalleren
    /// behandler det som "ingen kandidat".
    /// </summary>
    private static string SamleParagrafTekst(RettskildeNodeEntitet paragraf, Dictionary<Guid, List<RettskildeNodeEntitet>> barnPerForelder)
    {
        var deler = new List<string>();
        void SamleRekursivt(Guid forelderId)
        {
            foreach (var node in barnPerForelder.GetValueOrDefault(forelderId) ?? [])
            {
                if (!string.IsNullOrWhiteSpace(node.Tekst)) deler.Add(node.Tekst);
                SamleRekursivt(node.Id);
            }
        }
        SamleRekursivt(paragraf.Id);
        return string.Join(" ", deler);
    }

    /// <summary>Idempotent — se <see cref="HandlingRegelverksreferanseForslagEntitet"/> sin unik-indeks.
    /// Samme racy-sveip-vern som <see cref="TjenesteRegelverksreferanseforslagTjeneste.OpprettEllerFinnForslagAsync"/>.</summary>
    private async Task<(bool NyRad, HandlingRegelverksreferanseForslagEntitet Rad)> OpprettEllerFinnForslagAsync(
        Guid handlingId, Guid tilRettskildeId, string tilEid, string? begrunnelse, string opprettetAv,
        string kildeReferanserJson, CancellationToken ct)
    {
        var eksisterende = await db.HandlingRegelverksreferanseForslag.FirstOrDefaultAsync(
            f => f.HandlingId == handlingId && f.TilRettskildeId == tilRettskildeId && f.TilEid == tilEid, ct);
        if (eksisterende is not null) return (false, eksisterende);

        var rad = new HandlingRegelverksreferanseForslagEntitet
        {
            Id = Guid.NewGuid(),
            HandlingId = handlingId,
            TilRettskildeId = tilRettskildeId,
            TilEid = tilEid,
            Begrunnelse = begrunnelse,
            Status = "Venter",
            AiForslagVersjon = AiForslagVersjon,
            KildeReferanserJson = kildeReferanserJson,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.HandlingRegelverksreferanseForslag.Add(rad);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(rad).State = EntityState.Detached;
            var vantLopet = await db.HandlingRegelverksreferanseForslag.FirstOrDefaultAsync(
                f => f.HandlingId == handlingId && f.TilRettskildeId == tilRettskildeId && f.TilEid == tilEid, ct);
            if (vantLopet is not null) return (false, vantLopet);
            throw;
        }
        return (true, rad);
    }

    /// <summary>Scopet til ÉN virksomhet (via handlingens eiende tjeneste) — samme isolasjonsprinsipp som
    /// <see cref="TjenesteRegelverksreferanseforslagTjeneste.ListerAsync"/>. <paramref name="status"/> =
    /// <c>null</c> betyr ALLE statuser; default <c>"Venter"</c> (arbeidskøen).</summary>
    public Task<List<HandlingRegelverksreferanseForslagEntitet>> ListerAsync(
        Guid virksomhetId, string? status = "Venter", CancellationToken ct = default)
    {
        var handlingIderForVirksomhet = db.Tjenester
            .Where(t => t.VirksomhetId == virksomhetId)
            .Join(db.Handlinger, t => t.Id, h => h.TjenesteId, (t, h) => h.Id);
        var sporring = db.HandlingRegelverksreferanseForslag.Where(f => handlingIderForVirksomhet.Contains(f.HandlingId));
        if (status is not null) sporring = sporring.Where(f => f.Status == status);
        return sporring.OrderByDescending(f => f.OpprettetTidspunkt).ToListAsync(ct);
    }

    /// <summary>Samme "join inn det underliggende, klienten trenger ikke ett kall per rad"-mønster som
    /// <see cref="TjenesteRegelverksreferanseforslagTjeneste.ListerMedTjenesteAsync"/> — join'er inn
    /// handlingens Navn og eiende tjenestes Tittel.</summary>
    public async Task<List<(HandlingRegelverksreferanseForslagEntitet Forslag, HandlingEntitet Handling, string TjenesteTittel)>> ListerMedHandlingAsync(
        Guid virksomhetId, string? status = "Venter", CancellationToken ct = default)
    {
        var forslag = await ListerAsync(virksomhetId, status, ct);
        var handlingIder = forslag.Select(f => f.HandlingId).Distinct().ToList();
        var handlinger = await db.Handlinger.Where(h => handlingIder.Contains(h.Id)).ToDictionaryAsync(h => h.Id, ct);
        var tjenesteIder = handlinger.Values.Select(h => h.TjenesteId).Distinct().ToList();
        var tjenesteTitler = await db.Tjenester.Where(t => tjenesteIder.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Tittel, ct);
        // Samme "forsvunnet-referanse droppes stille" holdning som TjenesteRegelverksreferanseforslagTjeneste.
        return forslag.Where(f => handlinger.ContainsKey(f.HandlingId))
            .Select(f => (f, handlinger[f.HandlingId], tjenesteTitler.GetValueOrDefault(handlinger[f.HandlingId].TjenesteId, "")))
            .ToList();
    }

    /// <summary>
    /// Bekrefter forslaget: OPPGRADERER den eksisterende <see cref="HandlingRegelverksreferanseEntitet"/>
    /// via <see cref="HandlingregisterTjeneste.OppgraderRegelverksreferanseTilParagrafAsync"/> — denne
    /// tjenesten dupliserer ALDRI den oppgraderingslogikken.
    /// </summary>
    public async Task<HandlingRegelverksreferanseForslagEntitet?> GodkjennAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var forslag = await db.HandlingRegelverksreferanseForslag.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (forslag is null) return null;
        if (forslag.Status != "Venter")
        {
            throw new ArgumentException($"Forslaget har status '{forslag.Status}' — kan kun godkjenne forslag med status 'Venter'.");
        }
        try
        {
            await handlingregister.OppgraderRegelverksreferanseTilParagrafAsync(forslag.HandlingId, forslag.TilRettskildeId, forslag.TilEid, ct);
        }
        catch (ArgumentException)
        {
            // Allerede oppgradert/løst i mellomtiden (samme paragraf, et annet godkjent forslag, eller en
            // ny seed-kjøring) — ikke en feil for GODKJENNINGEN av DETTE forslaget selv, se
            // OppgraderRegelverksreferanseTilParagrafAsync sin doc-kommentar for de to tilfellene.
        }
        forslag.Status = "Godkjent";
        forslag.BehandletAv = behandletAv;
        forslag.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return forslag;
    }

    public async Task<HandlingRegelverksreferanseForslagEntitet?> AvvisAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var forslag = await db.HandlingRegelverksreferanseForslag.FirstOrDefaultAsync(f => f.Id == id, ct);
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

/// <summary>Sammendraget POST /api/tjenester/handlinger/regelverksreferanse-forslag/kjor svarer med —
/// <see cref="AntallKandidatrader"/> er hele omfanget (dokumentnivå + fritekst, ikke ennå paragrafnivå),
/// <see cref="AntallVurdert"/> hvor mange av dem som faktisk fikk minst én bekreftet kandidatparagraf (og
/// dermed et KI-kall), <see cref="AntallNyeForslag"/> hvor mange NYE kø-rader dette konkrete kjøret la
/// til (idempotent — kan være 0 ved et gjentatt kjør).</summary>
public sealed record HandlingRegelverksreferanseforslagResultat(
    int AntallKandidatrader, int AntallVurdert, int AntallNyeForslag, int? InputTokens, int? OutputTokens);
