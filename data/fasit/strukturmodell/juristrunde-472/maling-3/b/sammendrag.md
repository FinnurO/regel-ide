# Juristrunde 472: måling 3b, etter prosjekteierens avgjørelse 2026-11-25 (alternativ (a))

Prosjekteieren valgte 2026-11-25 alternativ (a) fra måling 3: en videreføring med dekningen «dokumenttype ikke nevnt» bærer ikke alene, heller
ikke når ingenting faller. Er alle premissene som bærer, ikke vurdert (rolle NULL eller bare slik videreføring), er samlet status «uavklart».
AK2 (FOR-1993-12-17-1314) er fortsatt videreført. Bygget i #501 (PR #506), docs/33 §5.21.

**Målingen er gjort i kopien `regelide_501b`, før noe er skrevet mot den delte basen.** Juristen har bare lest fra kopien. Prosjekteieren ba om
én runde, fordi avgjørelsen endret mer enn feilene fra måling 3: den ga 163 nye funn.

## Filene

| Fil | Innhold |
|---|---|
| `status-etter-avgjorelsen.json` | Måling 3-utvalget (E01–E15, K01–K08) lest på nytt i kopien |
| `inndata.py`, `utvalg.json`, `inndata.json` | Utvalget av de 163 nye funnene (frø 20261127) og de 16 vurderingskortene (N01–N16), med modellererens vurdering |
| `a/utfordringer.json`, `a/sammendrag.md` | Juristens runde |
| `a/svar.json` | Modellererens svar (aksepter 5) |
| `resultat.json` | Endelig klasse per kort og tallene under |

## Måling 3-utvalget, lest på nytt

- **E09, E10, E11 og E15** står nå som oppdaget, undertype uavklart. De fire teller som riktige: juristen godtok «uavklart» som et forsvarlig
  svar i replikken i måling 3.
- **E08** er fortsatt forklart bort (videreført). Alternativ (a) tetter ikke E08, fordi andre hjemler bærer og er vurdert. Det sa juristen også i
  replikken.
- **E12 og E13** er fortsatt forklart bort (#446, ikke kortnavnet). SF-kortene (E01–E07, E14) og kontrollkortene er uendret.
- **Endret: 12/15, 0,80 (0,55–0,93).** Uten #446: 12/13, 0,92 (0,67–0,99). Kontroll: 7/8, 0,88 (0,53–0,98). Totalt: 19/23, 0,83 (0,63–0,93).

## De nye funnene (163)

Alle har undertype uavklart: DEL 150, INS 11, LTI 1, SF 1. Før avgjørelsen var forskriftene «videreført» og ga ikke funn. Utvalget er
stratifisert etter prefiks: DEL 12, INS 2, LTI 1, SF 1.

| | Riktig / n | Presisjon (Wilson 95 %) |
|---|---|---|
| **Totalt** | 11/16 | **0,69 (0,44–0,86)** |
| DEL | 10/12 | 0,83 (0,55–0,95) |
| INS | 1/2 | 0,50 (0,09–0,91) |
| LTI | 0/1 | 0 (0–0,79) |
| SF | 0/1 | 0 (0–0,79) |
| Vektet etter lagene | | om lag 0,80 |

- **Riktig (11):** N01–N04, N06–N08, N10–N13. For delegeringsvedtak er «uavklart» riktig.
- **Feil (5):**
  - **N14 og N16 (dekningen):** enkeltvedtak i INS og SF (status som høgskole, tillatelse til Posten Norge). Overgangsregelen nevner enkeltvedtak,
    så vedtaket er dekket. Regelen leser vedtakstypen av basen (SF = forskrift, INS = instruks), ikke av innholdet. Juristen anslår at om lag 8 av
    de 12 INS- og SF-funnene har samme feil (lest av titlene).
  - **N05 (tekstlesingen):** delegeringen gjelder endringslovens egen myndighet, men hjemmelsteksten nevner ikke delen, så endringsloven følges
    til hovedloven. Feilen fantes før; avgjørelsen gjør bare at den blir et funn.
  - **N15 (populasjonen):** instruksen er opphevet ved forskrift 2022 nr. 874, som ikke er i korpus.
  - **N09 (dekningen):** funnet står, men undertypen burde vært hjemmel opphevet. Ordningen i straffeloven 1902 § 79 fjerde ledd finnes ikke i
    den nye loven [juristens slutning].
- **Usikker:** ingen.

## Spørsmål til prosjekteieren (ikke bygget)

Juristens forslag: når overgangsregelen nevner enkeltvedtak, skal dekningen være «dekket» for et SF- eller INS-vedtak som er et enkeltvedtak
(tillatelse, godkjenning eller status til en navngitt part), ikke en delegering eller instruks. Å avgjøre det krever at vedtaket leses.

## Uenighet som står igjen

Ingen. Modellereren godtok alle fem innvendingene (to av dem hadde modellereren selv satt som feil).

## Ikke kontrollert

- De 147 nye funnene utenfor utvalget, kort for kort.
- Sparebankloven § 60 og forretningsbankloven § 44 fra 1961 (N06), og ekomloven § 2-7 a (N12).
- Forarbeider og rettspraksis.
