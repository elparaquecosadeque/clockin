# Development public API

`ClockIn.PublicApi` runs independently alongside the existing SPA API. It uses the
same accounts, database and application handlers, without Wolverine. It refuses
to start outside `Development`; it is not part of Docker or production deployment.
See [ADR 0010](adr/0010-development-public-api-with-direct-handlers.md) and the
[feature specification](specs/public-api/personal-time.feature).

## Prepare once, then run

From the repository root, start the existing API to migrate and seed its local database:

```powershell
dotnet run --project backend/src/ClockIn.Api --launch-profile http
```

Wait for startup to finish. You may stop it or leave it running. In another terminal:

```powershell
dotnet run --project backend/src/ClockIn.PublicApi --launch-profile http
```

PublicApi listens at **http://localhost:5245**. Open **http://localhost:5245/scalar/v1**
for Scalar; the OpenAPI document is at **http://localhost:5245/openapi/v1.json**.
The existing API stays on port 5244 and the Angular proxy stays unchanged.

The default database is `backend/src/ClockIn.Api/clockin.dev.db`, resolved from
PublicApi's content root, not the terminal directory. A missing file is not created.
PublicApi never migrates, seeds or resets, even if `Database:MigrateOnStartup` is set.
Rerun the existing API when migrations or fresh demo data are needed.

For a custom database, configure **both hosts** with matching `Database__Provider`,
`Database__ConnectionString` and `Company__TimeZone` environment variables. Prepare
it through the existing API first. Never point this demo at real data.

## Authentication and operations

Sign in with `mia@clockin.demo` / `clockin-demo`. Mia starts without an open entry
and has an ended Draft week suitable for editing and submitting. Eve and Sam may
already be clocked in. Admins cannot use these personal-time endpoints.

| Method | Route | Body / result |
|---|---|---|
| POST | `/api/v1/auth/login` | Email/password → `accessToken`, `expiresAt` |
| POST | `/api/v1/time-entries/clock-in` | No body → entry with ID and start |
| POST | `/api/v1/time-entries/clock-out` | No body → closed entry |
| GET | `/api/v1/timesheets/mine/{weekStart}` | Monday `YYYY-MM-DD` → entries, status, history, version |
| PUT | `/api/v1/time-entries/{entryId}` | Start, nullable end, note, timesheet version → 204 |
| POST | `/api/v1/timesheets/mine/{weekStart}/submit` | No body → 204 |

Supply `Authorization: Bearer <accessToken>` except on login. Tokens expire after
15 minutes; sign in again to renew. Restarting PublicApi invalidates its tokens.
SPA tokens cannot be used here, or vice versa. Password/activation/session checks
are shared; no refresh secrets or cookies are returned.

In Scalar, open login and choose a prefilled request example: **Mia (Employee)**,
**Sam (Supervisor)** or **Ada (Admin)**. Mia is first and recommended for the full
workflow. Send the request, copy `accessToken`, then select Bearer authentication
and paste it. The UI shows the v1 request/response schemas and protected operations.

The week parameters and edit request are prefilled from Mia's seeded previous week,
including a real entry ID, timestamps, explanation and current timesheet version.
Punch in/out need no body. Reload Scalar after an edit or demo reset to refresh
these values. If the example week was already submitted, its existing lock rules
still apply; the documentation does not reset data or sign in automatically.

Only Draft weeks can change. Notes are required and limited to 500 characters.
Edits preserve original punches and history, reject overlaps/future times and use
the **containing timesheet's** latest version. Read the week again after a write.
An open entry can be manually closed; a closed entry cannot be reopened.
Submitting requires an ended week without open entries; empty weeks are allowed.
All dates follow company time (`America/Bogota` by default).

Errors use the same ProblemDetails and correlation IDs as the SPA API: 400 field
validation, 401 invalid/expired session, 403 permission/ownership, 404 missing
resource, 409 stale version, 422 business rule, 429 sign-in rate limit.

## Bruno and sample JSON

Open [bruno/ClockIn.PublicApi](../bruno/ClockIn.PublicApi/) as a collection and select
the **Local** environment. Run requests in numerical order:

1. Login captures a runtime bearer token and calculates the previous Monday from
   server time. Token values are not written into the environment file.
2. Punch in, then punch out. These affect the current week.
3. Read the previous week. This captures its version and its first entry's ID/times.
4. Edit that entry with an explanation. The sample preserves its times; change them
   if desired. Read again before another edit to refresh the version.
5. Submit the previous week. It is now locked; rerunning the collection needs fresh
   demo data or a different Draft week/account.

Change the runtime `weekStart` if you want to read the current week instead. Do not
submit that week: the existing rules correctly reject it before it has ended.

Standalone JSON bodies are in [examples](../bruno/ClockIn.PublicApi/examples/).
Replace the edit timestamps and version with values from your own GET response.
The punch and submit endpoints have no JSON body.

The existing API's daily demo reset deletes sessions and rebuilds entries. Both
clients must sign in again and read fresh IDs/versions afterwards. PublicApi alone
does not schedule resets, so demo data can age while the existing API is stopped.
