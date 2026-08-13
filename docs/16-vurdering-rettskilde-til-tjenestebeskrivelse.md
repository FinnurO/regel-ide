# 16. Vurdering: «Fra rettskilde til publisert tjenestebeskrivelse» mot faktisk kode

*Mottatt prosessdokument (arbeidsprosess for jurist og informasjonsforvalter, gjennomgangseksempel
skjenkebevilling). Denne vurderingen tar **ikke** stilling til om prosessen er riktig eller ønskelig
— den er velfundert — men utelukkende til **hvor Regel-IDE faktisk står i forhold til den**, påstand
for påstand, med referanse til kode og ikke til docs.*

Verifisert mot `master` @ `f7ad7eb` (2026-08-13). Alle linjenumre er fra det commit-et. Branchen
`feature/nettside-handbok-app` har null diff mot `master` og inneholder ingenting utover den.

---

## 0. Markører

| Markør | Betyr |
|---|---|
| **BYGGET** | Implementert, nåbart for en bruker, og dekket av tester |
| **DELVIS BYGGET** | Noe reelt finnes, med et konkret navngitt gap |
| **IKKE BYGGET** | Ingenting finnes — bekreftet med negativt søk |
| **LÅST SOM DESIGN** | Besluttet i `docs/13`/`docs/14`/`docs/15`, aldri kodet |
| **STRIDER MOT KODEN** | Dokumentet beskriver noe koden i dag gjør motsatt |

Et skille som går igjen under og fortjener å nevnes først: flere av dokumentets mekanismer finnes
som **bibliotekkode med ekte tester, men uten noen vei inn fra brukeren**. Det er verken «bygget»
eller «ikke bygget», og det er den vanligste tilstanden i denne gjennomgangen. Der bruker jeg
DELVIS BYGGET og navngir manglende koblingsledd.

---

## 1. Sammendrag i tall

Av 42 vurderbare påstander (artefakter, steg, delsteg, rolleeierskap og «aldri gjør»-punkter):

| | Antall |
|---|---|
| BYGGET | 7 |
| DELVIS BYGGET | 13 |
| IKKE BYGGET | 13 |
| LÅST SOM DESIGN | 4 |
| STRIDER MOT KODEN | 5 |

Hovedbildet: **dokumentets datamodell er i stor grad på plass; dokumentets prosess er i liten grad
det.** Registrene finnes (Tjeneste, Begrep, Kodeliste, Vilkår, Regelnode, Unntak, Datasett) med full
CRUD, statusløp, versjonering og proveniens. Det som mangler er nesten alltid ett av tre:
*et koblingsledd* (parser uten import-endepunkt), *en klassifisering* (ingen normtype, ingen
`Registertype`), eller *en utgang* (ingen eksport i det hele tatt).

De fem STRIDER-punktene er verdt å lese først, fordi de er de eneste stedene der dokumentet ikke
bare er forut for koden, men uenig med den: Steg 1s kommunale forskrift-import, Steg 2s
dekningsgaranti, Steg 4s tomt-felt-regel, Steg 4/§6s per-felt-proveniens, og Steg 7s
«ingenting går ut uten godkjenning».

---

## 2. Om dokumentets opphav — les det som en omskriving av `docs/15`, ikke som nye krav

Dette er ikke en kritikk av dokumentet, men det endrer hvordan det bør leses.

Prosessdokumentets bærende prinsipp i §6 — *«systemet finner ikke opp strukturen … Systemets jobb er
å samle det som finnes og gjøre det maskinlesbart … Der struktur ikke finnes, faller det tilbake på
loven, som alltid har den»* — er ord for ord samme prinsipp som `docs/15-handbok-dokumentgraf-notat.md`
§0 («Høst struktur — ikke generer den», med samme fallback-til-loven-begrunnelse). Videre gjenkjennes:

| Prosessdokumentet | Allerede besluttet i |
|---|---|
| Steg 6/7s «sperre i eksporten», bare tjeneste går ut | `docs/15` §10.2 (`Registertype`, LÅST) |
| Steg 4s «et felt uten kilde skal stå tomt» | `docs/15` §5.4 (`FeltkildeEntitet`) |
| Steg 5s tre funn-kategorier | `docs/15` §5.3 (differansen) |
| Steg 3s lukkede klassifiseringsliste | `docs/15` §6.2 |
| Steg 8s re-henting og diff | `docs/15` §6.6 |
| §4s rettslige status per kilde | `docs/15` §3.3 (splittet i to akser, LÅST) |
| §7s lovlighetskontroll mot nasjonale tak | `docs/15` §6.5 |

**Konsekvens:** dokumentet er i hovedsak en rollevendt fremstilling av beslutninger som alt er tatt
i dette repoet. Det er en styrke — det betyr at det ikke innfører ny arkitektonisk retning og ikke
krever en ny avklaringsrunde. Men det betyr også at «systemet gjør X» gjennomgående skal leses som
«`docs/15` besluttet X», og det er grunnen til at fire punkter under havner i LÅST SOM DESIGN framfor
BYGGET.

**Praktisk risiko, verdt å si rett ut:** dokumentet er skrevet i presens om et system som på flere
sentrale punkter ikke gjør dette ennå. Vist til en kommune eller en ekstern part uten forbehold, vil
det bli lest som en beskrivelse av en fungerende løsning. Særlig Steg 2s dekningsgaranti, Steg 4s
tomt-felt-regel og §6s «hver opplysning viser kilde og setning» er formulert som egenskaper systemet
har, mens de i dag er egenskaper det er besluttet at systemet skal ha.

---

## 3. Artefaktene (prosessdokumentets §1)

### 3.1 Tjenestebeskrivelser i CPSV-AP-NO, publisert eksternt — **DELVIS BYGGET**

Registeret er reelt og modent. `TjenesteEntitet` (`src/RegelIde.Data/Entiteter.cs:337-373`) har
CPSV-AP-NO-feltene `KompetentMyndighet`/`Output`/`Tjenestetype`/`Malgruppe`/`Kanaler`/`Kostnad`/
`Behandlingstid`/`Kontaktpunkt`/`KonsekvensVedBrudd`/`Sprak`, med 5-verdis statusløp, versjonering og
`ErstatterId`. Full CRUD i `src/RegelIde.Api/Program.cs:822-932`, tjeneste i
`src/RegelIde.Data/TjenesteregisterTjeneste.cs`, frontend i `TjenesterListe.tsx`/`TjenesteDetalj.tsx`.

**Gapet er «publiseres eksternt».** Det finnes ingen eksport av noe format:

- Null treff på `\brdf\b|RDF` i `src/`. Treff på `Cpsv|SKOS|turtle|jsonld` er utelukkende
  dokumentkommentarer eller strengfeltet `SkosUrl`.
- Null treff på et eksport-endepunkt blant de ~110 rutene i `Program.cs`.
- `BegrepEntitet.SkosUrl` (`Entiteter.cs:487`) er en inert streng som sendes `null` fra hver eneste
  seed og fra KI-veien (`BegrepsforslagTjeneste.cs:101`).
- Fire CPSV-AP-NO-konsepter er bevisst umodellert (`docs/14-byggesteg5-teknisk-design.md` §7):
  `cpsv:hasParticipation`, `cpsv:hasInput`, `dct:spatial`, `dct:requires`-vs-`hasPart`.

`AknXmlSkriver.cs` er ikke et motargument: den serialiserer den *importerte lovteksten* som steg 7 i
importpipelinen, og resultatet lagres i `RettskildeEntitet.AknXml` (`RettskildeImportTjeneste.cs:64,87,180`).
Den går én vei og berører ikke Tjeneste eller Begrep.

### 3.2 Begrepskatalog (SKOS), publisert eksternt — **DELVIS BYGGET**

`BegrepEntitet` (`Entiteter.cs:475-499`) med `Term`/`Definisjon`/`LovreferanseEid`/`Begrepstype`
(`faktabegrep`/`handlingsbegrep`, Schartum 7.3.3-7.3.4), validering av `LovreferanseEid` mot faktiske
noder, KI-forslagsflyt, full CRUD (`Program.cs:1091-1230`). Samme gap som over: ingen SKOS-serialisering.

Ett konkret underpunkt fra dokumentet mangler helt: *«med kommunen som definisjonsmyndighet»*. Det
finnes ingen definisjonsmyndighet-felt på `BegrepEntitet`. `VirksomhetId` sier hvem som *eier raden*,
ikke hvem som er definisjonsmyndighet for begrepet — for et lokalt begrep sammenfaller de, for et
nasjonalt begrep kommunen bare gjenbruker gjør de det ikke.

### 3.3 Håndbok — strukturert form, nettside og PDF — **DELVIS BYGGET**

Forfatterflyten er bygget og reell: `HandbokForfatterTjeneste.OpprettHandbokAsync`
(`src/RegelIde.Data/HandbokForfatterTjeneste.cs:31-65`) oppretter en `RettskildeEntitet` med
`Kildetype="Rundskriv"`, deretter kapitler, kommentarseksjoner, redigering, versjonshistorikk,
lovreferanser, revisjonsmerking og publisering per seksjon (`Program.cs:606-815`). Kommentarmetadata
med `Dokumenttype`/`Bindende`/`FesteNiva`/`Status`/`Marginord`
(`Entiteter.cs:194-227`). Tekst saneres serverside (`KommentarTekstSanering`).

Tre gap:

- **Publisering som nettside for innbyggere: IKKE BYGGET.** «Publiser» er et statusflagg per
  kommentarseksjon (`HandbokForfatterTjeneste.cs:407-421`), ikke en render. Det finnes ingen
  håndbok-visningsside i frontend i det hele tatt — lesing skjer i `RettskildeDetalj.tsx`, som
  brancher på `kildetype === 'Rundskriv'` (`RettskildeDetalj.tsx:426,521,571-587`) og viser
  forfatter-UI-et.
- **PDF-eksport: IKKE BYGGET.** `PdfPig` (`src/RegelIde.Data/RegelIde.Data.csproj:23`) er eneste
  PDF-pakke i løsningen og kan bare lese — `TestFilFixtures.cs:12` sier det eksplisitt. Null treff på
  `QuestPDF|iText|Puppeteer|WeasyPrint`; ingen PDF-bibliotek i `package.json`; null treff på
  `window.print`.
- **Retningslinjene «er ikke lenger en PDF»: forutsetter et import-endepunkt som ikke finnes** — se
  §5.2 og §5.4 under.

### 3.4 Forvaltningsoppgaveregister (internt) — **LÅST SOM DESIGN**

Dette er den reneste forekomsten av kategorien. `docs/15` §10.2/§8 låser løsningen ned til
feltnivå: én `TjenesteEntitet` med diskriminator, feltnavnet **`Registertype`** (ikke `Objekttype`),
non-nullable, ingen default, med `ForvaltningsoppgaveShape`/`TjenesteShape` og en strukturell
eksportsluse.

I koden: **null treff** på `Registertype|registertype` i `src/**/*.{cs,ts,tsx}`. Av 30 treff på
`Registertype|Forvaltningsoppgave|Oppgavetype|ErTjeneste` i hele repoet ligger 29 i `docs/13` og
`docs/15`; det ene kodetreffet er en doc-kommentar (`Entiteter.cs:941`) som nevner begrepet mens den
begrunner noe annet. Ingen kolonne i `RegelIdeDbContext.cs`, ingen av de 20+ migrasjonene, ingen
`types.ts`-felt. `TjenesteEntitet.Tjenestetype` (`Entiteter.cs:349`) er et *annet* felt (CPSV
tjenestetype, f.eks. «Enkeltvedtak» i `FasitRunde4Seed.cs:190`) — `docs/15` §8 punkt 8 advarer
selv mot forvekslingen.

### 3.5 Etterlevelsesrapport — **IKKE BYGGET**

Null treff på `etterlevelse` (case-insensitivt) i `src/**/*.{cs,ts,tsx}`. Ingen spørring, ingen
endepunkt, ingen side identifiserer «plikt uten tjeneste» eller «tjeneste uten plikt».

Verdt å presisere hvorfor dette er mer enn en manglende rapport: **det finnes ikke noe å spørre
med.** Ingen bestemmelse er klassifisert som plikt (§5.3), og ingen rad er merket som
forvaltningsoppgave (§3.4). Differansen i `docs/15` §5.3 forutsetter begge. Dokumentet kaller dette
«sannsynligvis den mest verdifulle leveransen i hele prosessen» — det er også den som er lengst unna.

*Nærmeste slektning i koden er test-only og noe annet:*
`RundskrivReproduksjonTests.cs:251` skriver en `| Seksjon | Dekning i dag |`-tabell til testoutput
fra en håndskrevet liste (`:272-278`), og sammenligner generert veiledningstekst mot en fixture —
ikke plikter mot tjenester.

### 3.6 Regelmodell — vilkår, unntak og skjønnsmomenter — **BYGGET**

Her underdriver dokumentet. Regelmodellen er den mest utbygde delen av Regel-IDE, ikke et framtidig
«grunnlag for vedtaksstøtte»:

- `VilkarEntitet` (`Entiteter.cs:600-655`) med `Vilkarstype` (formell/materiell), `Vurderingstype`
  (`regelbasert`/`skjonnsbasert`/`hybrid`), `SkjonnsgrunnlagBegrepId`, `SkjonnsmomenterJson`,
  `ParametreJson`, `KreverDokumentasjon`, `Eskaleringsrolle`, `ErFormel`.
- `RegelnodeEntitet` (`:670-699`) med `BarnOperator` OG/ELLER/IKKE, `ErRotnode` (INV-5),
  `InnvilgelseTekst`/`AvslagTekst`; polymorf `RegelnodeBarnEntitet` (`:702-715`) med `Rekkefolge`.
- `UnntakEntitet` (`:721-747`) med INV-3/INV-4 håndhevet i `UnntaksregisterTjeneste.cs:37-56`,
  inkludert syklussjekk.
- Grafeditor (`vilkarstre/VilkarstreGraf.tsx`), veiledningsvisning
  (`GET /api/tjenester/{id}/veiledning`, `Program.cs:1804`), kommentarer per node
  (`VilkarstreKommentarEntitet`).

At dokumentet plasserer dette som siste, interne artefakt er en rimelig prioritering ut fra
publiseringsformålet, men det gir et misvisende bilde av hvor Regel-IDE har mest ferdig maskineri.

---

## 4. Rollefordelingen (prosessdokumentets §2)

Rollematrisen er organisatorisk og kan ikke «bygges», men den forutsetter at systemet kan
representere hvem som avgjorde hva. Der er statusen delt:

| Forutsetning | Status |
|---|---|
| Roller finnes i modellen | **BYGGET** — `Bruker.Rolle` ∈ Fagansvarlig/Jurist/Systemforvalter/Saksbehandler (`Entiteter.cs:43`) |
| Handlinger tilskrives en navngitt person | **BYGGET** — `OpprettetAv`/`SistEndretAv` på alle registre; `ProveniensEntitet.EndretAv`+`Dato` (`Entiteter.cs:801-802`) |
| Rollen håndheves ved beslutninger | **IKKE BYGGET** — ingen autorisasjonssjekk på rolle noe sted; `Bruker.Rolle` leses ikke av noen endepunktsguard |
| Juristbeslutningene er egne, registrerbare handlinger | **IKKE BYGGET** — se §6 |

RBAC-matrisen i `docs/03-domenemodell.md` §2 er altså modellert som data, men ikke håndhevet. En
informasjonsforvalter kan i dag utføre samtlige handlinger dokumentet tilordner juristen, uten at
noe i koden skiller.

---

## 5. Stegene (prosessdokumentets §3)

### 5.1 Steg 0 — Avgrens omfanget — **DELVIS BYGGET**

Det finnes et omfangsbegrep, men på håndbok-nivå og ikke på tjenesteområde-nivå:
`HandbokRettskildeomfangEntitet` (`Entiteter.cs:462-469`) med API (`Program.cs:786-820`) deklarerer
hvilke rettskilder en håndbok som helhet omhandler — nøyaktig Steg 0s spørsmål, i én avgrenset form.

To gap: (a) det er ikke håndhevet — `docs/13-backlog.md` §3 sier selv «Håndbok-nivå rettskildeomfang
håndheves ikke — ingen varsel om en kobling/kommentar peker utenfor det deklarerte omfanget»; (b) det
finnes et *annet*, uavhengig omfangsvalg i KI-flyten, der brukeren plukker rettskilder per kjøring
(`TjenesteforslagKo.tsx`, `Program.cs:959`). Dokumentets «én gang per tjenesteområde» har ingen motpart.

Kildegrunnlaget for gjennomgangseksempelet er derimot reelt til stede: alkoholloven,
alkoholforskriften, serveringsloven og forvaltningsloven ligger som ekte fixtures i
`data/kilder/raw-lovdata/`, og Bergens/Tønsbergs/Venneslas retningslinjer i `data/kilder/`.

### 5.2 Steg 1 — Få kildene inn

Dette steget har fire påstander med fire forskjellige statuser.

**(a) Nasjonale lover og forskrifter hentes automatisk fra Lovdata — BYGGET.**
`POST /api/rettskilder/lovdata` (`Program.cs:552-588`) → `LovdataBulkHenter.HentRaaHtmlAsync`
(`LovdataBulkHenter.cs:34-69`, laster ned bulk-arkivet fra `api.lovdata.no/v1/publicData/get/…`) →
`LovdataKonverterer.Konverter` → `RettskildeImportTjeneste.ImporterAsync`, som skriver
`RettskildeNodeEntitet`-treet med foreldrelenker (`:108-158`) og kryssreferanser med
`Opprinnelse="import"` (`:153`). Idempotent, med versjonering ved endret innhold. Søkbar katalog
over datokoder (`LovdataKatalogTjeneste`, 24t lat fornying) fjerner kravet om at brukeren kjenner
datokoden (`Program.cs:590`).

**(b) «Kommunens egen forskrift likeså — den er kunngjort» — STRIDER MOT KODEN.**
Dette er ikke bare ubygget; det er avvist i tre uavhengige lag:

1. `LovdataBulkHenter` kjenner bare de to *nasjonale* arkivene (`:16-17`), og `DatokodeMønster`
   godtar kun `LOV|FOR` (`:19`) — `LF-…` kan ikke matche.
2. `LovdataIdentifikatorer.AvledEliFraDatokode` kaster `FormatException` for alt annet enn
   `^(LOV|FOR)-…` (`:18,30-35`).
3. `RettskildeImportTjeneste.TolkKildetypeFraEli` kjenner bare `/eli/lov/` og `/eli/forskrift/`, og
   kaster `NotSupportedException` med kommentaren «Ingen gjettet fallback (§3.3)» (`:305-311`).

Endepunktet hardkoder `virksomhetId: null` med begrunnelsen at bulk-datasettet «per definisjon kun
inneholder nasjonale Lov/Forskrift» (`Program.cs:581-583`). UI-et sier det til brukeren:
«Nettsidens HTML-format for lokale forskrifter (lovdata.no/dokument/LF/…) er **ikke** støttet ennå»
(`Importer.tsx:235-236`). Fil-opplastingsveien hjelper ikke — `POST /api/rettskilder/fil`
(`Program.cs:520-550`) mater samme `LovdataKonverterer` og godtar bare Lovdatas «XML-kompatible
HTML» (`accept=".html,text/html"`, `Importer.tsx:242`).

For gjennomgangseksempelet betyr det at *kommunens egen forskrift om salgs-, skjenke- og
åpningstider* — én av de syv kildene Steg 0 lister — ikke kan komme inn i systemet i dag.

**(c) «Retningslinjene lastes opp som fil; systemet leser dokumentets egen kapittel- og
punktnummerering og bygger et navigerbart tre» — DELVIS BYGGET, og dette er gjennomgangens skarpeste
enkeltgap.**

Parseren er ekte og god. `HandbokTekstParser` (`src/RegelIde.Kildekonvertering/HandbokTekstParser.cs`)
er ren regex-segmentering, «INGEN KI, INGEN HTML-parsing» (`:81-84`):

- kapittel: `^Kapittel\s+(\d{1,2})\.?(?:\s*[-–.]\s*(.+))?$` (`:144`)
- versalt tallpunkt-kapittel for forskrifter: `^(\d{1,2})\.\s+([A-ZÆØÅ][A-ZÆØÅ0-9,.\-\s]*)$` (`:170`)
- punkt: `^(\d{1,2}(?:\.\d{1,2}){1,2})\.?(?:\s+(.*))?$` (`:180`) — `4.3`, `3.4`, `4.1.2`, med
  `\d{1,2}` per segment nettopp for å ikke lese `DD.MM.ÅÅÅÅ` som et trenivå-punktnummer
- sidebrytnings- og kolofonfiltrering (`:121-138`) så tekst på hver side av et sideskift blir én node
- eId-er *er* dokumentets egne numre: `kap4`, `kap4/pkt4.1` (`:18-21,280-296`)

Den er regresjonstestet mot **to ekte Bergen-dokumenter** (`data/kilder/raw-handbok/`):
retningslinjene SD-24-113 og forskriften.

Gapet: **parseren har ingen produksjonskaller.** Repo-vidt grep gir bare klassen selv, tre
doc-kommentarer og tester (`HandbokTekstParserTests.cs`, `HandbokTekstParserEdgeCaseTests.cs`,
`BergenForskriftParserTests.cs`, `NettsideDokumentgrafTests.cs:230`). Null referanser i
`RegelIde.Api` eller `RegelIde.Data`; ikke DI-registrert. Koden sier det selv
(`HandbokTekstParser.cs:10-12`): `RettskildeImportTjeneste` «projiserer denne til
`RettskildeNodeEntitet`-rader ved en **fremtidig** import-kobling … **intet import-endepunkt**».
`docs/13-backlog.md:458` bekrefter: «Ikke gjort: … import-endepunkt for håndbøker».

To ekstra ledd mangler i samme kjede, og de er verdt å skille:

- Parseren tar en **`string`**, ikke en fil (`Parse(string raaTekst)`, `:98`). Den har ingen
  PDF-leser. PDF→tekst finnes (`KunnskapsbibliotekTekstUtvinner`, PdfPig + OpenXml), men i en
  *annen* pipeline — se (d).
- `HandbokNode` er en egen record-type, og det finnes **ingen mapper** fra `HandbokNode` til
  `RettskildeNodeEntitet` noe sted. Testen innrømmer det: håndbok-raden er «SEEDET her for å
  representere hva et fremtidig håndbok-import-endepunkt ville skrevet — IKKE bygget»
  (`NettsideDokumentgrafTests.cs:203-205`).

**«Originalfilen beholdes uendret som det rettslige artefaktet» — IKKE BYGGET.**
Feltene finnes: `RettskildeEntitet.Innhold` (bytea), `InnholdsHash`, `Url`, `Hentet`, `HttpEtag`,
`HttpLastModified` (`Entiteter.cs:93-106`). De settes av **ingen produksjonskode** — kun i en test
(`NettsideDokumentgrafTests.cs:215`). Rå bytes lagres på ett sted i hele løsningen, og det er den
andre pipelinen: `KunnskapsbibliotekFilEntitet.Innhold` (`Entiteter.cs:995`, skrevet i
`KunnskapsbibliotekTjeneste.cs:54`). Det er altså ikke slik at originalen ligger ved siden av det
parsede treet — det finnes ikke noe parset tre, og originalen ligger i et register hvis eneste
formål er KI-kontekst.

**(d) Den disconnected pipelinen, eksplisitt.** Dette fortjener å stå for seg, fordi det er lett å
tro at fil-opplasting og håndbokstrukturering er samme funksjon:

`POST /api/kunnskapsbibliotek/filer` (`Program.cs:1060-1084`) →
`KunnskapsbibliotekTjeneste.LeggTilFilAsync` (`:35-62`) → `KunnskapsbibliotekTekstUtvinner` →
**flat, sammenslått sidetekst** (`string.Join("\n", dokument.GetPages().Select(side => side.Text))`,
`KunnskapsbibliotekTekstUtvinner.cs:48`) → én rad med `Innhold` + `UtvunnetTekst`. Ingen noder,
ingen eId-er, ingen hierarki, ingen kryssreferanser, ingen kall til `HandbokTekstParser`. Eneste
konsument: `TjenesteforslagTjeneste` dumper teksten i prompten som `## {tittel}\n{UtvunnetTekst}`
(`:171-179`).

Så: **laster en informasjonsforvalter opp Bergens retningslinjer som PDF i dag, blir de flat
KI-kontekst — ikke en navigerbar håndbok.** Begge halvdeler av dokumentets setning finnes i repoet;
de snakker bare ikke med hverandre.

**(e) «Nettsider og operative dokumenter registreres som kilder de også» — DELVIS BYGGET.**
Kunnskapsbibliotek-lenker og -filer er bygget med API og UI (`Program.cs:1009-1090`).
Nettside-dokumentgrafen er bygget som *skjema og parser*: `NettsideDokumentEntitet`/
`NettsideStiEntitet`/`NettsideLenkeEntitet` (`Entiteter.cs:876-985`), `NettsideTekstParser`,
`NettsideGrafKobler`, med 23 ekte Bergen-sider som fixtures og et kjernebevis om at en nettside
kobles helt frem til importerte rettskilder på ELI. Men: **null API-flate og null frontend** — grep
på `Nettside` i `src/RegelIde.Api/` gir null treff, og ingen DI-registrering av `NettsideGrafKobler`.
Ingen henter finnes heller (`NettsideTekstParser.cs:51-52`: «ingen `NettsideHenterTjeneste` er
bygget»). Korpuset er testdata.

**(f) «Her stopper systemet og spør juristen om rettslig status per kilde» — IKKE BYGGET.**
Se §6.1 for full behandling. Kort: feltet finnes, populeres kun av to testfiler, og finnes ikke i
noen DTO eller UI. `PATCH /api/rettskilder/{id}/metadata` (`Program.cs:332-346`) godtar utelukkende
`Kortnavn` og `Utgiver`. Det finnes ingen stopp-og-spør-mekanisme noe sted i importflyten.

### 5.3 Steg 2 — Kartlegg pliktene i loven

**Normtype-klassifiseringen — IKKE BYGGET.** `Normtype` som feltnavn: null kodetreff (eneste
forekomst er `docs/13-backlog.md:375`). Søk på `Plikt|plikt|Kompetanse|Forbud|forbud` i
`src/**/*.{cs,ts,tsx}` gir 8 treff, alle delstrenger i norsk prose eller paragrafoverskrifter
(«meldeplikt», «veiledningsplikt», `"Bevillingsplikt"` i `AlkohollovenKonverteringTests.cs:185`) —
null som klassifiseringsverdi.

Viktig presisering: `RettskildeEntitet.NormativVirkning` (`Entiteter.cs:116`) ligger på **hele
rettskilden**, ikke på bestemmelsen. `RettskildeNodeEntitet` — den per-bestemmelse-tabellen som
faktisk populeres (`RettskildeImportTjeneste.cs:113-130`) — har ingen normtype-kolonne i det hele
tatt. Dokumentets Steg 2 opererer på et granularitetsnivå skjemaet ikke har.

**«Systemet går gjennom hver enkelt bestemmelse, én for én» og «dekningen er etterprøvbar» —
STRIDER MOT KODEN.** Dette er den mest konsekvensfulle uoverensstemmelsen, fordi dokumentet selv
utpeker den som juristens forsikring: *«Systemet hopper ikke over noe og velger ikke ut det som
virker relevant … kan dere dokumentere at hver bestemmelse i alkoholloven er vurdert.»*

Faktisk mekanisme, `RettskildeKontekstHjelper.ByggKontekstAsync`
(`src/RegelIde.Data/RettskildeKontekstHjelper.cs:32-48`): alle noder med tekst i de valgte
rettskildene hentes, konkateneres til **én blob** (`[{eId}] {tekst}` per linje) og sendes i **ett**
modellkall (pluss maks ett retry ved tomt svar). Modellen returnerer så mange tjenester den vil.
Ingen løkke over bestemmelser, ingen chunking, ingen lengdetrimming (klassekommentar `:10-13`).

RAG-varianten er *strengere utvelgende*, ikke mer systematisk:
`RagKontekstHjelper.cs:54-60` beholder kun topp-K etter kosinuslikhet, der `k` kommer rett fra
HTTP-body (`antallNoder`, `Program.cs:983`). Noder under kuttet er helt fraværende fra prompten, og
`:69` hopper over hele rettskilder som ikke bidrar med en topp-K-node. `KosinusLikhet` returnerer
stille 0 ved dimensjonsavvik (`:85-98`) — en tyst dekningssvikt.

**Ingen dekningssporing finnes.** Ingen entitet, kolonne eller spørring registrerer hvilken
`RettskildeNode` en agent har vurdert. `KildeReferanserJson` inneholder kilde-*dokument*-GUID-er,
ikke node-dekning. Nærmeste slektning, `TekstTaggEntitet.KreverGjennomgang` (`Entiteter.cs:325`),
handler om re-ankring av tagger etter reimport, ikke om KI-vurdering.

Dette er kjent og kostnadsberegnet ubygget arbeid, ikke en glipp: `docs/15` §8 punkt 9 setter
prislappen til «~65 kall × ~2 000 tokens ≈ 130 000 input-tokens mot 29 000. Fire–fem ganger dyrere,
mot bevisbar dekning» og ber om at avveiningen tas eksplisitt. `docs/13` §2.7 sier «ikke bygget,
ikke tatt stilling til».

**Forslag/godkjenn-mønsteret som Steg 2 forutsetter — BYGGET, men for to andre entitetstyper.**
Mekanismen er reell: `Status="foreslatt_av_ai"` på entiteten
(`TjenesteregisterTjeneste.cs:105`, `BegrepsregisterTjeneste.cs:78`), proveniensrad med
`Handling="foreslatt_av_ai"` + `AiForslagVersjon` (`ProveniensHjelper.cs:29-43`), kø-visning
(`Program.cs:934-957`), og Avvis/Rediger/Godkjenn i UI (`TjenesteforslagKo.tsx:118-130`). Den dekker
**kun Tjeneste og Begrep** — bekreftet strukturelt: bare `TjenesteregisterTjeneste.cs:17` og
`BegrepsregisterTjeneste.cs:13` har `"foreslatt_av_ai"` i `GyldigeStatuser`; `VilkarregisterTjeneste.cs:21`,
`RegelnoderegisterTjeneste.cs:15`, `UnntaksregisterTjeneste.cs:14` og `KodelisteregisterTjeneste.cs:15`
har det ikke. Det finnes ingen forslagsflyt for en plikt, en normtype eller en adressat, fordi det
ikke finnes noe å foreslå *til*.

**«Delegasjonsreglementet gjør en jobb her» — IKKE BYGGET.** `Delegasjon|delegasjon` gir **ett**
treff i hele `src/`, og det er en doc-kommentar som sier at arbeidet ikke har startet:
«AKSE B — feltet finnes, forblir nullable til delegasjonsreglement-arbeidet starter (§3.3)»
(`Entiteter.cs:118`). Null treff på `AnsvarligOrgan|AnsvarligMyndighet`. Ingen organ-entitet, ingen
kodeliste over organer, ingen resolver.

Feltet dokumentet sikter til, `TjenesteEntitet.KompetentMyndighet` (`Entiteter.cs:346`), fylles av
nøyaktig tre veier, ingen av dem en delegasjonsoppslag: manuell fritekst
(`TjenesteDetalj.tsx:367` → `Program.cs:852,874`), seed med hardkodet `"Testkommunen"`
(`Byggesteg2InnholdSeed.cs:120`, `FasitRunde4Seed.cs:190`), og **uvalidert LLM-gjetning** fra
prompten «hvilken myndighet/virksomhet som er ansvarlig for tjenesten»
(`TjenesteforslagTjeneste.cs:56` → `:84` → `:224`). Dokumentets egen formulering — «uten den er
feltet gjetning» — beskriver dermed presist hva feltet er i dag.

### 5.4 Steg 3 — Strukturér retningslinjene

Segmenteringen er dekket i §5.2(c). De fem klassifiseringene, hver for seg:

| Klassifisering | Status |
|---|---|
| **Lokal legaldefinisjon → begrep** | **DELVIS BYGGET.** `BegrepsforslagTjeneste` (`:51-105`) foreslår Term/Definisjon/Begrepstype/LovreferanseEid mot *valgte rettskilder*, med eId validert mot `db.RettskildeNoder` og stille nullet hvis ukjent (`:93-97`). Fordi håndbøker forfattet i verktøyet *er* `RettskildeNodeEntitet`-rader, kan en håndboknode i prinsippet være mål — men aldri for et opplastet, parset dokument (§5.2c). Ingen mekanisme klassifiserer et punkt *som* en definisjon; agenten foreslår begreper, ikke punkt-typer. |
| **Lokalt forbud** | **IKKE BYGGET.** `forbud|Forbud` gir ett treff i `src/`, en testkommentar (`RundskrivReproduksjonTests.cs:220`). Ingen entitet, felt, tagg-kind eller promptomtale. |
| **Unntakshjemmel** | **DELVIS BYGGET, men på en annen akse.** `UnntakEntitet` finnes, men INV-3/INV-4 krever at den peker på en **`Regelnode`** i et vilkårstre og på en betingelse (`UnntaksregisterTjeneste.cs:37-48`). Det finnes **ingen** `RettskildeNodeId`/`Eid` på `UnntakEntitet` og ingen vei fra et dokumentpunkt til et Unntak. Påstanden «punkt 3.4 er en unntakshjemmel» kan altså ikke registreres som en egenskap ved punktet — bare som en node i en regelgraf noen først må bygge. |
| **Parameter** | **DELVIS BYGGET, men utviklerseedet.** To mekanismer: `VilkarEntitet.ParametreJson` (`Entiteter.cs:622`), skrevet fra API-body (`VilkarregisterTjeneste.cs:60,101`), dvs. håndtastet; og den ekte «endre verdien, ikke regelen»-mekanismen `DatasettVerdiEntitet` (`Entiteter.cs:585-594`) — `KommunaleParametreSeed.cs:34-44` setter tre verdier for samme `klokkeslett.tidspunkt`-felt (Tønsberg, Bærum, nasjonal default med `VirksomhetId=null`), hver med fritekst-kildehenvisning. Det *er* dokumentets parameterbegrep, men det er seed-data skrevet av en utvikler bak en `if (… Navn == "Tønsberg kommune")`-guard (`:22`). Ingen kode leser et tall ut av et retningslinjepunkt. |
| **Kryssreferanse systemet kan følge** | **DELVIS BYGGET — detekteres, persisteres aldri.** `HandbokTekstParser.KryssreferanseMønster` (`:395`) `punkt\s+(\d{1,2}(?:\.\d{1,2}){1,2})` løser «punkt 4.7» mot dokumentets eget eId-register og dropper stille uløste (`:418-431`); `HjemmelMønster` (`:390`) fanger «jf. alkoholloven § 1-7 d» med `TilEid: null` fordi GUID-oppslaget er DB-avhengig (`:412`). `RettskildeReferanseEntitet` kan bære begge kanttypene uendret (konkludert i `docs/13:456`) — men **ingen kode skriver dem**. `Opprinnelse` settes til `"import"` kun av Lovdata-pipelinen (`RettskildeImportTjeneste.cs:153`) og `"manuell"` kun av håndbokens manuelle lovreferanse-handling (`HandbokForfatterTjeneste.cs:320`). Merk også `docs/13` §2.2-korreksjonen: **ingen lateral kryssreferansegraf mellom paragrafer finnes for lover heller** — kun vertikal `ParentNodeId`. |

Sluttpåstanden — «de kan navigeres, siteres punktvis, publiseres som nettside, og eksporteres
tilbake til PDF» — er **IKKE BYGGET** i tre av fire ledd: navigering forutsetter import (§5.2c),
nettsidepublisering finnes ikke (§3.3), PDF-generering finnes ikke (§3.3). Punktvis sitering er
derimot reelt for innhold som *er* i basen: `TekstTaggEntitet` (`Entiteter.cs:292-326`) lagrer
`StartOffset`/`EndOffset`/`QuotePrefix`/`QuoteExact`/`QuoteSuffix`/`NodeTekstHash` med re-ankring ved
reimport — den mest presise sitatmekanismen i hele kodebasen.

### 5.5 Steg 4 — Høst de operative opplysningene

**Feltene finnes — BYGGET.** `Kostnad`/`Behandlingstid`/`Kontaktpunkt`/`Kanaler`/`Sprak` på
`TjenesteEntitet` (`Entiteter.cs:350-355`), og KI-agenten fyller dem alle siden runde 3
(`docs/14` §7, prompt `TjenesteforslagTjeneste.cs:60-66`, deserialisering `:83-87`).

**«Systemet henter det derfra [nettsidene og gebyrregulativet]» — IKKE BYGGET.**
Det finnes ingen uttrekk av gebyr, behandlingstid, søknadskanal, kontaktpunkt, skjema eller språk
fra en nettside noe sted. `NettsideTekstParser.Parse` (`:82-106`) gjør presis to ting: beregner
innholdshash (`:89`) og trekker ut Markdown-lenker (`:95-102`) klassifisert som `Lovdatalenke` eller
`LenkerTil`. Ingen regex, felt eller KI-steg for de operative verdiene; `NettsideGrafKobler` er
lenkegraf alene. Og som nevnt i §5.2(e) er nettsidegrafen ikke nåbar fra den kjørende applikasjonen
i det hele tatt. Merk også at tjeneste-agenten ikke ser `NettsideDokumentEntitet.RaaTekst` — dens
kontekst er kunnskapsbiblioteklenker (kun URL + beskrivelsesstreng, `TjenesteforslagTjeneste.cs:165-170`)
og -filer.

De to faktiske kildene til disse feltene er: **uvalidert LLM-gjetning** og **manuell tasting**
(`TjenesteDetalj.tsx:34-35` → `Program.cs:853-854` → `TjenesteregisterTjeneste.cs:52-57`).

**«Den viktigste kvalitetsregelen … et felt uten kilde skal stå tomt» — STRIDER MOT KODEN, og det
er målt i dette repoet.**

Intensjonen er i prompten. `TjenesteforslagTjeneste.cs:52-53`: «kun "Tittel" er obligatorisk, resten
skal være null hvis konteksten ikke gir tydelig belegg (dikt ikke opp)». `BegrepsforslagTjeneste.cs:45-46`
tilsvarende.

Håndhevingen i kode gjelder **kun referanser, aldri verdier**. `OpprettForslagFraKiAsync` validerer
én ting: at `Tittel` ikke er tom (`TjenesteregisterTjeneste.cs:84-87`). Hvert annet felt lagres
ordrett som modellen leverte det, uten noe krav om kilde. Det som *faktisk* verifiseres er at en
påstått **referanse** løser til en ekte node — `BegrepsforslagTjeneste.cs:94-97` nuller en
ubekreftbar eId med en god begrunnelse i kommentaren (`:88-92`: «det er ikke en gjettet
fallback-VERDI, det er et bevisst valg om å ikke lagre et sitat vi ikke kan bekrefte»), og
`TjenesteforslagTjeneste.cs:244-245` dropper uoppløselige eId-er. Det mønsteret er riktig og verdt å
bygge videre på. Men en hallusinert `Behandlingstid` uten noen referanse lagres uten innvending, og
testen fastholder nettopp det skillet
(`BegrepsforslagTjenesteTests.cs:108-128`: `Hallusinert_eId_dropper_kun_den_referansen_ikke_hele_batchen`).

Og utfallet er dokumentert empirisk, ikke bare teoretisk mulig. `docs/13` §2.2 R1(a), et søk i selve
alkoholloven-fixturen etter verdiene agenten fylte inn:

- **Ekte lovtekst**, ikke konfabulert: «4 måneder»-fristen (§ 1-7a), «årlig bevillingsgebyr»
  (§ 6-8/§ 6-9), «Inndragning av bevilling» (§ 1-8).
- **Ikke i lovteksten i noen form**: `kanaler` (`"fysisk"`/`"digitalt"` på **samtlige** forslag) og
  `sprak` (`"norsk"` på samtlige) — null treff på noe tilsvarende. `kontaktpunkt` er kun en kopi av
  `kompetentMyndighet`, uten egen kilde. R1(b) samme dag: 14 av 14 forslag fikk
  `["digitalt","fysisk"]`.

Backlogens egen konklusjon: «dump-alts 83 % feltfullstendighet … er delvis kunstig høy fra
ugrunngede default-verdier». `docs/15` §5.4 er enda mer direkte: feltfullstendigheten «sannsynligvis
var oppdiktet fordi kanaler, behandlingstid og kontaktpunkt ikke står i en lov».

Dokumentet skriver: *«Alternativet — at systemet fyller inn noe sannsynlig — gir en beskrivelse som
ser komplett ut og er feil, og det er verre enn et hull dere kan se.»* Det er en presis beskrivelse
av hva koden gjør i dag.

**«Hvert utfylt felt viser hvilken side eller hvilket dokument verdien kom fra, og hvilken setning» —
STRIDER MOT KODEN.**

`FeltkildeEntitet` er den designede løsningen (`docs/15` §5.4, med `EierType`/`EierId`/`Feltnavn`/
`KildeType`/`KildeRef`/`Utdrag`) — **LÅST SOM DESIGN, IKKE IMPLEMENTERT**; `docs/15` §8 Trinn 2 sier
«R2/§5.4 (`FeltkildeEntitet`) gjenstår».

Det som finnes i dag, `KildeReferanserJson`, er tre ting mindre enn påstanden:

1. **Per batch, ikke per forslag.** Strengen beregnes én gang *utenfor* løkken over forslag
   (`TjenesteforslagTjeneste.cs:214-219`) og den *samme* strengen skrives til hvert forslag i
   kjøringen (`:226`).
2. **Hele dokumenter, ikke setninger.** Innholdet er `{ rettskildeIder, lenkeIder, filIder }` — bare
   GUID-er. Ingen eId, ingen node-id, ingen tegn-offset, intet sitat. For Begrep er den enda grovere:
   `{ rettskildeIder }` (`BegrepsforslagTjeneste.cs:82`).
3. **Per entitet, ikke per felt.** `ProveniensEntitet` (`Entiteter.cs:792-807`) er nøklet på
   `EntitetType` + `EntitetId`; det finnes ingen feltkolonne. `Tjeneste.Behandlingstid` bærer derfor
   ingen egen kildehenvisning.

Den svarer altså på «hvilke dokumenter var valgt for denne kjøringen», ikke «hvor kom denne verdien
fra». Og den **vises ingen steder**: feltet er med i DTO-en (`Dtos.cs:418`) og i `types.ts:662`, men
rendres ikke — køen viser Tittel/Beskrivelse/KI-versjon/Handlinger (`TjenesteforslagKo.tsx:238-241`).

Presisering for balansens skyld: den *ene* mekanismen i kodebasen som gjør nøyaktig det dokumentet
beskriver — eksakt sitat med prefiks/suffiks, tegn-offset og teksthash, med re-ankring ved reimport —
er menneskelig tagging (`TekstTaggEntitet`, `TekstTaggTjeneste.cs:70`). KI-flyten oppretter aldri en
`TekstTagg`. Byggematerialet finnes; koblingen gjør ikke.

### 5.6 Steg 5 — Koble plikter mot tjenester — **IKKE BYGGET som beskrevet**

Koblingsmaskineriet finnes: `TjenesteRegelverksreferanseEntitet` (`Entiteter.cs:376-382`) knytter
tjeneste → rettskilde-node, `TjenesteavhengighetEntitet` (`:429-449`) knytter tjeneste → tjeneste med
syklussjekk, og KI-agenten kan foreslå begge (`TjenesteforslagTjeneste.cs:240-268`, E#/T#-konvensjon).

Men Steg 5 forutsetter **to lister**, og den ene finnes ikke. «Hva loven pålegger (steg 2)» er ikke
representert noe sted (§5.3). Uten den kan ingen av de tre funnene beregnes:

- *Plikt uten tjeneste* — krever pliktlisten. IKKE BYGGET.
- *Tjeneste uten plikt* — **delvis innen rekkevidde i dag.** En Tjeneste uten
  `TjenesteRegelverksreferanse`-rader er spørrbar, og motsatt retning finnes allerede som endepunkt:
  `GET /api/rettskilder/{id}/referert-av-tjenester` (`Program.cs:379-381`). Ingen kode presenterer
  det som et etterlevelsesfunn, men grunnlaget er der. Dette er det billigste reelle steget mot
  etterlevelsesrapporten — se §9.7.
- *Tjeneste som finnes men ikke er beskrevet* — IKKE BYGGET; forutsetter nettside-høsting (§5.5).

### 5.7 Steg 6 — Klassifiser og valider

**Tjeneste vs. forvaltningsoppgave — LÅST SOM DESIGN.** Se §3.4. Dokumentets praktiske prøve («kan du
forestille deg en kanal der en søker ville forsøkt å motta dette») er ordrett Finlands
inklusjonsregel som `docs/15` §5.2 gjengir og deretter kaller «riktig for deres formål og
utilstrekkelig for vårt».

**«Systemet kjører deretter maskinell validering» — IKKE BYGGET.** Det finnes ingen
valideringstjeneste og intet valideringsendepunkt: grep på `Valider|validert` i `Program.cs` gir
**ingen treff**.

Hva som faktisk styrer `utkast → validert → publisert`: en streng-medlemskapssjekk, ingenting mer.

```csharp
// TjenesteregisterTjeneste.cs:187-190
if (!GyldigeStatuser.Contains(nyStatus))
{
    throw new ArgumentException($"Ukjent status '{nyStatus}'. ...");
}
```

Ingen tilstandsmaskin, ingen forgjenger-sjekk, ingen påkrevde felt — v1-forenklingen er notert i
koden selv (`:14-15`: «ikke full FSM-håndheving av lovlige overganger i v1»). Konsekvensene er
konkrete og direkte imot dokumentets to valideringsregler:

- Dokumentet: *«Tjenester må ha kanal og et resultat søkeren mottar.»* I koden er `Kanaler` og
  `Output` nullable (`Entiteter.cs:347,350`) og sjekkes aldri ved publisering; `Kanaler` tvinges til
  tom liste framfor å avvises (`TjenesteregisterTjeneste.cs:99`).
- Dokumentet: *«Forvaltningsoppgaver … skal ikke ha kanal eller gebyr.»* Ikke uttrykkbart — det
  finnes ingen forvaltningsoppgave (§3.4).
- Ingen DB-backstop heller: `RegelIdeDbContext.cs` har `HasCheckConstraint` for `rettskilder`
  (`:127,129`), `kodelister` (`:511`), `regelnoder` (`:657`), `unntak` (`:703`), `nettside_lenker`
  (`:830`) med flere — men **ingen for `tjenester` eller `begreper`**.

**«Poster som ikke passer noen av formene, går til gjennomgang framfor å bli forkastet stille» —
DELVIS, og delvis motsatt.** Avvis i køen setter status til `'utkast'` og **beholder raden**
(`TjenesteforslagKo.tsx:119`) — det er i dokumentets ånd. Men koden forkaster faktisk stille det den
ikke forstår, på referansenivå: uoppløselige eId-er droppes uten spor
(`TjenesteforslagTjeneste.cs:245`, `BegrepsforslagTjeneste.cs:94-97`), og hele batchen beholdes. Det
er et bevisst og forsvarlig valg, men det er «kaster stille», ikke «går til gjennomgang».

### 5.8 Steg 7 — Godkjenn og publiser

**«Alt systemet har foreslått, står merket som forslag inntil et menneske godkjenner det» —
DELVIS BYGGET, med en reell uoverensstemmelse.**

Merkingen er ekte og synlig: `Status="foreslatt_av_ai"` vises i statuskolonnen i den vanlige
tjenestelisten (`TjenesterListe.tsx:68,80`). Men det finnes **ingen forslagskø-tabell** — KI-en
skriver en ekte domenerad umiddelbart, med `Entitetsstatus="gjeldende"` fra opprettelsen
(`TjenesteregisterTjeneste.cs:78-113`). Og `ListerForAsync` filtrerer **kun** på `Entitetsstatus`,
ikke på `Status`:

```csharp
// TjenesteregisterTjeneste.cs:19-23
db.Tjenester
    .Where(t => t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende")
```

Følgene: forslaget er i registeret fra sekundet det oppstår, og det mates til neste agent-kjøring som
en «eksisterende tjeneste» E1/E2 (`TjenesteforslagTjeneste.cs:156-160`). UI-ets egen påstand — «må
godkjennes eksplisitt før de blir gjeldende» (`TjenesteforslagKo.tsx:140-141`) — er ikke det koden
gjør. Det er en tekst som bør rettes uavhengig av denne vurderingen.

To ting KI-en oppretter er dessuten **ikke merket som forslag i det hele tatt**: en
AI-generert `Tjenesteavhengighet` får `Handling="opprettet"`
(`TjenesteavhengighetregisterTjeneste.cs:116`), og `KobleRegelverksreferanseAsync` skriver **ingen
proveniensrad overhodet** (`TjenesteregisterTjeneste.cs:149-173`).

**«Godkjenningen registreres med navn og tidspunkt» — DELVIS BYGGET.**
Skjemaet er der: `ProveniensEntitet.EndretAv`/`Dato`/`GodkjentAv` (`Entiteter.cs:801-806`),
`ProveniensHjelper.NyRad` med `Dato = DateTimeOffset.UtcNow` (`:20`).

Ett sted i kodebasen **håndheves** en navngitt godkjenner, og det er godt bygget:

```csharp
// HandbokForfatterTjeneste.cs:410-413
if (metadata.Bindende && string.IsNullOrWhiteSpace(godkjentAv))
{
    throw new ArgumentException("Bindende seksjoner krever en registrert godkjenner før publisering.");
}
```

Endepunktet skiller reelt mellom to identiteter — `body.GodkjentAv` (godkjenneren) og `bruker.Navn`
(den handlende), `Program.cs:775`. Det er akkurat mønsteret dokumentet ber om.

For Tjeneste og Begrep finnes ingen tilsvarende sperre. `SettStatusRequest` har
`string? GodkjentAv = null` (`Dtos.cs:111`) — helt valgfritt — og verdien lagres uendret
(`TjenesteregisterTjeneste.cs:198-201`). En Tjeneste kan gå fra `foreslatt_av_ai` rett til
`publisert` i ett kall, uten godkjenner. Frontend fyller i praksis inn den innloggede selv
(`godkjentAv: gjeldendeBruker?.navn`, `TjenesteforslagKo.tsx:129`), så `EndretAv == GodkjentAv` ved
konstruksjon. Det er ikke et to-personers-mønster — men dokumentet krever heller ikke det, og
«navngitt godkjenning med tidspunkt» *er* oppfylt for den flyten. Det som ikke er oppfylt, er at det
skal være **umulig** å gå videre uten den.

**«Bare det som er klassifisert som tjeneste går ut i den eksterne tjenestekatalogen … det er en
sperre i eksporten» — IKKE BYGGET, dobbelt.** Det finnes verken en klassifisering (§3.4) eller en
eksport (§3.1). Sperren er beskrevet i `docs/15` §10.2 («Bare `Registertype = tjeneste` emitteres som
`cpsv:PublicService`»), med krav om at den gjøres strukturell via dedikerte repository-grensesnitt
pluss en regresjonstest — LÅST SOM DESIGN.

### 5.9 Steg 8 — Hold det levende — **IKKE BYGGET**

Ingen periodisk mekanisme finnes: **null treff** på
`AddHostedService|BackgroundService|IHostedService|PeriodicTimer` i `src/**/*.cs`, og null reelle
treff på `Hangfire|Quartz|Cron|setInterval`. De fire `Task.Delay`-treffene er retry-backoff.
Null treff på `Varsel|Varsling|Notifikasjon|Overvak` som funksjonalitet. `docs/13` §6 bekrefter:
«ingen bakgrunnsjobb finnes fortsatt». Eneste tidsstyrte ting i hele løsningen er
`LovdataKatalogTjeneste`s 24-timers, late fornying av **katalogtitler** — aldri innhold
(`LovdataKatalogTjeneste.cs:13,34-51`).

Det som *finnes*, og som er verdt å kjenne presist fordi det er nesten-nok:

`RettskildeImportTjeneste` ved reimport (`:33-52`) — og merk at sammenligningen ikke er hash-basert
slik `docs/15` §6.6 antyder, men en **full strengsammenligning** av regenerert AKN-XML med
importdato-linjen strippet (`NormaliserAknForSammenligning`, `:255`). Uendret ⇒ stille no-op (`:46`).
Endret ⇒ ny versjonsrad, `ErstatterId`, gammel rad `Entitetsstatus="erstattet"`, proveniensrad
`Handling="endret"` (`:165-198`). Deretter re-ankres tagger, og ved manglende eller flertydig treff
settes **`tagg.KreverGjennomgang = true`** (`:246`).

`KreverGjennomgang` er det nærmeste systemet kommer dokumentets «får dere varsel om **hvilke**
klassifiseringer som hviler på det som ble endret» — men det er en passiv boolean per tagg, eksponert
i tagg-DTO-en (`Dtos.cs:86,90`), uten push, innboks eller diff-visning, og **kun for menneskelige
tagger** — ikke for Tjeneste, Begrep eller Vilkår som hviler på den endrede noden.

Feltene for betinget re-henting er også allerede der og ubrukte: `Url`, `HttpEtag`,
`HttpLastModified`, `Hentet` (`Entiteter.cs:93-106`) — ingen produksjonskode setter dem (§5.2c), og
ingenting poller dem.

Selve påvirkningsanalysen dokumentet beskriver er byggesteg 8 i veikartet, eksplisitt utenfor MVP
(`docs/13-backlog.md:20-21`).

---

## 6. De fire beslutningene juristen eier alene (prosessdokumentets §4)

### 6.1 Rettslig status per kilde — **LÅST SOM DESIGN, og uleselig for systemet**

Taksonomien er avklart og delvis låst: `docs/15` §3.3/§8 splitter den i to ortogonale akser framfor
én tredeling, og `Entiteter.cs:108-121` implementerer begge som kolonner —
`NormativVirkning` (`bindende_borger`/`bindende_forvaltning`/`vektbaerende`/`faktisk_praksis`) og
`FunksjonellRolle` (`materiell_norm`/`kompetansenorm`/`prosessnorm`/`gebyr_okonomi`/`tolkning`).
Feltet er bevisst nullable fordi Schartum-spørsmålet står åpent (`:112-115`).

Tre observasjoner, i økende alvor:

1. **Populeres kun av tester.** Uttømmende grep: `RettsligStatusKontrastTests.cs:61,71` og
   `NettsideDokumentgrafTests.cs:216`. Null produksjonstilordninger — ikke i
   `RettskildeImportTjeneste` (begge konstruksjonssteder, `:77-94` og `:170-189`), ikke i
   `HandbokForfatterTjeneste.OpprettHandbokAsync` (`:40-60`).
2. **Finnes ikke i API eller UI.** Null treff i `Dtos.cs`, `Program.cs`, `types.ts`. `PATCH
   /api/rettskilder/{id}/metadata` godtar bare `Kortnavn` og `Utgiver` (`Program.cs:332-346`).
   **Juristens viktigste beslutning kan altså ikke registreres gjennom applikasjonen i det hele
   tatt.** Hva `RettsligStatusKontrastTests` faktisk beviser er at *kolonnen kan lagre to
   forskjellige verdier testen selv skrev inn* — dens egen header er ærlig om det (`:12-13`: feltene
   «er ALDRI faktisk blitt populert/testet før nå»).
3. **Ingenting vekter noe etter den.** Dokumentets begrunnelse er at statusen «avgjør hvor tungt
   systemet lar dokumentet veie senere» og at «systemet må vite forskjellen for ikke å behandle en
   retningslinje som om den var en forskrift». Kontekstbyggeren ignorerer feltet fullstendig:
   `RettskildeKontekstHjelper.cs:24-49` emitterer `# {Tittel}` + `[{eId}] {tekst}` per node, sortert
   på `RettskildeId` og `Sorteringsrekkefolge` — ingen `Kildetype`, ingen `NormativVirkning`, ingen
   vekting. Ingen av de to systeminstruksene (`TjenesteforslagTjeneste.cs:42-77`,
   `BegrepsforslagTjeneste.cs:29-47`) nevner normativ kraft eller rettskildehierarki.

Det eneste som i dag likner kildevekting er `RettskildeEntitet.Importrolle`
(`primaer`/`referanse`, `Entiteter.cs:69`), og den styrer reimport-atferd, ikke hvordan KI-en veier
teksten.

### 6.2 Normtype per bestemmelse — **IKKE BYGGET**

Se §5.3. Null kodetreff på `Normtype`; ingen normtype-kolonne på `RettskildeNodeEntitet`.

### 6.3 Hvem som er rett adressat — **IKKE BYGGET**

Se §5.3. `KompetentMyndighet` er fritekst fra manuell tasting, seed eller LLM-gjetning; ingen
organ-entitet, ingen delegasjonsmekanisme, og `cpsv:hasParticipation` (organisasjon + rolle som egen
struktur) er eksplisitt umodellert (`docs/14` §7).

### 6.4 Om et hull er et brudd — **IKKE BYGGET**

Krever hull-listen fra Steg 5, som krever pliktlisten fra Steg 2. Ingen av delene finnes. Skillet
dokumentet gjør mellom «kan» og «skal» — f.eks. at alkoholloven § 4-5s ambulerende bevilling er
valgfri mens tilsyn ikke er — har ingen representasjon: det finnes ikke noe felt som skiller en
pliktig fra en valgfri forvaltningsoppgave, fordi det ikke finnes noen forvaltningsoppgave.

### 6.5 Den femte, i grenselandet — retningslinjer som binder bort skjønnet — **IKKE BYGGET, men vokabularet er der**

Kontrollen finnes ikke. Men det er verdt å merke at ontologien uvanlig godt kan *uttrykke* funnet:
`VilkarEntitet.Vurderingstype` skiller `regelbasert`/`skjonnsbasert`/`hybrid`, med
`SkjonnsgrunnlagBegrepId` og `SkjonnsmomenterJson` (`Entiteter.cs:621-628`), og `UnntakEntitet`
representerer «med mindre …»-hjemmelen. «Et skjønnsbasert vilkår uten noe tilknyttet Unntak» er
altså et *spørrbart mønster* så snart innholdet finnes — kontrollen mangler, ikke begrepsapparatet.
Nærmeste slektning i planene er lovlighetskontrollen mot nasjonale tak (`docs/15` §6.5), som er
Trinn 4 punkt 13 og ubygget.

---

## 7. De fire informasjonsforvalteren eier (prosessdokumentets §5)

**Kildens aktualitet — DELVIS BYGGET.** `Ikrafttredelse`, `KonsolidertDato`, `GyldigFra`/`GyldigTil`,
`Status` (`Gjeldende`/`Opphevet`/`Utkast`), `Hentet`, `Versjon`, `Entitetsstatus`
(`Entiteter.cs:74-86,104`) — datamodellen bærer aktualitet godt, og `Opphevet`/`OpphevetDato` per
node (`:169-170`) er også der. Det som mangler er at noe *bruker* det: ingen foreldelsesvarsling,
ingen «denne kilden er ikke sjekket siden X»-visning, ingen re-sjekk (§5.9).

**Feltmapping — DELVIS BYGGET, og bedre enn dokumentet antar på ett punkt.** «At behandlingstid havner
i rett CPSV-egenskap og ikke i fritekst» er strukturelt løst: `Behandlingstid` *er* en egen typet
kolonne (`Entiteter.cs:352`), ikke et fritekstfelt, og agenten skriver til den direkte. Men uten
eksport finnes ingen CPSV-egenskap å mappe *til*, og fire konsepter har ingen kolonne i det hele tatt
(`docs/14` §7). Mappingen er altså halvveis gitt av skjemaet og halvveis ikke-eksisterende.

**Beskrivelsens brukbarhet / sjargongflagging — IKKE BYGGET.** `docs/13-backlog.md:405` er eksplisitt
om dimensjon B: «Ingen sperre mot vage formuleringer i fritekst; hører til en fremtidig
skrive-veiledning/lint, ikke et strukturelt gap». Ingen lesbarhetsanalyse, ingen sjargongliste, ingen
målgruppetilpasning finnes.

**Publiseringsomfanget — IKKE BYGGET.** «Hva går ut, i hvilken katalog, på hvilket språk» forutsetter
en utgang; det finnes ingen (§3.1). `Sprak[]`-feltet finnes, men fylles i praksis av agentens
ubegrunnede `"norsk"` (§5.5).

---

## 8. «Hva systemet aldri gjør» (prosessdokumentets §6)

Dette er dokumentets mest eksponerte avsnitt, fordi det er formulert som garantier.

| Påstand | Vurdering |
|---|---|
| «Det finner ikke opp opplysninger. Et felt uten kilde står tomt.» | **STRIDER MOT KODEN.** Målt: `kanaler`/`sprak` på samtlige forslag uten sporbarhet til noen kildetekst; `kontaktpunkt` kopierer `kompetentMyndighet`. Prompten sier «dikt ikke opp»; kodevalideringen krever kun ikke-tom `Tittel`. Se §5.5. |
| «Det avgjør ikke rettslig status, normtype eller adressat. Det foreslår, og forslaget må godkjennes.» | **Formelt sant, men tomt.** Systemet avgjør dem ikke fordi det ikke representerer dem (§6.1-6.3). For det det *faktisk* foreslår — Tjeneste og Begrep — er menneskeporten reell og bygget. Adressat er et delvis unntak: `KompetentMyndighet` **fylles** av LLM-gjetning uten kilde og uten validering (§5.3), altså nærmere «avgjør» enn «foreslår». |
| «Det publiserer ikke. Godkjenning er et menneskelig, navngitt steg.» | **DELVIS.** Første setning er trivielt sann — ingen eksport finnes. Andre setning er ikke håndhevet for Tjeneste/Begrep: `GodkjentAv` er valgfritt, ingen FSM, `foreslatt_av_ai → publisert` i ett kall. Håndhevet kun for bindende håndbokseksjoner (§5.8). |
| «Det skjuler ikke hvor noe kom fra. Hver opplysning viser kilde og den setningen den ble lest fra.» | **STRIDER MOT KODEN.** Proveniens er per batch, per entitet, og på hele-dokument-GUID-nivå — ingen eId, ingen offset, intet sitat — og rendres ingen steder i UI. Setningsnivå finnes kun i menneskelig tagging, som KI-flyten ikke bruker. Se §5.5. |
| «Det kaster ikke det det ikke forstår. Poster som ikke passer, går til gjennomgang framfor å forsvinne.» | **DELVIS.** Sant for forslag (avvis beholder raden som `utkast`). Usant for referanser: uoppløselige eId-er droppes stille og sporløst, ved design. Se §5.7. |
| «Systemet finner ikke opp strukturen … høster det som finnes.» | **BYGGET som prinsipp, og dokumentets sterkeste treff.** Det er ordrett `docs/15` §0, og det er realisert deterministisk i tre parsere: `LovdataHtmlParser` (lovens egen paragrafinndeling), `HandbokTekstParser` (dokumentets egen nummerering, med eId-er som *er* «punkt 4.7»), `NettsideTekstParser` (stier og lenker). Ingen KI oppdager struktur noe sted. Ærlig forbehold: den høstede håndbokstrukturen kommer ikke inn i databasen ennå (§5.2c), og «loven som fallback» er reell nettopp fordi den er den ene kilden som *er* importerbar. |

---

## 9. Prioritert liste — mest fremdrift per krone

Sortert etter gapets størrelse ganget med hvor godt tiltaket bygger på kode som alt finnes. Alle
åtte er avgrensede, ikke big-bang — samme stil som resten av `docs/13-backlog.md`.

**1. `FeltkildeEntitet` — proveniens per felt (`docs/15` §5.4).**
Høyest gevinst per innsats i hele listen. Én additiv tabell pluss en skriving i
`OpprettForslagFraKiAsync` gjør tre av dokumentets mest eksponerte påstander sanne i stedet for
usanne: Steg 4s tomt-felt-regel, §6s «viser setningen», og Steg 7s etterprøvbarhet. Målingen som
begrunner den er alt gjort (R1(a)/R1(b)), så det er ingen utredning igjen. Gjenbruk
`TekstTaggEntitet`s `QuotePrefix`/`QuoteExact`/`QuoteSuffix`/`NodeTekstHash`-triplett, som allerede
løser re-ankring ved reimport — ikke en ny sitatmekanisme. Håndhev deretter regelen én vei først:
*sekundærfelt uten `Feltkilde`-rad skrives ikke*. Det alene fjerner `kanaler`/`sprak`-konfabuleringen.

**2. Import-endepunkt for håndbok/retningslinjer: fil → `RettskildeNode`-tre.**
Parseren er ferdig og regresjonstestet mot to ekte Bergen-dokumenter, men nås bare fra tester. Det
som mangler er kort og kjent: en `HandbokNode → RettskildeNodeEntitet`-mapper, et endepunkt, og
kobling av PdfPig (allerede en avhengighet) som tekstuttrekk foran parseren. Dette er
**forutsetningen for det meste av Steg 3** — definisjoner, parametere og kryssreferanser detekteres
allerede, men har ingen rad å skrives til. Lagre samtidig originalbytene i de eksisterende, i dag
døde `Innhold`/`InnholdsHash`/`Url`-kolonnene, slik at «originalen som rettslig artefakt» faktisk
stemmer.

**3. `NormativVirkning` i DTO, endepunkt og UI — obligatorisk ved lokal kilde-import.**
Billigste punktet på listen: kolonne, migrasjon og verdiliste finnes alt; det som mangler er et
DTO-felt, en utvidelse av `PATCH /metadata` (som i dag bare tar `Kortnavn`/`Utgiver`) og et
skjemafelt. Uten det kan juristens beslutning nr. 1 ikke registreres gjennom applikasjonen i det hele
tatt. Gjør det obligatorisk kun for kilder med `VirksomhetId` satt — der er taksonomien avklart nok —
og la den åpne Schartum-vurderingen for retningslinjer generelt stå.

**4. `Registertype` på `TjenesteEntitet` + strukturell eksportsluse-test.**
LÅST til feltnivå i `docs/15` §10.2: én non-nullable kolonne, ingen default, to former, dedikerte
repository-grensesnitt og én regresjonstest som seeder en `forvaltningsoppgave`-rad og beviser at den
ikke kommer med. Lav kodekostnad, høy semantisk gevinst: det gjør §8.4s «3 av 6 tvilsomme som egne
CPSV-tjenester» til lagrbare funn framfor feil. Merk navnekollisjonsadvarselen mot det eksisterende
`Tjenestetype`.

**5. Krev godkjenner ved statusovergang for Tjeneste/Begrep — gjenbruk håndbok-mønsteret.**
`HandbokForfatterTjeneste.cs:410-413` håndhever alt presis dette for bindende seksjoner. Å løfte de
samme tre linjene inn i `SettStatusAsync`, pluss en forgjenger-tilstandssjekk som stenger
`foreslatt_av_ai → publisert`, lukker gapet mellom dokumentets «ingenting går ut uten navngitt
godkjenning» og dagens valgfrie `GodkjentAv`. Rett samtidig UI-teksten i
`TjenesteforslagKo.tsx:140-141`, som i dag lover mer enn koden holder, og vurder om et forslag bør
være `Entitetsstatus="forslag"` framfor `"gjeldende"` — det er den underliggende årsaken til at
teksten er feil.

**6. Sveip over én lov, paragrafgranularitet, med persistert dekningslogg.**
Dette er dokumentets mest distinktive løfte og i dag dets mest usanne. `docs/15` §8 punkt 9 har alt
kostnadsberegnet det (~65 kall, ~130k input-tokens mot 29k) og ber om at avveiningen tas eksplisitt —
den beslutningen er det egentlige arbeidet her, ikke koden. Avgrens hardt til normtype per paragraf i
alkoholloven alene, og persister én «vurdert»-rad per node. Foreldrekontekst er gratis i dag via
`ParentNodeId`/`Overskrift`/`Nummer`; den laterale henvisningsutvidelsen er det **ikke** (ingen
kryssreferansegraf mellom paragrafer finnes), så hold den utenfor første runde.

**7. Etterlevelsesdifferansen i svak form, nå.**
Ikke vent på pliktregisteret for hele leveransen. «Tjeneste uten hjemmel» er spørrbar i dag — en
Tjeneste uten `TjenesteRegelverksreferanse`-rader — og motsatt retning finnes alt som endepunkt
(`GET /api/rettskilder/{id}/referert-av-tjenester`). Én liste og én skjerm gir informasjonsforvalteren
et reelt etterlevelsesfunn med dagens data. «Plikt uten tjeneste»-halvdelen venter på punkt 4 og 6.

**8. Endringsvarsel som pull, ikke push.**
Mesteparten av Steg 8s verdi krever ingen scheduler. Reimport versjonerer allerede og setter allerede
`KreverGjennomgang` på berørte tagger. Utvid det flagget til Tjeneste/Begrep/Vilkår hvis en refererad
eId endret seg, og eksponer én «hviler på endret kilde»-liste. `HttpEtag`/`HttpLastModified`-kolonnene
for betinget re-henting finnes alt ubrukt, så en manuell «sjekk kildene nå»-knapp er et lite steg
videre — og et vesentlig billigere første steg enn en `BackgroundService`.

*Ikke på listen, bevisst:* CPSV-AP-NO-eksport (RDF/SKOS) og PDF-generering. Begge er reelle gap og
begge er forutsetninger for dokumentets eksterne artefakter — men de er nye avhengigheter og nytt
maskineri uten eksisterende motpart i koden, og de leverer lite før punkt 1-4 har gjort innholdet
verdt å publisere. En eksport av dagens data ville publisert nettopp de ubegrunnede feltene punkt 1
fjerner.
