# Varig elementidentitet og punktbokstaver (undersøkelse for #495 og #498)

*Status: undersøkelse, 2026-11-25. Ingenting er bygget. Prosjekteieren ga klarsignal til undersøkelse, ikke bygging. Notatet endrer ikke
docs/profil/04; forslaget til ny tekst står i §4.1 og venter på prosjekteieren.*

**Kortversjon.**
- **Vår eId er en intern adresse i én lagret versjon, ikke en varig identitet.** Den bygges på nytt i hver versjon av §-nummer og ledd- og
  punktindeks. Settes et punkt inn, får alle punktene etter det ny eId, og den gamle eId-en går til et annet element. Det er målt i korpuset:
  45 noder i 6 av 46 verk med ny konsolidering, og 2 981 noder i 190 verk der bare parseren var endret. Påstanden i 04 §2 om at vår eId har AKN
  sin `wId`-rolle, er feil. Den er heller ikke en AKN-`eId`, fordi den bygges av løpenummer og ikke av det trykte nummeret.
- **Ingenting følger et element over versjoner i dag.** Node-ID-en er ny i hver versjon. Taggene flyttes etter sitatet, og noen koblinger flagges,
  men ingen kobling flyttes til et element som har fått ny eId.
- **Lovdata har ingen varig ID** i HTML-en. Lovdatas egne `id`-er er også posisjoner og flytter seg på samme måte. Endringslovene har derimot
  maskinlesbare endringsinstrukser (`data-move-part` m.fl.) som kan brukes som bevis når et element skal følges.
- **#498 er data fra før #361, ikke en ny feil.** Kilde-HTML-en er riktig. Den delte basen ble sist synkronisert 3. september 2026, før #361 ble
  flettet 15. oktober. Dagens parser gir `a6/ledd-1/avslutning`. Kjøres dagens parser på den lagrede HTML-en, får 582 dokumenter 2 185 nye
  avslutningsnoder. To ting mangler også i dagens parser: punktbokstavene (de står i kilden, men parseren lagrer løpenummeret), og om lag 1 700
  underledd uten liste foran, som fortsatt limes inn i leddteksten (GDPR art. 8 nr. 1 annet ledd, funnet i juristrunden).
- **Forslag:** rett 04 §2 og docs/08 §1.2.1 nå (tekst i §4.1). Lag en varig elementidentitet som egen byggesak (alternativ B), med AKN-`wId`
  avledet av den. Lagre punktbokstaven i `nummer`, ikke i eId-en. Gi underledd en egen node (`{ledd}/underledd-N`).

---

## 1. Metode og kilder

Alt er lest eller målt 2026-11-25 i worktreen for grenen `495-498-undersokelse` (fra master 97cf396). Mot den delte basen (`regelide` i
`regel-ide-postgres-1`) er det bare kjørt SELECT. API-et er ikke startet.

| Måling | Slik ble den gjort |
|---|---|
| M1 Omnummerering mellom lagrede versjoner | For hvert par i `erstatter`-kjeden (1 118 par): ledd, punkt og avslutninger med mer enn 60 tegn og en `tekst_hash` som er unik i begge versjonene. Teksten er den samme, men eId-en er en annen. Delt etter om `konsolidert_dato` er endret (lovendring) eller ikke (bare ny parser) |
| M2 Dagens parser på lagret HTML | `LovdataKonverterer.Konverter` fra master kjørt på `rettskilder.innhold` for alle 6 656 gjeldende Lovdata-kilder med lagret HTML. Nodene sammenlignet med de lagrede på eId, `tekst_hash` og overskrift. Ingen feilet i parseren |
| M3 Etiketter i kilden | For hvert punkt fra M2: `data-name` på `<li>` og `type` på lista. For hvert ledd: `data-numerator` på `numberedLegalP` |
| M4 Attributter i kilde-HTML-en | Alle attributtnavn i `<main>` i 300 tilfeldige kilder, og deretter et fullt søk etter endringsinstruksene |
| M5 Koblinger | Alle tabeller med en kolonne som peker på eId eller node, telt og matchet mot endringene i M2. Fasitfilene i `data/fasit/` er matchet på samme måte |
| M6 Underledd uten liste | Nestet `<article class="legalP">` i et `numberedLegalP` uten `</ol>`/`</ul>` foran, talt med regulære uttrykk på lagret HTML. Juristen talte det samme med en litt annen avgrensning |

Skriptene ligger ikke i repoet; de er engangsmålinger.

---

## 2. #495: varig elementidentitet

### 2.1 Hva skjer med eId og node-ID ved ny versjon eller resynk?

**Slik bygges eId.** `LovdataIdentifikatorer.ParagrafEid` gir `{ELI}/{paragrafnummer}`, der paragrafnummeret er siste ledd i
`data-lovdata-URL` (`LovdataHtmlParser.cs:1085–1087`). `LeddEid` og `PunktEid` legger til `ledd-N` og `punkt-N`, der N er et løpenummer
som telles mens parseren går gjennom dokumentet (`LovdataIdentifikatorer.cs:60–63`, tellerne i `LovdataHtmlParser.cs:1118–1162` og
`1355–1364`). Kolliderer to eId-er, får den siste `-duplikat-N` etter rekkefølgen i dokumentet (`GjørEidUnik`, `LovdataHtmlParser.cs:169–175`).

**Slik lagres en ny versjon.** En ny konsolidert versjon er en ny `rettskilder`-rad med et helt nytt sett noder. Hver node får en ny `Guid`
(`RettskildeImportTjeneste.cs:252`). `erstatter_node_id` brukes bare for håndbøker: 0 av 926 940 noder har den satt.

**Ekte eksempel (lovendring).** Tollavgiftsforskriften § 4-1-1 første ledd, versjon 2 (konsolidert 2026-03-01) mot versjon 3
(2026-09-01). Avtalen med Kosovo er satt inn som punkt 21:

| eId | Versjon 2 | Versjon 3 |
|---|---|---|
| `…/§4-1-1/ledd-1/punkt-20` | EFTA–Jordan | EFTA–Jordan |
| `…/§4-1-1/ledd-1/punkt-21` | EFTA–Libanon | **EFTA–Kosovo** |
| `…/§4-1-1/ledd-1/punkt-22` | EFTA–Marokko | EFTA–Libanon |
| `…/§4-1-1/ledd-1/punkt-28` | EFTA–PLO | EFTA–Peru |

Alle punktene fra 21 og ut (18 av 38) har fått ny eId, og hver gammel eId peker nå på et annet punkt. Lovdatas egen `id` gjør det samme:
`kapittel-3-paragraf-2-nummer-1-punkt-21` var Libanon og er nå Kosovo. Samme endring finnes i vareførselsforskriften § 5-11-1.

Et annet eksempel er vareførselsforskriften § 5-11-5, der versjon 3 har et nytt ledd 2 (leverandørerklæringens ordlyd), og de gamle leddene 2–5
er blitt 3–6. Der er både teksten og parseren endret mellom versjonene, og kilde-HTML-en for versjon 2 er ikke lagret. Årsaken kan derfor ikke
skilles.

**Omfang (M1).**

| Årsak | Noder med ny eId | Verk | Gammel eId peker nå på et annet element |
|---|---|---|---|
| Ny konsolidering (lovendring) | 45 | 6 av 46 verk med ny konsolidering | 43 |
| Samme konsolidering, bare ny parser | 2 981 | 190 | 2 421 |

Bare 46 av de 1 118 versjonsparene har en ny konsolideringsdato, fordi den delte basen bare er synkronisert i august og september 2026. Av dem
har 6 omnummerering. Tallet vil vokse for hver resynk.

**Parserendringer flytter eId-er.** De 2 981 nodene i tabellen har fått ny eId uten at lovteksten er endret. To eksempler:
- Forskrift 2015-05-06-455, artikkel 78: «2. Eventuelle ytterligere tiltak …» var `a78/ledd-2` og er `a78/ledd-3`, fordi en endringsmarkør
  (`►M3`) nå blir et eget ledd.
- Forskrift 2005-12-16-1495 (M2): når dagens parser kjøres, forsvinner 35 punkt-eId-er i vedlegg 2, og `kap-vedlegg2/ledd-5/punkt-1` til `4`
  får annen tekst. Årsaken er #397, der avsnitt med liste i vedlegg blir ledd. docs/08 §3.1.3 sier at ingen eksisterende eId forsvant i de fem
  vedtakene som ble kontrollert. I hele korpuset forsvinner 184 punkt-eId-er i 10 forskrifter (M2).

**Løpenummeret er ikke det trykte nummeret (M3).** Uten noen endring siteres ikke alltid riktig element:
- 1 617 ledd i 144 verk har et trykt nummer (`data-numerator`) som er ulikt løpenummeret. Skatteloven § 10-5: leddet som er trykt «(6)», har
  eId `ledd-7`.
- 3 272 punkt i 320 verk har en trykt bokstav eller et tall som ikke svarer til løpenummeret. Lov 1999-06-18-40 § 7 annet ledd: det første
  punktet er trykt «f.», og eId-en er `punkt-1`.
- 21 punkt i 6 verk er innsatte punkter med nummer som «11a.» og «23b.», og de har løpenummer som eId.

### 2.2 Finnes det noe som følger et element over versjoner?

Nei. Det som finnes, er dette:

| Mekanisme | Hva den gjør | Følger den elementet? |
|---|---|---|
| Tagg-flytting (`RelokerTaggerAsync`, `RettskildeImportTjeneste.cs:530–571`) | Samme eId og samme hash: flytt. Ellers: søk etter sitatet. Ett treff: flytt dit. Ingen eller flere treff: «krever gjennomgang» | Ja, men bare for tagger, og bare når sitatet er unikt |
| «Hjemmel endret» (`HjemmelEndretFlagg.cs`, #367, #385, #429) | Sammenligner deltreet under samme eId i gammel og ny versjon. Strukturkanter, navneformbindinger og funn flagges når det er ulikt | Nei. Den oppdager at innholdet under eId-en er endret, men ser ikke om elementet bare er flyttet. Kanten blir stående på den gamle versjonen |
| Flyttbare bindinger (#384, `GeneriskNavneformTjeneste.cs:608`) | En binding kan brukes i ny versjon når paragrafspennet har de samme eId-ene | Nei, det sammenlignes på eId |
| `ix_rettskilde_noder_eid_hash` (docs/08 §2.1) | Indeks for sammenligning på eId og hash | Nei |

docs/08 §2.1 sier at taggene overlever en ny versjon gjennom eId og hash. Det gjelder bare når eId-en er den samme. Ellers er det sitatsøket som
redder taggen.

### 2.3 Har Lovdatas kilde noe som kan tjene som varig ID?

Nei. Attributtene i `<main>` (M4, 300 kilder) er:
- `id` (alle): en sti av posisjoner, `kapittel-10-kapittel-2-paragraf-2-nummer-1-punkt-1`. Den flytter seg med elementet (tollavgiftsforskriften
  over). docs/08 §1.2.1 sier det samme: `kilde_id` kan endre seg.
- `data-lovdata-URL` og `data-name` på paragrafen (`NL/lov/2018-06-15-38/gdpr/a6`): det trykte nummeret, altså en adresse.
- `data-name` og `data-li-identifier` på `<li>` (`a.`, `b)`), `type` på lista og `data-numerator` på ledd: det trykte nummeret eller bokstaven.
  Det er nyttig for #498, men det er også en adresse.

**Det som kan brukes:** endringslovene har maskinlesbare endringsinstrukser med Lovdatas egen adressesyntaks, for eksempel
`data-move-part="lov/2005-06-17-67/§3-3/ledd/4;;lov/2005-06-17-67/§3-3/ledd/3"` (ledd 4 blir ledd 3) og
`data-change-part="lov/2002-06-21-45/§9d/ledd/1/bokstav/d"`. I de lagrede kildene finnes 48 `data-move-part`, 129 `data-add-new-part`,
413 `data-change-part` og 60 `data-repeal-part`, i 21–56 dokumenter. Adressene bruker trykt bokstav (`bokstav/d`) og setning, ikke løpenummer.
Parseren leser dem ikke i dag. De kan brukes som bevis når et element skal følges fra én versjon til den neste, men de dekker bare
endringslovene som er i korpuset (Lovtidend er importert selektivt, docs/08 §3.1.2). Om alle omnummereringer har en slik instruks, er ikke
kontrollert.

### 2.4 Hvilke tabeller lagrer koblinger mot eId, og hva skjer ved omnummerering?

Telt i den delte basen (M5). «Versjon» betyr at raden også har en `rettskilde_id` eller `node_id` som peker på en bestemt versjon.

| Tabell og kolonne | Rader | Hvordan den peker | Ved omnummerering |
|---|---|---|---|
| `rettskilde_referanser.til_eid` | 106 219 (fra gjeldende versjoner) | Versjon + eId, bygget på nytt ved hver import fra Lovdatas lenker | Riktig når kildedokumentet importeres på nytt. En lenke fra et dokument som ikke er importert på nytt, kan peke på feil element i måldokumentet |
| `rettskilde_hjemler.hjemmel_eid` og `hjemmelsstatus.hjemmel_eid` | 25 011 + 25 011 | Bygget fra hodet ved hver import | Som over |
| `rettskilde_endringer.endring_eid` | 5 318 | Bygget fra hodet ved hver import | Som over |
| `strukturkanter.hjemmel_eid` + `hjemmel_node_id` | 2 504 | Versjon (node) | Blir stående på gammel versjon. Flagges «hjemmel endret» fordi innholdet under eId-en er annerledes. Ny peker settes for hånd |
| `navneform_bindinger.hjemmel_eid` + `hjemmel_node_id` | 100 | Versjon (node) | Som strukturkanter |
| `funn_koblinger.eid` + `node_id` | 1 465 | Versjon (node) | Som strukturkanter («kilde endret», #429) |
| `tekst_tagger.node_eid` | 1 143 gjeldende | Versjon + eId + sitat | Flyttes etter sitatet (§2.2). Følger elementet når sitatet er unikt |
| `begrepsforekomster.node_eid` | 6 017 | Versjon + eId | Ikke flyttet og ikke flagget. 1 554 peker allerede på en erstattet versjon |
| `navnekandidater.node_eid` | 1 269 | Versjon + eId | Ikke flyttet og ikke flagget |
| `virksomhet_kandidater.node_eid` | 1 169 | Versjon + eId | Ikke flyttet; 315 peker på en erstattet versjon |
| `handling_regelverksreferanser.til_eid` | 1 982 | Versjon + eId | Ikke flyttet; 243 peker på en erstattet versjon |
| `tjeneste_regelverksreferanser.til_eid` | 14 | Versjon + eId | Ikke flyttet; 2 peker på en erstattet versjon |
| `strukturkant_bekreftelser.hjemmel_eid` | 459 | Versjon + eId | Historikk, ikke flyttet |
| `aktortype_utsagn.hjemmel_eid` | 48 | Versjon + eId | Ikke flyttet |
| `rettskilde_disposisjonstyper.node_eid` | 127 | Versjon + eId | Ikke flyttet |
| `kildefeil.rettskilde_eid` | 139 | Versjon + eId | Ikke flyttet |
| `begreper.lovreferanse_eid` | 187 | **Bare eId**, slått opp i gjeldende versjon (`AktorsideTjeneste.cs:311–315`, `GeneriskeStandardregler.cs:337–341`, `KlassemedlemskapTjeneste.cs:172`) | **Peker uten varsel på det nye elementet med samme eId.** Det er den farligste formen |
| `vilkar.juridisk_grunnlag` (JSON `EId`) | 10 | Bare eId | Som `begreper` (oppslaget er ikke lest i koden) |
| `kodelister.juridisk_grunnlag_eid` | 1 | Bare eId | Som `begreper` (ikke lest i koden) |
| Fasitfilene (`data/fasit/`) | 3 254 ulike eId-er | eId + sitat | Sitatet kan kontrolleres, men ingenting flytter eId-en |

For tabellene merket «Versjon + eId» er det ikke kontrollert per tabell om visningen leser eId-en mot den gamle versjonen (riktig, men
utdatert) eller mot den gjeldende (feil element etter en omnummerering).

### 2.5 Hvordan bør 04 §2 rettes?

04 §2 sier: «Vår regel om at eId aldri endres (docs/08) gir altså vår eId samme rolle som AKN sin `wId`». Målingene over viser at det ikke
stemmer.

**Hva AKN sier** (AKN-NC 1.0 §5.3, §5.4.3, §5.5.2, §5.5.3, kontrollert av juristen hos OASIS):
- `eId` er en adresse i ett uttrykk (én versjon). Nummeret tas fra det trykte nummeret (`<num>`), og `eId` oppdateres når elementet får ny plass
  i et nytt uttrykk.
- `wId` har samme syntaks, men er den `eId`-en elementet har i master-uttrykket, eller i et tenkt grunnuttrykk («Ur-Expression»). Den endres
  aldri.
- Mangler `wId`, regnes dokumentet som master-uttrykket, og `wId` er lik `eId`. Å utelate `wId` er derfor ikke nøytralt: eksporterer vi to
  versjoner av tollavgiftsforskriften uten `wId`, sier eksporten at Kosovo (punkt 21 i versjon 3) er samme element som Libanon (punkt 21 i
  versjon 2).
- `GUID` er et eget, fritt attributt.

**Vår eId er ingen av delene.** Den er ikke `wId`, fordi den flytter seg. Den er heller ikke en AKN-`eId`, fordi den bygges av løpenummeret, ikke
av det trykte nummeret, og fordi den endres når bare parseren endres. Den er en intern adresse i én lagret versjon. docs/08 §1.2.1 sier at `eid`
er en «permanent identitet» som «endres aldri etter opprettelse». Det holder bare i den svake formen (en lagret eId skrives ikke om), og
§1.2.1 bør rettes sammen med 04.

| Alternativ | Hva det er | For | Mot |
|---|---|---|---|
| **A. Rett påstanden** | 04 §2 og docs/08 §1.2.1 sier at vår eId er en intern adresse i én versjon, og at vi ikke har noen `wId`. Eksporten skriver aldri flere versjoner av samme verk uten `wId`, og en eksport av én versjon merkes som ikke-konform på `wId` | Sant, billig, ingen kode. Hindrer at noen bygger GUID eller `wId` av eId (09 §4.1) | Hullet står åpent: koblinger uten versjon (`begreper`) peker fortsatt feil etter en omnummerering |
| **B. A + varig elementidentitet som egen byggesak** | Ny kolonne med en varig intern ID per node (GUID-rollen i AKN og RUML-ankeret). AKN-`wId` avledes av den: `eId`-en elementet hadde i et valgt og låst grunnuttrykk. Bevisrekkefølgen ved ny versjon står under | Gir `wId` et innhold. Bygger på det taggene gjør allerede. Riktig for RUML (09 §4.1) og HTML4EU (10 §5) | Byggekostnad (§6). En maskinell kobling er en påstand om identitet og må kunne vurderes av et menneske når den er usikker. Eldre versjoner må kobles i ettertid |
| C. Gjør eId-en selv varig | Et element beholder eId-en sin når det omnummereres; nye elementer får ny eId | En nøkkel i stedet for to | eId-en slutter å svare til det trykte nummeret. Parseren kan ikke bygge eId av kilden alene. Bryter AKN-NC §5.4.3 |
| D. Vent på offisiell seksjons-ELI eller ELI-underinndelinger versjon 3 | | Ingen kostnad nå | Begge er adresser, ikke identitet (10 §5) |

**Bevisrekkefølgen i B** (rettet etter juristrunden):
1. Lovdatas endringsinstruks (`data-move-part` m.fl.) der den finnes for elementet. Den er lovgivers egen angivelse og går foran resten.
2. Samme eId og samme hash, men bare når hashen er unik i paragrafen og teksten ikke er en standardtekst som «(Opphevet)».
3. Unik hash et annet sted i versjonen.
4. Sitat (som tagg-flyttingen i dag).
5. Resten, og alt der et trinn strider mot en endringsinstruks: ny ID og gjennomgang hos et menneske.

Grunnuttrykket for `wId` må velges og dokumenteres. Den delte basen har ikke originalvedtakene, bare konsolideringer fra august og september
2026; forslaget er første lagrede versjon. Innsatte elementer må få en `wId` som ikke kolliderer (AKN sitt eget eksempel i §5.5.3.4 kolliderer).
Koblinger som bare lagrer eId (`begreper.lovreferanse_eid`, `vilkar`, `kodelister`), flyttes til den varige ID-en og flagges «hjemmel endret»
når innholdet er endret, slik strukturkantene gjør.

**Anbefaling:** A nå, B som egen byggesak. Teksten for A står i §4.1.

---

## 3. #498: underledd og punktbokstaver i GDPR-teksten

### 3.1 Hvor mange noder har samme feil?

Det er feilen fra #361. Målt direkte med dagens parser på den lagrede HTML-en (M2), ikke med en heuristikk:

| Endring hvis den delte basen konverteres på nytt | Antall |
|---|---|
| Nye avslutningsnoder | 2 185 i 582 dokumenter (371 noder i 128 lover, 1 814 i 454 forskrifter) |
| Ledd og punkt med ny tekst (innledningen er skilt fra avslutningen) | 1 301 ledd og 996 punkt |
| Avslutningsnoder i den delte basen i dag | 24 i 14 dokumenter |

Feilen gjelder altså hele korpuset, ikke bare EØS-tekst. Formen i GDPR art. 6 nr. 1, der underleddet er et eget `<article class="legalP">` inne
i et nummerert ledd etter lista, gir 239 av avslutningsnodene, i 83 dokumenter (21 lover, 62 forskrifter). Resten er `p.leddfortsettelse` (890),
tekst etter en liste i et punkt (727) og andre former.

**Underledd uten liste foran (funnet i juristrunden).** Et nestet `legalP` i et nummerert ledd som *ikke* står etter en liste, får ingen node,
heller ikke med dagens parser. Det limes inn i leddteksten. Eksempel: GDPR art. 8 nr. 1 annet ledd («For disse formål kan medlemsstatene ved lov
fastsette en lavere aldersgrense, forutsatt at den ikke er lavere enn 13 år.») står i `a8/ledd-1` også når dagens parser kjøres. Omfang, talt to
ganger på den lagrede HTML-en med litt ulike avgrensninger: 1 700 i 166 dokumenter (modellereren) og 1 793 i 169 dokumenter (juristen), av 712
dokumenter med nummererte ledd. Formen finnes også i norsk lov: 1 043 står i nummererte ledd som begynner med «1.», blant dem eldre norske lover og
forskrifter, og 149 i ledd som begynner med «(1)», som ferieloven § 5. Ferieloven siterer selv «nr. 4 annet ledd».

M2 viser i tillegg endringer fra #397 (vedlegg): 757 nye ledd og 6 118 nye punkt i 91 dokumenter, og de 184 punkt-eId-ene som forsvinner (§2.1).

### 3.2 Årsak: parseren, ikke kilden

**Kilden er riktig.** Artikkel 6 nr. 1 i den lagrede HTML-en (personopplysningsloven, versjon 7):
- `<article class="numberedLegalP" data-numerator="1">1. Behandlingen er bare lovlig … oppfylt:`
- `<ol class="defaultList" type="a">` med `<li data-name="a." value="1">` … `<li data-name="f." value="6">`
- etter lista: `<article class="legalP" id="…-nummer-1-ledd-1">Nr. 1 bokstav f) får ikke anvendelse …</article>`

Bokstaven står i `data-name` og `type="a"`. Underleddet er et eget element med egen `id`. Det samme gjelder art. 8 nr. 1 annet ledd.

**Parseren på importtidspunktet limte underleddet inn.** Versjon 7 ble laget 2026-09-02. #361 ble flettet 2026-10-15 (5bd93b8). Siste resynk
var 2026-09-03, og ingen kilde er konvertert på nytt etter det. Den lagrede `a6/ledd-1` har derfor teksten «… minst ett av følgende vilkår er
oppfylt: Nr. 1 bokstav f) får ikke anvendelse …».

**Dagens parser skiller underleddet ut når det står etter en liste:** `a6/ledd-1` er bare innledningen, og `a6/ledd-1/avslutning` er «Nr. 1
bokstav f) …». Det samme gjelder `a6/ledd-3/avslutning`. Underledd uten liste foran (art. 8) limes fortsatt inn.

**Bokstaven mangler fortsatt.** Parseren setter `Nummer = punktIndeks.ToString()` (`LovdataHtmlParser.cs:1406` og `1425`) og leser aldri
`data-name`. Det gjelder alle punkter, også i dagens kode. På samme måte får ledd løpenummeret, ikke `data-numerator`.

**En følgefeil som finnes i dag (funnet i juristrunden):** `LosPresiseringAsync` (`RettskildeImportTjeneste.cs:381–406`) gjør Lovdatas presisering
i hjemmelsfeltet (`ledd/4`, `ledd/3/bokstav/a`) om til eId `ledd-4`. Lovdata bruker det trykte nummeret, vi bruker løpenummeret, så oppslaget
treffer feil ledd der de to er ulike (1 617 ledd, §2.1). Bokstaven treffer aldri, og koden faller da tilbake til leddet, som kommentaren der sier.

### 3.3 Hvordan bør punktbokstaven og underleddet lagres?

**Punktbokstaven: i `nummer`, ikke i eId-en.**
- `nummer` = den trykte etiketten fra `<li data-name>` uten tegnsetting: `a`, `b`, `11a`. For ledd: `data-numerator` der det finnes.
- Mangler etiketten (7 348 punkt har ingen `data-name`, og 18 285 står ikke i en `<li>`), beholdes løpenummeret. Et eget felt sier om nummeret er
  trykt eller et løpenummer. Det krever en ny kolonne.
- AKN-`<num>` får den trykte formen («f)», «a.»). Eksporten bygger AKN-`eId` (`point_f`) og HTML4EU-ID (`pnt_f`) av etiketten uten tegnsetting
  (AKN-NC §5.4.3).
- Oppslag fra et sitat, fra Lovdatas presisering og fra endringsinstruksene går via forelder, nodetype og `nummer`, ikke via eId. Det retter
  også feilen i `LosPresiseringAsync` (§3.2).
- Sitatet som skal kunne slås opp, er slik loven selv siterer: «personvernforordningen artikkel 6 nr. 1 bokstav f» (personopplysningsloven §§ 5
  og 8), og også «artikkel 6 nr. 1 første ledd bokstav f».

**Visningen** bruker listas type og etiketten: bokstavliste «bokstav a», talliste «nr. 1», romertall under bokstav «bokstav a) i)» (290
forekomster i korpuset mot 27 for «bokstav a punkt i»), strekliste uten etikett «første strekpunkt». For nummererte ledd se uenighet 1 i §5.

**Hvorfor ikke i eId-en:** en bokstav endres også ved omnummerering (bokstav c blir d), så eId-en blir ikke varig av det. Og alle punkt-eId-er i
korpuset ville endret seg, sammen med alle koblingene til dem.

**Underleddet: egen node `{ledd-eId}/underledd-N` (N ≥ 2)** (rettet etter juristrunden). Et nestet `<article class="legalP">` i et nummerert ledd
blir en egen node med `node_type` underledd, både etter en liste (art. 6 nr. 1) og uten liste foran (art. 8 nr. 1). Første underledd er leddet
selv, så ingen punkt-eId endres. Avslutningsnoden fra #361 beholdes for `p.leddfortsettelse` og løs tekst etter en liste.
- Grunnen: et selvstendig underledd er ikke avslutningsord til lista. Lagres begge som avslutning, kan eksporten ikke skille dem uten å gjette,
  og AKN-eksporten (`wrapUp`) og HTML4EU-eksporten (`sub_2`) ville sagt ulike ting om samme tekst.
- AKN: underledd blir `<subparagraph>` (`subpara_N`), avslutning blir `wrapUp`. HTML4EU: ledd med liste blir `par_X/sub_1`, underledd N blir
  `par_X/sub_N`, avslutning blir `par_X/sub_1/wrp`.
- Det endrer #361 for denne formen: de 239 nodene som i dag får `/avslutning`, får `/underledd-2`. De finnes ennå ikke i den delte basen, så det
  er billigst å bestemme det før basen konverteres på nytt.

### 3.4 Påvirker en retting eId-er eller koblinger som allerede er lagret?

**Ny konvertering av den delte basen (#361 og #397, kode som alt er flettet).** M2 matchet mot alle koblingstabellene (M5):
- **eId-er som forsvinner:** ingen ledd- eller punkt-eId forsvinner av #361. 184 punkt-eId-er forsvinner av #397, i 10 forskrifter. Ingen
  koblinger peker på dem.
- **Koblinger til noder som får ny tekst:** 32 rader. Begrepsforekomster 13, virksomhetskandidater 7, funnkoblinger 5, tagger 3,
  strukturkanter 2, navneformbinding 1 og navnekandidat 1. Strukturkantene, bindingen og funnene flagges «hjemmel endret» eller «kilde endret».
  Taggene flyttes etter sitatet. De andre blir stående på den gamle versjonen.
- **Fasit:** 15 eId-er i fasitfilene får ny tekst (blant dem helse-og-omsorgstjenesteloven § 12-4 fjerde ledd, som #361 AK7 gjelder).
- 619 dokumenter får ny versjon.

**Underleddsnoden.** Ingen eksisterende eId endres. Leddene med et underledd uten liste foran (om lag 1 700) får kortere tekst og ny hash, så
koblinger til dem flagges eller flyttes som over. Hvor mange koblinger det gjelder, er ikke målt.

**Bokstaven i `nummer`.** Ingen eId endres. `tekst_hash` endres ikke, og «hjemmel endret» sammenligner ikke `nummer`
(`HjemmelEndretFlagg.Nodeavtrykk` har bare eId, overskrift og hash), så ingen koblinger flagges. Men AKN endres (`<num>`), og resynken lager en
ny versjon når AKN er endret. Det gjelder om lag 3 500 dokumenter (3 497 dokumenter har punkt med etikett), med et fullt nytt sett noder hver.
Det er den største kostnaden, og grunnen til at det bør tas i samme kjøring som #361 og #397.

---

## 4. Forslag

### 4.1 Ny tekst i 04 §2 (alternativ A)

Erstatter avsnittet «Funnet ved verifiseringen» og listen over den doble nøkkelen. Teksten er juristens retting, tatt inn uendret. Ikke innført;
venter på prosjekteieren.

> **Funnet ved verifiseringen (rettet etter #495, docs/35):** AKN har selv en dobbel nøkkel [AKN-NC §5.3, §5.5.2, §5.5.3]. `eId` er
> uttrykksnivå. Den tar nummeret fra `<num>` (§5.4.3) og oppdateres når elementets plass i strukturen endres i et nytt uttrykk. `wId` er
> verksnivå, endres aldri og har samme syntaks: den er `eId`-en elementet har i master-uttrykket, eller i et tenkt «Ur-Expression» når det ikke
> finnes noe master-uttrykk. Mangler `wId`, antas dokumentet å være master-uttrykket, og `wId` er lik `eId`. Et uttrykk som ikke er master, skal
> ha `wId` der den avviker fra `eId` (§5.5.2). Vår eId er ingen av delene. Den bygges i hver lagrede versjon av §-nummeret og løpenummeret til
> ledd og punkt (docs/08 §1.2), ikke av det trykte nummeret. Den endres også når bare parseren endres (docs/35 §2.1). Den er en intern adresse i
> én lagret versjon. docs/08 §1.2.1 («endres aldri etter opprettelse») holder bare i den svake formen: en lagret eId skrives ikke om. Som påstand
> om varig identitet er den feil og rettes. Vi har i dag ingen `wId`. Ved eksport skriver vi en AKN-`eId` per versjon, avledet av det trykte
> nummeret. Til vi har en varig elementidentitet (#495), skriver vi aldri flere versjoner av samme verk uten `wId`. Etter §5.5.2 ville det si at
> elementer med samme `eId` er det samme elementet. Eksport av en enkelt versjon merkes som ikke-konform på `wId`.

Listen over den doble nøkkelen får fem punkter:
- vår eId som intern adresse i én lagret versjon (løpenummer, avhengig av parseren), verken AKN-`eId` eller varig identitet,
- en varig intern element-ID (`GUID`-rollen i AKN og RUML-ankeret), som må lages (#495),
- en avledet AKN-`eId` per versjon etter navnekonvensjonen, med nummeret fra den trykte etiketten (`<num>`),
- en avledet AKN-`wId`: den AKN-`eId`-en elementet hadde i det valgte master- eller grunnuttrykket, skrevet der den avviker fra `eId`
  (AKN-NC §5.5.2). Den avledes av den varige ID-en, aldri av vår eId,
- uttrykks-ID med `@dato` og versjonskjede.

Setningen «Å endre eksisterende eId-er bryter docs/08 sin regel om at eId ikke endres, og bør ikke gjøres» kan stå, men bør få et tillegg: parseren
endrer eId-er i praksis (2 981 noder, docs/35 §2.1). docs/08 §1.2.1 og §2.1 rettes i samme omgang.

### 4.2 Byggesaker (ingen er startet)

1. **#498 a: trykt nummer og underledd.** Parseren leser `<li data-name>`, listas type og `data-numerator` inn i `nummer`, med en ny kolonne for
   «trykt eller løpenummer». Nestet `legalP` i et nummerert ledd blir `underledd-N`. `LosPresiseringAsync` slår opp via `nummer`. Visningen skriver
   «bokstav a» osv. Fixture-tester: personopplysningsloven art. 6 og 8, tollavgiftsforskriften § 4-1-1, skatteloven § 10-5, ferieloven § 5.
2. **#498 b: konverter den delte basen på nytt** med dagens parser, etter 1. Ingen ny kode (`konverter-paa-nytt` eller resynk). Gir #361, #397,
   bokstavene og underleddene i én runde. Juristen ser koblingene som flagges og de 15 fasit-eId-ene.
3. **#495: varig elementidentitet (alternativ B).** Kolonne, koblingsrunde ved ny versjon med bevisrekkefølgen i §2.5, etterkobling av de 1 118
   versjonsparene, avledet `wId` og `begreper`, `vilkar` og `kodelister` over på den varige ID-en.

---

## 5. Juristrunden

Én runde etter `.claude/agents/jurist-utfordrer.md`, kjørt som egen agent 2026-11-25. Juristen fikk fem kort (J1–J5) med forslaget til 04 §2,
anbefalingen, bokstavvalget og underleddsvalget, og de målte fakta, men ikke modellererens begrunnelser. Kortene og svarene er ikke lagt i repoet;
PR-en inneholder bare dette notatet.

- **Resultat:** 0 holder, 5 innvendinger (4 middels: J1, J2, J3, J5; 1 lav: J4), ingen til kontroll og ingen prinsipielle. Hovedretningen holdt i
  alle fem: vår eId er ikke varig, A nå og B som byggesak, C og D forkastes, bokstaven i `nummer`.
- **Modellereren godtok:** at `wId` ikke kan utelates uten følger og ikke kan være en GUID (J1, J2), at vår eId heller ikke er en AKN-`eId` og at
  docs/08 §1.2.1 skal rettes, ikke tolkes om (J1), bevisrekkefølgen med endringsinstruksen først og flagging ved flytting (J3), sitatformen,
  romertall, ny kolonne og oppslaget via `nummer` (J4), og egen underleddsnode, også uten liste foran (J5). Art. 8 nr. 1 er kontrollert av
  modellereren med dagens parser. Tallet for underledd uten liste foran er talt på nytt (1 700 mot juristens 1 793).

**Uenighet som står igjen** (bare én runde, juristen har ikke replisert):

1. **Hva et nummerert ledd heter i visningen (J4, liten).**
   - *Juristen:* et nummerert ledd vises som «sjette ledd» i norsk lov (skatteloven § 10-5 er trykt «(6)») og «nr. 1» i en EØS-rettsakt.
   - *Modellereren:* dokumentets art avgjør ikke dette heller. Ferieloven § 5 er norsk lov, er trykt «(1)» til «(6)», og siterer seg selv som
     «nr. 4 annet ledd». Eldre norske lover og forskrifter bruker «1.». Visningen må lese hvordan bestemmelsen siterer seg selv, eller la
     etiketten stå uten ord («(6)») til det er avklart.
   - *Begge er enige om* at klassen `numberedLegalP` alene ikke avgjør det, og at ingenting skal gjettes.

---

## 6. Valg for prosjekteieren

| Valg | Alternativer | Anbefaling | Pris |
|---|---|---|---|
| 1. 04 §2 | A rett påstanden · B A + byggesak · C eId varig · D vent | **B** (A nå, byggesaken etterpå) | A: tekst i 04 og docs/08. B: se byggesak 3 |
| 2. Hvor bokstaven lagres | `nummer` · eId (`punkt-a`) | **`nummer`** | `nummer`: parser, én kolonne, oppslag og visning. eId: alle punkt-eId-er og koblinger endres |
| 3. EU-underledd og «nr. … ledd» | `/avslutning` som nå · egen node `/underledd-N` | **`/underledd-N`** (juristens forslag, godtatt) | Liten parserendring nå, før den delte basen konverteres. Endrer #361 for 239 noder som ennå ikke finnes i basen, og gir om lag 1 700 underledd en node |
| 4. Når den delte basen konverteres på nytt | Nå · etter byggesak 1 | **Etter byggesak 1**, i samme kjøring | 619 dokumenter (og om lag 3 500 med bokstavene) får ny versjon én gang i stedet for to |
| 5. Visning av nummerert ledd | «sjette ledd»/«nr. 1» etter dokumentets art (juristen) · etter hvordan bestemmelsen siterer seg selv, ellers bare etiketten (modellereren) | Modellerens | Se uenighet 1 i §5 |

**Anslag for byggingen** (omfang, ikke tid):
- **Byggesak 1:** én til to PR-er.
  - `LovdataHtmlParser`: etikett, numerator og underledd.
  - Migrasjon med én kolonne.
  - `LosPresiseringAsync`.
  - Visningen av punkt og ledd.
  - 5 fixture-tester.
  - Liten til middels.
- **Byggesak 2:** ingen kode. Én kjøring og en kontroll av koblingene som flagges og de 15 fasit-eId-ene. Liten, men gir mange nye versjoner.
- **Byggesak 3:** tre PR-er. Middels til stor. Koblingsrunden er en påstand om identitet og bør juristvurderes på et utvalg.
  - (a) Migrasjon og koblingsrunden i `OpprettNyVersjonAsync`, med tester.
  - (b) Etterkobling av eksisterende versjonspar, med en gjennomgangsliste for det som ikke kan kobles entydig.
  - (c) `begreper.lovreferanse_eid`, `vilkar` og `kodelister` over på den varige ID-en, og avledet `wId` i eksporten.

---

## 7. Det som ikke er sjekket

- Om visningen leser «versjon + eId»-koblingene (§2.4) mot den gamle eller den gjeldende versjonen. Bare `begreper` er lest i koden.
- Om alle omnummereringer i Lovdata har en endringsinstruks (`data-move-part`), og hvor mange slike som finnes i hele Lovtidend.
- Kilde-HTML-en for versjonene fra før 2026-09-02, som ikke er lagret. Derfor kan årsaken i vareførselsforskriften § 5-11-5 ikke skilles.
- Hvor mye den delte basen vokser når om lag 3 500 dokumenter får en ny versjon.
- Hvor mange koblinger som peker på de om lag 1 700 leddene som får en underleddsnode.
- Om den innsatte artikkelen i AKN sitt eksempel (§5.5.3.4) er ment å ha en implisitt `wId` (juristen).
- Etiketter som «i.» er talt som bokstav; noen kan være romertall.
