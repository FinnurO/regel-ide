# Juristutfordring, #472 måling 2, runde 1 (M01–M20)

Grunnlag: `inndata.json` (20 vurderingskort), kopien `regelide_472pr4` (bare SELECT med `default_transaction_read_only=on`) og Lovdata på nett.
- Alle 20 forskrifter er slått opp på lovdata.no/forskrift/<id>, og basen er notert (DEL, DELO, LTI eller SF).
- For alle kortene er det søkt i korpus etter forskriftens dato og nummer i andre kilder.
- Overgangsreglene i de opphevende lovene er lest i korpus. Der opphevingen bare står i den kunngjorte teksten, er de lest i Lovtidend: skipsarbeidsloven og næringsberedskapsloven.

## Antall per dom og alvor

| Dom | Antall | Kort |
|---|---|---|
| holder | 18 | M01–M03, M05, M07–M20 |
| innvending | 2 | M04, M06 |
| kontroller | 0 | |
| prinsipiell | 0 | |

| Alvor (bare innvending) | Antall | Kort |
|---|---|---|
| hoy | 0 | |
| middels | 2 | M04, M06 |
| lav | 0 | |

## Klassen jeg setter på hvert funn

| Kort | Undertype | Prefiks | Modellereren | Jeg | Merknad |
|---|---|---|---|---|---|
| M01 | hjemmel_opphevet | DEL | riktig | riktig | opphevet_dato skal være 1.1.2020, men premissmangelen står |
| M02 | hjemmel_opphevet | DEL | riktig | riktig | |
| M03 | hjemmel_opphevet | DEL | riktig | riktig | Som M01 |
| M04 | hjemmel_opphevet | DEL | riktig | **feil** | Arveavgiftsloven gjelder fortsatt for gaver og dødsfall før 2014 (kap. I første ledd andre punktum) |
| M05 | hjemmel_opphevet | DEL | riktig | riktig | |
| M06 | hjemmel_opphevet | DEL | riktig | **feil** | Som M04 |
| M07 | hjemmel_opphevet | DEL | riktig | riktig | |
| M08 | hjemmel_opphevet | LTI | riktig (#446) | riktig (#446) | Rent endringsvedtak til DEL/1988-4321 |
| M09 | hjemmel_opphevet | LTI | riktig (#446) | riktig (#446) | Rent opphevingsvedtak |
| M10 | hjemmel_opphevet | LTI | feil | feil | DELO «Opphevet» ved 2018-2052. R8-hull (populasjonen) |
| M11 | hjemmel_opphevet | SF | riktig | riktig | § 21 sto i kap. IV og falt med hele loven i 2017, så koblingen er riktig |
| M12 | hjemmel_opphevet | SF | riktig | riktig | 2013-990 erstatter i innhold, men opphever ikke |
| M13 | blandede_hjemler | SF | feil | feil | Viltressursloven § 61 andre ledd viderefører, og feilen er tverrgående (23 funn) |
| M14 | blandede_hjemler | SF | riktig | riktig | Svakt funn, som H11 |
| M15 | blandede_hjemler | SF | riktig | riktig | Svakt funn, som H11 |
| M16 | blandede_hjemler | LTI | feil | feil | DELO «Opphevet» ved 2008-1538. R8-hull (populasjonen) |
| M17 | uavklart | SF | riktig | riktig | Egentlig blandede: rolle NULL fordi «21 juni 2019» mangler punktum |
| M18 | uavklart | DEL | riktig | riktig | |
| M19 | videreforing_utlopt | SF | riktig | riktig | Rolle NULL fordi hjemmelsfeltet er på nynorsk («heimel») |
| M20 | videreforing_utlopt | SF | riktig | riktig | |

Modellereren har riktig 17 og feil 3. Jeg har **riktig 15, feil 5 og usikker 0**.

| | Riktig / (riktig + feil) | Wilson 95 % |
|---|---|---|
| Totalt | 15/20 | 0,75 (0,53–0,89) |
| Uten #446 (M08, M09) | 13/18 | 0,72 (0,49–0,88) |
| hjemmel_opphevet | 9/12 | 0,75 (0,47–0,91) |
| blandede_hjemler | 2/4 | 0,50 (0,15–0,85) |
| uavklart | 2/2 | |
| videreforing_utlopt | 2/2 | |
| DEL (M01–M07, M18) | 6/8 | 0,75 (0,41–0,93) |
| LTI (M08–M10, M16) | 2/4 | 0,50 (0,15–0,85) |
| SF | 7/8 | 0,88 (0,53–0,98) |

Utvalget er stratifisert og ikke proporsjonalt. Jeg har vektet grovt etter lagene i `utvalg.json` og holdt de 12 funnene i lag som ikke er trukket utenfor. Da blir anslaget om lag 0,73. Det er trolig for høyt for blandede/SF, se punkt 2.

- **#446:** M08 og M09 er rene endrings- og opphevingsvedtak, som modellereren sier. Ingen andre i utvalget er det. M17 har langt på vei uttømt virkningen (sedlene er ikke tvungent betalingsmiddel fra 15.11.2020) [slutning], men den er ikke et rent endringsvedtak.
- **Sitat og eId:** riktige i alle 20 kort.

## Det viktigste

1. **Opphevingen kan selv begrense sin virkning i tid (M04, M06).** Lov 13. desember 2013 nr. 110 kap. I sier at «Opphevelsen gis virkning for gaver som ytes 1. januar 2014 eller senere og arv etter dødsfall som skjer 1. januar 2014 eller senere».
   - For eldre saker gjelder arveavgiftsloven fortsatt. Unntaket er arv der avgiften ikke var forfalt innen 1.1.2021 (tredje ledd), som opphevingen også gjelder for.
   - Lovdata merker loven som opphevet bare for gaver og dødsfall fra 2014.
   - Retting kan skje innen ti år etter underretningen (§ 34). Delegeringene etter § 24 og § 35 har derfor fortsatt et virkeområde [slutning].
   - Regelen leser bare første punktum som en hel oppheving. Kortet leser tredje ledd som en frist for de gamle sakene, men det er en utvidelse av opphevingen.
   - Alle 6 funnene med LOV-2013-12-13-110 som opphevende bestemmelse har samme feil.
   - **Forslag (ikke bygget):** en oppheving med «gis virkning for …» eller «gjelder ikke for …» i samme ledd gir en egen form, «opphevet for nye tilfeller». Den gir ikke undertypen hjemmel_opphevet.
2. **Viltressursloven § 61 andre ledd er ikke lest (M13), og feilen er tverrgående.**
   - 23 av de 264 funnene har viltressursloven § 61 som opphevende bestemmelse: blandede 12, hjemmel opphevet 10 og uavklart 1.
   - 11 av de 12 blandede er SF-forskrifter, og bestemmelsen viderefører dem direkte («Forskrifter gitt i medhold av viltloven gjelder også …»). De er nesten sikkert feil.
   - For DEL- og LTI-vedtakene bærer ikke dekningen «forskrifter» alene. Overgangsforskriften SF/forskrift/2026-06-22-1158 kap. I første ledd viderefører likevel «Forvaltningsvedtak gitt med direkte hjemmel i viltloven» med varighet etter ikrafttredelsen. Om det dekker delegeringsvedtak, er ikke avgjort.
   - Funnet er ett av 20 i utvalget, men årsaken gjelder nesten 9 % av populasjonen. Det er den største enkeltårsaken som er igjen.
3. **R8-hullene står (M10, M16).** Begge er opphevet ved en kilde i korpus, og Lovdata har dem i DELO som «Opphevet». Det er to av de åtte hele opphevingene som docs/33 §5.21 allerede nevner (en tittel som begynner med «av …», og «oppheves 1. januar 2019»). Rettingen er stilt som spørsmål og ikke bygget.
4. **Rettingene fra måling 1 virker i utvalget.**
   - Ingen av de fire LTI-funnene er opphevet uten at korpus viser det. M08 og M09 er rene endrings- og opphevingsvedtak, og M10 og M16 er R8-hull.
   - Premissmangelen for kunngjort versjon står på alle fire.
   - Rollen for en forskrift som hjemmel er riktig i M19: kompetanseledd.
   - Gjenstandsløs og END forekommer ikke i utvalget.
5. **Tekstlesingen av hjemmelsfeltet gir rolle NULL, men ingen klassefeil i utvalget.**
   - I M17 mangler punktum i datoen («Lov 21 juni 2019 nr. 31»). Det gir uavklart i stedet for blandede.
   - I M19 er hjemmelsfeltet på nynorsk («med heimel i»).
   - I M11 mangler lovnummeret.
   - Bare M17 endrer undertypen, og bare til en svakere form.

## Tverrgående mønstre

- **Overgangsregler som ikke handler om forskrifter.** Regelen leter etter videreføring av forskrifter (V1–V4). Den leser ikke at selve opphevingen er avgrenset i tid eller etter saker. Mønsteret i M04 og M06 er en ny variant av feilårsaken videreføringsformen.
- **Kontekstlista i inndataene er kuttet etter 8 noder.** For viltressursloven falt § 61 andre ledd utenfor. For lover med mange treff på «forskrift» og et gjelde-ord er lista derfor ikke et fullstendig hjelpemiddel. Jeg har lest sluttbestemmelsene direkte i alle kortene.
- **opphevet_dato er dokumentdatoen.** For sentralbankloven 2019 (M01, M03, M17) er datoen 1.7.2019, men opphevingen trådte i kraft 1.1.2020. Premissmangelen står, så funnet er ikke feil, men datoen i raden er det.
- **Svake «blandede»** (M14, M15): forskriften har fått en ny lov som hjemmel, og den gamle står igjen i Lovdatas hjemmelsliste. Påstanden er riktig, men funnet sier lite. Det er det samme som H11 i måling 1.

## Prinsipielle innvendinger

Ingen mot modellregelen. M04 og M06 viser at kriteriet «videreføringen finnes ikke / er utløpt» bør omfatte opphevinger som selv er avgrenset i tid. Det må avgjøres om de skal telle som videreføring. Jeg har satt dem til feil, fordi den gamle loven etter ordlyden fortsatt gjelder for de gamle sakene.

## Ikke kontrollert

- Hvor mange arveavgiftssaker etter den gamle loven som faktisk er åpne i 2026 (M04, M06).
- De andre 22 viltressurslov-funnene enkeltvis. Tallene i punkt 2 er et tellesøk.
- Om «forvaltningsvedtak» i SF/forskrift/2026-06-22-1158 kap. I dekker delegeringsvedtak.
- Overgangsforskrifter utenfor korpus for sentralbankloven 2019, arbeidstvistloven 2012 og revisorloven 2020. Lovdatas fotnoter til sentralbankloven nevner ingen.
- Forarbeider og rettspraksis.
