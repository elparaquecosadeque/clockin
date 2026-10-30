# CLAUDE.md

Read these before writing code:

- [`CODE_STYLE.md`](CODE_STYLE.md): architecture, conventions and testing rules.
- [`STACK.md`](STACK.md): approved and forbidden libraries, and this project's
  choices.
- [`docs/adr/`](docs/adr/): decisions that deviate from or extend the two files
  above.

## Working rules

- **TDD:** write the failing test first, then the simplest code that passes,
  then refactor. Backend use cases are driven from `Application.Tests`. Use
  Gherkin first to write specs and uses cases.
- **No new packages** without asking first. If one seems necessary, explain why
  the framework or the approved stack can't cover it.
- **Never use a forbidden library**, even as a temporary workaround.
- When a decision isn't covered by the docs, ask, and propose an ADR for it.
- **Keep ADRs and the baseline in sync** ([ADR 0003](docs/adr/0003-adopt-baseline-code-style-and-stack.md)):
  an ADR that adds or changes a rule updates `CODE_STYLE.md` or `STACK.md` in
  the same commit, and the changed line links to the ADR. Never edit an
  accepted ADR; supersede it.
- **Log smaller decisions** in [`DECISIONS.md`](DECISIONS.md) as you make them
  (decision, why, who decided), in the same commit as the change. Anything
  that changes a baseline rule or is hard to reverse is an ADR instead.
- **Conventional Commits** (`feat:`, `fix:`, `chore:`, …), on a short-lived
  branch, never directly on `main`.
- Run the backend and frontend tests before declaring a task done.

## Skills

| Skill          | Use it for                                                                    |
| -------------- | ----------------------------------------------------------------------------- |
| **grill-me**   | Maturing informal user stories into clear requirements before implementation. |
| **ponytail**   | Keeping dependencies low; challenge every new package against `STACK.md`.     |
| **impeccable** | Mockups and UI/UX definition, aligned with Angular Material.                  |
