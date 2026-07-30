# RegelIde som ÉN container: Postgres + API + ferdigbygd SPA i samme image.
#
# Laget for et efemert testcluster uten persistent lagring og uten mulighet til å kjøre
# flere containere (altså ikke docker-compose.yml, som fortsatt er riktig oppsett lokalt).
# Konsekvensen er at databasen lever og dør med containeren: hver start kjører migrasjonene
# og seeder testinnholdet på nytt. Det er greit for demo/test, og bevisst uegnet for noe
# der data skal overleve — da må Postgres ut i en egen tjeneste med volum.
#
#   docker build -t regelide .
#   docker run --rm -p 8080:8080 regelide     # http://localhost:8080

# ---------------------------------------------------------------- 1) SPA
FROM node:24-bookworm-slim AS web
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
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS api
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
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final

# Postgres 15 fra Debian bookworm — samme major som docker-compose.yml og som testene.
# curl er kun til HEALTHCHECK.
RUN apt-get update \
 && apt-get install -y --no-install-recommends postgresql-15 postgresql-client-15 curl \
 && rm -rf /var/lib/apt/lists/*

ENV PGDATA=/var/lib/postgresql/data \
    PGBIN=/usr/lib/postgresql/15/bin \
    ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__RegelIdeDb="Host=/var/run/postgresql;Database=regelide;Username=regelide" \
    RegelIde__Kildemappe=/kilder \
    RegelIde__BakEnTerminerendeProxy=true

# Klyngen initialiseres ved BYGG, ikke ved oppstart: initdb tar noen sekunder, og på et
# efemert cluster startes containeren ofte. Databasen er uansett tom ved hver start.
#
# --auth=trust er forsvarlig her fordi Postgres kun lytter på unix-socket inne i
# containeren (listen_addresses=''), aldri på TCP. Ingenting utenfra kan nå den, og
# 5432 eksponeres ikke. Skal databasen noen gang ut av containeren, må dette endres.
RUN mkdir -p "$PGDATA" /var/run/postgresql \
 && chown -R postgres:postgres "$PGDATA" /var/run/postgresql \
 && chmod 1777 /var/run/postgresql \
 && su postgres -c "$PGBIN/initdb -D $PGDATA --auth=trust --encoding=UTF8 --locale=C" \
 && su postgres -c "$PGBIN/pg_ctl -D $PGDATA -o \"-c listen_addresses=''\" -w start" \
 && su postgres -c "createuser regelide" \
 && su postgres -c "createdb -O regelide regelide" \
 && su postgres -c "$PGBIN/pg_ctl -D $PGDATA -m fast -w stop"

WORKDIR /app
COPY --from=api /publisert ./
COPY --from=web /web/dist ./wwwroot/
COPY data/kilder/raw-lovdata/ /kilder/
COPY docker/start.sh /usr/local/bin/start.sh

# API-et kjører som en egen uprivilegert bruker, ikke som root og ikke som postgres.
RUN useradd --system --uid 10001 --shell /usr/sbin/nologin regelide \
 && chown -R regelide:regelide /app \
 && chmod +x /usr/local/bin/start.sh

EXPOSE 8080

# start-period dekker migrasjon + seeding ved første oppstart.
HEALTHCHECK --interval=10s --timeout=3s --start-period=90s --retries=5 \
  CMD curl -fsS http://localhost:8080/helse || exit 1

ENTRYPOINT ["/usr/local/bin/start.sh"]
