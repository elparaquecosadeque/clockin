# 0007. Static classes say what they are: `Extensions`, `Helper`, `Constants`

- **Status:** Accepted
- **Date:** 2026-10-31

## Context

The baseline only names one kind of static class, `*Mappings`. The others were named after a topic (`TimesheetLoading`, `WorkedTime`, `RateLimits`), so a reader couldn't tell from the name whether a class extends a type, holds stateless functions or only holds constants. The product owner asked in code review for the name to say which.

## Decision

- A static class whose methods extend one type (`this` parameter) ends in `Extensions`, named after the type it extends: `TimesheetRepositoryExtensions`.
- A static class of stateless functions ends in `Helper`: `ApprovedHoursCsvHelper`, `OpaqueTokenHelper`. Rules shared by several handlers are helpers too, even when written as extensions: `CorrectionRulesHelper`.
- A static class that only holds constants ends in `Constants`: `PoliciesConstants`, `RateLimitsConstants`, `PreferencesConstants`.
- Not covered, because their suffix already says what they are: `*Module` (DI registration), `*Mappings`, `*Middleware`.
- Left as they are for now: `Permissions`, `TextRules`, `PreferenceRules`, `WeekGuards`.
- Checked in review: the exceptions above make a reflection test more noise than help.

Rejected: a test that derives the suffix from whether the methods take `this`. `CorrectionRulesHelper` and the exceptions would each need an exemption.

## Consequences

- Class and file names get longer.
- A static method on a type with state (`TimesheetParties.LoadAsync`, `ReportingScope.LoadAsync`, domain factories) is not a helper and keeps its type's name.
