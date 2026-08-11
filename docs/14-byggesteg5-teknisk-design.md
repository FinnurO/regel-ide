# 14. Byggesteg 5, teknisk design — Kunnskapsbibliotek + KI-agenter

*Kort mal for rundene som bygger de gjenstående KI-agentene (Tjenestebeskrivelse/Vilkår-og-
Vilkårstre/Håndbok), skrevet etter at runde 1 («Identifiser tjenester» + «Identifiser begrep») var
verifisert. Se `06-veikart.md` for retningsbeslutningen og `02-produktkrav.md` kap. 3.10 for
brukerflaten. Mye kortere enn `08-byggesteg1-teknisk-design.md` — byggesteg 5 introduserer ett nytt
arkitektonisk mønster (ekstern KI-klient-abstraksjon), ikke et helt nytt delsystem.*

## 1. `IKiAgentKlient` — kontrakten fremtidige agenter skal bruke

```csharp
public interface IKiAgentKlient
{
    Task<string> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default);
}
```

- **Én metode, rå streng inn og ut.** Vendor-spesifikk request/response-oversettelse hører hjemme i
  den konkrete implementasjonen (`KiAgentKlientStub` i dag), IKKE i grensesnittet — det holder
  interfacet stabilt uansett hvilken leverandør som velges senere.
- **Parsing av svaret er agentens ansvar**, ikke klientens. Hver agent-service (`BegrepsforslagTjeneste`,
  `TjenesteforslagTjeneste`, og fremtidige) definerer sin egen private JSON-kontrakt for hva den ber
  om og forventer tilbake — samme mønster som `KiAgentKlientStub`s to faste svar viser.
- **Registrer i `Program.cs`**: betinget på `RegelIde:KiAgent:Leverandor` (`"Stub"` default |
  `"OpenAiKompatibel"`) — `AddHttpClient<IKiAgentKlient, KiAgentKlientOpenAiKompatibel>()` vs.
  `AddScoped<IKiAgentKlient, KiAgentKlientStub>()`.
- **Runde 3 (2026-08-10) generaliserte klienten** til å være leverandøragnostisk —
  `KiAgentKlientOpenAiKompatibel.cs` snakker OpenAI-kompatibelt chat-completions-format mot en
  KONFIGURERBAR `RegelIde:KiAgent:BaseUrl`, ikke en hardkodet leverandør-URL. Erstatter runde 2s
  `KiAgentKlientOpenRouter` (hardkodet mot openrouter.ai + en DeepSeek-default-modell) — begge er nå
  fjernet. Et leverandør- eller modellbytte er en ren konfigverdi-endring
  (`RegelIde:KiAgent:BaseUrl`/`Modell`/`ApiKey`), aldri en kodeendring. Vurdert leverandør: HostYourAI
  (bekreftet ekte — EU-hostet, GDPR-compliant, OpenAI- og Anthropic-kompatibelt API, kjører åpne
  modeller på EU-GPU-er) — løser samme Kina-hosting-bekymring som runde 2 flagget for DeepSeeks egen
  API, men for ETHVERT åpent modell-vektsett, ikke bare én navngitt leverandør. API-nøkkel:
  `RegelIde:KiAgent:ApiKey` via `dotnet user-secrets` (IKKE `appsettings.Local.json` — begge virker
  via `IConfiguration`, men User Secrets er null-risiko for commit ved uhell). **Modellvalg fra en
  admin-side i appen (uten restart) er fortsatt bevisst IKKE bygget** — konfig+restart holder for
  denne runden; en dispatcher-`IKiAgentKlient` som slår opp gjeldende leverandør/modell fra en
  DB-lagret innstilling per kall er en avgrenset, senere utvidelse hvis behovet faktisk oppstår.
- `AiForslagVersjon` (proveniens-feltet, vises som "KI-versjon" i kø-UI) beregnes nå dynamisk i
  begge agent-tjenestene (`$"OpenAiKompatibel:{Modell}"` når ekte leverandør er aktiv, ellers
  `"stub-v1"`) — en fast konstant ville løyet om proveniensen idet en ekte modell faktisk kjører.
- Tester mot en ekte leverandør (`KiAgentKlientOpenAiKompatibelTests.cs`) stubber
  `HttpMessageHandler` — ALDRI ekte nettverkskall i automatiserte tester her, i motsetning til
  `LovdataBulkHenterTests` (Lovdata er gratis/uautentisert offentlig data; et ekte KI-kall koster
  penger og krever en nøkkel som ikke skal ligge i CI).
- Trege, ekte KI-kall trenger fortsatt ingen bakgrunnsjobb-mekanisme i praksis — ingen slik mekanisme
  finnes i kodebasen (se `05-arkitektur-og-nfk.md`); vurder dette på nytt hvis en fremtidig agent
  bruker mye lengre kontekst.
- **`RettskildeKontekstHjelper` inkluderer nå `Eid` per node** (runde 3) — uten dette kunne en agent
  aldri returnere en presis `LovreferanseEid` for et Begrep, uansett hvor godt den var instruert,
  fordi informasjonen rett og slett ikke fantes i det den fikk se.
- **System-instruksene er nå fullstendige, skjema-beskrivende prompter** (runde 3), ikke ettords-
  etiketter — de spesifiserer eksakte feltnavn, tillatte enum-verdier, og "svar KUN med ren JSON,
  ingen markdown-kodeblokk". `JsonSvarHjelper.StrimleKodeblokk` strimler defensivt en evt.
  ` ```json ``` `-innpakning uansett, siden ekte chatmodeller ofte gjør dette selv når de er bedt om
  å la være.

## 2. Kunnskapsbibliotek — skjema (lenker + filer, runde 2)

`KunnskapsbibliotekLenkeEntitet` og `KunnskapsbibliotekFilEntitet` (`src/RegelIde.Data/Entiteter.cs`)
er **separate tabeller**, ikke én entitet med en `Type`-diskriminator — samme begrunnelse som andre
steder i kodebasen (`RettskildeReferanseEntitet` vs. Tjeneste-referanse-tabellen): formen er for ulik
(binært innhold + utvunnet tekst vs. en ren URL) til at en delt tabell med mange nullable kolonner
ville vært klarere. Begge scopet til `VirksomhetId` (ikke `TjenesteId` — se §3 for hvorfor).

`KunnskapsbibliotekLenkeEntitet`:

| Felt | Type | Beskrivelse |
|---|---|---|
| `VirksomhetId` | `Guid`, påkrevd | Alltid virksomhetens eget arbeidsprodukt |
| `Url` | string, påkrevd | Validert med `Uri.TryCreate(..., UriKind.Absolute)` + http/https-sjekk |
| `Beskrivelse` | string?, valgfri | |

`KunnskapsbibliotekFilEntitet` (runde 2, PDF/Word):

| Felt | Type | Beskrivelse |
|---|---|---|
| `VirksomhetId` | `Guid`, påkrevd | |
| `Filnavn`, `Filtype` | string, påkrevd | `Filtype` er `"pdf"` \| `"docx"` |
| `Innhold` | `byte[]`, påkrevd | Rå fil-bytes som `bytea` i Postgres (**ikke** Azure Blob Storage — bevisst valg for å unngå en ny ekstern avhengighet; bytt senere hvis volum blir et reelt problem) |
| `UtvunnetTekst` | string, påkrevd | Allerede validert ikke-tomt av `KunnskapsbibliotekTekstUtvinner` FØR raden opprettes |

**Tekstlag-sjekken (`KunnskapsbibliotekTekstUtvinner.cs`) er bevisst IKKE ekte OCR**: den forsøker
vanlig tekstuttrekk (PdfPig for PDF, `DocumentFormat.OpenXml` for .docx) og avviser filen
(`InvalidOperationException`, tydelig norsk feilmelding) hvis resultatet er under en tegn-terskel —
typisk et rent skann uten tekstlag. Word-filer vil i praksis nesten aldri treffe avvisningen (et
.docx-dokument *er* strukturert tekst per definisjon); sjekken er reelt sett en PDF-sjekk, men samme
kodesti brukes for begge for konsistens. Ekte OCR (bilde-til-tekst) er fortsatt utenfor scope — hvis
en fremtidig runde faktisk trenger å lese skannede dokumenter, er det en egen, ikke-triviell
beslutning (f.eks. Azure AI Document Intelligence), ikke noe denne sjekken forsøker å dekke.

## 3. Hvorfor rettskilde (ikke Tjeneste, ikke dokument-opplasting) er kontekst-kilden i runde 1

Veikartets opprinnelige forslag («kunnskapsbibliotek sentrert rundt Tjeneste») forutsetter implisitt
at Tjenesten allerede finnes. Det holder for agenter som *anriker* en eksisterende Tjeneste
(Tjenestebeskrivelse, Håndbok), men **ikke** for en agent som skal identifisere *hvilke* Tjenester som
finnes i utgangspunktet. Runde 1s to agenter er derfor begge rettskilde-drevne:

- Rettskilder er allerede ekte, strukturert, importert tekst (`RettskildeNodeEntitet.Tekst` på
  ledd/punkt-nivå) — ingen parsing-/OCR-arbeid, i motsetning til et opplastet dokument.
- «Identifiser tjenester» supplerer med kunnskapsbibliotek-**lenker** (virksomhets-scopet, ikke
  Tjeneste-scopet) fordi en lov ikke sier noe om hvilke tjenester en spesifikk kommune organiserer —
  det gjør derimot virksomhetens egen nettside.
- «Identifiser begrep» bruker ikke kunnskapsbiblioteket i det hele tatt — SKOS-begreper hentes
  fullstendig fra rettskilde-tekst.

**Konsekvens for fremtidige agenter**: spør alltid "hva er den mest naturlige, allerede-strukturerte
kontekstkilden for DENNE spesifikke agenten" før du antar kunnskapsbiblioteket er riktig input — det
var det ikke for noen av de to agentene i runde 1.

## 4. Generalisering av `foreslatt_av_ai` — mønsteret å kopiere

For hver ny entitetstype en agent skal foreslå (Vilkår, Regelnode, …):

1. Legg `"foreslatt_av_ai"` til entitetens `GyldigeStatuser`-array i dens registertjeneste.
2. Legg til en `OpprettForslagFraKiAsync(...)` — kopi av den eksisterende `OpprettAsync`, men
   `Status = "foreslatt_av_ai"` og `ProveniensHjelper.NyForslagRad(...)` i stedet for `NyRad(...,
   "opprettet", ...)`.
3. Utvid `SettStatusAsync` med en valgfri `godkjentAv`-parameter (bakoverkompatibel) som setter
   `ProveniensEntitet.GodkjentAv` — allerede gjort for Begrep/Tjeneste, kopier mønsteret.
4. **Ikke** bygg en ny, generalisert kø-endepunkt/UI per type — hold ett kø-endepunkt + én side per
   artefakttype til minst tre-fire typer finnes og et reelt gjenbruksmønster er synlig i praksis
   (samme "ikke abstraher for tidlig"-prinsipp som resten av kodebasen).

## 5. Kjent, ikke-fikset kvirk (arvet, ikke innført av byggesteg 5)

`GET /api/rettskilder` uten `virksomhetId`-param returnerer delte/nasjonale kilder pluss **alle**
virksomheters publiserte lokale kilder, ikke strengt "egne + delte" for den innloggede virksomheten.
Presist nok for et system med i praksis én test-virksomhet, men bør rettes (endre
`RettskildeRepository.AlleRettskilderAsync`s filter til `r.VirksomhetId == null || r.VirksomhetId ==
virksomhetId`) før en fremtidig agent-runde med faktisk flere virksomheter i samme miljø.

## 6. Lovdata-katalog + søk (runde 2) — ikke en full lokal Lovdata-kopi

`Importer.tsx` krevde tidligere at brukeren allerede kjente den eksakte Lovdata-datokoden
(`LOV-1989-06-02-27`). Lovdatas gratis bulk-datasett (alle gjeldende lover + sentrale forskrifter) er
til sammen kun **~26 MB komprimert** — for lite til at "kun metadata vs. full lokal kopi" er en reell
lagringsavveining. Det som manglet var en søkbar katalog, ikke mer lokalt lagret tekst.

- `LovdataBulkHenter.HentAlleOppforingerAsync` itererer *alle* oppføringer i begge arkiv og trekker
  kun ut tittel (via `HtmlAgilityPack` — allerede en transitiv avhengighet gjennom
  `RegelIde.Kildekonvertering`, `//dd[@class='title']`, SAMME felt som
  `LovdataHtmlParser.ParseMetadata` bruker for `Tittel`) — **ikke** hele
  `LovdataKonverterer.Konverter`-pipelinen, som bygger et fullt AKN-tre og er unødvendig tung bare for
  en katalograd.
- `LovdataKatalogOppforingEntitet` (`Datokode` som primærnøkkel, `Tittel`, `Type`, `SistOppdatert`) er
  en GLOBAL tabell — ingen `VirksomhetId`, siden Lovdata-innholdet er nasjonalt/delt. Hele katalogen
  slettes og bygges på nytt (`LovdataKatalogTjeneste.SikreOppdatertKatalogAsync`) når den er tom eller
  eldre enn 24 timer, matcher Lovdatas egen nattlige oppdateringssyklus.
- `GET /api/lovdata-katalog/sok?q=...` — case-insensitivt `ILIKE`-ekvivalent søk (`.ToLower().Contains(...)`,
  portabelt på tvers av Postgres- og SQLite-profilen, se `Databaseoppsett.cs`) på `Tittel`. **Ingen
  endring i det eksisterende `POST /api/rettskilder/lovdata`** — frontend sender rett og slett
  datokoden brukeren valgte fra søkeresultatet dit, akkurat som før.
- Tester (`LovdataKatalogTjenesteTests.cs`) kjører ekte nettverkskall mot Lovdata, samme kultur som
  `LovdataBulkHenterTests` — katalogtabellen er delt på tvers av HELE testkjøringen (embedded
  Postgres, ingen virksomhet-scoping mulig), så hver test nullstiller tabellen eksplisitt selv i
  stedet for å anta en bestemt starttilstand.

## 7. Tjeneste-forslag mot CPSV-AP-NO (runde 3) — hva agenten dekker, og fire bevisste gap

Johann la fram CPSV-AP-NO-standardens felt-tabell (obligatorisk/anbefalt/valgfritt). Sjekket mot
`TjenesteEntitet`: skjemaet hadde ALLEREDE `KompetentMyndighet`/`Output`/`Tjenestetype`/`Malgruppe`/
`Kanaler[]`/`Kostnad`/`Behandlingstid`/`Kontaktpunkt`/`KonsekvensVedBrudd`/`Sprak[]` som nullable
felt — men `OpprettForslagFraKiAsync` satte kun `Tittel`+`Beskrivelse`. Runde 3 utvider
`TjenesteForslagJson`/`OpprettForslagFraKiAsync` til å sette alle disse — kun `Tittel` er
obligatorisk fra agenten, resten `null` hvis konteksten ikke gir tydelig belegg.

**Fire CPSV-AP-NO-konsepter er bevisst IKKE modellert i skjemaet i det hele tatt** (uavhengig av KI
— dette er ikke noe en bedre prompt kan løse, siden feltene ikke finnes å skrive til):

- `cpsv:hasParticipation` — organisasjon+rolle som egen struktur. I dag: `KompetentMyndighet` er kun
  fritekst, ingen kobling til en `OffentligOrganisasjon`-lignende entitet eller rolle-vokabular.
- `cpsv:hasInput` — dokumentasjonskrav som konsept. Finnes ikke i det hele tatt i dag.
- `dct:spatial` — geografisk område tjenesten er tilgjengelig i. Finnes ikke i det hele tatt i dag.
- `dct:requires`/`dct:hasPart` — presis skille mellom "avhengig av" og "sammensatt av" på
  `TjenesteavhengighetEntitet.Rel`. Nærmest i dag: `"avhengig_av"` ≈ requires, men ingen
  "har_del"/hasPart-verdi — `Rel`-enumet ble designet rundt Hendelse-utløste relasjoner (byggesteg 2),
  ikke CPSV sin komposisjons-/avhengighets-akse.

Bevisst utsatt til en egen, senere beslutning — ikke bygget i denne runden.

## 8. Runde 4 (2026-08-10) — to parallelle, avgrensede spor

Live-testing av runde 3 avdekket to ting samtidig: et reelt kost-/kvalitetsproblem
(`RettskildeKontekstHjelper` dumper ALL bladtekst + hele kunnskapsbiblioteket uten relevansfilter —
en kjøring med 6 rettskilder, ~49k input-tokens, ga et tomt `[]`-svar, mens mindre kontekst ga rike,
presise forslag), og to dokumenterte gap i «Identifiser tjenester» (agenten foreslo ikke relaterte
tjenester eller regelverksreferanser — se kommentaren som satt i `TjenesteregisterTjeneste.cs` før
denne runden). Johann valgte å angripe begge parallelt, avgrenset, tidsboksede — ikke en full
ombygging av noen av dem.

### 8.1 Spor A — Relaterte tjenester + regelverksreferanser (CPSV-agentutvidelse)

Bygger PÅ eksisterende, allerede-testede mekanismer (`TjenesteavhengighetregisterTjeneste.OpprettAsync`,
`TjenesteregisterTjeneste.KobleRegelverksreferanseAsync`) — kun agent-integrasjonen er ny.

**Referanseproblem løst med samme "ingen gjettet fallback"-prinsipp som eId-fiksen i runde 3**: en
KI-agent kan ikke oppfinne en `Guid`. Server-siden nummererer i stedet selv: eksisterende, gjeldende
tjenester for virksomheten listes i konteksten som `E1: <tittel>`, `E2: ...` (kun tittel, holder
kostnaden lav); de NYE forslagene i samme batch nummereres `T1`, `T2`, ... etter sin posisjon i
JSON-arrayet agenten selv returnerer. `TjenesteForslagJson` utvidet med
`RegelverksreferanserEid: IReadOnlyList<string>?` (eksakte `[eId]`-tagger, samme regel som
`LovreferanseEid`) og `RelatertTil: IReadOnlyList<RelatertTjenesteJson>?` der
`RelatertTjenesteJson(string Referanse, string Rel)`. Server-siden løser `T#`/`E#` til ekte Guid-er
via `TjenesteforslagTjeneste.LosReferanse` ETTER at alle tjenestene i batchen er opprettet (T#-
referanser kan peke på hverandre). Ukjent/uoppløselig referanse droppes stille (samme mønster som
`Hallusinert_eId_dropper_kun_den_referansen_ikke_hele_batchen` fra runde 3) — kastes aldri, resten av
batchen upåvirket.

**Ny `Rel`-verdi: `"har_del"`** i `TjenesteavhengighetregisterTjeneste.GyldigeRel` (nå `internal`,
gjenbrukt direkte i agentens system-instruks i stedet for en duplisert, driftbar liste) — dekker
`dct:hasPart`-siden av det fjerde CPSV-AP-NO-konseptet fra §7 spesifikt for agent-forslag. **Løser
kun at agenten kan sette riktig `Rel`-verdi** — ingen egen typed hasPart-struktur (rekkefølge/
komposisjon-semantikk); `dct:requires`-vs-`hasPart`-distinksjonen er dermed lettet, ikke fullt løst.

**Bonus-hygienefiks** (bundlet fordi filen allerede endres): `TjenesteavhengighetregisterTjeneste.
OpprettAsync` gjorde tidligere INGEN sykel-sjekk (kun selvreferanse+duplikat) — ny bounded BFS
(`LukkerSykelAsync`, fra `tilTjenesteId` over eksisterende kanter) gjelder nå både menneskedrevet UI
og agent-forslag.

**Ingen frontend-endring** — `TjenesteDetalj.tsx` hadde allerede full admin-UI for avhengigheter og
regelverksreferanser uavhengig av opprinnelse; de nye agent-koblingene vises der automatisk.

### 8.2 Spor B — RAG-spike (uten pgvector — bevisst valg)

**Hvorfor ikke pgvector**: krever et nytt NuGet-paket (`Pgvector`/`Pgvector.EntityFrameworkCore`,
ingen referert i dag) OG at Postgres-instansen har `CREATE EXTENSION vector` — embedded-
testinfrastrukturen (`MysticMind.PostgresEmbed`, HELE testsuiten) leverer ikke pgvector
forhåndskompilert. Å bytte testinfrastruktur midt i en spike, rett etter at issue #10/#16 brukte
betydelig innsats på å stabilisere nettopp embedded-Postgres-oppstarten, ble vurdert som en
unødvendig, selvpålagt risiko for en spike hvis eneste mål er å måle om retrieval hjelper.

**Valgt tilnærming**: ny, generisk `IEmbeddingKlient` (samme leverandøragnostiske mønster som
`IKiAgentKlient`/`KiAgentKlientOpenAiKompatibel` — egen `RegelIde:KiAgent:EmbeddingBaseUrl`/
`EmbeddingModell`-konfig, gjenbruker samme `ApiKey`) + `EmbeddingKlientOpenAiKompatibel` (poster til
`/v1/embeddings`, parser `data[0].embedding`) og en `EmbeddingKlientStub` (samme "bevis rørledningen
uten en ekte leverandør"-rolle som `KiAgentKlientStub`, deterministisk hash-basert bag-of-words-
vektor). Embeddings lagres som en vanlig Postgres `double precision[]`-kolonne
(`RettskildeNodeEmbeddingEntitet`, 1:1 med `RettskildeNodeEntitet` — samme "egen tabell, ikke
alltid-NULL-kolonner"-begrunnelse som `HandbokKommentarMetadataEntitet`) — Npgsql støtter native
array-typer uten noe ekstra pakke. Kosinuslikhet beregnes i ren C# (`RagKontekstHjelper.
KosinusLikhet`) ved henting — ingen DB-indeks, ingen extension. Skalerer fint for spikens formål (en
virksomhets kunnskapsbase er noen hundre noder, ikke millioner).

**Avgrenset til rettskilde-noder**: `RettskildeNodeEntitet` er allerede naturlig chunket (én rad per
ledd/punkt), så ingen ny chunking-logikk trengs. Kunnskapsbibliotek-lenker/filer (IKKE chunket —
`UtvunnetTekst` er hele dokumentet i én streng) holdes utenfor spiken og dumpes fortsatt fullt ut —
det var uansett rettskilde-nodene som dominerte de 49k tokens-en.

**Retrieval-anker**: «Identifiser tjenester» har et naturlig søkeanker «Identifiser begrep» ikke har
— kunnskapsbibliotekets lenke-beskrivelser + fil-titler ER definisjonen av "hva vi leter etter
tjenester for". `RettskildeEmbeddingTjeneste.SikreEmbeddingerAsync(rettskildeId)` sikrer lazy (kun
noder som mangler en embedding-rad, ingen bakgrunnsjobb); `RagKontekstHjelper.ByggKontekstAsync`
embedder spørsmålsteksten og returnerer de K mest like nodene, formatert IDENTISK med
`RettskildeKontekstHjelper`s `[eId] Tekst`-format. `TjenesteforslagTjeneste.KjorForslagMedRagAsync`
er en ALTERNATIV kjørevei (IKKE erstatter `KjorForslagAsync`) — begge deler nå all logikk etter
kontekst-byggingen via en ny privat `KjorForslagFraKontekstAsync`-helper, kun selve
rettskilde-kontekst-byggingen skiller de to.

**Sammenligning**: ingen automatisert scoring — kjør begge kontekst-byggerne mot samme testcase,
rapporter rått (tokens, KI-versjon, selve forslagene), bruker vurderer selv (samme
"vi får jobbe med hva fasit er"-tilnærming som resten av byggesteg 5).

### 8.3 Bifangst: `EmbeddedPostgresFixture`s `DisposeAsync` manglet en try/catch

Under Spor B-testing (nye tester som legger til rader i en delt embedded-Postgres-instans) begynte
`Test Collection Cleanup Failure`-feil (samme kjente `UnauthorizedAccessException`/`icudt*.dll`-
fillås som allerede var håndtert for `_server?.Stop()`, se `EmbeddedPostgresFixture.cs`) å blåse opp
det rapporterte test-totalt/feilantallet dramatisk (223 reelle tester rapportert som 408, med 188
falske "feil") — fordi `PgServer.Dispose()` SELV kaller `Stop()` internt, og KUN den ytre,
eksplisitte `_server?.Stop()`-kallet var beskyttet. Fikset med samme try/catch-mønster rundt
`_server?.Dispose()` også. Ren infrastrukturfiks, ikke del av selve RAG-spiken, men nødvendig for å
faktisk kunne verifisere den.

### 8.4 Rå sammenligning mot en ekte embeddings-leverandør (2026-08-10, etterkant) — billigere, men tynnere

Johann fant og delte HostYourAI sitt faktiske embeddings-endepunkt (`POST /api/v1/embeddings`,
modell `BAAI/bge-multilingual-gemma2` — flerspråklig, bedre egnet for norsk juridisk tekst enn en
ren engelsk modell). `RegelIde:KiAgent:EmbeddingBaseUrl`/`EmbeddingModell` satt via
`dotnet user-secrets` (gjenbruker eksisterende `ApiKey`), og en ny, midlertidig
sammenligningsvei ble lagt til: `POST /api/tjenester/forslag/kjor-rag` (mirror av `/forslag/kjor`,
kaller `KjorForslagMedRagAsync` — ingen frontend-kobling, kun til denne sammenligningen).

**Første forsøk mot alkoholloven alene (~276 noder med tekst) traff `429 Too Many Requests`** —
`RettskildeEmbeddingTjeneste.SikreEmbeddingerAsync` kalte den gang embeddings-API-et ÉN GANG PER
NODE, sekvensielt, uten batching/backoff. Fikset (se §8.4.1 under: batching + retry-med-backoff),
og sammenligningen ble fullført på nytt mot SAMME rettskilde:

| Kjørevei | Input-tokens | Forslag | Snitt feltfullstendighet | Kommentar |
|---|---|---|---|---|
| Dump-alt (`/forslag/kjor`), kjøring 1 | 28 624–28 830 | 15 forslag | **83 %** | |
| Dump-alt (`/forslag/kjor`), kjøring 2 (samme kontekst) | 28 830 | **0** (tomt `[]`) | — | Samme rettskilde, samme kontekst-tekst — modellens EGEN sampling-variasjon ved denne kontekststørrelsen, ikke en kodeendring |
| RAG (`/forslag/kjor-rag`), K=20 | 2 808 | 0 (tomt `[]`) | — | 90 % færre tokens, men også tomt denne gangen |
| RAG (`/forslag/kjor-rag`), K=40 | **4 479** | 6 forslag | **28 %** | 84 % færre tokens enn dump-alt, IKKE tomt — men vesentlig tynnere per forslag, se under |

**Første lesning var for optimistisk.** Dump-alt på denne rettskilden er selv ustabil ved ~28-29k
tokens — samme kontekst gir noen ganger et rikt svar, noen ganger et tomt — og RAG med K=40 ga et
IKKE-tomt svar med 84 % færre tokens, som først ble lest som en klar seier for RAG. Men en faktisk
innholdssammenligning av de 15 dump-alt-forslagene mot de 6 RAG-forslagene (begge satt sammen i en
egen artefakt for gjennomgang, se lenke i chat-historikken for denne runden) viser et mer blandet
bilde:

- **Feltfullstendighet falt fra 83 % til 28 %** (snitt andel utfylte sekundærfelt —
  `kompetentMyndighet`/`kanaler`/`kostnad`/`behandlingstid`/`kontaktpunkt`/`konsekvensVedBrudd`/
  `sprak`/`output`/`malgruppe` — av 9). RAG-forslagene har nesten konsekvent tomme
  `kanaler`/`kostnad`/`behandlingstid`/`kontaktpunkt`/`sprak`, felt dump-alt fylte ut i 13 av 15
  forslag.
- **2 av 6 RAG-forslag er dubletter** av tjenester dump-alt allerede fant (`Tilvirkningsbevilling`,
  `Statlig skjenkebevilling`) — men med færre felt utfylt enn dump-alts versjon av samme tjeneste.
- **3 av 6 er tvilsomme som egne CPSV-tjenester**, ikke bare tynt utfylt: en beskrivelse av
  saksbehandlingen til en tjeneste som allerede fantes (`Søknad om skjenkebevilling med høringer`),
  en tilsynsplikt for myndigheten (`Tilsyn med privat innførsel...`), og en regulering/forbud uten
  noe en bruker søker om (`Forbud mot skjenking utenfor lokaler`).
- Netto reelt nye, velformede tjenester fra RAG-batchen: trolig **1**
  (`Klage på vedtak om begrensning av tilgang til nettbasert grensesnitt` — et ekte nisje-funn
  dump-alt ikke fant), ikke 6.

**Riktigere konklusjon**: K=40 var stor nok til å senke kostnaden dramatisk og unngå et tomt svar,
men IKKE stor/riktig nok til å gi modellen samme grunnlag til å fylle ut hele CPSV-skjemaet eller
til å unngå å plukke opp fragmenter som ikke er egne tjenester. Om det er K som må opp enda mer,
en prompt-justering som presiserer at sekundærfelt skal fylles fra det agenten faktisk ser (ikke
kun tittel/beskrivelse), eller en reell grense for hvor godt et lite, retrieval-utvalgt
kontekst-utdrag kan bære et fullt strukturert skjema — er IKKE undersøkt denne runden. Datagrunnlaget
er dessuten fortsatt tynt (én rettskilde, én kjøring per K-verdi, ingen automatisert scoring) — dette
er et første, kvalitativt funn, ikke et statistisk grunnlag for en anbefaling.

#### 8.4.1 Fiksen: batching + retry-med-backoff

`IEmbeddingKlient.EmbedAsync` endret fra å ta én streng til `IReadOnlyList<string>` (batching —
standard OpenAI `input`-som-array-format, ikke leverandørspesifikt). `RettskildeEmbeddingTjeneste`
batcher nå 16 noder per kall i stedet for ett kall per node. `EmbeddingKlientOpenAiKompatibel` har
fått enkel retry-med-backoff (maks 3 forsøk, doblende forsinkelse 300ms/600ms) spesifikt på `429`.
Løste det observerte rate-limit-problemet fullstendig — alkoholloven (~276 noder) embeddes nå uten
en eneste 429 på nytt forsøk.

### 8.5 Spor C (foreslått, IKKE bygget) — generer → forankre → verifiser

Forslag fra Johann etter §8.4-funnet, notert her som et ferdig skissert design — ikke bygget, ikke
tatt stilling til om det skal bygges. Snur rekkefølgen fra §8: i stedet for at retrieval bestemmer
HVA agenten får se før den foreslår noe, foreslår agenten FØRST, og retrieval brukes etterpå til å
forankre/verifisere hvert enkelt forslag mot lovteksten.

**Hvorfor dette adresserer §8.4-funnet direkte**: dagens RAG-svakhet (feltfullstendighet 83 % → 28 %)
kommer av at ÉN generisk spørsmålsvektor (kunnskapsbibliotek-teksten) skal dekke ALLE forslagene på
én gang — en node med gebyr-/behandlingstid-informasjon for "Salgsbevilling gruppe 1" konkurrerer om
de samme K plassene som noder relevante for "Statlig skjenkebevilling", uten at den generiske
forespørselen har grunn til å prioritere riktig. Med per-forslag forankring får hvert forslag sin
EGEN, presise spørsmålsvektor (forslagets egen tittel+beskrivelse) — retrieval blir presist per
kandidat i stedet for én kompromiss-pool delt på alle. Samme mekanisme gir også et konkret signal for
å luke ut de forslagene som ikke er egne tjenester i det hele tatt (§8.4 fant 3 av 6 tvilsomme) — hvis
forankringssteget ikke finner lovtekst som faktisk støtter kandidaten, droppes den, samme
"ingen gjettet fallback"-prinsipp som allerede brukes for hallusinerte eId-referanser.

**Tre steg:**

1. **Genereringssteg (billig)** — agenten foreslår kandidat-tjenester (kun tittel + kort beskrivelse,
   ingen CPSV-sekundærfelt ennå) fra en MYE mindre kontekst enn dagens dump-alt: paragraf-/
   kapittel-OVERSKRIFTER (`RettskildeNodeEntitet` med `NodeType='paragraf'`/`'kapittel'`, ikke hvert
   ledd/punkt), pluss kunnskapsbiblioteket. Få hundre til et par tusen tokens — modellen bruker sin
   egen forståelse av hva en tilsvarende lov "pleier" å strukturere tjenester rundt, forankret i
   lovens EGNE overskrifter, ikke løsrevet fra teksten.
2. **Forankringssteg (per kandidat)** — embed hver kandidats tittel+beskrivelse som EGET spørsmål,
   hent de K mest like nodene for AKKURAT den kandidaten via `RagKontekstHjelper` (gjenbrukt
   uendret — kun kalt N ganger med ulikt spørsmål, i stedet for én gang med ett delt spørsmål).
3. **Verifiseringssteg (per kandidat, eller batchet)** — gi agenten kandidaten + dens egen forankrede
   utdrag, be den enten (a) bekrefte og fylle CPSV-sekundærfeltene FRA det utdraget, med
   `RegelverksreferanserEid` kun hentet fra det den faktisk fikk se (reduserer hallusinasjonsrisiko
   siden vinduet er lite), eller (b) forkaste kandidaten hvis lovteksten ikke støtter den som egen
   tjeneste.

**Kostnadsbildet — presisering av en antakelse som ikke stemmer helt**: spørsmåls-embeddinger ER
billigere per kall enn å embedde opp hele kunnskapsdokumentet (korte tekster, og
dokumentembeddingen er uansett et engangskost som allerede caches av `RettskildeEmbeddingTjeneste`
— null marginalkost etter første kjøring). MEN dette designet gjør FLERE spørsmåls-embeddinger per
kjøring enn dagens RAG (N per kjøring, ett per kandidat — mot 1 i dag), og disse cacher IKKE på
samme måte siden kandidat-ordlyden er fersk hver kjøring. Den reelle kostnadsdriveren er likevel
IKKE embeddings i det hele tatt — det er **steg 3s N verifiseringskall til chat-modellen**
(embeddings-modeller er typisk vesentlig billigere per token enn chat-completion-modeller, og steg 3
bruker chat, ikke embeddings). Design C bytter altså kostnad mot presisjon — det er trolig DYRERE
totalt enn dagens RAG (flere KI-kall), ikke billigere, selv om hvert enkelt kall er lite.

**Åpne spørsmål, ikke besvart av skissen alene:**
- Steg 1 med kun overskrifter kan fortsatt hallusinere kandidater overskriftene ikke faktisk
  underbygger — steg 3 skal luke dette ut, men terskelen for "godt nok forankret" (hvor lav
  kosinuslikhet er for lav?) er ikke definert.
- N verifiseringskall (15+ for en middels rettskilde) trenger sannsynligvis samme
  batching/rate-limit-hensyn som embeddings-kallene fikk i §8.4.1 — ikke undersøkt for
  chat-completion-kall.
- Om steg 3 bør være ett kall per kandidat eller ett batchet kall med alle kandidatene + deres
  respektive utdrag er ikke avgjort — batchet er billigere (færre kall), men risikerer at modellen
  blander sammen hvilket utdrag som hører til hvilken kandidat.
- Ingen vurdering av om steg 1 i det hele tatt trenger RAG/embeddings — det bruker kun
  paragraf-overskrifter, ikke similaritetssøk.

### 8.6 Presisering: er chunkingen riktig for RAG? (2026-08-11, etterkant)

Uavhengig av Spor C over — gjelder like mye dagens §8.4-design (delt spørsmål) som en eventuell
Spor C (per-kandidat spørsmål), siden begge gjenbruker `RagKontekstHjelper` og samme underliggende
chunking uendret. Johann fikk en ekstern CoPilot-analyse om chunking-strategi for rettskilder, som
sammenlignet mot koden ga to konkrete, presise funn og ett genuint nytt forslag (reranking).

**Det CoPilot advarer mot er ikke noe vi gjorde.** Rådet "kutt ikke blindt hver 500/1000 tokens" er
myntet på systemer som chunker FRA GRUNNEN med et arbitrært tokenvindu. `RettskildeNodeEntitet` er
allerede strukturbasert (én rad per ledd/punkt, satt i byggesteg 1 — lenge før RAG-spiken), ikke et
tokenvindu. Det CoPilot kaller "Nivå 1: ett lovledd per chunk med metadata (lov/paragraf/ledd)" er
nesten ordrett det vi allerede har: `Eid` (kanonisk id, koder lov+paragraf+ledd-posisjon),
`RettskildeId`, `ParentNodeId`, `NodeType`, `Nummer` — se `RettskildeNodeEntitet`
(`src/RegelIde.Data/Entiteter.cs`). Metadataen finnes; den er bare ikke koblet inn i selve RAG-
hentingen (`RagKontekstHjelper` gjør i dag ren vektorsimilaritet, ingen bruk av det som allerede er
lagret der).

**To konkrete, ikke-bygde svakheter — bekreftet i koden, ikke bare antatt:**

1. **Chunk-teksten som embeddes har ingen anelse om hva den hører til.**
   `RettskildeEmbeddingTjeneste.SikreEmbeddingerAsync` embedder `n.Tekst!` alene —
   se `src/RegelIde.Data/RettskildeEmbeddingTjeneste.cs` linje 40. Et ledd som bare sier "gebyret
   skal betales innen 14 dager" embeddes UTEN sin paragrafs `Overskrift`/`Nummer` (som ligger på
   FORELDRE-noden via `ParentNodeId`, aldri på selve ledd-noden) — vektoren har null signal om
   hvilken tjeneste/paragraf leddet faktisk hører til.
2. **Chunking på ledd/punkt-nivå fragmenterer det dump-alt ser som én sammenhengende enhet.**
   Én paragraf definerer typisk én tjeneste over flere ledd (hvem kan søke → gebyr → frist).
   Dump-alt ser alle ledd i paragrafen samlet; RAG rangerer HVERT ledd separat — "hvem kan søke"-
   leddet er ofte semantisk nærmest en tjeneste-tittel-spørring, mens gebyr-/frist-leddet i SAMME
   paragraf kan rangere langt lavere og falle utenfor K. Dette er en plausibel mekanisme for akkurat
   de feltene som ble tomme i §8.4 (`kanaler`/`kostnad`/`behandlingstid`/`kontaktpunkt` er nettopp
   den typen prosedyre-detalj som ofte står i en paragrafs SENERE ledd).

CoPilots "Nivå 2: overlappende paragraf-chunks (naboledd)" er samme mekanisme som løsningsforslaget
i punkt 2 — en uavhengig bekreftelse av at paragraf-fragmentering trolig er en reell årsak, ikke bare
en teori.

**Der rådet ikke er direkte anvendbart ennå, ikke fordi det er feil:** forarbeider-/dommer-spesifikk
chunking er irrelevant i praksis — Presedensregisteret (byggesteg 3, som ville huset dommer/
forarbeider) er ikke bygget. Vi har kun Lov/Forskrift i systemet i dag.

**Der rådets tommelfingerregel bør presiseres for DENNE kodebasen:** CoPilots token-størrelse-
anbefaling (min. 200-300, ideelt 500-1200 tokens) er tenkt for arbitrær vindu-chunking. Å tvinge
VÅRE chunks opp mot et minimum ved å slå sammen korte ledd ville undergrave noe verdifullt vi
allerede har med vilje: eksakt eId-siterbarhet (et kort ledd på 20 ord er en presis, siterbar enhet,
ikke en feil å fikse). Utvidelse til naboledd/hele paragrafen bør derfor legges PÅ TOPPEN av dagens
leaf-presisjon ved HENTING (hvilken tekst som til slutt vises til agenten), ikke erstatte selve
lagrings-/siterings-enheten.

**Tre ikke-bygde fikser, lagdelt billigst → dyrest:**

1. **Fold forelder-kontekst inn i teksten som EMBEDDES** (kapittel-/paragraf-`Overskrift`+`Nummer`
   foran `node.Tekst` når vektoren beregnes) — men behold `node.Tekst` uendret i det som til slutt
   VISES til agenten. Ingen skjemaendring; ett metode-endring i `RettskildeEmbeddingTjeneste`
   (må slå opp forelder-noden(e) via `ParentNodeId` før `EmbedAsync`-kallet).
2. **Utvid til søskenledd/hele paragrafen ved HENTING, ikke ved lagring** — når en node treffer
   top-K, ta med dens søsken-ledd/punkt under samme paragraf i konteksten som faktisk sendes til
   agenten. Gjenbruker eksisterende, allerede lagrede vektorer uendret — ren endring i
   `RagKontekstHjelper.ByggKontekstAsync`s siste steg (fra "kun de valgte nodene" til "de valgte
   nodene + deres søsken").
3. **Reranking-steg** (CoPilots forslag, genuint nytt — ikke tidligere vurdert i denne runden): hent
   et bredere vektorsøk (f.eks. N=60 i stedet for K=40), la en dedikert reranker-modell ELLER selve
   chat-modellen (billig klassifiseringsprompt: "er dette utdraget relevant for kandidaten X, ja/
   nei") score kandidatene på nytt før endelig K velges. Ny modell-/prompt-avhengighet — størst
   kostnad/kompleksitet av de tre, og bør trolig prøves ETTER 1 og 2, ikke i stedet for dem.

**Metadata-prefilter** (CoPilots fjerde punkt) er allerede delvis der i praksis — `RagKontekstHjelper`
er allerede scopet til kun de valgte `rettskildeIder`, altså ETT nivå av metadata-filter før
vektorsøket kjører. En finere prefilter (per kapittel/paragraf) er ikke en tydelig gevinst før
korpuset faktisk spenner over flere rettskildetyper samtidig (lov+forskrift+forarbeider+dommer) —
CoPilots ramme forutsetter et større, mer heterogent korpus enn det RegelIde har i dag.

Ingen av de tre fiksene er bygget. Rangert som neste steg FØR et eventuelt Spor C, siden begge
retter seg mot samme underliggende chunking `RagKontekstHjelper` deler med Spor C uendret.

### 8.7 Ikke gjort i denne runden

Se `docs/13-backlog.md` §2.2 og `docs/06-veikart.md` byggesteg 5 for den fulle, delte listen over
utsatte punkter fra begge spor (holdes IKKE duplisert her for å unngå drift mellom de tre dokumentene).
