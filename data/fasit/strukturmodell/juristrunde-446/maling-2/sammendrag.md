# #446 PR 1, måling 2 (nytt utvalg N01–N45), runde 1: juristutfordreren

45 kort som regel 446-pr1-v2-2026-11-25 kaller rene. Jeg har bare lest. Lovdatas rå HTML for alle 45 er lest fra korpuset (`default_transaction_read_only=on`), også der utvalg45.md er avkortet (N06, N08, N13, N43). Lovdata er åpnet direkte for NL/lov/2019-12-13-83, 2019-12-20-103, 2025-06-20-83, 2026-03-06-8 og 2026-06-12-28. NL/lov/2026-06-23-74 ga 404, så der er bare korpuset brukt. Ikraft er kontrollert mot fotnoter i kildens egen HTML og mot endringsmerknadene i målet i korpus (hentet 2026-09-03).

## Tall

| Lag | Kort | holder | innvending | Avgjør rent eller ikke (utelatelse) | Bare underklasse eller ikraft |
|---|---|---|---|---|---|
| B LTI endring | 19 | 19 | 0 | 0 | 0 |
| C LTI ikrafttredelse | 4 | 4 | 0 | 0 | 0 |
| D LTI oppheving | 6 | 6 | 0 | 0 | 0 |
| E DEL | 1 | 1 | 0 | 0 | 0 |
| F SF | 7 | 5 | 2 (lav 2) | 0 | 2: N34, N37 |
| G NL | 7 | 1 | 6 (middels 1, lav 5) | 1: N40 | 5: N38, N39, N42, N43, N44 |
| H STV / uten dokument-id | 1 | 1 | 0 | 0 | 0 |
| **Totalt** | **45** | **37** | **8 (middels 1, lav 7)** | **1** | **7** |

Ingen kontroller og ingen prinsipielle. Ingen innvending gjelder underklassen.

**Presisjon for «rent»:** 44 av 45 er rene (97,8 %, nedre Wilson-grense 0,884 ved 95 %). Per klasse: rent_endringsvedtak 34/34 (nedre grense 0,898), rent_ikrafttredelsesvedtak 4/4 (0,51), rent_opphevingsvedtak 6/7 (0,49). De to små klassene klarer ikke 0,85 på dette utvalget alene, men det skyldes mest at de har få kort. Samlet, og for endringsvedtak, er kravet oppfylt.

**Uttømt virkning:** 35 kort har true, og 34 av dem er riktige (nedre grense 0,855). Feilen er N40. Ingen kilde står som true der den skulle vært false på grunn av ikraft. De sju NULL-kortene skulle vært false (sikkert ikke helt i kraft). Begge verdiene gir funn, så dette endrer ikke utelatelsen.

## Innvending som avgjør utelatelse

**N40 (middels), NL/lov/2019-12-13-83 «Lov om opphevelse av omstillingslova»: overgangsregel lest som ikrafttredelse.** § 2 har overskriften «Meldinger under behandling» og lyder: «Meldinger som er mottatt eller er under behandling i fylkeskommunen når loven trer i kraft, avsluttes.» Dette er en overgangsregel som pålegger fylkeskommunen noe, ikke en ikrafttredelse. Regelen ga den rollen I fordi «trer i kraft» står i leddsetningen, mens hovedverbet er «avsluttes». Riktig klasse er blandet, og uttømt virkning er false.

Forslag til retting: Et ledd er I bare når ikrafttredelsen er hovedutsagnet. «Når / før / etter at loven trer i kraft» i en leddsetning er ikke nok. I tillegg bør en paragrafoverskrift som ikke betyr ikrafttredelse, oppheving eller endring gi S.

Søk i populasjonen: Jeg søkte i gjeldende kilder med endrings-, opphevings- eller ikraftform i tittelen etter teksten «(når|før|etter at) (loven|forskriften …) trer i kraft». Søket ga fem andre treff. Jeg vet ikke hvilke av dem regelen kaller rene, og har ikke lest dem i sin helhet. Modellereren bør sjekke dem:
- NL/lov/2024-06-25-56: «Opphevelsen av § 6-2 fjerde ledd gjelder likevel også for eldre saker …»
- NL/lov/2011-12-16-59
- NL/lov/2014-03-07-5
- DEL/forskrift/1965-06-25-3368: tittelen har også «Delegering av myndighet»
- DEL/forskrift/2007-01-26-88: treffet ser ut til å stå i en merknad

## Innvendinger som bare gjelder ikraft (alle lav)

- **delvis i stedet for ukjent:**
  - N38: fotnoten i kildens egen HTML sier «… med unntak av opphevelsen av § 4».
  - N42: fotnoten sier «… med unntak av romertall I § 5-5 nytt tredje ledd».
  - N37: tre vassdrag flyttes «fra den tid Miljødirektoratet bestemmer», og målets merknad viser at det ikke er skjedd.
- **ikke_i_kraft i stedet for ukjent:**
  - N39 og N43: Lovdata viser status «Ikke i kraft».
  - N44 og N34: endringsmerknaden i målet sier «Endres ved … (i kraft fra den tid Kongen/Miljødirektoratet bestemmer)».
- I alle sju endres uttømt virkning fra NULL til false. Begge verdiene gir funn, så bare kunnskapsstatusen endres.

## Tverrgående mønstre

- Rettingene etter måling 1 virker. Ingen delegeringsvedtak med bare «Endrer» i hodet er kommet med. Ingen overgangsregel etter «– – –» er lest som sitat. Lagene B–F og H (38 kort) har riktig klasse i alle kort.
  - N10 («Delegeringsvedtaket skal lyde:») erstatter hele vedtaksteksten, men målet oppheves ikke i samme dokument. Det er derfor et riktig endringsvedtak.
  - N33: avsnittet «Forbudet mot import …» er Lovdatas leddfortsettelse inne i sitert § 2-3 tredje ledd. Det er riktig behandlet.
- Den gjenstående svakheten er overgangsregler som ikke har «overgang» i tittel eller overskrift, og som inneholder ikraftord i en leddsetning (N40). Det er samme familie som M29 i måling 1, men utløses på en annen måte.
- Ikraft er fortsatt systematisk ukjent der «I kraft fra» har «Kongen/X bestemmer». Fotnoten («Fra … iflg. res. … med unntak av …»), Lovdatas statusfelt («Ikke i kraft») og målets merknader («Endres ved … (i kraft fra den tid …)») avgjør spørsmålet sikkert i alle sju kortene. Det er det samme mønsteret som M38/M41/M43 i måling 1. Det påvirker ikke utelatelsen.
- Observasjon, ikke en innvending: Som M29/M40 i måling 1 er N40 blandet bare på grunn av en engangsregel som er løpt ut (1.1.2020). Om slike kilder skal regnes som uttømt, er fortsatt et spørsmål for prosjekteieren (#438). Jeg reiser det ikke som prinsipielt.

## Prinsipielle innvendinger

Ingen.
