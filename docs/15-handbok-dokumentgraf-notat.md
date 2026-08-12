# 15. Notat: Håndbøker, dokumentgraf og automatisert struktur­høsting

*Status: forslag til beslutning, ikke besluttet. Bør få en egen avklaringsrunde slik ontologilåsen
fikk (jf. `13-backlog.md` §2.6/§2.7). Mottatt fra Claude Chat 2026-08-12, konsolidert mot koden samme
dag — se §0.1 og de **[KORRIGERT 2026-08-12]**-merkede stedene under. Forutsetter og bygger videre på
analysen i §2.2s "Mottatt, ikke bygget"-punkt i `13-backlog.md` (v3-rapporten); korrigerer den på ett
punkt (§5.2 erstattes av §10.2 her).*

---

## 0. Bærende prinsipp

**Høst struktur — ikke generer den.**

Ambisjonen har vært formulert som at Regel-IDE «automagisk skal oppsummere alt det gode som finnes
der ute, uten den manuelle innsatsen med å komme opp med strukturene selv». Det er oppnåelig, men
ikke fordi en språkmodell finner opp struktur. Det er oppnåelig fordi **strukturen allerede finnes**
— forfattet av mennesker, bare distribuert og implisitt:

| Struktur som finnes | Hvor | Utvinnbar |
|---|---|---|
| Kapittel-/punktnummerering | Bergens retningslinjer (`4.7`, `8.2`) | Deterministisk |
| Hjemmelshenvisninger | «jf. Alkoholloven §1-7 d», «I medhold av alkohollovens § 4-5» | Deterministisk |
| Interne kryssreferanser | «det vises til retningslinjenes punkt 4.7» | Deterministisk |
| Definisjoner | «Med «hoteller» menes …» | Deterministisk mønster, KI-verifisert |
| Dokumentversjon og gyldighet | `Dok.nr: SD-24-113`, `Rev.nr.: 01`, `Gyldig til: 01.07.2028` | Deterministisk |
| Tjenestetaksonomi | Kommunens nettstedsmeny | Deterministisk |
| Kompetent myndighet | Organisatorisk URL-sti | Deterministisk |
| eId på lovnivå | Lovdata-import (byggesteg 1) | Ferdig |

Der struktur mangler, kan den ikke høstes — og da er **loven fallback**, fordi den er den eneste
universelt strukturerte kilden. Det er hele begrunnelsen for at identitet må komme fra rettskilden
og ikke fra nettsidene.

KI-ens rolle blir dermed smal og pålitelig: **klassifisere og justere høstet struktur**, aldri
oppdage eller finne opp den.

### 0.1 Presisering — prinsippet gjelder også notatets EGET skjemaforslag [KORRIGERT 2026-08-12]

Ironisk, men verdt å si rett ut: §0s prinsipp — strukturen finnes allerede, den skal leses, ikke
gjenoppfinnes — gjelder like mye for hvordan Regel-IDE selv er bygget som for Bergens retningslinjer.
Sjekket direkte mot `src/RegelIde.Data/Entiteter.cs`, migrasjonshistorien og
`RettskildeImportTjeneste.cs` før noe av §2/§3.3 legges til grunn for en avklaringsrunde:

| §2/§3.3s forslag | Finnes allerede som | Kommentar |
|---|---|---|
| `HandbokKildeEntitet.Innhold`/`InnholdsHash` | `RettskildeEntitet.AknXml` | Kanonisk serialisering finnes allerede for byggesteg-1-importert innhold (Lov/Forskrift) |
| `HandbokNodeEntitet.LokalEid`/`Nummer`/`Overskrift`/`ForelderId` | `RettskildeNodeEntitet.Eid`/`Nummer`/`Overskrift`/`ParentNodeId` | Ordrett samme felt, samme rolle |
| `HandbokNodeEntitet.SeksjonsHash` | `RettskildeNodeEntitet.TekstHash` | Kodekommentar (2026-07-26): *"kun i bruk for håndbok/rundskriv-noder"* — bygget for nøyaktig dette bruksområdet allerede |
| Hash-basert endringsdeteksjon ved reimport (§6.6) | `RettskildeImportTjeneste`s reimport-versionering | Rask vei: uendret eId + `TekstHash` → ingen endring. Implementert, testet |
| Node-nivå versjonering (forberedelse til Lag 3) | `RettskildeNodeEntitet.Versjon`/`Entitetsstatus`/`ErstatterNodeId` | Finnes allerede |
| `LokalRettskildeEntitet.Dokumenttype`/generell kildetyping | `RettskildeEntitet.Kildetype` | **Fri streng, INGEN CHECK-constraint** i databasen — "Rundskriv" og "Virksomhetsdokument" er allerede brukte verdier. Testkommunens seed-data har en rettskilde med tittel *"Alkoholpolitiske retningslinjer for Testkommunen 2024-2028"*, `Kildetype="Virksomhetsdokument"`, `VirksomhetId` satt — det ER Bergen-eksempelet, bare norsk og allerede i systemet |

**Konsekvens for §11s åpne spørsmål** *"Håndbok som forfattet artefakt vs. importert kilde — er det
samme entitet med ulik proveniens, eller to ting?"*: koden har allerede svart. Det er **samme
entitet** (`RettskildeEntitet`/`RettskildeNodeEntitet`), brukt både for import (`LovdataHtmlParser`
→ AKN → relasjonell tre) og forfatting (`HandbokForfatterTjeneste.OpprettBladNodeAsync` skriver
direkte til `RettskildeNodeEntitet`). Spørsmålet kan lukkes — se §11 under.

**Hva som FAKTISK er nytt** (gapet notatet peker på er ekte, bare feil sted i skjemaet):

1. En generisk PDF/nettside-segmenteringsparser (`^Kapittel \d+`/`^\d+\.\d+`-regex) — i dag finnes
   kun `LovdataHtmlParser.cs`, spesifikk for Lovdatas eksportformat. Bør produsere vanlige
   `RettskildeNodeEntitet`-rader, ikke en ny tabellfamilie.
2. URL-basert henting med endringsdeteksjon (`Url`/`HttpEtag`/`HttpLastModified`/`Hentet`) —
   `RettskildeEntitet` har ingen slike felt i dag (Lovdata-import går via en annen mekanisme:
   `LovdataBulkHenter`/katalogsøk, ikke generisk URL-henting).
3. **`RettsligStatus`** (forskrift/politisk_vedtak/administrativ_praksis) — genuint nytt felt, ikke
   dekket av `Kildetype` (som svarer på *hva slags dokument*, ikke *hvilken rettslig vekt* det har).
   Foreslås som ny kolonne PÅ `RettskildeEntitet` sammen med `VedtattAv`/`Vedtaksdato`/`Saksnummer`/
   `GyldigTil` (merk: `Ikrafttredelse`/`KonsolidertDato` finnes allerede og dekker delvis samme
   behov for nasjonale rettskilder) — ikke en helt separat `LokalRettskildeEntitet`-tabell. Om
   `Kommunenummer` bør ligge her eller hentes fra `VirksomhetEntitet` er ikke sjekket.
4. **AKN-eksport FRA relasjonell modell** — `AknXmlSkriver.cs` går i dag kun én vei (Lovdata-HTML →
   AKN, ved import). Håndbok-forfattet/-høstet innhold har ingen AKN-serialisering. Rundturstesten
   i §9.5 er derfor reelt ny og verdifull, ikke allerede dekket.
5. `HandbokAnnotasjonEntitet` (Lag 3, §2) overlapper IKKE med eksisterende
   `HandbokKommentarMetadataEntitet` — de gjør forskjellige ting (kommentarklassifisering +
   bindende-flagg vs. typet annotasjon med kildepeker) og kan begge stå.

---

## 1. Funnet: Bergens retningslinjer *er* allerede en håndbok

Retningslinjene (`https://www.bergen.kommune.no/api/rest/filer/V51903878`, Dok.nr. SD-24-113, Rev.
01, fastsatt av Bystyret 19.06.2024, gyldig 01.07.2024–01.07.2028) har alt en håndbok trenger:

**Hierarkisk, selvsiterende nummerering.** Kapittel 1–10 med punkt `1.1`, `3.4`, `4.10`, `8.6`. Og
avgjørende: dokumentet **siterer seg selv** med denne nummereringen — punkt 4.8 sier «det vises til
retningslinjenes punkt 4.7 for øvrig». Nummereringen *er* altså en etablert, autoritativ eId-ordning.
Den skal ikke finnes opp; den skal leses.

**Eksplisitte hjemler.** Punkt 1.1 viser til alkoholloven § 1-7d, kapittel 7 til § 4-5, punkt 8.6 til
plan- og bygningsloven, kapittel 9 til alkohollovens internkontrollbestemmelser.

**Lokale definisjoner som er SKOS-kandidater.** Punkt 4.2 «spisesteder», 4.3 «hoteller» (minst 30 rom
med dusj/bad, resepsjon), 4.4 «steder med liten eller ingen matservering», 4.5 «selskapslokaler», 4.6
«studentsteder», 8.1 «uteservering». Disse begrepene er *lokalt* definert — Bergens «hotell» er ikke
nødvendigvis Oslos. Det er nøyaktig den federerte semantikk-problematikken prosjektet har som
gjennomgangstema.

**Vilkår, unntak og parametere, tydelig adskilt.** Kapittel 2 lister fire dokumentasjonsvilkår. Punkt
3.4, 4.3 og 8.3 er unntakshjemler («I spesielle tilfeller kan det gjøres unntak …»). Og parametrene
ligger tallfestet: 25 ambulerende bevillinger (kap. 7), 30 hotellrom (4.3), mattilbud til kl. 22.00
(4.2), musikkstopp kl. 22.00 (8.2), kl. 02.00 som terskel for anonym kontroll (9.2), bevillingsperiode
til 30.09.2028 (kap. 10).

**Lokale absolutte forbud, strengere enn loven.** Ingen bevilling til stripping/toppløs-servering
eller ved pengeautomatspill (4.1), ingen til én-prosents MC-klubber (4.10), normalt ingen til
serveringsøyer i kjøpesenter (kap. 5).

**Konsekvens:** spørsmålet «hva gjør vi inntil vi får det strukturert» har et bedre svar enn ventet.
Omtrent 80 % av strukturen er utvinnbar **deterministisk i dag**, uten KI, fordi dokumentet
nummererer seg selv. Det som ikke er deterministisk er *semantisk typing* — er 4.2 en definisjon, er
3.4 et unntak, er 4.10 et forbud — og det er en lukket klassifiseringsoppgave.

---

## 2. Hva applikasjonen skal gjøre nå — tre lag, additivt [KORRIGERT 2026-08-12 — se §0.1]

Kravet er at strukturering senere skal være **anriking, ikke reimport**. Derfor tre lag der hvert
lag kan bygges uten å røre laget under. **Korrigert versjon: lag 1 og 2 er UTVIDELSER av eksisterende
`RettskildeEntitet`/`RettskildeNodeEntitet`, ikke nye tabeller** — se §0.1 for kartleggingen.

### Lag 1 — Bitidentisk original, uforanderlig

Opprinnelig forslag var en ny `HandbokKildeEntitet`. **Korrigert**: dette er nye FELT på
`RettskildeEntitet`, siden `AknXml` allerede er den kanoniske serialiseringen for importert innhold:

```csharp
// Nye felt på RettskildeEntitet, ikke en ny tabell:
Url               string?  // ved henting: eksakt URL — finnes ikke i dag
Innhold           byte[]?  // bytea, uendret original — for et hentet dokument (PDF), i motsetning
                            // til AknXml som er en AVLEDET serialisering
InnholdsHash      string?  // SHA-256 over Innhold — endringsdeteksjonen for kilder som KUN finnes
                            // på et kommunalt nettsted
Hentet            DateTimeOffset?
HttpEtag          string?
HttpLastModified  string?
```

Originalen er det rettslige artefaktet. Den muteres aldri. `InnholdsHash` er endringsdeteksjonen —
for et dokument som bare finnes på kommunens nettside, er hash-diff **den eneste
versjoneringsmekanismen som eksisterer**. Vedtar bystyret nye retningslinjer, varsler ingen deg.

### Lag 2 — Deterministisk segmentering på dokumentets egen nummerering

Opprinnelig forslag var en ny `HandbokNodeEntitet`. **Korrigert**: `RettskildeNodeEntitet` dekker
dette allerede felt-for-felt (`Eid`≈`LokalEid`, `ParentNodeId`≈`ForelderId`, `TekstHash`≈
`SeksjonsHash`, `Nummer`, `Overskrift`, `Tekst`, `Sorteringsrekkefolge`≈`Posisjon`). Det som er nytt
er PARSEREN som produserer disse radene fra en PDF/nettside, ikke feltene selv:

Segmenteringen er ren regex mot `^Kapittel \d+` og `^\d+\.\d+`, med sidebrytnings-støy
(`Dok.nr.: SD-24-113 Side 3 av 5`) filtrert bort. Ingen KI. En ny parser, sideordnet
`LovdataHtmlParser.cs`, som skriver til det SAMME `RettskildeNodeEntitet`-skjemaet — så
`RagKontekstHjelper`, `RettskildeEmbeddingTjeneste` og eventuelle sveip fungerer uendret, uten
særtilfeller.

`Eid` (utledet av dokumentets egen nummerering, f.eks. `"kap4/pkt4.1"`) er *dokumentets egen*
referanseordning, ikke en syntetisk id. Det gjør at kryssreferansen «punkt 4.7» kan løses
deterministisk, og at et sitat i Regel-IDE kan verifiseres av en jurist mot papiret.

**Der nummerering mangler** (mange kommuner har retningslinjer som løpende prosa): fall tilbake til
overskriftsbasert segmentering med `Eid = "h2-3/h3-1"` og marker `NodeType = "avsnitt"`. Dårligere,
men samme form — ingen egen kodesti, ingen ny tabell.

### Lag 3 — Annotasjonslag, tilføyd senere

```csharp
HandbokAnnotasjonEntitet
    Id              Guid
    HandbokNodeId   Guid     // → RettskildeNodeEntitet.Id, IKKE en ny nodetabell
    Annotasjonstype string   // "vilkar" | "unntak" | "definisjon" | "forbud"
                             // | "parameter" | "hjemmel" | "kryssreferanse"
                             // | "skjonnsmoment" | "kompetansenorm"
    MalRef          string?  // eId i rettskilde, VilkarId, BegrepId, LokalEid
    Verdi           string?  // for parameter: "25", "22:00", "30"
    Enhet           string?  // "antall" | "klokkeslett" | "rom" | "kroner"
    Utdrag          string   // den faktiske teksten annotasjonen hviler på
    Status          string   // gjenbruker GyldigeStatuser + "foreslatt_av_ai"
```

Dette ER genuint nytt — overlapper ikke med eksisterende `HandbokKommentarMetadataEntitet` (som
klassifiserer en kommentar som bindende/ikke, ikke en typet annotasjon med kildepeker). `Utdrag` er
ikke redundant. Det er forskjellen mellom «systemet påstår at Bergen har 25 ambulerende
bevillinger» og «systemet viser setningen det leste det fra». Uten kilde skrives ikke annotasjonen —
samme «ingen gjettet fallback»-prinsipp som eId-fiksen i runde 3.

### Rekkefølgen dette gir

Lag 1 og 2 kan bygges og verifiseres **uten en eneste KI-kall**, og gir umiddelbart verdi: siterbare
noder, endringsdeteksjon, og et korpus som kan chunkes og hentes fra på samme måte som rettskilder —
fordi det NÅ ER samme korpus, samme tabeller. Lag 3 er der KI kommer inn, og kan gjøres inkrementelt
per annotasjonstype uten å røre lag 1–2.

---

## 3. Dokumentgrafen

### 3.1 Nodetyper [KORRIGERT 2026-08-12 — se §0.1]

| Node | Kilde | Identitet |
|---|---|---|
| `Rettskilde` / `RettskildeNode` | Lovdata (byggesteg 1) | eId — nasjonal, kanonisk |
| `Rettskilde` / `RettskildeNode` med `Kildetype="Retningslinje"` e.l. *(var: `LokalRettskilde`)* | Kommunal forskrift (Lovdata) eller retningslinje (kommunens nettsted) | Se 3.3 |
| `Rettskilde` / `RettskildeNode` med `Kildetype="Virksomhetsdokument"`/`"Rundskriv"` *(var: `HandbokKilde`/`HandbokNode`)* | PDF, dokument, veileder | `Eid` (dokumentets egen nummerering) |
| `NettsideDokument` / `NettsideSeksjon` | Kommunens nettsider | URL + overskriftssti |
| `Forvaltningsoppgave` *(eller `Tjeneste` med `Objekttype="forvaltningsoppgave"`, se §10.2)* | Utledet av rettskildesveip | eId + normtype |
| `Tjeneste` | Utledet av oppgave + operative kilder | Egen Guid, aldri fra URL |
| `Begrep` | Definisjoner i alle kildetyper | SKOS |

`NettsideDokument`/`NettsideSeksjon` er fortsatt genuint nye nodetyper — ingen eksisterende tabell
dekker nettsideinnsamling.

### 3.2 Kanttyper, og hva som er deterministisk

| Kant | Fra → til | Utvinning |
|---|---|---|
| `hjemlet_i` | HandbokNode → RettskildeNode | **Deterministisk** — regex på «jf.», «i medhold av», «§ x-y» + lovnavn |
| `kryssrefererer` | HandbokNode → HandbokNode | **Deterministisk** — «punkt 4.7» løses mot `Eid` |
| `lenker_til` | NettsideSeksjon → * | **Deterministisk** — `<a href>` |
| `lovdatalenke` | NettsideSeksjon → RettskildeNode | **Deterministisk** — lovdata.no-URL-er parses til eId-kandidater |
| `forvaltes_av` | Tjeneste → Organisasjonsenhet | **Deterministisk** — se 3.4 |
| `versjon_av` | HandbokKilde → HandbokKilde | **Deterministisk** — `Dok.nr` + `Rev.nr` |
| `presiserer` | HandbokNode → Vilkår | KI (klassifisering) |
| `parameteriserer` | HandbokNode → Vilkår/Regelnode | KI (klassifisering) |
| `dekker` | Tjeneste → Forvaltningsoppgave | KI (matching) |
| `definerer` | HandbokNode → Begrep | KI (verifisering av deterministisk mønstertreff) |
| `presentasjonsvariant` | NettsideDokument → Tjeneste | KI (n:m-oppløsning) |

Ni av tolv kanttyper er deterministiske. Det er hele poenget med prinsippet i §0. Merk: `hjemlet_i`
og `kryssrefererer` kan implementeres som rader i eksisterende `RettskildeReferanseEntitet` (samme
tabell som allerede kobler rettskildenoder til Vilkår/Begrep/Tjeneste, se `RettskildeDetalj.tsx`s
"Referanser"-seksjon) — sjekk dette før en ny kanttabell bygges.

### 3.3 `RettsligStatus` — hullet i sporbarhetskjeden [OMDØPT fra `LokalRettskilde`, KORRIGERT 2026-08-12]

Bergens sidetittel pakker to instrumenter med fundamentalt ulik rettslig status: «Retningslinjer for
tildeling av salgs- og skjenkebevillinger **og** Forskrift om salgs-, skjenke- og åpningstider».

**Forskriften** er hjemlet i alkoholloven, kunngjort i Norsk Lovtidend, ligger på Lovdata, har eId.
Importeres via eksisterende byggesteg 1-løype.

**Retningslinjene** er ikke en forskrift. De er vedtatt av bystyret som politisk styringsdokument
(sammen med Rusplan og Folkehelseplan utgjør de kommunens alkoholpolitiske handlingsplan etter
alkoholloven § 1-7d, slik punkt 1.1 selv sier). De binder forvaltningens skjønn under § 1-7a, men er
ikke kunngjort noe sted utover kommunens eget nettsted.

**Gapet, presist formulert:** Lovdata er nasjonal ELI-koordinator, og ELI er implementert i beta på
lovdata.no (se §9.4). Kommunale *forskrifter* kunngjøres og har derfor sannsynligvis en ELI-URI.
Retningslinjer — bystyrevedtak, ikke forskrift — har ingen: ingen Lovdata-tilstedeværelse, ingen
ELI-URI, ingen kunngjøringsplikt, ingen maskinlesbar versjonering.

Gapet gjelder altså **spesifikt ikke-forskrift lokale instrumenter**, ikke lokale rettskilder
generelt. Det er en snevrere og mer håndterbar mangel, men den treffer nøyaktig der skjønnet bor:
retningslinjene er det som styrer § 1-7a-vurderingen. Hvis digital-rettsstat-premisset er en ubrutt
sporbarhetskjede fra rettskilde til tjeneste, er dette leddet som mangler — og det er et spørsmål
Digdir kan reise med Lovdata, ikke bare et implementasjonsproblem her.

**Korrigert forslag — nye kolonner på `RettskildeEntitet`, ikke en ny tabell** (se §0.1 punkt 3):

```csharp
// Nye felt på RettskildeEntitet:
RettsligStatus      string?    // "forskrift" | "politisk_vedtak" | "administrativ_praksis"
InterntDokNr        string?    // "SD-24-113" — les fra dokumentet når det finnes
Revisjonsnr         string?    // "01"
VedtattAv           string?    // "Bystyret"
Vedtaksdato         DateOnly?  // 2024-06-19
Saksnummer          string?    // bystyresak, når den finnes
GyldigTil           DateOnly?  // 2028-07-01 — merk: Ikrafttredelse/KonsolidertDato finnes allerede
                                // for nasjonale rettskilder, sjekk om de kan gjenbrukes/utvides
HjemmelEid          string?    // "alkoholloven/§1-7d"
```

`Kommunenummer` er UTELATT fra listen over — bør hentes via `VirksomhetId` → `VirksomhetEntitet`
heller enn dupliseres på `RettskildeEntitet`, men dette er ikke sjekket mot `VirksomhetEntitet`s
faktiske felt i denne runden.

Bergen versjonerer faktisk pent — `SD-24-113` + `Rev.nr. 01` + gyldighetsperiode er en brukbar nøkkel
lest rett ut av dokumentet. Anta ikke at alle 357 gjør det; `VirksomhetId + Kildetype + Vedtaksdato`
er fallback.

Merk at `RettsligStatus` er et *rettslig* felt, ikke et teknisk. En retningslinje kan ikke sette seg
over loven, og et system som behandler den som likeverdig med forskrift vil produsere gale svar.
Feltet må derfor være obligatorisk (for `Kildetype`-verdier der det er relevant) og autorisert av
jurist, ikke utledet av KI.

### 3.4 Grafen er ikke et tre — og det er en gave

De 21 sidene under «Bevilling og tillatelser» er **de samme nodene** som ligger under «Om kommunen →
Avdelinger → Kontor for skjenkesaker → Innbyggerhjelp». Samme dokumenter, to navigasjonsstier.

Det gir to uavhengige klassifikasjoner gratis:

- **Tematisk sti** → tjenesteområde
  (`Innbyggerhjelpen/naring-avgifter-og-anskaffelser/naring/bevilling-og-tillatelser`)
- **Organisatorisk sti** → **`kompetentMyndighet`**
  (`omkommunen/avdelinger/kontor-for-skjenkesaker`)

`kompetentMyndighet` er et CPSV-felt som kan utledes **deterministisk fra URL-stien**. Ingen modell
trengs. Det er et konkret eksempel på §0-prinsippet: strukturen finnes, den skal bare leses.

Praktisk konsekvens: dedupliser på kanonisk URL, og lagre *alle* stier en node opptrer under som
separate `NettsideSti`-rader. Å velge én sti og kaste resten kaster informasjon.

---

## 4. Prosess

Fire faser. Fase 1–2 gjøres én gang; fase 3–4 skalerer.

**Fase 1 — Nasjonal sveip (én gang per rettskilde).** Deterministisk iterasjon over alkoholloven og
alkoholforskriften, én liten kontekst per paragraf (~1–3k tokens, godt under Arums 10 000-tegns tak).
Lukket spørsmål: *pålegger denne bestemmelsen en plikt, tildeler den en kompetanse, setter den et
forbud, eller definerer den et begrep — og hvilket organ er adressat?* Output:
`Forvaltningsoppgave`-liste med eId (eller `Tjeneste` med `Objekttype="forvaltningsoppgave"`, se
§10.2). **Dekning er 100 % ved konstruksjon** — man kan si «vi har besøkt hver bestemmelse».
Vektorgjenfinning kan aldri si det, og for et etterlevelsesformål er det kravet, ikke en
optimalisering.

**Fase 2 — Kanonisk tjenesteliste og generell beskrivelse (én gang, 3–5 kommuner).** Crawl Oslo,
Bergen og et par mindre for å validere sveipets liste og finne det den ikke fanget. Forfatt den
generelle tjenestebeskrivelsen **én gang** — Finlands modell, der DVVs redaksjon skriver og eksperter
faktasjekker. Avtagende avkastning raskt: Bergen ga elleve skjenkebevillingsvarianter, en sjette
kommune gir sannsynligvis null nye.

**Fase 3 — Lokal utfylling (alle 357).** Målrettet uttrekk mot **fast** feltliste og fast
rettskildeliste: *finn Bergens gebyrsats for salgsbevilling; siter setningen*. Lukket spørsmål,
verifiserbart, billig. Her skalerer arbeidet, fordi det er uttrekk og ikke oppdagelse.

**Fase 4 — Kryssammenligning som kvalitetskontroll (kontinuerlig).** Dette er premien; se §6.5.

### Hvorfor identitet må komme fra loven

Oslo har **én** side som dekker minst ti rettslige bevillingsvarianter. Bergen har **elleve** sider
for det samme. Samme lov, tigangers forskjell i sidetall. Sidegranularitet er et redaksjonelt valg,
ikke en egenskap ved tjenesten. Deriverer man tjenesteidentitet fra sider, arver man 357 kommuners
redaksjonelle valg som om de var rettslige fakta, og katalogen blir usammenlignbar.

**Regel:** rettskilden gir identitet. Nettsidene og håndbøkene gir feltverdier og lokal
parametrisering. Pakkingen lagres som `presentasjonsvariant`, men styrer aldri identitet.

---

## 5. Struktur og modeller

### 5.1 Tre lag

| Lag | Kilde | Bestemmer |
|---|---|---|
| Nasjonal rettskilde | Alkoholloven, alkoholforskriften. Lovdata, eId. | Tjenesteidentitet, obligatoriske vilkår, **absolutte tak** |
| Lokal rettskilde | Kommunal forskrift + retningslinjer + gebyrregulativ | Parametere, skjønnsutøvelse, lokale forbud |
| Operativ informasjon | Nettsider, skjema, brevmaler | Kanaler, gebyr, frist, kontaktpunkt |

Skillet mellom lag 1 og 2 er ikke felttype, men **hvem som bestemmer verdien**: står den i lov eller
forskrift, er den generell; bestemmer kommunen den, er den lokal. Gebyrformelen i alkoholforskriften
er generell; Bergens 25 ambulerende bevillinger er lokale.

### 5.2 To registre — [SUPERSEDT av §10.2, se der]

*Opprinnelig forslag i denne runden (og i `13-backlog.md` §2.7): et separat
`ForvaltningsoppgaveEntitet`. §10.2 under korrigerer dette til én entitet med `Objekttype`-
diskriminator + SHACL-eksportsluse — billigere, samme funksjon. §2.7 i backlogen bør merkes
superseded når dette notatet legges inn.*

| | `Tjeneste` (CPSV) | `Forvaltningsoppgave` |
|---|---|---|
| Kriterium | Kundevendt, kanalbærende | Pålagt organet av rettskilde |
| Tilsyn, rapportering, interne vedtak | Nei | Ja |
| Formål | Findbarhet, interoperabilitet | Etterlevelse, dekningsanalyse |

Finlands FSC-veiledning er eksplisitt: tjenester er ikke organisasjonens oppgaver, og omfatter ikke
fakturering, valg eller tilsynsoppgaver; kan man ikke forestille seg en kanal der kunden ville
forsøkt å motta tjenesten, er det ikke en tjeneste. **Det er riktig for deres formål og
utilstrekkelig for vårt** — etterlevelse krever nettopp at tilsynsplikter, rapporteringsplikter og
interne vedtakskompetanser enumereres.

Dette omtolker §8.4-funnene (`14-byggesteg5-teknisk-design.md`): «Tilsyn med privat innførsel» og
«Forbud mot skjenking utenfor lokaler» var ikke hallusinasjoner, men reelle rettslige objekter uten
en boks å havne i. Anslaget «1 nyttig forslag av 6» er derfor for hardt. **Det var en skjemafeil,
ikke en prompt- eller retrieval-feil.**

### 5.3 Leveransen er differansen

```
Forvaltningsoppgaver (komplett sveip)
   ∖ Tjeneste/prosess   →  ETTERLEVELSESHULL
   ∩ Tjeneste/prosess   →  CPSV-KATALOG
Tjenester ∖ Oppgaver    →  TJENESTE UTEN HJEMMEL
```

Kravet «alle tjenester MÅ være forankret i rettskilde» blir dermed **oppfylt strukturelt** — en
tjeneste kan bare oppstå ved å matche en oppgave som allerede bærer eId. Forankring er ikke en
valideringsregel som legges på etterpå; det er den eneste veien data kan komme inn.

### 5.4 Provenans per felt

```csharp
FeltkildeEntitet
    Id           Guid
    EierType     string  // "tjeneste" | "forvaltningsoppgave" | "vilkar" | "begrep"
    EierId       Guid
    Feltnavn     string
    KildeType    string  // "rettskilde_node" | "handbok_node" | "nettside_seksjon"
    KildeRef     string  // eId | Url#overskriftssti — MERK: med §0.1/§2s korreksjon er
                          // "rettskilde_node" og "handbok_node" NÅ SAMME TING (begge er
                          // RettskildeNodeEntitet.Eid) — kan forenkles til to KildeType-verdier,
                          // ikke tre
    Utdrag       string
```

Gjelder begge registre og alle kildetyper. Uten kilde skrives ikke feltet. Dette er den
enkeltendringen som gjør konfabulering strukturelt umulig i stedet for noe man må oppdage i
etterkant — og den er direkte utløst av §8.4-funnet der feltfullstendigheten på 83 % sannsynligvis
var oppdiktet fordi kanaler, behandlingstid og kontaktpunkt ikke står i en lov. **Bekreftet
2026-08-12** ved et faktisk eksperiment (`13-backlog.md` §2.2, R1(a)/R1(b)): `kanaler`/`sprak` var
IKKE sporbare til noen kildetekst i det hele tatt for testcasen, mens `kostnad`/`behandlingstid`
faktisk var ekte lovtekst-verdier — begge tilfeller ville `FeltkildeEntitet` gjort synlige direkte i
stedet for å kreve et manuelt eksperiment for å oppdage.

---

## 6. Algoritmer og teknologistøtte

### 6.1 Deterministisk

Oppdagelse via `robots.txt` og `sitemap.xml` framfor menyparsing. Betinget henting med ETag.
Boilerplate-fjerning. PDF-tekstuttrekk (PdfPig finnes allerede via `KunnskapsbibliotekTekstUtvinner`).
Segmentering på dokumentets nummerering. Sidebrytnings- og kolofonfiltrering. Uttrekk av brødsmuler,
overskriftshierarki, lenketekster, `meta-description`, `canonical`, datoer. Hash per node. Parsing av
`§`-referanser, lovdata-lenker, kronebeløp, klokkeslett, varigheter, antall. Kanonisk
URL-deduplisering. Utledning av `kompetentMyndighet` fra organisatorisk sti. Sykel- og
gyldighetssjekker.

### 6.2 KI

Semantisk typing av høstede noder (vilkår/unntak/definisjon/forbud/parameter/skjønnsmoment) mot
lukket liste. Oppsplitting av grove nettsider i flere tjenester og sammenslåing på tvers av sider.
Matching `Tjeneste ↔ Forvaltningsoppgave`. Feltuttrekk **med obligatorisk sitat**. Flagging av
inkonsistens mellom kilder. Verifisering av deterministiske mønstertreff.

Mønsteret: KI-en oppdager aldri, henter aldri, navigerer aldri. Den dømmer på ferdig strukturert
input.

### 6.3 Agentløkke med verktøy — anbefaling: nei, ikke over rettskilder

Agency er for når man ikke vet hva man trenger. Over rettskilder og nummererte håndbøker **vet man
det**, fordi strukturen sier det. Alt en agent ville brukt verktøy til — hent forelder, følg
kryssreferanse, hent søskenledd — ligger allerede deterministisk i `ParentNodeId`, `Eid` og Lovdatas
kryssreferanselenker *(merk: kryssreferanser mellom PARAGRAFER er, i motsetning til det som ble
antatt i den forutgående v3-analysen, IKKE bygget i dag — kun vertikal `ParentNodeId`-hierarki finnes,
se `13-backlog.md` §2.2 for korreksjonen. Poenget her — agency er unødvendig over strukturert
innhold — står likevel, det er bare mindre "allerede der" enn antatt)*. Å bygge en løkke for at
modellen skal *be om* det, når koden kan *gi* det gratis, er å betale for nondeterminisme uten
gevinst.

Praktisk hindring uansett: `IKiAgentKlient.GenererAsync(string systemInstruks, string kontekst)` kan
ikke uttrykke en agentløkke. Streng inn, streng ut. Det er en kontraktsendring, ikke en
implementasjonsdetalj, og bør være en egen beslutning.

**Der agency tjener kostnaden:** over virksomhetens ustrukturerte dokumenter og nettsider, der man
ikke vet hvor svaret er. Anbefalt trapp: (1) deterministisk sveip, ingen ny abstraksjon; (2) kodebasert
kryssreferanseutvidelse *(forutsetter at kryssreferansegrafen bygges først — se korreksjonen over,
dette er ikke gratis lenger)*; (3) *hvis* 1–2 etterlater et målt gap, ett smalt verktøy
(`søk_virksomhetsdokumenter`) med hardt budsjett og **persistert bane** — i en rettsstatlig
anvendelse er banen en del av provenansen, ikke driftslogg.

### 6.4 RAG — anbefaling: strukturell gjenfinning først

For ett dokument på fem sider med tiende kapitler, eller en nettstedsseksjon på 40 sider, er
likhetssøk feil verktøy. `Eid`-oppslag, overskriftsmatch og lenkegraf er både billigere og
beviselige. RAG blir riktig når korpuset er hele kommunen på tvers av sektorer.

Uansett vei: gjenfinningsenheten må bære et siterbart anker (`Eid` | `Url#overskriftssti`), ellers
virker ikke §5.4. Det kravet begrenser chunkingen mer enn noe likhetshensyn.

Fra `14-byggesteg5-teknisk-design.md` §8.6 gjelder fortsatt: fold forelderkontekst inn i teksten som
*embeddes*, men behold nodens egen tekst i det som *vises*. For håndbøker betyr det at «Kapittel 4 –
Skjenkebevillinger» prefikses punkt 4.3s tekst i vektoren, ellers har «minst 30 rom med dusj/bad»
null signal om hva det gjelder.

### 6.5 Automatisk lovlighetskontroll — den demonstrerbare gevinsten

Alkoholloven setter harde nasjonale tak: skjenking maksimalt til 03:00, salg maksimalt til 20:00
hverdager og 18:00 lørdag *(ikke verifisert mot importert lovtekst — se §12, sjekk § 3-7/§ 4-4 før
dette kodes)*. Når lokale parametere først er trukket ut med sitat (§5.4), er sammenligningen mot
taket **helt deterministisk** — ingen modellskjønn i selve kontrollen, bare i uttrekket.

Det gir tre maskinelle kontroller på tvers av alle 357:

- **Lovlighet.** Lokal forskrift som setter skjenketid til 03:30 er ulovlig. Flagges automatisk.
- **Uteligger.** Behandlingstid 4 uker, 8 uker, 30 dager — reell variasjon eller datafeil?
- **Hull.** 340 kommuner har side om ambulerende skjenkebevilling, én har ikke. Tilbyr de den ikke
  (§ 4-5 sier «kan», så mulig lovlig) eller mangler siden?

Dette er nøyaktig det kommunale parameterlaget som allerede finnes i DMN-modellen med Tønsberg,
Bærum og Vennesla — forskjellen er at det fylles automatisk framfor manuelt. Og det er «etterlever
vi lovene vi forvalter», rettet mot de lokale reglene.

### 6.6 Endringsdeteksjon

Finlands redaksjon overvåker lovendringer og oppdaterer generelle beskrivelser, med automatisk
forplantning til alle tjenester som bruker beskrivelsen. Ekvivalenten her: hent på nytt, diff per
`TekstHash` *(var: `SeksjonsHash` — feltet finnes allerede, se §0.1)*, flagg alle `Tjeneste`- og
`Vilkår`-objekter hvis kildenoder endret seg.

Bygg dette fra første versjon. Det koster nesten ingenting og er umulig å legge på i etterkant uten
å re-hente alt. Uten det er dataene råtne innen et år — Bergen reviderer retningslinjene hver
bevillingsperiode og gebyrsatsene årlig.

### 6.7 Støyfiltrering krever mer enn URL-mønster

Oslo legger VM-skjenketidene under `aktuelt.oslo.kommune.no` — enkelt å ekskludere. Bergen legger dem
som ordinær tjenesteside, ved siden av «Kurs i ansvarlig alkoholhåndtering 2026» og
«Bevillingsgebyr 2025/2026 – Frist er 17.februar 2026». Samme innholdstype er nyhet hos den ene og
tjeneste hos den andre. Nødvendig: URL-mønster **og** datodeteksjon i tittel **og** et eksplisitt
gyldighetsperiode-felt på noden.

---

## 7. Verdiforslaget på tvers av kommuner

357 kommuner løser samme lovpålagte oppgave på 357 måter. For en kjede som driver serveringssteder i
20 kommuner betyr det 20 sett retningslinjer, 20 gebyrregulativ, 20 søknadsprosesser — med reelt
ulike krav (Bergen krever 30 hotellrom; nabokommunen kanskje ikke). Det er den konkrete
brukersmerten.

Regel-IDEs leveranse er derfor ikke bare en katalog, men **normalisering**: samme tjeneste, samme
feltnavn, lokale verdier side om side, med sitat og hjemmel per verdi. Det er også argumentet for at
fase 2 gjøres nasjonalt én gang framfor 357 ganger lokalt.

Finlands 26 000 tjenester kom forøvrig ikke fra oppdagelse — loven 571/2016 påla alle organisasjoner
å dokumentere sine tjenester, og 9 300 tjenestemenn oppdaterer dataene. Man kan ikke KI-e seg til det
tallet, fordi tallet er et produkt av lovhjemmel og fordelt arbeid. Men flaskehalsen deres var
konsistens og vedlikehold, ikke funn — og det er nettopp der struktur­høsting og automatisk
kryssammenligning har noe å tilby som 9 300 mennesker ikke har.

---

## 8. Prioritert rekkefølge [KORRIGERT 2026-08-12 — punkt 1 og 8 justert]

**Trinn 0 — avklaringer som må landes før koding (ikke kode).**

- AKN som serialisering eller primærlager (§9.5). Anbefaling foreligger; må besluttes.
- Én CPSV-entitet med `Objekttype` eller to entiteter (§10.2). Anbefaling foreligger; må besluttes.
- `RettsligStatus`-taksonomien juridisk avklart (§11).
- Sjekk CPOV mot `PublicService` (§10.3), og AKN-XSD-veien i .NET (§9.5).
- **Nytt:** bekreft at `Url`/`InterntDokNr`/`RettsligStatus`-utvidelsen (§0.1) faktisk går på
  `RettskildeEntitet` og ikke skaper konflikt med `ck_rettskilder_akn_xml`-constrainten (som i dag
  krever `AknXml IS NOT NULL` for `Importrolle='primaer'` — et hentet-men-ikke-AKN-serialisert
  PDF-dokument må trolig regnes som `Importrolle='referanse'` inntil AKN-eksport (§9.5) finnes).

**Trinn 1 — ingen KI, ingen ny abstraksjon.**

1. **[KORRIGERT]** Ny parser (sideordnet `LovdataHtmlParser.cs`) som segmenterer en PDF/nettside på
   egen nummerering og skriver til EKSISTERENDE `RettskildeEntitet`/`RettskildeNodeEntitet` (utvidet
   med §0.1/§3.3s nye felt), IKKE nye `HandbokKilde`/`HandbokNode`-tabeller. Verifiseringsmål:
   Bergens retningslinjer inn med korrekt kapittel/punkt-tre og `Eid` som løser «punkt 4.7».
2. Hash-basert endringsdeteksjon — **allerede bygget** (`TekstHash` + reimport-versionering i
   `RettskildeImportTjeneste`). Verifiser at den nye parseren i punkt 1 kan gjenbruke den uendret.
3. Deterministisk uttrekk av `hjemlet_i` og `kryssrefererer`. Verifiseringsmål: § 1-7d og § 4-5
   kobles mot importert alkohollov. Sjekk først om `RettskildeReferanseEntitet` (eksisterende) kan
   bære disse kanttypene før en ny tabell lages (§3.2).
4. Utvid `RettskildeEntitet` med `RettsligStatus`-feltet fra §3.3 (juristautorisert, obligatorisk
   der relevant) — IKKE en ny `LokalRettskildeEntitet`-tabell.
5. **AKN-eksport av én håndbok + rundturstest** (§9.5). Verifiseringsmål: Bergens retningslinjer ut
   som `<doc name="retningslinje">`, inn igjen, semantisk ekvivalent. Gjør dette tidlig og på ett
   dokument — det avdekker modellfeil mens de er billige å rette. Dette er det eneste punktet i
   Trinn 1 som er ubestridt NYTT arbeid uten eksisterende motpart i koden.

**Trinn 2 — stabilisering og måling.** **Status 2026-08-12: R0 gjenstår, R1 er kjørt** — se
`13-backlog.md` §2.2/§4 for fullt resultat (R1(a): kostnad/behandlingstid ekte i lovtekst,
kanaler/språk ikke sporbare i det hele tatt; R1(b): kunnskapsbiblioteket bidro null til
feltfullstendigheten for Testkommunen). R2/§5.4 (`FeltkildeEntitet`) gjenstår.

**Trinn 3 — de to registrene og sveipet.**

8. `Objekttype`-diskriminator på `TjenesteEntitet` + `OppgaveTjenesteEntitet` + de to SHACL-formene +
   eksportslusen (§10.2). **Merk navnekollisjon:** `TjenesteEntitet` har allerede et `Tjenestetype`-
   felt (Bevilling/Registrering/Tillatelse/...) — `Objekttype` (tjeneste/forvaltningsoppgave) er et
   ANNET, overordnet skille. To lignende navn på samme tabell er en reell forvekslingsrisiko; velg
   et tydelig differensiert navn før migrasjon skrives. Verifiseringsmål: en tilsynsoppgave
   valideres grønt mot `ForvaltningsoppgaveShape`, avvises av `TjenesteShape`, og kommer **ikke** med
   i CPSV-eksporten.
9. Sveip over alkoholloven, paragrafgranularitet. Mål kostnad mot dump-alt: ~65 kall × ~2 000 tokens
   ≈ 130 000 input-tokens mot 29 000. Fire–fem ganger dyrere, mot bevisbar dekning og
   per-eId-sporbarhet. **Ta avveiningen eksplisitt.**
10. Autorisert tjenestetypologi i kodeliste-maskineriet fra byggesteg 2, med Finlands
    inklusjonsregler som utgangspunkt og norsk delta.

**Trinn 4 — annotasjon og skalering.**

11. `HandbokAnnotasjonEntitet` med KI-forslag, én annotasjonstype om gangen. Start med `definisjon`
    (deterministisk mønster å verifisere mot) og `parameter` (verifiserbar verdi).
12. Nettsideinnsamling: `sitemap.xml`, per-URL-lagring, overskriftssegmentering, alle stier bevart.
13. Lovlighetskontroll mot nasjonale tak.

**Utsatt:** K-tuning (kan nå prioriteres — R1 er kjørt). §8.6 fiks 1–2 (kan nå prioriteres, samme
grunn). Reranking. pgvector. Generell agentløkke.

---

## 9. Akoma Ntoso som forfatterformat, ikke importformat

### 9.1 Reframen: strukturen først, PDF som rendering

Retningen i §1–2 er PDF → struktur. Det er en overgangsfase. Målbildet er det motsatte: **forfatt
strukturert, render PDF.** AKN (OASIS LegalDocML 1.0) er designet for nettopp det — et forfatter- og
utvekslingsformat der PDF/HTML er nedstrøms transformasjoner.

**[KORRIGERT 2026-08-12]** Dette er delvis allerede virkeligheten, ikke bare et målbilde: byggesteg
1s importpipeline serialiserer allerede til AKN (`AknXmlSkriver.cs`), lagret i
`RettskildeEntitet.AknXml`, for ALT importert Lov/Forskrift-innhold — bare i importretningen (HTML →
AKN), ikke eksportretningen (relasjonell → AKN) som ville trengtes for forfattet/høstet
håndbok-innhold. Bergen er allerede halvveis: «Dokumentkategori: Reglement, Dok.nr: SD-24-113,
Rev.nr.: 01» er et dokumentstyringssystem som stempler filen. De behandler retningslinjene som et
styrt dokument. Det de mangler er en strukturert kilde.

### 9.2 Mappingen er uvanlig ren

| Bergens struktur | AKN |
|---|---|
| Dokumenttype (ikke lovgivning) | `<doc name="retningslinje">` — generisk container for typer AKN ikke navngir |
| Kapittel 1–10 | `<chapter eId="kap_1">` |
| Punkt 4.1, 8.6 | `<paragraph>` / `<point>`, eId fra dokumentets egen nummerering |
| Nummerert liste i kap. 2 | `<list>` / `<item>` |
| «jf. Alkoholloven § 1-7d» | `<ref href="{ELI-URI}#§1-7d">` |
| «det vises til punkt 4.7» | `<ref href="#kap_4__para_7">` |
| «Med «hoteller» menes …» | `<def>` + `<term>`, med `<TLCTerm>` i `<references>` |
| Dok.nr + Rev.nr | FRBR Work / Expression / Manifestation i `<identification>` |
| Fastsatt av Bystyret 19.06.2024 | `<lifecycle><eventRef>` + `<TLCOrganization eId="bystyret">` |
| Gyldig 01.07.2024–01.07.2028 | `<temporalData><temporalGroup><timeInterval>` |
| Lokale absolutte forbud (4.1, 4.10) | ordinær hierarki + annotasjon i Regellaget |

`Eid` fra §2 (var: `LokalEid`) er altså ikke en midlertidig krykke — den er AKN-eId-en, uttrykt
relasjonelt. Dokumentet siterer seg selv med denne nummereringen, så den er autoritativ.

### 9.3 Hva AKN dekker — og den ene grensen

| Dokumenttype | AKN |
|---|---|
| Lov, forskrift | `<act>` |
| Retningslinje, reglement, rundskriv, veileder | `<doc name="…">` |
| Dommer, presedens (byggesteg 3) | `<judgment>` |
| Enkeltvedtak | `<doc name="vedtak">` |
| **Nettsider** | **Ikke AKN** |

En kommunal tjenesteside er ikke et rettslig dokument med hierarkisk normstruktur. Grensen matcher
trelagsmodellen i §5.1 presist: lag 1 og 2 er AKN-territorium, lag 3 forblir
`NettsideDokument`/`NettsideSeksjon`.

**AKN modellerer dokumenter, ikke regler.** Vilkår/Regel/Unntak hører i Regellaget, med eId som
skjøt. LegalRuleML er følgestandarden for å henge regler på AKN via eId-referanser. Ikke kod
regelgrafen inn i AKN.

### 9.4 ELI — og korreksjonen av §3.3

Lovdata er **nasjonal ELI-koordinator**, direkte knyttet til deres ansvar for å kunngjøre og
publisere regelverk, og de har deltatt i utarbeidingen av ELI gjennom EUs Publication Office og
European Forum of Official Gazettes. Lovdatas eksisterende URI-er tilfredsstiller ELIs krav til
stabile identifikatorer, og deres metadata ligger tett på ELI-definisjonene. Implementasjonen er i
**beta**, med RDFa-innbygging som tredje søyle, og de ber om tilbakemelding fra dem som vil ta det i
bruk.

Arbeidsdelingen mellom standardene:

> **ELI gir identitet og metadata. AKN gir struktur.**

Kartleggingen mellom de to ontologiene er etablert forskningsarbeid, blant annet gjennom
EU-prosjektet Manylaws. ELI-arbeidsgruppen har også utviklet en SHACL-basert metadatavalidator, som
er direkte brukbar for validering her.

Konsekvens for §3.3: URI-konvensjonen mangler ikke for lov og forskrift. Den mangler for
ikke-forskrift kommunale instrumenter.

### 9.5 Beslutningen som betyr noe: serialisering, ikke primærlager

**Anbefaling: AKN som kanonisk serialisering og forfattermål. Relasjonelt som arbeidslager.**

Begrunnelse: byggesteg 1 importerer allerede Lovdata til et relasjonelt tre OG en AKN-serialisering
side om side (`AknXml`-kolonnen — se §9.1s korreksjon); SQL og EF Core over `ParentNodeId` er appens
daglige arbeid; joins mot Vilkår, Tjeneste og Forvaltningsoppgave krever relasjonelt. XML-lagring i
Postgres med XPath er mulig, men mister EF og gjør hverdagsspørringene tunge. Å bytte primærlager er
en omskriving, ikke et tillegg — men det trengs ikke, siden mønsteret (relasjonelt + AKN-kolonne
side om side) allerede er etablert.

**Disiplinen som gjør det ærlig — rundturstesten:**

```
importer AKN → relasjonell modell → eksporter AKN → assert semantisk ekvivalens
```

Det er invarianten som gjør «start med struktur, produser PDF» reell i stedet for aspirasjonell, og
som hindrer at den relasjonelle modellen driver bort fra standarden. Den bør være en automatisert
test, ikke en engangsverifisering — samme kultur som `RundskrivReproduksjonTests.cs`. **Ikke bygget
i dag** — dagens flyt er kun importretningen (HTML → AKN → relasjonelt), aldri tilbake.

**Verktøyforbehold:** jeg kjenner ikke noe modent AKN-bibliotek for .NET. Moden verktøykjede (LIME,
AKN4EU-verktøy) er i hovedsak Java og JavaScript. Realistisk vei i denne kodebasen: generer klasser
fra AKN-XSD-en, eller håndrull med `System.Xml.Linq` mot skjemaet — samme tilnærming
`AknXmlSkriver.cs` allerede bruker for skriveretningen. Verifiser mot XSD i test. Dette bør prises
inn før §11 låses.

---

## 10. CPSV for både tjeneste og forvaltningsoppgave — én form, hard eksportsluse

### 10.1 CPSV gir mer enn antatt

Før man konkluderer at forvaltningsoppgaver trenger en egen modell, er dette allerede i CPSV-AP:

| CPSV-egenskap | Gjelder tjeneste | Gjelder oppgave |
|---|---|---|
| `cv:hasCompetentAuthority` | ✓ | ✓ |
| **`cv:hasLegalResource` → `eli:LegalResource`** | ✓ | ✓ **obligatorisk** |
| `cv:hasCriterion` (vilkår) | ✓ | ✓ |
| `cv:hasRule` (retningslinje, forskrift) | ✓ | ✓ |
| `dct:type` | ✓ | ✓ — diskriminatoren |
| `cv:sector`, `cv:thematicArea`, `cv:isClassifiedBy` | ✓ | ✓ |
| `cv:hasChannel`, `hasCost`, `processingTime`, `hasOutput`, `hasInput` | ✓ | **strukturelt null** |

To ting følger. **Hjemmelsforankring er allerede førsteklasses i CPSV** via `hasLegalResource` mot
`eli:LegalResource` — kravet om at alt skal være forankret har standardmaskineri, ikke en
egenkonstruksjon. Og siden bare `PublicService` og `PublicOrganisation` er obligatoriske klasser, er
en tjeneste uten kanal, kostnad og output **gyldig CPSV**. Standarden tillater formen.

Dermed blir §5.2s autoriserte typologi ikke «finn opp en mekanisme», men **utvid CPSV-AP-NOs
`dct:type`-kodeliste** — som Digdir eier.

**[KORRIGERT 2026-08-12]** `cv:hasLegalResource`-kravet er allerede strukturelt oppfylt for
`Tjeneste` i dagens skjema — `TjenesteRegelverksreferanseEntitet` (bygget byggesteg 5 runde 4, Spor A)
kobler en tjeneste til eksakte `[eId]`-tagger. Å gjøre `hasLegalResource` OBLIGATORISK for
`Objekttype="forvaltningsoppgave"` (§10.2) er dermed en valideringsregel på eksisterende
infrastruktur, ikke en ny kobling.

### 10.2 Anbefaling: CPSV som form, med eksportsluse

Én relasjonell entitet med obligatorisk `Objekttype` (`tjeneste` | `forvaltningsoppgave`) —
**[KORRIGERT] velg et navn som ikke kolliderer med eksisterende `Tjenestetype`-felt på
`TjenesteEntitet`, se §8 punkt 8.** To SHACL-former over samme data:

```
TjenesteShape
    krever    cv:hasChannel, cv:hasOutput, cv:hasCompetentAuthority
    krever    dct:type ∈ {tjeneste-verdier}

ForvaltningsoppgaveShape
    krever    cv:hasLegalResource, cv:hasCompetentAuthority
    FORBYR    cv:hasChannel, cv:hasCost, cv:processingTime, cv:hasOutput
    krever    dct:type ∈ {tilsyn, rapportering, internt_vedtak, regulering, …}
```

Og slusen, som er hele poenget:

> **Bare `Objekttype = tjeneste` emitteres som `cpsv:PublicService` i ekstern eksport.**

Publiserer man tilsynsplikter som `PublicService` til en nasjonal eller europeisk katalog, ser en
konsument tre ganger flere tjenester enn det finnes — og Finland ekskluderte nettopp disse med
overlegg. Intern modell samlet (én verktøykjede, ett UI, én eksportløype), ekstern kontrakt ærlig.

Dette erstatter det separate `ForvaltningsoppgaveEntitet`-forslaget i §5.2/`13-backlog.md` §2.7.
`OppgaveTjenesteEntitet` beholdes uendret som koblingstabell, siden fravær av rad er
etterlevelseshullet.

### 10.3 To ting å sjekke, ikke å anta

**CPOV** (Core Public Organisation Vocabulary) modellerer offentlige organisasjoners kompetanse. Det
kan passe en forvaltningsoppgave bedre enn å strekke `PublicService`. Jeg har ikke verifisert CPOVs
egenskapsliste — dette ligger i Digdirs eget fagfelt og bør sjekkes før §10.2 låses.

**Livsløpene divergerer.** En oppgave endres når loven endres — nasjonalt, én forfatting. En tjeneste
endres når kommunen omorganiserer — lokalt, 357 forfattinger. Det argumenterer for separat
versjonering og separat forfatterrettighet selv innenfor én tabell.

---

## 11. Åpne spørsmål

- ~~**Håndbok som forfattet artefakt vs. importert kilde.**~~ **[LUKKET 2026-08-12, se §0.1]** —
  koden har allerede svart: det er samme entitet (`RettskildeEntitet`/`RettskildeNodeEntitet`), ulik
  proveniens (import via `LovdataHtmlParser` vs. forfatting via
  `HandbokForfatterTjeneste.OpprettBladNodeAsync`).
- **`RettsligStatus`-taksonomien** må avklares juridisk før den låses. Skillet forskrift / politisk
  vedtak / administrativ praksis har reelle konsekvenser for hvor langt et system kan gå i å
  behandle en retningslinje som normerende.
- **AKN-URI for ikke-forskrift kommunale instrumenter** (§3.3, §9.4). Lovdata er ELI-koordinator for
  kunngjort regelverk; retningslinjer faller utenfor. Forslag til navngivning:
  `/akn/no/doc/retningslinje/{kommunenr}-{organ}/{vedtaksdato}/{dok-nr}/nor@/!main`. Men dette er en
  **nasjonal konvensjonsbeslutning**, ikke et prosjektvalg — og Digdir er organet som kan reise den
  med Lovdata. Inntil den finnes: bruk et internt, dokumentert skjema og hold det isolert bak én
  mapper-klasse, slik at bytte er billig.
- **AKN eller ELI som primær identitetsbærer** i den relasjonelle modellen. §9.4 gir arbeidsdelingen
  på papiret, men i praksis må én av dem være nøkkelen `FeltkildeEntitet.KildeRef` peker på. Ikke
  avgjort.
- **Sveipegranularitet**: paragraf eller kapittel? Paragraf gir presisjon, kapittel gir sammenheng og
  færre kall. Bør måles.
- **Normtype-firedelingen** (plikt/kompetanse/forbud/definisjon) — er den tilstrekkelig? Schartums
  skille mellom bunden rettsanvendelse og forvaltningsskjønn hører sannsynligvis inn, men må
  avklares mot den låste Vilkår/Regel/Unntak-ontologien først.
- **Deduplisering** av oppgaver som følger av flere bestemmelser: regelbasert, KI-basert eller
  manuelt?
- **Hvem autoriserer tjenestetypologien** — virksomheten, Digdir eller sektordirektoratet? Ikke et
  teknisk spørsmål.
- **Crawle-etikett og hjemmel.** `robots.txt`, rate-limiting, identifisert crawler med
  kontaktadresse. Og: spør før du skraper — Kontor for skjenkesaker kan ha eksport. Pass på at
  navngitte saksbehandlere og direktenumre på sidene ikke havner utilsiktet i kunnskapsbasen.
- **[NYTT 2026-08-12]** Går `Url`/`Innhold`/`InnholdsHash`-utvidelsen (§0.1 punkt 1) på
  `RettskildeEntitet` i konflikt med `ck_rettskilder_akn_xml`-constrainten? Må sjekkes før migrasjon
  skrives — se Trinn 0 i §8.
- **[NYTT 2026-08-12]** Kan `Kommunenummer` hentes fra `VirksomhetEntitet` i stedet for å dupliseres
  på `RettskildeEntitet`? Ikke sjekket denne runden.

---

## 12. Kildegrunnlag

**Lest direkte i denne runden:** Bergens retningslinjer SD-24-113 rev. 01 i fulltekst
(`/api/rest/filer/V51903878`); Bergens seksjonsside «Bevilling og tillatelser»; Bergens «Kontor for
skjenkesaker → Innbyggerhjelp»; Oslos «Salg, servering og skjenking». Alle strukturpåstander om disse
er lest, ikke antatt.

**Lest tidligere i samtalen:** `13-backlog.md` og `14-byggesteg5-teknisk-design.md` i sin helhet;
RAG-sammenligningsrapporten; rulemapping.org (Arum/Mura/RUML); SEMIC/DVV-casestudien om Finlands
tjenestekatalog; Finlands innholdsproduksjonsveiledning inkludert «Instructions for service
description» og «What are general descriptions?».

**ELI og AKN (§9):** Lovdatas egen ELI-side (`lovdata.no/eli/norsk`) for at Lovdata er nasjonal
ELI-koordinator, at implementasjonen er i beta, og de tre søylene. ELI-oversikt for versjon 1.4,
FRBRoo/CIDOC-grunnlaget og RDFa/JSON-LD-innbygging. ACM-artikkel om kartlegging av ELI- og
AKN-ontologiene fra Manylaws-prosjektet. Sparna-referanse for ELIs SHACL-baserte metadatavalidator.

**Verifisert mot kode 2026-08-12 (Claude Code, denne konsolideringen):** `src/RegelIde.Data/
Entiteter.cs` (RettskildeEntitet, RettskildeNodeEntitet, TjenesteEntitet, HandbokKommentarMetadata-
Entitet); migrasjonshistorien i `src/RegelIde.Data/Migrasjoner/` (bekrefter `Kildetype` er
constraint-fri fri streng, ikke en lukket enum); `RettskildeImportTjeneste.cs` (bekrefter
hash-basert reimport-versionering allerede er implementert); `HandbokForfatterTjeneste.cs`
(bekrefter håndbok-forfatting skriver til `RettskildeNodeEntitet`, ikke en egen tabell); live
API-data fra Testkommunens seed (bekrefter et `Kildetype="Virksomhetsdokument"`-eksempel allerede
finnes i systemet, med samme form som notatets Bergen-eksempel).

**Ikke verifisert — sjekk før implementering:**

- At alkohollovens tak er 03:00 skjenking og 20:00/18:00 salg er fra egen kunnskap, ikke importert
  lovtekst. Sjekk § 3-7 og § 4-4 før lovlighetskontrollen (§6.5) kodes.
- AKN-elementbruken i §9.2 er fra egen kunnskap om LegalDocML 1.0, ikke lest mot XSD-en i denne
  runden. Verifiser `<doc name>`, `<hcontainer>`, `<temporalGroup>` og TLC-klassene mot skjemaet før
  §9 låses.
- At det ikke finnes et modent .NET AKN-bibliotek er min vurdering, ikke et uttømmende søk.
- CPOVs egenskapsliste (§10.3). Ikke sjekket.
- Om kommunale forskrifter faktisk har ELI-URI hos Lovdata i praksis (§3.3). Antatt, ikke
  verifisert.
- Bergens forskrift om salgs-, skjenke- og åpningstider; Finlands swagger; den finske
  Excel-rapporten; om det finnes en finsk generell beskrivelse for `anniskelulupa`.
- Om `Kommunenummer` bør ligge på `VirksomhetEntitet` og gjenbrukes, eller dupliseres — ikke sjekket
  (§0.1/§11).

**Egen analyse, ikke lest noe sted (fra opprinnelig notat):** struktur­høstingsprinsippet (§0); at
nummereringen i retningslinjene er en autoritativ eId-ordning fordi dokumentet siterer seg selv;
trelagsmodellen; at organisatorisk URL-sti gir `kompetentMyndighet` deterministisk; at
lovlighetskontroll mot nasjonale tak er deterministisk etter uttrekk; rundturstesten (§9.5);
eksportslusen (§10.2); hele §2, §8 og §11.

**Egne tidligere feil, for ordens skyld (fra opprinnelig notat):** jeg foreslo først et strengt
SHACL-filter mot tjenestebegrepet, trakk det etter Johanns presisering, og fant deretter at Finlands
egen veiledning ligger nær mitt opprinnelige forslag. Resolusjonen ble tosregister-modellen i §5.2 og
eksportslusen i §10.2 — ikke at noen av de to første posisjonene var riktig. Jeg påsto også i §3.3 at
lokale rettskilder mangler nasjonal infrastruktur; det er korrigert til å gjelde spesifikt
ikke-forskrift instrumenter, etter at Lovdatas ELI-rolle ble bekreftet.

**Konsolideringsfeil å unngå fremover:** den forutgående v3-analysen (`13-backlog.md` §2.2) antok at
kryssreferanser mellom paragrafer («§ 1-7b viser til § 3-2») allerede er bygget i rådataen — det er
korrigert der, og gjentatt/rettet her i §6.3, siden dette notatet delvis bygde videre på den
antagelsen.
