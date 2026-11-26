# #446 runde 1: juristutfordreren

44 kort: U01–U30 (utvalget) og D1–D14 (definisjonene). Bare lesing. Korpuset er lest med `default_transaction_read_only=on`, og Lovdata er åpnet for U12, U18, U21, U22, U26, U28, U29 og U30, for atomenergiloven og for FOR-1974-12-13-4049.

## Tall

| | holder | innvending | kontroller | prinsipiell |
|---|---|---|---|---|
| U-kort (30) | 21 | 9 (lav 7, middels 2) | 0 | 0 |
| D-kort (14) | 3 | 9 (lav 4, middels 5) | 1 | 1 |
| **Totalt (44)** | **24** | **18 (lav 11, middels 7)** | **1** | **1** |

Klassen i utvalget: 24 av 30 holder. Seks klasser rettes, og bare to av rettingene endrer om kilden utelates: U28 (ukjent → rent_endringsvedtak) og U29 (ukjent → rent_endringsvedtak). De fire andre er rettinger mellom klasser som ikke utelates (U18 og U22 blandet → selvstendig, U30 ukjent → selvstendig). i_kraft rettes i fire kort: U01, U02 og U12 til ja, og U21 til utløpt.

## De viktigste innvendingene

1. **D2 (prinsipiell): formen er satt lik virkningen.** Reglene som bruker kolonnen, utelater på `rent_endringsvedtak == true` uten noe krav om i_kraft (HjemmelOpphevetFunnregel l. 210, UtgattAktorFunnregel l. 517, Sveipeutvalg l. 33, docs/33 §5.21). Et endringsvedtak som ikke er i kraft, har ikke uttømt virkningen. 57 av 403 rene endringsvedtak er ikke i kraft. I NL gjelder det 28 av 33, fordi Lovdata har endringslover i NL nettopp så lenge de ikke er helt i kraft. Rettingen: skriv true bare når klassen er ren og i_kraft = ja.
2. **D4 og D5 (middels): E- og O-mønstrene treffer setninger med eget innhold.** Unntaket for eget innhold gjelder bare for I, og rene opphevingsvedtak krever ikke signal fra tittelen. Fire kilder i populasjonen er feilklassifisert som rene, og ville blitt utelatt: DEL/forskrift/2000-12-01-1200 («gis myndighet til å oppheve», har i dag et #429-funn), DEL/forskrift/1999-12-10-1279 («oppheves og myndigheten delegeres til Luftfartstilsynet»), SF/forskrift/2012-06-08-499 (overgangsregler lest som E) og DEL/forskrift/2014-06-20-794 (delegering lest som E/Q). Rettingen: samme unntak for E og O, verbet må være hovedverb, og rene klasser bare med signal fra tittel eller hode.
3. **U28 (middels): tittelsignalet ble ikke godtatt, mot definisjonen.** «Ikrafttredelse av … Delegering av myndighet» med del II «punkt 4 skal lyde» er et rent endringsvedtak. Målet, DEL/forskrift/1974-12-13-4049, er i korpus, og delegeringen står konsolidert i § 3 der. Dagens funn hører hjemme i målet (D14).
4. **U21 og D7 (middels): tidsbegrenset og utløpt fanges ikke opp.** U21 gjaldt 17.01–31.12.2004 (Lovdata og ikrafttredelse_raa) og står med i_kraft = ja. Verdien «utløpt» mangler. Engangsdisposisjoner og organiseringsvedtak (navneendring U22) har også uttømt virkningen. #429 utelater dem allerede gjennom Livslopstyper, men #472 gjør det ikke (#438).
5. **D8 (middels): i_kraft leses av feil felt.** LTI-feltet «Kongen bestemmer» er frosset fra kunngjøringen. Ikraftsettingen ligger i korpus for U01 og U02, så de skal ha ja. Lovdatas hode kan også være utdatert når ikrafttredelsen avhenger av en betingelse: U12 står som «Ikke i kraft», men er i kraft 1.1.2022 etter fotnotene i atomenergiloven.
6. **D10 (middels, utenfor raden): #429 bør lese rollen per ledd.** Sitert ny tekst (Q) er historisk tekst også i blandede og selvstendige kilder når endringen er i kraft. 98 kilder går fra true til blandet, og de vil gi nye falske funn i Q-ledd.

## Tverrgående mønstre

- Kilder med signal fra tittelen og LTI-form er stabile. I utvalget fant jeg ingen feil i rene kilder med signal fra tittelen. Feilene ligger i DEL og SF og i kilder uten signal fra tittelen. Ingen av dem er i utvalget, fordi det bare er stratifisert etter klasse (D13). Presisjonen for de rene klassene, som er den tillatende konklusjonen, bør måles med et eget utvalg stratifisert etter prefiks og etter om det finnes signal fra tittelen.
- Vakten i tittelen virker. Den sendte delegeringer der I eller E var feillest, til ukjent i stedet for ren (D5). Den bør gjelde alle rene klasser.
- Ukjent fordi rå tekst mangler (24) er ikke én gruppe (D11). U29 er ny og lett å klassifisere når teksten hentes. U30 er opphevet på Lovdata (SFO, 1.9.2026), men står som Gjeldende i korpus. Statusen bør sjekkes før de klassifiseres.

## Det som holder

- Begrepet uttømt virkning for rene endrings-, ikrafttredelses- og opphevingsvedtak (D1, D3).
- At blandet og selvstendig aldri utelates (D6). Overgangsregler og forskriftshjemler i endringslover lever videre, og #472 må se dem.
- Klassen holder for alle 16 rene i utvalget: sju endringsvedtak, fem ikrafttredelsesvedtak og fire opphevingsvedtak. For U01, U02 og U12 er i_kraft feil lest.

## Prinsipielle innvendinger

Én: D2. Kolonnen som utelater, må bygge på at vedtaket både har ren form og er i kraft, ikke på formen alene.
