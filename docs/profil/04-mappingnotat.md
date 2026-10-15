# Grunnlagsnotat: regel-ide mot standardene (LKIF Core, UFO-L, LegalRuleML, ELI, Akoma Ntoso)

Skrevet 2026-10-09 mot FinnurO/regel-ide master `f066c98`. Repoet er ikke endret. Notatet skal gjøre det mulig å vurdere materialet Johann sender om standarder, punkt for punkt, mot det vi faktisk har.

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
