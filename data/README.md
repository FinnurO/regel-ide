# Datakontrakt — nettside/data

Alle filene her er statiske øyeblikksbilder eksportert fra en kjørende lokal instans av
`RegelIde.Api`, med [`nettside/tools/eksporter-data.ps1`](../tools/eksporter-data.ps1). Nettsiden
laster dem med `fetch()` — det finnes ingen kjøretidsavhengighet til API-et for selve visningen.

Hver fil har et `_kilde`-felt øverst som sier nøyaktig hvilket API-kall som fylte den og når den
sist ble eksportert. Ingen fil inneholder navngitte enkeltpersoners kontaktinformasjon — kun
virksomhets-, gruppe-, rettskilde- og begrepsdata (se «Personvern» nederst).

## To nivåer — ikke bland dem

### 1. Katalogfiler — ALT, ingen filtrering

| Fil | Kilde | Innhold |
|---|---|---|
| `rettskilder/katalog.json` | `GET /api/rettskilder` | Alle rettskilder: id, tittel, kildetype, ansvarlig(e) departement(er), eier-virksomhet. **Ingen fulltekst.** |
| `virksomheter/katalog.json` | `GET /api/virksomheter` | Alle virksomheter: id, navn, orgnr, forvaltningsnivå, sektorkode. |

Disse to brukes av `rettskilder/index.html` og `virksomheter/index.html` til søk/filter/telling over
*hele* katalogen — de skal aldri kuttes ned til et utvalg.

`grupper/register.json` er et tredje, lite støtteregister (alle gruppebegrep — `GET
/api/gruppebegrep`, ~11 rader) som `grupper/index.html` og `koblinger/index.html` bruker til å slå
opp navn/hjemmel for en gruppe-id uten et eget API-kall per gruppe. Det er ikke en av de to
formelt påkrevde katalogfilene, men eksportert i sin helhet fordi det er lite.

### 2. Detaljfiler — kun et STARTUTVALG

Detaljfiler eksisterer bare for de konkrete eksemplene showcasen bruker. Filnavnet er alltid
entitetens id (unntatt `begreper/oversikt.json`, som er en liten, kuratert oversikt — se under).

| Fil | Kilde | Hva |
|---|---|---|
| `virksomheter/455d90bf-3b8e-4d75-a735-b6b2768d17fa.json` | `GET /api/virksomheter/{id}/where-used` + `/myndighetstildelinger` + `/begrep` | Karasjok kommune (orgnr 963376030) — navneformer, hvor de er tagget, og gruppetildelingen. |
| `grupper/7057ff4b-bc2e-4d23-af7b-c681a082ea32.json` | `GET /api/gruppebegrep/{id}/medlemsgrupper` + `/overordnede-grupper` + `/tildelinger` | «forvaltningsområdet for samiske språk» — den overordnede gruppen (sameloven § 3-1). |
| `grupper/52dcc48d-d228-42d6-8bb1-d9397e29b734.json` | samme tre kall | «språkutviklingskommuner» (medlemsgruppe, forskriften § 1). |
| `grupper/ea37d4ce-b7a2-439b-91b0-b79920759ddf.json` | samme tre kall | «språkvitaliseringskommuner». |
| `grupper/86eefdfb-3c5f-46e4-80ec-95ea5dc51dd8.json` | samme tre kall | «språkstimuleringskommuner». |
| `begreper/oversikt.json` | `GET /api/begreper` (filtrert, se under) | Alle «ekte» SKOS-begreper (fakta-/handlingsbegrep + gruppebegrep) — virksomhets-navneformer er bevisst utelatt, de hører til virksomhetenes egne sider. |
| `begreper/0fe408ce-125b-480c-a17d-b04b44bc70cc.json` | `GET /api/begreper/{id}` + `/brukt-i-rettskilder` | «kommunens skjønnsutøvelse ved bevilling» (alkoholloven § 1-7a). |
| `begreper/c98ed4b3-fb01-4159-ae14-fe4cbd9a378d.json` | samme to kall | «skjenketid» (alkoholloven § 4-4). |
| `begreper/a7fbcd03-96c4-4216-82c6-60d602bf0543.json` | samme to kall | «styrer og stedfortreder» (alkoholloven § 1-7c). |
| `begreper/e9c77642-1c56-4152-bf60-477ea598d7b1.json` | samme to kall | «uklanderlig vandel» (alkoholloven § 1-7b). |

## Slik legger du til en ny detaljfil

1. Finn id-en til entiteten du vil legge til (fra den relevante katalogfilen, eller
   `GET /api/virksomheter`/`/api/gruppebegrep`/`/api/begreper` direkte mot en kjørende instans).
2. Åpne `nettside/tools/eksporter-data.ps1` og legg id-en til i riktig liste øverst i scriptet
   (`$VirksomhetIderForDetalj`, `$GruppeIderForDetalj` eller `$BegrepIderForDetalj`).
3. Kjør scriptet på nytt mot en kjørende lokal `RegelIde.Api`:
   ```powershell
   ./nettside/tools/eksporter-data.ps1 -BaseUrl https://localhost:7010
   ```
   Scriptet overskriver eksisterende filer (trygt å kjøre om igjen) og skriver den nye
   `<id>.json`-filen i riktig undermappe.
4. Lenk til den nye filen der det er naturlig i HTML/JS (f.eks. legg til i en liste over
   "flere eksempler" — katalogsidene trenger ingen kodeendring, de leser alltid hele katalogen).

## Personvern

Disse filene inneholder **kun** virksomhets-, gruppe-, rettskilde- og begrepsdata — ingen navngitte
enkeltpersoners kontaktinformasjon. Konkret sjekket før eksport:

- `virksomheter/*.json` inneholder organisasjonsnavn, orgnr, forvaltningsnivå og sektorkode — ingen
  saksbehandlernavn, e-post eller telefonnummer.
- `grupper/*.json` inneholder gruppebegrep-termer, hjemmelsparagrafer og virksomhets-id-er — ingen
  personnavn (feltene `opprettetAv`/`sistEndretAv`/`behandletAv` som finnes i de underliggende
  API-svarene, og som i dette dev-miljøet er testbruker-navn som «Kari Jurist», er **ikke** tatt med
  i noen av eksportfilene).
- `begreper/*.json` inneholder term, definisjon og lovreferanse — samme sjekk som over.
- `rettskilder/katalog.json` inneholder kun metadata (tittel, kildetype, departement, eier) — ingen
  fulltekst og ingen personnavn.

Et automatisk søk etter kjente testbruker-navn i alle filene under `nettside/data/` ga null treff
ved siste eksport (se sluttrapporten fra byggerunden som opprettet dette).
