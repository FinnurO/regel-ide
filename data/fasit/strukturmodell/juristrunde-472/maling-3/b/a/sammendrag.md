# Måling 3b, runde 1: juristens vurdering av 16 nye uavklart-funn

Gjelder #472 (sak #501), etter at prosjekteieren valgte alternativ (a) 2026-11-25. Utvalget er 16 av de 163 nye funnene med undertype uavklart
(DEL 12, INS 2, LTI 1, SF 1). Det er bare lest fra kopien `regelide_501b` (SELECT med `default_transaction_read_only=on`). Lovdata er slått opp
for alle 16 vedtakene.

## Tallene

| | Antall |
|---|---|
| Dom: holder | 11 |
| Dom: innvending | 5 (høy 1, middels 3, lav 1) |
| Dom: kontroller / prinsipiell | 0 / 0 |
| Klasse: riktig | 11 (N01–N04, N06–N08, N10–N13) |
| Klasse: feil | 5 (N05, N09, N14, N15, N16) |
| Klasse: usikker | 0 |
| **Presisjon** (riktig / (riktig + feil)) | **11/16 = 0,69** (Wilson 95 %: 0,44–0,86) |

Per prefiks: DEL 10/12, INS 1/2, LTI 0/1, SF 0/1.
Feilårsaker: dekningen 3 (N09, N14, N16), tekstlesingen 1 (N05), populasjonen 1 (N15).

## De viktigste innvendingene

1. **Enkeltvedtak i SF og INS er klart dekket (N14, N16, middels).** Statusen for Lovisenberg diakonale høgskole (INS) og Postens tillatelse til å
   bruke «Norge» på frimerker (SF) er vedtak om rettighetene til en navngitt part, altså enkeltvedtak [slutning]. Overgangsreglene nevner
   enkeltvedtak: uhl. 2024 § 17-3 første ledd sier «forskrifter og enkeltvedtak», postloven 2015 § 57 fjerde ledd sier «enkeltvedtak». Regelen
   leser typen ut fra basen (SF = forskrift, INS = instruks), og derfor blir dekningen «dokumenttype ikke nevnt». Riktig status er videreført, og
   funnet skal ikke stå.
2. **Endringsloven er fulgt til hovedloven (N05, høy).** Delegeringen gjelder myndigheten i endringsloven 2001 nr. 43 til å «bestemme ikrafttredelse
   og gi overgangsbestemmelser». Regelen følger endringsloven til verdipapirhandelloven 1997 og siterer opphevingen av den. Påstanden sier derfor at
   LOV-2001-06-15-43 er opphevet. Det er feil rettsgrunnlag: hjemmelen er endringslovens egen del (END), og virkningen er i hovedsak uttømt (#446).
   END-regelen fra PR 4 slo ikke inn fordi hjemmelsteksten ikke nevner delen.
3. **Vedtaket er selv opphevet (N15, middels).** Instruksen for Sivilforsvarets distriktssjefer er opphevet ved forskrift 20. mai 2022 nr. 874
   (Lovdata INSO). Den forskriften er ikke i korpus. Premissmangelen for kunngjort versjon peker på nettopp dette, men funnet skal ikke stå.
4. **Myndigheten finnes ikke lenger (N09, lav).** Delegering av påtalebegjæring etter straffeloven 1902 § 79 fjerde ledd. Etter straffeprosessloven
   § 62 a første ledd forfølger påtalemyndigheten straffbare handlinger uten begjæring, og en tilsvarende ordning finnes ikke [slutning]. Spørsmålet
   om dekning er da ikke reelt, og riktig undertype er hjemmel opphevet. Funnet står uansett. Det skiller seg fra viltlovvedtakene E09–E11: der
   finnes myndigheten i den nye loven.
5. **Påstanden passer dårlig på instrukser (N13, merknad, klassen er riktig).** Påstanden sier at et vedtak uten overgangsregel «i utgangspunktet» er
   uten rettslig virkning. En instruks bygger på instruksjonsmyndigheten og ikke bare på loven [slutning]. I N13 er kompetanseleddet (forskrift 1998
   nr. 1484) i tillegg aktivt under matloven, men regelen veier det ikke når det finnes en materiell premiss.

## Tverrgående mønstre

- **Delegeringsvedtak (DEL) er der regelen treffer.** 10 av 12 DEL-kort er riktige. Overgangsregelen nevner forskrifter, eller forskrifter og
  enkeltvedtak. Om en delegering etter en opphevet lov følger med, er et reelt tolkningsspørsmål. Uavklart er det riktige svaret fra en regel som ikke
  fortolker. I flere av dem har den nye loven lagt myndigheten et annet sted, så virkningen er trolig liten [slutning]. Det må juristen avgjøre i
  vurderingspunktet.
- **SF og INS er der regelen bommer.** Tellingen i kopien (alle uavklart-funn som står som oppdaget, også de 11 eldre) viser at 9 av 11 INS og 1 av 7 SF
  har en overgangsregel som nevner enkeltvedtak. Sju av INS-funnene er vedtak etter universitets- og høyskoleloven om status, navn, godkjenning
  og fusjon (2007 nr. 304, 2009 nr. 1428, 2010 nr. 1470, 2011 nr. 93, 2011 nr. 649, 2011 nr. 650, 2012 nr. 125). Uhl. 2024 § 17-3 nevner både
  forskrifter og enkeltvedtak og dekker dem trolig alle. Anslått er om lag 8 av de 12 INS- og SF-funnene feil på samme måte. Det er lest av titlene,
  ikke kort for kort.
- **Forslag til retting (ikke bygget, spørsmål til prosjekteieren):**
  - Når overgangsregelen nevner både forskrifter og enkeltvedtak, skal dekningen være «dekket» for vedtak i SF og INS som ikke er delegeringer eller
    instrukser.
  - Når den bare nevner enkeltvedtak, må det leses om vedtaket er en tillatelse, godkjenning eller status til en navngitt part. Basen alene kan ikke
    avgjøre det.
  - For delegeringer er uavklart riktig, slik det er bygget.
- **Videredelegeringer** (N07, N10): korpuset har vedtak der departementet videredelegerer til Sjøfartsdirektoratet med henvisning til det kortet gjelder
  (1984 nr. 4278 og 2002 nr. 967). De bør få samme svar. Ikke kontrollert om de er blant de 163.
- **#446 og uttømt virkning** (N02 kap. II, N03, N06, N08): flere vedtak er delvis ikrafttredelse eller oppheving. Premissmangelen #446 står allerede
  på funnene. Det gir ingen klassefeil.

## Prinsipielle innvendinger

Ingen. Alternativ (a) gir riktig svar for delegeringer. Feilene kommer av at dekningen leses ut fra basen, av at en endringslov følges, og av at
korpuset ikke har opphevingen av et LTI-vedtak. Ingen av dem gjelder selve regelen om at en videreføring med «dokumenttype ikke nevnt» ikke bærer alene.

## Ikke kontrollert

- Hva sparebankloven § 60 og forretningsbankloven § 44 (1961) gjaldt (N06).
- Om datalagringsforskriften ble fastsatt, og når ekomloven § 2-7 a ble opphevet (N12).
- De sju uhl-vedtakene i INS, kort for kort.
- Forarbeider og rettspraksis.
