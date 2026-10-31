# 0005. Demo data, seeded by the app and reset daily

- **Status:** Accepted
- **Date:** 2026-10-30

## Context

The product owner wants a shared demo: 3 supervisors and 15 employees in teams, with timesheets and entries that show most scenarios (clocked in now, clocked out today, forgot to clock out, submitted and approved weeks, flagged entries). Visitors change that data, so it must be rebuilt every 24 hours, and the rebuild can be switched off.

The baseline says development data is a FluentMigrator `[Profile("Development")]` class in `Persistence/Seeds/` (STACK.md), runs only in Development, and remote environments never migrate on startup (CODE_STYLE.md). That doesn't fit:

- The data must be **relative to now** (an entry that is open *today*), so it can't be fixed SQL written once.
- It must be **wiped and rebuilt on a timer** while the app runs, which a migration profile can't do.
- It must run in a **remote demo** deployment, not only on a laptop.
- Passwords must be hashed with the real hasher, which lives in Infrastructure.

## Decision

- **Demo data is C# code in `Persistence/Seeds/DemoSeed.cs`** that wipes every table and inserts the data in one transaction, with dates computed from the app's `TimeProvider` in company time. It replaces the FluentMigrator-profile rule for development data.
- **A hosted service in the API (`Demo/DemoDataService`) runs it**: on startup when the database was never seeded, and whenever the last seed is older than 24 hours (checked hourly). The seed time is kept in a one-row `demo_seeds` table.
- **Configuration:** `Demo:Enabled` turns demo data on; `Demo:DailyReset` (default `true`) turns the daily rebuild off, after which the data is seeded once and then kept.
- **Only in the `Development` and `Demo` environments.** Startup fails if `Demo:Enabled` is true in any other environment, so a real Production deployment can't be wiped by a wrong flag. The demo deployment runs with `ASPNETCORE_ENVIRONMENT=Demo`, which also migrates on startup like Development, as there's no separate deploy step for it.
- **Every demo user shares one known password.** `GET /api/demo/accounts` (anonymous, 404 when demo data is off) lists them, and the sign-in page shows that list so visitors can pick a role.
- **The seed grows with the features:** it fills only tables that exist. Returned timesheets, correction requests and approver history are added when those features land.

Rejected:

- **A Production flag, default off.** Simpler, but one wrong setting deletes real data.
- **A FluentMigrator profile plus an external cron job** that drops the database. Two moving parts, fixed dates, and the passwords would need a precomputed hash.

## Consequences

- Anyone can sign in to a demo as any role; it must never hold real data. The sign-in page says so.
- A reset deletes every session, so visitors are signed out once a day.
- The data ages between resets: an entry seeded as "clocked in 2 hours ago" is 20 hours old the next morning, and clocking out then hits the 16-hour rule. A daily reset keeps this to one day.
- `DemoSeed` writes records directly, below the domain, so it must be kept in line with the aggregates' rules. A test seeds a database and loads every seeded user and timesheet through the repositories.
