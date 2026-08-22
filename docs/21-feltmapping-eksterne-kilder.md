# Feltmapping: eksterne kilder → domenemodellen

## 0. Hensikt og status

Johann spurte (2026-08-22): *"vi må vel ha en mappingdefinisjon for hver importkilde. Du har vel
tidligere gjort en analyse på at modellen vår dekker alt i oppgaveregisteret?"*

**Svar: nei.** `OppgaveregisterHandlingSeed`s klassekommentar dokumenterer HVORDAN de feltene den
faktisk bruker mappes (og hvorfor), men det var aldri en fullstendig gjennomgang av HELE skjemaets
JSON-form mot applikasjonens datamodell — kun de feltene som var strengt nødvendige for den narrow
oppgaven (eier-matching, lovhjemmel-matching, én type-klassifisering) ble faktisk lest og vurdert.

Dette dokumentet er den FØRSTE fullstendige feltmappingen, og etablerer samtidig malen alle
fremtidige eksterne kilder (Altinn Ressurser, Altinn Skjemaoversikt, Statsforvalter-/fylkeskommune-
tjenestelistene — se §3) bør dokumenteres etter: **kildeformat → hvert felt → mappes til / mappes
IKKE til, med begrunnelse**. Ingen kilde skal "anta" full dekning uten at det er skrevet ned.

## 1. Oppgaveregisteret (Brønnøysundregistrene)

**Kilde**: `https://data.brreg.no/oppgaveregisteret/api/skjema/alle.json` — ett bulk-endepunkt, 903
skjemaer (2026-08-22), ingen paginering, ingen autentisering. Høstes rått av
[`OppgaveregisterHenter`](../src/RegelIde.Data/OppgaveregisterHenter.cs) inn i `EksternKildeEntitet.RaaJson`
— domenekoblingen skjer i et eget, separat steg,
[`OppgaveregisterHandlingSeed`](../src/RegelIde.Data/OppgaveregisterHandlingSeed.cs), som er det
denne mappingen dokumenterer.

Prosentandelene under er andelen av alle 903 skjemaer der feltet faktisk er utfylt (ikke `null`/tom
liste) — empirisk talt mot hele det live datasettet, ikke gjettet.

### 1.1 Mappes til domenemodellen

| Oppgaveregister-felt | Forekomst | Mappes til | Kommentar |
|---|---|---|---|
| `navn` | 903/903 (100 %) | `Handling.Navn` | Direkte. |
| `eier.organisasjonsnummer` | 903/903 (100 %) | Matching-nøkkel → `Virksomhet.Id` (via `Virksomhet.Organisasjonsnummer`, eksakt match) | Ingen treff ⇒ skjemaet hoppes over i sin helhet (se §1.2). |
| `guid` | 903/903 (100 %) | `EksternKildeEntitet.EksternId` (høstelaget) → `Handling.EksternKildeId` | Idempotens-nøkkelen — IKKE skjemaets `navn`, som kan endre seg mellom to høstinger. |
| `formaal.fritekst` | 903/903 (100 %) | `Handling.Merknad` | Direkte. |
| `bruksomraader[0].navn` | 903/903 (100 %) | `Handling.Handlingstype` (via `BruksomraadeKode`/`HandlingstypeForBruksomraade`) | Kun de tre kjente verdiene («Periodisk rapportering»/«Hendelsesrapportering»/«Søknad / registrering») — en fjerde, ukjent verdi ville gitt `"annet"`, ikke kastet. Kun FØRSTE bruksområde brukes (876/903 har akkurat ett; 27 har to — se §1.2). |
| `lovhjemler[].dato` (+ nøstede `forskrifter[].dato`) | 903/903 (100 %) | Matching-nøkkel → `Rettskilde.Eli` (via `LovdataIdentifikatorer.AvledEliFraDatokode`, eksakt streng-match) | Kun DOKUMENTNIVÅ — se §1.2. Lagres som `HandlingRegelverksreferanseEntitet.TilEid` = rettskildens egen Eli. |
| (implisitt: alltid `soker`) | — | `Handling.UtfortAv = "soker"` | Hardkodet konstant, ikke lest fra noe felt — Oppgaveregisteret har ikke noe tilsvarende felt (det er alltid en INNSENDING fra en virksomhet TIL myndigheten). |

### 1.2 Bevisste forenklinger (dokumentert i koden, ikke tilfeldige hull)

- **`eier.organisasjonsnummer` uten treff** → skjemaet hoppes over i sin helhet, telles i
  `HoppetOverUsikkerVirksomhet`. Ingen fuzzy-navnematch på `eier.etatsnavn` (se §1.3).
- **`lovhjemler[].henvisning`** (84 % utfylt — fritekst: `"§ 42"`, `"§§ 21-4, 22-3, ..."`,
  `"Kapittel 5"`, `"Kap 14 Del I Svangerskapspenger §14-4, annet ledd. Del II §14-11 - §14-16"`, osv.)
  brukes IKKE til å slå opp en spesifikk `RettskildeNode`/paragraf-eId — for ustrukturert til å tolke
  uten å gjette (samme "ingen gjettet fallback"-prinsipp som resten av kodebasen). Referansen lagres
  derfor kun på DOKUMENT-nivå.
- **To bruksområder på samme skjema** (27/903 — 3 %) → kun det FØRSTE brukes, det andre forkastes
  stille (`Handling.Bruksomraade` er ett enkelt felt, ikke en liste).
- **Manglende rettskilde-treff** (loven/forskriften ikke importert i DENNE instansen ennå) → telles i
  `RettskildematcherIkkeFunnet`, ikke en feil.

### 1.3 Mappes IKKE (funnet i denne gjennomgangen, ikke tidligere dokumentert)

| Oppgaveregister-felt | Forekomst | Hvorfor ikke | Reelt mappingpotensial? |
|---|---|---|---|
| `eier.etatsnavn` | 903/903 | **Deserialisert i `SkjemaEierJson`, men aldri faktisk lest** — den aggregerte Tjenestens tittel bruker `Virksomhet.Navn` fra EGEN database, ikke dette feltet. Reint dødt felt i dagens kode. | Lavt — vårt eget navn er allerede den riktige kilden. |
| `lovhjemler[].tittel` | 903/903 | Lovens/forskriftens EGEN tittel fra Oppgaveregisteret selv (f.eks. "Lov om advokater og andre som yter rettslig bistand") — ikke lest, kun `dato`/`henvisning`. | Lavt — vi henter allerede rettskildens tittel fra vår egen `Rettskilde`-tabell ved treff. Nyttig KUN som visningstekst ved ikke-treff (§1.2 sistnevnte). |
| `lovhjemler[].henvisning` (fritekst) | 759/903 (84 %) | Se §1.2 — for ustrukturert til paragraf-oppslag. | **Middels** — selv om den ikke kan LØSES til en eId, kunne selve fritekst-strengen lagres et sted (f.eks. en ny nullbar kolonne på `HandlingRegelverksreferanseEntitet`) som en menneskelesbar «§ 42»-antydning, uten å late som den er en strukturert referanse. |
| `vedleggskrav.fritekst` / `.kategorier` | 232/903 (26 %) / 249/903 (28 %) | Ikke lest i det hele tatt. | **Høyt** — `Handling.Vedlegg` er et EKSISTERENDE, dedikert felt (`HandlingVedleggInput[]`) nettopp for dette. Reell, ubrukt mappingmulighet. |
| `medium.kode`/`.verdi` (ELEKTRONISK/PAPIR/BEGGEDELER) | 903/903 | Ikke lest. | **Høyt** — `Handling.Kanaler` (`HandlingKanalInput[]`) er laget for nettopp «hvilken kanal»-informasjon. |
| `bruksomraader[].tidsfrister[]` (`{date, month}`) | 221/903 (24 %) | Ikke lest. | **Middels** — `Handling.Behandlingstid.Frist` er en fritekststreng; en årlig tidsfrist («30.04») ville passet naturlig der, men krever en liten formateringsbeslutning (flere tidsfrister per skjema er mulig). |
| `tidsbruk.{elektronisk,papir,antallPrAar,prosentandelPapir}` | 903/903 | Ikke lest. | Lavt/tvilsomt — semantisk ANNET enn `Behandlingstid` (dette er hvor lang tid DEN SOM FYLLER UT skjemaet bruker, ikke myndighetens saksbehandlingstid). Ville krevd et nytt felt, ikke gjenbruk av et eksisterende, for å ikke blande sammen to ulike konsepter. |
| `statustype` (PUBLISERT/…) | 903/903 | Ikke lest. | Lavt — `Handling.Status` styres av VÅR egen arbeidsflyt (utkast→publisert→…), betyr noe annet enn Oppgaveregisterets eget publiseringsstatus-felt for SKJEMAET. Å blande disse ville vært misvisende, ikke en reell forenkling. |
| `godkjenningsdato` | 903/903 | Ikke lest. | Lavt — ingen tilsvarende «godkjent dato»-felt på `Handling` i dag; ville krevd et nytt felt for lav antatt nytte. |
| `formaal.kategorier[]` (kode+verdi) | 903/903 | Ikke lest (kun `.fritekst`). | Lavt/middels — en strukturert kategori-kode i tillegg til fritekst kunne vært en fremtidig `Kodeliste`-kobling, men ingen umiddelbar bruker etterspør det ennå. |
| `eoesTilpasset` | 868/903 (96 %) | Ikke lest. | Lavt — nisjefelt, ingen tilsvarende modellering finnes. |
| `maalgruppe.*` (næringsgrupper/antall/osv.) | 903/903 | Ikke lest. | Middels — `Tjeneste.Malgruppe` er et eksisterende felt, men på TJENESTE-nivå, ikke Handling; den aggregerte plassholder-Tjenesten ("Innsendte skjemaer — X") passer uansett ikke naturlig til én bestemt målgruppe siden den samler ALLE virksomhetens skjemaer. Ville kreve at Handling-modellen selv fikk en Malgruppe, eller at skjemaer faktisk ble gruppert i egne Tjenester (§1.4). |
| `spraakMaalformer[]` | 892/903 (99 %) | Ikke lest. | Lavt/middels — `Tjeneste.Sprak` finnes, samme "feil nivå"-begrunnelse som Malgruppe over. |
| `nettadresser[]` | 807/903 (89 %) | Ikke lest. | Middels — kunne vært en kanal-adresse i `Handling.Kanaler` (`{kanal: "elektronisk", adresse: url}}`), samme felt som medium-mappingen over ville brukt. |
| `rapporteringsformer[]` | 903/903 | Ikke lest. | Lavt — ingen tilsvarende modellering, uklar nytteverdi. |
| `skjemainnhold[]` / `skjemainnholdAndreOpplysninger` | 903/903 / 367/903 (41 %) | Ikke lest. | Lavt — beskriver innholdet I skjemaet (hva man fyller ut), ikke noe eksisterende Handling-felt dekker naturlig. |
| `datakilder[]` | 258/903 (29 %) | Ikke lest. | Lavt — nisje, uklar nytteverdi i dagens modell. |
| `nummer` | 622/903 (69 %) | Ikke lest. | Ingen — Oppgaveregisterets eget sekvensnummer, ingen ekvivalent trengs (vi har `guid` som stabil nøkkel). |
| `links[]` (self-lenke) | 903/903 | Ikke lest. | Ingen — kun API-navigasjon, ikke domenedata. |

### 1.4 Kjent, dokumentert modellbegrensning (ikke et felt-mappingproblem)

Oppgaveregisteret gir INGEN «hvilken tjeneste»-gruppering — det er en flat skjemaliste, ikke en
tjenestekatalog. Alle en virksomhets skjemaer samles derfor i ÉN aggregert plassholder-`Tjeneste`
("Innsendte skjemaer — {virksomhet}") — se `OppgaveregisterHandlingSeed`s klassekommentar punkt (b)
og §2 under for navngivning/videre arbeid. Dette er en MODELLERINGS-beslutning, ikke et
felt-som-mangler-mapping — Oppgaveregisteret har simpelthen ikke informasjonen som ville trengtes
for en finere gruppering.

## 2. Videre arbeid (ikke gjort i denne runden)

Følgende ble identifisert som reelle, ikke-implementerte mappingmuligheter i §1.3 (i fallende
prioritet basert på "høyt/middels" i tabellen over): `vedleggskrav` → `Handling.Vedlegg`,
`medium`/`nettadresser` → `Handling.Kanaler`, `bruksomraader[].tidsfrister` → `Handling.Behandlingstid`,
og et fritekst-lagret `lovhjemler[].henvisning` som menneskelesbar paragraf-antydning (uten å late
som det er en løst referanse). Ingen av disse er implementert — kun identifisert her, som svar på
"dekker modellen alt". **Konklusjon: nei, modellen dekker foreløpig kun kjerneidentitet (navn, eier,
type, dokumentnivå-hjemmel) — en god del strukturert, potensielt nyttig informasjon i kildens egen
JSON-form flyter fortsatt kun gjennom uendret i `EksternKildeEntitet.RaaJson`, uthentet den dagen
noen faktisk trenger det.**

## 3. Andre eksterne kilder — status (ikke fullt gjennomgått i denne runden)

Disse har alle et eget, fungerende HØSTE-lag (rå JSON → `EksternKildeEntitet`), men **ingen egen
domenekoblings-seed** ennå — altså samme situasjon Oppgaveregisteret var i FØR
`OppgaveregisterHandlingSeed` ble bygget. Ingen feltmapping finnes for dem ennå; nevnt her kun for å
gjøre statusen synlig, ikke som en komplett gjennomgang:

- **Altinn Ressurser** ([`AltinnRessursHenter`](../src/RegelIde.Data/AltinnRessursHenter.cs)) — rå høsting, ingen kobling.
- **Altinn Skjemaoversikt** ([`AltinnSkjemaoversiktHenter`](../src/RegelIde.Data/AltinnSkjemaoversiktHenter.cs)) — rå høsting, ingen kobling.
- **Statsforvalter-/fylkeskommune-tjenestelister** ([`TjenestelisteImporter`](../src/RegelIde.Data/TjenestelisteImporter.cs)) — rå høsting (delt kode for to strukturelt like kilder), ingen kobling.

Når/hvis noen av disse skal kobles inn i domenemodellen (samme mønster som Oppgaveregisteret),
**skriv en tilsvarende seksjon i dette dokumentet FØR koden skrives** — det var nettopp mangelen på
en slik skriftlig mapping som gjorde at spørsmålet "dekker vi alt?" ikke kunne besvares uten denne
etterpåkommende gjennomgangen.
