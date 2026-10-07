# ARBEIDSSTATUS

Arbeid som er I GANG (CLAUDE.md §12). Slettes når alt er landet.

## Strukturmodell-epicen #318 (startet 2026-10-07)

Retning fra Johann 2026-10-07: bevis at aktør-, område- og kompetansemodellen bærer et utvalg rettskilder
før mer arbeid på tjenester, handlinger og regler. Designgrunnlag: `docs/33`.

| Sak | Gren | Status | Verifisert hvordan |
|---|---|---|---|
| #306 fasit + docs/33 | — | **Landet** (#319) | Sitater maskinelt sjekket mot base; `designtest.py` gjengir docs/33 §6 |
| #317 S8–S9 i docs/32 | — | **Landet** (#320) | Kun docs |
| #307 mønsterkonvertering | `strukturmodell-monster` | PR #321 åpen, venter Johanns bekreftelse (§19) | Strukturfasit 19/19 grønne etter rebase; Data.Tests 879/879 før rebase (master fikk bare docs siden) |
| #308 KI-konvertering | `strukturmodell-ki` (fra `strukturmodell-monster`, ingen PR før #321 er merget) | under arbeid | — |
| #309 fasitgjennomgang | — | Venter på Johann: arket er https://claude.ai/artifact/KCkiu3gWhyoCSCi2hXRVqG (80 utsagn + 54 negative + 30 aktører, seed 309). Vurderinger leses med ArtifactData, samling `vurderinger` | Lagring testet med én skrevet/lest/slettet rad |
| #310 nodetype-akse | `strukturmodell-nodetype` | PR åpen, venter Johanns bekreftelse (§19). Migrasjonen ER kjørt mot lokal `regelide` (API-oppstart 2026-10-07) | Data.Tests 904/904, Api.Tests 340/340, tsc + vitest 77/77; kaldt i nettleseren: BegrepDetalj (Klasse-tag), veiviser steg 2 (ingen forhåndsvalgt type, knapp sperret), VirksomhetDetalj (Rettssubjekt) |
| #311 kanttabell | — | Johann valgte **A: full konsolidering nå**. Starter når #310 er landet | — |
| #316 erstattede versjoner | — | Johann valgte **(a)** noder → `erstattet` + importfiks, og data-opprydding i samme sak. Kjøres etter #310/#311 | Kartlegging i kommentar på #316 |

**Johanns bindende beslutninger 2026-10-07:** #310 reklassifiseringslista godkjent som foreslått; #311 = A;
#309 = Johann gjennomgår selv; #317 = S8–S9; #316 = (a) + opprydding; #307 treffregel = tekstform ∪ varianter,
case-insensitivt.

**Rekkefølge for migrasjoner (ikke stable, §15):** #310 → #311 → #312 → #316.
