# Varig elementidentitet og punktbokstaver (undersøkelse for #495 og #498)

*Status: undersøkelse, 2026-11-25. Ingenting er bygget. Prosjekteieren ga klarsignal til undersøkelse, ikke bygging. Notatet endrer ikke
docs/profil/04; forslaget til ny tekst står i §4.1 og venter på prosjekteieren.*

**Kortversjon.**
- **Vår eId er en adresse i én versjon, ikke en varig identitet.** Den bygges på nytt i hver versjon av §-nummer og ledd- og punktindeks. Settes et
  punkt inn, får alle punktene etter det ny eId, og den gamle eId-en går til et annet element. Det er målt i korpuset: 45 noder i 6 av 46 verk med
  ny konsolidering, og 2 981 noder i 190 verk der bare parseren var endret. Påstanden i 04 §2 om at vår eId har AKN sin `wId`-rolle, er feil.
- **Ingenting følger et element over versjoner i dag.** Node-ID-en er ny i hver versjon. Taggene flyttes etter sitatet, og noen koblinger flagges,
  men ingen kobling flyttes til et element som har fått ny eId.
- **Lovdata har ingen varig ID** i HTML-en. Lovdatas egne `id`-er er også posisjoner og flytter seg på samme måte. Endringslovene har derimot
  maskinlesbare endringsinstrukser (`data-move-part` m.fl.) som kan brukes som bevis når et element skal følges.
- **#498 er data fra før #361, ikke en ny feil.** Kilde-HTML-en er riktig. Den delte basen ble sist synkronisert 3. september 2026, før #361 ble
  flettet 15. oktober. Dagens parser gir `a6/ledd-1/avslutning`. Kjøres dagens parser på den lagrede HTML-en, får 582 dokumenter 2 185 nye
  avslutningsnoder. Punktbokstavene mangler derimot også i dagens parser: de står i kilden, men parseren lagrer løpenummeret.
- **Forslag:** rett 04 §2 nå (tekst i §4.1). Lag en varig elementidentitet som egen byggesak (alternativ B). Lagre punktbokstaven i `nummer`, ikke i
  eId-en.

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
stemmer. Regelen i docs/08 betyr at en lagret eId ikke skrives om. Den betyr ikke at eId-en følger elementet.

| Alternativ | Hva det er | For | Mot |
|---|---|---|---|
| **A. Rett påstanden** | 04 §2 sier at vår eId er en adresse i én versjon (AKN `eId`-rollen), og at vi ikke har noen `wId`. Eksporten skriver AKN-`eId` per versjon og utelater `wId` | Sant, billig, ingen kode. Hindrer at noen bygger GUID eller `wId` av eId (09 §4.1) | Hullet står åpent: koblinger uten versjon (`begreper`) peker fortsatt feil etter en omnummerering |
| **B. A + varig elementidentitet som egen byggesak** | Ny kolonne med en varig ID per node. Ved ny versjon føres den videre etter samme eId og hash, så unik hash, så sitat, så Lovdatas endringsinstrukser. Det som ikke kan føres entydig, får ny ID og legges til gjennomgang. Eksporten bruker den som `wId` og GUID. Koblinger uten versjon flyttes til den varige ID-en | Gir `wId`-rollen et innhold. Bygger på det taggene gjør allerede. Riktig for RUML-ankere (09 §4.1) og HTML4EU (10 §5) | Byggekostnad (§6). En maskinell kobling er en påstand om identitet; den må kunne vurderes av et menneske når den er usikker. Eldre versjoner må kobles i ettertid |
| C. Gjør eId-en selv varig | Et element beholder eId-en sin når det omnummereres; nye elementer får ny eId | En nøkkel i stedet for to | eId-en slutter å svare til det trykte nummeret, så `§5/ledd-3` kan være «fjerde ledd». Parseren kan ikke bygge eId av kilden alene, den må kjenne historikken. Bryter AKN sin `eId`-regel |
| D. Vent på offisiell seksjons-ELI eller ELI-underinndelinger versjon 3 | | Ingen kostnad nå | Begge er adresser, ikke identitet (10 §5). Løser ikke problemet |

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

Feilen gjelder altså hele korpuset, ikke bare EØS-tekst. Formen i GDPR-teksten, der underleddet er et eget `<article class="legalP">` inne i
et nummerert ledd etter lista, finnes 239 ganger i 83 dokumenter (21 lover, 62 forskrifter). Resten er `p.leddfortsettelse` (890),
tekst etter en liste i et punkt (727) og andre former.

M2 viser i tillegg endringer fra #397 (vedlegg): 757 nye ledd og 6 118 nye punkt i 91 dokumenter, og de 184 punkt-eId-ene som forsvinner (§2.1).

### 3.2 Årsak: parseren, ikke kilden

**Kilden er riktig.** Artikkel 6 nr. 1 i den lagrede HTML-en (personopplysningsloven, versjon 7):
- `<article class="numberedLegalP" data-numerator="1">1. Behandlingen er bare lovlig … oppfylt:`
- `<ol class="defaultList" type="a">` med `<li data-name="a." value="1">` … `<li data-name="f." value="6">`
- etter lista: `<article class="legalP" id="…-nummer-1-ledd-1">Nr. 1 bokstav f) får ikke anvendelse …</article>`

Bokstaven står i `data-name` og `type="a"`. Underleddet er et eget element med egen `id`.

**Parseren på importtidspunktet limte underleddet inn.** Versjon 7 ble laget 2026-09-02. #361 ble flettet 2026-10-15 (5bd93b8). Siste resynk
var 2026-09-03, og ingen kilde er konvertert på nytt etter det. Den lagrede `a6/ledd-1` har derfor teksten «… minst ett av følgende vilkår er
oppfylt: Nr. 1 bokstav f) får ikke anvendelse …».

**Dagens parser gjør det riktig** for underleddet: `a6/ledd-1` er bare innledningen, og `a6/ledd-1/avslutning` er «Nr. 1 bokstav f) …». Det
samme gjelder `a6/ledd-3/avslutning`. Personopplysningsloven får 5 nye avslutningsnoder.

**Bokstaven mangler fortsatt.** Parseren setter `Nummer = punktIndeks.ToString()` (`LovdataHtmlParser.cs:1406` og `1425`) og leser aldri
`data-name`. Det gjelder alle punkter, også i dagens kode. På samme måte får ledd løpenummeret, ikke `data-numerator`.

### 3.3 Hvordan bør punktbokstaven lagres?

**Forslag:** i `rettskilde_noder.nummer`, ikke i eId-en.
- `nummer` = etiketten fra `<li data-name>` uten tegnsetting: `a`, `b`, `11a`. For ledd: `data-numerator` der det finnes. Mangler etiketten
  (7 348 punkt har ingen `data-name`, og 18 285 står ikke i en `<li>`), beholdes løpenummeret og merkes ikke som trykt. Ingenting gjettes.
- Hva etiketten er (bokstav, tall, strek), avledes av listas `type` og etiketten. Da kan visningen skrive «bokstav a» eller «nr. 1», og
  sitatet «personopplysningsloven artikkel 6 nr. 1 bokstav f» kan slås opp.
- AKN-`<num>` får etiketten. Da kan eksporten bygge AKN-`eId` (`point_f`) og HTML4EU-ID (`pnt_f`) fra den, slik 04 §2 avvik 3 krever.

**Hvorfor ikke i eId-en:** vår eId blir da ikke varig av den grunn. En bokstav endres også ved omnummerering (bokstav c blir d). Og alle
punkt-eId-er i korpuset ville endret seg, sammen med alle koblingene til dem. Med #495 alternativ B er eId-en adressen; da kan en senere sak
vurdere om adressen skal bygges av etiketten. Det er ikke en del av dette forslaget.

**Underledd (`sub`).** HTML4EU deler et nummerert ledd i underledd: `par_1/sub_1` (innledning og punkter) og `par_1/sub_2` (neste underledd).
Vi har ikke dette nivået. Forslaget er å beholde avslutningsnoden fra #361 og gjøre `sub` til en regel i eksporten: ledd med liste =
`sub_1`, avslutning N = `sub_{N+1}`. Kilden skiller de to formene (eget `legalP` mot `p.leddfortsettelse`), så eksporten kan velge `sub_2`
eller `wrp` (closing-part) uten å gjette. Et eget underleddsnivå i eId-en ville endret eId-en til hvert punkt i de berørte leddene.

Merk: de 239 avslutningsnodene av EU-formen finnes ennå ikke i den delte basen. Skal de ha en annen eId enn `/avslutning` (for eksempel
`/underledd-2`), er det billigst å bestemme det før den delte basen konverteres på nytt.

### 3.4 Påvirker en retting eId-er eller koblinger som allerede er lagret?

**Ny konvertering av den delte basen (#361 og #397, kode som alt er flettet).** M2 matchet mot alle koblingstabellene (M5):
- **eId-er som forsvinner:** ingen ledd- eller punkt-eId forsvinner av #361. 184 punkt-eId-er forsvinner av #397, i 10 forskrifter. Ingen
  koblinger peker på dem.
- **Koblinger til noder som får ny tekst:** 32 rader. Begrepsforekomster 13, virksomhetskandidater 7, funnkoblinger 5, tagger 3,
  strukturkanter 2, navneformbinding 1 og navnekandidat 1. Strukturkantene, bindingen og funnene flagges «hjemmel endret» eller «kilde endret».
  Taggene flyttes etter sitatet. De andre blir stående på den gamle versjonen.
- **Fasit:** 15 eId-er i fasitfilene får ny tekst (blant dem helse-og-omsorgstjenesteloven § 12-4 fjerde ledd, som #361 AK7 gjelder).
- 619 dokumenter får ny versjon.

**Bokstaven i `nummer`.** Ingen eId endres. `tekst_hash` endres ikke, og «hjemmel endret» sammenligner ikke `nummer`
(`HjemmelEndretFlagg.Nodeavtrykk` har bare eId, overskrift og hash), så ingen koblinger flagges. Men AKN endres (`<num>`), og resynken lager en
ny versjon når AKN er endret. Det gjelder om lag 3 500 dokumenter (3 497 dokumenter har punkt med etikett), med et fullt nytt sett noder hver.
Det er den største kostnaden, og grunnen til at det bør tas i samme kjøring som #361 og #397.

---

## 4. Forslag

### 4.1 Ny tekst i 04 §2 (alternativ A)

Erstatter avsnittet «Funnet ved verifiseringen» og den første kulen i listen over den doble nøkkelen. Ikke innført; venter på prosjekteieren.

> **Funnet ved verifiseringen (rettet etter #495, docs/35):** AKN har selv en dobbel nøkkel [AKN-NC §5.3, §5.5.2]. `eId` er uttrykksnivå og
> *skal* endres når et element omnummereres. `wId` er verksnivå, endres aldri og bygges med samme syntaks fra «master expression». Vår eId bygges
> på nytt i hver versjon av §-nummer og ledd- og punktindeks (docs/08 §1.2). Den oppfører seg derfor som AKN sin `eId`: settes et ledd eller
> punkt inn, får de neste ny eId, og den gamle eId-en går til et annet element (målt i docs/35 §2.1). Regelen i docs/08 om at eId ikke endres,
> betyr at vi ikke skriver om en eId som er lagret. Den betyr ikke at eId-en følger elementet. Vi har i dag ingen `wId`. Ved eksport skriver vi
> en avledet AKN-`eId` per versjon og utelater `wId` til vi har en varig elementidentitet (#495).

Og i listen over den doble nøkkelen:
- vår eId som adresse i én versjon (AKN `eId`-rollen), ikke som varig identitet,
- en varig elementidentitet i `wId`-rollen, som må lages (#495),
- en avledet AKN-eId etter navnekonvensjonen,
- og uttrykks-ID med `@dato` og versjonskjede.

docs/08 §1.2 («`eid` … endres aldri») og §2.1 (tagger overlever gjennom eId og hash) bør presiseres på samme måte. Det er ikke gjort her.

### 4.2 Byggesaker (ingen er startet)

1. **#498 a: bokstav og trykt nummer i `nummer`.** Parseren leser `<li data-name>`, listas `type` og `data-numerator`. Visningen skriver
   «bokstav a»/«nr. 1». Fixture-tester: personopplysningsloven artikkel 6, tollavgiftsforskriften § 4-1-1, skatteloven § 10-5. Ingen migrasjon.
2. **#498 b: konverter den delte basen på nytt** med dagens parser, etter 1. Ingen ny kode (`konverter-paa-nytt` eller resynk). Gir #361, #397
   og bokstavene i én runde. Juristen ser de 32 koblingene og de 15 fasit-eId-ene.
3. **#495: varig elementidentitet (alternativ B).** Kolonne, koblingsrunde ved ny versjon, etterkobling av de 1 118 versjonsparene, og
   `begreper`, `vilkar` og `kodelister` over på den varige ID-en.

---

## 5. Juristrunden

*Fylles inn etter runden.*

---

## 6. Valg for prosjekteieren

| Valg | Alternativer | Anbefaling | Pris |
|---|---|---|---|
| 1. 04 §2 | A rett påstanden · B A + byggesak · C eId varig · D vent | **B** (A nå, byggesaken etterpå) | A: én dokumentendring. B: se valg 4 |
| 2. Hvor bokstaven lagres | `nummer` · eId (`punkt-a`) | **`nummer`** | `nummer`: parser + tester + liten visningsendring, ingen migrasjon. eId: alle punkt-eId-er og koblinger endres |
| 3. EU-underledd | `/avslutning` som nå + `sub` i eksporten · egen eId (`/underledd-2`) | **`/avslutning` + eksportregel** | Første: ingen endring. Andre: liten parserendring nå, før den delte basen konverteres; dyrere etterpå |
| 4. Når den delte basen konverteres på nytt | Nå · etter byggesak 1 | **Etter byggesak 1**, i samme kjøring | 619 dokumenter (+ om lag 3 500 med bokstavene) får ny versjon én gang i stedet for to |

**Anslag for byggingen** (ikke tid, men omfang):
- Byggesak 1: én PR. `LovdataHtmlParser` (to steder), `AknXmlSkriver` (uendret, `<num>` får etiketten), visningen av punkt (én komponent),
  3 fixture-tester. Liten.
- Byggesak 2: ingen kode. Én kjøring og en kontroll av de 32 koblingene og 15 fasit-eId-ene. Liten, men gir mange nye versjoner.
- Byggesak 3: tre PR-er. (a) Migrasjon med kolonne og indeks, koblingsrunden i `OpprettNyVersjonAsync` med tester. (b) Etterkobling av
  eksisterende versjonspar og en gjennomgangsliste for det som ikke kan kobles entydig. (c) `begreper.lovreferanse_eid`, `vilkar` og
  `kodelister` over på den varige ID-en, og eksporten. Middels til stor. Koblingsrunden er en påstand om identitet og bør juristvurderes på et
  utvalg.

---

## 7. Det som ikke er sjekket

- Om visningen leser «versjon + eId»-koblingene (§2.4) mot den gamle eller den gjeldende versjonen. Bare `begreper` er lest i koden.
- Om alle omnummereringer i Lovdata har en endringsinstruks (`data-move-part`), og hvor mange slike som finnes i hele Lovtidend.
- Kilde-HTML-en for versjonene fra før 2026-09-02, som ikke er lagret. Derfor kan årsaken i vareførselsforskriften § 5-11-5 ikke skilles.
- Hvor mye den delte basen vokser når om lag 3 500 dokumenter får en ny versjon.
- Etiketter som «i.» er talt som bokstav; noen kan være romertall.
