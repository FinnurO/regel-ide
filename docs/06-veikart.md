# Veikart — byggerekkefølge

*Dette er forslaget til rekkefølge nevnt i chatten som opprettet dette repoet: start med rettskildene, deretter tjenester og begrep, så vilkårstreet (flere iterasjoner), saksbehandling parkert (men en tynn demo-slice), dashboard sist. Under er det rekkefølgen begrunnet og brutt ned i konkrete byggesteg. "Byggesteg" her er ikke det samme som "Fase 1/2/3" i `02-produktkrav.md` kap. 1.2 — de sistnevnte er den *arkitektoniske* lagdelingen (hva forutsetter hva teknisk); byggestegene under er *rekkefølgen vi faktisk bygger og fyller dem med innhold i*.*

## Forholdet til `digital-rettsstat`s eget veikart

`digital-rettsstat/docs/02-veikart.md` opererer på et høyere nivå (Fase 0 Forankring → Fase 1 Standard og pilot → Fase 2 Institusjonalisering → Fase 3 Skalering). Alt som beskrives her — hele regel-IDE — er **innholdet i digital-rettsstats Fase 1**: *"Demonstrer stabelen: ta et utvalg forskrifter inn i kildelaget med ELI-identifikatorer → formaliser regellogikken i regellaget med eksplisitt hjemmelssporing → koble på data → vis en tjeneste der et vedtak kan spores tilbake til versjonert bestemmelse."* Skjenkebevilling/alkoholloven som testcase er dessuten samme regelverk som Helsedirektoratets "Alkoholfloken"-arbeid (`digital-rettsstat/docs/04-norske-case.md`, Case D) — et bevisst, ikke tilfeldig, valg av pilotområde. Merk at alkoholloven har **både** bundet rettsanvendelse (skjenketid, aldersgrense) **og** forvaltningsskjønn (dispensasjon, vandelsvurdering) i samme regelverk — det er en fordel, ikke en ulempe, fordi byggesteg 4 uansett må løse grensen regel/skjønn, og da er det bedre å møte den tidlig på et virkelig eksempel enn å utsette den ved å velge et rendyrket regelbasert område først.

## Byggesteg 1 — Rettskildebibliotek

**Hvorfor først:** alt annet i domenemodellen refererer til en `eId` i en rettskilde (begrep via `lovreferanse`, vilkår via `juridisk_grunnlag`, presedens via `tilknyttede_bestemmelser`). Uten importert rettskildetekst har verken begrepsdefinisjoner eller vilkår noe å peke på, og tekstmerking/tagging (AK-3.3.1) er meningsløs. `digital-rettsstat/docs/06-regellaget.md` §7 sier det direkte: *"Kildelaget gjør sporbarhet mulig; regellaget gjør den virkelig"* — i feil rekkefølge arver alt nedstrøms gjettingen.

**Innhold:** import (Lovdata-søk + filopplasting, AK-3.3.5–3.3.7), AKN-lagring, tre-navigasjon, metadata-fane, tekstmerking/tagging-mekanikken (uten at det ennå finnes begreper/vilkår å knytte til — lagre taggen med `ref: null` inntil byggesteg 2/4 gir den noe å peke på, eller bygg tagging-UI-et samtidig med byggesteg 2). `quoteSelector`-robusthet (`05-arkitektur-og-nfk.md` §3.1) bygges her, ikke ettermontert senere.

**Testcase-innhold:** alkoholloven kapittel 1, 3 og 4 (bevillingsplikt, vilkår for bevilling, skjenketider) + relevant forskrift.

## Byggesteg 2 — Tjenester og begrep sammen

**Hvorfor sammen, og hvorfor nå:** Schartum (2025) 7.3.2 beskriver begrepskartlegging som noe som skal skje *med utgangspunkt i* eksisterende rettskilder — nøyaktig byggesteg 1s output. Å definere tjenesten (kap. 3.2) og begrepene (kap. 3.8) side om side gir de gode definisjonene chatten etterspurte, fordi tjenestedefinisjonens regelverksreferanser og begrepenes `lovreferanse` skal peke på de samme `eId`-ene — å bygge dem hver for seg risikerer at de driver fra hverandre.

**Innhold:** Tjenestedefinisjon (grunndata, hendelser, avhengigheter — uten vilkårskobling ennå, den kommer i byggesteg 4), Begrepsregister med `begrepstype` (faktabegrep/handlingsbegrep, `03-domenemodell.md` §1.3), og Kodelister/verdiregister (kap. 3.7) bygges parallelt siden begrep ofte har en `kodeliste_referanse`.

**Testcase-innhold:** tjenesten "Alminnelig skjenkebevilling"; begreper som "uklanderlig vandel", "styrer og stedfortreder", "skjenketid"; kodelistene `KL-VANDELSOMRADE-ALKOHOLLOV` og `KL-RETTSKILDEVEKT`.

## Byggesteg 3 — Presedensregister

**Hvorfor nå og ikke tidligere/senere:** presedens kan først kobles meningsfullt til `eId` (byggesteg 1) og gi kontekst til begrepstolkning (byggesteg 2) — men trengs *før* byggesteg 4, fordi AI-assistert forslag til vilkårstre (kap. 4.2 i produktkrav) eksplisitt søker presedensregisteret som andre steg i sin prosess.

**Innhold:** Presedensregister (kap. 3.9), uten AI-forslagsskjermen ennå.

## Byggesteg 4 — Vilkårstre (grafeditor) — flere iterasjoner forventet

**Hvorfor dette tar flere runder:** dette er punktet hvor `01-referansemodell.md` §5s åpne spørsmål (Vilkår vs. Regel vs. Unntak som distinkte nodetyper) faktisk må avgjøres — ikke i abstrakt, men mot alkoholloven-testcaset. Forslaget er å bygge i tre delrunder i stedet for å prøve å låse skjemaet før noe er bygget:

1. **Runde 1 — samlet node (som i førsteutkastet).** Bygg grafeditoren med dagens `Vilkår`-skjema (`03-domenemodell.md` §1.8) mot et lite, virkelig utsnitt: aldersvilkåret (rent regelbasert) + vandelsvilkåret (skjønnsbasert). Formål: få DAG-validering (AK-3.4.6), graf-/tre-veksling og egenskapspanelet til å virke i det hele tatt.
2. **Runde 2 — prøv oppdelingen.** Innfør `Regel`- og `Unntak`-nodetypene fra §5.3 i referansemodellen på det samme utsnittet, og legg til skjenketid-unntaket for lukket selskap (som testcase for om `Unntak` bør være egen nodetype eller et flagg, jf. §5.4 i referansemodellen). Formål: avgjøre skjemaet med et konkret eksempel å teste mot, ikke i teorien.
3. **Runde 3 — generaliser og migrer.** Når skjemaet er avklart: migrer testcasets vilkår og oppdater `04-api-kontrakter.md` §7 til det endelige skjemaet. Testmodulen (kap. 3.15) bygges i denne runden, siden testcaser (§1.11 i domenemodellen) trenger et stabilt nodeskjema å referere `tilknyttet_vilkar[]` mot.

Publiseringsmodellen (`03-domenemodell.md` §4) og livssyklusen (§3) implementeres i runde 3, ikke runde 1/2 — det er ikke poenget å publisere noe fra utforskningsrundene.

## Byggesteg 5 — AI-forslag

**Hvorfor etter byggesteg 4:** AI-assistenten (kap. 3.10) foreslår vilkårsnoder i det (nå avklarte) skjemaet fra byggesteg 4, og henter presedens fra byggesteg 3. Å bygge den før skjemaet er stabilt ville bety å bygge mot et mål som beveger seg.

## Byggesteg 6 — Datasett, informasjonsmodell, eksportmotor

**Innhold:** Datasett (kap. 3.5) kan til dels bygges parallelt med byggesteg 4 (vilkårsnoder trenger `input_datasett`-referanser), men Informasjonsmodell-skjermen (kap. 3.6, generert JSON Schema) og Eksportvisning (kap. 3.14, eFLINT/DMN/OpenFisca/RuleML) forutsetter et publisert vilkårstre å generere fra, og hører derfor naturlig til etter byggesteg 4/5.

## Byggesteg 7 — Saksbehandling og forklaringslogg: tynn demo-slice

Chatten ba om et forslag her siden saksbehandling er parkert, men hele kjeden bør kunne demonstreres. Forslag: bygg **ikke** full saksbehandlerarbeidsflyt (dokumenthåndtering, kommunikasjon, "Overstyr verdi"-UI) i denne omgang. Bygg i stedet:

- Et **read-only saksoversikt**-skjermutsnitt (deler av kap. 3.11: vilkårstabell med status per vilkår fra `KL-VILKARSUTFALL`) fylt med 2–3 seed-caser kjørt gjennom det publiserte vilkårstreet fra byggesteg 4.
- **Forklaringslogg-skjermen** (kap. 3.12) i sin helhet — dette er visningen som beviser at kjeden rettskilde→begrep→vilkår→data→vedtak→forklaring faktisk henger sammen, og er derfor mer demonstrasjonsverdi verdt enn selve saksbehandlerverktøyet.
- Dette forutsetter at grensesnittet mot `forklaringsmodell-api` er avklart (`05-arkitektur-og-nfk.md` §3.5) — minimum: regel-IDE kan eksportere et publisert vilkår i en form `forklaringsmodell-api` kan registrere som `Regel`/`Vilkar`, og lese tilbake en `Vurdering`/`Vedtak`/`Forklaringslogg` for visning.

Full saksbehandlerarbeidsflyt (overstyring med begrunnelse, dokumentopplasting, kommunikasjonsfane) forblir parkert til etter denne demo-sliceen er bevist, jf. brukerens instruks.

## Byggesteg 8 — Kunnskapsgraf og påvirkningsanalyse

Krever reelt innhold i byggesteg 1–6 for å vise noe meningsfullt — samme begrunnelse som i førsteutkastets opprinnelige Fase 3-plassering.

## Byggesteg 9 — Dashboard

Sist, fordi det er en ren aggregeringsvisning over alt det andre (KPI-er, aktivitetsgraf, AI-forslag-teller). Å bygge det først ville bare vise tomme tall.

## Oppsummert rekkefølge

| Byggesteg | Skjermer (kap. i `02-produktkrav.md`) | Forutsetter |
|---|---|---|
| 1. Rettskildebibliotek | 3.3 | — |
| 2. Tjenester + Begrep (+ Kodelister) | 3.2, 3.7, 3.8 | 1 |
| 3. Presedensregister | 3.9 | 1, 2 |
| 4. Vilkårstre (3 runder) | 3.4, delvis 3.15 | 1, 2, 3 |
| 5. AI-forslag | 3.10 | 4 |
| 6. Datasett, informasjonsmodell, eksport | 3.5, 3.6, 3.14 | 4, 5 |
| 7. Saksbehandling/forklaringslogg (tynn slice) | deler av 3.11, 3.12 | 4, 6, + avklaring mot `forklaringsmodell-api` |
| 8. Kunnskapsgraf/påvirkningsanalyse | 3.13 | 1–6 |
| 9. Dashboard | 3.1 | alt |
