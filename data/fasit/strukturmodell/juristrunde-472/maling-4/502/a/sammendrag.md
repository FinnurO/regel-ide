# Måling 4, del 1 (#502): juristens runde 1

Juristen vurderte kortene uten modellererens begrunnelser. Juristen har bare lest fra kopien `regelide_502_jurist` og fra Lovdata (åpne sider: LTI, DEL/DELO, INSO, SF, NL).

## Tallene

| Klasse | Antall | Kort |
|---|---|---|
| Riktig | 21 | E01–E10, K01–K11 |
| Feil | 0 | |
| Usikker | 1 | E11 |

| Dom | Antall |
|---|---|
| holder | 21 |
| innvending | 1 (E11, lav) |
| kontroller | 0 |
| prinsipiell | 0 |

- **E01–E08:** alle de åtte vedtakene er opphevet i sin helhet av den siterte setningen. Lovdatas ajourførte versjoner (DELO/INSO) viser dem som opphevet ved nettopp disse vedtakene, for E01, E03 og E05–E08 kontrollert hver for seg. E02 og E04 står i samme punkt 5 i FOR-2018-12-20-2052, som er lest. Funnet «Hjemmel opphevet» skal ikke stå.
- **E09:** funnet holder. Privatskulelova 1985 er opphevet, og verken overgangsforskriften eller privatskolelova 2003 § 8-1 viderefører forskrifter etter den.
- **E10:** kilden er opphevet, så #429-funnet er riktig forklart bort.
- **K01–K11:** alle lesingene er riktige. De fire setningene som vakten nå avviser (K07–K10), var feil godtatt før #502. K11 er riktig lest som delvis.

## Innvendingen

**E11 (lav, usikker):** at skiltforskriftene 1980 er opphevet, er riktig lest. Men beviset for «erstattet» er skiltforskriften 2005 § 28 første ledd, og den bestemmelsen ble valgt bare fordi den inneholder ordet «delegeres» (delegering til kommune eller politi for kommunal veg). Den tildeler ikke den myndigheten som ble delegert til Vegdirektoratet. Trolig er riktig bevis § 26 nr. 1 (Vegdirektoratets myndighet til høyere fartsgrense), og da er statusen riktig i sak [slutning]. 1980-teksten er ikke åpen på Lovdata. Utenfor raden: FOR-2005-11-09-1278 kap. II sier at delegeringen gjaldt «fram til 1. juni 2006». Vedtaket har løpt ut og bør ikke regnes som gjeldende. Feilen kommer fra den eldre regelen for tildeling (`TildelerKompetanse`, første node med «delegeres»), ikke fra #502.

## Mønstre og merknader

1. **#502 gjør det den skal.** De tre nye formene («av myndighet/fullmakt/utøvelse» etter nummeret, utstederen etter «fra», og en passert dato rett etter verbet) ga bare hele opphevinger i kortene. De nye vaktformene (gitt i medhold av, med hjemmel i, regler «i» målet, og en avgrensende setning etter) fjernet bare setninger som ikke opphever målet.
2. **Hele sveipet er lest** (r8-setninger.tsv, 76 rader), men bare sitatene, og ikke hver rad på Lovdata:
   - De 9 som går fra godtatt til avvist, er alle riktig avvist.
   - Ingen av de 56 som går fra avvist til godtatt, ser feil ut.
   - Blant de 11 som går fra avvist til avvist av vakten, er minst to hele opphevinger av dokumenter uten nummer: «retningslinjer 20. desember 2007» i INS/forskrift/2010-12-21-1790 og «forskrifter av 21. februar 1980» i SF/forskrift/1984-04-10-940. Utfallet er det samme som før #502, så dette er en dekningsmangel og ikke en regresjon.
3. **Datoen leses mot dagens dato** (`DateTime.Now` i `Opphevingskontroll`). Samme kjøring gir et annet svar før og etter datoen, uten at regelversjonen endres. K06 og fem rader med «1. januar 2026» ville vært lest som delvise før 2026-01-01. Lesingen er heller ikke konsekvent: «oppheves fra ‹dato›» og «med virkning fra ‹dato›» godtas uten datosjekk, mens «oppheves ‹dato›.» krever at datoen er passert. Det bør enten gjelde begge eller ingen. Det er en merknad, ikke en innvending mot noe kort.
4. **Hvitelisten ser bare på begynnelsen etter nummeret.** «… nr. N av myndighet til X, punkt 2, oppheves» ville trolig blitt godtatt som hel oppheving [slutning]. Det er ikke observert i sveipet og ikke testet.
5. **E1 går tapt for passiv med tittel:** «Samstundes blir lov 14. juni 1985 nr. 73 … (privatskulelova) oppheva.» (privatskolelova § 8-2 andre ledd) gir ikke E1. E09 får derfor E2 fra ikraftsettingsforskriften. Svaret er det samme, men beviset er svakere.

## Runde 2

Modellereren godtok E11 og merknad 5, og svarte «delvis» på merknad 3 og 4 (`svar.json`). Replikkene står i `replikk.json`:
- **E11: godtar.** Kortet er usikkert og telles ikke i presisjonen for #502. Oppfølgingen bør ta med både beviset (§ 28 i stedet for trolig § 26 nr. 1) og at vedtaket bare gjaldt til 1. juni 2006.
- **Merknad 3: presiserer.** Juristen godtar at en oppheving som ikke virker ennå, ikke er en oppheving (som ikraftstatusen i #446), og at inkonsekvensen med «fra ‹dato›» er eldre enn #502. Målinger og fasit må likevel oppgi kjøredatoen. Oppfølgingen bør også si hvilken vei det skal gå: en framtidig dato bør gi «ikke i kraft ennå», ikke «delvis».
- **Merknad 4: godtar.** Den føres som kjent begrensning.
- **Merknad 5: godtar.**

Ingen uenighet gjenstår som må til Johann. Tallene er uendret: riktig 21, feil 0, usikker 1.

## Ikke kontrollert

- Teksten i skiltforskriftene 1980 § 26 (E11). Den er ikke på Lovdatas åpne sider.
- Om mva-delen av FOR-2005-09-26-1093 ble delegert på nytt et annet sted (E05). Det påvirker ikke R8.
- Om tilskuddet i FOR-2003-02-13-224 nå har hjemmel i utdanningsstøtteloven, og om forskriften har uttømt virkningen (#446) (E09).
- Lovdata-status for målene i K-kortene og for de 56 nye godtatte radene. De er lest av sitatet.
- Om R8 selv sjekker at den opphevende kilden er i kraft (ikrafttredelse per dokument). Det er bare sett for hjemmelsstatusen.
