#!/bin/sh
# Oppstart for enkeltcontainer-imaget (se ../Dockerfile).
#
# Containeren har to prosesser: Postgres og API-et. Det er ett unntak fra "én prosess per
# container", gjort fordi målklyngen er efemer og ikke kan kjøre flere containere. Derfor
# ingen supervisor — bare et lite skall som starter databasen, kjører API-et i forgrunnen
# og sørger for at SIGTERM stopper begge pent slik at klyngen ikke må vente ut timeouten.
set -eu

PGBIN="${PGBIN:-/usr/lib/postgresql/15/bin}"
PGDATA="${PGDATA:-/var/lib/postgresql/data}"

logg() { echo "[start] $*"; }

stopp_postgres() {
    su postgres -c "$PGBIN/pg_ctl -D $PGDATA -m fast -w stop" >/dev/null 2>&1 || true
}

avslutt() {
    logg "fikk stoppsignal"
    if [ -n "${API_PID:-}" ]; then
        kill -TERM "$API_PID" 2>/dev/null || true
        wait "$API_PID" 2>/dev/null || true
    fi
    stopp_postgres
    logg "stoppet"
    exit 0
}
trap avslutt TERM INT

logg "starter Postgres (kun unix-socket)"
su postgres -c "$PGBIN/pg_ctl -D $PGDATA -o \"-c listen_addresses=''\" -w start"

# pg_ctl -w venter til serveren tar imot tilkoblinger, men vi sjekker eksplisitt slik at
# feilen blir tydelig i loggen hvis noe er galt, i stedet for en kryptisk Npgsql-eksepsjon.
if ! su postgres -c "$PGBIN/pg_isready -q"; then
    logg "FEIL: Postgres svarer ikke"
    exit 1
fi

logg "starter API på ${ASPNETCORE_URLS:-http://+:8080}"
# Migrasjon og seeding skjer i API-ets egen oppstart (Program.cs). Databasen er tom ved
# hver containerstart, så det skjer hver gang — se kommentaren i Dockerfile.
setpriv --reuid=regelide --regid=regelide --clear-groups \
    dotnet /app/RegelIde.Api.dll &
API_PID=$!

wait "$API_PID"
API_STATUS=$?
logg "API avsluttet med status $API_STATUS"
stopp_postgres
exit "$API_STATUS"
