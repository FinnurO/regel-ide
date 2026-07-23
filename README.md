# Regel-IDE

**Forvaltningsverktøy for å bygge digitale tjenester fra rettskilde til vedtak — for én virksomhet, med sporbarhet innebygd.**

> **Status:** v0.3 — ontologien for Vilkår/Regel/Unntak er låst (2026-07-23). Se [`docs/00-endringslogg-v0.1.md`](docs/00-endringslogg-v0.1.md) og [`docs/00-endringslogg-v0.2.md`](docs/00-endringslogg-v0.2.md) for hva som er endret og hvorfor.

Regel-IDE er referanseimplementasjonen av **Kildelaget** og **Regellaget** i [`digital-rettsstat`](https://github.com/FinnurO/digital-rettsstat) — verktøyet en virksomhet (f.eks. en kommune eller et direktorat) bruker til å gå fra rettskildetekst til en kjørbar, forklarbar og sporbar tjeneste. Bygget bevisst for **tverrfaglige team** (tjenestedesignere, jurister, fagansvarlige/saksbehandlere, utviklere) i samme verktøy, ikke for én rolle — jf. `digital-rettsstat` prinsipp 7. Testcase gjennom hele spesifikasjonen er **alminnelig skjenkebevilling** (alkoholloven) — samme regelverk som Helsedirektoratets "Alkoholfloken"-arbeid, omtalt i `digital-rettsstat/docs/04-norske-case.md`.

## To metaforer (begge gjelder samtidig)

- **Kompileringsplattform for digital forvaltning** — arkitekturen: rettskilder → begreper → vilkår/regler → data → kjørbar kode → saksbehandling → forklaring → vedtak.
- **IDE for juridiske regler** — brukeropplevelsen: navigator, editor, referanser, validering, AI-assistent, eksport, historikk, publisering.

Digital-rettsstats `06-regellaget.md` skiller mellom **Lag 1-editoren** (tekst — rettskildebiblioteket, kap. 4.3 under) og **Lag 2-editoren** (regel — vilkårs-/regeltreet, kap. 4.4). Regel-IDE er begge i samme skall, fordi begge skal brukes av de samme tverrfaglige teamene (prinsipp 7).

## Dokumenter

| Dokument | Innhold |
|---|---|
| [`docs/01-referansemodell.md`](docs/01-referansemodell.md) | Begrepsapparatet (regelkilde → regel → vilkår → fakta → beslutning), inkl. den låste Vilkår/Regel/Unntak-ontologien (§5) og Vedtak/skjønn-presiseringene. **Les denne først.** |
| [`docs/02-produktkrav.md`](docs/02-produktkrav.md) | Funksjonelle krav: skjermer, akseptkriterier, roller. PRD-nivå. |
| [`docs/03-domenemodell.md`](docs/03-domenemodell.md) | Entiteter og relasjoner, RBAC-matrise, livssykluser, publiseringsmodell, hendelsesmodell. |
| [`docs/04-api-kontrakter.md`](docs/04-api-kontrakter.md) | Systemgrensesnitt: hvilke operasjoner finnes (ikke full OpenAPI ennå). |
| [`docs/05-arkitektur-og-nfk.md`](docs/05-arkitektur-og-nfk.md) | Teknologivalg, eksportformater, ikke-funksjonelle krav, tekniske risikoområder. |
| [`docs/06-veikart.md`](docs/06-veikart.md) | Faseplan — rekkefølgen vi faktisk bygger i, og hvorfor. |
| [`docs/07-forklaringsmodell-api-avvik.md`](docs/07-forklaringsmodell-api-avvik.md) | Konkrete forslag til justeringer i `forklaringsmodell-api` for at begrepsbruken skal henge sammen på tvers av repoene. |

## Forhold til søsterrepoer

- **[`digital-rettsstat`](https://github.com/FinnurO/digital-rettsstat)** — rammeverket og charteret dette verktøyet realiserer (Lag 1–2 + tverrgående sporbarhet).
- **[`forklaringsmodell-api`](https://github.com/FinnurO/forklaringsmodell-api)** — kjøretidsmodellen for Sak/Faktum/Vurdering/Vedtak. Regel-IDE er *forfatterverktøyet*; `forklaringsmodell-api` er (deler av) *kjøretiden* som konsumerer det Regel-IDE produserer (eksporterte regel-/vilkårsdefinisjoner, kodelister, begreper).
- **[`forer-legeerklaering`](https://github.com/FinnurO/forer-legeerklaering)** — en konkret PoC på et annet fagområde; brukes som sanity check på at begrepsapparatet ikke er skjenkebevilling-spesifikt.

## Kilder lagt til grunn

- Dag Wiese Schartum, *Lovgivning i et digitalt samfunn* (CompLex 1/2025, Senter for rettsinformatikk/UiO) — særlig kap. 7 (automatiseringsvennlige begreper/opplysninger/behandlingsregler/strukturer) og kap. 5 (lovgivningsprinsipper: forklarbarhet, forståelighet, innbygging av rettsprinsipper og -regler).
- `digital-rettsstat` — rammeverk, veikart og kunnskapsgrunnlag (Rules as Code / Better Rules).
- Ekstern kravspesifikasjonsvurdering (v1, se [`docs/00-endringslogg-v0.1.md`](docs/00-endringslogg-v0.1.md)) — pekte på manglende RBAC-matrise, publiseringsmodell, API-kontrakter, livssyklusdiagrammer og eksplisitt DAG-krav. Alle er adressert i denne restruktureringen.
