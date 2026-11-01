# 0009. The access token names its session

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

The baseline access token carries `sub`, `security_stamp` and the audience only. Logging out revoked the refresh session, but the access token stayed valid until it expired, up to 15 minutes later. Security audit run 1 noted it. Changing the security stamp on logout would end it, but it would also sign the user out on every other device.

## Decision

- The access token also carries `sid`, the id of the session it was issued for. It identifies a session, like `sub` identifies a user; it carries no permission.
- `CurrentUserCheckHelper` loads that session on every request and rejects the token when the session is unknown, belongs to another user, was revoked or has expired.
- Logging out, deactivating a user, reusing a refresh token and a lockout from password change all revoke sessions, so they now end the matching access tokens at once.

Rejected:
- Rotating the security stamp on logout: it ends every device's session, not just this one.
- Shortening the access token to 5 minutes: it only narrows the window, and costs three times the refreshes.

## Consequences

- One more database read per authenticated request, next to the user lookup that was already there.
- Tokens issued before this change have no `sid` and are rejected; users sign in again once.
