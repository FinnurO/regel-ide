# Enkeltcontainer-image

Bygger hele Regel-IDE — Postgres, API og ferdigbygd SPA — til **ett** image, for et efemert
testcluster som verken har persistent lagring eller mulighet til å kjøre flere containere.

```bash
docker build -t regelide .
docker run --rm -p 8080:8080 regelide
# http://localhost:8080
```

Lokalt til utvikling er [`docker-compose.yml`](../docker-compose.yml) + `dotnet run` fortsatt
riktig oppsett. Dette imaget er for deploy, ikke for utviklingsløkka.

## Hva som skjer ved oppstart

`docker/start.sh` starter Postgres, venter til den svarer, og kjører deretter API-et i
forgrunnen. API-et kjører migrasjonene og seeder testinnholdet (`Program.cs`), som tar
20–30 sekunder — `HEALTHCHECK` har derfor `--start-period=90s`. `/helse` svarer 200 først
når databasen faktisk svarer, så den kan brukes som readiness-probe.

SIGTERM stopper API-et og deretter Postgres pent (målt til under ett sekund), slik at
klyngen slipper å vente ut stopp-timeouten.

## Postgres-versjon

Containeren kjører **Postgres 16** (eldste major i Alpine 3.23), mens `docker-compose.yml` og
de embedded-baserte testene kjører 15. Skjemaet bruker ingenting som skiller de to — jsonb,
GIN-fulltekstindeks, partial unique index og check-constraints er verifisert kjørende på 16 —
og databasen bygges uansett fra migrasjonene ved hver start. Verdt å vite, men ikke noe som
krever at compose følger etter.

## Databasen forsvinner ved omstart

Postgres-klyngen ligger i imaget, ikke i et volum. **All data går tapt når containeren
stopper**, og alt seedes på nytt ved neste start. Det er et bevisst valg gitt målmiljøet, og
gjør imaget uegnet til alt der data skal overleve. Skal det endres, må Postgres ut i en egen
tjeneste med volum og `ConnectionStrings__RegelIdeDb` peke dit — API-et trenger ingen
kodeendring for det.

## Sikkerhetsvalg

- Postgres lytter **kun på unix-socket** (`listen_addresses=''`), aldri på TCP. Bare API-et i
  samme container snakker med den, og 5432 eksponeres ikke. Derfor er `--auth=trust`
  forsvarlig her. Flyttes databasen ut, må begge deler endres.
- API-et kjører som den uprivilegerte brukeren `regelide` (uid 10001), ikke som root og ikke
  som postgres. Kun `start.sh` starter som root, for å kunne starte Postgres.
- Imaget inneholder **ingen** autentisering — API-et er fortsatt helt åpent, og identitet
  velges av klienten via `X-Bruker-Id`. Det må på plass før dette står et sted andre når.
  Se `GjeldendeBrukerTjeneste.cs`.

## Sårbarhetsskanning

Skann med Trivy før deploy:

```bash
docker build -t regelide .
docker save regelide -o regelide.tar
docker run --rm -v "$PWD:/scan" aquasec/trivy image --input /scan/regelide.tar
```

Målt 2026-07-30 på nøyaktig samme oppsett, kun basen byttet:

| Base | Kritisk | Høy | Totalt | Med tilgjengelig fiks | Størrelse |
|---|---|---|---|---|---|
| `aspnet:8.0-bookworm-slim` | 18 | 43 | 323 | 1 | 816 MB |
| `aspnet:8.0-alpine` (i bruk) | 0 | 0 | 1 | 1 | 339 MB |

Poenget er ikke bare tallet: **ingen** av de 323 på Debian hadde en tilgjengelig fiks. De var
merket `will_not_fix` eller `fix_deferred` i Debians sporing, så `apt-get upgrade` ville ikke
gjort noe som helst. Det meste kom fra perl (dras inn som avhengighet av `postgresql-15`) og
curl. Alpine trenger ingen av dem — postgres har ingen perl-avhengighet der, og busybox' wget
dekker helsesjekken.

Det ene gjenværende funnet er `CVE-2026-54570` (mXSS i AngleSharp 0.17.1, moderat), som kommer
fra HtmlSanitizer og fikses av pakkeoppdaterings-PR-en.

## Miljøvariabler

| Variabel | Standard i imaget | Hva den gjør |
|---|---|---|
| `ConnectionStrings__RegelIdeDb` | unix-socket, db `regelide` | Peker API-et mot databasen. |
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
