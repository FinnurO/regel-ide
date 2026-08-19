# Rådata — Johanns eksterne skript for kommune.no-skjemakataloger

Dette er **verktøyet som produserer** kildematerialet for `kommune_tjeneste`-kilden i
høstelaget (`src/RegelIde.Data/KommuneTjenesteHenter.cs`, docs/13-backlog.md) — ikke
kildematerialet selv. Lagt inn her for kildekontroll og reproduserbarhet, på Johanns
eksplisitte instruks (2026-08-19).

## Proveniens

| Felt | Verdi |
|---|---|
| Kilde | Johanns eget skript, kjørt fra hans egen maskin — **ikke** kjørt av regel-IDE |
| Mottatt | 2026-08-19 |
| Kjørt fra | Python 3.14 CLI, se docstring i `kommuner_hovedscript.py` |
| Avhengigheter | `requests`, `beautifulsoup4`; for de vanskeligste kommunenes sider (JS-rendrede kataloger) også `curl_cffi` og `playwright` (Chromium) — se `kommuner_unntak.py`s `fetch_with_playwright`/`request_catalog` |
| Kjøreorden | `kommuner_hovedscript.py` kjøres først (produserer et førsteutkast av `treff.json`/`resultat.csv`/`ingen_treff.csv` fra generisk katalog-/API-gjenkjenning per kommune); `kommuner_unntak.py` kjøres etterpå og fletter inn manuelt kartlagte unntakskilder (se `SOURCES`-dictet der) for kommuner den generiske gjenkjenningen ikke fant selv |
| Input | `Kommuner.csv` (357 rader, `OrganizationId;Name`) |
| Output | `treff.json` (importert som `kommune_tjeneste` i høstelaget), `resultat.csv`, `ingen_treff.csv` — ingen av disse er lagt inn her, kun selve skriptene og input-fila |

## Hvorfor dette IKKE er portet til C#

I motsetning til Altinn skjemaoversikt-skraperen (`AltinnSkjemaoversiktHenter.cs`, som skraper ÉN
konsistent sidestruktur) skraper dette skriptet ~356 individuelle kommune.no-nettsteder på fem ulike
underliggende måter (`SKJEMA_NO_API`/`ACOS_API`/`HTML_INNEBYGD_JSON`/`HTML`/`UNNTAK_HTML_KATALOG`),
med Chromium-rendering (Playwright) som fallback for JS-rendrede sider, `curl_cffi`s
nettleser-impersonering som fallback mot WAF-blokkering, og hundretalls linjer med
per-CMS-heuristikk for lenkeklassifisering. Dette er langt mer heterogent enn det som er rimelig å
gjenskape i .NET — `kommune_tjeneste`-kilden forblir derfor i samme kategori som
Statsforvalter-/fylkeskommune-kildene: Johanns eget periodiske eksterne uttrekk, importert som fil
via `POST /api/eksterne-kilder/kommune-tjenester/importer`.

## Kjente svakheter i uttrekket (2026-08-19-runden), verifisert mot faktisk kode og data

- **`beskrivelse` er alltid tom streng** i alle ~15 300 rader — verken skriptet bygger inn noen
  faktisk beskrivelsestekst, kun tittel/kategori/url.
- **~29 kommuner er bevisst utelatt**, ikke manglende pga. en feil: `STOPP_UTEN_SKJEMAKILDE`
  (navnebasert) og `STOPP_UTEN_SKJEMAKILDE_ORGNR` (org.nr-basert, tryggere) i
  `kommuner_unntak.py` markerer kommuner der ingen skjemakilde ble funnet, og skjemasøket
  eksplisitt avsluttet på Johanns instruks — disse kommunene har 0 rader i `treff.json` med vilje,
  ikke som en bug å rette.
- **To reelle organisasjonsnavn-kollisjoner i `Kommuner.csv`**: "HERØY KOMMUNE" (orgnr `872417982` i
  Nordland, `964978840` i Møre og Romsdal) og "VÅLER KOMMUNE" (orgnr `871034222`, `959272581`) opptrer
  hver som TO rader med samme navn men forskjellig organisasjonsnummer. `KommuneTjenesteHenter`s
  sammensatte identitetsnøkkel `(organisasjonsnummer, url)` (ikke `url` alene) er bygget nettopp for
  å håndtere dette — se den klassens kommentar punkt (a)/(b) for den ekte Herøy-kollisjonen dette
  løser i praksis (139 delte URL-er i produksjonsdataene).
- **Navnebaserte dict-oppslag i `kommuner_unntak.py` er fragile mot nettopp disse kollisjonene** —
  `load_orgs()` bygger en navn→org.nr-dict fra `Kommuner.csv`; har to kommuner samme navn (Herøy,
  Våler), vinner raden som kommer sist i CSV-en i den dicten. `STOPP_UTEN_SKJEMAKILDE` og `SOURCES`
  er navnebaserte og dermed utsatt for dette — `STOPP_UTEN_SKJEMAKILDE_ORGNR` er org.nr-basert og
  trygg. Ingen praktisk konsekvens observert i denne runden (begge Våler-radene endte uten tjenester
  uansett), men verdt å vite ved fremtidige kjøringer eller utvidelser av skriptet — se selve
  chat-loggen 2026-08-19 for detaljene, ikke gjentatt her.

## Regel-IDE-siden

Se `src/RegelIde.Data/KommuneTjenesteHenter.cs` for importlogikken som konsumerer `treff.json`-formatet
disse skriptene produserer, og `docs/13-backlog.md` for byggerunden som la til denne kilden.
