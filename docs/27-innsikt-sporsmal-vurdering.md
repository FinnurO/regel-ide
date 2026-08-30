# 27. Vurdering: Johanns 13 innsiktsspørsmål mot faktisk kodebase (2026-08-29/30)

*Bestilling: Johann limte inn et dokument med 13 kategorier "innsiktsspørsmål" en strukturert,
koblet kunnskapsmodell burde kunne besvare, og ba om en kritisk, kodebase-forankret vurdering av
hva som faktisk kan bygges NÅ — ikke en visjon om et fremtidig dashboard/kunnskapsgraf. Dette
dokumentet er den bestilte vurderingen.*

## Rammen dette vurderes innenfor

`docs/06-veikart.md` sier eksplisitt at byggesteg 8 (Kunnskapsgraf/påvirkningsanalyse) og 9
(Dashboard) er **bevisst utenfor MVP** — ikke nedprioritert, men "strukturelt umulige å bevise noe
med før byggesteg 1–7 har reelt innhold". Johanns egen formulering i bestillingen ("vi har bevisst
ventet med dashboards") bekrefter at dette er en kjent, stående beslutning, ikke noe han har glemt.

Denne vurderingen respekterer det. Ingen av anbefalingene under er en dashboard-side eller en
kunnskapsgraf-visning. Der noe faktisk kan bygges nå, er forslaget konsekvent: **et lite, read-only
rapport-endepunkt** (evt. en ny seksjon på en eksisterende detaljside), aldri en ny visningsside med
eget navigasjonspunkt. Der noe IKKE kan bygges nå, sier vurderingen det rett ut og navngir det
konkrete gapet (manglende data, manglende kobling, manglende felt) — ikke bare "mer arbeid trengs".

Metodisk: alle konklusjoner under er verifisert mot `src/RegelIde.Data/Entiteter.cs`,
`RegelIdeDbContext.cs`, relevante `*RegisterTjeneste.cs`-filer og `docs/13-backlog.md`/
`docs/20-virksomhetskatalog-og-rollemodell.md` — ikke antatt.

---

## 1. Begrepskonsistens på tvers av regelverk

**Konklusjon: ikke svarbart uten videre arbeid.**

`BegrepEntitet` (`Entiteter.cs:803`) har `LovreferanseEid` (ett enkelt `eId`, ikke en liste) og
`Begrepskategori` (`null`/`'virksomhet'`/`'rolle'`, docs/20 §2.3–2.4), men ingen mekanisme for å
oppdage at to begreper i to ulike rettskilder faktisk beskriver samme fenomen ulikt ("samboer" i lov
A vs. lov B). Det finnes ingen kollisjons-sveip, ingen `refersTo`-taggingsmekanisme, ingen
begrepsoppdagelsesinfrastruktur i det hele tatt per i dag.

Dette henger sammen med et parallelt arbeid: PR #61 ("Plan: begrepsoppdagelse (deterministisk
sveip) — vurdering mot faktisk kodebase", branch `begrep-oppdagelse-plan`) er **under vurdering, ikke
merget** per nå — den dekker nøyaktig dette gapet på planleggingsnivå. Innholdet i den PR-en
forutsettes ikke her; kategori 1 forblir "ikke svarbart" inntil den planen er besluttet OG bygget.

**Hva som konkret mangler:** en begrepsoppdagelsesmekanikk (oppdag kandidater for samme begrep på
tvers av rettskilder, sammenlign definisjoner), pluss en måte å faktisk MARKERE at to `BegrepEntitet`-
rader er "samme fenomen, ulik definisjon" — ingen slik relasjon finnes i skjemaet i dag (kun
`ErstatterId`, som betyr noe helt annet: at én rad ERSTATTER en annen, ikke at to rader kolliderer).

---

## 2. Myndighet og delegasjon

**Konklusjon: skjema finnes, data mangler i volum — ikke reelt svarbart i dag.**

`MyndighetstildelingEntitet` (`Entiteter.cs:854`) er akkurat det spørsmålet ber om: kobler et
rollebegrep (`RolleBegrepId` → `BegrepEntitet` med `Begrepskategori="rolle"`) til en konkret
`VirksomhetId`, hjemlet i `HjemmelRettskildeId` + et strukturert `ParagrafspennJson`
(`{FraEid, TilEid?}`-par). Mekanismen er reelt bygget og testet (`MyndighetstildelingTjenesteTests.cs`).

Men `docs/13-backlog.md` §8 dokumenterer allerede, verifisert samme dag som denne bestillingen kom
inn, at:
- **"Ingenting i appen leser fra `Myndighetstildeling` ennå for noe funksjonelt"** —
  `TjenesteEntitet.KompetentMyndighet` (`Entiteter.cs:438`) er fortsatt en flat fritekststreng, aldri
  utledet fra et rolletildelings-oppslag.
- Det finnes **ingen frontend-skjema for å opprette** `Myndighetstildeling`-rader — kun
  `POST /api/rollebegrep`/`POST /api/myndighetstildelinger` via Swagger/direkte HTTP, og én read-only
  tabell i `VirksomhetDetalj.tsx`. Uten UI er det ingen realistisk vei til volum av rader.
- De "aggregerte visningene" docs/20 §3 beskriver (rettskilde→virksomhet, virksomhet→rettskilde via
  paragrafspenn-treff) er, sitat, **"ren, ubrukt infrastruktur i dag"** — kun
  `MyndighetstildelingTjeneste.ErGjeldendeAsync`, kalt utelukkende fra tester.

Konklusjonen er derfor ikke "skjema mangler" (det gjør det ikke), men **"skjema er klart, volum av
faktiske rader mangler"** — å bygge et rapport-endepunkt over en håndfull manuelt Swagger-opprettede
rader ville ikke gi noe reelt innsiktssvar, uansett hvor korrekt spørringen er.

**Hva som konkret mangler:** en opprett-UI for `Myndighetstildeling` (så data faktisk kan akkumuleres
i normal bruk) FØR et spørrings-/rapportlag gir noen verdi. Sekundært: selve koblingen fra
`TjenesteEntitet.KompetentMyndighet` til et rolletildelings-oppslag (docs/13 §8s uavklarte punkt).

---

## 3. Konsekvensanalyse av regelverksendring

**Konklusjon: delvis svarbart i dag for "hvilke tjenester berøres" — men kun med eksisterende,
manuelt vedlikeholdte referanser, ingen automatisk endringsdeteksjon.**

`TjenesteRegelverksreferanseEntitet` (`Entiteter.cs:584`) kobler en `TjenesteId` til en
`TilRettskildeId`+`TilEid` (valgfritt ned på paragraf-/felt-nivå via `Felt`). Det finnes altså en
reell, spørrbar kobling tjeneste↔paragraf i dag, brukt av `TjenesteDetalj.tsx`s
"Regelverksreferanser"-fane. En spørring "`SELECT DISTINCT TjenesteId FROM TjenesteRegelverksreferanser
WHERE TilRettskildeId = X AND TilEid LIKE '%§4-3%'`" er teknisk triviell.

**Men** dette svarer bare på "hvilke tjenester i VÅR modell har eksplisitt registrert denne paragrafen
som hjemmel" — ikke det Johann faktisk spør om: en automatisk "impact-rapport" utløst når Lovdata/
Stortinget publiserer en endring. Det finnes:
- Ingen kobling til Stortinget/høringsvarsling i det hele tatt.
- Ingen automatisk gjenkjenning av at en paragraf ER endret på tvers av rettskilde-versjoner (kun at en
  ny `RettskildeEntitet`-versjon er importert med `ErstatterId`, se kategori 8 under — ingen diff på
  paragrafnivå beregnes eller lagres).
- `HandlingRegelverksreferanseEntitet` (`Entiteter.cs:615`) gir samme kobling for `Handling`, men
  peker KUN på dokumentnivå (`Eli`), ikke paragrafnivå, for handlinger seedet fra Oppgaveregisteret —
  svakere presisjon enn Tjeneste-koblingen.

**Konklusjon i praksis:** "gitt en paragraf, hvilke AV VÅRE registrerte tjenester har registrert den
som hjemmel" er svarbart som et lite rapport-endepunkt i dag. "Automatisk varsling når regelverket
faktisk endres" krever en heldekkende endringsdeteksjons-/varslingsmekanisme som ikke finnes —
ikke svarbart uten videre arbeid.

**Anbefaling for det delvis svarbare:** nytt read-only endepunkt, f.eks.
`GET /api/rettskilder/{id}/pavirkede-tjenester?eid=...`, i `RettskildeRepository.cs` (samme fil som
allerede har `ReferertAvAndreDokumenterAsync`) — spør `TjenesteRegelverksreferanser` og
`HandlingRegelverksreferanser` og returnerer treff. **Arbeidsmengde: liten** (én ny repository-metode
+ ett endepunkt, ingen skjemaendring). Presenteres naturlig som en ny seksjon på `RettskildeDetalj.tsx`
("Tjenester som viser til denne paragrafen"), ALDRI en egen "konsekvensanalyse"-side.

---

## 4. Dekningsgrad og datakvalitet (null-felt)

**Konklusjon: svarbart i dag med en enkel, gruppert spørring — det billigste elementet på hele
listen.**

Feltene som skal telles finnes direkte og er nullable nøyaktig der spørsmålet antar:
- `BegrepEntitet.LovreferanseEid` (`Entiteter.cs:830`, `string?`) — "citerbar kilde" for et begrep.
- `TjenesteEntitet` har ingen tilsvarende ETT felt, men "har ingen identifisert hjemmel" oversettes
  direkte til **"null rader i `TjenesteRegelverksreferanser` for denne `TjenesteId`"** — like enkelt å
  telle som et nullfelt, bare via en `LEFT JOIN ... WHERE referanse.Id IS NULL` i stedet for en
  `WHERE felt IS NULL`.
- `VilkarEntitet.JuridiskGrunnlagJson` (`Entiteter.cs:1028`, default `"[]"`) — "mangler juridisk
  grunnlag" = tom JSON-liste. Krever en enkel lengde-sjekk på JSON-strengen (EF Core oversetter ikke
  dette direkte til SQL på Postgres uten en rå spørring eller klient-side evaluering av et lite
  datasett — fortsatt triviell arbeidsmengde, bare ikke helt "gratis" LINQ).
- `RettskildeEntitet.VirksomhetId` (kategori 10/11) gir gratis gruppering "hvilken virksomhet/hvilke
  rettskilder" for samme spørring.

Alle disse feltene/koblingene er ekte, eksisterende kolonner/tabeller — ingen ny infrastruktur
nødvendig, kun en aggregerende spørring.

**Anbefaling:** ett nytt, samlet read-only endepunkt, f.eks. `GET /api/rapporter/dekningsgrad`, som en
liten ny `DekningsgradRapportTjeneste.cs` i `RegelIde.Data/` (samme "egen liten registertjeneste"-
mønster som `TjenesteEksportTjeneste.cs`) — teller nullfelt/manglende koblinger gruppert på
virksomhet og rettskilde, ett samlet JSON-svar (samme "les fra flere eksisterende registre, ingen ny
lagret tilstand"-prinsipp som `TjenesteEksportTjeneste` allerede følger). **Arbeidsmengde: liten.**
Presenteres best som en ny seksjon nederst på en eksisterende liste-side (f.eks. et lite sammendrag
øverst på `TjenesterListe.tsx`/`BegreperListe.tsx`), ikke en egen side.

---

## 5. Vilkårs- og regelgraf-spørring

**Konklusjon: delvis svarbart — skjemaet TILLATER gjenbruk teknisk, men det finnes ingen registrert
mekanisme for å OPPDAGE det, og sannsynligheten for reell gjenbruk i dagens data er lav.**

`VilkarEntitet` (`Entiteter.cs:1009`) er en bladnode, koblet inn i et tre via
`RegelnodeBarnEntitet.BarnId` (`Entiteter.cs:1111`, polymorf `BarnType='vilkar'|'regelnode'`). Det
finnes **ingen FK-begrensning som hindrer at samme `VilkarId` refereres fra `RegelnodeBarn`-rader
under to ulike regelnode-trær** (altså to ulike tjenesters vilkårstrær) — teknisk sett er
gjenbruk på tvers av tjenester mulig i skjemaet i dag.

Men i praksis: `VilkarEntitet.TjenesteId` (`Entiteter.cs:1021`, kommentar "Hvilken tjeneste dette
vilkåret er identifisert for") antyder at hvert `Vilkar` konseptuelt hører til ÉN tjeneste, og
`GeneriskMal` (`Entiteter.cs:1025`, fritekst-kode som "GM-VANDEL-PERSON", eksplisitt "ingen egen
registertabell i v1") er den ENESTE mekanismen som kunne signalisert "dette er samme underliggende
vilkårstype som et annet" — men den er ufullstendig fritekst uten validering, ikke en gjenbrukbar
node-referanse. Det finnes ingen "koble til eksisterende vilkår i stedet for å opprette nytt"-UI
(i motsetning til `HandlingTjenesteEntitet`s "Koble eksisterende handling"-mønster fra
`docs/13-backlog.md` §0, punkt 7, som ER bygget for `Handling`, men ikke for `Vilkar`).

**Konklusjon i praksis:** en spørring som teller "hvor mange DISTINKTE regelnode-trær refererer samme
`VilkarId`" er triviell å skrive og ville riktig rapportere 0 eller nesten 0 gjenbruk i dag — fordi
selve OPPRETTELSES-flyten alltid lager et nytt `Vilkar` per tjeneste, aldri kobler til et
eksisterende. En "gjenbrukbare vilkår"-rapport i dag ville altså mest sannsynlig vise et tomt/nesten
tomt resultat — korrekt, men ikke innsiktsfullt, fordi spørsmålet egentlig tester en mekanisme
(bevisst gjenbruk-ved-opprettelse) som ikke finnes ennå, ikke en skjult sammenheng i eksisterende data.
En `GeneriskMal`-gruppering ("vilkår med samme frie kode på tvers av tjenester") er mulig som en
svakere proxy, men verdien avhenger av at feltet faktisk er utfylt konsekvent — ikke verifisert her.

**"Motstridende vilkår mellom to regelverk"** og **"minste tilstrekkelige vilkårsett"**: begge krever
semantisk tolkning av vilkårsinnhold (er A og B faktisk motstridende, ikke bare distinkte) — ingen
del av dagens skjema koder dette. Ikke svarbart uten videre arbeid (og trolig et KI-assistert, ikke
et rent spørrings-, problem uansett).

**Anbefaling for den svake, men reelle delen (GeneriskMal-gruppering + gjenbrukstelling):** ett lite
endepunkt i en ny/utvidet `VilkarregisterTjeneste.cs`-metode, f.eks.
`GET /api/vilkar/gjenbruk-pa-tvers`. **Arbeidsmengde: liten**, men lav forventet innsiktsverdi gitt
hvordan opprettelsesflyten faktisk fungerer i dag — se prioriteringslisten nederst.

---

## 6. Tjeneste vs. forvaltningsoppgave — riktig klassifisering

**Konklusjon: ikke svarbart — ingen diskriminator finnes, og dette er en ren menneskelig
skjønnsvurdering ingen del av dagens modell fanger.**

Søk i hele `src/`-treet etter et felt eller en enum som skiller "ekte, borgerrettet tjeneste"
(`cpsv:PublicService`) fra "intern forvaltningsoppgave" gir ingen treff. `TjenesteEntitet.Type`
(`Entiteter.cs:470`, "rettighetstype: myndighetsutøvelse/ytelse/...") kommer nærmest, men skiller
rettighetstyper INNENFOR antakelsen om at raden allerede ER en ekte, ekstern tjeneste — den skiller
ikke "publisert som ekte tjeneste" fra "egentlig bare en intern prosess".

`Tjenestetype` (`Entiteter.cs:440`, fri streng) er den eneste kandidaten, men den er ukontrollert
fritekst uten kodeliste bak seg — en telling gruppert på denne strengen ville reflektere hva
forfatterne tilfeldigvis skrev, ikke en faktisk vurdert klassifisering.

Selve spørsmålet ("beskriver tjenestebeskrivelsen retten, eller bare søknadsprosessen") er dessuten
en tolkningsoppgave av FRITEKST-feltene `Beskrivelse`/`Formal`/`InnholdJson` — ingen strukturert
signal skiller de to i dag.

**Hva som konkret mangler:** en eksplisitt diskriminator-kolonne (f.eks.
`ErInternForvaltningsoppgave: bool` eller en kodeliste-validert `Registertype`), som i så fall må
SETTES av et menneske ved opprettelse/gjennomgang — ingen eksisterende data kan avlede den bakover.
Ikke byggbart som en spørring over eksisterende data; krever et nytt, manuelt utfylt felt FØR noe kan
telles.

---

## 7. Tverrsektoriell / tverrkommunal sammenligning

**Konklusjon: ikke svarbart uten videre arbeid — høstelaget er rått og domenekoblingen mangler
bevisst.**

Høstelaget (`EksternKildeEntitet`, `Entiteter.cs:1439`) dekker nå seks kilder
(Oppgaveregisteret, Altinn ressursregister, Altinn skjemaoversikt, Statsforvalter-tjenester,
fylkeskommune-dialogtjenester, kommune.no-tjenester — docs/13-backlog.md §0c–§0g). Hver kilde lagrer
`RaaJson` verbatim med kun nok metadata til idempotent oppslag (`Kildetype`+`EksternId`).
**Bevisst, gjentatt, dokumentert i HVER av §0c–§0g**: "ingen FK/kobling fra `EksternKildeEntitet` til
noen domeneentitet" — dette er ikke et glemt steg, det er en eksplisitt, gjentatt designbeslutning
inntil en uavklart arkitekturdiskusjon (`17-forvaltningsstruktur-master-tjeneste.md`/
`18-vurdering-rettighet-samhandling-modell.md`, Rettighet/Samhandling-splitten) er avgjort.

Kommune.no-kilden (§0g, ~15 332 rader i produksjon) er den mest direkte relevante for "tolker to
kommuner samme hjemmel ulikt" — men en slik sammenligning krever at to kommuners rader FØRST kobles
til samme nasjonale hjemmel. Det finnes ingen slik kobling i dag: `EksternKildeEntitet.RaaJson` har
ingen strukturert `TilRettskildeId`/`TilEid`-referanse, kun hva kilden selv leverte (fritekst-
kategori/tema, ingen paragrafhenvisning).

**Hva som konkret mangler:** en domenekobling fra høstet ekstern rad → nasjonal rettskilde/paragraf
(enten manuell kuratering eller en fremtidig automatisert matching), FØR noen tverrkommunal
sammenligning er mulig i det hele tatt. Dette er nøyaktig samme gap som kategori 9 deler (se under)
— ikke byggbart som en enkel spørring, krever ny kobling/infrastruktur først.

---

## 8. Endringshistorikk og sporbarhet

**Konklusjon: delvis svarbart for `Rettskilde` (reelt versjonert), IKKE svarbart for
`Tjeneste`/`Begrep`/`Vilkar`/`Regelnode` til tross for at de har de samme feltnavnene.**

Alle fem entitetene (`RettskildeEntitet`, `TjenesteEntitet`, `BegrepEntitet`, `VilkarEntitet`,
`RegelnodeEntitet`) har identisk `Versjon`/`Entitetsstatus`/`ErstatterId`-felt-trippel. Men den
FAKTISKE bruken er ulik, verifisert direkte i koden:

- **`RettskildeEntitet`**: `RettskildeImportTjeneste.OpprettNyVersjonAsync` (`RettskildeImportTjeneste.cs:210`)
  lager ved reimport en HELT NY rad ("ny rad, samme Eli, Versjon+1, `ErstatterId` til den gamle") og
  merker den gamle raden `Entitetsstatus="erstattet"`. Dette ER en ekte historisk kjede — den gamle
  teksten er fysisk bevart og kan hentes.
- **`BegrepEntitet`**: `BegrepsregisterTjeneste.OppdaterAsync` (`BegrepsregisterTjeneste.cs:88`)
  MUTERER raden i stedet (`begrep.Term = term; begrep.Definisjon = definisjon; ...
  begrep.Versjon++;`) — de gamle verdiene overskrives og går tapt, `ErstatterId` settes ALDRI ved en
  ordinær oppdatering. `Versjon` er dermed en ren teller uten noen tilhørende snapshot å telle OPP MOT.

Dette betyr: **"hvordan har definisjonen av et begrep utviklet seg over tid" er IKKE rekonstruerbart
i dag** — den forrige definisjonen finnes ikke lenger noe sted i databasen etter en oppdatering. Det
samme gjelder etter all sannsynlighet `Tjeneste`/`Vilkar`/`Regelnode` (samme mønster forventes i deres
respektive `OppdaterAsync`, ikke eksplisitt linjenummerbekreftet for alle tre her, men
`Versjon`-feltets bruk i `TjenesteregisterTjeneste.cs` sin kommentar om `SetNull`-kaskade ved sletting
bekrefter samme grunnmodell — ingen egen historikktabell).

**Hva som konkret mangler:** en ekte endringslogg/audit-tabell (rad per felt-endring ELLER en
snapshot-per-versjon-tabell) for `Begrep`/`Tjeneste`/`Vilkar`/`Regelnode` — enten ved å endre
`OppdaterAsync`-metodene til samme "ny rad + `ErstatterId`"-mønster `Rettskilde` allerede bruker
(størst endring, påvirker alle avhengige FK-er), eller en enklere tilleggstabell som logger
`(EntitetType, EntitetId, Felt, GammelVerdi, NyVerdi, EndretAv, Tidspunkt)` uten å røre eksisterende
oppdateringslogikk (mindre invasivt, men ikke "gratis" — krever en endring i HVER `OppdaterAsync`
for å skrive til loggen). Ikke svarbart som en spørring i dag — data som trengs finnes rett og slett
ikke.

**Det som ER svarbart nå** (kun for Rettskilde): "hvilken tekst gjaldt på tidspunkt T" via
`ErstatterId`-kjeden og `GyldigFra`/`GyldigTil`. Dette er allerede eksponert implisitt gjennom
eksisterende rettskilde-endepunkter — ingen ny kode nødvendig for akkurat den, delspørringen.

---

## 9. Omfang og digitaliseringspotensial

**Konklusjon: ikke byggbart i dag — samme domenekoblingsgap som kategori 7, pluss et manglende
"digitaliserbarhet"-signal som ikke finnes noe sted.**

"Hvor mange tjenester finnes reelt i norsk forvaltning" krever en pålitelig, deduplisert opptelling
på tvers av høstelagets ~15 332 (kommune) + ~4200 (Altinn ressurser) + ~900 (Oppgaveregister) + øvrige
rader — men høstelaget har verken innbyrdes deduplisering på tvers av KILDER (kun innad i hver kilde,
se docs/13-backlog.md §0g punkt 2 om Herøy-kommune-kollisjonen som eksempel på hvor subtilt dette er
selv INNAD i én kilde) eller kobling til `TjenesteEntitet` (kun 14 seedede rader der, jf. §0h/§2.2s
"14 seedede Tjeneste-rader"-referanse). Å telle "kandidater for digitalisering vs. faktisk
digitalisert" krever i tillegg et signal som ikke finnes i noe skjema: verken en andel
skjønnsmessige/regelbaserte vilkår (nærmeste proxy, `VilkarEntitet.Vurderingstype` — men den finnes
kun for vilkår som ER modellert i regel-IDE, en brøkdel av totalen) eller en kobling til "har digital
søknadsflate" (Altinn-ressursregisteret har `resourceType`, men ingen kobling tilbake til en
konseptuell rettighet/tjeneste å måle "digitalisert vs. ikke" mot).

**Hva som konkret mangler:** (1) domenekobling fra høstet rad til rettighetsbegrep (samme gap som §7),
(2) et helt nytt "digitaliserbarhet"-signal per tjeneste/rettighet (eksisterer ikke i noen form i
dag — måtte enten avledes fra `Vurderingstype`-fordelingen i et fullt modellert vilkårstre, som kun
finnes for de 14 seedede tjenestene, eller et helt nytt, manuelt vurdert felt). Ikke byggbart som en
rapport over eksisterende data — de nødvendige signalene finnes ikke.

---

## 10. Virksomhet ↔ rettskilde-kardinalitet

**Konklusjon: direkte svarbart i dag — ingen ny infrastruktur nødvendig.**

`RettskildeEntitet.VirksomhetId` (`Entiteter.cs:148`, `Guid?`, dokumentert eksplisitt: "NULL =
delt/nasjonal rettskilde ... Satt = virksomhetens egen lokale kilde") er nøyaktig feltet spørsmålet
trenger. "Hvor mange rettskilder forvalter én virksomhet" = `GROUP BY VirksomhetId` (ekskl. `NULL`).
"Hvor mange virksomheter er koblet til samme lov" krever litt mer: en delt/nasjonal rettskilde
(`VirksomhetId = NULL`) er per definisjon IKKE koblet til én bestemt virksomhet i dette feltet alene
— den reelle koblingen "hvilke virksomheter faktisk BRUKER denne loven" må hentes indirekte, via
`TjenesteRegelverksreferanser`/`VilkarEntitet.JuridiskGrunnlagJson` som PEKER til rettskilden, gruppert
på tjenestens/vilkårets `VirksomhetId` — fortsatt en triviell, eksisterende join, ikke ny
infrastruktur, bare én ekstra tabell inn i spørringen.

"Er koordineringsansvaret eksplisitt regulert" (siste underspørsmål) er derimot IKKE svarbart uten
videre — det krever tolkning av selve rettskildeteksten (finnes det en samarbeidsforskrift), ikke noe
strukturelt felt fanger dette i dag.

**Anbefaling:** ett nytt endepunkt, f.eks. `GET /api/rapporter/virksomhet-rettskilde-fordeling`, i
samme nye `DekningsgradRapportTjeneste.cs` som kategori 4 (naturlig å samle disse to nært beslektede
spørringene i samme lille tjeneste — begge er "grupper og tell"-rapporter over eksisterende
koblinger). **Arbeidsmengde: liten.**

---

## 11. Virksomhetens dekningskontroll

**Konklusjon: samme vurdering som kategori 10, med samme forbehold.**

De to første underspørsmålene ("er alle lover koblet" / "har virksomheten ansvar for en lov den ikke
har registrert forhold til") er en variant av kategori 10s spørring sett fra virksomhetssiden —
samme `RettskildeEntitet.VirksomhetId`-gruppering, presentert per virksomhet i stedet for
aggregert. Svarbart med samme lille rapport-endepunkt som kategori 10 (samme
`DekningsgradRapportTjeneste.cs`), naturlig som en utvidelse av den eksisterende, allerede bygde
tabellen i `VirksomhetDetalj.tsx` (som allerede viser `Myndighetstildeling`-rader for virksomheten,
jf. kategori 2) — IKKE en ny side.

"Blindsone" (har reelt forvaltningsansvar, men intet registrert forhold) er derimot **ikke svarbart**
— det krever en ekstern, autoritativ kilde til "hvem har FAKTISK ansvar" å sammenligne mot (f.eks.
en offisiell ansvarsfordelings-oversikt), noe som ikke finnes i modellen. Uten en slik ekstern
sannhetskilde kan modellen kun rapportere "hva er registrert", aldri "hva SKULLE vært registrert men
mangler".

**Anbefaling:** samme endepunkt/tjeneste som kategori 10, filtrert/presentert per `virksomhetId` i
stedet for aggregert på tvers. **Arbeidsmengde: liten** (marginal utvidelse av kategori 10s arbeid,
ikke et eget stykke arbeid).

---

## 12. Regelverkskompleksitet — kryssreferanser mellom lover

**Konklusjon: delvis svarbart i dag — grafstrukturen finnes og er god nok for telling, sykel-
deteksjon krever litt mer, men fortsatt en spørring, ikke ny infrastruktur.**

`RettskildeReferanseEntitet` (`Entiteter.cs:344`, bekreftet ved lesing — IKKE linje 344 for
`RettskildeReferanseEntitet` presist som forhåndsantatt i oppdraget, men verifisert her direkte) har
nøyaktig feltene som trengs: `FraNodeId` (en node i én rettskilde), `TilRettskildeId` (en ANNEN
rettskilde), `TilEid` (paragrafnivå i mål-dokumentet), pluss `Opprinnelse` (`'import'` = auto-fanget
Lovdata-kryssreferanse, `'manuell'` = brukerlagt) for å skille ekte kryssreferanser fra en juridisk
håndboks referanser til loven den kommenterer.

- **Utgående/inngående referanser per lov**: `GROUP BY` `FraNodeId`s eiende `RettskildeId` (utgående)
  og `TilRettskildeId` (inngående) — triviell aggregering, samme mønster
  `ReferertAvAndreDokumenterAsync` (`RettskildeRepository.cs`, allerede bygget for én rettskilde av
  gangen) allerede demonstrerer, bare aggregert på tvers av ALLE rettskilder i stedet for én.
- **Referansesykler** (lov A → B → C → A): krever en graf-traversering (DFS/BFS med et besøkt-sett)
  over `RettskildeReferanseEntitet`-kantene på DOKUMENT-nivå (ikke paragraf-nivå — en sykel mellom to
  paragrafer i samme lov er mindre interessant enn mellom to ulike lover). Dette er noe mer enn en
  ren SQL `GROUP BY`, men samme kompleksitetsklasse som `TjenesteavhengighetregisterTjeneste
  .LukkerSykelAsync` og `TjenestereiseGrafTjeneste.ByggAsync` (`TjenestereiseGrafTjeneste.cs:37`)
  allerede implementerer for tjenestegrafen — samme mønster, ny graf å traversere.

**Anbefaling:** ett nytt endepunkt, f.eks. `GET /api/rapporter/rettskilde-kryssreferanser`, i en ny
liten `RettskildeReferanseRapportTjeneste.cs` (eller som metode på eksisterende
`RettskildeRepository.cs`) — teller inn-/utgående kryssreferanser per rettskilde OG kjører en enkel
sykel-sjekk (gjenbruker BFS-mønsteret fra `TjenestereiseGrafTjeneste`/`LukkerSykelAsync`).
**Arbeidsmengde: liten til middels** (opptellingen er liten; sykel-deteksjonen er middels, siden den
krever egen algoritme-kode, ikke bare en spørring — men mønsteret finnes allerede å kopiere fra).
Presenteres som en ny seksjon på `RettskildeDetalj.tsx` ("Kryssreferanser — oversikt") eller et
samlet sammendrag øverst på `RettskilderListe.tsx`, ikke en egen visualiseringsside.

---

## 13. Borgerens reise gjennom forvaltningen

**Konklusjon: allerede substansielt bygget og verifisert — men på tjeneste-til-tjeneste-nivå, ikke
som en fullstendig, livssituasjons-drevet borgerreise ennå.**

`TjenesteavhengighetEntitet` (partial lest, `Entiteter.cs` rundt linje 697+) og
`TjenestereiseGrafTjeneste.cs` (ny, datert 2026-08-28 — samme dag som denne bestillingen) gir
FAKTISK det meste av det spørsmålet ber om:

- Multi-hopp BFS-traversering (`ByggAsync`, `TjenestereiseGrafTjeneste.cs:37`) fra en gitt
  "sentrum"-tjeneste, opptil `MaksDybde=5` hopp, over de rettede `TjenesteavhengighetEntitet`-kantene
  (`forutsetning_for`/`gir_mulighet_til`/`utlost_av`/`for`/`avhengig_av`/`input_til`).
- Krysser VIRKSOMHETSGRENSER allerede i dag: `docs/13-backlog.md` §0h bekrefter at
  avhengighetsgrafen bevisst er "virksomhet-uskjermet" (ingen ownership-filter), og
  `EksternTjenestereferanseEntitet` lar en kant peke til en tjeneste utenfor egen virksomhet, eller
  til en ekstern plassholder for en virksomhet som ikke er onboardet ennå.
- Filtrerbar på `Livshendelser` (fritekst-liste på `TjenesteEntitet`, §2026-08-20-runden) — akkurat
  "gitt en konkret livssituasjon" fra spørsmålets ordlyd.
- Frontend (`Tjenestereise.tsx`) finnes allerede og er verifisert (jf. `TjenestereiseGrafTjenesteTests.cs`).

**Det som IKKE er dekket ennå**: (1) eksterne referanser (tjenester hos ikke-onboardede
virksomheter) kan ikke traverseres videre — de vises, men er blindspor i grafen ("ingen ekte
Tjeneste-rad å hente data fra", kommentert eksplisitt i koden); (2) grafen viser kun det som er
MANUELT registrert som `Tjenesteavhengighet` — den er ikke utledet fra regelverket selv, så
fullstendigheten avhenger av hvor grundig noen har koblet inn avhengigheter (per i dag: 13 fasit-
tjenester rundt "Alminnelig skjenkebevilling", ikke et representativt tverrsnitt av norsk
forvaltning); (3) "hvor mange trinn avhenger av at et tidligere vedtak foreligger" er strengt tatt
allerede besvart av `Rel`-verdien `forutsetning_for` i grafen, men ingen egen opptelling/visning av
DETTE spesifikt er bygget (triviell utvidelse om ønsket).

**Vurdering, ikke anbefaling om nytt arbeid**: dette er kategorien der MINST gjenstår — det som
mangler er bredde i DATA (flere reelle tjenester/avhengigheter koblet inn på tvers av virksomheter),
ikke ny funksjonalitet. Ingen ny rapport/endepunkt anbefales her utover det som allerede finnes;
eventuell forbedring er ren datapopulering, ikke kode.

---

## Oppsummering — prioritert liste over de billigste/mest verdifulle å bygge først

Vurdert på (a) hvor mye faktisk allerede finnes å spørre mot, (b) arbeidsmengde, og (c) hvor
innsiktsfullt svaret faktisk blir (ikke bare teknisk mulig, men et svar som sier noe ekte):

1. **Kategori 10 + 11 (virksomhet↔rettskilde-kardinalitet + virksomhetens dekningskontroll)** —
   direkte svarbart på `RettskildeEntitet.VirksomhetId` alene, null ny infrastruktur, og svaret er
   umiddelbart nyttig (viser reelt hvilke lover er delt mellom virksomheter). Bygg disse to sammen —
   de deler nesten hele spørringen.
2. **Kategori 4 (dekningsgrad/datakvalitet — nullfelt)** — samme lave kostnad, og svaret er det mest
   direkte handlingsrettede av alle 13: det peker konkret på HVOR i katalogen kildehenvisninger
   mangler, noe et fagansvarlig kan rette opp umiddelbart.
3. **Kategori 12 (regelverkskompleksitet/kryssreferanser)** — litt høyere arbeidsmengde
   (sykel-deteksjonen), men datagrunnlaget (`RettskildeReferanseEntitet`) er allerede rikt fra Lovdata-
   importen selv (auto-fangede kryssreferanser, ikke noe som må tastes inn manuelt først) — høy
   svar-til-innsats-ratio.
4. **Kategori 3 (konsekvensanalyse, avgrenset til "hvilke av VÅRE tjenester peker på paragraf X")** —
   ikke like billig som 10/11/4, men gjenbruker et allerede bygget mønster
   (`ReferertAvAndreDokumenterAsync`) rett over på `TjenesteRegelverksreferanser`, og svarer på en
   reell, ofte stilt underspørsmål ("hvem rammes om vi endrer denne paragrafen") — selv om den fulle
   "automatisk impact-rapport ved lovendring"-visjonen forblir utenfor rekkevidde.

De resterende ni kategoriene (1, 2, 5, 6, 7, 8, 9, 13) er enten reelt blokkert på manglende
infrastruktur/data (1, 2, 7, 8, 9), en ren menneskelig skjønnskategori ingen datamodell fanger uten et
nytt, manuelt felt (6), lav forventet informasjonsverdi selv om spørringen er triviell (5), eller
allerede tilstrekkelig dekket av eksisterende arbeid og trenger data, ikke kode (13).
