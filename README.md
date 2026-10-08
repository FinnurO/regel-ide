# Regel-IDE

**Forvaltningsverktøy for å bygge digitale tjenester fra rettskilde til vedtak — for én virksomhet, med sporbarhet innebygd.**

> **Status:** v0.3 — ontologien for Vilkår/Regel/Unntak er låst (2026-07-23), og siden har appen fått
> et fullt saksbehandler-grensesnitt (`src/RegelIde.Web`), en virksomhetskatalog med roller og
> relasjoner — alle strukturutsagn (relasjoner, medlemskap, rolleinnehav, kompetanse, områder,
> organtilhørighet, klassenivå) i én typestyrt kanttabell med hjemmel eller kilde utenfor korpus,
> avgrensning og polaritet (issue #311); begrepene med gruppefunksjon er typet som klasse/rolle/område,
> virksomheter har en aktørtype, og organer som Stortinget og Kongen i statsråd er virksomheter (#310/#311), en navnekandidat-veiviser som fanger opp alle disse ved sveip AV manuell
> tagging, og en familie KI-forslagstjenester (begrep/tjeneste/handling, alltid med et menneske i
> godkjenn/avvis-loopen — se `docs/14-byggesteg5-teknisk-design.md`). Se
> [`docs/00-endringslogg-v0.1.md`](docs/00-endringslogg-v0.1.md),
> [`docs/00-endringslogg-v0.2.md`](docs/00-endringslogg-v0.2.md) og
> [`docs/00-endringslogg-v0.3.md`](docs/00-endringslogg-v0.3.md) for hva som er endret og hvorfor.
> **[`docs/25-funksjonsoversikt.md`](docs/25-funksjonsoversikt.md) er nærmeste ting til en sannhet
> om hva som faktisk finnes i appen akkurat nå** — denne READMEen gir bare et førsteinntrykk.

Regel-IDE er referanseimplementasjonen av **Kildelaget** og **Regellaget** i [`digital-rettsstat`](https://github.com/FinnurO/digital-rettsstat) — verktøyet en virksomhet (f.eks. en kommune eller et direktorat) bruker til å gå fra rettskildetekst til en kjørbar, forklarbar og sporbar tjeneste. Bygget bevisst for **tverrfaglige team** (tjenestedesignere, jurister, fagansvarlige/saksbehandlere, utviklere) i samme verktøy, ikke for én rolle — jf. `digital-rettsstat` prinsipp 7. Testcase gjennom hele spesifikasjonen er **alminnelig skjenkebevilling** (alkoholloven) — samme regelverk som Helsedirektoratets "Alkoholfloken"-arbeid, omtalt i `digital-rettsstat/docs/04-norske-case.md`.

## To metaforer (begge gjelder samtidig)

- **Kompileringsplattform for digital forvaltning** — arkitekturen: rettskilder → begreper → vilkår/regler → data → kjørbar kode → saksbehandling → forklaring → vedtak.
- **IDE for juridiske regler** — brukeropplevelsen: navigator, editor, referanser, validering, AI-assistent, eksport, historikk, publisering.

Digital-rettsstats `06-regellaget.md` skiller mellom **Lag 1-editoren** (tekst — rettskildebiblioteket, kap. 4.3 under) og **Lag 2-editoren** (regel — vilkårs-/regeltreet, kap. 4.4). Regel-IDE er begge i samme skall, fordi begge skal brukes av de samme tverrfaglige teamene (prinsipp 7).

## Strukturmodellen for forvaltningen

Hvem som finnes i forvaltningen, hvordan de henger sammen og hvem som har hvilken myndighet, hentet ut
av lovteksten med hjemmel på hver kobling: seks typer (rettssubjekt, organ, organisatorisk enhet, rolle,
klasse, område) og åtte koblinger, testet mot en fasit på 1 797 utsagn fra fem lover. Områderegisteret (#312) gir
fylker og kommuner fra Kartverket, domstolstrukturen fra inndelingsforskriften, statsforvalterne og helseregionene,
og svarer på «gitt kommune X: hvilket fylke, hvilken tingrett, statsforvalter og RHF?». Lettlest innføring
for lesere uten forkunnskap: [finnuro.github.io/regel-ide/strukturmodell/](https://finnuro.github.io/regel-ide/strukturmodell/).
Spesifikasjonen og målingene: [`docs/33-strukturmodell-aktor-omrade-kompetanse.md`](docs/33-strukturmodell-aktor-omrade-kompetanse.md)
(arbeidsplanen er epic #318).

## Dokumenter

**35+ dokumenter i `docs/` — [`docs/README.md`](docs/README.md) er den levende, oppdaterte indeksen**
(hva som er BINDENDE/REFERANSE/SPESIFIKASJON/LEVERT/REFERAT, og når hvert dokument sist ble
verifisert mot koden). Tabellen under er kun et førsteinntrykk til de mest sentrale dokumentene —
ved motstrid, stol på `docs/README.md` og selve koden, ikke denne listen.

| Dokument | Innhold |
|---|---|
| [`docs/01-referansemodell.md`](docs/01-referansemodell.md) | Begrepsapparatet (regelkilde → regel → vilkår → fakta → beslutning), inkl. den låste Vilkår/Regel/Unntak-ontologien (§5), skjønn/avklaringsbehov (§6.1) og Vedtak/skjønn-presiseringene. **Les denne først.** |
| [`docs/02-produktkrav.md`](docs/02-produktkrav.md) | Funksjonelle krav: skjermer, akseptkriterier, roller. PRD-nivå. |
| [`docs/03-domenemodell.md`](docs/03-domenemodell.md) | Entiteter og relasjoner, RBAC-matrise, livssykluser, publiseringsmodell, hendelsesmodell. |
| [`docs/04-api-kontrakter.md`](docs/04-api-kontrakter.md) | Systemgrensesnitt: hvilke operasjoner finnes (ikke full OpenAPI ennå). |
| [`docs/05-arkitektur-og-nfk.md`](docs/05-arkitektur-og-nfk.md) | Teknologivalg, eksportformater, ikke-funksjonelle krav, tekniske risikoområder. |
| [`docs/06-veikart.md`](docs/06-veikart.md) | Faseplan — rekkefølgen vi faktisk bygger i, og hvorfor. |
| [`docs/09-design-konvensjoner.md`](docs/09-design-konvensjoner.md) | **BINDENDE.** Designsystemet i praksis: temaoppsett, tokens, Card-alltid-rendret-mønsteret (§14), navigasjonsmønster. Les FØR ny UI. |
| [`docs/14-byggesteg5-teknisk-design.md`](docs/14-byggesteg5-teknisk-design.md) | KI-agentene: forslagsmønsteret (kø → godkjenn/avvis → proveniens med `AiForslagVersjon`/`GodkjentAv`) som `Begrepsforslag`/`Tjenesteforslag`/`Handlingsforslag` og navnekandidat-oppdagelsen alle følger. |
| [`docs/20-virksomhetskatalog-og-rollemodell.md`](docs/20-virksomhetskatalog-og-rollemodell.md) | Virksomhetskatalogen. Tildelinger, relasjoner og gruppemedlemskap er siden #311 strukturkanter — se `docs/33` §4.3. |
| [`docs/25-funksjonsoversikt.md`](docs/25-funksjonsoversikt.md) | **Nærmeste ting til en sannhet om hva som faktisk finnes i appen i dag.** |
| [`docs/32-formal-roller-og-sporsmal.md`](docs/32-formal-roller-og-sporsmal.md) | **BINDENDE.** Formålet, rollene, og §3-spørsmålene (S1–S9) modellen skal kunne besvare — hvorfor dette bygges. |
| [`docs/design-canvas/`](docs/design-canvas/) | 16-artboard visuell designreferanse (Claude Design-canvas), publisert som Artifact — brukt som fasit ved nye skjermer. |
| [`prototyper/`](prototyper/) | Interaktive HTML-mockuper fra Claude Design — frontend-siden, holdt bevisst atskilt fra det tekniske designet i `docs/`. |
| [`historikk/`](historikk/) | Det opprinnelige kravspesifikasjons-kildedokumentet (v1.0), beholdt for sporbarhet — ikke gjeldende krav. |
| [`nettside/`](nettside/) | Den offentlige forklaringssiden ([finnuro.github.io/regel-ide](https://finnuro.github.io/regel-ide/)), statisk generert fra ekte data (se `nettside/tools/eksporter-data.ps1`). Egen sak/label `nettside-tilbakemelding` for tilbakemeldinger på den. |

## Kjøre lokalt

Forutsetter .NET 10 SDK, Node.js, og Docker eller Podman.

1. **Start database** (fra repo-roten, matcher connection string i `src/RegelIde.Api/appsettings.json`):
   ```
   docker compose up -d
   ```
   (eller `podman compose up -d` / `podman-compose up -d`).

2. **Kjør migrasjoner** (kun første gang, og etter at noen har lagt til en ny migrasjon):
   ```
   dotnet ef database update --project src/RegelIde.Data --startup-project src/RegelIde.Data
   ```
   Krever `dotnet-ef`-verktøyet (`dotnet tool install --global dotnet-ef` hvis det ikke finnes fra før). API-et migrerer IKKE databasen selv ved oppstart.

3. **Start API-et** (fra repo-roten):
   ```
   dotnet run --project src/RegelIde.Api
   ```
   Lytter på `http://localhost:5187`.

4. **Start frontend** (i et eget terminalvindu):
   ```
   cd src/RegelIde.Web
   npm install   # kun første gang
   npm run dev
   ```
   Lytter på `http://localhost:5173`.

5. **(Valgfritt) Ekte KI-modell** — uten dette kjører KI-forslagsfunksjonene på en deterministisk stub. Sett `RegelIde:KiAgent:Leverandor`/`BaseUrl`/`Modell`/`ApiKey` via `dotnet user-secrets` fra `src/RegelIde.Api` (aldri i en committed fil) — se [`docs/14-byggesteg5-teknisk-design.md`](docs/14-byggesteg5-teknisk-design.md).

## Forhold til søsterrepoer

- **[`digital-rettsstat`](https://github.com/FinnurO/digital-rettsstat)** — rammeverket og charteret dette verktøyet realiserer (Lag 1–2 + tverrgående sporbarhet).
- **[`forklaringsmodell-api`](https://github.com/FinnurO/forklaringsmodell-api)** — kjøretidsmodellen for Sak/Faktum/Vurdering/Vedtak. Regel-IDE er *forfatterverktøyet*; `forklaringsmodell-api` er (deler av) *kjøretiden* som konsumerer det Regel-IDE produserer (eksporterte regel-/vilkårsdefinisjoner, kodelister, begreper).
- **[`forer-legeerklaering`](https://github.com/FinnurO/forer-legeerklaering)** — en konkret PoC på et annet fagområde; brukes som sanity check på at begrepsapparatet ikke er skjenkebevilling-spesifikt.

## Kilder lagt til grunn

- Dag Wiese Schartum, *Lovgivning i et digitalt samfunn* (CompLex 1/2025, Senter for rettsinformatikk/UiO) — særlig kap. 7 (automatiseringsvennlige begreper/opplysninger/behandlingsregler/strukturer) og kap. 5 (lovgivningsprinsipper: forklarbarhet, forståelighet, innbygging av rettsprinsipper og -regler).
- `digital-rettsstat` — rammeverk, veikart og kunnskapsgrunnlag (Rules as Code / Better Rules).
- Ekstern kravspesifikasjonsvurdering (v1, se [`docs/00-endringslogg-v0.1.md`](docs/00-endringslogg-v0.1.md)) — pekte på manglende RBAC-matrise, publiseringsmodell, API-kontrakter, livssyklusdiagrammer og eksplisitt DAG-krav. Alle er adressert i denne restruktureringen.
