# Funksjonell kravspesifikasjon — Forvaltningsverktøy for digitale tjenester («Regel-IDE»)

**Versjon:** 1.0 · **Dato:** 2026-07-23
**Kilde:** Utledet fra interaktiv prototype (`Regel-IDE.dc.html`) + spesifikasjon v3.
**Testcase:** Alminnelig skjenkebevilling (alkoholloven).
**Formål med dokumentet:** Implementeringsklart underlag for Claude Code. Beskriver datamodell, skjermer, funksjonelle regler, akseptkriterier, roller og ikke-funksjonelle krav.

> **Merk om prototypen:** Prototypen er bygget som én Design Component med statiske data og simulerte interaksjoner. Denne spesifikasjonen beskriver produktet som skal bygges — den løfter prototypens skjermer og interaksjoner til reelle krav med persistens, API-er og validering. Der prototypen kun er skisse (f.eks. faktisk Lovdata→AKN-konvertering, diff-algoritme, testkjøringsmiljø) er kravene formulert som grensesnitt, ikke som ferdig logikk.

---

## 1. Konsept og rammer

### 1.1 To metaforer (begge gjelder)
- **Kompileringsplattform for digital forvaltning** — beskriver arkitekturen: rettskilder → semantisk modell → begreper → regler → metadata → kjørbar kode → saksbehandling → forklaring → vedtak. Brukes i teknisk/arkitektonisk kommunikasjon.
- **IDE for juridiske regler** — beskriver brukeropplevelsen: navigator, editor, referanser, validering, AI-assistent, eksport, historikk, publisering. Brukes i UI og mot jurister/fagansvarlige/beslutningstakere.

### 1.2 Faseinndeling (leveranserekkefølge)
| Fase | Innhold | Begrunnelse |
|---|---|---|
| **Fase 1 — Plattformlag** | Rettskildebibliotek, Begrepsregister, Kodelister/verdiregister | Nyttig alene; fundament for alt annet. |
| **Fase 2 — Regelmodellering** | Vilkår og regler (grafeditor), Testmodul | Krever fase 1-innhold. |
| **Fase 3 — Anvendelse** | Eksportmotor, Saksbehandlingsgenerator, Informasjonsmodell, Kunnskapsgraf/påvirkningsanalyse | Gir mest verdi når fase 1/2 har reelt innhold. |

Implementasjonen skal ikke arkitektonisk sperre for at faser bygges inkrementelt. Alle entiteter (kap. 3) skal finnes som datamodell fra fase 1, selv om UI-et for dem kommer senere.

### 1.3 Anbefalt teknologi
- **Frontend:** React + TypeScript. Komponenter og tokens fra **Designsystemet** (`@digdir/designsystemet-react`, `@digdir/designsystemet-css`, `@digdir/designsystemet-theme`). Se kap. 9.
- **Rettskildeformat:** Akoma Ntoso (AKN) som kanonisk lagringsformat for lov/forskrift/rundskriv/presedens.
- **Regeleksport:** eFLINT, OpenFisca, DMN, RuleML — via mellomformat (kap. 7.4).
- **Begreper:** SKOS, publiseres til Felles datakatalog (data.norge.no).
- **Tjeneste:** CPSV-AP-NO for tjenestedefinisjon.

---

## 2. Overordnet navigasjon og skall

Applikasjonen er et enkeltbruker-arbeidsverktøy for **én virksomhet** (f.eks. en kommune) — ikke et flervirksomhets-fellesverktøy. Tjenester fra andre virksomheter refereres via eksternt oppslag (data.norge.no), ikke ved deling av redigeringsrettigheter.

**Skall:**
- **Venstre sidemeny** (mørk, IDE-følelse) med navigasjonsgrupper. Kollapser til skuff med hamburgermeny < 880px bredde.
- **Topplinje** per skjerm: brødsmulesti, tittel, undertittel og skjermspesifikke handlingsknapper (f.eks. «Ny kilde», «Eksporter»).
- **Rollevelger** nederst i sidemenyen (kap. 8) — bytter aktiv rolle og påvirker hvilke handlinger som er tilgjengelige.
- **Responsivt:** kolonnelayouter stables til én kolonne på smale skjermer.

**Navigasjonselementer (skjermer):**
1. Dashboard
2. Tjenester → Tjenestedefinisjon
3. Rettskilder (Rettskildebibliotek)
4. Vilkår og regler (grafeditor)
5. Datasett
6. Informasjonsmodell
7. Kodelister / verdiregister
8. Begreper (Begrepsregister)
9. Presedens (Presedensregister)
10. AI-forslag
11. Saksbehandling
12. Forklaringslogg
13. Kunnskapsgraf
14. Eksport

**AK-2.1** Gitt en bruker på en hvilken som helst skjerm, når hun velger et element i sidemenyen, så navigeres det til tilhørende skjerm og aktivt element markeres.
**AK-2.2** Gitt skjermbredde < 880px, når siden lastes, så er sidemenyen skjult bak en hamburgerknapp, og et klikk på knappen åpner den som overlay med bakteppe.

---

## 3. Datamodell

Alle sentrale entiteter har **versjonsidentitet** (kap. 3.10) og **proveniens** (kap. 3.11). ID-er er stabile på tvers av versjoner.

### 3.1 Rettskilde
| Felt | Type | Beskrivelse |
|---|---|---|
| `id` | string | Unik ID |
| `doctype` | enum | `act` (lov/forskrift), `doc` (rundskriv), `judgment` (presedens), `internal` (virksomhetsdokument) |
| `kildetype` | enum | Lov, Forskrift, Rundskriv, Presedens, Virksomhetsdokument |
| `tittel`, `kortnavn` | string | |
| `eli` / `identifikator` | string | F.eks. `LOV-1989-06-02-27` |
| `aknXml` | text | Kanonisk AKN-representasjon |
| `ikrafttredelse`, `konsolidertDato` | date | |
| `utgiver` | string | F.eks. Lovdata |
| `status` | enum | Gjeldende / Opphevet / Utkast |
| `noder[]` | tre | Kapittel/paragraf/ledd/bokstav med `eId` |

Adresserbare enheter har `eId` (f.eks. `par_1-7b`). Presedens modelleres som AKN `judgment` (kap. 3.7).

### 3.2 Tekst-tag (kobling fra rettskildetekst)
Tagger knytter en tekstflate i en rettskilde til en modell-entitet, lagret som `<term>` i AKN.
| Felt | Type | Beskrivelse |
|---|---|---|
| `id` | string | |
| `kildeId`, `eId` | string | Hvilken bestemmelse |
| `start`, `end` | int | Tegn-offset i normalisert tekst (posisjonsbasert — tillater overlappende tagger) |
| `kind` | enum | `begrep` / `vilkar` / `regel` |
| `ref` | string | ID til begrep/vilkår/regel (ny eller eksisterende) |

### 3.3 Begrep (SKOS)
| Felt | Type | Beskrivelse |
|---|---|---|
| `begrep_id` | string | F.eks. `uklanderlig-vandel` |
| `term` (`skos:prefLabel`) | string | |
| `definisjon` (`skos:definition`) | text | Egenformulert, kort |
| `lovreferanse` (`dct:source`) | eId | |
| `gjelder_for` | string[] | Roller/tjenester |
| `kodeliste_referanse` | string, nullable | Peker til verdiområde (kap. 3.4) |
| `skosUrl` | url | Publisert URI i Felles datakatalog (data.norge.no) |
| `skosType` | const | `skos:Concept` |

Samme begrep kan refereres fra flere vilkårsnoder uten duplisering. Begreper publiseres som SKOS.

### 3.4 Kodeliste / verdidomene
| Felt | Type | Beskrivelse |
|---|---|---|
| `kodeliste_id` | string | F.eks. `KL-VANDELSOMRADE-ALKOHOLLOV` |
| `type` | enum | `juridisk` / `teknisk` / `ekstern-referanse` |
| `juridisk_grunnlag` | eId | Kun `juridisk` |
| `ekstern_kilde` | uri + versjon | Kun `ekstern-referanse` |
| `koder[]` | liste | `{kode, term, definisjon, gyldig_fra, gyldig_til, erstattes_av}` |

**Tre typer verdidomener:**
- **Juridisk forankret** — eies/valideres av jurist (f.eks. `KL-VANDELSOMRADE`, `KL-RETTSKILDEVEKT`).
- **Teknisk/operasjonell** — eies av systemforvalter (f.eks. `KL-VILKARSUTFALL`, `KL-SAKSSTATUS`).
- **Ekstern autoritativ** — refereres, dupliseres ikke (f.eks. kommunenummer/SSB, organisasjonsnummer, Digdirs felles kodelister).

`KL-VILKARSUTFALL` (teknisk) har seks verdier: `oppfylt`, `ikke_oppfylt`, `krever_skjonn`, `krever_dokumentasjon`, `ukjent`, `ma_vurderes_av_jurist`.

### 3.5 Tjeneste (CPSV-AP-NO)
| Felt | Beskrivelse |
|---|---|
| Tittel, beskrivelse | |
| Kompetent myndighet | |
| Output (hva tjenesten gir) | |
| Tjenestetype, målgruppe | |
| Kanaler, kostnad/gebyr, behandlingstid, språk, kontaktpunkt | |
| Konsekvens ved brudd | |
| Regelverksreferanser | `eId`-lenker |
| Vilkår | referanse til modul 3 |
| **Hendelser[]** | Se 3.5.1 |
| Tjenesteavhengigheter[] | Se 3.5.2 |
| Status, versjon | |

**3.5.1 Hendelse**
| Felt | Beskrivelse |
|---|---|
| `navn` | F.eks. «Søknad om bevilling» |
| `type` | Forretningshendelse / Innrapportering / Hendelse |
| `trigger` | Hva/hvem som utløser (søker, kommune, periodisk) |

Eksempler: søknad om bevilling, søknad om fornyelse, endringsmelding, årlig omsetningsoppgave, kontroll/tilsyn, vedtak om inndragning.

**3.5.2 Tjenesteavhengighet**
| Felt | Beskrivelse |
|---|---|
| `rel` | `før` / `avhengig av` / `input til` |
| `navn` | Tjeneste/register |
| `kilde` | Intern tjeneste eller «eksternt oppslag · data.norge.no» |

Tjenesteavhengigheter er én relasjonstype i kunnskapsgrafen (kap. 6), ikke en separat graf.

### 3.6 Datasett / datapunkt
| Felt | Beskrivelse |
|---|---|
| `felt`, `prop` | Visningsnavn og maskinnavn (f.eks. `styrer.fodselsdato`) |
| `dtype` | string / integer / boolean / date / object |
| `type` | `oppslagbart` / `brukeroppgitt` / `utledet` |
| `kilde` | Register/API, søker, eller beregning |
| `vilkar` | Kobling til vilkår som bruker datapunktet |
| `kodeliste` | Kobling til verdidomene (kap. 3.4), der relevant |
| `grunnlag` | Rettslig grunnlag for behandling/oppslag |
| `lagring` | Lagringstid |
| `mottakere` | Hvem opplysningen deles med |
| `bruk` | Hvordan informasjonen behandles og brukes |

### 3.7 Presedens (AKN `judgment`)
| Felt | Beskrivelse |
|---|---|
| `id`, `saksnummer`, `dato` | |
| `organ` | Klagenemnd, statsforvalter, domstol |
| `utfall` | Medhold / avslag / delvis medhold |
| `tilknyttede_bestemmelser[]` | `eId`-referanser |
| `relevans_for_vilkar[]` | Kobling til vilkårsnoder |
| `rettskildevekt` | Verdi fra `KL-RETTSKILDEVEKT` (bindende/tungtveiende/veiledende/illustrerende/historisk) — **ikke fritekst** |
| `sammendrag` | Kort, egenformulert |

Presedens kan foreslå/begrunne tolkning av et vilkår, men blir **aldri automatisk bindende regelendring** — jurist/fagansvarlig avgjør.

### 3.8 Vilkår (regelnode)
| Felt | Beskrivelse |
|---|---|
| `vilkar_id` | F.eks. `V-101` |
| `tittel`, `beskrivelse` | |
| `generisk_mal` | F.eks. `GM-VANDEL-PERSON` (to-lags modell, kap. 7.1) |
| `gjelder_rolle` | F.eks. bevillingshaver, styrer/stedfortreder |
| `juridisk_grunnlag[]` | `{kilde, eId}` |
| `begrep` | Referanse til begrep |
| `vurderingstype` | `regelbasert` / `skjonnsbasert` / `hybrid` (kap. 7.5) |
| `parametre` | F.eks. `minimumsalder=20`, `kodeliste`-referanse |
| `barn[]` | Delvurderinger (hierarki) |
| `barn_operator` | Logisk operator mellom barn: `OG` / `ELLER` / `XOR` / `NAND` (kap. 7.6) |
| `input_datasett[]` | Datapunkter brukt som input (kap. 3.6) |
| `utdata_parameter` | `{navn, type}` — hva vilkåret produserer |
| `status` | `utkast` → `foreslått av AI` → `validert` → `publisert` |
| `versjon`, proveniens | Kap. 3.10–3.11 |

**3.8.1 Skjønnsfelt** (for `skjonnsbasert`/`hybrid`):
- `skjonnsmomenter[]` — momenter som skal vektes, hver med kort beskrivelse og ev. presedensreferanse.
- `krever_dokumentasjon` — hvilken dokumentasjon som må foreligge.
- `eskaleringsrolle` — hvem som gjør endelig vurdering (typisk jurist).

**3.8.2 Metadata / tekster (modul 4)** — per vilkår:
- Veiledningstekst til bruker
- Veiledning til saksbehandler
- Innvilgelsestekst
- Avslagstekst
- Referanser til lovverk
- Gyldighetsperiode (gyldig fra/til)

### 3.9 Generisk vilkårsmal
`{id, beskrivelse, vurderingstype, brukt_i_antall_tjenester}`. Instansieres flere ganger med ulikt omfang/lovreferanse.

### 3.10 Versjonsidentitet (alle sentrale entiteter)
| Felt | Beskrivelse |
|---|---|
| `versjon` | Heltall, økende |
| `entitetsstatus` | `gjeldende` / `erstattet` / `arkivert` |
| `erstatter` / `erstattes_av` | Peker til forrige/neste versjon |
| `gyldig_fra` / `gyldig_til` | |
| `brukt_i_saker` | Antall historiske vedtak som bygger på denne versjonen |

Historiske vedtak refererer alltid til den **eksakte** versjonen som gjaldt på avgjørelsestidspunktet. Versjonering handler om å reprodusere en historisk avgjørelse presist — ikke om å håndtere regelendring midt i en åpen sak (jf. kap. 11).

### 3.11 Proveniens / endringslogg (atskilt fra versjonering)
Versjonering svarer «hvilken versjon gjelder»; proveniens svarer «hvordan ble denne versjonen til». Per oppføring:
| Felt | Beskrivelse |
|---|---|
| `endret_av` | Person/rolle |
| `dato` | |
| `handling` | opprettet / endret / foreslått_av_ai / validert / publisert / arkivert |
| `kilde_referanser[]` | `eId`, rundskriv-avsnitt, presedens-noder |
| `ai_forslag_versjon` | nullable |
| `godkjent_av` | nullable — jurist/fagansvarlig |

### 3.12 Testcase
| Felt | Beskrivelse |
|---|---|
| `testcase_id` | |
| `tilknyttet_vilkar[]` | Hvilke vilkårsnoder testen dekker |
| `input` | Eksempeldata |
| `forventet_resultat` | Verdi fra `KL-VILKARSUTFALL` |
| `forventet_forklaring` | Kort forventet begrunnelsestekst |
| `juridisk_grunnlag` | Bestemmelse/presedens testen demonstrerer |

---

## 4. Skjermer — funksjonelle krav

Hver skjerm bruker Designsystemet-komponenter (kap. 9). Detaljpaneler bruker faner. Lister/tabeller har søk/filter der det er relevant.

### 4.1 Dashboard
KPI-kort (antall tjenester, vilkår, presedens, kodelister), tabell «Nylig oppdaterte tjenester» (klikkbar → tjenestedefinisjon), aktivitetsgraf (siste 30 dager: endringer i vilkår, AI-forslag), og «AI-forslag til gjennomgang»-kort med antall og lenke.

**AK-4.1.1** Gitt dashboardet, når en tjenesterad klikkes, så åpnes tjenestedefinisjonen for den tjenesten.

### 4.2 Tjenester / Tjenestedefinisjon (CPSV-AP-NO)
Liste med status/søk. Definisjonsskjerm viser: grunndata (kap. 3.5), regelverksreferanser (klikkbare `eId`), tjenesteavhengigheter (med «Søk i data.norge.no» for eksterne), og **Hendelser** (3.5.1). «Ny tjeneste»-handling.

**AK-4.2.1** Gitt tjenestedefinisjonen, så vises alle CPSV-AP-NO-grunndatafelt, hendelsesliste og avhengigheter.
**AK-4.2.2** Gitt en ekstern tjenesteavhengighet, så er den merket som eksternt oppslag mot data.norge.no.

### 4.3 Rettskildebibliotek
Tre-navigasjon (lov → kapittel → paragraf; forskrift; rundskriv; presedens; virksomhetens egne dokumenter) med søk. Detaljvisning med faner:
- **Tekst** — lovtekst med taggede ord/avsnitt (begrep/vilkår/regel), lagvis (overlappende tagger tillates), med filter «Vis tagger» (begrep/vilkår/regel) og liste over egne tagger med «Fjern».
- **Kilde (XML)** — underliggende AKN, taggede ord som `<term refersTo="#…">`.
- **Metadata** — kilde, `eId`/identifikator, ikrafttredelse, kildetype, versjon (også på kildenivå for topp-noder).
- **Koblinger** — begrep/vilkår denne bestemmelsen er koblet til.
- **Historikk** — proveniens.
Høyre kolonne: tilknyttede presedensavgjørelser med rettskildevekt.

**Tekstmerking → tagging (2-stegs meny):**
**AK-4.3.1** Gitt Tekst-fanen, når bruker markerer tekst og høyreklikker (eller slipper museknapp), så vises en meny: steg 1 velg type (begrep/vilkår/regel), steg 2 velg handling (ny / knytt til eksisterende), med «tilbake».
**AK-4.3.2** Når en tag opprettes, så markeres den valgte tekstflaten med fargekode (begrep=lilla, vilkår=gul, regel=grønn), lagres posisjonsbasert, og reflekteres i Koblinger-fanen og AKN-kilden.
**AK-4.3.3** Gitt markering av et helt avsnitt som overlapper et allerede tagget ord, så skjules ikke den innerste taggen — begge vises lagvis.
**AK-4.3.4** Gitt en egendefinert tag, når «Fjern» velges, så fjernes taggen og teksten går tilbake til forrige tilstand.

**Ny kilde / import:**
**AK-4.3.5** Gitt «Ny kilde» eller «Importer AKN», så åpnes en modal med to metoder: hent fra Lovdata (søk) eller last opp fil.
**AK-4.3.6** Gitt en importert kilde i annet XML-format (Lovdata-XML/NLM), så vises konvertering side-ved-side til AKN (kildeformat → normalisert AKN).
**AK-4.3.7** Gitt importmodalen, så kan bruker sette/redigere metadata (tittel, kortnavn, ELI-identifikator, kildetype, ikrafttredelse, konsolidert dato, utgiver) før «Legg til i biblioteket».

### 4.4 Vilkår og regler (grafeditor) — **kjernefunksjon**
To visninger via veksler: **Graf** (standard) og **Tre**.

**Grafmodell (DMN-inspirert DRD):**
- Noder: toppnode «Vedtak om skjenkebevilling» (beslutning), vilkårsnoder (med §-referanse og skjønn/regel-indikator), delvurderinger (barn), og inndata-noder (datasett).
- Kanter: informasjonsflyt nedenfra og opp (input → vilkår → vedtak), med pilhoder mot den noden som krever input.
- Klikk på node velger den og fyller egenskapspanelet. Valgt node og dens naboer utheves; øvrige dempes.
- Nummererte noder (rulemapping-stil).

**Tre-visning:** hierarkisk liste med ROT-operator, operator per foreldernode («barn: OG»), og generert regeluttrykk.

**Egenskapspanel (faner):**
- **Generelt** — ID, tittel, generisk mal, juridisk grunnlag, begrep, status, gyldig fra, beskrivelse, **input-datasett**, **utdata-parameter** (navn + type), og logisk operator mellom barn (OG/ELLER/XOR/NAND) med generert uttrykk.
- **Tekster** — veiledning til bruker/saksbehandler, innvilgelses-/avslagstekst (modul 4).
- **Standardref.** — referanser til rettskilde/begrep/mal/lovhenvisninger.
- **Output** — motoruavhengige målformat (eFLINT/DMN/OpenFisca/RuleML-RUML/XML) med av/på, og «Eksporter (N format)» → Eksport-skjermen.
- **Metadata** — versjon/status/gyldighet, brukt i N vedtak, lovreferanser.
- **Historikk** — proveniens med handling og kildereferanser.

**AK-4.4.1** Gitt grafvisningen, når en vilkårs- eller delvurderingsnode klikkes, så velges tilhørende vilkår og egenskapspanelet oppdateres.
**AK-4.4.2** Gitt et vilkår med barn, når operator endres (OG/ELLER/XOR/NAND), så oppdateres det genererte regeluttrykket tilsvarende. *(Prototypen viser valgt operator; produktet skal lagre og anvende endringen.)*
**AK-4.4.3** Gitt et vilkår, så viser Generelt-fanen hvilke datasett som er input og navn+type på utdata-parameteren.
**AK-4.4.4** Gitt Output-fanen, når formater velges og «Eksporter» klikkes, så åpnes Eksport-skjermen med forhåndsvisning per valgt format.
**AK-4.4.5** Vilkårsmodellen skal være generell (nodetyper og operatorer er ikke tjenestespesifikke) slik at samme struktur kan gjenbrukes for andre tjenester og eksporteres til andre motorer.

**AI-forslag (fra denne skjermen):** «AI-forslag»-knapp åpner AI-skjermen (kap. 4.10).

### 4.5 Datasett
Filtrerbar tabell (oppslagbare/brukeroppgitte/utledede/alle) med datapunkt, type, kilde, brukt-i-vilkår, kodeliste og rettslig grunnlag. Banner + lenke som forklarer forholdet til Informasjonsmodellen. «Nytt datapunkt»-handling.

### 4.6 Informasjonsmodell
Maskinlesbar modell (ikke skjemabygger) som et søknadssystem konsumerer.
- Intro som forklarer at modellen publiseres (JSON Schema / SKOS) for gjenbruk.
- Liste over opplysningselementer med opphav (oppgis av søker / oppslag / utledet), datatype, property-navn, påkrevd-flagg.
- Detaljpanel med to faner: **Modell** (property, datatype, opphav, kilde, kodeliste, brukt-i-vilkår) og **Behandling & bruk** (behandlingsgrunnlag, hvordan info behandles og brukes, rettslig hjemmel, lagringstid, mottakere).
- «Vis/skjul JSON Schema» og «Publiser modell».

**AK-4.6.1** Gitt informasjonsmodellen, så kan JSON Schema vises, avledet av datasettdefinisjonene, med `required` = brukeroppgitte felter.
**AK-4.6.2** Gitt et opplysningselement, så viser Behandling & bruk-fanen behandlingsgrunnlag, lagringstid, mottakere og bruk.

### 4.7 Kodelister / verdiregister
Filtrerbar liste (alle/juridiske/tekniske/eksterne). Detaljpanel med faner **Koder** (kode, term, definisjon, gyldighet, «Ny kode»), **Metadata** (type, juridisk grunnlag, antall koder, brukes i, sist endret) og **Historikk**.

### 4.8 Begrepsregister
Ordliste + detaljpanel med faner **Detaljer** (definisjon, lovreferanse, gjelder for, kodeliste, SKOS-type, **publisert-begrep-lenke til data.norge.no**, brukt-i-vilkår), **Historikk**, **Metadata**.

**AK-4.8.1** Gitt et begrep, så vises `skos:Concept`-type og en lenke til det publiserte begrepet på data.norge.no.

### 4.9 Presedensregister
Filtrerbar tabell (sak, organ, dato, utfall, vekt). Detaljpanel med faner **Sammendrag** (dato, organ, saksnummer, rettskildevekt, bestemmelse, sammendrag), **Metadata**, **Koblinger** (bestemmelse/vilkår). Rettskildevekt fra `KL-RETTSKILDEVEKT` (aldri fritekst). «Ny presedens»-handling.

### 4.10 AI-forslag
Kø av forslag (venstre) med konfidens, valgt forslag (midt) og forslagsdetaljer (høyre).
- Forslag viser: tittel, kilde, foreslått plassering, begrunnelse med kildesitering, presedens brukt som grunnlag, foreslått generisk mal og parametre, konfidens, status.
- Handlinger: **Avvis**, **Rediger**, **Godkjenn og legg til**.

**AK-4.10.1** AI-assistenten foreslår vilkårsnoder med kildehenvisning, forslag til generisk mal, kodeliste for parametre, `vurderingstype` og presedensbaserte tolkningsforslag — men **publiserer aldri selv**.
**AK-4.10.2** Gitt et AI-forslag, når det godkjennes, så opprettes/oppdateres vilkåret med status `validert`, og overgangen logges i proveniensen med `ai_forslag_versjon` og `godkjent_av`.

### 4.11 Saksbehandling
Saksdetalj med saksinfo og faner (oversikt, vilkår, dokumenter, kommunikasjon, vedtak, historikk). Vilkårstabell viser status per vilkår (fra `KL-VILKARSUTFALL`) inkl. skjønnskrevende, vurderingskilde (automatisk/saksbehandler/mangler dok.) og handling (vis/vurder/be om dokumentasjon). «Overstyr verdi» og «Generer vedtak».

**AK-4.11.1** Gitt en sak, så vises hver vilkårsstatus med kilde og handling; skjønnskrevende vilkår er tydelig merket.
**AK-4.11.2** Gitt et vilkår i en sak, når «Forklaringslogg» velges, så åpnes forklaringsloggen for saken.
**AK-4.11.3** Saksbehandler kan overstyre en automatisk verdi med begrunnelse (logges).

### 4.12 Forklaringslogg per sak
Resultatbanner + tidslinje per vilkår: utfall, versjon av vilkåret som ble brukt, data brukt, rettslig grunnlag/presedens, tidspunkt. Faner: forklaring, data brukt, regler brukt, presedens, eksport. Sidepanel: sammendrag, rettslig grunnlag, unike datakilder. «Lag rapport (PDF)».

**AK-4.12.1** Forklaringsloggen viser hvilken **versjon** av hvert vilkår som ble brukt, og kobling til rettslig grunnlag og presedens.

### 4.13 Kunnskapsgraf og påvirkningsanalyse
Tverrgående graf over hele modellen (erstatter tidligere tjeneste-til-tjeneste-graf).
- **Noder:** rettskilde (`eId`), begrep, vilkår, datasett, kodeliste, forklaringsmal, tjeneste, vedtak, presedens.
- **Kanter (typede):** `tolker/begrunner` (presedens→rettskilde), `bestemmer` (begrep→vilkår), `bruker` (vilkår→datasett), `basert_på` (forklaringsmal←datasett), `genererer` (forklaringsmal→vedtak), `relatert_til` (vilkår↔rundskriv), `refererer` (presedens→vilkår).
- **Utforsking:** naviger fra en node til naboer; filter per nodetype; relasjonstype-forklaring; valgt node med relasjons-/detalj-/historikk-/metadata-faner.
- **Sti til vedtak:** hovedflyt fra rettskilde → begrep → vilkår → datasett → forklaringsmal → vedtak.
- **Påvirkningsanalyse:** gitt en endring i én node, vis alle transitivt berørte noder nedstrøms (begreper, vilkår, tjenester, skjema, eksportformater) og hvilke testcaser som bør kjøres på nytt.

**AK-4.13.1** Gitt en node, når «Kjør påvirkningsanalyse» velges, så listes alle nedstrøms berørte noder gruppert etter type, med antall.
**AK-4.13.2** Historiske vedtak listes som *berørt av kildeendring* men *ikke automatisk endret* — de beholder sin opprinnelige versjon.

### 4.14 Eksportvisning
Faner per målformat (eFLINT/OpenFisca/DMN/RuleML). Forhåndsvisning av generert kode i lesevisning, med filnavn per format. Genereres fra vilkårstreet.

### 4.15 Testpanel (fase 2)
Velg testcase → kjør regel → vis forklaring → sammenlign mot forventet resultat og forventet forklaring.
**AK-4.15.1** Testcaser kjøres automatisk ved endring/republisering av et vilkår (koblet til påvirkningsanalysen); et vilkår kan ikke publiseres uten at berørte testcaser er kjørt og godkjent.

### 4.16 Diff / versjonsvisning
Sammenlign to versjoner av et vilkår; vis endrede felt, berørte noder og hvilke testcaser som må kjøres på nytt. *(Selve diff-algoritmen er ikke spesifisert her — kun skjermen.)*

---

## 5. Tverrgående funksjonelle regler

### 7.1 To-lags modell (generisk vs. tjenestespesifikk)
Samme generiske mal (f.eks. `GM-VANDEL-PERSON`) instansieres flere ganger med ulikt omfang og lovreferanse. Begreper, terskelverdier og rettslig forankring kan være tett koblet til lov/tjeneste; modellen støtter begge deler samtidig.

### 7.2 AI-assistert forslag til vilkårstre
1. Fagansvarlig peker ut relevante `eId`-noder. 2. AI søker i presedensregisteret etter avgjørelser knyttet til samme noder. 3. AI foreslår vilkårsnoder (kildehenvisning, generisk mal, kodeliste, `vurderingstype`, presedensbaserte tolkningsforslag). 4. Status: `utkast → foreslått av AI → validert → publisert`; hver overgang logges i proveniensen.

### 7.4 Eksportformater
| Format | Egnet for |
|---|---|
| eFLINT | Deontisk struktur |
| OpenFisca | Parametriserte beregninger |
| DMN | Beslutningstabeller |
| RuleML | Generell regelutveksling |

Kun mellomformat og målformat spesifiseres — ikke full kodegenerator-motor.

### 7.5 Skjønnsbaserte vilkår
`vurderingstype` ∈ {`regelbasert`, `skjonnsbasert`, `hybrid`}. Skjønns-/hybridvilkår har skjønnsmomenter, dokumentasjonskrav og eskaleringsrolle (kap. 3.8.1). Utfall hentes fra `KL-VILKARSUTFALL` (seks verdier), ikke bare oppfylt/ikke oppfylt.

### 7.6 Logiske operatorer
Mellom barnenoder og på rot: `OG`, `ELLER`, `XOR`, `NAND`. Systemet genererer et lesbart regeluttrykk og skal kunne oversette dette til målformatene (kap. 7.4).

### 7.7 Avledede artefakter
- **Informasjonsmodell** — generert av datasettdefinisjonene (kap. 4.6).
- **Saksbehandlingsverktøy** — kjører regelkoden mot data, viser status per vilkår, genererer vedtakstekst fra metadata.
- **Forklaringsmodell** — skrives av saksbehandlingsverktøyet: hvilke vilkår slo til, hvilke data ble brukt, kobling til rettslig grunnlag/presedens, og hvilken versjon av hvert vilkår. Alle strukturerte felt har verdiområde fra kodelisteregisteret.

---

## 8. Roller og tilgang
| Rolle | Ansvar |
|---|---|
| Fagansvarlig | Definerer tjeneste, datasett, regelverksreferanser |
| Jurist | Validerer AI-foreslåtte vilkår, juridisk forankrede kodelister og skjønnsmomenter |
| Systemforvalter/arkitekt | Eier tekniske/operasjonelle kodelister |
| Saksbehandler | Bruker saksbehandlingsverktøyet, utfører skjønnsvurderinger, kan overstyre med begrunnelse |
| AI-assistent | Foreslår vilkårstre, skjønnsmomenter og presedenskobling — publiserer aldri selv |

**AK-8.1** Rollevelgeren bytter aktiv rolle; tilgjengelige handlinger (f.eks. validering/publisering) styres av rollen.

---

## 9. Designsystem (bindende)
Bruk **Designsystemet** (Digdir). Ingen egne farger/typografi/komponenter utenfor systemet.
- **Typografi:** Inter (400/500/600). Overskrifter via `data-size`.
- **Farger:** semantiske roller (accent, neutral, brand1, brand2, info, success, warning, danger) via `--ds-color-*`-tokens. Ikke gjett tokennavn.
- **Fokus:** signatur dobbel fokus-ring (3px ytre + 3px indre) — skal aldri fjernes.
- **Komponenter:** bruk `@digdir/designsystemet-react` (alert, badge, button, card, tabs, table, tag, tooltip, dialog, field, input, chip, breadcrumbs, pagination, m.fl.). Mangler en komponent i systemet, flagg det heller enn å designe en egen.
- **Ikoner:** `@navikt/aksel-icons` (outline, 24×24, 1.5px).
- **Radius/skygge/spacing:** konservative tokens (radius default 4px; skygger xs–xl). Ingen gradienter, glassmorphism eller full-bleed foto.
- **Språk:** norsk bokmål primært, engelsk full dekning. Setningsstor forbokstav overalt. Ingen emoji i produkt-UI.
- **Tema:** støtt virksomhetstemaer (digdir/altinn/portal/uutilsynet) via `@digdir/designsystemet-theme`.

---

## 10. Ikke-funksjonelle krav
- **Tilgjengelighet (WCAG 2.1 AA):** tastaturnavigasjon, synlig fokus (Designsystemets fokus-ring), tilstrekkelig kontrast, semantisk markup, ledetekster på skjemafelt. Grafvisninger må ha tekstlig/tabellalternativ.
- **Responsivt:** fungerer fra mobil til brede skjermer; sidemeny kollapser < 880px.
- **Internasjonalisering:** all UI-tekst i strengfiler (no/en).
- **Ytelse:** lister/grafer må håndtere realistiske volum (hundrevis av vilkår, tusenvis av vedtak) med paginering/virtualisering.
- **Sporbarhet:** all endring av sentrale entiteter skal skrives til proveniens.
- **Reproduserbarhet:** et historisk vedtak skal kunne rekonstrueres presist fra de eksakte entitetsversjonene som gjaldt.
- **Interoperabilitet:** AKN for rettskilder, SKOS for begreper, CPSV-AP-NO for tjeneste, JSON Schema for informasjonsmodell.

---

## 11. Eksplisitt utenfor scope
- Håndtering av lov-/regelendring mens en sak er under behandling (overgangsregler).
- Varslingsmekanisme (kanal, mottakere) ved endring — påvirkningsanalysen viser *hva* som berøres, ikke *hvordan* parter varsles.
- Full eFLINT/OpenFisca/DMN-kodegenerator — kun mellom- og målformat spesifiseres.
- Selve kjøremiljøet for testmodulen (CI-pipeline).
- Diff-algoritmen bak versjonsvisningen — skjermen er spesifisert, ikke logikken.
- Automatisk synkronisering med eksterne autoritative registre — disse refereres, men synk-mekanismen er ikke designet.
- Faktisk Lovdata→AKN-konvertering (kun grensesnittet er spesifisert).

---

## 12. Ordliste
- **AKN** — Akoma Ntoso, XML-standard for rettsdokumenter.
- **CPSV-AP-NO** — norsk applikasjonsprofil for beskrivelse av offentlige tjenester.
- **SKOS** — Simple Knowledge Organization System (for begreper).
- **DMN** — Decision Model and Notation.
- **eId** — adresserbar identifikator for en bestemmelse i AKN.
- **Vurderingstype** — regelbasert / skjønnsbasert / hybrid.
- **Rettskildevekt** — kategorisk vekt (bindende/tungtveiende/veiledende/illustrerende/historisk).
