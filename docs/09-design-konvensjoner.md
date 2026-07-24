# Designkonvensjoner (Designsystemet i praksis)

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

## 6. Ikke gjort ennå

- Selvhosting av Inter (kjører fortsatt mot rsms.me i dev).
- Responsiv kollaps av sidemenyen under 880px (krav i produktkrav kap. 7 — ikke implementert).
- `--ds-color-neutral-background-tinted`-varianten av hovedinnhold (kun den enkleste to-flate-varianten
  er valgt så langt).
