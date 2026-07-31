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
| 2 | Tjenester + Begrep + Kodelister | ✅ Bygget. **Utestående:** Hendelse (CPSV Event) og Tjenesteavhengighet som ekte tabeller — designet 2026-07-31, ikke bygget (§2.1 under) |
| 3 | Presedensregister | ⬜ Ikke startet |
| 4 | Vilkårstre (grafeditor) | ✅ Runde 1 bygget og verifisert. Runde 2 (testmodul + full publiseringsmodell) ⬜ ikke startet |
| 5 | AI-forslag (utvidet: kunnskapsbibliotek + skillsbaserte agenter) | ⬜ Ikke startet — omfang avklart 2026-07-31 (§2.2 under), men verken kunnskapsbibliotek eller agenter er bygget |
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

### 2.2 Kunnskapsbibliotek + skillsbaserte AI-agenter (byggesteg 5, utvidet omfang)
*Full spesifikasjon: `06-veikart.md` byggesteg 5.*

- Opplastingsflate sentrert rundt Tjeneste (dokumenter/lenker/notater).
- Fem spesialiserte agenter (Tjenestebeskrivelse/Begrep/Vilkår+Vilkårstre/Håndbok/Rettskilder),
  kjørt i fast pipeline (rettskilder → begrep → vilkår → tjenestebeskrivelse/håndbok).
- Alt AI-generert lander som `foreslatt_av_ai`/`utkast` — aldri automatisk publisert.
- **Forutsetter ikke** byggesteg 3, men presedens (byggesteg 3) ville styrket "Rettskilder og
  strukturering"-agenten betydelig — vurder rekkefølge.

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

- I dag må et Vilkår opprettes separat (i Vilkårstre-siden) FØR det kan kobles til en tagget
  lovtekst-passasje — ingen vei fra «merk tekst i loven → opprett Vilkår direkte».
- Avklart omfang: ny handling på en umerket `kind='vilkar'`-tagg som (1) oppretter Vilkåret med
  juridisk grunnlag forhåndsutfylt fra taggen, (2) kobler taggen til det nye Vilkåret, (3) setter
  `Vilkår.TjenesteId` til valgt tjeneste — **uten** samtidig å plassere det i regelgrafen (det er et
  eget, tyngre steg, jf. §2.6 dimensjonen om håndboken som forfatterflate).
- Ikke bygget ennå — ingen eksplisitt avgjørelse om timing.

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
7. **Byggesteg 5 — Kunnskapsbibliotek + AI-agenter** (§2.2). Størst av de gjenstående — vurder om
   byggesteg 3 bør være ferdig først, siden «Rettskilder og strukturering»-agenten forutsetter et
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

- **Daglig Lovdata-synkronisering (full + delta) for bedre søk på rettskilder.** I dag er import
  ren pull, brukerinitiert (fil-opplasting eller ett-og-ett Lovdata-søk via `AK-3.3.5`) — det finnes
  ingen bakgrunnsjobb (bekreftet: ingen `BackgroundService`/`IHostedService`/cron noe sted i
  `src/`), og ingen fulltekstsøk-mekanisme på rettskilde-innholdet (bekreftet: ingen
  `tsvector`/fulltekstindeks noe sted). To atskilte spørsmål bak dette ene punktet: (a) *daglig
  fullimport* — hold hele Lovdata-bulk-datasettet (`05-arkitektur-og-nfk.md` §1.1) i synk mot vår
  DB, oppdager nye/endrede lover/forskrifter automatisk i stedet for kun ved manuelt søk; (b) *daglig
  delta* — mer presis, kun endringene siden forrige kjøring (Lovdatas strukturerte
  `documentHistory`-endepunkt krever API-nøkkel, jf. samme §1.1 — registrering hos Lovdata er en
  forutsetning her, ikke bare kode); (c) *bedre søk* er i realiteten et eget delkrav (fulltekstsøk
  mot rettskilde-tekst, i dag kun tre-navigasjon) som ville dra nytte av at (a)/(b) finnes, men kan i
  prinsippet bygges uavhengig (Postgres `tsvector`/`GIN`-indeks på eksisterende `RettskildeNode.Tekst`
  ville holde for v1, ingen ny avhengighet). Ingen av de tre er designet i detalj.
- **Valg av grafeditor-bibliotek.** `VilkarstreGraf.tsx` er i dag en egenhendig, enkel SVG-komponent
  (bevisst valg i byggesteg 4 runde 1 — «ingen ny npm-avhengighet», automatisk lagdelt layout, ikke
  dra-og-slipp). Det eksplisitte spørsmålet «bør vi velge et ekte grafbibliotek» (f.eks. React Flow/
  dagre for layout, eller noe annet) er aldri besvart — kun utsatt. Relevant igjen når/hvis byggesteg
  4 runde 2 (testmodul + full publiseringsmodell) eller et fremtidig dra-og-slipp-krav vekker temaet;
  ingen grunn til å bytte i dag uten et konkret behov dagens SVG-løsning ikke dekker.
- **Andre punkter fra samme kilde-setning, notert for fullstendighet** (ingen retning tatt): PDF/Word-
  import av kildedokumenter, data.norge.no-oppslag (lesing/søk — ikke høsting, som er separat og
  designet i `05-arkitektur-og-nfk.md` §1.2), egen håndbok-liste-side, mulighet til å velge
  underliggende rettskilde ved opprettelse av ny håndbok, og Forskrift→Lov-kobling utover den
  generelle kryssreferanse-mekanismen.
