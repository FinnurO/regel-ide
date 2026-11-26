# Juristrunde 472: måling 3 etter #501 (PR 5, kortnavnet fra tittelen)

Måling 3 gjelder endringen i #501 (#472 PR 5, regelversjon `472-pr5-2026-11-25` for hjemmelsstatusen, docs/33 §5.21): kortnavnet til en opphevet
lov leses fra tittelen («lov 29. mai 1981 nr. 38 om jakt og fangst av vilt (viltloven)») og godtas som mål i V4 når det er entydig. **Målingen er
gjort i kopien `regelide_501`, før noe er skrevet mot den delte basen.** Juristen har bare lest fra kopien.

I kopien endret PR 5 statusen for 73 hjemmelsrader, alle til viltloven 1981. 23 av de 264 funnene ble forklart bort, alle med viltressursloven
som opphevende lov. Ingen andre funn ble endret.

## Filene

| Fil | Innhold |
|---|---|
| `utvalg.py`, `utvalg.json` | Utvalget med frø 20261126: 15 endrede funn (E01–E15, uten M13) og 8 kontrollfunn (K01–K08) |
| `kortnavn.json` | Kortnavnet regelen leser per opphevet lov (fra rapporten med `?medRader=true`) |
| `inndata.py`, `inndata.json` | De 23 vurderingskortene, med funnet før PR 5, radene etter, kortnavnet og konteksten, og modellererens vurdering i runde 1 |
| `a/utfordringer.json`, `a/sammendrag.md` | Juristens runde 1 |
| `a/svar.json` | Modellererens svar (aksepter 8, og én merknad) |
| `a/replikk.json` | Juristens replikk i runde 2 (godtar 4, presiserer 5) |
| `resultat.json` | Endelig klasse per kort og tallene under |

## Utvalget

- **Endret (15 av 22):** funn som sto som oppdaget før PR 5 og som PR 5 forklarte bort. M13 er utelatt. Lagene er undertypen før PR 5 og prefikset:
  blandede SF 7 og DEL 1, hjemmel opphevet DEL 3, LTI 2 og SF 1, uavklart DEL 1.
- **Kontroll (8):** funn som fortsatt står, der en hjemmel som faller, er en lov med kortnavn i tittelen. Fire der kortnavnet er entydig, men regelen
  ikke fant noen V4-setning (kirkeloven, ligningsloven to ganger, omstillingslova). Fire der kortnavnet også brukes om den nye loven, så regelen ikke
  gir svar (sentralbankloven to ganger, revisorloven, statistikkloven).

Klassene: et endret kort er riktig når forskriften faktisk er videreført, så funnet ikke skal stå. Et kontrollkort er riktig når funnet holder, og
regelen ikke har oversett en videreføring med kortnavnet.

## Resultat etter to runder

| | Riktig / n | Presisjon (Wilson 95 %) |
|---|---|---|
| **Totalt** | 15/23 | **0,65 (0,45–0,81)** |
| **Endret** | 8/15 | **0,53 (0,30–0,75)** |
| Endret uten #446 (E12, E13) | 8/13 | 0,62 (0,36–0,82) |
| Endret SF | 8/8 | 1 (0,68–1) |
| Endret DEL | 0/5 | 0 (0–0,43) |
| Endret LTI | 0/2 | 0 (0–0,66) |
| **Kontroll** | 7/8 | **0,88 (0,53–0,98)** |
| Kontroll, kortnavnet entydig | 3/4 | 0,75 (0,30–0,95) |
| Kontroll, kortnavnet tvetydig | 4/4 | 1 (0,51–1) |

- **Riktig (15):** E01–E07, E14, K02–K08.
- **Feil (8):** E08–E11 og E15 (dekningen), E12 og E13 (annet, #446), K01 (populasjonen).
- **Usikker:** ingen.
- **Anslag for alle 23 endrede** (lest av radene, ikke kort for kort): om lag 12 riktige. De 12 SF-forskriftene er dekket, de 8 DEL-vedtakene har
  alle dekningen «dokumenttype ikke nevnt», og de 3 LTI-vedtakene er endringsforskrifter.

Modellereren satte alle 23 som riktige før runde 1, med en merknad om at DEL-vedtakene var svake.

## Feilmønstre

1. **Delegeringsvedtak regnes som videreført** (E08–E11, E15). Paragraf 61 viderefører bare «forskrifter», og regelen setter selv dekningen
   «dokumenttype ikke nevnt» (fortolkning). Når ingen annen premiss faller, blir den samlede statusen likevel «videreført», fordi regelen fra
   PR 4 (`BaererVurdert`, H14) bare slår inn når noe faller. Myndigheten etter viltloven §§ 7 og 39 finnes ikke lenger, og det er gitt nye
   delegeringer etter viltressursloven. **Ikke bygget. Spørsmål til prosjekteieren**, med to alternativer fra juristen:
   - **(a) Svak:** når ingenting faller, og alle premissene som bærer, er ikke vurdert (rolle NULL eller bare videreført med «dokumenttype ikke
     nevnt»), er samlet status uavklart. Samme vilkår som H14. Tetter E09–E11 og E15, men ikke E08. AK2 (FOR-1993-12-17-1314) er uendret, fordi den
     også har en vurdert, aktiv premiss.
   - **(b) Sterk:** en videreføring med «dokumenttype ikke nevnt» regnes ikke som videreføring, så hjemmelen faller. Tetter alle fem, men AK2 blir
     blandede hjemler.
   - Telt i kopien (direkte premisser, uten rekursjon, et anslag): (a) endrer om lag 178 forskrifter (DEL 160, INS 13, LTI 4, SF 1), (b) om lag 194
     (DEL 175, INS 13, LTI 5, SF 1). 239 forskrifter har minst én slik videreføring.
2. **En særskilt oppheving blir oversett** (E12, E13). Begge er endringsforskrifter (LTI) til forskriftene 1984 nr. 1878 og 1879, som ble opphevet
   ved forskrift 2026 nr. 1158 kap. II. Regelen leser den generelle videreføringen og stopper der. Utfallet (ikke funn) er ufarlig, men statusen er
   feil, og funnet skulle stått med premissmangelen #446, som M08 og M09 i måling 2. Feilen gjelder ikke kortnavnet.
3. **K01:** vedtaket er selv opphevet ved LTI/forskrift/2021-11-14-3414 kap. I, i formen «Delegering ‹dato› … av …», som er et kjent R8-hull (#502).
   I tillegg finnes en videreføring med kortnavnet i en forskrift fra et annet organ: kirkeordningen § 44 (SF/forskrift/2019-03-30-2307),
   «Forskrifter og andre bestemmelser gitt av Kirkemøtet … i medhold av kirkeloven gjelder også …». V2a søker bare i lover og forskrifter med
   «overgang» i tittelen, og krever dato og nummer. Det er en egen sak, som må måles for seg.
4. **Ledd-nummeret:** korpusversjonen av § 61 mangler opphevingsleddet, som bare står i den kunngjorte teksten. Det som i korpus er §61/ledd-2, er
   tredje ledd på Lovdata. Sitatet er riktig. Feilen ligger i korpusdataene og gir ingen klassefeil.

**Det som holder:** de åtte SF-forskriftene er gitt i medhold av viltloven og dekket av § 61. «Viltloven» er entydig. For K02–K08 finnes ingen
videreføring i korpus, heller ikke med kortnavnet. Der kortnavnet brukes om to lover (K05, K07, K08), går det altså ikke tapt noe når regelen ikke
gir svar.

## Uenighet som står igjen

Ingen. Modellereren godtok alle åtte innvendingene. Juristen godtok svarene. For DEL-vedtakene presiserte juristen at spørsmålet bør ha to
alternativer, og fastholdt ikke at «hjemmel opphevet» er det eneste riktige svaret: «uavklart» er forsvarlig fra en maskinregel som ikke fortolker.

## Ikke kontrollert

- De 7 endrede funnene utenfor utvalget, kort for kort.
- Forarbeidene til viltressursloven og rettspraksis.
- Rekursjonen i tellingen for alternativene (a) og (b).
