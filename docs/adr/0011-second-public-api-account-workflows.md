# 0011. SecondPublicApi exposes existing account workflows

- **Status:** Accepted
- **Date:** 2026-11-02

## Context

The owner requested another small ASP.NET controller host for public information,
authentication, identity and admin-only user creation. The scope was confirmed in
an interview. Existing handlers already implement account activation, password
rules, permissions and team requirements.

## Decision

- Add `ClockIn.SecondPublicApi` at port 5246 with `/api/v1`, Scalar and Bruno.
  Apply the Development-only guard and database ownership from
  [ADR 0010](0010-development-public-api-with-direct-handlers.md). No deployment.
- Expose exactly info (name/version), login, activation by secret token,
  authenticated `/me`, and `ManageUsers`-protected creation.
- Invoke existing handlers directly with explicit existing validators. Retain
  their transaction semantics: these account commands are not `ITransactional`;
  rejected login must still persist failed-sign-in counts.
- Reuse shared user/session validation and 15-minute ES256 tokens. Each development
  public host has its own ephemeral key and audience, here `ClockIn.SecondPublicApi`.
  Common authentication/rate-limit registration lives in `ClockIn.Hosting`;
  neither public host depends on the other or Wolverine.
- Accept existing roles and return the existing activation result. Employees
  require an active team; supervisors/admins have no team. Anonymous activation
  still requires the secret, expiring token.
- Scalar prefills Ada/Mia logins, a supervisor and the real active Office team ID
  for an employee. It never generates activation tokens; callers copy creation's
  response token. Bruno chains responses and generates unique emails.

## Consequences

No new domain rules, migrations or packages. The SPA API remains the database
preparation/reset owner. New accounts affect all hosts and disappear on demo reset.
Refresh/logout, user listing, editing and deletion are excluded. The first
PublicApi's personal-time scope remains unchanged.
