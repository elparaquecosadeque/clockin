# Decisions log

Smaller decisions made while developing, newest first. A decision that changes a rule in `CODE_STYLE.md` or `STACK.md`, or that is hard to reverse, gets an ADR in [`docs/adr/`](docs/adr/) instead, and only a one-line pointer here.

Each entry has the decision, the reason, and who decided (**user**, or **dev** when it was taken while implementing and not objected to).

## 2026-10-30: Timesheet persistence

| Decision | Why | By |
|---|---|---|
| `time_entries.owner_id` is copied from the timesheet, with a partial unique index `WHERE ended_at IS NULL`. | Enforces one open entry per person under concurrency; partial indexes work on both engines. | dev |
| A unique-index violation while saving entries maps to 409 `EntryAlreadyOpen`; on the timesheet row (same owner and week) or a stale `Version`, to 409 `ConcurrencyConflict`. | Matches the `@api` concurrency scenario in `clock-in.feature` and the optimistic-concurrency rule. | dev |
| Saving rewrites the timesheet's entries (delete, then insert) inside the save. | The version check already serializes writers; it also covers deleted entries with no diffing. | dev |
| `SaveAsync` opens its own transaction (`ponytail:`) instead of the mediator's unit of work. | Each command saves one aggregate today. Move it to middleware with domain events. | dev |
| Domain aggregates expose `Restore(...)` for repositories. | Rebuilding needs private state; it checks no rules. | dev |
| Records hold plain `Guid`s; the repository maps typed IDs. No linq2db value converters yet. | Records never leave Persistence, so converters add nothing yet. | dev |
| Timestamps use a custom `timestamptz` column type. | FluentMigrator can't map `DateTimeOffset` on SQLite; SQLite accepts any type name. | dev |
| Repository tests live in `Api.Tests` (real PostgreSQL); they were also run once against SQLite. | No `Persistence.Tests` project in `CODE_STYLE.md`; the database is what's under test. | dev |
| Migration class names are exempt from CA1707. | `CODE_STYLE.md` names them `M<yyyyMMddHHmm>_<Description>`. | dev |

## 2026-10-30: Clock in and out (domain and application)

| Decision | Why | By |
|---|---|---|
| Time entries live inside the `Timesheet` aggregate (one per user and week); no `TimeEntry` repository. | A week's entries share its rules (Draft only, approval, 16 h) and change together. | dev |
| The company timezone is `TimeProvider.LocalTimeZone`; production will supply a `TimeProvider` set from deploy config. | One injected dependency gives both "now" and the zone, and tests swap both with `CompanyClock`. | dev |
| "One open entry per user" is checked by the aggregate within its week, by the clock-in handler across weeks, and (to come) by a unique database index under concurrency. | An aggregate can't see other weeks; only the database is safe against two simultaneous punches. | dev |
| Handlers don't check roles; "admins don't punch" will be a permission policy on the endpoint. | `CODE_STYLE.md`: permission-based policies, never roles in code. | dev |
| A user agent longer than 512 characters is truncated, not rejected. | A punch must not fail over request metadata. | dev |
| No Bogus fakers yet; tests build domain objects directly. | Construction is two lines today. Add Bogus fakers when setup grows. | dev |
| Deferred: `Version` on `Timesheet`. | It belongs with the repository and the optimistic-concurrency check. | dev |

## 2026-10-30: Walking skeleton

| Decision | Why | By |
|---|---|---|
| .NET 10 SDK, pinned in `backend/global.json` (`latestFeature` roll-forward). | `Guid.CreateVersion7()` needs .NET 9+, and .NET 8 support ends November 2026. .NET 10 is the current LTS. | user |
| Git branch `main`; work on short-lived branches. | Matches the trunk-based workflow in `CODE_STYLE.md`. | user |
| No GitHub remote yet; work locally until the user creates it. | The user will create the repository later. | user |
| xUnit v3 on Microsoft.Testing.Platform; Reqnroll package will be `Reqnroll.xunit.v3`. | Current xUnit; ADR 0002 needed the version to pick the package. | dev |
| Angular 22 with its defaults: Vitest unit-test runner and Prettier formatting. | "Latest stable major" and "the CLI's default runner" in `CODE_STYLE.md`. | dev |
| Reference `FluentMigrator.Runner.Postgres` and `.SQLite`, not the `FluentMigrator.Runner` meta-package. | The meta-package pulls SQL Server's client, with a high-severity vulnerable dependency. Fewer packages too. Recorded in `STACK.md`. | dev |
| Add `WolverineFx.RuntimeCompilation`. | Wolverine 6 split the runtime compiler out; without it the mediator does not start. Chosen over pre-generated code to avoid a codegen step. Recorded in `STACK.md`. | user |
| Engine drivers `Npgsql` and `Microsoft.Data.Sqlite` alongside linq2db. | Required by the two approved engines. Recorded in `STACK.md`. | dev |
| `ClockIn.Persistence` uses `FrameworkReference Microsoft.AspNetCore.App`. | Gives configuration, DI and health-check abstractions with no extra packages. | dev |
| Database health check runs `SELECT 1` through linq2db. | Works on both engines without a health-check package. | dev |
| Skip migrations while none exist (`ponytail:` guard in `PersistenceModule`). | FluentMigrator throws when it finds no migrations. Remove the guard with the first migration. | dev |
| Integration tests run on PostgreSQL only; SQLite is for local development. | `CODE_STYLE.md`: test every engine the project *deploys* to. | dev |
| `Domain.Tests` and `Application.Tests` are created with their first test. | The test platform fails a project that has zero tests. | dev |
| Correlation IDs: accept an incoming `X-Correlation-Id` only if it matches `[A-Za-z0-9-]{1,64}`; otherwise generate a Guid v7. | The header comes from outside and ends up in logs and responses. | dev |
| Error responses carry `code` (the rule code from the specs) for 409 and 422. | Lets the frontend and tests tell rules apart without parsing messages. | dev |
| `.editorconfig`: "var elsewhere" is a suggestion, not a warning. | `CODE_STYLE.md`'s own example (`var builder = WebApplication.CreateBuilder(...)`) would fail CI otherwise. | dev |
| Tests are exempt from the `Async` suffix, CA1707 (underscores) and CA1822 (could be static). | Test names follow `Behavior_when_condition`. | dev |
| `GenerateDocumentationFile` on, CS1591 off. | Needed for the unused-`using` check (IDE0005) at build time; XML docs are not required. | dev |
| Line endings LF everywhere (`.gitattributes`), CRLF only for Windows scripts. | Stops the CRLF warnings on every commit and keeps diffs clean across OSes. | dev |
| Locales served under `/en/` and `/es/`; a missing Spanish translation fails the build. | ADR 0001; a forgotten translation should never reach users. | dev |
| End-to-end tests run English on desktop and Spanish on a phone viewport. | Covers both locales and the mobile-first clock screen in one pass. | dev |
| Deferred: 80% coverage gate, frontend linting, Reqnroll. | Coverage needs Domain/Application code; linting needs `angular-eslint` (new package, to be asked); Reqnroll comes with the first `@api` scenario. | dev |

## 2026-10-30: Mockups

| Decision | Why | By |
|---|---|---|
| Mockups on a Claude design canvas now; Penpot once a designer owns the UI. | Fast product-owner review with comments; Penpot as the long-term source of truth. | user |
| Angular Material Azure palette, light theme, Roboto. Unbranded. | Mockups must align with Angular Material; no brand exists yet; office daylight use. | user |

## 2026-10-30: Requirements

The interview decisions are in [`docs/specs/`](docs/specs/) and [`docs/glossary.md`](docs/glossary.md). These were inferred while writing the specs and confirmed by the user:

| Decision | By |
|---|---|
| One role per user; the Admin role is never granted or removed by a role change. | user |
| A timesheet can be submitted only after its week ends. | user |
| A pending correction request blocks approve, return and reopen. | user |
| Admins can make reasoned, flagged edits on any Draft timesheet. | user |
| Archiving a team is blocked by pending reviews; team names stay unique after archiving. | user |
| Timesheets record the team they were approved under. | user |
| Hours count on the day an entry started. | user |
| Exceptions: "long entry" over 10 h, "many manual changes" 3 or more. | user |
| Login failures always give one generic error, even while locked. | user |
| The "declined request" badge counts the last 7 days. | user |
| Deactivation and correction requests need no reason. | user |

Larger decisions: [ADR 0001](docs/adr/0001-internationalization.md) (i18n), [ADR 0002](docs/adr/0002-reqnroll-for-api-specs.md) (Reqnroll), [ADR 0003](docs/adr/0003-adopt-baseline-code-style-and-stack.md) (baseline and ADR sync).
