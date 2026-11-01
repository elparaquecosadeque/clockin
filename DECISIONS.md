# Decisions log

Smaller decisions made while developing, newest first. A decision that changes a rule in `CODE_STYLE.md` or `STACK.md`, or that is hard to reverse, gets an ADR in [`docs/adr/`](docs/adr/) instead, and only a one-line pointer here.

Each entry has the decision, the reason, and who decided (**user**, or **dev** when it was taken while implementing and not objected to).

## 2026-10-31: Security audit leads (run 1)

| Decision | Why | By |
|---|---|---|
| A sign-in whose save loses a concurrency race is rejected with `InvalidCredentials`, not 409. A user signing in from two tabs at once may have to retry. | Only registered emails reached the versioned save, so the 409 revealed which emails exist. Confirmed by a failing handler test. | user |
| The email in the failed sign-in audit entry has its control characters replaced by `?`. | It comes from an anonymous caller, and Serilog's default text template writes it unquoted, so a line break forged audit lines. Confirmed by a failing handler test. | user |

## 2026-10-31: Code review naming

| Decision | Why | By |
|---|---|---|
| Repository variables end in `Repository`. | [ADR 0006](docs/adr/0006-repository-suffix-on-variables.md). | user |
| Repositories' save method is `SaveOrUpdateAsync`: it inserts a new aggregate (`Version` 0) and otherwise updates with the version check. | The name says it does both. | user |
| `TimesheetLoading.NeighboursAsync` keeps its lookups sequential, not `Task.WhenAll`. | They share one database connection, which isn't thread-safe. | user |
| Constructors with more than 2 dependencies, and methods that take dependencies as parameters, list one parameter per line. Checked in review; reflection can't see line breaks. | The number of dependencies is visible at a glance. | user |
| Static classes end in `Extensions`, `Helper` or `Constants`. | [ADR 0007](docs/adr/0007-suffixes-for-static-classes.md). | user |
| `CorrectionRules` becomes `CorrectionRulesHelper`, though its methods are extensions; `Permissions`, `TextRules`, `PreferenceRules` and `WeekGuards` keep their names for now. | Its rules are shared by two handlers. | user |
| Logic shared by handlers that needs dependencies is an injected service; the timesheet permission checks move into `TimesheetAccess`. | [ADR 0008](docs/adr/0008-shared-handler-logic-in-injected-services.md). | user |

## 2026-10-31: Demo sign-in list and own team

| Decision | Why | By |
|---|---|---|
| The sign-in page lists every demo account that can sign in (17), not 7 featured ones. Fay (pending), Ed (deactivated) and Lou (locked out) stay off the list; admins see their states. | Asked by the user. | user |
| The demo gains a 4th supervisor, Tom, who leads no team (changes the earlier "3 supervisors"). | A supervisor leads one team at most, so with every supervisor leading one, "Change supervisor" and "New team" had nobody to offer. | dev |
| When no supervisor is free, "Change supervisor" and "New team" explain why and link to Users instead of opening an empty list. | An empty dropdown gave no hint about the one-team rule or what to do. | dev |
| Employees see their team and supervisor in the account menu, from `/api/me` ([see-own-team.feature](docs/specs/teams/see-own-team.feature)). | Asked by the user; the menu is on every page. Supervisors and admins have no team, so no line. | user / dev |

## 2026-10-30: Timesheets, review, reports, administration and settings (frontend)

| Decision | Why | By |
|---|---|---|
| No home page: the app opens on the clock for anyone who punches, otherwise the review queue, otherwise settings. Main navigation is a tab bar with to-do badges. | Every role's first task is one of those screens; the badges carry what a home page would have listed. | dev |
| Times follow the user's 12 h/24 h preference through a `TIME_FORMAT` token; changing language or time format reloads the app. | The locale is a separate build (ADR 0001), so a language change needs a reload anyway; one path keeps it simple. | dev |
| Users are sent to their preferred language's build only when the URL is under `/en/` or `/es/`. | The dev server and tests serve one locale at the root; redirecting there would loop. | dev |
| In the entry dialog, an end time earlier than the start means the next day. | Night shifts need it; the API already counts hours on the start day. | dev |
| The team-week grid filters teams on the client. | One request already returns every team the viewer may see; phase 1 teams are small. | dev |
| Initial bundle warning raised from 500 kB to 600 kB (565 kB raw, 139 kB compressed). | Tabs and badges in the shell; features are lazy-loaded. | dev |
| Row action menus are labelled with visually hidden text instead of an interpolated `aria-label`. | The interpolated, translated `aria-label` rendered empty on the menu triggers (caught by axe). | dev |

## 2026-10-30: Reporting and administration (backend)

| Decision | Why | By |
|---|---|---|
| Reports, queues, user and team lists read through one flat `IReportingReads` port that loads whole tables; handlers scope and shape the rows. | A small company fits in memory, and one port keeps the SQL in one place. Marked `ponytail:`: filter in SQL per report past a few thousand rows. | dev |
| Exceptions skip Approved timesheets. | Approved weeks were already reviewed; flagging them again is noise. | dev |
| The admin queue holds timesheets of anyone who punches time and has no team, which means supervisors. | Their approver is derived from the team, and they have none. | dev |
| The CSV writes "manually changed" as `true`/`false`. | Neutral across locales and easy to filter in a spreadsheet. | dev |
| Language and time format are stored as `users.language` and `users.time_format`, defaulting to `en` and `24h`. | ADR 0001: preferences follow the user across devices. | dev |
| Archive checks (active members, pending reviews) reuse `IReportingReads` instead of new repository queries. | Same data the queues already read; no new SQL to test. | dev |
| Changing one's own password revokes every session and returns new tokens for the device that asked; it shares the sign-in rate limit. | change-own-password.feature; it also checks a password, like sign-in. | dev |
| Role changes and deactivations are audited; creating and reactivating a user audit the access link they issue. | CODE_STYLE: security events go to the Audit log, never with tokens. | dev |

## 2026-10-30: Timesheet workflow (backend)

| Decision | Why | By |
|---|---|---|
| Commands opt into one transaction per command with an `ITransactional` marker, applied as mediator middleware (`TransactionMiddleware`). | CODE_STYLE asks for transactions as middleware; a blanket transaction would roll back a failed sign-in's lockout counter, which must be saved even though the command then fails. | dev |
| Domain events are dispatched by the unit of work to `IDomainEventSubscriber`s built in the command's own scope, not through a nested mediator call. | A nested mediator call gets its own scope and connection, so it would run outside the transaction CODE_STYLE requires. Wolverine also forbids the service location a generic lookup would need. | dev |
| Correction requests are their own aggregate; `CorrectionRequested`, `CorrectionRequestAccepted` and `CorrectionRequestDeclined` update the timesheet's state and history in the same transaction. | Glossary: Accept reopens through an event in one transaction. | dev |
| Timesheet history is an append-only `timesheet_history` table keyed by (timesheet, position). Entry rows carry before and after snapshots, including punched values. | view-timesheet.feature asks for every transition and entry change, deleted entries included. | dev |
| "On behalf of" isn't stored: a Submitted row whose actor isn't the owner was submitted on their behalf. | Derivable, so not stored. | dev |
| Approval keeps the approver, the time and the owner's team at that moment; reopening clears the current approval, and history keeps the old one. | The CSV export shows the team at approval even after a move. | dev |
| Access is derived in one place (`TimesheetParties`) from permissions: the owner, the owner's current team supervisor (`timesheets.review-team`), or admins (`timesheets.review-all`, `timesheets.manage`). | Code checks permissions, never roles (glossary), and the approver follows the employee's current team. | dev |
| New permissions: `teams.manage`, `timesheets.review-team` (supervisors), `timesheets.review-all` and `timesheets.manage` (admins). | Each one gates a separate set of screens and actions. | dev |
| Overlaps are checked against the previous week, the next week and the week holding the open entry, which runs until now. | Entries last at most 16 hours, so nothing further away can overlap. | dev |
| Edits and deletes send the timesheet version they were based on; a stale one gets 409 `ConcurrencyConflict`. | edit-entry.feature, "two devices". | dev |
| Manual entries record the request's IP and user agent as their source. | Columns are required, and it helps audits. | dev |

## 2026-10-30: Demo data

Larger decision: [ADR 0005](docs/adr/0005-demo-data-and-daily-reset.md) (app-seeded demo data, daily reset, Development and Demo environments only).

| Decision | Why | By |
|---|---|---|
| 2 admins, 3 supervisors (one per team), 15 employees (5 per team), plus an archived "Old Store". Names follow the specs (Eve, Sam, Oli, …). | The user asked for 3 supervisors and 15 employees in teams; spec names make scenarios easy to recognise. | user / dev |
| Scenarios: clocked in now, clocked out today, forgot to clock out (open 20 h), not started; last week Submitted, Approved or Draft; two weeks ago Approved; a >10 h entry; 3 manual changes; one pending, one deactivated, one locked-out user. | "Cover most if not all scenarios" with what the schema holds today; returns and corrections join the seed with their features. | dev |
| Added once their features existed: approved weeks record the approver (team supervisor, or Ada for supervisors), time and team, with Submitted/Approved history; Hal's last week is returned with a comment; Jon has a pending correction on two weeks ago. | Fills the approval line, the CSV export, the returned banner and the corrections queue on first sign-in. | dev |
| All demo accounts share the password `clockin-demo`; the sign-in page lists 7 featured accounts that fill the form when picked. | Agreed with the user. | user |
| The dev database is a demo database too: the first start after this change wipes it. | Development runs with `Demo:Enabled` true; turn it off for a database you keep. | dev |
| The `users.team_id` foreign key exists on PostgreSQL only. | SQLite can't add a foreign key to an existing table; the domain keeps it valid there. | dev |

## 2026-10-30: Teams (domain and persistence)

| Decision | Why | By |
|---|---|---|
| Teams are built down to persistence (create team, move employee) before any API or screen, so the demo seed can place users in teams. | Agreed with the user; endpoints and admin screens come with the admin slice. | user |
| An employee's team is `users.team_id`; supervisors and admins have none. | Glossary: membership is a `TeamId` on the user, and the approver is derived from the team. | dev |
| Names are compared through a stored `normalized_name` (trimmed, upper-cased) with a unique index, archived teams included. | create-team.feature; the index also wins races the handler's check can't. | dev |
| A partial unique index keeps a supervisor to one active team. A lost race surfaces as 409 `ConcurrencyConflict`. | Same pattern as one open entry per user. | dev |

## 2026-10-30: Frontend sign-in, set-password and clock screens

| Decision | Why | By |
|---|---|---|
| On app start the session is restored with one silent refresh; the access token stays in memory only. | CODE_STYLE.md; a reload keeps the user signed in while the refresh cookie lasts. | dev |
| Interceptor order: error (outermost), correlation ID, auth. Auth refreshes once on a 401 and retries; a failed refresh sends the user to sign in. | The auth interceptor must see the raw 401 before it turns into an `ApiError`. | dev |
| A request can silence the error snackbar for rule codes it explains itself, through the `SILENT_ERRORS` context token (`'*'` for all). | Sign-in, set-password and EntryTooLong show their errors inline. | dev |
| Snackbars show the server's English `detail` for 403/404/409/422 (`ponytail:`); the codes the screens handle have Spanish text. | Translating every rule code client-side waits until more screens exist. | dev |
| `/` redirects to `/clock` with the punch permission, otherwise to `/home`, a placeholder for admins. | Admins can't punch; their pages come later. | dev |
| Auth pages are top-level routes (`/sign-in`, `/set-password`), not a lazy `''` parent. | An empty-path lazy parent swallowed `/` and rendered nothing. | dev |
| Times are 24h in the company timezone from the API response (`ponytail:`); the 12h preference comes with `update-preferences.feature`. | No preferences endpoint yet. | dev |
| The EntryTooLong card explains the 16-hour limit but has no "edit the entry" link yet. | The edit-entry screen doesn't exist yet. | dev |
| Material Symbols is registered as the default ligature font and icons use `fontIcon`. | Ligature text inside buttons failed axe's label/name check, and the default `material-icons` class didn't match the loaded font. | dev |
| The dev server proxies `/api` to `http://localhost:5244`. | Same origin in development, so the SameSite=Strict refresh cookie works. | dev |
| E2E tests mock the API with `page.route` and a fixed clock. | Fast and deterministic; the API has its own HTTP tests (ADR 0004). | dev |

## 2026-10-30: Access links and the first admin

| Decision | Why | By |
|---|---|---|
| Access-link tokens are `{userId}.{secret}` like refresh tokens, through one shared `OpaqueToken` helper in the domain; the hash and expiry live on the `User`. | One token format, stored hashed, found by id. A newer link overwrites the hash, so older ones stop working. | dev |
| The seeded admin's activation link goes to the log at Warning under the `Audit` category, with the path `/set-password?token=…`. | `seed-first-admin.feature` names the deploy log as the delivery channel; it's the one exception to "never log tokens". Treat that log line as a secret. | dev |
| The first-admin seed runs as a hosted service after Wolverine starts, reading `FirstAdmin:Email`; a missing email with no admin stops startup. | The mediator only works on a started host; the spec says startup fails. | dev |
| Remote environments must run migrations before the app starts, since the seed needs the `users` table. | Migrations run as a separate deploy step there (CODE_STYLE.md). | dev |
| Admins get the `users.manage` permission; `POST /api/users/{id}/access-links` requires it. `POST /api/auth/set-password` is anonymous and rate-limited with sign-in. | Spec permissions; the link is the credential for setting a password. | dev |
| Development seeds `admin@clockin.local`. | So a fresh local database is usable straight away. | dev |

## 2026-10-30: Authentication in the API and clock endpoints

| Decision | Why | By |
|---|---|---|
| Access tokens are issued in `ClockIn.Api/Auth` (ES256, `JsonWebTokenHandler`), not in Infrastructure. | STACK.md: "Own `Auth` feature in `ClockIn.Api`"; the API holds the only private key and validates with it. | dev |
| Development generates a throwaway P-256 key when `Auth:SigningKey` is empty; every other environment fails at startup without one. | CODE_STYLE.md: a development key is generated and never committed. | dev |
| Permissions live in the domain (`Permissions.For(role)`); one authorization policy per permission, same name. Admins have none yet. | Glossary: a role is a bundle of permissions; code checks permissions, never roles. | dev |
| Every request loads the user to check status and security stamp, then adds permission claims; no cache yet (`ponytail:`). | CODE_STYLE.md: permissions are checked on every request. Cache when profiling says so. | dev |
| Anonymous requests to unknown routes get 401, not 404. | Effect of the authentication fallback policy; it doesn't reveal which routes exist. | dev |
| Sign-in and refresh share a fixed-window limit per client IP (`Auth:SignInPermitsPerMinute`, default 10). | CODE_STYLE.md: strict limits on login and refresh. | dev |
| Routes: `POST /api/auth/login`, `/refresh`, `/logout`; `GET /api/me`; `POST /api/time-entries/clock-in` and `/clock-out` (200 with the entry). | Punches are actions on the user's open entry, not a resource the client names. | dev |
| A clock-in that loses a race to a simultaneous punch returns 409 `EntryAlreadyOpen`. | `clock-in.feature` @api scenario; the repository's `ConcurrencyConflict` is translated by the handler. | dev |
| The company timezone is `Company:TimeZone` (default `America/Bogota`), validated at startup; the app's `TimeProvider` uses it as its local zone. | Glossary: one company timezone from deploy config. | dev |
| Client IP is the direct peer address (`ponytail:`). | Enable forwarded headers when deploying behind a reverse proxy. | dev |
| `AppDataConnection` is registered by constructor, not a factory lambda. | Wolverine's generated handlers cannot call factory lambdas for scoped services. | dev |
| The two `@api` scenarios so far (rate limit, simultaneous clock-ins) are plain xUnit HTTP tests. | Confirmed by [ADR 0004](docs/adr/0004-http-tests-instead-of-reqnroll.md), which supersedes ADR 0002. | user |

## 2026-10-30: Accounts persistence and infrastructure

| Decision | Why | By |
|---|---|---|
| `PasswordHasher<T>` and logging come from `FrameworkReference Microsoft.AspNetCore.App` in Infrastructure; no `Microsoft.Extensions.Identity.Core` package. | The shared framework already ships it; STACK.md's approved package is the same code. | dev |
| A lost race on a session save (two refreshes with one token) is a 401, not a 409. | Only one refresh may rotate a token; the loser's token is no longer current anyway. | dev |
| No foreign key from `timesheets.owner_id` to `users` yet. | SQLite can't add a constraint to an existing table; add it when the tables are next rebuilt. | dev |
| Test helper `Scopes` resolves each repository from its own DI scope and disposes them. | Mimics separate requests and stops leaking connections. | dev |

## 2026-10-30: Sign-in, refresh and log-out (domain and application)

| Decision | Why | By |
|---|---|---|
| A `Session` aggregate per signed-in device; the refresh token is `{sessionId}.{secret}`, and only the secret's SHA-256 is stored. | A token whose session exists but whose secret is no longer current is a reused, rotated token. Reuse detection needs no history of old tokens. | dev |
| Refresh-token reuse revokes every session of the user with one bulk update. | `refresh-session.feature`; loading each session as an aggregate adds nothing. | dev |
| Sign-in failures return a 422 `InvalidCredentials`, as the specs' error table says, and log-out always succeeds. | The specs map "breaks the rule" to 422; a log-out that can fail would reveal whether a token was valid. | dev |
| Unknown emails still run a password verification (against a dummy hash, inside the hasher). | Response times don't reveal which emails have accounts. | dev |
| Sign-in saves the failure count before rejecting (`ponytail:` note). | A future transaction-per-command middleware must not roll it back. | dev |
| `IAuditLog` port with one method per security event. | Tests can check each event; the implementation writes to the `Audit` logger category (`CODE_STYLE.md`). | dev |
| New `UnauthorizedException` → 401. | The "it is unauthorized" outcome in the specs had no exception yet. | dev |
| `FluentValidation` referenced directly by Application, pinned at 12.0.0. | It was only transitive through Wolverine; validators live in Application. Same version, so nothing changes at runtime. | dev |
| Email check is deliberately loose: one `@` with text on both sides, no spaces, at most 254 characters. | The activation link reaching the person is the real validation. | dev |

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

Larger decisions: [ADR 0001](docs/adr/0001-internationalization.md) (i18n), [ADR 0002](docs/adr/0002-reqnroll-for-api-specs.md) (Reqnroll), [ADR 0003](docs/adr/0003-adopt-baseline-code-style-and-stack.md) (baseline and ADR sync), [ADR 0004](docs/adr/0004-http-tests-instead-of-reqnroll.md) (no Reqnroll; supersedes 0002), [ADR 0006](docs/adr/0006-repository-suffix-on-variables.md) (repository variable names), [ADR 0007](docs/adr/0007-suffixes-for-static-classes.md) (static class suffixes), [ADR 0008](docs/adr/0008-shared-handler-logic-in-injected-services.md) (shared handler logic as a service).
