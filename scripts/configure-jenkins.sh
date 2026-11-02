#!/usr/bin/env bash
set -euo pipefail
umask 077
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
jenkins_root=${1:?usage: configure-jenkins.sh /path/to/jenkins-local}
[[ -f "$jenkins_root/.env" ]] || { echo 'Jenkins .env not found' >&2; exit 1; }
[[ -s "$root/.secrets/clockin-signing-key.pem" && -s "$root/.secrets/clockin-postgres-password.txt" ]] || { echo 'Run prepare-production-secrets.sh first' >&2; exit 1; }
for patch_name in jenkins-local jenkins-permissions jenkins-logging; do
    if git -C "$jenkins_root" apply --ignore-space-change --check "$root/scripts/$patch_name.patch" 2>/dev/null; then
        git -C "$jenkins_root" apply --ignore-space-change "$root/scripts/$patch_name.patch"
    elif ! git -C "$jenkins_root" apply --ignore-space-change --reverse --check "$root/scripts/$patch_name.patch" 2>/dev/null; then
        echo 'Jenkins config differs; review patch before applying' >&2
        exit 1
    fi
done
chmod 644 "$jenkins_root/telemetry/server.js" "$jenkins_root/telemetry/schema.sql" "$jenkins_root/grafana/dashboards/clockin.json"
if ! grep -q '^CLOCKIN_LOGGING_PASSWORD=' "$jenkins_root/.env"; then
    printf '\nCLOCKIN_LOGGING_PASSWORD=%s\n' "$(cat "$root/.secrets/clockin-logging-password.txt")" >> "$jenkins_root/.env"
fi
if ! grep -q '^CLOCKIN_FIRST_ADMIN_EMAIL=' "$jenkins_root/.env" && [[ -s "$root/.secrets/clockin-first-admin-email.txt" ]]; then
    printf '\nCLOCKIN_FIRST_ADMIN_EMAIL=%s\n' "$(cat "$root/.secrets/clockin-first-admin-email.txt")" >> "$jenkins_root/.env"
fi
if ! grep -q '^CLOCKIN_POSTGRES_PASSWORD=' "$jenkins_root/.env"; then
    printf '\nCLOCKIN_POSTGRES_PASSWORD=%s\n' "$(cat "$root/.secrets/clockin-postgres-password.txt")" >> "$jenkins_root/.env"
fi
if ! grep -q '^CLOCKIN_SIGNING_KEY_BASE64=' "$jenkins_root/.env"; then
    printf 'CLOCKIN_SIGNING_KEY_BASE64=%s\n' "$(base64 -w0 "$root/.secrets/clockin-signing-key.pem")" >> "$jenkins_root/.env"
fi
if ! grep -q '^CLOCKIN_DEPLOY_ENABLED=' "$jenkins_root/.env"; then
    printf 'CLOCKIN_DEPLOY_ENABLED=false\n' >> "$jenkins_root/.env"
fi
printf 'ClockIn credentials and job configured. Secret values were not printed. Rebuild Jenkins to apply.\n'
