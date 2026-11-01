# 0008. Logic shared by handlers that needs dependencies is an injected service

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

The timesheet handlers all check who may do what through `TimesheetParties.LoadAsync`, a static method that needs the user and team repositories. Being static, it couldn't get them from DI, so every handler injected both repositories, and two extension methods (`ForEntryChangeAsync`, `GetForApproverAsync`) took them as parameters only to pass them on. Ten handlers carried repositories they never called, and the signatures grew to five or six dependencies. The product owner asked in code review for a cleaner layout.

## Decision

- Static helpers and extensions only use what they are given. They never take a dependency (a repository, a service, `ICurrentUser`) just to pass it on.
- Logic that several handlers share and that needs dependencies is a sealed, scoped service in the feature folder, injected into the handlers: `TimesheetAccess(ICurrentUser, IUserRepository, ITeamRepository)`.
- A handler injects a repository only when it calls it itself.
- The rules over loaded data stay pure (`TimesheetParties` is a record with the permission rules), so they are tested without mocks.

Rejected: keeping static helpers and passing the repositories down. It hides which handler really uses what, and every new caller repeats the list.

## Consequences

- Handlers get shorter constructors; the service is registered once in `Program.AddModules`.
- Application tests build the service from their test doubles (`TimesheetWorld.Access`).
- Checked in review: a test can't tell a dependency that is only passed on from one that is used.
