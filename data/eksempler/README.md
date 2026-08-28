# Eksempel-modelleksporter — hånd-modellert, ikke Testkommunens gjeldende innhold

Filene her er ferdig hånd-modellerte tjenestereiser i modelleksport-JSON-formatet (samme form som
`GET /api/tjenester/modelleksport` produserer / `TjenesteModellSkjema` beskriver, se
`docs/23-tjeneste-modell-eksport-og-skjema.md`) — laget for å teste og videreutvikle import-wizarden
(`Importer rettighetsmodell`, `src/RegelIde.Web/src/pages/ImportWizard.tsx`) mot en rik, realistisk
batch, ikke bare syntetiske 2–3-rads eksempler. De er **ikke** importert som Testkommunens gjeldende
innhold ennå — se filtabellen for status per fil.

Samme prinsipp som `ServeringsbevillingModellSeed.cs` (som opprinnelig kom fra en tilsvarende
hånd-modellert JSON-øvelse, `serveringsbevilling-modell-forslag.json` — den filen ble aldri selv
committet til repoet, kun overført til kode) — forskjellen her er at selve JSON-filen bevisst
BEHOLDES i repoet, slik at den kan brukes om igjen som testcase for import-wizarden (bulk-import,
avhengighetsoppløsning, virksomhet-gjetting) fremover, i stedet for å gå tapt etter én manuell
gjennomkjøring.

## Filer og status

| Fil | Innhold | Status |
|---|---|---|
| `gifte-seg-reise.modelleksport.json` | 14 rettigheter langs livshendelsen "Gifte seg" — Prøvingsattest, vigsel (kommunal/tros-livssyn/utenriksstasjon), navneendring, ektepakt, familieinnvandring, skattekort, m.fl. 13 batch-interne avhengigheter (11 tjeneste↔tjeneste, 2 `ekstern_referanse` uten organisasjonsnummer — se docs/13-backlog.md for den kjente begrensningen dette avdekket i import-wizarden). | **Testcase kun** — bevisst IKKE importert som gjeldende innhold ennå (2026-08-28-runden). Brukt til å live-verifisere bulk-import + avhengighetsoppløsning på ekte data; opprettede rader ble deretter ryddet bort igjen. Kan bli en ekte seed (`GifteSegReiseSeed.cs`, samme mønster som `ServeringsbevillingModellSeed.cs`) i en senere runde dersom innholdet besluttes tatt inn som gjeldende. |

## Kjente funn fra denne filen (relevant hvis du bruker den som testcase igjen)

- **Virksomhet-gjetting kan false-positive på substreng-treff**: `kompetent_myndighet: "Brønnøysundregistrene (Ektepaktregisteret)"` ble gjettet til virksomheten *"NTL Brønnøysundregistrene"* (en fagforeningsavdeling, ikke selve etaten) — se docs/13-backlog.md for status på dette.
- **`ekstern_referanse`-avhengigheter uten organisasjonsnummer** kan i dag ikke opprettes av import-wizarden (backend krever både orgnr og navn, uten gjettet fallback) — filen har to slike (`Vigsel gjennomført av utenlandsk vigselsmyndighet`, `Eksisterende registrert partnerskap`), begge konseptuelle motparter uten et ekte norsk organisasjonsnummer.
