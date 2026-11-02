# ClockIn

Weekly time tracking with supervisor approval. Employees record and correct their hours; supervisors review their team's weeks; admins manage users, teams and approvals. The interface is available in English and Spanish.

## Run the app

### Locally

Install the [.NET 10 SDK](backend/global.json) and Node.js 24. Docker is required for integration tests and Docker deployments.

From the repository root, start the API in one terminal:

```sh
dotnet run --project backend/src/ClockIn.Api --launch-profile http
```

In a second terminal, start the frontend:

```sh
cd frontend
npm ci
npm start
```

Open [the local app](http://localhost:4200). To run Spanish instead, replace `npm start` with `npx ng serve --configuration es` and use the same address.

The API listens at `http://localhost:5244`; the Angular dev server proxies `/api` to it. Development uses a local SQLite database, runs migrations on startup and seeds demo data. The API's [launch settings](backend/src/ClockIn.Api/Properties/launchSettings.json), [development settings](backend/src/ClockIn.Api/appsettings.Development.json) and [frontend proxy](frontend/proxy.conf.json) define this setup.

### Hosted deployment

Try the public demo: **[English](https://clockin.gerardoleon.dev/en/)** · **[Español](https://clockin.gerardoleon.dev/es/)**.

Local development and the hosted demo use these sample accounts, all with password **`clockin-demo`**:

| Role | Email |
|---|---|
| Admin | `ada@clockin.demo` |
| Supervisor | `sam@clockin.demo` |
| Employee | `eve@clockin.demo` |

The sign-in page lists more demo accounts. The hosted deployment runs in the `Demo` environment: sample data and sessions reset daily. Its isolated `clockin_demo` database is for sample data only ([ADR 0005](docs/adr/0005-demo-data-and-daily-reset.md)).

The production hosting topology is:

```text
Browser → HTTPS / Cloudflare Tunnel → nginx
                                      ├─ /en/ and /es/: Angular production builds
                                      └─ /api/ and /health/: .NET API → PostgreSQL
```

[Docker Compose](docker-compose.yml) runs separate API and web images on Ubuntu WSL's Docker Engine. Only nginx publishes a host port (`127.0.0.1:8082`). The API connects to the existing shared PostgreSQL server through `shared-db`, with a dedicated application database and role. Migrations run as a separate deployment step, with startup migrations disabled. Application logs go through Serilog to the existing PostgreSQL telemetry database and Grafana.

To deploy from a checkout, first complete the host, database, logging and secret setup in the **[production operations guide](docs/deployment.md)** and **[logging guide](docs/logging.md)**. Then run this on the configured Ubuntu WSL host, choosing a unique release tag:

```sh
CLOCKIN_TAG=your-unique-release-tag bash scripts/deploy-local.sh
```

The deployment builds versioned images, backs up the application database, runs migrations, updates the containers and checks readiness and both locale routes. The operations guide covers failure handling and schema-compatible rollback. The [Jenkins pipeline](Jenkinsfile) provides a deployment path with quality gates; enabling its job is a separate setup step.

For real employee data, use `Production`, disable demo seeding and provision a separate database; the checked-in Compose configuration currently serves the public demo. GitHub Actions runs CI and publishes coverage to GitHub Pages; the application itself is hosted through Docker and Cloudflare.

Health endpoints: `/health/live` and `/health/ready`. The OpenAPI document at `/openapi/v1.json` is available in Development.

## Architecture and Excalidraw diagrams

| Diagram | What it shows | Editable source | Preview |
|---|---|---|---|
| Application architecture | Backend layers and dependencies, Angular, databases and logs | [Excalidraw](docs/architecture/architecture.excalidraw) | [SVG](docs/architecture/architecture.svg) |
| Request and message flow | Pipeline, mediator, validation, transactions, handlers and domain events | [Excalidraw](docs/architecture/message-flow.excalidraw) | [SVG](docs/architecture/message-flow.svg) |
| Deployment architecture | Cloudflare, nginx, API, shared PostgreSQL, telemetry and Grafana | [Excalidraw](docs/architecture/deployment-architecture.excalidraw) | [SVG](docs/architecture/deployment-architecture.svg) |
| Deployment process | Quality gates, image builds, backups, migrations, smoke checks and rollback | [Excalidraw](docs/architecture/deployment-process.excalidraw) | [SVG](docs/architecture/deployment-process.svg) |

Open the editable files in [Excalidraw](https://excalidraw.com/). See the [diagram guide](docs/architecture/README.md) for scope and maintenance notes.

## Security audits

| Review | Scope | Report |
|---|---|---|
| Run 1 · 2026-10-31 | Quick, partial review of authorization, authentication and timesheet workflows | [Findings, fixes and accepted risks](docs/security/audit-run-1.md) |
| Run 2 · 2026-11-01 | Broader review, including supply chain, resource exhaustion and lifecycle/concurrency behavior | [Findings, mitigations and remaining work](docs/security/audit-run-2.md) |

Both audits were source-only reviews. Their reports identify the reviewed commits and track fixes, accepted risks and unresolved leads. Deployment and logging validation is documented separately in the [operations](docs/deployment.md#validation-and-access) and [logging](docs/logging.md#validation) guides.

## Features and specifications

Behavior is specified in Gherkin `.feature` files. These scenarios drive domain and application tests; `@api` scenarios become xUnit HTTP tests against a real database. The [specification guide](docs/specs/README.md) explains the mapping to tests and the sample company.

| Area | Feature specifications |
|---|---|
| [Accounts](docs/specs/accounts/) | [Create a user](docs/specs/accounts/create-user.feature) · [Log in](docs/specs/accounts/log-in.feature) · [Set a password](docs/specs/accounts/set-password-with-link.feature) · [Refresh a session](docs/specs/accounts/refresh-session.feature) · [Preferences](docs/specs/accounts/update-preferences.feature) |
| [Teams](docs/specs/teams/) | [Create a team](docs/specs/teams/create-team.feature) · [Change supervisor](docs/specs/teams/change-team-supervisor.feature) · [Move an employee](docs/specs/teams/move-employee.feature) · [Archive a team](docs/specs/teams/archive-team.feature) |
| [Time entries](docs/specs/time-entries/) | [Clock in](docs/specs/time-entries/clock-in.feature) · [Clock out](docs/specs/time-entries/clock-out.feature) · [Add a manual entry](docs/specs/time-entries/add-manual-entry.feature) · [Edit an entry](docs/specs/time-entries/edit-entry.feature) · [Delete an entry](docs/specs/time-entries/delete-entry.feature) |
| [Timesheets](docs/specs/timesheets/) | [View a week](docs/specs/timesheets/view-timesheet.feature) · [Submit](docs/specs/timesheets/submit-timesheet.feature) · [Approve](docs/specs/timesheets/approve-timesheet.feature) · [Return](docs/specs/timesheets/return-timesheet.feature) · [Reopen](docs/specs/timesheets/reopen-timesheet.feature) |
| [Correction requests](docs/specs/correction-requests/) | [Request a correction](docs/specs/correction-requests/request-correction.feature) · [Accept](docs/specs/correction-requests/accept-correction.feature) · [Decline](docs/specs/correction-requests/decline-correction.feature) |
| [Reports](docs/specs/reporting/) | [Team week](docs/specs/reporting/team-week-grid.feature) · [Review queue](docs/specs/reporting/review-queue.feature) · [Exceptions](docs/specs/reporting/exceptions.feature) · [Clocked in now](docs/specs/reporting/clocked-in-now.feature) · [To-do badges](docs/specs/reporting/to-do-badges.feature) |
| [Exports](docs/specs/exports/) | [Approved-hours CSV](docs/specs/exports/export-approved-csv.feature) · [Print a timesheet](docs/specs/exports/print-timesheet.feature) |

Product context: [PRODUCT.md](PRODUCT.md). Domain terms: [glossary](docs/glossary.md). Contributor rules and decisions: [code style](CODE_STYLE.md), [stack](STACK.md), [ADRs](docs/adr/) and [decisions log](DECISIONS.md).

## Tests and code coverage

**[Latest coverage dashboard](https://elparaquecosadeque.github.io/clockin/)** · **[CI runs](https://github.com/elparaquecosadeque/clockin/actions/workflows/ci.yml)**

| Application | Line coverage | Branch coverage | Detailed reports |
|---|---|---|---|
| Backend | ![Backend line coverage][backend-lines] | ![Backend branch coverage][backend-branches] | [HTML](https://elparaquecosadeque.github.io/clockin/backend/) · [Markdown](https://elparaquecosadeque.github.io/clockin/backend/SummaryGithub.md) |
| Frontend | ![Frontend line coverage][frontend-lines] | ![Frontend branch coverage][frontend-branches] | [HTML](https://elparaquecosadeque.github.io/clockin/frontend/) · [Markdown](https://elparaquecosadeque.github.io/clockin/frontend/SummaryGithub.md) |

The badges read the published coverage summaries rather than fixed percentages in this README. A successful `main` build publishes both reports from the same run; failed builds keep the last successful reports online. The dashboard identifies the source commit and generation time. PRs publish downloadable CI artifacts without deploying Pages.

Backend coverage merges unit, architecture and HTTP/integration results for all five production assemblies. Frontend coverage includes all TypeScript source, including untested files. Test code and generated backend `obj` files are excluded; type-only frontend files have no executable lines. Domain and Application must **each reach 80% line coverage**, using exact covered/coverable counts. Other layers and the frontend are report-only. Playwright remains a separate behavior and accessibility check.

### Run the checks

Backend checks, from `backend/`:

```sh
dotnet test                     # unit, architecture and HTTP/integration tests; needs Docker
dotnet build -p:ContinuousIntegrationBuild=true
dotnet format --verify-no-changes
```

From `frontend/`:

```sh
npm run format:check
npm run test:ci
npm run build                  # production builds for English and Spanish
npm run e2e                    # Playwright + axe, both languages
npm run extract-i18n            # refresh message catalog after changing texts
```

New UI texts need an `i18n` id and a Spanish translation in `frontend/src/locale/messages.es.xlf`; missing translations fail the build.

Coverage tooling checks, from the repository root: `node --test scripts/*.test.mjs`.

### Generate coverage locally

Install the pinned collectors and report tool after pulling dependency changes:

```sh
dotnet tool restore
dotnet restore backend/ClockIn.slnx
cd frontend
npm ci
cd ..
```

From the repository root, run `node scripts/coverage.mjs` for both applications, or pass `backend` or `frontend` to run one side. `npm run test:coverage` is also available from `frontend/`. Each run replaces that target's generated coverage so stale hits do not inflate the result. Docker must be running for backend integration tests.

| Generated report | Backend | Frontend |
|---|---|---|
| HTML with highlighted source | `coverage/backend/index.html` | `coverage/frontend/index.html` |
| Markdown summary | `coverage/backend/SummaryGithub.md` | `coverage/frontend/SummaryGithub.md` |
| Machine-readable summary | `coverage/backend/Summary.json` | `coverage/frontend/Summary.json` |

Run `node scripts/coverage-site.mjs` after collection to preview the dashboard at `coverage/site/index.html`. Raw backend collector files remain CI artifacts and are not published to Pages. Generated reports are ignored by Git.

[backend-lines]: https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Felparaquecosadeque.github.io%2Fclockin%2Fbackend%2FSummary.json&query=%24.summary.linecoverage&label=lines&suffix=%25&color=blue
[backend-branches]: https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Felparaquecosadeque.github.io%2Fclockin%2Fbackend%2FSummary.json&query=%24.summary.branchcoverage&label=branches&suffix=%25&color=blue
[frontend-lines]: https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Felparaquecosadeque.github.io%2Fclockin%2Ffrontend%2FSummary.json&query=%24.summary.linecoverage&label=lines&suffix=%25&color=blue
[frontend-branches]: https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Felparaquecosadeque.github.io%2Fclockin%2Ffrontend%2FSummary.json&query=%24.summary.branchcoverage&label=branches&suffix=%25&color=blue
