#!/usr/bin/env bash
set -euo pipefail
umask 077
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
mkdir -p "$root/.secrets"
if [[ ! -s "$root/.secrets/clockin-signing-key.pem" ]]; then
    openssl ecparam -name prime256v1 -genkey -noout -out "$root/.secrets/clockin-signing-key.pem"
fi
openssl ec -in "$root/.secrets/clockin-signing-key.pem" -check -noout >/dev/null 2>&1
for password_file in clockin-postgres-password clockin-logging-password; do
    if [[ ! -s "$root/.secrets/$password_file.txt" ]]; then
        openssl rand -hex 32 | tr -d '\n' > "$root/.secrets/$password_file.txt"
    fi
done
printf 'Production secrets prepared; existing files preserved. Values were not printed.\n'
