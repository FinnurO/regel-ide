# HTML4EU vurdert mot profilforslaget og rettskildemodellen

[Ny, 2026-11-24] Prosjekteieren ba 24. november 2026 om en vurdering av HTML4EU, med eksemplene og dokumentasjonen. Notatet sier hva HTML4EU er, hva eksemplene viser, hvordan det passer med rettskildenodene, eId og ELI hos oss, og hva det betyr for sak #495 (varig elementidentitet). Det gir en anbefaling. Valget er prosjekteierens. Mappingnotatet (04) er ikke endret.

Status: arbeidsutkast, venter på prosjekteierens valg. Normativt: nei (informativt, som 04).

**Kortversjon.** HTML4EU er Publikasjonskontorets HTML-profil for EU-rettsakter på EUR-Lex. Versjon 2.0.0.0 ble publisert 21. september 2026. Formatet bygger HTML fra AKN4EU, gir hver del av akten en ID etter ELI-underinndelingene (`art_9/par_1/sub_2/pnt_a`) og knytter delene sammen med ELI-ontologien i RDFa (`rel="eli:has_part"`). De 12 eksemplene er engelske originalutgaver (OJ) av forordninger, direktiver og beslutninger. Ingen av dem er konsolidert, og ingen er endringsakter. Spesifikasjonen dekker verken endringer eller konsolidering. EUR-Lex brukte ikke formatet i to stikkprøver 24. november 2026. Anbefalingen er **(b) følge med**, med en rad i sjekklisten (08). HTML4EU kan ikke gi oss en varig elementidentitet (#495): ID-ene er adresser i én utgave, bygget av trykt nummer og løpenummer, som vår eId. Formatet er likevel nyttig som mønster for presise ELI-adresser og for en eventuell HTML-eksport med RDFa.

---

## 1. Kildene og merkingen

Merkingen er den samme som i 04: **(v)** er slått opp i primærkilden, **(ikke verifisert)** betyr at kilden ikke kunne nås eller ikke sier noe, og **[slutning]** er vår egen slutning. Alt er hentet 2026-11-24. Filene ble lastet ned til en egen mappe og lest som data.

Kortnøkler:
- **[DS]** Datasettsiden: https://op.europa.eu/en/web/eu-vocabularies/dataset/-/resource?uri=http://publications.europa.eu/resource/dataset/html4eu, med fanene Downloads og Documentation for versjon 2.0.0.0 og 1.0.0.0. Fanene Alignments, Release notes og Diff files var tomme.
- **[H2]** Spesifikasjonen HTML4EU 2.0.0.0, HTML og PDF (95 sider), Cellar-adresse `…/distribution/documentation/html4eu/20260921-0/…`. Lest i HTML-utgaven.
- **[H1]** «HTML Format specification» versjon 1.0, 31. mars 2025 (PDF, 40 sider), Cellar-adresse `…/html4eu/20250331-0/…`.
- **[EKS]** `html4eu-samples-2.0.0.0.zip`: 12 HTML-filer og `HTML4EU.css`. Stilarket ligger også som egen distribusjon.
- **[BG]** Bakgrunnssiden: https://op.europa.eu/en/web/eu-vocabularies/html4eu, med diagrammet over forholdet til andre spesifikasjoner.
- **[LH]** LegalHTML-siden: https://op.europa.eu/en/web/eu-vocabularies/legalhtml
- **[AKN4EU]** AKN4EU-siden: https://op.europa.eu/en/web/eu-vocabularies/akn4eu
- **[EUR-Lex]** To akter åpnet i nettleser: CELEX 32024L2831 (originalutgaven, et av eksemplene) og 02016R0679-20160504 (konsolidert GDPR).
- **[Lovdata]** https://lovdata.no/dokument/NL/lov/2025-06-20-81 (forvaltningsloven 2025), lest som HTML.
- **[Korpus]** Vår database: nodene i personopplysningsloven, 2026-11-24.

---

## 2. Hva HTML4EU er

### 2.1 Det som er dokumentert

| Tema | Hva kilden sier | |
|---|---|---|
| Eier og utgiver | Publikasjonskontoret er både forfatter og utgiver. Kategorien er markeringsskjema, og brukssammenhengen er EUR-Lex. Kontakt går til AKN4EU-postkassen | (v) [DS; BG] |
| Formål | En standardisert HTML-profil for EU-rettsakter og andre dokumenter på nettet, med felles elementer, attributter og konvensjoner. Målet er ensartethet, tilgjengelighet, maskinlesbarhet og gjenbruk. Dokumentene publiseres først og fremst på EUR-Lex | (v) [H2 §1, §1.1] |
| Avgrensning | Spesifikasjonen sier hvordan strukturen skrives i HTML, ikke hva den betyr rettslig, og ikke hvordan teksten skal skrives. Den er uavhengig av kildeformat | (v) [H2 §1.1] |
| Oppbygging | En kjerne (dokumentmal, overskrifter, avsnitt, lister, tabeller, figurer, formler, fotnoter) og profiler. Profilene er «Legal act» og «Caselaw». Caselaw er en tom plassholder | (v) [H2 §1.2, §3.1, §3.3] |
| Tekniske krav | HTML Living Standard, serialisert som HTML, men velformet som XML. WCAG 2.2 nivå AA. Responsiv visning. RDFa 1.1. Bygger på Publikasjonskontorets «HTML5 Publications for OP» | (v) [H2 §4.2] |
| Versjoner | 1.0 (31. mars 2025, «WG Deliverable», første versjon) og 2.0.0.0 (publisert 21. september 2026). Firedelt versjonsnummer W.X.Y.z med regler for bakoverkompatibilitet. Endringsønsker får ID på formen ÅÅÅÅMMDD-NN | (v) [DS; H1; H2 §4.4, §4.5] |
| Lisens | Ikke oppgitt på datasettsiden, i spesifikasjonen eller i eksemplene | (ikke verifisert) |
| Status | Datasettsiden sier at spesifikasjonen er grunnlaget for et valideringsrammeverk som utvikles. Kartleggingsvedlegget (AKN4EU–RDFa–HTML) er merket som utkast. Datasettsiden merker både 1.0.0.0 og 2.0.0.0 som nyeste versjon | (v) [DS; H2 §5.1] |

### 2.2 Hvordan strukturen markeres

- **Dokumentet** står i `<main>` som `<article class="act">`. Tittelen, fortalen, de normative bestemmelsene, klausulen om direkte anvendelse, sluttdelen med signaturer og vedleggene er egne `<section>`-er med klasse og ID (`tit_1`, `pbl_1`, `enc_1`, `dia_1`, `fnp_1`, `anx_I`) (v) [H2 §3.2.2].
- **Fortalen:** henvisningene er `<ul class="citations">` med `<li id="cit_N">`, betraktningene `<ol class="recitals">` med `<li id="rct_N">` (v) [H2 §3.2.4–3.2.6].
- **Høyere inndeling:** Part, Title, Chapter, Section og Subsection, i den rekkefølgen. Mellomnivåer kan mangle. Overskriften er en `<hgroup>` med egen ID (`cpt_I/tit_1`) (v) [H2 §3.2.7, §3.2.8].
- **Artikkel:** `<section class="article" id="art_N">` (v) [H2 §3.2.9].
- **Lavere inndeling** (v) [H2 §3.2.10]:
  - nummerert ledd: `<ol>` med `<li class="paragraph" id="art_2/par_1">`, nummeret i `<span>`;
  - unummerert ledd: `<p class="unnumbered-paragraph" id="art_11/unp_1">`;
  - underledd i et nummerert ledd: `<ul>` med `<li class="subparagraph" id="…/sub_1">`;
  - punkt: `<li class="point" id="…/pnt_a">`; tankestrek: `<li class="indent" id="…/idt_1">`;
  - innledning foran en liste: `<p class="introductory-part" id="…/sub_1/inp">`; avslutning etter lista: `<p class="closing-part" id="…/sub_1/wrp">`.
  Et nummerert ledd med punkter skrives alltid som ledd → underledd → innledning + punkter.
- **Endringer:** 2.0 har ingen markering av endringsbestemmelser. Malen har en valgfri `html4eu-modification-notice`, men den er ikke beskrevet (v) [H2 §2.1]. Versjon 1.0 hadde et utkast for endringsbestemmelser (`blockquote` med tekst som settes inn), merket som uferdig (v) [H1 §4.4.3].
- **Konsolidering:** ikke beskrevet i 2.0 (v). LegalHTML, som HTML4EU har erstattet, var laget for konsoliderte akter (v) [LH].
- **Kryssreferanser:** ikke beskrevet i 2.0. Bare fotnoter har lenker (`ftnref_N` ↔ `ftn_N`) (v) [H2 §2.4].

### 2.3 Identifikatorene

- **`@id`** er koden fra ELI-underinndelingene og autoritetstabellen Subdivision, skrevet som en sti relativt til akten: `art_9/par_1/sub_2/pnt_a` (v) [H2 §1.3.2, §3.2; EKS].
- **Trykt nummer eller løpenummer.** `art`, `par`, `cpt` og `pnt` bruker det trykte nummeret eller bokstaven (`cpt_II`, `pnt_a`). `sub`, `unp`, `idt`, `rct` og `cit` bruker løpenummer (v) [EKS].
- **`@resource`** er ELI-URI-en til delen, med prefikset `doc:` for aktens ELI-basis: `resource="doc:art_9/par_1/oj"` blir `http://data.europa.eu/eli/dir/2024/2831/art_9/par_1/oj`. Alle eksemplene slutter på `/oj`, altså originalutgaven i EU-tidende (v) [H2 §4.3; EKS].
- **Hvordan stien bygges**, står ikke i HTML4EU. Den viser til spesifikasjonen for ELI-underinndelinger, der versjon 3 er «pending publication» (v) [H2 §1.3.2, fotnote]. I eksemplene er artikkelen ikke prefikset med kapitlet (`cpt_I` inneholder `art_1`), mens høyere nivåer er nestet (`prt_1/tis_I/cpt_1`) (v).
- **Varighet på tvers av versjoner:** spesifikasjonen sier ingenting om hva som skjer med ID-ene når akten endres eller konsolideres (v, søkt i hele [H2]).

### 2.4 Forholdet til andre standarder

| Standard | Forholdet | |
|---|---|---|
| **AKN4EU** | Kildeformatet. Bakgrunnssiden kaller AKN4EU «source format», og eksemplene er laget med verktøyet `akn4eu2html4eu-1.0.0.0`. Kartleggingsvedlegget kobler AKN4EU-elementer til HTML og RDFa (utkast) | (v) [BG; EKS; H2 §5.1] |
| **Akoma Ntoso** (OASIS) | Bare gjennom AKN4EU. HTML4EU bruker ELI-koder (`art_1/par_2`), ikke AKN-navnekonvensjonens `eId`/`wId` (`art_1__para_2`) | (v) [H2; AKN4EU] |
| **ELI** | ID-ene er ELI-underinndelingskoder. `@resource` er ELI-URI-er | (v) [H2 §3.2, §4.3] |
| **ELI-ontologien** | Prefikset `eli:`. Spesifikasjonen nevner `has_part`/`is_part_of`, `has_annex`, `number` og `title`. Eksemplene bruker bare `rel="eli:has_part"` og `rel="eli:has_annex"`, ingen `@property` eller `@typeof` | (v) [H2 §4.3; EKS] |
| **ELI i HTML/RDFa** | HTML4EU er en måte å legge ELI-semantikk inn i selve teksten, del for del. Metadata om akten som helhet (vedtatt av, basert på) legges ikke i RDFa; v1.0 begrunnet det med at EUR-Lex har dem i et eget ELI-lag | (v) [H1 §2.1.4.5; H2 §4.3] |
| **CDM** (Publikasjonskontorets datamodell) | v1.0 brukte `@typeof` og CDM-egenskaper (`cdm:work_contains_work`). I 2.0 står det bare igjen i kartleggingsvedlegget, som er merket utkast. Selve spesifikasjonen (§4.3) og eksemplene bruker ELI | (v) [H1; H2 §4.3, §5.1] |
| **Formex** | Ikke nevnt i HTML4EU. EUR-Lex sin HTML i dag er avledet av Formex: siden for 32024L2831 heter `L_202402831EN.000101.fmx.xml`, har ID-er som `art_9`, `art_9.tit_1` og `009.001`, og har ingen RDFa. HTML4EU erstatter altså den Formex-avledede HTML-en, ikke Formex som format [slutning] | (v for EUR-Lex-siden) |
| **Cellar** | Distribusjonene ligger i Cellar. Spesifikasjonen sier ikke hvor HTML4EU-dokumentene skal lagres | (v) [DS] |
| **LegalHTML** | Grunnlaget, ifølge diagrammet på bakgrunnssiden. LegalHTML er ikke lenger vedlikeholdt og er erstattet av HTML4EU | (v) [BG; LH] |

### 2.5 Eksemplene

Alle 12 er engelske originalutgaver (`/oj`), har `op-spec-version` 2.0.0.0, er laget med `akn4eu2html4eu-1.0.0.0` og har samme byggetidspunkt, `2026-09-15T18:42:15Z` (v). Ingen er konsolidert, og ingen er endringsakter (søkt etter «amended as follows» og tilsvarende, ingen treff) (v). Alle ID-er er unike i fila (v). Rotelementet har Office-navnerom (`xmlns:mso`) og betingede kommentarer i `<head>`, som tyder på en Word-mal [slutning].

| Fil | Dokumenttype (ELI) | Hva den viser |
|---|---|---|
| 32014R0699 | Kommisjonens gjennomføringsforordning (`reg_impl`) | Figur med bilde i vedlegg (`fgr`, `img`), unummererte ledd, punkter, `resource="doc:oj"` på `<body>`, `eli:has_annex` |
| 32017L0593 | Kommisjonens delegerte direktiv (`dir_del`) | Kapitler, 16 artikler, nummererte ledd, underledd, innledninger og 81 punkter |
| 32022R0092 | Gjennomføringsforordning (`reg_impl`) | Tre tabeller, en formel (MathML), tankestreker (bare i tabeller, uten ID), vedlegg, `doc:oj` på `<body>` |
| 32022R1031 | Europaparlamentets og Rådets forordning (`reg`) | Kapitler, 15 artikler, to signaturblokker, og to erklæringer etter skillelinjer (`section.statement`, ikke i spesifikasjonen) |
| 32024D1284 | Gjennomføringsbeslutning (`dec_impl`) | Kort akt: to artikler med unummererte ledd |
| 32024D2133 | Rådsbeslutning (`dec`) | Tre artikler, fotnoter |
| 32024L2831 | Europaparlamentets og Rådets direktiv (`dir`) | Den største: 6 kapitler, 32 artikler, 73 betraktninger, nestede punkter (`…/pnt_a/sub_1/pnt_i`) |
| 32024R0203 | Gjennomføringsforordning (`reg_impl`) | Fire artikler, nummererte ledd |
| 32024R2166 | Kommisjonsforordning (`reg`) | Vedlegg med tabell |
| 32026D0429 | Delegert beslutning (`dec_del`) | Den minste: to artikler |
| 32026R0174 | Delegert forordning (`reg_del`) | Ti artikler, punkter |
| 32026R0718 | Gjennomføringsforordning (`reg_impl`) | Tre artikler, 17 betraktninger, mange fotnoter |

Det eksemplene **ikke** viser: Part, Title, Section, Subsection, Appendix, avslutning (`wrp`), note, konsolidert tekst, endringsbestemmelser, andre språk enn engelsk.

### 2.6 Uoverensstemmelser i spesifikasjonen og eksemplene

1. **RDFa har skiftet retning.** v1.0 sa at `@rel` ikke skal brukes i det semantiske laget, og brukte `@typeof` og `@property`. 2.0 §4.3 bruker `rel="eli:has_part"`, mens kartleggingsvedlegget (utkast) fortsatt bruker `@typeof` og CDM. Eksemplene følger §4.3 (v).
2. **Toppnivået:** bare 2 av 12 eksempler har `resource="doc:oj"` på `<body>`. I de andre har `has_part`-kjeden ingen uttrykkelig akt som subjekt [slutning; en RDFa-prosessor er ikke kjørt].
3. **Klasser som ikke står i spesifikasjonen:** `document-separator` og `statement` (v).
4. **Klassenavn:** spesifikasjonen sier både `paragraph` og `numbered-paragraph` om nummerert ledd. Eksemplene bruker `paragraph` (v).
5. **Vedleggskoden:** eksempelet for Appendix skriver `ann_II`, men koden er `anx`. Eksemplene bruker arabertall (`anx_1`), spesifikasjonen romertall (`anx_I`) (v).

### 2.7 Hvem bruker det, og hva finnes rundt det

- **EUR-Lex:** brukte ikke HTML4EU i to stikkprøver 2026-11-24 (en originalutgave og en konsolidert GDPR). Begge sidene er Formex-avledet HTML uten RDFa. Den konsoliderte GDPR-siden har ELI-ID-er på artikkelnivå (`art_1`, `art_1.tit_1`) og kanonisk adresse `eli/reg/2016/679/2016-05-04/eng` (v) [EUR-Lex].
- **Andre brukere:** ikke funnet. Søk etter HTML4EU gir bare Publikasjonskontorets egne sider (2026-11-24).
- **AKN4EU:** «current working version», ikke fullt innført ennå. Omfatter rettsakter etter den ordinære lovgivningsprosedyren og forslagene til dem (v) [AKN4EU].
- **LegalHTML:** erstattet av HTML4EU (v) [LH]. En ESWC-artikkel fra 2023 beskrev LegalHTML (bare søketreff lest).
- **LEOS** (Kommisjonens redigeringsverktøy): ikke undersøkt i denne runden.
- **ELI-DL:** omtalt i 04 §7.3. Ingen kobling til HTML4EU funnet.
- **Nasjonale HTML-profiler:** ingen funnet.
- **Lovdata:** dokumentsidene har ID-er som `KAPITTEL_N` og `PARAGRAF_N` (fulgte §-nummeret i forvaltningsloven 2025), ledd som `data-id="AVSNITT_N"` og egne `data-id` som `lov/2025-06-20-81/§5`. Ingen RDFa i dokumentsiden (v) [Lovdata]. ELI-siden har RDFa på aktnivå (04 §7.3). Ingenting tilsvarende HTML4EU funnet.
- **Norden:** Finlex har Akoma Ntoso og ELI-alias (04 §7.5). Ingen HTML-profil med underinndelings-ID funnet i Sverige, Danmark eller Finland (bare søk, ikke side for side).

---

## 3. Kan HTML4EU brukes hos oss? (spørsmål 1)

Rettet etter juristrunden (§7).

**(a) Importformat for EU-rett i korpuset: nei for tekst, ja for adressering.**
- Det som gjelder i Norge, er forordningen med EØS-tilpasningene (for eksempel personopplysningsloven § 1). Den norske teksten er like gyldig og kunngjøres i EØS-tillegget (EØS-avtalen art. 129 nr. 1). HTML4EU gir EU-språkene og originalutgaven, ikke den norske teksten. HTML er heller ikke den autentiske utgaven; den er den elektroniske utgaven av EU-tidende [ikke verifisert: forordning (EU) nr. 216/2013 ble ikke åpnet].
- Forordninger som er inkorporert som lov og trykt hos Lovdata, ligger allerede i korpus som norsk lov. Eksempel: GDPR-artiklene i personopplysningsloven har eId som `…/lov/2018/06/15/38/nor/a5`, under `kap-gdpr/rom-II` (v) [Korpus]. Formuleringen i 03 §3, «EØS-rett er kilde utenfor korpus», er derfor upresis for slike forordninger [slutning].
- Det finnes bare 12 eksempler, og EUR-Lex brukte ikke formatet i to stikkprøver.
- **Ja som adressering:** ELI-underinndelingene kan peke presist på en EU-bestemmelse som kilde utenfor korpus, og kan gi en krysstabell for EU-akter i norske lover (`a5` ↔ `art_5`). Krysstabellen må kalles «tilsvarende bestemmelse i EU-utgaven», ikke identitet. Under artikkelnivå mangler vi både underleddet og den trykte bokstaven (K6, K7 i §4).

**(b) Eksportformat for vår konsoliderte tekst eller nettside: ikke som det står.**
- Profilen «Legal act» er skrevet for EU-akter (fortale, betraktninger, artikler, signaturer). Den har ledd (`par`, `unp`, `sub`) og punkter som kan brukes på norske ledd, men ikke punktum, versjon, konsolidert tekst eller endringsbestemmelser.
- Kjernen kan brukes som **mønster**: dokumentmalen, `id` = underinndelingssti, `resource` = full ELI med versjonsledd, og `rel="eli:has_part"`. Det passer for et HTML-uttrekk med RDFa når Lovdatas paragraf-ELI får funksjon (04 §2, «Seksjons-ELI»). Versjonsleddet må med, ellers får vi samme uklarhet som vår uttrykks-ID uten dato (04 §2).

**(c) Kilde til varig elementidentitet (#495): nei.** Se §5.

---

## 4. Mappingtabell (spørsmål 2)

Koblingstypene er grunnlagsdokumentets §5.7: **nært beslektet**, **snevrere** (vårt begrep er snevrere), **bredere** (vårt begrep er bredere), **ingen match**. Opplysningene om HTML4EU er (v) mot [H2] og [EKS]. Radene er rettet etter juristrunden (§7); kolonnen «Runde» viser hva som ble endret.

**Status for tabellen:** mapping vurdert, ikke del av profilen. Den føres ikke inn i 04 før prosjekteieren har valgt.

| Vårt begrep (eId) | HTML4EU 2.0.0.0 | ELI | Kobling | Merknad | Runde |
|---|---|---|---|---|---|
| K1 Rettskilde, kunngjort eller konsolidert (`https://lovdata.no/eli/lov/…/nor`) | `<article class="act">`; `doc:` = aktens ELI-basis; `resource="doc:oj"` på `<body>` i 2 av 12 eksempler | `…/{type}/{år}/{nr}/oj` | bredere | Kunngjort tekst (Lovtidend) ↔ `/oj`: nært beslektet. Konsolidert tekst ↔ EU sin konsoliderte ELI med dato, som HTML4EU ikke beskriver: ingen match. At HTML4EU er laget for originalutgaven, er en slutning fra §4.3 og eksemplene. Vår uttrykks-ID mangler dato (04 §2) | rettet (var «nært beslektet») |
| K2 Rettskildeversjon (`erstatter`-kjeden, `tekst_hash`) | Ingen. `op-html-build` er når fila ble laget | Konsolidert ELI har dato i stedet for `oj` | ingen match | HTML4EU har ingen versjonskjede | holder |
| K3 Kapittel (`kap-N`, lokal) | `<section class="chapter" id="cpt_I">`, overskrift `cpt_I/tit_1` | `cpt` | nært beslektet | Begge bygger ID av trykt nummer. HTML4EU-ID-en blir global med `doc:`, vår er lokal uten rot. Unntak: i personopplysningsloven er `kap-gdpr` hele forordningen (svarer til akten), og EU-kapitlene er Underinndeling | presisert |
| K4 Underinndeling (`{forelder}/rom-X`): romertallsavsnitt i et norsk kapittel, og i personopplysningsloven både EU-kapitler (`kap-gdpr/rom-III`) og EU-seksjoner (`kap-gdpr/rom-III/rom-1`) | `section` (`sct`); over kapittel `part` (`prt`), `title-subdivision` (`tis`); under `subsection` (`sbs`) | `sct`, og `cpt` for EU-akter i norske lover | bredere | Rekkefølgen Part > Title > Chapter > Section > Subsection er påbudt. Del (`prt`) har ingen node hos oss. Eksport må bruke dybden og kilden, ikke nodetypen, for å velge `cpt` eller `sct` | rettet (var «nært beslektet») |
| K5 Paragraf (`{lov-ELI}/§1-1`; `…/a5` for GDPR-artikler) | `<section class="article" id="art_1">`, overskrift `art_1/tit_1` | `art` | nært beslektet | Lovdata-paragraf = AKN article (04 §2). Begge bruker trykt nummer, og ingen av dem prefikser med kapittel | holder |
| K6 Ledd (`…/ledd-N`, løpenummer) | Nummerert: `li.paragraph#art_X/par_N`; unummerert: `p.unnumbered-paragraph#art_X/unp_N`. Underledd i et nummerert ledd (`sub_N`) har ikke noe tilsvar hos oss | `par` / `unp` (`sub`: ingen match) | bredere | Vårt ledd dekker `par` og `unp`. I EU-tekst i korpuset limes underleddene inn i leddteksten foran lista (personopplysningsloven `a6/ledd-1` og `ledd-3`, (v) [Korpus]). I EØS-norsk heter EU sin paragraph «nr.» og subparagraph «ledd», så vår Ledd svarer der til «nr.». Nummeret står i teksten («(1)», «1.»), men ikke i et eget felt | rettet |
| K7 Punkt (`…/punkt-N`, løpenummer) | Bokstav eller tall: `li.point#…/sub_1/pnt_a` (trykt bokstav); tankestrek: `li.indent#…/idt_N` | `pnt` / `idt` | bredere | Vårt punkt dekker begge, også nestede. Den trykte bokstaven er ikke lagret hos oss, verken i nummeret eller i teksten (`a6/ledd-1/punkt-1` til `6`), så `punkt-N` ↔ `pnt_x` krever kilde-HTML (04 §2, avvik 3). Stien hos HTML4EU har et ekstra nivå: `par_1/sub_1/pnt_a` mot `ledd-1/punkt-1`. `idt` finnes i eksemplene bare i tabeller, uten ID | presisert |
| K8 Leddets tekst foran en liste (innledningen, docs/08 §1.2) | `p.introductory-part#…/sub_1/inp` | `inp` | nært beslektet | HTML4EU gjør innledningen til en egen del med egen ELI. Hos oss er den leddets tekst. Forbehold: i EU-tekst i korpuset inneholder leddteksten også de neste underleddene (K6), og er da ikke det samme som `inp` | presisert |
| K9 Avslutning (`{forelder}/avslutning`, `-2` …, #361) | `p.closing-part#…/sub_1/wrp` | `wrp` | nært beslektet | Hver liste står i sitt eget underledd, så HTML4EU trenger ikke nummerere avslutninger. Vi trenger det fordi vi mangler underleddet [slutning] | holder |
| K10 Vedlegg (ingen egen nodetype, docs/08 §3.1.3) | `section.annex#anx_I`, tillegg `anx_I/app_I`, `rel="eli:has_annex"` | `anx` / `app` | ingen match | Teksten finnes hos oss, typen gjør ikke | holder |
| K11 Tabell (flates ut, docs/08 §1.2) | `<table id="tab_N">` | `tab` | ingen match | Et tap for normative tabeller (satser, frister) | holder |
| K12 eId som identitet | `@id` = underinndelingssti; `@resource` = `doc:` + sti + `/oj` | ELI-underinndeling for originalutgaven | nært beslektet (som adresse); ingen match (som varig identitet) | Begge bygger ID-en av trykt nummer og løpenummer, men HTML4EU bruker trykt bokstav for `pnt`, vi bruker løpenummer. Den fulle EU-ressursen (`…/oj`) er knyttet til en utgave som ikke endres, og blir ikke gitt til et annet element [slutning]. Vår eId bygges på nytt i hver versjon og kan gå til et annet element (#495). Ingen av dem følger elementet på tvers av versjoner (AKN `wId` eller GUID) | rettet |
| K13 Seksjons-ELI (`offisiell_eli`, tom; egen utvidelse `{ELI}/§10-2`) | `resource="doc:art_10/par_2/oj"` med `rel="eli:has_part"` | EU-malen for underinndelinger; Lovdatas `section/152` uten funksjon | nært beslektet | Mønsteret er det vi trenger når Lovdata gir nivået funksjon. EU-kodene (`art`, `par`) er ikke Lovdatas (`section`). Ressursen må ha versjonsledd (`oj` eller `consolidated`) | holder |
| K14 Forelder–barn mellom noder | Nesting + `rel="eli:has_part"`; `rel="eli:has_annex"` på vedlegg | `has_part` / `is_part_of`, `has_annex` | nært beslektet | Kjedene har ulik dybde: EU går `par → sub → pnt`, vi `ledd → punkt`. Grupperende beholdere (citations, recitals) får ingen ressurs | holder |
| K15 Kryssreferanse (`<ref href="#{ELI-eId}">`) | Ikke beskrevet i 2.0, bare fotnoteankre | – | ingen match | | holder |
| K16 Endringer og opphevelse | Ingen. `html4eu-modification-notice` står i malen uten beskrivelse | `amends`/`amended_by`, `consolidates`/`consolidated_by`, `in_force` | ingen match | v1.0 hadde et uferdig utkast. LegalHTML dekket konsolidering | holder |

**Tverrgående mønster** (fra juristrunden): EU-akter i norske lover ligger ett nivå forskjøvet. Akten blir Kapittel (`kap-gdpr`), EU-kapitlet blir Underinndeling, og EU sitt nummererte ledd blir Ledd. Underleddet forsvinner, og punktbokstaven lagres ikke.

---

## 5. Betydning for #495 (varig elementidentitet)

- HTML4EU har **ingen** identitet som følger et element fra versjon til versjon. ID-ene er adresser i én utgave (`/oj`), bygget av trykt nummer der det finnes og løpenummer ellers (§2.3).
- Forskjellen fra vår eId: EU-ressursen `…/oj` hører til en utgave som ikke endres, så den gis ikke til et annet element senere [slutning]. Vår eId bygges på nytt i hver konsolidert versjon og kan gå til et annet element etter en omnummerering. HTML4EU bekrefter altså ikke at vi kan klare oss med én nøkkel. Det trengs fortsatt en egen varig ID ved siden av eId [slutning; spesifikasjonen sier ikke noe om dette].
- HTML4EU viser en ryddig form for den **offentlige adressen**: `id` = relativ sti, `resource` = full ELI med versjonsledd. Den kan brukes for adresse-delen av en dobbel nøkkel. Den varige delen må vi lage selv.
- Reglene for ID-ene kan endres når ELI-underinndelinger versjon 3 publiseres.
- At EU sjelden omnummererer artikler (innsatte artikler får 4a), er lovteknisk praksis, ikke en egenskap ved ID-en [slutning].
- **Utenfor #495, men funnet her:** i GDPR-teksten i personopplysningsloven er underleddene limt inn i leddteksten foran punktlista (`a6/ledd-1` og `ledd-3`), og punktbokstavene er ikke lagret. Det ligner feilen #361 rettet for norske ledd. Kontrollert i korpuset 2026-11-24 for `a6/ledd-1`; årsaken i kilde-HTML-en er ikke undersøkt.

---

## 6. Anbefaling (spørsmål 3)

Valget er prosjekteierens. Anbefalingen er **(b) følge med**, med en rad i sjekklisten (08).

**Hvorfor ikke (a) ta inn i profilen nå:**
1. For norsk rett gir HTML4EU aldri den teksten som gjelder (EØS-tilpasninger, norsk tekst i EØS-tillegget), og HTML er ikke den autentiske utgaven. Formatet kan derfor bare brukes til adressering og visning, også når EUR-Lex tar det i bruk.
2. Versjon 2.0 er to måneder gammel, var ikke i bruk på EUR-Lex i to stikkprøver, har et kartleggingsvedlegg merket utkast og ingen profil for rettsavgjørelser. RDFa-tilnærmingen har skiftet mellom 1.0 og 2.0 (§2.6).
3. Formatet dekker ikke endringer eller konsolidering, som LegalHTML gjorde.
4. Profilen for rettsakter er skrevet for EU-akter og passer ikke norsk lovstruktur uten tilpasning (§3 b).

**Hvorfor ikke (c) ikke relevant:** HTML4EU er Publikasjonskontorets formelle HTML-format for EUR-Lex, bygger på ELI og ELI-ontologien, har erstattet LegalHTML, og blir trolig formen EU-rett publiseres i [slutning]. Det gir presise adresser til EU-bestemmelser og et mønster for HTML med RDFa som vi kan bruke selv.

**Utløsere for å ta det opp igjen** (kan kontrolleres, står i 08):
1. EUR-Lex publiserer i HTML4EU (se etter `<meta name="op-spec-version">` på EUR-Lex-sidene).
2. HTML4EU får profil for konsolidert tekst eller endringsbestemmelser, eller `modification-notice` blir beskrevet.
3. ELI-underinndelinger versjon 3 publiseres.
4. Lovdata gir paragraf-ELI funksjon eller publiserer HTML med ELI-underinndelinger.
5. Lovdata eller EØS-tillegget publiserer EØS-akter med ELI-underinndelinger. Da kan krysstabellen (§3 a) bygges uten å lese HTML.
6. Prosjekteieren bestemmer å bygge HTML-eksport med RDFa eller lenking til EU-kilder.

**Hva (b) koster:** én rad i 08. Ingen kode. **Hva (a) ville kostet:** tabellen i §4 inn i 04, en ny juristrunde mot den versjonen som da gjelder, og en avgjørelse om ELI-koder for norske nivåer (paragraf, ledd, punktum) som Lovdata ikke har tatt.

---

## 7. Juristrunden (spørsmål 4)

Én runde, etter instruksen i `.claude/agents/jurist-utfordrer.md`, kjørt som egen agent 2026-11-24. Juristen fikk kortene uten begrunnelsene og leste ikke dette notatet. Kort, utfordringer og svar ligger i [juristrunde-html4eu-koblinger.json](juristrunde-html4eu-koblinger.json).

- **Runde 1:** 20 kort (16 koblinger, 3 påstander om bruk, 1 anbefaling). 9 holder og 11 innvendinger (5 middels, 6 lav). Ingen høy, ingen kontroller, ingen prinsipielle. Ingen konklusjon ble snudd.
- **Svar:** modellereren aksepterte 10 og svarte delvis på 1 (A1). Tabellen og teksten over er rettet etter det aksepterte.

Det runden endret:
- **EØS-rett (B1):** grunnlaget er nå forordningen med EØS-tilpasningene og den norske teksten i EØS-tillegget. Krysstabellen kalles «tilsvarende bestemmelse», ikke identitet. «EØS-rett er kilde utenfor korpus» er upresist for forordninger som er inkorporert som lov.
- **Ledd og underledd (K6, K8):** `sub` har ikke noe tilsvar hos oss, og underleddene limes inn i leddteksten i GDPR-teksten. Terminologien i EØS-norsk («nr.» og «ledd») er tatt med.
- **Varighet mot identitet på tvers av versjoner (K12, B3):** to ulike egenskaper. EU-ressursen er varig for én utgave; vår eId er ingen av delene.
- **Koblingstyper:** K1 og K4 er rettet til «bredere».
- **Anbefalingen (A1):** det sterkeste argumentet (teksten som gjelder i Norge) og en norsk utløser (5) er lagt til.

**Uenighet som står igjen** (bare én runde, juristen har ikke replisert):

1. **Publiseringsdatoen (A1, liten).** Juristen fant ikke 21. september 2026 i de lokale kildene, bare byggetidspunktet 15. september 2026 i eksemplene. Modellereren har datoen fra datasettsiden (fanen Downloads, «Published: 2026-09-21») og fra Cellar-adressene til distribusjonene (`…/html4eu/20260921-0/…`), lest 2026-11-24. Byggetidspunktet er når HTML-filene ble laget, ikke når spesifikasjonen ble publisert.

Det runden ikke kontrollerte: forordning (EU) nr. 216/2013 (EUR-Lex blokkerte oppslaget), og om Lovdatas kilde-HTML har punktbokstaven.

---

## 8. Det som ikke er sjekket

- Spesifikasjonen for ELI-underinndelinger (versjon 2) og autoritetstabellen Subdivision: EUR-Lex blokkerte nedlastingen. Reglene for innsatte, omnummererte og opphevede deler er derfor ikke lest.
- Om EUR-Lex bruker HTML4EU for andre akter enn de to stikkprøvene.
- Lisens for spesifikasjonen og eksemplene.
- Valideringsrammeverket datasettsiden nevner: ikke funnet. Et søketreff nevnte SHACL og HTML-linting; det står ikke på sidene vi leste (ikke verifisert).
- LEOS og hvordan AKN4EU faktisk produseres.
- Kildene «EUR-Lex requirements» og «HTML Specification Framework» som v1.0 viser til.
- At eksemplene gir gyldig RDFa: ingen RDFa-prosessor er kjørt.
- Om Lovdatas kilde-HTML eller XML har punktbokstaven eller noe som kan tjene som varig elementidentitet.
- Nordiske kilder side for side.
