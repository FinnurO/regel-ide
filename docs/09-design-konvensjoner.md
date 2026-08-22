# Designkonvensjoner (Designsystemet i praksis)

> **[LÅST — 2026-08-22]** Detaljert spesifikasjon fra Johann, verifisert mot faktisk installerte
> pakkeversjoner (`package.json`: `@digdir/designsystemet-css`/`-react` `^1.18.0`,
> `-theme` `^1.11.0` — stemmer eksakt). Dette er nå den AUTORITATIVE kilden for eksakte
> farge-/mål-/typografiverdier — §§1-8 under er fortsatt gyldige for MØNSTRE (hvilken komponent, hvor
> tokens brukes), men eksakte hex/px-verdier her vinner ved motstrid.

## 0. Eksakt spesifikasjon (målt mot en kjørende instans, tema "Digdir")

**Typografi**: Inter (UI/løpetekst); IBM Plex Mono kun for kode/eId/organisasjonsnummer-aktig tekst.
Brødtekst/paragraf 18px/400/line-height 27px. Sideheading (H1, "lg") 36px/500/line-height 46.8px.
Underheadinger typisk 21–24px/600. Småtekst/meta (tags, hjelpetekst) 14px.

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
`List`, `Alert`. De første 12 (Button→Checkbox) er alt som faktisk er brukt i dag — resten finnes i
biblioteket men er ALDRI brukt ennå; bruk dem der de løser et reelt problem (se kjente
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

## 7. Ikke gjort ennå

- Selvhosting av Inter (kjører fortsatt mot rsms.me i dev).
- Responsiv kollaps av sidemenyen under 880px (krav i produktkrav kap. 7 — ikke implementert).
- `--ds-color-neutral-background-tinted`-varianten av hovedinnhold (kun den enkleste to-flate-varianten
  er valgt så langt).
