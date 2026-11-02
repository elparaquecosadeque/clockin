# 0010. Development PublicApi invokes application handlers directly

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

The owner wants a second way to exercise the existing personal-time workflow with
Scalar and Bruno, locally. There is no external integration consumer or production
deployment. The baseline's machine credentials and Wolverine dispatch add work
without serving that goal. The scope and exceptions were agreed in the planning interview.

## Decision

- Add `ClockIn.PublicApi`, a separate ASP.NET controller host with `/api/v1` routes.
  It refuses to start outside Development and is omitted from deployment.
- Expose only login, punch in/out, read my week, edit my entry and submit my week.
  The `PunchTime` policy excludes admins; existing application ownership checks
  restrict employees and supervisors to changing their own entries.
- Controllers inject and call existing application handlers directly. They execute
  the existing FluentValidation validators explicitly. A scoped MVC action filter
  wraps the four time-changing actions in the existing unit of work, including
  event dispatch and rollback. Login is deliberately outside a transaction so
  failed-sign-in counters persist. No Wolverine dependency in the new host.
- Extract shared token, current-user and HTTP helpers into `ClockIn.Hosting`.
  It references Application, never either host, Persistence or Wolverine.
  Existing SPA endpoints retain their routes, audience and behavior.
- Login uses existing human demo accounts, password hashing, lockout and sessions.
  Issue a 15-minute ES256 JWT with audience `ClockIn.PublicApi`, a process-local
  signing key and the existing identity/session claims. Validate the current user,
  security stamp and session on each request. No refresh token/cookie or logout
  endpoint in this slice. Only the access token and expiry leave the host.
- Keep built-in OpenAPI generation, add the approved `Scalar.AspNetCore` package
  for interactive documentation, and provide a version-controlled Bruno collection.
- Share the prepared database. The existing Api remains the sole migration,
  seed and daily-reset owner. PublicApi resolves the existing Api's SQLite file
  explicitly by default and opens it without creating a missing file. Either host
  can run without the other after preparation. PostgreSQL remains configurable.
- Reuse application results as v1 response contracts. A breaking change to these
  results must consider PublicApi versioning as well as the SPA.

## Consequences

This supersedes the baseline's mediator-only controllers, public-key-only PublicApi,
client credentials and mandatory refresh flow **for this development slice only**.
Production machine authentication remains deferred and requires a new decision.
PublicApi tokens die on restart; users sign in again after expiry. Session resets
and data changes affect both hosts. Each host has its own sign-in rate limiter;
the account lockout is shared. The new host uses ASP.NET logging locally and adds
no hosted logging/deployment configuration. Scalar's UI is outside the JSON-only
CSP, while `/api` responses keep the existing restrictive security headers.
