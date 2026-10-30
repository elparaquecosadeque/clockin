# 0003. Adopt the baseline code style and stack

- **Status:** Accepted
- **Date:** 2026-10-30

## Context

ClockIn starts from a shared baseline: [`CODE_STYLE.md`](../../CODE_STYLE.md) (architecture, conventions, testing) and [`STACK.md`](../../STACK.md) (approved and forbidden libraries). Adopting it is itself a decision. Recording each of its rules as a separate ADR would duplicate the baseline, and the copies would drift.

## Decision

- ClockIn adopts `CODE_STYLE.md` and `STACK.md` as its baseline. This ADR records that decision once; the files hold the rules.
- The baseline files always describe the rules in force **now**. ADRs record **why** a rule was added or changed, and are never edited afterwards. A replaced ADR is marked `Superseded by NNNN`.
- **An ADR that adds or changes a rule updates the baseline in the same commit**, and the changed line links to the ADR.
- Wording fixes and clarifications to the baseline need no ADR; git history records them.
- If the baseline is shared with other projects, it gets a version number, and this ADR is updated to name the adopted version.

Alternatives considered:

- **One ADR per baseline rule.** Two sources of truth for every rule. Rejected.
- **No ADR for the baseline.** Leaves the adoption itself unrecorded. Rejected.

## Consequences

- Reading `CODE_STYLE.md` and `STACK.md` is enough to know today's rules. The links show where a rule came from.
- A pull request that adds an ADR without changing the baseline, or the other way round, is a review smell.
- ADR 0001 and ADR 0002 already follow this rule through their entries in `STACK.md`.
