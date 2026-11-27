# Juristrunde 472, måling 4 del 1: #502 (R8, flere former for hel oppheving)

Måling 4 del 1 gjelder #502 (docs/33 §5.15, merknaden «#502»). Prosjekteieren ga klarsignal 2026-11-27. **Målingen er gjort i kopien
`regelide_502_jurist`**, en klone av `regelide_502` etter kaldkjøringen, før noe er skrevet mot den delte basen. Juristen har bare lest fra kopien
og fra Lovdata. Kjøredatoen er 2026-11-27: en dato rett etter verbet leses mot dagens dato (se merknad 3 under).

## Filene

| Fil | Innhold |
|---|---|
| `r8-setninger.tsv` | Sveipet: alle (node, mål) i gjeldende primærkilder der lesingen endres. Kolonnene er før, etter, kilde, eId, mål (år-mnd-dag-nr), sitat og hele teksten |
| `inndata.py`, `inndata.json` | De 22 vurderingskortene: E01–E11 (endrede utfall) og K01–K11 (kontroll fra sveipet) |
| `a/utfordringer.json`, `a/sammendrag.md` | Juristens runde 1 |
| `a/svar.json` | Modellererens svar (aksepter 2, delvis 2) |
| `a/replikk.json` | Juristens replikk i runde 2 (godtar 3, presiserer 1) |
| `resultat.json` | Endelig klasse per kort, tallene og oppfølgingen |

## Sveipet

Alle noder i gjeldende primærkilder med et oppheve-ord, og barna til en opphevingsformel, er lest med koden før og etter #502 (4 357 par av node og
mål). 76 par endres:
- **56 går fra avvist til godtatt.** Alle er lest. Formene er «av myndighet/fullmakt/utøvelse …» etter nummeret, utstederen etter «fra», og en passert
  dato rett etter verbet.
- **9 går fra godtatt til avvist av vakten.** Alle var feil godtatt før: det som oppheves, er gitt i medhold av målet («forskrifter gitt i medhold av
  lov … nr. 62», «Tidligere enkeltvedtak … fastsatt med hjemmel i lov …», «løyve … etter lov …»), eller målet nevnes bare i en annen sammenheng.
- **11 går fra avvist til avvist av vakten.** Utfallet er det samme.

Ingen setning som ble avvist før, er godtatt ved en feil. Fire setninger ville vært det uten tilleggene i vakten. Det var bare datoen etter verbet
som stoppet dem før: tillatelser gitt i medhold av fire forskrifter (SF/forskrift/2017-06-15-844 § 144), reglene om ett fag i handverksloven
(DEL/forskrift/1997-03-21-228) og en oppheving som neste setning avgrenser til nye saker (LTI/forskrift/2016-12-13-1596). En dato som ikke er
passert (SF/forskrift/2026-03-23-454 § 25, «oppheves 31. desember 2026»), gir ingen hel oppheving.

## Resultat etter to runder

| | Riktig / (riktig + feil) | Presisjon (Wilson 95 %) |
|---|---|---|
| **Totalt** | 21/21 (+1 usikker) | **1,00 (0,85–1,00)** |
| Endret (E01–E11) | 10/10 (+1 usikker) | 1,00 (0,72–1,00) |
| Kontroll (K01–K11) | 11/11 | 1,00 (0,74–1,00) |

- **E01–E08:** alle de åtte LTI-vedtakene er opphevet i sin helhet av den siterte setningen. Lovdata (DELO/INSO) viser dem som opphevet av nettopp
  disse vedtakene. Funnet «Hjemmel opphevet» skal ikke stå.
- **E09:** det nye funnet holder. Privatskulelova 1985 er opphevet, og ingen overgangsregel viderefører forskrifter etter den.
- **E10:** kilden er opphevet, så #429-funnet er riktig forklart bort og kildefeilen riktig.
- **E11 (usikker):** opphevingen av skiltforskriftene 1980 er riktig lest, men statusen «erstattet» bygger på den eldre tildelingsregelen. Den er
  ikke endret av #502.
- **K01–K11:** riktige.

## Uenighet som står igjen

Ingen. Juristen godtok E11 og merknad 4 og 5, og presiserte merknad 3.

## Oppfølging (ikke bygget)

1. **E11:** «erstattet» peker på skiltforskriften 2005 § 28 første ledd fordi den inneholder «delegeres». Trolig riktig bevis er § 26 nr. 1.
   FOR-2005-11-09-1278 gjaldt dessuten bare «fram til 1. juni 2006».
2. **Datosjekken (merknad 3, presisert):** en framtidig dato bør gi «ikke i kraft ennå», ikke «delvis». Sjekken bør også gjelde «fra ‹dato›» og «med
   virkning fra ‹dato›», som ble godtatt uten datosjekk før #502. Målinger og fasit må oppgi kjøredatoen.
3. **Hvitelisten** leser bare begynnelsen etter nummeret («… nr. N av myndighet til X, punkt 2, oppheves» ville blitt godtatt). Det gjelder også
   titlene fra før. Ikke observert i sveipet.
4. **Passiv form med tittelen mellom målet og verbet** gir ikke E1 (privatskolelova § 8-2 andre ledd). E09 fikk E2 fra ikraftsettingen.
5. **Dokumenter uten nummer i lister** avvises av vakten (INS/forskrift/2010-12-21-1790, SF/forskrift/1984-04-10-940), som før.

## Ikke kontrollert

- Teksten i skiltforskriftene 1980 § 26 (ikke åpen på Lovdata).
- Lovdata-status for målene i K-kortene og de 56 nye godtatte radene. De er lest av sitatet.
- Om tilskuddet i FOR-2003-02-13-224 har fått ny hjemmel, og om forskriften har uttømt virkningen (#446).
