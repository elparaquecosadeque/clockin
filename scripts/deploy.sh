#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${CLOCKIN_TAG:?set CLOCKIN_TAG}"
: "${CLOCKIN_DB_PASSWORD:?set CLOCKIN_DB_PASSWORD}"
: "${CLOCKIN_LOGGING_PASSWORD:?set CLOCKIN_LOGGING_PASSWORD}"
: "${CLOCKIN_SIGNING_KEY:?set CLOCKIN_SIGNING_KEY}"
: "${CLOCKIN_FIRST_ADMIN_EMAIL:?set CLOCKIN_FIRST_ADMIN_EMAIL}"
[[ "$CLOCKIN_TAG" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$ ]] || { echo 'Invalid release tag' >&2; exit 1; }
[[ "$CLOCKIN_DB_PASSWORD" =~ ^[a-f0-9]{64}$ ]] || { echo 'Expected generated 64-character hex database password' >&2; exit 1; }
[[ "$CLOCKIN_LOGGING_PASSWORD" =~ ^[a-f0-9]{64}$ ]] || { echo 'Expected generated 64-character hex logging password' >&2; exit 1; }
export CLOCKIN_LOGGING_CONNECTION_STRING="Host=shared-postgres;Port=5432;Database=telemetry;Username=clockin_log_writer;Password=$CLOCKIN_LOGGING_PASSWORD;Maximum Pool Size=2;Timeout=2;Command Timeout=3"
export CLOCKIN_CONNECTION_STRING="Host=shared-postgres;Port=5432;Database=clockin_demo;Username=clockin_demo;Password=$CLOCKIN_DB_PASSWORD"
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
state=${CLOCKIN_STATE_DIR:-/var/jenkins_home/deploy-state/clockin}
release="$state/$CLOCKIN_TAG"
mkdir -p "$state/backups"

if [[ "${CLOCKIN_ROLLBACK:-false}" == true ]]; then
    [[ "${ACK_SCHEMA_COMPATIBLE:-false}" == true ]] || { echo 'Rollback requires ACK_SCHEMA_COMPATIBLE=true; migrations are not reversed.' >&2; exit 1; }
    [[ -f "$release/docker-compose.yml" ]] || { echo 'Release not found' >&2; exit 1; }
else
    [[ ! -e "$release" ]] || { echo 'Release already exists; use rollback mode' >&2; exit 1; }
    docker compose -p clockin -f "$root/docker-compose.yml" build api web
    mkdir -p "$release/scripts"
    cp "$root/docker-compose.yml" "$release/docker-compose.yml"
    cp "$root/scripts/deploy.sh" "$release/scripts/deploy.sh"
    ready=false
    for attempt in {1..30}; do
        if docker exec shared-postgres pg_isready -U postgres >/dev/null 2>&1; then ready=true; break; fi
        sleep 2
    done
    [[ "$ready" == true ]] || { echo 'Shared PostgreSQL is not ready' >&2; exit 1; }
    DB_PASSWORD="$CLOCKIN_DB_PASSWORD" docker exec -e DB_PASSWORD jenkins ensure-db clockin_demo clockin_demo
    docker exec shared-postgres pg_dump -U postgres -Fc clockin_demo > "$state/backups/$CLOCKIN_TAG.dump"
    docker compose -p clockin -f "$release/docker-compose.yml" run --rm --no-deps migrate
fi

docker compose -p clockin -f "$release/docker-compose.yml" up -d --no-build --wait --wait-timeout 120 api web
docker run --rm --network host curlimages/curl:8.11.1 -fsS "http://127.0.0.1:${CLOCKIN_PORT:-8082}/health/ready"
for locale in en es; do
    docker run --rm --network host curlimages/curl:8.11.1 -fsS -o /dev/null "http://127.0.0.1:${CLOCKIN_PORT:-8082}/$locale/sign-in"
done
docker run --rm --network host curlimages/curl:8.11.1 -fsS -o /dev/null "http://127.0.0.1:${CLOCKIN_PORT:-8082}/api/demo/accounts"
printf '%s\n' "$CLOCKIN_TAG" > "$state/last-good"
printf 'Release %s passed smoke checks. Database migrations were not reversed.\n' "$CLOCKIN_TAG"
