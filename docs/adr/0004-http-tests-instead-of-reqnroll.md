# 0004. Plain xUnit HTTP tests for `@api` scenarios, no Reqnroll

- **Status:** Accepted
- **Date:** 2026-10-30
- Supersedes [0002](0002-reqnroll-for-api-specs.md)

## Context

[ADR 0002](0002-reqnroll-for-api-specs.md) planned Reqnroll to run `@api` scenarios in `ClockIn.Api.Tests`. By the time the first `@api` scenarios were implemented (the sign-in rate limit and two simultaneous clock-ins), they were already written as ordinary xUnit HTTP tests over `WebApplicationFactory<Program>` and Testcontainers. Each was a few lines. Adding Reqnroll for them would bring a new package, generated code, and step definitions that have to stay in sync with the wording of the specs.

## Decision

- `@api` scenarios, and one happy path per feature, are **plain xUnit tests** in `ClockIn.Api.Tests`, over HTTP with a real database.
- Each such test class names the `.feature` file it covers in its summary comment, and its test names follow the scenario titles as `<Behavior>_when_<condition>`.
- `.feature` files in `docs/specs/` stay the single source of the requirements; they are not executed.
- Reqnroll is **not** added. Revisit if the product owner wants to read test results as Gherkin, or if specs and tests are seen to drift.

Alternatives considered:

- **Keep ADR 0002 (Reqnroll for `@api`).** Executable specs, but one more package and a step layer for a handful of scenarios. Rejected for now.
- **No HTTP tests for scenarios.** Leaves concurrency, rate limits and auth untested end to end. Rejected.

## Consequences

- One test style across the backend; no step bindings to break when a scenario is reworded.
- The specs only stay true by discipline: when a scenario changes, its test has to be found and changed by hand. The `.feature` file name in each test class's summary is how to find it.
- `STACK.md` drops the Reqnroll row.
