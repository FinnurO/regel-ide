# 34 · Arkitekturvurdering: Regel-IDE mot forslag til norsk profil

**Status:** Vurdering og forslag, 15. oktober 2026. [ENDRET 2026-10-15] **Punkt 1–3 (§3–5) er under bygging i #367** (sammen med #335,
én migrasjon, Johanns beslutning 2026-10-15) — se docs/33 §4.6. Punkt 4 og 5 er ikke besluttet.

**Grunnlag:**
- `docs/profil/00-grunnlagsdokument.md` (Johanns standardforslag, revidert 9.10.2026),
- `docs/profil/03-metodenotat.md`,
- `docs/profil/04-mappingnotat.md` (verifisert mot primærkildene),
- dagens skjema (`strukturkanter`, `rettskilde_noder`) og docs/33.

**Formål:** Vurdere hvilke endringer Regel-IDE trenger for å være en troverdig utprøvingsarena for de første profilene (A kilde og tekst, B institusjon og struktur, C kompetanse) i standardforslaget §16. Det skal skje uten ombygging og uten å bryte beslutningene fra #341–#356.

---

## 1. Konklusjon først

**Grunnarkitekturen holder.** Én kanttabell med hjemmel, avgrensning, polaritet, gyldighet, status og opphav er i praksis et reifisert modellutsagn, slik standardforslaget §9.2 ber om. Det trengs ingen ny utsagnstabell, men fem utvidelser:

| # | Hull | Anbefaling | Kostnad |
|---|---|---|---|
| 1 | Status for et modellutsagn er for grov | Ny kolonne for **kunnskapsstatus**. Dagens `status` blir godkjenning | Lav. Én migrasjon |
| 2 | Analysen har ikke egen identitet og historikk | Kantens Id brukes som analyse-id. Nye felt: `erstatter`, `entitetsstatus`, `begrunnelse` og `tolkningsargumenter`. Egen tabell for premisser | Lav. Samme migrasjon |
| 3 | Hjemmelen peker ikke på en versjon | `hjemmel_node_id` peker på den versjonerte noden. Resynk flagger kanter når hjemmelen er endret | Lav. Infrastrukturen finnes |
| 4 | Oppgave/funksjon mangler | Ny kategori **F** (oppgave/funksjon). Krever beslutning og juristrunde på kandidatene | Middels |
| 5 | Plikt ≠ kravsrett | Semantikken til P dokumenteres. Feltet `kravsrett` legges til (ikke vurdert / etablert / ikke etablert) | Lav |

Tre ting kan vente:
- forrang (hører til profil E, regellaget),
- skjønnsmarkør på «kan»,
- eksport til RDF/PROV-O/SHACL (§7, trengs før RC1, men ikke før punkt 1–3).

**Anbefalt rekkefølge:**
1. Punkt 1–3 og #335 i én migrasjon («utsagnsstatus»).
2. Oppgave/funksjon (punkt 4).
3. Forslagskøen (#313).
4. Beviset (#315).
5. Eksport (§7).

---

## 2. Dagens modell, kort

`strukturkanter` har:
- kategori (R, K, P, M, O, A, G, I, T) og typekode,
- fra og til (virksomhet eller begrep),
- objekt,
- avgrensning (paragrafspenn og tekst),
- polaritet,
- hjemmel (`hjemmel_rettskilde_id` + `hjemmel_eid`) **eller** kilde utenfor korpus (tekst, lenke, type, dokumentasjonsgrad),
- gyldig fra og til,
- `status` (`foreslatt_av_ai` | `validert`), `oppdagelses_kilde`,
- kommentar, opprettet og endret av/tid,
- egenskaper for K: `delegerbar`, `grunnlag`, `normform`, `undertype`,
- egenskap for P: `modalitet`.

`rettskilde_noder` er allerede versjonert: `entitetsstatus`, `versjon` og `erstatter_node_id`, med én gjeldende node per eId.

**Mot profilene i standardforslaget:**

| Profil | Dekning i dag |
|---|---|
| A: kilde og tekst | God: ELI, egen eId, nodeversjoner. Mangler: versjonsdato i AKN-uttrykket og en eId etter AKN-navnekonvensjonen (mappingnotatet §2) |
| B: institusjon og struktur | God: nodetypene, organtilhørighet, område og ordning. Mangler: oppgave/funksjon og stilling (stilling kan uttrykkes som rolle avgrenset til ett organ, #355) |
| C: kompetanse | God: familier, undertype, normform, motpart og avgrensning. Skillet mellom kompetanseregel, disposisjon og resultat holder (K delegering, R har_delegert_til). Mangler: kunnskapsstatus, begrunnelse og versjonert hjemmel |
| D: normative posisjoner | Delvis: P med modalitet. Mangler: kravsrett og begunstiget som egne roller, og et disability/immunity-skille ved eksport |
| E: regelstruktur | Regellaget (docs/01) finnes som spesifikasjon og delvis som kode. Utenfor RC1 |

---

## 3. Punkt 1: Kunnskapsstatus og godkjenning

**Problem:** `status` blander to ting. Den sier hvem som har godkjent utsagnet (KI eller menneske), men ikke hvordan det er kommet fram (tekst, tolkning eller slutning). Metodenotatet §5 krever det siste.

**Forslag:**
- **Ny kolonne `kunnskapsstatus`:** `tekstfunn` | `normalisering` | `fortolkning` | `avledet` | `eksternt_dokumentert` | `uavklart` | `omstridt` | `foreldet`. Null betyr «ikke vurdert».
- **`status` blir godkjenning**, med verdiene `maskinelt_foreslatt` | `manuelt_foreslatt` | `faglig_vurdert` | `godkjent` | `avvist` (standardforslaget §20.2). Gamle verdier mappes slik: `foreslatt_av_ai` → `maskinelt_foreslatt`, `validert` → `godkjent`.
- **CHECK-regler:**
  - `eksternt_dokumentert` krever kilde utenfor korpus.
  - `tekstfunn` og `normalisering` krever hjemmel i korpus.
  - `avledet` krever minst én premiss (punkt 2).

**Migrasjon:** Eksisterende rader får **ikke** en gjettet kunnskapsstatus. De blir stående med null og vises som «ikke vurdert». Fasitkonverteringen setter statusen der regelen er sikker: tekstfunn ved uttrykkelig ordlyd, normalisering der en dokumentert regel er brukt. Resten settes i juristrunder.

**#335 slås inn her.** Delegeringsadgangen (`uttrykkelig_tillatt` | `uttrykkelig_forbudt` | `ikke_regulert`) og slutningen («trolig delegerbar») er det samme prinsippet anvendt på ett felt. Det er bedre å bygge dem sammen enn i to migrasjoner.

**Mapping:** PROV-O for opphav og godkjenning. Kunnskapsstatus har ingen direkte motpart i standardene (mappingnotatet §4: en norsk utvidelse).

## 4. Punkt 2: Analysen som eget utsagn med historikk

**Problem:** En kant har én verdi. Det går ikke an å ha to konkurrerende tolkninger av samme tekst (*omstridt*), eller å ta vare på en revidert analyse sammen med den gamle (*foreldet*). Begrunnelsen står i fritekst i `kommentar`.

**Forslag:** Kantens `Id` *er* analyse-id-en (standardforslaget §13.1). Ny tabell er ikke nødvendig.
- **`entitetsstatus`** (`gjeldende` | `foreldet`) og **`erstatter_kant_id`**, slik `rettskilde_noder` allerede gjør. En revisjon gir en ny rad, og den gamle blir foreldet.
- **`begrunnelse`** (tekst) og **`tolkningsargumenter`** (lukket liste: ordlyd, kontekst/system, formål, forarbeider, praksis, reelle hensyn, antitetisk/analogisk). Kreves når kunnskapsstatus er `fortolkning`.
- **Ny tabell `strukturkant_premisser`** (kant_id, premiss_kant_id). Den kreves for `avledet` og gjør at en slutning kan spores tilbake til premissene.
- **Konkurrerende tolkninger:** identitetsregelen som i dag hindrer duplikater (eId + kategori + type + fra/til), slås av når kunnskapsstatus er `omstridt`. Da kan to rader stå side om side.
- **`profilversjon`** på raden. Korpusversjonen ligger allerede i hjemmelens nodeversjon (punkt 3).

**Avledede svar som beregnes** (ankeinstans via lagdømme, plikter i begge retninger) lagres ikke. API-et returnerer dem med status `avledet` og premissenes id-er. Det følger prinsippet fra #345 om ikke å lagre det samme dobbelt.

**Mapping:** PROV-O `wasDerivedFrom` (premisser) og `wasRevisionOf` (erstatter).

## 5. Punkt 3: Versjonert hjemmel

**Problem:** `hjemmel_eid` peker på bestemmelsen, ikke på versjonen. Når teksten endres ved resynk, mister analysen grunnlaget sitt uten at det synes.

**Forslag:**
- **Ny kolonne `hjemmel_node_id`:** fremmednøkkel til den versjonerte raden i `rettskilde_noder`, satt når kanten opprettes. eId-en beholdes som stabil, menneskelesbar adresse. Den tilsvarer `wId` i Akoma Ntoso (mappingnotatet §2).
- **Resynk:** når en node får ny versjon, blir alle kanter som peker på den gamle versjonen, flagget «hjemmel endret, vurder på nytt». De beholder kunnskapsstatus og godkjenning. Ingenting endres automatisk; flagget sender dem til faglig vurdering.
- **Kategorier bundet til en lov får gyldighetstid.** Det gjelder `fvl_kategori` (forvaltningsloven 1967 § 2 og ny lov § 7) og delegeringsadgangen (ny fvl. §§ 22–24). Typekonfigurasjonen får `gjelder_fra` og `gjelder_til` for kategoridefinisjonen.

**Mapping:** ELI `LegalExpression` / `is_realized_by`, og AKN-uttrykk med `@dato`.

## 6. Punkt 4 og 5: Oppgave/funksjon og plikt ≠ kravsrett

### 6.1 Oppgave/funksjon (ny kategori F)

**Problem:** «Skal samordne», «sørge for», «forvalte», «føre oversikt» og «være sekretariat» blir i dag presset inn som K (samordning), P (bistand) eller R (`sekretariat_for`, `radgir`). Standardforslaget §7.8 sier at en oppgave verken er kompetanse eller plikt overfor en motpart, og at den ikke gir instruksjonsmyndighet.

**Forslag:** Ny kategori **F: oppgave/funksjon**.
- Felt: `fra` (aktøren), `objekt` (oppgaven), valgfri `til` (område, klasse eller aktør oppgaven gjelder), `modalitet` (som P).
- Et lite sett typekoder: `forvaltning`, `samordning`, `sorge_for`, `oversikt`, `bistand`, `sekretariat`, `radgivning`.
- **Regel:** F gir aldri K eller P i seg selv. Står det i teksten at oppgaven også gir kompetanse eller plikt, er det egne rader.

**Det må avgjøres før bygging:**
- Hva skjer med dagens K `samordning`, R `sekretariat_for`, R `radgir` og P `bistand`? Er de egentlig F? Anbefaling: en juristrunde på kandidatene, ikke automatisk flytting.
- Forholdet til regellaget: oppgaven er «hva», plikten er «skal». F + modalitet `skal` dekker det enkle tilfellet. Vilkår og frister hører til regellaget.

**Mapping:** LKIF `Function` (role.owl), «nært beslektet». Ingen motpart i UFO-L eller LegalRuleML. Det er en norsk utvidelse, med begrunnelse i forvaltningsretten.

### 6.2 Plikt er ikke kravsrett

**Problem:** P-kanten har en «motpart». Den kan leses som om motparten har en håndhevbar kravsrett (Hohfelds korrelativ), og det sier teksten sjelden.

**Forslag:**
- **Dokumenter semantikken til P:** «pliktbæreren skal gjøre X overfor eller til motparten». Det sier ingenting om kravsrett, begunstiget eller hvem som kan håndheve plikten.
- **Nytt felt `kravsrett`:** `etablert` | `ikke_etablert`, null = ikke vurdert. `etablert` krever kunnskapsstatus `tekstfunn` eller `fortolkning` med kilde.
- **Begunstiget og kravshaver** som egne roller hører til regellaget (rettsfølge), som besluttet i #353. Strukturlaget får bare flagget.

**Mapping:** P tilsvarer UFO-L `Duty` («nært beslektet»). Den tilsvarer `Right-Duty Relator` bare når `kravsrett = etablert`. I LegalRuleML er `Obligation` med `Bearer` lik pliktbæreren og `AuxiliaryParty` lik motparten.

## 7. Eksport: RDF, PROV-O og SHACL

Standardforslaget §2.7 og §5.6 skiller semantikk (OWL), validering (SHACL) og opphav (PROV-O). Regel-IDE trenger ikke bytte lagring. Postgres og CHECK-reglene er det interne. Profilen trenger derimot en **eksport** som andre implementasjoner kan validere mot.

**Forslag (etter punkt 1–3):**
- **RDF-eksport per rettskilde eller utvalg:** én node per aktør og én reifisert node per modellutsagn (kant-id = analyse-id), med kunnskapsstatus, godkjenning, hjemmel (ELI-uttrykk og eId), PROV-O (`wasAttributedTo`, `generatedAtTime`, `wasDerivedFrom`, `wasRevisionOf`), og mappingene som informative `skos:closeMatch` / `broadMatch` / `narrowMatch`. Aldri `owl:equivalentClass` (standardforslaget §5.7).
- **SHACL-former for profil A–C** som speiler CHECK-reglene: kategori og felt, kilde-krav, kunnskapsstatus og premisser. SHACL-formene blir normative i profilen. Regel-IDE består dem; det er konformitetstesten (standardforslaget §15–16).
- **AKN-eksport:** dagens `AknXmlSkriver` får `@dato` i uttrykks-ID-en, og eId etter navnekonvensjonen ved siden av vår (mappingnotatet §2, §5).

Dette er det som skiller «profil» fra «implementasjon». Spesifikasjonen og SHACL-formene er normative. Regel-IDE er én implementasjon som består dem.

## 8. Fasit, testkorpus og beviset

- **Fasiten** (`data/fasit/strukturmodell`, 1 802 utsagn) er utviklingsdata. Den er laget og kontrollert med de samme metodene som evalueres (metodenotatet §10).
- **Testkorpus for profilen:** et låst utvalg, for eksempel 150–200 utsagn spredt over familiene og de vanskelige tilfellene i standardforslaget §17.1. Det annoteres av minst to menneskelige jurister uten å se systemets forslag. Det krever en beslutning om hvem og hvordan (metodenotatet §12, spørsmål 2).
- **#313 (forslagskø) og #315 (bevis)** bør vente til punkt 1–3 er bygget. Da kan forslagene lastes inn med kunnskapsstatus og versjonert hjemmel fra starten, i stedet for å måtte rettes etterpå.

## 9. Hva som ikke endres

- K, P og R holdes fra hverandre, og det samme gjelder kompetansefamiliene, undertype i stedet for nye typer, «ordning», og «disposisjon først». Alt dette er bekreftet av standardforslaget og mappingnotatet.
- Kanttabellen er det reifiserte utsagnet. Det lages ingen parallell utsagnsmodell.
- «Ikke gjett» og synlige hull.
- Juristrundene (CLAUDE.md §23) er kvalitetssikring i utviklingen, ikke uavhengig kontroll.

## 10. Beslutninger som trengs

1. **Punkt 1–3 og #335 i én migrasjon** («utsagnsstatus»)?
2. **Oppgave/funksjon som ny kategori F**, og en juristrunde på dagens kandidater (K samordning, R sekretariat_for/radgir, P bistand) før noe flyttes?
3. **Rekkefølgen:** utsagnsstatus → F → #313 → #315 → eksport. Eller eksport tidligere?
4. **Testkorpuset:** hvem annoterer, hvor stort skal det være, og når?
