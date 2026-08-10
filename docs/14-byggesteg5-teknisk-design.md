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
