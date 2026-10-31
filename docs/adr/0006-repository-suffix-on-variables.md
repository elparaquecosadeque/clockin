# 0006. Repository variables end in `Repository`

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

The baseline names an injected repository after what it holds (`IOrderRepository orders`, CODE_STYLE.md). In handlers that also hold the loaded aggregates and their lists, `timesheets` and `timesheet`, or `users` and `user`, sit side by side and are easy to misread. The product owner asked in code review for the repository to be obvious from its name.

## Decision

- Every parameter, field, property and local whose type is a repository ends in `Repository`: `ITimesheetRepository timesheetsRepository`, `IUserRepository usersRepository`, `private readonly ISessionRepository _sessionsRepository`.
- The name is the old plural plus the suffix, so the change is mechanical.
- `Architecture.Tests` checks constructor parameters, method parameters and fields in every layer. Locals and test code follow the rule by review.

Rejected: keeping the baseline's short names. They read well in small handlers, but they are the source of the confusion described above.

## Consequences

- Handler signatures get longer.
- Reads (`IOrderReads`) keep their short names. Extending the rule to them needs another decision.
