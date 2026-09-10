# docs — hva er hva

35 dokumenter, ~13 000 linjer. Uten et statusvokabular er det ikke mulig å se hvilke som **binder**
arbeidet og hvilke som er **øyeblikksbilder** — og da leses de feil i begge retninger: et referat
behandles som en spesifikasjon, eller en bindende konvensjon overses.

Opprettet 2026-09-10 fordi ingen slik oversikt fantes.

## Statusene

| Status | Betyr | Skal den oppdateres? |
|---|---|---|
| **BINDENDE** | Gjeldende regler for arbeidet. Brytes de, er koden gal. | JA — holdes alltid sann |
| **REFERANSE** | Beskriver hvordan systemet ER. | JA — etter hver runde som endrer bildet |
| **SPESIFIKASJON** | Styrer en runde som ikke er ferdig bygget. | JA — til runden er levert, så → LEVERT |
| **LEVERT** | Designgrunnlaget noe faktisk ble bygget etter. | NEI — men avvik mot koden skal merkes |
| **REFERAT** | Øyeblikksbilde: mottatt innspill, bestilling, vurdering, transkripsjon. | NEI — aldri. Det ville forfalske et referat |
| **HISTORIKK** | Endringslogg for et tidligere utkast. | NEI |

**Den viktigste regelen:** et REFERAT skal *ikke* oppdateres når virkeligheten endrer seg. Det er en
nedtegnelse av hva noen mente på en dato. Er det utdatert, får det en peker til hva som ble gjort —
ikke ny tekst.

## Bindende

| Dok | Hva | Sist verifisert |
|---|---|---|
| [09-design-konvensjoner](09-design-konvensjoner.md) | UI-konvensjoner. Les FØR ny UI, oppdater ETTER designbeslutninger. | 2026-09-09 |
| [32-formal-roller-og-sporsmal](32-formal-roller-og-sporsmal.md) | Formålet, rollene, og §3 spørsmålene S1–S7 modellen skal kunne besvare. | 2026-09-08 |
| [../CLAUDE.md](../CLAUDE.md) | Arbeidsregler (§0 formålet, §13 slett grenen ved merge, §14 regex treffer prosaen). | 2026-09-10 |

## Referanse — beskriver systemet som det er

| Dok | Hva | Sist verifisert |
|---|---|---|
| [25-funksjonsoversikt](25-funksjonsoversikt.md) | Hva som faktisk finnes i appen i dag. Nærmeste ting til en sannhet om omfang. | 2026-09-08 |
| [03-domenemodell](03-domenemodell.md) | Entitetene og feltene. | 2026-08-02 |
| [01-referansemodell](01-referansemodell.md) | Ontologien Vilkår/Regel/Unntak (låst 2026-07-23, nå implementert). | 2026-09-10 |
| [02-produktkrav](02-produktkrav.md) | Kravspesifikasjonen. | 2026-08-02 |
| [04-api-kontrakter](04-api-kontrakter.md) | API-formen. | 2026-07-24 |
| [05-arkitektur-og-nfk](05-arkitektur-og-nfk.md) | Arkitektur og ikke-funksjonelle krav. | 2026-07-24 |
| [06-veikart](06-veikart.md) | Byggestegene 1–6 og retningsbeslutningene. | 2026-08-11 |
| [21-feltmapping-eksterne-kilder](21-feltmapping-eksterne-kilder.md) | Mapping fra eksterne kilder til våre felt. Ny kilde ⇒ ny seksjon her. | 2026-08-28 |
| [10-rules-as-code-landskap](10-rules-as-code-landskap.md) | Landskapet rundt. | 2026-07-30 |
| [07-forklaringsmodell-api-avvik](07-forklaringsmodell-api-avvik.md) | Avvik mot søsterrepoet. | 2026-07-23 |
| [autentisering](autentisering.md) | Altinn-innlogging. | 2026-07-31 |
| [deploy-altinn-app-cluster](deploy-altinn-app-cluster.md) | Deploy. | 2026-07-31 |

## Spesifikasjon — styrer en runde som ikke er ferdig

| Dok | Hva | Sist verifisert |
|---|---|---|
| [13-backlog](13-backlog.md) | **Trenger splitting** — §0–§0i er gjennomførte runder, §2/§4 peker framover og konkurrerer med 56 åpne GitHub-issues. To backlogger er verre enn én. | 2026-08-30 |
| [24-begrepsoppdagelse-plan](24-begrepsoppdagelse-plan.md) | M1–M17 mønsterkatalogen for begrepsoppdagelse. M1/M11 er bygget; resten er katalog. | 2026-08-30 |
| [29-gruppe-relasjon-spesifikasjon](29-gruppe-relasjon-spesifikasjon.md) | Gruppebegrep + virksomhetsrelasjoner. Del C (relasjonstyper) er bygget og data-styrt. | 2026-09-02 |
| [31-navneform-berikelse-snl-ssr](31-navneform-berikelse-snl-ssr-spesifikasjon.md) | SNL/SSR-oppslag og konfidens. §9 er konfidens-beslutningen. | 2026-09-09 |
| [23-tjeneste-modell-eksport-og-skjema](23-tjeneste-modell-eksport-og-skjema.md) | Eksportformatet. §6 har de to harde importproblemene. | 2026-08-28 |
| [14-byggesteg5-teknisk-design](14-byggesteg5-teknisk-design.md) | KI-agentene. Runde 1 bygget, resten er mal. | 2026-08-11 |
| [15-handbok-dokumentgraf-notat](15-handbok-dokumentgraf-notat.md) | Håndbok-laget. Lag 1 (rå kilde) er bygget og brukes nå også for Lovdata. | 2026-08-13 |
| [20-virksomhetskatalog-og-rollemodell](20-virksomhetskatalog-og-rollemodell.md) | Katalogen og rollene. §4/§7.2 låser at forvaltningsnivå aldri settes fra Brreg. | 2026-08-22 |

## Levert — designgrunnlag for noe som er bygget

| Dok | Hva |
|---|---|
| [08-byggesteg1-teknisk-design](08-byggesteg1-teknisk-design.md) | Rettskildebiblioteket. Bygget og i drift — 5899 rettskilder. |
| [12-fasit-handbok-leveranse](12-fasit-handbok-leveranse.md) | Fasiten for håndbok-leveransen. |
| [11-brukerflyt-ny-tjeneste](11-brukerflyt-ny-tjeneste.md) | Brukerflyten for ny tjeneste. |

## Referat — øyeblikksbilder. Oppdateres ALDRI

| Dok | Hva | Dato |
|---|---|---|
| [30-saksbehandlertilpasning-bestilling](30-saksbehandlertilpasning-bestilling.md) | Johanns bestilling, verbatim. Destillatet er `09` §14. | 2026-09-02 |
| [28-navnekandidat-presisjon-innspill](28-navnekandidat-presisjon-innspill.md) | Johanns observasjoner etter et korpussveip. Én beslutning er markert i teksten. | 2026-09-02 |
| [27-innsikt-sporsmal-vurdering](27-innsikt-sporsmal-vurdering.md) | Vurdering av 13 innsiktsspørsmål mot kodebasen. | 2026-08-30 |
| [22-tjeneste-side-redesign-brief](22-tjeneste-side-redesign-brief.md) | Underlag til et designverktøy. | 2026-08-27 |
| [18-vurdering-rettighet-samhandling-modell](18-vurdering-rettighet-samhandling-modell.md) | Vurdering av rettighet-modellen mot kode. | 2026-08-14 |
| [17-forvaltningsstruktur-master-tjeneste](17-forvaltningsstruktur-master-tjeneste.md) | Forvaltningsstruktur/mastertjeneste. | 2026-08-14 |
| [16-vurdering-rettskilde-til-tjenestebeskrivelse](16-vurdering-rettskilde-til-tjenestebeskrivelse.md) | Vurdering av en mottatt arbeidsprosess mot kode. | 2026-08-13 |

## Historikk

`00-endringslogg-v0.1.md`, `-v0.2.md`, `-v0.3.md` — hva som endret seg mellom utkastene av
referansemodellen.

`arkiv/` — erstattede dokumenter, med `-SUPERSEDED` i filnavnet.

## Andre steder det står noe

- **GitHub-issues** er den ekte backloggen (56 åpne). `13-backlog` §2/§4 overlapper og skal splittes.
- `kildegrunnlag/` — proveniens for eksterne kilder.
- `../data/kilder/*/README.md` — proveniens for hver rå datakilde. Disse er nedtegnelser, ikke
  dokumentasjon: de sier hvor en fil kom fra og når.
- `tjeneste-modell.schema.json` — maskinlesbart skjema for eksportformatet.

## Hva som gjenstår i oppryddingen

Se GitHub-issuet «Docs-runde 2». Kort: splitt `13-backlog`, oppdater `src/README.md` (208 linjer,
sist rørt 2026-07-30) og root-README, og arkiver de referatene som er helt overtatt av senere
beslutninger.
