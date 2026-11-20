# Juristrunde om «regjeringen» (#463): sammendrag

Johann ba 2026-11-18 om en juristrunde før modellvalget for «regjeringen» tas. De to lesningene var:

- **A:** statsrådet som eget organ (Grl. § 12 første ledd og § 15);
- **B:** et annet navn på Kongen i statsråd (Grl. § 13).

Ingenting er bygget, og ingenting er skrevet til basen.

## Filene

| Fil | Innhold |
|---|---|
| `utvalg.json`, `inndata.json` | Utvalget og kortene, se under. |
| `a/utfordringer.json`, `a/sammendrag.md` | Runde 1: juristens dom per kort. |
| `a/svar.json` | Modellererens svar. |
| `a/replikk.json` | Runde 2: juristens replikk. |

`inndata.json` har tre slags kort:

- **Q:** modellvalget mellom A og B;
- **R:** modellererens forslag til regel;
- **r1–r50:** ett kort per forekomst i utvalget, med lesningen modellereren foreslo.

## Grunnlaget

Ordene «regjeringen» og «regjeringens» står 268 ganger i 97 gjeldende rettskilder. Det er målt i delt `regelide` med SELECT 2026-11-18, og bare hele ord er telt, så «regjeringene» er ikke med.

Tallet 267 / 104 fra PR-en kom fra et grovere søk: det talte noder med ord som begynner på «regjeringen».

Utvalget ble trukket tilfeldig (seed 463), med inntil 2 forekomster per lov og 1 per forskrift. Det ga 26 forekomster fra lover og 24 fra forskrifter.

Juristen leste i tillegg:

- Grunnloven i korpuset;
- Dokument 19 (2011–2012), Grunnlovsspråkutvalget, pkt. 3.4.4;
- NOU 2019: 5;
- Håndbok for politisk ledelse (2026).

## Runde 1

| Dom | Antall | Kort |
|---|---:|---|
| Holder | 34 | |
| Innvending | 14 | 10 lav, 2 middels (r14, r42), 2 høy (R, r13) |
| Kontroller | 3 | r1, r2, r38 |
| Prinsipiell | 1 | Q |

Q fikk dommen prinsipiell fordi valget mellom A og B er falskt. Grunnloven selv bruker ordet i minst tre betydninger:

- styringen av riket (§§ 9, 13, 40, 41, 43, 44);
- statsrådet som kollegium (§§ 76 og 82);
- medlemmene, i uttrykket «medlemmer av regjeringen» (§ 72).

I § 25 tredje ledd betyr ordet dessuten den utøvende makt.

## Svar

Modellereren aksepterte 17 av 18 innvendinger og kontroller. Det gjelder også den reviderte regelen og rettingen av r13: unntaket i forvaltningsloven treffer Kongen i statsråd, ikke statsrådet alene.

Hun svarte «delvis» på Q. Hun er enig i at ordet skal tolkes etter sammenhengen. Uenigheten gjaldt hvor store enheter bindingen skal gjelde for: hun foreslo noden og ikke hver enkelt forekomst.

## Runde 2

Juristen «presiserer» Q. Hun godtar noden som standardenhet på tre vilkår:

1. En tagg for én forekomst har samme status som en binding (godkjent eller faglig vurdert, med gyldighet, og følger verket). Taggen går foran bindingen for noden.
2. Har noden flere forekomster og bare noen av dem er tagget, gir oppslaget «flertydig».
3. Ordlydsreglene virker som tagger per forekomst.

Hun viste også at premisset om to forekomster i samme node ikke holder for korpuset. 25 noder har minst to forekomster. Ett eksempel er Helsingforsavtalen art. 48 første ledd, der den tredje forekomsten omfatter Norge og gir en kant.

Modellereren godtar de tre vilkårene. **Det er ingen uenighet igjen mellom jurist og modellerer.**

## Anbefaling til Johann

1. **Hverken A eller B.** «regjeringen» blir ikke navneform for noe organ. Statsrådet (A) og Kongen i statsråd (B) skal begge finnes som organer.
2. **Ordet er kontekstavhengig og tolkes uten gjetting.** Ordlydsreglene prøves i denne rekkefølgen, og hver regel setter en tagg per forekomst:
   1. fremmed stat;
   2. generisk konvensjonsstat (omfatter også Norge);
   3. ikke aktør (regjeringen.no, avtaletitler, navnedeler);
   4. rollen statsråd («medlem av regjeringen», også i oppramsing);
   5. funksjonen (styret av riket);
   6. «Regjeringens res.» er Kongen i statsråd, etter Grl. § 32. `FastsattAvTolker` tolker det alt slik.
   
   Alt annet får et forslag merket [slutning], og et menneske bekrefter det.
3. **Johann må velge hvem plikten ligger hos** når «regjeringen skal legge fram for Stortinget» (r1, r2, r10). Den kan ligge hos kollegiet (A), slik Dok. 19 leser Grl. §§ 76 og 82, eller hos Kongen i statsråd (B), som godkjenner proposisjonene formelt. r38 avgjøres ikke av teksten.
4. **Modellen for regjeringen bygges ikke i #463** (Johann 2026-11-18). Regelen hører hjemme i en egen sak.
