# src/ — Byggesteg 1: konverteringspipeline + database + les-API

.NET 8-løsning. Jf. `docs/06-veikart.md` byggesteg 1 og `docs/08-byggesteg1-teknisk-design.md`:

- `RegelIde.Kildekonvertering` — konverteringspipelinen (`LovdataKonverterer.Konverter`).
- `RegelIde.Kildekonvertering.Tests` — xUnit-tester mot de ekte, fullstendige
  dokumentene i `data/kilder/raw-lovdata/` (ikke syntetiske utdrag).
- `RegelIde.Data` — EF Core + PostgreSQL, skjemaet fra §2 i teknisk design (låst, ikke
  endret her) + `RettskildeImportTjeneste` som persisterer et `KonverteringResultat`.
- `RegelIde.Data.Tests` — kjører migrasjonen og importtjenesten mot en ekte,
  embedded Postgres-instans (se eget avsnitt under — ingen Docker/Podman nødvendig).
- `RegelIde.Api` — HTTP-API som **gir ut** rettskilder fra databasen (se eget avsnitt under).

```bash
dotnet test src/RegelIde.Kildekonvertering.Tests
dotnet test src/RegelIde.Data.Tests
dotnet test src/RegelIde.Api.Tests
docker compose up -d   # eller: podman-compose up -d / podman compose up -d
dotnet run --project src/RegelIde.Api   # krever kjørende Postgres, se Swagger på /swagger
```

## Database: PostgreSQL uten Docker/Podman i denne byggeøkten

Verken Docker eller Podman (Podman-CLI-en fantes, men VM-en/maskinen bak var ikke startet)
var tilgjengelig i miljøet denne økten ble bygget i. Løst med
[`MysticMind.PostgresEmbed`](https://github.com/mysticmind/mysticmind-postgresembed) — laster
ned og kjører ekte Postgres-binærfiler direkte som en lokal prosess, ingen container-runtime
nødvendig. Brukt **kun i tester** (`RegelIde.Data.Tests`, `RegelIde.Api.Tests`) for å verifisere
migrasjonen og importlogikken mot ekte Postgres (partial unique index, GIN-fulltekstindeks,
check-constraints — alt bekreftet fungerende, ikke bare kompilerende). `docker-compose.yml` i
repo-roten er den tiltenkte veien for faktisk lokal kjøring av `RegelIde.Api`, men er **ikke
verifisert kjørende** i denne økten — selve skjemaet er det, uavhengig av hvordan Postgres
faktisk startes.

**To testprosjekter som begge starter embedded Postgres kolliderte** når hele løsningen
testes samlet (`dotnet test` uten prosjektfilter) — løst med eksplisitt, forskjellig port per
fixture (`55432`/`55433`) og unik `instanceId`, se kommentarer i
`EmbeddedPostgresFixture.cs`/`EmbeddedPostgresApiFixture.cs`.

**Design-time-migrasjoner:** `RegelIdeDbContextFactory.cs` brukes kun av `dotnet ef migrations
add …` (peker på en placeholder-connection-string, ikke en ekte database).

## RegelIde.Api — les-API for rettskilder

Bygget etter eksplisitt instruks ("Sett et API for å gi ut rettskilder", 2026-07-24). Kjører nå
mot ekte Postgres via `RegelIde.Data` (ikke lenger in-memory, se git-historikk for den
mellomliggende in-memory-varianten). Migrerer databasen og sår de tre kjente
fixture-dokumentene ved oppstart hvis den er tom (`Program.cs`) — en utviklings-bekvemmelighet,
ikke en generell importmekanisme.

**Endepunkter** (`RettskildeRepository.cs`, `Program.cs`):
- `GET /api/rettskilder` — sammendragsliste (id, ELI, tittel, kortnavn, kildetype).
- `GET /api/rettskilder/{id}` — full metadata + kanonisk AKN-XML.
- `GET /api/rettskilder/{id}/noder` — hele nodetreet (flat liste med eId/parentNodeId, for tre-navigasjon).
- `GET /api/rettskilder/{id}/noder/oppslag?eid=…` — én node ved eId. eId gis som
  query-parameter, ikke rutesegment — en eId er en full ELI-URI med både `://` og flere
  skråstreker (`.../§1-1/ledd-1`), upraktisk/tvetydig i selve URL-stien.
- `GET /api/rettskilder/{id}/referanser` — kryssreferansene funnet i løpeteksten.

**`{id}` er databaseradens Guid, ikke datokode.** Det låste skjemaet (§2 i teknisk design) har
ingen egen "datokode"-kolonne — kun en (nullable) ELI, som selv inneholder skråstreker og
derfor ikke passer som rutesegment. Guid-en er den naturlige, alltid URL-sikre nøkkelen for et
databasebacket API. (Den forrige in-memory-varianten av dette API-et brukte datokode som nøkkel
— endret i samme slag som databasen ble koblet inn.)

7 integrasjonstester (`RegelIde.Api.Tests`, `WebApplicationFactory` + embedded Postgres) mot de
samme ekte dokumentene — ingen mocking, verken av repositoryet eller databasen.

## Referanse-stubber (§3.1 steg 6) er nå faktisk implementert

`RettskildeImportTjeneste` løser eksterne kryssreferanser mot databasen: finnes rettskilden
(primær eller stub) fra før, gjenbrukes den; ellers opprettes en stub
(`importrolle='referanse'`, `akn_xml=NULL`, `tittel` satt til ELI-en siden ingen ekte tittel er
tilgjengelig før stubben forfremmes ved faktisk import). Bekreftet med ekte data: alkoholloven
§ 9-4 ledd-3 viser til markedsføringsloven (ikke importert i dette settet) og oppretter en stub.

**To reelle bugs funnet under dette arbeidet og rettet:**
- Samme kryssreferanse kan forekomme flere ganger i samme ledd (samme mål lenket til to ganger
  i løpeteksten) — `UNIQUE(fra_node_id, til_rettskilde_id, til_eid)` er der nettopp for å
  forhindre dette (jf. kommentaren i §2), men importtjenesten prøvde å sette inn duplikatet i
  stedet for å deduplisere selv. Rettet med en enkel `HashSet`-sjekk før innsetting.
- (Se forrige runde i denne fila for øvrige rettinger i selve konverteringspipelinen.)

## Datakvalitetsfunn: fixture-filene var dobbelt feilkodet

`data/kilder/raw-lovdata/*.html` var på disk kodet med en mojibake-feil (UTF-8-tekst
der norske tegn var kodet feil én gang for mye — sannsynligvis en cp1252→UTF-8-
konvertering kjørt på tekst som allerede var UTF-8). Dette er rettet i denne
byggeøkten ved å skrive filene tilbake med korrekt UTF-8 (samme innhold, kun
tegnkoding endret — se git-historikk). `LovdataKonverterer.Konverter` forutsetter
nå korrekt UTF-8 inn; `DekodCp1252TilUtf8` dekker det *ekte* scenarioet (rå bytes
rett fra Lovdatas bulk-datasett), ikke denne engangsrettingen.

## Bevisste utvidelser utover det låste designet (§6 i teknisk design dekker kun to åpne punkter — disse er nye, oppdaget under implementasjon)

Låst gjennom tre QA-runder før koding, men noen strukturelle detaljer i den ekte
HTML-en var ikke dekket av spec'en og krevde et implementasjonsvalg. Alle er
lavrisiko/reversible, men bør nevnes for jurist/fagansvarlig ved side-ved-side-
verifiseringen (AK-3.3.6) og ev. tas opp i en fjerde QA-runde:

1. **Underinndeling (romertall) sin eId** — ikke i §1.2s tabell. Valgt:
   `{kapittel-eId}/rom-{romertall}` (f.eks. `kap-3/rom-I`).
2. **Punkt-i-punkt (flernivå punktlister)** — bekreftet i ekte data
   (alkoholforskriften § 6-2, § 14-3). Håndteres rekursivt: et punkt kan ha egne
   underpunkt med samme `PunktEid`-mønster (`{punkt-eId}/punkt-N}`).
3. **Punkt med flere direkte legalP-"ledd"** (§ 14-3 punkt 14) — bladteksten
   konkatenerer alle direkte legalP-barns tekst i dokumentrekkefølge, i stedet for
   å kreve nøyaktig ett.
4. **Datokode uten løpenummer** — eldre lover/forskrifter identifisert av Lovdata
   med bare dato (f.eks. `LOV-1927-04-05`, ingen `-NN`) forekommer i kryssreferanser.
   ELI-en utelater da løpenummer-segmentet.
5. **`regelIde:`-XML-navnerom** — design-eksemplene i §1.1/§1.3 bruker prefikset
   `regelIde:` uten å vise en `xmlns:regelIde`-deklarasjon. Denne byggeøkten
   deklarerer `xmlns:regelIde="https://regel-ide.no/ns/akn-utvidelse/1.0"` — en
   plassholder-URI, ikke bekreftet/registrert noe sted.
6. **Kapitteloverskrift kan selv ha `data-repealeddate`** (alkoholloven kapittel 4,
   på `<h2>`-nivå, ikke bare på `<article>` som §3.2 dekker) — **ikke fanget opp**
   i denne byggeøkten. Selve paragrafene i kapittelet er ikke opphevet (kun
   utvalgte enkeltparagrafer, som håndteres korrekt), så dette påvirker ikke
   testcaset, men et fremtidig kapittel-nivå opphevet-flagg er ikke implementert.
7. **Romertall kan være selve hovedkapittelnummereringen**, ikke bare en
   underinndeling inni et arabisk-nummerert kapittel (§3.2 sitt eksempel er fra
   alkoholloven kapittel 3). Forvaltningsloven nummererer sine toppnivå-kapitler
   "Kapittel I", "Kapittel II" osv. — håndteres uten kodeendring, siden
   kapittelnummeret uansett behandles som en opak streng.
8. **Dokumentnivå-merknad direkte i `documentBody`** (`<article class="defaultP">`)
   — forvaltningsloven har et varsel om at hele loven oppheves fra en fremtidig
   dato, plassert før første kapittel. Behandlet som endringshistorikk/metadata,
   ikke tekstinnhold — samme prinsipp som `changesToParent`, ingen node produseres.

## Ekstern code review (Copilot, 2026-07-24) — funn og oppfølging

Kjørt mot diff'en for koden over. Triagert punkt for punkt mot faktisk kode/ekte data
(ikke blindt implementert) — se commit-historikk for de faktiske rettingene.

**Reelle bugs funnet og rettet:**
- Fotnotetekst duplikerte etiketten (f.eks. "1 Jf. EØS-avtalen …" i stedet for
  "Jf. EØS-avtalen …") — `<span class="footnoteLabel">` ble telt med i løpeteksten
  i tillegg til at `Fotnote.Etikett` hentet den separat.
- Tekst før og etter en hoppet-over liste smeltet sammen uten mellomrom
  (f.eks. "herunderDet skal …") — bekreftet reelt i alkoholforskriften § 7-2 sin
  `<p class="leddfortsettelse">`. Samme feil fantes ved skjøten mellom flere
  direkte legalP-"ledd" i samme punkt (§ 14-3 punkt 14). Begge rettet ved å sette
  inn et mellomrom-segment ved skillet; ev. doble mellomrom ryddes bort med
  `KollapsDobleMellomrom` (kun kosmetisk — påvirker ikke tekst_hash, som uansett
  normaliserer all whitespace, §3.4).

**Testdekning lagt til** (verifiserte påstander om manglende dekning):
lettermerkede paragrafer (§ 1-4a), kapittel med bokstav (3A) sin eId, kryssreferanse
til lettermerket paragraf i et ANNET dokument (§ 9-4 → markedsføringsloven § 43a —
ekte data), datokode uten løpenummer (`LovdataIdentifikatorerTests`, direkte
enhetstester uten HTML), flere fotnoter på samme paragraf (syntetisk — forekommer
ikke i de ekte dokumentene), tom/ufullstendig HTML gir kontrollert `FormatException`
i stedet for stille feil, norske bokstaver (æøå) i slugifisering.

**Sjekket, men bekreftet IKKE et problem:** slugifisering av "Ø/Æ/Å" — Copilot fryktet
at bokstaven forsvinner (f.eks. "Økonomiske" → "konomiske"). Verifisert med en
syntetisk test: `ToLowerInvariant()` kjører før de eksplisitte æ/ø/å→ae/o/a-erstatningene,
så dette fungerer korrekt. Testen beholdes som regresjonssikring.

**Bevisst IKKE endret (uenig med anbefalingen, eller lav verdi gitt prosjektets
prinsipper):**
- *"Parseren bør tåle ekstra wrapper-elementer rundt kjente strukturer."* Dette
  strider mot det låste prinsippet i §3.3 om at ingen gjettet fallback skal
  produseres — en uventet HTML-struktur skal feile høylytt med kontekst, ikke
  gjette seg forbi. Verdt en bevisst diskusjon med Johann hvis det blir aktuelt,
  ikke noe jeg endrer stille.
- URL-enkodet paragraftegn i href — ingen av de 498 hrefene i de to ekte dokumentene
  bruker URL-enkoding (alle bruker bokstavelig `§`). Uendret href faller uansett
  trygt tilbake til synlig tekst (ingen kryssreferanse spores, ingen krasj) hvis et
  ukjent mønster skulle dukke opp — akseptert restrisiko, ikke fikset spekulativt.
- `regelIde:`-navnerom-URI-en er allerede notert over som en plassholder som bør
  fryses før tverrsystem-validering — ingen ny handling utover det.

## Flervirksomhet (v0.3) — se `docs/00-endringslogg-v0.3.md`

Regel-IDE skal potensielt brukes av opptil ~1000 offentlige virksomheter — én delt
driftsatt løsning, ikke én instans per virksomhet (for kostbart i den skalaen).
`RegelIde.Data` har derfor en `virksomheter`-tabell og `virksomhet_id` på
virksomhetseide entiteter (`Rettskilde` nullable — `NULL`=delt/nasjonal kilde;
`TekstTagg` påkrevd; `Proveniens` nullable). Databaseskjemaet håndhever isolasjon
(virksomhet-scopede unike indekser, bekreftet med tester mot ekte Postgres), men
**API-laget filtrerer ikke på virksomhet ennå** — det finnes ingen autentisering i
`RegelIde.Api`. Innloggingsløsning er besluttet (Ansattporten), men ikke
implementert. Se `docs/00-endringslogg-v0.3.md` for fullstendig liste over hva som
gjenstår.

## Bevisst utenfor scope nå

Tre-navigasjon-UI, tekstmerking/tagging-UI (selve UI-et, ikke databasestøtten —
`tekst_tagger`-tabellen finnes), Ansattporten-integrasjon (OIDC/tokenvalidering),
API-lags tilgangskontroll på virksomhet, onboarding-flyt for nye virksomheter,
Lovdata API-nøkkel-integrasjon (leser fortsatt fra lokale filer/bulk-datasett).
