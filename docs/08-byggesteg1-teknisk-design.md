# Teknisk design — Byggesteg 1 (Rettskildebibliotek)

*Dekker AKN-skjema, databasetabeller og konverteringspipeline (punkt 1–3 fra behovet identifisert i chatten). Frontend-komponentstruktur (punkt 4) er bevisst utelatt her — overlatt til samme verktøy som produserte den opprinnelige interaktive prototypen (`Regel-IDE.dc.html`). Dette dokumentet er **underlag til ekstern kvalitetssikring** — ingen kode er skrevet mot det ennå.*

*Status: første eksterne kvalitetssikringsrunde er gjennomført og innarbeidet (2026-07). Viktigste endring: eId-strategien er lagt om fra "Lovdatas HTML-id direkte" til "ELI som kanonisk rot" (§1.2). §5 viser fullstendig status — hva som er avklart og hva som fortsatt er åpent til neste runde.*

Basert på ekte kildedata: `data/kilder/raw-lovdata/alkoholloven-LOV-1989-06-02-27.html` og `.../alkoholforskriften-FOR-2005-06-08-538.html` (se `data/kilder/README.md` for proveniens og strukturell råmapping).

## 1. AKN-skjema

### 1.1 Toppnivå og FRBR-metadata

Akoma Ntoso (OASIS LegalDocML) krever en FRBR-basert metadatablokk (Work/Expression/Manifestation) i tillegg til selve teksten. Utledet fra alkohollovens Lovdata-header (`legacyID`, `dokid`, `dateInForce`, `ministry`), **og rettet etter ekstern kvalitetssikring på to punkter (FRBRauthor, konsolideringsdato):**

**FRBRauthor er nå kildetype-avhengig, ikke alltid departementet.** For en **lov** er den lovgivende myndigheten Stortinget, ikke fagdepartementet — departementet er ansvarlig for oppfølging/forslag, ikke vedtakelse. For en **forskrift** derimot er departementet (eller "Kongen i statsråd") ofte selve forskriftsgiveren under delegert hjemmel, så der er departement-som-`FRBRauthor` riktig. Regelen: `Lov → Stortinget`, `Forskrift → utstedende myndighet (Lovdatas ministry-felt)`, `Rundskriv/Virksomhetsdokument → utstedende etat`. Ansvarlig fagdepartement beholdes uansett som en egen referanse (ikke fjernet, bare ikke lenger forvekslet med lovgiver).

**Konsolideringsdato er nå eksplisitt definert:** `FRBRExpression/FRBRdate[@name='konsolidering']` = Lovdatas *"Ikrafttredelse av siste endring"* (header-feltet `lastChangeInForce`, her `2026-07-20`) — **ikke** importtidspunktet. `FRBRManifestation/FRBRdate[@name='regel-ide-import']` = når regel-IDE faktisk hentet/konverterte dokumentet (her `2026-07-23`). To ulike datoer med ulik betydning, ikke samme dato gjentatt.

```xml
<akomaNtoso xmlns="http://docs.oasis-open.org/legaldocml/ns/akn/3.0">
  <act name="lov">
    <meta>
      <identification source="#regel-ide">
        <FRBRWork>
          <FRBRthis value="/akn/no/act/1989-06-02/27"/>
          <FRBRuri value="/akn/no/act/1989-06-02/27"/>
          <FRBRdate date="1989-06-02" name="vedtakelse"/>
          <FRBRauthor href="#stortinget"/>  <!-- lov: lovgivende myndighet, IKKE departementet -->
          <FRBRcountry value="no"/>
        </FRBRWork>
        <FRBRExpression>
          <FRBRthis value="/akn/no/act/1989-06-02/27/nor@2026-07-20"/>
          <FRBRuri value="/akn/no/act/1989-06-02/27/nor@2026-07-20"/>
          <FRBRdate date="2026-07-20" name="konsolidering"/>  <!-- = Lovdatas "Ikrafttredelse av siste endring" -->
          <FRBRauthor href="#lovdata"/>
          <FRBRlanguage language="nor"/>
        </FRBRExpression>
        <FRBRManifestation>
          <FRBRthis value="/akn/no/act/1989-06-02/27/nor@2026-07-20.xml"/>
          <FRBRuri value="/akn/no/act/1989-06-02/27/nor@2026-07-20.xml"/>
          <FRBRdate date="2026-07-23" name="regel-ide-import"/>  <!-- = når VI hentet/konverterte, ikke konsolideringsdato -->
          <FRBRauthor href="#regel-ide"/>
        </FRBRManifestation>
      </identification>
      <references source="#regel-ide">
        <TLCOrganization eId="stortinget" href="/ontology/organization/no/stortinget" showAs="Stortinget"/>
        <TLCOrganization eId="helse-og-omsorgsdepartementet" href="/ontology/organization/no/hod" showAs="Helse- og omsorgsdepartementet"/>
        <TLCOrganization eId="lovdata" href="/ontology/organization/no/lovdata" showAs="Lovdata"/>
      </references>
      <proprietary source="#regel-ide">
        <regelIde:eli>https://lovdata.no/eli/lov/1989/06/02/27/nor</regelIde:eli>  <!-- verifisert ELI-URI, se §1.2 -->
        <regelIde:kildetype>Lov</regelIde:kildetype>
        <regelIde:status>Gjeldende</regelIde:status>
        <regelIde:ansvarligDepartement>Helse- og omsorgsdepartementet</regelIde:ansvarligDepartement>
      </proprietary>
    </meta>
    <preface>
      <p>Lov om omsetning av alkoholholdig drikk m.v. (alkoholloven)</p>
    </preface>
    <body>
      <!-- se §1.2 -->
    </body>
  </act>
</akomaNtoso>
```

`<proprietary>` bærer regel-IDEs egne felt (`03-domenemodell.md` §1.1: `doctype`, `kildetype`, `status` …) som ikke har noen standard AKN-motpart — dette er AKNs offisielle utvidelsesmekanisme, ikke en omgåelse.

### 1.2 eId-konvensjon — revidert: ELI som kanonisk rot, ikke Lovdatas HTML-id

**Endret etter ekstern kvalitetssikring.** Det opprinnelige forslaget (gjenbruk Lovdatas HTML-`id` direkte, f.eks. `kapittel-1-paragraf-1`) ble utfordret på ett presist punkt: Lovdatas HTML-`id` er en **implementasjonsdetalj ved rendring av nettsiden deres** — endrer Lovdata malen sin, endrer den seg, med brutte referanser/annotasjoner/URI-er som konsekvens. Løsningen er ikke å finne opp vårt eget identifikatorrom (samme risiko, bare vår egen versjon av den), men å **forankre i en identifikator som allerede er en ekstern standard**: **ELI (European Legislation Identifier)**, som `digital-rettsstat`s eget rammeverk allerede peker på som ryggraden for Kildelaget, og som Lovdata selv publiserer parallelt med HTML-siden sin.

**Verifisert direkte (ikke antatt):** `https://lovdata.no/eli/lov/1989/06/02/27` løser til alkoholloven, med realisert URI `/eli/lov/1989/06/02/27/nor` — dette er en solid, ekstern, stabil identifikator på **lovnivå**, uavhengig av Lovdatas HTML-rendring.

**Ikke verifisert:** paragraf-/ledd-nivå ELI-adressering. Et søk fant et eksempel for straffeloven (`/eli/lov/2005/05/20/28/section/152`), men to direkte forsøk på tilsvarende URI for alkohollovens § 1-1 (`/eli/lov/1989/06/02/27/section/1-1` i to skrivemåter) løste **ikke** til en paragrafspesifikk side — de falt tilbake til lov-nivå. Enten er URI-formatet et annet enn antatt, eller Lovdata har ikke publisert seksjonsnivå-ELI for denne loven. **Dette er selve punktet den eksterne AKN/ELI-kompetansen bør avklare** (§5) — ikke noe jeg kan bekrefte fra websøk alene.

**Foreslått konvensjon i mellomtiden — bygger på det verifiserte lovnivå-URI-et, utvidet med selve §-nummeret (den faktiske juridiske sitatformen), ikke Lovdatas HTML-struktur:**

| Nivå | `eId` | Kilde |
|---|---|---|
| Rettskilde (lovnivå) | `https://lovdata.no/eli/lov/1989/06/02/27/nor` | **Verifisert ELI-URI** |
| Kapittel | `kap-1` | Regel-IDE-lokal — kapitler siteres ikke selvstendig i norsk juridisk praksis, trenger ingen ekstern standard |
| Paragraf | `{lov-eli}/§1-1` | §-nummeret er selve den juridiske sitatformen (ikke en Lovdata-renderingsdetalj) — samme semantiske identifikator som Lovdatas `data-lovdata-URL="NL/lov/1989-06-02-27/§1-1"` allerede koder, bare med ELI-rot i stedet for `NL/`-prefiks |
| Ledd | `{paragraf-eId}/ledd-1` | Regel-IDE-lokal utvidelse — ELI-spesifikasjonen adresserer ikke bekreftet under seksjonsnivå |
| Punkt | `{ledd-eId}/punkt-1` | Regel-IDE-lokal utvidelse, samme grunn |

**Lovdatas HTML-`id` (f.eks. `kapittel-1-paragraf-1-ledd-1`) forkastes ikke** — den lagres som en egen `kilde_id`-kolonne (§2) for sporbarhet tilbake til det konkrete parset dokumentet, men er **ikke lenger systemets kanoniske identitet**. Det er nøyaktig skillet den eksterne vurderingen etterlyste: kildeidentifikator (Lovdata, kan endre seg) atskilt fra kanonisk identifikator (ELI-forankret, ekstern standard).

`03-domenemodell.md`s eksempel `par_1-7b` (fra førsteutkastet) reflekterer verken den gamle eller den nye konvensjonen presist — det var en antatt form før vi hadde ekte data. Rettes i domenemodellen når ELI-spørsmålet i §5 er avklart.

### 1.3 Fullt eksempel, bygget fra ekte tekst (§ 1-1 og § 1-3)

```xml
<chapter eId="kap-1">
  <num>Kapittel 1.</num>
  <heading>Alminnelige bestemmelser.</heading>

  <article eId="https://lovdata.no/eli/lov/1989/06/02/27/nor/§1-1" kildeId="kapittel-1-paragraf-1">
    <num>§ 1-1</num>
    <heading>Lovens formål.</heading>
    <paragraph eId="https://lovdata.no/eli/lov/1989/06/02/27/nor/§1-1/ledd-1" kildeId="kapittel-1-paragraf-1-ledd-1">
      <content>
        <p>Reguleringen av innførsel og omsetning av alkoholholdig drikk etter denne lov har som mål å
        begrense i størst mulig utstrekning de samfunnsmessige og individuelle skader som alkoholbruk
        kan innebære. Som et ledd i dette sikter loven på å begrense forbruket av alkoholholdige
        drikkevarer.</p>
      </content>
    </paragraph>
  </article>

  <article eId="https://lovdata.no/eli/lov/1989/06/02/27/nor/§1-3" kildeId="kapittel-1-paragraf-3">
    <num>§ 1-3</num>
    <heading>Definisjoner</heading>
    <paragraph eId="https://lovdata.no/eli/lov/1989/06/02/27/nor/§1-3/ledd-1" kildeId="kapittel-1-paragraf-3-ledd-1">
      <content>
        <p>I denne lov brukes alkoholholdig drikk som fellesbetegnelse på drikker som inneholder mer
        enn 2,5 volumprosent alkohol, likevel slik at aldersgrensebestemmelsen i
        <ref href="#https://lovdata.no/eli/lov/1989/06/02/27/nor/§1-5">§ 1-5</ref> også får anvendelse på drikk mellom 0,7 og 2,5
        volumprosent alkohol.</p>
      </content>
    </paragraph>
    <paragraph eId="https://lovdata.no/eli/lov/1989/06/02/27/nor/§1-3/ledd-2" kildeId="kapittel-1-paragraf-3-ledd-2">
      <content><p>I denne loven betyr:</p></content>
      <list>
        <point eId="https://lovdata.no/eli/lov/1989/06/02/27/nor/§1-3/ledd-2/punkt-1" kildeId="kapittel-1-paragraf-3-ledd-2-punkt-1">
          <content><p>alkoholfri drikk: drikk som inneholder under 0,7 volumprosent alkohol</p></content>
        </point>
        <!-- flere punkt … -->
      </list>
    </paragraph>
  </article>
</chapter>
```

`kildeId` (ikke en standard AKN-attributt — regel-IDE-lokal, jf. `<proprietary>`-mønsteret i §1.1) bærer Lovdatas opprinnelige HTML-`id` for sporbarhet, uten å være den kanoniske identiteten (§1.2). Merk `<ref href="#…/§1-5">` — den interne kryssreferansen fra Lovdatas `<a href="lov/1989-06-02-27/§1-5">` er bevart som en AKN-intern lenke, ikke fjernet. Dette er nøyaktig den typen henvisning `06-veikart.md` byggesteg 1 begrunner hvorfor hele loven må lastes inn (§ 1-3 refererer § 1-5, som ligger utenfor "de relevante kapitlene" i den opprinnelige, snevrere planen).

## 2. Databasetabeller

*Kolonnetyper er PostgreSQL-stil (`uuid`, `text`, `timestamptz`, `jsonb`) som en arbeidsantagelse — selve databasevalget er ikke låst (`05-arkitektur-og-nfk.md` §1). Feltnavn er norske, i tråd med resten av modellen og `forklaringsmodell-api`s konvensjon.*

```sql
-- Rettskilden selv: metadata + kanonisk AKN-XML som autoritativ kilde
CREATE TABLE rettskilder (
  id                uuid PRIMARY KEY,
  doctype           text NOT NULL,        -- 'act' | 'doc' | 'judgment' | 'internal'
  kildetype         text NOT NULL,        -- 'Lov' | 'Forskrift' | 'Rundskriv' | 'Presedens' | 'Virksomhetsdokument'
  importrolle       text NOT NULL DEFAULT 'primaer',  -- 'primaer' | 'referanse' — se §3.1 steg 6
  tittel            text NOT NULL,
  kortnavn          text,
  eli               text,                 -- f.eks. 'LOV-1989-06-02-27'
  akn_xml           text,                 -- NULL for referanse-stubber (kun metadata, ikke hentet ennå); NOT NULL når importrolle='primaer' eller stubben er forfremmet
  ikrafttredelse    date,
  konsolidert_dato  date,
  utgiver           text,                 -- f.eks. 'Lovdata' — NLOD 2.0-attribusjon, se 05-arkitektur-og-nfk §1.1
  status            text NOT NULL,        -- 'Gjeldende' | 'Opphevet' | 'Utkast'
  versjon           int NOT NULL DEFAULT 1,
  entitetsstatus    text NOT NULL DEFAULT 'gjeldende',  -- felles basemetadata, 03-domenemodell §0
  erstatter_id      uuid REFERENCES rettskilder(id),
  gyldig_fra        date,
  gyldig_til        date,
  opprettet_av      text NOT NULL,
  opprettet_tidspunkt timestamptz NOT NULL DEFAULT now(),
  sist_endret_av    text,
  sist_endret_tidspunkt timestamptz,
  CHECK (importrolle IN ('primaer', 'referanse')),
  CHECK (importrolle = 'referanse' OR akn_xml IS NOT NULL)  -- primaer-kilder skal alltid ha fullt innhold
);

-- Denormalisert projeksjon av AKN-treet, for navigasjon/søk/tagging-join uten å parse XML per kall.
-- rettskilde_noder er ALLTID en materialisert projeksjon — ALDRI en autoritativ kilde.
-- Regenereres synkront (samme transaksjon) fra rettskilder.akn_xml ved hver import/endring — aldri redigert direkte.
-- (Bekreftet riktig avveining i ekstern kvalitetssikring: konsistens > et par ekstra ms ved import.)
CREATE TABLE rettskilde_noder (
  id                uuid PRIMARY KEY,
  rettskilde_id     uuid NOT NULL REFERENCES rettskilder(id) ON DELETE CASCADE,
  eid               text NOT NULL,        -- kanonisk, ELI-forankret identitet, §1.2 — f.eks. '…/§1-1/ledd-1'
  kilde_id          text NOT NULL,        -- Lovdatas opprinnelige HTML-id, KUN for sporbarhet/parsing-korrelasjon (§1.2) — ikke kanonisk
  parent_node_id    uuid REFERENCES rettskilde_noder(id),
  node_type         text NOT NULL,        -- 'kapittel' | 'underinndeling' | 'paragraf' | 'ledd' | 'punkt'
  nummer            text,                 -- f.eks. '§ 1-1', 'Kapittel 1.'
  overskrift        text,
  tekst             text,                 -- kun for ledd/punkt-noder (bladtekst)
  tekst_hash        text,                 -- sha256(tekst) — for versjonsdiff og tag-integritet, se §2.1
  sorteringsrekkefolge int NOT NULL,
  UNIQUE (rettskilde_id, eid)
);
CREATE INDEX ix_rettskilde_noder_parent ON rettskilde_noder(parent_node_id);
CREATE INDEX ix_rettskilde_noder_tekst_fts ON rettskilde_noder USING gin(to_tsvector('norwegian', tekst));
CREATE INDEX ix_rettskilde_noder_eid_hash ON rettskilde_noder(eid, tekst_hash);  -- for versjonssammenligning, §2.1

-- Interne kryssreferanser innenfor/på tvers av rettskilder (fra <ref href="#...">)
CREATE TABLE rettskilde_referanser (
  id                uuid PRIMARY KEY,
  fra_node_id       uuid NOT NULL REFERENCES rettskilde_noder(id) ON DELETE CASCADE,
  til_rettskilde_id uuid NOT NULL REFERENCES rettskilder(id),
  til_eid           text NOT NULL         -- målnodens eid, kan være i en annen rettskilde (f.eks. forskrift → lov)
);

-- Tekst-tag: kobler en tekstflate til begrep/vilkår/regel (03-domenemodell §1.2)
CREATE TABLE tekst_tagger (
  id                uuid PRIMARY KEY,
  rettskilde_id     uuid NOT NULL REFERENCES rettskilder(id) ON DELETE CASCADE,
  node_eid          text NOT NULL,        -- hvilken ledd/punkt-node taggen ligger i
  start_offset      int NOT NULL,
  end_offset        int NOT NULL,
  quote_prefix      text NOT NULL,        -- quoteSelector: kontekst før (W3C Web Annotation-mønster)
  quote_exact       text NOT NULL,        -- selve det taggede sitatet
  quote_suffix      text NOT NULL,        -- kontekst etter
  node_tekst_hash   text NOT NULL,        -- rettskilde_noder.tekst_hash PÅ TAGGETIDSPUNKTET — for å oppdage at noden er endret siden (§2.1)
  kind              text NOT NULL,        -- 'begrep' | 'vilkar' | 'regel'
  ref_id            uuid,                 -- peker til begrep/vilkår/regelnode/unntak — nullable inntil byggesteg 2/4
  entitetsstatus    text NOT NULL DEFAULT 'gjeldende',
  opprettet_av      text NOT NULL,
  opprettet_tidspunkt timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_tekst_tagger_node ON tekst_tagger(rettskilde_id, node_eid);

-- Proveniens (append-only, 03-domenemodell §1.14) — delt av alle entitetstyper, ikke bare rettskilder
CREATE TABLE proveniens (
  id                uuid PRIMARY KEY,
  entitet_type      text NOT NULL,        -- 'rettskilde' | 'begrep' | 'vilkar' | 'regelnode' | 'unntak' | …
  entitet_id        uuid NOT NULL,
  endret_av         text NOT NULL,
  dato              timestamptz NOT NULL DEFAULT now(),
  handling          text NOT NULL,        -- 'opprettet' | 'endret' | 'foreslatt_av_ai' | 'validert' | 'publisert' | 'arkivert'
  kilde_referanser  jsonb,                -- liste av eId/rundskriv-avsnitt/presedens-noder
  ai_forslag_versjon text,
  godkjent_av       text
);
CREATE INDEX ix_proveniens_entitet ON proveniens(entitet_type, entitet_id);
```

**Design-avveining, bekreftet i ekstern kvalitetssikring:** `rettskilder.akn_xml` og `rettskilde_noder` er bevisst redundante — XML-en er autoritativ, node-tabellen er en regenererbar projeksjon for ytelse (unngå XML-parsing på hvert navigasjons-/søkekall, jf. ytelseskravet i `05-arkitektur-og-nfk.md` §2). Enhver skriveoperasjon på en rettskilde regenererer `rettskilde_noder` i **samme transaksjon** — synkront, ikke lat/asynkront. Vurdert og bekreftet: konsistens er viktigere enn noen få ekstra millisekunder ved import, og lat/async regenerering ville gitt et konsistensvindu ingen konkret gevinst her rettferdiggjør.

### 2.1 Versjonering — dokumentnivå, ikke nodenivå

**Empirisk avklart (ikke antatt):** en norsk lov publiseres på nytt som et **helt, konsolidert dokument** når den endres — Lovdata-headeren har ett `lastChangeInForce`-felt for *hele* loven (her `2026-07-20`), ikke separate endringstidspunkt per paragraf. `digital-rettsstat/docs/07-standarder-og-sporbarhetskjeden.md` beskriver samme "point-in-time"-modell fra Storbritannia. **Konsekvens: det innføres ingen separat versjonsteller på nodenivå.** Hver ny konsolidert versjon av en rettskilde er en helt ny `rettskilder`-rad (`erstatter_id`-kjeden, `03-domenemodell.md` §0) med et helt nytt sett `rettskilde_noder`-rader — ikke en inkrementell oppdatering av eksisterende noder.

Dette etterlater likevel et reelt spørsmål den eksterne vurderingen traff riktig: hvordan overlever en tagg (`tekst_tagger`) på et **uendret** avsnitt når *resten* av loven får en ny versjon? Løsning: `tekst_hash` (sha256 av `tekst`) på hver node, og `node_tekst_hash` lagret på hver tag *da den ble opprettet*.

- Ved import av en ny versjon av en rettskilde: for hver ny node, slå opp forrige versjons node med samme `eid`. Er `tekst_hash` uendret → paragrafen er ordrett uendret → tagger med `node_tekst_hash` lik gjeldende hash regnes fortsatt som gyldige og kan trygt forbli koblet (samme `eid`, ny `rettskilde_id`).
- Er `tekst_hash` endret (eller noden borte/flyttet) → taggen flagges for manuell gjennomgang (jf. AK-3.3.-serien) i stedet for å stille bli stående mot en tekst den ikke lenger stemmer med.

Dette krever ingen egen nodeversjonstabell — kombinasjonen "ny rettskilde-versjon = nytt sett noder" + "tekst_hash for å oppdage hva som faktisk endret seg" er tilstrekkelig, og unngår kompleksiteten en fullverdig nodenivå-versjonshistorikk ville innført.

## 3. Konverteringspipeline (Lovdata-HTML → AKN)

**Viktig presisering fra forrige runde:** vi anbefalte tidligere "AI-assistert AKN-konvertering" generelt (`05-arkitektur-og-nfk.md` §1.1). Etter å faktisk ha sett Lovdata-dataen, bør det presiseres: **for Lovdata-kildet innhold er dette en deterministisk, regelbasert transformasjon — ikke en LLM-oppgave.** Strukturen (`section`/`legalArticle`/`legalP`/`data-lovdata-URL`) er allerede fullt maskinlesbar; en LLM ville innføre unødvendig ikke-determinisme i noe som kan løses med en ren HTML-parser. **AI-assistert konvertering forbeholdes uploaded, ustrukturerte dokumenter** (rundskriv-PDF, virksomhetsdokumenter) i AK-3.3.6/3.3.7 — der finnes ingen `data-lovdata-URL`-ekvivalent, og strukturgjenkjenning er en reell tolkningsoppgave.

### 3.1 Steg (Lovdata-kildet import)

1. **Hent** — `GET api.lovdata.no/…` (når API-nøkkel er på plass) eller les fra allerede hentet fil (`data/kilder/raw-lovdata/`, midlertidig for byggesteg 1).
2. **Dekod** — `cp1252` → UTF-8 (kritisk, se `data/kilder/README.md` — feil koding korrumperer æøå stille, ingen feilmelding).
3. **Parse HTML** til DOM (HTML-parserbibliotek, ikke regex — dokumentet er nøstet og har uregelmessig innhold som fotnoter og `changesToParent`).
4. **Ekstraher dokumentmetadata** fra `<header class="documentHeader">` (`legacyID`, `dokid`, `dateInForce`, `ministry`, `title`, `titleShort`) → `rettskilder`-rad + FRBR-blokk (§1.1).
5. **Vandre `<main class="documentBody">`** — for hver node: bygg `eid` fra ELI-roten + §-nummer/ledd-/punkt-indeks (§1.2), sett `kilde_id` = elementets Lovdata-`id`-attributt (bevart for sporbarhet, ikke kanonisk), og beregn `tekst_hash = sha256(tekst)` for ledd/punkt-noder (§2.1):
   - `<section class="section">` → `rettskilde_noder`-rad, `node_type='kapittel'`.
   - `<article class="legalArticle">` → `node_type='paragraf'`, `nummer` fra `legalArticleValue`, `overskrift` fra `legalArticleTitle`.
   - Nøstet `<article class="legalP">` → `node_type='ledd'`, `tekst` = elementets tekstinnhold **med** `<a href="lov/…">`-elementer bevart som markører (ikke bare strippet til plain text) — disse blir `rettskilde_referanser`-rader (steg 6).
   - `<li><article class="listArticle">` → `node_type='punkt'`.
   - `<article class="changesToParent">` → **ikke** en tekstnode. Skriv i stedet en `proveniens`-rad (`handling='endret'`, `kilde_referanser` = lenkene i elementet).
6. **Kryssreferanser — ekstern kilde importeres automatisk som stub, avgrenset (ett hopp, ikke transitivt).** For hver `<a href="lov/…">`/`<a href="forskrift/…">` funnet **inni selve løpeteksten** (en ledd- eller punkt-node — **ikke** header-metadata som «Endrer»/«Sist endret ved»/EØS-henvisninger, som er lovhistorikk, ikke normativt innhold loven bruker):
   - Er `ÅÅÅÅ-MM-DD-N` samme rettskilde? → intern referanse, `til_eid` i samme dokument.
   - Finnes `ÅÅÅÅ-MM-DD-N` allerede i biblioteket? → opprett `rettskilde_referanser`-rad mot den.
   - Finnes den ikke? → **opprett en referanse-stub**: ny `rettskilder`-rad med `importrolle='referanse'`, `akn_xml=null`, kun metadata hentet fra Lovdatas dokumentheader (tittel, ELI, status, kildetype) — deretter `rettskilde_referanser`-raden. Stubben **følges ikke videre** (dens egne referanser importeres ikke rekursivt — det ville gitt uavgrenset transitiv import, se § 5). En referanse-stub kan senere forfremmes til `importrolle='primaer'` (en eksplisitt brukerhandling), som trigger full AKN-henting/-konvertering av den (steg 1–5 kjøres da for stubben).
7. **Generer kanonisk AKN-XML** fra det bygde treet (§1) og skriv `rettskilder.akn_xml`.
8. **Skriv `rettskilde_noder`** fra samme tre, i én transaksjon med steg 7 (se avveiningen i §2).
9. **Status:** `utkast`.
10. **Menneskelig verifisering** (jurist/fagansvarlig, jf. livssyklusen `03-domenemodell.md` §3.2): sammenlign generert AKN mot kildeteksten side-ved-side (AK-3.3.6) — her er oppgaven å fange parserfeil (uvanlig nøsting, `(Opphevet)`-paragrafer, fotnoter), ikke å vurdere en AI-tolkning. Godkjent → `gjeldende`.

### 3.2 Kjente vanskelige tilfeller (funnet i den ekte teksten, ikke antatt)

- **`(Opphevet)`-paragrafer** (f.eks. § 1-12, § 1-13, § 3-5, § 3-6, § 5-1, § 8-7, § 8-10 i alkoholloven) — har ingen `legalP`-innhold. **Rettet etter ekstern kvalitetssikring:** ikke en oppfunnet `status="opphevet"`-attributt — AKN har en reell, standardisert mekanisme for dette via temporal-gruppens `start`/`end`-attributter: en artikkel som har `end` uten tilhørende `start` markerer en bestemmelse som var del av originaldokumentet, men er opphevet før eller ved siste registrerte versjon. Parseren skal altså sette `end`-dato (fra Lovdatas opphevelsesinformasjon der den finnes) på et gyldig, tomt `<article>` — ikke krasje, ikke hoppe over, og ikke en egen regel-IDE-oppfunnet attributt. **Fortsatt flagget til §5** — nøyaktig hvilken `start`/`end`/`status`-kombinasjon som er riktig AKN-konvensjon her bør bekreftes av AKN-kompetent hold, ikke bare utledes fra ett søkeresultat.
- **Fotnoter** (`footnote`/`footnotereference`/`footnotes`-klasser) — separate fra hovedteksten; foreslått: egen AKN `<authorialNote>`, ikke inline i `<p>`.
- **Romertall-underinndelinger** (f.eks. kapittel 3 har "I.", "II.", "III." som underoverskrifter mellom paragrafer, ikke egne `<section>`) — **rettet etter ekstern kvalitetssikring:** dette er reell dokumentstruktur (en gruppe paragrafer hører sammen under romertallet), ikke bare et overskriftsfelt å gjemme unna på artikkelnivå. AKN har to kandidater: det standardiserte hierarkiske elementet `<subchapter>` (kapittel → subchapter → article, hvis det passer AKNs skjemakrav for norsk lovstruktur), eller det generiske `<hcontainer>` (eksplisitt ment for "et element regelverket trenger, men som ikke finnes blant AKNs faste hierarkiske elementer"). **Fortsatt flagget til §5** — hvilket av de to som er riktig valg bør bekreftes av AKN-kompetent hold.

## 4. Hva som er ute av scope for dette designet

- Faktisk parser-/serialiseringskode (dette er et design, ikke en implementasjon).
- API-nøkkel-registrering hos Lovdata for de strukturerte endepunktene (`05-arkitektur-og-nfk.md` §1.1) — bulk-filene dekker byggesteg 1s behov i mellomtiden.
- Frontend — se innledningen.

## 5. Til ekstern kvalitetssikring — status etter første runde

*Første runde ekstern kvalitetssikring er gjennomført (2026-07). Denne seksjonen er oppdatert med resultatet — fortsatt åpne punkter er tydelig merket, avklarte punkter er beholdt for sporbarhet, ikke fjernet.*

1. **eId-konvensjon — delvis avklart, ett residual-spørsmål.** Endret fra "gjenbruk Lovdatas HTML-id" til "ELI som kanonisk rot + §-nummer/ledd/punkt-utvidelse" (§1.2). Lovnivå-ELI er verifisert direkte (`https://lovdata.no/eli/lov/1989/06/02/27` løser korrekt). **Fortsatt åpent:** paragraf-/ledd-nivå ELI-adressering er *ikke* verifisert for alkoholloven — to direkte forsøk falt tilbake til lovnivå. AKN/ELI-kompetent hold bør bekrefte om Lovdata faktisk publiserer seksjonsnivå-ELI konsekvent, og i så fall nøyaktig URI-format for sammensatte paragrafnumre (`§1-7b`) — før den foreslåtte `{lov-eli}/§X-Y`-konvensjonen låses helt.
2. **Redundans XML + node-tabell — bekreftet, ikke lenger åpent.** Synkron regenerering i samme transaksjon er riktig avveining (§2); lat/async ble vurdert og forkastet.
3. **Romertall-underinndelinger — retning avklart, ett residual-spørsmål.** Skal modelleres som egen strukturenhet (§3.2), ikke et overskriftsfelt. **Fortsatt åpent:** `<subchapter>` (AKNs standard hierarkiske element) eller `<hcontainer>` (generisk utvidelse) — AKN-kompetent hold bør avgjøre hvilket som passer norsk lovstruktur best.
4. **Opphevede bestemmelser — retning avklart, ett residual-spørsmål.** Skal bruke AKNs reelle temporal-mekanisme (`start`/`end`-attributter), ikke en oppfunnet `status="opphevet"`-attributt (§3.2). **Fortsatt åpent:** nøyaktig hvilken attributtkombinasjon som er riktig AKN-konvensjon for denne typen opphevelse, bør bekreftes av AKN-kompetent hold — ikke utledet fra ett søkeresultat alene.
5. **Ekstern kryssreferanse til ikke-importert rettskilde — avklart.** Import kaskaderer automatisk som en metadata-**stub** (`importrolle='referanse'`, ikke fullt AKN-innhold), avgrenset til ett hopp (ikke transitivt) og kun for referanser i selve løpeteksten (ikke header-metadata) (§3.1 steg 6). Residual-spørsmål: er ett hopp riktig grense, eller bør sentrale, tverrgående kilder (forvaltningsloven, personopplysningsloven — jf. `digital-rettsstat` prinsipp 9) heller forhåndsimporteres fullt ut som `primaer` uansett, siden de vil bli referert fra svært mange lover uavhengig av hvilken vi starter med?
6. **Versjonering av noder og annotasjoner — avklart.** Ingen egen nodenivå-versjonsteller; versjonering skjer på hele rettskilde-dokumentet (empirisk bekreftet: Lovdata republiserer hele det konsoliderte dokumentet, ikke inkrementelt per paragraf), kombinert med `tekst_hash` per node for å oppdage endringer og la tagger overleve uendrede paragrafer på tvers av versjoner (§2.1).
7. **Er den deterministiske/AI-skillelinjen i §3 riktig trukket — bekreftet.** Ekstern kvalitetssikring støttet eksplisitt prinsippet: kanonisk transformasjon av Lovdata-data skal være deterministisk og reproduserbar; LLM tilfører her hovedsakelig usikkerhet der Lovdata allerede gir struktur, metadata, identifikatorer og referanser. LLM forbeholdes kvalitetskontroll/forslag/forklaring/klassifisering — ikke generering av autoritativ AKN fra Lovdata-kildet innhold.

**Prioritert rekkefølge for neste QA-runde (fra den eksterne vurderingen):** eId/ELI-verifisering (1) → opphevede bestemmelser i AKN (4) → romertall-underinndelinger (3) → ett-hopp-grensen for referanser (5).
