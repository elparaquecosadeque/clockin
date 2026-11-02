# Production deployment operations

## Current public demo

The public site now runs in the existing `Demo` environment with `Demo__Enabled=true` and `Demo__DailyReset=true`. It reuses DemoDataService and DemoSeed unchanged: two admins, four supervisors, fifteen employees, teams and relative-date timesheets. Every demo account uses `clockin-demo`; useful accounts are `ada@clockin.demo`, `sam@clockin.demo`, and `eve@clockin.demo`. The login page lists available accounts. Data and sessions reset once the last seed is 24 hours old, checked hourly, or on startup when due.

This deployment uses database/role `clockin_demo`, not the earlier `clockin` database. The earlier personal admin and any existing data remain in `clockin` and are not copied into the public demo. Only sample data belongs in this deployment. Migrations still run as a separate deployment step because Compose overrides the Demo settings' startup-migration flag to false. The Production-mode guard against demo resets is unchanged.

The production-only setup and initial validation below describe the original non-demo deployment. Current deployment scripts provision and back up `clockin_demo`; public demo readiness checks must expect `/api/demo/accounts` to return accounts instead of 404. Old release snapshots retain their original database selection. Local rollback now executes the saved target script as Jenkins already does.

Run `node scripts/check-demo.mjs` to verify public account discovery, admin/supervisor/employee sign-in, Secure/HttpOnly cookies and refresh. It uses the published demo password and logs out its test sessions; it never prints access or refresh tokens.

## Host and secrets

For the hosted API, provision PostgreSQL logging before deployment as described in [logging operations](logging.md): the existing telemetry database, restricted `clockin_log_writer` credential, Grafana dashboard and seven-day cleanup are managed in `jenkins-local`. Deployment requires `CLOCKIN_LOGGING_PASSWORD` in addition to the application database password. `prepare-production-secrets.sh` creates the separate ignored password and `configure-jenkins.sh` installs its Jenkins credential.

Use Ubuntu WSL's Docker Engine, the daemon that hosts `jenkins` and healthy `shared-postgres`. Windows Docker Desktop is a different, currently unavailable daemon. Run Docker commands below inside Ubuntu, not against Docker Desktop.

The frontend is published on `127.0.0.1:8082`; the intended HTTPS hostname is `clockin.gerardoleon.dev`. The private app subnet is `172.30.82.0/24`; verify it is unused before moving this deployment to another host. Only nginx publishes a port. The API and migration process also join external network `shared-db`.

Run `bash scripts/prepare-production-secrets.sh` once. It generates an ES256 P-256 private PEM key and a 64-character hex database password under ignored `.secrets/`. Repeated calls preserve existing files; do not delete these files to redeploy. Set `.secrets/clockin-first-admin-email.txt` privately to the admin email. No email delivery is implemented: the bootstrap link is logged once per startup until activation.

`bash scripts/configure-jenkins.sh /path/to/jenkins-local` applies the reviewed Jenkins patches and adds missing values to its ignored `.env`. It preserves existing credentials. The PEM is base64-encoded for transport, not encrypted; treat both forms as secret. The configuration-copy ownership fix ensures the non-root Jenkins user can read CAsC even when patch application used a restrictive umask.

Rebuild/recreate only Jenkins: `docker compose -f /path/to/jenkins-local/docker-compose.yml up -d --build --no-deps jenkins`. Wait for “Jenkins is fully up and running” and verify `Apps/clockin` exists. Existing apps and PostgreSQL do not need restarting. The new job is disabled by default. After these ClockIn changes are published to `main` and the pipeline is validated, set `CLOCKIN_DEPLOY_ENABLED=true` in Jenkins' `.env` and recreate Jenkins. Do not enable polling while remote main lacks `Jenkinsfile`.

Credentials are `clockin-postgres-password` and `clockin-signing-key-base64`. `CLOCKIN_FIRST_ADMIN_EMAIL` in the server environment supplies the default admin email; the job parameter can override it. Keep a protected backup of the key and the server environment. Restrict log access: bootstrap activation tokens are secrets.

## Deploy

Jenkins builds/tests the exact checkout before deploying. `scripts/deploy.sh` expects `CLOCKIN_TAG`, `CLOCKIN_DB_PASSWORD`, `CLOCKIN_SIGNING_KEY` (decoded PEM), and `CLOCKIN_FIRST_ADMIN_EMAIL`. Optional settings: `CLOCKIN_PORT` (8082), `CLOCKIN_TIME_ZONE` (America/Bogota), and `CLOCKIN_STATE_DIR` (`/var/jenkins_home/deploy-state/clockin`). It rejects non-generated password formats to avoid connection-string escaping ambiguity.

For an initial deployment from a local checkout, use `CLOCKIN_TAG=<unique-tag> bash scripts/deploy-local.sh`. This loads ignored secrets and keeps release state/backups in ignored `.deploy-state/`. Run the backend and frontend checks first. This is a separate release-state location from Jenkins; subsequent Jenkins deployments create their own release history.

Deployment builds tagged API/web images, waits for shared PostgreSQL, provisions the dedicated role/database using Jenkins' `ensure-db`, takes a custom-format database dump, runs the new image's `migrate` command, updates containers and checks readiness and both locale routes. Production explicitly disables demo data and startup migrations. The migration command starts neither the API nor first-admin seeding.

The deployment aborts on any failure. It does not automatically switch back to old code after a schema change. Treat a failed `up` or smoke test as requiring operator attention; old containers may already have been replaced. Normal Compose updates can interrupt service briefly.

nginx serves `/en/` and `/es/` with independent SPA fallbacks and preserves `/api/*` when proxying. A root request selects English; unprefixed activation links redirect into `/en/` with their token query preserved. Missing assets return 404. Cloudflare/cloudflared reach nginx through the host gateway; nginx replaces the forwarded client IP, and ASP.NET trusts only nginx's dedicated address. Revalidate that trust chain if networking changes.

## Backups and rollback

Each new deployment writes `backups/<tag>.dump` before migrations. The dump is PostgreSQL custom format. Store an additional protected off-host copy; local Docker volumes and release backups do not survive host loss. Release snapshots contain Compose/deploy files, not credentials. Backups and images currently require operator-managed retention; never prune an image needed for rollback.

To roll back, verify the target release supports the current schema, then use Jenkins `DEPLOY_TAG=<previous-tag>` and `ACK_SCHEMA_COMPATIBLE=true`. The target release's saved deployment script/config and images are reused. With local state, set `CLOCKIN_ROLLBACK=true ACK_SCHEMA_COMPATIBLE=true CLOCKIN_TAG=<tag>` when invoking `deploy-local.sh`.

Rollback never runs down migrations. If a migration fails, investigate before restarting deployment; a failed migration may require repair even when the runner reports failure. If schema changes are incompatible, prefer a forward fix. Data restoration requires explicitly stopping writes and a separately reviewed recovery operation; it is not an automatic image rollback.

Test restoration into an isolated database first. Copy a selected dump into a disposable PostgreSQL 16 container and run `pg_restore --exit-on-error --no-owner --no-acl --dbname=<empty-test-database> <dump>`. Verify tables and representative records there. Never test restore into `clockin`, another app's database, or the shared cluster's system database.

## Validation and access

Initial deployment validation (2026-11-01): 462 backend tests on PostgreSQL 16 and 112 frontend tests passed; production builds, backend strict build and format checks passed. Jenkins accepted the pipeline definition. Public HTTPS readiness, both locale routes, activation redirects, static-asset 404s, demo-disabled behavior, security headers and separate client rate limits passed. A pre-migration dump restored into an isolated PostgreSQL 16 container with its pending-admin record intact. Bootstrap activation is intentionally left to the admin; a private link is saved in ignored `.secrets/first-admin-activation-link.txt` in the local checkout. Browser login/refresh and the full Playwright suite are not part of this completed validation.

Run backend tests on PostgreSQL 16, strict build and format checks, frontend unit/E2E checks, and both production locale builds. Verify empty/repeated migrations, admin activation, sign-in, refresh, clock-in/out, restart persistence, proxy client IP, security headers, database outage recovery and rollback compatibility. Integration tests use disposable containers, never the shared production database.

Readiness runs `SELECT 1`; it proves database connectivity, not schema validity. Deployment success requires migrations and application smoke checks too. Browser authentication must be checked through HTTPS because the refresh cookie is Secure and HttpOnly.

Useful commands: `docker compose -p clockin ps`, `docker compose -p clockin logs --tail 100 api`, and `curl -f http://127.0.0.1:8082/health/ready`. Run Compose commands with the saved release file and required environment if they need configuration interpolation. Do not paste API logs publicly.

For public access, add the hostname to `jenkins-local/cloudflared/config.yml` before the final 404 rule, create its DNS tunnel route using authenticated cloudflared, and restart only cloudflared. Verify HTTPS before activating the admin. New secrets should never be passed in public URLs or committed files.
