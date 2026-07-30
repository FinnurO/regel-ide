# Enkeltcontainer-image

Bygger API og ferdigbygd SPA til **ett** image med **SQLite** som database, for et app-cluster
som verken gir root, persistent volum eller mulighet til å kjøre en databasetjeneste ved siden av.

```bash
docker build -t regelide .
docker run --rm -p 8080:8080 regelide
# http://localhost:8080
```

Lokalt til utvikling er [`docker-compose.yml`](../docker-compose.yml) + `dotnet run` fortsatt
riktig oppsett, og det kjører **Postgres** — som også er målbildet i drift. Dette imaget er for
deploy, ikke for utviklingsløkka.

## Databaseprofilen

Motoren velges med `RegelIde:Database` (`postgres` | `sqlite`), se
[`Databaseoppsett.cs`](../src/RegelIde.Data/Databaseoppsett.cs). Postgres er standard overalt;
SQLite settes kun i dette imaget. En ukjent verdi feiler ved oppstart i stedet for å falle
tilbake til noe.

Det som faktisk skiller de to i modellen:

| | Postgres | SQLite |
|---|---|---|
| JSON-kolonner | `jsonb` (validerer innholdet) | `TEXT` (validerer ingenting) |
| `text[]` | native array | JSON-kolonne via EF |
| `opprettet_tidspunkt` | `DEFAULT now()` | ingen default — appen setter verdien |
| `DateTimeOffset` | `timestamptz` | `long` med UTC-ticks, se under |
| Skjema | migrasjonene i `Migrasjoner/` | `EnsureCreated` fra modellen |

### DateTimeOffset må konverteres

SQLite har ingen dato-type, og EF Core **kaster** `NotSupportedException` på `ORDER BY` og
sammenligning av `DateTimeOffset`. Det ville tatt ned proveniens-/historikk-endepunktene, som
sorterer på `dato`.

Vær oppmerksom på at EF Core sin innebygde `DateTimeOffsetToBinaryConverter` **ikke** kan brukes:
den pakker offset inn i verdien, så sorteringen følger lokal veggklokke i stedet for faktisk
tidspunkt. Den kaster ikke — den gir stille feil rekkefølge, som er verre. Profilen bruker derfor
en egen konverter på `UtcTicks`, som er monotont i faktisk tid. Verdien leses tilbake som UTC;
det er ufarlig fordi all kode setter tidsstempler med `DateTimeOffset.UtcNow`.

### Migrasjonene gjelder ikke for SQLite

Migrasjonene er generert for Npgsql og inneholder typenavn SQLite ikke kjenner (`uuid`, `jsonb`,
`text[]`, `timestamp with time zone`, `now()`). SQLite bygger derfor skjemaet rett fra modellen
med `EnsureCreated`. Et eget migrasjonssett ville kostet et nytt prosjekt og dobbelt vedlikehold
ved hver skjemaendring, uten å gi noe — SQLite-basen har aldri data å migrere.

Prisen er at SQLite-skjemaet kan drive fra migrasjonene uten at noen merker det. Det er en grunn
til at profilen ikke skal ta imot data som skal overleve.

## Hva som skjer ved oppstart

`dotnet` er eneste prosess og PID 1. Den bygger skjemaet, importerer Lovdata-kildene fra
`/kilder` og kjører seedene (`Program.cs`) — 20–30 sekunder, derfor `--start-period=90s` på
`HEALTHCHECK`. `/helse` svarer 200 først når databasen faktisk svarer, så den kan brukes som
readiness-probe. SIGTERM går rett til `dotnet` (målt til under ett sekund).

## Databasen forsvinner ved omstart

SQLite-filen ligger i containerens eget filsystem (`/data`). **All data går tapt når containeren
stopper**, og alt bygges opp igjen ved neste start. Det er et bevisst valg gitt målmiljøet, og
gjør imaget uegnet til alt der data skal overleve.

Når en ekte database blir tilgjengelig — CloudNativePG i klyngen eller en managed Postgres — er
byttet å sette `RegelIde__Database=postgres` og la `ConnectionStrings__RegelIdeDb` peke dit.
Ingen kodeendring trengs, og migrasjonene tar over skjemaet.

## Sikkerhetsvalg

- **Ingen root, noe sted.** Containeren kjører som uid 1000 hele veien, uten oppstartsskript.
  Det var ikke mulig med Postgres i containeren, som måtte starte som root for å få opp
  `pg_ctl` — og som derfor ville blitt avvist av en `runAsNonRoot`-policy.
- `/data` er `1777` framfor eid av én bestemt bruker, slik at imaget også fungerer når klyngen
  tvinger gjennom en annen uid via `securityContext`. Mappen inneholder kun en efemer
  demodatabase. SQLite trenger skrivetilgang til mappen, ikke bare filen, fordi WAL-modus
  oppretter `-wal` og `-shm` ved siden av.
- Ingen databaseport eksponeres, fordi det ikke finnes noen databaseprosess.
- ⚠️ Imaget inneholder **ingen** autentisering — API-et er fortsatt helt åpent, og identitet
  velges av klienten via `X-Bruker-Id`. Det må på plass før dette står et sted andre når.
  Se `GjeldendeBrukerTjeneste.cs`.

## Sårbarhetsskanning

Skann med Trivy før deploy:

```bash
docker build -t regelide .
docker save regelide -o regelide.tar
docker run --rm -v "$PWD:/scan" aquasec/trivy image --input /scan/regelide.tar
```

Målt 2026-07-30, samme applikasjon gjennom tre varianter:

| Variant | Kritisk | Høy | Totalt | Størrelse |
|---|---|---|---|---|
| Debian + Postgres i container | 18 | 43 | 323 | 816 MB |
| Alpine + Postgres i container | 0 | 0 | 0 | 339 MB |
| Alpine + SQLite (i bruk) | 0 | 0 | **0** | **303 MB** |

Det interessante med Debian-tallene var at **ingen** av de 323 hadde en tilgjengelig fiks — de var
merket `will_not_fix`/`fix_deferred`, så `apt-get upgrade` gjorde ingenting. Det meste kom fra
perl, som ble dratt inn som avhengighet av `postgresql-15`. Med SQLite forsvinner hele den
avhengighetskjeden.

## Miljøvariabler

| Variabel | Standard i imaget | Hva den gjør |
|---|---|---|
| `RegelIde__Database` | `sqlite` | Velger databasemotor (`postgres` \| `sqlite`). |
| `ConnectionStrings__RegelIdeDb` | `Data Source=/data/regelide.db` | Peker API-et mot databasen. |
| `RegelIde__Kildemappe` | `/kilder` | Hvor førstegangs-seedingen leser Lovdata-HTML fra. |
| `RegelIde__BakEnTerminerendeProxy` | `true` | Slår av `UseHttpsRedirection`, siden TLS termineres foran containeren. |
| `ASPNETCORE_URLS` | `http://+:8080` | Porten API-et lytter på. |

## Bygget av SPA-en

Steget kjører `npx vite build`, ikke `npm run build`. Sistnevnte kjører `tsc -b` først, og
typesjekken er rød på `master` fra før — `Textarea`/`Select` i `@digdir/designsystemet-react`
har ingen `label`-prop, men brukes med en 24 steder. Den feilen er reell og bør rettes for
seg; å la den blokkere containerbygget ville blandet sammen to ubeslektede ting.

`VITE_API_BASE_URL` settes tom, slik at klienten kaller `/api/…` relativt til seg selv.
API-et serverer SPA-en fra `wwwroot`, så det er samme origin og CORS er ikke i bildet.
