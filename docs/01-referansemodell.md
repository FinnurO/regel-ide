# Referansemodell for regelverk, beslutninger og digitalisering

*Felles begrepsapparat for jurister, fagpersoner, arkitekter og utviklere som arbeider med Regel-IDE. Les denne før `02-produktkrav.md` — skjermene og datamodellen der bruker begrepene herfra, og §4 forklarer hvor kravspesifikasjonens egne begreper (bevisst) avviker fra denne modellen og hvorfor det er markert som en åpen beslutning.*

## 1. Formål

Modellen skiller tydelig mellom:

- hva som gjelder etter regelverket,
- hvordan regelverket vurderes,
- hvordan vurderingen modelleres,
- hvordan logikken uttrykkes,
- hvordan løsningen implementeres teknisk.

Grunnprinsipp: **en regel er ikke det samme som teksten den står i, beslutningslogikken som bruker den, eller programkoden som realiserer den.** Disse er ulike uttrykk og realiseringer av samme normative innhold. `digital-rettsstat/docs/06-regellaget.md` sier dette enda skarpere: *"Regel" betyr tre ulike ting* — for juristen en rettsregel, for fageksperten en beslutnings-/forretningsregel, for utvikleren kjørbar logikk. Samme ord, tre artefakter, tre eiere, tre feilmodi. Regel-IDE eksisterer for å gjøre disse tre til separate, navngitte, sporbart koblede artefakter i stedet for én sammensmeltet ting — se §4.

## 2. Overordnet begrepsmodell

Regelbasert beslutningstaking består av fire hovedområder:

```
┌─────────────────────────────────────────────┐
│ 1. Normativt område                          │
│    Regler og rettslig/faglig innhold         │
├─────────────────────────────────────────────┤
│ 2. Faktisk område                            │
│    Opplysninger om virkeligheten             │
├─────────────────────────────────────────────┤
│ 3. Vurderings- og beslutningsområde          │
│    Anvendelse av regler på fakta             │
├─────────────────────────────────────────────┤
│ 4. Teknisk område                            │
│    Modellering og implementasjon             │
└─────────────────────────────────────────────┘
```

---

## DEL 1 — Normativt område

### 3. Regelkilde
En **regelkilde** er en kilde som etablerer, konkretiserer, begrunner eller påvirker forståelsen av regler — juridiske, organisatoriske, avtalemessige eller faglige. Eksempler: lov, forskrift, avtale, virksomhetspolicy, interne instrukser.

### 4. Rettskilde
En **rettskilde** er en regelkilde som brukes i juridisk metode for å fastslå, tolke eller begrunne gjeldende rett: lovtekst, forskrift, rettspraksis, forarbeider, juridisk teori.

```
Regelkilder
      └── Rettskilder
              ├── Lov
              ├── Forskrift
              ├── Rettspraksis
              └── Forarbeider
```

Alle rettskilder bidrar til forståelsen av regler, men ikke alle regelkilder er rettskilder.

### 5. Regel
En **regel** er en norm som beskriver en sammenheng mellom bestemte vilkår og en konsekvens. En regel består normalt av vilkår, rettsfølge, unntak og definisjoner.

> Eksempel: *En person har rett til ytelse dersom personen har fylt 18 år og oppfyller medlemskravet.*

### 6. Vilkår
Et **vilkår** er en betingelse som følger av en regel — hva som må være oppfylt. Vilkåret beskriver kravet, ikke den konkrete situasjonen.

Schartum (2025, avsnitt 7.6.4) skiller mellom **formelle vilkår** (må være oppfylt for at saken skal realitetsbehandles: gyldig søknad, klarlagt identitet, nødvendig dokumentasjon) og **materielle vilkår** (må være oppfylt for at selve kravet skal tas til følge, f.eks. alder ≥ 18). Anbefalt redigeringsrekkefølge i regelverket — og dermed i vilkårstreet — er: formelle vilkår → materielle vilkår → rettsfølger. Regel-IDE bør la et vilkår merkes med `vilkarstype: formell | materiell`.

### 7. Rettsfølge
En **rettsfølge** er konsekvensen som følger av regelen når vilkårene er oppfylt: rett til ytelse, plikt til betaling, fritak, ansvar.

### 8. Unntak
Et **unntak** begrenser eller endrer hovedregelen. Schartum (2025, avsnitt 7.6.4) anbefaler at unntak/restriksjoner formuleres i egne, selvstendige ledd/noder etter hovedregelen — aldri underforstått. Dette har en direkte konsekvens for vilkårstreet: unntak bør være en **egen, navngitt nodetype**, ikke skjult som en ordinær `barn`-node med `IKKE`-operator (se §8).

### 9. Definisjoner
En **definisjon** fastsetter betydningen av sentrale begreper i regelverket, og sikrer felles forståelse mellom fag og teknologi.

---

## DEL 2 — Faktisk område

### 10. Fakta
**Fakta** beskriver den konkrete virkeligheten regelen anvendes på — fødselsdato, inntekt, bosted, medlemsperiode, dokumenterte forhold. Fakta eksisterer uavhengig av regelen.

---

## DEL 3 — Vurderings- og beslutningsområde

### 11. Vilkårsvurdering
En **vilkårsvurdering** sammenholder fakta med et vilkår og gir et resultat (f.eks. *oppfylt*). Viktig for forklarbarhet, kontroll, klagebehandling og revisjon.

### 12. Regelanvendelse
**Regelanvendelse** er når en regel brukes på et konkret faktagrunnlag: relevante fakta + aktuelle regler + gjennomførte vurderinger → resultat.

### 13. Beslutningsmodell
En **beslutningsmodell** beskriver hvordan et beslutningsspørsmål avgjøres — hvilke vurderinger (vilkår) det er avhengig av.

### 14. Beslutningsregel
En **beslutningsregel** er en operasjonalisering av én eller flere regler for å avgjøre et bestemt spørsmål — komposisjonslogikken (f.eks. "alle obligatoriske vilkår oppfylt og ingen unntak gjelder").

### 15. Beslutningsresultat
Et **beslutningsresultat** er utfallet av en konkret beslutningsprosess: innvilget, avslått, sendt til manuell behandling, mangler dokumentasjon, avventer opplysninger. Beslutningsresultatet er ikke nødvendigvis det samme som rettsfølgen.

---

## DEL 4 — Representasjon og teknologi

### 16. Regelmodell
En **regelmodell** beskriver strukturen i regelverket: regler, vilkår, begreper, sammenhenger, avhengigheter, unntak.

### 17. Regelrepresentasjon
Formen en regel uttrykkes i:

| Representasjon | Formål |
|---|---|
| Juridisk tekst | Uttrykke gjeldende rett |
| Fagmodell | Strukturere regelinnhold |
| Beslutningstabell | Modellere beslutningslogikk |
| DMN | Modellere beslutninger |
| Regelmotorspråk (eFLINT/OpenFisca/RuleML) | Gjøre regler kjørbare |
| Programkode | Teknisk utførelse |

### 18. Regeluttrykk
Den konkrete formuleringen av en regel innenfor én representasjon. Regelrepresentasjon = formen; regeluttrykk = innholdet i formen.

### 19. Regelimplementasjon
Den tekniske realiseringen av regeluttrykket (regelmotor, applikasjonslogikk, API), sporbar tilbake til beslutningslogikk og regelverk.

---

## 20. Samlet referansemodell

```
                    REGELKILDER
                         │
                         ▼
                       REGLER
                         │
          ┌──────────────┼──────────────┐
          │              │              │
       Vilkår        Rettsfølge       Unntak
                                          
                       FAKTA
                         │
                         ▼
              VILKÅRSVURDERING
                         │
                         ▼
               REGELANVENDELSE
                         │
                         ▼
            BESLUTNINGSRESULTAT

REGEL
 ├── Regelmodell
 ├── Beslutningsmodell
 └── Regelrepresentasjon
          │
          ▼
      Regeluttrykk
          │
          ▼
   Regelimplementasjon
```

## 21. Samlet begrepsoversikt

| Begrep | Betydning |
|---|---|
| Regelkilde | Kilde som etablerer eller påvirker forståelsen av regler |
| Rettskilde | Juridisk relevant regelkilde |
| Regel | Normativt innhold |
| Vilkår | Betingelse i regelen |
| Rettsfølge | Normativ konsekvens |
| Unntak | Begrensning av regel |
| Definisjon | Forklaring av begrep |
| Fakta | Opplysninger om virkeligheten |
| Vilkårsvurdering | Sammenstilling av fakta og vilkår |
| Regelanvendelse | Bruk av regel på konkret sak |
| Beslutningsmodell | Modell av beslutningsprosessen |
| Beslutningsregel | Logikk for å avgjøre spørsmål |
| Beslutningsresultat | Utfallet av behandlingen |
| Regelmodell | Strukturert modell av regler |
| Regelrepresentasjon | Formen regelen uttrykkes i |
| Regeluttrykk | Konkret formulering av regelen |
| Regelimplementasjon | Teknisk realisering |

## 22. Hovedprinsipp

- **Norm og virkelighet** — regler beskriver hva som skal gjelde; fakta beskriver hva som faktisk foreligger.
- **Regel og beslutning** — regelen beskriver normen; beslutningen beskriver anvendelsen av normen.
- **Vilkår og vurdering** — vilkåret beskriver kravet; vilkårsvurderingen avgjør om kravet er oppfylt.
- **Innhold og representasjon** — regelen er innholdet; regeluttrykket og representasjonen er formen.
- **Faglig logikk og teknisk implementasjon** — beslutningslogikken beskriver hva som skal avgjøres; implementasjonen beskriver hvordan det utføres.

Dette gir en sporbar kjede fra regelkilde til automatisert beslutning uten å blande sammen juss, fag og teknologi.

---

## 3. Logiske operatorer: hvorfor OG/ELLER/IKKE — ikke XOR/NAND

Førsteutkastet av kravspesifikasjonen (kap. 7.6) lot `barn_operator` på en vilkårsnode være ett av `OG / ELLER / XOR / NAND`. Den eksterne vurderingen av dokumentet foreslo å vurdere om XOR/NAND var nødvendige. Svaret er nei, av to uavhengige grunner:

1. **Schartum (2025), avsnitt 7.3.4.2** ("Handlingsbegreper om vilkår og sammenligninger") gjennomgår systematisk hvilke operatorer automatiseringsvennlig lovgivning trenger for å uttrykke vilkårsstrukturer, og lander på nettopp **OG, ELLER, IKKE** — pluss sammenligningsoperatorene `<`, `<=`, `>`, `>=`, `=`, `≈` for beregninger. XOR og NAND nevnes ikke; norsk regelverk uttrykker den underliggende logikken (f.eks. "gjensidig utelukkende vilkår") ved eksplisitt tekst eller ved å bryte den opp i separate bestemmelser med IKKE, ikke ved en dedikert eksklusiv-eller-operator.
2. **`digital-rettsstat`s eksportformater** (DMN, FLINT/eFLINT, OpenFisca, NRML/RuleML) modellerer beslutningslogikk med boolske kombinasjoner av OG/ELLER/IKKE og sammenligningsoperatorer som primitiv. XOR/NAND må uansett kompileres om til disse ved eksport, noe som gjør dem til en ekstra abstraksjon uten tilsvarende presisjonsgevinst — og som gjør den genererte forklaringsteksten (forklaringsloggen, kap. 4.12 i `02-produktkrav.md`) unødvendig vanskeligere å formulere presist for en part som ikke er teknisk.

**Beslutning:** `barn_operator` (kap. 7.6 i `02-produktkrav.md`) har verdiene `OG`, `ELLER`, `IKKE` (unær, på ett barn eller en gruppe). Sammenligninger på datapunkter (`<`, `<=`, `>`, `>=`, `=`, `≈`) er en egen operatorklasse knyttet til `input_datasett`, ikke til `barn_operator`. `KL-VILKARSUTFALL` (kap. 3.4 i `02-produktkrav.md`) er uendret.

---

## 4. Terminologi på tvers av repoene

Regel-IDE, `forklaringsmodell-api` og `digital-rettsstat` er skrevet uavhengig av hverandre og bruker delvis ulike ord for de samme begrepene i denne referansemodellen. Tabellen under er den autoritative oversettelsesnøkkelen — bruk den når du leser på tvers av repoene, og hold den oppdatert når begreper endres i noen av dem.

| Referansemodell (denne) | Regel-IDE kravspesifikasjon | `forklaringsmodell-api` | Kommentar |
|---|---|---|---|
| Rettskilde | `Rettskilde` (kap. 3.1) | `Rettskilde` | Samsvarer |
| Fakta | `Datasett`/`datapunkt` (kap. 3.6) | `Faktum` | Samsvarer, ulikt navn — vurder å bruke "Faktum" konsekvent, se `07-forklaringsmodell-api-avvik.md` |
| Vilkårsvurdering | Implisitt i `KL-VILKARSUTFALL` (Saksbehandling) | `Vurdering` (med `Utfall`) | Samsvarer i innhold |
| Beslutningsresultat | Ikke egen entitet — kun `innvilgelsestekst`/`avslagstekst` på vilkårsnoden | `Vedtak.Utfall` | **Gap**: Regel-IDE mangler en eksplisitt `Vedtak`-entitet i domenemodellen, se `03-domenemodell.md` §1 |
| Regelrepresentasjon | Output-format (eFLINT/DMN/OpenFisca/RuleML), kap. 3.8 | `Regel.Teknologi` | Samsvarer |
| Regelimplementasjon | Eksportmotor (kap. 4.14) | `Regel.RegeldefinisjonReferanse` (ekstern peker) | Samsvarer i prinsipp |
| **Regel** *(beslutningsregel/komposisjonslogikk)* | **Ikke en egen entitet** — smeltet inn i `Vilkår` (kap. 3.8) | `Regel` (operasjonalisert, med `Teknologi`, koblet til `Rettskilde`) | **Se §5 — den åpne beslutningen** |
| **Vilkår** *(atomært, testbart)* | **Samme entitet som over** — `Vilkår` (kap. 3.8) er både regelnode og betingelse | `Vilkar` (flat katalogpost, `RegelId` valgfri kobling) | **Se §5** |
| Rettsfølge | Implisitt i `utdata_parameter` / innvilgelses-/avslagstekst | `Vedtaksvirkning` | Bør gjøres eksplisitt, se §5 |
| Unntak | Ikke modellert som egen nodetype | Ikke modellert | Bør legges til, se §5 |

## 5. Den åpne beslutningen: vilkårstreet

*Dette er markert eksplisitt som et punkt som trenger flere iterasjoner — ikke en avgjort løsning. Beskrivelsen under er et forslag til utgangspunkt for diskusjon, ikke en låst spesifikasjon.*

### 5.1 Problemet

Kravspesifikasjonens `Vilkår`-entitet (kap. 3.8 i `02-produktkrav.md`) er strukturelt en **regeltre-node**: den har `barn[]` (undernoder), `barn_operator` (komposisjonslogikk), `input_datasett[]` og `utdata_parameter` (hva noden produserer). Det er ikke det samme som denne referansemodellens **Vilkår** (§6 over — en enkelt, atomær betingelse). Det kravspesifikasjonen kaller "Vilkår" er faktisk en sammensmelting av:

- et **Vilkår** i referansemodellens forstand (bladnode: en enkelt testbar betingelse, f.eks. "alder ≥ 18"),
- en **Regel**/beslutningsregel (komposisjonsnode: hvordan flere vilkår/regler kombineres med OG/ELLER/IKKE til et delresultat), og
- delvis en **Rettsfølge** (via `utdata_parameter` og innvilgelses-/avslagstekst på samme node).

`digital-rettsstat/docs/06-regellaget.md` navngir nettopp denne sammensmeltingen som *hovedårsaken til oversettelsesgapet* mellom jurist, fagekspert og utvikler, og foreskriver som mottiltak: *"Navngi de tre, aldri bare 'regel'."* Det taler for å dele opp kravspesifikasjonens `Vilkår`-node.

### 5.2 Hvorfor det ikke bare kan rettes med et navnebytte

`forklaringsmodell-api` har allerede løst dette på sin måte: `Vilkar` er flat referansedata (en gjenbrukbar, katalogført betingelse/virkning), mens `Regel` er det operasjonaliserte, versjonerte artefaktet (DMN/Python/LLM) som er koblet til `Rettskilde`, og som en `Vilkar` *valgfritt* kan referere til (`Vilkar.RegelId`). Grafstrukturen — hvordan flere vilkår komponeres til ett resultat — er **ikke modellert eksplisitt der**; den ligger implisitt inni regelmotor-artefaktet (`Regel.RegeldefinisjonReferanse`, f.eks. selve DMN-XML-en).

Regel-IDE sin **grafeditor (kap. 4.4)** er derimot nettopp verktøyet for å *bygge* den komposisjonsstrukturen visuelt, før den eksporteres til DMN/eFLINT/osv. Det betyr at regel-IDE trenger å modellere komposisjonsleddet eksplisitt — noe `forklaringsmodell-api` bevisst ikke gjør (det tar imot det ferdige regelartefaktet). De to modellene løser altså delvis forskjellige problemer, og en ren "gjør det likt `forklaringsmodell-api`"-løsning er ikke tilstrekkelig alene.

### 5.3 Forslag til retning (til diskusjon)

Innfør tre distinkte nodetyper i grafeditoren, i stedet for én `Vilkår`-type:

| Nodetype | Rolle | Tilsvarer i referansemodellen | Tilsvarer i `forklaringsmodell-api` |
|---|---|---|---|
| **Vilkår** (bladnode) | Én atomær, testbar betingelse mot ett eller flere `input_datasett` | Vilkår | `Vilkar` |
| **Regel** (komposisjonsnode) | `barn[]` + `barn_operator` (OG/ELLER/IKKE) + `utdata_parameter`; kan ha `Vilkår`- eller `Regel`-noder som barn | Regel / Beslutningsregel | `Regel` (ett `Regel`-eksportformat per node, eller én DMN/eFLINT-graf for hele treet — se `05-arkitektur-og-nfk.md` §1) |
| **Unntak** (spesialisert barn av Regel) | Egen, navngitt nodetype i stedet for en vanlig node med IKKE-operator, jf. Schartum §8 over | Unntak | Ikke modellert ennå |

Rotnoden i treet ("Vedtak om skjenkebevilling") er da en **Regel**-node hvis `utdata_parameter` er selve beslutningsresultatet/rettsfølgen. Dette holder også `avklaringsbehov`-mønsteret fra `digital-rettsstat/docs/06-regellaget.md` §3 (NAVs "stopp-punkt" for skjønn) rendyrket: et skjønnsbasert/hybrid vilkår (kap. 3.8.1) er en **Vilkår**-node der `vurderingstype ≠ regelbasert`, og som dermed alltid får utfall fra `KL-VILKARSUTFALL` i stedet for et beregnet resultat — grensen regel/skjønn blir synlig som en egen nodeegenskap, ikke en implisitt konsekvens av hvor i treet noden ligger.

### 5.4 Hva som IKKE er avgjort ennå

- Om `Regel`-noder i treet eksporteres én-til-én til separate regelartefakter (én DMN-node per `Regel`-node) eller om hele (del-)treet kompileres til ett samlet DMN/eFLINT-dokument. Dette påvirker `Regel.RegeldefinisjonReferanse`-granulariteten i `forklaringsmodell-api` og bør avklares før eksportmotoren (kap. 4.14) bygges.
- Om migrering av eksisterende `Vilkår`-noder fra førsteutkastet til det nye skjemaet gjøres automatisk (heuristikk: node uten barn → Vilkår, node med barn → Regel) eller manuelt av fagansvarlig/jurist per tjeneste.
- Om `Unntak` skal være en egen nodetype i grafen, eller et flagg (`erUnntak: boolean`) på en vanlig node. Anbefalingen i §5.3 er egen nodetype, for å tvinge frem eksplisitt formulering (jf. Schartum §8), men dette bør testes mot alkoholloven-testcaset (f.eks. unntaket fra skjenketid ved lukkede selskap) før det låses.

Dette punktet eies foreløpig av fagansvarlig + jurist for testcaset, med teknisk støtte fra den som bygger grafeditoren og eksportmotoren. Se `06-veikart.md` for når i faseplanen dette tas.
