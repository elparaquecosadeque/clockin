# Specs

Use cases of ClockIn v1, written in Gherkin. They are the source for the TDD loop. Domain and application tests are translated from them by hand into xUnit. `@api` scenarios become plain xUnit HTTP tests in `Api.Tests` ([ADR 0004](../adr/0004-http-tests-instead-of-reqnroll.md)).

## Mapping to tests

| Gherkin | Test |
|---|---|
| Folder (`time-entries/`) | Feature folder (`Application/TimeEntries/`) |
| File (`clock-in.feature`) | Use case: `ClockInCommand` → `ClockInHandlerTests` in `Application.Tests` |
| `Rule:` | Domain invariant → `Domain.Tests` for the aggregate that owns it |
| `Scenario:` | One test method, `<Behavior>_when_<condition>` |
| `Scenario Outline:` | One `[Theory]` with one `[InlineData]` per example row |
| `@api` | xUnit test in `Api.Tests` over HTTP, with a real database. Tag one happy path per feature as well. |
| `@e2e` | Playwright test in the frontend, not a backend test |

## Conventions

- **Times are company local time** (`America/Bogota`, UTC−5, no DST) unless written with `Z`. Tests convert them to UTC before calling the domain.
- **"Now"** comes from a fake `TimeProvider`. The default is **Friday 2026-10-30 10:00**.
- **Weeks** run Monday 00:00 to Sunday 23:59:59 and are named by their Monday: the current week is `2026-10-26`, the previous one is `2026-10-19`.
- **Outcomes** map to the error table in [`CODE_STYLE.md`](../../CODE_STYLE.md#error-handling):

| Step | Exception | HTTP |
|---|---|---|
| `it fails validation on "<field>"` | FluentValidation | 400 |
| `it is forbidden` | `ForbiddenException`, or missing permission | 403 |
| `it is not found` | `NotFoundException` | 404 |
| `it conflicts with "<Code>"` | `ConflictException` | 409 |
| `it breaks the rule "<Code>"` | `DomainException` with that code | 422 |
| `it is unauthorized` | Authentication failure | 401 |

## Cast

Every feature assumes this background unless it says otherwise:

| User | Role | Team | Status |
|---|---|---|---|
| Ada | Admin | — | Active |
| Abe | Admin | — | Active |
| Sam | Supervisor | leads Warehouse | Active |
| Sue | Supervisor | leads Office | Active |
| Eve | Employee | Warehouse | Active |
| Ed | Employee | Warehouse | Active |
| Olga | Employee | Office | Active |

Terms are defined in the [glossary](../glossary.md).
