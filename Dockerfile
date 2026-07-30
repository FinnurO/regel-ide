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
#
# ALPINE, IKKE DEBIAN: en Debian-basert variant av nøyaktig samme oppsett ga 323 kjente
# sårbarheter (18 kritiske, 43 høye) i Trivy, mot 1 her — og ingen av de 323 hadde en
# tilgjengelig fiks, siden Debian har merket dem will_not_fix/fix_deferred. Det meste kom
# fra perl (dras inn av postgresql) og curl. På Alpine slipper vi begge: postgres trenger
# ikke perl, og busybox' wget dekker helsesjekken. Imaget går samtidig fra 816 MB til
# 339 MB. Samme base som Altinn-app-malen bruker.

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
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS api
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
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final

# postgresql16 er den eldste majorversjonen i Alpine 3.23 — docker-compose.yml og de
# embedded-baserte testene kjører 15. Skjemaet bruker ingenting som skiller de to
# (jsonb, GIN-fulltekst, partial unique index og check-constraints er verifisert kjørende
# her), og databasen bygges uansett fra migrasjonene ved hver start. Verdt å vite, men
# ikke noe som krever at compose følger etter.
#
# su-exec dekker det setpriv gjør på Debian. icu kreves for at .NET skal ha ekte
# kulturdata — uten den faller den tilbake til invariant kultur, som gir feil sortering
# og formatering av norsk tekst.
RUN apk add --no-cache postgresql16 postgresql16-client su-exec icu-libs icu-data-full tzdata

ENV PGDATA=/var/lib/postgresql/data \
    PGBIN=/usr/bin \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
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
 && su-exec postgres initdb -D "$PGDATA" --auth=trust --encoding=UTF8 --locale=C \
 && su-exec postgres pg_ctl -D "$PGDATA" -o "-c listen_addresses=''" -w start \
 && su-exec postgres createuser regelide \
 && su-exec postgres createdb -O regelide regelide \
 && su-exec postgres pg_ctl -D "$PGDATA" -m fast -w stop

WORKDIR /app
COPY --from=api /publisert ./
COPY --from=web /web/dist ./wwwroot/
COPY data/kilder/raw-lovdata/ /kilder/
COPY docker/start.sh /usr/local/bin/start.sh

# API-et kjører som en egen uprivilegert bruker, ikke som root og ikke som postgres.
RUN adduser -S -u 10001 -H -s /sbin/nologin regelide \
 && chown -R regelide:nogroup /app \
 && chmod +x /usr/local/bin/start.sh

EXPOSE 8080

# wget kommer fra busybox — ingen grunn til å installere curl bare for dette.
# start-period dekker migrasjon + seeding ved første oppstart.
HEALTHCHECK --interval=10s --timeout=3s --start-period=90s --retries=5 \
  CMD wget -q -O- http://localhost:8080/helse || exit 1

ENTRYPOINT ["/usr/local/bin/start.sh"]
