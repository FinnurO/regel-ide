# Teknisk design — Byggesteg 1 (Rettskildebibliotek)

*Dekker AKN-skjema, databasetabeller og konverteringspipeline. Frontend-komponentstruktur er utelatt — overlatt til samme verktøy som produserte den opprinnelige interaktive prototypen (`Regel-IDE.dc.html`). Dette dokumentet er underlag til ekstern kvalitetssikring; ingen kode er skrevet mot det ennå. En interaktiv mockup som implementerer dette designet visuelt finnes i [`../prototyper/Byggesteg1-Rettskilder.dc.html`](../prototyper/Byggesteg1-Rettskilder.dc.html) — verdt å sammenligne mot hvis noe under endres videre.*

*Begrunnelsene bak designvalgene er samlet i **Vedlegg A**, ikke gjentatt i hoveddokumentet — hver seksjon under peker dit med én kort referanse. Tre runder ekstern kvalitetssikring er gjennomført og innarbeidet (2026-07); status og de to gjenstående spørsmålene står i §6.*

Basert på ekte kildedata: `data/kilder/raw-lovdata/alkoholloven-LOV-1989-06-02-27.html` og `.../alkoholforskriften-FOR-2005-06-08-538.html` (proveniens og strukturell råmapping: `data/kilder/README.md`).

## 1. AKN-skjema

### 1.1 Toppnivå og FRBR-metadata

Akoma Ntoso (OASIS LegalDocML) krever en FRBR-basert metadatablokk (Work/Expression/Manifestation) i tillegg til selve teksten.

**Regler:**
- `FRBRWork/FRBRauthor` er kildetype-avhengig: `Lov → Stortinget`, `Forskrift → utstedende myndighet` (Lovdatas `ministry`-felt), `Rundskriv/Virksomhetsdokument → utstedende etat`. Ansvarlig fagdepartement lagres uansett, som en egen referanse og i `<proprietary>` (`ansvarligDepartement`) — se Vedlegg A.1.
- `FRBRExpression/FRBRdate[@name='konsolidering']` = Lovdatas *"Ikrafttredelse av siste endring"* (header-feltet `lastChangeInForce`). `FRBRManifestation/FRBRdate[@name='regel-ide-import']` = når regel-IDE hentet/konverterte dokumentet. To distinkte datoer — se Vedlegg A.2.
- **Migrasjonsstrategi ved endring i URI-format:** `eId`-verdier er immutable. Endrer Lovdata sitt URI-format, eller ELI-spesifikasjonen selv, håndteres det som en ny rettskilde-versjon (§2.1) — aldri en omskriving av eksisterende `eId`-er. Nye språkvarianter (f.eks. nynorsk) modelleres som en ny `FRBRExpression` under samme `FRBRWork`; AKNs FRBR-modell støtter dette direkte.

```xml
<akomaNtoso xmlns="http://docs.oasis-open.org/legaldocml/ns/akn/3.0">
  <act name="lov">
    <meta>
      <identification source="#regel-ide">
        <FRBRWork>
          <FRBRthis value="/akn/no/act/1989-06-02/27"/>
          <FRBRuri value="/akn/no/act/1989-06-02/27"/>
          <FRBRdate date="1989-06-02" name="vedtakelse"/>
          <FRBRauthor href="#stortinget"/>
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
        <TLCOrganization eId="stortinget" href="/ontology/organization/no/stortinget" showAs="Stortinget"/>
        <TLCOrganization eId="helse-og-omsorgsdepartementet" href="/ontology/organization/no/hod" showAs="Helse- og omsorgsdepartementet"/>
        <TLCOrganization eId="lovdata" href="/ontology/organization/no/lovdata" showAs="Lovdata"/>
      </references>
      <proprietary source="#regel-ide">
        <regelIde:eli>https://lovdata.no/eli/lov/1989/06/02/27/nor</regelIde:eli>
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

`<proprietary>` bærer regel-IDEs egne felt (`03-domenemodell.md` §1.1: `doctype`, `kildetype`, `status` …) — AKNs offisielle utvidelsesmekanisme.

### 1.2 eId-konvensjon: ELI som kanonisk rot, låst nå

**Arkitekturbeslutning (ikke lenger avventet):** vi venter ikke på at Lovdata skal publisere offisiell seksjonsnivå-ELI. Regel-IDE definerer sin egen, deterministiske utvidelse av det verifiserte lovnivå-URI-et, og låser den som kanonisk identitet nå — se Vedlegg A.3 for begrunnelse.

**Regel:** dersom en offisiell seksjons-ELI finnes og er verifisert, brukes den. Inntil videre finnes ingen slik for alkoholloven, så Regel-IDEs egen utvidelse brukes.

**Verifisert direkte:** `https://lovdata.no/eli/lov/1989/06/02/27` løser til alkoholloven, realisert som `/eli/lov/1989/06/02/27/nor` — ekstern, stabil identifikator på lovnivå.

**Regel-IDEs deterministiske utvidelse under lovnivå (låst):**

| Nivå | `eId` |
|---|---|
| Rettskilde (lovnivå) | `https://lovdata.no/eli/lov/1989/06/02/27/nor` (verifisert ELI-URI) |
| Kapittel | `kap-1` |
| Paragraf | `{lov-eli}/§1-1` |
| Ledd | `{paragraf-eId}/ledd-1` |
| Punkt | `{ledd-eId}/punkt-1` |

**Migreringsvei dersom Lovdata senere publiserer offisiell seksjons-ELI:** `rettskilde_noder.offisiell_eli` (§2) fylles ut da, som et rent tillegg — `eid` endres ikke. Dette er den samme append-only-migreringsstrategien som §1.1 beskriver for URI-formatendringer generelt: en ekstern endring legger til informasjon, den omskriver ikke identitet som allerede er i bruk.

### 1.2.1 Canonical Identity vs. Source Identity — generelt prinsipp

Skillet mellom `eid` (kanonisk, ELI-forankret, endres aldri) og Lovdatas HTML-`id` (kildesystemets egen, kan endre seg) generaliseres til et navngitt, gjennomgående prinsipp for **alle** entiteter regel-IDE henter fra en ekstern kilde, ikke bare rettskilde-noder:

- **`canonical_id`** (i skjemaet: `eid`) — regel-IDEs egen, permanente identitet. Endres aldri etter opprettelse.
- **`source_id`** (i skjemaet: `kilde_id` + `kildesystem`) — identiteten i det systemet dataen kom fra (Lovdata i dag; Domstol.no for presedens, regjeringen.no for enkelte dokumenttyper senere). Kan endre seg uten at det påvirker `canonical_id`.

Dette er allerede anvendt for rettskilde-noder (§2); prinsippet gjelder tilsvarende når presedens (Domstol.no) eller andre eksterne kilder legges til senere byggesteg — `kilde_id`-kolonnen utvides da med et `kildesystem`-felt (se §2) i stedet for at hver entitetstype finner opp sitt eget mønster.

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

`kildeId` er en regel-IDE-lokal attributt (samme mønster som `<proprietary>` i §1.1), ikke en del av AKN-standarden. `<ref href="#…/§1-5">` bevarer den interne kryssreferansen fra Lovdatas `<a href="lov/1989-06-02-27/§1-5">` — begrunnelsen for hvorfor hele loven importeres (ikke bare "relevante" kapitler) står i `06-veikart.md` byggesteg 1.

## 2. Databasetabeller

*Kolonnetyper er PostgreSQL-stil (`uuid`, `text`, `timestamptz`, `jsonb`) som arbeidsantagelse — databasevalget er ikke låst (`05-arkitektur-og-nfk.md` §1). Feltnavn er norske, i tråd med resten av modellen og `forklaringsmodell-api`s konvensjon.*

```sql
-- Rettskilden selv: metadata + kanonisk AKN-XML som autoritativ kilde
CREATE TABLE rettskilder (
  id                uuid PRIMARY KEY,
  doctype           text NOT NULL,        -- 'act' | 'doc' | 'judgment' | 'internal'
  kildetype         text NOT NULL,        -- 'Lov' | 'Forskrift' | 'Rundskriv' | 'Presedens' | 'Virksomhetsdokument'
  importrolle       text NOT NULL DEFAULT 'primaer',  -- 'primaer' | 'referanse' — §3.1 steg 6
  tittel            text NOT NULL,
  kortnavn          text,
  eli               text,                 -- f.eks. 'https://lovdata.no/eli/lov/1989/06/02/27/nor'
  akn_xml           text,                 -- NULL for referanse-stubber; NOT NULL for primaer-kilder og forfremmede stubber
  ikrafttredelse    date,
  konsolidert_dato  date,
  utgiver           text,                 -- f.eks. 'Lovdata' — NLOD 2.0-attribusjon, 05-arkitektur-og-nfk §1.1
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
  CHECK (importrolle = 'referanse' OR akn_xml IS NOT NULL)
);
-- Kun én 'gjeldende' rad per ELI av gangen (nye versjoner er nye rader, entitetsstatus skifter på den forrige)
CREATE UNIQUE INDEX ux_rettskilder_eli_gjeldende ON rettskilder(eli) WHERE entitetsstatus = 'gjeldende';

-- Materialisert projeksjon av AKN-treet for navigasjon/søk/tagging-join. Aldri autoritativ i seg selv —
-- regenereres synkront (samme transaksjon) fra rettskilder.akn_xml ved hver import/endring, aldri redigert direkte.
CREATE TABLE rettskilde_noder (
  id                uuid PRIMARY KEY,
  rettskilde_id     uuid NOT NULL REFERENCES rettskilder(id) ON DELETE CASCADE,
  eid               text NOT NULL,        -- canonical_id: kanonisk, ELI-forankret identitet, §1.2 — endres aldri
  kildesystem       text NOT NULL DEFAULT 'lovdata',  -- source_id-systemet, §1.2.1 — 'lovdata' i dag, 'domstol.no' m.fl. senere
  kilde_id          text NOT NULL,        -- source_id: opprinnelig id i kildesystemet — sporbarhet, ikke kanonisk (§1.2.1)
  offisiell_eli     text,                 -- nullable — fylles ut hvis/når Lovdata publiserer offisiell seksjons-ELI (§1.2), endrer ikke eid
  parent_node_id    uuid REFERENCES rettskilde_noder(id),
  node_type         text NOT NULL,        -- 'kapittel' | 'underinndeling' | 'paragraf' | 'ledd' | 'punkt'
  nummer            text,                 -- f.eks. '§ 1-1', 'Kapittel 1.'
  overskrift        text,
  tekst             text,                 -- kun for ledd/punkt-noder (bladtekst)
  tekst_hash        text,                 -- se §3.4 for presis definisjon
  sorteringsrekkefolge int NOT NULL,
  UNIQUE (rettskilde_id, eid)
);
CREATE INDEX ix_rettskilde_noder_parent ON rettskilde_noder(parent_node_id);
CREATE INDEX ix_rettskilde_noder_tekst_fts ON rettskilde_noder USING gin(to_tsvector('norwegian', tekst));
CREATE INDEX ix_rettskilde_noder_eid_hash ON rettskilde_noder(eid, tekst_hash);  -- versjonssammenligning, §2.1

-- Interne kryssreferanser innenfor/på tvers av rettskilder (fra <ref href="#...">)
CREATE TABLE rettskilde_referanser (
  id                uuid PRIMARY KEY,
  fra_node_id       uuid NOT NULL REFERENCES rettskilde_noder(id) ON DELETE CASCADE,
  til_rettskilde_id uuid NOT NULL REFERENCES rettskilder(id),
  til_eid           text NOT NULL,        -- målnodens eid, kan være i en annen rettskilde
  UNIQUE (fra_node_id, til_rettskilde_id, til_eid)  -- forhindrer duplikatimport av samme referanse
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
  node_tekst_hash   text NOT NULL,        -- rettskilde_noder.tekst_hash på taggetidspunktet, §2.1
  kind              text NOT NULL,        -- 'begrep' | 'vilkar' | 'regel'
  ref_id            uuid,                 -- peker til begrep/vilkår/regelnode/unntak — nullable inntil byggesteg 2/4
  entitetsstatus    text NOT NULL DEFAULT 'gjeldende',
  opprettet_av      text NOT NULL,
  opprettet_tidspunkt timestamptz NOT NULL DEFAULT now(),
  UNIQUE (rettskilde_id, node_eid, start_offset, end_offset, kind, ref_id)
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
  kilde_referanser  jsonb,
  ai_forslag_versjon text,
  godkjent_av       text
);
CREATE INDEX ix_proveniens_entitet ON proveniens(entitet_type, entitet_id);
```

`rettskilder.akn_xml` og `rettskilde_noder` er bevisst redundante, og regenereres synkront i samme transaksjon (Vedlegg A.5).

### 2.1 Versjonering: dokumentnivå, ikke nodenivå

Versjonering skjer på hele `rettskilder`-dokumentet, ikke per node — bekreftet empirisk (Vedlegg A.6): en ny konsolidert versjon er en helt ny `rettskilder`-rad (`erstatter_id`-kjeden, `03-domenemodell.md` §0) med et helt nytt sett `rettskilde_noder`-rader, aldri en inkrementell oppdatering.

**Mekanisme for at tagger overlever en ny versjon:** hver node har en `tekst_hash` (§3.4); hver tag lagrer `node_tekst_hash` fra da den ble opprettet.

- Ved import av en ny rettskilde-versjon: for hver ny node, slå opp forrige versjons node med samme `eid`. Uendret `tekst_hash` → paragrafen er ordrett uendret → tagger forblir gyldige, koblet til den nye `rettskilde_id`.
- Endret `tekst_hash` (eller noden borte) → taggen flagges for manuell gjennomgang.

Dette krever ingen egen nodeversjonstabell.

## 3. Konverteringspipeline (Lovdata-HTML → AKN)

Transformasjonen fra Lovdata-kildet innhold til AKN skal være **referansielt transparent**: samme kilde-HTML skal alltid gi bit-identisk AKN-innhold (samme `<body>`, `FRBRWork`, `FRBRExpression`) — med ett unntak, `FRBRManifestation/FRBRdate[@name='regel-ide-import']` (§1.1), som legitimt varierer med *når* importen kjørte, ikke med innholdet som importeres. Referansiell transparens i den innholdsbærende delen er det som gjør parseren testbar, diff-bar, signerbar og cache-bar (Vedlegg A.8). AI-assistert konvertering er forbeholdt opplastede, ustrukturerte dokumenter (rundskriv, virksomhetsdokument, AK-3.3.6/3.3.7) — der finnes ingen strukturell ekvivalent til Lovdatas `data-lovdata-URL` å bygge på, og ingen autoritativ rettskilde skal noensinne produseres av en LLM.

### 3.1 Steg (Lovdata-kildet import)

1. **Hent** — `GET api.lovdata.no/…` (når API-nøkkel er registrert) eller les fra allerede hentet fil (`data/kilder/raw-lovdata/`, midlertidig for byggesteg 1).
2. **Dekod** `cp1252` → UTF-8 (`data/kilder/README.md`).
3. **Parse HTML til DOM** med et HTML-parserbibliotek — dokumentet er nøstet og inneholder uregelmessig innhold (fotnoter, `changesToParent`).
4. **Ekstraher dokumentmetadata** fra `<header class="documentHeader">` → `rettskilder`-rad + FRBR-blokk (§1.1).
5. **Vandre `<main class="documentBody">`** — for hver node: bygg `eid` fra ELI-roten + §-nummer/ledd-/punkt-indeks (§1.2), sett `kilde_id` fra elementets Lovdata-`id`, beregn `tekst_hash` (§3.4) for ledd/punkt-noder:
   - `<section class="section">` → `node_type='kapittel'`.
   - `<article class="legalArticle">` → `node_type='paragraf'`, `nummer` fra `legalArticleValue`, `overskrift` fra `legalArticleTitle`.
   - Nøstet `<article class="legalP">` → `node_type='ledd'`, `tekst` = tekstinnhold med `<a href="lov/…">` bevart som markører for steg 6.
   - `<li><article class="listArticle">` → `node_type='punkt'`.
   - `<article class="changesToParent">` → skriv en `proveniens`-rad (`handling='endret'`), ikke en tekstnode.
6. **Kryssreferanser** — for hver `<a href="lov/…">`/`<a href="forskrift/…">` i selve løpeteksten (ikke header-metadata som «Endrer»/EØS-henvisninger, jf. Vedlegg A.7):
   - Samme rettskilde → intern referanse, `til_eid` i samme dokument.
   - Annen rettskilde, allerede i biblioteket → `rettskilde_referanser`-rad.
   - Annen rettskilde, ikke i biblioteket → opprett en referanse-stub (`importrolle='referanse'`, `akn_xml=null`, kun metadata) → `rettskilde_referanser`-rad. Stubben følges ikke videre (ett hopp, ikke transitivt). En stub kan forfremmes til `importrolle='primaer'` som egen handling, som trigger full henting (steg 1–5).
7. **Generer kanonisk AKN-XML** og skriv `rettskilder.akn_xml`.
8. **Skriv `rettskilde_noder`** i samme transaksjon som steg 7.
9. **Status:** `utkast`.
10. **Menneskelig verifisering** (jurist/fagansvarlig, `03-domenemodell.md` §3.2): sammenlign generert AKN mot kildeteksten side-ved-side (AK-3.3.6) for å fange parserfeil. Godkjent → `gjeldende`.

### 3.1.1 Forhåndsimporterte infrastrukturlover

Ett-hopps referanse-stub (steg 6) er hovedregelen, men noen lover brukes på tvers av så godt som alle andre — **forvaltningsloven, offentleglova, personopplysningsloven og kommuneloven** er kandidater — og forhåndsimporteres derfor fullt ut som `importrolle='primaer'` uansett hvilket testcase byggesteg 1 starter med, i stedet for å vente på at et testcase tilfeldigvis refererer dem inn som stub. Begrunnelsen er ikke teknisk (stub-mekanismen håndterer dem fint) — den er at brukerne kommer til å slå opp i disse lovene svært ofte, og det er bedre å ha dem fullt tilgjengelige fra start. Nøyaktig hvilke lover som kvalifiserer som "infrastrukturlov" og deres eksakte ELI-referanser bekreftes før implementasjon, ikke antatt fra denne listen alene.

### 3.2 Kjente vanskelige tilfeller

Funnet i den ekte teksten, ikke antatt:

- **`(Opphevet)`-paragrafer** (f.eks. § 1-12, § 1-13, § 3-5, § 3-6, § 5-1, § 8-7, § 8-10 i alkoholloven) har ingen `legalP`-innhold. **Regel, uten unntak: en opphevet paragraf skal alltid produsere en node — aldri hoppes over.** En opphevet bestemmelse er fortsatt del av rettshistorien, og referanser til den (interne eller fra andre dokumenter) skal forbli løsbare. Parseren skal representere den som et gyldig, tomt `<article>`, markert opphevet med AKNs temporal-attributter (`end` uten `start`, Vedlegg A.9). Er den nøyaktige attributtkombinasjonen usikker i en gitt sak, skal parseren likevel produsere `<article>`-noden med opphevelsen markert i `<proprietary>` (regel-IDEs egen `opphevet`-flagg) som reserveløsning — riktig AKN-idiomatikk (§6 punkt 4) påvirker hvor godt dokumentet samspiller med andre AKN-verktøy, ikke om referanser forblir stabile.
- **Fotnoter** (`footnote`/`footnotereference`/`footnotes`-klasser) modelleres som egen AKN `<authorialNote>`, atskilt fra hovedteksten.
- **Romertall-underinndelinger** (f.eks. kapittel 3s "I.", "II.", "III." mellom paragrafer, uten egne `<section>`) modelleres som egen strukturenhet: **`<hcontainer>` (låst)**, ikke `<subchapter>` — romertallsgrupperinger i norske lover er ofte redaksjonelle, ikke nødvendigvis et selvstendig juridisk hierarkinivå, og et generisk containerelement påstår ikke mer semantikk enn det som faktisk er verifisert (Vedlegg A.10). Migrering til `<subchapter>` er mulig senere dersom AKN-miljøet viser seg å forvente det; motsatt vei er vanskeligere.

### 3.3 Feilhåndtering

- Hele importen (steg 1–9) kjøres i én transaksjon. Enhver feil — ugyldig HTML, manglende påkrevd metadata (ELI, tittel, kildetype), parserunntak — ruller hele transaksjonen tilbake. Ingen delvis importert rettskilde skal kunne stå igjen i `utkast`-tilstand.
- Feil logges med nok kontekst (hvilket element, tegnposisjon i kildefilen) til manuell diagnose.
- Import gjenforsøkes manuelt, ikke automatisk.
- Manglende påkrevde felt gir en feilet import — ingen gjettede fallback-verdier.

### 3.4 tekst_hash — presis definisjon

`tekst_hash = SHA-256(normalisert(tekst))`, der normalisering er, i rekkefølge:

1. HTML-tagger fjernes, med unntak av interne `<a href="lov/…">`-referanser, som erstattes med sin synlige tekst (selve §-henvisningen, f.eks. "§ 1-5") — en endring i referansens *mål* skal ikke endre hash for teksten den står i.
2. Unicode NFC-normalisering.
3. Alt whitespace (mellomrom, linjeskift, tab) kollapses til ett enkelt mellomrom, trimmet i start og slutt.

Dette holder hashen stabil på tvers av kosmetiske formateringsvarianter fra Lovdata så lenge ordlyden er uendret, og forhindrer falske "endret"-flagg på tagger (§2.1).

## 4. Ikke-funksjonelle krav for byggesteg 1

Konkrete mål for pilotskala (Testkommunen, ett testcase) — foreslått, ikke bekreftet av produkteier, basert på de faktiske dokumentstørrelsene hentet (alkoholloven 165 KB / alkoholforskriften 117 KB HTML, ikke gjettet).

| Krav | Mål |
|---|---|
| Antall rettskilder i biblioteket | Noen hundre, inkl. referanse-stubber — realistisk 10–20 primærkilder + 50–100 stubber for testcaset |
| Størrelse per dokument (AKN-XML) | Opp til noen MB uten spesialtilpasning |
| Importtid, enkeltdokument | Innen 10 sekunder for et dokument på denne størrelsen |
| Responstid, tre-navigasjon/nodeoppslag | Innen 1 sekund |
| Samtidige brukere | Ikke dimensjonert for skala i byggesteg 1 — tverrfaglig team, noen få samtidige brukere, jf. optimistic concurrency i `05-arkitektur-og-nfk.md` §2 |
| Minnebruk | Ingen streaming-arkitektur nødvendig gitt dokumentstørrelsene over |

## 5. Hva som er ute av scope for dette designet

- Faktisk parser-/serialiseringskode.
- API-nøkkel-registrering hos Lovdata for de strukturerte endepunktene (`05-arkitektur-og-nfk.md` §1.1) — bulk-filene dekker byggesteg 1s behov i mellomtiden.
- Frontend — se innledningen.

## 6. Til ekstern kvalitetssikring — status etter tre runder

Alle arkitekturvalg som kan besluttes uavhengig av eksterne standarder er nå låst. To punkter gjenstår genuint åpne, fordi de omhandler fakta om eksterne standarder/systemer utenfor regel-IDEs kontroll:

1. **Offisiell seksjons-ELI, hvis/når den finnes.** Regel-IDEs egen eId-konvensjon (§1.2) er låst og fungerer uavhengig av dette — punktet er kun relevant for en fremtidig `offisiell_eli`-mapping (§2), ikke blokkerende. Bekreft, når det er praktisk mulig: publiserer Lovdata seksjonsnivå-ELI konsekvent, og i så fall nøyaktig format for sammensatte paragrafnumre (`§1-7b`)?
2. **Nøyaktig AKN-attributtkombinasjon for opphevede bestemmelser.** Regelen "aldri hopp over en opphevet paragraf" (§3.2) og reserveløsningen (regel-IDE-egen `<proprietary>`-markering) er låst og fungerer uavhengig av dette — punktet påvirker kun hvor AKN-idiomatisk representasjonen er, ikke om systemet fungerer. Bekreft riktig `start`/`end`/`status`-konvensjon når AKN-kompetent hold er tilgjengelig.

**Avklart i denne runden (tidligere åpne punkter, nå arkitekturbeslutninger):**
- Romertall-underinndelinger: `<hcontainer>`, ikke `<subchapter>` (§3.2).
- Ekstern kryssreferanse: ett hopp, aldri transitivt (§3.1 steg 6) — **pluss** et definert unntak: sentrale infrastrukturlover forhåndsimporteres fullt som `primaer` uansett testcase (§3.1.1).
- Deterministisk/AI-skillelinjen: skjerpet til et eksplisitt krav om referansiell transparens (§3).
- Canonical Identity vs. Source Identity er gjort til et navngitt, generelt prinsipp (§1.2.1), ikke bare et engangsvalg for rettskilde-noder.

**Tidligere avklart, uendret:** redundans XML + node-tabell (synkron regenerering), dokumentnivå-versjonering + `tekst_hash`.

---

## Vedlegg A: Designbegrunnelser

*Samlet her for å holde hoveddokumentet konsist og skannbart — hver seksjon over peker til det aktuelle punktet i stedet for å gjenta resonnementet.*

**A.1 — FRBRauthor kildetype-avhengig.** Stortinget vedtar en lov; departementet er ansvarlig for oppfølging/forslag, ikke lovgivning. En forskrift derimot gis ofte av departementet (eller Kongen i statsråd) under delegert hjemmel, så der er departementet riktig `FRBRauthor`. Én universell regel ville vært feil for den ene eller den andre kildetypen.

**A.2 — Konsolideringsdato ≠ importdato.** Uten eksplisitt skille kunne de to lett bli forvekslet eller satt til samme verdi ved en feiltakelse, og dermed skjule *når loven faktisk sist ble endret* bak *når vi tilfeldigvis hentet den*.

**A.3 — ELI fremfor Lovdatas HTML-id.** Lovdatas HTML-`id` er en implementasjonsdetalj ved rendring av nettsiden deres; endrer Lovdata malen sin, endrer den seg, med brutte referanser som konsekvens. Å finne opp et eget regel-IDE-identifikatorrom løser ikke problemet, bare flytter det. ELI er allerede en ekstern, EU-forankret standard som `digital-rettsstat` selv peker på som ryggraden for Kildelaget, og som Lovdata publiserer parallelt med HTML-siden sin — å forankre i den kobler oss fra Lovdatas renderingsdetaljer uten å oppfinne noe nytt.

**A.4 — `kilde_id` beholdes separat.** Lovdatas HTML-id er fortsatt nyttig for sporbarhet tilbake til nøyaktig hvilket element i kildefilen en node ble parset fra — det forkastes ikke, det degraderes fra kanonisk identitet til proveniensfelt.

**A.5 — Synkron regenerering av `rettskilde_noder`.** Node-tabellen er en projeksjon for ytelse (unngå XML-parsing på hvert navigasjons-/søkekall). Lat/asynkron regenerering ble vurdert og forkastet: konsistens er viktigere enn noen få millisekunder spart ved import, og et konsistensvindu her har ingen motsvarende gevinst.

**A.6 — Versjonering på dokumentnivå.** Lovdata-headeren har ett `lastChangeInForce`-felt for *hele* loven, ikke separate endringstidspunkt per paragraf — en norsk lov republiseres som ett helt, konsolidert dokument ved endring, ikke inkrementelt. `digital-rettsstat/docs/07-standarder-og-sporbarhetskjeden.md` beskriver samme "point-in-time"-modell fra Storbritannia. En egen nodenivå-versjonshistorikk ville løst et problem som ikke finnes i kildedataen.

**A.7 — Ett-hopps referanse-stub, ikke transitiv import.** Å følge referanser-av-referanser uten grense ville betydd at én lovimport i verste fall drar med seg store deler av lovverket (alkoholloven → forvaltningsloven → dens referanser → …). Header-metadata («Endrer», EØS-henvisninger) er lovhistorikk, ikke normativt innhold loven faktisk bruker, og trigger derfor ikke import.

**A.8 — Lovdata-konvertering er deterministisk, ikke LLM-basert.** Strukturen (`section`/`legalArticle`/`legalP`/`data-lovdata-URL`) er allerede fullt maskinlesbar. En LLM ville tilført ikke-determinisme til noe en ren HTML-parser løser presist. LLM-bruk er forbeholdt opplastede, ustrukturerte dokumenter, der det faktisk finnes en tolkningsoppgave.

**A.9 — AKNs `start`/`end`-mekanisme for opphevelser.** En regel-IDE-oppfunnet `status="opphevet"`-attributt ville dupliserte noe AKN-standarden allerede løser (temporal-gruppens `start`/`end`-attributter), og gjort dokumentene mindre gjenkjennelige for andre AKN-baserte verktøy.

**A.10 — Romertall-underinndelinger som egen strukturenhet.** En gruppe paragrafer under et romertall (kapittel 3s "I."/"II."/"III.") er reell dokumentstruktur, ikke pynt — å gjemme dem i et overskriftsfelt på artikkelnivå ville tapt informasjon om hvilke paragrafer som faktisk hører sammen.
