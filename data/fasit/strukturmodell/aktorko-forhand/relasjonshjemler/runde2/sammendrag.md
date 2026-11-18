# Juristutfordrer, runde 2: replikk (#469)

Modellereren aksepterte 25 kort som rettet. To fikk svaret «delvis» (n102.r1 og n102.r2). Replikk på disse to:

| Kort | Replikk |
|---|---|
| n102.r1 Folketrygden forvaltes av Helsedirektoratet | presiserer |
| n102.r2 Folketrygden forvaltes av Arbeids- og velferdsetaten | godtar |

**Ingen uenighet går videre til Johann.** Modellererens løsning er et eget `avgrensning`-felt per relasjon i fila, som knappen skriver som kantens `AvgrensningTekst`. Den oppfyller innvendingen fra runde 1: skriv med avgrensningen, ellers vent.

## Avgrensningsteksten

- **r1:** teksten bør være nøyaktig **«folketrygdloven kapittel 5 (stønad ved helsetjenester)»**, likt fasiten u303. Nytt argument: køknappen sjekker duplikater på kategori, type, fra og til, men `OpprettAsync` har avgrensningen i nøkkelen. Med ulik tekst kan kanten fra køen og fasitkanten (#387) bli liggende som to kanter for samme forhold. Parentesen er beskrivende. Kjernen «kapittel 5» står ordrett i ftrl. § 21-11 a første ledd, og ftrl. § 5-1 første ledd bekrefter innholdet.
- **r2:** **«folketrygdloven unntatt kapittel 5»** godtas. Den svarer til Nav-loven § 4 andre ledd («med unntak av kapittel 5») og er det motsatte av r1. Jeg anbefaler, men krever ikke, samme parentes som r1, så paret leses som komplementært. Ingen flere unntak skal legges til. Avgrensningen gjengir hjemmelen.

## Tall etter runde 2

Alle 27 innvendinger fra runde 1 er nå avklart. 25 er akseptert, og 2 er løst med den nye avgrensningen. Det eneste åpne spørsmålet til Johann er fortsatt spørsmålet fra runde 1, om konstitutiv «er» teller som pliktuttrykk for T `skal_ha` (n127, n139). Det er ikke en uenighet med modellereren.
