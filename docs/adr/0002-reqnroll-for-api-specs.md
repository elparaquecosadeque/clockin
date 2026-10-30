# 0002. Reqnroll for executable specs in Api.Tests only

- **Status:** Superseded by [0004](0004-http-tests-instead-of-reqnroll.md)
- **Date:** 2026-10-30

## Context

Use cases are written in Gherkin first (`CLAUDE.md`) under `docs/specs/`. We want some of them to run as living documentation, but the main TDD loop in `Application.Tests` follows `CODE_STYLE.md`: one xUnit class per use case, `<Behavior>_when_<condition>` names, and ports substituted with NSubstitute. A Gherkin runner there would add a layer of step definitions to keep in sync with both the specs and the handlers, for every behavior.

## Decision

- Add **Reqnroll** (BSD-3-Clause, the maintained successor of SpecFlow) with its xUnit integration, to **`ClockIn.Api.Tests` only**. Use the package that matches the xUnit version chosen at scaffolding (`Reqnroll.xUnit` or `Reqnroll.xunit.v3`).
- Reqnroll runs:
  - every scenario tagged `@api`;
  - one happy-path scenario per feature, tagged `@api` when it is promoted.
- Those scenarios run over HTTP with `WebApplicationFactory<Program>` and a Testcontainers database, like any other `Api.Tests` test.
- `.feature` files stay in `docs/specs/` as the single source and are linked into `Api.Tests`; they are not copied.
- `Domain.Tests` and `Application.Tests` stay plain xUnit, translated from the specs by hand.
- `@e2e` scenarios stay plain Playwright in the frontend.

Alternatives considered:

- **No runner.** Least code, but the specs only stay true by discipline. Rejected in favour of executable specs at the edge.
- **Reqnroll everywhere.** Doubles the artifacts in the main TDD loop and clashes with the test naming rules. Rejected.
- **SpecFlow.** End of life. Rejected.
- **playwright-bdd for `@e2e`.** Possible later; not needed while the end-to-end scenarios are few.

## Consequences

- Step definitions live in `Api.Tests/Steps/<Feature>/`, one class per feature, and reuse `Tests.Common` fakers.
- Reqnroll scenarios are named by their Gherkin title, which is an accepted exception to the `<Behavior>_when_<condition>` rule.
- A scenario's wording change can break its step binding. CI catches it as an undefined step.
- Revisit if `@api` scenarios grow past "one happy path per feature plus the cross-cutting cases".
