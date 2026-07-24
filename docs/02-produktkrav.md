# Produktkrav — Regel-IDE

**Versjon:** 0.4 (multi-virksomhet, se `00-endringslogg-v0.3.md`) · **Testcase:** Alminnelig skjenkebevilling (alkoholloven)

> Dette dokumentet dekker **konsept, navigasjon, skjermer, akseptkriterier, roller og designsystem** — altså det som er produktkrav i snever forstand. Full entitetsdefinisjon, relasjoner, RBAC-matrise, livssykluser og publiseringsmodell står i [`03-domenemodell.md`](03-domenemodell.md). Ikke-funksjonelle krav og arkitektur står i [`05-arkitektur-og-nfk.md`](05-arkitektur-og-nfk.md). Begrepene under (Rettskilde, Begrep, Vilkår, Regel …) er definert presist i [`01-referansemodell.md`](01-referansemodell.md) — les den først hvis noe virker underspesifisert her.
>
> **Krav vs. anbefaling:** "skal" er bindende, "bør" er anbefalt (kan fravikes med begrunnelse). Der en skjerm/entitet er merket **[Fase 2]** eller **[Fase 3]**, se [`06-veikart.md`](06-veikart.md) for rekkefølge — alt annet er Fase 1/MVP.

**Primærbrukere:** Regel-IDE er bevisst bygget for **tverrfaglige team**, ikke én rolle — tjenestedesignere, jurister, fagansvarlige/saksbehandlere og utviklere skal kunne jobbe i samme verktøy, samtidig, i samme rom (jf. `digital-rettsstat` prinsipp 7). Dette er ikke en uavklart avgrensning; det er selve konseptet. Konsekvensen for skjermdesign: ingen skjerm skal forutsette teknisk bakgrunn for å forstås (jf. forklarbarhets-/forståelighetskravet i kap. 7), og RBAC-matrisen (`03-domenemodell.md` §2) — ikke skjermseparasjon — er mekanismen som styrer hvem som kan gjøre hva.

---

## 1. Konsept og rammer

### 1.1 To metaforer (begge gjelder)
- **Kompileringsplattform for digital forvaltning** — arkitekturen: rettskilder → semantisk modell → begreper → regler → metadata → kjørbar kode → saksbehandling → forklaring → vedtak. Brukes i teknisk/arkitektonisk kommunikasjon.
- **IDE for juridiske regler** — brukeropplevelsen: navigator, editor, referanser, validering, AI-assistent, eksport, historikk, publisering. Brukes i UI og mot jurister/fagansvarlige/beslutningstakere.

### 1.2 Faseinndeling (leveranserekkefølge)

| Fase | Innhold |
|---|---|
| **Fase 1 — Plattformlag** | Rettskildebibliotek, Begrepsregister, Kodelister/verdiregister |
| **Fase 2 — Regelmodellering** | Vilkår og regler (grafeditor), Testmodul |
| **Fase 3 — Anvendelse** | Eksportmotor, Saksbehandlingsgenerator, Informasjonsmodell, Kunnskapsgraf/påvirkningsanalyse |

Denne tabellen er den *arkitektoniske* lagdelingen (hva forutsetter hva). Den *faktiske byggerekkefølgen* — hvilken rekkefølge vi implementerer i, inkludert hvorfor rettskilder kommer før begreper og hvorfor vilkårstreet trenger flere iterasjoner — står i [`06-veikart.md`](06-veikart.md). Implementasjonen skal ikke arkitektonisk sperre for at faser bygges inkrementelt. Alle entiteter i [`03-domenemodell.md`](03-domenemodell.md) skal finnes som datamodell fra Fase 1, selv om UI-et for dem kommer senere.

### 1.3 Anbefalt teknologi
Se [`05-arkitektur-og-nfk.md`](05-arkitektur-og-nfk.md) for begrunnelse og alternativer. Kort oppsummert:
- **Frontend:** React + TypeScript, med komponenter og tokens fra Designsystemet (`@digdir/designsystemet-react/-css/-theme`). Se kap. 6.
- **Rettskildeformat:** Akoma Ntoso (AKN) som kanonisk lagringsformat.
- **Regeleksport:** eFLINT, OpenFisca, DMN, RuleML.
- **Begreper:** SKOS, publisert til Felles datakatalog (data.norge.no).
- **Tjeneste:** CPSV-AP-NO.

---

## 2. Overordnet navigasjon og skall

**Endret i v0.3 (se `00-endringslogg-v0.3.md`):** Regel-IDE er en **flervirksomhets-applikasjon** — én driftsatt løsning, delt av opptil ca. 1000 offentlige virksomheter, ikke én separat driftsatt instans per virksomhet. Bakgrunnen er ren driftsøkonomi: en egen database/instans per virksomhet ville vært uforholdsmessig kostbart å drifte og vedlikeholde i den skalaen. Hver virksomhet ser og redigerer **kun sine egne** entiteter (Begrep, Vilkår/Regel/Unntak, Tjeneste, Testcase, lokale rettskilder/virksomhetsdokumenter — se `03-domenemodell.md` §0/§2 for hvordan dette håndheves per entitetstype). Delte/nasjonale rettskilder (Lov/Forskrift fra Lovdata) er unntaket: de importeres og vedlikeholdes **én gang**, synlige for alle virksomheter — å duplisere f.eks. alkoholloven per virksomhet ville betydd N separate vedlikeholdsjobber ved hver lovendring i stedet for én. Tjenester fra andre virksomheter refereres fortsatt via eksternt oppslag (data.norge.no), ikke ved deling av redigeringsrettigheter til virksomhetens egne entiteter.

**Innlogging:** Ansattporten (se `05-arkitektur-og-nfk.md` for identitetsløsning og hvordan virksomhetstilhørighet avledes derfra).

**Onboarding av en ny virksomhet** (opprette virksomheten, knytte dens første brukere) er ikke spesifisert i detalj ennå — se `00-endringslogg-v0.3.md`, "Ikke bekreftet ennå".

**Testcasets virksomhet: Testkommunen.** Skjenkebevilling er en kommunal oppgave (alkoholloven kapittel 4, "Kommunale skjenkebevillinger" — kommunen er bevillingsmyndighet, ikke staten). Digdir har allerede en fiktiv **Testdepartementet** for statlig testing; siden vår kompetente myndighet (`Tjeneste.kompetent_myndighet`, `03-domenemodell.md` §1.5) er en kommune, bruker testcaset **Testkommunen** i stedet — ikke Testdepartementet.

**Skall:**
- **Venstre sidemeny** (mørk, IDE-følelse) med navigasjonsgrupper. Kollapser til skuff med hamburgermeny < 880px bredde.
- **Topplinje** per skjerm: brødsmulesti, tittel, undertittel og skjermspesifikke handlingsknapper (f.eks. «Ny kilde», «Eksporter»).
- **Rollevelger** nederst i sidemenyen (kap. 5) — bytter aktiv rolle og påvirker hvilke handlinger som er tilgjengelige, iht. RBAC-matrisen i [`03-domenemodell.md`](03-domenemodell.md) §2.
- Skallet skal være responsivt: kolonnelayouter stables til én kolonne på smale skjermer.

**Navigasjonselementer (skjermer):** — MVP/konseptbevis-grensen er eksplisitt: kap. 1–9 under er *innenfor* det første beviset (rettskilde→begrep→vilkår→test→eksport→forklaring, se `06-veikart.md`); Dashboard og Kunnskapsgraf er eksplisitt **utenfor** — ikke fordi de er uviktige, men fordi de ikke beviser noe før resten har reelt innhold.

1. Dashboard **[Utenfor MVP/konseptbevis — se veikart]**
2. Tjenester → Tjenestedefinisjon **[Fase 1]**
3. Rettskilder (Rettskildebibliotek) **[Fase 1]**
4. Vilkår og regler (grafeditor) **[Fase 2]**
5. Datasett **[Fase 1/2]**
6. Informasjonsmodell **[Fase 3]**
7. Kodelister / verdiregister **[Fase 1]**
8. Begreper (Begrepsregister) **[Fase 1]**
9. Presedens (Presedensregister) **[Fase 1/2]**
10. AI-forslag **[Fase 2]**
11. Saksbehandling **[På vent — tynn demo-slice, se veikart]**
12. Forklaringslogg **[På vent — tynn demo-slice, se veikart]**
13. Kunnskapsgraf **[Utenfor MVP/konseptbevis — se veikart]**
14. Eksport **[Fase 2/3]**

**AK-2.1** Gitt en bruker på en hvilken som helst skjerm, når hun velger et element i sidemenyen, så skal det navigeres til tilhørende skjerm og aktivt element markeres.
**AK-2.2** Gitt skjermbredde < 880px, når siden lastes, så skal sidemenyen være skjult bak en hamburgerknapp, og et klikk på knappen skal åpne den som overlay med bakteppe.

---

## 3. Skjermer — funksjonelle krav

Hver skjerm skal bruke Designsystemet-komponenter (kap. 6). Detaljpaneler skal bruke faner. Lister/tabeller skal ha søk/filter der det er relevant.

### 3.1 Dashboard **[Utenfor MVP/konseptbevis]**
KPI-kort (antall tjenester, vilkår, presedens, kodelister), tabell «Nylig oppdaterte tjenester» (klikkbar → tjenestedefinisjon), aktivitetsgraf (siste 30 dager: endringer i vilkår, AI-forslag), og «AI-forslag til gjennomgang»-kort med antall og lenke.

**AK-3.1.1** Gitt dashboardet, når en tjenesterad klikkes, så skal tjenestedefinisjonen for den tjenesten åpnes.

### 3.2 Tjenester / Tjenestedefinisjon (CPSV-AP-NO) **[Fase 1]**
Liste med status/søk. Definisjonsskjerm skal vise: grunndata, regelverksreferanser (klikkbare `eId`), tjenesteavhengigheter (med «Søk i data.norge.no» for eksterne), og Hendelser (se `03-domenemodell.md` §1.5). «Ny tjeneste»-handling.

**AK-3.2.1** Gitt tjenestedefinisjonen, så skal alle CPSV-AP-NO-grunndatafelt, hendelsesliste og avhengigheter vises.
**AK-3.2.2** Gitt en ekstern tjenesteavhengighet, så skal den være merket som eksternt oppslag mot data.norge.no.

### 3.3 Rettskildebibliotek **[Fase 1]**
Tre-navigasjon (lov → kapittel → paragraf; forskrift; rundskriv; presedens; virksomhetens egne dokumenter) med søk. Detaljvisning med faner:
- **Tekst** — lovtekst med taggede ord/avsnitt (begrep/vilkår/regel), lagvis (overlappende tagger tillates), med filter «Vis tagger» og liste over egne tagger med «Fjern».
- **Kilde (XML)** — underliggende AKN, taggede ord som `<term refersTo="#…">`.
- **Metadata** — kilde, `eId`/identifikator, ikrafttredelse, kildetype, versjon (også på kildenivå for topp-noder).
- **Koblinger** — begrep/vilkår denne bestemmelsen er koblet til.
- **Historikk** — proveniens.

Høyre kolonne: tilknyttede presedensavgjørelser med rettskildevekt.

**Tekstmerking → tagging (2-stegs meny):**
**AK-3.3.1** Gitt Tekst-fanen, når bruker markerer tekst og høyreklikker (eller slipper museknapp), så skal en meny vises: steg 1 velg type (begrep/tjeneste/vilkår/regel), steg 2 velg handling (ny / knytt til eksisterende), med «tilbake».
**AK-3.3.2** Når en tag opprettes, skal den valgte tekstflaten markeres med fargekode (begrep=lilla, tjeneste=blå, vilkår=gul, regel=grønn), lagres posisjonsbasert **og** med `eId` + quote-selector (se `05-arkitektur-og-nfk.md` §3 for hvorfor ren tegnoffset ikke er tilstrekkelig), og reflekteres i Koblinger-fanen og AKN-kilden.
**AK-3.3.3** Gitt markering av et helt avsnitt som overlapper et allerede tagget ord, skal ikke den innerste taggen skjules — begge skal vises lagvis.
**AK-3.3.4** Gitt en egendefinert tag, når «Fjern» velges, skal taggen fjernes og teksten gå tilbake til forrige tilstand.

**Ny kilde / import:**
**AK-3.3.5** Gitt «Ny kilde» eller «Importer AKN», skal en modal åpnes med to metoder: hent fra Lovdata (søk) eller last opp fil.
**AK-3.3.6** Gitt en importert kilde i annet XML-format (Lovdata-XML/NLM), skal konvertering vises side-ved-side til AKN (kildeformat → normalisert AKN).
**AK-3.3.7** Gitt importmodalen, skal bruker kunne sette/redigere metadata (tittel, kortnavn, ELI-identifikator, kildetype, ikrafttredelse, konsolidert dato, utgiver) før «Legg til i biblioteket».

### 3.4 Vilkår og regler (grafeditor) — **kjernefunksjon** **[Fase 2]**

> Nodemodellen bruker de tre låste nodetypene fra `01-referansemodell.md` §5: **Vilkår** (bladnode), **Regel** (komposisjonsnode) og **Unntak**. Se der for kardinaliteter og invarianter — beskrivelsen under er skjermatferden, ikke ontologien.

To visninger via veksler: **Graf** (standard) og **Tre**.

**Grafmodell (DMN-inspirert DRD, skal være en rettet asyklisk graf — DAG, se `03-domenemodell.md` §1.12):**
- Noder, visuelt skilt per type: **Regel**-noder (inkl. rotnoden «Vedtak om skjenkebevilling»), **Vilkår**-noder (med §-referanse og skjønn/regel-indikator), **Unntak**-noder (visuelt koblet til sin `gjelder_regel` med en «unntak fra»-merking), og inndata-noder (datasett).
- Kanter: informasjonsflyt nedenfra og opp (input → vilkår → regel → vedtak), med pilhoder mot den noden som krever input. Unntak-noder tegnes med en distinkt kantstil mot sin `gjelder_regel`.
- Klikk på node velger den og fyller egenskapspanelet — panelets innhold varierer per nodetype (se under). Valgt node og dens naboer utheves; øvrige dempes.
- Nummererte noder (rulemapping-stil).

**Tre-visning:** hierarkisk liste med ROT-operator, operator per Regel-node («barn: OG»), Unntak vist som et eget, innrykket element under sin `gjelder_regel`, og generert regeluttrykk.

**Egenskapspanel (faner, feltene varierer per nodetype — se `03-domenemodell.md` §1.8–1.10 for fullstendig feltliste per type):**
- **Generelt** — ID, tittel, generisk mal, juridisk grunnlag, begrep, status, gyldig fra, beskrivelse. For **Vilkår**: `vilkarstype` (formell/materiell), `vurderingstype`, input-datasett, og skjønnsfelt (skjønnsgrunnlag/skjønnsmomenter/eskaleringsrolle) når relevant. For **Regel**: `barn[]`, logisk operator (**OG/ELLER/IKKE** — ikke XOR/NAND, se `01-referansemodell.md` §3) med generert uttrykk, `utdata`. For **Unntak**: `gjelder_regel` og `betingelse` (valgt fra eksisterende Vilkår/Regel eller opprett ny).
- **Tekster** — veiledningstekst til bruker/saksbehandler (alle nodetyper); innvilgelses-/avslagstekst (kun Regel, se `03-domenemodell.md` §1.9).
- **Standardref.** — referanser til rettskilde/begrep/mal/lovhenvisninger.
- **Output** — kun på Regel-noder: motoruavhengige målformat (eFLINT/DMN/OpenFisca/RuleML-RUML/XML) med av/på, og «Eksporter (N format)» → Eksport-skjermen.
- **Metadata** — versjon/status/gyldighet, brukt i N vedtak, lovreferanser.
- **Historikk** — proveniens med handling og kildereferanser.

**AK-3.4.1** Gitt grafvisningen, når en node (Vilkår/Regel/Unntak) klikkes, skal noden velges og egenskapspanelet oppdateres med feltene for dens type.
**AK-3.4.2** Gitt en Regel-node med barn, når operator endres (OG/ELLER/IKKE), skal det genererte regeluttrykket oppdateres tilsvarende, lagres og anvendes.
**AK-3.4.3** Gitt en Vilkår-node, skal Generelt-fanen vise hvilke datasett som er input; gitt en Regel-node, skal fanen vise `utdata` (navn + type).
**AK-3.4.4** Gitt Output-fanen på en Regel-node, når formater velges og «Eksporter» klikkes, skal Eksport-skjermen åpnes med forhåndsvisning per valgt format.
**AK-3.4.5** Vilkårs-/regelmodellen skal være generell (nodetyper og operatorer er ikke tjenestespesifikke) slik at samme struktur kan gjenbrukes for andre tjenester og eksporteres til andre motorer.
**AK-3.4.6** Grafen skal ikke tillate sykler på tvers av `barn`- og `unntak`/`betingelse`-relasjoner samlet — et forsøk på å koble en node til en av sine egne etterkommere (via noen av kanttypene) skal avvises med en feilmelding som viser sykelen.
**AK-3.4.7** Gitt en Unntak-node, skal den vises tydelig knyttet til sin `gjelder_regel` (visuelt og i tre-visningens innrykk), og kan aldri opprettes uten både `gjelder_regel` og `betingelse` satt (INV-3/INV-4, `01-referansemodell.md` §5.4).

**AI-forslag (fra denne skjermen):** «AI-forslag»-knapp åpner AI-skjermen (kap. 3.10).

### 3.5 Datasett **[Fase 1/2]**
Filtrerbar tabell (oppslagbare/brukeroppgitte/utledede/alle) med datapunkt, type, kilde, brukt-i-vilkår, kodeliste og rettslig grunnlag. Banner + lenke som forklarer forholdet til Informasjonsmodellen. «Nytt datapunkt»-handling.

### 3.6 Informasjonsmodell **[Fase 3]**
Maskinlesbar modell (ikke skjemabygger) som et søknadssystem konsumerer.
- Intro som forklarer at modellen publiseres (JSON Schema / SKOS) for gjenbruk.
- Liste over opplysningselementer med opphav (oppgis av søker / oppslag / utledet), datatype, property-navn, påkrevd-flagg.
- Detaljpanel med to faner: **Modell** (property, datatype, opphav, kilde, kodeliste, brukt-i-vilkår) og **Behandling & bruk** (behandlingsgrunnlag, hvordan info behandles og brukes, rettslig hjemmel, lagringstid, mottakere).
- «Vis/skjul JSON Schema» og «Publiser modell» (gjør modellen tilgjengelig for gjenbruk — ikke en direkte publisering til data.norge.no, se `05-arkitektur-og-nfk.md` §1.2).

**AK-3.6.1** Gitt informasjonsmodellen, skal JSON Schema kunne vises, avledet av datasettdefinisjonene, med `required` = brukeroppgitte felter.
**AK-3.6.2** Gitt et opplysningselement, skal Behandling & bruk-fanen vise behandlingsgrunnlag, lagringstid, mottakere og bruk.

### 3.7 Kodelister / verdiregister **[Fase 1]**
Filtrerbar liste (alle/juridiske/tekniske/eksterne). Detaljpanel med faner **Koder** (kode, term, definisjon, gyldighet, «Ny kode»), **Metadata** (type, juridisk grunnlag, antall koder, brukes i, sist endret) og **Historikk**.

### 3.8 Begrepsregister **[Fase 1]**
Ordliste + detaljpanel med faner **Detaljer** (definisjon, lovreferanse, gjelder for, kodeliste, SKOS-type, publisert-begrep-lenke til data.norge.no, brukt-i-vilkår), **Historikk**, **Metadata**.

**AK-3.8.1** Gitt et publisert begrep, skal `skos:Concept`-type vises, og en lenke til det publiserte begrepet på data.norge.no når `skosUrl` er satt — feltet fylles ut asynkront etter data.norge.nos neste høsting (`05-arkitektur-og-nfk.md` §1.2), ikke umiddelbart ved publisering. Inntil da vises publiseringsstatus uten ekstern lenke.

### 3.9 Presedensregister **[Fase 1/2]**
Filtrerbar tabell (sak, organ, dato, utfall, vekt). Detaljpanel med faner **Sammendrag** (dato, organ, saksnummer, rettskildevekt, bestemmelse, sammendrag), **Metadata**, **Koblinger** (bestemmelse/vilkår). Rettskildevekt fra `KL-RETTSKILDEVEKT` (aldri fritekst). «Ny presedens»-handling.

### 3.10 AI-forslag **[Fase 2]**
Kø av forslag (venstre) med konfidens, valgt forslag (midt) og forslagsdetaljer (høyre).
- Forslag skal vise: tittel, kilde, foreslått plassering, begrunnelse med kildesitering, presedens brukt som grunnlag, foreslått generisk mal og parametre, konfidens, status.
- Handlinger: **Avvis**, **Rediger**, **Godkjenn og legg til**.

**AK-3.10.1** AI-assistenten skal foreslå vilkårsnoder med kildehenvisning, forslag til generisk mal, kodeliste for parametre, `vurderingstype` og presedensbaserte tolkningsforslag — men skal aldri publisere selv.
**AK-3.10.2** Gitt et AI-forslag, når det godkjennes, skal vilkåret opprettes/oppdateres med status `validert`, og overgangen skal logges i proveniensen med `ai_forslag_versjon` og `godkjent_av`.

### 3.11 Saksbehandling **[På vent — se veikart for demo-slice]**
Saksdetalj med saksinfo og faner (oversikt, vilkår, dokumenter, kommunikasjon, vedtak, historikk). Vilkårstabell viser status per vilkår (fra `KL-VILKARSUTFALL`) inkl. skjønnskrevende, vurderingskilde (automatisk/saksbehandler/mangler dok.) og handling (vis/vurder/be om dokumentasjon). «Overstyr verdi» og «Generer vedtak».

**AK-3.11.1** Gitt en sak, skal hver vilkårsstatus vises med kilde og handling; skjønnskrevende vilkår skal være tydelig merket.
**AK-3.11.2** Gitt et vilkår i en sak, når «Forklaringslogg» velges, skal forklaringsloggen for saken åpnes.
**AK-3.11.3** Saksbehandler skal kunne overstyre en automatisk verdi med begrunnelse (logges).

### 3.12 Forklaringslogg per sak **[På vent — se veikart for demo-slice]**
Resultatbanner + tidslinje per vilkår: utfall, versjon av vilkåret som ble brukt, data brukt, rettslig grunnlag/presedens, tidspunkt. Faner: forklaring, data brukt, regler brukt, presedens, eksport. Sidepanel: sammendrag, rettslig grunnlag, unike datakilder. «Lag rapport (PDF)».

**AK-3.12.1** Forklaringsloggen skal vise hvilken **versjon** av hvert vilkår som ble brukt, og kobling til rettslig grunnlag og presedens.

### 3.13 Kunnskapsgraf og påvirkningsanalyse **[Utenfor MVP/konseptbevis]**
Tverrgående graf over hele modellen (erstatter tidligere tjeneste-til-tjeneste-graf).
- **Noder:** rettskilde (`eId`), begrep, vilkår, datasett, kodeliste, forklaringsmal, tjeneste, vedtak, presedens.
- **Kanter (typede):** `tolker/begrunner` (presedens→rettskilde), `bestemmer` (begrep→vilkår), `bruker` (vilkår→datasett), `basert_på` (forklaringsmal←datasett), `genererer` (forklaringsmal→vedtak), `relatert_til` (vilkår↔rundskriv), `refererer` (presedens→vilkår).
- **Utforsking:** naviger fra en node til naboer; filter per nodetype; relasjonstype-forklaring; valgt node med relasjons-/detalj-/historikk-/metadata-faner.
- **Sti til vedtak:** hovedflyt fra rettskilde → begrep → vilkår → datasett → forklaringsmal → vedtak.
- **Påvirkningsanalyse:** gitt en endring i én node, vis alle transitivt berørte noder nedstrøms (begreper, vilkår, tjenester, skjema, eksportformater) og hvilke testcaser som bør kjøres på nytt.

**AK-3.13.1** Gitt en node, når «Kjør påvirkningsanalyse» velges, skal alle nedstrøms berørte noder listes, gruppert etter type, med antall.
**AK-3.13.2** Historiske vedtak skal listes som *berørt av kildeendring* men *ikke automatisk endret* — de skal beholde sin opprinnelige versjon.

### 3.14 Eksportvisning **[Fase 2/3]**
Faner per målformat (eFLINT/OpenFisca/DMN/RuleML). Forhåndsvisning av generert kode i lesevisning, med filnavn per format. Genereres fra vilkårstreet.

### 3.15 Testpanel **[Fase 2]**
Velg testcase → kjør regel → vis forklaring → sammenlign mot forventet resultat og forventet forklaring.

**AK-3.15.1** Testcaser skal kjøres automatisk ved endring/republisering av et vilkår (koblet til påvirkningsanalysen); et vilkår skal ikke kunne publiseres uten at berørte testcaser er kjørt og godkjent (se publiseringsmodell, `03-domenemodell.md` §4).

### 3.16 Diff / versjonsvisning **[Fase 2/3]**
Sammenlign to versjoner av et vilkår; vis endrede felt, berørte noder og hvilke testcaser som må kjøres på nytt. Selve diff-algoritmen er ikke spesifisert her — kun skjermen (se `11. Eksplisitt utenfor scope`).

---

## 4. Tverrgående funksjonelle regler

### 4.1 To-lags modell (generisk vs. tjenestespesifikk)
Samme generiske mal (f.eks. `GM-VANDEL-PERSON`) skal kunne instansieres flere ganger med ulikt omfang og lovreferanse. Dette er direkte relevant for `digital-rettsstat` prinsipp 9 ("modellér delte regler én gang") — tverrgående/felles rettskilder (forvaltningsloven, GDPR, arkivloven) bør uttrykkes som generiske maler som gjenbrukes på tvers av tjenester, ikke tolkes på nytt per tjeneste. Begreper, terskelverdier og rettslig forankring kan være tett koblet til lov/tjeneste; modellen skal støtte begge deler samtidig.

### 4.2 AI-assistert forslag til vilkårstre
1. Fagansvarlig peker ut relevante `eId`-noder.
2. AI søker i presedensregisteret etter avgjørelser knyttet til samme noder.
3. AI foreslår vilkårsnoder (kildehenvisning, generisk mal, kodeliste, `vurderingstype`, presedensbaserte tolkningsforslag).
4. Status: `utkast → foreslått av AI → validert → publisert`; hver overgang skal logges i proveniensen. Se livssyklusdiagram i `03-domenemodell.md` §3.

AI skal aldri validere eller publisere selv (jf. RBAC-matrisen, `03-domenemodell.md` §2, og `digital-rettsstat` prinsipp 4: skjønn forblir hos mennesker).

### 4.3 Eksportformater

| Format | Egnet for |
|---|---|
| eFLINT | Deontisk struktur |
| OpenFisca | Parametriserte beregninger |
| DMN | Beslutningstabeller |
| RuleML | Generell regelutveksling |

Kun mellomformat og målformat spesifiseres — ikke full kodegenerator-motor (se kap. 11).

### 4.4 Skjønnsbaserte vilkår
`vurderingstype` ∈ {`regelbasert`, `skjonnsbasert`, `hybrid`}. Skjønns-/hybridvilkår skal ha ett skjønnsgrunnlag, 1..N skjønnsmomenter, dokumentasjonskrav og eskaleringsrolle (presise definisjoner: `01-referansemodell.md` §6.1). Utfall hentes fra `KL-VILKARSUTFALL` (seks verdier), ikke bare oppfylt/ikke oppfylt. Et avklaringsbehov oppstår alltid ved evaluering av et skjønns-/hybridvilkår — dette er samme mønster som NAVs "avklaringsbehov" (`digital-rettsstat/docs/06-regellaget.md` §3) — et navngitt stopp-punkt, ikke en unntakshåndtering.

### 4.5 Logiske operatorer
Mellom barnenoder og på rot: **OG, ELLER, IKKE**. Systemet skal generere et lesbart regeluttrykk og skal kunne oversette dette til målformatene (kap. 4.3). Se begrunnelse for å utelate XOR/NAND i `01-referansemodell.md` §3.

### 4.6 Avledede artefakter
- **Informasjonsmodell** — generert av datasettdefinisjonene (kap. 3.6).
- **Saksbehandlingsverktøy** — kjører regelkoden mot data, viser status per vilkår, genererer vedtakstekst fra metadata.
- **Forklaringsmodell** — skrives av saksbehandlingsverktøyet: hvilke vilkår slo til, hvilke data ble brukt, kobling til rettslig grunnlag/presedens, og hvilken versjon av hvert vilkår. Alle strukturerte felt skal ha verdiområde fra kodelisteregisteret.

### 4.7 Domenehendelser
Sentrale entitetsendringer skal utløse en domenehendelse (se `03-domenemodell.md` §5 for fullstendig liste og skjema): `RulePublished`, `RuleArchived`, `SourceImported`, `AIProposalApproved`, `ConceptChanged`. Dette er grunnlaget for lovspeil-varsling (jf. `07-forklaringsmodell-api-avvik.md`) og for påvirkningsanalysen i kap. 3.13.

---

## 5. Roller og tilgang

| Rolle | Ansvar |
|---|---|
| Fagansvarlig | Definerer tjeneste, datasett, regelverksreferanser |
| Jurist | Validerer AI-foreslåtte vilkår, juridisk forankrede kodelister og skjønnsmomenter |
| Systemforvalter/arkitekt | Eier tekniske/operasjonelle kodelister |
| Saksbehandler | Bruker saksbehandlingsverktøyet, utfører skjønnsvurderinger, kan overstyre med begrunnelse |
| AI-assistent | Foreslår vilkårstre, skjønnsmomenter og presedenskobling — publiserer aldri selv |

**AK-5.1** Rollevelgeren skal bytte aktiv rolle; tilgjengelige handlinger (f.eks. validering/publisering) skal styres av rollen iht. RBAC-matrisen i `03-domenemodell.md` §2.

---

## 6. Designsystem (bindende)
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

## 7. Ikke-funksjonelle krav

Full liste (ytelse, sporbarhet, reproduserbarhet, interoperabilitet, tekniske risikoområder) står i [`05-arkitektur-og-nfk.md`](05-arkitektur-og-nfk.md). De brukervendte kravene som direkte styrer skjermdesign:

- **Tilgjengelighet (WCAG 2.1 AA):** tastaturnavigasjon, synlig fokus (Designsystemets fokus-ring), tilstrekkelig kontrast, semantisk markup, ledetekster på skjemafelt. Grafvisninger skal ha tekstlig/tabellalternativ.
- **Responsivt:** skal fungere fra mobil til brede skjermer; sidemeny kollapser < 880px.
- **Internasjonalisering:** all UI-tekst i strengfiler (no/en).
- **Forståelighet og forklarbarhet** (Schartum 2025, avsnitt 5.9–5.10): UI-tekst for jurister/fagansvarlige skal være forståelig uten teknisk baggrunn; der teknisk kompleksitet ikke kan skjules (f.eks. eksportert DMN-XML), skal det finnes en forklarende visning ved siden av (jf. kap. 3.14 Eksportvisning).

---

## 8. Eksplisitt utenfor scope
- Håndtering av lov-/regelendring mens en sak er under behandling (overgangsregler).
- Varslingsmekanisme (kanal, mottakere) ved endring — påvirkningsanalysen viser *hva* som berøres, ikke *hvordan* parter varsles.
- Full eFLINT/OpenFisca/DMN-kodegenerator — kun mellom- og målformat spesifiseres.
- Selve kjøremiljøet for testmodulen (CI-pipeline).
- Diff-algoritmen bak versjonsvisningen — skjermen er spesifisert, ikke logikken.
- Automatisk synkronisering med eksterne autoritative registre — disse refereres, men synk-mekanismen er ikke designet.
- Faktisk Lovdata→AKN-konvertering (kun grensesnittet er spesifisert).

---

## 9. Ordliste
- **AKN** — Akoma Ntoso, XML-standard for rettsdokumenter.
- **CPSV-AP-NO** — norsk applikasjonsprofil for beskrivelse av offentlige tjenester.
- **SKOS** — Simple Knowledge Organization System (for begreper).
- **DMN** — Decision Model and Notation.
- **eId** — adresserbar identifikator for en bestemmelse i AKN.
- **ELI** — European Legislation Identifier, stabil identifikator for rettsakter (jf. `digital-rettsstat`).
- **Vurderingstype** — regelbasert / skjønnsbasert / hybrid.
- **Rettskildevekt** — kategorisk vekt (bindende/tungtveiende/veiledende/illustrerende/historisk).
- **Vilkarstype** — formell / materiell (jf. `01-referansemodell.md` §6).
- **Lovspeil** — vedlikeholdt samsvar mellom rettskildeversjon og regelen som faktisk er implementert (Schartum; jf. `07-forklaringsmodell-api-avvik.md`).
- **Regelnode** — komposisjonsnode i vilkårstreet (`barn[]` + operator + `utdata`), kalt "Regel" i referansemodellen — se `01-referansemodell.md` §5.6 for hvorfor navnet "regelnode" brukes i API-et for å unngå kollisjon med `forklaringsmodell-api`s `Regel`.
- **NLOD 2.0** — Norsk lisens for offentlige data, lisensen Lovdatas gratis API-datasett publiseres under (kildeangivelse påkrevd).
- **DCAT-AP-NO / SKOS-AP-NO-Begrep / CPSV-AP-NO** — norske forvaltningsstandarder for hhv. datasettkataloger, begrepsbeskrivelser og tjenestebeskrivelser, brukt av data.norge.nos høstingsmekanisme (`05-arkitektur-og-nfk.md` §1.2).
- **Testkommunen** — fiktiv kommune brukt som testcasets virksomhet, siden skjenkebevilling er en kommunal oppgave (se kap. 2).
- **Skjønnsgrunnlag / skjønnsmoment / avklaringsbehov** — se `01-referansemodell.md` §6.1.
- **Vedtak / vedtaksgrunnlag / vedtaksvirkning** — se `01-referansemodell.md` §15.1.

Se `01-referansemodell.md` for det fullstendige begrepsapparatet (Regelkilde, Rettskilde, Regel, Vilkår, Rettsfølge, Unntak, Fakta, Vilkårsvurdering, Regelanvendelse, Beslutningsmodell/-regel/-resultat, Regelmodell/-representasjon/-uttrykk/-implementasjon).
