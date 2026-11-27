# Juristrunde 472, måling 4 del 2: #514 (enkeltvedtak som overgangsregelen dekker)

Måling 4 del 2 gjelder #514 (docs/33 §5.21, merknaden «#514»). Prosjekteieren ga klarsignal 2026-11-27. Målingen er gjort i kopien `regelide_502`
etter kaldkjøringen med #502 og #514, før noe er skrevet mot den delte basen. Juristen har bare lest fra kopien, koden og Lovdata.

## Filene

| Fil | Innhold |
|---|---|
| `inndata.py`, `inndata.json` | 17 kort: V01–V05 er alle treffene i første versjon av regelen, K01–K12 er kontroll |
| `a/utfordringer.json`, `a/sammendrag.md` | Juristens runde 1 |
| `a/svar.json` | Modellererens svar (aksepter 4, delvis 1). Tre rettinger er bygget |
| `a/replikk.json` | Juristens replikk i runde 2 (godtar 4, presiserer 1) |
| `resultat.json` | Endelige klasser og tallene |

## Resultat etter to runder og rettingene

| | Riktig / n | Presisjon (Wilson 95 %) |
|---|---|---|
| **Treffene (V01–V04)** | 4/4 | **1,00 (0,51–1,00)** |
| Treffene før rettingen (V01–V05) | 4/5 | 0,80 (0,38–0,96) |
| Kontroll (K01–K12 og V05) | 13/13 | 1,00 (0,77–1,00) |
| **Totalt** | 17/17 | **1,00 (0,82–1,00)** |

- **N14 og N16 er dekket** (V01 og V02), og funnene forklares bort. Det samme gjelder Ansgar (V03) og NLA (V04).
- **V05 (TONO)** var feil som treff: vedtaket har også en generell regel. En tittel som begynner med «Forskrift», leses nå ikke som enkeltvedtak.
- **De statlige høgskolevedtakene** (K06–K09) er organiseringsvedtak, ikke enkeltvedtak. Juristen godtok valget.
- Ingen oversette enkeltvedtak i SF og INS (juristens søk i titlene). Anslaget i måling 3b (om lag 8 av 12) var for høyt.

## Rettet etter runde 1

1. Kunnskapsstatusen er fortolkning når dekningen bygger på lesingen av vedtakstypen (V02).
2. TONO: tittelen «Forskrift …» (V05).
3. Bare ordet «vedtak»/«enkeltvedtak» dekker, ikke «vedtaksmyndighet».

## Uenighet som står igjen

Ingen.

## Kjente begrensninger (ikke bygget)

- Vilkår 3 fanger ikke en generell regel uten overskrift. Rettingen for TONO ser bare på tittelen.
- Statlig organ kjennes bare på navnet.
- «vedtak» alene som dekning er ikke prøvd.
- Utenfor #514: vedtektene for Norsk Tipping (K01) ble gitt på nytt i 2022.

## Ikke kontrollert

Om departementet har «fastsatt noe annet» etter uhl. 2024 § 17-3, om TONO-bevillingen er fornyet utenfor korpus, forarbeider og rettspraksis.
