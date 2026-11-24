# RUML vurdert mot profilforslaget og strukturmodellen

[Ny, 2026-11-24] Prosjekteieren ba 24. november 2026 om en vurdering av RUML (lansert samme uke): kan det gå inn i modellene og standardene våre? Notatet svarer på det, foreslår koblinger og gir en anbefaling. **Ingenting er besluttet.** Mappingnotatet (04) er ikke endret. Koblingene under er forslag til rader i 04 §7 hvis prosjekteieren velger det.

Status: arbeidsutkast. Normativt: nei (informativt, som 04).

**Kortversjon.** RUML er et åpent JSON-format for beslutningstrær over lovtekst, utgitt i versjon 1.0.0 den 15. november 2026 av et selskap i Rulemapping-gruppen i Berlin. Det dekker regellaget vårt (vilkår, regel, operatorer), kobling til lovtekst gjennom ELI og Akoma Ntoso, og kjøring av en konkret sak. Det dekker ikke strukturmodellen (aktører, kompetanse, plikter, områder) og har ingen kunnskapsstatus eller godkjenning per utsagn. KI-verktøyene til selskapet lager utkast til slike trær fra lovtekst. Selve formatet lar i tillegg en språkmodell avgjøre et vilkår i en konkret sak. Anbefalingen er **(b) følge med** for regellaget og **(c) ikke relevant** for resten av profilen: før RUML i sjekklisten (08), og ta det opp igjen når en av tre utløsere inntreffer (§6). Juristrunden (§7) fant i tillegg at vår eId ikke er en varig elementidentitet, noe som berører mappingnotatet 04 §2 uansett hva som velges for RUML.

---

## 1. Kildene og merkingen

Merkingen er den samme som i 04: **(v)** er slått opp i primærkilden, **(ikke verifisert)** betyr at kilden ikke kunne nås eller ikke sier noe. Alt er hentet 2026-11-24.

Kortnøkler:
- **[RUML]** RUML – Rulemapping Logic Format, spesifikasjon 1.0.0: https://ruml.org/ (versjonsadresse https://ruml.org/1.0.0/). Kapitlene er lest både på nettstedet og som Markdown i repoet.
- **[RUML-repo]** https://github.com/Law-as-Code-Institute/ruml, lest via GitHub-API-et: README, LICENSE.md, CONTRIBUTING.md, `spec/en/`, `schemas/ruml.schema.json`, utgivelsen v1.0.0 (2026-11-15) og sakene #1 og #2.
- **[RUML-parsere]** https://github.com/Law-as-Code-Institute/ruml-js og https://github.com/Law-as-Code-Institute/ruml-python (README).
- **[RM]** https://rulemapping.org/ (forsiden; undersiden /ruml svarte 401). PoC-siden https://rulemapping.org/pocs/ai-powered-decision-support-for-building-permits.
- **[LaC]** Law as Code Institute: https://law-as-code.org/
- **[TUM-ARUM]** https://legaltech.cit.tum.de/projects/5_project/
- **[SPRIND]** https://sprind.org/en/actions/projects/rulemapping
- **[ITPLR]** Presentasjon fra Bundesministerium für Digitales und Staatsmodernisierung (BMDS) til IT-Planungsrat, «Information 2026/04 AL Sonderauftrag Law as Code»: https://www.it-planungsrat.de/fileadmin/beschluesse/2026/Information_2026_04_AL_Sonderauftrag_Law_as_Code.pdf (tekst trukket ut av PDF-en)
- **[arXiv]** «Beyond Imperfect Alternatives with Rulemapping: A Neuro-Symbolic Case Study on Online Hate Speech», arXiv:2605.16280 (bare sammendraget er lest)

---

## 2. Hva RUML er

### 2.1 Det som er dokumentert

| Tema | Hva kilden sier | |
|---|---|---|
| Navn og formål | «RUML – Rulemapping Logic Format». Et åpent, JSON-basert filformat for maskinkjørbar representasjon av juridisk beslutningslogikk. En fil holder et *rulemap*: vilkårene i en norm eller en beslutning som et tre av noder, med dataene det trenger og teksten det bygger på | (v) [RUML §1.1; RUML-repo README] |
| Versjon og dato | 1.0.0, «first published version». Utgivelsen v1.0.0 i repoet er datert 2026-11-15. Repoet ble opprettet 2026-11-10 | (v) [RUML endringshistorikk; RUML-repo] |
| Utgiver og rettigheter | Utgitt av Law as Code Institut GmbH. Opphavsrett og varemerke: Rulemapping Group GmbH, med søknad om EU-varemerke | (v) [RUML-repo LICENSE.md, README] |
| Lisens | Spesifikasjonsteksten: CC BY-SA 4.0. Skjemaer, tester, pakker og eksempler: Apache 2.0. Å lage, lese eller utveksle RUML-filer krever ingen lisens, og rulemaps tilhører forfatterne | (v) [RUML; RUML-repo LICENSE.md] |
| Styring | Spesifikasjonen definerer uttrykkelig **ikke** styringsmodell, sertifisering eller organisatoriske prosesser. Repoet er «an export of our internal sources»; pull requests tas ikke imot ennå, tilbakemelding går som issues. To åpne saker per 2026-11-24 | (v) [RUML §1.1; RUML-repo CONTRIBUTING.md] |
| Konformitet | Fem klasser: fil, parser, produsent, evaluator og valideringsverktøy. «Either conforms or it does not»; ingen nivåer og ingen sertifiseringsordning. Konformitetstester følger med som data | (v) [RUML §1.4, vedlegg A] |
| Implementasjoner | Parsere i TypeScript og Python (Apache 2.0). Python-parseren validerer bare og har «no evaluation logic». Skjemaene publiseres på npm og PyPI (selve registrene er ikke åpnet) | (v) [RUML-parsere] |
| Forhold til andre standarder | Bruker JSON, JSON Schema 2020-12, JSONata 2.2.0, RFC 8785, JWS, JWE, CMS, ISO 3166/639/8601/4217. Om nabostandarder: spesifikasjonen «does not define formal mappings» og gjør «no comparative claims» | (v) [RUML §1.5] |

### 2.2 Datamodellen

- **Kjernen** [RUML §3]: et flatt kart av noder med nøkkel (LexID, «1» er roten). Hver node har `id` (UUID v4) og `label`. En foreldernode har `children` og `operator`. En bladnode har ingen operator. Typen (rot, forelder, blad) lagres ikke, den følger av strukturen (v).
- **Operatorene** [RUML §3.6]: `AND`, `OR`, `XOR`, deres negasjoner `NOT_AND`, `NOT_OR`, `NOT_XOR`, og `DELEGATED` (kombinasjonen gis av en kapabilitet, normalt et JSONata-uttrykk som «minst tre av»). Logikken er treverdi: `true`, `false`, `undefined`. `XOR` er definert som en disjunksjon over barn «constructed so that at most one can be true», ikke som paritet (v).
- **Metadata for fila** [RUML §3.3]: blant annet `status` (`draft`, `review`, `approved`, `inForce`, `suspended`, `expired`, `replaced`), `authors`, `organization`, `jurisdiction` (ISO 3166), `validFrom`/`validUntil`, `documentType` (`law`, `regulation`, `guideline`, `decision` …) og `derivedFrom` (arbeidskopi, endring, revisjon). Spesifikasjonen har **ikke** noe felt for at innholdet er laget av en maskin eller en KI (v).
- **Kapabiliteter** [RUML §4]: alt utover treet legges på som deklarerte utvidelser. De offisielle er datapunkter, `ruml:query` (JSONata over data), `ruml:ai-query` (språkmodell, §4 under), handlinger (`onTrue`/`onFalse`/`onUndefined`), kontekstmateriale, tekstreferanser, protokoll (revisjonsspor som CloudEvents), uttrykk og referanser til andre RUML-filer. Tredjeparter kan lage egne med omvendt domenenavn som prefiks (v) [RUML §4.4].
- **Resultatfilen** [RUML §5]: evalueringen skrives i en egen `.ruml-result`-fil med status per node (`true`, `false`, `undefined`, `ignored`) og en begrunnelse (`reason`). `undefined` har bare to årsakstyper: `error` og `default` (v).
- **Container** [RUML §7]: ZIP med hash, signaturer (JWS, CMS) og kryptering (v).

### 2.3 Koblingen til lovteksten (det viktigste for oss)

- En RUML-fil har egne tekstoppføringer (`data`). Hver har `uri` (ELI anbefales der myndigheten utsteder ELI; Akoma Ntoso-sti og Gesetze-im-Internet-URL er også nevnt), `status` (`draft`, `proposed`, `enacted`, `inForce`, `amended`, `repealed`), `validFrom`/`validUntil`, `version` (fritekst) og `timestamp` (v) [RUML §4.12].
- En node peker på teksten med et **anker**: `represents` (normen noden gjennomfører, høyst ett per node) eller `refers-to` (en tekst noden bygger på uten å gjennomføre den, for eksempel en definisjon) (v).
- Ankeret kan peke på et element (paragraf, ledd, liste, punkt eller setningsmarkør) **bare hvis hele lovteksten ligger i fila** som «RUML Text»: et Akoma Ntoso-dokument skrevet som JSON. Peker fila bare på en URL, kan ankeret bare gjelde hele teksten (v).
- Elementene adresseres med `GUID`, ikke `eId`. Spesifikasjonen sier at `eId` er lesbar betegnelse som endres ved omnummerering, mens `GUID` aldri endres og skal føres videre til nye versjoner av teksten (v) [RUML §4.12 «Element Identity»]. Skjemaet godtar enhver UUID-versjon som GUID (v) [RUML-repo `ruml.schema.json`, `TextAnchor.element`].
- Tegnområder og sitatvelgere støttes uttrykkelig ikke (v).

### 2.4 Hvem står bak, og hvem bruker det

- **Rulemapping Group** (Berlin) fikk 12 millioner euro i egenkapital i april 2025, med SPRIND (det tyske byrået for sprangvis innovasjon) som betydelig investor (v) [SPRIND]. Metoden kommer fra en jusprofessor ved Viadrina (v) [SPRIND].
- **Rulemapping Solutions GmbH** driver rulemapping.org med verktøyene (v) [RM]. Sammenhengen mellom Law as Code Institut GmbH, Rulemapping Group GmbH og Rulemapping Solutions GmbH er ikke beskrevet noe sted vi leste (ikke verifisert).
- **Offentlig bruk av metoden:** BMDS (det tyske departementet for digitalisering og statsmodernisering) presenterte i 2026 Rulemapping for IT-Planungsrat som «eine Methode» for Law as Code, med pilotprosjekter, Rulemap Builder for departementenes regelverksarbeid og opplæring av lovgivere. Presentasjonen nevner også prøving av alternative metoder. RUML-formatet er ikke nevnt (v) [ITPLR]. En byggesaks-PoC med Thüringens digitaliseringsdepartement er beskrevet på [RM], uten omtale av RUML (v).
- **Offentlig bruk av RUML-formatet:** ikke funnet. Formatet er ni dager gammelt. Ingen norsk eller nordisk omtale av Rulemapping eller RUML ble funnet ved søk (2026-11-24).
- Logoer for tyske departementer og andre vises på [RM] uten at forholdet (kunde, partner, pilot) er oppgitt. Det er ikke tolket som bruk.

### 2.5 Det som er markedsføring eller ikke kontrollert

- «Open Format for Digital Law» og «open»: lisensen er åpen, men styringen er det ikke ennå (§2.1). Varemerket eies av ett selskap.
- At beslutninger «remain explainable and open to challenge» [RM]: en målsetting. Resultatformatet har begrunnelse per node, men se §4.2 om KI-avgjørelser.
- ISO 27001-sertifisering [RM]: påstand uten sertifikat (ikke verifisert).
- «97 % av kontrollstegene» i byggesaks-PoC-en: allerede vurdert i docs/10 («RUML 97 %-påstanden»). Leverandørtall fra én modul, ikke fagfellevurdert.
- arXiv-artikkelen: sammendraget sier at trærne er «expert-authored», altså laget av mennesker, og oppgir recall 0,82–0,89 og presisjon 0,80–0,86. docs/10 (juli 2026) oppgir 0,90–0,98 for samme artikkel. Avviket er ikke avstemt; det kan skyldes en ny versjon eller et annet mål (ikke verifisert). RUML er ikke nevnt i sammendraget.

---

## 3. Hvilket lag RUML dekker (spørsmål 1)

Modulene er grunnlagsdokumentets §6. Strukturmodellen er docs/33 §4.

| Lag hos oss | RUML 1.0.0 | Vurdering |
|---|---|---|
| **Modul A Kilder og dokumentuttrykk** | Tekstoppføringer med ELI-URI, status, gyldighet og digest. Hele lovteksten kan bæres som Akoma Ntoso (RUML Text) med stabile GUID-er | **Delvis, og godt.** Samme byggesteiner som profilen (ELI, AKN). Men kildeidentiteten er lokal i hver fil, og tidsdata står per tekstoppføring, ikke per bestemmelse |
| **Modul B Entiteter og institusjoner** | Ingenting. Mottakere i handlinger er fritekst som `role:social-worker` | **Ingen dekning** |
| **Modul C Kompetanse** og **Modul D Normative posisjoner** | Ingenting. Ingen plikt, tillatelse, forbud, kompetanse eller modalitet | **Ingen dekning** |
| **Modul E Regelstruktur** | Tre med OG/ELLER/XOR og negasjon, datapunkter, beregninger (JSONata), gjenbruk av delnoder | **Delvis.** Vilkår og sammensetning ja. Unntak, rettsfølge, prioritet, frister og skjønn nei |
| **Modul F Bruk og beslutningsstøtte** | Evaluering av en konkret sak, resultatfil, protokoll, handlinger ved uavklart | **Dekket, men utenfor profilens første versjon.** Profilen handler om hva loven sier, ikke anvendelse på en sak (grunnlagsdokumentet §15, «utenfor RC1») |
| **Tverrgående: proveniens** | Per fil: forfattere, organisasjon, avledning, signatur | **Delvis.** Ingen proveniens per utsagn |
| **Tverrgående: kunnskapsstatus, godkjenning, svakeste premiss** (docs/33 §4.6) | Ingenting per node. Filstatus `draft` → `review` → `approved` → `inForce` | **Ingen dekning** per utsagn |

**Kort sagt:** RUML er et regellag med god kildeforankring og en kjøremotor-kontrakt. Strukturmodellen vår er et annet lag. De overlapper i Modul E og delvis i Modul A, og ikke ellers. Profilforslaget og RUML er derfor ikke konkurrenter: RUML svarer på «er vilkårene oppfylt i denne saken?», strukturmodellen på «hvem har myndighet og plikter etter denne bestemmelsen?» (S1–S9 i docs/32).

**Hull hos RUML sett fra oss:** aktører og kompetanse, kunnskapsstatus per node, godkjenning per node, merking av maskinforslag, unntak som eget begrep, skjønn (skjønnsgrunnlag og -momenter), modalitet.

**Hull hos oss sett fra RUML:** en kjørbar evalueringskontrakt med resultatfil og protokoll, beregningsuttrykk (vi har bare `ErFormel`-annoteringen), gjenbruk av delnoder på tvers av filer, signert pakking, og konformitetstester som data.

---

## 4. Kobling, eksport og import (spørsmål 2)

### 4.1 Forslag til koblingsrader (samme form som 04 §7)

Koblingstypene er grunnlagsdokumentets §5.7: **nært beslektet**, **snevrere** (vårt begrep er snevrere), **bredere** (vårt begrep er bredere), **ingen match**. Typen gjelder hvilke tilfeller begrepene dekker, ikke hvor mange felt de har. At vi har noe per utsagn og RUML per fil, er en forskjell i nivå og står i merknaden. Alle RUML-opplysninger er (v) mot [RUML] 1.0.0. Radene er rettet etter juristrunden (§7); kolonnen «Runde» viser hva som ble endret.

**Status for tabellen:** mapping vurdert, ikke del av profilen. Den føres ikke inn i 04 før prosjekteieren har valgt.

| Vårt begrep | RUML 1.0.0 | Kobling | Merknad | Runde |
|---|---|---|---|---|
| Regel (komposisjonsnode, OG/ELLER/IKKE) (docs/01 §5.3) | Foreldernode med `operator` [§3.5, §3.6] | nært beslektet | OG → `AND`, ELLER → `OR`. IKKE på ett barn → `NOT_AND` eller `NOT_OR` med ett barn. IKKE på en gruppe krever et valg per node: «ingen av» = `NOT_OR`, «ikke alle» = `NOT_AND`. Vår modell sier ikke hvilken som menes, så eksporten må spørre. Operatorene er like, men utfallet ved ukjente vilkår er det ikke: i RUML avhenger det av evalueringsmodus (`optimized`/`complete`) og barnas rekkefølge | rettet |
| Vilkår (bladnode) | Bladnode; verdien kommer fra `ruml:query` (også over et manuelt innlagt boolsk datapunkt) eller `ruml:ai-query` [§3.5, §4.7–4.9] | snevrere | Et RUML-blad kan også være noe vi ikke kaller vilkår: et helt deltre fra en annen fil (`ruml:tree-include`) eller en plassholder. Uten beslutningskapabilitet er bladet alltid `undefined`. `ruml:actions` er en reaksjon, ikke en verdikilde | rettet (var «bredere») |
| Unntak (`gjelder_regel` + `betingelse`, INV-3/INV-4) | Ingen egen type. Nærmeste omskriving er `AND(regel, NOT_OR(betingelse))` | ingen match | Omskrivingen er ikke nøytral: er unntaksbetingelsen ukjent, blir hele regelen `undefined`. Om hovedregelen skal gjelde til unntaket er godtgjort, er en rettslig vurdering per bestemmelse, som vår modell heller ikke har tatt. En slik presumsjon kan i RUML bare uttrykkes som standardverdi på et datapunkt, og må da merkes som rettslig slutning. Import lager aldri et unntak automatisk | rettet |
| Rettsfølge / `utdata` (docs/01 §7) | Ingen. `ruml:actions` iverksetter (dokument, betaling, tillatelse) | ingen match | Rettsfølgen skal aldri eksporteres som en `onTrue`-handling: det ville gjort et vedtaksforslag til en automatisk iverksatt avgjørelse | holder, presisert |
| *Ny rad:* Avklaringsbehov (eskaleringsrolle, `krever_dokumentasjon`; docs/01 §6.1) | `ruml:actions` `onUndefined` med `requestDecision` (mottaker `role:…`) eller `requestData` [§4.10] | nært beslektet | Begge er et navngitt stoppunkt ved ukjent utfall | ny |
| Skjønnsgrunnlag og skjønnsmomenter (docs/01 §6.1) | Ingen | ingen match | Skjønnsmomenter skal aldri eksporteres som barn under en terskel («minst 3 av 5») i `DELEGATED`: momenter vektes, de telles ikke. En importert terskelnode kan være en helhetsvurdering gjort om til telling og må merkes | presisert |
| *Ny rad:* Presedens (docs/03 §1.7) og presedensreferansen på et skjønnsmoment | `ruml:context`, referansemateriale (rettsavgjørelser, rundskriv, eksempelsaker) [§4.11] | bredere | Vår presedens har rettskildevekt; RUML graderer ikke | ny |
| Datasett / datapunkt (`input`) | `ruml:datapoints` med typer og enheter [§4.7] | nært beslektet | | holder |
| `ErFormel` (formel-annotering, docs/10) | Beregnet datapunkt (`binding.type: computed`, JSONata) [§4.7] | snevrere | Ikke `ruml:expression`, som kombinerer barnas sannhetsverdier i `DELEGATED`. `ErFormel` ligger på en node i treet, et beregnet datapunkt er ikke en node: eksporten må flytte formelen ut av treet, ellers blir noden alltid `undefined` | rettet |
| `juridisk_grunnlag[]` = {kilde, eId} på vilkår, regel og unntak | Anker i `refs` bare inn i tekst i samme fil: `represents` (normen noden gjennomfører, høyst én) eller `refers-to`, med `element` = GUID i innebygd RUML Text; eller `ruml:tree-reference` til en node i et eget lov-rulemap [§4.12, §4.15] | nært beslektet | Skillet `represents`/`refers-to` finnes ikke hos oss. Hvilken hjemmel en node «gjennomfører», er en fortolkning. Eksporten bruker `refers-to` til et menneske har valgt `represents`. Ankeret kan bare treffe et ledd hvis hele teksten ligger i fila som Akoma Ntoso og validerer (TXT-18); om vår AKN gjør det, er ikke kontrollert (04 §2) | rettet |
| Vår eId (bygget i hver versjon fra §-nummer og ledd- og punktindeks, docs/08 §1.2) | Mot AKN-`eId` i RUML Text (betegnelse i gjeldende versjon): nært beslektet. Mot `GUID` (varig elementidentitet, TXT-03): ingen motpart hos oss | ingen match (mot GUID) | **Viktigste funn i runden.** Vår eId oppfører seg som AKN sin `eId`, ikke som `wId` eller GUID: blir § 3 til § 4, får elementet ny eId, og den gamle kan gå til et annet element. GUID kan derfor **ikke** avledes av eId. Gjøres det, peker et anker etter en omnummerering på feil bestemmelse uten varsel, fordi RUML-ankere ikke har versjon eller hash. GUID må utstedes per element og føres videre med en eksplisitt koblingsrunde mellom versjoner. Påstanden i 04 §2 om at vår eId har `wId`-rollen bør derfor vurderes på nytt (§7) | rettet (høy) |
| Rettskilde med ELI | Tekstoppføring med `uri` = ELI på uttrykksnivå, med versjonsdato [§4.12] | nært beslektet | Vår uttrykks-ID uten dato betyr «gjeldende versjon» (04 §2) og kan ikke brukes for en eldre tekst. Vår seksjons-ELI-utvidelse (`{ELI}/§10-2`) skal ikke i `uri`; elementer adresseres med ankere | presisert |
| Rettskildeversjon (`erstatter`-kjeden, `tekst_hash`, opphevet) | Tekstoppføring med `version`, `validFrom`/`validUntil`, `status`, `digest` | nært beslektet | `amended` («still valid for legacy cases») krever en overgangsrettslig vurdering vi ikke registrerer; `enacted` krever ikrafttredelse per bestemmelse, som vi ikke har. Med innebygd tekst skal status og datoer komme fra AKN `lifecycle`/`temporalData`, som vi ikke skriver. `digest` er ikke `tekst_hash` og må beregnes på nytt | presisert |
| Kunnskapsstatus (åtte verdier) (docs/33 §4.6) | Ingen | ingen match | Kan bæres i en egen kapabilitet; andre verktøy vil ignorere den | holder |
| Svakeste premiss (docs/33 §4.6) | Ingen. Treverdi-logikken sprer `undefined` i en sak | ingen match | To ulike akser: utfall i en sak mot epistemisk status for et utsagn | holder |
| Godkjenning per utsagn (`maskinelt_foreslatt` … `avvist`) | `metadata.status` for fila [§3.3] | nært beslektet | RUML-statusen blander arbeidsstadium (`draft`, `review`, `approved`) og gyldighet (`inForce`, `suspended` …). Vi har `avvist` og skillet maskinelt/manuelt. Nivå: per utsagn mot per fil. Hvem som godkjenner (`approved`), er udefinert i RUML. Godkjenningen finnes bare på strukturkanter, som ikke kan uttrykkes i RUML, så raden har liten praktisk betydning | rettet (var «bredere») |
| Livssyklusen for vilkår, regel og unntak (docs/03 §3.1) | Filstatus (`draft`, `review`, `approved`), `derivedFrom`, `replaces`/`replacedBy` | nært beslektet | **Samleregel ved eksport:** fila får statusen til den svakeste noden; én node i `utkast` eller `foreslatt_av_ai` gjør hele fila `draft`. `publisert` hos oss er ikke `inForce`: eksport gir høyst `approved`. `inForce` passer bare når et organ har tatt kartet i bruk | rettet |
| Oppdagelseskilde (`manuell`, `monster:<id>`, `ki:<modell>`) | Ingen | ingen match | | holder |
| Proveniens (PROV-O, grunnlagsdokumentet §13) | `authors`, `organization`, `derivedFrom`, signatur i containeren [§3.3, §7.4] | nært beslektet | Signaturen gir ekthet, som vi ikke har. Vår proveniens per utsagn er et mål, ikke bygget fullt ut | rettet (var «bredere») |
| Kantene A og O (ansvarsområde, områdesammensetning) | Ingen | ingen match | A og O er stedlig kompetanse og forvaltningsstruktur, ikke jurisdiksjon | rettet |
| *Ny rad:* Rettskildens territoriale virkeområde (grunnlagsdokumentet §4.6, §12.7) | `metadata.jurisdiction` (ISO 3166) for hele fila | nært beslektet | Om ISO 3166-2:NO har koder under fylkesnivå, er ikke kontrollert; lokale forskrifter kan da ikke angis presist | ny |
| Tid på vilkår, regel og unntak (`gyldig_fra`/`gyldig_til`) | `validFrom`/`validUntil` for fila | bredere | Ingen av sidene skiller gyldighet fra anvendelsestidspunkt | rettet |
| K, P, R, M, O, A, G, I, T og F (strukturkantene) | Ingen | ingen match | Hele strukturmodellen | holder |
| Modalitet (skal/kan/bør) og polaritet | Ingen deontikk | ingen match | | holder |
| Vilkårsutfall (`KL-VILKARSUTFALL`) | Nodestatus i `.ruml-result`: `true`, `false`, `undefined` (`error`/`default`), `ignored` [§5] | nært beslektet | Fire av våre seks utfall er ulike grunner til `undefined`. RUML har `ignored` og `error`, som vi ikke har. **`ignored` skal aldri leses som oppfylt:** RUML teller ikke `ignored` med i forelderens operator, så et vilkår som hoppes over manuelt under AND kan gi `true`. Skillet `ignored`/`undefined` er det samme som docs/10 kandidat 1 («ikke spurt» mot «bevisst uavklart») | rettet (var «bredere») |

### 4.2 Eksport til RUML: hva går tapt

Et regeltre fra DigRett kan skrives som gyldig RUML. Logikken kan bevares, men bare med valg som modellen ikke har tatt (IKKE over en gruppe, tvilsrisiko ved ukjent unntak). Det som går tapt:

1. **Unntak som begrep.**
2. **Kunnskapsstatus, godkjenning og oppdagelseskilde per node.**
3. **Skjønnsgrunnlag og skjønnsmomenter.**
4. **Elementnivå-ankeret**, med mindre lovteksten bæres i fila som Akoma Ntoso med GUID. Det krever (a) AKN-eksport som validerer (04 §2, ikke bygget) og (b) en varig elementidentitet som vi ikke har (eId-raden i §4.1).

Punkt 1–3 kan bæres i en egen kapabilitet med vårt eget prefiks (omvendt domenenavn), merket `required: false`. Da er rundturen DigRett → RUML → DigRett tapsfri for dette, men andre verktøy vil ignorere opplysningene og vise treet som om alt var avgjort. Samleregelen for filstatus (§4.1, livssyklus) er det eneste vernet alle verktøy forstår.

**Eksporteres aldri:** `ruml:ai-query`, og handlinger som iverksetter en avgjørelse (`onTrue`/`onFalse` med vedtak, betaling eller tillatelse). Se §5.2.

### 4.3 Import fra RUML: regler

1. **Alt som importeres**, får livssyklusstatus `foreslatt_av_ai` (strukturkanter: godkjenning `maskinelt_foreslatt`) og en proveniensrad med handlingen `import:ruml`. Raden tar vare på det utgiveren oppga: `metadata.id`, `version`, hash, `status`, `organization`, `authors` og signatur (verifisert eller ikke). Opphavet registreres som «eksternt, ukjent om maskinelt» når fila ikke sier noe. Utgiverens status gir aldri godkjenning hos oss.
2. **Lovtekst i fila** (et lov-rulemap) erstatter aldri korpusteksten. Den kan bare sammenlignes, og regnes som kilde utenfor korpus.
3. **Sykliske strukturer** (rekursive delnoder, [RUML §6.4]) bryter DAG-kravet (INV-7) og avvises, de rettes ikke stille.
4. **`XOR`** importeres som ELLER med merknad om evalueringsmodus og at rekkefølgen betyr noe (ved ukjente barn gir `XOR` og `OR` ulikt svar). At barna utelukker hverandre, er forfatterens forutsetning: fortolkning eller uavklart, aldri tekstfunn. Til regellaget får en egen utsagnstype, står den i regelens beskrivelse, i proveniensen og som testtilfelle. `NOT_XOR` blir IKKE(ELLER) med samme forbehold.
5. **`DELEGATED` med JSONata** («minst tre av fem») har ingen motpart. Den kan bare importeres som en merknad med uttrykket som tekst, og en terskel over skjønnsmomenter merkes (§4.1).
6. **`ruml:ai-query`-noder** importeres som vilkår med vurderingstype satt etter lovteksten (som forslag, ikke utledet av at en språkmodell var satt til å svare), status `foreslatt_av_ai` og et flagg om at forfatteren lot en språkmodell vurdere vilkåret. Prompt og kontekstreferanser importeres som fortolkningsforslag med kilde. Eksempelmateriale kontrolleres for personopplysninger før det lagres. En språkmodells avgjørelse i en sak er aldri kilde for modellen.
7. **Unntak** kan ikke gjenkjennes og lages aldri automatisk.
8. **Resultatfiler:** `ignored` blir `ukjent` eller `ma_vurderes_av_jurist`, aldri oppfylt.

**Svar på spørsmålet:** RUML-trær kan kobles til våre rettskilder på verksnivå (ELI) og, med arbeid, på elementnivå. Elementnivået krever en varig elementidentitet som vi ikke har i dag. Kunnskapsstatus kan ikke uttrykkes i RUML uten en egen utvidelse. Eksport og import er mulig, men ikke tapsfri med bare de offisielle kapabilitetene, og eksporten krever valg som modellen ikke har tatt.

---

## 5. KI-verktøyet og regelen om maskinforslag (spørsmål 3)

Det er to forskjellige ting som begge kalles KI her. De må vurderes hver for seg.

### 5.1 KI som lager treet (ARUM)

- **Hva selskapet sier** (v) [RM]: ARUM («Automated Rulemap from Unstructured Material») tar lover, forskrifter og interne retningslinjer og lager «a first Rulemap that is refined in the Rulemap Builder». ARUM «provides the starting point; the finished model comes from modelling». Det kan også oppdatere et rulemap fra en endringslov, og leser BPMN og DMN. Nodene i byggeren lenkes til tekstavsnittene de kommer fra, og ARUM fyller dem ut. Om verktøyene generelt: resultatene er «drafts and findings that remain open to review».
- **Forskningsprosjektet** (v) [TUM-ARUM]: «ARUM – Artificially Generated Rulemaps» ved TU München, finansiert av det tyske forskningsdepartementet (KMU-innovativ), to år, med Rulemapping Group som partner. Siden sier at språkmodeller «can potentially take over parts of this formalization process», men beskriver ikke metode, evaluering eller menneskelig kontroll. Utvidelsen av forkortelsen er ulik hos selskapet og universitetet; om det er samme verktøy, er ikke kontrollert.
- **Ikke kontrollert:** verktøyet ligger bak registrering i en testfase. Vi har ikke prøvd det, ikke sett et resultat, og vet ikke om det skriver RUML eller hvor presise tekstkoblingene er.

**Forholdet til vår regel.** Vår regel er at et maskinforslag er et forslag til et menneske har godkjent (`maskinelt_foreslatt` → `godkjent`, docs/33 §4.6 og §5.3), med kilde per utsagn. Selskapets **egen beskrivelse** sier at ARUM gir et utkast som modelleres ferdig. Om det faktisk skjer, kan ikke kontrolleres, og **formatet viser det ikke på noe nivå**: ingen felt sier at en node eller en fil er laget maskinelt, `authors` og `derivedBy` er personer, og hvem som godkjenner `approved`, er udefinert. Et lett redigert ARUM-utkast satt til `approved` kan ikke skilles fra et kart jurister har gått gjennom. Derfor behandles alt importert som ikke godkjent og med ukjent opphav (§4.3 punkt 1).

Sporbarheten til lovteksten er derimot godt tenkt i formatet (anker med `represents`, GUID, ELI), forutsatt at teksten bæres i fila og at elementidentiteten er varig.

### 5.2 KI som avgjør et vilkår i en sak (`ruml:ai-query`)

Dette er en del av selve standarden, ikke bare av verktøyet:

- En bladnode kan få verdien sin fra en språkmodell som svarer `true`, `false` eller `undefined` på en avgrenset prøving, med veiledning og eksempelsaker som kontekst (v) [RUML §4.9]. Eksemplet i spesifikasjonen gjelder om en begrunnelse for et unntak i en sosial ytelse er plausibel.
- **Modellen og ledeteksten står ikke nødvendigvis i fila.** `model` er en symbolsk verdi som `strong`; den faktiske modellen og malen ligger i verktøyets innstillinger (v).
- **Avgjørelsen dokumenteres ikke typet.** Resultatskjemaet har ikke noe eget format for KI-avgjørelser, så en gyldig resultatfil kan si at en språkmodell avgjorde et vilkår uten å si noe om hvordan. Dette er meldt som sak #1 i repoet 19. november 2026 (åpen) (v) [RUML-repo].
- Menneskelig vurdering kreves ikke. Spesifikasjonen anbefaler («SHOULD») en manuell handling når svaret er `undefined`, ikke når det er `true` eller `false` (v).

**Rettslig** (juristrundens retting, §7; ikke en fullstendig vurdering):
- **Personvernforordningen artikkel 22:** lar en språkmodell alene avgjøre et vilkår, og iverksetter en handling resultatet (`onTrue`/`onFalse`), er det normalt en avgjørelse som utelukkende bygger på automatisert behandling, med rettsvirkning (nr. 1). Den krever hjemmel med egnede garantier (nr. 2 bokstav b), og strengere krav for særlige kategorier som helseopplysninger (nr. 4). EU-domstolens dom C-634/21 (SCHUFA) tilsier at også et delresultat som i praksis avgjør utfallet, kan være en «avgjørelse» [slutning, overført fra kredittscore]. Et menneske i løkka teller bare når vurderingen er reell.
- **Forvaltningsloven** §§ 24–25 (begrunnelse) og § 17 (utredning): en `true`/`false` med en kort tekst er ingen begrunnelse, og formatet sikrer ikke at modell, ledetekst og svar kan etterprøves. Det svekker klagebehandling. Reglene om automatiserte avgjørelser i den nye forvaltningsloven (ikke i kraft) er ikke kontrollert.
- **KI-forordningen:** bruk hos offentlige myndigheter for å vurdere rett til offentlige ytelser og tjenester er høyrisiko etter vedlegg III nr. 5 bokstav a. Forordningen er ikke tatt inn i EØS-avtalen ennå. Regjeringen tar sikte på å legge fram en norsk KI-lov våren 2027, og EU har utsatt høyrisikoreglene til 2. desember 2027 eller 2. august 2028, avhengig av type system (regjeringen.no, pressemelding 4. august 2026, (v)).

**For oss:** profilen eksporterer aldri `ruml:ai-query` eller iverksettende handlinger (§4.2), og en `ruml:ai-query`-node blir aldri et tekstfunn eller en godkjent modell hos oss (§4.3 punkt 6). Rettslig vurdering per konkret bruk er ikke gjort.

---

## 6. Anbefaling (spørsmål 4)

Valget er prosjekteierens. Anbefalingen er delt etter lag:

- **For regellaget (Modul E): (b) følge med, med sjekklistepost i 08 og merknad i docs/10.** Koblingstabellen i §4.1 ligger her som informativ vurdering med status «mapping vurdert, ikke del av profilen».
- **For kilder, institusjoner, kompetanse og normative posisjoner (Modul A–D): (c) ikke relevant.** RUML har ingenting for kompetanse, plikt, struktur eller kunnskapsstatus. For kilder bruker det ELI og Akoma Ntoso, som profilen alt bygger på; det tilfører ikke noe nytt der.

**Hvorfor ikke (a) ta inn som mappet standard nå.** Mappinger er informative i første versjon (grunnlagsdokumentet §14.6), så en mapping er ingen tilslutning. Det som taler mot, er:
1. Vedlikehold mot en spesifikasjon som er ni dager gammel og har én utgiver uten styringsmodell eller åpen endringsprosess. Endres den, må mappingen kontrolleres på nytt.
2. Profilens første versjon har full regelmodellering «utenfor RC1» (grunnlagsdokumentet §15). Det er lite i profilen nå å mappe mot.
3. RUML er skrevet for å kjøre saker (handlinger, språkmodellavgjørelser). Det er uttrykkelig utenfor profilens første versjon.
4. Ingen offentlig aktør bruker formatet så langt vi har funnet. Den tyske forvaltningen bruker metoden, men har ikke valgt formatet.

**Hvorfor ikke (c) for alt.** RUML løser den samme oppgaven som regellaget vårt, med de samme kildestandardene, og har en kjøringskontrakt (resultatfil, protokoll, konformitetstester) som er nyttig å kjenne når regellaget skal eksporteres. Tysk forvaltning prøver ut metoden i regelverksarbeid og vedtak [ITPLR].

**Utløsere for å ta det opp igjen** (kan kontrolleres, føres i 08):
1. RUML får en åpen styringsmodell, åpner for bidrag fra andre enn utgiveren, eller overføres til et standardiseringsorgan.
2. En offentlig myndighet tar formatet i bruk: et norsk, nordisk eller EU-organ, eller et nasjonalt departement i EU/EØS (for eksempel at BMDS eller IT-Planungsrat velger RUML som format).
3. Prosjekteieren beslutter å bygge eksport av regellaget (docs/10 kandidat 5, eller utvidelse av #476).

**Gjelder fra første import eller eksport:** reglene i §4.2 og §4.3, med forbudet mot å eksportere `ruml:ai-query` og iverksettende handlinger.

**Opphavsrett:** spesifikasjonsteksten er CC BY-SA 4.0. Profilen skal henvise til begreper, ikke oversette eller bearbeide definisjonstekst, ellers kan del-på-samme-vilkår gjelde for den delen [slutning].

**Hva (b) koster:** én rad i 08 og en kort merknad i docs/10. Ingen kode. **Hva (a) ville kostet:** radene i §4.1 inn i 04 §7, en ny juristrunde mot den versjonen som da gjelder, og et kort på profilsiden. Ingen kode før eksporten bygges.

**Et funn som gjelder uansett valg:** juristrunden viste at vår eId ikke er en varig elementidentitet (§7). Det berører påstanden i 04 §2 og enhver framtidig eksport til Akoma Ntoso, ikke bare RUML. Det bør vurderes for seg.

---

## 7. Juristrunden (spørsmål 5)

Én runde, etter instruksen i `.claude/agents/jurist-utfordrer.md`, kjørt som egen agent 2026-11-24. Juristen fikk kortene uten begrunnelsene og leste ikke dette notatet. Kort, utfordringer og svar ligger i [juristrunde-ruml-koblinger.json](juristrunde-ruml-koblinger.json).

- **Runde 1:** 28 kort (22 koblinger, 3 importregler, 2 KI-vurderinger, 1 anbefaling). 6 holder og 22 innvendinger (1 høy, 14 middels, 7 lav). Ingen kontroller, ingen prinsipielle.
- **Svar:** modellereren aksepterte 20, svarte delvis på 2 (K1 og A1). Tabellen og reglene over er rettet etter det aksepterte.

Det runden endret:
- **Vår eId er ikke en varig identitet (høy).** Den bygges i hver versjon fra §-nummer og ledd- og punktindeks (docs/08 §1.2). GUID kan derfor ikke avledes av eId, og påstanden i 04 §2 om at vår eId har AKN sin `wId`-rolle bør vurderes på nytt. 04 er ikke endret.
- **«Bredere» sto feil vei** i fire rader (vilkår, godkjenning, proveniens, vilkårsutfall). Typen gjelder hvilke tilfeller begrepet dekker, ikke antall felt.
- **Feil begrep for jurisdiksjon:** A og O er stedlig kompetanse. Ny rad for rettskildens territoriale virkeområde. To andre nye rader: avklaringsbehov og presedens.
- **Unntak og tvilsrisiko**, **samleregel for filstatus**, **`ignored` er aldri oppfylt**, **`represents` er en fortolkning**, og **`ruml:expression` er ikke en formel**.
- **Personvernforordningen artikkel 22 var undervurdert og KI-forordningen overvurdert som gjeldende rett.** Pressemeldingen fra regjeringen 4. august 2026 er kontrollert av modellereren etter runden.
- **Importreglene** brukte ord fra strukturlaget som regellaget ikke har. De tar nå vare på utgiverens metadata, avviser sykliske strukturer og flagger språkmodellbruk i stedet for å utlede vurderingstype av den.
- **Anbefalingen** ble delt etter lag, utløserne gjort kontrollerbare, og kostnaden ved (a) rettet: mappinger er informative og binder ikke.

**Uenighet som står igjen** (bare én runde, juristen har ikke replisert):

1. **Kildegrunnlaget for at ARUM lager utkast (K1).** Juristen fant ingen kilde for at ARUM-utkast skal godkjennes av et menneske; juristen leste bare TU München-siden. Selskapets egen side sier at ARUM gir «the starting point; the finished model comes from modelling», og at verktøyenes resultater er «drafts and findings that remain open to review» [RM]. Modellereren mener derfor at selskapets *beskrivelse* er forenlig med vår regel; juristen mener påstanden mangler grunnlag. Begge er enige om konsekvensen: det kan ikke kontrolleres, formatet viser det ikke, og alt importert behandles som ikke godkjent og med ukjent opphav. §5.1 gjengir bare selskapets beskrivelse.
2. **Utløser 2 (A1, liten).** Juristen foreslo «bruk i et norsk, nordisk eller EU-organ». Modellereren har lagt til nasjonale departementer i EU/EØS, fordi Tyskland er det mest sannsynlige første stedet formatet tas i bruk, og fordi det kan kontrolleres.

Det runden ikke kontrollerte: ISO 3166-2:NO under fylkesnivå, om vår AKN validerer mot skjemaet, reglene om automatiserte avgjørelser i ny forvaltningslov, ordlyden i KI-forordningen art. 27 og 86 (vedlegg III ble lest fra en sekundærkilde), og IT-Planungsrat-dokumentet (lest av modellereren, ikke av juristen).

---

## 8. Det som ikke er sjekket

- ARUM, Rule AI og de andre verktøyene: ikke prøvd (registrering og testfase). Ingen utdata sett.
- rulemapping.org/ruml svarte 401. Lanseringsmeldingen selskapet eventuelt har publisert 24. november 2026, er ikke funnet; vi har bare spesifikasjonsdatoen 15. november.
- Sammenhengen mellom de tre selskapene, og hvem som eier Law as Code Institut GmbH (impressum ikke lest).
- Kapittel 6–8 i spesifikasjonen (delnoder, container, valideringsreglene) er bare lest i utdrag. Vedlegg A (testene) er ikke kjørt.
- Om npm- og PyPI-pakkene finnes og stemmer med repoet.
- Om Lovdatas XML har noe som kan tjene som varig elementidentitet (GUID) (ikke undersøkt). Juristrunden viste at vår eId ikke kan brukes til det (§4.1, §7).
- Om andre tyske delstater, Østerrike eller EU-organer bruker RUML.
- Regelrett, Skatteetatens regelverksutvikling og Lovdatas eventuelle planer for regellag: ikke sjekket i denne runden. Danmark (digitaliseringsklar lovgivning) er sjekket i 04 §7.5 og har ingen datamodell for regler.
- Den rettslige vurderingen i §5.2 (personvernforordningen artikkel 22, KI-forordningen, begrunnelsesplikt) er ikke gjort.
- Hele arXiv-artikkelen og avviket mot tallene i docs/10.
