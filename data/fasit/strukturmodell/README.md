# Fasit: strukturutsagn i fem rettskilder

Målegrunnlaget for `docs/33-strukturmodell-aktor-omrade-kompetanse.md`.

| Fil | Innhold |
|---|---|
| `FORMAT.md` | Annotasjonsformatet. Også KONTRAKTEN for automatisk konvertering (`docs/33` §5.1). |
| `<kilde>.json` | Fasit: aktører + strukturutsagn med eksakt sitat og eId, for hovedkilden og dens ledsagende forskrifter. |
| `noder/<kilde>.json` | Nodetekstene fasiten er laget fra (gjeldende versjon, ikke opphevede noder), slik at målingen kan kjøres uten database. |
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
| domstolloven | 4 | 726 | 613 | 832 |
