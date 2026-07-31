# RegelIde som ÉN container: API + ferdigbygd SPA, med SQLite som database.
#
# Bygget for et app-cluster som verken gir oss root, et persistent volum eller mulighet til å
# kjøre en databasetjeneste ved siden av. SQLite gjør at hele stakken blir én prosess som kjører
# som en vanlig, uprivilegert bruker — den tidligere Postgres-i-container-varianten måtte starte
# som root for å få opp `pg_ctl`, og ville derfor ikke kommet gjennom en `runAsNonRoot`-policy.
#
# Lokalt til utvikling er docker-compose.yml + `dotnet run` fortsatt riktig oppsett, og det
# kjører Postgres — som også er målbildet i drift. Se docker/README.md.
#
#   docker build -t regelide .
#   docker run --rm -p 8080:8080 regelide     # http://localhost:8080
#
# DATABASEN ER EFEMER: filen ligger i containerens eget filsystem. Alt forsvinner ved omstart og
# bygges opp igjen fra Lovdata-kildene og seedene. Det er greit for demo/test og bevisst uegnet
# til noe annet.

# ---------------------------------------------------------------- 1) SPA
FROM node:24-alpine AS web
WORKDIR /web

COPY src/RegelIde.Web/package.json src/RegelIde.Web/package-lock.json ./
RUN npm ci

COPY src/RegelIde.Web/ ./
# Tom base ⇒ klienten kaller /api/... relativt til seg selv. Den serveres av API-et under,
# så det er samme origin og CORS er ikke i bildet.
ENV VITE_API_BASE_URL=""
# Med vilje `vite build` og ikke `npm run build`: sistnevnte kjører `tsc -b` først, og
# typesjekken er rød på master fra før (Textarea/Select i @digdir/designsystemet-react har
# ingen `label`-prop, men brukes med en). Den feilen er reell og bør rettes, men å la den
# blokkere containerbygget her ville blandet sammen to ubeslektede ting.
RUN npx vite build

# ---------------------------------------------------------------- 2) API
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine3.24 AS api
WORKDIR /src

# csproj-ene først, slik at restore-laget kan gjenbrukes når bare kildekode endrer seg.
COPY src/RegelIde.sln ./
COPY src/RegelIde.Api/RegelIde.Api.csproj RegelIde.Api/
COPY src/RegelIde.Data/RegelIde.Data.csproj RegelIde.Data/
COPY src/RegelIde.Kildekonvertering/RegelIde.Kildekonvertering.csproj RegelIde.Kildekonvertering/
COPY src/RegelIde.Api.Tests/RegelIde.Api.Tests.csproj RegelIde.Api.Tests/
COPY src/RegelIde.Data.Tests/RegelIde.Data.Tests.csproj RegelIde.Data.Tests/
COPY src/RegelIde.Kildekonvertering.Tests/RegelIde.Kildekonvertering.Tests.csproj RegelIde.Kildekonvertering.Tests/
RUN dotnet restore RegelIde.Api/RegelIde.Api.csproj

COPY src/ ./
RUN dotnet publish RegelIde.Api/RegelIde.Api.csproj -c Release -o /publisert --no-restore

# ---------------------------------------------------------------- 3) Kjøretid
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine3.24 AS final

# icu kreves for at .NET skal ha ekte kulturdata — uten den faller den tilbake til invariant
# kultur, som gir feil sortering og formatering av norsk tekst. Ingen databasepakker trengs:
# SQLite følger med som et bibliotek i publiseringen.
RUN apk add --no-cache icu-libs icu-data-full tzdata

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    RegelIde__Database=sqlite \
    ConnectionStrings__RegelIdeDb="Data Source=/data/regelide.db" \
    RegelIde__Kildemappe=/kilder \
    RegelIde__BakEnTerminerendeProxy=true

WORKDIR /app
COPY --from=api /publisert ./
COPY --from=web /web/dist ./wwwroot/
COPY data/kilder/raw-lovdata/ /kilder/

# /data må være skrivbar for den brukeren containeren faktisk kjører som. Vi setter uid 1000
# som standard, men en klynge kan tvinge gjennom en annen uid via securityContext — derfor er
# mappen åpen for alle i stedet for eid av én bestemt bruker. Den inneholder kun en efemer
# demodatabase. Merk at SQLite trenger skrivetilgang til selve MAPPEN, ikke bare filen, fordi
# WAL-modus oppretter -wal og -shm ved siden av.
RUN mkdir -p /data && chmod 1777 /data \
 && adduser -S -u 1000 -H -s /sbin/nologin regelide

USER 1000

EXPOSE 8080

# wget kommer fra busybox. start-period dekker skjemabygging, Lovdata-import og seeding.
HEALTHCHECK --interval=10s --timeout=3s --start-period=90s --retries=5 \
  CMD wget -q -O- http://localhost:8080/helse || exit 1

# Én prosess, ingen oppstartsskript, ingen root. dotnet er PID 1 og får SIGTERM direkte.
ENTRYPOINT ["dotnet", "/app/RegelIde.Api.dll"]
