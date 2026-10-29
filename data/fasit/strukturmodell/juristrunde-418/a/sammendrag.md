# Juristrunde #418, del a: sammendrag av runde 1

Kortsettet har 45 kort: 43 nye K-forslag fra mønsterlaget med klassen kommune eller statsforvalter som fra, rollen «lokal barnehagemyndighet» (r1) og rolleinnehavet (r2). Detaljene står i `utfordringer.json`. Avgjørelsene (a) og (b) på #418 er lagt til grunn og er ikke prøvd på nytt.

## Tall

| Dom | Antall |
|---|---:|
| holder | 17 |
| innvending | 28 |
| kontroller | 0 |
| prinsipiell | 0 |

| Alvor | Antall | Kort |
|---|---:|---|
| høy | 5 | k15, k16, k17, k65, k66 |
| middels | 17 | k4, k9, k27, k28, k33, k35, k36, k37, k41, k43, k47, k82, k95, k100, k101, k102, k103 |
| lav | 6 | k7, k25, k56, k97, k99, k106 |

Alle innvendingene gjelder raden. Ingen kort har forslag til ny rad. Kortene som holder: k5, k6, k10, k11, k32, k34, k46, k83, k91–k94, k96, k98, k105, r1 og r2.

## De viktigste innvendingene

1. **Merknader og veiledning er fortsatt brukt som hjemmel (k15, k16, k17, k65, k66, høy).** Det er samme feil som f11/f12/f29–f32 i runde 375, registrert i #419. Alle fem er dubletter av kanter som har riktig hjemmel i forskriftsteksten (k5, k6, k47). De bør avvises, og nodene bør holdes utenfor konverteringen før neste kjøring.
2. **Klagekantene mangler sakstype (11 kort: k4, k9, k27, k35, k41, k43, k47, k82, k95, middels; k106, lav).** Mønstrene `klage-paklages-til` og `klage-er-klageinstans` setter objekt = null. Raden sier da at statsforvalteren er klageinstans for kommunens vedtak generelt. Det er feil rett: uten særskilt hjemmel går klage over kommunale vedtak til kommunestyret eller klagenemnda (fvl. § 28 andre ledd). Hvilke vedtak hjemmelen gjelder, er selve innholdet i kanten. I k9, k35 og k47 mangler også motparten (til = kommune), selv om teksten sier «kommunens vedtak». Mønsteret `klage-paklages-til` fant den i k4, k41, k43 og k95.
3. **«Gi pålegg» er lagret som `vedtak`, ikke `palegg` (k28, k36, k37, k100, k103, middels).** Fasiten og FORMAT.md har gjort «A kan pålegge B» til paleggskompetanse (#353), men leksikonregelen `vedtak-forvaltningsverb` sender fortsatt «X kan gi pålegg» til vedtak. Rettingen hører hjemme i leksikonet, ikke per kort.
4. **Reaksjoner er lagret som `vedtak`, ikke `sanksjon` (k33, k101, middels).** Tvangsmulkt og overtredelsesgebyr er sanksjon etter FORMAT.md (#355). Å holde tilbake, redusere eller kreve tilbake tilskudd ved lovbrudd (§ 53 tredje ledd) er også en reaksjon. Regelverket kaller det selv «økonomiske reaksjoner» (FOR-2021-10-06-3079 § 4). Det skiller seg fra tilbakesøking ved feilutbetaling (k7, k46), som er vedtak og holder.
5. **Vilkår og tid mangler (k36, k37, k41, k103, k56, k7).** Overgangsforskriften fra 2021 gjelder bare tilsyn etter kapittel V der varsel var sendt før 1. januar 2022. Uten avgrensning står kommunen og statsforvalteren som gjeldende påleggsmyndighet i økonomisaker, mot hovedregelen i § 53 første ledd. Statsforvalterens pålegg etter § 55 andre ledd gjelder bare tilsyn «i særlige tilfeller» etter første ledd. Satsvedtaket i tildelingsforskriften § 9 (k56) er avløst av satsforskrift fra tilskuddsåret 2027 (finansieringsforskriften §§ 1 og 25).

I tillegg mangler motparten i k102 (statsforvalterens lovlighetstilsyn med kommunen, § 54).

## Tverrgående mønstre

- **Distributiv lesning på fra-siden fungerer.** Ingen av de 43 kortene har feil innehaver. «Kommunen» og «statsforvalteren» står i alle kortene for alle kommuner og alle embeter, og teksten knytter ingen av kompetansene til rollen «barnehagemyndighet». Det er riktig at de står på klassen, jf. #418 (b) («teksten avgjør»). Lovens egne steder for rollen er § 10 femte ledd (innsyn «for å ivareta kommunens oppgaver som barnehagemyndighet»), § 11 og § 30 andre ledd. Ingen av dem er blant kortene.
- **Klage fra klasse til klasse parer to grupper.** «Statsforvalteren er klageinstans for kommunens vedtak» er bare riktig for paret statsforvalter og kommune i samme embete (docs/33 §4.2). Oppslaget må velge paret via ansvarsområdet. Klassen statsforvalter har ingen M-kanter (hull, docs/33 §4.3), så klagekantene kan ikke besvare «hvem er klageinstans for X kommune» ennå.
- **Svakeste premiss (#411).** Alle 43 K-kortene og r2 har `normalisering`, men fra-noden er løst med standardregelen, og den har et premiss som ikke er vurdert. Det er registrert i #411 og er ikke gjentatt per kort.
- **K har ingen modalitet.** «Kommunen skal gi forskrift / skal fatte vedtak» (k5, k6, k10, k16, k56, k66, k97) mister «skal», som f47/f49 i runde 375. Det er en mangel i formatet.
- **Objekt brukes som avgrensning.** Unntaket for kapittel V (k99, #421), vilkåret i k7 og automatiseringen i k25 står i objekt eller mangler. Objektet blir kuttet ved 200 tegn (k102, k11; i k11 går bare ordet «året» tapt).
- **Fylkeskommunen** (k82, k96, k102) får ingen egen rad før klassen finnes (#424). Det er ikke kortenes feil.
- **Gyldighet.** Tildelingsforskriften gjelder til 31. desember 2026, og finansieringsforskriften gjelder fra tilskuddsåret 2027. Begge regimene er altså lastet som gjeldende. Bare k56 er reelt avløst i dag. For den midlertidige Ukraina-forskriften (k105, k106) gir Lovdata ingen sluttdato (kontrollert 2026-10-29).

## Prinsipielle innvendinger

Ingen. Modellregelen fra #418 (a) og (b) gir riktig resultat i alle kortene. Feilene ligger i leksikonet (pålegg, sanksjon), i klagemønstrene (objekt og til) og i at merknads- og veiledningsnoder er med i inndataene.
