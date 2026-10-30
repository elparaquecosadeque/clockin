# 0001. Internationalization with build-time locales

- **Status:** Accepted
- **Date:** 2026-10-30

## Context

`CODE_STYLE.md` defers internationalization until a project needs it. ClockIn needs it: each user chooses English or Spanish, and separately a 12-hour or 24-hour time format. The choice must follow the user across devices. `STACK.md` approves no translation library.

## Decision

- Use Angular's first-party `@angular/localize`. Build one bundle per locale and serve them at `/en/` and `/es/`.
- Store `Language` (`en` | `es`) and `TimeFormat` (`12h` | `24h`) on the `User` in the backend, and return them from `/me`. Admins set them when creating a user; users change them in their profile.
- After sign-in, or when the language changes, the app redirects to the matching build. Changing language reloads the page once.
- The time format is applied by a single `time` pipe that wraps Angular's `DatePipe` (`HH:mm` or `h:mm a`). It does not depend on the language.
- Dates and numbers are formatted by the active build's locale. CSV exports are locale-independent (English headers, `.` decimals, ISO dates).

Alternatives considered:

- **Runtime translation library (Transloco, ngx-translate).** Switches instantly, but adds a dependency for something a user changes once or twice. Rejected.
- **Own signal-based dictionary.** No dependency, but it reinvents pluralization and locale formatting and loses compile-time checking of messages. Rejected.

## Consequences

- Every user-facing string in a template is marked with `i18n` from the first component. Retrofitting it later would mean touching every template.
- CI builds and tests both locales. Playwright runs critical flows in both.
- The web server routes `/en/*` and `/es/*` to their builds, and `/` to the user's language or `en`.
- Adding a language means adding a translation file and a build target, with no code changes.
