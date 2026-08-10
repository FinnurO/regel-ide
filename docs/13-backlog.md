# 13. Backlog — samlet status og neste steg

*Samler alt som er avklart, men ikke bygget, på tvers av `06-veikart.md` (byggestegene),
`12-fasit-handbok-leveranse.md` (dimensjonsgapene mot fasiten) og designavklaringene fra
2026-07-31-runden (Hendelse/Tjenesteavhengighet, kunnskapsbibliotek, editor, vilkår-referanser).
Ment å oppdateres etter hver runde — ikke en engangs-plan.*

## 1. Byggesteg-status

| # | Byggesteg | Status |
|---|---|---|
| 0 | Lås ontologien (Vilkår/Regel/Unntak) | ✅ Fullført 2026-07-23 |
| 1 | Rettskildebibliotek (+ håndbok/rundskriv-forfatterflyt) | ✅ Bygget og verifisert. Utvidet 2026-07-31 med håndbok-nivå rettskildeomfang (§3 under) |
| 2 | Tjenester + Begrep + Kodelister | ✅ Bygget, inkl. Hendelse (CPSV Event) og Tjenesteavhengighet som ekte tabeller (§2.1, ferdig 2026-07-31) |
| 3 | Presedensregister | ⬜ Ikke startet |
| 4 | Vilkårstre (grafeditor) | ✅ Runde 1 bygget og verifisert, inkl. tekst-først «opprett vilkår fra tagg»-flyt (§2.5). Runde 2 (testmodul + full publiseringsmodell) ⬜ ikke startet |
| 5 | AI-forslag (utvidet: kunnskapsbibliotek + skillsbaserte agenter) | ✅ Runde 1 ferdig 2026-07-31 — to agenter («Identifiser tjenester»/«Identifiser begrep») + `IKiAgentKlient`-stub. ✅ Runde 2 ferdig 2026-08-02 (§2.2 under) — ekte KI-leverandør (OpenRouter/DeepSeek), fil-opplasting til kunnskapsbiblioteket, Lovdata-søk. ⬜ Runde 3+ (de tre resterende agentene) gjenstår. |
| 6 | Datasett, informasjonsmodell, eksportmotor | 🚧 Datasett-registeret + `DatasettVerdi` bygget (byggesteg 4-runden). Informasjonsmodell-skjerm og eksportmotor ⬜ ikke startet |
| 7 | Saksbehandling/forklaringslogg (tynn slice) | ⬜ Ikke startet — MVP-grensen |
| 8 | Kunnskapsgraf/påvirkningsanalyse | ⬜ Bevisst utenfor MVP |
| 9 | Dashboard | ⬜ Bevisst utenfor MVP |

## 2. Avklarte designbeslutninger — klare til bygging, venter på prioritering

Disse er ferdig diskutert og dokumentert (i domenemodell/veikart), men **ikke implementert**. Ingen
av dem krever mer avklaring — bare et "ja, bygg det".

### 2.1 Hendelse (CPSV Event/LifeEvent/BusinessEvent) + Tjenesteavhengighet
*Full spesifikasjon: `03-domenemodell.md` §1.5, `06-veikart.md` byggesteg 2.*

✅ **Ferdig 2026-07-31.** `HendelseEntitet` (delt register, nasjonal/lokal) + `TjenesteHendelseEntitet`
(symmetrisk M:N, `cpsv:isClassifiedBy`) + `TjenesteavhengighetEntitet` (rettet kant, `Rel` ∈
`forutsetning_for`/`gir_mulighet_til`/`utlost_av`/`for`/`avhengig_av`/`input_til`) — erstatter de tomme
`hendelser`/`tjenesteavhengigheter`-jsonb-feltene på `TjenesteEntitet` fullstendig. Retningsberegnet
visningstekst (`HentForTjenesteAsync`) verifisert: samme rad viser riktig tekst fra begge sider, ingen
duplisert lagring. `HendelseTjenesteavhengighetSeed.cs` kobler de 13 fasit-tjenestene til «Alminnelig
skjenkebevilling» — inkl. domenemodellens egne to worked examples ("Kontroll/tilsyn" klassifiserer
begge tjenestene; "Endring av eierskap" → "Endring av eiere eller eierandeler"). 291/291 backend-tester
grønt (17 nye), `tsc -b --noEmit` rent, ny UI-seksjon i `TjenesteDetalj.tsx` verifisert i browser.

### 2.2 Kunnskapsbibliotek + skillsbaserte AI-agenter (byggesteg 5)
*Full spesifikasjon: `06-veikart.md` byggesteg 5, `docs/14-byggesteg5-teknisk-design.md` for den
tekniske malen.*

**✅ Runde 1 ferdig 2026-07-31.** Under planleggingen ble den opprinnelige "kunnskapsbibliotek
sentrert rundt Tjeneste"-antakelsen korrigert (Johann): den gir ikke mening for en agent som skal
finne ut *hvilke* Tjenester som finnes — Tjenesten eksisterer ikke ennå når den agenten kjører. Bygget
i stedet:

- To uavhengige, **rettskilde-drevne** agenter — «Identifiser tjenester» (`/tjenester/forslag`) og
  «Identifiser begrep» (`/begreper/forslag`) — ikke Tjeneste-sentrert. Kunnskapsbiblioteket forenklet
  til et lite virksomhets-scopet lenke-register (`KunnskapsbibliotekLenkeEntitet`), brukt kun av
  tjeneste-agenten; ingen fil-/notat-opplasting i denne runden.
- `IKiAgentKlient`-abstraksjon + `KiAgentKlientStub` — beviser hele rørledningen (kø,
  Avvis/Rediger/Godkjenn, proveniens med `AiForslagVersjon`/`GodkjentAv`) uten en ekte KI-leverandør.
  Leverandørvalg forblir en egen, senere beslutning.
- `foreslatt_av_ai` generalisert til Tjeneste og Begrep, inkl. en fiks av en tidligere
  uoverensstemmelse mellom AK-3.10.2 og statusdiagrammet i `03-domenemodell.md`.
- 291+15 nye backend-tester grønt, `tsc -b --noEmit` rent, begge agentene verifisert ende-til-ende
  i browser (inkl. at de IKKE krysskobler hverandres køer).

**✅ Runde 2 ferdig 2026-08-02.** Tre uavhengige utvidelser, ikke nye agenter (se
`docs/14-byggesteg5-teknisk-design.md` §1/§2/§6 for teknisk detalj):

- **Ekte KI-leverandør**: `KiAgentKlientOpenRouter` mot OpenRouter, modell DeepSeek V4 Flash 0731
  (konfigurerbar streng) — valgt fremfor DeepSeeks egen (Kina-hostede) API. Konfigurasjonsstyrt
  (`RegelIde:KiAgent:Leverandor`, default fortsatt stub); nøkkel via `dotnet user-secrets`.
  Modellvalg fra en admin-side i appen (uten restart) er bevisst IKKE bygget denne runden.
- **Kunnskapsbibliotek utvidet med fil-opplasting** (`KunnskapsbibliotekFilEntitet`, PDF/Word) —
  avviser filer uten tekstlag (sannsynlige skann) via tekstuttrekk-forsøk, IKKE ekte OCR. Fil-bytes
  som bytea i Postgres, ikke ekstern blob-lagring.
- **Lovdata-katalog + søk** — `Importer.tsx` krevde tidligere at brukeren kjente den eksakte
  datokoden. En søkbar katalog (kun tittel+datokode, bygges/fornyes automatisk) løser dette; ingen
  endring i selve import-endepunktet.
- 364 backend-tester grønt (201+163, inkl. ekte nettverkskall mot både OpenRouter-stub-handler og
  Lovdatas bulk-API), `tsc -b --noEmit` rent, Lovdata-søk+import verifisert ende-til-ende i browser.

**✅ Runde 3 ferdig 2026-08-10.** Utløst av forberedelsen til et ekte testcase (Agder fylkeskommune)
— avdekket at runde 2s design hadde reelle mangler før den kunne gi et meningsfullt resultat mot en
ekte modell (se `docs/14-byggesteg5-teknisk-design.md` §1/§7 for teknisk detalj):

- **KI-klienten generalisert** — `KiAgentKlientOpenRouter` (hardkodet OpenRouter+DeepSeek-default)
  erstattet med `KiAgentKlientOpenAiKompatibel` (leverandøragnostisk — `BaseUrl`/`Modell`/`ApiKey`
  alle konfig). Vurdert leverandør: HostYourAI (EU-hostet, GDPR-compliant, kjører åpne modeller).
- **Rettet et reelt gap**: `RettskildeKontekstHjelper` sendte ikke `Eid` per node — en agent kunne
  aldri returnere en presis `LovreferanseEid`, uansett instruks. System-instruksene var også kun
  ettords-etiketter uten skjemabeskrivelse; nå fullstendige, skjema-beskrivende prompter + defensiv
  markdown-kodeblokk-strimling (`JsonSvarHjelper`).
- **Tjeneste-forslaget dekker nå resten av CPSV-AP-NO-feltene** som allerede fantes i skjemaet
  (`KompetentMyndighet`/`Output`/`Tjenestetype`/`Malgruppe`/`Kanaler`/`Kostnad`/`Behandlingstid`/
  `Kontaktpunkt`/`KonsekvensVedBrudd`/`Sprak`), ikke bare Tittel/Beskrivelse. Fire CPSV-AP-NO-
  konsepter (`hasParticipation`/`hasInput`/`dct:spatial`/`requires`-vs-`hasPart`) er bevisst IKKE
  modellert i skjemaet i det hele tatt — dokumentert som eget, senere spørsmål, ikke bygget.
- **Kunnskapsbibliotek-filer kan nå ha en egen `Tittel`** (uavhengig av opplastet filnavn).
- **Ny testcase-virksomhet** (`AgderFylkeskommuneSeed.cs`) — kun Virksomhet+Bruker seedet; selve
  rettskilde-import/fil-opplasting/lenker/agent-kjøring gjøres live gjennom appen.
- 370 backend-tester grønt (206+164), `tsc -b --noEmit` rent.

**⬜ Runde 3+ gjenstår** (fortsatt retningsnivå, ikke bygget): de tre andre agentene
(Tjenestebeskrivelse/Vilkår-og-Vilkårstre/Håndbok) i fast pipeline, en generalisert multi-type
forslagskø, `foreslatt_av_ai` for Vilkår/Regelnode/Unntak, ekte OCR for skannede dokumenter, og
modellvalg fra en admin-side. **Forutsetter ikke** byggesteg 3, men presedens (byggesteg 3) ville
styrket "Rettskilder og strukturering"-agenten betydelig — vurder rekkefølge.

### 2.3 Editor: punktliste/nummerert liste-knapper i `MinimalEditor`
*Fasit dimensjon G, skåret 50 %.*

- ✅ **Ferdig 2026-07-31.** Lagt til «•»/«1.»-knapper i `MinimalEditor`s verktøylinje
  (`insertUnorderedList`/`insertOrderedList`), og utvidet klientside-`DEFAULT_ALLOW` med `ul`/`ol`/`li`.
  Verifisert i browser: liste satt inn, lagret, lest tilbake via API med `<ul><li>`-markup intakt.

### 2.4 Vilkår-referanser i håndbok-/veiledningstekst
*Presisert 2026-07-31 — se samtalen om CPSV-hendelser.*

- ✅ **Ferdig 2026-07-31.** `KommentarRedigering.tsx` tar nå `alleVilkar`/`alleTjenester`-props
  (gjenbruker `RettskildeDetalj.tsx`s allerede hentede `vilkarPerId`/`tjenestePerId`-registre) og
  slår dem sammen med rettskilder i `referanser`-listen til `MinimalEditor`. Fortsatt **ikke** ekte
  tekst-fletting — kun en typet peker (`data-ref-kind="vilkar"`/`"tjeneste"`), verifisert i browser.

### 2.5 «Opprett vilkår fra dette utdraget» — tekst-først-forfatterflyt
*Fra samtalen om vilkår-tagging.*

✅ **Ferdig 2026-07-31.** Ny `onOpprettFraTag`/`opprettFraTagKinds`-prop på `TagTekst.tsx` — en
knapp «Opprett vilkår fra dette utdraget →» vises for umerkede `kind='vilkar'`-tagger (kun for de
kinds forelder spesifikt slår på, `RettskildeDetalj.tsx` bruker `['vilkar']`). Åpner et lite
inline-skjema (tittel forhåndsutfylt fra sitatet, obligatorisk Tjeneste-valg); ved opprettelse settes
juridisk grunnlag automatisk fra rettskildens kortnavn + taggens node-eId, `TjenesteId` fra valget, og
taggen kobles umiddelbart — **uten** noen kobling til regelgrafen (bekreftet via API: vilkåret finnes
i ingen regelnodes `barn[]`). Ren frontend-endring, ingen backend-kode trengtes. Verifisert
ende-til-ende i browser mot ekte alkoholloven-tekst (§ 1-6, "Bevillingsperioden") — vilkåret som ble
opprettet under verifiseringen ble stående (ekte, korrekt innhold, ikke fjernet: taggens kobling er nå
"publisert referanse" og kan ikke slettes igjen, AK-3.3.4 — selve beviset på at koblingen faktisk ble
skrevet til databasen via den ekte flyten).

### 2.6 Virkningsregel — forslag til å lukke dimensjon E (Vilkår-i-vedtak-taksonomi, 0 %)
*Forslag mottatt 2026-07-31 (Claude Chat-notat, se full tekst i samtalen). Terminologi allerede låst
i `01-referansemodell.md` §15.1: Vedtaksvirkning = «den konkrete, tidsavgrensede konsekvensen av
vedtaket … én rettsfølge instansiert for den konkrete saken» — bekreftet ved lesing, notatets premiss
stemmer med koden/dokumentasjonen som faktisk finnes.*

**⚠️ Dette er IKKE en avklart beslutning som §2.1–2.5** — det er et forslag som selv eksplisitt ber om
en diskusjonsrunde før det låses («Ontologi-diskusjonen om Vilkår/Regel/Unntak tok fem runder å lande
— denne bør trolig få samme behandling»). Stå her som et notert, analysert forslag, ikke en kravspesifikasjon.

**Kjernen i forslaget:** speile Vilkår/Regel/Unntak-mønsteret på virkningssiden — en ny
**Virkningsregel**-entitet (+ Virkningsunntak) i Regellaget som Vedtaksvirkning instansieres fra ved
kjøretid, i stedet for at gebyrformel/plikt-liste/skjønnskobling kun eksisterer som fritekst i
forklaringsmodellen. Konkret hjemmel: rundskriv §9 (fast plikt / trappet gebyrberegning / sakspesifikk
opplysning / skjønnsbasert tilleggsvilkår), og §4 i notatet peker på en reell, ikke-triviell kobling:
en Virkningsregels aktiveringsbetingelse må noen ganger vise til utfallet av én **navngitt**
vilkårsvurdering i treet (f.eks. Kommunal skjønnsvurdering → «med vilkår»), ikke bare til rot-Regelens
endelige utfall.

**Vurdering — arkitektonisk lavere risiko enn det ser ut som ved første øyekast:** dette krever ikke
et nytt mønster. Regel-IDE eier allerede ikke Vedtak/Vedtaksgrunnlag/Vedtaksvirkning som driftsdata
(§15.1) — nøyaktig samme forhold som gjelder Regelnode i dag, som forfattes/publiseres her og
**instansieres** som en `forklaringsmodell-api`-`Regel`-rad ved kjøretid (`01-referansemodell.md`
§5.6). Virkningsregel ville følge identisk mønster: forfattes/publiseres i regel-IDE, instansieres som
`Vedtaksvirkning` i `forklaringsmodell-api`. Det er altså en anvendelse av et allerede akseptert
prinsipp på en andre akse, ikke en ny grensesnitt-idé.

**Det åpne spørsmålet (notatets §6) — min anbefaling:** hold Virkningsregel/Virkningsunntak i en
**egen, men koblet** tabell/graf — IKKE samme `RegelnodeBarnEntitet`/`UnntakEntitet`-graf som
Vilkår/Regel/Unntak. Begrunnelse: `VilkarstreGrafHjelper.KanNaAsync` (DAG-sykelsjekk, INV-7) er bygget
for én entydig kant-semantikk («kan nå» = «er logisk forutsetning for»). En Virkningsregels
aktiveringsbetingelse er en helt annen relasjon («utløses av utfallet av», retning motsatt av
avhengighet) — å presse den inn i samme graf ville enten kreve at BFS-en skiller to inkompatible
kant-typer, eller risikere falske sykel-avvisninger. En enkel FK-peker fra Virkningsregel til
Vilkår/Regelnode (samme polymorfe `BetingelseType`/`BetingelseId`-mønster som `UnntakEntitet` allerede
bruker, men uten å delta i sykelsjekken) dekker notatets §4-behov uten å røre den låste ontologien.

**Status:** ikke bygget, ikke tatt stilling til av Johann ennå — venter på en egen avklaringsrunde
(samme prosess som ontologilåsen 2026-07-23), ikke bare et «ja, bygg det».

## 3. Kjente gap fra fasit-dokumentet (`12-fasit-handbok-leveranse.md`)

| Dimensjon | Skår | Gap |
|---|---|---|
| B — Presisjon på tall | *(ikke skåret — forfatterdisiplin, ikke kode)* | Ingen sperre mot vage formuleringer i fritekst; hører til en fremtidig skrive-veiledning/lint, ikke et strukturelt gap |
| D — Skjønn som egen sjanger | 25 % *(uendret)* | Skjønnsmomenter kan festes en veiledningskommentar via samme mekanisme som dimensjon A, men koblingen skjønnsmoment↔kommentartekst er ikke bygget strukturert |
| E — Vilkår-i-vedtak-taksonomi | 0 % *(uendret)* | Gyldighet/Prikkbelastning/gebyr er kun fritekst-kommentarer på rotnoden (runde 4) — `Vedtaksvirkning` eies bevisst av `forklaringsmodell-api`, ikke regel-ide. Krever avklaring om grensesnitt, ikke bare kode her |
| F — Dokumentgraf | 75 % *(uendret)* | Kryssreferanse-mekanismen dekker det teknisk; ingen forfatterveiledning for NÅR man bør lenke vs. skrive |
| G — Sjekkliste/handlingsstruktur | 50 % | Se §2.3 over |
| — | — | `DatasettDetalj.tsx` validerer ikke at en registrert verdi faktisk matcher `Dtype` (en boolsk kan få en fritekst-streng uten varsel) |
| — | — | Håndbok-nivå rettskildeomfang håndheves ikke — ingen varsel om en kobling/kommentar peker utenfor det deklarerte omfanget |
| — | — | `RundskrivReproduksjonTests.cs` sin dekningstabell måler kun den delte seed-baselinen (`Byggesteg4VilkarstreSeed`) — reflekterer **ikke** innholdet som faktisk ble opprettet i den kjørende databasen i fasit-runde 4/5 (Habilitet, Formalia, Serveringsbevillingsvilkår, Kunnskapsprøve, Kommunal skjønnsvurdering, serveringsloven, 12 tjenester). Testen gir dermed et unødvendig pessimistisk bilde permanent, med mindre seed-dataene oppdateres til å inkludere det samme innholdet — se §4 anbefaling |

## 4. Anbefalt rekkefølge for neste runde(r)

Ingen av disse krever videre avklaring — bare en prioritering:

1. ✅ **Ferdig 2026-07-31.** Ny `FasitRunde4Seed.cs` + serveringsloven-fixture lagt inn i
   `data/kilder/raw-lovdata/`; `RundskrivReproduksjonTests.cs`s dekningstabell oppdatert til å
   reflektere at §2/§4/§5 nå er fullt/delvis dekket og kun §12 gjenstår som rent modellgap. Se
   `docs/12-fasit-handbok-leveranse.md` "Runde 5". 274/274 backend-tester grønt, `tsc -b --noEmit` rent.
2. **Editor: punktliste/nummerert liste** (§2.3) — liten, avgrenset, avklart.
3. **Vilkår-referanser i håndbok/veiledning** (§2.4) — liten, avgrenset, avklart.
4. **Hendelse + Tjenesteavhengighet** (§2.1) — middels, ferdig designet, gir umiddelbar verdi
   (kobler de 12 fasit-tjenestene faktisk sammen).
5. **«Opprett vilkår fra tagget utdrag»** (§2.5) — den reelle forfatterflyt-forbedringen fra
   dagens samtale om tagging.
6. **Byggesteg 3 — Presedensregister.** Løser samtidig «Testkommunen 2017 Vurdering av habilitet
   2018»-referansen fra rundskriv v4 §3, som i dag ikke har noe sted å høre hjemme.
7. **Byggesteg 5 runde 3+ — de tre resterende AI-agentene** (§2.2, runde 1+2 ferdig). Vurder
   om byggesteg 3 bør være ferdig først, siden «Rettskilder og strukturering»-agenten forutsetter et
   presedensregister for å være noe mer enn en ren rettskilde-importer.
8. **Byggesteg 6/7** — informasjonsmodell/eksportmotor og saksbehandling/forklaringslogg-slice,
   etter 3–5 er på plass.

## 5. Bevisst utenfor scope (ikke glemt, bare rangert bak)

- «Testkommunen 2017 Vurdering av habilitet 2018» (rundskriv v4 §3) — venter på byggesteg 3.
- Byggesteg 8 (kunnskapsgraf/påvirkningsanalyse) og 9 (dashboard) — strukturelt umulige å bevise noe
  med før 1–7 har reelt innhold, jf. `06-veikart.md`.

## 6. Uavklarte spørsmål — venter på beslutning, ikke bare bygging

Disse er **eksplisitt nevnt som uløste, større funksjonsønsker** i `docs/11-brukerflyt-ny-tjeneste.md`
(linje 8–9), men aldri tatt videre til en design- eller prioriteringsbeslutning. I motsetning til §2
(ferdig avklart, venter kun på tur) og §2.6 (nytt forslag som trenger en diskusjonsrunde), er dette
spørsmål der vi ikke engang har landet retning ennå:

- **Daglig Lovdata-synkronisering (full + delta) for bedre søk på rettskilder.** **Delvis adressert
  2026-08-02** — se `docs/14-byggesteg5-teknisk-design.md` §6: en ny, søkbar **katalog**
  (`LovdataKatalogOppforingEntitet`, kun tittel+datokode+type) fjerner kravet om at brukeren allerede
  kjenner den eksakte datokoden ved import (`Importer.tsx`), og fornyer seg selv (lat, 24t) — men
  dette løser IKKE de tre opprinnelige underspørsmålene fullt ut: (a) *daglig fullimport* av selve
  rettskilde-**innholdet** (ikke bare katalogtittelen) skjer fortsatt kun ved eksplisitt
  brukervalgt import — ingen bakgrunnsjobb finnes fortsatt (`BackgroundService`/`IHostedService`);
  (b) *daglig delta* via Lovdatas `documentHistory`-endepunkt (krever egen API-nøkkel/registrering)
  er fortsatt ikke vurdert; (c) *fulltekstsøk mot allerede importert rettskilde-tekst*
  (`RettskildeNode.Tekst`, i dag kun tre-navigasjon) er en helt egen ting fra katalog-søket over —
  katalogen søker kun i Lovdatas egne titler, ikke i innholdet til rettskilder som faktisk er
  importert til denne appen. Postgres `tsvector`/`GIN`-indeks på `RettskildeNode.Tekst` ville
  fortsatt vært riktig løsning for (c), ingen ny avhengighet.
- **Valg av grafeditor-bibliotek.** `VilkarstreGraf.tsx` er i dag en egenhendig, enkel SVG-komponent
  (bevisst valg i byggesteg 4 runde 1 — «ingen ny npm-avhengighet», automatisk lagdelt layout, ikke
  dra-og-slipp). Det eksplisitte spørsmålet «bør vi velge et ekte grafbibliotek» (f.eks. React Flow/
  dagre for layout, eller noe annet) er aldri besvart — kun utsatt. Relevant igjen når/hvis byggesteg
  4 runde 2 (testmodul + full publiseringsmodell) eller et fremtidig dra-og-slipp-krav vekker temaet;
  ingen grunn til å bytte i dag uten et konkret behov dagens SVG-løsning ikke dekker.
- **Andre punkter fra samme kilde-setning, notert for fullstendighet** (ingen retning tatt): PDF/Word-
  import **av rettskilder** (fortsatt ikke løst — `POST /api/rettskilder/fil` tar fortsatt kun
  Lovdatas «XML-kompatible HTML»-format; PDF/Word-opplasting bygget 2026-08-02 er til
  **kunnskapsbiblioteket**, dvs. ekstra KI-kontekstmateriale, en helt annen ting enn å importere en
  ny rettskilde), data.norge.no-oppslag (lesing/søk — ikke høsting, som er separat og designet i
  `05-arkitektur-og-nfk.md` §1.2), egen håndbok-liste-side, mulighet til å velge underliggende
  rettskilde ved opprettelse av ny håndbok, og Forskrift→Lov-kobling utover den generelle
  kryssreferanse-mekanismen.
