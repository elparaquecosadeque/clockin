# Docker deployment research and plan

Research date: 2026-11-01. Scope: ClockIn and the adjacent `jenkins-local` repository. The findings and original plan below preserve the pre-implementation assessment.

Implementation update, 2026-11-01: the Docker deployment is now running on Ubuntu WSL with the shared PostgreSQL 16 server and public HTTPS ingress. See [deployment operations](deployment.md). The original findings below describe the pre-implementation state. Automatic Jenkins deployment stays disabled until the new pipeline is published to main.

## Recommendation

Build separate API and web images, deploy them as Compose project `clockin`, and reuse the existing `shared-postgres` container through external network `shared-db`. Give ClockIn its own database and login role, both named `clockin`. Run migrations as a one-shot deployment command before starting the API. Keep SQLite as the default local-development engine.

Proposed traffic path:

```text
Browser -- HTTPS --> Cloudflare Tunnel
                       |
                 host 127.0.0.1:8082 (proposed; verify availability)
                       |
                  ClockIn web / nginx
                    |           |
             /en/*, /es/*      /api/* and /health/*
             static Angular       |
                              ClockIn API
                                  |
                          shared-db network
                                  |
                       shared-postgres:5432
                          database clockin
```

Only API and migration containers join `shared-db`; web and API also share a ClockIn-specific network. No additional PostgreSQL service or database host port is needed. Compose explicitly supports joining a separately managed network with `external: true`: [Docker networking](https://docs.docker.com/compose/how-tos/networking/).

## Verified findings

| Area | Evidence and deployment implication |
| --- | --- |
| Application stack | [.NET SDK pin](../backend/global.json), [backend target](../backend/Directory.Build.props), and [frontend dependencies](../frontend/package.json): .NET 10, Angular 22, Node 24. Match the SDK pin in the build image; use the ASP.NET 10 runtime image for the final API image. |
| Database switch | [PersistenceModule](../backend/src/ClockIn.Persistence/Common/PersistenceModule.cs) selects LinqToDB and FluentMigrator providers from `Database:Provider`; accepted values are `PostgreSQL` and `SQLite`. No ORM replacement or new database package is needed. |
| Environment defaults | [Base settings](../backend/src/ClockIn.Api/appsettings.json) default to PostgreSQL with an empty connection string and startup migrations disabled. [Development settings](../backend/src/ClockIn.Api/appsettings.Development.json) select SQLite and enable migrations and demo data. |
| Missing deployment assets | No Dockerfile, Compose file, nginx config, or Jenkinsfile exists in ClockIn's inspected tracked source. Existing [.github/workflows/ci.yml](../.github/workflows/ci.yml) handles CI and coverage publication, not application deployment. |
| Migration gap | [Program](../backend/src/ClockIn.Api/Program.cs) only invokes migrations conditionally during API startup. The reusable `MigrateDatabase()` runner already exists in PersistenceModule, but there is no migration-only process entry point. [CODE_STYLE](../CODE_STYLE.md), “Migrations and seed data,” requires a separate production deployment step. This is FluentMigrator, not EF Core. |
| Startup dependency | [FirstAdminSeeder](../backend/src/ClockIn.Api/Auth/FirstAdminSeeder.cs) invokes [SeedFirstAdminCommand](../backend/src/ClockIn.Application/Accounts/Commands/SeedFirstAdminCommand.cs) during startup. The schema must already exist; a fresh production database also requires `FirstAdmin:Email`. |
| Signing key | [AuthOptions](../backend/src/ClockIn.Api/Auth/AuthOptions.cs) and [AccessTokenIssuer](../backend/src/ClockIn.Api/Auth/AccessTokenIssuer.cs) require a persistent ES256 P-256 private key in PEM format. A Worktime-style random signing string is unsuitable. The automatic throwaway key is Development-only. |
| Localized frontend | [angular.json](../frontend/angular.json) builds English and Spanish with locale subpaths `en` and `es`; [SessionService](../frontend/src/app/core/services/session.service.ts) switches between those paths. nginx needs per-locale SPA fallbacks, not one global `index.html`. [Angular deployment guidance](https://angular.dev/guide/i18n/deploy) confirms locale-specific base paths. |
| Same-origin API | [AuthService](../frontend/src/app/features/auth/services/auth.service.ts) and [development proxy](../frontend/proxy.conf.json) use root `/api` paths. Proxy `/api/*` unchanged, outside the locale paths; no production CORS policy is necessary with this topology. |
| Auth cookies | [RefreshCookieHelper](../backend/src/ClockIn.Api/Auth/RefreshCookieHelper.cs) uses Secure, HttpOnly, SameSite=Strict cookies scoped to `/api/auth`. Validate sign-in and refresh through public HTTPS, not only internal HTTP health probes. |
| Proxy handling | [Program](../backend/src/ClockIn.Api/Program.cs) rate-limits by `RemoteIpAddress`; [TimeEntriesController](../backend/src/ClockIn.Api/Controllers/TimeEntriesController.cs) records that address. Forwarded headers are not configured. Establish a trusted Cloudflare/cloudflared/nginx chain before enabling client-IP processing. [Microsoft guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer) requires explicit proxy trust. |
| Health endpoints | Program already exposes anonymous `/health/live` and `/health/ready`. [DatabaseHealthCheck](../backend/src/ClockIn.Persistence/Common/DatabaseHealthCheck.cs) readiness runs `SELECT 1`; it checks connectivity, not schema completeness. Add migration and application-flow checks to deployment validation. |
| Runtime code generation | [STACK](../STACK.md) and [DECISIONS](../DECISIONS.md) explicitly select `WolverineFx.RuntimeCompilation` rather than build-time code generation. Preserve this choice and verify the published image starts and executes a handler as a non-root user. No AOT/trimming or code-generation redesign is needed. [Wolverine codegen guidance](https://wolverinefx.io/guide/codegen). |
| Production database version | [jenkins-local Compose](../../jenkins-local/docker-compose.yml) uses PostgreSQL `16-alpine`; [ApiFactory](../backend/tests/ClockIn.Api.Tests/Common/ApiFactory.cs) tests on `17-alpine`. Run deployment-relevant integration tests on PostgreSQL 16; do not upgrade the shared server as part of ClockIn deployment. |
| Shared database provisioning | [ensure-db](../../jenkins-local/bin/ensure-db) creates a missing role/database, assigns the database owner, resets that role's password on every run, and revokes database privileges from PUBLIC. It does not wait for readiness, repair existing ownership, or remove other explicit grants. Use a fresh dedicated ClockIn role/database. |
| Jenkins conventions | [casc/jenkins.yaml](../../jenkins-local/casc/jenkins.yaml) creates `apps/<app>` jobs, polls every two minutes, disables overlapping builds, and defines `DEPLOY_TAG`. [Jenkins README](../../jenkins-local/README.md) describes tagged images, retained release snapshots and rollback; each app must implement those behaviors itself. |
| Configuration reload | [Jenkins Dockerfile](../../jenkins-local/Dockerfile) copies CAsC and `ensure-db` into its image. Changes to those files require `docker compose up -d --build jenkins`; a restart alone does not update baked files or changed environment values. |
| Existing ingress | [cloudflared config](../../jenkins-local/cloudflared/config.yml) maps hostnames to host-local ports; [Compose](../../jenkins-local/docker-compose.yml) uses host networking for cloudflared. Add ClockIn before the final 404 entry. Existing configured ports include 3000, 8080, 8081, 8090 and 3001. |

Secrets files (`jenkins-local/.env` and tunnel credentials) were not read. The Windows Docker context resolves to `desktop-linux`, but its daemon pipe is unavailable. Running containers, actual port availability, database health, and the WSL Docker daemon were not verified. Repository configuration describes WSL Ubuntu plus Docker Engine; implementation must identify and use the daemon hosting Jenkins and `shared-postgres`, rather than accidentally deploying onto a separate Docker Desktop daemon.

## Implementation plan

### 1. Make the production process explicit

- Add a migration-only command to the API executable, reusing PersistenceModule and exiting nonzero on failure. Dispatch it before web-host startup so it never starts Wolverine, first-admin seeding, demo services, or HTTP listeners, and needs only database configuration.
- Keep production `Database__MigrateOnStartup=false`; explicitly use `ASPNETCORE_ENVIRONMENT=Production`, `Database__Provider=PostgreSQL`, and `Demo__Enabled=false`.
- Configure persistent `Auth__SigningKey`, `FirstAdmin__Email`, and `Company__TimeZone`. Preserve `America/Bogota` unless the operator chooses another company timezone; the workstation timezone is not the application's business setting.
- Add focused tests for migration-only success, repeatability, failure propagation and absence of application seeding. Run PostgreSQL integration tests on the deployed major version, 16.

### 2. Containerize API and web

- Add `backend/Dockerfile`: SDK build/publish stage and ASP.NET runtime stage, non-root execution, explicit internal port 8080. Preserve timezone data and writable paths required by runtime compilation.
- Add `frontend/Dockerfile`: Node 24 plus `npm ci` and production locale build; nginx serves only the built assets. Inspect actual build output paths before writing COPY rules.
- Add nginx configuration: `/` redirects to `/en/`; `/en/*` and `/es/*` fall back to their respective indexes; missing assets return 404; `/api/*` preserves its path and goes to the API. Preserve query strings on redirects so first-admin activation links survive. Apply SPA security headers separately from the API's JSON-only CSP.
- Add appropriate `.dockerignore` files so `.git`, local databases, logs, secrets, dependency folders and build/test outputs do not enter image contexts. Allow a committed `.env.example` explicitly because the root `.gitignore` currently excludes `.env.*`.
- Configure production console logging and bounded Docker log rotation. The current file sink targets `logs/app-.log`; avoid unbounded or unwritable file logging in the runtime container. Restrict log access because the first-admin activation token is intentionally logged.
- Keep SQLite development through existing `dotnet run` and Angular dev commands. Add a small Docker development override selecting SQLite, migrations and a persistent database volume only if running the dev environment in containers is part of the desired workflow; never enable demo resets against the production database.

### 3. Compose and deployment lifecycle

- Add root Compose configuration with `web`, `api`, and a one-shot migration service reusing the API image. Use project `clockin`, a private app network, and external `shared-db`. Publish only web on proposed `127.0.0.1:8082`; confirm availability on the actual host.
- Keep database credentials out of image layers and committed release snapshots. Inject them at runtime through Jenkins credential bindings. Handle PEM newlines and connection-string escaping explicitly; test the chosen secret-delivery format without printing resolved secrets.
- Add bounded readiness waiting for the shared database before `ensure-db clockin clockin`; wait again for application-role connectivity as appropriate. Cross-project services cannot be ordered with ordinary Compose `depends_on`.
- Deploy in this order: provision database, back up an existing database, run the new image's migration-only command to completion, update app containers, wait for readiness, then smoke-test through web. Stop immediately on migration failure.
- Implement tagged images and saved non-secret Compose/deploy release files with a last-good pointer and retained releases, matching Jenkins' documented conventions. Disable concurrent deployments and support `DEPLOY_TAG` rollback.
- Roll back application images/configuration only when compatible with the migrated schema. Do not run automatic down migrations or claim image rollback restores database state. Prefer additive migrations; destructive changes need a separately planned backup/restore or forward-fix procedure. Initial Compose recreation may briefly interrupt service; zero-downtime rolling deployment is not assumed.

### 4. Integrate Jenkins and HTTPS ingress

- Add root `Jenkinsfile`: checkout, strict backend build/format/tests/coverage, frontend format/unit tests/build, existing E2E checks, image build/tag, provisioning, migrations, deploy and smoke test. Reuse the existing coverage scripts and retain the GitHub Pages reporting workflow. Ensure deployment is gated on checks for the exact deployed commit.
- Run backend integration tests in disposable containers, never in the shared application database. Containerized Jenkins test runners need Docker socket access and correct Testcontainers host routing/workspace mounts for the host-daemon topology; validate this before first deployment. Jenkins has two executors and finite memory, so avoid needless parallel SDK/browser builds.
- In `jenkins-local`, add the `clockin` main-branch job for `https://github.com/elparaquecosadeque/clockin`, a dedicated database-password credential, and a dedicated signing-key credential. Provide their values in the ignored server environment; ensure the GitHub token can read ClockIn. Prefer a secret-file credential if PEM delivery is awkward, with an explicit runtime loading path.
- Rebuild/recreate Jenkins after CAsC changes. Do not recreate the shared database or remove its volume.
- Proposed hostname: `clockin.gerardoleon.dev`. Add its ingress entry and DNS route, then restart cloudflared. Hostname and port are proposals, not verified allocations.
- Configure nginx to normalize the trusted ingress headers and ASP.NET Core to trust only the intended proxy path. Verify actual client IP and HTTPS scheme end to end; do not globally trust arbitrary forwarded headers. Confirm sign-in rate limiting separates real clients and punch-source IP is correct.

### 5. Acceptance checks and operator documentation

- On a disposable PostgreSQL 16 database: migrate empty schema, migrate again successfully, start production image, activate first admin, sign in, refresh session, perform a representative clock-in/out, restart containers and confirm data/session continuity.
- Against the built web image: test both locale entry points and deep links, language switching, static asset handling, API errors returning JSON rather than SPA HTML, activation query-string preservation and browser security headers.
- Through the actual HTTPS hostname: validate auth cookies, refresh, proxy IP handling, readiness failure on database loss, recovery, and persistence across app redeploys. Verify no real data is reset and demo accounts are unavailable.
- Exercise failed migration, failed smoke test, compatible application rollback and explicit `DEPLOY_TAG` rollback. Test database backup restoration in an isolated database, never over another app's data.
- Document prerequisites, environment variables, signing-key generation, migration and deployment commands, secure admin activation, logs, backups, recovery and SQLite local development in README. Record the accepted hosting choice in STACK/DECISIONS and an ADR if required by the repo's decision rules.

## Decisions to settle during implementation

The default proposal is real Production with no demo data, one API instance, `/en/` as the root destination, the existing company timezone, port 8082, and `clockin.gerardoleon.dev`. Supply the real first-admin email and secrets privately. If this is instead a public throwaway demo, use a separate database and `Demo` environment; its daily destructive reset is defined by [ADR 0005](adr/0005-demo-data-and-daily-reset.md).

No implementation changes, service restarts, database provisioning, DNS changes or deployment were performed during this research.
