# Juristrunde #375 (pilot barnehageloven): sammendrag av runde 1

92 kort: 71 forslag (f1–f71), 3 modellvalg (p1–p3) og 18 kontroller av BU-kartleggingen (b-kort). Detaljene står i `utfordringer.json`.

## Tall

| Dom | Antall |
|---|---:|
| holder | 38 |
| innvending | 53 |
| kontroller | 0 |
| prinsipiell | 1 (p2) |

| Alvor (innvendinger) | Antall | Kort |
|---|---:|---|
| høy | 11 | f1, f2, f11, f12, f14, f15, f29, f30, f31, f32, f66 |
| middels | 18 | f3, f7, f10, f16, f17, f18, f19, f21–f28, f71, p1, b6_6 |
| lav | 24 | f6, f9, f13, f20, f34, f36–f39, f41, f46–f49, f55, f56, f59, f64, f67, f69, f70, b2_7, b3_1, b6_2 |

Fire av innvendingene gjelder noe utenfor raden (f34, f38, f55, f67) og har forslag til ny rad. Per gruppe:

| Gruppe | holder | innvending | prinsipiell |
|---|---:|---:|---:|
| f-kort | 23 | 48 | 0 |
| p-kort | 1 | 1 | 1 |
| b-kort | 14 | 4 | 0 |

## De viktigste innvendingene

1. **Merknader og veiledning er lastet som hjemmel (f11, f12, f29–f32, høy).** Nodene `kap-merknader-…` og `kap-veiledning-…` er departementets kommentarer, ikke forskriftstekst. De har ingen ELI-eId. I tildelings- og finansieringsforskriften er 210 av 389 gjeldende noder slike merknader. Forslagene gjør et budsjettanslag (kommunal deflator) til vedtak og forskrift, og satser som står i selve forskriften, til departementets enkeltvedtak. Merknads- og veiledningsnoder bør holdes utenfor konverteringen, eller merkes som ikke-normative, før neste kjøring.
2. **K kan ikke ha klassen kommune eller statsforvalter som fra (p2, prinsipiell).** Kjernen i loven falt bort ved lastingen: godkjenning, dispensasjon, lokal forskrift, tilsyn, pålegg, stenging, klage og tilskuddsvedtak (103 utsagn). Forslagene som står igjen, er skjeve mot «departementet kan gi forskrift» og F- og P-rader. Mitt syn: tillat klasse som fra i K med distributiv lesning, som i P og F, og registrer i tillegg rollen «lokal barnehagemyndighet» der loven selv navngir den (§ 10 første ledd, § 11).
3. **Tidsavgrensninger og uttømte regler mangler (f1, f2, f16, f18–f20, f69, høy til lav).** Delegeringen fra 2017 gjaldt 21. juni–31. august 2017 og viser til en eldre versjon av § 14. Den står som gjeldende R- og K-kant. Det samme gjelder overgangsregler (2006, 2022) og ikrafttredelse. Tildelingsforskriften fra 2015 oppheves 31. desember 2026 (finansieringsforskriften § 25). 17.4: mister vilkår, bruker feil lovversjon.
4. **Saksbehandling og beregningsregler er gjort til struktur (f10, f14, f15, f22–f28, f71).** KI-laget gjør «Kommunen skal + verb» til F forvaltning eller P informasjon: begrunne vedtak, opplyse saken, beregne, indeksregulere, gjøre fratrekk. Det hører til regellaget. I f14 og f15 er retningen dessuten snudd: kommunen innhenter fra søkeren, men raden sier at kommunen skylder informasjon (høy).
5. **Negasjon med komplement er lest feil (f66, høy).** «kan ikke utleveres til andre enn Statistisk sentralbyrå» har blitt negativ vedtakskompetanse med SSB som fra-node. SSB er den eneste tillatte mottakeren, og leddet har ingen vedtakskompetanse (jf. #309 lærdom 4).

## Tverrgående mønstre

- **Objektet blir kuttet ved om lag 200 tegn i mønsterlaget (f41, f56, f59, f64).** Det som forsvinner, er ofte en avgrensning: «fra Ukraina», «på nattid», fylkeskommunens ansvar.
- **Lovens fravikshjemler og «skal gi forskrift» forsvinner (f46–f49).** «Forskrift … kan gjøre unntak fra reglene i første og andre ledd» står ikke i avgrensningen. K har ingen modalitet, så pålagt normgivning kan ikke uttrykkes. Det er en formatmangel.
- **Unntak står i objekt i stedet for avgrensning (f36, f37, f39).** Unntaket for kapittel V er der, men kan ikke brukes som avgrensning (jf. b1_1, b7_1).
- **Idempotensnøkkelen i lasteren mangler objekt (f67).** Docs/33 §5.3 har ikke objekt i nøkkelen, mens tabellnøkkelen i §4.3 har det. To forskriftshjemler fra samme aktør i samme ledd kan dermed bli én kant. Det bør kontrolleres.
- **Kunnskapsstatus (p3).** 31 normgivningsforslag og 4 F-forslag har normalisering, selv om aktøren bare er løst med standardregelen. Etter regelen om svakeste premiss bør status stå som ikke vurdert (NULL) til ressortkanten er vurdert, og deretter avledet med den som premiss. Tekstlig antesedent i samme ledd bør gå foran standardregelen (f1). Klassen statsforvalter har et premiss som ikke er vurdert (#402).
- **Grammatisk subjekt eller rettssubjekt (b6_2, b6_6, p1).** «Barnehagen» er ikke et rettssubjekt; den som er bundet, er eieren (§ 7). Modellererens kontroll kaller BUs «barnehageeiere» feil aktør (b6_6). Det er selv 17.4-feilen «antar at grammatisk subjekt er rettssubjekt».
- **Avvist fordi motparten var privat (f71).** KI-laget satte til = «Private barnehager», raden falt bort som «aktør ikke i katalogen», og P betaling forsvant. For begunstigede private skal til være null (#353).
- **Mulig motstrid som bør kontrolleres (f17, f5).** FOR-2008-09-12-1071 § 11 tredje ledd har departementet som klageinstans. FOR-2025-07-02-1462 § 6 andre ledd legger de samme klagene til klagenemnda.

## BU-kortene

Modellererens lesning holder på 14 av 18 kort. Innvendinger: b2_7 (feilen er det speilvendte av 17.4 første punkt, plikt gjort til adgang, og den speilvendte feilen står ikke på lista), b3_1 (§ 31 femte ledd, fylkeskommunens ansvar, mangler i kontrollen), b6_2 og b6_6 (grammatisk subjekt lest som rettssubjekt; i b6_6 er BUs lesning forsvarlig).

## Prinsipielle innvendinger

Én: p2. Noderegelen for K, «aktør eller rolle», gir systematisk tap av lovens kompetanser når den generiske omtalen løses til en klasse. P og F tar allerede klasse. Regelen bør endres (se punkt 2 over). Avgjørelsen er Johanns.
