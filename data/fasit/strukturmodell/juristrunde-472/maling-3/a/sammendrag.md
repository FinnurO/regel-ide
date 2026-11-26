# Juristutfordring, #472 måling 3 (#501, PR 5), runde 1 (E01–E15, K01–K08)

Grunnlag: kortene i `maling3-kort.json` (23 kort), kopien `regelide_501` (bare SELECT med `default_transaction_read_only=on`) og Lovdata på nett.
- Viltressursloven § 61, overgangsforskriften 2026 nr. 1158 og de nye delegeringene etter viltressursloven (2026 nr. 1153, 1378 og 1550) er lest i korpus.
- DEL 2002-995 og 1996-1008, SF 2005-247 og fem av kontrollvedtakene er slått opp på Lovdata. Viltloven § 7 og § 39 er lest i den opphevede teksten.
- For alle 23 forskriftene er det søkt i korpus etter datoen og nummeret i andre kilder.
- For kontrollkortene er det søkt i hele korpuset etter kortnavnet og den fulle tittelen sammen med et videreføringsord.
- Koden for samlet status (`Hjemmelsstatus.BaererVurdert`, `HjemmelsstatusSamlet`) og V2a-søket (`HjemmelsstatusRegel`, `Hjemmelstekstlesing.Videreforinger`) er lest.

## Antall per dom og alvor

| Dom | Antall | Kort |
|---|---|---|
| holder | 15 | E01–E07, E14, K02–K08 |
| innvending | 8 | E08–E13, E15, K01 |
| kontroller | 0 | |
| prinsipiell | 0 | |

| Alvor (bare innvending) | Antall | Kort |
|---|---|---|
| hoy | 4 | E09, E10, E11, K01 |
| middels | 4 | E08, E12, E13, E15 |
| lav | 0 | |

## Klassen jeg setter

| Kort | Prefiks | Før PR 5 | Klasse | Feilårsak | Merknad |
|---|---|---|---|---|---|
| E01–E07 | SF | blandede | riktig | | Forskrifter; § 61 dekker dem, og de fleste har fått tilføyd hjemmel i viltressursloven (forskrift 2026 nr. 1380) |
| E08 | DEL | blandede | **feil** | dekningen | Kap. III er en delegering etter viltloven § 7 annet ledd, ikke en forskrift |
| E09 | DEL | hjemmel opphevet | **feil** | dekningen | Delegering etter viltloven § 39 (jegerprøven) |
| E10 | DEL | hjemmel opphevet | **feil** | dekningen | Videredelegering etter § 39 |
| E11 | DEL | hjemmel opphevet | **feil** | dekningen | Delegering etter § 7 annet ledd |
| E12 | LTI | hjemmel opphevet | **feil** (#446) | annet | Endringsforskrift til forskrift 1984 nr. 1879, som er opphevet ved 2026 nr. 1158 kap. II |
| E13 | LTI | hjemmel opphevet | **feil** (#446) | annet | Endringsforskrift til forskrift 1984 nr. 1878, som er opphevet på samme måte |
| E14 | SF | hjemmel opphevet | riktig | | Forskrift; virkningen er langt på vei uttømt (fristen var 2007) |
| E15 | DEL | uavklart | **feil** | dekningen | Viltlovsdelen av en blandet delegering |
| K01 | LTI | | **feil** | populasjonen | Vedtaket ble opphevet i 2021 (R8-hull), og videreføringen i kirkeordningen § 44 er oversett |
| K02–K04 | DEL | | riktig | | Entydig kortnavn, ingen videreføring |
| K05, K07, K08 | DEL | | riktig | | Kortnavnet brukes også om den nye loven, og ingen videreføring finnes |
| K06 | SF | | riktig | | Svakt funn: uavklart fordi datoen står uten punktum (som M17) |

**Riktig 15, feil 8, usikker 0.**

| | Riktig / (riktig + feil) | Wilson 95 % |
|---|---|---|
| Endret (E01–E15) | 8/15 | 0,53 (0,30–0,75) |
| Endret, SF | 8/8 | 1 (0,68–1) |
| Endret, DEL | 0/5 | 0 (0–0,43) |
| Endret, LTI (#446) | 0/2 | |
| Endret uten #446 (E12, E13) | 8/13 | 0,62 |
| Kontroll (K01–K08) | 7/8 | 0,88 (0,53–0,98) |
| Totalt | 15/23 | 0,65 (0,45–0,81) |

Utvalget er stratifisert, så totalen er ikke et populasjonsanslag. Mønsteret er likevel tydelig. I kopien har alle 8 DEL-vedtak med V4-videreføring
fra viltressursloven dekningen `dokumenttype_ikke_nevnt`. Alle 5 LTI-vedtakene er rene endringsvedtak: 3 endrer forskriftene om felles viltområde
og minsteareal, som er opphevet, og 2 er navneendringer. De 13 SF-forskriftene har dekningen `dekket`. Av de 22 endrede funnene (uten M13) er
trolig om lag 9 riktige (SF) og inntil 13 feil eller #446 (DEL og LTI). Det forutsetter at alle 13 DEL- og LTI-vedtakene er blant
de endrede funnene [slutning, ikke kontrollert enkeltvis utenfor utvalget].

## Det viktigste

1. **En videreføring som ikke nevner vedtakstypen, bærer alene når ingenting faller** (E08–E11, E15). Viltressursloven § 61 viderefører
   «Forskrifter gitt i medhold av viltloven». Regelen setter selv dekningen `dokumenttype_ikke_nevnt` på delegeringsvedtakene. Men
   `HjemmelsstatusSamlet` gir videreført straks ingen premiss faller (`faller.Count == 0`). H14-regelen (`BaererVurdert`) slår bare inn når noe
   faller. Resultatet er asymmetrisk:
   - En DEL med én opphevet hjemmel og en videreføring som ikke dekker vedtakstypen, gir uavklart.
   - En DEL der alle hjemlene bare har en slik videreføring, gir videreført.

   Materielt er svaret feil. Myndigheten etter viltloven § 7 og § 39 finnes ikke etter 1. juli 2026. Regjeringen og departementet har gitt nye
   delegeringer etter viltressursloven (2026 nr. 1153, 1378 og 1550), noe som viser at de gamle ikke er ment å leve videre [slutning].
   Overgangsforskriften 2026 nr. 1158 kap. I viderefører «forvaltningsvedtak … med varighet», som etter sammenhengen gjelder enkeltvedtak og
   tillatelser, ikke delegeringer [slutning].
   - **Forslag (ikke bygget):** en premiss som bare er videreført med dekningen `dokumenttype_ikke_nevnt`, skal regnes som ikke vurdert også når
     ingenting faller. Står bare slike igjen, er samlet status uavklart (eller hjemmel opphevet, se under), ikke videreført. Merk at dette endrer
     AK2 fra fase 1 (FOR-1993-12-17-1314), og det må sjekkes om den saken har samme dekning.
   - Etter mitt syn bør E09–E11 få hjemmel opphevet. Uavklart er det minste regelen bør gi uten fortolkning.
2. **En generell videreføring går ikke foran en særskilt oppheving** (E12, E13). Endringsforskriftene endrer forskrift 1984 nr. 1878 og 1879, og
   de er opphevet ved 2026 nr. 1158 kap. II fra 1. juli 2026. Regelen ser § 61 og stopper der. Utfallet (ingen funn) er ufarlig, og når #446
   klassifiserer dem, faller de ut av riktig grunn. Statusen «videreført» er likevel feil. Etter måling 2 (M08, M09) burde funnet ha stått med
   #446.
3. **K01: vedtaket er opphevet, og videreføringen med kortnavnet er oversett.**
   - LTI/forskrift/2021-11-14-3414: «Delegering 16. april 2018 nr. 852 … oppheves.» Setningen begynner med «Delegering <dato> nr. … av»,
     altså samme R8-hull som i docs/33 §5.21. Funnet skulle vært forklart bort.
   - Uavhengig av det: Kirkeordning for Den norske kirke (SF/forskrift/2019-03-30-2307) § 44 viderefører «Forskrifter og andre bestemmelser gitt
     av Kirkemøtet … i medhold av kirkeloven». Regelen finner den ikke av to grunner:
     - V2a søker bare i lover og i forskrifter med «overgang» i tittelen;
     - V2a godtar ikke kortnavnet, bare dato og nummer.
   - Dette er det eneste tilfellet i utvalget der en videreføring med kortnavnet er oversett. Den står i en forskrift, ikke i den opphevende
     loven.
4. **SF-forskriftene er riktige** (E01–E07, E14). § 61 dekker dem etter ordlyden, og kortnavnet «viltloven» er entydig. Forskrift 2026 nr. 1380 har
   gitt de fleste ny hjemmel i viltressursloven, så de bærer også på den nye loven.
5. **Kontrollkortene står** (K02–K08). Ingen av de opphevende lovene (skatteforvaltningsloven, lov 2019 nr. 83, sentralbankloven 2019,
   revisorloven 2020, statistikkloven 2019) viderefører forskrifter eller vedtak. Et søk i hele korpuset på kortnavnene sammen med et
   videreføringsord gir ingen videreføring. For K05, K07 og K08 er det riktig at regelen ikke gir svar på kortnavnet, og ingenting går tapt.

## Tverrgående mønstre

- **Dekningen brukes bare halvveis.** Regelen vet at «forskrifter» ikke dekker et delegeringsvedtak (`dokumenttype_ikke_nevnt`), men samlet status
  bruker det bare når noe annet faller. PR 5 gjorde problemet synlig fordi viltressursloven er den første store V4-videreføringen som treffer
  mange DEL-vedtak samtidig.
- **eId og ledd.** Korpusversjonen av § 61 mangler opphevingsleddet (det er lest i Lovtidend, EN_LTI). Videreføringen er derfor `§61/ledd-2` i
  korpus, men tredje ledd på Lovdata. Sitatet er riktig. Den som siterer «§ 61 andre ledd» etter korpus, peker på feil ledd. Dette gir lav
  alvorlighet og ingen klassefeil, men det bør stå i påstanden eller premissmangelen.
- **Premissmangelen om ikraftsetting** kan nå lukkes for viltressursloven: Lovdata sier at hele loven trådte i kraft 1. juli 2026 etter
  res. 20. juni 2025 nr. 1105, og at viltloven er opphevet fra samme dag.
- **Kompetanseledd under opphevet lov.** DEL 1982 nr. 592 står som «aktiv» kompetanseledd i E02, E05 og E06, men vedtaket er selv en delegering
  etter viltloven med samme svakhet som E09–E11. Det påvirker ikke klassene, fordi kompetanseledd ikke teller når det finnes materielle
  hjemler.

## Prinsipielle innvendinger

Ingen mot selve modellregelen. Punkt 1 er en feil i hvordan H14-regelen er avgrenset, ikke i modellen. Prosjekteieren valgte avgrensningen
(«Når ingenting faller, endrer den ingenting»), så rettingen må avgjøres av prosjekteieren.

## Ikke kontrollert

- De andre 7 endrede funnene utenfor utvalget, enkeltvis. Tallene over bygger på et tellesøk etter prefiks og dekning.
- Om FOR-1993-12-17-1314 (AK2) har dekningen `dokumenttype_ikke_nevnt`.
- Forarbeidene til viltressursloven (Prop. L) om delegeringer og overgang, og rettspraksis.
- Om Lovdata senere merker de gamle viltlovsdelegeringene som opphevet.
