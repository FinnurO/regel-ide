# Teknisk design — Byggesteg 1 (Rettskildebibliotek)

*Dekker AKN-skjema, databasetabeller og konverteringspipeline (punkt 1–3 fra behovet identifisert i chatten). Frontend-komponentstruktur (punkt 4) er bevisst utelatt her — overlatt til samme verktøy som produserte den opprinnelige interaktive prototypen (`Regel-IDE.dc.html`). Dette dokumentet er **underlag til ekstern kvalitetssikring** — ingen kode er skrevet mot det ennå. §5 lister eksplisitt hva som mest trenger å bli utfordret.*

Basert på ekte kildedata: `data/kilder/raw-lovdata/alkoholloven-LOV-1989-06-02-27.html` og `.../alkoholforskriften-FOR-2005-06-08-538.html` (se `data/kilder/README.md` for proveniens og strukturell råmapping).

## 1. AKN-skjema

### 1.1 Toppnivå og FRBR-metadata

Akoma Ntoso (OASIS LegalDocML) krever en FRBR-basert metadatablokk (Work/Expression/Manifestation) i tillegg til selve teksten. Utledet fra alkohollovens Lovdata-header (`legacyID`, `dokid`, `dateInForce`, `ministry`):

```xml
<akomaNtoso xmlns="http://docs.oasis-open.org/legaldocml/ns/akn/3.0">
  <act name="lov">
    <meta>
      <identification source="#regel-ide">
        <FRBRWork>
          <FRBRthis value="/akn/no/act/1989-06-02/27"/>
          <FRBRuri value="/akn/no/act/1989-06-02/27"/>
          <FRBRdate date="1989-06-02" name="vedtakelse"/>
          <FRBRauthor href="#helse-og-omsorgsdepartementet"/>
          <FRBRcountry value="no"/>
        </FRBRWork>
        <FRBRExpression>
          <FRBRthis value="/akn/no/act/1989-06-02/27/nor@2026-07-20"/>
          <FRBRuri value="/akn/no/act/1989-06-02/27/nor@2026-07-20"/>
          <FRBRdate date="2026-07-20" name="konsolidering"/>
          <FRBRauthor href="#lovdata"/>
          <FRBRlanguage language="nor"/>
        </FRBRExpression>
        <FRBRManifestation>
          <FRBRthis value="/akn/no/act/1989-06-02/27/nor@2026-07-20.xml"/>
          <FRBRuri value="/akn/no/act/1989-06-02/27/nor@2026-07-20.xml"/>
          <FRBRdate date="2026-07-23" name="regel-ide-import"/>
          <FRBRauthor href="#regel-ide"/>
        </FRBRManifestation>
      </identification>
      <references source="#regel-ide">
        <TLCOrganization eId="helse-og-omsorgsdepartementet" href="/ontology/organization/no/hod" showAs="Helse- og omsorgsdepartementet"/>
        <TLCOrganization eId="lovdata" href="/ontology/organization/no/lovdata" showAs="Lovdata"/>
      </references>
      <proprietary source="#regel-ide">
        <regelIde:eli>LOV-1989-06-02-27</regelIde:eli>
        <regelIde:kildetype>Lov</regelIde:kildetype>
        <regelIde:status>Gjeldende</regelIde:status>
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

### 1.2 eId-konvensjon

**Valg: gjenbruk Lovdatas egne `id`-verdier direkte, med minimal omskriving.** Lovdata-HTML-en har allerede `id="kapittel-1-paragraf-1-ledd-1"` — global unikt innenfor dokumentet, hierarkisk, og null tolkningsrisiko å avlede fra. Alternativet (kortere AKN-konvensjonelle former som `kap_1__art_1-1__ledd_1`) er penere, men krever en omskrivingsregel per nivå som kan introdusere feil uten tilsvarende gevinst. **Dette er markert i §5 som noe å utfordre** — kort form kan vise seg mer lesbar i UI-et (kap. 3.3/3.4) og mer robust hvis Lovdata endrer sin egen id-konvensjon.

| AKN-element | `eId` (foreslått, fra Lovdata-`id` direkte) |
|---|---|
| `<chapter>` | `kapittel-1` |
| `<article>` (paragraf) | `kapittel-1-paragraf-1` |
| `<paragraph>` (ledd) | `kapittel-1-paragraf-1-ledd-1` |
| `<point>` (bokstav/nummer) | `kapittel-1-paragraf-3-ledd-2-punkt-1` |

`03-domenemodell.md`s eksempel `par_1-7b` (fra førsteutkastet) er dermed **ikke** den faktiske konvensjonen lenger — det var en antatt form før vi hadde ekte data. Rettes i domenemodellen når dette designet er kvalitetssikret.

### 1.3 Fullt eksempel, bygget fra ekte tekst (§ 1-1 og § 1-3)

```xml
<chapter eId="kapittel-1">
  <num>Kapittel 1.</num>
  <heading>Alminnelige bestemmelser.</heading>

  <article eId="kapittel-1-paragraf-1">
    <num>§ 1-1</num>
    <heading>Lovens formål.</heading>
    <paragraph eId="kapittel-1-paragraf-1-ledd-1">
      <content>
        <p>Reguleringen av innførsel og omsetning av alkoholholdig drikk etter denne lov har som mål å
        begrense i størst mulig utstrekning de samfunnsmessige og individuelle skader som alkoholbruk
        kan innebære. Som et ledd i dette sikter loven på å begrense forbruket av alkoholholdige
        drikkevarer.</p>
      </content>
    </paragraph>
  </article>

  <article eId="kapittel-1-paragraf-3">
    <num>§ 1-3</num>
    <heading>Definisjoner</heading>
    <paragraph eId="kapittel-1-paragraf-3-ledd-1">
      <content>
        <p>I denne lov brukes alkoholholdig drikk som fellesbetegnelse på drikker som inneholder mer
        enn 2,5 volumprosent alkohol, likevel slik at aldersgrensebestemmelsen i
        <ref href="#kapittel-1-paragraf-5">§ 1-5</ref> også får anvendelse på drikk mellom 0,7 og 2,5
        volumprosent alkohol.</p>
      </content>
    </paragraph>
    <paragraph eId="kapittel-1-paragraf-3-ledd-2">
      <content><p>I denne loven betyr:</p></content>
      <list>
        <point eId="kapittel-1-paragraf-3-ledd-2-punkt-1">
          <content><p>alkoholfri drikk: drikk som inneholder under 0,7 volumprosent alkohol</p></content>
        </point>
        <!-- flere punkt … -->
      </list>
    </paragraph>
  </article>
</chapter>
```

Merk `<ref href="#kapittel-1-paragraf-5">` — den interne kryssreferansen fra Lovdatas `<a href="lov/1989-06-02-27/§1-5">` er bevart som en AKN-intern lenke, ikke fjernet. Dette er nøyaktig den typen henvisning `06-veikart.md` byggesteg 1 begrunner hvorfor hele loven må lastes inn (§ 1-3 refererer § 1-5, som ligger utenfor "de relevante kapitlene" i den opprinnelige, snevrere planen).

## 2. Databasetabeller

*Kolonnetyper er PostgreSQL-stil (`uuid`, `text`, `timestamptz`, `jsonb`) som en arbeidsantagelse — selve databasevalget er ikke låst (`05-arkitektur-og-nfk.md` §1). Feltnavn er norske, i tråd med resten av modellen og `forklaringsmodell-api`s konvensjon.*

```sql
-- Rettskilden selv: metadata + kanonisk AKN-XML som autoritativ kilde
CREATE TABLE rettskilder (
  id                uuid PRIMARY KEY,
  doctype           text NOT NULL,        -- 'act' | 'doc' | 'judgment' | 'internal'
  kildetype         text NOT NULL,        -- 'Lov' | 'Forskrift' | 'Rundskriv' | 'Presedens' | 'Virksomhetsdokument'
  tittel            text NOT NULL,
  kortnavn          text,
  eli               text,                 -- f.eks. 'LOV-1989-06-02-27'
  akn_xml           text NOT NULL,        -- kanonisk, fullstendig AKN-dokument
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
  sist_endret_tidspunkt timestamptz
);

-- Denormalisert projeksjon av AKN-treet, for navigasjon/søk/tagging-join uten å parse XML per kall
-- Regenereres deterministisk fra rettskilder.akn_xml ved hver import/endring — aldri redigert direkte
CREATE TABLE rettskilde_noder (
  id                uuid PRIMARY KEY,
  rettskilde_id     uuid NOT NULL REFERENCES rettskilder(id) ON DELETE CASCADE,
  eid               text NOT NULL,        -- f.eks. 'kapittel-1-paragraf-1-ledd-1'
  parent_node_id    uuid REFERENCES rettskilde_noder(id),
  node_type         text NOT NULL,        -- 'kapittel' | 'paragraf' | 'ledd' | 'punkt'
  nummer            text,                 -- f.eks. '§ 1-1', 'Kapittel 1.'
  overskrift        text,
  tekst             text,                 -- kun for ledd/punkt-noder (bladtekst)
  sorteringsrekkefolge int NOT NULL,
  UNIQUE (rettskilde_id, eid)
);
CREATE INDEX ix_rettskilde_noder_parent ON rettskilde_noder(parent_node_id);
CREATE INDEX ix_rettskilde_noder_tekst_fts ON rettskilde_noder USING gin(to_tsvector('norwegian', tekst));

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

**Design-avveining å merke seg (til §5):** `rettskilder.akn_xml` og `rettskilde_noder` er bevisst redundante — XML-en er autoritativ, node-tabellen er en regenererbar projeksjon for ytelse (unngå XML-parsing på hvert navigasjons-/søkekall, jf. ytelseskravet i `05-arkitektur-og-nfk.md` §2). Konsekvens: enhver skriveoperasjon på en rettskilde må regenerere `rettskilde_noder` i samme transaksjon, ellers driver de fra hverandre.

## 3. Konverteringspipeline (Lovdata-HTML → AKN)

**Viktig presisering fra forrige runde:** vi anbefalte tidligere "AI-assistert AKN-konvertering" generelt (`05-arkitektur-og-nfk.md` §1.1). Etter å faktisk ha sett Lovdata-dataen, bør det presiseres: **for Lovdata-kildet innhold er dette en deterministisk, regelbasert transformasjon — ikke en LLM-oppgave.** Strukturen (`section`/`legalArticle`/`legalP`/`data-lovdata-URL`) er allerede fullt maskinlesbar; en LLM ville innføre unødvendig ikke-determinisme i noe som kan løses med en ren HTML-parser. **AI-assistert konvertering forbeholdes uploaded, ustrukturerte dokumenter** (rundskriv-PDF, virksomhetsdokumenter) i AK-3.3.6/3.3.7 — der finnes ingen `data-lovdata-URL`-ekvivalent, og strukturgjenkjenning er en reell tolkningsoppgave.

### 3.1 Steg (Lovdata-kildet import)

1. **Hent** — `GET api.lovdata.no/…` (når API-nøkkel er på plass) eller les fra allerede hentet fil (`data/kilder/raw-lovdata/`, midlertidig for byggesteg 1).
2. **Dekod** — `cp1252` → UTF-8 (kritisk, se `data/kilder/README.md` — feil koding korrumperer æøå stille, ingen feilmelding).
3. **Parse HTML** til DOM (HTML-parserbibliotek, ikke regex — dokumentet er nøstet og har uregelmessig innhold som fotnoter og `changesToParent`).
4. **Ekstraher dokumentmetadata** fra `<header class="documentHeader">` (`legacyID`, `dokid`, `dateInForce`, `ministry`, `title`, `titleShort`) → `rettskilder`-rad + FRBR-blokk (§1.1).
5. **Vandre `<main class="documentBody">`:**
   - `<section class="section">` → `rettskilde_noder`-rad, `node_type='kapittel'`, `eid` = elementets `id`-attributt.
   - `<article class="legalArticle">` → `node_type='paragraf'`, `eid` fra `id`-attributt, `nummer` fra `legalArticleValue`, `overskrift` fra `legalArticleTitle`.
   - Nøstet `<article class="legalP">` → `node_type='ledd'`, `tekst` = elementets tekstinnhold **med** `<a href="lov/…">`-elementer bevart som markører (ikke bare strippet til plain text) — disse blir `rettskilde_referanser`-rader (steg 6).
   - `<li><article class="listArticle">` → `node_type='punkt'`.
   - `<article class="changesToParent">` → **ikke** en tekstnode. Skriv i stedet en `proveniens`-rad (`handling='endret'`, `kilde_referanser` = lenkene i elementet).
6. **Kryssreferanser** — for hver `<a href="lov/ÅÅÅÅ-MM-DD-N/§X-Y">` inni en ledd-node: slå opp om `ÅÅÅÅ-MM-DD-N` er samme rettskilde (intern referanse, `til_eid` i samme dokument) eller en annen (ekstern — krever at målrettskilden allerede er importert, ellers lagre referansen med `til_rettskilde_id=null` og et ventende-oppslag-flagg til den importeres).
7. **Generer kanonisk AKN-XML** fra det bygde treet (§1) og skriv `rettskilder.akn_xml`.
8. **Skriv `rettskilde_noder`** fra samme tre, i én transaksjon med steg 7 (se avveiningen i §2).
9. **Status:** `utkast`.
10. **Menneskelig verifisering** (jurist/fagansvarlig, jf. livssyklusen `03-domenemodell.md` §3.2): sammenlign generert AKN mot kildeteksten side-ved-side (AK-3.3.6) — her er oppgaven å fange parserfeil (uvanlig nøsting, `(Opphevet)`-paragrafer, fotnoter), ikke å vurdere en AI-tolkning. Godkjent → `gjeldende`.

### 3.2 Kjente vanskelige tilfeller (funnet i den ekte teksten, ikke antatt)

- **`(Opphevet)`-paragrafer** (f.eks. § 1-12, § 1-13, § 3-5, § 3-6, § 5-1, § 8-7, § 8-10 i alkoholloven) — har ingen `legalP`-innhold. Parseren må håndtere en artikkel uten ledd som et gyldig, tomt AKN `<article>` med `status="opphevet"`, ikke krasje eller hoppe over.
- **Fotnoter** (`footnote`/`footnotereference`/`footnotes`-klasser) — separate fra hovedteksten; foreslått: egen AKN `<authorialNote>`, ikke inline i `<p>`.
- **Romertall-underinndelinger** (f.eks. kapittel 3 har "I.", "II.", "III." som underoverskrifter mellom paragrafer, ikke egne `<section>`) — disse er ikke fanget i mappingen over og trenger en egen nodetype eller markeres som overskrift på artikkel-nivå. **Flagget til §5.**

## 4. Hva som er ute av scope for dette designet

- Faktisk parser-/serialiseringskode (dette er et design, ikke en implementasjon).
- API-nøkkel-registrering hos Lovdata for de strukturerte endepunktene (`05-arkitektur-og-nfk.md` §1.1) — bulk-filene dekker byggesteg 1s behov i mellomtiden.
- Frontend — se innledningen.

## 5. Til ekstern kvalitetssikring — spør spesielt om disse

1. **eId-konvensjon (§1.2):** er "gjenbruk Lovdatas egne id-verdier" riktig avveining, eller bør vi normalisere til en kortere, mer lesbar AKN-konvensjonell form (med den ekstra transformasjonsrisikoen det innebærer)?
2. **Redundans XML + node-tabell (§2):** er det riktig avveining å holde begge synkront i én transaksjon, eller bør node-tabellen heller være en cache som kan bygges lat/async (med tilhørende konsistensvindu)?
3. **Romertall-underinndelinger (§3.2):** hvordan bør disse modelleres i AKN — egen nodetype, eller et overskriftsfelt på artikkelen?
4. **Ekstern kryssreferanse til ikke-importert rettskilde (§3.1 steg 6):** er "ventende-oppslag-flagg" god nok håndtering, eller bør import blokkeres/varsles tydeligere når en lov refererer til noe vi ikke har?
5. **Er den deterministiske/AI-skillelinjen i §3 riktig trukket** — bør noe av Lovdata-konverteringen likevel ha en LLM-assistert komponent (f.eks. for å oppsummere `changesToParent`-historikk til lesbar tekst), eller er «ren parser for Lovdata, LLM kun for opplastede dokumenter» riktig?
