# 30. Bestilling: Saksbehandlertilpasning av Designsystemet i Regel-IDE

> Fulltekst-transkripsjon av Johanns bestilling, mottatt 2026-09-02, verbatim (samme
> transkripsjons-konvensjon som `docs/28-navnekandidat-presisjon-innspill.md`). Se
> `docs/09-design-konvensjoner.md` §14 for den destillerte, bindende oppsummeringen — DETTE dokumentet
> er detaljspesifikasjonen/bestillingen, §14 er hva enhver fremtidig UI-endring faktisk skal sjekkes mot
> først.

**Til:** Claude Code, `FinnurO/regel-ide`, `src/RegelIde.Web`
**Bakgrunn:** Full gjennomgang av frontend (App.tsx, Sidebar, alle `pages/*.tsx`, `tjeneste/*`,
`vilkarstre/*`) mot `docs/09-design-konvensjoner.md`. Konklusjon: Designsystemet-tokens og
-komponenter brukes stort sett korrekt isolert sett, men **appen er bygget som om den var en
informasjonsnettside** (én ting av gangen, generøs luft, lange skjema nedover siden) når den faktisk
er et **saksbehandler-/ekspertverktøy** (mange entiteter, tette relasjoner, store lister — 5893
rettskilder, ~3990 navnekandidater, dype vilkårstrær). Dette dokumentet er en komplett bestilling for
å rette det, uten å bryte designsystem-bindingen (kap. 6, produktkrav) — vi endrer MØNSTRE og
TETTHET, ikke farger/fonter/tokens.

---

## 1. Diagnose — gjennomgående funn

### 1.1 To ulike sidemønstre lever side om side
`TjenesteDetalj.tsx` (redesignet 2026-08-27) er allerede riktig retning: brødsmulesti, faner +
accordions, alt-i-ett lagre, og et **høyre kontekstpanel** (`KontekstPanel.tsx`) som samler
relasjoner. Resten av entitetssidene (`VirksomhetDetalj`, `BegrepDetalj`, `HandlingDetalj`,
`RettskildeDetalj`, `VilkarstreDetalj`) er fortsatt det gamle mønsteret: en lang, endimensjonal
stabel av `<section>`-er, hver med egen `Heading level=2` + eget mini-skjema + egen "Lagre"-knapp,
ingen brødsmulesti (kun en bar "← Tilbake"-lenke), ingen samlet relasjonsvisning.

Konsekvens: `RettskildeDetalj.tsx` (62 KB, sannsynligvis 15-20 seksjoner) og `HandlingDetalj.tsx`
(38 KB, 12 seksjoner) er begge én lang scroll uten oversikt — nøyaktig det brukeren allerede har
opplevd som problemet i Tjeneste-siden FØR redesignet.

### 1.2 Card brukes betinget, ikke strukturelt
Gjennomgående (`VirksomhetDetalj.tsx` og flere): `<Card>` rendres KUN når en liste har innhold; tomme
lister faller til en bar `<Paragraph>` uten ramme. Resultat: samme seksjon ser strukturert ut når den
har data og ustrukturert/flat ut når den er tom — brukeren kan ikke stole på et konsistent visuelt
skjelett.

### 1.3 Ingen samlet relasjons-/kontekstvisning utenfor Tjeneste-siden
`KontekstPanel.tsx` er riktig idé (regelverksreferanser, hendelser, avhengigheter i én
alltid-synlig liste), men finnes KUN på Tjeneste-siden. På `RettskildeDetalj`, `BegrepDetalj`,
`VilkarstreDetalj` er kryssreferanser spredt som separate `<section>`-er nedover siden ("Hjemmel",
"Hjemmel for", "Brukt i tjenester", "Brukt i vilkår", "Referert fra håndbøker" …) — reelt den samme
informasjonskategorien (relasjoner ut fra denne entiteten), men vist inkonsekvent seks forskjellige
steder i seks forskjellige filer.

### 1.4 Typografi og tetthet er kalibrert for lesing, ikke saksbehandling
Designsystemets standard brødtekst er 18px/line-height 27px — riktig for en informasjonsside der
folk leser løpende tekst. Regel-IDE-teamet har allerede oppdaget dette og kompensert ved å tvinge
`fontSize: 'var(--ds-font-size-1)'` (14px) på nesten ALL hjelpetekst/metadata i hele kodebasen (§7 i
designkonvensjonene nevner dette gjentatte ganger) — et tegn på at grunntettheten ikke passer
verktøyet, løst med punktvise unntak i stedet for en bevisst tett variant.

### 1.5 Tabeller er lagd for å bla i, ikke for å behandle i bulk
`Table`-bruken i de fleste lister er riktig (paginering finnes, §9), men
**redigeringsmønsteret** er gjennomgående "les-only tabell + eget skjema under" (legg til →
refresh → se i tabellen igjen), aldri inline-redigering. For et saksbehandlerverktøy med tette
datasett er dette tregt. `NavnekandidaterListe.tsx` (massehandling, gruppering, sortering) er det
ENESTE stedet i appen som er bygget med ekspertbruker-tetthet i tankene — resten av listesidene
mangler samme investering.

### 1.6 Status/arbeidsflyt er en gjemt dropdown
Alle entiteter (Vilkår, Regelnode, Unntak, Begrep, Handling) har samme 6-trinns statusmodell
(`utkast → under_revisjon → validert → publisert → tilbaketrukket → arkivert`), men den vises alltid
som en bar `<Select>` uten visuell indikasjon av hvor i flyten man er, ingen bekreftelse ved
kritiske overganger (publisert→tilbaketrukket), og ingen synlig "hvem kan gjøre denne overgangen".
For en saksbehandlingsløsning er statusflyt kjernefunksjonalitet, ikke et skjemafelt blant andre.

### 1.7 Ingen global navigasjon på tvers av korpuset
Med 5893 rettskilder, ~3990 navnekandidater og et voksende antall tjenester/vilkår/begrep finnes det
ikke noe globalt søk/kommandopalett — kun sidemenyen (statiske lenker) og hver listesides eget
filter. En domeneekspert som vet hva de leter etter (f.eks. "§ 5-2 i alkoholloven") må navigere
Kilder → Rettskilder → finn i en lang liste, hver gang.

### 1.8 Ingen "sist lagret"/dirty-state
Hver seksjon har sin egen "Lagre"-knapp uten indikasjon av om noe er endret-men-ulagret. I et skjema
med 10+ uavhengige lagre-knapper på én side (`HandlingDetalj.tsx`) er det lett å tro alt er lagret
når kun én seksjon faktisk ble det.

---

## 2. Designprinsipp: saksbehandlerverktøy ≠ informasjonsside

Designsystemet ER riktig valgt (offentlig, kontrastsikret, kjent for norske saksbehandlere fra andre
verktøy) — det er BRUKSMØNSTERET som må endres. Vi beholder 100 % av fargene, fontfamilien og alle
`--ds-*`-tokens uendret. Det som endres:

| Dimensjon | Informasjonsside (dagens standardbruk) | Saksbehandlerverktøy (målbilde) |
|---|---|---|
| Tetthet | Generøs luft, 18px brødtekst | Kompakt, `--ds-font-size-1/-2` som standard i arbeidsflater, store tekststørrelser reservert for titler |
| Navigasjon | Én side av gangen, "← Tilbake" | Brødsmulesti + kontekstpanel + globalt søk, alltid vite hvor man er og hva som henger sammen |
| Relasjoner | Nevnt i løpetekst/lenker spredt utover siden | Samlet, alltid synlig relasjonspanel (KontekstPanel-mønsteret, appliseres overalt) |
| Redigering | Skjema → lagre → se resultat et annet sted | Inline der mulig, tydelig ulagret-indikator, bulk-handling for lister |
| Status/flyt | Et felt blant andre | Egen visuell komponent (stepper/badge-rekke), synlig og styrende |
| Tomme tilstander | Usynlig/ustrukturert | Samme strukturelle skjelett (Card) uansett innhold |
| Lister | Bla side for side | Søk/filter/gruppering/massehandling som standard, ikke unntak |

---

## 3. Konkret spesifikasjon

### 3.1 Ett felles "Entitetsside"-mønster (bygg som delt struktur, ikke kopier per side)

Lag en delt shell — `src/RegelIde.Web/src/entitet/EntitetSide.tsx` (eller tilsvarende) — etter
nøyaktig samme oppskrift som `TjenesteDetalj.tsx` + `KontekstPanel.tsx` allerede beviser fungerer:

1. **Brødsmulesti** øverst (samme `nav aria-label="Brødsmulesti"`-mønster) på ALLE entitetssider —
   `VirksomhetDetalj`, `BegrepDetalj`, `HandlingDetalj`, `RettskildeDetalj`, `VilkarstreDetalj`
   mangler dette i dag.
2. **Tittel + statuslinje** rett under (tittel, Tag-rekke for status/type/eier) — gjenbruk mønsteret
   fra `TjenesteDetalj`/`VirksomhetDetalj`, men konsekvent overalt.
3. **Faner ELLER accordions for innhold** når en entitet har mer enn ~4 seksjoner (Rettskilde og
   Handling kvalifiserer klart; Begrep og Virksomhet er grensetilfeller — bruk faner der det er >4
   seksjoner, behold enkel scroll der det er 2-3).
4. **`KontekstPanel`-mønsteret generaliseres** til en delt komponent som tar en liste av
   `{ heading, items: {label, onClick}[] }`-grupper — bruk den på Rettskilde (Hjemmel/Hjemmel
   for/Brukt i tjenester/Brukt i vilkår/Referert fra håndbøker blir ÉN panel, ikke fem seksjoner),
   Begrep (Brukt i vilkår/Brukt i rettskilder), Vilkårstre (Egenskapspanelets kryssreferanser).
5. **Card rendres alltid**, uavhengig av om innholdet er tomt — tom-tilstand er en `<Paragraph>` INNI
   kortet, aldri utenfor.

### 3.2 Kompakt arbeidsflate-modus
Innfør et konsekvent tetthetsvalg for ALT som er arbeidsflate (skjemafelt, tabeller, lister,
metadata) fremfor punktvise `fontSize`-unntak: `data-size="sm"` konsekvent på
`Textfield`/`Select`/`Button`/`Table` i disse kontekstene (biblioteket støtter dette allerede — bruk
det systematisk, ikke ad-hoc `fontSize`-overstyring per element som i dag). Reserver standard/`md`-
størrelse for sidetittel (H1) og de aller viktigste tallene (KPI-kort).

### 3.3 Statusflyt som egen komponent
Bygg `StatusStepper` (delt komponent, samme 6 statusverdier gjenbrukt overalt): en horisontal rekke
av `Tag`/`Badge` som viser hele flyten med gjeldende steg fremhevet (`data-color="accent"` på aktivt
steg, `neutral` på passerte, `subtle`/outline på fremtidige), IKKE en dropdown. Klikk på et fremtidig
steg = samme handling som dagens `Select onChange`, men brukeren SER hele flyten. Kritiske
overganger (`publisert → tilbaketrukket/arkivert`) krever en `Dialog`-bekreftelse (finnes i
komponent-vokabularet, ubrukt i dag).

### 3.4 Globalt søk (kommandopalett)
Legg til ett globalt søk i toppen av `.sidebar` eller som en `Cmd/Ctrl+K`-triggered `Dialog` med
`Suggestion`/`Combobox`-mønsteret appen allerede har bygget for store lister (§10 i
designkonvensjonene — samme "kun topp 50 treff mountes"-teknikk). Søker på tvers av rettskilder,
tjenester, begrep, vilkår — kritisk for et verktøy med tusenvis av rader og domeneeksperter som
søker på navn/§.

### 3.5 Inline-redigering og dirty-state for lister
For korte, entydige felter i lister (f.eks. `Handling.kanaler`, `Vedlegg`-navn) — vurder
inline-redigerbare celler fremfor "skjema under tabellen + refresh". Der full-skjema fortsatt er
riktig (komplekse felt med flere underverdier), legg til en delt `LagreStatusIndikator`-komponent
(liten tekst/ikon: "Lagret" / "Ulagrede endringer" / "Lagrer …") ved siden av hver seksjons
lagre-knapp, slik at en side med 10 uavhengige skjemaer alltid viser hvilke som har ulagrede
endringer.

### 3.6 Datatett tabellvariant
Legg til en `data-density="compact"`-variant (egen CSS-klasse i `index.css`, kun padding-verdier —
ingen nye farger/tokens) for `Table` i sider med store datasett (`RettskilderListe`,
`NavnekandidaterListe`, `VirksomhetKandidaterListe`) — reduser radpadding fra dagens 8-12px til
~4-6px, behold `--ds-color-*` uendret. Gjør dette til systemets standard i disse kontekstene, ikke en
engangs-inline-style.

---

## 4. Migreringsrekkefølge (foreslått)

1. **Bryt ut `KontekstPanel` og `Accordion`-skallet fra `tjeneste/*` til en delt, entitetsuavhengig
   plassering** (f.eks. `src/RegelIde.Web/src/entitet/`) — ingen visuell endring i seg selv, bare
   gjør dem gjenbrukbare.
2. **`RettskildeDetalj.tsx`** (størst og mest presserende — 62 KB, en rettskilde med opptil 15+
   seksjoner er brukerens mest besøkte side): brødsmulesti + faner (Metadata / Innhold&Tagging /
   Relasjoner / Håndbok) + `KontekstPanel` for alle "referert av/hjemmel/brukt i"-seksjonene.
3. **`HandlingDetalj.tsx`**: samme mønster, faner for (Egenskaper/Status, Kanaler&Vedlegg,
   Veiledning&Årsaker, Regelverk&Rotnode).
4. **`VirksomhetDetalj.tsx`**: mindre — behold enkel scroll, men fiks 1.2 (Card alltid rendret) og
   legg til brødsmulesti.
5. **`BegrepDetalj.tsx`**: samme lette fiks som Virksomhet.
6. **`VilkarstreDetalj.tsx`/`Egenskapspanel.tsx`**: allerede to-kolonne (graf/tre + egenskapspanel) —
   legg til brødsmulesti og vurder om Egenskapspanelets "Veiledning"-fane bør flyttes inn i et
   KontekstPanel-lignende mønster for konsistens.
7. **Globalt søk + StatusStepper + kompakt tabelltetthet**: innføres parallelt som delte komponenter,
   tas i bruk side for side i samme rekkefølge som over.

## 5. Ikke i scope her (foreslå egen runde)
- Selve fargepalett/typografi-tokens — uendret, dette er et layout-/mønsterprosjekt.
- Responsiv sidemeny under 880px (kjent, dokumentert mangel i §8 — egen sak).
- Selvhosting av Inter-fonten — egen sak.

## 6. Akseptansekriterier
- Alle entitetssider (Tjeneste, Rettskilde, Handling, Vilkårstre-node, Begrep, Virksomhet) har
  brødsmulesti.
- Ingen seksjon skifter mellom "med ramme" og "uten ramme" avhengig av om den har data.
- Relasjoner/kryssreferanser for én entitet vises på ÉTT sted per side (kontekstpanel eller
  tilsvarende), ikke spredt i 3-6 separate `<section>`-er.
- Statusfelt vises som stepper/badge-rekke på alle entiteter med den delte 6-trinns statusmodellen.
- `RettskilderListe`, `NavnekandidaterListe`, `VirksomhetKandidaterListe` bruker kompakt
  tabelltetthet.
- Et globalt søk finner en rettskilde/tjeneste/begrep/vilkår fra en hvilken som helst side uten å
  navigere via sidemenyen først.
