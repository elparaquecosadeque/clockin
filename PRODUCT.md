# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

ASP.NET Core Web API (no Razor views) and an Angular frontend with Angular Material. See `CODE_STYLE.md`, `STACK.md` and `docs/adr/`.

## Users

- **Employees** record their working hours by punching in and out, mostly from an office desk on a laptop or phone. They fix their own mistakes (a forgotten clock-out) and submit their week.
- **Supervisors** lead one team. They punch time like employees, and review, approve or return their team's weekly timesheets.
- **Admins** are positions of trust and don't track time. They manage users and teams, approve supervisors' timesheets, and troubleshoot.
- **Product owner**: reviews and approves UI changes before development, from mockups.

## Product Purpose

Trustworthy weekly time records. Punches are recorded live, manual corrections are visible, and every week is approved by a named person. Success: each week ends with an approved timesheet per person and no disputes about who changed what.

## Positioning

Records and approves time; doesn't interpret it. No overtime, break or payroll rules in v1. Every manual change stays visible next to the originally punched values.

## Operating Context

- Punching happens at the start and end of the workday, at a desk, often in a few seconds between other tasks.
- Weeks run Monday to Sunday in one company timezone (`America/Bogota`). Supervisors review on Mondays.
- No email: admins share activation and reset links by hand.
- Mockups are reviewed on a Claude design canvas now and move to Penpot once a designer owns the UI.

## Capabilities and Constraints

- Domain terms are fixed in `docs/glossary.md`; use cases in `docs/specs/`.
- English and Spanish (one build per locale), and a 12h or 24h time format per user.
- Responsive throughout; the clock screen is designed mobile-first even though most use is at a desk.
- Angular Material is the only component library.
- No notifications in v1; to-do badges are derived from state.

## Brand Commitments

Unbranded for now. Product name: **ClockIn**. The theme lives in one place (`_theme.scss`) so a brand can be applied later.

## Evidence on Hand

None. Mockups use invented sample people (the cast in `docs/specs/README.md`) clearly as sample data.

## Product Principles

1. Punching is one action, never a form.
2. Trust is visible: show what was punched, what was edited, by whom and why.
3. Never block without saying how to get unblocked.
4. Every role sees only what they act on.

## Accessibility & Inclusion

Keyboard-operable everything, labelled controls, WCAG AA contrast, checked with axe in Playwright (`CODE_STYLE.md`).
