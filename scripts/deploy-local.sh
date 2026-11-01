#!/usr/bin/env bash
set -euo pipefail
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
export CLOCKIN_DB_PASSWORD="$(cat "$root/.secrets/clockin-postgres-password.txt")"
export CLOCKIN_SIGNING_KEY="$(cat "$root/.secrets/clockin-signing-key.pem")"
export CLOCKIN_FIRST_ADMIN_EMAIL="$(cat "$root/.secrets/clockin-first-admin-email.txt")"
export CLOCKIN_STATE_DIR="${CLOCKIN_STATE_DIR:-$root/.deploy-state}"
if [[ "${CLOCKIN_ROLLBACK:-false}" == true ]]; then
    [[ "${CLOCKIN_TAG:-}" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$ ]] || { echo 'Invalid release tag' >&2; exit 1; }
    bash "$CLOCKIN_STATE_DIR/$CLOCKIN_TAG/scripts/deploy.sh"
else
    bash "$root/scripts/deploy.sh"
fi
