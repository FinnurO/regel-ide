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
| #311 kanttabell | — | **Landet** (#329). Migrasjonen `20261007202146_InnforStrukturkanttabell` er kjørt mot lokal `regelide` (29 → 29 kanter, gamle tabeller droppet) | Data 918/918, Api 333/333, tsc + vitest 77/77, kaldt i nettleseren (VirksomhetDetalj, BegrepDetalj, veiviser steg 4) |
| #330 kodeharmonisering | — | **Landet** (#333) | Se #330 |
| #312 områderegister | — | **Landet** (#342). Domstolkantene er forslag og venter på samlet godkjenning | Se #312 |
| #345 lagmannsrett + sogner-kjeden | `omrader-lagmannsrett` | **PR åpen, venter på Johann (§19).** Seeden er kjørt mot lokal `regelide`: 6 lagmannsrett → lagdømme og 28 tingrett `sogner_til` lagsogn som forslag, 357 utflatede lagsogn → kommune slettet gjennom tjenesten. 467 forslag venter på samlet godkjenning. Herøy 1515 og Våler 3114 har kommunenummer og territoriekant | Data 952, Api 337, tsc + vitest 77. Oppslaget for alle 357 kommuner er likt grunnlinjen før endringen (0 avvik). Kaldt i nettleseren: Karasjok og lagsogn Finnmark |
| #316 erstattede versjoner | — | Johann valgte **(a)** noder → `erstattet` + importfiks, og data-opprydding i samme sak. Kjøres etter #310/#311 | Kartlegging i kommentar på #316 |

**Johanns bindende beslutninger 2026-10-07:** #310 reklassifiseringslista godkjent som foreslått; #311 = A;
#309 = Johann gjennomgår selv (innføring lagt i arket); #317 = S8–S9; #316 = (a) + opprydding; #307 treffregel = tekstform ∪ varianter,
case-insensitivt; organer ligger som Virksomhet (Stortinget fra Brreg 971524960, «Kongen i statsråd» uten orgnr, hjemmel Grunnloven);
#311 kilde utenfor korpus har type (kgl_res|instruks|tildelingsbrev|vedtekter|styrevedtak|forarbeider|nettside_annet) og
dokumentasjon (primaer|sekundaer), begge påkrevd uten hjemmel og NULL med.

**Rekkefølge for migrasjoner (ikke stable, §15):** #310 → #311 → #330 → #312 → #316 (Johann 2026-10-08: #330 før #312).
