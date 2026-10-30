# Juristrunde #375 (tredje lasting), del a: sammendrag av runde 1

Kortsettet har 35 kort. m1–m3 kommer fra mønsterlaget, og k1–k32 er KI-lagets K-forslag fra pilotlastingen 2026-10-30, etter #419 og #420. Alle detaljene står i `utfordringer.json`. To avgjørelser er lagt til grunn og ikke prøvd på nytt: #418 (a) og (b), og at merknader ikke er hjemmel (#419). Hvert kort er sjekket mot de andre forslagene i samme ledd. Nodeteksten og de eksisterende kantene er lest i den delte basen (bare SELECT, 2026-10-30). Overgangsforskriften og registerforskriften for kontantstøtte er i tillegg kontrollert på Lovdata.

## Tall

| Dom | Antall |
|---|---:|
| holder | 3 |
| innvending | 32 |
| kontroller | 0 |
| prinsipiell | 0 |

| Alvor | Antall | Kort |
|---|---:|---|
| høy | 3 | k23, k26, k31 |
| middels | 19 | m2, m3, k2, k4, k5, k9, k10, k11, k12, k13, k14, k15, k16, k17, k24, k25, k29, k30, k32 |
| lav | 10 | m1, k1, k3, k6, k7, k18, k19, k20, k21, k27 |

Kortene som holder, er k8, k22 og k28. Alle innvendingene gjelder raden. Tre kort foreslår en ny rad:
- m3: P informasjon, statsforvalteren sender stengingsvedtaket til kommunen (§ 55 andre ledd).
- k23: P betaling, kommunen gir grunntilskudd til private barnehager (§ 19 a første ledd).
- k24: K vedtak, vilkår for ekstra grunntilskudd (§ 19 a andre ledd tredje punktum).

## Dubletter: 15 av 35 kort

Feltet `andre_forslag_i_samme_ledd` viser at 15 av de nye forslagene er samme disposisjon som en kant som alt står i køen:
- 7 har en annen type: k1, k4, k13, k15, k16, k18, k19.
- 3 har til = null: k2, k25, k32.
- k20 har også en annen type, og objektet sier ikke noe.
- k21 har hjemmel i et annet ledd.
- k26 og k30 har feil type eller kategori.
- k7 er identisk med det runde 418 ba om.

#420 gjør at en annen type eller en annen til gir en ny nøkkel, og da blir forslaget ikke paret. Dette er restrisikoen docs/33 §5.3 beskriver: en synlig dublett, ikke et stille tap.

For hver dublett gjelder dette:
- Godkjenn bare én av de to kantene.
- Der det nye forslaget har riktig type eller motpart, viser `retting` hva det trenger for å stå. Da kan den gamle avvises i stedet for å rettes etter runde 418. Det gjelder k1, k2, k7, k13, k15, k16, k18, k19, k20, k25 og k32.
- Der det nye forslaget er dårligere eller feil, er anbefalingen å avvise det: k4, k21, k26, k30.

Disse er egne utsagn og ikke dubletter, selv om de står i samme ledd som en eksisterende kant:
- m1, m2 og m3 står i andre punktum, mens den eksisterende kanten står i første. m2 og m3 er stengingsutsagnene #420 skulle hente tilbake.
- k3, k11, k12 og k27 står i en annen setning enn den eksisterende kanten.
- k14 og k17 er statsforvalterens rader, som 418 k38 og k40 ba om som nye rader.

## De viktigste innvendingene

1. **Oppgave eller avtaleadgang gjort til kompetanse (høy).**
   - k26: «Kommunen skal samordne tjenestetilbudet» er F samordning, ikke K samordning (#368). Leddet gir ingen myndighet over tjenesteytere, og de omfatter også statlige og private. F-raden finnes alt (5fe9bcda).
   - k31: «kan det etableres … der begge parter ønsker det» er passivt og bygger på avtale. Det gir kommunen ingen kompetanse (17.4: grammatisk subjekt som rettssubjekt; jf. 418 k104).
   - k23: «Grunnlaget for beregningen er det siste tilgjengelige kommuneregnskapet» er en beregningsregel. Raden gjør den til vedtakskompetanse.
   - Alle tre bør avvises.
2. **Overgangsforskriften fra 2021 mangler tidsavgrensning (k9–k17, middels).**
   - Alle ni kortene gjelder FOR-2021-10-06-3079. Den gjelder bare tilsyn etter kapittel V som ikke var avsluttet før 1. januar 2022, der varsel om vedtak var sendt før den datoen.
   - Uten den avgrensningen står kommunen og statsforvalteren med en gjeldende kompetanse til å stenge, sanksjonere og kreve innsyn i økonomisaker. Det strider mot unntaket i barnehageloven § 53 første ledd. Samme innvending kom i 418 k36–k40.
   - Innsyn og adgang (k9, k10) er tilsyn, ikke vedtak.
3. **Vilkår i sitatet er ikke ført i avgrensningen (m1, m2, m3, k24, k29).**
   - Stenging krever at et pålegg ikke er etterkommet (m2, m3). Statsforvalterens stenging hører også til tilsynet «i særlige tilfeller» (§ 55 første ledd).
   - Satsforskriften for ekstra grunntilskudd forutsetter at utgifter er holdt utenfor (m1).
   - Registeret i § 48 a er bundet til statistikk og forskning (k29).
   - k24 gjør grensen i § 19 a femte ledd til en egen kompetanse, i stedet for å føre den som avgrensning på kantene den begrenser.
4. **K klage uten motpart (k2, k25, k32, middels).** KI-laget setter sakstypen i objekt, men til = null. Mønsterlaget gjør det motsatte: til = kommune og objekt = null. Hver kant mangler halvparten. Slått sammen gir de én riktig kant.
5. **Kontroll av egne registerdata er ikke tilsyn (k5, middels).** «Kommunen fører kontroll med opplysninger fra barnehageeierne» gir ingen adgang til å følge opp overfor eierne. Det er F kontroll.

## Tverrgående mønstre

- **KI-laget gjentar typefeil som er rettet i runde 418, men nå i motsatt retning.** I 418 var reaksjonene `vedtak` og satsene `beslutning`. Nå foreslår KI-laget `sanksjon` og `vedtak`, altså de riktige typene, som nye kanter ved siden av de gamle. Rettingene fra 418 er ikke gjennomført i basen (målt 2026-10-30), så begge står i køen.
- **Klasse som fra og distributiv lesning fungerer** (#418 (a) og (b)). Ingen kort har feil innehaver bare fordi klassen er brukt. Feil subjekt finnes bare i k31 (passiv setning) og k21 (fra hentet fra et annet ledd).
- **K har ingen modalitet.** «skal gi forskrift» (m1) og «skal bestemme» (k27) mister «skal». Dette er formatmangelen fra f47/f49 i runde 375 og gjentas ikke per kort.
- **Svakeste premiss (#411).** Fra er løst med standardregelen for «kommunen» og «statsforvalteren», og mønsterkortene har `normalisering`. Det er registrert i #411.
- **Gyldighet.** Tildelingsforskriften oppheves 31. desember 2026 (k18–k21), og finansieringsforskriften gjelder fra tilskuddsåret 2027 (m1, k3). Forslaget har ikke et felt for gyldighet.
- **Uenighet med tidligere runder.** k29: runde 375 f67 kalte registeret i § 48 a første ledd opprettingskompetanse, i en merknad utenfor raden. Jeg mener at beslutning er riktigere, fordi et register ikke er et organ. Formålsavgrensningen gjelder uansett typevalget. k27: runde 375 f34 sa at andre punktum ikke gir K overfor andre. Det er jeg enig i, og K beslutning med til = null holder.

## Prinsipielle innvendinger

Ingen. Feilene ligger i KI-lagets typevalg (oppgave mot kompetanse, vedtak mot tilsyn og oppretting), i manglende avgrensning og i at dubletter med ny type eller ny motpart ikke pares. Den siste er en kjent restrisiko etter #420.
