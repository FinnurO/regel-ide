# Tjeneste-modellen — eksport og JSON Schema

> **Status:** etablert 2026-08-28 (Johann: «hele modellen-JSON-en... er viktig for hele
> applikasjonen»). Dokumenterer `RettighetModellEksportTjeneste`/`TjenesteModellSkjema`
> (`RegelIde.Data`) og de tre `GET /api/tjenester/.../modelleksport*`-endepunktene. Ikke et
> UI-dokument (se [`docs/22-tjeneste-side-redesign-brief.md`](22-tjeneste-side-redesign-brief.md)
> for det) — dette er modellen/kontrakten, uavhengig av hvordan/om den vises i en skjerm.

## 1. Formål — tre bruksområder

1. **Intern verifisering.** Felt for felt bekreftelse av at det som faktisk er bygget i appen
   stemmer med det som ble avtalt i modelleringsrunden (opprinnelig `serveringsbevilling-modell-
   forslag.json` — se §5 for historikken).
2. **Ekstern deling.** En tjenestes fulle modell skal kunne deles UT AV applikasjonen — til en
   annen virksomhet, et annet system, eller et menneske som vil se hele bildet uten å klikke seg
   gjennom flere faner.
3. **Fremtidig importmål.** Eksporten er tenkt som formatet en fremtidig importer til slutt skal
   kunne lese. **Det finnes IKKE en importmotpart i dag** — se §6.

## 2. De tre endepunktene

| Endepunkt | Returnerer | Bruk |
|---|---|---|
| `GET /api/tjenester/{id}/modelleksport` | Ett bart rettighet-objekt | Én tjeneste (uendret siden 2026-08-20). |
| `GET /api/tjenester/modelleksport?ids=<guid>&ids=<guid>...` | `{ "rettigheter": [...] }` | Et eksplisitt SETT av tjenester. Ukjente/slettede ider hoppes stille over. |
| `GET /api/tjenester/modelleksport?virksomhetId=<guid>` | `{ "rettigheter": [...] }` | ALLE gjeldende tjenester for én virksomhet. |
| `GET /api/tjenester/modelleksport/schema` | JSON Schema (`application/schema+json`) | Selvbeskrivende kontrakt for de tre over — for mennesker OG KI-agenter. |

Nøyaktig én av `ids`/`virksomhetId` må oppgis til flertalls-endepunktet — verken gjettet fallback
eller stille tomt resultat hvis ingen av delene er satt (`400 Bad Request`).

**Committet kopi i docs/.** [`docs/tjeneste-modell.schema.json`](tjeneste-modell.schema.json) er en
committet snapshot av samme skjema — Johann ba om dette (2026-08-28) slik at skjemaet kan leses/
deles uten å kjøre opp API-et, i hvert fall til applikasjonen er stabil. Filen er GENERERT, ikke
hånd-redigert — `TjenesteModellSkjemaTests.Committet_docs_kopi_er_i_sync_med_det_genererte_skjemaet`
(`RegelIde.Data.Tests`) feiler hvis den drifter fra `TjenesteModellSkjema.Bygg()`. Regenerer med:

```bash
curl -s http://localhost:5187/api/tjenester/modelleksport/schema \
  | node -e "const fs=require('fs');let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>fs.writeFileSync('docs/tjeneste-modell.schema.json', JSON.stringify(JSON.parse(d),null,2)+'\n','utf8'));"
```

(API-serveren må kjøre lokalt på port 5187 — se README §"Kjøre lokalt". `node` brukes kun for å få
ekte UTF-8/pen 2-space-indentering i stedet for .NET-serialiseringens `\uXXXX`-escaping.)

Alle tre modelleksport-variantene er bevisst en HELT ANNEN eksport enn
`GET /api/tjenester/{id}/eksport` (`TjenesteEksportTjeneste`, det flate CPSV-dokumentet uten
Handlinger/Innhold) — de to skal ikke slås sammen, se klassekommentaren i
`RettighetModellEksportTjeneste.cs`.

## 3. Feltreferanse

Snake_case gjennomgående (modellfilens egen konvensjon, ikke appens interne camelCase). Full,
levende referanse ligger i selve JSON Schema-et (`$defs.Rettighet` m.fl.) — denne seksjonen er en
kort oversikt, ikke en duplisert sannhet.

- **Rot**: `navn`, `tjenesteomrade`, `los_klassifisering`, `livshendelser[]`, `type` (enum:
  myndighetsutovelse/ytelse/infrastruktur/veiledning/medvirkning), `kompetent_myndighet` (fri
  tekst — se den kjente begrensningen i §4), `status` (enum, 7 verdier), `malgruppe[]`, `formal`,
  `innhold`, `regelverksreferanser[]`, `handlinger[]`, `avhengigheter[]`.
- **`innhold`**: de faste feltene fra Innhold-fanen (`tidspunkt_og_frister`, `vedlegg[]`,
  `opplysninger_som_skal_sendes_inn[]`, `veiledning_og_utfylling[]`,
  `innsender_og_tilgang`/`innsending_og_oppfolging`/`kontakt_og_hjelp`/`hva_rettigheten_innebarer`
  — alle valgfrie underobjekter, utelatt når ikke relevant) **pluss** (nytt 2026-08-28)
  `egne_innholdselementer[]` — de frie, brukerdefinerte innholdsseksjonene
  (`TjenesteEntitet.EgneInnholdselementerJson`).
- **`regelverksreferanser[]`**: `lov`, `henvisning`, og (nytt 2026-08-28) `felt` — `null` betyr
  referansen gjelder hele tjenesten (den flate listen i Regelverk-fanen), satt betyr den er
  knyttet til ett bestemt felt. Verdien er en feltnøkkel fra den dokumenterte konvensjonen i
  `TjenesteregisterTjeneste.cs` (`TjenesteFeltnokler`-doc-kommentaren) — samme nøkler som
  frontend bruker, ingen oversettelsestabell.
- **`handlinger[]`**: uendret feltsett pluss (nytt 2026-08-28) `eies_av_denne_tjenesten` (bool) —
  lista er nå UNIONEN av handlinger denne tjenesten eier og handlinger den har koblet inn fra en
  annen tjeneste (delt mange-til-mange, `HandlingTjenesteEntitet`), ikke bare de den eier.
- **`avhengigheter[]`**: uendret — `rel` (enum, 8 verdier), `retning` (`fra`/`til`, beregnet — se
  §4), `mal_type` (`tjeneste`/`ekstern_referanse`, beregnet), `mal_navn`, valgfritt `mal_id`/
  `organisasjonsnummer`/`kildeurl`/`merknad`.

## 4. Kjente, bevisste begrensninger

- **`retning` og `mal_type` har ingen egen kodeliste-array** i domenemodellen — de beregnes fra
  hvilken side av lagringsraden rettigheten står på, hhv. om motparten har en ekte Tjeneste-rad.
  JSON Schema-et lister dem som literal-enum med forklaring i sin `description`, ikke som en
  referanse til en `GyldigeX`-array (det finnes ingen å referere).
- **`kompetent_myndighet` er fortsatt fri tekst** — IKKE utledet fra rollebegrep/
  `Myndighetstildeling`. Kjent, uløst gap (samme rollenavn kan i praksis være ulike virksomheter i
  ulike deler av samme rettskilde) — se [`docs/13-backlog.md`](13-backlog.md) §8.

## 5. Historikk — `serveringsbevilling-modell-forslag.json`

Filen `RettighetModellEksportTjeneste` opprinnelig ble bygget for å speile eksakt, ble ALDRI
committet noe sted (ren modellutforskning, se `docs/13-backlog.md` §7) — den finnes ikke i dette
repoet eller git-historikken. Navnet lever videre kun som en konvensjon: rotnøkkelen
`rettigheter[]` for flere rettigheter i én fil (bekreftet ved `rettigheter[0]`/`rettigheter[1]`-
indeksering i kodekommentarer, bl.a. `ServeringsbevillingModellSeed.cs`) er gjenbrukt direkte for
flertalls-eksporten i denne runden, IKKE en ny, oppdiktet nøkkel. Fremtidige lesere bør ikke lete
etter selve filen — den er borte, kun navnekonvensjonen består.

## 6. Import — fortsatt ikke bygget

`RettighetModellEksportTjeneste` har KUN eksport-metoder (`EksporterAsync`,
`EksporterFlereAsync`). Det finnes ingen kode noe sted som leser dette formatet tilbake inn i
applikasjonen. Skulle en importer bygges senere, må den bl.a. løse:

- **Navn → FK-oversettelse.** Eksporten bruker `lov`/`mal_navn` (menneskelesbare navn), mens
  de virkelige skrive-API-ene bruker GUID-er (`tilRettskildeId`, `tilEid`, `mal_id`) — en importer
  må slå opp/matche navn mot eksisterende rader, ikke anta de finnes ordrett.
- **`eies_av_denne_tjenesten`/koblede handlinger** — en importer må avgjøre om en handling som
  allerede finnes andre steder (samme navn?) skal kobles inn på nytt eller opprettes på nytt.
- **`felt`-verdier** som ikke lenger matcher en gyldig feltnøkkel (f.eks. hvis et
  `egneInnholdselementer.{id}`-mål er fjernet i mellomtiden).

Se [`docs/13-backlog.md`](13-backlog.md) for status på dette som et åpent backlog-punkt.
