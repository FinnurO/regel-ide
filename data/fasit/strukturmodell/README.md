# Fasit: strukturutsagn i fem rettskilder

Målegrunnlaget for `docs/33-strukturmodell-aktor-omrade-kompetanse.md`.

| Fil | Innhold |
|---|---|
| `FORMAT.md` | Annotasjonsformatet. Også KONTRAKTEN for automatisk konvertering (`docs/33` §5.1). |
| `<kilde>.json` | Fasit: aktører + strukturutsagn med eksakt sitat og eId, for hovedkilden og dens ledsagende forskrifter. |
| `noder/<kilde>.json` | Nodetekstene fasiten er laget fra (gjeldende versjon, ikke opphevede noder), slik at målingen kan kjøres uten database. |
| `maling-monster.md` | **[Ny 2026-10-07, #307]** Måling av mønsterkonverteringen (`MonsterStrukturkonverterer`) mot fasiten: presisjon/gjenfinning per kanttype, kilde og mønster, vanligste feil, forkastede mønstre. GENERERT av `src/RegelIde.Data.Tests/Strukturfasit/` — ikke rediger for hånd. |
| `maling-ki.md` | **[Ny 2026-10-07, #308]** Måling av KI-konverteringen (`KiStrukturkonverterer`) side om side med mønsterlaget og unionen: presisjon/gjenfinning per kanttype, kilde og type, kastede rader per årsak, kall, tokens og kostnad. GENERERT — live av `KiStrukturkonvertererLiveMalingTests` (gated, `REGELIDE_KI_LIVE_MALING=1`), og regenerert uten nettverk fra `ki-utdata/` av `KiMalingRapportTests`. Ikke rediger for hånd. |
| `ki-utdata/` | **[Ny 2026-10-07, #308]** Utdata fra live-kjøringen: KI-dokumentet per kilde i fasit-formatet (`oppdagelseskilde = "ki:<modell>"`) og `kjoring.json` (modell, instruksavtrykk, kall, tokens, alle kastede rader med årsak). Lagret fordi en KI-kjøring verken er reproduserbar eller gratis. |
| `designtest.py` | Klassifiserer hvert utsagn mot dagens og revidert modell (`docs/33` §6). `python designtest.py`. |

## Proveniens

- **Nodetekst:** Lovdata, hentet via regel-ide sin Lovdata-import (NLOD 2.0, kildeangivelse påkrevd —
  samme lisens og kanal som `data/kilder/`). Eksportert fra lokal base 2026-10-07, filtrert på
  `rettskilder.entitetsstatus = 'gjeldende'` (se `docs/33` §7.5 om erstattede versjoner).
- **Fasit:** annotert av KI-agenter (Claude) 2026-10-07, én agent per kilde, alle noder lest. Hvert sitat
  er maskinelt verifisert som eksakt delstreng av nodeteksten. **Ikke menneskelig verifisert** — se
  egen sak om stratifisert gjennomgang før terskler låses.

| Kilde | Rettskilder | Tekstnoder | Aktører | Utsagn |
|---|---|---|---|---|
| sameloven | 5 | 144 | 162 | 285 |
| energiloven | 4 | 339 | 52 | 254 |
| helse-og-omsorgstjenesteloven | 1 | 285 | 63 | 192 |
| spesialisthelsetjenesteloven | 3 | 378 | 70 | 302 |
| domstolloven | 4 | 726 | 540 | 764 |

[ENDRET, issue #312, 2026-10-08] Domstolloven hadde 613 aktører og 832 utsagn før den systemiske rettelsen under.

## Rettelser

- **#312 — domstollovens inndelingsdel (2026-10-08).** Johanns funn fra fasitkontrollen (#309, kommentarene på #312):
  rettskretsen har ikke eget navn, og rettssted og kommune er samme område. `rettelse-312-domstolinndeling.py`
  (kjørt én gang, idempotent) har: fjernet de 28 oppfunne rettskrets-aktørene (`rk_*`); gjort de 357
  «rettskrets består av kommune»-radene om til «tingrett `har_ansvarsomrade` kommune» (samme id, eId og sitat) og
  fjernet de 28 «tingrett har ansvarsområde rettskrets»-radene; gjort «lagsogn består av rettskrets» (28) om til det
  teksten sier, «tingrett `annet:sogner_til` lagsogn»; fjernet 56 rader som bare beskrev rettskretsen (28 AVLEDEDE
  «rettskrets del av lagdømme», 24 «gruppert under fylkesgruppe», 4 «del av fylke»); slått 45 av 61 rettssteder
  sammen med kommune-aktøren med samme navn, og gjort de 16 andre til område med `del_av` kommunen (kommunen fra
  Kartverkets SSR, avgrenset til tingrettens egne kommuner — `kilde_utenfor_korpus: true`). 0 uløste steder.
  Lagsogn og lagdømme er beholdt som områder. Hver endret rad har `verifisert_av`. Måling før/etter står i PR-en
  for #312 og i `docs/33` §5.4.
- **#353 — plikt overfor motpart og ordning (2026-10-09).** `konvertering-353-plikt.py` (deterministisk, idempotent, også på
  `ki-utdata/`): 79 utsagn fra samarbeid/bistand/informasjonsdeling/konsultasjon/finansiering (det `docs/33` §4.4 holdt
  utenfor) er kategorien `plikt` med modalitet; Folketrygden og Energifondet er entitetstype `ordning`. For
  `forvaltes_av` er **én node fra folketrygdloven** (§ 21-11 a første ledd, «Helsedirektoratet skal forvalte kapittel 5 …»)
  lagt i `noder/spesialisthelsetjenesteloven.json`, og folketrygdloven står som ledsagende kilde — bare den ene noden, loven
  er ikke lest i sin helhet. Spesialisthelsetjenesteloven har dermed 379 tekstnoder og 303 utsagn.
