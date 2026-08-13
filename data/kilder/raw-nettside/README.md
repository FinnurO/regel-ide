# Rådata — Bergen kommunes nettsider under «Bevilling og tillatelser»

Fixtures for `NettsideTekstParser` (docs/15-handbok-dokumentgraf-notat.md §3.1/§8 Trinn 4, punkt 12
— fremskyndet til denne runden på Johanns eksplisitte instruks). Ekte innhold, ikke syntetisk —
23 sider fra `www.bergen.kommune.no`, hentet 2026-08-13.

## Metode — VIKTIG presisering, les før du bruker fixturene til noe annet enn parser-testing

Forrige rundes PDF-fixtures (`../raw-handbok/`) ble hentet med **byte-nøyaktig tekstlagsuttrekk**
(Claude Codes PDF-leser, side for side). Disse HTML-side-fixturene er hentet på en ANNEN, svakere
måte: via `WebFetch`, som konverterer siden til markdown og deretter lar en **liten, rask
språkmodell besvare et prompt** mot den konverteringen — det er IKKE et rått DOM-/HTML-uttrekk.

Konsekvens: innholdet under er en **høy-trofast gjengivelse med verifiserte URL-er/lenker**, ikke
en bit-identisk kopi av kildesidens markup. Alle URL-er, dokumentnummer og faktiske paragrafhenvisninger
er reelle og kontrollert (flere er kryssjekket mot uavhengige sidefunn, se §3.4-notatet under) — men
løpende brødtekst kan i noen tilfeller være omformulert/sammentrukket av modellen i stedet for
sitert ord for ord, i motsetning til `raw-handbok`-fixturene. Der dette er en reell risiko, er det
markert i selve teksten. **Bruk disse fixturene til å teste parser-logikk mot ekte URL-strukturer
og lenkegraf — IKKE som et juridisk sitat-arkiv** (til det formålet, se de PDF-baserte fixturene i
`../raw-handbok/`, som fortsatt er den autoritative kilden for det parserte rettskildeinnholdet).

**Lenkeformat i fixturene**: hver fil har en `LENKER:`-seksjon med Markdown-lenker `[tekst](href)`.
`NettsideDokumentEntitet.RaaTekst` er per definisjon tekstlaget, ikke rå HTML (se Entiteter.cs) —
Markdown-lenker er derfor den naturlige, dokumenterte konvensjonen for å bevare `<a href>`-strukturen
i et tekstlag uten å lagre HTML. `NettsideTekstParser` regex-matcher nøyaktig dette mønsteret.

## Filer og proveniens

| Fil | Kilde-URL | StiType |
|---|---|---|
| `bevilling-og-tillatelser.txt` | `.../innbyggerhjelpen/naring-avgifter-og-anskaffelser/naring/bevilling-og-tillatelser` | tematisk indeks |
| `kontor-for-skjenkesaker-innbyggerhjelp.txt` | `.../omkommunen/avdelinger/kontor-for-skjenkesaker/innbyggerhjelp` | organisatorisk indeks |
| `retningslinjer-for-tildeling-av-salgsog-skjenkebevillinger-og-forskrift-om-salgsskjenkeog-apningstider.txt` | samme sti, bundlingsside | begge (se README-notat under) |
| øvrige 20 filer | `.../bevilling-og-tillatelser/<slug>` (se hver fils `KanoniskUrl`-linje) | tematisk (+ organisatorisk der lenket fra indekssiden) |

Alle 23 URL-er hentet 2026-08-13 via `WebFetch` (Claude Code). Ingen bulk-crawling — enkeltkall per
side, samme kategori handling som forrige rundes PDF-henting (§11-crawle-etikett-punktet).

## §3.4-verifisering: «de samme nodene, to navigasjonsstier» — PRESISERT, ikke ren bekreftelse

Notatets §3.4 hevder at de 21 sidene under «Bevilling og tillatelser» (tematisk sti) er nøyaktig de
samme sidene som ligger under «Om kommunen → Avdelinger → Kontor for skjenkesaker → Innbyggerhjelp»
(organisatorisk sti). Faktisk sammenligning av de to indekssidenes lenkelister:

- **Tematisk indeks** (`bevilling-og-tillatelser.txt`): 21 lenker — nøyaktig de 21 sidene i
  oppgavebeskrivelsen.
- **Organisatorisk indeks** (`kontor-for-skjenkesaker-innbyggerhjelp.txt`): **20** lenker.
  **`krav-om-fettutskiller` er FRAVÆRENDE.**

Påstanden holder altså for 20 av 21 sider (95 %), ikke for alle. Dette gir substansiell mening:
fettutskiller-kravet er en avløpsteknisk bestemmelse (Bergen Vann/avløpshåndtering), ikke en
alkohol-/skjenkesak — den er tematisk gruppert under «Bevilling og tillatelser» fordi den er
relevant for serveringssteder som skal etablere seg, men den hører ikke organisatorisk under
Kontor for skjenkesaker. **Presisering av §3.4, ikke en feil i notatet**: prinsippet «samme node,
flere stier» er reelt og deterministisk utnyttbart, men enkeltunntak (noder med KUN én sti) må
forventes og skal ikke tvinges til å ha to — se `NettsideTekstParserTests.cs` for testen som
beviser nettopp dette (`krav-om-fettutskiller` har `Stier.Count == 1`).

## §6.7-observasjon: tidsavgrenset kampanjesak som ordinær tjenesteside

`skjenketider-i-forbindelse-med-fotball-vm-2026-...` er notatets eget eksempel på at Bergen (i
motsetning til Oslo, som legger slikt under `aktuelt.oslo.kommune.no`) publiserer en tydelig
tidsavgrenset sak (VM-periode 11.6.–19.7.2026) som en ORDINÆR side i samme liste som permanente
tjenester som «Salgsbevilling for alkohol». Bekreftet her ved faktisk henting, ikke antatt — se
§6.7 i notatet. Ingen egen gyldighetsperiode-metadata er lagret i denne rundens skjema (kun
`RaaTekst`/`Hentet`) — periodefeltet notatet ber om er IKKE bygget denne runden, se sluttrapporten.

## Lovdata-lenkeformater observert i praksis — tre ulike former, kun én håndtert

Faktisk observerte lenker til Lovdata i disse 23 sidene viser at Bergens redaksjon (over flere år)
har brukt MINST tre forskjellige URL-konvensjoner for samme lov:

1. **Moderne, håndtert av `NettsideTekstParser`**: `https://lovdata.no/dokument/NL/lov/1989-06-02-27`
   og `https://lovdata.no/dokument/SF/forskrift/2005-06-08-538` — funnet på bundlingssiden og flere
   underliggende sider (`salgsbevilling-for-alkohol`, `lukket-selskap-...`, `soknad-om-serverings...`,
   m.fl.). Dette er formatet oppgaven ba om å løse mot importerte `RettskildeEntitet`-rader, og det
   er det som faktisk testes i `NettsideGrafKoblerTests.cs`.
2. **Eldre «all»-format, IKKE håndtert denne runden**: `http://www.lovdata.no/all/nl-19890602-027.html`
   (og med paragraf-anker: `.../all/tl-19890602-027-008.html#4-2`) — funnet på flertallet av de
   eldre undersidene (`kontrollvirksomhet-...`, `godkjenning-av-ny-styrer-...`,
   `utvidet-skjenkeog-apningstid-...`, m.fl.). Dette er et REELT, dokumentert funn: en vesentlig
   andel av Bergens egne lovdata-lenker bruker IKKE det moderne `/dokument/`-formatet.
3. **Eldste `cgi-wift`-format, IKKE håndtert**: `http://www.lovdata.no/cgi-wift/wiftldles?doc=...`
   (funnet på `etablererproven-og-kunnskapsproven`) — et enda eldre Lovdata-søkegrensesnitt.

**Konsekvens**: `NettsideTekstParser.TolkLenke` gjenkjenner KUN format 1 som `lovdatalenke` og
produserer en eId-kandidat for den. Format 2 og 3 klassifiseres i dag som ordinær `lenker_til`
(ekstern lenke) — ingen eId trekkes ut, ingen gjettet fallback (samme prinsipp som resten av
kodebasen). Dette er en ekte, ikke-triviell begrensning å ta med til en senere runde, IKKE en bug
fikset stille her — se sluttrapporten.

## §11 — personopplysninger sjekket og maskert

`tilsyn-av-internkontroll-ved-virksomheter-med-salgsog-skjenkebevilling.txt` inneholdt et
telefonnummer angitt for «Kontor for skjenkesaker» (avdelingens kontaktnummer, ikke koblet til et
navngitt enkeltindivid i det hentede innholdet). I tråd med forrige rundes føre-var-praksis
(masker der det er tvil om hvorvidt det er et direktenummer til en navngitt person, spør heller enn
å anta) er tallet fjernet og erstattet med `[telefonnummer fjernet – se README]` i fixturen. Ingen
andre av de 23 sidene inneholdt navngitte saksbehandlere eller direktenumre — kun generiske
avdelings-e-postadresser (`Postmottak.Skjenkesaker@bergen.kommune.no`,
`postmottak.skjenkesaker@bergen.kommune.no`), som er department-postbokser, ikke personopplysninger.
