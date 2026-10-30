# Glossary

The ubiquitous language of ClockIn. Code, specs and UI use these words and no synonyms.

## People

| Term | Meaning |
|---|---|
| **User** | Anyone with an account. Never deleted. Has exactly one role. |
| **Role** | `Employee`, `Supervisor` or `Admin`. A role is a bundle of permissions; code checks permissions, never roles. |
| **Employee** | Tracks time. Belongs to exactly one team. |
| **Supervisor** | Tracks time and leads at most one team. Not a member of the team it leads. Its own timesheets are approved by any admin. |
| **Admin** | A position of trust. Does not track time and belongs to no team. Manages users and teams, approves supervisors' timesheets, reopens approved timesheets. The Admin role is never granted or removed by a role change. |
| **User status** | `PendingActivation` (no password yet), `Active`, `Deactivated`. |
| **Lockout** | Temporary block after 5 consecutive failed sign-ins, for 15 minutes. Separate from status. |
| **Access link** | A one-time link an admin generates and shares by hand: an *activation link* for a pending user, a *reset link* for an active one. Valid for 72 hours, stored hashed, replaced by the next one. |

## Teams

| Term | Meaning |
|---|---|
| **Team** | A named group with exactly one supervisor and any number of employees. Archived, never deleted. Names are unique, archived ones included. |
| **Approver** | Who decides on a timesheet or correction request: the supervisor of the owner's team for employees, any admin for supervisors. Derived at decision time, never stored on the user. A user is never the approver of their own timesheet. |
| **Queue** | What an approver has to decide: Submitted timesheets and Pending correction requests. |

## Time

| Term | Meaning |
|---|---|
| **Company time** | The single timezone of the system, from deploy config. Default `America/Bogota` (UTC−5, no DST). Everything is stored in UTC. |
| **Week** | Monday 00:00 to Sunday 23:59:59 in company time, named by its Monday (`2026-10-26`). |
| **Punch** | A clock-in or clock-out made live. Records time, IP and user agent. |
| **Entry** | A span of worked time: a start and, once closed, an end. Belongs to the week it started in, permanently. |
| **Open entry** | An entry with no end yet. At most one per user. |
| **Punched values** | The start and end recorded by punches. Kept forever, even after edits. |
| **Manual change** | Adding, editing or deleting an entry by hand. Flagged, needs a note, recorded in history. |
| **Timesheet** | One user's week. Holds the entries and the approval state. Created as a Draft on the first entry or on submit. Stores its `WeekStart`, and the team at approval time. |
| **Timesheet status** | `Draft` (editable), `Submitted` (waiting for the approver), `Approved` (final). "Not started" in reports means no timesheet exists. |
| **Return** | Approver sends a Submitted timesheet back to Draft with a comment. |
| **Reopen** | An Approved timesheet goes back to Draft, by an admin with a reason or by an accepted correction request. |
| **Correction request** | The owner asks the approver to reopen a Submitted or Approved timesheet. `Pending`, `Accepted` or `Declined`. At most one Pending per timesheet, and it blocks approve, return and reopen until decided. |
| **History** | Append-only record of every timesheet transition and every manual change, with actor, time and comment or reason. |

## Rules in numbers

All are domain constants, not configuration.

| Rule | Value |
|---|---|
| Maximum entry length | 16 hours |
| "Long entry" exception | over 10 hours |
| "Many manual changes" exception | 3 or more |
| Password length | 12 to 128 characters |
| Lockout | 5 failures, 15 minutes |
| Access link validity | 72 hours |
| Access token / refresh token | 15 minutes / 12 hours sliding |
| Notes, comments, reasons | up to 500 (notes) or 1000 (comments, reasons) characters |

## Aggregates

| Aggregate | Owns | Notes |
|---|---|---|
| `User` | role, status, team, credentials, preferences | Team membership is a `TeamId`. |
| `Team` | name, supervisor, archived flag | |
| `Timesheet` | its entries and its history | Entries live inside, because whether they can change depends on the timesheet's status. |
| `CorrectionRequest` | status, reason, decision | Accepting raises `CorrectionRequestAccepted`; a handler reopens the timesheet in the same transaction. |

Rules that span aggregates (one open entry per user, no overlaps across a week boundary) are checked by the handler and backed by a database constraint (a unique partial index on open entries), so concurrent requests can't break them.
