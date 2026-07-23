# Referansemodell for regelverk, beslutninger og digitalisering

*Felles begrepsapparat for tverrfaglige team — tjenestedesignere, jurister, fagansvarlige/saksbehandlere og utviklere — som arbeider med Regel-IDE (jf. `digital-rettsstat` prinsipp 7: tverrfaglighet fra dag én). Les denne før `02-produktkrav.md` — skjermene og datamodellen der bruker begrepene herfra. §4 forklarer hvor kravspesifikasjonens egne begreper avviker fra denne modellen; §5 er ontologien for Vilkår/Regel/Unntak (låst 2026-07-23, se `docs/00-endringslogg-v0.2.md`).*

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

#### 6.1 Skjønnsgrunnlag, skjønnsmoment og avklaringsbehov

Disse tre presiserer hva "skjønnsbasert" (§14, `vurderingstype`) faktisk betyr, og var tidligere kun brukt som feltnavn i datamodellen uten egen definisjon her — det er hullet den siste vurderingen fant.

- **Skjønnsgrunnlag** er den rettslige (eller faglige) standarden et skjønnsbasert vilkår bygger på — selve Begrepet (jf. `03-domenemodell.md` §1.3) hvis innhold er *bevisst* upresist, jf. Schartum (2025) avsnitt 7.5: lovgiver kan ha legitime grunner til å la et begrepsinnhold være skjønnsmessig, for å gi rom for rettsutvikling uten lovendring. Eksempel: **"uklanderlig vandel"** (alkoholloven § 1-7b). Et skjønnsbasert eller hybrid vilkår har nøyaktig **1** skjønnsgrunnlag.
- **Skjønnsmoment** er ett av flere navngitte hensyn som skal vektes for å avgjøre om skjønnsgrunnlaget er oppfylt i den konkrete saken. Et vilkår har **1..N** skjønnsmomenter, hver med `{navn, beskrivelse, presedensreferanse?}`. Eksempel for "uklanderlig vandel": *tidligere bevillingsbrudd*, *økonomisk vandel/skatteforhold*, *straffbare forhold knyttet til virksomheten*. Et skjønnsmoment er **ikke** i seg selv en betingelse som kan evalueres til sant/usant — det er et hensyn en jurist/saksbehandler vekter i sin begrunnelse (tilsvarer `Vurdering.Hovedhensyn` i `forklaringsmodell-api`, som er obligatorisk nettopp når `Type == Skjonn`).
- **Avklaringsbehov** er, i motsetning til de to over, ikke en modelleringstidsegenskap — det er en **hendelse i en konkret sak**: at én bestemt vilkårsvurdering ikke kan avgjøres automatisk med tilstrekkelig grunnlag/konfidens, og derfor må rutes til et menneske. Dette er NAV-mønsteret `digital-rettsstat/docs/06-regellaget.md` §3 kaller et navngitt "stopp-punkt", ikke en unntakshåndtering. Et avklaringsbehov oppstår **alltid** når et vilkår med `vurderingstype ∈ {skjonnsbasert, hybrid}` evalueres, og **kan** i tillegg oppstå for et regelbasert vilkår med manglende/usikkert faktumgrunnlag (`krever_dokumentasjon` i `KL-VILKARSUTFALL`). Feltene `eskaleringsrolle` og `krever_dokumentasjon` (`03-domenemodell.md` §1.8) er avklaringsbehovets egne data: hvem det rutes til, og hva som eventuelt mangler.

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

#### 15.1 Vedtak, vedtaksgrunnlag og vedtaksvirkning

Beslutningsresultatet manglet en eksplisitt, sentral domenekonstruksjon — den siste vurderingen påpekte at "Vedtak" bare fantes implisitt (i `innvilgelsestekst`/`avslagstekst` på vilkårsnoden). Presisert:

- **Vedtak** er den formelle avgjørelsen truffet i en konkret sak, på grunnlag av **én** evaluering av rot-Regelen (§5.3) i et vilkårstre mot et konkret faktagrunnlag. Vedtaket er et frosset øyeblikksbilde (jf. `forklaringsmodell-api`s `Vedtak`), ikke en levende tilstand.
- **Vedtaksgrunnlag** er settet av vilkårsvurderinger, fakta og rettskilde-/presedensreferanser som begrunner vedtaket — innholdet i forklaringsloggen (produktkrav kap. 3.12), og det som gjør vedtaket etterprøvbart iht. forvaltningsloven § 25.
- **Vedtaksvirkning** er den konkrete, tidsavgrensede konsekvensen av vedtaket (tillatelse, plikt, gebyr …) — én rettsfølge (§7) instansiert for den konkrete saken.

Regel-IDE **eier ikke** Vedtak/Vedtaksgrunnlag/Vedtaksvirkning som driftsdata — det gjør `forklaringsmodell-api`, som allerede har `Vedtak` og `Vedtaksvirkning` i sitt skjema (se `07-forklaringsmodell-api-avvik.md`). Men forfatterverktøyet må kjenne begrepene presist for å (a) generere innvilgelses-/avslagstekst per Regel-node som senere blir vedtaksgrunnlag-tekst, og (b) simulere et Vedtak i testmodulen (produktkrav kap. 3.15) uten å måtte kjøre mot en reell sak. Feltnivå-skjemaet regel-IDE bruker for dette står i `03-domenemodell.md` §1.16.

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
| Vedtak | Den formelle, frosne avgjørelsen i en konkret sak |
| Vedtaksgrunnlag | Vurderinger/fakta/kilder som begrunner vedtaket |
| Vedtaksvirkning | Konkret, tidsavgrenset konsekvens av vedtaket |
| Skjønnsgrunnlag | Den (bevisst upresise) rettslige standarden et skjønnsvilkår bygger på |
| Skjønnsmoment | Ett hensyn som vektes for å avgjøre om skjønnsgrunnlaget er oppfylt |
| Avklaringsbehov | Hendelse: en konkret vurdering må rutes til et menneske |
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
| **Regel** *(beslutningsregel/komposisjonslogikk)* | `Regelnode` — egen nodetype, se §5 | `Regel` (operasjonalisert, med `Teknologi`, koblet til `Rettskilde`) | Ulikt navn bevisst valgt — se §5.6 |
| **Vilkår** *(atomært, testbart)* | `Vilkår` — egen nodetype (alltid bladnode), se §5 | `Vilkar` (flat katalogpost, `RegelId` valgfri kobling) | Samsvarer nå strukturelt |
| **Unntak** | `Unntak` — egen nodetype, se §5 | Ikke modellert ennå | Forslag i `07-forklaringsmodell-api-avvik.md` |
| Rettsfølge | `utdata` på rot-Regelnoden, jf. §15.1 (Vedtak) | `Vedtaksvirkning` | Samsvarer nå |
| Vedtak/Vedtaksgrunnlag/Vedtaksvirkning | Ikke egendrevet — se §15.1 og `03-domenemodell.md` §1.16 | `Vedtak` / `Vedtaksvirkning` | Regel-IDE kjenner begrepene, `forklaringsmodell-api` eier driftsdataene |

## 5. Ontologien: Vilkår, Regel og Unntak (låst 2026-07-23)

*Dette var markert som en åpen beslutning som trengte flere iterasjoner. Den er nå låst som en formell modelleringsøvelse — mot fire konkrete alkoholloven-eksempler — uavhengig av at ingen kode er skrevet ennå. Se `docs/00-endringslogg-v0.2.md` for hva som endret seg fra forrige utkast. §5.7 lister de gjenværende spørsmålene som fortsatt er implementasjonsdetaljer, ikke ontologi.*

### 5.1 Problemet (uendret fra forrige utkast)

Kravspesifikasjonens opprinnelige `Vilkår`-entitet (kap. 3.8) var strukturelt én **regeltre-node**: den hadde `barn[]`, `barn_operator`, `input_datasett[]` og `utdata_parameter` samtidig. Det var en sammensmelting av et **Vilkår** i referansemodellens forstand (bladnode: én testbar betingelse), en **Regel**/beslutningsregel (komposisjonsnode) og delvis en **Rettsfølge** (via `utdata_parameter`). `digital-rettsstat/docs/06-regellaget.md` navngir nettopp denne sammensmeltingen som hovedårsaken til oversettelsesgapet mellom jurist, fagekspert og utvikler: *"Navngi de tre, aldri bare 'regel'."*

### 5.2 Hvorfor det ikke bare kan rettes med et navnebytte (uendret)

`forklaringsmodell-api` løser dette annerledes: `Vilkar` er flat referansedata, `Regel` er det operasjonaliserte, versjonerte artefaktet (DMN/Python/LLM), og grafstrukturen — hvordan flere vilkår komponeres til ett resultat — ligger implisitt *inni* regelmotor-artefaktet, ikke modellert eksplisitt. Regel-IDEs grafeditor er derimot nettopp verktøyet for å *bygge* komposisjonsstrukturen visuelt før eksport, så komposisjonsleddet må modelleres eksplisitt her, uavhengig av at `forklaringsmodell-api` ikke trenger det på sin side.

### 5.3 De tre nodetypene

| Nodetype | Rolle | Er alltid bladnode? |
|---|---|---|
| **Vilkår** | Én atomær, testbar betingelse mot ett eller flere `input_datasett` | **Ja** |
| **Regel** | Komposisjonsnode: `barn[]` + `barn_operator` (OG/ELLER/IKKE) + `utdata` | Nei |
| **Unntak** | Begrenser én bestemt Regel; har selv én betingelse (Vilkår eller Regel) | Nei (har nøyaktig én betingelse) |

### 5.4 Kardinaliteter, invarianter og tillatte relasjoner

| Fra | Relasjon | Til | Kardinalitet |
|---|---|---|---|
| Regel | `barn` | Vilkår \| Regel | 1..N |
| Regel | `unntak` | Unntak | 0..N |
| Unntak | `gjelder_regel` | Regel | **1** (påkrevd) |
| Unntak | `betingelse` | Vilkår \| Regel | **1** (påkrevd) |
| Vilkår | `input` | Datasett | 1..N |
| Regel | `utdata` | Rettsfølge (§7, §15.1) | 1 |

**Invarianter:**
- **INV-1 — Vilkår er alltid bladnode.** `Vilkår.barn = ∅`. Et Vilkår kan ikke inneholde Regel eller et annet Vilkår.
- **INV-2 — Regel må komponere noe.** `Regel.barn.length ≥ 1`; hvert element er enten Vilkår eller Regel (rekursivt).
- **INV-3 — Unntak må referere nøyaktig én Regel.** Et unntak begrenser en regel (jf. §8), ikke et enkeltvilkår direkte — `Unntak.gjelder_regel` kan derfor ikke peke på en Vilkår-node.
- **INV-4 — Unntak har alltid én betingelse.** `Unntak.betingelse` er påkrevd og følger samme rekursjon som `Regel.barn` (Vilkår eller Regel) — dette er selve "med mindre …"-testen.
- **INV-5 — Rotnoden i et vilkårstre er alltid en Regel**, aldri et bare Vilkår, siden rotnoden representerer selve beslutningen (produktkrav kap. 3.4: "Vedtak om skjenkebevilling").
- **INV-6 — `barn_operator` finnes kun på Regel.** Vilkår er blad og har ingen operator. Unntak har ingen `barn_operator` — dets forhold til `gjelder_regel` er implisitt IKKE: *"Regel X gjelder, med mindre Unntak.betingelse er oppfylt."*
- **INV-7 — DAG bevares på tvers av begge kanttyper.** Samme DAG-krav som før (`03-domenemodell.md` §1.10/§1.12) gjelder for `barn`- **og** `unntak`/`betingelse`-kantene samlet — et Unntak kan ikke (via sin betingelse) skape en sykel tilbake til en node som selv er forelder til Regelen unntaket gjelder.

### 5.5 Testet mot alkoholloven-testcaset

```
Regel  R-ROOT  "Vedtak om skjenkebevilling"   barn_operator = OG
 ├─ Vilkår  V-ALDER       vilkarstype=materiell, vurderingstype=regelbasert
 │                        input: styrer.fodselsdato · parameter: minimumsalder=20
 ├─ Vilkår  V-VANDEL      vilkarstype=materiell, vurderingstype=skjonnsbasert
 │                        skjonnsgrunnlag: Begrep "uklanderlig vandel" (§ 1-7b)
 │                        skjonnsmomenter: [tidligere bevillingsbrudd, økonomisk vandel, straffbare forhold]
 └─ Regel   R-SKJENKETID  barn_operator = OG
     ├─ Vilkår  V-STED
     ├─ Vilkår  V-KLOKKESLETT
     └─ unntak: Unntak  U-LUKKET-SELSKAP
                  gjelder_regel: R-SKJENKETID
                  betingelse: Vilkår V-ER-LUKKET-SELSKAP
```

Dette holder alle sju invariantene: V-ALDER og V-VANDEL har ingen barn (INV-1); R-ROOT og R-SKJENKETID har ≥1 barn (INV-2); U-LUKKET-SELSKAP peker på en Regel, ikke et Vilkår (INV-3); U-LUKKET-SELSKAP har nøyaktig én betingelse (INV-4); R-ROOT er rot (INV-5); kun R-ROOT og R-SKJENKETID har `barn_operator` (INV-6); ingen sykel oppstår (INV-7). Det viser også at `avklaringsbehov` (§6.1) blir en egen, synlig egenskap på V-VANDEL alene — ikke noe som "smitter" over på hele R-ROOT-treet.

### 5.6 Navnevalg: hvorfor Regel-IDEs "Regel" ikke heter det samme som `forklaringsmodell-api`s "Regel"

For å unngå at to ulike ting begge kalles "Regel" på tvers av repoene (jf. §1s poeng om at "regel betyr tre ulike ting"), kaller API-kontraktene (`04-api-kontrakter.md` §7) Regel-IDEs komposisjonsnode for **regelnode**, ikke "regel". `forklaringsmodell-api`s `Regel` er noe annet: det ferdig eksporterte, operasjonaliserte artefaktet (DMN-XML e.l.) — se `05-arkitektur-og-nfk.md` §1 og `07-forklaringsmodell-api-avvik.md` for hvordan én regelnode (eller et helt regelnode-tre) blir til én `forklaringsmodell-api`-`Regel`-rad ved eksport.

### 5.7 Hva som fortsatt er implementasjonsdetaljer (ikke ontologi)

- Om regelnoder eksporteres én-til-én til separate regelartefakter (én DMN-node per regelnode) eller om hele (del-)treet kompileres til ett samlet DMN/eFLINT-dokument. Avklares før eksportmotoren (kap. 4.14) bygges, se `06-veikart.md` byggesteg 4/6.
- Migrering av data fra det aller første kravspesifikasjonsutkastet (som kun hadde én `Vilkår`-type) er ikke aktuelt lenger — det var aldri implementert.
