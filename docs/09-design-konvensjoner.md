# Designkonvensjoner (Designsystemet i praksis)

> **[LÅST — 2026-08-22]** Detaljert spesifikasjon fra Johann, verifisert mot faktisk installerte
> pakkeversjoner (`package.json`: `@digdir/designsystemet-css`/`-react` `^1.18.0`,
> `-theme` `^1.11.0` — stemmer eksakt). Dette er nå den AUTORITATIVE kilden for eksakte
> farge-/mål-/typografiverdier — §§1-8 under er fortsatt gyldige for MØNSTRE (hvilken komponent, hvor
> tokens brukes), men eksakte hex/px-verdier her vinner ved motstrid.

## 0. Eksakt spesifikasjon (målt mot en kjørende instans, tema "Digdir")

**Typografi**: Inter (UI/løpetekst); IBM Plex Mono kun for kode/eId/organisasjonsnummer-aktig tekst.
Brødtekst/paragraf 18px/400/line-height 27px. Sideheading (H1, "lg") 36px/500/line-height 46.8px.
Underheadinger typisk 21–24px/500 (RETTET 2026-08-22, frontend-design-audit: stod tidligere "600" —
`--ds-heading-{xs,sm}-font-weight` er 500 i installert `digdir.css`, IKKE 600, for ALLE headingstørrelser
2xs→2xl; 600 finnes kun som `--ds-font-weight-semibold` for enkeltkomponenter som `Label`/`Table`-header,
ikke for `Heading`). Småtekst/meta (tags, hjelpetekst) 14px.

**Farger (lys tema)**: nøytral tekst `#1F2C3D` (standard)/`#545E6B` (dempet); nøytral
bakgrunn/overflate `#FFFFFF`, hover `#D8DADD`, tint `#E7E9EA`; nøytral kant `#B8BCC1` (subtil)/
`#717A84` (standard); aksent base `#0062BA`, tekst `#002C54`, tint-bakgrunn `#DDEAF6`, subtil kant
`#99C0E3`; info tekst `#042D4D`, tint `#DCEBF6`, sterk kant `#0860A3`; success tekst `#023409`;
warning tekst `#3C2807`, base `#EA9B1B`; danger tekst `#590D0D`, kant `#CE4D4D`.

**Mellomrom og form**: hjørner 2px (liten)/4px (standard — knapper/inputs/kort)/8px (stor)/
full-pill (tags). Standard inputs/knapper: høyde 48px, padding ~8–12px vertikalt/12–16px
horisontalt. Tabellrader: padding 8px/12px, bunnkant 1–2px aksent-subtil. Fast venstremeny: 260px
bred, 16px innvendig padding, hvit bakgrunn, høyre kantlinje 1px `#B8BCC1`. Toppfelt: padding
12px/24px, bunnkant 1px `#B8BCC1`, kun identitetsbrikke høyre-justert. Hovedinnhold: padding 24px,
maks-bredde 1100px (IKKE fullbredde-design).

**Komponent-vokabular** — kun disse (ekte, tilgjengelige komponenter): `Button`, `Field`,
`Fieldset`, `Heading`, `Label`, `Link`, `Paragraph`, `Select`, `Combobox`, `Table`, `Tag`,
`Textarea`, `Textfield`, `Tabs`, `ToggleGroup`, `Checkbox`, `Radio`, `Switch`, `Dropdown`, `Card`,
`Badge`, `Chip`, `Breadcrumbs`, `Dialog`, `Popover`, `Tooltip`, `Details`, `Divider`,
`ErrorSummary`, `ValidationMessage`, `Search`, `Pagination`, `Skeleton`, `Spinner`, `Avatar`,
`List`, `Alert`, `EXPERIMENTAL_Suggestion` (importert som `Suggestion`, se §10). De første 12
(Button→Checkbox) er alt som faktisk er brukt i dag, PLUSS `Combobox` og `Pagination` (tatt i bruk
2026-08-22, branch `paginering` — se §9) og `Suggestion` (tatt i bruk 2026-08-27 — se §10) — resten
finnes i biblioteket men er ALDRI brukt ennå; bruk dem der de løser et reelt problem (se kjente
UX-mangler under), ikke utenfor denne listen.

**Kjente UX-mangler å adressere** (ikke bare pynte på): ingen brødsmulesti — hver side finner på sin
egen «← Tilbake»-lenke eller har ingen; ingen delt visning av valideringsfeil per felt, kun én
global feilbanner nederst i formen; «Laster …» som ren tekst overalt, ingen skeleton/spinner;
sidemenyen ER nå gruppert (løst siden dette punktet først ble skrevet, se §3).

**Kildegrunnlag ved motstrid**: designsystemet.no / Storybook (storybook.designsystemet.no, ekte
rendret DOM, mest pålitelig for eksakt utseende) / github.com/digdir/designsystemet — MERK: velg
temaet "Digdir" spesifikt der, andre offentlige temaer har egen palett. Disse kildene vinner over
denne filen ved en reell versjonsforskjell — de målte verdiene over er fra 1.18.0/1.11.0, ikke
nødvendigvis en nyere versjon.

Kap. 6 i [`02-produktkrav.md`](02-produktkrav.md) sier at Designsystemet er bindende og at vi ikke skal
gjette tokennavn. Dette dokumentet er den konkrete oppskriften vi faktisk fulgte da vi bygde GUI-et for
byggesteg 1 (`src/RegelIde.Web`) — slik at neste skjerm bygges likt uten at vi må diskutere det på nytt.

## 1. Oppsett (må gjøres én gang, i appens rot)

`src/RegelIde.Web/index.html` og `src/main.tsx`:

```html
<!-- index.html -->
<link rel="preconnect" href="https://rsms.me/" />
<link rel="stylesheet" href="https://rsms.me/inter/inter.css" />
<body data-color-scheme="light" data-size="md">
```

```ts
// main.tsx
import '@digdir/designsystemet-css';
import '@digdir/designsystemet-theme/digdir.css';
```

- **`data-color-scheme` og `data-size` må stå på et forfedre-element** (vi bruker `<body>`) — uten dem
  faller alt tilbake til nettleserens standardstyling, selv om CSS-en laster helt fint. Dette er den
  vanligste feilen å gjøre først.
- **Inter-fonten følger ikke med** i `designsystemet-css`/`-theme` (bevisst valg fra Digdir — se pakkenes
  `package.json`, ingen `@font-face` noe sted). Uten en egen fontkilde faller `font-family: Inter` tilbake
  til systemfont, som ser feil ut selv når alt annet er riktig satt opp. Vi bruker rsms.me (Inters
  offisielle CDN) i dev; for prod bør dette selvhostes (`.woff2` + egen `@font-face`) for å unngå en
  ekstern avhengighet.
- Sett aldri `font-family` manuelt på `body` eller komponenter — la det arve `--ds-font-family` fra temaet.

## 2. Bakgrunn — to-flate-mønsteret

Sidepanel og hovedinnhold ligger begge på `--ds-color-neutral-background-default` (hvit). De skilles med
en **1px `--ds-color-neutral-border-subtle`-strek**, ikke med farge — se `.sidebar` i
`src/RegelIde.Web/src/index.css`. (Alternativ hvis man vil ha mer visuell struktur: gi hovedinnholdet
`--ds-color-neutral-background-tinted` og la kort/paneler stå hvite oppå — ikke gjort her ennå.)

## 3. Navigasjonsmønster (venstre sidemeny)

Nav-elementer er lenker, ikke knapper:

| Tilstand | Bakgrunn | Tekst | Venstre kant | Font-vekt |
|---|---|---|---|---|
| Hvile | transparent | `--ds-color-neutral-text-default` | 3px transparent | 400 |
| Hover | `--ds-color-neutral-surface-hover` | (uendret) | — | — |
| Aktiv (gjeldende side) | `--ds-color-accent-surface-tinted` | `--ds-color-accent-text-default` | 3px `--ds-color-accent-base-default` | 600 |

Poenget med aktiv-markeringen er at brukeren alltid skal se hvor de er — ikke bare on hover. Kanten er
transparent (ikke fraværende) i hviletilstand nettopp for å unngå at layouten hopper 3px når en side blir
aktiv. Radius `--ds-border-radius-sm`, padding `--ds-size-2`/`--ds-size-3`, gap mellom elementer
`--ds-size-1`. Fokus-ringen fra Designsystemet skal aldri overstyres. Se `.sidebar nav a` i `index.css`.

## 4. Tokens — faktiske navn (ikke gjett)

Verifiser alltid mot den installerte pakken (`node_modules/@digdir/designsystemet-theme/brand/digdir.css`)
før du bruker et nytt token — vi fant selv et eksempel på hvor galt det går: en tidlig versjon av
`index.css` brukte `--ds-spacing-1` … `--ds-spacing-6` gjennomgående, med harde px-fallbacks
(`var(--ds-spacing-4, 1rem)`). Det tokenet **finnes ikke** — riktig familie er `--ds-size-*`. Fallbacken
gjorde at ingenting så synlig "feil" ut, så feilen ble ikke oppdaget før noen faktisk sjekket. Riktige
familier vi bruker:

- `--ds-color-neutral-{background,surface,border,text}-{default,subtle,hover,tinted,...}`
- `--ds-color-accent-{base,surface,text,border}-{default,hover,tinted,...}`
- `--ds-color-{info,success,warning,danger}-{surface,text}-{default,...}` (statusmerker/feilmeldinger)
- `--ds-size-0` … `--ds-size-9` (spacing/padding/gap — **ikke** `--ds-spacing-*`)
- `--ds-font-size-1` … `--ds-font-size-10`
- `--ds-border-radius-{sm,md,lg,xl,full}`

## 5. Bruk komponenter fra `@digdir/designsystemet-react` — aldri rå HTML for disse

Digdir kan ikke style rå `<table>`, `<input>` eller `<a>` — kun sine egne komponenters klasser
(`ds-input`, `ds-link`, osv.). Der byggesteg 1 opprinnelig brukte rå HTML, erstattet vi med:

| Rå HTML | Digdir-komponent | Fil (eksempel) |
|---|---|---|
| `<table>` | `Table` / `Table.Head` / `Table.Body` / `Table.Row` / `Table.Cell` / `Table.HeaderCell` | `pages/RettskilderListe.tsx`, `pages/RettskildeDetalj.tsx` |
| `<input type="checkbox">` | `Checkbox` (krever `label`-prop) | `pages/RettskilderListe.tsx`, `pages/Importer.tsx` |
| `<input type="file">` | `Textfield` med `type="file"` (samme komponent som andre tekstfelt) | `pages/Importer.tsx` |
| `<select>` | `Field` + `Label` + `Select` / `Select.Option` | `App.tsx` (`BrukerVelger`) |
| react-router `<Link>` alene | Digdirs `Link asChild` rundt react-router sin `Link` — beholder rutingen, gir riktig lenkefarge/hover/fokus-ring | `pages/RettskilderListe.tsx`, `pages/RettskildeDetalj.tsx` |

Mangler Designsystemet en komponent for noe (jf. produktkrav kap. 6), flagg det — ikke design en egen
erstatning.

## 6. Detaljside-typografi (2026-08-20/22, "Startside Alternativ 1c" + virksomhetskatalog-runden)

Presisering utover §4s tokenfamilier — hva som FAKTISK brukes på nyere detaljsider
(`TjenesteDetalj.tsx`, `VirksomhetDetalj.tsx`), ikke bare hvilke tokens som finnes:

- **Seksjonsoverskrift** i en detaljside: `<Heading level={2} data-size="sm">`.
- **Underoverskrift INNI en seksjon** (en rik seksjon med flere del-temaer, f.eks. Tjenestes "Innhold"):
  `<Heading level={3} data-size="xs">` — ikke bare én flat liste av `level=2`-seksjoner. Bruk dette når
  en seksjon har mer enn ett tydelig avgrenset del-tema.
- **Støttetekst/metatekst** (forklarende ingress under en seksjonsoverskrift, hjelpetekst): `fontSize:
  'var(--ds-font-size-1)'` KOMBINERT med `color: 'var(--ds-color-neutral-text-subtle)'` — begge, ikke
  bare fargen alene. Feil begått én gang (virksomhetskatalog-rundens første utkast av
  `VirksomhetDetalj.tsx`): kun subtil FARGE uten den mindre STØRRELSEN, som ser feil ut side ved side
  med `TjenesteDetalj.tsx`.
- **Tabell i en Card**: `<Card style={{ padding: 0, overflow: 'hidden' }}><Table>…` når tabellen ER hele
  kortets innhold (radene fyller kortet); `<Card style={{ padding: '1rem' }}><Table>…` når det er en
  enklere nøkkel/verdi-tabell (Grunndata-mønsteret).
- **Overskrift + status-tagger på linje 2** (rett under H1): `<Paragraph style={{ display: 'flex', gap:
  '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>` med `<Tag data-size="sm">`-elementer inni —
  ikke egne `<div>`, gjenbruk `Paragraph` som wrapper selv når det ikke er løpetekst.

**Sjekk denne §6 (og resten av dokumentet) FØR du bygger en ny detaljside** — ikke bare den nyeste
siden i git-loggen, siden nye presiseringer skal landes her, ikke bare i koden.

## 7. Gjentatte avvik funnet ved full frontend-revisjon (2026-08-22, `frontend-design-audit`)

Denne runden gikk gjennom ALLE `.tsx`-filer i `src/RegelIde.Web/src` mot §0-§6. Utover det som allerede
var rettet (se git-historikk på branchen), dukket disse mønstrene opp mer enn ett sted — nevnt her så de
ikke gjentas:

- **Metatekst med `opacity: 0.7` i stedet for subtil FARGE** (samme feil som §6 allerede nevner for
  `VirksomhetDetalj.tsx`s første utkast) dukket opp fire steder til, i `BegrepsforslagKo.tsx` og
  `TjenesteforslagKo.tsx` (token-tellingen etter et KI-kall, og stub-KI-merknaden). Rettet til
  `fontSize: 'var(--ds-font-size-1)'` + `color: 'var(--ds-color-neutral-text-subtle)'`. Skann alltid etter
  `opacity:` når du skriver metatekst — det er ALDRI riktig mønster for det.
- **`<input type="file">` rått i stedet for `Textfield type="file"`**: fantes i `TjenesteforslagKo.tsx`
  (ved siden av `Importer.tsx`, som allerede gjorde det riktig — se §5-tabellen). Samme fiks: `<Textfield
  type="file" label="…" accept="…" onChange={…} ref={…} />` — `ref` er typet
  `HTMLInputElement | HTMLTextAreaElement` og fungerer med eksisterende `useRef<HTMLInputElement>`-kode
  som nuller ut valgt fil etter opplasting.
- **`<a href>` rått i stedet for `Link`**: fantes i `KodelisteDetalj.tsx` (ekstern kilde-lenke). Digdir kan
  ikke style en rå `<a>` (jf. §5) — bruk `<Link href={url} target="_blank" rel="noopener noreferrer">`
  som resten av kodebasen allerede gjør for eksterne lenker (`RettskildeDetalj.tsx`,
  `RettskilderListe.tsx`, `TjenesteDetalj.tsx`, `TjenesteforslagKo.tsx`, `RaaTekstMedLenker.tsx`).
- **Rå `<button>` uten noen styling** (så ut som nettleserens standard-3D-knapp): én forekomst i
  `RettskildeDetalj.tsx` (vis/skjul AKN-XML). Rettet til `<Button data-size="sm" variant="tertiary">`.
  MERK: dette gjelder KUN knapper uten egen styling — `.tabell-sorter-knapp`-mønsteret (rå `<button
  className="tabell-sorter-knapp">` inni en `Table.HeaderCell` for sorterbare kolonner, brukt konsekvent
  i alle liste-sider) og en bar `×`-fjern-knapp inni en `Tag` (`KommentarRedigering.tsx`) er BEVISST rå
  HTML — `Button` har fast padding/høyde/kant som ikke passer disse to stedene, og begge er allerede
  eksplisitt unstylet/gjennomsiktig via CSS/inline style. Ikke "fiks" disse to til `Button`.
- **Ad-hoc CSS-klasser `.badge-delt`/`.badge-virksomhet`** i `index.css` var udokumenterte, ubrukte (ingen
  `.tsx` refererte dem) og dupliserte nøyaktig hva `Tag`/`Badge` løser — fjernet, samme resonnement som
  `.feilmelding`/`.infomelding`-fjerningen. Sjekk `index.css` for lignende død kode før du legger til en ny
  ad-hoc-klasse — søk først etter om `Tag`/`Badge`/`Chip`/`Alert` allerede dekker behovet.
- **§0-rettelse**: "Underheadinger typisk 21–24px/600" var feil — verifisert mot installert
  `digdir.css`/`heading.css`, `--ds-heading-{xs,sm,...}-font-weight` er **500** for ALLE headingstørrelser
  (2xs→2xl), ikke 600. 600 finnes kun som `--ds-font-weight-semibold`, brukt av enkeltkomponenter som
  `Label`/`Table`-header — ikke av `Heading`. Øvrige §0-verdier (alle hex-farger, border-radius 2/4/8px,
  input/knapp-høyde 48px, tabellrad-padding 8px/12px, font-size-skalaen, line-height-multiplikatorene)
  ble kryssjekket linje for linje mot samme fil og stemmer eksakt.

## 8. Ikke gjort ennå

- Selvhosting av Inter (kjører fortsatt mot rsms.me i dev).
- Responsiv kollaps av sidemenyen under 880px (krav i produktkrav kap. 7 — ikke implementert).
- `--ds-color-neutral-background-tinted`-varianten av hovedinnhold (kun den enkleste to-flate-varianten
  er valgt så langt).

## 9. Paginering av lange lister + søkbar virksomhetsvelger (2026-08-22, branch `paginering`)

Bakgrunn: fem liste-sider hentet ALT → filtrerte/sorterte klient-side → rendret HELE resultatet i én
`Table` — reelt observert ytelsesproblem (ikke bare teoretisk), bekreftet ved at
`VirksomhetKandidaterListe.tsx`s `<Select>` med ~451 virksomheter som `<option>` fikk et
render-timeout under live-verifisering.

**To ulike problemer, to ulike løsninger — ikke bland dem:**

- **Mange TABELLRADER** → paginering (`src/RegelIde.Web/src/tabell/usePaginering.ts` +
  `Pagineringskontroll.tsx`).
- **Mange `<option>` i én dropdown** (virksomhetsvelgere) → `Combobox` (søkbar), IKKE paginering —
  en dropdown sin options-liste pagineres ikke, den gjøres søkbar. Se
  `src/RegelIde.Web/src/virksomhet/VirksomhetVelger.tsx`.

### 9.1 `usePaginering<T>(rader: T[])` — delt hook

```ts
const viste = useMemo(() => /* filtrer + sorter */, [data, filterTekst, sortKolonne, sortStigende]);
const paginering = usePaginering(viste ?? []);
// paginering.visteRader  ← bruk DENNE i Table.Body.map(...), ikke `viste` direkte
// <Pagineringskontroll {...paginering} />  ← rendres rett under tabellen
```

Returnerer `{ side, settSide, sidestorrelse, settSidestorrelse, totaltAntallSider,
totaltAntallRader, visteRader }`. Sentrale designvalg:

- Paginerer et allerede FILTRERT OG SORTERT array — filter/sortering virker fortsatt på hele
  datasettet, paginering er kun en render-optimalisering av visningen, ikke en serverside-
  begrensning.
- **Nullstiller automatisk til side 1** når `rader` får en ny referanse (nytt filter, ny sortering,
  nye data hentet) via en `useEffect([rader])` — bevisst, se kommentar i kildekoden. Siden kallerens
  `useMemo` for `viste` uansett kun lager en ny array-referanse når filter/sortering/data faktisk
  endrer seg, trigger dette IKKE ved urelaterte re-renders (f.eks. at selve siden endres).
- Faste sidestørrelser `20 | 50 | 100 | 'alle'` (`SIDESTORRELSER`-konstanten) — bevisst ikke fritekst
  (Johanns eksplisitte ønske).
- `settSidestorrelse` nullstiller også til side 1 (en ny sidestørrelse endrer sidetallingen).

`Pagineringskontroll` (samme mappe) rendrer sidestørrelse-`Select` + "Viser X–Y av Z"-tekst +
Designsystemets `Pagination` (skjules helt ved 0 rader; selve `Pagination`-elementet skjules når
sidestørrelse er «Alle» eller alt får plass på én side). Bygget på `usePagination`-hjelpehooken fra
`@digdir/designsystemet-react` selv (`Pagination.List`/`Pagination.Item`/`Pagination.Button`,
`data-current`/`data-total` som STRENGER — verifisert mot installert `1.18.0` sine `.d.ts`-filer,
ikke gjettet).

**Fem sider har fått paginering**: `RettskilderListe.tsx` (BEGGE tabellene — hovedlisten og
"ikke-importert"-underlisten, samme mønster to ganger i én fil), `TjenesterListe.tsx`,
`HandlingerListe.tsx`, `VirksomheterListe.tsx`, `VirksomhetKandidaterListe.tsx`.

**Massehandling + paginering** (`VirksomhetKandidaterListe.tsx` spesifikt): "Velg alle viste"-
checkboxen i tabellhodet betyr etter denne endringen alle rader på GJELDENDE SIDE
(`paginering.visteRader`), ikke hele det filtrerte treffsettet — brukerens eksisterende utvalg på
tvers av sider bevares når man blar (kun checkbox-tilstanden i toppen reflekterer siden man står på).

### 9.2 `VirksomhetVelger` — søkbar erstatning for `<Select>` med alle virksomheter

`src/RegelIde.Web/src/virksomhet/VirksomhetVelger.tsx`, bygget på `Combobox` (§0-vokabularet).
Props: `virksomheter`, `value` (valgt id, `''` for tomt valg), `onChange`, `label`, `tomValgTekst`
(teksten på det tomme valget, f.eks. «Alle virksomheter» / «Velg virksomhet …» /
«(nasjonal standardverdi)» — varierer fra side til side, se bruken), `hideLabel?`, `style?`.

To ting man MÅ gjøre riktig med `Combobox` her, begge verifisert mot installert `1.18.0` sine
`.d.ts`-filer (ikke gjettet):

- **Egen `filter`-prop er obligatorisk**: standardfilteret i `Combobox` matcher
  `option.value.toLowerCase().startsWith(inputValue)` — her er `value` virksomhetens GUID, ikke
  navnet. Uten en egen `filter={(inputValue, option) => option.label.toLowerCase().includes(...)}`
  må brukeren skrive inn en GUID for å finne noe.
- `Combobox` har `label` innebygd (encapsulerer selv i et `<label>`-element) — IKKE wrap den i
  `Field`+`Label` slik `Select` krever (§5-tabellen); det mønsteret gjelder kun `Select`.

Erstattet tre forekomster av «render alle virksomheter som `<Select.Option>`»:
`VirksomhetKandidaterListe.tsx` (både "Virksomhet å sveipe for" og "Virksomhet"-filteret) og
`TjenesteVeiledning.tsx`. Andre steder i kodebasen som viser/velger ÉN virksomhet ut fra en
allerede liten, kontekstavgrenset liste (f.eks. brukerens egen virksomhet) er IKKE endret — dette
gjelder kun steder som rendret HELE virksomhetslisten (~451 rader) som options.

## 10. `RettskildeFlervalg` — flervalg av rettskilder uten å mounte hele lista (2026-08-27)

Johann pekte på at «Identifiser tjenester» og «Identifiser begrep» rendret én `Checkbox` per
rettskilde for å velge hvilke som skulle inn i et KI-forslagskall — med 5893 reelle rettskilder i
dag (`curl localhost:5187/api/rettskilder`) en uholdbart lang liste, og pekte på Designsystemets
`Suggestion`-komponent (dokumentert på designsystemet.no som «Flervalg»-varianten av den) som
erstatning.

**Eksport-navnet er `EXPERIMENTAL_Suggestion`**, ikke `Suggestion` — verifisert mot installert
`1.18.0` sine `.d.ts`/kildefiler i `node_modules/@digdir/designsystemet-react` (komponenten er
fortsatt experimental i denne versjonen). Importeres omdøpt: `import { EXPERIMENTAL_Suggestion as
Suggestion, ... }`.

**Samme DOM-monteringsfelle som `Combobox` (§9) — unngått med egen filtrering, ikke library'ets
`filter`-prop:** `Suggestion` mounter ALLE `<Suggestion.Option>`-barn i DOM-en uansett — dens
innebygde filtrering virker ved å SKJULE allerede mount'ede options (`option.disabled`), ikke ved å
utelate dem fra treet. §9 dokumenterer et reelt render-timeout med bare ~451 `<option>` (native
`Select`) — med 5893 rettskilder (13× så mange) er samme felle nesten garantert. Løsningen i
`src/RegelIde.Web/src/rettskilde/RettskildeFlervalg.tsx`: `filter={false}` på `Suggestion`, og en
egen `sok`-React-state (fra `Suggestion.Input`s `onInput`, IKKE dens `value`/`onChange` — komponenten
advarer selv i konsollen mot begge, siden `Suggestion` skal styre input-verdien via `selected`)
filtrerer `rettskilder`-arrayet FØR rendering, og kun de første 50 treffene mountes som
`<Suggestion.Option>`. Ved tomt søk mountes null options (viser en «Skriv for å søke …»-tekst via
`Suggestion.Empty`) — ingen 5893-elements DOM-tre eksisterer noe sted i denne komponentens levetid.

**Props**: `rettskilder` (`RettskildeSammendrag[]`), `valgte` (`Set<string>`), `onChange`
(`(valgte: Set<string>) => void`, kan sendes direkte som en `useState`-setter), `label?`
(default `"Rettskilder"`). Brukt i `TjenesteforslagKo.tsx` og `BegrepsforslagKo.tsx` — begge hadde
identisk `Checkbox`-liste + `valgteRettskilder: Set<string>`-tilstand, kun `onChange`-kallet endret
(direkte `setValgteRettskilder` i stedet for en `vekslRettskilde(id, checked)`-hjelpefunksjon per
rad, som nå er fjernet fra begge filene).

Live-verifisert (ikke bare kompilert): søk på "alkohol" mounter nøyaktig de matchende
`<u-option>`-elementene (ikke alle 5893), valg gir en fjernbar `Chip` og aktiverer
"Kjør KI-forslag", og å fjerne chippen nullstiller utvalget og deaktiverer knappen igjen.

### 10.1 `useRettskildeSok` + `RettskildeVelger` — samme teknikk, ENKELTvalg (2026-08-27)

Johann ba om samme behandling "andre steder med lange lister". Et sveip over hele frontend
(`grep Checkbox\|Select\.Option`) fant to reelle familier av samme problem utover §10s to KI-
forslag-sider — begge over ALLE 5893 rettskilder, ikke et filtrert delsett:

- **`HandbokOpprett.tsx`** — identisk `Checkbox`-per-rettskilde-liste ("Rettskilder håndboken
  omhandler") → byttet direkte til `RettskildeFlervalg` (ingen ny kode, samme komponent).
- **Enkeltvalg av ÉN rettskilde** i tre skjemaer — `KommentarRedigering.tsx` (koble lovreferanse
  til en håndbok-kommentar), `RettskildeDetalj.tsx` (både "Legg til rettskilde"/håndbok-omfang OG
  den generelle "Koble referanse"-sekjonen) — alle tre var et rått `<Select>` med
  `alleRettskilder.map(...)` som `<Select.Option>`. `RettskildeFlervalg` passer ikke (den er
  flervalg/`Set`), så ny søsterkomponent `RettskildeVelger` (samme mappe): `multiple={false}`,
  streng `value`/`onChange(id: string)` i stedet for `Set`.

**Delt filtreringslogikk** flyttet ut i `useRettskildeSok(rettskilder)` (samme mappe) — brukes av
BEGGE komponentene, som nå kun står for selve `Suggestion`-oppsettet (multiple vs. enkelt).
Returnerer `{ sok, setSok, treff, alleTreffAntall }`; samme 50-treffs grense og samme
"skriv-for-å-søke"-tomtekst som §10 (ingen ny avveining, bare unngått duplisering).

`RettskildeVelger` sine props: `rettskilder`, `value` (valgt id, `''` for tomt), `onChange`
(`(id: string) => void`), `label?` (default `"Rettskilde"`). Kallerne kan sende et allerede
FILTRERT `rettskilder`-array (f.eks. `RettskildeDetalj.tsx`s "Legg til rettskilde" ekskluderer
rettskilder som allerede er i håndbokens omfang, og seg selv) — komponenten selv vet ikke noe om
denne filtreringen, den får bare den ferdige kandidatlisten.

Live-verifisert end-to-end, ikke bare kompilert: opprettet en ny håndbok via `RettskildeFlervalg`
(chip-valg → `Opprett` → naviger til den nye håndbokens `RettskildeDetalj`-side), deretter i
"Legg til rettskilde" søkte på "alkohol", bekreftet at den allerede lenkede rettskilden var
UTELATT fra treffene (filtreringen i kalleren virker), valgte en ny, klikket "Legg til", og

## 11. Tjenestedetalj-redesignrunden (2026-08-27) — `Details`, full-bredde-unntak, per-bruker visningsinnstillinger

Full omskriving av `TjenesteDetalj.tsx` (fra ett 1253-linjers endimensjonalt skjema til en
fanebasert side, se docs/22-tjeneste-side-redesign-brief.md for hva den løste) — tre nye,
gjenbrukbare mønstre verdt å dokumentere for senere sider:

**`Details`/`Details.Summary`/`Details.Content`** — FØRSTE bruk i appen (§0-vokabularet listet den
som "aldri brukt ennå" til nå). Brukt som accordion-primitiv i Innhold-fanen i stedet for
håndrullede divs. Kontrollert `open`+`onToggle` (verifisert mot installert `1.18.0` sine
`.d.ts`-filer — `onToggle` er en native `toggle`-hendelse, `(e.target as
HTMLDetailsElement).open` gir den NYE tilstanden, siden hendelsen fyres ETTER at den allerede har
endret seg). Egne opp/ned/fjern-knapper i `Summary` MÅ `e.stopPropagation()` — hele `Summary`
toggler ellers accordion-en når man klikker en knapp inni den.

**`.tjenestedetalj-fullbredde`-klassen** (`index.css`) — unntak fra den globale `.innhold > *
{ max-width: 1100px }`-regelen (§0). `TjenesteDetalj.tsx` er den FØRSTE og til nå ENESTE siden som
trenger full bredde, for å få hovedinnhold + et høyre kontekstpanel side ved side (samme
"navigator/editor/referanser"-IDE-behov som README sine to metaforer beskriver). Legg til flere
spesifikke unntak etter SAMME mønster (én klasse per side som faktisk trenger det) hvis flere
sider får et tilsvarende to-kolonne-behov — gjør ALDRI 1100px-regelen generelt løsere for å løse
ett enkelt sides behov.

**Per-bruker visningsinnstillinger** (`BrukerVisningsinnstillingEntitet`/
`GET+PUT /api/brukere/meg/tjeneste-visning`, frontend-hooken
`src/RegelIde.Web/src/tjeneste/useVisningsinnstillinger.ts`) — første gang appen lagrer en
UI-STRUKTUR-preferanse (fanerekkefølge/-synlighet, accordion-rekkefølge/åpen-tilstand) server-side
PER BRUKER i stedet for enten localStorage (tapes på annen enhet/nettleser) eller per tjeneste
(ville tvunget alle som ser samme tjeneste til samme layout). Bevisst grense: INNHOLD som er
tjeneste-spesifikt (egendefinerte innholdselementers egen rekkefølge/åpen-tilstand) styres LOKALT
i selve fane-komponenten, ALDRI i denne delte per-bruker-tilstanden — en custom-nøkkel fra
tjeneste A gir ikke mening på tjeneste B. Gjenbrukbart mønster hvis en annen side senere trenger
samme "husk brukerens fanerekkefølge"-behov — samme `Bruker`-FK, samme
`GjeldendeBrukerTjeneste.FinnAsync`-oppslag som resten av skrivende endepunkter allerede bruker.
bekreftet at den dukket opp i "Denne håndboken omhandler"-tabellen og at velgeren nullstilte seg.

## 12. `@xyflow/react` (React Flow) — appens første nye frontend-npm-avhengighet (2026-08-28)

Bakgrunn: `docs/13-backlog.md` hadde et åpent, aldri besvart spørsmål — "bør vi velge et ekte
grafbibliotek (React Flow/dagre) i stedet for `VilkarstreGraf.tsx` sin egenhendige SVG-løsning?" —
eksplisitt utsatt til «et fremtidig dra-og-slipp-krav vekker temaet». `Tjenestereise.tsx`
(tjenestereise-graf-runden) er nettopp det kravet: dra-bare noder, konfigurerbar dybde/filter/
felt-visning — noe utover det `VilkarstreGraf.tsx`s rene, tre-formede lag-layout kunne dekke uten
betydelig egenbygd maskineri (drag-håndtering, zoom/pan, minimap).

**Verifisert FØR bruk, ikke antatt**: `@xyflow/react` sine `peerDependencies` godtar `react`/
`react-dom` `>=17` — testet direkte mot appens faktiske `react@19.2.7`/`vite@8.1.5`/`typescript
~6.0.2`-oppsett (`npm install`, `tsc -b --noEmit`, `npm run build`, samt en reell render i
nettleseren) FØR resten av grafsiden ble bygget — ingen npm-avhengighet legges inn "og vi får se".

**Konvensjon for videre bruk**: importer kun det som faktisk trengs (`ReactFlow`, `Background`,
`Controls`, `MiniMap`, typene `Node`/`Edge`) — ikke layoutbiblioteker (dagre/elkjs) med mindre et
konkret, udekket layout-behov faktisk dukker opp (samme "ingen ny avhengighet uten et konkret behov"-
holdning som fikk denne saken utsatt i utgangspunktet). CSS-importen (`@xyflow/react/dist/style.css`)
er nødvendig — biblioteket styler IKKE via `--ds-*`-tokens, men egne noder/kanter i `Tjenestereise.tsx`
bruker `--ds-*`-tokens for farge/border/radius der det er mulig (se `beregnPosisjoner`/`nodeLabel`).

## 13. `Switch` — første bruk (2026-08-30, rettskilde-irrelevant-markering)

`Switch` sto i §0-vokabularet som "finnes i biblioteket men aldri brukt ennå" — første reelle bruk er
`RettskildeDetalj.tsx`s «Marker som irrelevant for regel-ide»-toggel. Valgt over `Checkbox` fordi dette
er en RENDYRKET av/på-INNSTILLING for én rettskilde (ikke et flervalgsfilter i en liste, som resten av
appens `Checkbox`-bruk faktisk er — f.eks. `kunMine`/`visIkkeImportert`/`visIrrelevante` i
`RettskilderListe.tsx`). Samme props-mønster som `Checkbox` (`label`, `checked`, `onChange`), ingen
`Field`/`Label`-wrapping nødvendig (encapsulerer selv, som `Combobox` gjør — se §9.2).

Listesidens tilsvarende «vis også irrelevante»-toggel (`RettskilderListe.tsx`) bruker fortsatt
`Checkbox`, BEVISST — det er et vanlig synlighetsfilter i en tabell, samme familie som de to andre
checkboxene rett ved siden av, ikke en enkeltrettskildes av/på-innstilling.

## 14. Saksbehandlerverktøy, ikke informasjonsside (2026-09-02, `docs/30-saksbehandlertilpasning-bestilling.md`)

> **SJEKK DENNE §14 FØR DU BYGGER ELLER ENDRER NOEN ENTITETSSIDE/LISTESIDE** — samme regel som §6, nå
> utvidet til hele appens mønster, ikke bare typografidetaljer på én sidetype.

**Prinsippet**: Regel-IDE ER et saksbehandler-/ekspertverktøy (5893 rettskilder, ~3990
navnekandidater, dype vilkårstrær) — IKKE en informasjonsnettside, selv om Designsystemet (kap. 6,
produktkrav) opprinnelig ble tatt i bruk med informasjonsside-mønstre (én ting av gangen, generøs
luft, lange skjema nedover siden). Full diagnose, spesifikasjon og migreringsplan ligger i
`docs/30-saksbehandlertilpasning-bestilling.md` — les DEN i sin helhet før du bygger noe av dette;
denne §14 er kun den destillerte, bindende oppsummeringen.

**Vi endrer MØNSTRE og TETTHET, ALDRI farger/fontfamilie/`--ds-*`-tokens** — §0-§13 over gjelder
fortsatt uendret for selve verdiene. Det som endres er hvordan komponentene SETTES SAMMEN:

| Dimensjon | Informasjonsside (unngå) | Saksbehandlerverktøy (mål) |
|---|---|---|
| Tetthet | Generøs luft, 18px brødtekst | `data-size="sm"` konsekvent i arbeidsflater (skjema/tabell/liste), IKKE ad-hoc `fontSize`-unntak per element |
| Navigasjon | Én side av gangen, bar "← Tilbake" | Brødsmulesti + kontekstpanel + globalt søk, alltid vite hvor man er |
| Relasjoner | Spredt i 3-6 separate `<section>`-er nedover siden | Samlet i ÉTT `KontekstPanel`-mønster (delt, entitetsuavhengig komponent) |
| Redigering | Skjema → lagre → se resultat et annet sted | Inline der mulig, tydelig ulagret-indikator (`LagreStatusIndikator`) |
| Status/flyt | Bar `<Select>`-dropdown | Egen `StatusStepper`-komponent (Tag/Badge-rekke, gjeldende steg fremhevet), `Dialog`-bekreftelse på kritiske overganger |
| Tomme tilstander | `<Card>` kun rendret når data finnes → usynlig/flatt når tomt | `<Card>` ALLTID rendret, tom-tilstand er en `<Paragraph>` INNI kortet |
| Lister | Bla side for side | Søk/filter/gruppering/massehandling som standard (samme investering som `NavnekandidaterListe.tsx` allerede har) |

**Delt "Entitetsside"-mønster** (bygg ÉN gang, gjenbruk — ikke kopier per side): brødsmulesti +
tittel/statuslinje + faner-eller-accordion for >4 seksjoner + generalisert `KontekstPanel` (tar en
liste `{ heading, items }[]`, ikke hardkodet per entitetstype). `TjenesteDetalj.tsx`/
`KontekstPanel.tsx` (§11) er allerede riktig retning og selve malen — resten av entitetssidene
(`VirksomhetDetalj`, `BegrepDetalj`, `HandlingDetalj`, `RettskildeDetalj`, `VilkarstreDetalj`) skal
migreres TIL dette mønsteret, ikke ha sitt eget.

**Kompakt tabelltetthet** (`data-density="compact"`, kun padding-CSS, ingen nye farger) er standard
for store datasett — `RettskilderListe`, `NavnekandidaterListe`, `VirksomhetKandidaterListe`.

**Bygger du en HELT NY entitets- eller listeside**: følg dette mønsteret fra dag én — ikke bygg det
gamle endimensjonale skjema-mønsteret og planlegg å migrere senere. Bygger du VIDERE på en side som
ennå ikke er migrert (`docs/30` §4 har rekkefølgen): enten migrer siden til dette mønsteret som del
av arbeidet ditt, eller flagg eksplisitt til Johann at du bevisst IKKE gjorde det og hvorfor — la det
aldri stille forbli det gamle mønsteret uten et valg.

---

## 15. Veiviser-mønsteret (2026-09-07, `NavnekandidatVeiviser.tsx`) + `NavneformgrunnTag`/`NavneformgrunnVelger`

Appens FØRSTE fler-stegs veiviser for å behandle ÉN rad ende til ende (til forskjell fra
`ImportWizard`, som er en engangs-importflyt). Bygget etter §14 fra dag én. Bindende for neste
veiviser:

- **Egen, dypt lenkbar rute** (`/navnekandidater/:id/behandle`) med et eget GET-endepunkt for ÉN rad
  — ikke en modal over lista, og ikke "hent hele køen og finn iden klientside".
- **Steg-indikator som `Tag`-rekke** øverst: gjeldende steg `data-color="accent"`, ferdige steg
  `neutral` fylt, kommende steg `neutral` + `variant="outline"`. Samme visuelle idiom som
  `StatusStepper` (§14-tabellen), men en egen komponent — `StatusStepper` er bundet til den 6-trinns
  ENTITETS-statusmodellen og skal ikke gjenbrukes for veiviser-steg.
- **Progressiv avdekking, ikke utbytting**: hvert steg er et `Card` som blir stående synlig når man
  går videre (`{steg >= n && …}`), med feltene satt `readOnly`/`disabled` når steget ikke er aktivt.
  Saksbehandleren skal kunne lese hele beslutningskjeden sin på én skjerm, ikke huske steg 1.
- **Avsluttende bekreftelse er en «dette skjedde»-oppsummering**, ikke bare en suksess-`Alert`:
  hva som ble opprettet/endret, direktelenker til resultatet, og en `warning`-`Alert` når en
  dokumentert degradering slo inn. Påstander i denne oppsummeringen må FØLGE utfallet, ikke være
  hardkodet — f.eks. hvilket tagg-lag taggen faktisk havnet i («Virksomhet» for virksomhet-veien,
  «Begrep» for gruppe-veien). Å sende saksbehandleren til feil lag er en påstand som ikke stemmer.
- **«Ikke kjent»/«ingen treff» skal ALDRI vises mens data fortsatt lastes** — da er svaret ikke
  ukjent, bare ikke kommet ennå. Bruk `Spinner` på `null`-tilstanden og reserver den negative
  påstanden for faktisk tomt svar (samme «ikke funnet ≠ oppfunnet»-holdning som datalaget).

**`NavneformgrunnTag` / `NavneformgrunnVelger`** (`src/virksomhet/Navneformgrunn.tsx`) er ÉN delt
kilde for både visning og valg av `BegrepDto.navneformgrunn`, brukt av `VirksomhetDetalj`,
`VirksomheterListe`, `BegrepDetalj` og veiviseren — legg aldri en fjerde, lokal variant ved siden av.
Fargevalget er et KRAV, ikke pynt: `success` kun for `gjeldende`, `warning` for `utgatt`, `danger`
for `feilskriving`, `info` for `kortform` — en utgått eller feilskrevet navneform skal aldri kunne
forveksles med det offisielle navnet. En uspesifisert (NULL) grunn gir som standard INGEN merkelapp
(tom celle) i tabeller, siden de aller fleste eksisterende radene mangler grunn og en
«Uspesifisert»-merkelapp på hver av dem ville vært ren støy; `visUspesifisert` slås på der fraværet
er selve poenget. En verdi komponenten ikke kjenner vises RÅ i stedet for å skjules.

---

## 16. Navigerbare tagger i løpetekst + medlemslister på et gruppebegrep (2026-09-08, issue #164)

**En koblet tagg i løpeteksten SKAL være en lenke.** `TagTekst` rendret tidligere hver markering som
en bar `<mark>` med et `title`-tooltip som viste taggens rå GUID; den eneste veien fra en markering
til entiteten den peker på gikk via tagg-listen UNDER teksten. Det er markeringen i teksten man peker
på når man leser en lovtekst, ikke en liste lenger ned. Er `resolveRef` i stand til å gi en lenke for
taggens `ref`, er markeringen derfor selv navigerbar (`TaggetSegment`).

Tre bindende detaljer, alle tre av samme grunn — markeringen skal bli klikkbar UTEN å bli noe annet:

- **`draggable={false}` på lenken.** En `<a>` inne i tekstflaten gjør at klikk-og-dra starter en
  lenke-dragging i stedet for en tekst-SELEKSJON, og seleksjon er nøyaktig hvordan en ny tagg
  opprettes (`selectionOffsets`). Uten dette ville navigerbare tagger ha ødelagt taggingen over og
  rundt allerede taggede ord. Dette er ikke pynt, og skal ikke fjernes.
- **`color: inherit` på lenken, uendret markeringsfarge.** Taggfargen bærer allerede en egen
  betydning (hvilket tagg-lag), og skal ikke overstyres av lenkefargen. At markeringen er klikkbar
  formidles av `cursor: pointer` og `title`, ikke ved å bryte fargekoden.
- **Ingen ny `kind` for grupper.** Et gruppebegrep i løpetekst er en `begrep`-tagg hvis `refId` peker
  på et begrep med kategori `'gruppe'`. Drill-through skiller seg derfor på REFERANSEMÅLET, ikke på
  taggens `kind` — legg aldri til en `gruppe`-kind for å slippe det oppslaget.

**Tre lister på et gruppebegrep, ikke én** (`GruppeMedlemmer`, vist av `BegrepDetalj` for kategori
`'gruppe'`): «Medlemsgrupper» (gruppe-av-gruppe, ett nivå ned), «Virksomheter i gruppen»
(myndighetstildelingene) og «Medlem av» (retningen oppover). De er tre ULIKE påstander, og en
sammenslått «medlemmer»-liste ville skjult nettopp det som er poenget — at en kommune kan få plikter
INDIREKTE, gjennom en gruppe som selv er medlem av en gruppe. «Medlem av» er ikke valgfri pynt: uten
den er drill-throughen en enveiskjørt gate.

**Hjemmel per RAD, aldri per liste.** Hvert medlemskap har sin egen hjemmel med sitt eget
paragrafspenn — det er hele grunnen til at medlemskapet er en egen entitet. Lenken går til nøyaktig
paragrafen (`rettskildeLenkeForId`), ikke bare til rettskilden. En felles «hjemlet i …»-setning over
tabellen er feil så snart to medlemmer kommer fra to ulike forskrifter.

**Bevisst IKKE gjort, eksplisitt flagget etter §14 sin migreringsplikt:** `BegrepDetalj` er fortsatt
ikke migrert til det delte `KontekstPanel`-mønsteret. Å migrere hele siden er et større, selvstendig
grep enn å lukke gruppe-drill-throughen, og de to hører ikke i samme endring. De nye seksjonene
følger derfor sidens eksisterende seksjonsmønster — men de følger §14 og §15 på alt annet: `Card`
ALLTID rendret med tom-tilstand som `Paragraph` inni, `null` = laster ⇒ `Spinner`,
`data-density="compact"` på tabellene, og `Link asChild` rundt react-router sin `Link`.

---

## 17. Aktivt tagg-lag følger DATAENE + appens første frontend-tester (2026-09-08, `tagg-synlig-og-navneformkjede`)

**En visning som viser ETT lag av flere må velge lag etter hva noden faktisk inneholder.**
`TagTekst`/`RettskildeDetalj` forhåndsvalgte det første konfigurerte laget («Begrep») uten å se på
nodens tagger. Forskrift 2005-06-17-657 § 1 ledd-1 har kun `virksomhet`-tagger, så siden åpnet med
14 taggrader listet under teksten og teksten HELT umarkert — for en leser ser det ut som taggingen
ikke virker. Bindende regel, og den gjelder alle framtidige lag-/fane-/filtervelgere:

- **Defaulten deriveres av innholdet** (`velgAktivtLag` i `src/tagging/lagvalg.ts`): første lag i
  konfigurasjonsrekkefølgen som HAR minst én tagg, ellers `kinds[0]`. Rekkefølgen i konfigurasjonen
  brukes bare til å bryte likhet, aldri som svar i seg selv.
- **Brukerens eget valg vinner alltid** over defaulten, også når det valgte laget er tomt — da er
  tom markering det riktige svaret, for brukeren spurte om det laget. «Ikke valgt ennå» må derfor
  være en EGEN tilstand (`null`/tom streng), ikke representert ved en forhåndsvalgt verdi. Et
  kontrollerende forelder-komponent starter på `''` og setter først en verdi når brukeren velger.
- **Defaulten hører i komponenten som ser dataene**, ikke i forelderen. `activeKind` er
  sidenivå-tilstand mens taggene varierer per node; en `useEffect` i forelderen som «fyller inn» en
  default kan ikke følge noden som vises. Det var nettopp en slik effekt som var feilen.

**En liste og en markering skal aldri kunne motsi hverandre.** Tagg-listen under teksten viser ALLE
lag, teksten markerer ett. Løsningen er ikke å filtrere listen — da skjules at noden har arbeid i
andre lag, og det er en dårligere feil. Listen står komplett, og raden ER veien til markeringen: et
klikk på lag-merkelappen eller sitatet aktiverer radens eget lag og ruller markeringen inn i
synsfeltet (`data-tagg-id` på `<mark>` er ankeret). Rader utenfor aktivt lag er dempet
(`opacity: 0.6`) og forklarer seg selv i `title` — uoverensstemmelsen vises og forklares framfor å
skjules.

**Hovedledd vs. hover: det leddet brukeren leser kjeden gjennom skal være det domenet mener, ikke
det registeret heter.** En virksomhet-taggs kjede viser
`«Karasjok» → [Kortform] → «Karasjok kommune»`, der siste ledd er den `gjeldende` NAVNEFORMEN —
ikke virksomhetens tospråklige registernavn («Karasjoga gielda / Karasjok kommune»), som sto der før
og var Johanns innvending. Registernavnet er fortsatt sant og nyttig og er derfor flyttet til
`title` (`titleTillegg`), ikke fjernet: en opplysning som ikke skal være hovedledd, skal degraderes
til hover framfor å forsvinne. Finnes ingen `gjeldende` navneform, faller hovedleddet tilbake til
registernavnet — det som FAKTISK finnes, aldri et navn utledet av kortformen.

**Kjeden løses i visningen, ikke i datamodellen** (`src/virksomhet/navneformKjede.ts`): begge
navneformene peker på samme virksomhet, og mellomleddet finnes ved å slå opp den `gjeldende`
navneformen for den virksomheten. Det er et bevisst, dokumentert valg — en navneform→navneform-FK
ville kostet en migrasjon, et felt å holde konsistent og en ny syklusrisiko (A → B → A) uten å gi
noen opplysning modellen ikke alt har. Revurderes den dagen én virksomhet trenger FLERE gjeldende
navneformer (f.eks. én per målform).

**Appens første frontend-tester.** Det fantes ingen JS/TS-test-runner i repoet før dette; gaten var
`tsc -b --noEmit` + `npm run build` + en faktisk rendring. `vitest` er lagt til som ENESTE nye
devDependency (`npm test` → `vitest run`), og bevisst UTEN `@testing-library`/jsdom: det som testes
er rene funksjoner (`lagvalg.ts`, `navneformKjede.ts`), ikke rendret DOM. Regelen som følger av det
er verdt å ta med videre — **er en regel viktig nok å teste, skal den bo i en ren modul uten
React-avhengigheter**, slik at testen ikke krever et rendringsoppsett. `tsc -b --noEmit` dekker
`*.test.ts` også, siden de ligger under `src`.

## 18. Hjemmel i en tabellrad: paragrafen, ikke eId-en (2026-09-09, nemnd/sekretariat-runden)

**En hjemmel vises som BESTEMMELSEN den er, med lenke til noden.** «§ 36 sjette ledd» — ikke en rå
`https://lovdata.no/eli/lov/2004/03/05/12/nor/§36/ledd-6`, som var det relasjonstabellen på
`VirksomhetDetalj` viste før. Rettskildens navn tas med når raden IKKE alt står under den
rettskilden (relasjonene på én virksomhet peker på ulike lover), og utelates når man alt er på lovens
egen side.

**Paragrafnummeret hentes ALLTID via `src/rettskilde/paragrafEtikett.ts`, aldri fra nodens eget
`nummer`.** Hjemler peker på LEDD, og et ledds `nummer` er leddnummeret: en tagg i konkurranseloven
§ 35 første ledd ble vist som «§ 1», og en hjemmel i § 36 sjette ledd som «§ 6». Feil paragraf er
verre enn en rå eId — den ser riktig ut. Hjelperen klatrer opp til paragrafnoden via `parentNodeId`,
og håndterer at Lovdata-importen legger «§» inn i paragrafnodens nummer men ikke i leddets (rå
strengbygging ga «§ § 36»). Den returnerer `undefined` når nodene ikke er hentet — kalleren viser rå
eId, ingen gjettet etikett. Ti tester i `paragrafEtikett.test.ts` låser dette, inkludert
sirkulær-forelder og manglende paragrafnode.

**«Ingen hjemmel» er en egen, synlig tilstand**, ikke en tom celle: `Tag` med `warning` pluss
kommentaren som sier hvor opplysningen kommer fra i stedet. Et forhold som bare er bekreftet mot et
organisasjonskart skal ikke kunne forveksles med et som står i en bestemmelse — det er samme skille
som `NavneformgrunnTag` håndhever for navneformer (§15).

**Relasjoner leses fra BEGGE sider.** «Organrelasjoner hjemlet her» ligger øverst i
`RettskildeDetalj`s Relasjoner-fane, før dokument-til-dokument-gruppene, fordi det er svaret på
«hvem forvalter loven, og i hvilken egenskap» (docs/32 §3 S1/S2). En hjemmel som bare er synlig fra
virksomhetssiden er ikke etterprøvbar fra bestemmelsen den står i. Fra lovens ståsted brukes ALLTID
Fra-malen («X har sekretariat hos Y») — det finnes ingen «motpart» å velge retning ut fra der.

**Motpartens navn lenkes der det alt står i visningsteksten**
(`src/virksomhet/RelasjonstekstMedLenke.tsx`). Malen fra `RelasjonsTypeKonfigurasjon` inneholder
navnet, så en påhengt «({navn})»-lenke ga «er sekretariat for Konkurranseklagenemnda
(Konkurranseklagenemnda)».

## 19. De to kandidatkøene skal forklares der de brukes (2026-09-09, nemnd/sekretariat-runden)

Navnekandidater og virksomhetskandidater går i MOTSATT retning, og det var ikke synlig noe sted i
UI-et: begge heter «kandidater», begge har Venter/Godkjent/Avvist, begge utløses av en knapp som
heter «Kjør sveip». Johann 2026-09-09: «her flyter det litt sammen».

- **Navnekandidater** — fra TEKST til nytt navn. Leser ÉN rettskilde, foreslår navn vi ikke kjenner.
  Utfall: virksomhet/gruppebegrep + navneform + ÉN tagg (forekomsten kandidaten ble funnet i).
- **Virksomhetskandidater** — fra KJENT navn til alle tekstene. Tar navneformene til én virksomhet og
  leter gjennom hele korpuset. Utfall: én tagg per godkjent forekomst, massegodkjenning tagger alle.

Forklaringen bor i ÉN delt komponent (`src/kandidater/KandidatflytForklaring.tsx`), brukt av begge
listesidene — ikke tre lokale avsnitt som kommer i utakt. Den aktive køen utheves med `fontWeight`,
ikke med farge: begge er like gyldige, dette er «du er her», ikke en tilstand.

**Veiviseren skal tilby NESTE handling, ikke bare vise at den er ferdig.** Etter at en navnekandidat
er behandlet er ÉN forekomst tagget; oppsummeringen tilbyr derfor «Kjør virksomhetssveip» for den
virksomheten, med treff/nye-tall rett i skjermbildet og en lenke inn i den andre køen. En
saksbehandler skal ikke måtte vite at det finnes en annen kø for resten av jobben.

**Et steg uten en beslutning er ikke et steg.** Veiviserens gamle steg 0 («Kontekst») viste bare
setningen fra rettskilden og krevde et «Neste»-klikk. Kortet vises fortsatt, alltid, øverst — men
steg-rekken starter nå på den første faktiske beslutningen («Er teksten riktig?»). Fem steg ble
fire. Gjelder for neste veiviser også: tell beslutninger, ikke skjermbilder.

## 20. Én oppdagelse, ett lag (2026-09-09)

De to køene skal produsere det SAMME når de sier det samme. Godkjenning av en virksomhetskandidat
lagde taggen med `kind = 'begrep'`, mens navnekandidat-veiviseren lagde den med
`kind = 'virksomhet'` — samme påstand om samme organ i to ulike lag, i samme paragraf, avhengig av
hvilken vei den kom fra. Begge lager nå `'virksomhet'`, og en migrasjon flyttet de eksisterende
radene (`OmklassifiserNavneformTaggerTilVirksomhetslaget`). Gruppebegrep-tagger forblir `'begrep'` —
de ER begreper.

**`TekstTagg.VirksomhetId` er EIERSKAP, aldri «hvem taggen handler om».** Godkjenningen satte den til
det TAGGEDE organet, og siden `GET /api/rettskilder/{id}/tagger` bare viser innlogget virksomhets egne
tagger, ble hver godkjente kandidat en tagg ingen kunne se — organene i katalogen har ingen brukere.
Retningslinje: enhver ny skrivevei til `TekstTagg` må ta eieren fra den innloggede brukeren, ikke fra
dataene den behandler.

## 21. Konfidens, ikke avvisning (2026-09-09)

**Systemet klassifiserer; mennesket avgjør.** En navnekandidat SNL/SSR ikke bekrefter får LAV
KONFIDENS og blir stående som «Venter» — den avvises ikke. Se `docs/31` §9 for hvorfor: de organene
som er «skjult og kun synlig på nettsider» er nettopp dem SNL ikke skriver om, så SNL-dekning kan
ikke brukes som en avgjørelse.

`KonfidensTag` (`src/kandidater/KonfidensTag.tsx`) er ÉN delt kilde for visningen, brukt av
listesiden og veiviseren. Fargevalget er et krav, samme prinsipp som `NavneformgrunnTag` (§15):
`success` for høy, `neutral` for lav. Lav er BEVISST ikke `warning`/`danger` — det er ingen feil og
ingen advarsel, bare fravær av bekreftelse, og en rød lapp ville gjenskapt avvisningen vi fjernet.
Ingen lapp for `null` (ikke klassifisert).

**Grunnen skrives ut, ikke bare konfidensen.** I tabellen som `title` under merkelappen, i veiviseren
som hel setning i kontekstkortet. «Lav» uten hvorfor er ikke handlingsrettet — se
`konfidensGrunnTekst` for de fire kodene, inkludert `null`-tilfellet («grunn ikke registrert»), som er
en ekte, dokumentert tilstand for rader migrert fra den gamle auto-avvisningen.

**Et filter, ikke bare en kolonne.** «Lav konfidens» er der de reelle, men lite omtalte organene
ligger; den listen er en arbeidsliste i seg selv.

**Når data flytter seg, må lagvalget følge.** `finnStandardLag` valgte det FØRSTE laget med minst én
tagg. Da gruppebegrepene ble tagget i forskrift 2005-06-17-657 § 1 fikk noden 7 `begrep`-tagger ved
siden av 14 `virksomhet`-tagger, og defaulten skjulte de 14 kommunenavnene bak en fane. Regelen er nå
«laget med FLEST tagger», med `kinds`-rekkefølgen som likhetsbryter. Prinsippet: en default som
bestemmer hva brukeren SER ved kald åpning skal vise mest mulig av det som faktisk er markert.

## 22. Appens ytre side (html/body) skal ALDRI scrolle — kun de to indre panelene (2026-09-11, issue #269)

Johann observerte doble scrollbarer på `/navnekandidater` i Chrome ved 100 % zoom — to
scrollbar-spor tett ved siden av hverandre, ikke sidemenyens og hovedinnholdets naturlige to (som
sitter langt fra hverandre, én ved x≈260px, én ved høyre kant).

**Første forklaring var FEIL — selvkorrigert samme runde (§16).** Første antagelse var en
100vh-vs-viewport avrundingskvirk (skjermens fysiske piksler som ikke deler seg jevnt på CSS-piksler
ved visse zoom-nivåer). `html, body { overflow: hidden }` ble lagt til på det grunnlaget — men Johann
oppdaget rett etter at bunnen av siden (pagineringskontrollen på `/begrepskandidater`, «Vis pr.
side»/sidetall) nå var permanent AVKUTTET og umulig å scrolle til, ikke bare kosmetisk dobbel-
scrollbar. Det var beviset på at forklaringen var feil: en brøkdels-piksel-avrunding kan ikke
forklare et konsekvent, MÅLBART tap på 48px av innhold.

**Den faktiske årsaken**: `.innhold` (`index.css`) har `height: 100%` OG `padding: var(--ds-size-6)`
(24px), men INGEN `box-sizing: border-box`. Nettleserens default `content-box` betyr at padding
legges UTENPÅ den angitte høyden — elementets faktiske rendrede høyde ble dermed alltid 100% + 2×24px
= 48px HØYERE enn `.layout` (målt: `.innhold.clientHeight` 768px mot `.layout.offsetHeight` 720px,
helt uavhengig av zoom). Dette hadde INGENTING med 100vh-avrunding å gjøre — det var en ren
boksmodell-feil, til stede på ALLE zoom-nivåer hele tiden. Før denne runden ble de 48 pikslene
absorbert (stygt, men funksjonelt) av at `body` fikk lov til å overflow scroll dem inn i syne — det
VAR "den doble scrollbaren" Johann så, ikke en synsvilling. Da `overflow: hidden` ble lagt til på
`html`/`body` uten å først fjerne selve overflow-ÅRSAKEN, forsvant scrollbaren, men de 48 pikslene
forble fysisk utenfor viewporten og ble nå util­gjengelige i stedet for bare stygt synlige — en reell
regresjon, ikke en forbedring, inntil dette ble oppdaget og rettet i samme runde.

**Retting**: `.innhold` fikk `box-sizing: border-box`, slik at `height: 100%` inkluderer paddingen i
stedet for å legge den utenpå. Bekreftet: `.innhold.clientHeight` er nå identisk med
`.layout.offsetHeight` (720 = 720), scroll til bunn av `/begrepskandidater` viser hele
pagineringskontrollen.

**Kartlagt (samme runde)**: dette er IKKE en side-spesifikk feil som må rettes 74 steder — `.layout`/
`.sidebar`/`.innhold` er ÉN delt struktur i `App.tsx`, brukt av HVER ENESTE rute i appen, så
boksmodell-feilen rammet alle sider likt, og fiksen retter alle likt. Sjekket for øvrig at appens
andre `overflow: auto`-bruk (kodevisning i `RettskildeDetalj.tsx`, kontekstpanel i
`TjenesteDetalj.tsx`/`KontekstPanel.tsx`, korte lister i `AvhengigheterFane.tsx`/`HandlingerFane.tsx`
m.fl.) er bevisst avgrensede indre scroll-bokser (`maxHeight` satt), ikke berørt av denne
boksmodell-sårbarheten. `.sidebar__nav` bruker `flex: 1`, ikke `height: 100%` + padding på samme
element — samme feilmønster gjelder den IKKE på samme måte, ikke sjekket videre utover det.

**Regel, to deler**:
1. Ethvert element som kombinerer en `height`/`width` basert på foreldrens størrelse (`100%`, eller en
   `flex`-fordelt størrelse) MED egen `padding`, MÅ ha `box-sizing: border-box` — ellers overflower
   det foreldren sin med nøyaktig paddingens størrelse, uansett zoom, alltid, ikke bare i grensetilfeller.
2. `html, body { overflow: hidden }` STÅR VED LAG som en permanent, defensiv sperre mot at den ytre
   siden noensinne kan scrolle — men den er en sperre, ikke en fiks. Legges en slik sperre til for å
   dekke over et symptom (en scrollbar), sjekk ALLTID om det faktisk fjerner ekte, tilgjengelig
   innhold FØR den landes — se selvkorrigeringen over for hvorfor det ikke er en retorisk øvelse.

## 23. Design-canvasen (2026-09-11) — hva den er, og hvor den bor

En hel-applikasjons design-gjennomgang (`design`-skillet i Claude Code) resulterte i 16 artboards
som dekker fundament, komponenter, alle 14 listesider + 8 detaljsider, og hvert avvikende mønster
(veivisere, rettskildelesing/tagging, KI-forslagskø, graf/tre-visualisering, dokumentvisning,
skjema, tjenestereise/søk, håndbokforfatting). Kilden ligger i `docs/design-canvas/` (16
`.dc.html`-filer + `canvas.json`) — sjekket inn som et varig øyeblikksbilde, ikke generert på nytt
ved behov, fordi verktøyets egen midlertidige arbeidsmappe og den publiserte lenken begge er
skjøre (økt-lokale, kan ryddes/miste eierskap). En interaktiv, redigerbar visning av samme innhold
er publisert som et Artifact; lenken står i `docs/design-canvas/README.md`.

**Canvasen er referanse, ikke kravspesifikasjon.** Den er skrevet for å vise HVORDAN noe skal se
ut og hvorfor, med ekte målte tall og faktiske kodesitater — men den binder ingenting alene. Det
canvasen fant og Johann besluttet, er destillert til §24–28 under; det er DE seksjonene som er
bindende, på samme måte som resten av dette dokumentet. Der canvasen viser et forslag som ikke er
besluttet ennå (VirksomhetDetalj-omstruktureringen, §28), sier §28 det eksplisitt.

## 24. Metatekst — en delt komponent for 12px hjelpetekst (issue #266, 2026-09-11)

**231 steder i koden bruker `--ds-font-size-1` (12px) direkte i en inline-stil**, fordi
`Paragraph`-komponentens egen `data-size`-skala aldri når ned dit (`xs` = 14px, `sm` = 16px, `md`
= 18px — ingen av dem er "metatekst"). Resultatet er 231 uavhengige steder som må endres samstemt
den dagen 12px-verdien endres, og ingen enkelt kilde å lese kontrakten fra.

**Regel**: enhver 12px-hjelpetekst (feltbeskrivelser, tabell-metadata, tidsstempler, `title`-aktig
sekundærtekst) skal gå gjennom én delt `Metatekst`-komponent, ikke en ny inline `fontSize`/
`--ds-font-size-1`-referanse. Komponenten er ren presentasjon (ingen egen logikk) — poenget er
étt sted å style om fra, ikke ny funksjonalitet.

## 25. Statusfarge-konsolidering — fire ulike domener, ikke én generisk funksjon (issue #267, 2026-09-11, bygget samme dag)

Seks lokale status→farge(+tekst)-konstanter ble funnet — men, rettet ved selve bygningen (under er
det som faktisk stemte, ikke det opprinnelige canvas-anslaget): **kun fire av dem er reell
duplisering**. `STATUS_FARGE` fantes i SEKS filer, ikke fire — men to av dem
(`AdministrasjonLovdataResynk.tsx`: `Pågår/Fullført/Feilet`, `KildefeilListe.tsx`:
`Ny/Kjent/Rettet-hos-oss/Venter-på-Lovdata`) har samme variabelNAVN men et helt annet nøkkelrom og
domene (synkroniseringsjobb-status, feilkø-status) — samme navn valgt uavhengig tre steder, ikke
duplisert kode. Konsolidert kun de fire med IDENTISK `Venter/Godkjent/Avvist`-skjema:

| Konstant | Reelt duplikat i | Ikke duplikat (samme navn, annet domene) |
|---|---|---|
| `STATUS_FARGE` (Venter/Godkjent/Avvist) | `VirksomhetKandidaterListe`, `Begrepskandidater`, `NavnekandidaterListe`, `BegrepDefinisjonRelasjonKo` (×4) | `AdministrasjonLovdataResynk`, `KildefeilListe` |
| `DOKUMENTTYPE_FARGE`/`VEILEDNINGSDOKUMENTTYPE_FARGE` (hjemmel/praktisk-råd/sjekkliste/kommentar) | `TjenesteVeiledning`, `Egenskapspanel` (×2) | `KommentarRedigering`s `DOKUMENTTYPER` (kommentar/retningslinje/instruks/håndbok — håndbok-SEKSJONENS type, et annet domene) |
| `STATUS_VISNING` (6-status entitetsstatus) | `TjenesterListe`, `HandlingerListe` (×2) | — |
| `KommentarStatus` (under_arbeid/til_godkjenning/publisert/må_revideres) | `RettskildeTre` (×1, ingen duplikat å konsolidere) | — |

**Johanns beslutning (2026-09-11, via spørsmålsverktøyet)**: konsolider PER REELT DOMENE, ikke til
én generisk «status → farge»-funksjon for alt. Bygget:

1. **Kandidatstatus** (`STATUS_FARGE` ×4) → `kandidater/KandidatStatusTag.tsx`.
2. **Dokumenttype** (×2) → `handbok/DokumenttypeTag.tsx`.
3. **Entitetsstatus** (`STATUS_VISNING` ×2) → flyttet inn i `entitet/StatusStepper.tsx` (samme fil
   som allerede eier `STATUS_VERDIER`) og eksportert derfra, ikke en ny, egen fil — se rettelsen
   rett under for hvorfor "gjenbruk StatusStepper sin fargekilde" var upresist formulert.
4. **`KommentarStatus`** (RettskildeTre) → EGEN, ikke slått sammen med de tre andre — reelt ulikt
   domene, ingen kodeendring.

**Rettelse ved bygging**: opprinnelig sto det her "gjenbruk `StatusStepper` sin egen fargekilde" —
upresist. `StatusStepper`s Tag-RAD farger etter POSISJON relativt til gjeldende steg (`accent`+fylt
for nåværende, `neutral`+fylt for passerte, `neutral`+outline for fremtidige; `Dialog`-bekreftelse
KUN på publisert → tilbaketrukket/arkivert) — det er en annen, posisjonsbasert fargelogikk enn det
`TjenesterListe`/`HandlingerListe` trenger (én enkelt Tag med FAST farge per statusverdi, uavhengig
av hva som er "gjeldende"). Det som faktisk lot seg dele var LABEL+FARGE-PARET for de seks
verdiene — flyttet til samme modul som `STATUS_VERDIER` (`entitet/StatusStepper.tsx`, eksportert som
`STATUS_VISNING`), ikke stepper-radens interne posisjonslogikk.

## 26. Mål tabelltetthet, ikke anta den (issue #267, 2026-09-11)

Reelt målt radantall (live API, 2026-09-11): Begreper=1, Brukere=7, Datasett=4, **Handlinger=1020**,
Kodelister=7, Tjenester=1, Vilkårstre=1 (samme datakilde som Tjenester), **Virksomheter=501**.

**Regel**: `data-density="compact"` på `Table` skal begrunnes med et reelt målt radantall for den
konkrete listen, ikke antas fordi «det er en liste». Med tallene over kvalifiserer per nå kun
Handlinger og Virksomheter — de seks andre listesidene har for få rader til at tetthet er et reelt
problem, og skal IKKE tvinges inn i samme mønster kun for konsistensens skyld (jf. §16, «mål det,
ikke anta det»).

## 27. `data-size` på knapper — konvensjon, ikke automatikk (issue #267, 2026-09-11)

`Button`/`data-size="sm"` skal brukes i kompakte arbeidsflater (tabellrader, inline-handlinger ved
siden av skjemafelt, verktøylinjer) — standardstørrelsen er for åpne sider, ikke for tette rader.
Ved gjennomgangen manglet 57 av 241 knapp-forekomster i koden en eksplisitt `data-size` der
konteksten tilsa `sm`. Retting rulles ut sammen med resten av issue #267, ikke som en egen runde —
den henger sammen med tabelltetthet-arbeidet i §26 (samme tette rader).

## 28. Liste- og detaljsidemal — generisk mønster for alle 14 + 8 sider (issue #266/#267, 2026-09-11)

**Alle 8 nykartlagte detaljsider** (og reelt alle 14 listesider) bruker samme to strukturer, målt
ved full gjennomgang av hver enkelt fil — ikke antatt fra noen få eksempler:

- **Listemal**: søkefelt + evt. gruppering/filter øverst, `Table` (tetthet per §26), og — punktet
  som IKKE var konsistent ved gjennomgangen — en `Card`-basert tom-tilstand som skal vises alltid
  når listen er tom, ikke bare betinget rendret rundt en betingelse som glemmer selve Card-en (reell
  bug funnet og rettet i `VirksomhetKandidaterListe`/`NavnekandidaterListe`, issue #265).
- **Detaljmal**: brødsmulesti → faner (`Tab`) → `KontekstPanel`-grupper i sidepanelet. Antall
  KontekstPanel-grupper er IKKE nødvendigvis likt mellom entiteter — RettskildeDetalj har 5,
  TjenesteDetalj har 3 — det er riktig, ikke en inkonsekvens, når entitetene faktisk har ulikt
  antall relasjonstyper (§29 utdyper for RettskildeDetalj spesifikt).
- Ett funnet, ikke rettet ennå: `KodelisteDetalj`/`DatasettDetalj` har en tom-tilstand som avviker
  fra malen over — dokumentert i canvasen, ikke bygget.

## 29. VirksomheterListe/VirksomhetDetalj — foreslått, IKKE besluttet omstrukturering (issue #268)

**Dette er et forslag i canvasen, ikke en vedtatt endring** — i motsetning til §24–27, som er
Johanns faktiske beslutninger. Forslaget: `VirksomheterListe` har i dag tre fullt synlige
opprett-skjema (søk i Brreg / opprett med navn / koble eksisterende navn) alltid synlig over selve
katalogen, slik at katalogen (hovedsaken) først blir synlig etter scroll. Alle tre skjemaene har
individuelt god begrunnelse — problemet som foreslås løst er at de er flate, ikke at de finnes.
Forslaget samler dem bak én «+ Legg til virksomhet»-inngang som faner i ett panel (progressiv
avdekking, ingen funksjon fjernet), og tilsvarende slår `VirksomhetDetalj` sine 8 flate seksjoner
sammen til faner, der «Myndighet & relasjoner» slår sammen 3 tidligere seksjoner. Bygges kun hvis/
når issue #268 tas videre — ikke gjør denne endringen ut fra denne seksjonen alene.

