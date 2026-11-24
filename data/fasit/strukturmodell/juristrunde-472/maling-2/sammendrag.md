# Juristrunde 472: presisjonsmåling 2 for funnkategorien «Hjemmel opphevet»

Måling 2 gjelder maskinfunnene etter rettingene i #472 PR 4 (regelversjon `472-pr4-2026-11-24`, docs/33 §5.21). Prosjekteieren bestilte den
2026-11-24: «Bygg rettingene, mål på nytt». **Målingen er gjort i kopien `regelide_472pr4`, før noe er skrevet mot den delte basen.** I kopien
er hjemmelsstatusen og funnregelen kjørt med PR 4, og der er 264 funn oppdaget. Juristen har bare lest fra kopien.

## Filene

| Fil | Innhold |
|---|---|
| `utvalg.json`, `utvalg.py` | Utvalget med frø 20261125, lagene og kvotene. Ingen av H01–H20 er med. Skriptet trekker det samme utvalget på nytt fra kopien |
| `inndata.py`, `inndata.json` | De 20 vurderingskortene (M01–M20): funnet, koblingene, hjemmelsstatusen, en kontekst fra korpus og modellererens vurdering i runde 1 |
| `a/utfordringer.json`, `a/sammendrag.md` | Juristens runde 1 |
| `a/svar.json` | Modellererens svar (aksepter 2) |
| `a/replikk.json` | Juristens replikk i runde 2 (godtar begge) |
| `resultat.json` | Endelig klasse per funn og tallene under |

## Utvalget

Populasjonen er de 264 funnene uten H01–H20, altså 250. Utvalget er stratifisert etter undertype og prefiks og om lag proporsjonalt, med fast frø
og uten tilbakelegging:
- hjemmel_opphevet: 12 (DEL 7, LTI 3, SF 2);
- blandede_hjemler: 4 (SF 3, LTI 1);
- uavklart: 2 (SF 1, DEL 1);
- videreforing_utlopt: 2 (SF 2).

Gjenstandsløs er ikke med. Det eneste funnet er H19 fra måling 1. Klassene er de samme som i måling 1.

## Resultat etter to runder

| | Riktig / (riktig + feil) | Presisjon (Wilson 95 %) |
|---|---|---|
| **Totalt** | 15/20 | **0,75 (0,53–0,89)** |
| **Uten #446** (M08, M09) | 13/18 | **0,72 (0,49–0,88)** |
| Uten de to kjente R8-hullene (M10, M16) | 15/18 | 0,83 (0,61–0,94) |
| hjemmel_opphevet | 9/12 | 0,75 (0,47–0,91) |
| blandede_hjemler | 2/4 | 0,50 (0,15–0,85) |
| uavklart | 2/2 | 1 (0,34–1) |
| videreforing_utlopt | 2/2 | 1 (0,34–1) |
| DEL | 6/8 | 0,75 (0,41–0,93) |
| SF | 7/8 | 0,88 (0,53–0,98) |
| LTI | 2/4 | 0,50 (0,15–0,85) |

- **Riktig (15):** M01–M03, M05, M07–M09, M11, M12, M14, M15, M17–M20.
- **Feil (5):** M04, M06, M10, M13 og M16.
- **Usikker:** ingen.
- **#446:** M08 er et rent endringsvedtak, og M09 er et rent opphevingsvedtak.
- **Sitat og eId:** riktige i alle kortene.
- **Vektet etter lagene** (238 av 250 funn ligger i lag som er med): om lag 0,73.

Til sammenligning ga måling 1 0,58 (0,36–0,77) på regelversjon PR 2. De to utvalgene er trukket på ulik måte, så forskjellen er et anslag, ikke en
test. Intervallene overlapper.

Modellereren satte 17 riktig og 3 feil før runde 1. Juristen fant at arveavgiftsfunnene (M04, M06) er feil.

## Feilmønstre (ikke bygget)

1. **Videreføringsformen: kortnavnet står ikke i opphevingssetningen** (M13). Viltressursloven § 61 andre ledd sier «Forskrifter gitt i medhold av
   viltloven gjelder også …». V4 leser kortnavnet bare når det står i opphevingssetningen, og der står bare «lov 29. mai 1981 nr. 38 om jakt og
   fangst av vilt».
   - Opphevingen er lest i Lovtidend.
   - 23 av de 264 funnene har viltressursloven som opphevende bestemmelse: blandede 12 (SF 11, DEL 1), hjemmel opphevet 10 og uavklart 1.
   - Dette er den største enkeltfeilen som står igjen, om lag 9 % av populasjonen.
   - **Forslag:** les kortnavnet også fra tittelen til den opphevede loven («viltloven»), eller fra andre steder i den opphevende loven.
2. **Opphevingen gjelder bare for nye tilfeller** (M04, M06). Lov 13. desember 2013 nr. 110 kap. I: «Opphevelsen gis virkning for gaver som ytes
   1. januar 2014 eller senere og arv etter dødsfall som skjer 1. januar 2014 eller senere.»
   - For eldre saker gjelder arveavgiftsloven fortsatt, uten sluttdato, og delegeringene etter den har fortsatt et virkeområde.
   - Ingen undertype passer. Det gjelder 6 funn i kopien.
   - **Juristens anbefaling:** en egen markering, «opphevet for nye tilfeller». Med «ukjent» ville også funnet for de nye tilfellene forsvinne, og
     der er hjemmelen faktisk opphevet.
3. **Populasjonen: R8-hullene** (M10, M16). Vedtakene er opphevet i en annen kilde i korpus, men R8 leser setningen som delvis:
   - «… nr. 802 av myndighet til … oppheves 1. januar 2019»;
   - «… nr. 1093 av utøvelsen av …».
   - Det er to av de åtte hullene som er nevnt i docs/33 §5.21. De rettes i R8 (#429), ikke her.

**Svake funn som teller som riktige:**
- M14 og M15 har fått ny hjemmel (som H11).
- I M17 og M19 er rollen ikke vurdert:
  - M17 skriver datoen «21 juni 2019» uten punktum. Det gir uavklart, der det skulle vært blandede.
  - M19 bruker nynorsk «heimel» i hjemmelsfeltet.
- For sentralbankloven 2019 står opphevingsdatoen som 2019-07-01 (dokumentnivå), men opphevingen trådte i kraft 1. januar 2020 (M01, M03 og M17).
  Premissmangelen om ikraftsettingen står i påstanden.

## Uenighet som står igjen

Ingen. Modellereren aksepterte M04 og M06, og juristen godtok svarene i runde 2. Juristen har ingen nye innvendinger mot de 18 andre kortene.

## Ikke kontrollert

- Hvor mange gamle arveavgiftssaker som fortsatt er åpne i 2026 (M04, M06).
- Overgangsforskrifter utenfor korpus.
- Rettspraksis.
