using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Oppdagelsesmekanismen (docs/13-backlog.md §9) — ren tekstanalyse (regex), ALDRI KI/LLM (eksplisitt
/// instruks fra Johann, ikke bare en foretrukket løsning). Komplementær til
/// <see cref="VirksomhetKandidatSveipTjeneste"/>, som er en BEKREFTELSES-mekanisme (krever en allerede
/// kjent navneform-<see cref="BegrepEntitet"/>-rad og leter etter FLERE forekomster av DEN kjente
/// strengen). Denne klassen foreslår derimot HELT NYE kandidatnavn — mønstre, ikke kjente strenger —
/// til <see cref="NavnekandidatEntitet"/>-køen (se den klassens kommentar for hvorfor en egen tabell).
/// <para>
/// Én klasse dekker både selve sveipet og selve køen (motsatt av virksomhet-kandidat-parets to klasser)
/// — denne køen har ingen separat gjenbruker av bare "opprett/lister"-delen (VirksomhetKandidatTjeneste
/// brukes bl.a. direkte av godkjennings-flyten i TekstTaggTjeneste-sammenheng), så en splitt ga ikke
/// samme gevinst her.
/// </para>
/// <para>
/// <b>Mønstre (docs/13-backlog.md §9, Johanns liste, ikke uttømmende):</b>
/// </para>
/// <list type="number">
/// <item>Suffiksmønster + STOR forbokstav MIDT i en setning → <c>"virksomhet"</c> (ekte egennavn, f.eks.
/// "Miljødirektoratet", "Datatilsynet"). "Midt i en setning" — ikke bare fordi ordet står først i en
/// setning, se <see cref="ErSetningsstart"/> — er den avgjørende presisjonssiden: uten dette filteret
/// ville et vanlig substantiv som tilfeldigvis er stort fordi det åpner en setning (f.eks.
/// "Departementet kan …" i begynnelsen av et ledd) gitt et falskt "virksomhet"-treff.</item>
/// <item>Suffiksmønster + LITEN forbokstav → <c>"gruppe"</c> (beskrivelse av en funksjon, f.eks.
/// "forurensningsmyndighetene", ikke et egennavn) — posisjon i setningen er irrelevant her, siden
/// liten forbokstav i seg selv allerede utelukker et egennavn.</item>
/// <item>Fast liste juridisk-aktør-substantiv UTEN suffiks ("Kongen", "Kongen i statsråd", "Stortinget",
/// "Regjeringen", samt BØYNINGSFORMENE av "statsforvalter"/"kommune"/"fylkeskommune"/"departement" —
/// se <see cref="FasteRollesubstantiv"/>) → ALLTID <c>"gruppe"</c>, uansett store/små bokstaver — disse
/// er generiske rollesubstantiv, ikke navn på én bestemt institusjon, og posisjon i setningen endrer
/// ikke det.</item>
/// <item><b>[Ny, kodegjennomgang 2026-08-30]</b> Flerords-mønster: inntil 3 STOR-forbokstav-ord (ev.
/// med bindeordet "og" MELLOM to av dem, f.eks. "Møre og Romsdal"), UMIDDELBART
/// etterfulgt av ett kjent institusjonsord i UBESTEMT FORM som eget, mellomromsdelt ord (se
/// <see cref="Institusjonsord"/>, f.eks. "fylkeskommune", "kommune") → <c>"virksomhet"</c>. Fanger
/// egennavn+institusjonsord-par som verken suffiksmønsteret (institusjonsordet er IKKE smeltet sammen
/// med egennavnet, det er et eget ord) eller den faste gruppelisten (som kun matcher institusjonsordet
/// ALENE, uten et navn foran) dekker — bekreftet i live data, FOR-2019-09-30-1310 §2 andre ledd:
/// "Østfold fylkeskommune: Driftsområde Ytre Oslofjord Øst", "Møre og Romsdal fylkeskommune: …", osv.
/// Se <see cref="FinnEgennavnForanInstitusjonsord"/> og <see cref="ErFlerordsKontekstTillatt"/> for
/// presisjonsvernet (krever stor forbokstav rett før institusjonsordet — ellers hverken "en
/// fylkeskommune" eller "et statlig tilsyn" ville vært trygt).</item>
/// </list>
/// <para>
/// <b>[Ny, kodegjennomgang 2026-08-30] Normalisering før lagring — KUN <c>"gruppe"</c>:</b> for
/// <c>"gruppe"</c>-treff er selve store/små bokstaver-formen IKKE del av identiteten (en gruppe er per
/// definisjon ikke et egennavn — "statsforvalteren" og "Statsforvalteren" er samme gruppe, kun ulik
/// forbokstav fordi den ene tilfeldigvis sto ved en setningsstart). Bekreftet i live data: 68
/// forekomster av "statsforvalteren" og 45 av "Statsforvalteren" ga tidligere separate kandidater, ren
/// posisjonell idempotens fanget aldri opp at det var samme term. Løsning: <see cref="SveipAsync"/>
/// folder <c>"gruppe"</c>-treffets tekst til små bokstaver (<see cref="string.ToLowerInvariant"/>) FØR
/// den brukes som dedup-nøkkel og FØR den lagres som <see cref="NavnekandidatEntitet.ForeslattTekst"/>.
/// <c>"virksomhet"</c>-treff (inkl. flerords-mønsteret over) er IKKE del av denne normaliseringen —
/// der ER store/små bokstaver et reelt signal (et egennavn skal beholde sin faktiske stavemåte), så
/// disse beholder rå tekst uendret.
/// </para>
/// <para>
/// <b>[Ny, kodegjennomgang 2026-08-30] Term-basert dedup, i tillegg til posisjonell (KUN <c>"gruppe"</c>):</b>
/// idempotens var tidligere REN posisjon (<c>RettskildeId</c>, <c>NodeEid</c>, <c>StartOffset</c>) — to
/// ulike posisjoner med (etter normalisering) SAMME tekst i SAMME rettskilde ga tidligere to separate
/// <see cref="NavnekandidatEntitet"/>-rader (nettopp "statsforvalteren"/"Statsforvalteren"-caset over).
/// Dette er en reell arkitekturendring, ikke bare en bugfiks: <see cref="SveipAsync"/> sjekker nå, FØR
/// <see cref="OpprettEllerFinnAsync"/> kalles, om det ALLEREDE finnes en <c>"gruppe"</c>-kandidatrad
/// (uansett status — Venter/Godkjent/Avvist — og uansett tekstposisjon) med samme normaliserte tekst
/// for samme <c>RettskildeId</c> — samme prinsipp som den eksisterende "alleredeDekket mot godkjent
/// Begrep"-filtreringen under, nå utvidet til Å OGSÅ dekke ikke-godkjente kandidater. Uten dette ville
/// normaliseringen over kun forhindret NYE duplikater fra ETT sveip (samme treff, samme kjøring), ikke
/// duplikater på TVERS av sveip/posisjoner — som var selve det bekreftede problemet. Kun <c>"gruppe"</c>,
/// av samme grunn som normaliseringen over er scopet dit (<c>"virksomhet"</c> har ingen normalisert
/// term å slå opp mot — case er signal, ikke støy — der gjelder fortsatt ren posisjonell idempotens).
/// </para>
/// <para>
/// <b>Kjøres KUN mot allerede importerte rettskilde-noder</b> — samme datakilde som
/// <see cref="VirksomhetKandidatSveipTjeneste"/> (<c>Entitetsstatus == "gjeldende" &amp;&amp; !Opphevet</c>),
/// IKKE en ny, live skraping av Lovdata. Dekningen er derfor begrenset til det som faktisk ER importert
/// — en reell begrensning, ikke noe denne klassen later som er komplett.
/// </para>
/// <para>
/// <b>[Rettet, kodegjennomgang 2026-08-30]</b> Sveipet er scopet til KUN delt/nasjonal, gjeldende
/// rettskilde (<c>Rettskilde.VirksomhetId == null &amp;&amp; Entitetsstatus == "gjeldende"</c>) — en
/// tidligere versjon søkte uskjermet gjennom ALLE virksomheters rettskilder (inkl. private/lokale) og
/// eksponerte tekstutdrag derfra til enhver bruker, samme klasse kryssvirksomhet-lekkasje som allerede
/// ble funnet og fikset én gang i <see cref="VirksomhetKandidatSveipTjeneste"/> (Agder/Bergen,
/// 2026-08-22). Dette er ikke en per-virksomhet-scoping (ingen <c>virksomhetId</c>-parameter finnes her,
/// med vilje — «oppdag nye navn i lovkorpuset» er et generelt, ikke brukerspesifikt søk), men en
/// synlighetsgrense: kun det som uansett er delt/nasjonalt innhold sveipes. <paramref name="rettskildeId"/>
/// i <see cref="SveipAsync"/> er en valgfri innsnevring til ÉN slik delt rettskilde — å oppgi en
/// virksomhets private rettskilde der kaster nå en tydelig feil i stedet for å sveipe den.
/// </para>
/// <para>
/// <b>Filtrering av allerede DEKKEDE treff</b> (docs/13-backlog.md §9): et treff som samsvarer
/// case-insensitivt med <see cref="BegrepEntitet.Term"/> til en eksisterende, gjeldende
/// <see cref="BegrepEntitet"/>-rad skal IKKE gi en ny kandidat — poenget er å oppdage NYE navn, ikke
/// duplisere det <see cref="VirksomhetKandidatSveipTjeneste"/> allerede finner/kan finne. Scopet ulikt
/// per kategori, siden identiteten er ulik (docs/20 §2.3 vs. §2.4): et <c>"virksomhet"</c>-treff sjekkes
/// mot ALLE eksisterende virksomhet-navneformer (globalt delt, uansett rettskilde) — et <c>"gruppe"</c>-treff
/// sjekkes kun mot gruppebegrep for NØYAKTIG DENNE rettskilden (gruppebegrepets identitet er
/// <c>(Term, LovkildeId)</c> sammen, samme gruppenavn i en annen lov er en annen rad og dekker ikke dette
/// treffet).
/// </para>
/// </summary>
public sealed class NavnekandidatOppdagelseTjeneste(
    RegelIdeDbContext db, VirksomhetsbegrepTjeneste virksomhetsbegrep,
    TekstTaggTjeneste tekstTaggTjeneste, VirksomhetOppslagTjeneste virksomhetOppslag,
    EksternNavneoppslagTjeneste eksternOppslag)
{
    /// <summary>Diskriminatorverdien skrevet til <see cref="NavnekandidatEntitet.OppdagelsesKilde"/> for
    /// alle kandidater produsert av <see cref="SveipStorBokstavAsync"/> (docs/31) — se den entitetsfeltets
    /// kommentar. Public (ikke internal): brukt av RegelIde.Api ved berikelse-oppslag på lesetidspunktet
    /// (se NavnekandidatDto/Program.cs).</summary>
    public const string StorBokstavOppdagelsesKilde = "stor-bokstav-snl-ssr";
    /// <summary>Suffiksene fra Johanns liste (docs/13-backlog.md §9) — sortert lengst-først i den
    /// sammensatte alternasjonen (samme "unngå kortere delvis treff av en lengre streng"-prinsipp som
    /// <see cref="VirksomhetKandidatSveipTjeneste"/>), selv om ingen av dagens suffikser er substrenger
    /// av hverandre — defensivt, ikke bevist nødvendig for akkurat denne listen.
    /// <para>
    /// [Rettet, kodegjennomgang 2026-08-30] "departementet" fjernet herfra — det ER selve suffikset
    /// (et sammensatt ord MED dette suffikset, f.eks. et fiktivt "xdepartementet", er ikke reelt norsk),
    /// og hørte kun hjemme i <see cref="FasteRollesubstantiv"/>. Sto tidligere i BEGGE listene, som
    /// motsa en eksisterende tests egen kommentar («'departementet' står IKKE i suffikslisten») — testen
    /// besto likevel, ved en tilfeldighet (dette mønsteret er case-sensitivt, literalen er små bokstaver,
    /// så stor forbokstav midt i en setning traff aldri suffiks-grenen) — ikke ved design.
    /// </para></summary>
    private static readonly string[] Suffikser =
    [
        "tilsynet", "direktoratet", "nemnda", "nemnden",
        "domstolen", "ombudet", "verket", "etaten", "banken",
    ];

    /// <summary>
    /// [Rettet, kodegjennomgang 2026-08-30] Eksplisitt denyliste for "verket"-suffikset — bekreftet i
    /// live data: "fiskeriregelverket" (stor forbokstav midt i setning) ble foreslått som en
    /// <c>"virksomhet"</c>-kandidat, men er åpenbart ikke et egennavn. "verket" fanger ekte
    /// institusjonsnavn ("Patentverket", "Sjøfartsverket", "Kartverket", "Kystverket" — se
    /// <c>organisasjoner-norge.json</c>), men fanger UNNGÅELIG også helt vanlige norske PRODUKTIVE
    /// sammensetninger av formen «(hvilket som helst substantiv +) regelverket/lovverket/avtaleverket/
    /// rammeverket for noe» — ikke en institusjon, og ikke en lukket liste med egennavn å legge TIL
    /// suffikslisten (hvilket som helst substantiv foran gir gyldig norsk, så det finnes ingen endelig
    /// "uttømt" liste av sammensetninger å forby der).
    /// <para>
    /// Løsningen er derfor IKKE å fjerne "verket" fra <see cref="Suffikser"/> (det ville også miste de
    /// ekte "Patentverket"/"Sjøfartsverket"/"Kartverket"-treffene), men en egen, eksplisitt, dokumentert
    /// denyliste (samme "ingen gjettet fallback"-filosofi som resten av klassen) over de KJENTE
    /// falske positivene — sjekket ved <c>EndsWith</c> (case-insensitivt), ikke eksakt likhet, nettopp
    /// fordi disse er PRODUKTIVE sammensetninger: "fiskeriregelverket" må fanges av "regelverket" selv
    /// om selve ordet aldri er nøyaktig "regelverket". Sveip av hele det lokale korpuset (docs/13-
    /// backlog.md §9-tekster, seed-data, dokumentasjon) etter alle "*verket"-forekomster fant ingen
    /// flere kandidater utover disse fire (Johanns egen liste) — "Kartverket"/"Kystverket" (ekte
    /// institusjoner) og "Skatteverket" (kun nevnt som svensk sammenligning i docs/10, ikke en norsk
    /// rettskildetekst) endte IKKE med noen av de fire ordene under, så de forblir upåvirket.
    /// Gjelder KUN <c>"virksomhet"</c>-klassifiseringen (stor forbokstav midt i setning) — en
    /// tilsvarende liten-forbokstav-forekomst gir uansett <c>"gruppe"</c>, ikke <c>"virksomhet"</c>, og
    /// var aldri det bekreftede problemet.
    /// </para>
    /// </summary>
    private static readonly string[] VerketDenyliste =
    [
        "regelverket", "lovverket", "avtaleverket", "rammeverket",
    ];

    /// <summary>Faste juridisk-aktør-substantiv UTEN suffiks (docs/13-backlog.md §9) — ALLTID
    /// <c>"gruppe"</c>-kandidater, uansett store/små bokstaver. Lengst-først i alternasjonen, slik at
    /// "Kongen i statsråd" foretrekkes framfor et delvis treff på bare "Kongen".
    /// <para>
    /// <b>[Ny, kodegjennomgang 2026-08-30]</b> "statsforvalter"/"kommune"/"fylkeskommune"/"departement"
    /// er utvidet fra KUN bestemt entall (den opprinnelige, eneste dekkede formen) til ALLE fire
    /// bøyningsformer via <see cref="Bøyningsformer"/> — ubestemt entall, bestemt entall, ubestemt
    /// flertall, bestemt flertall. Bekreftet i live data: "kommuneloven" har 71 forekomster av
    /// "kommuner" og 71 av "fylkeskommuner" (ubestemt flertall) som IKKE ble fanget i det hele tatt før
    /// denne utvidelsen. Bevisst en LUKKET liste over kjente stammer + et lite, begrenset sett
    /// bøyningsendelser — IKKE en generell lemmatizer/språkmodell (eksplisitt forbudt, ren regex).
    /// </para></summary>
    private static readonly string[] FasteRollesubstantiv =
    [
        "Kongen i statsråd", "Kongen", "Stortinget", "Regjeringen",
        .. Bøyningsformer("statsforvalter", "en", "e", "ne"), // statsforvalter/-en/-e/-ne
        .. Bøyningsformer("kommune", "n", "r", "ne"), // kommune/-n/-r/-ne
        .. Bøyningsformer("fylkeskommune", "n", "r", "ne"), // fylkeskommune/-n/-r/-ne
        .. Bøyningsformer("departement", "et", "er", "ene"), // departement/-et/-er/-ene
    ];

    /// <summary>Bygger de fire bøyningsformene (ubestemt entall = stammen selv, + de tre oppgitte
    /// endelsene i rekkefølgen bestemt entall/ubestemt flertall/bestemt flertall) av én kjent stamme —
    /// se <see cref="FasteRollesubstantiv"/>s kommentar for hvorfor dette er en lukket liste, ikke en
    /// generell bøyningsregel.</summary>
    private static string[] Bøyningsformer(string stamme, string bestemtEntallEndelse, string ubestemtFlertallEndelse, string bestemtFlertallEndelse) =>
        [stamme, stamme + bestemtEntallEndelse, stamme + ubestemtFlertallEndelse, stamme + bestemtFlertallEndelse];

    private static readonly Regex SuffiksMønster = new(
        @"\b\p{L}[\p{L}]*(?:" + string.Join('|', Suffikser) + @")\b");

    private static readonly Regex FasteGruppeMønster = new(
        @"\b(?:" + string.Join('|', FasteRollesubstantiv.OrderByDescending(s => s.Length).Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// Kjente institusjonsord i UBESTEMT FORM (docs/13-backlog.md §9, Johanns liste — ikke uttømmende)
    /// brukt av flerords-mønsteret (<see cref="FinnEgennavnForanInstitusjonsord"/>) — MÅ stå som eget,
    /// mellomromsdelt ord etter et egennavn (til forskjell fra <see cref="Suffikser"/>, som er smeltet
    /// sammen med stammen). "vegvesen" lagt til utover Johanns opprinnelige liste — "Statens vegvesen"
    /// skrives (til forskjell fra f.eks. "tilsyn"-institusjoner, som alltid er ETT sammensatt ord som
    /// "Datatilsynet") faktisk som to ord i virkelig bruk, og ordet har lav tvetydighetsrisiko alene
    /// (nesten utelukkende brukt om denne ene, spesifikke etaten).
    /// <para>
    /// <b>[Ny, kodegjennomgang 2026-08-30] Skole-relaterte ord</b> — bekreftet i live data (korpusomfattende
    /// sveip + direkte tekstsøk mot den kjørende dev-databasen, se PR-beskrivelsen) at korpuset inneholder
    /// MANGE navngitte fagskoler av nettopp formen "[Egennavn] fagskole" i selve rettskilde-TEKSTEN, ikke
    /// bare i titler — f.eks. "Nortrain fagskole", "Nordland fagskole", "Noroff fagskole", "TISIP fagskole".
    /// Lagt til: "fagskole", "høyskole", "høgskole", "høgskule" (BEGGE bokmål/nynorsk-stavemåter forekommer
    /// i ekte navn, f.eks. "Høgskulen på Vestlandet" — "høgskule" er derfor tatt med selv om den ikke sto i
    /// Johanns opprinnelige liste), "universitet", "barnehage".
    /// </para>
    /// <para>
    /// <b>Bevisst UTELATT: "skole" alene</b> (uten fag-/høy-/høg-prefiks). Samme "verket"-fallgruve som
    /// <see cref="VerketDenyliste"/> ble opprettet for: et korpusomfattende testsveip med "skole" i lista
    /// ga et FLOM av falske positiver av formen "[Adjektiv/Stedsnavn] skole" der "skole" er en helt
    /// generisk fellesbetegnelse, ikke del av et spesifikt egennavn — f.eks. "Denne skole", "Norsk skole",
    /// "Samisk videregående skole" (her ville mønsteret uansett feilaktig fanget bare "Samisk" pga.
    /// mellomrommet i "videregående skole", ikke hele den reelle institusjonsbetegnelsen) — i tillegg til at
    /// "skole" i seg selv (til forskjell fra "fagskole"/"høyskole"/"barnehage"/"universitet", som er
    /// LUKKEDE, spesifikke institusjonstyper) inngår produktivt i sammensetninger med ETHVERT stedsnavn/
    /// skoletype-adjektiv ("kunstskole", "sykepleierskole", "sommerskole", osv.) — akkurat samme "ingen
    /// endelig uttømt denyliste mulig" begrunnelse som <see cref="VerketDenyliste"/>s kommentar. Til
    /// forskjell fra "verket" (der en denyliste FUNGERTE, fordi de produktive sammensetningene var få og
    /// kjente — regelverket/lovverket/avtaleverket/rammeverket) er "skole"-sammensetningene for mange og
    /// åpne til at en tilsvarende denyliste ville vært uttømmende. Løsning: utelatt helt, presisjon foran
    /// recall — de sammensatte institusjonsordene ("fagskole" m.fl.) dekker likevel det STORE flertallet av
    /// de bekreftede, navngitte skolene i korpuset (Nortrain fagskole, Nordland fagskole, osv.).
    /// </para>
    /// </summary>
    private static readonly string[] Institusjonsord =
    [
        "fylkeskommune", "kommune", "direktorat", "tilsyn", "departement", "fylkesmannsembete", "vegvesen",
        "fagskole", "høyskole", "høgskole", "høgskule", "universitet", "barnehage",
    ];

    private static readonly Regex InstitusjonsordMønster = new(
        @"\b(?:" + string.Join('|', Institusjonsord.OrderByDescending(s => s.Length).Select(Regex.Escape)) + @")\b");

    /// <summary>
    /// Det ENESTE bindeordet flerords-mønsteret tillater MELLOM to store-forbokstav-ord (f.eks. "Møre
    /// og Romsdal", "Troms og Finnmark", "Sogn og Fjordane" — alle bekreftet ekte, tidligere/nåværende
    /// fylkesnavn i live-data-sveipet under).
    /// <para>
    /// <b>[Ny, kodegjennomgang 2026-08-30]</b> Opprinnelig vurdert å også inkludere "i" (Johanns
    /// pseudo-eksempel "Sør i Nordland"), men et korpusomfattende testsveip mot den kjørende
    /// dev-databasen avdekket et konkret falskt positiv: "Inntaksnemnda i Finnmark fylkeskommune" —
    /// her er "i" en ekte PREPOSISJON ("Inntaksnemnda i [fylket] Finnmark fylkeskommune"), ikke et
    /// navneinternt bindeord, og fanget dermed feilaktig med et helt urelatert substantiv foran. "og"
    /// viste INGEN tilsvarende feil i samme sveip (kun ekte sammensatte fylkesnavn). Fjernet "i" på
    /// bakgrunn av dette funnet — presisjon foran Johanns opprinnelige eksempel, som viste seg ikke å
    /// holde mål mot ekte data.
    /// </para></summary>
    private static readonly HashSet<string> TillatteBindeord = new(StringComparer.Ordinal) { "og" };

    /// <summary>
    /// [Ny, kodegjennomgang 2026-08-30] Lukket liste over norske determinativer/kvantorer/pronomen som
    /// ALDRI skal telle som et egennavn-ord i flerords-mønsteret, SELV OM de er stor forbokstav (de er
    /// det typisk KUN fordi de tilfeldigvis åpner en node/setning — nøyaktig samme tvetydighet som
    /// begrunner <see cref="ErSetningsstart"/> for suffiksmønsteret). Avdekket av samme korpusomfattende
    /// testsveip som begrunner <see cref="TillatteBindeord"/>-innstrammingen: uten denne lista ga
    /// mønsteret falske positiver som "Enhver fylkeskommune", "En kommune", "Hver kommune",
    /// "Det departement" — generiske funksjonsord, ikke navn. En LUKKET liste (determinativer/pronomen
    /// er en grammatisk lukket ordklasse i norsk, til forskjell fra f.eks. adjektiv) — IKKE et forsøk på
    /// å luke ut ALLE tenkelige falske positiver (et adjektiv som "Statlig tilsyn" ville fortsatt sluppet
    /// gjennom — se <see cref="ErFlerordsKontekstTillatt"/>s kommentar om denne gjenværende, dokumenterte
    /// begrensningen).
    /// </summary>
    private static readonly HashSet<string> AldriEgennavnOrd = new(StringComparer.Ordinal)
    {
        "En", "Et", "Ei", "Den", "Det", "Denne", "Dette", "Disse",
        "Enhver", "Ethvert", "Enkelte", "Hver", "Hvert", "Alle", "Ingen",
        "Flere", "Mange", "Noen", "Andre", "Annen", "Annet",
        "Hvilken", "Hvilket", "Hvilke", "Slik", "Slike", "Samme",
    };

    /// <summary>
    /// [Ny, docs/31-navneform-berikelse-snl-ssr-spesifikasjon.md] Det NYE, brede "stor bokstav midt i
    /// en setning"-mønsteret (§5 punkt 2) — se <see cref="FinnStorBokstavKandidaterITekst"/> for selve
    /// utløseren og <see cref="SveipStorBokstavAsync"/> for klassifiseringskjeden (§2) som skiller
    /// ekte institusjonsnavn fra stedsnavn/personnavn/utenlandske ord via SNL/SSR.
    /// <para>
    /// <b>KUN Title-case ord</b> (stor forbokstav + ETT ELLER FLERE små bokstaver, ALDRI en ordform der
    /// bokstav nummer to også er stor) — utelukker BEVISST forkortelser i store bokstaver ("NKR", "SFO",
    /// "NVE") fra selve rå-utløseren. Disse er en KJENT, allerede dokumentert falsk-positiv-kilde i
    /// denne klassen (se <see cref="FinnEgennavnForanInstitusjonsord"/>s "NKR og fagskole"/"SFO og
    /// skole"-funn) — ingen av dem er reelle egennavn i seg selv, og SNL/SSR-oppslag mot en bar
    /// forkortelse ville uansett aldri gitt et meningsfullt treff. Ren tegnbasert avgrensning, ikke en
    /// forkortelsesordliste.
    /// </para>
    /// </summary>
    private static readonly Regex StorBokstavOrdMønster = new(@"\b\p{Lu}\p{Ll}+\b");

    /// <summary>Flate, case-insensitive oppslagsmengder av <see cref="FasteRollesubstantiv"/>/
    /// <see cref="Institusjonsord"/> — brukt KUN av <see cref="FinnStorBokstavKandidaterITekst"/> til å
    /// UNNGÅ å sende en term de EKSISTERENDE, presise mønstrene allerede dekker med høyere presisjon
    /// videre til et kostbart eksternt SNL/SSR-oppslag (rent en ytelses-/høflighets-optimalisering mot
    /// de eksterne API-ene — begge mønstrene kjører uansett uavhengig av hverandre, se
    /// <see cref="SveipStorBokstavAsync"/>s metodekommentar for hvorfor duplikat-posisjoner likevel er
    /// trygt idempotent på tvers av de to).</summary>
    private static readonly HashSet<string> FasteRollesubstantivOrdSet = new(FasteRollesubstantiv, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> InstitusjonsordSet = new(Institusjonsord, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ren, testbar funksjon uten DB/HTTP-avhengighet — selve rå-utløseren for det nye mønsteret,
    /// separert fra <see cref="SveipStorBokstavAsync"/>s async SNL/SSR-klassifisering av samme grunn som
    /// <see cref="FinnKandidaterITekst"/> er separert fra <see cref="SveipAsync"/>.
    /// <para>
    /// Ekskluderer (i tillegg til <see cref="ErSetningsstart"/>, samme "midt i en setning"-vern som
    /// resten av klassen): <see cref="AldriEgennavnOrd"/> (determinativer/pronomen — grammatisk lukket
    /// klasse, aldri egennavn), <see cref="FasteRollesubstantivOrdSet"/>/<see cref="InstitusjonsordSet"/>
    /// (allerede dekket av presise, eksisterende mønstre — se feltkommentaren), og ord som ender på et
    /// kjent <see cref="Suffikser"/>-suffiks (allerede dekket av <see cref="SuffiksMønster"/> med høyere
    /// presisjon — INGEN grunn til å sende f.eks. "Miljødirektoratet" videre til et eksternt SNL-oppslag
    /// når suffiksmønsteret allerede klassifiserer det som "virksomhet" med kjent, dokumentert presisjon).
    /// </para>
    /// </summary>
    internal static List<(int Start, int Lengde, string RaaTekst)> FinnStorBokstavKandidaterITekst(string tekst)
    {
        var funnet = new List<(int, int, string)>();
        foreach (Match m in StorBokstavOrdMønster.Matches(tekst))
        {
            if (ErSetningsstart(tekst, m.Index)) continue;
            var ord = m.Value;
            if (AldriEgennavnOrd.Contains(ord)) continue;
            if (FasteRollesubstantivOrdSet.Contains(ord)) continue;
            if (InstitusjonsordSet.Contains(ord)) continue;
            if (Suffikser.Any(s => ord.EndsWith(s, StringComparison.OrdinalIgnoreCase))) continue;
            funnet.Add((m.Index, m.Length, ord));
        }
        return funnet;
    }

    /// <summary>Det første hele ordet (bokstaver) som starter etter evt. mellomrom/tab fra
    /// <paramref name="index"/> — brukt av <see cref="SveipStorBokstavAsync"/> til å sjekke om et
    /// <see cref="Institusjonsord"/> står RETT ETTER et SSR-bekreftet stedsnavn (docs/31 §2 punkt 2).
    /// Samme "kun mellomrom/tab, stopp på ALT annet"-restriktivitet som
    /// <see cref="FinnEgennavnForanInstitusjonsord"/>s bakoverskanning (linjeskift/skilletegn teller
    /// IKKE som "rett etter").</summary>
    private static string? NesteOrdEtter(string tekst, int index)
    {
        var i = index;
        while (i < tekst.Length && (tekst[i] == ' ' || tekst[i] == '\t')) i++;
        if (i >= tekst.Length || !char.IsLetter(tekst[i])) return null;
        var start = i;
        while (i < tekst.Length && char.IsLetter(tekst[i])) i++;
        return tekst[start..i];
    }

    /// <summary>Projeksjon brukt av BÅDE <see cref="SveipAsync"/> og <see cref="SveipStorBokstavAsync"/>
    /// (se <see cref="HentSveipbareNoderAsync"/>) — <see cref="Tekst"/> er ALLTID non-null her (filtrert
    /// i spørringen), til forskjell fra <see cref="RettskildeNodeEntitet.Tekst"/> selv.</summary>
    private sealed record SveipbarNode(Guid RettskildeId, string Eid, string Tekst);

    /// <summary>
    /// Delt node-spørring for BEGGE sveipmetodene (<see cref="SveipAsync"/> og
    /// <see cref="SveipStorBokstavAsync"/>) — samme "delt/nasjonal OG gjeldende"-scoping for begge, se de
    /// utfyllende kommentarene som fantes her FØR denne ble faktorert ut (git-historikk, kodegjennomgang
    /// 2026-08-30): (1) KUN <c>VirksomhetId == null</c> rettskilder — unngår kryssvirksomhet-lekkasje
    /// (samme reelt fikset bug som <see cref="VirksomhetKandidatSveipTjeneste"/> hadde, Agder/Bergen
    /// 2026-08-22), (2) KUN <c>Entitetsstatus == "gjeldende"</c> på BÅDE noden og selve
    /// <see cref="RettskildeEntitet"/> (en reimportert lovs gamle rettskilde-rad blir 'erstattet', men
    /// dens noder forblir for alltid 'gjeldende' på nodenivå — uten dette dobbeltfilteret ble kandidater
    /// fra utdaterte rettskilder tidligere opprettet, men kunne ALDRI godkjennes).
    /// </summary>
    private async Task<List<SveipbarNode>> HentSveipbareNoderAsync(Guid? rettskildeId, CancellationToken ct)
    {
        // Krever delt/nasjonal OG gjeldende, se spørringen under — samme to vilkår, kontrollert her på
        // forhånd for en presis feilmelding når EN bestemt rettskilde etterspørres eksplisitt (i stedet
        // for at den bare gir 0 treff stille).
        if (rettskildeId is not null && !await db.Rettskilder.AnyAsync(
                r => r.Id == rettskildeId && r.VirksomhetId == null && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException(
                $"Fant ingen gjeldende, delt/nasjonal rettskilde med id '{rettskildeId}'. Ingen gjettet fallback.");
        }

        return await db.RettskildeNoder
            .Join(db.Rettskilder, n => n.RettskildeId, r => r.Id, (n, r) => new { Node = n, r.VirksomhetId, RettskildeStatus = r.Entitetsstatus })
            .Where(x => x.Node.Tekst != null && !x.Node.Opphevet && x.Node.Entitetsstatus == "gjeldende"
                        && x.VirksomhetId == null && x.RettskildeStatus == "gjeldende"
                        && (rettskildeId == null || x.Node.RettskildeId == rettskildeId))
            .Select(x => new SveipbarNode(x.Node.RettskildeId, x.Node.Eid, x.Node.Tekst!))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Kjører oppdagelsessveipet — enten mot ÉN rettskilde (<paramref name="rettskildeId"/> satt) eller
    /// mot HELE det importerte korpuset (<paramref name="rettskildeId"/> = <c>null</c>).
    /// </summary>
    public async Task<NavnekandidatSveipResultat> SveipAsync(Guid? rettskildeId, string opprettetAv, CancellationToken ct = default)
    {
        var noder = await HentSveipbareNoderAsync(rettskildeId, ct);

        // Eksisterende Begrep-termer, forhåndslastet ÉN gang for hele sveipet (ikke ett spørring per
        // treff) — samme "unngå N+1" -hensyn som ellers i kodebasen. To separate mengder, se
        // klassekommentaren for HVORFOR scopingen er ulik per kategori.
        var virksomhetTermer = new HashSet<string>(
            await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende")
                .Select(b => b.Term).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);
        var gruppeTermerPerLovkilde = (await db.Begreper
                .Where(b => b.Begrepskategori == "gruppe" && b.Entitetsstatus == "gjeldende" && b.LovkildeId != null)
                .Select(b => new { b.Term, b.LovkildeId }).ToListAsync(ct))
            .GroupBy(b => b.LovkildeId!.Value)
            .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.Term), StringComparer.OrdinalIgnoreCase));

        // [Ny, kodegjennomgang 2026-08-30] Forhåndslastet, ÉN gang for hele sveipet, samme "unngå N+1"
        // -hensyn som mengdene over — normaliserte (små bokstaver) "gruppe"-termer PER RettskildeId, fra
        // EKSISTERENDE Navnekandidat-rader, uansett status. Brukes til å utvide "alleredeDekket"-sjekket
        // under til Å OGSÅ dekke ikke-godkjente kandidater, ikke bare godkjente Begrep-rader — se
        // klassekommentarens "Term-basert dedup"-avsnitt for hvorfor. Oppdateres fortløpende i løkken
        // under (samme sveip kan treffe samme normaliserte term flere ganger på ulike posisjoner).
        var gruppeKandidatTermerPerRettskilde = (await db.Navnekandidater
                .Where(k => k.Kategori == "gruppe")
                .Select(k => new { k.RettskildeId, k.ForeslattTekst }).ToListAsync(ct))
            .GroupBy(k => k.RettskildeId)
            .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.ForeslattTekst.ToLowerInvariant()), StringComparer.Ordinal));

        var antallTreff = 0;
        var antallNyeKandidater = 0;
        foreach (var node in noder)
        {
            foreach (var (start, lengde, kategori) in FinnKandidaterITekst(node.Tekst!))
            {
                var raaTekst = node.Tekst![start..(start + lengde)];
                // [Ny, kodegjennomgang 2026-08-30] Normaliser KUN "gruppe" til små bokstaver — se
                // klassekommentarens "Normalisering før lagring"-avsnitt. "virksomhet" beholder rå tekst
                // (case er signal, ikke støy, for et egennavn).
                var tekst = kategori == "gruppe" ? raaTekst.ToLowerInvariant() : raaTekst;

                var alleredeDekketAvBegrep = kategori == "virksomhet"
                    ? virksomhetTermer.Contains(tekst)
                    : gruppeTermerPerLovkilde.TryGetValue(node.RettskildeId, out var gruppeTermer) && gruppeTermer.Contains(tekst);
                // [Ny, kodegjennomgang 2026-08-30] Term-basert dedup mot EKSISTERENDE, ikke-godkjente
                // kandidater — kun "gruppe" (se klassekommentaren). Uavhengig av tekstposisjon: samme
                // normaliserte term i samme rettskilde skal ikke gi en ny rad, selv om posisjonen er ny.
                var alleredeDekketAvEksisterendeKandidat = kategori == "gruppe"
                    && gruppeKandidatTermerPerRettskilde.TryGetValue(node.RettskildeId, out var eksisterendeTermer)
                    && eksisterendeTermer.Contains(tekst);
                if (alleredeDekketAvBegrep || alleredeDekketAvEksisterendeKandidat) continue;

                antallTreff++;
                var forAntall = await db.Navnekandidater.CountAsync(
                    k => k.RettskildeId == node.RettskildeId && k.NodeEid == node.Eid && k.StartOffset == start, ct);
                await OpprettEllerFinnAsync(tekst, kategori, node.RettskildeId, node.Eid, start, start + lengde, opprettetAv, ct);
                if (forAntall == 0)
                {
                    antallNyeKandidater++;
                    if (kategori == "gruppe")
                    {
                        // Registrer umiddelbart, slik at en SENERE posisjon i samme sveip (samme
                        // rettskilde, samme normaliserte term) også blir korrekt gjenkjent som dekket.
                        if (!gruppeKandidatTermerPerRettskilde.TryGetValue(node.RettskildeId, out var settForRettskilde))
                        {
                            settForRettskilde = new HashSet<string>(StringComparer.Ordinal);
                            gruppeKandidatTermerPerRettskilde[node.RettskildeId] = settForRettskilde;
                        }
                        settForRettskilde.Add(tekst);
                    }
                }
            }
        }

        return new NavnekandidatSveipResultat(antallTreff, antallNyeKandidater);
    }

    /// <summary>
    /// [Ny, docs/31-navneform-berikelse-snl-ssr-spesifikasjon.md §5 punkt 2-3] Sveiper det NYE, brede
    /// "stor bokstav midt i en setning"-mønsteret (<see cref="FinnStorBokstavKandidaterITekst"/>),
    /// klassifisert mot SNL/SSR (<see cref="BeholdSomKandidatAsync"/>, docs/31 §2) — en HELT SEPARAT
    /// metode fra <see cref="SveipAsync"/> (samme node-scoping, <see cref="HentSveipbareNoderAsync"/>,
    /// men et uavhengig kjøretidspunkt/kall). Bevisst IKKE slått sammen med <see cref="SveipAsync"/> i
    /// ett kall: docs/31 §6 ber EKSPLISITT om at et FØRSTE kall mot dette mønsteret skjer mot et
    /// AVGRENSET delsett av korpuset (én/få rettskilder), ikke hele det importerte korpuset på én gang
    /// — ingen av de to eksterne API-ene har dokumentert ratelimit, og et fullkorpussveip kan generere
    /// svært mange unike SNL/SSR-oppslag. En egen metode/endepunkt gjør denne forsiktige, avgrensede
    /// bruken enkel uten å risikere at noen ved en feil trigger den sammen med det etablerte,
    /// veletablerte <see cref="SveipAsync"/>-sveipet.
    /// <para>
    /// <b>Overlapp med <see cref="SveipAsync"/> sine posisjoner er trygt</b> — samme
    /// (RettskildeId, NodeEid, StartOffset)-idempotens som <see cref="OpprettEllerFinnAsync"/> allerede
    /// garanterer uansett kilde/mønster: kjøres begge sveipene mot samme node, og begge mønstrene treffer
    /// nøyaktig samme START-posisjon (f.eks. et suffiks-treff som "Miljødirektoratet", som òg er et
    /// Title-case-ord), vinner den som kom FØRST — ingen duplikatrad, ingen krasj. Se også
    /// <see cref="FasteRollesubstantivOrdSet"/>/<see cref="InstitusjonsordSet"/>/suffiks-sjekken i
    /// <see cref="FinnStorBokstavKandidaterITekst"/> selv, som i tillegg proaktivt UNNGÅR å sende disse
    /// allerede-dekkede posisjonene til et eksternt oppslag i utgangspunktet.
    /// </para>
    /// </summary>
    public async Task<NavnekandidatSveipResultat> SveipStorBokstavAsync(Guid? rettskildeId, string opprettetAv, CancellationToken ct = default)
    {
        var noder = await HentSveipbareNoderAsync(rettskildeId, ct);

        var antallTreff = 0;
        var antallNyeKandidater = 0;
        foreach (var node in noder)
        {
            foreach (var (start, lengde, raaTekst) in FinnStorBokstavKandidaterITekst(node.Tekst))
            {
                antallTreff++;

                // Samme idempotens-nøkkel som OpprettEllerFinnAsync — sjekket HER, FØR det eksterne
                // SNL/SSR-oppslaget, for å unngå et helt unødvendig (kostbart, nettverksavhengig) kall
                // for en posisjon som allerede har en kandidat (fra ETT ELLER ANNET tidligere sveip,
                // uansett mønster/kilde).
                var eksisterendeAntall = await db.Navnekandidater.CountAsync(
                    k => k.RettskildeId == node.RettskildeId && k.NodeEid == node.Eid && k.StartOffset == start, ct);
                if (eksisterendeAntall > 0) continue;

                var behold = await BeholdSomKandidatAsync(raaTekst, node.Tekst, start + lengde, ct);
                if (!behold) continue; // SSR-bekreftet geografisk løpetekst-referanse — docs/31 §2 punkt 2.

                await OpprettEllerFinnAsync(
                    raaTekst, "virksomhet", node.RettskildeId, node.Eid, start, start + lengde, opprettetAv,
                    ct, StorBokstavOppdagelsesKilde);
                antallNyeKandidater++;
            }
        }

        return new NavnekandidatSveipResultat(antallTreff, antallNyeKandidater);
    }

    /// <summary>
    /// docs/31 §2 — selve klassifiseringskjeden for ETT rått "stor bokstav midt i setning"-treff.
    /// Returnerer <c>true</c> hvis kandidaten skal BEHOLDES:
    /// <list type="number">
    /// <item>SNL-bekreftet institusjon (<see cref="EksternNavneoppslagTjeneste.SlaOppSnlAsync"/> gir
    /// treff) — høy starttillit, positiv institusjonskandidat.</item>
    /// <item>Ingen SNL-treff, MEN SSR-bekreftet stedsnavn MED et <see cref="Institusjonsord"/> RETT ETTER
    /// i løpeteksten (<see cref="NesteOrdEtter"/>) — samme positive bekreftelsesrolle som når
    /// <see cref="InstitusjonsordMønster"/> allerede finner "X kommune" et annet sted i klassen; SSR gir
    /// en EKSTRA bekreftelse på at "X" er et reelt stedsnavn.</item>
    /// <item>Ukjent i BEGGE (ingen SNL-treff, ingen SSR-treff) — lav-tillit, men IKKE forkastet ("ingen
    /// gjettet fallback", samme filosofi som resten av mekanismen).</item>
    /// </list>
    /// Returnerer <c>false</c> (FORKAST) KUN i det ene tilfellet spesifikasjonen faktisk ber om å
    /// forkaste: SSR-bekreftet stedsnavn UTEN institusjonsord rett etter — en ren geografisk
    /// løpetekst-referanse, ikke et institusjonsnavn.
    /// </summary>
    private async Task<bool> BeholdSomKandidatAsync(string raaTekst, string tekst, int matchSlutt, CancellationToken ct)
    {
        var snl = await eksternOppslag.SlaOppSnlAsync(raaTekst, ct);
        if (snl.Treff) return true;

        var ssr = await eksternOppslag.SlaOppSsrAsync(raaTekst, ct);
        if (ssr.Treff)
        {
            var nesteOrd = NesteOrdEtter(tekst, matchSlutt);
            return nesteOrd is not null && InstitusjonsordMønster.IsMatch(nesteOrd);
        }

        return true; // ukjent i begge — lav-tillit, ikke forkastet.
    }

    /// <summary>
    /// Ren, testbar funksjon uten DB-avhengighet — selve mønstergjenkjenningen (docs/13-backlog.md §9),
    /// separert fra sveipets DB-orkestrering slik at klassifiseringslogikken kan enhetstestes direkte
    /// mot en tekststreng, uten en hel rettskilde-node/embedded Postgres.
    /// <para>
    /// <b>"Midt i en setning"</b> (<see cref="ErSetningsstart"/>): et suffikstreff med STOR forbokstav
    /// som er setningens FØRSTE ord telles IKKE som et egennavn (ambiguøst — kunne bare være vanlig
    /// stor forbokstav ved setningsstart) og gir INGEN kandidat i det hele tatt (verken "virksomhet"
    /// eller "gruppe") — det faller ikke tilbake til "gruppe", siden det fortsatt HAR stor forbokstav og
    /// dermed ikke oppfyller "gruppe"-regelens "liten forbokstav"-vilkår heller. Bevisst redusert recall
    /// for økt presisjon, som spesifisert.
    /// </para>
    /// </summary>
    internal static List<(int Start, int Lengde, string Kategori)> FinnKandidaterITekst(string tekst)
    {
        var funnet = new List<(int, int, string)>();

        foreach (Match m in SuffiksMønster.Matches(tekst))
        {
            var forsteBokstav = tekst[m.Index];
            if (char.IsUpper(forsteBokstav))
            {
                var erDenylistetVerketSammensetning = VerketDenyliste.Any(
                    ord => m.Value.EndsWith(ord, StringComparison.OrdinalIgnoreCase));
                if (!ErSetningsstart(tekst, m.Index) && !erDenylistetVerketSammensetning)
                {
                    funnet.Add((m.Index, m.Length, "virksomhet"));
                }
                // else: setningsstart (ambiguøst) ELLER denylistet "verket"-sammensetning (se
                // VerketDenyliste-kommentaren) — ingen kandidat i det hele tatt, verken tilfellet.
            }
            else
            {
                funnet.Add((m.Index, m.Length, "gruppe"));
            }
        }

        foreach (Match m in FasteGruppeMønster.Matches(tekst))
        {
            funnet.Add((m.Index, m.Length, "gruppe"));
        }

        // [Ny, kodegjennomgang 2026-08-30] Flerords-mønster (klassekommentarens punkt 4) — se
        // FinnEgennavnForanInstitusjonsord/ErFlerordsKontekstTillatt for presisjonsvernet.
        foreach (Match m in InstitusjonsordMønster.Matches(tekst))
        {
            var egennavn = FinnEgennavnForanInstitusjonsord(tekst, m.Index);
            if (egennavn is null) continue; // ingen store-forbokstav-ord rett før — generisk forekomst
                                             // (f.eks. "en fylkeskommune", "et statlig tilsyn"), ikke et navn.
            var (navnStart, _) = egennavn.Value;
            if (!ErFlerordsKontekstTillatt(tekst, navnStart)) continue;

            funnet.Add((navnStart, m.Index + m.Length - navnStart, "virksomhet"));
        }

        return funnet;
    }

    /// <summary>
    /// Skanner BAKOVER fra <paramref name="institusjonsordStart"/> og samler inntil 3 STOR-forbokstav-ord,
    /// ev. med ETT av <see cref="TillatteBindeord"/> mellom to av dem (f.eks. "Møre og Romsdal" foran
    /// "fylkeskommune"). Stopper ved skilletegn (<c>. , : ; ( )</c>), linjeskift, tekststart, eller et
    /// ord som verken er stor forbokstav eller et tillatt bindeord. Returnerer <c>null</c> hvis INGEN
    /// store-forbokstav-ord ble funnet rett før (den generiske "en fylkeskommune"/"et statlig
    /// tilsyn"-casen — presisjonsvernet docs/13-backlog.md §9 krever) — et dinglende bindeord i hver
    /// ende av det innsamlede spennet fjernes (bindeordet skal kun stå MELLOM to store-forbokstav-ord,
    /// aldri innlede eller avslutte selve navnet), og et for langt spenn (mer enn 3 store-forbokstav-ord
    /// eller mer enn ett bindeord — bør ikke kunne skje gitt filtreringen under, men sjekket eksplisitt
    /// for lesbarhet/defensivt) forkastes HELT i stedet for å bli kappet vilkårlig — ingen gjettet
    /// fallback for hvor navnet "egentlig" starter.
    /// </summary>
    private static (int Start, int Lengde)? FinnEgennavnForanInstitusjonsord(string tekst, int institusjonsordStart)
    {
        var tokens = new List<(int Start, int Lengde, bool StorForbokstav)>();
        var posisjon = institusjonsordStart;
        while (tokens.Count < 4) // maks 3 store-forbokstav-ord + ett bindeord
        {
            var j = posisjon - 1;
            while (j >= 0 && (tekst[j] == ' ' || tekst[j] == '\t')) j--;
            if (j < 0) break; // tekststart
            if (tekst[j] is '.' or ',' or ':' or ';' or '(' or ')' or '\n' or '\r') break; // skilletegn/linjeskift
            if (!char.IsLetter(tekst[j])) break; // ukjent tegn rett før (f.eks. et siffer) — stopp konservativt

            var ordSlutt = j + 1;
            var ordStart = j;
            while (ordStart > 0 && char.IsLetter(tekst[ordStart - 1])) ordStart--;
            var ord = tekst[ordStart..ordSlutt];

            var erBindeord = TillatteBindeord.Contains(ord);
            // [Ny, kodegjennomgang 2026-08-30] "AldriEgennavnOrd" — se den listens kommentar. Et
            // determinativ/pronomen (f.eks. "Enhver", "Det") teller IKKE som et egennavn-ord selv om
            // det er stor forbokstav (typisk kun fordi det tilfeldigvis åpner en node/setning).
            var erStorForbokstav = char.IsUpper(ord[0]) && !AldriEgennavnOrd.Contains(ord);
            if (!erStorForbokstav && !erBindeord) break; // hverken egennavn-ord eller tillatt bindeord

            tokens.Add((ordStart, ord.Length, erStorForbokstav));
            posisjon = ordStart;
        }

        tokens.Reverse(); // nå i lese-rekkefølge (venstre-til-høyre, nærmest institusjonsordet sist)

        // Fjern et evt. dinglende bindeord i STARTEN — bindeordet skal kun stå MELLOM to
        // store-forbokstav-ord, aldri innlede selve det fangede navnet. Trygt å bare fjerne og gå
        // videre her: forkastet Start blir ganske enkelt neste (ekte) token, uten noen konsekvens for
        // selve tekstspennet som caller (FinnKandidaterITekst) fanger, siden det spennet uansett
        // begynner nøyaktig ved <see cref="forste"/>.Start.
        while (tokens.Count > 0 && !tokens[0].StorForbokstav) tokens.RemoveAt(0);

        // [Rettet, kodegjennomgang 2026-08-30] Et dinglende bindeord UMIDDELBART FØR institusjonsordet
        // (dvs. det SISTE innsamlede tokenet, nærmest institusjonsordet) kan IKKE bare fjernes på samme
        // måte som i starten — det må forkaste HELE treffet. Årsak: til forskjell fra starten, strekker
        // callerens fangede tekstspenn (FinnKandidaterITekst) seg alltid fra <see cref="forste"/>.Start
        // og HELT TIL institusjonsordets slutt (ikke til denne metodens returnerte Lengde) — å bare
        // fjerne det dinglende bindeordet her og falle tilbake til et tidligere token ville derfor
        // uansett re-inkludert bindeordet (og mellomrommet) i den fangede teksten, siden det ligger
        // FYSISK MELLOM det gjenværende tokenet og institusjonsordet. Bekreftet i live data (dette
        // sveipets skole-relaterte testing): "nivå 5 i NKR og fagskole 1" ga tidligere den falske
        // kandidaten "NKR og fagskole" (der "NKR" er en forkortelse — nivåbetegnelse i det nasjonale
        // kvalifikasjonsrammeverket — og "og" her er en ekte, urelatert setningskonjunksjon, ikke et
        // navneinternt bindeord), og "SFO og skole" (samme mønster, "SFO" og "skole" er to separate
        // generiske substantiv koordinert med "og", ikke ett institusjonsnavn). Denne bug'en fantes
        // allerede før dagens skole-utvidelse (allerede i "Møre og Romsdal fylkeskommune"-mekanismen fra
        // forrige runde), men ble aldri utløst da — ingen tidligere institusjonsord hadde et vanlig
        // forkortelse+"og"-mønster rett foran seg i korpuset.
        if (tokens.Count > 0 && !tokens[^1].StorForbokstav) return null;

        if (tokens.Count == 0) return null;
        if (tokens.Count(t => t.StorForbokstav) > 3 || tokens.Count(t => !t.StorForbokstav) > 1) return null;

        var forste = tokens[0];
        var siste = tokens[^1];

        // [Ny, kodegjennomgang 2026-08-30] Genitiv-vern, SNEVER: forkast HELE treffet KUN når ordet
        // rett før institusjonsordet er [ET KJENT SUFFIKS-NAVN, se Suffikser] + genitiv-"s" — f.eks.
        // "Finanstilsynets"="Finanstilsynet"+"s", "Oljedirektoratets"="Oljedirektoratet"+"s" (avdekket
        // av samme korpusomfattende testsveip: ga falske positiver som "Finanstilsynets tilsyn",
        // "Arbeidstilsynets tilsyn", "Oljedirektoratets tilsyn" — genitivsform AV en ALLEREDE navngitt
        // institusjon + institusjonsordet, betyr "tilsynet TIL X", ikke et navn på en NY institusjon).
        // <b>Bevisst IKKE "et hvilket som helst ord som ender på s"</b> — det FØRSTE forsøket testet
        // nettopp det og brøt STRAKS to av mønsterets egne, ekte positive treff: "Akershus
        // fylkeskommune" (Akershus ender på "s" av rene etymologiske grunner, ikke genitiv) og "Statens
        // vegvesen" ("Statens" ER den faktiske, offisielle stavemåten — ikke en genitivkonstruksjon AV
        // noe annet navngitt). Snevret inn til KUN suffiks+s-mønsteret over — dekker ikke enhver
        // tenkelig genitivkonstruksjon (f.eks. en forkortelse som "NVEs tilsyn" slipper fortsatt
        // gjennom), men unngår den brede kollateralskaden det første forsøket ga.
        var sisteOrdSlutt = siste.Start + siste.Lengde;
        var sisteOrdUtenGenitivS = tekst[sisteOrdSlutt - 1] is 's' or 'S'
            ? tekst[siste.Start..(sisteOrdSlutt - 1)]
            : null;
        if (sisteOrdUtenGenitivS is not null
            && Suffikser.Any(s => sisteOrdUtenGenitivS.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return (forste.Start, sisteOrdSlutt - forste.Start);
    }

    /// <summary>
    /// Presisjonsvernet for flerords-mønsteret (analogt <see cref="ErSetningsstart"/> for
    /// suffiksmønsteret, men IKKE identisk — se hvorfor under). Tillatt kontekst:
    /// <list type="bullet">
    /// <item>Midt i en setning (samme sjekk som <see cref="ErSetningsstart"/> — ikke rett etter et
    /// setningsavsluttende tegn) — alltid tillatt.</item>
    /// <item>ABSOLUTT start av teksten (<paramref name="tekst"/> begynner selv med navnet) — TILLATT
    /// her, til forskjell fra suffiksmønsterets strengere "aldri ved setningsstart"-regel. Bekreftet i
    /// live data (FOR-2019-09-30-1310 §2): AKN-listepunkter importeres HVER som sin EGEN
    /// <c>RettskildeNode</c> uten noen tekstlig liste-markør i selve <c>Tekst</c> (bokstav-/tallmarkøren
    /// er strukturell metadata, ikke en del av teksten) — "Østfold fylkeskommune: …" står derfor
    /// bokstavelig på posisjon 0 av SIN node. Risikoen suffiksregelen verner mot (et vanlig substantiv
    /// tilfeldigvis stort fordi det åpner en node, f.eks. "Departementet kan …") reduseres her av at
    /// mønsteret i tillegg KREVER et spesifikt institusjonsord rett etter — en langt sterkere
    /// bekreftelse enn stor forbokstav alene.</item>
    /// <item>Rett etter et setningsavsluttende tegn, MEN kun hvis det tegnet faktisk er en
    /// liste-linje-markør (bokstav/tall + punktum, f.eks. "a." eller "1.") ved starten av GJELDENDE
    /// linje, ikke en ekte setningsslutt — se <see cref="ErListePrefiksVedLinjestart"/>. Uten dette
    /// unntaket ville f.eks. "… se listen. a. Østfold fylkeskommune: …" (en annen importrute enn den
    /// bekreftede AKN-punkt-per-node-varianten over, der liste-markøren ER en del av rå teksten) blitt
    /// avvist som "tvetydig setningsstart".</item>
    /// </list>
    /// <para>
    /// <b>[Ny, kodegjennomgang 2026-08-30] Dokumentert, GJENVÆRENDE begrensning (bevisst ikke fikset):</b>
    /// den ABSOLUTTE tekststart-tillatelsen over kombinert med <see cref="FinnEgennavnForanInstitusjonsord"/>
    /// slipper fortsatt gjennom et adjektiv/verb som tilfeldigvis åpner en node og er stor forbokstav —
    /// f.eks. "Statlig tilsyn", "Overordnet departement", "Føre tilsyn" (funnet i samme korpusomfattende
    /// testsveip som begrunner <see cref="AldriEgennavnOrd"/>/<see cref="TillatteBindeord"/>-innstrammingen
    /// over). <see cref="AldriEgennavnOrd"/> fjerner determinativer/pronomen (grammatisk LUKKET ordklasse
    /// i norsk), men adjektiv/verb er en ÅPEN ordklasse — en uttømmende liste er ikke mulig uten enten
    /// et uholdbart stort ordforråd eller ekte POS-tagging (NLP/språkmodell, eksplisitt forbudt). Godtatt
    /// som en bevisst presisjon/recall-avveining, samme filosofi som <see cref="ErSetningsstart"/>s
    /// tilsvarende, dokumenterte begrensning for suffiksmønsteret — IKKE besluttet stilltiende, flagget
    /// eksplisitt til Johann i samme PR som denne kommentaren.
    /// </para>
    /// </summary>
    private static bool ErFlerordsKontekstTillatt(string tekst, int index)
    {
        var i = index - 1;
        while (i >= 0 && char.IsWhiteSpace(tekst[i])) i--;
        if (i < 0) return true; // absolutt tekststart — se metodekommentaren.
        if (tekst[i] is '.' or '!' or '?') return ErListePrefiksVedLinjestart(tekst, index);
        return true; // midt i en setning (ikke rett etter et setningsavsluttende tegn).
    }

    /// <summary>Er <paramref name="index"/> — etter at en evt. liste-markør er trukket fra — starten av
    /// SIN linje? Ser på teksten fra siste linjeskift (eller tekststart) og fram til <paramref name="index"/>,
    /// og krever at det ENTEN er tomt (rent linjeskift rett før) ELLER består av nøyaktig én kort
    /// bokstav-/tallmarkør + punktum + mellomrom (f.eks. "a. ", "12. ") — IKKE en vilkårlig lengre
    /// tekst som tilfeldigvis ender på et punktum (det ville vært en ekte setningsslutt, ikke en
    /// liste-markør).</summary>
    private static bool ErListePrefiksVedLinjestart(string tekst, int index)
    {
        var linjeStart = index;
        while (linjeStart > 0 && tekst[linjeStart - 1] != '\n') linjeStart--;
        var prefiks = tekst[linjeStart..index];
        return ListePrefiksMønster.IsMatch(prefiks);
    }

    private static readonly Regex ListePrefiksMønster = new(@"^\s*[\p{L}0-9]{1,3}\.\s+$");

    /// <summary>
    /// Skanner bakover fra <paramref name="index"/>, hopper over whitespace, og ser på det første
    /// ikke-whitespace-tegnet før det. Start av teksten ELLER et setningsavsluttende tegn (<c>. ! ?</c>)
    /// rett før → setningsstart. Ren tegnbasert heuristikk (ingen ekte språklig setningsparsing) —
    /// dokumentert begrensning: et paragraf-/leddnummer som "(1) " rett før treffet regnes IKKE som en
    /// setningsavslutning (parentesen er ikke i tegnlisten over), så "(1) Advokattilsynet utsteder …"
    /// telles som MIDT i en setning (ikke setningsstart) — et bevisst enkelt valg konsistent med at hele
    /// mekanismen er "ren tekstanalyse (regex)", ikke ekte NLP-setningsgrensededeksjon.
    /// </summary>
    private static bool ErSetningsstart(string tekst, int index)
    {
        var i = index - 1;
        while (i >= 0 && char.IsWhiteSpace(tekst[i])) i--;
        return i < 0 || tekst[i] is '.' or '!' or '?';
    }

    /// <summary>Idempotent — samme (rettskilde, node, START-posisjon) gir samme rad tilbake i stedet
    /// for et duplikat, uansett status (samme mønster som <see cref="VirksomhetKandidatTjeneste.OpprettEllerFinnAsync"/>).</summary>
    public async Task<NavnekandidatEntitet> OpprettEllerFinnAsync(
        string foreslattTekst, string kategori, Guid rettskildeId, string nodeEid, int startOffset, int endOffset,
        string opprettetAv, CancellationToken ct = default, string? oppdagelsesKilde = null)
    {
        var eksisterende = await db.Navnekandidater.FirstOrDefaultAsync(
            k => k.RettskildeId == rettskildeId && k.NodeEid == nodeEid && k.StartOffset == startOffset, ct);
        if (eksisterende is not null) return eksisterende;

        if (kategori is not ("virksomhet" or "gruppe"))
        {
            throw new ArgumentException($"Ukjent kategori '{kategori}'. Gyldige verdier: 'virksomhet', 'gruppe'.");
        }
        var node = await db.RettskildeNoder.FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == nodeEid, ct);
        if (node is null)
        {
            throw new ArgumentException($"Fant ingen rettskilde-node med eId '{nodeEid}' i rettskilde '{rettskildeId}'. Ingen gjettet fallback.");
        }
        if (endOffset <= startOffset || startOffset < 0 || endOffset > (node.Tekst?.Length ?? 0))
        {
            throw new ArgumentException(
                $"Ugyldig tegnintervall [{startOffset}, {endOffset}) for node '{nodeEid}' (tekstlengde {node.Tekst?.Length ?? 0}).");
        }

        var kandidat = new NavnekandidatEntitet
        {
            Id = Guid.NewGuid(),
            ForeslattTekst = foreslattTekst,
            Kategori = kategori,
            RettskildeId = rettskildeId,
            NodeEid = nodeEid,
            StartOffset = startOffset,
            EndOffset = endOffset,
            Status = "Venter",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
            OppdagelsesKilde = oppdagelsesKilde,
        };
        db.Navnekandidater.Add(kandidat);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // [Rettet, kodegjennomgang 2026-08-30] Sjekk-så-sett mot en unik DB-indeks
            // (ux_navnekandidater_rettskilde_node_start) uten låsing — to overlappende sveip kan begge
            // passere FirstOrDefaultAsync-sjekken over før noen av dem committer. I stedet for en
            // ufanget 500 til klienten: den andre skriveren har allerede vunnet, hent OG returner DEN
            // raden — samme idempotente utfall som om sjekken over hadde funnet den først.
            db.Entry(kandidat).State = EntityState.Detached;
            var vantLopet = await db.Navnekandidater.FirstOrDefaultAsync(
                k => k.RettskildeId == rettskildeId && k.NodeEid == nodeEid && k.StartOffset == startOffset, ct);
            if (vantLopet is not null) return vantLopet;
            throw; // reelt uventet — verken vår rad eller en konkurrerende rad finnes, kast videre
        }
        return kandidat;
    }

    /// <summary>Full liste, valgfritt filtrert på status og/eller kategori. <paramref name="status"/> =
    /// <c>null</c> betyr ALLE statuser (samme eksplisitte "ingen stille standard"-mønster som
    /// <see cref="VirksomhetKandidatTjeneste.ListerAsync"/>).</summary>
    public Task<List<NavnekandidatEntitet>> ListerAsync(
        string? status = null, string? kategori = null, Guid? rettskildeId = null, CancellationToken ct = default)
    {
        var spørring = db.Navnekandidater.AsQueryable();
        if (status is not null) spørring = spørring.Where(k => k.Status == status);
        if (kategori is not null) spørring = spørring.Where(k => k.Kategori == kategori);
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        return spørring.OrderBy(k => k.RettskildeId).ThenBy(k => k.NodeEid).ThenBy(k => k.StartOffset).ToListAsync(ct);
    }

    /// <summary>
    /// Godkjenner kandidaten. Oppførsel avhenger av <see cref="NavnekandidatEntitet.Kategori"/> (se
    /// klassekommentaren på <see cref="NavnekandidatEntitet"/> for HVORFOR):
    /// <list type="bullet">
    /// <item><c>"gruppe"</c> — oppretter et EKTE gruppebegrep direkte
    /// (<see cref="VirksomhetsbegrepTjeneste.OpprettGruppebegrepAsync"/>, <c>Term</c>=<see cref="NavnekandidatEntitet.ForeslattTekst"/>,
    /// <c>LovkildeId</c>=kandidatens <see cref="NavnekandidatEntitet.RettskildeId"/>) — alt godkjenningen
    /// trenger er allerede kjent fra selve kandidaten. [Rettet, 2026-08-30] Sender også med
    /// <see cref="NavnekandidatEntitet.NodeEid"/> som gruppebegrepets <c>LovreferanseEid</c> — uten
    /// dette var det umulig å se, fra selve paragrafen, at gruppebegrepet stammer derfra (Johann
    /// observerte at «Statsforvalteren» ikke viste seg tagget i vergemålsforskriften § 19 ledd 1,
    /// der den faktisk ble funnet).</item>
    /// <item><c>"virksomhet"</c> — oppretter INGEN <see cref="BegrepEntitet"/>. Godkjenning her betyr
    /// kun "reelt navn, verdt å følge opp" — selve koblingen til en konkret <see cref="Virksomhet"/>
    /// (ny eller eksisterende) krever et menneske og skjer via den eksisterende
    /// navneform-tilleggsflyten i <c>VirksomhetDetalj.tsx</c>/<c>VirksomhetsbegrepTjeneste.OpprettVirksomhetsbegrepAsync</c>.</item>
    /// </list>
    /// Hvis gruppebegrep-opprettelsen kaster (f.eks. en rad med samme (Term, LovkildeId) allerede finnes
    /// — <see cref="VirksomhetsbegrepTjeneste.OpprettGruppebegrepAsync"/> sitt eget "ingen gjettet
    /// fallback"-vern), forblir kandidatens status <c>"Venter"</c> og feilen forplantes uendret —
    /// samme "ikke sett status før den faktiske handlingen lyktes"-prinsipp som
    /// <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/>.
    /// <para>
    /// <b>[Ny, tekst-tagg-departement-eierskap, 2026-08-31] Ekte <see cref="TekstTaggEntitet"/> for
    /// BEGGE kategorier:</b> Johanns eksplisitte designvalg — et gruppebegrep/navneform funnet her er
    /// delt/nasjonalt (ingen eiende virksomhet på selve <see cref="BegrepEntitet"/>), men
    /// <see cref="TekstTaggEntitet.VirksomhetId"/> er ikke-nullbar ("en tagg er alltid en virksomhets
    /// eget arbeidsprodukt"). Løsningen: opprett taggen med <see cref="TekstTaggEntitet.VirksomhetId"/>
    /// = virksomheten til rettskildens <see cref="RettskildeEntitet.AnsvarligDepartement"/> ("det eies
    /// av ansvarlig departement [...] men det skal jo være mulig å se taggene allikevel — opprett
    /// disse med virksomheten til departementet"). <see cref="TekstTaggEntitet.RefId"/> settes til det
    /// NYE gruppebegrepets id for <c>"gruppe"</c> (samme <c>Kind="begrep"</c>-mønster som
    /// <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/> allerede bruker for navneform-treff), og
    /// forblir <c>null</c> for <c>"virksomhet"</c> (INGEN Begrep-rad opprettes for den kategorien i det
    /// hele tatt her — "ingen gjettet fallback": ingen fabrikert id å peke på). Se
    /// <see cref="OpprettDepartementTaggHvisMuligAsync"/> for selve implementasjonen, inkl. når INGEN
    /// tagg opprettes (ukjent/uoppløsbart departement — en reell, dokumentert begrensning, ikke noe å
    /// arbeide rundt). Denne siden-effekten kan ALDRI hindre selve godkjenningen: en stale
    /// node/tekstposisjon (rettskilden endret siden sveipet) degraderer til "ingen tagg", ikke en kastet
    /// feil — til forskjell fra <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/>, der taggen ER
    /// selve hovedformålet med godkjenningen.
    /// </para>
    /// </summary>
    public async Task<NavnekandidatEntitet?> GodkjennAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.Navnekandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        if (kandidat.Status != "Venter")
        {
            throw new ArgumentException(
                $"Kandidaten har status '{kandidat.Status}' — kan kun godkjenne kandidater med status 'Venter'.");
        }

        Guid? refIdForTagg = null;
        if (kandidat.Kategori == "gruppe")
        {
            var gruppebegrep = await virksomhetsbegrep.OpprettGruppebegrepAsync(
                kandidat.RettskildeId, kandidat.ForeslattTekst, behandletAv, kandidat.NodeEid, ct);
            refIdForTagg = gruppebegrep.Id;
        }
        // "virksomhet": ingen Begrep-entitet opprettes her — se metodekommentaren.

        await OpprettDepartementTaggHvisMuligAsync(kandidat, refIdForTagg, behandletAv, ct);

        kandidat.Status = "Godkjent";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>
    /// Oppretter den faktiske <see cref="TekstTaggEntitet"/>-forekomsten for en godkjent kandidat, eid
    /// av virksomheten til rettskildens <see cref="RettskildeEntitet.AnsvarligDepartement"/> — se
    /// <see cref="GodkjennAsync"/> sin metodekommentar for designvalget. Oppretter INGEN tagg (returnerer
    /// stille) hvis:
    /// <list type="bullet">
    /// <item>rettskilden ikke har noe kjent <see cref="RettskildeEntitet.AnsvarligDepartement"/>, ELLER</item>
    /// <item>departementstrengen ikke løser til noen ekte <see cref="Virksomhet"/>-rad
    /// (<see cref="VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnAsync"/> — gjenbrukt, ikke duplisert,
    /// samme mekanisme som <c>RettskildeRepository</c> bruker for "Ansvarlig for"-visningen), ELLER</item>
    /// <item>noden/tekstintervallet ikke lenger er gyldig (rettskilden endret siden sveipet) — degraderer
    /// til "ingen tagg" i stedet for å kaste, se <see cref="GodkjennAsync"/>s kommentar for hvorfor.</item>
    /// </list>
    /// <paramref name="refId"/> kobles inn via <see cref="TekstTaggTjeneste.KobleTilEntitetAsync"/> KUN
    /// når den er satt (kun for <c>"gruppe"</c> — se <see cref="GodkjennAsync"/>); for <c>"virksomhet"</c>
    /// opprettes taggen med <c>RefId=null</c>, samme "ubundet inntil videre"-tilstand som
    /// <see cref="TekstTaggTjeneste.OpprettAsync"/> selv dokumenterer.
    /// </summary>
    private async Task OpprettDepartementTaggHvisMuligAsync(
        NavnekandidatEntitet kandidat, Guid? refId, string behandletAv, CancellationToken ct)
    {
        var ansvarligDepartement = await db.Rettskilder
            .Where(r => r.Id == kandidat.RettskildeId)
            .Select(r => r.AnsvarligDepartement)
            .FirstOrDefaultAsync(ct);
        if (ansvarligDepartement is null) return; // ukjent departement — ingen gjettet fallback.

        var departementVirksomhetId = await virksomhetOppslag.FinnVirksomhetIdForNavnAsync(ansvarligDepartement);
        if (departementVirksomhetId is null) return; // uoppløsbart departement — ingen gjettet fallback.

        var node = await db.RettskildeNoder.FirstOrDefaultAsync(
            n => n.RettskildeId == kandidat.RettskildeId && n.Eid == kandidat.NodeEid, ct);
        var tekst = node?.Tekst;
        if (tekst is null
            || kandidat.StartOffset < 0 || kandidat.EndOffset > tekst.Length || kandidat.EndOffset <= kandidat.StartOffset)
        {
            // Noden finnes ikke lenger, eller intervallet er ikke lenger gyldig (rettskilden er
            // reimportert/endret siden sveipet) — degraderer til "ingen tagg" i stedet for å kaste, se
            // GodkjennAsync sin metodekommentar for hvorfor dette IKKE skal hindre selve godkjenningen.
            return;
        }

        // Samme 30-tegns kontekstvindu som VirksomhetKandidatTjeneste.GodkjennAsync og klienten
        // (RettskildeDetalj.tsx sin manuelle tagging) bruker for QuotePrefix/QuoteSuffix.
        const int kontekstLengde = 30;
        var quoteExact = tekst[kandidat.StartOffset..kandidat.EndOffset];
        var quotePrefix = tekst[Math.Max(0, kandidat.StartOffset - kontekstLengde)..kandidat.StartOffset];
        var quoteSuffix = tekst[kandidat.EndOffset..Math.Min(tekst.Length, kandidat.EndOffset + kontekstLengde)];

        var tagg = await tekstTaggTjeneste.OpprettAsync(
            kandidat.RettskildeId, departementVirksomhetId.Value, behandletAv, kandidat.NodeEid,
            kandidat.StartOffset, kandidat.EndOffset, quotePrefix, quoteExact, quoteSuffix, "begrep", ct);
        if (tagg is null) return; // racy sletting av node mellom sjekkene over — samme "degrader" som over.

        if (refId is not null)
        {
            await tekstTaggTjeneste.KobleTilEntitetAsync(tagg.Id, refId.Value, behandletAv, ct);
        }
    }

    public async Task<NavnekandidatEntitet?> AvvisAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.Navnekandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        if (kandidat.Status != "Venter")
        {
            throw new ArgumentException(
                $"Kandidaten har status '{kandidat.Status}' — kan kun avvise kandidater med status 'Venter'.");
        }
        kandidat.Status = "Avvist";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>
    /// [Ny, 2026-08-30] Ekte sletting (<c>Remove</c>, IKKE soft-delete) av ÉN kandidatrad, uansett
    /// status. Til forskjell fra <see cref="VirksomhetKandidatTjeneste.HardslettAvvistAsync"/> (som KUN
    /// tillater hardsletting av <c>'Avvist'</c>-rader, docs/20 §2.6) er det HER ingen slik begrensning:
    /// <see cref="NavnekandidatEntitet"/> har ingen Entitetsstatus/proveniens-kobling (se klassekommentaren
    /// der) — den er en ren oppdagelseskø, ikke et revisjonsspor. Formålet med sletting (Johann, ytelsestest
    /// av sortering/filtrering-UI-en + de nye flerords-mønsterreglene) krever nettopp å kunne tømme
    /// KORPUSET, inkludert allerede godkjente/avviste rader — den posisjonsbaserte idempotensen i
    /// <see cref="OpprettEllerFinnAsync"/> (<c>RettskildeId</c>, <c>NodeEid</c>, <c>StartOffset</c>) gir
    /// ellers ALDRI en ny rad på en posisjon som allerede har en (selv avvist) kandidat, så et nytt sveip
    /// kan aldri re-evaluere allerede sveipet tekst mot de nye reglene uten ekte sletting her.
    /// </summary>
    public async Task<bool> SlettAsync(Guid id, CancellationToken ct = default)
    {
        var kandidat = await db.Navnekandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return false;
        db.Navnekandidater.Remove(kandidat);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// [Ny, 2026-08-30] Massesletting — samme valgfrie filter-signatur som <see cref="ListerAsync"/>
    /// (status/kategori/rettskildeId, hver <c>null</c> betyr "ingen filtrering på DEN dimensjonen", samme
    /// eksplisitte "ingen stille standard"-mønster). Lar Johann slette f.eks. kun kandidatene for ÉN
    /// rettskilde (for et avgrenset ytelsestest-sveip på nytt), i stedet for kun "alt eller ingenting".
    /// Ekte sletting, samme begrunnelse som <see cref="SlettAsync"/>. Returnerer antall slettede rader,
    /// slik at klienten kan bekrefte at det faktiske antallet stemte med det som ble varslet før kallet.
    /// </summary>
    public async Task<int> SlettAlleAsync(
        string? status = null, string? kategori = null, Guid? rettskildeId = null, CancellationToken ct = default)
    {
        var spørring = db.Navnekandidater.AsQueryable();
        if (status is not null) spørring = spørring.Where(k => k.Status == status);
        if (kategori is not null) spørring = spørring.Where(k => k.Kategori == kategori);
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        var rader = await spørring.ToListAsync(ct);
        db.Navnekandidater.RemoveRange(rader);
        await db.SaveChangesAsync(ct);
        return rader.Count;
    }
}

/// <summary>Oppsummering av ett sveip — <see cref="AntallTreffFunnet"/> teller ALLE mønstertreff (også
/// de som allerede fantes som kandidat fra et tidligere sveip, eller som ble filtrert bort fordi de
/// allerede er dekket av et eksisterende Begrep — se <see cref="NavnekandidatOppdagelseTjeneste.SveipAsync"/>
/// for at "dekket"-filtreringen skjer FØR denne telles opp, altså telles et dekket treff IKKE med her),
/// <see cref="AntallNyeKandidater"/> kun de som faktisk ble en NY rad i køen denne kjøringen.</summary>
public sealed record NavnekandidatSveipResultat(int AntallTreffFunnet, int AntallNyeKandidater);
