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
| #345 lagmannsrett + sogner-kjeden | — | **Landet** (#346). Seeden er kjørt mot lokal `regelide`: 6 lagmannsrett → lagdømme og 28 tingrett `sogner_til` lagsogn som forslag, 357 utflatede lagsogn → kommune slettet gjennom tjenesten. 467 forslag venter på samlet godkjenning. Herøy 1515 og Våler 3114 har kommunenummer og territoriekant | Data 952, Api 337, tsc + vitest 77. Oppslaget for alle 357 kommuner er likt grunnlinjen før endringen (0 avvik). Kaldt i nettleseren: Karasjok og lagsogn Finnmark |
| #341 kompetanse med motpart | — | **Landet** (#351). Migrasjonen `KompetanseMedMotpart` er kjørt mot lokal `regelide`: 1261 → 1261 kanter, 3 R `klageinstans_for` → K `klage`, 19 M/I fikk hjemmelsstedet som `hjemmel_eid`. Fasiten konvertert med `konvertering-341-kompetanse.py` | Data 974/974, Api 338/338, tsc + vitest 77/77. Kaldt i nettleseren: Energiklagenemnda og RME |
| #352 rester etter #341 (oppnevningsfamilien, anke, forelegging, godkjenning, `settes_med`) | — | **Landet** (#354). Migrasjonen `OppnevningsfamilienOgRester` er kjørt mot lokal `regelide` (Down prøvd og snudd nøyaktig, så kjørt på nytt): 1261 → 1261 kanter (ingen lokale kanter med de flyttede kodene), konfigurasjonen 49 → 46 typer (−R velger, −R ankeinstans_for, −K utpeking, −K ansettelse, +G settes_med), familie personell 4 → 0. Fasit og KI-utdata konvertert med `konvertering-352-oppnevning.py` (1797 → 1797, 1043 → 1043). Hovedøktens tolkninger Johann må bekrefte: avsetting i familien oppnevning, undertype `anke`, at u16/u18 ikke er konvertert | Data 989/989, Api 339/339, tsc -b + vitest 77/77. Kaldt i nettleseren: Energiklagenemnda → «Legg til relasjon eller kompetanse» (familien Oppnevning, forelegging = kontroll, ingen velger/ankeinstans/utpeking/ansettelse; undertype-feltet for oppnevning og overprøving, ikke for klage) |
| #353 plikt overfor motpart (P) og nodetypen ordning | — | **Landet** (#358). Migrasjonen `PliktOgOrdning` er kjørt mot lokal `regelide` (Down prøvd og snudd nøyaktig, så kjørt på nytt): 1261 → 1261 kanter, konfigurasjonen 46 → 54 typer (P ×6, R forvaltes_av, G tilhorer), 0 kanter flyttet. Fasit og KI-utdata konvertert med `konvertering-353-plikt.py` (1797 → 1798, 77 til plikt etter juristgjennomgangen; 1043 → 1043, 43 til plikt). Folketrygden → ordning, forvaltes_av Helsedirektoratet slått opp i folketrygdloven § 21-11 a første ledd (kap. 5). Ingen data (Folketrygden, plikter) er lagt inn i den lokale basen. Hovedøktens tolkninger Johann må bekrefte: se PR-en | Data 1018/1018, Api 340/340, tsc -b (fersk .tsbuildinfo) + vitest 97/97. Mønster P 58,1/23,4 %, alle 92,7/48,2 %. IKKE åpnet kaldt i nettleseren (preview_start starter hovedrepoet, og basen var stoppet) |
| #355 avslutning speiler innsetting (avsetting/oppsigelse/avskjed, utnevning, konstitusjon, tilbakekall), forelegging etter rettsvirkning, ankeinstans avledet, ankeadgang i regellaget | — | **Landet** (#360). Migrasjonen `AvslutningSpeilerInnsetting` er kjørt mot lokal `regelide` (Down prøvd og snudd nøyaktig, så kjørt på nytt): 1261 → 1261 kanter, konfigurasjonen 54 → 53 (−K forelegging), 0 K forelegging-kanter, 1 proveniensrad. Fasit og KI-utdata konvertert med `konvertering-355-avslutning.py` (1798 → 1809, inkludert juristrundens 16 aksepterte rettinger; 1043 → 1043). Leksikon 2026-10-09.3. Ingen data (anke-kanter, klasse «tingrett») er lagt inn i den lokale basen | Se PR-en: Data- og Api-suitene, tsc -b + vitest, kald API-sjekk. Mønster 92,7/47,9 %, KI 36,5/21,1 %, union 58,2/55,4 % (samme prediksjoner, fasiten +11 rader) |
| #356 konverteringsrester etter #341/#352 (oppretting, «kan ikke delegeres», beslutning, delegeringsunntak som avgrensning, juristkortene fra #356 og #309) | `fasit-rester` | **PR åpen, venter på Johann (§19). Ikke merget.** `konvertering-356-fasitrester.py` (regler R0–R8, kortene R9, juristrunden R10) kjørt på fasit (1 809 → 1 802) og KI-utdata (1 043 → 1 042), idempotent (andre kjøring: 0 endringer). Ingen migrasjon: lokal `regelide` har ingen kanter rettingene gjelder. Juristrunden: 74 kort, 23 innvendinger, 21 akseptert, 2 delvis — godtatt i runde 2. Åpent for hovedøkta: familie for eierstyring (hfl § 16, u51/u56); domstolloven:u263 venter på Johann | Data- og Api-suitene, Strukturfasit-målingene regenerert (mønster 92,5/47,9 %, KI 36,8/21,3 %, union 58,2/55,6 %). Ingen UI berørt |
| #316 erstattede versjoner | — | Johann valgte **(a)** noder → `erstattet` + importfiks, og data-opprydding i samme sak. Kjøres etter #310/#311 | Kartlegging i kommentar på #316 |

**Johanns bindende beslutninger 2026-10-07:** #310 reklassifiseringslista godkjent som foreslått; #311 = A;
#309 = Johann gjennomgår selv (innføring lagt i arket); #317 = S8–S9; #316 = (a) + opprydding; #307 treffregel = tekstform ∪ varianter,
case-insensitivt; organer ligger som Virksomhet (Stortinget fra Brreg 971524960, «Kongen i statsråd» uten orgnr, hjemmel Grunnloven);
#311 kilde utenfor korpus har type (kgl_res|instruks|tildelingsbrev|vedtekter|styrevedtak|forarbeider|nettside_annet) og
dokumentasjon (primaer|sekundaer), begge påkrevd uten hjemmel og NULL med.

**Rekkefølge for migrasjoner (ikke stable, §15):** #310 → #311 → #330 → #312 → #316 (Johann 2026-10-08: #330 før #312).
