# ARBEIDSSTATUS

Arbeid som er I GANG (CLAUDE.md §12). Slettes når alt er landet.

## Strukturmodell-epicen #318 (startet 2026-10-07)

Retning fra Johann 2026-10-07: bevis at aktør-, område- og kompetansemodellen bærer et utvalg rettskilder
før mer arbeid på tjenester, handlinger og regler. Designgrunnlag: `docs/33`.

| Sak | Gren | Status | Verifisert hvordan |
|---|---|---|---|
| #306 fasit + docs/33 | — | **Landet** (#319) | Sitater maskinelt sjekket mot base; `designtest.py` gjengir docs/33 §6 |
| #317 S8–S9 i docs/32 | — | **Landet** (#320) | Kun docs |
| #307 mønsterkonvertering | — | **Landet** (#321) | Strukturfasit 19/19; 93,8 % presisjon / 45,3 % gjenfinning |
| #308 KI-konvertering | — | **Landet** (#322) | Data.Tests 909/909; KI 34,4 % / 19,2 %, union 57,2 % / 52,1 % (én live-kjøring) |
| #309 fasitgjennomgang | — | Venter på Johann: arket er https://claude.ai/artifact/KCkiu3gWhyoCSCi2hXRVqG (80 utsagn + 54 negative + 30 aktører, seed 309). Vurderinger leses med ArtifactData, samling `vurderinger` | Lagring testet med én skrevet/lest/slettet rad |
| #310 nodetype-akse | — | **Landet** (#323). Migrasjonen kjørt mot lokal `regelide` | Data 904/904, Api 340/340, tsc + vitest 77/77, kaldt i nettleseren |
| #311 kanttabell | `strukturmodell-kanttabell` | under arbeid. Johann: **A full konsolidering** + Stortinget/«Kongen i statsråd» som Virksomhet (organ-begrepskategorien fjernes) | — |
| #316 erstattede versjoner | — | Johann valgte **(a)** noder → `erstattet` + importfiks, og data-opprydding i samme sak. Kjøres etter #310/#311 | Kartlegging i kommentar på #316 |

**Johanns bindende beslutninger 2026-10-07:** #310 reklassifiseringslista godkjent som foreslått; #311 = A;
#309 = Johann gjennomgår selv (innføring lagt i arket); #317 = S8–S9; #316 = (a) + opprydding; #307 treffregel = tekstform ∪ varianter,
case-insensitivt; organer ligger som Virksomhet (Stortinget fra Brreg 971524960, «Kongen i statsråd» uten orgnr, hjemmel Grunnloven).

**Rekkefølge for migrasjoner (ikke stable, §15):** #310 → #311 → #312 → #316.
