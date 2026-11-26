# #446 PR 1, presisjonsmåling 2, runde 1: juristutfordreren

45 kort (M01–M45) som regelen 446-pr1-2026-11-25 kaller rene. Jeg har bare lest. Lovdatas rå HTML for alle 45 er lest fra korpuset (`innhold`, `default_transaction_read_only=on`), også der utvalg45.md er avkortet (M06, M07, M18, M33, M35, M38, M45). Lovdata er åpnet direkte for NL/lov/2020-04-03-18 og NL/lov/2023-06-20-77. For M38 og M27 ga NL/DEL-adressen 404, så LTI-versjonen er lest (hodet der er frosset). Ikraftsettingsvedtak og målets endringsmerknader er sjekket i korpus.

## Tall

| Lag | Kort | holder | innvending | Av dem: rent eller ikke (avgjør utelatelse) | Av dem: bare underklasse eller ikraft |
|---|---|---|---|---|---|
| A bare hodesignal | 10 | 6 | 4 (middels 3, høy 1) | 4: M01, M03, M05, M08 | 0 |
| B LTI endring | 8 | 7 | 1 (middels) | 1: M18 (ikraft, NULL → true) | 0 |
| C LTI ikrafttredelse | 3 | 3 | 0 | 0 | 0 |
| D LTI oppheving | 4 | 4 | 0 | 0 | 0 |
| E DEL | 6 | 5 | 1 (middels) | 1: M29 | 0 |
| F SF | 6 | 6 | 0 | 0 | 0 |
| G NL | 6 | 1 | 5 (middels 1, lav 4) | 1: M40 | 4: M38, M39, M41, M43 |
| H STV | 2 | 2 | 0 | 0 | 0 |
| **Totalt** | **45** | **34** | **11 (høy 1, middels 6, lav 4)** | **7** | **4** |

Ingen kontroller og ingen prinsipielle.

**Presisjon:** Regelen kaller 45 kilder rene, og 39 av dem er rene (87 %). Feilen sitter i lag A, der 6 av 10 er rene. Utenfor lag A er 33 av 35 rene. Av de 37 kortene med uttømt virkning = true er 31 riktige; seks skulle vært false (M01, M03, M05, M08, M29, M40). Én kilde skulle vært true, men står som NULL (M18).

## Innvendinger som avgjør utelatelse

1. **M03 (høy), M01, M05 og M08 (middels): erstatningsvedtak lest som endringsvedtak.** Delegeringsvedtakene etter energiloven og naturmangfoldloven sier at det forrige vedtaket «endres» (eller «Nytt vedtak … skal lyde»). Deretter følger hele den nye delegeringen og «Fra samme tidspunkt oppheves delegeringsvedtak …» (det samme vedtaket). Målet oppheves altså av vedtaket selv, så innholdet lever bare her. Hodefeltet «Endrer» er eneste signal. M03 (DEL/forskrift/2025-06-26-1340) er den **gjeldende** delegeringen til NVE, og med true ville #472 og #429 tie om den. Regelen er også inkonsistent: forgjengere med samme form (LTI 2013-11-27-1386, LTI 2019-10-31-1455, DEL 2025-02-28-320, LTI 2015-10-09-1178) er klassifisert selvstendig. Utenfor utvalget har LTI/forskrift/2021-01-08-65 samme feil. Av 19 rene kilder i lag A i hele populasjonen har 5 dette mønsteret. Retting: er målet for «endringen» opphevet i samme dokument, eller er instruksen «Nytt vedtak … skal lyde», så er teksten eget innhold.
2. **M40 (middels): overgangsregel etter Lovdatas «– – –».** NL/lov/2017-12-19-118 har «gjøres følgende endringer: – – –» og deretter overskriften «Overgangsregel til eigedomsskattelova §§ 3 og 4:». Overgangsregelen er eget innhold i endringsloven og er selv endret i 2018. Regelen teller ikke overskriften og lar sitatet fortsette etter «– – –». Riktig klasse er blandet. Retting: «– – –» avslutter sitatspennet, og en overskrift som «Overgangsregel …» etter det starter eget innhold. I populasjonen fant jeg bare denne kilden med akkurat dette mønsteret.
3. **M29 (middels): overgangsregel lest som ikrafttredelse.** DEL/forskrift/2003-07-04-896 heter «Ikraftsetjing og overgangsreglar». Punkt 2 («For skolar som er godkjende etter … privatskulelova, gjeld …») er en overgangsregel for en gruppe og er senere endret. Unntaket for eget innhold («for saker/søknader/personer …») fanger ikke «For skolar». Riktig klasse er blandet. Retting: «overgangsregl-» i tittelen bør stenge for ren klasse. Dette er den eneste rene kilden i populasjonen med «overgang» i tittelen.
4. **M18 (middels): ikraft burde vært ja.** LTI/lov/2008-06-20-47 er satt helt i kraft ved tre «(Delvis) ikraftsetting»-vedtak som alle ligger i korpus (2008-10-17-1124, 2010-03-19-407, 2011-01-28-99). D8 har ikke slått inn. Følgen er at kilden gir funn (NULL), men skulle vært utelatt.

## Innvendinger som bare gjelder underklasse eller ikraft

- **M38, M41, M43 (lav): ikraft ukjent → delvis.** Fotnoten og målets endringsmerknader i korpus («Endres ved lov … (i kraft fra den tid Kongen bestemmer)») viser sikkert at loven bare delvis er i kraft. NULL og false gir begge funn, så bare kunnskapsstatusen endres. Når målet i korpus har slike merknader, er det en direkte og billig kilde til ikraft per endring.
- **M39 (lav): rent_endringsvedtak → rent_opphevingsvedtak, ukjent → ikke_i_kraft.** «Lov … oppheves» ble merket som sitert tekst, og CO2-avgiftsloven er fortsatt gjeldende i korpus.

## Tverrgående mønstre

- Lag A er svakt, med 4 av 10 feil. Feilene er systematiske: «endres» pluss full ny tekst pluss oppheving av målet. Ingen av de øvrige lagene har feil med samme årsak. De 9 rene kildene i lag A utenfor utvalget bør sees over for hånd, blant dem LTI/forskrift/2018-06-18-2321 (namsfogdsammenslåing) og LTI/forskrift/2004-10-08-1395 (omorganisering). Jeg har ikke lest dem.
- Lagene B, C, D, F og H (23 kort) holder. Klassen er riktig i alle 23, og den ene innvendingen der (M18) gjelder ikraft.
- I NL er klassen stort sett riktig, men ikraft er systematisk ukjent også der fotnoter eller målets endringsmerknader avgjør spørsmålet. Det gjelder alle de fire ukjent-kortene i G.
- Observasjon, ikke en innvending: både M29 og M40 er blandet bare på grunn av en overgangsregel som har løpt ut (2006 og 2024). Etter de avgjorte reglene er de ikke uttømt. Om blandet med utløpt eget innhold skal kunne regnes som uttømt, er et spørsmål for prosjekteieren og hører eventuelt til #438. Jeg reiser det ikke som prinsipielt her.

## Prinsipielle innvendinger

Ingen.
