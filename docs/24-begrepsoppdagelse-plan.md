# 24. Begrepsoppdagelse — plan

Vurdering av Johanns kravspesifikasjon for deterministisk (regex-basert) sveip av rettskildetekst for
å oppdage begreps-definisjoner (`oppdag_begreper`, mønsterkatalog M1–M17) og en sekundær
kollisjonssveip på tvers av korpuset (`sveip_begrepskollisjoner`). Full spesifikasjonstekst finnes i
chat-historikken denne planrunden kom fra — den limes ikke inn på nytt her. Dette dokumentet er en
kritisk vurdering av spesifikasjonen mot den FAKTISKE kodebasen, i samme stil og med samme
gjennomgangsmetode som `docs/20-virksomhetskatalog-og-rollemodell.md`.

**Kort oppsummert hva spesifikasjonen ber om:** to funksjoner — en per-dokument-sveip som klassifiserer
hver paragraf individuelt (aldri kapitteloverskrift som filter) og ekstraherer begrep-definisjon-par med
full kildesporing (`Begrepsforekomst`-skjema, §5), og en korpus-sveip som grupperer forekomster på
normalisert term og eksponerer variasjon mellom regelverk uten å slå sammen eller "avgjøre" noe juridisk.
Relasjoner mellom begreper (avhengighet/utelukkelse/unntak) skal lagres som egne kanter, ikke fritekst.
AKN sitt `<term>`/`<def>`-apparat foreslås brukt til å tagge nøyaktig de tekstspennene som bidrar til en
definisjon.

## 1. De fem vurderingspunktene

### 1.1 Skal `Begrepsforekomst`-feltene inn på `BegrepEntitet`, eller egen tabell?

**Konklusjon: egen tabell (`BegrepsforekomstEntitet`), IKKE en utvidelse av `BegrepEntitet`.**

Dette er en annen situasjon enn virksomhetskatalog-runden, selv om Johann selv trakk parallellen.
`BegrepEntitet` (`src/RegelIde.Data/Entiteter.cs:803-845`) er et **register**: én rad representerer
virksomhetens ene, gjeldende, godkjente forståelse av ett begrep, med full status-pipeline (`utkast` →
`foreslatt_av_ai` → `under_revisjon` → `validert` → `publisert` → `tilbaketrukket` → `arkivert`, se
`BegrepsregisterTjeneste.cs:13`) og versjonering/`ErstatterId`. Ett begrep = én (aktiv) rad.

`Begrepsforekomst` er derimot strukturelt en **1-til-mange-observasjon**: samme term ("samboer") kan
dukke opp som 5+ ulike, delvis motstridende forekomster på tvers av korpuset — det er selve poenget med
`sveip_begrepskollisjoner` (§8 i spesifikasjonen). De fleste forekomstene (lav konfidens, `krever_oppslag`,
rene kryssreferanser uten egen definisjon) skal ALDRI bli en egen `BegrepEntitet`-rad. Å skrive dem inn
som `BegrepEntitet`-rader med `Status="foreslatt_av_ai"` ville forsøplet registeret med dupliserte,
delvis selvmotsigende "forslag" for samme term hver gang sveipet finner enda en forekomst i enda et
dokument — helt ulikt hvordan dagens KI-forslagsmekanisme fungerer (se §1.2 under: ÉN KI-kjøring foreslår
ÉN rad per begrep den identifiserer, ikke N rader per forekomst av samme begrep).

Dette er faktisk nøyaktig samme avveining Johann selv gjorde i virksomhetskatalog-runden — bare med
motsatt konklusjon, fordi formen er ulik: `Virksomhet` ble utvidet fordi det var en ren 1:1-berikelse
(samme rad, flere felt fra Brreg). `VirksomhetKandidat` (`Entiteter.cs:878-908`) ble derimot en HELT NY
tabell, fordi det er en arbeidskø med annen livssyklus enn den entiteten den til slutt kobles til
(`Virksomhet`/`Begrep`). Begrepsoppdagelse er strukturelt sistnevnte tilfelle, ikke førstnevnte.

Ny tabell `BegrepsforekomstEntitet` bærer spesifikasjonens §5-felt (se §3 under for konkret skjema).
`BegrepId` (nullable FK → `BegrepEntitet`) settes først når en forekomst godkjennes og enten kobles til
en eksisterende begreps-rad eller oppretter en ny.

### 1.2 Skal sveipet lande i den eksisterende `BegrepsforslagKo`, eller en egen kø?

**Konklusjon: egen kø, egen tabell, egen frontend-side — samme mønster som `VirksomhetKandidat`,
IKKE samme mønster som dagens KI-forslag for begrep.**

Verifisert i kode: dagens "Identifiser begrep" (`BegrepsforslagTjeneste.cs`,
`BegrepsregisterTjeneste.OpprettForslagFraKiAsync` linje 58-73) oppretter **direkte** `BegrepEntitet`-rader
med `Status="foreslatt_av_ai"` — det finnes ingen egen kandidat-tabell for KI-forslag om begrep. Køen
(`GET /api/begreper/forslag`, `Program.cs:2039`, filtrert på `Status == "foreslatt_av_ai"`, frontend
`src/RegelIde.Web/src/pages/BegrepsforslagKo.tsx`) er bare et filter på selve `Begrep`-tabellen. Dette
fungerer fordi KI-forslag allerede er "sluttform" — ett begrep inn, én kandidatrad ut, klar til å bli et
register-oppføring ved godkjenning.

Deterministisk sveip er formmessig identisk med `VirksomhetKandidat`
(`Entiteter.cs:878-908`, §2.6/§2.6.1 i docs/20), ikke med KI-forslags-mekanismen: mange rå treff per
sveip, variabel/lav konfidens, treff som ALDRI skal bli en registerrad (rene kryssreferanser, negative
avgrensninger som kun er relasjoner), og et eksplisitt krav om enkeltvis godkjenn/avvis FØR noe lander i
registeret (spesifikasjonens §10: M13/M4/M17 skal starte i "kun-flagg"-modus, ingen automatisk opptak).
`VirksomhetKandidat`s already-bygde flyt (`VirksomhetKandidatTjeneste.GodkjennAsync`, referert i
docs/20 §2.6.1) løser nøyaktig dette: re-kjør matching mot nodens DÅVÆRENDE tekst ved godkjenning,
opprett en ekte `TekstTaggEntitet` (kind="begrep") pekende til presist tegn-intervall, koble taggens
`RefId` til register-raden. Samme flyt gjenbrukes her nesten uendret (se §1.4 og §3).

Egen kø `Begrepsforekomster` (rute `/api/begrepsforekomster`, egen frontend-side) med samme
`Venter`/`Godkjent`/`Avvist`-statusmodell som `VirksomhetKandidat` — bevisst avvik fra full
`Entitetsstatus`/`Versjon`-husstil, av samme grunn som der (arbeidskø, ikke autoritativt innhold).

### 1.3 Er `RettskildeNode`-granulariteten (kapittel/underinndeling/paragraf/ledd/punkt) nok for M1/M11/M14?

**Konklusjon: strukturelt ja for M1/M11/M14 — men to konkrete, verifiserte hull må løses/erkjennes
FØR implementasjon, og de er ikke nevnt i spesifikasjonen.**

Bekreftet: `RettskildeNodeEntitet.NodeType` (`Entiteter.cs:249`) har nettopp disse fem verdiene, `Tekst`
er kun satt på bladnivå (ledd/punkt, `Entiteter.cs:252`), og `LovdataHtmlParser.cs` bygger konsekvent
ledd/punkt-noder for ALL faktisk parset lovtekst (ikke en "vanligvis"-oppførsel) — `ParseLedd`/`ParsePunkt`
kalles fra samme sted uansett om paragrafen står i et kapittel, en underinndeling, eller direkte i loven
(`LovdataHtmlParser.cs:87-105, 596-611, 727-745`). Dette dekker M1 (bokstavliste = søsken-`Punkt`-noder
under en `Ledd`), M11 (paragraf-node hvis `Overskrift` er selve termen, første `Ledd`s `Tekst` er
definisjonssetningen) og M14 (flere søsken-`Ledd`-noder under samme `Paragraf` — `definisjon_spenn` blir
naturlig en liste av `Ledd`-eId-er).

**Hull 1 — verifisert, ikke nevnt i spesifikasjonen:** `data/kilder/raw-lovdata/` inneholder i dag 8
faktisk importerte lover (alkoholloven, serveringsloven, forvaltningsloven, motorferdselloven,
personopplysningsloven, tannhelsetjenesteloven, advokatloven, alkoholforskriften) — **verken
FOR-2015-06-25-793 eller folketrygdloven** (spesifikasjonens egne §11-testdokumenter) finnes i korpuset.
Siden sveipet per spesifikasjonens egen algoritme (§4) opererer på `RettskildeNode`-rader i databasen,
ikke på rå HTML/PDF, MÅ disse to dokumentene importeres inn i korpuset før M1/M11 i det hele tatt kan
valideres mot ekte data slik spesifikasjonen selv foreskriver som testgrunnlag. Dette er et reelt,
konkret første steg spesifikasjonen forutsetter uten å nevne det.

**Hull 2 — verifisert, ikke nevnt i spesifikasjonen:** `NodeType`-enumen i
`src/RegelIde.Kildekonvertering/Modeller.cs:7-14` har KUN `Kapittel`, `Underinndeling`, `Paragraf`,
`Ledd`, `Punkt` — det finnes ingen `Vedlegg`-nodetype, og ingen parser-logikk for tabeller i vedlegg,
noe sted i `RegelIde.Kildekonvertering`. M8 (vedleggs-/tabellbasert definisjon) har derfor INGEN
strukturell hjemmel i dagens skjema — det er ikke en liten utvidelse, det er en ny nodetype pluss ny
parser-gren pluss ny AKN-serialisering (`AknXmlSkriver.cs` har heller ingen `<attachment>`/tabell-case i
dag). Anbefaling: M8 tas eksplisitt UT av første leveranse (se §4, byggerekkefølge) — ikke fordi
mønsteret er juridisk mindre viktig, men fordi det krever et helt annet stykke infrastrukturarbeid enn
resten av mønsterkatalogen.

**Mindre observasjon:** `LovdataHtmlParser.cs` sin kommentar ved `ParseChildPunkter` (linje ~848) bekrefter
at punktlister kan nøstes vilkårlig dypt i ekte data (eksempel: alkoholforskriften § 6-2). M1s
listepunkt-klassifisering ("listepunkt parser_til (term, forklaring)") må derfor håndtere at ett
listepunkt i en definisjonsliste i seg selv kan inneholde en nøstet liste (typisk M16-vurderingsmoment
under en allerede etablert definisjon) — ikke bare flat iterasjon over direkte barn.

### 1.4 AKN `<term>`/`<def>`-tagging — bygg fra bunnen, eller gjenbruk `TekstTaggEntitet`?

**Konklusjon: gjenbruk `TekstTaggEntitet` (`Kind="begrep"`). Bygg IKKE AKN `<term>`/`<def>`-tagging.
Dette er den viktigste enkeltkonklusjonen i denne planen.**

Verifisert: `RettskildeEntitet.AknXml` (`Entiteter.cs:156`) er eksplisitt dokumentert som "NULL for
referanse-stubber" og "en AVLEDET serialisering, ikke originalen selv" (linje 180). AKN-XML-en
GENERERES av `AknXmlSkriver.Skriv` (steg 7 i konverteringspipelinen) FRA `RettskildeNodeEntitet`-treet
(`Tekst`, `Overskrift`, struktur) — den redigeres aldri direkte, og skjemaets EGEN doc-kommentar
(`AknXmlSkriver.cs:6-18`) sier eksplisitt at klassens output er "referansielt transparent": samme
(metadata, noder) gir ALLTID bit-identisk XML. Å skrive `<term>`/`<def>`-tagger "tilbake" i AKN-dokumentet
slik spesifikasjonens §7 foreslår, ville enten:

1. Injisere XML-markup inn i `RettskildeNodeEntitet.Tekst` selv — en ren streng som i dag brukes av
   `TekstHash` (endringsdeteksjon), `TekstTaggTjeneste.OpprettAsync`s eksakte substring-matching
   (`quoteExact != faktiskUtdrag` kaster), embeddings (`RettskildeNodeEmbeddingEntitet`) og fritekstsøk —
   ALLE disse konsumentene forutsetter i dag at `Tekst` er ren prosa uten markup. Dette ville brutt dem
   samtidig, for et formål de ikke er bygget for.
2. Eller gjøre `AknXmlSkriver` avhengig av en ny, separat term/def-annotasjonsmodell den spleiser inn
   ved serialisering — noe som i praksis er å bygge en ny, AKN-spesifikk variant av nøyaktig den
   mekanismen `TekstTaggEntitet` allerede er: en offset-basert annotasjon over ellers uendret ren tekst.
   Det ville også brutt den eksplisitte referanse-transparens-garantien i pkt. 6, siden output da ville
   avhenge av sveipresultater, ikke bare (metadata, noder).

`TekstTaggEntitet` (`Entiteter.cs:384-418`) løser "pek til et presist tekstutdrag uten å kopiere det"
allerede, for et nesten identisk formål (virksomhetsnavn i tekst, samme kandidatsøk-og-godkjenning-runde
som Johann selv trekker paralleller til): `StartOffset`/`EndOffset` + `QuotePrefix`/`QuoteExact`/
`QuoteSuffix` + `NodeTekstHash`, revalidert live mot `RettskildeNode.Tekst` ved opprettelse
(`TekstTaggTjeneste.OpprettAsync`, kaster hvis teksten er endret siden markeringen ble laget) og
revalidert IGJEN ved godkjenning i `VirksomhetKandidat`-flyten
(`VirksomhetKandidatTjeneste.GodkjennAsync`, docs/20 §2.6.1: "re-kjører matchingen mot nodens DÅVÆRENDE
tekst ... i stedet for å lagre quoteSelector-en på kandidaten"). Dette er PRESIS den
stale-mot-reimport-garantien M14s ko-referanse-spenn trenger. `TekstTaggEntitet.Kind` inkluderer
allerede `"begrep"` med `RefId` som FK til `BegrepEntitet`.

Praktisk konsekvens for skjemaet (se §3): `definisjon_spenn` (spesifikasjonens liste av eId-er, §5/§7)
blir i praksis en liste av `TekstTaggEntitet`-rader (opprettet ved godkjenning, én per bidragende
ledd/setning ved M14), IKKE eId-referanser inn i en AKN-`<def>`-tagg. AKN-dokumentet forblir urørt,
nøyaktig slik spesifikasjonens egen §6 sier om relasjonslaget ("Selve AKN-dokumentet forblir urørt") —
den samme begrunnelsen gjelder like mye for selve definisjonsspennet, ikke bare for relasjonene.

### 1.5 Relasjonsmodellen (M9/M15) — gjenbruk `Tjenesteavhengighet`-mønsteret?

**Konklusjon: ja, samme entitetsform bør gjenbrukes for en ny `Begrepsrelasjon`-tabell — ikke samme
tabell (annen domeneentitet), men samme mønster.**

`TjenesteavhengighetEntitet` (`Entiteter.cs:697-722`) er nøyaktig formen spesifikasjonens §6 beskriver:
en typet, rettet kant (`Rel`: `forutsetning_for`\|`gir_mulighet_til`\|`utlost_av`\|`for`\|`avhengig_av`\|
`input_til`) mellom to rader av SAMME entitetstype (`FraTjenesteId`/`TilTjenesteId`), med en
fritekst-nyanse (`Beskrivelse`) og standard attribusjon. `Begrepsrelasjon` (avhenger_av/utelukker/
unntak_fra mellom to Begrep) er samme struktur, bare mellom `Begrep`-rader i stedet for `Tjeneste`-rader.

Én reell forskjell å ta stilling til: `Tjenesteavhengighet` kobler alltid til EKTE `Tjeneste`-rader (eller
en eksplisitt ekstern plassholder, `EksternTjenestereferanseEntitet`). For begrep-relasjoner oppdaget
under sveip vil MANGE av `fra_term`/`til_term` ikke ha noen `BegrepEntitet`-rad ennå (termen er kanskje
selv bare en uapprovert `Begrepsforekomst`, se §1.1). Anbefalt løsning: `Begrepsrelasjon` peker primært
på `Begrepsforekomst`-nivå (`FraForekomstId`/`TilForekomstId`, begge nullable — analogt
`EksternTjenestereferanseEntitet`-mønsteret der nøyaktig én av to referansefelt er satt) pluss
fritekst-fallback (`TilTermFritekst`) for tilfeller der målbegrepet ikke er funnet av sveipet i det hele
tatt (f.eks. et begrep definert i en lov utenfor korpuset). Se §3 for feltforslag.

Merk: `RettskildeReferanseEntitet` (`Entiteter.cs:344-366`, kryssreferanser mellom `RettskildeNode`-rader,
allerede med `TekstStart`/`TekstLengde`) er en ANNEN, allerede eksisterende mekanisme — den sporer
paragraf-til-paragraf-siteringer generelt (auto-fanget ved import), ikke begrep-til-begrep-relasjoner
spesifikt. Den er ikke riktig gjenbrukskandidat her, siden M15/M9s relasjoner er om BEGREPER (termer), ikke
om vilkårlige tekstsitater — men sveipet KAN med fordel konsultere `RettskildeReferanseEntitet` som et
signal når det leter etter M15-kryssreferanser (`"se § 1-10"`-mønsteret har ofte allerede blitt fanget som
en importert kryssreferanse), i stedet for å re-parse referansen fra bunnen av.

## 2. Anbefalt dataskjema-plan

Ny migrasjon, foreslått navn `LeggTilBegrepsoppdagelse` (følger `LeggTil...`-konvensjonen i
`src/RegelIde.Data/Migrasjoner/`).

### 2.1 `BegrepsforekomstEntitet` (ny tabell — sveip-resultat / arbeidskø)

| Felt | Type | Kommentar |
|---|---|---|
| `Id` | `Guid` PK | |
| `RettskildeId` | `Guid` FK → `Rettskilde` | Dokumentet forekomsten er funnet i |
| `NodeEid` | `string` | Paragraf-/ledd-nivå referanse — samme eId-mønster som resten av modellen |
| `Begrep` | `string` | Normalisert grunnform, lowercase (spesifikasjonens `begrep`) |
| `BegrepOriginal` | `string` | Ordlyd slik den faktisk står i teksten |
| `Definisjon` | `string?` | Rå tekst. `null` ved `krever_oppslag`/bruksdefinisjon |
| `Kildetype` | `string` | `eksplisitt_liste`\|`egen_paragraf`\|`inline_menes`\|`skal_forstas_som`\|`copula`\|`heretter_kalt`\|`ekstern_referanse`\|`eos_referanse`\|`vedleggstabell`\|`distribuert` |
| `MonsterId` | `string` | `M1`–`M17` — hvilket mønster som traff (sporbarhet/tuning, ikke i spesifikasjonens skjema eksplisitt, men billig å legge til nå) |
| `Konfidens` | `string` | `hoy`\|`middels`\|`lav`\|`krever_oppslag` |
| `Scope` | `string` | `hele_dokumentet`\|`kapittel`\|`paragraf` |
| `ScopeRefEid` | `string?` | eId til kapittel/paragraf hvis `Scope` er begrenset |
| `HenvisningsMaal` | `string?` | Kun ved `ekstern_referanse`/`eos_referanse` |
| `Status` | `string` | `Venter`\|`Godkjent`\|`Avvist` — samme lette arbeidskø-modell som `VirksomhetKandidat`, bevisst avvik fra full `Entitetsstatus`/`Versjon` |
| `BegrepId` | `Guid?` FK → `Begrep` | Satt når godkjent OG koblet til en register-rad |
| `OpprettetAv` | `string` | |
| `OpprettetTidspunkt` | `DateTimeOffset` | |
| `BehandletAv` | `string?` | |
| `BehandletTidspunkt` | `DateTimeOffset?` | |

`definisjon_spenn` fra spesifikasjonens §5 er BEVISST ikke en kolonne her — ved godkjenning opprettes én
eller flere `TekstTaggEntitet`-rader (`Kind="begrep"`, `RefId` = `BegrepId`), én per bidragende
ledd/setning (flere ved M14). Spennet leses ut som "alle tagger med dette `RefId`", ikke lagret separat —
unngår duplisert lagring av noe som kan avledes (samme resonnement som docs/20 §2.6.1 brukte for å IKKE
duplisere quoteSelector-felt på kandidatraden).

### 2.2 `BegrepsrelasjonEntitet` (ny tabell)

| Felt | Type | Kommentar |
|---|---|---|
| `Id` | `Guid` PK | |
| `FraForekomstId` | `Guid` FK → `Begrepsforekomst` | |
| `TilForekomstId` | `Guid?` FK → `Begrepsforekomst` | Nullable — nøyaktig én av denne og `TilTermFritekst` satt |
| `TilTermFritekst` | `string?` | Fallback når målbegrepet ikke er funnet av sveipet |
| `Relasjonstype` | `string` | `avhenger_av`\|`utelukker`\|`unntak_fra` |
| `TilReferanseEid` | `string` | Kildehenvisning (eId) relasjonen fremgår av |
| `OpprettetAv` | `string` | |
| `OpprettetTidspunkt` | `DateTimeOffset` | |

Samme felt-form som `TjenesteavhengighetEntitet` (§1.5), tilpasset at målet ofte er en uapprovert
forekomst, ikke en ferdig register-rad.

### 2.3 Ingen endring på `BegrepEntitet` eller `RettskildeNodeEntitet`

`BegrepEntitet` forblir uendret — en godkjent `Begrepsforekomst` oppretter eller oppdaterer en vanlig
`BegrepEntitet`-rad via EKSISTERENDE `BegrepsregisterTjeneste`-metoder (`OpprettAsync`/
`OpprettForslagFraKiAsync`-mønsteret, evt. en tredje `OpprettFraForekomstAsync`), ikke nye felt på
entiteten selv. `RettskildeNodeEntitet.NodeType` utvides IKKE med `Vedlegg` i denne runden (se §1.3/§4 —
M8 er eksplisitt utenfor scope for første leveranse).

### 2.4 `sveip_begrepskollisjoner` — ingen ny tabell

Kollisjonsregisteret (spesifikasjonens §8) er en BEREGNET visning (gruppering av `Begrepsforekomst` på
normalisert `Begrep` på tvers av `RettskildeId`), ikke lagret som egen fakta — samme prinsipp som docs/20
§3 ("Aggregerte visninger (beregnet, ikke lagret)") allerede etablerer for virksomhetskatalogen. Ingen
migrasjon nødvendig for funksjon 2 utover det `Begrepsforekomst` allerede gir.

## 3. Anbefalt byggerekkefølge

Spesifikasjonen selv anbefaler å starte med M1 og M11 ("høyest konfidens, strukturelt enklest"). Det er
riktig retning, men to infrastruktur-steg må komme FØR eller SAMMEN MED det, som spesifikasjonen selv
ikke nevner (funn fra §1.3):

1. **Importer testgrunnlaget inn i korpuset.** Verken FOR-2015-06-25-793 eller folketrygdloven kapittel 1
   finnes i `data/kilder/raw-lovdata/` i dag. Sveipet opererer på `RettskildeNode`-rader i databasen —
   uten disse to dokumentene faktisk importert kan M1/M11 ikke valideres mot spesifikasjonens eget §11
   testgrunnlag i det hele tatt.
2. **Skjemamigrasjon for `Begrepsforekomst`/`Begrepsrelasjon`** (§2 over) — selv en "kun-flagg"-første-
   iterasjon av M1/M11 trenger et sted å skrive resultater.
3. **`klassifiser_paragraf` + ekstraksjon for M1 og M11**, validert mot de to importerte
   referansedokumentene — som spesifikasjonen selv anbefaler.
4. **Godkjenningsflyt**: gjenbruk `VirksomhetKandidatTjeneste.GodkjennAsync`-mønsteret rått —
   revalider mot nodens dåværende tekst, opprett `TekstTaggEntitet`(kind="begrep"), koble/opprett
   `BegrepEntitet`-rad. Bygges nå, ikke utsettes — uten denne flyten er sveipet en ren rapport uten vei
   inn i det faktiske begrepsregisteret, og hele poenget med `Begrepsforekomst`-arbeidskøen (§1.2) uteblir.
5. **Utvid mønsterkatalogen iterativt**: M2/M3/M9 (materielle bestemmelser), deretter M5/M6
   (ekstern/EØS-referanse, `krever_oppslag`), deretter M14 (distribuert — teknisk mest krevende per
   spesifikasjonens eget §10, vurder som eget delsteg med lavere automatiseringsgrad). M4/M13/M17 sist,
   og alltid i "kun-flagg"-modus til treffsikkerhet er validert (spesifikasjonens egen anbefaling, § 10).
6. **M8 (vedleggstabell) tas eksplisitt UT av dette byggeløpet** — krever ny `NodeType.Vedlegg`, ny
   parser-gren i `LovdataHtmlParser`, og ny AKN-serialiseringsgren. Egen, senere runde.
7. **`sveip_begrepskollisjoner`** bygges når nok dokumenter med overlappende terminologi er sveipet til
   at grupperingen gir noe interessant å se (i praksis: etter at flere enn de to referansedokumentene er
   importert og sveipet) — ren lesemodell over `Begrepsforekomst`, ingen egen migrasjon.

## 4. Reelle risikoer og åpne spørsmål

Fra spesifikasjonens eget §10 (tatt med for fullstendighet):

- M13 (copula) og M4/M17 (heretter kalt) har høyest risiko for falske positiver — start i
  "kun-flagg"-modus.
- M14 (distribuert/ko-referanse) er teknisk mest krevende — vurder eget delsteg med lavere
  automatiseringsgrad.
- M10 (bruksdefinisjon) er ikke mønstergjenkjennbar — kun indirekte tellbar, egen rapporttype.

Egne funn fra kodebase-gjennomgangen, IKKE dekket av spesifikasjonen:

- **`rettsomrade`-feltet spesifikasjonen forutsetter finnes "andre steder i systemet" (§10, §8) finnes
  IKKE.** Verifisert: ingen `Rettsomrade`/`Rettsfelt`/tilsvarende-kolonne noe sted i `Entiteter.cs`.
  `TjenesteEntitet.Tjenesteomrade` (fri tekst) er nærmeste eksisterende presedens for et lignende felt,
  men på feil entitet (`Tjeneste`, ikke `Rettskilde`). Kollisjonsregisterets `rettsomrade`-gruppering
  (§8 i spesifikasjonen) kan ikke bygges før dette feltet enten legges til på `RettskildeEntitet`, eller
  spesifikasjonen omformuleres til å klare seg uten det (f.eks. gruppere på `Kildetype`/`Tittel` i
  stedet). Dette må avklares med Johann — ikke noe denne planen bør gjette seg til et svar på.
- **M8 har ingen strukturell hjemmel i dagens skjema** (se §1.3, §4 pkt. 6) — verken `NodeType.Vedlegg`,
  parser-støtte for tabeller i vedlegg, eller AKN-serialisering for dette finnes. Reelt, ikke-trivielt
  forarbeid, ikke en "liten utvidelse".
- **Testgrunnlaget (FOR-2015-06-25-793, folketrygdloven kap. 1) er ikke importert i korpuset ennå** (se
  §1.3, §4 pkt. 1) — konkret blokkerende førstesteg spesifikasjonen selv forutsetter uten å si det.
- **Nøstede punktlister** (bekreftet i ekte data, alkoholforskriften § 6-2 via `LovdataHtmlParser.cs`)
  betyr at M1s "listepunkt → (term, forklaring)"-klassifisering må ta stilling til nøstede lister
  eksplisitt (er en nøstet liste under et definisjonspunkt en M16-vurderingsmomentliste, eller en
  fortsatt del av definisjonen?) — spesifikasjonens pseudokode (§4) itererer flatt over
  `seksjon.etterfolgende_liste` og adresserer ikke dette tilfellet.
- **Ingen avklaring i spesifikasjonen på hvordan `Begrepsforekomst` og eksisterende KI-forslag
  (`Status="foreslatt_av_ai"` på `BegrepEntitet`) skal forholde seg til hverandre i UI-et** når begge
  mekanismene nå kan produsere "forslag til nytt begrep" for samme term — bør en bruker se ÉN samlet
  kandidatliste (deterministisk + KI, med kilde-badge), eller to atskilte køer (`BegrepsforslagKo` og en
  ny `BegrepsforekomstKo`)? Denne planen anbefaler to atskilte køer av strukturelle grunner (§1.2), men
  det UX-messige spørsmålet om hvorvidt de bør VISES sammen et sted er ikke besluttet her.
- **Idempotens ved gjentatt sveip** er ikke adressert i spesifikasjonen: kjøres `oppdag_begreper` på nytt
  etter at en `Rettskilde` reimporteres (ny versjon, endret tekst), må sveipet kunne skille "denne
  forekomsten er allerede sett og behandlet" fra "dette er en ny forekomst" — samme problem
  `VirksomhetKandidatTjeneste.OpprettEllerFinnAsync` allerede løser for virksomhetssveipet (idempotent på
  en utvidet nøkkel inkludert `StartOffset`). `Begrepsforekomst` bør få tilsvarende unik nøkkel
  (`RettskildeId`, `NodeEid`, `Begrep`, en form for tegn-intervall) før første sveip-implementasjon, ikke
  ettpåhengt.
