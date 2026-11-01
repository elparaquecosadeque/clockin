# Security audit, run 2

- **Date:** 2026-11-01
- **Reviewed commit:** `7e45769`, plus `21d2048`
- **Profile:** standard. Reviewers ran in waves until two independent checks found no area left uncovered.
- **Scope:** the .NET API (`backend/src`), the Angular app (`frontend/src`), package manifests and CI (`.github`). Run 1 had deferred supply chain and resource exhaustion, so those came first.
- **How:** source reading only. No code was run and no live system was contacted.

## Result

No confirmed vulnerability. The authorization model held up again:

- Access to timesheets, reports and exports is decided from stored records.
- Deactivating a user revokes every session and link at once.
- A change of role or team takes effect on the next request.
- Private responses can't be served from a shared cache.
- The set-password link isn't leaked to other sites.

The review left four leads. Each is reachable from the source but needs a test or a deployment fact to confirm. None of them lets anyone see data they couldn't already see. They concern concurrency and capacity.

## Items and status

**Status key:** *Open* waits for a fix or a check. The other statuses are as in [run 1](audit-run-1.md).

### Leads that need confirming

| Item | Status |
|---|---|
| Two admins deactivating each other can still leave no active admin. Run 1's fix (`f4d5896`) only covers the case where both requests reach the database at once. The handler never re-checks that the acting admin is still active. | Open: re-check the actor inside the transaction |
| Requests waiting on the per-user lock from `9b0d25d` each hold a database connection. One user sending many entry changes at once could use up the shared pool. | Open: give up on the lock quickly (`pg_try_advisory_xact_lock` or `lock_timeout`), and set a command timeout |
| An employee can add entries to any past week and any number of very short entries. Every supervisor's to-dos and reports load all of them, on every page change. | Reporting amplification mitigated in source: SQL counts/existence checks, targeted open-entry reads and 50-item server-side pages for review queues and exceptions. Personal draft links are read without entries/history. Backfill remains supported; storage growth and production-scale query costs remain unvalidated. |
| A sign-in email of any length is logged in full. A few large requests could fill the day's log file, and later audit events would then be dropped. | Fixed in source: command validation rejects emails over 254 characters before logging. The sign-in form enforces the same limit, explains it in both locales and displays server field errors. Boundary, HTTP and form regression tests cover the change. Password and request-body limits remain separate hardening work. |

### Supply chain

| Item | Status |
|---|---|
| The CI workflow doesn't restrict its token to read-only | Fixed: the default token has `contents: read`; only the GitHub Pages deployment job adds `pages: write` and `id-token: write`. |
| CI actions are pinned by tag, not by commit | Open, until the workflow gains secrets or a publish step |
| No NuGet lock file | Open |
| The test database image uses a movable tag | Open |

### Sessions and limits

| Item | Status |
|---|---|
| A session that keeps refreshing never expires | Open: add an absolute lifetime |
| On IPv6, one client can rotate addresses to get around the sign-in rate limit | Phase 2, with the proxy |
| No request-body cap, request timeout or database command timeout | Open |

### Workflow and data

| Item | Status |
|---|---|
| Opening the week of 9999-12-27 returns 500 instead of 400 | Open |
| Creating a user can race archiving their team | Open |
| A clock-in racing a deactivation can leave the deactivated user with an open entry | Open |
| The SQLite provider is accepted outside Development, where the per-user lock is skipped | Open: refuse SQLite outside Development |

### Client and headers

| Item | Status |
|---|---|
| API responses carry no `Cache-Control: no-store` | Open |
| The app's host sends no referrer policy or content security policy | Phase 2, with the app's host |
| Fonts load from Google | Open: self-host them |
