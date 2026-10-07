# ARBEIDSSTATUS

Arbeid som er I GANG (CLAUDE.md §12). Slettes når alt er landet.

## Strukturmodell-epicen #318 (startet 2026-10-07)

Retning fra Johann 2026-10-07: bevis at aktør-, område- og kompetansemodellen bærer et utvalg rettskilder
før mer arbeid på tjenester, handlinger og regler. Designgrunnlag: `docs/33`.

| Sak | Gren | Status | Verifisert hvordan |
|---|---|---|---|
| #306 fasit + docs/33 | `strukturmodell-fasit` | PR åpen, venter Johanns bekreftelse (§19) | Sitater maskinelt sjekket mot base; `designtest.py` gjengir tallene i docs/33 §6 |
| #307 mønsterkonvertering + måleoppsett | `strukturmodell-monster` (fra `strukturmodell-fasit`, ingen PR før #306 er merget — ikke stable) | under arbeid | — |
| #310 nodetype-akse | — | venter Johanns godkjenning av reklassifiseringslista | — |
| #311 kanttabell | — | venter Johanns valg A/B/C | — |
| #309 fasit-gjennomgang | — | venter: hvem gjennomgår | — |

**Rekkefølge:** #310 → #311 → #312 har hver sin migrasjon og skal ikke stables. #316 (erstattede versjoner)
landes før eller etter dem hvis den trenger migrasjon.
