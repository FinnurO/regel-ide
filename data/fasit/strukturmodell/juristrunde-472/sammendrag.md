# Juristrunde 472: presisjonsmåling 1 for funnkategorien «Hjemmel opphevet»

Måling 1 gjelder maskinfunnene fra `monster:hjemmel-opphevet` (docs/33 §5.21, regelversjon `472-pr2-2026-11-24`), før noe går videre
fra «oppdaget» (Johanns avgjørelse 2026-11-18 (4), fase 1 AK 8). Prosjekteieren godkjente målingen 2026-11-24. Mot den delte basen er det
bare gjort lesing (SELECT, `default_transaction_read_only`). Ingen status og ingen vurdering er skrevet.

## Filene

| Fil | Innhold |
|---|---|
| `utvalg.json`, `utvalg.py` | Utvalget med frø 20261124, lagene og kvotene. Skriptet trekker det samme utvalget på nytt (kontrollert) |
| `inndata.json` | De 20 vurderingskortene (H01–H20): funnet, koblingene, hjemmelsstatusen og modellererens vurdering i runde 1 |
| `a/utfordringer.json`, `a/sammendrag.md` | Juristens runde 1 |
| `a/svar.json` | Modellererens svar (aksepter 9, delvis 1) |
| `a/replikk.json` | Juristens replikk i runde 2 (H15: godtar) |
| `resultat.json` | Endelig klasse per funn og tallene under |

## Utvalget

319 funn, alle «oppdaget». Utvalget er stratifisert etter undertype og prefiks, med fast frø og uten tilbakelegging:
- hjemmel_opphevet: 10 (DEL 6, LTI 3, SF 1)
- blandede_hjemler: 4 (SF 2, LTI 1, DEL 1)
- uavklart: 2 (SF 1, LTI 1)
- videreforing_utlopt: 2 (SF 1, DEL 1)
- gjenstandslos: 2 (DEL 2)

Begge de gjenstandsløse er med. Kvoten for de utløpte (DEL 1 i stedet for 0) er satt for at begge prefiksene skal være med.

**Klassene:**
- **riktig:** påstanden holder og undertypen er riktig. Hjemmelen er opphevet med den koblede bestemmelsen, og videreføringen finnes ikke, er utløpt, eller gjenstanden er opphevet;
- **feil:** påstanden eller undertypen er feil;
- **usikker:** kan ikke avgjøres fra kildene.

Sitat og eId og #446 (rent endrings- eller ikrafttredelsesvedtak) er vurdert for seg.

## Resultat etter to runder

| | Riktig / (riktig + feil) | Presisjon (Wilson 95 %) |
|---|---|---|
| **Totalt** | 11/19 | **0,58 (0,36–0,77)** |
| **Uten #446** (H18) | 11/18 | **0,61 (0,39–0,80)** |
| Uten populasjonsfeilene (H07, H08, H09, H13, H16) | 11/14 | 0,79 (0,52–0,92) |
| hjemmel_opphevet | 7/10 | 0,70 (0,40–0,89) |
| blandede_hjemler | 2/3 (+1 usikker) | 0,67 (0,21–0,94) |
| uavklart | 0/2 | 0 (0–0,66) |
| videreforing_utlopt | 1/2 (1/1 uten #446) | 0,50 (0,09–0,91) |
| gjenstandslos | 1/2 | 0,50 (0,09–0,91) |
| DEL | 7/9 | 0,78 (0,45–0,94) |
| SF | 4/5 | 0,80 (0,38–0,96) |
| LTI | 0/5 | 0 (0–0,43) |

- **Riktig (11):** H01–H06, H10, H11, H12, H17, H19.
- **Feil (8):** H07, H08, H09, H13, H15, H16, H18 (#446) og H20.
- **Usikker (1):** H14.
- **#446:** bare H18 er et rent ikrafttredelsesvedtak.
- **Sitat og eId:** feil i H07, H09 og H18 (feil opphevende bestemmelse eller feil dokument). Ellers er de riktige.

Utvalget er stratifisert og ikke proporsjonalt. Vektet grovt etter prefiks (DEL 140, SF 88, LTI 84 funn) blir anslaget om lag 0,57, så det
endrer lite.

Modellereren satte 16 riktig og 4 feil før runde 1. Juristen fant fem LTI-funn der forskriften selv er opphevet, og det var den største
rettingen.

## Feilmønstre og forslag til retting (ikke bygget)

Feilene er gruppert etter årsak. Oppdraget nevnte fem årsaker. «Populasjonen» er lagt til som en sjette, fordi den største feilkilden ikke
passer i noen av de fem. Juristen og modellereren er enige om det.

1. **Populasjonen: forskriften er selv opphevet.** Gjelder H07, H08, H09, H13 og H16, alle LTI.
   - Korpuset har vedtaket bare som kunngjort LTI-versjon (`kildeform = kunngjort`, status «Gjeldende»). Lovdata har det i DELO, merket «Opphevet».
   - For H08, H13 og H16 står opphevingen i en annen kilde i korpus:
     - DEL/forskrift/2006-06-30-761 pkt. 2;
     - LTI/forskrift/2013-07-18-921 ledd-2;
     - DEL/forskrift/2022-12-22-2510 ledd-2.
   - For H07 og H09 er opphevingen ikke i korpus. Militærnekterloven § 10 falt med kap. III ved LOV-2012-04-20-19, så forsvarsloven § 87 er feil opphevende bestemmelse.
   - Juristens tellesøk: minst 28 av de 84 LTI-funnene er nevnt med dato og nummer i en opphevingssetning i en annen kilde. Det samme gjelder 3 av 140 DEL-funn, der 1–2 er falske treff, og 0 av 88 SF-funn.
   - **Forslag:** før funnet skrives, sjekker regelen om forskriften selv er målet for en hel oppheving i korpus (samme lesing som R8 og målvedtaket, `Hjemmelstekstlesing.LesOppheving`). Treff gir kildefeil og ikke funn. Kunngjorte LTI-versjoner får premissmangelen «Lovdata-status for vedtaket ikke kontrollert», eller holdes utenfor til de er slått opp.
2. **Rekursjonen og aggregeringen.**
   - **H18:** regelen følger endringsloven til hovedloven (`fulgt_eli`) også når hjemmelen er endringslovens egen ikrafttredelsesbestemmelse (del II). Funnet sier da at endringsloven er «opphevet» ved en setning som opphever hovedloven.
     - **Forslag:** følg ikke endringsloven når hjemmelen er en del av endringsloven som ikke endrer hovedloven. Det gir `ukjent` (END).
   - **H20:** gjenstandsløs settes når én gjenstand er opphevet (`Any`, `Hjemmelsstatus.cs:235`), selv om odelslova lever og vedtaket også gjelder etater.
     - **Forslag:** gjenstandsløs bare når alle gjenstandene er opphevet. Ellers står det «delvis gjenstandsløs» med listen over de opphevede gjenstandene.
3. **Rollen.**
   - **H15:** FOR-1975-12-29-1 står i basedOn med rolle NULL og teller som premiss. En forskrift kan for en annen forskrift i høyden være et kompetanseledd, og den materielle hjemmelen (sivilforsvarsloven 1953 § 41) er videreført av sivilbeskyttelsesloven § 39. Riktig status er videreført.
     - Juristens forbehold: at § 41 er den materielle hjemmelen, bygger på Lovdatas basedOn, og ordlyden er ikke kontrollert.
   - **H16 (sekundært):** barnevernloven 1992 § 1-2 står bare i basedOn som hjemmelen bak hjemmelen.
   - **Forslag:**
     - En hjemmel som selv er en forskrift eller et vedtak, får rollen kompetanseledd (normalisering).
     - En basedOn-rad som hjemmelsteksten ikke nevner, teller ikke når en hjemmel i hjemmelsteksten er vurdert.
4. **Dekningen.**
   - **H14:** videreføringen av «forskrifter» (finansforetaksloven § 23-2 andre ledd, dekning `dokumenttype_ikke_nevnt`) regnes som «bærer» for et delegeringsvedtak.
     - **Forslag:** en slik videreføring teller som ikke vurdert, så undertypen blir uavklart og ikke blandede.
   - **H05 (gjorde ikke funnet feil):** premissmangelen om ulik ikraftsetting slår bare til når loven sier «til ulik tid». Støtteprosessloven ble satt i kraft i to trinn.
5. **Tekstlesingen og videreføringsformen:** ingen feil i utvalget.
   - Verken juristen eller modellereren fant noen oversett overgangsregel.
   - FOR-1995-03-15-254 § 40 (H15) er en delvis videreføring for vedlikehold, i en form regelen ikke leser.

**Svake funn som teller som riktige:**
- **H11:** forskriften har fått ny hjemmel («Tilføyd hjemmel»).
- **H12:** den opphevede hjemmelen bar bare en endringsbestemmelse, altså #446 på delnivå.

## Uenighet som står igjen

Ingen. Juristen godtok H15 i runde 2 og har ingen innsigelser mot de ni aksepterte svarene.

## Ikke kontrollert

- Lovdata-status for H11, H12 og H17 er bare delvis kontrollert.
- Overgangsforskrifter utenfor korpus er ikke kontrollert for utlendingsloven 2008 (H08) og sentralbankloven 2019 (H14).
- Ordlyden i sivilforsvarsloven 1953 § 41 (H15) er ikke kontrollert.
- Juristens telling av 28 av 84 LTI-funn er et tekstsøk, ikke en kontroll funn for funn.
- Rettspraksis er ikke kontrollert.
