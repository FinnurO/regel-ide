# Grunnlagsnotat: regel-ide mot standardene (LKIF Core, UFO-L, LegalRuleML, ELI, Akoma Ntoso) og standardlandskapet (LOMO, SEMIC, regellaget, Norden)

Skrevet 2026-10-09 mot FinnurO/regel-ide master `f066c98`. Repoet er ikke endret. Notatet skal gjøre det mulig å vurdere materialet Johann sender om standarder, punkt for punkt, mot det vi faktisk har.

[ENDRET, #473, 2026-11-12] §7 er en egen, bred gjennomgang av standardlandskapet: LOMO, SEMIC-vokabularene (CPOV, CPSV-AP, Core Location, Core Person og Business, CCCEV), ELI-ontologien og ELI-DL, FLINT/eFLINT, ODRL, LegalRuleML-oppfølgingen, nyere forskning og norske og nordiske initiativer. Den har egne kortnøkler (§7, «Nye kortnøkler») og en egen verifiseringslogg til slutt. Kildene som skal sjekkes ved hver profilversjon, står i [08-standardlandskap-sjekkliste.md](08-standardlandskap-sjekkliste.md).

**Om kildegrunnlaget for standardene (les dette først).** Alle repo-referanser er lest direkte. Standardbegrepene er kontrollert mot primærkildene 2026-10-09 (se «Verifiseringslogg» til slutt). Merkingen betyr:
- **(v) [kilde]** er slått opp i primærkilden. Kortnøkkelen i klammen viser til listen under.
- **(korrigert)** betyr at det første utkastet tok feil, og at teksten nå følger kilden.
- **(finnes ikke i spesifikasjonen)** betyr at begrepet ble lett etter og ikke finnes.
- **(ikke verifisert)** står bare igjen der primærkilden ikke kunne nås eller ikke sier noe.

Primærkildene (kortnøkler):
- **[LRML]** LegalRuleML Core Specification 1.0, OASIS Standard 30.08.2021: https://docs.oasis-open.org/legalruleml/legalruleml-core-spec/v1.0/os/legalruleml-core-spec-v1.0-os.html. Elementnavn er kontrollert mot **[LRML-XSD]** https://docs.oasis-open.org/legalruleml/legalruleml-core-spec/v1.0/os/xsd-schema/compact/lrml-compact.xsd
- **[LKIF]** LKIF Core (ESTRELLA, Hoekstra mfl.): https://github.com/RinkeHoekstra/lkif-core, modulene `norm.owl`, `legal-action.owl`, `legal-role.owl`, `role.owl`, `action.owl`, `expression.owl`, `time-modification.owl` (base-IRI `http://www.estrellaproject.org/lkif-core/<modul>.owl#`)
- **[UFO-L 2018]** Griffo, Almeida, Guizzardi, «Conceptual Modeling of Legal Relations», ER 2018, LNCS 11157 s. 169–183, https://doi.org/10.1007/978-3-030-00847-5_14 (forfatter-PDF: https://nemo.inf.ufes.br/wp-content/papercite-data/pdf/conceptual_modeling_of_legal_relations_2018.pdf)
- **[UFO-L 2023]** Griffo, Almeida, Lima, Sales, Guizzardi, «Legal Powers, Subjections, Disabilities, and Immunities: Ontological Analysis and Modeling Patterns», Data & Knowledge Engineering 148 (2023) 102219, https://doi.org/10.1016/j.datak.2023.102219 (forfatter-PDF: https://nemo.inf.ufes.br/wp-content/papercite-data/pdf/legal_powers_subjections_disabilities_and_immunities_ontological_analysis_and_modeling_patterns_2023.pdf)
- **[ELI]** ELI-ontologien v1.5: http://data.europa.eu/eli/ontology# (RDF/XML hentet fra http://data.europa.eu/eli/ontology)
- **[AKN-1]** Akoma Ntoso 1.0 Part 1: Vocabulary, OASIS Standard 29.08.2018: https://docs.oasis-open.org/legaldocml/akn-core/v1.0/os/part1-vocabulary/akn-core-v1.0-os-part1-vocabulary.html
- **[AKN-XSD]** Part 2: Specifications, skjemaet `akomantoso30.xsd`: https://docs.oasis-open.org/legaldocml/akn-core/v1.0/os/part2-specs/schemas/akomantoso30.xsd
- **[AKN-NC]** Akoma Ntoso Naming Convention 1.0, OASIS Standard 21.02.2019: https://docs.oasis-open.org/legaldocml/akn-nc/v1.0/os/akn-nc-v1.0-os.html
- **[Lovdata-ELI]** Lovdatas ELI-dokumentasjon («ELI (beta)»): https://lovdata.no/eli/
- **[Lovdata-fvl]** LOV-2025-06-20-81: https://lovdata.no/dokument/NL/lov/2025-06-20-81

---

## 1. Hva vi har (inventar)

### 1a. Strukturlaget: hvem gjør hva overfor hvem

| Begrep hos oss | Hva det er | Definert i |
|---|---|---|
| Nodetypene `rettssubjekt`, `organ` og `organisatorisk_enhet` | Aktør i juridisk forstand. Registeret beskriver aktøren, loven definerer den | docs/33 §4.1; CLAUDE.md §0; #310, #311 |
| `ordning` (trygdeordning, fond, tilskuddsordning) | Ikke-aktør som loven gir en funksjon, f.eks. folketrygden | docs/33 §4.1, §4.5 pkt. 4; #353 |
| `rolle` | En funksjon som en aktør innehar, alltid avgrenset til en paragraf («beredskapsmyndigheten») | docs/33 §4.1, funn 7 |
| `klasse` | Gruppe aktører, enten listet (ekstensjonal) eller gitt ved kriterium (intensjonal), f.eks. «kommunene» | docs/33 §4.1, §4.2 |
| `omrade` | Område med type og kode. Identitet = (type, kode), ikke navn | docs/33 §4.1; #312 |
| Kantkategori R | Struktur uten myndighet: eies_av, ledes_av, sekretariat_for, rapporterer_til, etterfolger, representerer, har_delegert_til, forvaltes_av | docs/33 §4.3; #341, #352 |
| Kantkategori K | Kompetanse: A kan X, eventuelt overfor B, når det gjelder Y. Familier: beslutning, struktur, oppnevning, styring, normgivning, kontroll, klage og overprøving, vedtak, sanksjon | docs/33 §4.3 (P1, P2); #341, #352, #355 |
| Kantkategori P | Plikt overfor en motpart: samarbeid, avtale, betaling, bistand, informasjon, konsultasjon. Har modalitet skal/kan/bør | docs/33 §4.5; #353 |
| Kantkategoriene M, O, A, G, I og T | M medlem_av · O bestar_av · A har_ansvarsomrade, har_jurisdiksjon, har_sete_i · G del_av, har_organ, settes_med, tilhorer · I innehar · T skal_ha (gjelder på klassenivå) | docs/33 §4.2–4.3 |
| Felt på K | `normform`, `undertype` (valg, utnevning, konstitusjon, avskjed, anke, tilbakekall …), `grunnlag` (offentligrettslig eller privatrettslig), `delegerbar`, `familie`, `fvl_kategori` (forskrift, enkeltvedtak, ikke_vedtak) | docs/33 §4.3; #341, #352, #355 |
| Delegeringsadgang: tekstfunn eller slutning | `uttrykkelig_tillatt`, `uttrykkelig_forbudt`, `ikke_regulert`, pluss en egen, merket slutning («Kongen» uten «i statsråd») | #335, beslutning 2026-10-09. **Ikke bygget**: i dag er feltet bool |
| `polaritet` positiv/negativ | «kan ikke instruere», «kan ikke delegeres» | docs/33 §4.3, funn 6 |
| `modalitet` skal/kan/bør | Bare på P. Strukturkanter har ingen modalitet | docs/33 §4.5; #355 pkt. 8 |
| Hjemmel | `hjemmel_eid` sier hvor det står. Avgrensningen (paragrafspenn og tekst) sier hva kanten gjelder for | docs/33 §4.3 (#341 «hjemmelssted og avgrensning») |
| Kilde utenfor korpus | Art: kgl_res, instruks, tildelingsbrev, vedtekter, styrevedtak, forarbeider, nettside_annet. Dokumentasjon: primær eller sekundær. Gjensidig utelukkende med hjemmel-eId | docs/33 §4.3; CHECK `ck_strukturkanter_kilde` |
| Status og proveniens | `foreslatt_av_ai`/`validert`; oppdagelseskilde `manuell`, `monster:<id>` eller `ki:<modell>`; `gyldig_fra`/`gyldig_til` | docs/33 §4.3, §5.3; docs/03 §1.14 |
| Utenfor strukturlaget | Møteplikt, saksforberedelse, partsrettigheter, ankeadgang (partsposisjon), plikter for private | docs/33 §4.4; #355 pkt. 5 |

### 1b. Regellaget: hva som skal avgjøres

| Begrep | Definert i |
|---|---|
| Regelkilde ⊃ rettskilde (lov, forskrift, rettspraksis, forarbeider, teori) | docs/01 §3–4 |
| Regel = vilkår + rettsfølge + unntak + definisjoner | docs/01 §5–9 |
| Vilkår (bladnode; formell eller materiell; regelbasert, skjønnsbasert eller hybrid) | docs/01 §6, §5.3; docs/03 §1.8 |
| Skjønnsgrunnlag, skjønnsmoment, avklaringsbehov | docs/01 §6.1 |
| Regelnode (OG, ELLER, IKKE), unntak (`gjelder_regel` + `betingelse`), invariantene INV-1 til INV-7 | docs/01 §3, §5.3–5.4; docs/03 §1.9–1.12 |
| Rettsfølge, vedtak, vedtaksgrunnlag, vedtaksvirkning | docs/01 §7, §15.1 |
| Begrep (SKOS; faktabegrep eller handlingsbegrep) | docs/03 §1.3 |
| Presedens (AKN `judgment`, `rettskildevekt` fra KL-RETTSKILDEVEKT) | docs/03 §1.7. **Ikke bygget** (byggesteg 3) |
| Kandidater som er utsatt: deontisk dimensjon, unntak som overstyringsrelasjon, mapping til LegalRuleML | docs/10 «Hva som er tatt inn»; docs/01 §5.8 |

### 1c. Kildelaget: hvor det står

| Begrep | Definert i |
|---|---|
| Rettskilde (`kildetype`: Lov, Forskrift, Rundskriv, Presedens, Virksomhetsdokument; `eli`; `aknXml`; `status`) | docs/03 §1.1 |
| eId-konvensjonen (ELI som rot) | docs/08 §1.2; `LovdataIdentifikatorer.cs` |
| AKN-serialisering (FRBR, TLCOrganization, proprietary, `regelIde:`-navnerom) | docs/08 §1.1; `AknXmlSkriver.cs`; validert mot akomantoso30.xsd (docs/15 §9.5) |
| Versjonering på dokumentnivå, `tekst_hash` | docs/08 §2.1, §3.4 |
| Håndbok-kommentar (`dokumenttype`; `bindende` utledet av den) | docs/03 §1.1.1 |
| Tekst-tag (W3C-sitatvelger, `<term>`) | docs/03 §1.2 |

---

## 2. Kildelaget: slik bruker vi ELI og AKN i dag

| Tema | Hos oss (kode) | Standarden | Avvik og konsekvens |
|---|---|---|---|
| Verk-ID | `https://lovdata.no/eli/lov/1989/06/02/27`, utledet av Lovdatas datokode (`AvledEliFraDatokode`) | ELI-ontologien fastsetter ikke URI-malen; den er nasjonal. Lovdatas ELI (merket «BETA») har **ingen jurisdiksjonssegment** (korrigert). Malen er `/eli/{type}/{year}/{month}/{day}/{natural_identifier}/{level…}/{version}/{point in time}/{language}/{format}`, der `version` er `oj` eller `consolidated`, `point in time` er en tidligere konsolidert versjon angitt med dato, `language` er `no`, `nb` eller `nn`, og `level` er f.eks. `section/152` («doesn't currently provide any functionality») (v) [Lovdata-ELI]. Lovdata er nasjonal ELI-koordinator (docs/15 §9.4) | Ingen avvik på verksnivå. Men Lovdatas mal har allerede plass til versjonsdato og nivå, se de to neste radene og «Seksjons-ELI» |
| Uttrykks-ID (FRBR Expression) | Verk-ID + `/nor`, **uten dato** (`AknXmlSkriver.SkrivMeta`: `expressionUri = m.Eli`) | AKN: `…/nor@ÅÅÅÅ-MM-DD`. Språkkoden er ISO 639-2 alpha-3, og `@` er påkrevd. **Uten versjonsidentifikator betyr IRI-en «den versjonen som gjelder når IRI-en slås opp»**, og en naken `@` betyr originalversjonen (v) [AKN-NC §4.6.1]. ELI: hver versjon er en `eli:LegalExpression` (⊑ `Expression`), knyttet til verket med `eli:is_realized_by`, og har `eli:version_date` (v) [ELI] | **Alle konsoliderte versjoner får samme uttrykks-ID, og etter AKN-NC betyr den ID-en «gjeldende versjon».** Historiske versjoner blir dermed feilidentifisert, ikke bare uidentifisert. I tillegg er `/nor` verken Lovdata-ELI (språk `no`/`nb`/`nn`, versjonsdato som eget segment) eller AKN (`nor@dato`) [Lovdata-ELI; AKN-NC §4.6.1]. docs/08 §1.1 viser `/akn/no/act/1989-06-02/27/nor@2026-07-20`, som er riktig form, men koden skriver ELI-URL uten @dato. Dokumentet og koden sier altså ulike ting |
| FRBR-dato | Verket får ikrafttredelse (merket ærlig), eller `9999-01-01 name="ukjent"`. Uttrykket får `konsolidering` | AKN: verkets `FRBRdate` er «original creation date» (v) [AKN-NC §4.5]. ELI: `date_document` er «date of adoption or signature» (v) [ELI] | Bevisst og dokumentert, fordi vi ikke gjetter vedtaksdato. Merk at ikrafttredelse ikke er det standardene ber om her |
| eId-format | `kap-1` (lokal) · `{ELI}/§10-2` · `…/ledd-2` · `…/punkt-1` (løpenummer, ikke bokstav) | AKN-NC: `[prefiks __] element_ref [_ nummer]`. Forkortelsene `art` (article), `para` (paragraph), `chp` (chapter) og `sec` (section) er (v), mens `point` og `list` skrives fullt (v) [AKN-NC §5.4, §5.4.2]. Lovdata-paragraf = `<article>`, så formen er `art_10-2__para_2__…` og **ikke `sec_`**. Nummeret tas fra `<num>`, uten tegnsetting og overflødige tegn (v) [AKN-NC §5.4.3]. `list_N` som eget ledd foran `point_a`: Vocabulary-eksemplene bruker det (`…__list_1__point_a`, `art_10__para_1__list_1__point_1`) [AKN-1 §5.12, §5.10.1], men et eksempel i AKN-NC §4.6.1 utelater det (`art_3__para_5__point_c`). Prefiksregelen lar forfatteren velge konteksten (v, med denne uklarheten) | Tre avvik: (1) Vi blander lokale og globale ID-er: kapitlet er lokalt, paragrafen er en full URL. (2) Tegnet «§» og «/» i eId. «§» ville blitt strippet etter nummerregelen. (3) Punktet nummereres med indeks, ikke med `<num>`-verdien (a, b …). Det strider mot §5.4.3 |
| Lister og #361 | `<list>` skrives som **søsken etter** `<paragraph>` i `<article>` (`SkrivLedd`), og tekst etter lista limes inn i leddteksten (949 tilfeller i 407 dokumenter) | Hierarkisk liste: `<list>` med `intro`, `point`/`indent` og `wrapUp` (v) [AKN-XSD, type `hierarchy`; AKN-1 §5.12]. blockList: `<blockList>` med `listIntroduction?`, `item+` og `listWrapUp?` (v) [AKN-XSD `blockListType`]. **Et hierarkisk element har *enten* `<content>` *eller* `intro` → underelementer → `wrapUp`, aldri begge** (v) [AKN-XSD `hierarchy`]. Forkortelser: `wrapUp` → `wrapup`, `intro`/`listIntroduction` → `intro`. For `listWrapUp` er spesifikasjonen selvmotsigende: `wrap` i forkortelsestabellen, men `wrapup` i listen over delte element_ref og i Vocabulary-eksemplet `…__list_1__wrapup` [AKN-NC §5.4.2; AKN-1 §5.12]. Det finnes **intet element som heter `wrap`** | #361 bestemmer en egen avslutningsnode med eId `{ledd}/avslutning`. AKN-formen blir `…__list_1__wrapup` (korrigert: var «ikke verifisert»). Akseptansekriterium 4 i #361 blander de to listetypene. Velg `<list>` med `intro`/`wrapUp`, og legg lista *inne i* `<paragraph>`, slik eksemplet i docs/08 §1.3 gjør. Leddteksten foran lista må da ligge i `<intro>` (i ledd eller liste) og ikke i `<content>`, ellers validerer ikke dokumentet |
| Kryssreferanser | `<ref href="#{full ELI-eId}">` | En intern referanse er `#eId` (v) [AKN-NC §4.10.1]. En referanse til et annet dokument er alltid global (v) [AKN-NC §4.10]. En del av et annet dokument angis med porsjonssyntaksen `~eId` etter verks- eller uttrykks-IRI-en, f.eks. `/akn/eu/act/2003-11-13/87/eng@/~art_3` (korrigert: ikke bare «IRI uten #») [AKN-NC §4.8.4] | `#https://…` er verken det ene eller det andre |
| Opphevelse og tid | `regelIde:opphevet` og `regelIde:opphevetDato`; ingen `<lifecycle>` eller `<temporalData>` | AKN: `lifecycle/eventRef`, `temporalData/temporalGroup/timeInterval`, attributtet `period` (v) [AKN-XSD]. ELI: `in_force` med verdiene in force / partially in force / not in force, `first_date_entry_in_force`, `date_no_longer_in_force` (v) [ELI] | Åpent punkt i docs/08 §6 pkt. 2. Ikrafttredelse per paragraf og overgangsregler finnes ikke i modellen |
| Endringer | En ny konsolidert versjon er en ny `rettskilder`-rad (`erstatter`-kjeden); `changesToParent` blir en proveniensrad | ELI: `amends`/`amended_by`, `consolidates`/`consolidated_by` (v) [ELI]. AKN: `passiveModifications`/`textualMod` (v) [AKN-XSD] | Vi vet *at* noe er endret (`tekst_hash`), men ikke *hvilken endringslov* som endret det |
| Aktører i AKN | `TLCOrganization href="/ontology/organization/no/stortinget"` | TLCOrganization, TLCRole, TLCLocation, TLCConcept, TLCTerm (v) [AKN-XSD; AKN-NC §4.11.1]. De er bare referansestumper uten semantikk for kompetanse eller plikt | Ikke koblet til `Virksomhet`/`Begrep`. Dette er en naturlig skjøt (se §3) |
| Seksjons-ELI | Egen utvidelse, låst. Feltet `offisiell_eli` er klart for når Lovdata publiserer | `eli:LegalResourceSubdivision` (⊑ `LegalResource`, `WorkSubdivision`), koblet med `has_part`/`is_part_of` (v) [ELI]. Lovdatas mal har et `level`-segment (`/eli/lov/2005/05/20/28/section/152`), men det har foreløpig ingen funksjon (v) [Lovdata-ELI] | docs/08 §1.2, §6 pkt. 1: venter på Lovdata. Vår `{ELI}/§10-2` følger ikke Lovdatas nivåsyntaks (`section/10-2`) og bør mappes mot den |
| Intern inkonsistens | docs/15 §9.2 (Bergen-retningslinjer) bruker AKN-formen `kap_1` / `kap_4__para_7` | – | Vi har to eId-konvensjoner i dokumentasjonen |

**Hovedpoenget:** ELI-forankringen på verksnivå er solid. eId-konvensjonen er en *egen utvidelse* som ligger nær ELI-tenkningen, men bryter med AKN NC. Skal regel-ide bli en referanseimplementasjon for en norsk standard, trenger vi en **dobbel nøkkel**:
- vår eId som stabil intern identitet (docs/08 §1.2.1),
- en avledet AKN-eId etter navnekonvensjonen,
- og uttrykks-ID med `@dato` og versjonskjede.

**Funnet ved verifiseringen:** AKN har selv en dobbel nøkkel [AKN-NC §5.3, §5.5.2]. `eId` er uttrykksnivå og *skal* endres når et element omnummereres. `wId` er verksnivå, endres aldri og bygges med samme syntaks fra «master expression». Vår regel om at eId aldri endres (docs/08) gir altså vår eId samme rolle som AKN sin `wId`, men under navnet til AKN sin `eId`. Ved eksport bør vi derfor skrive en avledet AKN-`eId` per versjon, og en `wId` avledet fra vår stabile ID.

Å endre eksisterende eId-er bryter docs/08 sin regel om at eId ikke endres, og bør ikke gjøres.

---

## 3. Krysstabell

Forklaring av samsvarskolonnen: **lik** · **delvis** · **mangler hos dem** (vi har noe standarden ikke har) · **mangler hos oss**.

| Vårt begrep | LKIF Core | UFO-L | LegalRuleML | ELI / AKN | Samsvar | Merknad |
|---|---|---|---|---|---|---|
| **K kompetanse** (A kan X overfor B) | `Competence` (finnes ikke i spesifikasjonen). Nærmest er `Potestative_Expression` («attributes a power to some agent») med underklassene `Hohfeldian_Power`, `Enabling_Power` og `Declarative_Power` (v) [LKIF norm.owl]. `Potestative_Right` (korrigert) er en `Enabling_Power` «meant to benefit the holder», og passer derfor dårlig for offentlig kompetanse | Power, med Subjection hos motparten, i en `Legal Power-Subjection Relator` (v) [UFO-L 2018 §3; UFO-L 2023 §4.1]. «Power is created by a competence norm» [UFO-L 2018 §3]. UFO-L 2023 bruker «legal power» og «legal competence» om hverandre [UFO-L 2023 §2] | `Authority` (v) er «a person or organization with the power to create, endorse, or enforce Legal Norms», men er metadata om normens opphav og autentisitet [LRML §3.4, §4.3.4]. LegalRuleML har **ingen** Power-, Competence-, Immunity- eller Disability-element (v) [LRML-XSD]. `Permission` (v) er en tillatelse, ikke en rettsmakt | – | delvis | Det nærmeste er Hohfelds power/subjection i UFO-L. Bruk ikke `Authority` for K. Motparten vår (til-noden) tilsvarer innehaveren av Subjection (Subjection Holder) |
| **K-familiene** (oppnevning, styring, kontroll, klage …) | delvis i legal-action: `Delegation`, `Mandate`, `Decision` (v) [LKIF legal-action.owl] | – | – | – | mangler hos dem | Norsk forvaltningsrett skiller personell, materiell og prosessuell kompetanse (Eckhoff/Smith, *Forvaltningsrett*). Familiene våre er en annen akse, ordnet etter disposisjonstype. Kan bli en norsk utvidelse |
| **P plikt overfor motpart** | `Obligation`, `Obligative_Right` (v) [LKIF norm.owl]. `Obligative_Right` er en rettet plikt («obligatory, toward k, that j does A») | Duty med korrelatet Right i en `Right-Duty to an Action Relator`. Posisjonene heter bl.a. «Duty to Act», «Duty to Omit» og «Right to an Omission» (v) [UFO-L 2018 §3, fig. 3] | `Obligation` med `Bearer` (den plikten «is primarily directed» mot) og `AuxiliaryParty` (v) [LRML §3.4; LRML-XSD]; `SuborderList` (v) | – | delvis | Hos oss er P en *aktørnær projeksjon* (docs/33 §4.5 pkt. 2). Korrelatet (retten hos motparten) lagres bevisst ikke (§4.4: å slutte gjensidighet). Det samsvarer med UFO-L, der Right er motstykket til Duty, men UFO-L modellerer begge sider |
| **F oppgave/funksjon** ([Ny, #368]) | `Function` [LKIF role.owl] er **nært beslektet**: en funksjon et organ eller en rolle har. Klassen står i krysstabellens rolle-rad (under) og er ikke merket (v) — definisjonsteksten er ikke kontrollert mot `role.owl` i #368 | – (ingen motpart: UFO-L har posisjoner med korrelat, en oppgave har ingen) | – (`Figure` er en funksjon *innehatt av* en aktør, ikke en oppgave lagt til en aktør) | – | mangler hos dem / delvis | En norsk utvidelse, begrunnet i forvaltningsretten (grunnlagsdokumentet §7.8): oppgave, kompetanse, plikt og kravsrett er fire ting. F gir aldri K eller P i seg selv. Definisjon, typer og grenser: docs/profil/02-begrepsmanual.md §F. Ved eksport: `Function`, ikke `Obligation` eller `Hohfeldian_Power` |
| **Polaritet negativ** | `Prohibition` og `Disallowed` (v) [LKIF norm.owl]. `Immunity` ⊑ `Hohfeldian_Power` har kommentaren «Also called Disabilities» (v): LKIF skiller altså *ikke* disability fra immunity | Disability og Immunity er korrelater i **én** relator, `Disability-Immunity Relator` (P8-DI-LR). «Kan ikke» (disability) hos A er det samme forholdet som immunity hos B (v) [UFO-L 2023 §4.2]. Forbud er en annen posisjon: «Duty to an Omission» [UFO-L 2023 §4.2] | `Prohibition` (v) [LRML §3.4]. Ingen disability eller immunity | – | delvis (korrigert) | Vi bruker én negativ flagg for to ulike ting, ikke tre. (1) **Disability–immunity**: «kan ikke delegeres» og «kan ikke instruere» er begge mangel på kompetanse. Hos den andre parten heter det immunity, altså uavhengighet. Rettsvirkningen er at handlingen er **ugyldig** («void»). (2) **Forbud** («skal ikke» på P): et brudd er **ulovlig** og kan sanksjoneres [UFO-L 2023 §4.2: «A violation of a Right–Duty is illegal behaviour … an attempt to change a legal relation in the presence of a Disability–Immunity is a void act»] |
| **Modalitet skal / kan / bør** | Obligation / Permission (v) [LKIF norm.owl]. Merk at LKIF modellerer `Obligation` som ekvivalent med `Prohibition` og underklasse av `Permission`. Nærmest «bør» på kildenivå er `Soft_Law` (⊑ `Legal_Source`, «weaker» bindende kraft) (v) | Duty / Permission (v) [UFO-L 2018 §3] | `Obligation` / `Permission` / `Prohibition` / `Right` (v) [LRML §4.2.3]. «bør» har **ingen** egen operator (v). LegalRuleML er likevel «neutral about the different subclasses of the deontic operators»: en egen modalitet kan angis med `@iri` på deontikkelementet eller med `<lrml:appliesModality>` i en `Association` [LRML §4.2.3.1, eks. 6–7] | – | delvis | «kan» i P betyr adgang (permission). «kan» i K betyr kompetanse (power). Skillet er riktig hos oss (#353 pkt. 1). «bør» er en norsk utvidelse, men den kan eksporteres til LegalRuleML gjennom `@iri`/`appliesModality` uten å bryte skjemaet |
| **Ordning** | `Legal_Person` (v) [LKIF legal-action.owl] passer ikke. Ingen ordningsklasse i de kontrollerte modulene (finnes ikke i spesifikasjonen) | – | – | – | mangler hos dem | En begrunnet norsk utvidelse: ikke-aktør med funksjon, `forvaltes_av` og `tilhorer` bare når hjemlet |
| **Rolle / innehav (I)** | `Legal_Role`, `Social_Legal_Role`, `Professional_Legal_Role` (v) [LKIF legal-role.owl]; `Role`, `Organisation_Role`, `Function` [LKIF role.owl] | `Legal Role` og rolletyper som `Power Holder`, `Duty Holder` og `Immunity Holder` (v) [UFO-L 2018 §3; UFO-L 2023 §4]. «Anti-rigid» er UFOs generelle rollebegrep og står ikke i UFO-L-artiklene (ikke verifisert) | `Figure` (v) er «an instantiation of a function by an Actor» (f.eks. senator). `Role` (v) er «a function of or part played by an Actor relative to a LegalRuleML expression» (f.eks. forfatter av en regel), koblet med `filledBy` [LRML §3.4, §4.3.2] (korrigert: rollen knytter ikke agenten til figuren) | AKN `TLCRole` (v) [AKN-XSD] | delvis | Det er LegalRuleML sin `Figure` som ligger nær vårt `rolle`-begrep (funksjon innehatt av en aktør). Vi har i tillegg avgrensning per paragraf (funn 7), som ingen av dem har |
| **Område / jurisdiksjon (A, O)** | Ingen `Jurisdiction`-klasse (finnes ikke i spesifikasjonen). `time-modification.owl` har bare endringstyper som begrenser eller utvider en norms virkeområde til en jurisdiksjon | – | `Jurisdiction` (v): «a geographic area or subject-matter over which an Authority applies its legal power», brukt til å annotere regler [LRML §4.3.3] | `eli:jurisdiction` (v) er «the jurisdiction from which the legal resource originates», med ATU-koder [ELI]. AKN `TLCLocation` (v) | delvis | Våre A-kanter sier hvilket område en *aktør* har ansvar for. LegalRuleML sier hvor *normen* gjelder, og kan også angi saksområde. ELI sier hvor *dokumentet kommer fra*. Områdeinndeling (O bestar_av, valgkrets ved komplement) finnes ikke hos dem |
| **Hjemmel (eId)** | `Legal_Source` (v) [LKIF norm.owl] | – | `LegalSource` + `Association` (`appliesSource`, `toTarget`) og `Context` (v) [LRML §4.3.1, §4.4] | `eli:based_on` («empowered by another one, typically a constitution, a treaty or an enabling act») / `eli:basis_for` (v) [ELI] | delvis | Vårt skille mellom *hjemmelssted* og *avgrensning* (#341) finnes ikke som to felt hos dem |
| **Kilde utenfor korpus** (art + primær/sekundær) | `Legal_Source`-subklasser: `Legal_Document` (med bl.a. `Contract`, `Decree`, `Directive`, `Code_of_Conduct`), `Customary_Law`, `Legal_Doctrine`, `Precedent`, `Soft_Law`, `International_Agreement`, `Proclamation` (v) [LKIF norm.owl]. `Custom` (korrigert) er ikke en `Legal_Source`, men et `expression:Medium`. Det er `Customary_Law` som er kilden | – | `LegalSource` krever IRI (`sameAs`). Kilder uten IRI føres som `LegalReference` med `refID` og `refIDSystemName` (navnekonvensjonen). Ikke-rettslige kilder føres som `Source` (korrigert) [LRML §4.3.1, eks. 15–17] | Ingen ELI for kgl.res. eller vedtekter | delvis (korrigert, var «mangler hos dem») | Skillet mellom primær og sekundær dokumentasjon er vårt eget og godt begrunnet (feilen med Tilsynsutvalget, docs/33 §4.3). LegalRuleML sin `LegalReference` passer for kilder uten ELI. Artslisten bør mappes mot LKIF sine kildetyper |
| **Tekstfunn vs. slutning** (#335) og `foreslatt_av_ai`/`validert` | – | – | Styrke via `hasStrength`: `StrictStrength`, `DefeasibleStrength` og `Defeater` (v; `Strength` er bare overbegrepet) [LRML §3.4, §4.2.1]. Det handler om regelstyrke, ikke epistemisk status | – | mangler hos dem | Ikke det samme. Defeasibility er *rettslig* («gjelder med mindre»). Slutning hos oss er *epistemisk* («teksten sier det ikke»). Bland dem aldri. Vår proveniens er nærmest W3C PROV |
| **Unntak** | `Exception` finnes to steder (v): i `expression.owl` som epistemisk rolle («an exception is a rule which has an exception relation to another rule» i LKIF rules), og i `time-modification.owl` som endring av virkeområde [LKIF] | – | `Override` («a Legal Rule takes precedence over another Legal Rule») og `OverrideStatement` (v); `DefeasibleStrength`/`Defeater` (v) [LRML §3.4, §4.2.1] | – | delvis | Vi har unntak som egen node med kardinalitet 1 (INV-3). LegalRuleML har en overstyringsrelasjon mellom regler, som docs/10 anbefaler (kandidat 4) |
| **Regel / rettsfølge** | `Norm` (v) [LKIF norm.owl] | `Legal Relator` (v) [UFO-L 2018 §3] | `PrescriptiveStatement` (plikt, tillatelse, forbud) og `ConstitutiveStatement` («defines concepts and does not prescribe behaviors») (v) [LRML §3.4] | – | delvis | Regelnoden vår skiller ikke normtyper. Definisjon (docs/01 §9) ≈ ConstitutiveStatement. Rettsfølge «plikt til betaling» ≈ PrescriptiveStatement |
| **Sanksjon / reaksjon** | – | – | `PenaltyStatement`, `ReparationStatement`/`Reparation`, `Violation` og `Compliance` (v) [LRML §3.4, §4.2.3.2; LRML-XSD]. Det finnes ikke noe eget `Penalty`-element i skjemaet | – | mangler hos oss | Familien sanksjon er *kompetansen* til å reagere. Selve reaksjonen på brudd er ikke modellert (#355 pkt. 3 sier at sanksjon er funksjon). Hører hjemme i regellaget |
| **Skjønn** (skjønnsgrunnlag, skjønnsmomenter) | – | – | – | – | mangler hos dem | Ingen standard har skjønnsmomenter. En klar norsk styrke |
| **Delegering** (K delegering; R har_delegert_til) | `Delegation` (v) [LKIF legal-action.owl] er en *handling* («entrust a task or responsibility to some other person»), dvs. en `Public_Act` og `Legal_Speech_Act` med en `Public_Body` som aktør | Skillet mellom opprinnelige og avledede («derived») power-subjection-relasjoner. De avledede krever en grunnleggende hendelse (v) [UFO-L 2023 §3] | – | – | delvis | Vi skiller adgang fra gjennomført delegering. LKIF har bare den gjennomførte handlingen (v). Delegeringsadgang må uttrykkes som power (UFO-L) eller som en norsk utvidelse |
| **Tidsdimensjon** (`gyldig_fra`/`gyldig_til` på kanter) | `time.owl` og `time-modification.owl` (v) [LKIF], med bl.a. `Enter_in_Force_Date`, `In_Force_Interval`, `Repeal` og `Retroactivity` | – | `TemporalCharacteristic` (status som `InForce` eller `Efficacious`, utvikling som `Starts`) og `ruleml:Time` i `lrml:Times` (v). `TimeInstant` (finnes ikke i spesifikasjonen) [LRML §4.3.5] | ELI `in_force` (v) [ELI]; AKN `temporalGroup` (v) [AKN-XSD] | delvis | Vi har tid på kantene, men ikke på kildenodene (se §2) |
| **Aktør i dokumentet** | `Public_Body`, `Legislative_Body` (v) [LKIF legal-action.owl] | `Legal Agent` (v) [UFO-L 2023] | `Agent` (v) [LRML §4.3.2] | `eli:passed_by` (v) [ELI]; AKN `TLCOrganization` (v) | delvis | Koble `Virksomhet` til `TLCOrganization`/`eli:passed_by` med orgnr der det finnes |

---

## 4. Rettslig metode: hva modellen allerede koder, og hva som mangler

**Bakgrunn:** Eckhoff, *Rettskildelære*, 5. utg. ved Jan E. Helgesen (Universitetsforlaget 2001). Boe, *Grunnleggende juridisk metode* (Universitetsforlaget, 5. utg. 2025). Eckhoff/Smith, *Forvaltningsrett* (personell, materiell og prosessuell kompetanse). Ingen sidetall er oppgitt.

### Allerede kodet

| Metodeelement | Hvor | Merknad |
|---|---|---|
| Rettskildefaktorer (delvis) | `kildetype`; docs/01 §4; KL-RETTSKILDEVEKT (bindende / tungtveiende / veiledende / illustrerende / historisk, docs/02 l. 335) | Vekt finnes bare for presedens, og den er ikke bygget |
| Hjemmelskrav og legalitet | Hjemmel-eId er påkrevd på **alle** kanter, ellers kilde utenfor korpus (CHECK) | Gjelder strukturlaget. Regellaget krever `juridisk_grunnlag`, men det kobles ikke til K |
| Hjemmelens dokumentasjonsgrad | Kilde utenfor korpus: art + primær/sekundær | Vårt eget, godt begrunnet |
| Bevisrekkefølgen (jurist-utfordrer §2) | Sitat → ledd → bestemmelse → interne henvisninger → definisjoner → samme kilde → annet korpus → forarbeider → rettspraksis → prinsipper/teori | Dette er i praksis en **operativ rettskildelære for klassifisering**. Den er ikke en vektlære: den sier hvor man leter først, ikke hva som veier tyngst ved motstrid |
| «Ikke gjett» | CLAUDE.md §0, §8; docs/33 §0; `referent: null` med `oppløsning`; FORMAT.md | Et sterkt epistemisk prinsipp som standardene ikke har |
| Tekstfunn vs. slutning | #335, jurist-utfordrer §3 («[slutning]») | Delvis bygget |
| Skjønn | docs/01 §6.1 | Bare for vilkår, ikke for «kan» i K (se under) |
| Fvl. § 2-kategori | `fvl_kategori` på typen | Bundet til forvaltningsloven av 1967. **Ny forvaltningslov (LOV-2025-06-20-81, kunngjort 20.06.2025) er ikke i kraft.** Lovdata merker den «Ikke i kraft», med ikrafttredelse «Kongen bestemmer». § 99 sier at loven trer i kraft når Kongen bestemmer, at bestemmelsene kan settes i kraft til ulik tid, og at 1967-loven oppheves fra samme tid (v) [Lovdata-fvl, kontrollert 2026-10-09]. Kategoriene enkeltvedtak og forskrift står i ny § 7 (i dag § 2). Ny lov kapittel 3 (§§ 22–24) lovfester delegering, blant annet adgangen til å delegere i § 23. Det berører #335: «ikke_regulert» i en særlov vil da falle tilbake på § 23. Kategorien og delegeringsfeltet må få tidsangivelse |

### Mangler

| Mangler | Hvorfor det betyr noe | Nærmeste standard |
|---|---|---|
| Vekt per rettskildefaktor (forarbeider, forvaltningspraksis, sedvane, reelle hensyn, EØS-rett og folkerett) | «Rettskilde» er i dag en dokumenttype, ikke en faktor med vekt | LKIF `Legal_Source`-subklasser (v) [LKIF norm.owl], bl.a. `Mandatory_Precedent`/`Persuasive_Precedent` og `Soft_Law`. Det er typer, ikke vekt |
| Kollisjonsprinsipper (lex superior, lex specialis, lex posterior) | Vi kan ikke si at forskrift viker for lov, eller at en særregel går foran. Unntak finnes bare innenfor én regel | LegalRuleML `Override`/`OverrideStatement` (v) [LRML §4.2.1]. LKIF har `normatively_strictly_better` mellom normer (v) [LKIF norm.owl] |
| Trinnhøyde og delegert normgivning | En forskrift «gitt i medhold av» § X bør kobles til K normgivning med normform forskrift | `eli:based_on` / `basis_for` (v) [ELI] |
| Gyldighet i tid på kildenivå | Ikrafttredelse per paragraf, overgangsregler, spørsmålet «hvilken rett gjaldt på dato D» | ELI `in_force` og datofeltene (v) [ELI], AKN `temporalGroup` (v) [AKN-XSD], LegalRuleML `TemporalCharacteristic` (v) [LRML §4.3.5] |
| Tolkningsprinsipper (ordlyd, formål, system, utvidende, innskrenkende, analogi, antitetisk) | Juristrunden *bruker* dem, men begrunnelsen lagres ikke typet | Ingen standard dekker dette godt |
| Praksis og forarbeider som importerte kilder | Forarbeider finnes bare som `KildeUtenforKorpusType`. Presedens er spesifisert, men ikke bygget | AKN `judgment`, `doc` (v) [AKN-XSD] |
| Skjønn i kompetansen (fritt skjønn vs. rettsanvendelsesskjønn i «kan») | K har i dag ingen markør for skjønn | – |
| Ugyldighet og virkning av feil | Hva skjer når kompetanse brukes feil | UFO-L Disability–Immunity: handling uten kompetanse er «void» (v) [UFO-L 2023 §4.2]. LegalRuleML `Violation` dekker bare pliktbrudd, ikke ugyldighet (v) [LRML §3.4] |

---

## 5. Avvik som kan bli konflikter

| Vårt valg | Standarden | Risiko | Anbefaling |
|---|---|---|---|
| Kompetanse (K) og plikt (P) er to kategorier, og R er uten myndighet (#341, #353) | UFO-L: fire par legale posisjoner, Right/Duty, NoRight/Permission, Power/Subjection og Disability/Immunity (v) [UFO-L 2018 §3]. Hohfelds liberty uttrykkes i UFO-L som en sammensatt `Liberty Relator` av NoRight–Permission-relatorer (korrigert) [UFO-L 2018 §3] | Lav. K = power, P = duty, så delingen stemmer | **Map.** Dokumenter K→Power og P→Duty. Korrelatene utledes ved eksport, de lagres ikke |
| Én polaritetsflagg | Disability og immunity er to sider av **samme** relator. Prohibition (Duty to an Omission) er en annen posisjon, med en annen rettsvirkning: ugyldig mot ulovlig (korrigert) [UFO-L 2023 §4.2]. LKIF slår disability og immunity sammen [LKIF norm.owl]. LegalRuleML har bare `Prohibition` [LRML] | Middels: eksport til UFO-L blir tvetydig | **Map** med en utledningsregel: K negativ = Disability–Immunity-relator. Disability ligger hos fra-noden. Har kanten en motpart («kan ikke instruere/omgjøre»), er motparten Immunity Holder. Uten motpart («kan ikke delegeres») er det bare disability. P negativ = prohibition / Duty to Omit. Kontrolleres av juristagenten |
| `ordning` som nodetype | Ingen tilsvarende i LKIF eller UFO-L (finnes ikke i spesifikasjonen; lett etter i LKIF-modulene og UFO-L 2018/2023) | Lav | **Norsk utvidelse.** Begrunnelsen står i #353 |
| Forelegging klassifiseres etter rettsvirkning (#355 pkt. 9) | LegalRuleML: konstitutiv vs. preskriptiv (v) [LRML §4.2.2] | Lav, og i tråd med standarden | **Map:** bindende svar → K (konstitutiv), rådgivende → P konsultasjon |
| Kompetansefamilier ordnet etter disposisjon | Norsk lære: personell, materiell og prosessuell kompetanse. LKIF har ingen familie | Middels: jurister vil spørre etter tredelingen | **Map:** legg tredelingen som egen akse på typen (som `fvl_kategori`). Ingen ombygging |
| Rolle avgrenset per paragraf | LKIF og LegalRuleML har ikke avgrensning | Lav | **Norsk utvidelse** (funn 7) |
| A har_ansvarsomrade (aktør → område) | LegalRuleML `Jurisdiction` gjelder normen (område eller saksfelt der en Authority utøver makt). `eli:jurisdiction` gjelder hvor dokumentet kommer fra [LRML §4.3.3; ELI] | Middels: samme ord, tre betydninger | **Map** og ikke bruk ordet «jurisdiksjon» om A. Vurder om koden `har_jurisdiksjon` bør hete noe annet |
| eId-format | AKN NC: `eId` er uttrykksnivå og endres ved omnummerering. `wId` er verksnivå og endres aldri [AKN-NC §5.3] | Høy hvis vi skal være referanseimplementasjon | **Map:** behold vår eId (den har rollen til AKN sin `wId`), og avled AKN-`eId` per versjon og `wId` ved eksport |
| Uttrykks-ID uten @dato | AKN: uten versjon betyr IRI-en «gjeldende versjon» [AKN-NC §4.6.1]. Lovdata-ELI: `point in time` og `no/nb/nn` [Lovdata-ELI] | Høy for tidsspørsmål | **Juster** (nye importer). Tidligere data får en avledet ID |
| Tekstfunn vs. slutning | Ingen standard | – | **Norsk utvidelse**, og et salgspunkt. Hold det atskilt fra defeasibility |
| Unntak som node | LegalRuleML Override | Middels ved unntak fra unntak (energiloven har det, docs/33 funn 6) | **Juster senere** (docs/10, kandidat 4): legg til en overstyringsrelasjon ved siden av noden |

---

## 6. Åpne spørsmål til Johann

1. **Hvilket nivå skal standarden gjelde?** Strukturlaget (hvem har kompetanse og plikt overfor hvem), regellaget (vilkår og rettsfølge) eller begge? Krysstabellen viser at strukturlaget er der vi har mest som er nytt.
2. **Hohfeld som grunnmodell:** skal K og P eksporteres eksplisitt som UFO-L power og duty, med korrelatene utledet? Eller skal vi bare dokumentere en mapping?
3. **Polaritet:** skal den ene negative flagget deles i to, disability–immunity (ugyldig) og prohibition (ulovlig), eller skal det utledes ved eksport? Etter verifiseringen er skillet to-delt, ikke tre-delt.
4. **eId:** godtar du en dobbel nøkkel, der vi beholder vår eId (i AKN-rollen `wId`) og avleder en AKN NC-`eId`? Og skal avslutningsnoden i #361 få AKN-navnet (`wrapUp`, eId-ledd `wrapup`) fra starten?
5. **Versjonering:** skal uttrykks-ID-en få `@konsolideringsdato`, slik at «hvilken rett gjaldt på dato D» blir mulig? Det er en forutsetning for lex posterior og overgangsregler.
6. **Kompetanselæren:** skal tredelingen i personell, materiell og prosessuell kompetanse inn som egen akse på K-typene?
7. **Rettskildefaktorer og vekt:** skal standarden ha en typet liste over faktorer med vekt (utvidet KL-RETTSKILDEVEKT), eller holder bevisrekkefølgen til klassifisering?
8. **Navn:** skal en norsk standard bruke de engelske standardnavnene (power, duty, figure) eller norske navn med mapping? Det avgjør hvordan K, P og «ordning» presenteres utad.

---

## 7. Standardlandskapet: LOMO, SEMIC, ELI-ontologien, regellaget og Norden (sak #473)

[Ny, #473, 2026-11-12] Johann 2026-11-06: «hvorfor kom ikke LOMO opp da vi jobbet med norsk profil og bruk av standarder?» Årsaken er at §1–§6 ble skrevet for å vurdere de fem standardene Johann sendte. Det ble ikke gjort en egen bred gjennomgang av landskapet. Denne delen er den gjennomgangen. Kildene som skal sjekkes ved hver profilversjon, står i [08-standardlandskap-sjekkliste.md](08-standardlandskap-sjekkliste.md).

**Metode.**
- Primærkildene er hentet 2026-11-06 og 2026-11-12: spesifikasjonssidene, ontologifilene (Turtle eller RDF/XML), de offisielle repoene (lest via API) og API-ene til Lovdata, Brreg, SSB og Felles datakatalog. Hver kilde står i den utvidede verifiseringsloggen til slutt.
- Merkingen er den samme som i resten av notatet: (v) betyr slått opp i primærkilden, (ikke verifisert) betyr at kilden ikke kunne nås eller ikke sier noe.
- Koblingene bruker typene i grunnlagsdokumentet §5.7: **nært beslektet**, **snevrere** (vårt begrep er snevrere enn deres), **bredere** (vårt begrep er bredere) og **ingen match**. Ingen kobling er «lik». Krysstabellen i §3 bruker en eldre skala (delvis, mangler hos dem, mangler hos oss) og er ikke skrevet om.
- Alle koblingene i tabellene under har vært gjennom en juristrunde i to runder (CLAUDE.md §23). Utfallet står i §7.7, og alle kortene, innvendingene, svarene og replikkene ligger i [juristrunde-473-koblinger.json](juristrunde-473-koblinger.json).

**Nye kortnøkler:**
- **[LOMO]** Legal Obligation Metadata Ontology 1.1.0, spesifikasjon: https://drpm.pages.code.europa.eu/lomo/1.1.0/ (1.0.0: https://drpm.pages.code.europa.eu/lomo/1.0.0/). Ontologifil, vokabularer og SHACL lest fra **[LOMO-repo]** https://code.europa.eu/drpm/lomo, tagg `v1.1.0`: `ontologies/lomo-ontology.ttl`, `deontic-modality.ttl`, `temporal-function.ttl`, `result-category.ttl`, `digital-dimensions.ttl`, `shapes/lomo-shapes.ttl`, `CHANGELOG`, `LICENSE`
- **[IE-LOMO]** Interoperable Europe, nyhet 2026-11-04: https://interoperable-europe.ec.europa.eu/interoperable-europe/news/legal-obligation-metadata-ontology-lomo-jurix-20226
- **[JURIX26]** JURIX 2026, Toulouse 8.–10. desember: https://www.irit.fr/jurix2026/program/
- **[CPOV]** Core Public Organisation Vocabulary 2.1.2: https://semiceu.github.io/CPOV/releases/2.1.2/ (med `voc/core-public-organisation-ap.ttl`)
- **[CPSV-AP]** Core Public Service Vocabulary Application Profile 3.2.0: https://semiceu.github.io/CPSV-AP/releases/3.2.0/ (med `rdf/cpsv-ap.ttl`)
- **[CLV]** Core Location Vocabulary 2.1.1: https://semiceu.github.io/Core-Location-Vocabulary/releases/2.1.1/
- **[CPV]** Core Person Vocabulary 2.1.2: https://semiceu.github.io/Core-Person-Vocabulary/releases/2.1.2/ · **[CBV]** Core Business Vocabulary 2.3.0: https://semiceu.github.io/Core-Business-Vocabulary/releases/2.3.0/
- **[CCCEV]** Core Criterion and Core Evidence Vocabulary 2.2.0: https://semiceu.github.io/CCCEV/releases/2.2.0/
- **[ELI]** som før, men nå kontrollert at http://data.europa.eu/eli/ontology fortsatt leverer `owl:versionInfo "1.5"`. **[ELI-DL]** ELI Draft Legislation Ontology: http://data.europa.eu/eli/eli-draft-legislation-ontology (Turtle). **[ELI-reg-NO]** https://eur-lex.europa.eu/eli-register/norway.html
- **[eFLINT]** van Binsbergen, Liu, van Doesburg, van Engers, «eFLINT: A Domain-Specific Language for Executable Norm Specifications», GPCE 2020, https://doi.org/10.1145/3425898.3426958 (PDF: https://publications.tno.nl/publication/34639016/S2Rnu7/binsbergen-2020-eflint.pdf), og van Binsbergen mfl., arXiv 2511.12276. **[FLINT-ont]** https://gitlab.com/normativesystems/knowledge-modeling/flint-ontology (`flint.ttl`)
- **[ODRL-M]** ODRL Information Model 2.2, W3C Recommendation 15.02.2018: https://www.w3.org/TR/odrl-model/ · **[ODRL-V]** https://www.w3.org/TR/odrl-vocab/ · **[ODRL-WS]** W3C-workshoprapport «Future of ODRL» (juli 2026): https://www.w3.org/2026/ODRLWS/report.html
- **[LRML-RO]** Palmirani, «A LegalRuleML specialisation for reporting obligations» (mai 2024): https://interoperable-europe.ec.europa.eu/sites/default/files/news/2024-07/A%20LegalRuleML%20specialisation.pdf
- **[CPSV-AP-NO]** https://data.norge.no/specification/cpsv-ap-no · **[SKOS-AP-NO]** https://data.norge.no/specification/skos-ap-no-begrep · **[DCAT-AP-NO]** https://data.norge.no/specification/dcat-ap-no
- **[Lovdata-API]** https://api.lovdata.no/publicData og XML-dokumentasjonen https://api.lovdata.no/xmldocs

### 7.1 LOMO (Legal Obligation Metadata Ontology)

**Hva det er.** En ontologi fra Europakommisjonen for å beskrive tolkningen av regler i rettskilder: plikter, tillatelser, forbud, rettigheter, definisjoner og virkeområde. Hver regel knyttes til kilden med ELI. Formålet er å vise «who must do what, under which conditions, by when» [IE-LOMO].

**Status (v), kontrollert 2026-11-12:**
- Versjon **1.1.0**, utgitt 2026-10-21. 1.0.0 kom 2026-07-10 og var den første versjonen som ble erklært stabil [LOMO-repo, CHANGELOG].
- Namespace `http://data.europa.eu/leg/lomo#`, `owl:versionIRI` `http://data.europa.eu/leg/lomo/ontology/1.1.0`, `dcterms:creator` European Commission [LOMO-repo].
- **Lisens: CC BY 4.0** (`dcterms:license` og fila `LICENSE`). Lisensen ble byttet fra EUPL 1.2 til CC BY 4.0 2026-07-16 etter ønske fra EU Vocabularies [LOMO-repo, CHANGELOG].
- Repoet er offentlig og ble opprettet 2025-12-02. Prosjektet het tidligere LRMO (eksempeldata bruker fortsatt `data.europa.eu/lrmo/`).
- **Artikkelen** «Legal Obligation Metadata Ontology: a representation of legal rules with a practitioner perspective» (Nardin, Audrito, Abrard, Jacquet) er akseptert til JURIX 2026 (nr. 48 på listen over aksepterte artikler) [JURIX26]. Den publiseres med konferanseboka innen utgangen av 2026 [IE-LOMO].
- **Merking: foreløpig.** Ontologien er publisert, men artikkelen er ikke det. Koblingene under bygger på ontologifila og spesifikasjonen, ikke på artikkelen.
- **(korrigert) Premisset i #473** var at ontologifil, namespace og lisens ikke er publisert. Det stemmer ikke: kunngjøringen [IE-LOMO] lenker ikke til dem, men spesifikasjonen og repoet var offentlige da kunngjøringen kom (1.1.0 er fra 2026-10-21).

**Begrepene (v) [LOMO]:**

| Term | Hva det er |
|---|---|
| `LegalRule` | Tolkningen av en regel (ikke ordlyden). Skal ha minst én `establishedIn` til en ELI-ressurs |
| `PrescriptiveRule` (`SubstantiveRule`, `ImplementationAndOversightRule`) | Regel som påbyr, tillater eller forbyr. `hasDeonticModality` er påkrevd. `ImplementationAndOversightRule` gjelder myndigheters plikter til å gjennomføre, føre tilsyn og håndheve |
| `ConstitutiveRule` (`InstitutionalRule`, `ScopeRule`, `Definition`) | Regel som skaper institusjonelle fakta. `InstitutionalRule` oppretter organer, gir status, fastsetter sammensetning og «attribute powers or competences». Spesifikasjonen sier at «X may [institutional action]» er institusjonell kapasitet, ikke en tillatelse til atferd |
| `Action`, `ActionResult`, `ActionResultType` | Handlingen regelen foreskriver (`produces`), og resultatet (rapport, sertifikat) |
| `Agent`, `AgentType`, `AgentRole` | Et bestemt rettssubjekt, en kategori (f.eks. «Member States»), og deltakelsen i en handling (`forAgent`, `withRole`). Rollen kan bare være ADDRESSER, ADDRESSEE eller BENEF fra OP-tabellen «role», låst med `owl:oneOf` og SHACL |
| `TemporalSpecification` | Tid knyttet med `atTime`. Funksjonen (`hasFunction`) er Deadline, Validity, Starting Point eller Trigger. Verdier: `instant`, `duration`, `frequency` (xsd:duration mellom gjentakelser), `repetitions` og `temporalLabel`, pluss Allen-relasjonene `before`, `after`, `starts`, `finishes`, `overlaps`, `in` |
| `specifies`, `derogatesFrom`, `relatesTo` | Forholdet mellom regler: presisering uten konflikt, avvik med deontisk konflikt, og generell kobling |
| `hasRequirementType`, `hasCategory`, `involvesProcess` | Klassifisering etter OP-tabellene «administrative obligation» og «law implementation and oversight», og etter digitale dimensjoner |

Plikthaveren har ingen egen rolle. Den som utfører handlingen, er ADDRESSER: endringsloggen sier at rollen «bearer» ble erstattet med «addresser» (v) [LOMO-repo, CHANGELOG].

**Koblinger (etter juristrunden):**

| Vårt begrep | LOMO | Kobling | Merknad |
|---|---|---|---|
| P plikt overfor motpart, modalitet skal | `PrescriptiveRule` med Obligation, `produces` `Action` med `AgentRole` | snevrere | P er den aktørnære projeksjonen av plikter i forvaltningen (seks typer). LOMO dekker alle foreskrivende regler |
| P-modaliteten kan (adgang) | Permission | nært beslektet | Ikke kompetanse (#353 pkt. 1) |
| P-modaliteten bør | – | ingen match | Skjemaet har fire verdier, og SHACL krever en av dem |
| P med negativ polaritet (forbud) | Prohibition | nært beslektet | |
| Fra-noden på P (pliktsubjektet) | `AgentRole` med ADDRESSER | nært beslektet | Bare når regelen er Obligation, Prohibition eller Permission (P kan). Under Right kan ADDRESSER være rettighetshaveren |
| Til-noden på P (motparten), også betalingsmottakeren | `AgentRole` med ADDRESSEE | nært beslektet | BENEF svarer til «begunstiget», som docs/33 §4.5 pkt. 3 holder atskilt fra mottakeren. Mottakeren skal ikke eksporteres som BENEF |
| K kompetanse | `InstitutionalRule` | snevrere | Koblingen gjelder **regelen** som hjemler K-kanten. LOMO har ingen `Action` eller `AgentRole` på konstitutive regler, så fra og til har ingen motpart: LOMO kan ikke si hvem som har kompetanse overfor hvem |
| F oppgave med modalitet skal | `PrescriptiveRule` med Obligation, uten ADDRESSEE | nært beslektet | |
| F oppgave uten modalitet (attributiv, ressortfordeling) | `InstitutionalRule` | nært beslektet | En ressortfordeling «tildeler ansvar, den pålegger ingen plikt» (docs/33 §5.9). En F uten modalitet skal aldri eksporteres som Obligation |
| klasse | `AgentType` | nært beslektet | |
| Hjemmel-eId | `establishedIn` | nært beslektet | `establishedIn` peker på ELI og kan ikke uttrykke kilde utenfor korpus |
| Avgrensning | – | ingen match | `legalText` er ordrett tekst og svarer til sitatet eller tekst-taggen, ikke til avgrensningen |
| Unntak (node med `gjelder_regel` og betingelse) | `derogatesFrom` | bredere | `derogatesFrom` krever en egen regel med deontisk konflikt. Vårt unntak er også en «med mindre»-betingelse inne i én regel |
| `gyldig_fra`/`gyldig_til` på kanten (og #437) | `TemporalSpecification` med Validity | nært beslektet | |
| Frist | `TemporalSpecification` med Deadline | ingen match | **Mangler hos oss** |
| Frekvens og gjentakelse | `frequency`, `repetitions` | ingen match | **Mangler hos oss** |

**Funnet som betyr mest.** LOMO dekker nettopp det vi mangler i P: frist, startpunkt, utløsende hendelse, frekvens og antall gjentakelser, og forventet resultat. #437 gjelder bare gyldighet (Validity), ikke frist og frekvens. Omvendt har LOMO ikke det strukturlaget vårt bygger på: kompetanse som kant mellom aktører, oppgave atskilt fra plikt, polaritet som skiller ugyldig fra ulovlig, områder, kunnskapsstatus og avgrensning. LOMO skiller selv kompetanse fra tillatelse, og det støtter skillet i #353 pkt. 1.

### 7.2 SEMIC Core Vocabularies

**Status (v), kontrollert 2026-11-06 og 2026-11-12.** Alle har namespace `http://data.europa.eu/m8g/` for nye termer og gjenbruker W3C ORG, `locn:`, `dct:` og `eli:`. CPOV er ikke slått sammen med de andre. Hvert vokabular har eget repo og egen versjon.

| Vokabular | Versjon og dato | Status | Lisens |
|---|---|---|---|
| CPOV | 2.1.2, 2026-05-12 | SEMIC Recommendation | CC BY 4.0. Spesifikasjonssiden sier i tillegg «published under "CC0"» i lisensavsnittet. Den er altså selvmotsigende |
| CPSV-AP | 3.2.0, 2024-05-06 | SEMIC Recommendation | CC BY 4.0 |
| Core Location | 2.1.1, 2026-05-12 | SEMIC Candidate Recommendation | CC BY 4.0 |
| Core Person / Core Business | 2.1.2 / 2.3.0, 2026-05-12 | SEMIC Recommendation | CC BY 4.0 |
| CCCEV | 2.2.0, 2026-05-12 | SEMIC Candidate Recommendation | CC BY 4.0 |

Interoperable Europe-sidene viser fortsatt eldre versjoner (CPOV 2.1.1, CCCEV 2.1.0). Spesifikasjonene på semiceu.github.io er derfor kilden.

**CPOV (v) [CPOV].** Beskriver offentlige organisasjoner: `cv:PublicOrganisation` («defined as being part of the public sector by a legal framework at any level»), `org:OrganizationalUnit`, `org:subOrganizationOf`, `org:memberOf`, `dct:spatial` («the Administrative Region(s) that it covers»), `org:purpose` («remit» eller «responsibility», helst COFOG), `org:classification` og `org:ChangeEvent` med `cv:hasFormalFramework` (regelverket som utløste endringen) og `prov:startedAtTime`. Vokabularet har ingen begreper for kompetanse eller plikt, og folkevalgte organer er uttrykkelig utenfor.

| Vårt begrep | CPOV | Kobling | Merknad |
|---|---|---|---|
| rettssubjekt (offentlig) | `cv:PublicOrganisation` | snevrere | PublicOrganisation krever ikke rettssubjektivitet og omfatter også departementer og direktorater, som er organer for staten. At rettssubjektet bærer plikter og ansvar, går tapt ved eksport |
| organ | `cv:PublicOrganisation` med `org:subOrganizationOf` til rettssubjektet | nært beslektet | Mengdene overlapper. Folkevalgte organer (kommunestyret, Stortinget) har ingen motpart i CPOV og skal ikke eksporteres uten en merknad om det |
| organisatorisk_enhet | `org:OrganizationalUnit` (`org:unitOf`) | nært beslektet | |
| R etterfolger og organiseringskildene (docs/33 §5.9) | `org:ChangeEvent`, `org:resultedFrom`/`changedBy`, `cv:hasFormalFramework`; `xhv:prev`/`next` | nært beslektet | Den beste eksportformen for departementenes livsløp: hendelsen har både tid og hjemmel |
| A har_ansvarsomrade | `dct:spatial` | nært beslektet | CPOV har ingen hjemmel eller avgrensning på koblingen |
| F oppgave | `org:purpose` | nært beslektet | purpose er organisasjonens eksistensgrunn og svarer i beste fall til summen av en aktørs hovedoppgaver. Hjemmel og modalitet går tapt |
| M medlem_av (aktør → klasse) | `org:classification` | nært beslektet | Ikke `org:memberOf`, som gjelder medlemskap i en annen organisasjon |
| K og P | – | ingen match | |

**CPSV-AP (v) [CPSV-AP].** Beskriver offentlige tjenester. Det eneste som ligner myndighet, er `cv:hasCompetentAuthority` (påkrevd): organisasjonen som er ansvarlig for å levere tjenesten, også når tjenesten er satt bort. Hjemmel er `cv:hasLegalResource` → `eli:LegalResource`, og `cpsv:Rule` med `cpsv:implements`. Vilkår hentes fra CCCEV (`cv:Requirement`). Tid: `cv:processingTime` (estimert behandlingstid, xsd:duration) og `cv:frequency`. CPSV-AP har ingen egenskap for lovbestemte frister.

| Vårt begrep | CPSV-AP | Kobling | Merknad |
|---|---|---|---|
| F sorge_for | `cv:hasCompetentAuthority` | nært beslektet | Ansvar for at tjenesten ytes, også når den er satt bort |
| F forvaltning | `cv:hasCompetentAuthority` | ingen match | Et departement som forvalter en lov, er ikke ansvarlig myndighet for tjenesten |
| K vedtak | `cv:hasCompetentAuthority` | ingen match | Kan være premiss for å utlede ansvarlig myndighet, men er ikke det samme |
| Hjemmel-eId | `cv:hasLegalResource` | snevrere | Vår hjemmel er på bestemmelse, ledd eller punkt |
| (mangler) lovbestemt saksbehandlingsfrist | `cv:processingTime` | ingen match | Estimert tid er ikke en frist |
| vilkår (regellaget, docs/01 §6) | `cv:Requirement` / `cv:Criterion` (CCCEV) | ikke vurdert i juristrunden | Hører til regellaget |

**Core Location, Core Person og Core Business (v) [CLV; CPV; CBV].** Core Location har `cv:AdminUnit` («organized in a hierarchy», med kode og nivå), men ingen egenskap til overordnet enhet og ingen klasse for jurisdiksjon. Core Person har `dct:Jurisdiction` («the limits or territory within which authority may be exercised»), men bare som verdi for statsborgerskap. Core Business har `a4g:PowerOfAttorney` (fullmakt).

| Vårt begrep | Term | Kobling | Merknad |
|---|---|---|---|
| omrade og O bestar_av | `cv:AdminUnit` | bredere | Våre områder omfatter også lagsogn, lagdømmer, tettsteder og helseregioner, og O har sammensetning, som AdminUnit mangler |
| A har_jurisdiksjon (kanten) | `dct:Jurisdiction` | ingen match | dct:Jurisdiction er en klasse for et territorium, mens vår er en relasjon. Det styrker likevel anbefalingen i §5 om å vurdere å gi koden et annet navn |
| R representerer | `a4g:PowerOfAttorney` | ingen match | Privatrettslig fullmakt |

**CCCEV (v) [CCCEV].** `cv:Requirement`, `cv:Criterion`, `cv:InformationConcept`, `cv:Evidence` og `cv:EvidenceType`, med `cv:isDerivedFrom` → `cv:ReferenceFramework` (regelverket kravet bygger på) og `cv:validityPeriod`. Det er regellaget: vilkår og bevis, ikke aktører.

### 7.3 ELI-ontologien (utover URI-malen), ELI-DL og ELI i Norge

**Status (v), kontrollert 2026-11-12.** Gjeldende versjon er fortsatt **1.5** (http://data.europa.eu/eli/ontology). Det finnes ingen 1.6. OWL-fila som lenkes fra op.europa.eu/en/web/eu-vocabularies/eli, er **1.4** (januar 2022), og Interoperable Europe viser bare 1.3. Bruk namespace-adressen. Lisens og utgivelsesdato for 1.5 er ikke oppgitt i noen av kildene (ikke verifisert).

**Koblinger (v) [ELI]:**

| Vårt begrep | ELI | Kobling | Merknad |
|---|---|---|---|
| K normgivning (kompetansen) | `eli:based_on` / `basis_for` | ingen match | `based_on` er en relasjon mellom verk. Den viser at kompetansen er brukt, ikke hvem som har den |
| Dokumentrelasjonen forskrift → hjemmelsbestemmelse («gitt i medhold av») | `eli:based_on` | nært beslektet | **Ikke bygget hos oss** (§4 «Trinnhøyde og delegert normgivning»). Kan bli premiss for K normgivning |
| K normgivning | `eli:passed_by` | ingen match | Den som vedtok akten, ikke kompetansen |
| F forvaltning (departementet som forvalter en lov) | `eli:responsibility_of_agent` | snevrere | ELI: «some kind of responsibility for the legislation», typisk departementet som utarbeidet den, eller kontaktpunktet etter publisering |
| A har_ansvarsomrade | `eli:relevant_for` (overegenskap til `jurisdiction`) | ingen match | Gjelder rettsressursen, ikke aktøren |
| (mangler) anvendelsesdato på kildenivå | `eli:date_applicability` | ingen match | **Mangler hos oss**, sammen med `in_force` (se §2) |

ELI 1.5 har ingen begreper for plikt, frist eller hvem en regel gjelder for (søkt i hele fila).

**ELI-DL (v) [ELI-DL].** Utvidelse for lovforslag og lovgivningsprosesser, versjon V3.0 hos Publikasjonskontoret. Den har `Activity`, `Participation`, `ParticipationRole`, `had_responsible_organization`, `was_submitted_by`, `had_legal_basis` og `transposition_deadline`. Den beskriver hvem som deltar i prosessen som lager reglene, ikke kompetanse eller plikter som reglene gir: **ingen match** for K og P. Status er uklar: Interoperable Europe viser både «Under development» og «Archived» (ikke verifisert).

**ELI i Norge (v):**
- Lovdatas ELI er fortsatt merket «BETA» (kontrollert 2026-11-12, uendret siden 2026-10-09) [Lovdata-ELI].
- EUR-Lex-registeret oppgir «Pillar 3» for Norge, og at metadata er innebygd som RDFa [ELI-reg-NO]. RDFa på en ELI-side hos Lovdata (`lovdata.no/eli/lov/2005/05/20/28/no`) har bare `type_document`, `title`, `title_short`, `id_local`, `date_document`, `date_publication`, `first_date_entry_in_force`, `is_realized_by` og `publisher`. Den har ikke `based_on`, `passed_by` eller `responsibility_of`.
- Lovdatas åpne XML-data har derimot feltene `ministry` (ansvarlig departement), `basedOn` (hjemmel), `dateInForce` og `dateInForceEnd` [Lovdata-API]. Det er kilden for #437 (gyldighet fra kilden) og for en framtidig dokumentrelasjon forskrift → hjemmel. Lisensen for de gjeldende lovene og forskriftene er ikke oppgitt på api.lovdata.no (ikke verifisert). Felles datakatalog oppgir NLOD 2.0 for Norsk Lovtidend avd. I.

### 7.4 Regellaget og forskningen

**FLINT og eFLINT (v) [eFLINT; FLINT-ont].** Rammebasert språk for eksplisitte tolkninger av rettskilder. eFLINT er den kjørbare varianten (Haskell-implementasjon 4.1.0.1, BSD-3-Clause). FLINT-ontologien 1.0.0 (TNO, 2025-12-03, Apache 2.0) har `Act`, `Fact`, `Duty`, `Agent`, `hasActor`, `hasRecipient`, `creates`, `terminates` og `hasSource`/`hasTextFragment` (lenke til teksten).

| Vårt begrep | eFLINT | Kobling | Merknad |
|---|---|---|---|
| K kompetanse | Act-type (actor, recipient, holds when, creates, terminates) | snevrere | Aktør og mottaker står i et power-liability-forhold. Alle handlinger er act frames, også rene oppfyllelseshandlinger |
| P plikt overfor motpart | Duty-type (holder, claimant, violated when) | nært beslektet | En Duty har alltid en claimant. Eksporter bare som Duty når kravsrett = etablert (docs/34), aldri P uten motpart |
| K med negativ polaritet | Precondition som ikke er oppfylt, eller Invariant | ingen match | |

En frist uttrykkes i eFLINT som et «violated when»-vilkår på et faktum som en ekstern hendelse oppretter (GPCE 2020 §4). Språket har ingen egen tidstype og ingen frekvens.

**ODRL 2.2 (v) [ODRL-M; ODRL-V].** W3C-anbefaling fra 2018 for bruksvilkår: `Permission`, `Prohibition`, `Duty`, `assigner`, `assignee`, partsfunksjoner (`informedParty`, `compensatedParty` m.fl.) og begrensninger på tid og gjentakelse (`dateTime`, `delayPeriod`, `elapsedTime`, `timeInterval`, `count`). En `duty` fra en Permission er «a pre-condition» for tillatelsen [ODRL-M §2.6.1, §2.6.5]. Bare `obligation` på policynivå er en selvstendig plikt.

| Vårt begrep | ODRL | Kobling | Merknad |
|---|---|---|---|
| P informasjon og betaling | `odrl:obligation` (Duty) med assignee og `informedParty`/`compensatedParty` | nært beslektet | Ikke `duty` på en Permission. Tidsbegrensningene er det beste vokabularet for frist og frekvens vi har funnet, men vi har ikke frist og frekvens selv |
| K kompetanse | `assigner` / Permission | ingen match | |

En W3C-workshop i juli 2026 anbefalte en ODRL 3.0-arbeidsgruppe og nevnte LegalRuleML, LOMO og Akoma Ntoso som kandidater for integrasjon [ODRL-WS]. Om charteret er vedtatt, er ikke verifisert.

**LegalRuleML.** Ingen ny versjon etter OASIS Standard 1.0 (2021), og ingen offisiell OWL-ontologi (v) (https://www.oasis-open.org/committees/legalruleml/). Det nærmeste er [LRML-RO], en spesialisering for rapporteringsplikter (Bologna, 2024). Den kobler regler til Akoma Ntoso og ELI og bruker Governatoris plikttyper (vedlikeholds- og oppnåelsesplikt m.fl.), men frister og frekvens står der som vanlige vilkår i regelen, ikke som egne elementer.

**Forskning (v, landingsside eller PDF åpnet):**
- Griffo mfl., Data & Knowledge Engineering 2023 (UFO-L, allerede [UFO-L 2023]): power–subjection og disability–immunity.
- Dong og Roy, «Dynamic Logic of Legal Competences», Journal of Logic, Language and Information 2021, https://doi.org/10.1007/s10849-021-09340-z: skiller det å *kunne* rettslig (K) fra det å ha *lov* til.
- Kharraz, Leucker og Schneider, «Timed Dyadic Deontic Logic», JURIX 2021, https://doi.org/10.3233/FAIA210336, og Chircop, Pace og Schneider, JURIX 2022, https://doi.org/10.3233/FAIA220460: normer med frister i kontinuerlig tid.
- Ingen JURIX- eller ICAIL-artikkel fra 2022–2025 om ontologier for kompetanse i offentlig forvaltning ble funnet.
- JURIX 2026 [JURIX26] har, ved siden av LOMO, artikler om å vurdere språk for Rules as Code (Ali, Sileno, van Engers), «Conditional Authority in Legal CBR» (Di Florio, Dong, Rotolo) og KI-uttrekk av EUs rapporteringskrav (Lecat, Corazza, Palmirani). De er ikke publisert ennå.

**Catala og OECD «Cracking the Code» (v).** Catala (1.3.0, Apache 2.0) beregner beløp etter lovregler og har ingen aktører, kompetanse eller plikter. OECD-rapporten (2020) er en innføring i Rules as Code uten datamodell. Ingen av dem er relevante for strukturlaget.

### 7.5 Norge og Norden

**Digdir og Felles datakatalog (v), kontrollert 2026-11-12:**
- **CPSV-AP-NO 1.2.0** (gjeldende, 2026-05-05, CC BY 4.0) [CPSV-AP-NO] bygger på CPSV-AP 3.2 og CCCEV 2.1. `cv:hasCompetentAuthority` («har kompetent organ») er påkrevd. `cv:hasLegalResource` peker på Lovdatas ELI. Roller hentes fra vokabularet `data.norge.no/vocabulary/role-type` (tjenesteyter, tjenesteprodusent, tjenestemottaker m.fl.). Status i referansekatalogen er ikke funnet (ikke verifisert).
- **Ingen norsk profil for organisasjoner** (ingen CPOV-AP-NO). Felles datakatalogs organisasjonskatalog bruker `rov:RegisteredOrganization`, `org:subOrganizationOf`, `rov:orgType` og `br:orgPath` (f.eks. `/STAT/932384469/991825827`).
- **SKOS-AP-NO-Begrep 2.0.15** (gjeldende, CC BY 4.0, anbefalt i referansekatalogen) [SKOS-AP-NO]: en definisjon har `dct:source` (kan være en lovbestemmelse, eksempel med Lovdata-ELI) og `skosno:relationshipWithSource` med verdiene sitat fra kilde, basert på kilde og egendefinert.
- **DCAT-AP-NO 3.0.7** [DCAT-AP-NO]: `dcatap:applicableLegislation` → `eli:LegalResource`, «For referanse til Lovdata bør ELI-URI brukes».
- **Veilederen for digitaliseringsvennlig regelverk** (Digdir) gir anbefalinger, men har ingen datamodell for aktører eller kompetanse.

| Vårt begrep | Norsk spesifikasjon | Kobling | Merknad |
|---|---|---|---|
| kunnskapsstatus for en definisjon (tekstfunn, normalisering, fortolkning) | `skosno:relationshipWithSource` | nært beslektet | Tekstfunn ↔ sitat fra kilde. Normalisering og fortolkning ↔ basert på kilde. Egendefinert har ingen motpart, fordi vi krever hjemmel eller kilde utenfor korpus. Avledet, uavklart, omstridt og foreldet har ingen motpart hos dem |

Koblingene til CPSV-AP i §7.2 gjelder også CPSV-AP-NO.

**Registre (v).** Enhetsregisteret har organisasjonsformene (ORGL, STAT, KOMM, FYLK m.fl.) og `overordnetEnhet` under NLOD. SSB KLASS har kommune- og fylkesinndelingen med `validFrom`/`validTo` under CC BY 4.0. Begge brukes allerede: Enhetsregisteret i virksomhetskatalogen (docs/20) og KLASS i områderegisteret (`src/RegelIde.Data/Seed/fornye-omraderegister.py`). De gir gyldighetsperiode for områder og organisasjoner, men ingen hjemmel.

**Norden:**
- **Danmark (v):** «Digitaliseringsklar lovgivning» (digst.dk) er en obligatorisk vurdering med sju prinsipper, uten datamodell. «OIO Specifikation af model for Organisation» 2.0 har Organisation, OrgEnhed, ForretningsFunktion og Opgave (KLE). «Anvendelsesprofil for organisationer» bygger på W3C ORG og CPOV, men er et utkast fra 2018. Gjeldende status er ikke verifisert.
- **Sverige (v):** SCBs myndighetsregister (449 statlige myndigheter). Domstolsverkets rapport fra 2025 sier at åpne data, ELI og ECLI må utredes videre.
- **Finland (v):** Finlex har åpne data med Akoma Ntoso og ELI-alias under CC BY 4.0. Suomi.fi-tjenestekatalogen har `roleType` (Responsible, Producer), områder med kommunekode og en lenke til lovgivning per tjeneste. At den bygger på CPSV-AP, er ikke verifisert.

Ingen av de norske eller nordiske initiativene modellerer kompetanse som en kant knyttet til en bestemt bestemmelse.

### 7.6 Hva de har som vi mangler, og omvendt

| De har, vi mangler | Hvor | Hva det berører hos oss |
|---|---|---|
| **Frist** for å oppfylle en plikt, startpunkt og utløsende hendelse | LOMO `TemporalSpecification` (Deadline, Starting Point, Trigger); ODRL-begrensninger; eFLINT «violated when» | P og F. Ikke dekket av #437, som gjelder gyldighet |
| **Frekvens** og antall gjentakelser | LOMO `frequency`, `repetitions`; ODRL `timeInterval`, `count` | P informasjon (rapporter), F (oversikt) |
| Gyldighet for regelen og anvendelsesdato | LOMO Validity; ELI `in_force`, `date_applicability`; Lovdata `dateInForce`/`dateInForceEnd` | #437 |
| Forventet resultat av en handling (rapport, sertifikat) | LOMO `ActionResult`; CPSV-AP `cv:Output` | P (objekt er fritekst i dag) |
| Rekkefølge mellom handlinger | LOMO `hasNext`/`hasPrevious` | Saksgang, som bevisst er utenfor strukturlaget (docs/33 §4.4) |
| Presisering uten konflikt (særregel) | LOMO `specifies` | Kollisjonsprinsipper (§4 «Mangler») |
| Endringshendelse for organisasjoner, med hjemmel | CPOV `org:ChangeEvent` med `cv:hasFormalFramework` | R etterfolger har dette i dag som kanter, ikke som hendelse |
| Dokumentrelasjonen forskrift → hjemmel | ELI `based_on`; Lovdata `basedOn` | Ikke bygget (§4) |
| Ansvarlig myndighet for en tjeneste | CPSV-AP / CPSV-AP-NO | Tjenester er utenfor modellen |

| Vi har, de mangler | Kommentar |
|---|---|
| Kompetanse som kant mellom aktører, med motpart, familie og delegeringsadgang | LOMO har kompetanse bare som regeltype uten aktør. CPOV og CPSV-AP har ingen kompetanse. eFLINT har act frames, men skiller ikke kompetanse fra andre handlinger |
| Oppgave (F) atskilt fra plikt (P), og plikt atskilt fra kravsrett | LOMO legger begge under Obligation eller InstitutionalRule. eFLINT Duty har alltid en kravshaver |
| Negativ polaritet som skiller ugyldig (disability) fra ulovlig (forbud) | Bare UFO-L (§3) |
| «bør» som egen modalitet | Ingen |
| Områder med type, kode, sammensetning (O) og ansvarsområde (A) med hjemmel | CPOV har `dct:spatial` uten hjemmel. Core Location har ingen sammensetning |
| Klasser lest distributivt, og medlemskap med hjemmel | LOMO `AgentType` uten medlemskap |
| Kunnskapsstatus (åtte verdier) og skillet mellom tekstfunn og slutning | SKOS-AP-NO har tre verdier, og bare for definisjoner |
| Avgrensning per paragraf, og hjemmel på eId for hvert utsagn | Ingen |
| `ordning` som ikke-aktør | Ingen |
| Skjønn | Ingen |

### 7.7 Juristrunden (CLAUDE.md §23)

Kortene er koblingene i §7.1–§7.5 slik de var før runden. Alle dokumentene ligger i [juristrunde-473-koblinger.json](juristrunde-473-koblinger.json).

- **Runde 1:** 38 kort. 22 holder og 16 innvendinger (1 høy, 9 middels, 6 lav). Ingen kontroller og ingen prinsipielle.
- **Svar:** modellereren aksepterte 14 og svarte delvis på 2 (C2 og E3).
- **Runde 2:** juristen godtok begge og presiserte én akseptert retting (L5 skal også gjelde Permission). **Ingen uenighet går til Johann.**

Det runden endret:
- **Oppgave forvekslet med plikt (høy):** F uten modalitet er ikke en Obligation. F deles etter modalitet ved eksport (§7.1).
- **Kompetanse forvekslet med resultat:** `eli:based_on` viser at kompetansen er brukt, ikke hvem som har den (§7.3).
- **Plikt og kravsrett:** eFLINT Duty har alltid en kravshaver, så P eksporteres bare som Duty når kravsrett er etablert (§7.4).
- **LOMO-rollene:** ADDRESSER er den som utfører handlingen, og betalingsmottakeren er ADDRESSEE, ikke BENEF (§7.1).
- **Nivå og delingslinjer:** rettssubjekt er snevrere enn PublicOrganisation, organ er nært beslektet, og `org:purpose` er bare nært beslektet med F (§7.2).
- **ODRL:** bare `obligation` er en selvstendig plikt (verifisert mot [ODRL-M] etter runde 1).
- Juristen tok feil i runde 1 om at «typically the ministry» ikke står i ELI. Den står der. Koblingstypen ble likevel «snevrere» (E3).

Det runden ikke kontrollerte: den norske definisjonsteksten i CPSV-AP-NO (data.norge.no svarte 406 til juristens verktøy) og eFLINT-semantikken i F3.

### 7.8 Hva som bør påvirke modellen

Dette er forslag. Ingen av dem er besluttet, og ingenting er bygget i #473.

1. **Frist og frekvens på P og F.** LOMO viser en etablert form: en tidsspesifikasjon med funksjon (frist, gyldighet, startpunkt, utløsende hendelse) og verdi (tidspunkt, varighet, frekvens, antall gjentakelser, navngitt hendelse). #437 dekker bare gyldighet. Frist og frekvens er en ny evne og bør være en egen sak eller en utvidelse av #437. Det må avgjøres om det hører til strukturlaget (P-kanten) eller regellaget (rettsfølgen).
2. **Aktører: ingen modellendring, men en eksportmapping til CPOV.** Rettssubjekt, organ og organisatorisk enhet har gode motparter i CPOV og W3C ORG. Departementenes livsløp (docs/33 §5.9) passer `org:ChangeEvent` med hjemmel. Folkevalgte organer må merkes ved eksport.
3. **Plikter: eksportmapping til LOMO.** P kan eksporteres som `PrescriptiveRule` med `Action` og `AgentRole`. «bør» har ingen plass, og K og F mister aktørene. Det er et argument for å beholde vår kantmodell og bruke LOMO som eksportformat for regellaget.
4. **`har_jurisdiksjon`.** Navnet brukes i tre betydninger (§5). Core Person bruker «jurisdiction» om territoriet, ikke om relasjonen. Det styrker anbefalingen om et annet navn.
5. **Kunnskapsstatus for begreper.** Kan eksporteres til SKOS-AP-NO-Begrep med verditabellen i §7.5, når begrepene publiseres i Begrepskatalogen.
6. **Dokumentrelasjonen forskrift → hjemmel.** Lovdatas XML har `basedOn` og `ministry`. Det er en kilde for K normgivning og F forvaltning som ikke er tatt i bruk (ikke målt i #473).

---

## Verifiseringslogg (2026-10-09)

**Metode.** Spesifikasjonene er hentet direkte og søkt i: LegalRuleML-HTML og kompakt XSD, AKN Vocabulary-HTML, `akomantoso30.xsd`, AKN-NC-HTML, LKIF-modulene (`.ttl` fra GitHub), ELI-ontologien v1.5 (RDF/XML), UFO-L-artiklene 2018 og 2023 (forfatter-PDF) og Lovdata. Notatet verifisert-primarkilder.md i samme mappe er brukt som innspill. Der det avvek fra kildene, gjelder kilden (se nederst).

**De 17 begrepene som var merket «ikke verifisert»:**

| Begrep | Resultat | Kilde |
|---|---|---|
| LKIF `Competence` | finnes ikke i spesifikasjonen. Nærmest er `Potestative_Expression`/`Hohfeldian_Power` | [LKIF norm.owl] |
| UFO-L «Duty to Act» / «Duty to Omit» | verifisert | [UFO-L 2018 §3] |
| LegalRuleML `Obligation` med `Bearer`/`AuxiliaryParty` | verifisert | [LRML §3.4, §4.2.3.1] |
| LKIF `Prohibition`, `Disallowed` | verifisert | [LKIF norm.owl] |
| LegalRuleML: ingen operator for «bør» | verifisert. Kan likevel uttrykkes med `@iri`/`appliesModality` | [LRML §4.2.3.1] |
| LKIF: ingen ordningsklasse | verifisert fravær | [LKIF] |
| UFO-L «Legal role / anti-rigid» | `Legal Role` er verifisert. «Anti-rigid» står ikke i UFO-L-artiklene og er fortsatt ikke verifisert | [UFO-L 2018; UFO-L 2023] |
| LKIF `Jurisdiction` | finnes ikke i spesifikasjonen | [LKIF] |
| LegalRuleML `LegalSource` «uten ID-krav» | korrigert: `LegalSource` krever IRI, `LegalReference` dekker kilder uten IRI | [LRML §4.3.1] |
| LKIF `Exception` | verifisert, to steder (`expression.owl`, `time-modification.owl`) | [LKIF] |
| LKIF skiller ikke delegeringsadgang fra delegering | verifisert: `Delegation` er bare en handling | [LKIF legal-action.owl] |
| LKIF `time.owl`, `time-modification.owl` | verifisert | [LKIF] |
| UFO-L `Legal Agent` | verifisert | [UFO-L 2023] |
| AKN `blockList`/`listIntroduction`/`item`/`listWrapUp` | verifisert | [AKN-XSD; AKN-1 §5.12] |
| AKN `list_N` foran `point_a` | verifisert i Vocabulary-eksemplene, men AKN-NC har et eksempel uten | [AKN-1 §5.12; AKN-NC §4.6.1] |
| AKN-forkortelse for `wrapUp` | verifisert som `wrapup`. For `listWrapUp` er spesifikasjonen selvmotsigende (`wrap` eller `wrapup`) | [AKN-NC §5.4.2] |
| `ordning` i LKIF/UFO-L (§5) | finnes ikke i spesifikasjonen | [LKIF; UFO-L] |

Oppsummert: **11 verifisert, 1 korrigert, 4 finnes ikke, 1 delvis** («anti-rigid» står igjen som ikke verifisert).

**Stikkprøver av (v), 35 begreper.** 26 holdt. Disse 9 ble korrigert eller presisert:
- LKIF `Potestative_Right` er en makt «meant to benefit the holder» og er en dårlig mapping for K. Bruk `Potestative_Expression`/`Hohfeldian_Power`.
- LKIF-kildetypen `Custom` er et `Medium`, ikke en `Legal_Source`. Riktig er `Customary_Law`.
- LegalRuleML `TimeInstant` finnes ikke. Riktig er `ruleml:Time` i `lrml:Times`.
- LegalRuleML `Role` knytter en aktør til et LegalRuleML-uttrykk, ikke til en `Figure`.
- LegalRuleML `Strength` er overbegrep. Elementene er `StrictStrength`, `DefeasibleStrength` og `Defeater`.
- En AKN-referanse til en del av et annet dokument bruker `~eId`.
- Lovdatas ELI har ingen jurisdiksjon i URI-en. Den har derimot `point in time` og språkkodene `no`/`nb`/`nn`.
- UFO-L-paret heter NoRight/Permission. Liberty er en sammensatt relator.
- `eli:jurisdiction` betyr opphavsjurisdiksjon.

**Avvik fra verifisert-primarkilder.md:**
- (a) `Violation` og `Compliance` *er* elementer i LegalRuleML-skjemaet (`lrml-compact.xsd`), ikke bare ordlistebegreper.
- (b) I LKIF er `Obligation` ⊑ `Permission` og `owl:equivalentClass` `Prohibition`, og `Prohibition` ⊑ `Permission`. Det er altså ikke Obligation ⊑ Prohibition.
- (c) LKIF `Custom` er ikke en `Legal_Source` (se over).

**Lovfakta.** Ny forvaltningslov, LOV-2025-06-20-81, er **ikke i kraft** per 2026-10-09. Lovdata oppgir ikrafttredelse «Kongen bestemmer», og etter § 99 kan bestemmelsene settes i kraft til ulik tid [Lovdata-fvl]. En presentasjon fra Statsforvalteren (mai 2026) sier «trer i kraft i 2027», men det er ikke kontrollert mot noe ikraftsettingsvedtak og er ikke lagt inn i notatet.

**Endringer i de fem hovedfunnene:**
1. *K vs. Authority:* står seg og er styrket. Authority er metadata om opphav [LRML §4.3.4]. LegalRuleML har ingen power-element, og LKIF har ingen `Competence`. Bare UFO-L har kompetanse eksplisitt («power is created by a competence norm»). LKIF-mappingen for K er rettet fra `Potestative_Right` til `Potestative_Expression`/`Hohfeldian_Power`.
2. *Polaritet:* **korrigert.** Skillet er to-delt, ikke tre-delt. Disability og immunity er to sider av samme relator, mens prohibition er en atferdsposisjon. Rettsvirkningen skiller dem: ugyldig mot ulovlig [UFO-L 2023 §4.2]. LKIF slår dessuten disability og immunity sammen.
3. *AKN eId og uttrykk:* står seg og er skjerpet. `/nor` uten `@` betyr «gjeldende versjon» etter AKN-NC, og er heller ikke gyldig Lovdata-ELI. AKN har selv dobbel nøkkel (`eId`/`wId`), og vår stabile eId svarer til `wId`. `wrapup` er bekreftet. Et ledd med liste må ha `intro`, ikke `content`.
4. *Norske utvidelser:* står seg (ordning, skjønnsmomenter, tekstfunn vs. slutning, avgrensning per paragraf). «Bør» er fortsatt en utvidelse, men kan eksporteres via LegalRuleML `@iri`/`appliesModality`. «Kilde utenfor korpus» er justert fra «mangler hos dem» til «delvis», fordi LegalRuleML har `LegalReference`.
5. *Hull i rettslig metode:* står seg. Ny fvl. lovfester delegering (§§ 22–24) og flytter vedtaksdefinisjonene til § 7. Når loven trer i kraft, må `fvl_kategori` og #335 (delegeringsadgang) få tidsangivelse.
