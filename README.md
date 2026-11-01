# ClockIn

Weekly time tracking with supervisor approval. Product context: [`PRODUCT.md`](PRODUCT.md). Rules for contributors: [`CODE_STYLE.md`](CODE_STYLE.md), [`STACK.md`](STACK.md), [`docs/adr/`](docs/adr/). Use cases: [`docs/specs/`](docs/specs/).

## Prerequisites

- .NET SDK 10 (pinned in `backend/global.json`)
- Node.js 24
- Docker, for the integration tests (PostgreSQL in Testcontainers)

## Backend

```sh
cd backend
dotnet run --project src/ClockIn.Api        # http://localhost:5244, SQLite file, migrates on startup
dotnet test                                 # architecture + integration tests (needs Docker)
dotnet build -p:ContinuousIntegrationBuild=true   # strict build, as in CI
dotnet format --verify-no-changes
```

Health: `/health/live` and `/health/ready`. OpenAPI document (Development only): `/openapi/v1.json`.

**Database.** Development uses SQLite (`appsettings.Development.json`). Other environments use PostgreSQL: set `Database:ConnectionString` through user secrets or the environment, never in `appsettings.json`. Migrations run on startup only where `Database:MigrateOnStartup` is true (local and tests); remote environments run them as a deployment step.

**Demo data** ([ADR 0005](docs/adr/0005-demo-data-and-daily-reset.md)). In Development, and in a deployment with `ASPNETCORE_ENVIRONMENT=Demo`, the API wipes the database and seeds a sample company: admins Ada and Abe, supervisors Sam, Sue and Sid leading Warehouse, Office and Night Shift, and 15 employees with three weeks of timesheets. Every account is `<name>@clockin.demo` with password `clockin-demo`, and the sign-in page lists the most useful ones. The data is rebuilt once it is 24 hours old; set `Demo:DailyReset` to `false` to seed once and keep it, or `Demo:Enabled` to `false` to turn it off. Startup refuses `Demo:Enabled` in any other environment.

## Frontend

```sh
cd frontend
npm ci
npm start                                  # English, http://localhost:4200
npx ng serve --configuration es            # Spanish
npm run test:ci                            # unit tests
npm run e2e                                # Playwright + axe, both languages
npm run build                              # production build, one folder per language
npm run extract-i18n                       # refresh src/locale/messages.xlf after changing texts
```

New texts get an `i18n` id in the template, then a `<target>` in `src/locale/messages.es.xlf`. The build fails on a missing translation.

## Code coverage

Install the pinned collectors and report tool once after pulling dependency changes:

```sh
dotnet tool restore
dotnet restore backend/ClockIn.slnx
cd frontend
npm ci
cd ..
```

From the repository root, run `node scripts/coverage.mjs` to collect backend and frontend coverage and generate reports. Docker must be running for the backend integration tests. To run one side, use `node scripts/coverage.mjs backend` or `node scripts/coverage.mjs frontend` (also `npm run test:coverage` from `frontend`). Regular `dotnet test` and `npm run test:ci` remain available without coverage.

Each run replaces that target's generated coverage, so stale hits cannot inflate later results:

| Report | Backend | Frontend |
|---|---|---|
| HTML with highlighted source | `coverage/backend/index.html` | `coverage/frontend/index.html` |
| Markdown summary | `coverage/backend/SummaryGithub.md` | `coverage/frontend/SummaryGithub.md` |
| Machine-readable summary | `coverage/backend/Summary.json` | `coverage/frontend/Summary.json` |

Backend coverage merges unit, architecture and HTTP/integration test results for all five production assemblies. Tests, third-party assemblies and generated `obj` source are excluded. Unexecuted code in the measured assemblies remains uncovered. Frontend coverage includes `src/**/*.ts`, including files with no tests; test files are excluded and type-only files have no executable lines.

Line and branch coverage are reported separately. Domain and Application must **each reach 80% line coverage**, using exact covered/coverable line counts. Other layers and the frontend are report-only for now. The gate fails if any production backend layer is missing from the report. Run its regression checks with `node --test scripts/coverage-check.test.mjs`.

CI uses the same commands, adds Markdown summaries to the Actions run summary and uploads separate `backend-coverage` and `frontend-coverage` artifacts, including reports available after a failing test run. Generated reports are ignored by Git. Playwright remains a separate behavior/accessibility check and does not contribute to these code coverage figures.

## Architecture diagrams

See [`docs/architecture/`](docs/architecture/README.md).
