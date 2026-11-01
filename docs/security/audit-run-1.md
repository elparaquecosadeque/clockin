# Security audit, run 1

- **Date:** 2026-10-31
- **Reviewed commit:** `58a623f`
- **Profile:** quick (one pass of reviewers, then one check for missed areas). It is a partial review, not a full-coverage claim.
- **Scope:** the .NET API (`backend/src`), the Angular app (`frontend/src`), package manifests and CI (`.github`).
- **How:** source reading only. No code was run and no live system was contacted.

## Result

No confirmed vulnerability. The authorization model held up:

- Every endpoint requires sign-in unless it is explicitly anonymous (sign-in, demo, health).
- Each request reloads the user, their role and security stamp from the database, so a deactivated user or changed role takes effect at once.
- Access to a timesheet, report or correction is decided from stored records (owner, team, approver), never from ids the client sends.
- The refresh token is an `HttpOnly; Secure; SameSite=Strict` cookie limited to `/api/auth`; every other call uses an in-memory access token.

Two leads on the sign-in endpoint needed a test to confirm, and the review listed hardening notes. Every item and what became of it is below.

## Items and status

**Status key:** *Fixed* links the commit. *Accepted* means the risk was weighed and kept, with the reason in [`DECISIONS.md`](../../DECISIONS.md). *Phase 2* means it waits for deployment decisions; see the [backlog](../backlog.md).

### Leads that needed confirming

| Item | Status |
|---|---|
| A sign-in email containing line breaks could forge lines in the audit log | Fixed in `1d63cdd` |
| Two concurrent wrong-password sign-ins could reveal whether an email is registered (409 instead of 401) | Fixed in `1d63cdd` |
| Two entry changes at the same moment in neighbouring weeks could save overlapping entries | Rejected as a security finding (only people who may already edit those hours can trigger it), fixed anyway in `9b0d25d` with a per-user lock |

### Access and identity

| Item | Status |
|---|---|
| The former supervisor of an archived team kept approval rights over members left in it | Fixed in `2ebaf34` |
| An admin can issue a password link for another admin | Accepted: admins are equally trusted, and it is the only way back in for an admin who forgot their password |
| Two admins deactivating each other at the same moment could leave no active admin | Partly fixed in `f4d5896`; the remaining case is open in [run 2](audit-run-2.md) |
| Concurrent admin actions on users and teams could leave a team led by a deactivated or demoted user | Fixed in `020515e` |
| Unlocking a user wrote no audit entry | Fixed in `f4d5896` |

### Sessions and credentials

| Item | Status |
|---|---|
| After logout, the access token stayed valid for up to 15 minutes | Fixed in `f4d5896` ([ADR 0009](../adr/0009-session-id-in-the-access-token.md)) |
| Wrong current passwords on password change didn't count towards lockout | Fixed in `f4d5896` |
| The first admin's activation link is logged while that admin is pending | Phase 2: restrict log access and retention |
| Two tabs refreshing at the same moment signed the user out everywhere | Fixed in `a94c603` |

### Deployment

| Item | Status |
|---|---|
| Behind a reverse proxy, all clients would share one rate-limit bucket | Phase 2 |
| No HTTPS redirect or HSTS | Phase 2: depends on whether the API or the proxy terminates TLS |
| No anti-framing or content security policy headers | Fixed on the API in `2ebaf34`; the app's host is phase 2 |
| `.gitignore` didn't cover secret files (`.env`, keys, certificates) | Fixed in `d9f4123` |
| No NuGet lock file; CI actions pinned by tag | Covered by run 2 (supply chain) |
| In the demo, the shared password covers the admin accounts | Accepted ([ADR 0005](../adr/0005-demo-data-and-daily-reset.md)): the demo holds no real data and resets every 24 hours |

### Client

| Item | Status |
|---|---|
| A crafted owner id in the review page URL could redirect an API request | Fixed in `2ebaf34` |
| The set-password `?token=` stays in browser history | Accepted |

### Workflow and data

| Item | Status |
|---|---|
| Unknown ids answer 404 and other people's ids 403, revealing that an id exists | Accepted: ids are random GUIDs that can't be guessed |
| Approve and return don't check the version the reviewer saw | Accepted |
| Clock-out and recording a declined correction skipped the timesheet status check | Fixed in `7e45769` |
| Editing an open entry skipped the 16-hour limit | Fixed in `7e45769` |
| Creating a team with no name returned 500 instead of 400 | Fixed in `d9f4123` |
| A link-local IPv6 address could be too long for the punch's IP column | Fixed in `7e45769` |
| CSV export doesn't neutralize formulas behind leading spaces or full-width signs | Accepted |

## Not covered by this run

Supply chain (packages, lock files, CI permissions) and resource exhaustion (request sizes, unbounded inputs) were deferred to run 2.
