# Arbeidsregler for regel-ide

Reglene under er skrevet fordi de er brutt i praksis. Hver av dem har en konkret hendelse bak seg.
Kodebasen er tungt kommentert og `docs/` har 35 dokumenter — dette er ikke en oversikt over dem, det
er en liste over de tingene som faktisk går galt.

**Start her:** `docs/README.md` er kartet over de 35 dokumentene, med et statusvokabular som sier
hvilke som BINDER arbeidet og hvilke som er øyeblikksbilder. Les det før du åpner noe i `docs/` —
ellers behandles et referat som en spesifikasjon. `ARBEIDSSTATUS.md` sier hva som er kjørt mot
databasen og hva som er målt; er den tom, slett den.

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

## 13. Slett grenen med én gang PR-en er merget

Johann fant 57 grener på repoet. Alle unntatt den aktive hadde en MERGET PR — de
hadde ligget igjen fordi ingen ryddet etter seg, én runde om gangen, i ukevis.

Regelen: rett etter at en PR er merget, slett grenen både på origin og lokalt.
Det er en del av å merge, ikke en oppgave for senere.

```
gh pr merge <nr> --squash --delete-branch
```

`--delete-branch` fjerner både fjern- og lokalgrenen i samme steg, og er derfor
den formen som skal brukes. Merger du via nettsiden eller uten flagget, rydd med
`git push origin --delete <gren>` + `git branch -d <gren>` med en gang.

Merk at squash-merge gjør at grenen IKKE ser merget ut for `git branch --merged`
— innholdet ligger i master som én ny commit, ikke som grenens egne commits.
Sjekk PR-tilstanden i stedet, ellers ser 56 ryddeklare grener ut som 56 grener
med uferdig arbeid:

```
git branch -r | sed 's|  origin/||' | while read b; do gh pr list --head "$b" --state all --json number,state -q ".[]|\"$b #\(.number) \(.state)\""; done
```

Sletting av grener er dessuten en av handlingene auto-modus blokkerer, så det
kan hende kommandoen må kjøres av Johann selv — desto større grunn til å bruke
`--delete-branch` mens PR-en merges.

## 14. Regex som bytter et identifikatornavn i .tsx treffer også prosaen

Da de tre kandidatsidene ble koblet til `useKandidatvalg`, byttet skriptet
`valgte` → `valg.valgte` litt for grådig: regexen traff JSX-TEKST og strenger,
ikke bare uttrykk. Resultatet var seks synlige knapper som sa «Godkjenn
valg.valgte», pluss en feilmelding og en bekreftelsesdialog med samme lekkasje.

`tsc` og testene var grønne hele veien — det er ikke en type- eller logikkfeil,
det er tekst. Nettleserkontrollen fanget den heller ikke, fordi den sjekket
tellerne og sorteringspilene, ikke knappenavnene.

Etter et slikt bytte: `find`/`read_page` på knappetekstene, og et grep etter
det nye navnet i kommentar-/strengposisjon.

## 15. Arbeid fra issues — GitHub ER backloggen

**Én sak → én gren → én PR → merge med `--delete-branch`.** Ikke flere saker i samme gren, og ikke
arbeid uten en sak.

`docs/13-backlog.md` §2/§4 ser ut som en backlog, men er utdatert og konkurrerer med de åpne issuene.
To backlogger er verre enn én: den ene blir lest, den andre blir gal. Splittingen er meldt som egen
sak — inntil den er gjort, er **GitHub** kilden.

Slik en runde skal gå:

1. **Les saken helt**, inkludert akseptansekriteriene. Er de formulert som «feltet finnes», omformuler
   til spørsmål modellen skal kunne besvare (§0).
2. **Mål premissene før du bygger** (§16). En sak kan være riktig i sak og feil i premiss.
3. **Skriv målingene INN i saken** som kommentar. Da overlever de økten. Tallene fra i dag —
   4701 av 5565 fastsettere, 77,3 % gyldige hjemler, 416 oppgraderte ledd — er verdiløse hvis de bare
   står i en chat.
4. **Funn utenfor omfanget blir NYE saker**, ikke inline-arbeid. Fem slike ble meldt 2026-09-10
   (#231, #233, #239 m.fl.); ingen av dem hørte i den runden de ble funnet i.
5. **PR-beskrivelsen skal si hva som ble MÅLT**, ikke hva som ble endret. Diffen viser endringen;
   PR-en skal vise at den virker.
6. **Merge:** `gh pr merge <nr> --squash --delete-branch`.
7. **Lukk saken selv, med tallene.** «Lukker #N» er IKKE et GitHub-nøkkelord — bare engelske
   (`Closes`/`Fixes`) lukker automatisk. Fem saker sto åpne 2026-09-10 med merget PR fordi
   PR-beskrivelsene sa «Lukker #215» på norsk. Lukk med `gh issue close <nr> -c "..."` og legg
   MÅLINGENE i kommentaren — det er den varige nedtegnelsen av at saken faktisk ble løst.

**Ikke stable PR-er.** Brutt 2026-09-10: #230 ble tatt ut fra `hjemmel-ledd-presisjon` fordi
migrasjonen var generert oppå den. Da #227 ble merget med `--delete-branch`, forsvant basegrenen og
**GitHub lukket #230 automatisk** — arbeidet måtte rebases og åpnes på nytt som #232. Trenger to saker
samme migrasjonssnapshot: land den første helt først, eller si det til Johann og la ham velge.

## 16. Mål det, ikke anta det — og korriger deg selv når målingen sier noe annet

Dette er den regelen som ga mest 2026-09-10, og den ble brutt tre ganger samme dag av meg selv:

- **Issue #217 hadde feil premiss.** Saken (som jeg selv skrev) antok at ledd-presisjonen bare fantes
  i prosa, og at ordenstall måtte tolkes ut av tekst. Målingen viste at Lovdata oppgir den som
  STRUKTUR, i et header-felt vi ikke leste — 38 av 1712 lenker hadde `/ledd/4` i href-en, og NULL
  hadde ordenstall i teksten uten at href-en også hadde det. Arbeidet ble enklere og mer presist enn
  saken beskrev.
- **«Fem dokumenter har foreldede påstander» var galt.** Jeg grep'et etter fraser som «ikke bygget
  ennå» og rapporterte fem. Da jeg sjekket koden var TRE av dem sanne — Presedensregisteret er reelt
  ikke bygget, `PraksisJson` er alltid `[]`. Et grep finner formuleringer, ikke sannheter.
- **kgl.res var ikke et problem i det hele tatt.** Jeg presenterte 1588 «ukoblede» rettskilder som en
  mangel. Johann spurte hva som skiller Jan Mayen-forskriften fra andre, og svaret var «ingenting»:
  departementet er koblet, hjemmelen er på plass. Jeg hadde latt en tom kobling se ut som et hull.

Regelen: **en påstand om korpuset skal ha et tall bak seg, og tallet skal komme fra en spørring — ikke
fra et grep etter formuleringer.** Og når målingen motsier det du nettopp sa, si det rett ut i samme
melding. Johanns tid går ikke til å oppdage at forrige avsnitt var feil.

## 17. Verifiser før du sletter — også når du «vet» at det er merget

Brutt 2026-09-10, minutter etter at §13 var skrevet: jeg slettet den siste lokale grenen med
`git branch -D` (tvungen) uten å sjekke om den var merget. `git branch -d` ville nektet; `-D` gjør det
uansett. Etterpå viste det seg at commiten ikke var forfar av master — som ser ut som tapt arbeid —
men PR #236 var merget og innholdet lå i master som squash-commit.

Det gikk bra, men ikke fordi jeg hadde sjekket. Squash-merge er nettopp grunnen til at git IKKE kan
brukes som eneste sjekk (§13), og det er ikke en unnskyldning for å hoppe over sjekken helt.
Rekkefølgen er: verifiser at PR-en er merget, så slett.

Merk også: grensletting krever en tillatelsesregel. `.claude/settings.local.json` (gitignorert) har nå
`Bash(git push origin --delete*)` og `Bash(git branch -d*|-D*)` ved siden av `Bash(gh pr merge*)`. Uten
disse blir bulk-sletting av grener avslått av auto-modus-klassifiseringen, mens
`gh pr merge --delete-branch` går gjennom — det var forklaringen på at 56 grener lå igjen i ukevis.

## 18. Avklaringer stilles som konkrete spørsmål i verktøyet, ikke begravet i lang tekst

Brutt 2026-09-10: issue #249 ble skrevet med et prisatt A/B-valg («egen `Kildefeil`-tabell» vs. «ren
visning av eksisterende spørring») formulert som prosa under en overskrift i issue-teksten, i stedet
for å faktisk spørre. Johann måtte lete opp spørsmålet selv og svare i fritekst — treigere og mer
feilutsatt enn nødvendig, når et verktøy for nettopp dette allerede finnes.

Regel: når en beslutning jeg ikke kan ta selv (arkitekturvalg, prioritering, omfangsgrense,
A/B-alternativ) skal legges fram for Johann — bruk spørsmålsverktøyet MED DET SAMME, ikke bare skriv
den ned og vent på neste melding. Gjelder også når spørsmålet SKAL stå skriftlig et sted for
sporbarhet (f.eks. i selve GitHub-issuen) — skriv det ned der, men still det også som et ekte
spørsmål samtidig, ikke i stedet for. Etter svar: skriv beslutningen inn i issuen/saken som en
kommentar (samme «varig nedtegnelse»-prinsipp som §15/§16), ikke bare la den stå i chat-historikken.

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
