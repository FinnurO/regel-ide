# Arbeidsregler for regel-ide

Reglene under er skrevet fordi de er brutt i praksis. Hver av dem har en konkret hendelse bak seg.
Kodebasen er tungt kommentert og `docs/` har 30+ dokumenter — dette er ikke en oversikt over dem, det
er en liste over de tingene som faktisk går galt.

## 0. Formålet — bær det inn i hver runde

**Loven er ustrukturert og ikke maskinlesbar. Hele formålet er å gjøre den maskinlesbar, uten å gjette.**
Forvaltningsapparatet — hvem som forvalter en lov, fatter vedtak, er klageinstans, fører tilsyn, blir
berørt — står i lovteksten, men ingen kan lese hele korpuset, så kunnskapen lever på nettsider istedenfor
i kilden. Regel-IDE gjør apparatet spørrbart ved å strukturere det som alt står der.

Spørsmålene modellen skal kunne besvare står i `docs/32-formal-roller-og-sporsmal.md` §3 (S1–S7).
**Før en byggerunde: navngi hvilket av dem leveransen flytter, og for hvilken rolle** (modelløren først,
Johann 2026-09-08). Kan det ikke besvares, er oppgaven ikke forstått ennå. Akseptansekriterier
formuleres som spørsmål modellen skal kunne besvare etterpå — ikke «feltet finnes» eller «siden viser X».

To konsekvenser som har vært brutt gjentatte ganger:

- **En tagg er sporbarhetsleddet mellom tekst og modell**, ikke en markering for at noe skal se pent ut.
  Kriteriet for om noe er verdt å tagge er om taggen bidrar til å besvare et av S1–S7 — ikke om ordet er
  et egennavn. «Karasjok» i forskrift om samiske språk binder kommunen til en gruppe med plikter;
  «Karasjok» i en fredningsforskrift er bare et stedsnavn.
- **Loven definerer, registeret beskriver** (Johann 2026-09-08): «Virksomhet, org.nummer og brreg er
  strengt tatt bare attributter på det som er definert av lov.» Det teksten sier, er navneformen. Pek
  alltid koblinger på navneformen, aldri på virksomhetens registernavn — registernavnet er avledet.

## 1. Avklar omfang FØR bygging — og oppgi prisen

Legg fram formål og akseptansekriterier før du skriver kode. Johann vil se dem før koding starter, ikke
før merge.

**Prissett alternativene når du legger dem fram.** Et alternativ som er tre strenger i en JSON-fil og et
alternativ som er migrasjon + ny ekstern integrasjon + endringer i femten frontend-filer skal ikke
presenteres side om side uten at forskjellen er synlig. Uten prisen kan ikke Johann velge.

Brutt 2026-09-08 (registernavn-runden): tre virksomhetsnavn med feil kasus ble presentert som tre
arkitekturvalg. Ingen av dem nevnte at den minimale varige rettingen var å endre tre `navn`-verdier i
`src/RegelIde.Data/Seed/organisasjoner-norge.json`.

**Si det høyt når omfanget vokser.** Når en oppgave har blitt N endringer, si «dette er nå N endringer,
skal vi lande dem hver for seg?» istedenfor å fortsette.

## 2. Les naboskapet før du foreslår retning

Finn ut om mønsteret alt finnes i repoet før du spør Johann om arkitektur. De fleste designspørsmål her
er alt besvart et sted i koden eller i `docs/`.

Brutt samme runde: `POST /api/virksomheter/fra-brreg` i `src/RegelIde.Api/Program.cs` implementerte
allerede den modellen som ble konklusjonen — navn rått fra registeret (issue #158), navneform fra
ekstern kilde. Ett endepunkt lest på forhånd ville spart en hel spørsmålsrunde.

## 3. Data-opprydning er ikke en funksjon

Spør først: er dette gale DATA eller en manglende EVNE?

Gale data i seedet materiale rettes i kildefila under `src/RegelIde.Data/Seed/` (+ en `UPDATE` for rader
som alt ligger i en kjørende base, siden seedene aldri overskriver `Navn` på en rad de finner). Det
krever ingen migrasjon, ingen ny tjeneste og ingen frontend-endring.

Bygg mekanisme først når evnen faktisk mangler, eller når Johann eksplisitt ber om den. En rettelse som
bare skal gjelde et kjent, lukket sett rader er opprydning.

## 4. Oppstart: ingenting med sideeffekter uten gate

Enhver ny `IHostedService`/`BackgroundService`, og enhver ny tilbakefylling i oppstartsblokken i
`Program.cs`, som gjør **nettverkskall** eller **skriving til databasen**:

- skal gates bak en `RegelIde:*`-konfignøkkel, og
- gaten skal settes `false` i `src/RegelIde.Api.Tests/EmbeddedPostgresApiFixture.cs` **i samme endring**.

Mønsteret å kopiere er `LovdataFullimportBakgrunnstjeneste` (`RegelIde:LovdataFullimport:AktivVedOppstart`)
og fixturens tilsvarende `RegelIde__LovdataFullimport__AktivVedOppstart`. Fixturen reiser verten flere
ganger per testklasse; en ugated jobb kjører derfor mange ganger, mot ekte API-er, og skriver i
testdatabasen midt i andre tester.

Brutt 2026-09-08: `VirksomhetRegisternavnSynkBakgrunnstjeneste` ble registrert ugated. 245 API-tester
ble røde, i områder som ikke hadde noe med endringen å gjøre.

**Kjør API-testene straks etter enhver endring i `Program.cs` sin DI-/oppstartsdel** — ikke til slutt.

**Seed-vakter sjekker på den STABILE nøkkelen, aldri på navn.** For virksomheter er det
organisasjonsnummer (`docs/15` §3.3). En navnebasert vakt går i stykker i det noe annet skriver om
`Navn` — og det er nettopp jobben over som gjør det. Brutt 2026-09-08 i tre seeds samtidig, med to ulike
symptomer: `BergenKorpusSeed` krasjet synlig på `ux_virksomheter_organisasjonsnummer` (og veltet 245
tester), mens `AgderFylkeskommuneSeed` og `KommunaleParametreSeed` oppretter rader uten orgnr — der
stopper ingen constraint dem, så de ville duplisert stille. Den stille varianten er den farlige.

## 5. Tester: hvert prosjekt for seg, i forgrunnen

Embedded Postgres kolliderer på port når prosjektene kjører samtidig (issue #10). Kjør dem separat, og
ikke i bakgrunnen:

```bash
dotnet test src/RegelIde.Data.Tests --nologo
```

```bash
dotnet test src/RegelIde.Api.Tests --nologo
```

Ved mange røde tester samtidig: sjekk om feilene er ekte assertions eller `Test Collection Cleanup
Failure` fra fixturens opprydning. Sistnevnte tilskrives alle testene i samlingen og skjuler den ekte
årsaken.

## 6. Frontend

Typesjekk må kjøres **med `src/RegelIde.Web` som arbeidskatalog**, og med `-b`. Uten `-b` sjekkes null
filer, og kommandoen lykkes uten å ha gjort noe. `npm --prefix` duger ikke — den flytter pakkekatalogen,
ikke arbeidskatalogen, og `tsc` leter da etter `tsconfig.json` i repo-roten:

```bash
cd src/RegelIde.Web && npx tsc -b --noEmit
```

Merk at `-b` er inkrementell og cacher i `.tsbuildinfo`. En tom utskrift kan derfor bety «ingenting var
endret siden sist» og ikke «alt er sjekket» — ved tvil, slett `.tsbuildinfo` og kjør på nytt.

Er `node_modules` ufullstendig, rapporterer kommandoen `TS2307: Cannot find module 'vitest'` for
`*.test.ts`-filene. Det er en miljøfeil, ikke en kodefeil — `npm install` i `src/RegelIde.Web` retter
den. Ikke tolk de to som at endringen din brøt noe.

**Les `docs/09-design-konvensjoner.md` FØR du bygger ny UI**, ikke etterpå — den er normativ, ikke
beskrivende, og §7 er en liste over feil som alt er begått. Oppdater den når en designbeslutning tas, så
neste runde ikke må gjette.

Særlig: fargeroller på `Tag` er låst per betydning (§15), metatekst er `--ds-font-size-1` **kombinert
med** `--ds-color-neutral-text-subtle` (§6), og `opacity` er aldri riktig for dempet tekst (§7).

## 7. Kommentarkulturen skal holdes

Denne kodebasen forklarer **hvorfor**, ikke hva — inkludert avviste alternativer, hvem som bestilte noe,
og hva som ble verifisert kontra antatt. Skriv i samme stil. Konkret:

- Marker endringer med `[Ny, <runde>, <dato>]` / `[ENDRET, …]` / `[FJERNET, …]` og begrunnelsen.
- Skriv «verifisert live mot <kilde> <dato>» bare når du faktisk har gjort det. Ellers skriv at det er
  antatt.
- Når en avgrensning er bindende, merk den `[LÅST]` med kilde (issue-nummer eller `docs/`-paragraf).
- Fjerner du en mekanisme, la et spor stå om hvorfor — se `Program.cs` sin `[FJERNET]`-kommentar der
  ledd-kasus-tilbakefyllingen sto.

Ikke vask bort eksisterende kommentarer for å korte ned. De er dokumentasjonen.

## 8. Ingen gjettede verdier

Gjennomgående prinsipp i hele kodebasen, formulert som «ingen gjettet fallback» og «ikke funnet ≠
oppfunnet»: finner du ikke en verdi, la den være NULL og rapporter det synlig. Velg aldri det nærmeste
eller mest sannsynlige treffet.

Dette gjelder også algoritmisk «forbedring» av data fra en autoritativ kilde. En navneomskriving som
virket opplagt ga «Statsforvalteren i innlandet», «(nve)» og «Nærings- Og Fiskeridepartementet».

## 9. Verifisering: åpne kald, ellers er den verdiløs

Åpne skjermen slik en bruker gjør — fra forsiden, uten å først klikke seg til den tilstanden du
forventer. Å navigere til riktig tilstand og så bekrefte at den er der, er en selvoppfyllende sjekk, ikke
en verifisering.

Brutt 2026-09-08: taggene var «verifisert synlige» fordi verifiseringen først byttet til riktig lag i
`TagTekst`. Johann åpnet siden kald og så umarkert tekst. Årsaken var én linje —
`useState(kinds[0]?.id)` — som alltid valgte «Begrep», uansett hvilke lag noden hadde tagger i.

Skill dessuten **systemtilstand fra påstand om verden** i det du viser: «Avvist» må vise om det var
maskinen eller et menneske (`BehandletAv IS NULL` = maskinen), og «sovende» må ikke kunne leses som at
en kommune er nedlagt.

## 10. Parallelle oppgaver: eieren er den som foreslo dem

Foreslår du en bakgrunnsoppgave, eier du den — inkludert at den ikke ødelegger for noe annet som kjører.
Før du starter én til: sjekk hvilke filer de to vil ta på. Overlapper de, kjør dem etter hverandre.

Brutt 2026-09-08 (begge deler samme kveld): to oppgaver redigerte `RettskildeDetalj.tsx` samtidig, og en
`until ... ; sleep`-løkke jeg selv startet lå igjen og gikk i bakgrunnen til Johann spurte hva den var.
Stopp det du starter, og skriv aldri at Johann startet en oppgave du foreslo.

## 11. Avhengighets-PR-er: sjekk diffstørrelsen før du merger

En dependabot-PR er en GENERERT lockfil, ikke en endring som kan rebases meningsfullt. Har den ligget
en stund, er den bygget på en gammel `master`, og da bærer den med seg alt som er kommet TIL siden —
som slettinger.

**Regel: se på diffstatistikken i `package-lock.json` før du merger.** En lockfil-PR som legger til en
oppgradering skal ha en liten, symmetrisk diff. Hundrevis av slettinger er ikke en fiks, det er en
stale branch.

Brutt 2026-09-09: `#187` («Bump postcss from 8.5.22 to 8.5.26») var bygget på en master fra FØR vitest
ble lagt inn. Diffen mot dagens master var 11 innsettinger og **315 slettinger** — den ville fjernet
vitest og hele avhengighetstreet, altså landet sårbarhetsfiksen og fjernet frontend-testene i samme
merge. Løsningen er å gjøre bumpen på nytt på dagens master (`npm update <pakke>
--package-lock-only`), åpne en PR som erstatter, og LUKKE dependabot-PR-en med begrunnelsen skrevet
inn i den, slik at neste person ser hvorfor.

**En transitiv avhengighet skal forbli transitiv.** `npm install <pakke>@versjon` legger den inn i
`package.json` som direkte avhengighet — det gjør oss ansvarlige for å vedlikeholde en versjon vi ikke
bruker selv. Bruk `npm update <pakke>` for noe som kommer inn via en annen pakke (`vite` → `postcss`),
og la `package.json` være urørt.

**Verifiser med `npm ci`, ikke `npm install`.** `--package-lock-only` endrer bare lockfila, og et
etterfølgende `npm install` kan svare «up to date» og la den gamle versjonen ligge i `node_modules` —
da har du verifisert ingenting. `npm ci` river `node_modules` og installerer fra lockfila, som er det
som faktisk beviser at lockfila er konsistent. Sjekk deretter den installerte versjonen:

```bash
cd src/RegelIde.Web && node -e "console.log(require('postcss/package.json').version)"
```

Merk at `npm ci` feiler med «file already in use» hvis vite-serveren kjører — stopp den først, og
start den igjen etterpå.

## 12. Avbrudd kan komme uten forvarsel — gjør tilstanden varig underveis

En økt kan stoppe midt i arbeidet (tokenkvote, sesjonsgrense). Da er alt som bare finnes i
konteksten tapt. Regelen er derfor: **commit og push underveis, ikke til slutt**, og hold
`ARBEIDSSTATUS.md` i repo-roten oppdatert etter hvert steg.

`ARBEIDSSTATUS.md` beskriver arbeid som er I GANG: branches i luften og hva som mangler på hver,
hva som er verifisert kontra bare kompilert, køen videre, og beslutninger Johann har tatt som binder
det gjenstående arbeidet. Er alt landet, SLETT fila — den skal ikke bli et arkiv. Varige
arbeidsregler hører her i CLAUDE.md, varige designbeslutninger i `docs/09-design-konvensjoner.md`.

**Skriv HVA som er verifisert og HVORDAN, aldri bare «ferdig».** «Kompilerer» er ikke «testet», og
«testet» er ikke «åpnet kaldt i nettleseren» (§9). Neste økt må kunne stole på statusen uten å
gjenta arbeidet.

Brutt 2026-09-09: tre agenter i egne worktrees døde på SAMME sesjonsgrense, midt i arbeidet, uten å
ha committet noe — 1065 linjer kode og tester lå bare i arbeidskopiene. Worktreene hindret
filkonflikter, men tokenbudsjettet er DELT: parallellitet flerdobler forbruket og gir ingen
beskyttelse mot at kvoten tar slutt. Får du agenter til å jobbe parallelt, instruer dem eksplisitt
om å committe og pushe underveis.

## Nyttige kommandoer

Kjør appen (Browser-panelet, aldri `dotnet run` via Bash) — konfigurasjonene heter `regel-ide-api` og
`regel-ide-web` i `.claude/launch.json`. (Sto tidligere som `virksomhet-api`/`virksomhet-web` her;
rettet 2026-09-09 etter at `preview_start` feilet på de navnene. Fila inneholder mange andre
konfigurasjoner fra tidligere runder — les den framfor å gjette.)

Ny migrasjon — merk at `--startup-project` må være `RegelIde.Data`, ikke `RegelIde.Api` (bare Data har
`Microsoft.EntityFrameworkCore.Design`):

```bash
dotnet ef migrations add <Navn> --project src/RegelIde.Data --startup-project src/RegelIde.Data
```
