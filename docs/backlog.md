# Phase 2 backlog

Agreed during the v1 requirements interview (2026-10-30) as out of scope for v1.

| Item | Why it waits | Hook in v1 |
|---|---|---|
| Notifications and a pending-tasks inbox | v1 derives to-do badges from stored state | Domain events already exist for every transition |
| Email delivery (invites, resets, notifications) | Admins share links by hand | The access-link token stays the same; only delivery is added |
| Server-side PDF generation | Needs a library and a licence review (ADR) | Print views cover human-made PDFs |
| MFA, at least for admins | Needs a package or hand-written TOTP | Security stamp and per-request checks are in place |
| Remote deploys through Jenkins | v1 runs CI on GitHub Actions | Build scripts stay CI-agnostic |
| Punch restrictions (IP allow-list, geofence, kiosk) | Only if abuse appears; each needs an ADR | Each punch records IP and user agent |
| Data retention policy | Depends on labour law in your jurisdiction | Everything is kept |
| Rate limiting behind a reverse proxy | Depends on the deploy topology; security audit run 1 | Configure forwarded headers with the proxy's address, so limits key on the client's IP and not the proxy's |
| Security headers on the SPA's host (CSP, frame options) | The repository has no frontend hosting yet; security audit run 1 | The API already sends them; copy the policy to the host |
| Restrict access and retention of the log files | The first-admin activation link is logged while that admin is pending (by design); security audit run 1 | Logs stay under `logs/`, 31 days by default |
