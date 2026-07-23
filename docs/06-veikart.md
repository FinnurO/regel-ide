# Veikart — byggerekkefølge

*Dette er forslaget til rekkefølge nevnt i chatten som opprettet dette repoet: start med rettskildene, deretter tjenester og begrep, så vilkårstreet (flere iterasjoner), saksbehandling parkert (men en tynn demo-slice), dashboard sist. Under er det rekkefølgen begrunnet og brutt ned i konkrete byggesteg. "Byggesteg" her er ikke det samme som "Fase 1/2/3" i `02-produktkrav.md` kap. 1.2 — de sistnevnte er den *arkitektoniske* lagdelingen (hva forutsetter hva teknisk); byggestegene under er *rekkefølgen vi faktisk bygger og fyller dem med innhold i*.*

## MVP-/konseptbevis-grense

Byggesteg 1–7 under (rettskilde → begrep → vilkår → test → eksport → forklaring, med saksbehandling som tynn slice) *er* konseptbeviset. Byggesteg 8 (kunnskapsgraf) og 9 (dashboard) er eksplisitt **utenfor** — ikke nedprioritert, men strukturelt umulige å bevise noe med før 1–7 har reelt innhold å vise frem. Se `02-produktkrav.md` kap. 2 for samme grense markert per skjerm.

**Foreslått suksessmål (KPI) — ikke bekreftet, til diskusjon:** *tid fra en rettskildeendring til en oppdatert, testet og publisert regel* (lovspeil-latens). Dette er valgt fordi det er selve tesen bak oversettelsesgap-problemet (`digital-rettsstat/docs/06-regellaget.md`) — ikke fordi det er det eneste rimelige målet. Andre kandidater (forklarbarhetsgrad, gjenbruksgrad på tvers av tjenester, redusert feilrate) er sekundære effekter av at lovspeil-latensen er lav, ikke uavhengige mål i seg selv. Dette er et forslag som venter på bekreftelse fra produkteier, ikke en låst beslutning på linje med ontologien under.

## Byggesteg 0 — Lås ontologien (Vilkår/Regel/Unntak) — **fullført 2026-07-23**

I motsetning til byggestegene under krevde dette ingen kode: det var en ren modelleringsøvelse, testet mot fire konkrete alkoholloven-eksempler (aldersvilkår, vandel-skjønnsvilkår, skjenketid-regel, skjenketid-unntak for lukket selskap). Resultatet — formelle kardinaliteter, invarianter og tillatte relasjoner for de tre nodetypene — står i `01-referansemodell.md` §5, og er lagt inn i `03-domenemodell.md` §1.8–1.10 og `04-api-kontrakter.md` §7. Samtidig ble skjønnsvokabularet (skjønnsgrunnlag/skjønnsmoment/avklaringsbehov, `01-referansemodell.md` §6.1) og Vedtak/Vedtaksgrunnlag/Vedtaksvirkning som eksplisitte begrep (§15.1 der) presisert. Se `00-endringslogg-v0.2.md` for full endringslogg fra denne runden.

Konsekvensen for byggesteg 4 under: det gjenstår ikke lenger å *avgjøre* nodeskjemaet — bare å *implementere og validere* det låste skjemaet mot testcaset.

## Forholdet til `digital-rettsstat`s eget veikart

`digital-rettsstat/docs/02-veikart.md` opererer på et høyere nivå (Fase 0 Forankring → Fase 1 Standard og pilot → Fase 2 Institusjonalisering → Fase 3 Skalering). Alt som beskrives her — hele regel-IDE — er **innholdet i digital-rettsstats Fase 1**: *"Demonstrer stabelen: ta et utvalg forskrifter inn i kildelaget med ELI-identifikatorer → formaliser regellogikken i regellaget med eksplisitt hjemmelssporing → koble på data → vis en tjeneste der et vedtak kan spores tilbake til versjonert bestemmelse."* Skjenkebevilling/alkoholloven som testcase er dessuten samme regelverk som Helsedirektoratets "Alkoholfloken"-arbeid (`digital-rettsstat/docs/04-norske-case.md`, Case D) — et bevisst, ikke tilfeldig, valg av pilotområde. Merk at alkoholloven har **både** bundet rettsanvendelse (skjenketid, aldersgrense) **og** forvaltningsskjønn (dispensasjon, vandelsvurdering) i samme regelverk — det er en fordel, ikke en ulempe, fordi byggesteg 4 uansett må løse grensen regel/skjønn, og da er det bedre å møte den tidlig på et virkelig eksempel enn å utsette den ved å velge et rendyrket regelbasert område først.

## Byggesteg 1 — Rettskildebibliotek

**Hvorfor først:** alt annet i domenemodellen refererer til en `eId` i en rettskilde (begrep via `lovreferanse`, vilkår via `juridisk_grunnlag`, presedens via `tilknyttede_bestemmelser`). Uten importert rettskildetekst har verken begrepsdefinisjoner eller vilkår noe å peke på, og tekstmerking/tagging (AK-3.3.1) er meningsløs. `digital-rettsstat/docs/06-regellaget.md` §7 sier det direkte: *"Kildelaget gjør sporbarhet mulig; regellaget gjør den virkelig"* — i feil rekkefølge arver alt nedstrøms gjettingen.

**Innhold:** import (Lovdata-søk + filopplasting, AK-3.3.5–3.3.7), AKN-lagring, tre-navigasjon, metadata-fane, tekstmerking/tagging-mekanikken (uten at det ennå finnes begreper/vilkår å knytte til — lagre taggen med `ref: null` inntil byggesteg 2/4 gir den noe å peke på, eller bygg tagging-UI-et samtidig med byggesteg 2). `quoteSelector`-robusthet (`05-arkitektur-og-nfk.md` §3.1) bygges her, ikke ettermontert senere.

**Testcase-innhold: hele alkoholloven og hele alkoholforskriften, ikke bare de "relevante" kapitlene.** Opprinnelig plan var å bare hente kapittel 1, 3 og 4. Det er feil av to grunner: (1) formålsparagrafen (§ 1-1) og definisjonene (§ 1-3, § 1-4) — som direkte gir oss tjenestebeskrivelsen og begrepsgrunnlaget, jf. `02-produktkrav.md` kap. 3.2/3.8 — står i kapittel 1, ikke i kapittel 4 der skjenkebevilling-vilkårene ligger; (2) vilkårsparagrafene vi faktisk skal modellere (§ 4-1 til § 4-7, kommunale skjenkebevillinger) henviser tilbake til kapittel 1 (§ 1-7a kommunens skjønnsutøvelse, § 1-7b krav til vandel, § 1-5 aldersgrenser) — et bibliotek med bare "de relevante kapitlene" ville brutt disse kryssreferansene. Rettskildebiblioteket (§1.1 i domenemodellen) skal derfor inneholde **hele** loven og **hele** forskriften; utvalget av vilkår vi faktisk bygger i byggesteg 4 (alder, vandel, skjenketid, unntak for lukket selskap) er fortsatt lite, men navigasjon og kryssreferanser skal virke for hele dokumentet.

- **Lov:** LOV-1989-06-02-27, "Lov om omsetning av alkoholholdig drikk m.v. (alkoholloven)", 11 kapitler.
- **Forskrift:** FOR-2005-06-08-538, "Forskrift om omsetning av alkoholholdig drikk mv. (alkoholforskriften)".
- Begge hentet 2026-07-23 fra Lovdatas offisielle, gratis bulk-datasett (`gjeldende-lover.tar.bz2` / `gjeldende-sentrale-forskrifter.tar.bz2`, NLOD 2.0-lisens, kildeangivelse påkrevd) — se `data/kilder/README.md` for proveniens og `05-arkitektur-og-nfk.md` §1 for hvordan importfunksjonen (AK-3.3.5) skal bruke Lovdatas offisielle API i produktet, ikke denne engangs-bulk-hentingen.
- Rådataen er Lovdatas "XML-kompatible HTML": `<section>`=kapittel, `<article class="legalArticle" data-lovdata-URL="…/§4-3">`=paragraf, `<article class="legalP">`=ledd, med kryssreferanser allerede som `<a href="lov/…/§X">`. `data-lovdata-URL`-verdien er så godt som en ferdig `eId` — konverteringen til AKN (byggesteg 1s implementasjon) er derfor en strukturell ombygging av allerede taggede grenser, ikke en tolkning av løpetekst.

**Testkommunens egne lokale rettskilder skal også inn i biblioteket, ikke bare den nasjonale loven/forskriften.** Bekreftet ved gjennomgang av ekte kommunale dokumenter (Vennesla og Tønsberg kommuner, se funnene under): en kommune har typisk **to** typer egne rettskilder for skjenkebevilling, og begge er reelle, ikke valgfrie for testcaset:

- En **lokal forskrift** (f.eks. om salgs-/skjenketider) — dette *er* en Lovdata-registrert rettskilde (`kildetype='Forskrift'`, `doctype='act'`), akkurat som alkoholforskriften, bare med en kommune som utsteder i stedet for et departement. Tønsberg kommunes `LF/forskrift/2020-12-09-2924` er et reelt eksempel.
- **Alkoholpolitiske retningslinjer** (vedtatt av kommunestyret, alkoholloven § 1-7d) — dette er **ikke** en Lovdata-registrert rettskilde. Den passer `kildetype='Virksomhetsdokument'` i skjemaet vi allerede har (`03-domenemodell.md` §1.1) — ingen skjemaendring nødvendig. Dette er en konkret, virkelig instans av skillet mellom **Regelkilde** og **Rettskilde** som `01-referansemodell.md` §3–4 definerer: retningslinjene styrer forvaltningen (regelkilde), men er ikke en rettskilde i streng juridisk-metode-forstand.

Begge typer setter reelle, tilleggsvilkår og -parametre ut over selve alkoholloven/-forskriften (skjenketider, konseptbegrensninger, individuelle vilkår som universell utforming og "Ansvarlig vertskap"-kurs) — de er ikke bare en gjentakelse av nasjonalt regelverk, og må derfor faktisk importeres/forfattes for Testkommunen, ikke bare nevnes.

**Idé til fremtidig scope (ikke besluttet, ikke i MVP):** et mål for regel-IDE kan være å la brukeren **forfatte** slike lokale retningslinjer/forskrifter i verktøyet selv — ikke bare importere dem — og publisere dem når kommunestyret har vedtatt dem. Dette er teknisk en naturlig utvidelse, ikke en ny arkitektur: Rettskilde-livssyklusen (`03-domenemodell.md` §3.2, `utkast → gjeldende`) dekker allerede "forfattet lokalt, så vedtatt"-forløpet uten endring — det som mangler er en "Opprett ny rettskilde fra bunnen av"-modus ved siden av "Importer" i kap. 3.3 i produktkrav. Dette er ikke tatt inn i noe byggesteg ennå og krever en egen avklaring av omfang (er dette Fase 1/MVP, eller en senere utvidelse av Kildelaget-siden av verktøyet?) før det speces videre.

### Funn fra reelle virksomhetsdokumenter (2026-07)

Gjennomgang av Vennesla kommunes rutine for saksbehandling av bevillingssøknader, Vennesla og Tønsberg kommuners alkoholpolitiske retningslinjer 2024–2028, og Helsedirektoratets *Veileder i salgs- og skjenkekontroll* (IS-2038) bekreftet mye av eksisterende design (aldersgrensen 20 år for styrer/stedfortreder, tjenesteavhengigheten skjenkebevilling→serveringsbevilling, Kontroll/tilsyn som reell hendelsestype, og at samme generiske vilkårsmal faktisk får ulike parametre per kommune i praksis — Vennesla og Tønsberg har forskjellige skjenketider/aldersgrenser på samme lovhjemmel). Seks funn er ikke dekket av dagens design, og noteres her som kontekst for de byggestegene de påvirker — ingen av dem endrer noe alt bygget:

1. **Forvaltningsskjønn, atskilt fra rettsanvendelsesskjønn (byggesteg 4).** "Serveringsbevilling *skal* gis dersom vilkårene er oppfylt" vs. "Skjenkebevilling *kan* gis dersom vilkårene er oppfylt (større lokalpolitisk skjønn)" — en kommune kan avslå selv når alle navngitte vilkår er oppfylt. Dette er noe annet enn å tolke et vagt rettslig standard (skjønnsgrunnlag, `01-referansemodell.md` §6.1), og er ikke dekket av ontologien i dag.
2. **Vandelsvurderingens tidsvindu varierer med lovhjemmel (byggesteg 4).** Serveringsloven ser 5 år tilbake, alkoholloven 10 år — og det er gjerningstidspunktet, ikke doms-/foreleggstidspunktet, som teller. En parameter på skjønnsgrunnlaget vi ikke har modellert ennå.
3. **To distinkte sakstyper (byggesteg 6/7).** Søknad om bevilling og kontroll/reaksjon (prikktildeling → inndragning, egen klagegang til Statsforvalteren) er strukturelt forskjellige saksforløp, ikke varianter av samme flyt. `forklaringsmodell-api`s `HendelseType` har allerede `Kontroll` som verdi, men saksforløpets *struktur* er ikke tenkt gjennom.
4. **Saksbehandler-habilitet, fvl. § 6 (byggesteg 7).** Et vilkår om selve saksbehandlingsprosessen, ikke om søkeren — verken vilkårstreet eller domenemodellen dekker denne typen prosessvilkår i dag.
5. **"Grader av påvirkning" som reell kodeliste (byggesteg 2/6).** Edru → lett påvirket → åpenbart påvirket → kraftig påvirket → bevisstløs — brukt i løpende kontroll av skjenkesteder. En konkret `KL-`-kandidat.
6. **Individuelle vilkår satt ved selve bevillingsvedtaket, alkoholloven § 4-3 (byggesteg 4/7).** Utover eligibility-vilkårene kan kommunen sette ekstra, saksspesifikke vilkår ved vedtaket. Delvis dekket av `forklaringsmodell-api`s `Vedtaksvirkning.LopendeVilkar`, ikke eksplisitt i vår egen modell ennå.

## Byggesteg 2 — Tjenester og begrep sammen

**Hvorfor sammen, og hvorfor nå:** Schartum (2025) 7.3.2 beskriver begrepskartlegging som noe som skal skje *med utgangspunkt i* eksisterende rettskilder — nøyaktig byggesteg 1s output. Å definere tjenesten (kap. 3.2) og begrepene (kap. 3.8) side om side gir de gode definisjonene chatten etterspurte, fordi tjenestedefinisjonens regelverksreferanser og begrepenes `lovreferanse` skal peke på de samme `eId`-ene — å bygge dem hver for seg risikerer at de driver fra hverandre.

**Innhold:** Tjenestedefinisjon (grunndata, hendelser, avhengigheter — uten vilkårskobling ennå, den kommer i byggesteg 4), Begrepsregister med `begrepstype` (faktabegrep/handlingsbegrep, `03-domenemodell.md` §1.3), og Kodelister/verdiregister (kap. 3.7) bygges parallelt siden begrep ofte har en `kodeliste_referanse`.

**Testcase-innhold:** tjenesten "Alminnelig skjenkebevilling"; begreper som "uklanderlig vandel", "styrer og stedfortreder", "skjenketid"; kodelistene `KL-VANDELSOMRADE-ALKOHOLLOV` og `KL-RETTSKILDEVEKT`.

## Byggesteg 3 — Presedensregister

**Hvorfor nå og ikke tidligere/senere:** presedens kan først kobles meningsfullt til `eId` (byggesteg 1) og gi kontekst til begrepstolkning (byggesteg 2) — men trengs *før* byggesteg 4, fordi AI-assistert forslag til vilkårstre (kap. 4.2 i produktkrav) eksplisitt søker presedensregisteret som andre steg i sin prosess.

**Innhold:** Presedensregister (kap. 3.9), uten AI-forslagsskjermen ennå.

## Byggesteg 4 — Vilkårstre (grafeditor)

**Ontologien er låst (byggesteg 0)** — Vilkår/Regel/Unntak som distinkte nodetyper med faste kardinaliteter og invarianter, testet mot alkoholloven-eksemplene i `01-referansemodell.md` §5.5. Det som gjenstår her er implementasjon og validering, ikke skjemadesign:

1. **Runde 1 — bygg mot testcaset.** Grafeditor + egenskapspanel for alle tre nodetyper (`02-produktkrav.md` kap. 3.4), fylt med aldersvilkåret (Vilkår, regelbasert), vandelsvilkåret (Vilkår, skjønnsbasert, med skjønnsgrunnlag/skjønnsmomenter), skjenketid-regelen (Regel) og skjenketid-unntaket for lukket selskap (Unntak). Formål: bekrefte at DAG-validering (AK-3.4.6), graf-/tre-veksling og de type-spesifikke egenskapspanelene faktisk virker på et reelt eksempel, ikke bare i diagrammet i referansemodellen.
2. **Runde 2 — testmodul og publisering.** Testmodulen (kap. 3.15) og publiseringsmodellen/livssyklusen (`03-domenemodell.md` §3–§4) bygges når grunnstrukturen fra runde 1 er verifisert — testcaser (§1.13 i domenemodellen) trenger et stabilt nodeskjema å referere `tilknyttet_node[]` mot, og publisering skal ikke øves på noe som fortsatt er under utforskning.

Hvis alkoholloven-eksemplet avdekker at en av de sju invariantene (`01-referansemodell.md` §5.4) faktisk ikke holder i praksis, er det en grunn til å gå tilbake til byggesteg 0 og justere ontologien — ikke til å lage en unntaksregel i implementasjonen som omgår den.

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
| 0. Lås ontologien (Vilkår/Regel/Unntak) — **fullført** | — (kun `01-referansemodell.md` §5) | — |
| 1. Rettskildebibliotek | 3.3 | 0 |
| 2. Tjenester + Begrep (+ Kodelister) | 3.2, 3.7, 3.8 | 1 |
| 3. Presedensregister | 3.9 | 1, 2 |
| 4. Vilkårstre (2 runder) | 3.4, delvis 3.15 | 0, 1, 2, 3 |
| 5. AI-forslag | 3.10 | 4 |
| 6. Datasett, informasjonsmodell, eksport | 3.5, 3.6, 3.14 | 4, 5 |
| 7. Saksbehandling/forklaringslogg (tynn slice) — **MVP-grense** | deler av 3.11, 3.12 | 4, 6, + avklaring mot `forklaringsmodell-api` |
| 8. Kunnskapsgraf/påvirkningsanalyse — utenfor MVP | 3.13 | 1–6 |
| 9. Dashboard — utenfor MVP | 3.1 | alt |
