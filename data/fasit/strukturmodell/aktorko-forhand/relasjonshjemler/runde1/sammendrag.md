# Juristutfordrer, runde 1: hjemmel per relasjon (#469)

57 kort fra `inndata.json`. Jeg har kontrollert hjemmel, sitat, kunnskapsstatus og modalitet mot basen (`regelide`, bare SELECT). Alle nye sitater i `retting` står ordrett i noden de peker på.

## Tall

| Dom | Alvor | Antall |
|---|---|---:|
| holder | | 30 |
| innvending | lav | 24 |
| innvending | middels | 3 |
| innvending | høy | 0 |
| kontroller | | 0 |
| prinsipiell | | 0 |
| **Sum** | | **57** |

Av de 24 lave gjelder 16 bare sitat eller hjemmelssted i to grupper: tiltakssonen (n129, n188 ×8) og Trollheimen (n190 ×7).

## De viktigste innvendingene

1. **n15.r3, tilsynsmyndighet innehas av kommune (middels, `skrives: false`).** Forskriften om helse og miljø i barnehager og skoler (§§ 19–20) bærer at kommunen er tilsynsmyndighet. Men merknaden sier at forekomsten skilles ut, og kanten går likevel til samme rolle som Helsetilsynet og statsforvalteren (r1, r2). I innehar har ingen avgrensning. Da blir kommunen tilsynsmyndighet også i helse- og omsorgstjenesteloven. Relasjonen hører til den utskilte rollen.
2. **n102.r1 og r2, Folketrygden forvaltes av Helsedirektoratet og Nav (middels).** Hjemlene holder (ftrl. § 21-11 a og Nav-loven § 4 andre ledd, tekstfunn). Men hver forvaltning gjelder bare en del av folketrygden: kapittel 5, og alt unntatt kapittel 5. Fasiten (u303) og docs/33 §4.5 har denne avgrensningen. Knappen setter ingen avgrensning, så begge kantene ville sagt at organet forvalter hele ordningen. Det er en mangel i formatet. Kantene bør skrives med avgrensningen.
3. **n116.r1, Høyesteretts ankeutvalg (lav).** Hjemmelen bør være dl. § 4 første ledd («kan Høyesterett nedsette ett eller flere utvalg, som betegnes som Høyesteretts ankeutvalg»), ikke § 5, som bare regulerer sammensetningen. Fasiten (domstolloven u14) bruker § 4 og typen del_av.
4. **n3.r2, kommunen som barnehageeier (lav).** Bhl. § 11 første ledd sier uttrykkelig at kommunen har oppgaver «som barnehageeier». Det er tekstfunn og bedre enn § 7 første ledd, der eierrollen er en tolkning av «ansvaret».
5. **n190.r1–r7, Trollheimen og omegn (lav).** Trollheimenloven § 1 er en formålsbestemmelse og nevner ikke området. Reindriftsloven § 2 andre ledd er den eneste bestemmelsen som har både navnet «Trollheimen og omegn» og kommunelista, så den bør være hjemmelen. Statusen er fortsatt fortolkning.

Andre lave innvendinger:
- **n129 og n188.r1–r8:** sitatet er hele leddet med tabellen og stemmer med basen bare når linjeskift gjøres om til mellomrom. Snevres inn til definisjonen i siste punktum, som står ordrett.
- **n101:** at felles samarbeidsutvalg er et samarbeidsutvalg, er en tolkning (fortolkning, ikke tekstfunn). Sitatet snevres inn til tredje punktum.
- **n156:** sitatet er kuttet midt i bemanningsnormen. Det utvides til hele punktumet.
- **n33.r2, n144, n150:** «Klagenemnda» er en kortform som bindes til NOKUTs klagenemnd via kapittel 2. Status bør være normalisering, ikke tekstfunn. For n33.r2 er klagekompetansen dessuten avgrenset til de tre vedtakstypene i § 2.
- **n139:** kommuneloven § 5-3 sier at kommunestyret «er det øverste organet», ikke «skal». Modaliteten skal holder, men den er en lesning av teksten, så status bør være fortolkning.

## Det merknadene pekte på

- **Tilsynsmyndighet / FOR-2023-03-28-449:** riktig hjemmel, men feil node. Se punkt 1.
- **Dommer / dl. § 55:** § 55 gjelder utnevningen. Forslagene bruker §§ 19, 10 og 27 for «skal ha» og § 52 for at dette er dommere. Det holder, og det er bedre enn § 55.
- **Folketrygden / ftrl. § 21-11 a og Nav-loven § 4:** riktige hjemler, men kantene mangler avgrensning. Se punkt 2.
- **NOKUTs klagenemnd / § 2:** riktig hjemmel. Status og avgrensning er nevnt over.
- **Familiebarnehage / bhl. §§ 14–15:** forskriften om familiebarnehager § 1 første ledd («En familiebarnehage er en barnehageform») bærer relasjonen uttrykkelig og er bedre enn §§ 14–15. Holder.

## Tverrgående mønstre

- **Avgrensning finnes ikke i inndataene, og knappen skriver den ikke.** Det er den største svakheten. Den rammer n102.r1/r2 (middels) og n15.r3 (middels), og i mindre grad n33.r2, n3.r2 og n86.r5 («i denne lov»). Dette er en mangel i formatet, ikke en prinsipiell innvending. Men en kant som bare er riktig med avgrensning, bør ikke skrives med én knapp før avgrensningen kan følge med.
- **Kortform via kapittel** («Klagenemnda» i FOR-2025-07-02-1462) gir normalisering, ikke tekstfunn. Kapittelnodene har ingen overskrift i basen.
- **Lange sitater.** Der hele leddet er brukt, men bare ett punktum bærer relasjonen, bør sitatet snevres inn: tiltakssonen og felles samarbeidsutvalg. I den motsatte retningen er et sitat kuttet så meningen endres (pedagogisk leder).
- **Konstitutiv «er» som pliktuttrykk.** Johanns regel fra 2026-11-17 krever «skal» eller et tilsvarende pliktuttrykk for T. Kommuneloven § 5-3 («Kommunestyret er det øverste organet i kommunen», n139) og fvl. § 28 andre ledd («er klageinstansen kommunestyret …», n127) bruker «er», ikke «skal». Jeg har lest konstitutiv «er» som tilsvarende, fordi rollen eller organet da er lovbestemt for hvert medlem og ikke valgfritt, men med status fortolkning. Leses regelen bokstavelig, får begge `ordlyden_avgjor_ikke`, og det skrives ingen kant for kommunestyret. Det er etter min vurdering et urimelig resultat. Spørsmålet bør bekreftes av Johann. Det er ikke en innvending mot modellregelen.
- **Modalitet kan / ordlyden avgjør ikke** er riktig satt i alle tre tilfellene (n3.r1, n57, n143).

## Prinsipielle innvendinger

Ingen.
