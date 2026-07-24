# Domenemodell — entiteter, relasjoner, RBAC, livssykluser, publisering

*Begrepene under er definert presist i [`01-referansemodell.md`](01-referansemodell.md); dette dokumentet er feltnivå-skjemaet. Skjermene som bruker disse entitetene er beskrevet i [`02-produktkrav.md`](02-produktkrav.md).*

## 0. Felles basemetadata

Alle entiteter i §1 arver følgende felt (samler den eksterne vurderingens punkt om "felles metadata i en felles basemodell"). De er ikke gjentatt per entitet under.

| Felt | Type | Beskrivelse |
|---|---|---|
| `id` | string | Stabil, unik identifikator, uforandret på tvers av versjoner |
| `versjon` | int | Se §3.10 i tidligere utkast — heltall, økende |
| `entitetsstatus` | enum | `gjeldende` / `erstattet` / `arkivert` |
| `erstatter` / `erstattes_av` | ref, nullable | Peker til forrige/neste versjon |
| `gyldig_fra` / `gyldig_til` | date, nullable | |
| `opprettet_av`, `opprettet_tidspunkt` | string, datetime | |
| `sist_endret_av`, `sist_endret_tidspunkt` | string, datetime | |

Proveniens (endringslogg) er en separat, append-only tabell — se §3.11 under.

### 0.1 Virksomhetstilhørighet (`virksomhet_id`) — lagt til i v0.3

*Se `00-endringslogg-v0.3.md` for bakgrunnen (opptil ~1000 offentlige virksomheter deler én driftsatt løsning). Ikke en del av basemetadataen over — behandlingen varierer bevisst per entitetstype:*

- **Alltid satt (påkrevd):** Begrep, Vilkår/Regel/Unntak, Tjeneste, Kodeliste (juridisk/teknisk — ikke ekstern-referanse), Testcase, Tekst-tag (§1.2). Dette er virksomhetens egne arbeidsprodukter, aldri delt — to virksomheter kan tagge samme delte rettskilde-node helt ulikt.
- **Nullable, med betydning (delt/nasjonalt vs. virksomhetseid):** Rettskilde (§1.1) — `NULL` for Lov/Forskrift importert fra Lovdata (delt av alle virksomheter, importert/vedlikeholdt én gang), satt for virksomhetens egne lokale rettskilder (lokal forskrift, virksomhetsdokument).
- **Nullable, følger den underliggende hendelsen:** Proveniens (§1.14) — arver samme delt/virksomhetseid-status som entiteten hendelsen gjelder.
- **Ikke egen kolonne (arves via forelder):** Rettskildenoder og -referanser arver virksomhetstilhørighet fra sin Rettskilde via foreign key, i stedet for å duplisere feltet på hver node.

RBAC (§2) og alle spørringer i praksis skal derfor alltid filtrere på "brukerens virksomhet ELLER delt" for Rettskilde, og strengt på "brukerens virksomhet" for alt annet i denne lista.

## 1. Entiteter og relasjoner

### 1.1 Rettskilde
| Felt | Type | Beskrivelse |
|---|---|---|
| `virksomhet_id` | ref, nullable | **Nytt i v0.3, se §0.1.** `NULL` = delt/nasjonal (Lov/Forskrift); satt = virksomhetens egen lokale kilde |
| `doctype` | enum | `act` (lov/forskrift), `doc` (rundskriv), `judgment` (presedens), `internal` (virksomhetsdokument) |
| `kildetype` | enum | Lov, Forskrift, Rundskriv, Presedens, Virksomhetsdokument |
| `tittel`, `kortnavn` | string | |
| `eli` / `identifikator` | string | F.eks. `LOV-1989-06-02-27` |
| `aknXml` | text | Kanonisk AKN-representasjon |
| `ikrafttredelse`, `konsolidertDato` | date | |
| `utgiver` | string | F.eks. Lovdata |
| `status` | enum | Gjeldende / Opphevet / Utkast |
| `noder[]` | tre | Kapittel/paragraf/ledd/bokstav med `eId` |

Adresserbare enheter har `eId`. **`par_1-7b`-formen her var en antatt konvensjon før vi hadde ekte Lovdata-data** — den faktiske foreslåtte konvensjonen (gjenbruk av Lovdatas egne `id`-verdier, f.eks. `kapittel-1-paragraf-3`) står i `08-byggesteg1-teknisk-design.md` §1.2, til ekstern kvalitetssikring. Presedens modelleres som AKN `judgment` (§1.7).

### 1.2 Tekst-tag
Kobler en tekstflate i en rettskilde til en modell-entitet, lagret som `<term>` i AKN.

| Felt | Type | Beskrivelse |
|---|---|---|
| `virksomhet_id` | ref | **Nytt i v0.3, se §0.1.** Påkrevd, ikke nullable — en tagg er alltid virksomhetens eget arbeidsprodukt, selv når den peker på en delt/nasjonal rettskilde-node |
| `kildeId`, `eId` | string | Hvilken bestemmelse |
| `start`, `end` | int | Tegn-offset i normalisert tekst (posisjonsbasert — tillater overlappende tagger) |
| `quoteSelector` | string | **Nytt.** Sitatet selv (før/etter-kontekst + eksakt tekst), etter W3C Web Annotation-mønster. Se `05-arkitektur-og-nfk.md` §3 for hvorfor ren offset ikke er robust nok ved konsoliderte lovendringer og korrektur |
| `kind` | enum | `begrep` / `vilkar` / `regel` |
| `ref` | ref | ID til begrep/vilkår/regel (ny eller eksisterende) |

### 1.3 Begrep (SKOS)
| Felt | Type | Beskrivelse |
|---|---|---|
| `term` (`skos:prefLabel`) | string | |
| `definisjon` (`skos:definition`) | text | Egenformulert, kort |
| `lovreferanse` (`dct:source`) | eId | |
| `gjelder_for` | string[] | Roller/tjenester |
| `kodeliste_referanse` | ref, nullable | Peker til verdiområde (§1.4) |
| `skosUrl` | url | Publisert URI i Felles datakatalog (data.norge.no) |
| `skosType` | const | `skos:Concept` |
| `begrepstype` | enum | **Nytt.** `faktabegrep` / `handlingsbegrep`, jf. Schartum (2025) 7.3.3–7.3.4. Faktabegreper er relativt statiske (aktør/beslutningsgrunnlag/resultat); handlingsbegreper angir hva som skal gjøres (vilkårsprøving, beregning, informasjonsutveksling, sikkerhet) |

Samme begrep kan refereres fra flere vilkårsnoder uten duplisering. Ett begrepsuttrykk skal ha ett begrepsinnhold — unngå synonymer (Schartum 2025, 7.3.1).

### 1.4 Kodeliste / verdidomene
| Felt | Type | Beskrivelse |
|---|---|---|
| `type` | enum | `juridisk` / `teknisk` / `ekstern-referanse` |
| `juridisk_grunnlag` | eId | Kun `juridisk` |
| `ekstern_kilde` | uri + versjon | Kun `ekstern-referanse` |
| `koder[]` | liste | `{kode, term, definisjon, gyldig_fra, gyldig_til, erstattes_av}` |

**Tre typer verdidomener:**
- **Juridisk forankret** — eies/valideres av jurist (f.eks. `KL-VANDELSOMRADE`, `KL-RETTSKILDEVEKT`).
- **Teknisk/operasjonell** — eies av systemforvalter (f.eks. `KL-VILKARSUTFALL`, `KL-SAKSSTATUS`).
- **Ekstern autoritativ** — refereres, dupliseres ikke (f.eks. kommunenummer/SSB, organisasjonsnummer, Digdirs felles kodelister).

`KL-VILKARSUTFALL` (teknisk) har seks verdier: `oppfylt`, `ikke_oppfylt`, `krever_skjonn`, `krever_dokumentasjon`, `ukjent`, `ma_vurderes_av_jurist`.

### 1.5 Tjeneste (CPSV-AP-NO)
Felt: tittel, beskrivelse, kompetent myndighet, output, tjenestetype, målgruppe, kanaler, kostnad/gebyr, behandlingstid, språk, kontaktpunkt, konsekvens ved brudd, regelverksreferanser (`eId`-lenker), vilkår (ref til §1.8), **hendelser[]**, tjenesteavhengigheter[], status, versjon.

**Hendelse:** `{navn, type: Forretningshendelse/Innrapportering/Hendelse, trigger}`. Eksempler: søknad om bevilling, søknad om fornyelse, endringsmelding, årlig omsetningsoppgave, kontroll/tilsyn, vedtak om inndragning.

**Tjenesteavhengighet:** `{rel: før/avhengig av/input til, navn, kilde: intern tjeneste eller "eksternt oppslag · data.norge.no"}`. Dette er én relasjonstype i kunnskapsgrafen (kap. 3.13 i produktkrav), ikke en separat graf.

### 1.6 Datasett / datapunkt
Felt: `felt`/`prop` (visningsnavn/maskinnavn, f.eks. `styrer.fodselsdato`), `dtype` (string/integer/boolean/date/object), `type` (oppslagbart/brukeroppgitt/utledet), `kilde`, `vilkar` (kobling), `kodeliste` (der relevant), `grunnlag` (rettslig grunnlag for behandling/oppslag), `lagring` (lagringstid), `mottakere`, `bruk`.

### 1.7 Presedens (AKN `judgment`)
Felt: `saksnummer`, `dato`, `organ` (klagenemnd/statsforvalter/domstol), `utfall` (medhold/avslag/delvis medhold), `tilknyttede_bestemmelser[]` (`eId`), `relevans_for_vilkar[]`, `rettskildevekt` (fra `KL-RETTSKILDEVEKT` — aldri fritekst), `sammendrag`.

Presedens kan foreslå/begrunne tolkning av et vilkår, men blir **aldri automatisk bindende regelendring** — jurist/fagansvarlig avgjør.

### 1.8 Vilkår (bladnode — ontologi låst, se `01-referansemodell.md` §5)

| Felt | Beskrivelse |
|---|---|
| `tittel`, `beskrivelse` | |
| `generisk_mal` | F.eks. `GM-VANDEL-PERSON` (to-lags modell, produktkrav kap. 4.1) |
| `vilkarstype` | `formell` / `materiell`, jf. `01-referansemodell.md` §6 |
| `gjelder_rolle` | F.eks. bevillingshaver, styrer/stedfortreder |
| `juridisk_grunnlag[]` | `{kilde, eId}` |
| `begrep` | Referanse til begrep |
| `vurderingstype` | `regelbasert` / `skjonnsbasert` / `hybrid` |
| `parametre` | F.eks. `minimumsalder=20`, `kodeliste`-referanse |
| `input[]` | Datapunkter brukt som input (§1.6) — 1..N, jf. INV på referansemodellens §5.4 |
| `status` | Se livssyklus, §3 under |

**Invariant (INV-1, `01-referansemodell.md` §5.4):** et Vilkår har aldri `barn` — det er alltid en bladnode. Har en node barn, er den en Regel (§1.9), ikke et Vilkår.

**Skjønnsfelt** (kun når `vurderingstype ∈ {skjonnsbasert, hybrid}`): `skjonnsgrunnlag` (ref. til Begrep, kardinalitet 1, `01-referansemodell.md` §6.1), `skjonnsmomenter[]` (1..N, hver `{navn, beskrivelse, presedensreferanse?}`), `krever_dokumentasjon`, `eskaleringsrolle` (typisk jurist) — de to siste er avklaringsbehovets egne data.

**Tekster (per vilkår):** veiledningstekst til bruker, veiledning til saksbehandler, referanser til lovverk, gyldighetsperiode. (Innvilgelses-/avslagstekst flyttet til Regel, §1.9, siden det er komposisjonsnoden som produserer et resultat — se §1.16.)

### 1.9 Regel (komposisjonsnode)

| Felt | Beskrivelse |
|---|---|
| `tittel`, `beskrivelse` | |
| `barn[]` | Vilkår- **eller** Regel-noder (rekursivt) — **1..N** (INV-2), skal danne en DAG, se §1.12 |
| `barn_operator` | `OG` / `ELLER` / `IKKE` (kun definert på Regel — INV-6; se `01-referansemodell.md` §3 for hvorfor ikke XOR/NAND) |
| `unntak[]` | 0..N referanser til Unntak-noder (§1.10) hvis `gjelder_regel` peker hit |
| `utdata` | `{navn, type}` — rettsfølgen/resultatet noden produserer, jf. `01-referansemodell.md` §15.1 |
| `er_rotnode` | boolsk — kun rotnoden i et vilkårstre kjennetegnes slik (INV-5); rotnodens `utdata` er selve vedtaksforslaget |
| `juridisk_grunnlag[]`, `generisk_mal`, `status` | Som for Vilkår |

**Tekster (per regelnode):** innvilgelsestekst, avslagstekst (kun meningsfullt på en Regel, siden bare en komposisjonsnode med `utdata` kan innvilges/avslås — et enkeltvilkår er verken innvilget eller avslått, bare oppfylt/ikke oppfylt).

### 1.10 Unntak

| Felt | Beskrivelse |
|---|---|
| `tittel`, `beskrivelse` | |
| `gjelder_regel` | Ref. til Regel — **kardinalitet 1, påkrevd** (INV-3). Kan ikke peke på et Vilkår. |
| `betingelse` | Vilkår- **eller** Regel-node — **kardinalitet 1, påkrevd** (INV-4). Selve "med mindre …"-testen. |
| `juridisk_grunnlag[]`, `status` | Som for Vilkår |

Et Unntak har ingen `barn_operator` (INV-6) — forholdet til `gjelder_regel` er implisitt IKKE. Se `01-referansemodell.md` §5.5 for et fullt eksempel (unntak fra skjenketid ved lukket selskap) testet mot alle sju invariantene.

### 1.11 Generisk vilkårsmal
`{beskrivelse, vurderingstype, brukt_i_antall_tjenester}`. Instansieres flere ganger med ulikt omfang/lovreferanse. Gjelder Vilkår-, Regel- og Unntak-noder likt.

### 1.12 DAG-krav for vilkårs-/regeltreet

Vilkårs-/regelgrafen (produktkrav kap. 3.4) **skal** være en rettet asyklisk graf (DAG):

- Enhver node skal ikke kunne nå seg selv via en kjede av `barn`- **eller** `unntak`/`betingelse`-relasjoner (INV-7, `01-referansemodell.md` §5.4).
- Systemet skal validere dette ved lagring (ikke bare i UI) — se AK-3.4.6 i produktkrav.
- Årsak: sykler gir udefinert oppførsel ved evaluering, eksport (DMN/eFLINT krever DAG), testkjøring og påvirkningsanalyse (kunnskapsgraf, kap. 3.13).
- Datasett-noder (§1.6) er alltid blad-input, aldri mål for en kant fra en Vilkår-node — informasjonsflyten er strengt input → vilkår → regel → vedtak.

### 1.13 Testcase
Felt: `tilknyttet_node[]` (Vilkår-, Regel- eller Unntak-referanser), `input` (eksempeldata), `forventet_resultat` (verdi fra `KL-VILKARSUTFALL` for Vilkår-noder, eller `utdata`-verdi for Regel-noder), `forventet_forklaring` (kort forventet begrunnelsestekst), `juridisk_grunnlag`.

### 1.14 Proveniens / endringslogg (append-only, atskilt fra versjonering)
Versjonering (§0) svarer «hvilken versjon gjelder»; proveniens svarer «hvordan ble denne versjonen til».

| Felt | Beskrivelse |
|---|---|
| `virksomhet_id` | **Nytt i v0.3, se §0.1.** Nullable — arver delt/virksomhetseid-status fra entiteten hendelsen gjelder |
| `endret_av` | Person/rolle |
| `dato` | |
| `handling` | opprettet / endret / foreslått_av_ai / validert / publisert / arkivert |
| `kilde_referanser[]` | `eId`, rundskriv-avsnitt, presedens-noder |
| `ai_forslag_versjon` | nullable |
| `godkjent_av` | nullable — jurist/fagansvarlig |

### 1.15 Testcase-simulert Vedtak, Vedtaksgrunnlag og Vedtaksvirkning

*Regel-IDE eier ikke løpende vedtak — det gjør `forklaringsmodell-api`. Feltene under er hva regel-IDEs testmodul (produktkrav kap. 3.15) og eksportvisning trenger for å simulere/forhåndsvise et Vedtak uten en reell sak, jf. `01-referansemodell.md` §15.1.*

| Entitet | Felt | Beskrivelse |
|---|---|---|
| Vedtak (simulert) | `rotnode_id`, `input_testcase_id`, `resultat` | Rot-Regelnodens `utdata`-verdi for en gitt testcase |
| Vedtaksgrunnlag (simulert) | `vilkarsvurderinger[]`, `rettskilde_referanser[]`, `presedens_referanser[]` | Speiler forklaringsloggens innhold (produktkrav kap. 3.12) |
| Vedtaksvirkning (simulert) | `type`, `beskrivelse`, `gyldig_fra`/`gyldig_til` | Én instans av rotnodens `utdata` — feltnavnene følger `forklaringsmodell-api`s `Vedtaksvirkning` bevisst, for å unngå enda et tredje navnesett, se `07-forklaringsmodell-api-avvik.md` |

### 1.16 ER-diagram (relasjoner mellom hovedentitetene)

```mermaid
erDiagram
  RETTSKILDE ||--o{ TEKST_TAG : "har tagger"
  TEKST_TAG }o--|| BEGREP : "refererer (kind=begrep)"
  TEKST_TAG }o--|| VILKAR : "refererer (kind=vilkar)"
  BEGREP }o--o| KODELISTE : "verdiområde"
  VILKAR }o--o{ RETTSKILDE : "juridisk_grunnlag"
  VILKAR }o--o| BEGREP : "begrep (skjonnsgrunnlag)"
  VILKAR }o--o{ DATASETT : "input"
  REGEL ||--o{ VILKAR : "barn"
  REGEL ||--o{ REGEL : "barn (DAG)"
  REGEL ||--o{ UNNTAK : "unntak"
  UNNTAK }o--|| REGEL : "gjelder_regel"
  UNNTAK }o--o| VILKAR : "betingelse"
  UNNTAK }o--o| REGEL : "betingelse"
  VILKAR }o--o| GENERISK_MAL : "instans av"
  TJENESTE }o--o{ REGEL : "vilkår (rotnode)"
  TJENESTE }o--o{ RETTSKILDE : "regelverksreferanser"
  TJENESTE ||--o{ HENDELSE : "har"
  PRESEDENS }o--o{ RETTSKILDE : "tilknyttede_bestemmelser"
  PRESEDENS }o--o{ VILKAR : "relevans_for_vilkar"
  TESTCASE }o--o{ VILKAR : "tilknyttet_node"
  TESTCASE }o--o{ REGEL : "tilknyttet_node"
  REGEL ||--o| VEDTAK_SIMULERT : "utdata → resultat"
  VILKAR ||--o{ PROVENIENS : "endringslogg"
  REGEL ||--o{ PROVENIENS : "endringslogg"
  RETTSKILDE ||--o{ PROVENIENS : "endringslogg"
```

---

## 2. Rolle- og autorisasjonsmodell (RBAC)

*"Vilkår" i tabellen under dekker alle tre nodetyper fra `01-referansemodell.md` §5 (Vilkår, Regel, Unntak) — de deler RBAC-regler, ikke bare felt.*

**Lagt til i v0.3 (se `00-endringslogg-v0.3.md`):** rollene under gjelder alltid **innenfor brukerens egen virksomhet** — en Jurist i virksomhet A kan aldri validere/publisere/endre virksomhet B sine entiteter, uansett rolle. Delte/nasjonale rettskilder (§0.1) er unntaket: import/vedlikehold der er ikke virksomhetsbundet på samme måte, men følger fortsatt rollene i tabellen. Nøyaktig håndheving (tilgangskontroll i API-et, avledet fra Ansattporten-innlogging) er **ikke bygget/spesifisert i detalj ennå** — se `00-endringslogg-v0.3.md`, "Ikke bekreftet ennå".

| Handling | Fagansvarlig | Jurist | Systemforvalter | Saksbehandler | AI-assistent |
|---|---|---|---|---|---|
| Opprette/endre rettskilde (import) | ✓ | ✓ | | | |
| Opprette/endre begrep | ✓ | ✓ | | | |
| Opprette vilkår/regelnode/unntak (utkast) | ✓ | ✓ | | | Foreslå (status `foreslått av AI`) |
| Endre vilkår/regelnode/unntak | ✓ | ✓ | | | |
| Validere vilkår/regelnode/unntak/AI-forslag | | ✓ | | | |
| Publisere vilkår/regelnode/unntak/tjeneste | | ✓ | | | |
| Endre juridiske kodelister | | ✓ | | | |
| Endre tekniske kodelister | | | ✓ | | |
| Referere ekstern autoritativ kodeliste | ✓ | ✓ | ✓ | | |
| Opprette presedens | ✓ | ✓ | | | |
| Kjøre påvirkningsanalyse | ✓ | ✓ | ✓ | | |
| Godkjenne testcase-kjøring før publisering | | ✓ | | | |
| Bruke saksbehandlingsverktøy | | | | ✓ | |
| Overstyre saksutfall (med begrunnelse) | | | | ✓ | |
| Bytte rolle (rollevelger) | ✓ | ✓ | ✓ | ✓ | — |

Prinsipp: **AI-assistenten har ingen rad med ✓ utenom "foreslå"** — den kan aldri validere, publisere eller overstyre, jf. `digital-rettsstat` prinsipp 4 og AK-3.10.1 i produktkrav.

---

## 3. Livssykluser

### 3.1 Vilkår / Regel / Unntak

*Samme livssyklus for alle tre nodetyper (`01-referansemodell.md` §5). Et Unntak kan kun nå `publisert` samtidig med eller etter at sin `gjelder_regel` er publisert — se publiseringsmodellen §4.*

```mermaid
stateDiagram-v2
  [*] --> utkast
  utkast --> foreslatt_av_ai: AI foreslår
  foreslatt_av_ai --> utkast: avvist av jurist/fagansvarlig
  utkast --> under_revisjon: endring startet
  foreslatt_av_ai --> under_revisjon: redigert før godkjenning
  under_revisjon --> validert: jurist validerer
  validert --> publisert: publisering (§4)
  publisert --> under_revisjon: ny endring (oppretter ny versjon, §0)
  publisert --> tilbaketrukket: jurist trekker tilbake
  tilbaketrukket --> arkivert
  publisert --> arkivert: erstattet av ny versjon
```

Samme mønster (utkast → under revisjon → validert → publisert → tilbaketrukket/arkivert) gjelder for **rettskilder**, **tjenester** og **kodelister**, med ett unntak: eksterne autoritative kodelister (§1.4) har ikke `publisert`-steget — de er alltid `gjeldende` så lenge kilden de refererer til er det.

### 3.2 Rettskilde

```mermaid
stateDiagram-v2
  [*] --> utkast: importert/lastet opp
  utkast --> gjeldende: metadata bekreftet
  gjeldende --> opphevet: erstattet av ny lov/forskrift
  opphevet --> arkivert
```

---

## 4. Publiseringsmodell

Svarer på den eksterne vurderingens spørsmål: hva publiseres, er det atomisk, kan det rulles tilbake?

- **Publiseringsenhet er ett vilkår/én regelnode**, ikke hele tjenesten og ikke hele regelsettet. Å publisere en tjeneste betyr at *alle* dens vilkårsnoder med status `validert` blir `publisert` i samme transaksjon — men et enkeltvilkår kan publiseres uavhengig av søsken-nodene sine, så lenge grafen fortsatt er en gyldig DAG uten referanser til upubliserte noder fra publiserte noder.
- **Publisering er atomisk per enhet**: et vilkår går fra `validert` til `publisert` i én transaksjon som (a) låser feltverdiene for den versjonen, (b) skriver en proveniens-rad, (c) trigger domenehendelsen `RulePublished` (§5), og (d) kjører berørte testcaser (AK-3.15.1) — publisering feiler og rulles tilbake som helhet hvis testcaser ikke er godkjent.
- **En publisert node skal aldri redigeres i-place.** En endring etter publisering oppretter en ny versjon (`erstatter`-kjeden i §0); den gamle versjonen forblir lesbar og er hva historiske vedtak fortsatt peker på (jf. `forklaringsmodell-api`s append-only-prinsipp).
- **Rollback** = trekke tilbake (`tilbaketrukket`), ikke slette. En tilbaketrukket node kan ikke lenger brukes i nye evalueringer, men forblir i historikken for spor tilbake fra eksisterende vedtak.
- **En regelnode kan ikke publiseres før alle dens `barn[]` enten er `publisert` eller eksplisitt markert som ikke påkrevd** (f.eks. en alternativ/ELLER-gren som ikke er ferdigstilt ennå skal blokkere publisering av foreldrenoden, ikke stille inn).
- **Et Unntak kan ikke publiseres før sin `gjelder_regel` er publisert** (kan skje i samme publiseringstransaksjon, men aldri før). Omvendt: å publisere en Regel som har ett eller flere `utkast`/`under_revisjon`-Unntak knyttet til seg, skal ikke blokkeres av det — et upublisert unntak betyr bare at unntaket ennå ikke har virkning, ikke at hovedregelen ikke kan tas i bruk.
- **For Begrep og Tjeneste spesifikt:** "publisert" betyr at entiteten inngår i regel-IDEs egne, offentlig eksponerte RDF-endepunkter (`04-api-kontrakter.md` §13) — **ikke** at noe sendes til data.norge.no. Data.norge.no høster (crawler) disse endepunktene selv, typisk én gang i døgnet, etter at organisasjonen har registrert dem én gang. Se `05-arkitektur-og-nfk.md` §1.2 for hele resonnementet — dette skiller seg fra publisering av Vilkår/Regel/Unntak, som ikke har noe eksternt harvest-mottak.

---

## 5. Hendelsesmodell (domenehendelser)

| Hendelse | Utløses av | Nyttelast (minimum) |
|---|---|---|
| `SourceImported` | Ny rettskilde lagt til biblioteket (AK-3.3.5–3.3.7) | `rettskildeId`, `eli`, `importmetode` |
| `ConceptChanged` | Begrep opprettet/endret/publisert | `begrepId`, `endringstype`, `nyVersjon` |
| `AIProposalApproved` | AI-forslag godkjent (AK-3.10.2) | `forslagId`, `vilkarId`, `godkjentAv` |
| `RulePublished` | Vilkår/regel publisert (§4) | `vilkarId`, `versjon`, `publisertAv`, `tidspunkt` |
| `RuleArchived` | Vilkår/regel arkivert eller tilbaketrukket | `vilkarId`, `versjon`, `arsak` |
| `SourceAmended` | En publisert rettskildes `eId` får ny versjon | `rettskildeId`, `eId`, `gammelVersjon`, `nyVersjon` — **utløser lovspeil-varsel** til alle vilkår som refererer `eId` (se `07-forklaringsmodell-api-avvik.md`) |

Disse hendelsene er input til kunnskapsgrafens påvirkningsanalyse (produktkrav kap. 3.13) og til dashbordets aktivitetsgraf (produktkrav kap. 3.1). De skal logges append-only, samme mønster som proveniensen i §1.12.
