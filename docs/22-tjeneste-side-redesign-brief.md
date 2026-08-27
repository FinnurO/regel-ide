# Tjeneste-siden — redesign-brief

> **Status:** notat for redesign-runden 2026-08-27. Skrevet for å gi et design-verktøy (Claude
> Design, jf. README §"Forhold til søsterrepoer"/`prototyper/`) et fullstendig, korrekt bilde av
> hva siden faktisk viser og henger sammen med — IKKE et forslag til ny visuell løsning. Bygget
> ved å lese `TjenesteDetalj.tsx` (1253 linjer, dagens implementasjon) linje for linje, pluss de
> autoritative typene i `api/types.ts` og `TjenesteEntitet` i `RegelIde.Data/Entiteter.cs`.
> Designkonvensjonene (tokens, komponentvokabular) ligger i [`docs/09-design-konvensjoner.md`](09-design-konvensjoner.md)
> og gjelder fortsatt — dette dokumentet er innhold/struktur, ikke visuelt språk.

## 1. Formålet med siden

Siden viser og redigerer **én `Tjeneste`** — regel-IDEs betegnelse på det andre lag kaller en
**Rettighet**: en konkret offentlig tjeneste/rettighet en virksomhet (kommune, direktorat …) tilbyr
innbyggere/næringsdrivende, modellert etter CPSV-AP-NO (EUs standardvokabular for offentlige
tjenester — se [`docs/03-domenemodell.md`](03-domenemodell.md) §1.5). Eksempelet brukt gjennom hele
spesifikasjonen er «Alminnelig skjenkebevilling» (alkoholloven).

Siden er **arbeidsflaten** der et tverrfaglig team (tjenestedesigner, jurist, fagansvarlig,
utvikler — README §"To metaforer") bygger opp én tjeneste fra bunnen: fra ren metadata
(tittel, målgruppe, kostnad …), via et forfattet "hva innebærer dette"-innhold, til de faktiske
**koblingene** som gjør tjenesten en del av kunnskapsgrafen — regelverket den bygger på
(regelverksreferanser), hendelsene som gjør den relevant (hendelser), andre tjenester den henger
sammen med (tjenesteavhengigheter), de konkrete brukerinteraksjonene den brytes ned i (handlinger),
og — til syvende og sist — det kjørbare vilkårstreet som avgjør et faktisk vedtak.

Siden er **ikke** en ferdig, presentasjonsklar visning for en innbygger (det er
`TjenesteVeiledning.tsx`, se §4 under) — det er et **forfatter-/redigeringsverktøy**, tett koblet
til statusløpet en tjeneste går gjennom (`utkast → under_revisjon → validert → publisert →
tilbaketrukket/arkivert`).

Siden nås fra: `TjenesterListe.tsx` (hovedlisten), `TjenesteforslagKo.tsx` ("Identifiser
tjenester" — KI-forslags-køen, samme side men med et forslags-badge før godkjenning),
`VilkarstreListe.tsx`, `RettskildeDetalj.tsx` (en rettskilde-nodes "referert av tjenester"-liste),
og `HandlingDetalj.tsx` (tilbake til eiende tjeneste).

## 2. Informasjonselementer — full liste

Gruppert som dagens seksjoner (topp til bunn). "Kilde" viser feltnavnet på `TjenesteDto`/
`TjenesteEntitet` der det finnes ett direkte.

### 2.1 Header (utenfor seksjonene)
- **Tittel** — vises som H1.
- **Status** — vises som `Tag` rett under tittelen (samme verdi som §2.7 sin redigerbare `Select`).
- **JSON-modelleksport** — knapp som viser/skjuler en rå, forhåndsformatert JSON-dump av HELE den
  sammensatte modellen (`GET /api/tjenester/{id}/modelleksport`), pluss en lenke som åpner samme
  respons i en ny fane. Lastes lazy (kun ved klikk). Ikke et redigeringsverktøy — et
  utviklerorientert innsynsvindu.

### 2.2 Vilkårstre (kobling til byggesteg 4)
- **Rotnode-id** (`tjeneste.rotnodeId`, nullbar) — peker til rot-`Regelnode` i tjenestens
  vilkårstre. Uten den finnes ikke noe kjørbart vilkårstre for tjenesten ennå.
- **Rotnodens tittel** — slått opp separat via rotnodens id (`Regelnode.tittel`), vist som "Rotnode: …".
- Handlinger: opprett en ny rotnode (tittel), bytt til en eksisterende regelnode (`Select` over
  ALLE regelnoder i systemet — ingen filtrering per virksomhet i dag), eller fjern rotnoden.

### 2.3 Egenskaper (hovedredigeringsformen — CPSV-AP-NO-metadata)
Alle felt under er enkeltverdier på selve `Tjeneste`-raden, redigert i én lang, endimensjonal form:

| Felt | Kilde | Type | Notat |
|---|---|---|---|
| Tittel | `tittel` | streng, påkrevd | |
| Beskrivelse | `beskrivelse` | fritekst (flerlinjet) | kort, notat-aktig — se Formål for kontrast |
| Formål | `formal` | fritekst (flerlinjet) | typisk lovens eget «§1 Formål»-avsnitt, BEVISST atskilt fra Beskrivelse |
| Kompetent myndighet | `kompetentMyndighet` | fritekst | |
| Tjenestetype | `tjenestetype` | fritekst | |
| Rettighetstype | `type` | enum, 5 verdier | `myndighetsutovelse / ytelse / infrastruktur / veiledning / medvirkning` |
| Målgruppe | `malgruppe` | liste (kommaseparert i dagens UI) | |
| Kanaler | `kanaler` | liste (kommaseparert) | f.eks. "Nett, Skranke" |
| Kostnad | `kostnad` | fritekst | |
| Behandlingstid | `behandlingstid` | fritekst | |
| Kontaktpunkt | `kontaktpunkt` | fritekst | |
| Konsekvens ved brudd | `konsekvensVedBrudd` | fritekst | |
| Språk | `sprak` | liste (kommaseparert) | f.eks. "nb, en" |
| Livshendelser | `livshendelser` | liste (kommaseparert) | fri tekst i dag, ikke koblet mot noe eksternt vokabular ennå |
| LOS-klassifisering | `losKlassifisering` | fritekst | Digdirs LOS-vokabular — fri tekst i dag (LOS 4 ikke lansert ennå) |
| Tjenesteområde | `tjenesteomrade` | fritekst | f.eks. "Næring, salg og servering" — egen akse fra LOS-klassifisering |

**Merk — feil på siden i dag, verdt å adressere i redesignet:** `output`-feltet
(`TjenesteEntitet.Output`, CPSV `cv:produces` — hva tjenesten faktisk PRODUSERER, f.eks. selve
bevillingen/vedtaket) finnes på entiteten og API-et, men har **ingen** feltinput i dagens
"Egenskaper"-form — `lagre()` sender bare `output: tjeneste.output` uendret. Reelt hull, ikke en
bevisst utelatelse.

### 2.4 Innhold (rettighetens rike, forfattede tekstseksjoner)
Egen, klapp-ut/rediger-seksjon under Egenskaper — atskilt fordi dette er lengre, forfattet
prosa/lister, ikke korte metadatafelt. Nullbar som helhet (`tjeneste.innhold`); hver underseksjon
er også selvstendig nullbar. Alle listefelt redigeres linjeseparert (én rad tekst per linje).

| Seksjon | Underfelt |
|---|---|
| Tidspunkt og frister | (én fritekst) |
| Innsender og tilgang | Hvem kan sende (liste), Innlogging (fritekst) |
| Vedlegg | Vedlegg (liste), Merknad (fritekst) |
| Opplysninger som skal sendes inn | Opplysninger (liste), Merknad (fritekst) |
| Veiledning og utfylling | Veiledningspunkter (liste), Merknad (fritekst) |
| Innsending og oppfølging | Kanal (fritekst), Etter mottak (liste), Merknad (fritekst) |
| Kontakt og hjelp | Generelt (fritekst), Kommunen kan veilede om (liste) |
| Hva rettigheten innebærer | Innledning, Varighet, Plikter (liste), Endringer i virksomheten → Plikt + Eksempler (liste), Krav til drift, Tømmeavtale og kontroll, Rapportering, Kontroll og tilsyn, Avgrensning/merknad — **9 underfelt i én seksjon**, supersett av det to ulike modellerte rettigheter faktisk bruker |

Dette er i praksis **20 tekstfelt** (de fleste flerlinjede) i redigeringsmodus — den klart
tyngste enkeltseksjonen på siden, og en naturlig kandidat for en annen visuell struktur
(faner/akkordeon/trinnvis) i redesignet.

### 2.5 Status
- **Status** (`tjeneste.status`) — én av 6 verdier: `utkast / under_revisjon / validert /
  publisert / tilbaketrukket / arkivert`. Endres direkte via en dropdown, egen seksjon, ingen
  bekreftelse eller forklaring av hva overgangen betyr.

### 2.6 Regelverksreferanser
- **Liste** av koblinger til rettskilde-tekst, gruppert på rettskilde (lov/forskrift/håndbok).
  Hver rad: en lenke til den refererte paragrafen (`{kortnavn} § {nummer} — {overskrift}`, eller
  rå eId når oppslag mislykkes) + en "Fjern"-knapp.
- **Koble ny referanse**-form: velg rettskilde (`Select` over ALLE ~5893 rettskilder — se §5),
  deretter en paragraf-nedtrekk begrenset til den valgte rettskildens blad-noder, ELLER en
  "avansert/manuell eId"-fritekst som alternativ inntastingsvei.
- Denne listen er også inndata til «Foreslå handlinger (KI)» i §2.8 under.

### 2.7 Hendelser
- **Liste** av koblede `Hendelse`-rader (delt/nasjonalt register, samme
  nasjonal/lokal-eierskapsmønster som rettskilder). Hver rad: navn + type-`Tag`
  (`generell`/`livshendelse`/`virksomhetshendelse`) + "Fjern".
- Koble EN eksisterende hendelse (nedtrekk, ekskluderer allerede koblede), ELLER opprette en helt
  ny hendelse (navn + type) og koble den i samme handling.
- **Domenepresisering (viktig for redesign):** koblingen er REN, SYMMETRISK klassifisering — ingen
  lagret retning. To tjenester som deler samme hendelse er «relaterte» uten at én forårsaker den
  andre. Dette skal IKKE forveksles med Tjenesteavhengigheter (§2.9), som ER rettet.

### 2.8 Handlinger
- **Liste** av koblede `Handling`-rader — konkrete, tidsavgrensede interaksjoner (søknad, melding,
  klage …) knyttet til denne rettigheten. Hver rad: navn (lenke til egen `HandlingDetalj`-side),
  handlingstype-`Tag`, «utført av»-`Tag` (valgfri), status-`Tag`.
- **«Foreslå handlinger (KI)»**-knapp — bruker tjenestens EGNE, allerede koblede
  regelverksreferansers rettskilder som KI-kontekst (ingen egen rettskilde-velger denne runden).
  Feiler tydelig hvis ingen regelverksreferanser er koblet ennå.
- **Opprett handling**-miniform: navn, handlingstype (14 gyldige verdier — `soke, endre, si_opp,
  melde, registrere, rapportere, ettersende_dokumentasjon, klage, gi_samtykke, trekke_samtykke,
  be_om_innsyn, bestille, kontrolleres, avslutte, annet`), utført av (`soker / forvaltning /
  tredjepart`, valgfri).
- En `Handling` har SELV et rikt sett underfelt (kanaler, behandlingstid, kostnad, vedlegg,
  veiledningstekst, årsaker, resultat, egen rotnode-override) — disse redigeres på
  `HandlingDetalj`-siden, IKKE her. Denne siden viser bare identifiserende metadata per rad.

### 2.9 Tjenesteavhengigheter
- **Liste** av rettede, årsaksforklarte koblinger til ANDRE tjenester. Hver rad: motpartens navn
  (lenke til `/tjenester/:id` når motparten er en ekte, egen-eid tjeneste — ellers ren tekst for en
  ekstern/plassholder-motpart), org.nr-`Tag` (for eksterne), ↗-lenke (hvis URL finnes),
  nyanse/unntak-`Tag` (valgfri fritekst), "Fjern".
- **Relasjonstyper** (6, kun de tre første har presis betydning i domenemodellen — resten er
  generelle): `forutsetning_for`, `gir_mulighet_til`, `utlost_av` (krever valg av en `Hendelse`
  fra §2.7-registeret — dette er skjæringspunktet mellom Hendelse og Tjenesteavhengighet), `for`,
  `avhengig_av`, `input_til`.
- **Tre ulike måter å velge MOTPART**, alle i samme form:
  1. **Egen virksomhets tjeneste** — nedtrekk over ALLE denne virksomhetens tjenester.
  2. **En annen virksomhets PUBLISERTE tjeneste** — live cross-tenant-søk
     (`GET /api/tjenester/sok-tverr-tenant`, 300ms debounce) med treffliste å velge fra.
  3. **Ekstern, manuell referanse** — organisasjonsnummer + navn + valgfri URL, for tjenester som
     ikke finnes som egen rad i Regel-IDE i det hele tatt (f.eks. hos Mattilsynet/Politiet).
  De tre er gjensidig utelukkende inntastingsmål — å velge én tømmer de andre to.

## 3. Alle linker til andre objekter

### 3.1 Utgående (fra Tjeneste-siden til et annet objekt)

| Lenke/relasjon | Til objekt | Kardinalitet | Vist som |
|---|---|---|---|
| Rotnode | `Regelnode` (vilkårstre-rot) | 1 (nullbar) | "Åpne vilkårstre →"-lenke til `/vilkarstre/:rotnodeId` |
| Veiledning | Live-generert veiledningsvisning av SAMME vilkårstre | 1 (avledet av rotnode) | "Åpne veiledning →"-lenke til `/tjenester/:id/veiledning` |
| Regelverksreferanse | `RettskildeNode` (paragraf/side i en rettskilde) | 0..n | Lenke til `/rettskilder/:id#...` (via `rettskildeLenke`) |
| Hendelse | `Hendelse` (delt register) | 0..n | Ren visning (navn + type-`Tag`), ingen egen detaljside å lenke til |
| Handling | `Handling` (eid av DENNE tjenesten) | 0..n | Lenke til `/tjenester/:tjenesteId/handlinger/:handlingId` |
| Tjenesteavhengighet → egen tjeneste | En ANNEN `Tjeneste`, samme virksomhet | 0..n | Lenke til `/tjenester/:id` |
| Tjenesteavhengighet → cross-tenant tjeneste | En ANNEN virksomhets publiserte `Tjeneste` | 0..n | Lenke til `/tjenester/:id` (samme rute, annen eier) |
| Tjenesteavhengighet → ekstern | Ingen ekte rad (kun org.nr/navn/URL) | 0..n | Ekstern URL (ny fane) hvis oppgitt, ellers ingen lenke |
| Tjenesteavhengighet → hendelse | `Hendelse` | 0..1 per avhengighet (kun for `utlost_av`) | Kun navn i `Tag`, ingen lenke |
| JSON-modelleksport | Rå API-respons | 1 | Ny fane til `GET /api/tjenester/:id/modelleksport` |

### 3.2 Inngående (hvem lenker TIL denne siden)

| Fra side | Kontekst |
|---|---|
| `TjenesterListe.tsx` | Hovedlisten over virksomhetens tjenester |
| `TjenesteforslagKo.tsx` | «Identifiser tjenester» — KI-forslagskø, tjenesten er «foreslått av KI» før godkjenning |
| `VilkarstreListe.tsx` | Fra en regelnode-liste, tilbake til tjenesten den er rotnode for |
| `RettskildeDetalj.tsx` | En rettskilde-nodes «referert av tjenester»-liste (baklengs av §3.1s regelverksreferanse-lenke) |
| `HandlingDetalj.tsx` | Tilbake-lenke til eiende tjeneste |
| `HandlingerListe.tsx` | Toppnivå handlings-liste, med lenke til tjenesten hver handling hører til |

### 3.3 Objekter denne siden LESER, men ikke lenker direkte til
- **Alle rettskilder** (`GET /api/rettskilder`, i dag ~5893 rader) — brukt som kandidatliste i
  "Koble referanse"-formen (§2.6). Se §5 — dette er en rå `<Select>` med ALLE radene, samme
  skala-problem som ble løst med `Suggestion`-komponenten andre steder i appen (docs/09 §10),
  IKKE løst her ennå.
- **Alle regelnoder** (`GET /api/regelnoder`) — kandidatliste for "Bytt rotnode" (§2.2), samme
  rå `<Select>`-mønster.
- **Alle egne tjenester** (`GET /api/tjenester`) — kandidatliste for "Til tjeneste (egen
  virksomhet)" i §2.9.
- **Alle hendelser** (`GET /api/hendelser`) — kandidatliste for både §2.7 og "Hendelse"-valget i §2.9.

## 4. Nærliggende, men BEVISST separate sider (ikke del av denne siden)

- **`HandlingDetalj.tsx`** — én handling sine egne rike underfelt (kanaler, vedlegg,
  behandlingstid, veiledningstekst, årsaker, resultat, egen rotnode-override). Denne siden viser
  kun identifiserende metadata per handling (§2.8).
- **`TjenesteVeiledning.tsx`** — presentasjonsklar, lineær visning av SAMME vilkårstre for en
  sluttbruker/saksbehandler, vevd sammen med kommunale/nasjonale datasett-verdier og
  veiledningskommentarer. Ikke et redigeringsverktøy.
- **`VilkarstreDetalj.tsx`** (nås via "Åpne vilkårstre →") — selve vilkårs-/regeltreet
  (Lag 2-editoren, jf. README) som avgjør et faktisk vedtak. Denne siden eier kun KOBLINGEN
  (rotnode-id), ikke selve treet.

## 5. Skala-fakta relevante for redesignet

- **Rettskilder:** ~5893 rader i dag. `TjenesteDetalj.tsx`s "Koble referanse"-rettskildevelger
  (§2.6) og "Bytt rotnode"-regelnodevelger (§2.2) er FORTSATT rå `<Select>`-er over hele
  datasettet — samme mønster som nettopp ble byttet ut andre steder i appen med en søkbar
  `Suggestion`-komponent (se `docs/09-design-konvensjoner.md` §10/§10.1 for den løsningen og
  hvorfor den var nødvendig). Verdt å vurdere samme fiks her.
- **Egne tjenester:** typisk et lite, men veksende antall (denne virksomhetens egne rader) — ikke
  samme skala-problem som rettskilder ennå, men "Til tjeneste (egen virksomhet)"-nedtrekket i
  §2.9 vil vokse over tid.
- **Handlinger:** kan være mange PER tjeneste etter Oppgaveregister-seeding (docs/21) — flere
  hundre handlinger totalt i systemet i dag, fordelt på tjenester. §2.8s handlingsliste er i dag
  en flat, upaginert liste — samme type skala-risiko som er løst andre steder med paginering
  (docs/09 §9) hvis en enkelt tjeneste får svært mange handlinger.

## 6. Observerte strukturelle egenskaper ved dagens side (ikke forslag — bare fakta)

- Siden er **én lang, vertikal sekvens av 8 seksjoner** (Vilkårstre, Egenskaper, Innhold, Status,
  Regelverksreferanser, Hendelser, Handlinger, Tjenesteavhengigheter) — ingen faner, ingen
  sammenklappet standardtilstand utover Innhold og JSON-eksport.
- **Egenskaper** (§2.3, 15 felt) og **Innhold** (§2.4, 20 felt) er begge lange, endimensjonale
  formularer — til sammen 35 tekstfelt før man når noen relasjon til et annet objekt.
- **Tre av seksjonene** (Regelverksreferanser, Hendelser, Tjenesteavhengigheter) følger samme
  gjentatte mønster: liste av eksisterende koblinger → inline "opprett ny kobling"-miniform rett
  under. Handlinger følger nesten det samme mønsteret, men lenker videre til en egen side i
  stedet for å redigere inline.
- **Tjenesteavhengigheter** (§2.9) har den mest sammensatte enkelt-formen på siden — tre
  gjensidig utelukkende motpart-inntastingsmåter i én form.
- Ingen brødsmulesti, ingen sammenhengende "hvor er jeg i arbeidsflyten"-indikator utover
  Status-taggen i toppen (kjent, generelt UX-gap — docs/09 §0 "Kjente UX-mangler").
