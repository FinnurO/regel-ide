# Juristrunde #418: sammendrag

Kortene: 106 nye K-forslag med klasse som fra fra den nye lastingen av piloten 2026-10-29 (del a: 43 mønster + rollen r1 og rolleinnehavet r2; del b: 63 KI). Inndata: `inndata-a.json`, `inndata-b.json` (samlet i `../juristrunde-418-kort.json`).

**Runde 1** (`a/utfordringer.json`, `b/utfordringer.json`): 26 holder, 81 innvendinger (35 høy, 31 middels, 15 lav), 1 kontroller (k45), 0 prinsipielle. Modellregelen fra #418 (a) og (b) gir riktig innehaver i alle kortene, og ingen kort bør flyttes fra klassen til rollen. Rollen (r1) og rolleinnehavet (r2) holder.

**Svar** (`a/svar.json`, `b/svar.json`): alle innvendinger akseptert, unntatt k4, k27, k41, k43, k82 og k95 (delvis: objekt mangler, men kanten er avgrenset av hjemmel-eId-en, så alvoret bør være lav).

**Runde 2** (`a/replikk.json`): juristen godtok for k4, k27, k43, k82 og k95 (alvor lav, rettingen står) og fastholdt k41 med et nytt argument: overgangsvilkåret står i §§ 1–2 i overgangsforskriften og kan ikke leses av hjemmelen i § 5. Modellereren godtar det. Det er derfor **ingen gjenværende uenighet**.

**Tverrgående funn** (ikke feil i #418, men i uttrekket):
- 33 kort hviler på merknader eller veiledning (#419), og mange er dubletter av rader med riktig hjemmel. Bør løses i lasteren.
- «Gi pålegg» lagres som vedtak, ikke pålegg, og økonomiske reaksjoner som vedtak, ikke sanksjon (leksikonet i mønsterlaget).
- Klagekanter uten sakstype i objektet; noen mangler motparten kommune.
- KI-laget leser plikter («kommunen skal gi tilskudd») som kompetanse, og beregningsregler som vedtak.
- Overgangsforskriften fra 2021 og tildelingsforskriften mangler tidsavgrensning.
- Svakeste premiss: mønsterlaget gir normalisering når fra er løst med standardregelen (#411).

Anbefalt behandling i forslagskøen: `anbefalinger.md`.
