# Juristrunde 375b: sammendrag

Kortene er de 60 nye forslagene fra den tredje lastingen av piloten 2026-10-30, etter #420 (PR #430) og #419 (PR #431). Del a har 35 kort: mønsterlaget (m1–m3) og KI-lagets K-forslag (k1–k32). Del b har 25 kort: KI-lagets F- og P-forslag (f1–f25). Inndata: `inndata-a.json` og `inndata-b.json`. Hvert kort har med forslagene som alt står i køen for samme ledd, med anbefalingen fra juristrunde 375 eller 418 der den finnes.

**Runde 1** (`a/utfordringer.json`, `b/utfordringer.json`): 4 holder og 56 innvendinger (5 høy, 39 middels, 12 lav). Ingen kontroller og ingen prinsipielle. Avgjørelsene på #418 er ikke prøvd på nytt.

**Svar** (`a/svar.json`, `b/svar.json`): modellereren aksepterte alle innvendingene unntatt k23 (delvis). På k23 er spørsmålet om den nye raden for kommunens plikt til å gi grunntilskudd (barnehageloven § 19 a første ledd) skal ha de private barnehagene som motpart. Del b foreslo samme rad med til = null. For dublettene valgte modellereren å følge anbefalingen på den eldre kanten og avvise det nye forslaget.

**Runde 2** (`a/replikk.json`): juristen presiserte k23 og godtok til = null. Raden skal stå én gang (under f1). Det er derfor **ingen gjenværende uenighet**.

**Tverrgående funn:**
- **17 av 60 nye forslag er dubletter** av forslag som alt står i køen (15 i del a, f17 og f21 i del b). I tillegg er f13 og f22 nesten-dubletter av forslag i køen, og f5 og f6 av det nye f4. KI-laget foreslår nå typene juristrunde 418 ba om (sanksjon, godkjenning, vedtak, motpart i klage) som nye kanter. Rettingene fra 418 er ikke gjort i basen, og ulik type, motpart eller objekt gir en ny nøkkel etter #420. Derfor pares de ikke.
- **KI-laget fører «Kommunen skal …» som F forvaltning** (19 av 20 F-kort får innvending). Betalingsplikter skal være P betaling (5). Beregningsregler hører til regellaget (3), og henvisninger og saksbehandling er ingen oppgave (4).
- **Overgangsforskriften fra 2021 mangler tidsavgrensning** (k9–k17), som i runde 418.
- **Vilkår i sitatet mangler i avgrensningen** (m1–m3, k24, k29). De to stengingsvedtakene som #420 hentet tilbake (m2, m3), mangler vilkåret om at pålegget ikke er fulgt.
- **Hovedpliktene til å betale tilskudd mangler i basen** (barnehageloven § 19 a og § 19 b, tildelingsforskriften § 3). De står som nye rader i `anbefalinger.md`.
- **FORMAT.md (#353)** sier at «yter tilskudd til X» gir til = X. Pilotpraksisen har til = null for private tilskuddsmottakere. Formatet bør rettes (k23).
- **Påslaget** for administrasjon er en del av beregningen av tilskuddet, ikke en egen betaling (f2, f8, f19). Det løser tvetydigheten fra 418 k67.

**Tid og tokens** (harnessens telling for agentene): runde 1 del a 12 min 11 s og 314 327 tokens, del b 9 min 35 s og 287 140 tokens (parallelt), runde 2 52 s og om lag 11 000 tokens.

Anbefalt behandling i forslagskøen: `anbefalinger.md` (4 godkjenn, 22 rett og godkjenn, 34 avvis, og 5 rader å legge til for hånd).
