# Pilot: barnehageloven med aktiv bruk (#375)

**Status:** REFERAT fra kjøringen 2026-10-28, før Johanns gjennomgang. §1–§7 er ikke endret etterpå. §8 er et tillegg fra den nye lastingen 2026-10-29 etter #418. Presisjon og gjenfinning mot det godkjente resultatet mangler til gjennomgangen er gjort (se §6). Nummer 07 fordi 05 er satt av til planen for testkorpus.

Første aktive bruk av Regel-IDE utenfor de fem fasitlovene: konvertering (mønster og KI) → lasting som forslag → juristrunde → gjennomgang i forslagskøen, sammenlignet med en kartlegging BU-prosjektet har gjort av kommunens plikter etter barnehageloven. BU-teksten er ikke i repoet; den omtales med punktnummer («BU 2.7») og korte omskrivninger. Verken BU eller Regel-IDE er fasit.

Data: `data/fasit/strukturmodell/pilot-375-kjoring.json` (tall per kilde og steg), `juristrunde-375-pilot.json` og `juristrunde-375/` (juristrunden og anbefalingene), `pilot-375-maling.sql` (måltall etter gjennomgangen).

## 1. Korpus

Barnehageloven (LOV-2005-06-17-64) er i korpuset, konsolidert 2026-08-01 og hentet 2026-09-03. Alle de sju forskriftene BU nevner, er i korpuset: FOR-2017-04-24-487, -2005-12-16-1477, -1478, -2015-11-11-1285, -2026-03-23-454, -2005-12-16-1555 og -2023-03-28-449. Forskriften BU ikke fant ID-en til (pedagogisk bemanning), er FOR-2017-06-22-1049. Om versjonene er de nyeste hos Lovdata, er ikke kontrollert; det skjer bare gjennom importøren.

Piloten kjørte loven og 19 forskrifter: de 20 gjeldende forskriftene med hjemmel i loven, minus to rene endringsforskrifter (FOR-2021-05-18-1550, FOR-2026-06-26-1394), pluss forskrift om helse og miljø (BU nevner den; hjemmelen er folkehelseloven). Til sammen 1 603 noder og omtrent 245 000 tegn.

## 2. Aktørsveip og navneformer

| | Mønster før | KI før | Mønster etter |
|---|---:|---:|---:|
| Utsagn | 118 | 297 | 118 |
| Aktørrader kjent | 7 | 18 | 25 |
| Ny navneform for kjent aktør | 11 | 14 | 2 |
| Ny aktør | 29 | 92 | 20 |

Skrevet før lasting: de korpusvide generiske navneformene **«kommunen»** og **«statsforvalteren»**. De løses med Johanns standardregler fra #394 (klassen kommune, klassen statsforvalter, kunnskapsstatus avledet). «departementet» fantes fra før og løses til Kunnskapsdepartementet via Lovdatas felt (sekundær kilde). «kommunen» alene sto for 132 forekomster i KI-sveipet.

Ingen aktør, klasse eller rolle er opprettet. Det som mangler, med forslag til sortering og hjemmel, står i #424. De største er Utdanningsdirektoratet (17 forekomster; opprettelsesvedtaket er ikke i korpuset), «barnehagen»/«barnehager» (37), «private barnehager» (14), barnehageeier og «den som er ansvarlig etter § 7» (9), fylkeskommunen (klassen finnes ikke), PPT, styrer og pedagogisk leder, foreldrerådet og samarbeidsutvalget.

Datafeil funnet under sveipet: navneformen «Fylkeskommune» peker på Agder fylkeskommune (#423).

## 3. Lasting (AK 1)

Forslag skrevet til den delte lokale basen: **71**, alle `maskinelt_foreslatt`.

| Lag | Utsagn | Nye forslag | Fantes som forslag | Dublett i kjøringen | Ikke lastet |
|---|---:|---:|---:|---:|---:|
| Mønster | 118 | 38 (K 33, F 4, R 1) | 0 | 0 | 80 |
| KI (DeepSeek-V4-Flash) | 354 | 33 (K 14, F 13, P 6) | 20 | 4 | 297 |

Kunnskapsstatus: 38 `normalisering` (mønsterlaget), 33 ikke vurdert (KI-laget).

Ikke lastet, med årsak:

| Årsak | Mønster | KI |
|---|---:|---:|
| Aktør ikke i katalogen | 33 | 207 |
| Avvist av skriveveien: klasse som fra i K | 47 | 56 |
| Avvist av skriveveien: annet (G uten til-node m.m.) | 0 | 6 |
| Fra ikke i teksten | 0 | 17 |
| Type uten databasetype (`annet:*`) | 0 | 10 |
| Generisk omtale uten binding | 0 | 1 |

Andre skrivinger: 109 navnekandidater (oppdagelseskilde `strukturforslag`, 92 ulike tekster, blant dem mønsterartefakter), 10 tagger og 4 aktørtypeforslag fra navnetreffene, 2 generiske navneformer. Ingen bindinger, aktører eller godkjenninger.

## 4. Juristrunden (AK 2)

92 kort: 71 forslag, 3 modellvalg (p1–p3) og 18 kontroller av modellererens lesning av BU. Runde 1: 38 holder, 53 innvendinger (11 høy, 18 middels, 24 lav) og 1 prinsipiell. Modellereren aksepterte alle innvendinger unntatt p1 (delvis). Runde 2: p1 presisert, p2 godtatt. **Det er ingen gjenværende uenighet.** Johann får to beslutninger (#418) og ett kontrollpunkt:

- **(a) Kompetanse med klasse som innehaver.** Skriveveien tar klasse som fra i P og F, men ikke i K. 103 utsagn om kommunens og statsforvalterens kompetanse falt derfor bort, blant dem godkjenning, dispensasjon, lokal forskrift, tilsyn, pålegg, stenging, klage og tilskuddsvedtak. Juristen og modellereren mener K bør ta klasse med distributiv lesning, som P og F.
- **(b) Rollen «lokal barnehagemyndighet».** Den står ordrett i § 10 første ledd, og § 11 krever at den holdes atskilt fra eierrollen. Den kommer i tillegg til (a).
- **Kontrollpunkt:** FOR-2008-09-12-1071 § 11 har departementet som klageinstans for godkjenning av utenlandske yrkeskvalifikasjoner, mens FOR-2025-07-02-1462 § 6 andre ledd legger de samme klagene til klagenemnda. Hvilken som gjelder, er et spørsmål om gjeldende rett.

Anbefalt behandling av de 71 forslagene (`juristrunde-375/anbefalinger.md`): 27 godkjenn, 23 rett og godkjenn, 21 avvis.

## 5. Sammenligning med BU (AK 4)

BU kartlegger kommunens plikter. Sammenligningen gjelder derfor utsagn der kommunen er aktør. «Regel-IDE fant» betyr at konverteringen ga utsagnet, lastet eller ikke; hvorfor det ikke ble lastet, står i kolonnen.

**Loven (36 BU-punkter):**

| Utfall | Antall | Punkter |
|---|---:|---|
| Begge fant, i hovedsak enige | 14 | 2.6, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 4.3, 5.4, 6.1, 6.5, 6.6, 7.1, 7.2 |
| Begge fant, uenige | 5 | 1.1 (unntaket for kap. V), 2.1 (§ 14 og § 14 a slått sammen), 5.1 (pliktsubjekt for ledelse), 5.3 og 6.2 (barnehagen eller kommunen) |
| Bare BU | 17 | 1.2–1.6, 2.2–2.5, 2.7, 3.1, 3.8, 4.1, 4.2, 5.2, 6.3, 6.4 |
| Bare Regel-IDE | 11 | § 12 (statsforvalteren klageinstans), § 19 a første, andre og fjerde ledd, § 20 første ledd, § 20 a, § 26 andre ledd tredje punktum, § 26 fjerde ledd, § 29 tredje ledd, § 53 tredje ledd, § 56 femte ledd |

Av de 19 som begge fant, er bare 2 lastet (1.1 og 6.1). 7 falt på klasse som fra i K, 9 på aktører utenfor katalogen og 1 på manglende fra-aktør. Ingen av de 11 som bare Regel-IDE fant, er lastet. Mange av punktene som bare BU har, fant ingen av KI-kjøringene: KI-laget ga ingen utsagn i §§ 5–19 i lastekjøringen (#422).

**Forskriftene:** av 5 kontrollerbare BU-punkter fant begge 3 (8.3 uenige, 8.4, 8.7) og bare BU 2 (8.1, 8.5). 8.2 og 8.6 inneholder ingen påstand som kan kontrolleres. Bare Regel-IDE: 27 utsagn om kommunen i de samme forskriftene, hvorav 5 lastet (og juristen anbefaler å avvise 3 av dem).

**Feil per kritisk feilklasse (docs/profil/00 §17.4).** BU-tallene er modellererens kontroll mot lovteksten, rettet i juristrunden (b-kortene). Regel-IDE-tallene gjelder de 71 lastede forslagene etter juristrunden, pluss lastingen.

| Feilklasse | BU | Regel-IDE |
|---|---|---|
| Omdanner adgang til plikt | 1 (6.4, § 47 a «kan behandle» listet som plikt) | 0 |
| Speilbildet: plikt gjort til adgang (ikke på lista) | 1 (2.7, § 19 «skal gi tilskudd» som «kan») | 2 (f47, f49: «skal gi forskrift» mister «skal», fordi K ikke har modalitet) |
| Plasserer plikten på feil aktør | 0 (6.2 og 6.6 omklassifisert i juristrunden) | 3 (f14, f15 snudd retning; f66 SSB som fra) |
| Forveksler kompetanse med faktisk handling | 0 | 2 (f11, f12: et økonomisk anslag som vedtak og forskrift) |
| Mister vilkår, unntak eller negasjon | 6 (1.1, 2.1, 2.6, 3.1, 6.1, 7.1) | 14 (unntak i objektet i stedet for avgrensning, kuttede objekter, tidsavgrensning; f9, f36, f37, f39, f41, f46, f48, f56, f59, f64, f2, f16, f69, f70) |
| Antar at grammatisk subjekt er rettssubjekt | 0 | 0 lastet; 39 tapte KI-utsagn har «barnehagen» eller «barnehager» som aktør |
| Forveksler klageinstans med klageadgang | 0 | 0 |
| Presenterer slutninger som tekstfunn | 10 (3.1, 4.1, 4.2, 5.1, 5.2, 5.3, 6.2, 8.1, 8.3, 8.7) | 36 (34 med `normalisering` selv om aktøren er løst med en regel, #411; f20, f21) |
| Bruker feil lovversjon | 0 | 1 (f1: delegeringen viser til en eldre § 14) |
| Utenfor klassene: merknad eller veiledning brukt som hjemmel | 0 | 6 (f11, f12, f29–f32, #419) |
| Utenfor klassene: saksbehandling eller beregning gjort til struktur | 0 | 11 (f7, f10, f14, f15, f22–f28, f71) |

Påstandene fra førstesammenligningen 2026-10-15, kontrollert mot dagens lovtekst: snudd modalitet i § 19 **holder** (men det er plikt → adgang, ikke omvendt). § 14 og § 14 a slått sammen **holder**. Tapt «med unntak av kapittel V» **holder** (§ 10 og § 53). Manglende klageinstans i § 12 **holder** som utelatelse. Feil pliktbærer i § 2 b **holder ikke**: «Kommunen skal samordne» står i § 2 b andre ledd; det BU mangler, er unntaket for barnekoordinator. Slutning som funn via § 7 for §§ 41–43 **holder** for § 41 og § 42; § 43 viser selv til § 7.

**De viktigste funnene:**
1. Regel-IDE får nesten ingenting av kommunens myndighet inn i køen. Årsaken er modellregelen om K (#418) og aktører som mangler, ikke konverteringen: 19 av BUs 36 lovpunkter ble funnet, men bare 2 ble lastet.
2. BUs typiske feil er slutninger presentert som verifiserte funn (10) og tapte unntak (6). Regel-IDEs typiske feil er tapte unntak og avgrensninger i mønsterlaget (14) og saksbehandling eller beregning gjort til struktur i KI-laget (11).
3. Begge mister unntaket «med unntak av kapittel V» som søkbar avgrensning. Regel-IDE har det i objektteksten (#421).
4. Regel-IDE finner det BU ikke har: klageinstansene (§ 12, § 26 fjerde ledd), kommunens forskriftskompetanse (§§ 20, 20 a, 26) og tilskuddsreaksjonen i § 53. I kartleggingen av kommunens ansvar er det disse som gir svar på «hvem kan overprøve» og «hva kan kommunen selv bestemme».
5. Regel-IDE har to feilkilder BU ikke har: merknader og veiledning lest som forskriftstekst (#419), og utløpte overgangsregler og delegeringer som står som gjeldende (f1, f2, f16, f69).

## 6. Måltall (AK 5)

**Tid (veggtid):** aktørsveip mønster 14 s og KI 7 min 4 s; generiske navneformer under 1 s (vurderingen er manuell); lasting mønster 16 s og KI 7 min 6 s; juristrunde 1 14 min og runde 2 42 s. KI-tokens i lastingen: 386 432 inn, 111 277 ut. Agentens analyse og sammenligning er ikke tidtatt hver for seg.

**Utsagn per kunnskapsstatus (lastet):** normalisering 38, ikke vurdert 33.

**Presisjon og gjenfinning** venter på Johanns gjennomgang. `data/fasit/strukturmodell/pilot-375-maling.sql` regner dem ut etterpå: presisjon per lag og kategori (godkjent / vurdert), gjenfinning (godkjente maskinforslag / godkjente maskinforslag + utsagn lagt til for hånd på de samme hjemlene etter piloten), og fordelingen per kunnskapsstatus. Gjenfinningen blir bare så god som det Johann legger til for hånd; utsagnene som falt bort ved lastingen, regnes ikke med før lasteren er kjørt på nytt etter #418 og #424.

## 7. Lærdommer for profilen

1. **Navneoppløsning er flaskehalsen, ikke uttrekket.** Konverteringen fant 19 av BUs 36 lovpunkter, men lastingen slapp gjennom 2. En pilot på en ny lov må starte med aktørsveip og en beslutning om de generiske omtalene. Profilen bør ha en eksplisitt regel for klasser som innehavere av kompetanse.
2. **«Barnehagen» er ikke et rettssubjekt.** Loven bruker virksomheten som grammatisk subjekt for plikter som binder eieren (§ 7). Profilen trenger et begrep for dette («pliktbærer via ansvarsbestemmelse») så slutningen kan stå som `avledet` med § 7 som premiss, ikke som tekstfunn og ikke som tap.
3. **Struktur og regel må skilles tydeligere i KI-instruksen.** Saksbehandling (utredning, begrunnelse) og beregning hører til regellaget. Begrepsmanualen har moteksemplene; KI-laget bruker dem ikke ennå.
4. **Tid er en del av utsagnet.** Utløpte delegeringer og overgangsregler må få gyldighet fra teksten. Ellers blir de gjeldende styringslinjer.
5. **Svakeste premiss må håndheves der utsagnet skrives** (#411). Lasteren ga `normalisering` til 34 utsagn som bygger på en regel med et ikke vurdert premiss.
6. **Sammenligning med en annen metode er nyttig begge veier.** BU fant 17 lovpunkter Regel-IDE ikke fikk med, og Regel-IDE fant klageinstanser og forskriftskompetanse BU ikke har. Feilene er av ulik art: BU presenterer slutninger som funn, mens Regel-IDE taper presisjon på vei inn i modellen.

## 8. Tillegg 2026-10-29: ny lasting etter #418

Johann avgjorde 2026-10-29 at K kan ha en klasse som innehaver, lest distributivt som P og F (a), og at rollen «lokal barnehagemyndighet» registreres som rolle klassen kommune har (b). Modellen er endret i docs/33 §4.3. Deretter er lasteren kjørt på nytt for de samme 20 kildene, med begge lag og samme bruker som i §3. Aktørsveipet er ikke kjørt på nytt. Kjøringen står i `data/fasit/strukturmodell/pilot-418-kjoring.json`.

**Skrevet før lastingen:** rollen «lokal barnehagemyndighet» (tekstfunn, § 10 første ledd, `maskinelt_foreslatt`, oppdagelseskilde `issue-418`) og rolleinnehavet «kommune innehar lokal barnehagemyndighet» (I fra klassen, normalisering, `manuelt_foreslatt`, fordi skjemaveien ikke tar oppdagelseskilde). § 11 første ledd og § 30 andre ledd står som kontekst i begrunnelsen. Ingen kompetanse er flyttet til rollen: juristrunden fant ingen kort der teksten knytter kompetansen til rollen og ikke til «kommunen».

**Før → etter, mønsterlaget** (deterministisk):

| | 2026-10-28 | 2026-10-29 |
|---|---:|---:|
| Utsagn | 118 | 117 |
| Nye forslag | 38 | 43 (alle K med klasse som fra: 28 kommune, 15 statsforvalter) |
| Fantes som forslag | 0 | 38 (de 38 fra 2026-10-28) |
| Dublett i kjøringen | 0 | 4 |
| Ikke lastet: aktør ikke i katalogen | 33 | 32 |
| Ikke lastet: klasse som fra i K | 47 | 0 |

Ett utsagn og én tapt aktør færre stemmer med rettingen av `plikt-dekkes-av` i PR #425 (§ 19 a). Av de 47 tapte ble 43 lastet. De 4 andre er **falske dubletter (#420)**: idempotensnøkkelen har ikke objektet, så et nytt utsagn med samme ledd, type og innehaver ble regnet som det første. Disse ble ikke lastet:
- § 53 andre ledd: kommunens vedtak om stenging (slått sammen med pålegget i samme ledd).
- § 55 andre ledd: statsforvalterens vedtak om stenging (samme).
- Finansieringsforskriften § 1 første ledd: et andre utsagn om kommunens forskrift (slått sammen med satsforskriften).
- Finansieringsforskriften, veiledningen til § 1 andre ledd: vedtaket om pensjonstilskudd per årsverk. Det står i en veiledningsnode (#419).

**Før → etter, KI-laget.** Laget er kjørt på nytt og er ikke deterministisk, så tallene er ikke parvise:

| | 2026-10-28 | 2026-10-29 |
|---|---:|---:|
| Utsagn | 354 | 351 |
| Nye forslag | 33 | 96 (K 76, F 20) |
| Fantes som forslag | 20 | 42 |
| Dublett i kjøringen | 4 | 10 (9 falske, #420) |
| Ikke lastet: aktør ikke i katalogen | 207 | 179 |
| Ikke lastet: klasse som fra i K | 56 | 0 |
| Ikke lastet: annet fra skriveveien | 6 | 1 (K fra klassen til seg selv) |
| Fra ikke i teksten | 17 | 9 |
| Type uten databasetype | 10 | 11 |
| KI-medlemskap sendes ikke | 0 | 3 |
| Generisk omtale uten binding | 1 | 0 |

Av de 96 nye har 63 klasse som fra i K (57 kommune, 6 statsforvalter). De andre 33 er ikke en følge av #418, men av at KI-laget ga andre utsagn denne gangen: 13 K med aktør som fra, og 20 F (19 med klasse som fra). 2 av de 42 «fantes som forslag» har et annet sitat enn kanten de ble regnet som. Det kan også være #420. Tid: mønster 11 s, KI 25 min 12 s (barnehageloven alene 7 min 26 s). KI-tokens: 398 540 inn og 111 135 ut, og 261 rader kastet av KI-konverteringen.

**De 103 tapte utsagnene:** 47 i mønsterlaget, hvorav 43 er lastet og 4 tapt på #420. KI-utsagnene fra 2026-10-28 finnes ikke lenger, så de 56 kan ikke følges ett for ett. I den nye kjøringen er ingen KI-utsagn avvist for klasse som fra, og 63 er lastet.

**Skrevet til den delte basen 2026-10-29:** 140 strukturkanter (43 mønster, 96 KI, 1 rolleinnehav), alle forslag, og 1 rolle. Pilotkildene har nå 211 forslag. Ingen er godkjent eller avvist. Lasteren skrev også navnekandidater og navnetreff som i §3 (ikke talt her).

**Kompetansen for ett medlem** regnes nå ut på aktørsiden (docs/33 §4.3, docs/09 §48). Alta kommune får 97 treff, alle merket «Forslag». 85 er kompetanse kommunen har via klassen: 28 `avledet` (mønsterlaget, normalisering) og 57 `uavklart`, fordi KI-forslagene ikke har vurdert kunnskapsstatus og det svakeste premisset bestemmer. 12 er kompetanse andre har overfor klassen kommune (10 fra klassen statsforvalter, 2 fra Kunnskapsdepartementet), hvorav 6 `avledet` og 6 `uavklart`. Statsforvalterembetene får ingen treff, fordi klassen statsforvalter ikke har registrert medlemskap. Det står som hull.
