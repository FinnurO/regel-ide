# 13. Backlog — samlet status og neste steg

*Samler alt som er avklart, men ikke bygget, på tvers av `06-veikart.md` (byggestegene),
`12-fasit-handbok-leveranse.md` (dimensjonsgapene mot fasiten) og designavklaringene fra
2026-07-31-runden (Hendelse/Tjenesteavhengighet, kunnskapsbibliotek, editor, vilkår-referanser).
Ment å oppdateres etter hver runde — ikke en engangs-plan.*

## 0. UX/datamodell-gjennomgang 2026-08-13 (10 punkter fra skjermbilder, plan `squishy-squishing-hearth`)

Etter at appen ble kjørt lokalt mot Bergen-korpuset (PR #30) ga Johann 10 konkrete tilbakemeldinger på
faktiske skjermbilder. Bygget, i planens rekkefølge:

1. **Eier vises overalt** — ny `useVirksomheter`-hook (`src/RegelIde.Web/src/virksomhet/`) slår opp
   `virksomhetId` mot faktisk navn i `RettskilderListe`/`RettskildeDetalj`/`TjenesterListe`/
   `BegreperListe` (badge/kolonne viser navn, aldri en rå GUID eller en generisk "Virksomhetseid"-badge).
2. **Sortering + filter** på `RettskilderListe`/`TjenesterListe`/`BegreperListe` — klikkbare
   kolonneoverskrifter (client-side, ingen ny avhengighet) + ett fritekstfilter, mønsteret gjentatt
   (ikke abstrahert til en delt hook, bevisst, se filenes egne endringer).
3. **Håndbok-rettskildeomfang som ekte tabell** (Tittel/Kildetype/Fjern) i stedet for komma-separerte
   pills, i `RettskildeDetalj.tsx`.
4. **Håndbok-metadata utvidet** — `InterntDokNr`/`Revisjonsnr`/`VedtattAv`/`Vedtaksdato`/`GyldigTil`/
   `KonsolidertDato` er nå redigerbare via en ny redigeringsform i `RettskildeDetalj.tsx` (utvidet
   `PATCH /api/rettskilder/{id}/metadata`). `Eli` er OG FORBLIR permanent skrivebeskyttet — aldri i
   requesten, kun vist.
5. **Menneskelesbar eId-visning** — ny `eidVisningstekst()` i `eidLenker.ts`
   (`"{kortnavn} § {nummer} — {overskrift}"`, `undefined`/rå-eId-fallback når noden ikke er kjent —
   ingen gjettet tekst), brukt i `TjenesteDetalj.tsx`s regelverksreferanser. **Gjenstående** (bevisst
   utelatt, se §5 under): samme mønster i `BegrepDetalj.tsx`/`Egenskapspanel.tsx`/`TjenesteVeiledning.tsx`.
6. **Revers-oppslag håndbok→lov** — ny `RettskildeRepository.ReferertAvAndreDokumenterAsync` +
   `GET /api/rettskilder/{id}/referert-av-dokumenter`, sideordnet den eksisterende
   tjeneste-varianten. Vist BÅDE som global liste og PER NODE i `RettskildeDetalj.tsx` (punkt 9).
7. **Paragraf-picker på Tjeneste** — `TjenesteDetalj.tsx`s "Koble referanse" viser nå en `<Select>` av
   den valgte rettskildens faktiske blad-noder (§ ikke kapittel-noder), fritekstfeltet består som
   markert "avansert / manuell eId".
8. **Brukerveiledning-konvergens (størst endring)** — `NettsideDokumentEntitet` er FJERNET.
   En nettside er nå en ordinær `RettskildeEntitet` (`Kildetype="Brukerveiledning"`,
   `Doctype="webside"`, `Importrolle="primaer"`), importert av ny `BrukerveiledningImportTjeneste.cs`
   (sideordnet `HandbokImportTjeneste.cs`), med ÉN `RettskildeNode` (`NodeType="side"`) — ingen
   oppfunnet seksjonsstruktur (§3.1 i `15-handbok-dokumentgraf-notat.md` er oppdatert). `/api/nettsider`
   er fjernet; to nye, smalere endepunkter (`/api/rettskilder/{id}/stier`, `.../nettside-lenker`) dekker
   det generiske `/api/rettskilder`-settet IKKE allerede fanger (§3.4-multi-sti, §3.2-lenker).
   `NettsiderListe.tsx`/`NettsideDetalj.tsx` fjernet; `RettskildeDetalj.tsx` har en ny gren for
   `kildetype === 'Brukerveiledning'` (Stier-badges + `RaaTekstMedLenker`, flyttet til
   `src/rettskilde/`). **Avklart under implementasjon**: `NettsideLenkeEntitet` konvergerte IKKE inn i
   `RettskildeReferanseEntitet` — feltene passer ikke uten friksjon (`TilEid` er påkrevd der, en
   nettside-lenke har det ofte ikke; `RaaHref`/`AnkerTekst`/`TilEidKandidat` ville blitt alltid-NULL-
   kolonner på en delt tabell). Den forble en egen, liten tabell, kun med FK-formen oppdatert
   (`FraNettsideDokumentId` → `FraNodeId`; `TilNettsideDokumentId`+`TilRettskildeId` kollapset til
   ÉN `TilRettskildeId`, siden alle lenkemål nå ER `RettskildeEntitet`-rader). Ny migrasjon
   `KonvergerNettsideTilRettskilde`. Kjernebeviset fra forrige runde
   (`NettsideDokumentgrafTests.Bundlingssiden_kobler_helt_frem…`) er bevart, oppdatert til den nye
   modellen, ikke slettet.
9. **Ingen duplikat lenkevisning** — løst naturlig av punkt 8: `RettskildeDetalj.tsx`s generiske
   "Referanser"-seksjon viser nå BÅDE `RettskildeReferanseEntitet`- og (for Brukerveiledning)
   `NettsideLenkeEntitet`-rader i SAMME liste, ikke to parallelle strukturerte tabeller.
10. Se punkt 8 — nettside-detaljens tidligere dupliserte "LENKER:"-blokk finnes ikke mer (hele siden
    som eide den er fjernet).

**Bevisst utelatt/gjenstående denne runden**: `eidVisningstekst` er ikke rullet ut i
`BegrepDetalj.tsx`/`Egenskapspanel.tsx`/`TjenesteVeiledning.tsx` (punkt 5, se over) — samme rå-eId-
visning som før, ingen regresjon, bare ikke forbedret ennå.

## 0.1 Tre funn fra en levende gjennomgang av UX-runden over (2026-08-13/14)

1. **Metadata-panelet splittet i to grupper** — `RettskildeDetalj.tsx`s Metadata-visning (les-modus)
   er nå to tydelig merkede tabeller i stedet for én udifferensiert liste: **«Fra Lovdata»**
   (ELI/Kortnavn/Konsolidert dato/Utgiver — skrivebeskyttet, kun populert for importerte
   Lov/Forskrift) og **«Lokalt forvaltet»** (Internt dok.nr/Revisjonsnr/Vedtatt av/Vedtaksdato/Gyldig
   til — redigerbar via «Rediger», populert for håndbøker o.l.). Ren visning, ingen API-/skjemaendring.

2. **Bugfiks: «Referert fra håndbøker/andre dokumenter» talte rettskildens EGNE interne referanser
   som om et annet dokument refererte den.** Sett i praksis på alkoholforskriften — seksjonen listet
   dusinvis av rader der «det andre dokumentet» var forskriften selv (dens egne §10-1→§10-6-type
   interne kryssreferanser, `Opprinnelse="import"`, fanget av `LovdataHtmlParser` ved import).
   `RettskildeRepository.ReferertAvAndreDokumenterAsync` filtrerer nå på `Opprinnelse == "manuell"`
   (nøyaktig skillet mellom «en jurist koblet dette fra en håndbok» og «lovens egen struktur, funnet
   ved import») pluss en eksplisitt `DokumentId != rettskildeId`-sjekk som forsvarslag. Ny
   regresjonstest: `RettskilderEndepunktTests.Referert_av_dokumenter_utelater_rettskildens_egne_interne_referanser`
   (bruker den ekte, allerede kjente §1-3→§1-5-selvreferansen i alkoholloven-fixturen). Samtidig
   gjort lesbar: `RettskildeDetalj.tsx` viser nå `TilEid` gjennom `eidVisningstekst` (samme mønster som
   `TjenesteDetalj.tsx`s regelverksreferanser) i stedet for rå eId-kjeder, med raw-eId som fallback
   når noden ikke er funnet ennå.

3. **Undersøkt, IKKE en bug**: «nytt begrep vises ikke i begrepslisten» ble reprodusert i en isolert
   kjørende instans (egen DB, porter 5287/5273) i worktreet. Konklusjon: **hypotese (b)**, ikke (a).
   Både `GET /api/begreper` og `POST /api/begreper` bruker konsekvent
   `bruker.VirksomhetId` fra samme `GjeldendeBrukerTjeneste.FinnAsync`-oppslag — ingen filterfeil,
   ingen client-side staleness (`BegreperListe.tsx` refetcher friskt ved hver mount). I browseren:
   opprettet «test-begrep-verifikasjon» som «Ola Fagansvarlig» (Testkommunen) — vises umiddelbart i
   lista. Byttet bruker til «Silje Jurist» (Agder fylkeskommune, en ANNEN virksomhet) via
   brukervelgeren — lista viser korrekt 0 begreper (Agder fylkeskommune har ingen). Byttet tilbake til
   Ola — begrepet er der igjen, uendret. Dette er korrekt multi-tenant-filtrering
   (`BegrepsregisterTjeneste.ListerForAsync(bruker.VirksomhetId)`), ikke en bug — men reelt
   FORVIRRENDE fordi hver `Bruker`-rad har én fast `VirksomhetId`, så «bytt bruker» i velgeren OGSÅ
   bytter virksomhet-kontekst uten at det er tydelig markert som sådan. Samme rotårsak som det
   parallelle identitets-UX-arbeidet adresserer — ingen kodeendring gjort her, bevisst utenfor scope
   for denne runden.

## 0b. Identitetsbrikke + brukerhåndtering 2026-08-14 (`feature/virksomhet-bruker-ux`)

Johann pekte på Kontaktlisteregisteret (en annen, ubeslektet Digdir-PoC) som referanseeksempel på at
det til enhver tid skal være tydelig **hvem man representerer** — ikke som design å kopiere, men som
forbilde for hva som manglet i regel-ide. Bygget:

1. **Identitetsbrikke øverst til høyre** — `App.tsx` har en ny `.topbar` (over `.innhold`, samme sted
   på alle sider) med en kompakt `IdentitetsBrikke` som alltid viser navn, virksomhet og rolle. Under
   testbruker-profilen er brikken en `@digdir/designsystemet-react` `Dropdown.Trigger` som åpner en
   `Dropdown` for å bytte testbruker — dette ERSTATTER den tidligere fullbredde `<select>`en i
   sidebaren, ikke i tillegg til den. Under ekte Altinn-innlogging vises identiteten som ren tekst
   uten bytt-mulighet (`ekteInnlogging`/`innloggingsfeil`-håndteringen fra `BrukerContext.tsx` er
   bevart uendret).
2. **Ny brukerhåndteringsside** (`/brukere`, `src/RegelIde.Web/src/pages/BrukereListe.tsx`) — lister
   ALLE brukere (`GET /api/brukere` utvidet til å returnere både testbrukere og ekte Altinn-brukere,
   med nytt `ErAltinnBruker`-felt på `BrukerDto`; en `Tag` skiller de to typene tydelig i tabellen).
   Nytt opprett-skjema (Navn/Rolle/Virksomhet) og inline rediger-rad (Rolle+Virksomhet) bruker to nye
   endepunkter, `POST /api/brukere` og `PUT /api/brukere/{id}`, støttet av en ny
   `BrukerregisterTjeneste` (`src/RegelIde.Data/BrukerregisterTjeneste.cs`, samme primary-constructor-
   DI-mønster som `BegrepsregisterTjeneste`/`TjenesteregisterTjeneste`). `BrukerContext.tsx` filtrerer
   bort `ErAltinnBruker`-rader for testbruker-velgeren og eksponerer en `lastBrukerePaNytt()` som
   brukerhåndteringssiden kaller etter opprett/rediger, slik at identitetsbrikken viser en nyopprettet
   bruker uten full sidelast (verifisert i nettleser: ny bruker opprettet, tilordnet en virksomhet, og
   deretter valgt i brikken — samme mekanisme som `X-Bruker-Id`/`GjeldendeBrukerTjeneste` alltid har
   brukt, ingen egen innloggingsvei).
3. **Bevisst utelatt denne runden**: ingen ny virksomhet-opprettelse-UI (kun tilordning til
   *eksisterende* virksomhet var etterspurt); ingen endring av selve Altinn-autentiseringsflyten (kun
   hvordan den vises); ingen RBAC-håndhevelse (rollen kan settes og vises, men begrenser ennå ikke hva
   en bruker faktisk kan gjøre — samme kjente gap som i `16-vurdering-rettskilde-til-tjenestebeskrivelse.md`
   §4); ingen sletting av brukere (reiser spørsmål om hva som skjer med data en slettet bruker "eier" —
   utenfor scope).

## 1. Byggesteg-status

| # | Byggesteg | Status |
|---|---|---|
| 0 | Lås ontologien (Vilkår/Regel/Unntak) | ✅ Fullført 2026-07-23 |
| 1 | Rettskildebibliotek (+ håndbok/rundskriv-forfatterflyt) | ✅ Bygget og verifisert. Utvidet 2026-07-31 med håndbok-nivå rettskildeomfang (§3 under) |
| 2 | Tjenester + Begrep + Kodelister | ✅ Bygget, inkl. Hendelse (CPSV Event) og Tjenesteavhengighet som ekte tabeller (§2.1, ferdig 2026-07-31) |
| 3 | Presedensregister | ⬜ Ikke startet |
| 4 | Vilkårstre (grafeditor) | ✅ Runde 1 bygget og verifisert, inkl. tekst-først «opprett vilkår fra tagg»-flyt (§2.5). Runde 2 (testmodul + full publiseringsmodell) ⬜ ikke startet |
| 5 | AI-forslag (utvidet: kunnskapsbibliotek + skillsbaserte agenter) | ✅ Runde 1 ferdig 2026-07-31 — to agenter («Identifiser tjenester»/«Identifiser begrep») + `IKiAgentKlient`-stub. ✅ Runde 2 ferdig 2026-08-02 — ekte KI-leverandør, fil-opplasting, Lovdata-søk. ✅ Runde 3 ferdig 2026-08-10 — generisk KI-klient, resten av CPSV-AP-NO-feltene, ny testcase. ✅ Runde 4 ferdig 2026-08-10 (§2.2 under) — relaterte tjenester/regelverksreferanser fra agenten + RAG-spike (uten pgvector) mot samme kost-/kvalitetsproblem. ⬜ De tre resterende agentene (Tjenestebeskrivelse/Vilkår-og-Vilkårstre/Håndbok) gjenstår. |
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

**✅ Runde 4 ferdig 2026-08-10.** To parallelle, avgrensede spor (se
`docs/14-byggesteg5-teknisk-design.md` §8 for teknisk detalj), utløst av live-testing av runde 3 som
avdekket et reelt kost-/kvalitetsproblem (en kjøring med 6 rettskilder, ~49k input-tokens, ga et tomt
`[]`-svar) og to dokumenterte agent-gap:

- **Spor A — Relaterte tjenester + regelverksreferanser.** «Identifiser tjenester» kan nå foreslå
  `TjenesteavhengighetEntitet`-relasjoner (til EN eksisterende tjeneste via server-nummererte `E#`,
  eller til en annen ny tjeneste i samme batch via `T#` — samme "server nummererer, agent
  refererer"-prinsipp som eId-fiksen i runde 3) og `TjenesteRegelverksreferanseEntitet`-koblinger
  (eksakte `[eId]`-tagger). Uoppløselige/hallusinerte referanser droppes stille, aldri hele batchen.
  Ny `"har_del"`-`Rel`-verdi (dekker `dct:hasPart`-siden av det fjerde CPSV-AP-NO-konseptet fra
  §2.2 runde 3 — kun lettet, ikke fullt løst). Bonus: `TjenesteavhengighetregisterTjeneste.
  OpprettAsync` hadde tidligere INGEN sykel-sjekk — ny bounded BFS lagt til (gjelder både UI og
  agent). Ingen frontend-endring — `TjenesteDetalj.tsx` viser de nye koblingene automatisk.
- **Spor B — RAG-spike (uten pgvector, bevisst valg — se §8.2 i teknisk design for begrunnelsen).**
  Ny `IEmbeddingKlient`/`EmbeddingKlientOpenAiKompatibel`/`EmbeddingKlientStub`, embeddings lagret
  som en vanlig Postgres `double precision[]`-kolonne (`RettskildeNodeEmbeddingEntitet`), kosinuslikhet
  i ren C#. `TjenesteforslagTjeneste.KjorForslagMedRagAsync` er en ALTERNATIV kjørevei (erstatter
  ikke `KjorForslagAsync`) som henter kun de K mest like rettskilde-nodene mot kunnskapsbibliotekets
  lenke-/fil-tekst som "spørsmål" — for direkte, rå sammenligning mot dagens dump-alt-baseline.
- 223 backend-tester i `RegelIde.Data.Tests` (opp fra 213 — 10 nye) + 164 i `RegelIde.Api.Tests`,
  alle grønt. Bifangst: fikset en latent `EmbeddedPostgresFixture`-bug (manglende try/catch rundt
  `_server?.Dispose()`) som blåste opp rapportert feilantall under denne rundens testing — se §8.3 i
  teknisk design.

**Eksplisitt IKKE gjort i runde 4 — deferred, ikke glemt:**

*Fra Spor A:*
- De tre andre CPSV-AP-NO-konseptene fra runde 3 (`cpsv:hasParticipation`, `cpsv:hasInput`,
  `dct:spatial`) er FORTSATT ikke modellert.
- `"har_del"` gir agenten riktig `Rel`-verdi, men ingen egen typed hasPart-struktur (rekkefølge/
  komposisjon) — `dct:requires`-vs-`hasPart` er lettet, ikke fullt løst.
- Agenten ser kun TITTEL på eksisterende tjenester når den vurderer relasjoner, ikke beskrivelse/
  CPSV-felt — kan gi upresise forslag der to tjenester har like titler men ulikt innhold.

*Fra Spor B:*
- «Identifiser begrep» er IKKE del av RAG-spiken — ingen naturlig retrieval-anker identifisert for
  den agenten (ingen kunnskapsbibliotek-side å måle mot).
- Kunnskapsbibliotek-lenker/filer chunkes/embeddes IKKE — fortsetter å dumpes fullt ut. Sannsynlig
  neste store kostnadsdriver etter at rettskilde-noder er løst med RAG.
- Ingen pgvector/produksjons-skala vektorindeks — spiken bruker cosinelikhet i C# over en vanlig
  array-kolonne. Egen, senere beslutning hvis RAG viser seg nyttig i praksis.
- Ingen automatisk re-embedding ved reimport/versjonering av en rettskilde — embeddings beregnes
  lazy uten invalideringsstrategi hvis nodetekst endres.
- ~~Hvilken leverandør som faktisk tilbyr embeddings er ubekreftet~~ **Bekreftet 2026-08-10**:
  HostYourAI har et eget embeddings-endepunkt (`POST /api/v1/embeddings`, modell
  `BAAI/bge-multilingual-gemma2` — flerspråklig), separat fra chat-completions.
- ~~Ingen automatisert evaluering/scoring av RAG-vs-dump-alt~~ **Rå (ikke automatisert)
  sammenligning faktisk kjørt 2026-08-10**, OG en faktisk innholdsgjennomgang av forslagene (ikke
  bare tokens) mot alkoholloven (~276 noder) via et nytt, ikke frontend-koblet endepunkt
  (`POST /api/tjenester/forslag/kjor-rag`) — se `docs/14-byggesteg5-teknisk-design.md` §8.4 for
  fullt funn. Kort oppsummert: RAG med K=40 brukte **84 % færre input-tokens** enn dump-alt
  (4 479 vs. 28 830) og ga et ikke-tomt svar der dump-alt SAMME kontekst var ustabilt (tomt ett
  forsøk, rikt et annet). MEN en innholdssammenligning viste at RAG-forslagene var vesentlig
  tynnere — **snitt feltfullstendighet falt fra 83 % til 28 %** (kanaler/kostnad/behandlingstid/
  kontaktpunkt/språk nesten alltid tomme), 2 av 6 var dubletter av dump-alt-funn (med færre felt
  utfylt), og 3 av 6 var tvilsomme som egne CPSV-tjenester (en saksbehandlings-beskrivelse, en
  tilsynsplikt, en regulering/forbud). Netto reelt nye, velformede tjenester fra RAG: trolig 1, ikke
  6. RAG med K=20 ga et tomt svar. **Konklusjon revidert**: RAG er billigere og unngikk denne
  gangen dump-alts tomme-svar-problem, men prisen var vesentlig lavere feltfullstendighet — ikke en
  entydig seier. Tynt datagrunnlag (én rettskilde, én kjøring per K) — ikke et statistisk bevis.
- Underveis avdekket og fikset: `RettskildeEmbeddingTjeneste`/`EmbeddingKlientOpenAiKompatibel`
  kalte embeddings-API-et ÉN GANG PER NODE, sekvensielt, uten batching/backoff — traff HostYourAI
  sin `429 Too Many Requests` konsekvent ved ~276 noder. Fikset: `IEmbeddingKlient.EmbedAsync` tar
  nå en LISTE av tekster (batcher 16 noder per kall, standard OpenAI `input`-som-array-format) +
  enkel retry-med-backoff (maks 3 forsøk) på 429.
- **Ny, konkret post**: ingen systematisk tuning/undersøkelse av riktig K (antall noder RAG henter)
  — K=20 var for lite for alkoholloven (tomt svar), K=40 unngikk det tomme svaret men ga fortsatt
  vesentlig lavere feltfullstendighet enn dump-alt. En fast K uavhengig av rettskildens
  størrelse/antall noder er sannsynligvis feil for en fremtidig, mindre eksperimentell versjon;
  bør trolig skalere med enten rettskilde-størrelse eller en likhets-terskel i stedet for et fast tall.
- **Ny, konkret post**: uklart OM feltfullstendighets-fallet (83 % → 28 %) er en egenskap ved
  RAG-retrieval generelt (mindre kontekst per node → mindre grunnlag for agenten å utlede
  sekundærfelt fra) eller en prompt-svakhet som kan fikses uavhengig av kontekst-størrelse (f.eks.
  presisere i system-instruksen at sekundærfelt skal utledes aktivt fra det agenten faktisk ser,
  ikke bare når det er "tydelig"). Ikke undersøkt — se full innholdssammenligning i
  `docs/14-byggesteg5-teknisk-design.md` §8.4.
- **Foreslått, IKKE bygget — Spor C: generer → forankre → verifiser** (Johanns forslag etter §8.4-
  funnet). Snur rekkefølgen: agenten foreslår kandidat-tjenester FØRST (billig, fra paragraf-/
  kapittel-overskrifter, ikke hvert ledd/punkt), og RAG brukes ETTERPÅ til å forankre/verifisere
  HVERT forslag mot sin egen, presise spørsmålsvektor — løser trolig feltfullstendighets-fallet
  over, siden retrieval ikke lenger deler én kompromiss-pool på alle forslagene. Trolig DYRERE
  totalt enn dagens RAG (flere KI-kall, ikke færre — kostnadsdriveren er verifiseringsstegets N
  chat-kall, ikke embeddings), ikke billigere. Full skisse med åpne spørsmål:
  `docs/14-byggesteg5-teknisk-design.md` §8.5. Ikke tatt stilling til om det skal bygges.
- **Foreslått, IKKE bygget — er selve chunkingen (ledd/punkt-nivå) riktig for RAG?** Ekstern
  analyse (CoPilot) sammenlignet mot koden bekreftet to konkrete svakheter uavhengig av Spor A/B/C:
  (1) teksten som embeddes har ingen forelder-kontekst (paragraf-`Overskrift`/`Nummer` ligger på en
  ANNEN node enn ledd-teksten som embeddes), (2) chunking på ledd-nivå fragmenterer det dump-alt ser
  som én sammenhengende paragraf/tjeneste — plausibel forklaring på §8.4s feltfullstendighets-fall.
  Tre lagdelte, ikke-bygde fikser (billigst→dyrest: forelder-kontekst inn i embedding-teksten →
  utvid til søskenledd ved henting → reranking-steg) — full presisering, inkl. hvorfor
  forarbeider/dommer-delen av rådet ikke er anvendbar før byggesteg 3 (Presedensregister) finnes:
  `docs/14-byggesteg5-teknisk-design.md` §8.6. Rangert FØR et eventuelt Spor C siden begge deler
  samme underliggende chunking.

**Mottatt, ikke bygget — ekstern konsolidert analyse (Claude Chat, 2026-08-12).** Tre-rundes
research (PTV/Suomi.fi åpne API, Finlands innholdsproduksjonsveiledning, Arum/rulemapping.org),
sammenlignet mot koden. Fem punkter verifisert/korrigert direkte mot repoet:

- **To registre, ikke ett skjema.** Finlands egen innholdsveiledning sier eksplisitt at tilsyn/
  rapportering/interne vedtak IKKE er CPSV-tjenester («Services do not refer to the tasks of the
  organisation») — presist det motsatte av hva jeg selv trakk tilbake i forrige runde. Konklusjon:
  Regel-IDE trenger et EGET register for lovpålagte forvaltningsoppgaver (kundevendt-uavhengig) i
  tillegg til `TjenesteEntitet` (kundevendt, kanalbærende) — §8.4s «Tilsyn med privat innførsel»/
  «Forbud mot skjenking utenfor lokaler» er ikke hallusinasjon, de er reelle funn uten riktig boks.
  Fullt skjemaforslag (`ForvaltningsoppgaveEntitet`/`OppgaveTjenesteEntitet`/`FeltkildeEntitet`) og en
  deterministisk «sveip»-arkitektur som alternativ til RAG (100 % dekning ved konstruksjon, sporbar
  til én eId per funn) — se §2.7 under. **Dette er en arkitekturbeslutning, IKKE en byggeklar post.**
- **Korpuset er invertert — antakelse, ikke bekreftet for Testkommunen (se R1(b) under).**
  Analysens tese: rettskilde-noder (RAG-embedded, §8.2) bærer normalt IKKE
  kanaler/kostnad/behandlingstid/kontaktpunkt — kunnskapsbiblioteket gjør, men er IKKE chunket/
  embedded (dumpes fullt i begge kjøreveier), så RAG-investeringen gikk til korpuset som ikke bærer
  de omstridte feltene. **R1(b) fant det motsatte for Testkommunen**: kunnskapsbiblioteket der er så
  tynt (én lenke, under 10 ord) at det aldri kunne vært kilden — feltene som faktisk er ekte
  (gebyr/frist/inndragning) kommer fra RETTSKILDEN, ikke biblioteket. Tesen kan fortsatt stemme for
  en virksomhet med et reelt kunnskapsbibliotek (Agder fylkeskommune har opplastede filer med
  utvunnet tekst) — ikke testet der. Fortsatt riktig at kunnskapsbiblioteket ikke er chunket/embedded
  og er en sannsynlig fremtidig kostnadsdriver, uavhengig av dette funnet.
- **R1 («det gratis eksperimentet») kjørt 2026-08-12 — resultatet er IKKE et rent ja/nei mellom
  §8.6-hypotesen og konfabulering, begge har delvis rett.** Søk i selve alkoholloven-fixturen
  (`data/kilder/raw-lovdata/alkoholloven-LOV-1989-06-02-27.html`) etter de faktiske verdiene dump-alt
  fylte inn i de 13 av 15 forslagene med sekundærfelt utfylt (se §8.4/artefakten):
  - **Ekte, i lovteksten** — IKKE konfabulert: «4 måneder»-fristen (§ 1-7a, viser til tjenesteloven
    § 11 — men med et unntak for enkelte bevillinger dump-alt ikke skiller ut, altså mulig OVER-
    generalisering, ikke oppspinn), «årlig gebyr»/«årlig bevillingsgebyr» (§ 6-8/§ 6-9, ordrett),
    «Inndragning av bevilling» (§ 1-8, ordrett). Støtter §8.6s paragraf-fragmenteringshypotese — disse
    er nettopp den typen prosedyre-detalj som ofte står i en paragrafs SENERE ledd, adskilt fra
    "hvem kan søke"-leddet RAG rangerer høyest.
  - **Ikke i lovteksten i noen form** — ikke sporbart til rettskilde uansett kontekst-bygger:
    `kanaler` (`"fysisk"`/`"digitalt"` på samtlige forslag) og `sprak` (`"norsk"` på samtlige) — null
    treff på noe tilsvarende i fixturen. LLM-ens egne, plausible standardvalg, ikke uttrekk.
    `kontaktpunkt` er kun en kopi av `kompetentMyndighet`, ingen egen kilde. Disse feltene er
    strukturelt IKKE lovtekst-sporbare for noen RAG-fiks — de hører til Finlands **lokale lag** (A.4
    i analysen: virksomheten, ikke rettskilden, bestemmer kanal/språk i praksis) — en fremtidig
    to-lagsskjema (§2.7-tilstøtende, se `docs/14 §8.4`) løser dette bedre enn chunking gjør.
  - **Konsekvens**: dump-alts 83 % feltfullstendighet i §8.4 er delvis kunstig høy fra ugrunngede
    default-verdier (kanaler/språk), ikke bare fra reelt bedre uttrekk enn RAG. §8.6s tre fikser
    (forelder-kontekst i embedding, søskenledd ved henting, reranking) er fortsatt riktig retning for
    kostnad/behandlingstid/konsekvensVedBrudd — men vil aldri løse kanaler/språk, uansett hvor god
    chunkingen blir.
  - **R1(b) kjørt 2026-08-12 — entydig resultat, korrigerer A.2s «korpuset er invertert»-antakelse
    for denne testcasen.** Testkommunens kunnskapsbibliotek slettet helt (dens ENE lenke,
    `https://testkommunen.no/tjenester — Om tjenestetilbudet` — allerede for tynn til å bære noe
    reelt, se `TjenesteforslagTjeneste.cs` linje ~162), dump-alt kjørt på nytt mot samme alkoholloven
    (ekte kall mot HostYourAI, `POST /api/tjenester/forslag/kjor`), lenken gjenopprettet etterpå.
    Resultat: **89,7 % feltfullstendighet, 14 forslag, 28 902 input-tokens** — HØYERE
    feltfullstendighet enn originalkjøringen med lenken til stede (83 %), og praktisk talt identisk
    tokenforbruk (kunnskapsbiblioteket bidro med under 10 ord til konteksten). Konklusjon: for DENNE
    testcasen kom **null** av dump-alts feltfullstendighet fra kunnskapsbiblioteket — alt kommer fra
    rettskilde-nodene (de ekte verdiene, jf. punktet over) og fra LLM-ens egne standardvalg
    (kanaler/språk, nå enda mer konsekvent — `["digitalt","fysisk"]` på samtlige 14 forslag). A.2s
    "korpuset er invertert"-tese (at kunnskapsbiblioteket bærer de omstridte feltene) stemmer altså
    IKKE empirisk her — det gjorde det aldri en forskjell fordi lenken var for tynn til å bære noe i
    utgangspunktet. Dette lukker en reell åpen usikkerhet fra forrige runde (§8.4: "uklart OM
    feltfullstendighets-fallet er en egenskap ved RAG-retrieval eller en prompt-svakhet") ytterligere
    mot RAG-retrievalens egen mekanisme (§8.6-fragmentering) som hovedforklaring, ikke
    kunnskapsbiblioteket. Merk: dette er ett datapunkt for én virksomhet med et nesten tomt
    kunnskapsbibliotek — konklusjonen kan være annerledes for Agder fylkeskommune, som HAR reelt
    kunnskapsbibliotek-innhold (opplastede filer med utvunnet tekst).
- **Faktisk feil i den eksterne analysen, rettet før noe bygges på den**: rapportens skisse (del B.5,
  "sveipe"-arkitekturen) forutsetter at "henvisningsfølging er deterministisk … fra lenker som
  allerede finnes i rådataen". Sjekket direkte mot koden (`RettskildeImportTjeneste.cs`,
  `Entiteter.cs`) — **dette finnes ikke**. Kun `ParentNodeId` (vertikal hierarki: ledd→paragraf→
  kapittel, + `Overskrift`/`Nummer` på forelder) finnes; INGEN lateral kryssreferanse-graf mellom
  paragrafer (f.eks. "§ 1-7b viser til § 3-2") er bygget noe sted. Sveipets foreldre-kontekst-del er
  byggbar i dag helt gratis; henvisnings-utvidelsen krever at en kryssreferanse-graf bygges FØRST fra
  AKN-importen — en ekte, ikke-triviell ny arbeidspost rapporten framstiller som allerede løst.
- **Del C (agentløkke med verktøy vs. enkeltstående kall) bekrefter en konklusjon koden allerede
  støtter**: over rettskilde er `ParentNodeId`/`Overskrift`/`Nummer` nok til å gi et sveip deterministisk
  foreldre-kontekst gratis — ingen agency nødvendig. Agency gir trolig reell verdi kun over
  kunnskapsbibliotekets ustrukturerte dokumenter (ingen eId, ingen henvisningsgraf, ingen garanti om
  hvor et gebyr står) — anbefalt rekkefølge der: sveip først, kode-basert henvisningsutvidelse andre,
  smalt agentisk søkeverktøy (kun over kunnskapsbiblioteket) sist og kun hvis 1+2 etterlater et målt
  gap. Ingen ny abstraksjon i `IKiAgentKlient` trengs for de to første stegene.
- Full kildeliste, Finland-API-adresser (åpent PTV API, CC0, uten innlogging —
  `api.palvelutietovaranto.suomi.fi`) og hva analysen selv flagger som ikke lest/ikke reprodusert:
  se den delte rapporten i samtalehistorikken for denne runden (ikke kopiert inn her for å unngå
  drift — dette avsnittet er et sammendrag med verifiseringsstatus, ikke primærkilden).

**⬜ Fortsatt gjenstår** (fortsatt retningsnivå, ikke bygget): de tre andre agentene
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

### 2.7 To registre: CPSV-tjeneste vs. forvaltningsoppgave — forslag til å løse §8.4s skjemamismatch
*Forslag mottatt 2026-08-12 (ekstern konsolidert analyse, Claude Chat — se §2.2s "Mottatt, ikke
bygget"-punkt over for full verifisering mot koden). Samme status som §2.6: et analysert forslag,
ikke en kravspesifikasjon.*

**⚠️ SUPERSEDT 2026-08-12 av `docs/15-handbok-dokumentgraf-notat.md` §10.2.** Et oppfølgingsnotat
samme dag foreslo en billigere løsning på samme problem: én `TjenesteEntitet` med `Objekttype`-
diskriminator (`tjeneste`/`forvaltningsoppgave`) + to SHACL-former + en hard eksportsluse
(`Objekttype=tjeneste` er det ENESTE som emitteres som `cpsv:PublicService`), i stedet for en helt
separat `ForvaltningsoppgaveEntitet`-tabell. Løser samme skjemamismatch, samme dekningsdifferanse
(§5.3/§10.2 i det nye notatet), men uten en ny tabell å holde synkron med `TjenesteEntitet`. Denne
seksjonen (§2.7) stå igjen som historikk — se `docs/15` for den oppdaterte anbefalingen, inkl. en
navnekollisjons-advarsel (`TjenesteEntitet` har allerede et `Tjenestetype`-felt, forskjellig fra det
foreslåtte `Objekttype`).

**Kjernen:** §8.4 fant at 3 av 6 RAG-forslag var "tvilsomme som egne CPSV-tjenester" (en
saksbehandlings-beskrivelse, en tilsynsplikt, et forbud). Finlands egen innholdsveiledning sier
tilsyn/rapportering/interne oppgaver bevisst IKKE skal beskrives som CPSV-tjenester — agenten fant
reelle forvaltningsoppgaver, men skjemaet har ingen boks for dem utenom `Tjeneste`. Foreslått:

- `ForvaltningsoppgaveEntitet` (`RettskildeId` påkrevd, `Normtype` ∈ plikt/kompetanse/forbud/
  definisjon, `Adressat`) — ETT register per bestemmelse, bygget av et deterministisk «sveip» over
  rettskildens struktur (paragraf for paragraf, ikke RAG/top-K) i stedet for et generert forslag.
  100 % dekning ved konstruksjon («vi har besøkt hver bestemmelse»), sporbar til én eId per funn.
- `OppgaveTjenesteEntitet` (kobling oppgave↔tjeneste; fravær av rad = eksplisitt etterlevelseshull).
- `FeltkildeEntitet` (provenans per CPSV-sekundærfelt — `KildeType`/`KildeRef`/`Utdrag`), som gjør
  §8.4/R1(a)-funnet over (kanaler/språk uten kildegrunnlag) strukturelt synlig i stedet for noe man må
  oppdage ved manuell gjennomgang: uten kilde skrives feltet aldri.
- Leveransen er en DIFFERANSE, ikke en katalog: `Oppgaver ∖ Tjenester = etterlevelseshull` (det
  egentlig verdifulle funnet), `Oppgaver ∩ Tjenester = CPSV-katalogen` (biprodukt).

**Vurdering — reell, ikke-triviell kostnad, ikke bare en ekstra tabell:** krever et sveip-endepunkt
(65 kall à ~2000 tokens for alkoholloven ≈ 130k input-tokens, 4-5× dyrere per kjøring enn dagens
dump-alt, mot bevisbar dekning), en ny kodeliste-autorisert typologi for `Normtype`/klassifisering
(gjenbruk byggesteg 2s kodelistemaskineri), og et åpent spørsmål om `Forvaltningsoppgave`/`Tjeneste`
bør være separate tabeller eller én med diskriminator (kodebasens etablerte mønster peker mot
separate, men semantikken overlapper mer her enn i eksisterende par som `KunnskapsbibliotekLenke`/
`-Fil`). Løser samtidig to av Spor Cs (§8.5) åpne spørsmål (terskel for "godt nok forankret" faller
bort — kriteriet blir om en kildechunk faktisk inneholder verdien, ikke en kosinusverdi), men
forutsetter en henvisningsgraf som IKKE finnes i dag for full "hent bestemmelser paragrafen viser
til"-funksjonalitet (se §2.2-korreksjonen over) — kan bygges uten den, bare med redusert kontekst per
sveip-kall.

**Status:** ikke bygget, ikke tatt stilling til av Johann ennå — venter på en egen avklaringsrunde
(samme prosess som ontologilåsen 2026-07-23 og §2.6), ikke bare et «ja, bygg det».

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
7. **Byggesteg 5 runde 5 — stabiliser agenten før mer måling (§2.2s "Mottatt, ikke bygget"-punkt,
   R0).** ✅ **R1(b) kjørt 2026-08-12** — se §2.2 for fullt resultat (89,7 % feltfullstendighet med
   tomt kunnskapsbibliotek, høyere enn originalens 83 %; null av verdien kom fra biblioteket for
   Testkommunen). Underveis avdekket: dump-alt-endepunktet traff en `TaskCanceledException` (100s
   HttpClient-timeout mot HostYourAI) på FØRSTE forsøk, lyktes på andre — samme type transient
   ustabilitet §8.4 allerede dokumenterte (der som tomt `[]`-svar, her som timeout), ikke en ny feil,
   men ytterligere en konkret grunn til R0. ✅ **R0 bygget 2026-08-12** — ny delt
   `KiForslagRetryHjelper` (ett retry ved tomt forslag-array, etter parsing i
   `TjenesteforslagTjeneste`/`BegrepsforslagTjeneste`) + ett retry ved transient HTTP-feil/timeout
   direkte i `KiAgentKlientOpenAiKompatibel.GenererAsync` (fast 300ms, ikke doblende backoff — en
   engangs-timeout, ikke rate-limiting). `ILogger<T>` lagt til (valgfri, non-breaking). 438/438
   backend-tester grønt (9 nye). **`n≥5` per arm-målingsinfrastruktur er fortsatt ikke bygget** —
   egen, senere evalueringsoppgave, ikke en klient-kodeendring.
8. **Håndbok/dokumentgraf-notatet (`docs/15-handbok-dokumentgraf-notat.md`) — egen avklaringsrunde,
   ikke kode.** Mottatt og konsolidert mot koden 2026-08-12. **Avklaringsrunde 1 kjørt samme dag**
   (se `docs/15` §13) — tre av fire Trinn 0-punkter nå LÅST: AKN som serialisering (ikke
   primærlager), én `TjenesteEntitet` med `Registertype`-diskriminator (ikke `Objekttype`, ikke to
   tabeller), `RettsligStatus` splittet i `NormativVirkning`/`FunksjonellRolle`. **Fortsatt åpent**:
   om `NormativVirkning="bindende_forvaltning"` er riktig snitt for retningslinjer generelt
   (Schartum-spørsmål, ikke teknisk). **Avklaringsrunde 2 kjørt samme dag** (se `docs/15` §14) —
   CPOV-sjekk og AKN-XSD-vei i .NET nå også avklart (CPOV bekreftet irrelevant for
   forvaltningsoppgave-spørsmålet; intet .NET AKN-bibliotek finnes; full XSD-kodegenerering vurdert
   lite verdifullt). ✅ **Trinn 1, punkt 1-4 bygget 2026-08-12** — ny `HandbokTekstParser.cs`
   (regex-segmentering på dokumentets egen nummerering, ingen KI), skjemautvidelse på
   `RettskildeEntitet`/`Virksomhet` (de låste feltene fra §13), ekte testfixture (Bergens
   retningslinjer, faktisk hentet via WebFetch — ikke syntetisk). Konkludert at
   `RettskildeReferanseEntitet` kan bære `hjemlet_i`/`kryssrefererer` uendret, ingen ny tabell.
   451/451 backend-tester grønt (uavhengig verifisert). **Ikke gjort**: AKN-eksport/rundtur
   (punkt 5, bevisst utenfor scope), import-endepunkt for håndbøker, `FunksjonellRolle`-populering,
   GUID-oppslag av `hjemlet_i` mot en faktisk importert rettskilde (krever et import-endepunkt som
   ikke finnes ennå). ✅ **Utvidet 2026-08-13 (Johanns eksplisitte instruks: «alle 21 sider + begge
   PDF-er»)**: Bergens forskrift som andre PDF-fixture (`HandbokTekstParser` utvidet med
   `TallpunktumSeksjonMønster` for en reelt annen dokumentstruktur, regresjonstestet mot
   retningslinjene). Ny `NettsideDokumentEntitet`/`NettsideStiEntitet`/`NettsideLenkeEntitet`
   (§3.1/§3.2 — ikke bygget før nå), 23 ekte Bergen-sider som fixtures. **§3.4s "samme nodene, to
   stier"-påstand presisert, ikke fullt bekreftet**: 20 av 21 sider deler begge stier, «Krav om
   fettutskiller» har kun én (reelt, testet unntak). Kjernebevis
   (`Bundlingssiden_kobler_helt_frem_til_importerte_rettskilder_pa_eli_og_url`): en nettside kobles
   via `lovdatalenke`/`lenker_til` HELT FREM til ekte importerte `RettskildeEntitet`-rader
   (alkoholloven/alkoholforskriften, GUID-matchet på `Eli`). Ekte funn: Bergens egne Lovdata-lenker
   bruker minst tre URL-format over årene, kun det moderne håndteres (ingen gjettet fallback for de
   to eldre). 477/477 backend-tester grønt (uavhengig verifisert, inkl. en csproj-mergekonflikt løst
   mot samtidig AKN-fix-arbeid). **Ikke gjort**: `NettsideSeksjon` (dokument-granularitet denne
   runden), `NettsideHenterTjeneste`/live henting, `presentasjonsvariant`-kanten (krever KI),
   `FunksjonellRolle`. Overlapper delvis med byggesteg 3 (Presedensregister, punkt 6) og byggesteg 5
   (punkt 7/9 under) — bør trolig fortsette som egen avklaringsrunde uavhengig av hvilken
   byggesteg-rekkefølge som ellers
   velges.
   ✅ **Applikasjonslaget bygget 2026-08-13** (Johanns eksplisitte instruks: «vi må jo kunne lagre og
   behandle det vi henter») — de to forrige rundenes bibliotek-/testnivå-modell er nå faktisk koblet til
   en kjørende database, API-endepunkter og en frontend-side:
   - Ny `HandbokImportTjeneste.cs` (RegelIde.Data) — persisterer `HandbokParseResultat` som en ekte
     `RettskildeEntitet` (**`Importrolle="primaer"`, ikke `"referanse"`** — se nedenfor for hvorfor dette
     avviker fra forrige rundes seedede eksempel) + node-tre, to-pass (Eid→Guid FØRST, deretter
     `ParentNodeId`-oppslag) siden HandbokTekstParser IKKE garanterer foreldre-før-barn-rekkefølge.
     `Kryssrefererer`→`RettskildeReferanseEntitet` skrevet etter at hele treet er lagret. `HjemletI`
     løses KUN til en `HandbokRettskildeomfangEntitet` (håndbok-nivå, ikke paragraf-presisjon) når
     eksakt ÉN Lov/Forskrift-Tittel matcher `EksternLovnavn` — null/flere treff forblir ULØST, ALDRI
     gjettet (se `AntallHjemletILovnavnUlost`-diagnostikken).
   - **Ekte funn under bygging, IKKE fikset i parseren (bevisst utenfor scope)**: Bergens forskrift-
     fixture har «kl. … 18.00.» linjebrutt slik at «18.00.» står alene på en linje —
     `HandbokTekstParser.PunktMønster` tolker den som et gyldig 2-segments punktnummer og åpner en
     `kap18/pkt18.00`-node hvis foreldre-`kap18` aldri finnes (og kapper samtidig kap1s ekte
     løpetekst midt i en setning). `HandbokImportTjeneste` krasjer IKKE på dette (`ParentNodeId=null`,
     diagnostikk i `AntallNoderMedUlostForelder`, regresjonstestet) — men selve tekst-tapet i kap1 er
     IKKE rettet. En fremtidig runde bør utvide `PunktMønster`/sidebrytningsfiltreringen for dette.
   - Ny `BergenKorpusSeed.cs` — Bergen kommune-`Virksomhet` (Kommunenummer "4601",
     Forvaltningsniva="kommune") + alkoholloven/alkoholforskriften (DELT/nasjonalt, `VirksomhetId=null`
     — et bevisst avvik fra en literal lesning av oppgaveteksten, se kommentaren i filen: nasjonale
     Lov/Forskrift skal ALDRI dupliseres per virksomhet) + begge håndbok-fixturene (Bergens forskrift
     importert via `HandbokImportTjeneste`, IKKE `LovdataKonverterer` — README nevner ingen Lovdata-URL
     for den, kun en direkte PDF-URL, og å konstruere en ELI ville vært gjetting) + alle 23
     nettside-fixturene (inkl. de to indekssidene selv som ekte `NettsideDokumentEntitet`-rader — et
     lite, dokumentert avvik fra `NettsideDokumentgrafTests.ByggKorpusAsync`, som kun parser dem for
     lenkelisten uten å lagre dem). Idempotent (global guard på "Bergen kommune"), wired inn i
     `Program.cs` etter de øvrige seedene.
   - Nye `GET /api/nettsider` og `GET /api/nettsider/{id}` — liste (Tittel/KanoniskUrl/Hentet/StiTyper)
     og detalj (RaaTekst, Stier, Lenker med flat oppløsningsstatus: `TilNettsideDokumentId`/
     `TilRettskildeId`-par, begge null når uløst/ekstern). Nye DTO-er i `Dtos.cs`, samme
     `FraEntitet`-mønster som resten av filen.
   - Frontend: `NettsiderListe.tsx`/`NettsideDetalj.tsx`, ny `src/nettside/RaaTekstMedLenker.tsx`
     (konverterer Markdown-lenker `[tekst](href)` i `RaaTekst` til ekte `<a>`, løst internt til
     `/nettsider/{id}` eller `/rettskilder/{id}`, ellers ekstern ny-fane-lenke), rutet inn i `App.tsx`
     med sidebar-lenke. Verifisert i ekte browser (SQLite-profil) — bundlingssidens `lovdatalenke`/
     `lenker_til`-lenker løser helt frem til ekte `alkoholloven`/`alkoholforskriften`/håndbok-rader.
   - **To reelle integrasjonshull funnet og fikset under browser-verifiseringen** (ikke antatt —
     bekreftet ved faktisk kjøring): (1) `RettskildeRepository.AlleRettskilderAsync` filtrerer på
     `Importrolle=="primaer"` — forrige rundes seedede eksempel brukte `"referanse"` for nøyaktig denne
     håndboken, noe som ville gjort Bergens innhold usynlig i rettskilder-LISTEN (kun nåbart direkte på
     GUID). Rettet ved å bruke `Importrolle="primaer"` + en minimal AKN-plassholder (egen kopi av
     `HandbokForfatterTjeneste.MinimalAknPlassholder`, som er `private`) i `HandbokImportTjeneste`.
     (2) `RettskildeDetalj.tsx` viste "ingen egen løpetekst" for `kapittel`-noder MED egen tekst (§
     HandbokNode-kommentaren: kapittel 6/7/9/10 har hele sin tekst direkte på kapittel-nivå) fordi
     `kanTagges` var hardkodet til kun `ledd`/`punkt` — utvidet til å vise (ikke-taggbar) tekst for
     enhver node som faktisk har `Tekst`.
   - Én til test-kollisjon funnet og rettet i EKSISTERENDE `TestkommuneInnholdSeedTests.cs`: to
     uscopede `Kildetype`-oppslag (uten `Tittel`-filter) forutsatte at Testkommunens Forskrift/
     Virksomhetsdokument var de ENESTE radene av sin type i den delte Postgres-testdatabasen — brøt i
     det øyeblikket Bergens egne rader av samme Kildetype dukket opp. Scopet på `Tittel.Contains
     ("Testkommune")`, samme mønster som en søsken-assert i samme fil allerede brukte.
   - 486/486 backend-tester grønt (75 Kildekonvertering + 168 Api + 243 Data, uavhengig verifisert),
     `npx tsc -b --noEmit` fra RegelIde.Web rent.
   - **Ikke gjort denne runden**: `NettsideSeksjon` (fortsatt kun dokument-granularitet),
     `NettsideHenterTjeneste`/live henting, `presentasjonsvariant`-kanten, `FunksjonellRolle`, GUID-
     oppløsning av `hjemlet_i` mot en PRESIS paragraf-eId (kun håndbok-NIVÅ-kobling via
     `HandbokRettskildeomfangEntitet` bygget — å konstruere en paragraf-eId fra `EksternParagraf` uten
     et verifisert format ville vært gjetting), retting av `PunktMønster`-linjebrytningsfunnet over.
9. **[LØST 2026-08-13] `AknXmlSkriver.cs` genererte ugyldig AKN 3.0 — rettet.** Bifangst fra
   avklaringsrunde 2s AKN-XSD-forskning (`docs/15` §14) fant to konkrete brudd: (a) `kildeId`-
   attributtet på `<article>`/`<paragraph>`/`<point>` var ikke gyldig i noe navnerom skjemaet
   tillater, (b) `FRBRWork`/`FRBRExpression` manglet alltid det obligatoriske `FRBRdate`-elementet.
   Under selve rettingen ble AknXmlSkriver.cs sitt faktiske output kjørt mot den ekte
   `akomantoso30.xsd` (vendoret i `RegelIde.Kildekonvertering.Tests/Testdata/Xsd/`) for tre reelle
   fixtures — det avdekket FIRE flere, tidligere ukjente brudd i samme fil: duplikat
   `TLCOrganization eId="stortinget"` for Lov (kolliderer med FrbrAuthorHref, som alltid er
   "stortinget" for Lov) pluss manglende påkrevd `href` på den generiske organisasjonen;
   `end="…"`-attributtet på opphevede `<article>`-elementer fantes ikke i noe attributeGroup
   skjemaet definerer (den gamle kommentaren kalte dette "ikke bekreftet" — nå bekreftet ugyldig);
   `<authorialNote>` (fotnoter) skrevet som block-nivå-sibling av `<article>`s barn i stedet for
   inline i tekstflyt (authorialNote er et subFlow-element); `<hcontainer>` manglet det påkrevde
   `name`-attributtet. Alle seks rettet i samme omgang (kildeId/opphevet flyttet til
   `regelIde:`-navneromsattributter — skjemaets faktiske utvidelsesmekanisme,
   `xsd:anyAttribute namespace="##other"` — IKKE `<proprietary>`, som viste seg IKKE å være gyldig
   som barn av hierarkiske elementer; manglende `FRBRdate` løst med Ikrafttredelse, ærlig merket
   `name="ikrafttredelse"` — ikke som vedtakelsesdato — med en eksplisitt "ukjent"-sentinel
   (`date="9999-01-01"`) som siste utvei, ikke en gjettet dato). Ny automatisert test
   `AknXmlSkjemaValideringTests.cs` validerer ekte alkoholloven-/alkoholforskriften-/
   forvaltningsloven-fixtures mot det ekte skjemaet (`System.Xml.Schema.XmlSchemaSet` +
   `ValidationType.Schema`), så dette ikke kan regressere stille. Full backend-testsuite grønt
   etter rettingen.
10. **Byggesteg 5 runde 3+ — de tre resterende AI-agentene** (§2.2, runde 1+2 ferdig). Vurder
    om byggesteg 3 bør være ferdig først, siden «Rettskilder og strukturering»-agenten forutsetter et
    presedensregister for å være noe mer enn en ren rettskilde-importer.
11. **Byggesteg 6/7** — informasjonsmodell/eksportmotor og saksbehandling/forklaringslogg-slice,
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
