# Juristutfordring, #472 måling 1, runde 1 (H01–H20)

Grunnlag: inndata.json (20 vurderingskort), korpuset (bare SELECT) og Lovdata/Lovtidend på nett. Alle kort er kontrollert mot korpus. For 17 av 20 forskrifter er statusen i tillegg slått opp på lovdata.no/forskrift/<id>. For H11, H12 og H17 er Lovdata-status bare delvis eller ikke kontrollert, se `kilder`.

## Antall per dom og alvor

| Dom | Antall | Kort |
|---|---|---|
| holder | 10 | H01, H02, H03, H04, H06, H10, H11, H12, H17, H18 |
| innvending | 10 | se nedenfor |
| kontroller | 0 | |
| prinsipiell | 0 | |

| Alvor (bare innvending) | Antall | Kort |
|---|---|---|
| hoy | 6 | H07, H08, H09, H13, H14, H15 |
| middels | 1 | H16 |
| lav | 3 | H05, H19, H20 |

## Klassen jeg setter på hvert funn

| Kort | Undertype | Prefiks | Modellereren | Jeg | Merknad |
|---|---|---|---|---|---|
| H01 | hjemmel_opphevet | DEL | riktig | riktig | |
| H02 | hjemmel_opphevet | DEL | riktig | riktig | |
| H03 | hjemmel_opphevet | DEL | riktig | riktig | |
| H04 | hjemmel_opphevet | DEL | riktig | riktig | |
| H05 | hjemmel_opphevet | DEL | riktig | riktig | Opphevingen trådte i kraft 1.1.2023, ikke 22.6.2022. Premissmangelen om ikraftsetting mangler |
| H06 | hjemmel_opphevet | DEL | riktig | riktig | |
| H07 | hjemmel_opphevet | LTI | riktig | **feil** | Opphevet i Lovdata (DELO). § 10 falt i 2012, ikke ved forsvarsloven § 87 |
| H08 | hjemmel_opphevet | LTI | riktig | **feil** | Opphevet ved DEL/forskrift/2006-06-30-761 pkt. 2 |
| H09 | hjemmel_opphevet | LTI | riktig | **feil** | Som H07 |
| H10 | hjemmel_opphevet | SF | riktig | riktig | |
| H11 | blandede_hjemler | SF | riktig | riktig | |
| H12 | blandede_hjemler | SF | riktig | riktig | |
| H13 | blandede_hjemler | LTI | riktig | **feil** | Opphevet ved LTI/forskrift/2013-07-18-921 og erstattet med tvl. § 22-3 |
| H14 | blandede_hjemler | DEL | riktig | **usikker** | Videreføringen av «forskrifter» dekker neppe et delegeringsvedtak |
| H15 | uavklart | SF | feil | **usikker** | Det er ikke vist at FOR-1975-12-29-1 ikke er hjemmel. Lovdata oppgir den som hjemmel |
| H16 | uavklart | LTI | feil | feil | Riktig klasse, men hovedgrunnen er at vedtaket selv er opphevet (2022-2510) |
| H17 | videreforing_utlopt | SF | riktig | riktig | |
| H18 | videreforing_utlopt | DEL | feil | feil | |
| H19 | gjenstandslos | DEL | riktig | riktig | Én påstand i grunnen om DEL/forskrift/2022-1778 er udokumentert |
| H20 | gjenstandslos | DEL | feil | feil | Feilårsaken er aggregeringen (Any), ikke rollen |

Sum: modellereren har riktig 16 og feil 4. Jeg har **riktig 11, feil 7 og usikker 2**.
Per prefiks hos meg: DEL 7 riktig, 2 feil og 1 usikker av 10. SF 4 riktig og 1 usikker av 5. **LTI 0 riktig og 5 feil av 5.**
Utvalget er stratifisert og ikke proporsjonalt, så 11/20 er ikke et populasjonsestimat. Vektet grovt etter prefiks (DEL 140, SF 88, LTI 84) blir presisjonen i underkant av 55 %, med stor usikkerhet.

## Det viktigste

1. **LTI-prefikset er systematisk feil (5 av 5).** Alle fem LTI-funnene i utvalget (H07, H08, H09, H13, H16) gjelder vedtak som Lovdata har i **DELO og merker «Opphevet»**. Korpuset har bare den kunngjorte versjonen (`kildeform = kunngjort`, `status = Gjeldende`). Påstandens første setning, «Lovdata viser … som gjeldende», er da uriktig, og funnet hører ikke hjemme i kategorien. Tre av dem er opphevet ved uttrykkelige vedtak som ligger i korpuset:
   - 2006-761 pkt. 2 opphever H08;
   - 2013-921 ledd-2 opphever H13;
   - 2022-2510 ledd-2 opphever H16.

   Et tellesøk i korpuset viser at minst **28 av de 84 LTI-funnene** er nevnt ved dato og nummer i en opphevingssetning i en annen kilde. Tallet er en nedre grense, fordi H07 og H09 ikke fanges av søket. Til sammenligning gjelder det 3 av 140 DEL-funn (1–2 av dem er falske treff) og 0 av 88 SF-funn. Mitt forslag er at maskinregelen enten holder `kildeform = kunngjort` utenfor eller først sjekker om forskriften selv er målet for en oppheving (målvedtaket/R8 i #429) før den skriver «hjemmel opphevet».
2. **Feil opphevende bestemmelse (H07, H09).** Militærnekterloven § 10 hørte til kap. III «Sivil verneplikt». Kapitlet ble opphevet ved endringslov 20. april 2012 nr. 19 (i kraft 1.6.2012), og den loven er ikke i korpus. Koblingen til forsvarsloven § 87 nr. 3, som i 2017 opphevet resten av loven, er derfor feil. Etter målekriteriet må hjemmelen være «opphevet med den bestemmelsen som er koblet», så det alene gir klassen feil. Konklusjonen om at det ikke finnes noen videreføring, står seg.
3. **Videreføring av «forskrifter» er ikke videreføring av delegert kompetanse (H14).** Finansforetaksloven § 23-2 andre ledd viderefører forskrifter. Et delegeringsvedtak om å gi forskrifter og treffe enkeltvedtak etter en opphevet paragraf holdes neppe i live av den regelen [slutning]. Regelen har riktig merket dekningen `dokumenttype_ikke_nevnt`, men kortet godtar «bærer» uten å drøfte det. Det samme spørsmålet gjelder trolig alle DEL-funn med undertypen blandede, der videreføringen har den dekningen.
4. **«Feil»-kortene holder ikke alle.**
   - H18 holder.
   - H16 og H20 har riktig klasse, men feil eller ufullstendig feilårsak.
   - H15 er for bastant: det er ikke dokumentert at 1975-forskriften bare er gjenstanden. Lovdata oppgir den som hjemmel, og reglene fra 1976 bygger direkte på normer som er opphevet. «Uavklart» er en forsvarlig beskrivelse, og riktig klasse er usikker.
5. **Ikraftsetting til ulik tid uten ordene «til ulik tid» (H05).** Støtteprosessloven § 18 sier bare «fra den tiden Kongen bestemmer», men loven ble satt i kraft 22.6.2022 og 1.1.2023. Premissporten fanger ikke dette, og opphevet_dato (dokumentdatoen) blir feil.

## Tverrgående mønstre

- **Status for forskriften selv kontrolleres ikke.** Regelen ser på hjemlene, men ikke på om forskriften er opphevet ved et senere vedtak. Feilen rammer nesten bare LTI, fordi Lovdata holder opphevede DEL-vedtak i DELO, og korpuset da bare har LTI-kopien.
- **Kortets kontroll av videreføring er grundig, men kortet sjekker ikke forskriften selv.** Modellerens kontroller av overgangsregler (sktfvl. kap. 16, vareførselsforskriften § 14-2-2, stiftelsesforskriften § 5, utlendingsloven § 140) stemmer. Jeg fant ingen oversett videreføring. Feilene ligger et annet sted: kortet søkte ikke etter forskriftens eget dato-nummer i opphevingssetninger, og det slo ikke opp Lovdata-statusen.
- **Feilårsakslista er for smal.** Den mangler en årsak for «forskriften er selv opphevet / feil populasjon». I `retting` har jeg brukt «populasjonen», som ikke finnes i lista, se H07, H08, H09, H13 og H16. Lista mangler også en årsak for aggregeringen til samlet status (H20) og for det å følge en endringslov til hovedloven (`EndringslovTil`, H18). Den siste er ikke R1-rekursjonen i §5.20. Feilårsakene bør defineres skriftlig.
- **#446-merkingen er riktig i alle kort.** Bare H18 er et rent ikrafttredelsesvedtak. H08 har en oppheving i kap. II, men en reell delegering i kap. I.
- **Rolle:** I H01, H07, H09 og H11 står kompetanseleddet (delegeringsvedtaket) som «aktiv», selv om det selv bare er hjemlet i den opphevede loven. Etter R1 burde det arve statusen. Utfallet endres ikke, fordi materielle hjemler går foran, men raden er misvisende på rettskildesiden.

## Prinsipielle innvendinger

Ingen mot modellregelen. Én merknad til målekriteriet: kravet om at hjemmelen er opphevet «med den bestemmelsen som er koblet», gjør H07 og H09 til «feil» selv om konklusjonen om at forskriften mangler hjemmel, står seg. Hvis Johann mener at feil kobling bare skal gi `sitat_eid_riktig = false` og ikke klassen feil, er H07 og H09 likevel feil, fordi vedtakene er opphevet i Lovdata (punkt 1).
