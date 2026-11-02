# Code style

Reusable conventions for a C# ASP.NET Core Minimal API with user registration, authentication, seeded memory storage, and Scalar. Libraries and fixed baseline choices live in [STACK.md](STACK.md). These guides specify a future implementation; no application is included.

`<Project>` means the chosen solution name. Business requirements will be supplied separately; do not invent task management or other business features from this baseline.

## Principles

- Readability first: small files, explicit names, and direct calls.
- One API project organized by feature and one test project. Add abstractions only for demonstrated needs.
- Preserve validation, secure password handling, authorization, and safe concurrent state changes even in a demo.
- Test observable behavior; use test-first development for authentication, validation, and store concurrency.
- Keep framework features ahead of packages. No frontend, controllers, mediator, domain events, or separate architecture layers are required.

## Repository layout

```text
/
  <Project>.sln
  global.json
  .editorconfig
  src/<Project>.Api/
    Program.cs
    Features/
      Auth/
        AuthEndpoints.cs
        AuthService.cs
        User.cs
        RegisterRequest.cs
        LoginRequest.cs
        UserResponse.cs
        TokenResponse.cs
    Data/
      InMemoryStore.cs
      DemoSeed.cs
    Common/                  auth setup, token issuer/options, error handling
    appsettings.json
    Properties/launchSettings.json
  tests/<Project>.Api.Tests/
    AuthTests.cs
    StoreTests.cs
    ApiDocumentationTests.cs
  STACK.md
  CODE_STYLE.md
  README.md
```

Endpoints translate HTTP inputs and results. Feature services implement use cases. The concrete store owns collections and synchronization. Future business features follow this shape under `Features/<Feature>/`; do not create placeholder features now.

## Language and file conventions

- Enable nullable reference types. Use file-scoped namespaces matching folders and explicit usings for project namespaces.
- One top-level type per file, named after the type. Small private nested types may remain with their owner.
- PascalCase for types/public members, camelCase for parameters/locals, `_camelCase` for private fields.
- Classes are sealed by default. Use primary constructors for dependency injection and immutable request/response records.
- Use `var` when the right-hand side makes the type obvious. Map DTOs explicitly; no mapping libraries.
- Use ordinary `Guid` IDs, generated with `Guid.CreateVersion7()`; fixture IDs are fixed constants.
- Inject `TimeProvider` and use UTC `DateTimeOffset` values; do not read the system clock directly in feature code.
- Keep memory operations synchronous. Do not introduce fake async methods or `Task.Run`. Real async methods end in `Async` and pass a final `CancellationToken` through to dependencies.
- Commit `.editorconfig`; comments explain a non-obvious reason, not the code itself.

## Startup and route organization

- Use a public, non-static `Program` class with explicit static `Main`, compatible with `WebApplicationFactory<Program>`.
- Keep startup readable: register configuration/services, build, seed, configure middleware, map endpoints, run. Extract cohesive setup blocks into named methods.
- Register one store and one signing key per host, plus `TimeProvider.System`. Feature services may be scoped; never inject scoped services into the singleton store.
- Register auth routes through `AuthEndpoints.MapAuthEndpoints`, using a route group at `/api/auth`.
- Use typed results, endpoint names, tags, summaries, and documented success/error responses so OpenAPI accurately describes the contract.
- Set an authenticated fallback policy; only registration/login and Development-only documentation explicitly use `AllowAnonymous()`.
- Configure exception handling early, HTTPS redirection, routing-aware rate limiting, authentication, and authorization in their required order. Use HTTPS for credentials and bearer tokens.

## Baseline API contract

| Method | Route | Result |
|---|---|---|
| POST | `/api/auth/register` | Anonymous. Create user; 201 with public user data. Registration does not issue a token. |
| POST | `/api/auth/login` | Anonymous. 200 with `accessToken`, `tokenType` (`Bearer`), and `expiresIn` in seconds. |
| GET | `/api/auth/me` | Authenticated. 200 with the caller's public user data. |

- Registration accepts display name, email, and password. Trim name/email; use trimmed email normalized with `ToUpperInvariant()` as the uniqueness/lookup key. Never trim or silently truncate passwords.
- Validate display name (1–100 characters after trimming), email (valid format, at most 254 characters), and password (12–128 characters). Reject missing/null fields and blank names.
- Login validates required email/password inputs; wrong credentials return a generic 401. Registration conflicts after email normalization return 409.
- Public user DTOs expose only ID, display name, and email; never serialize internal users or password hashes directly.
- JSON uses camelCase, dates use ISO 8601 UTC, and any future enums serialize as strings with undefined/numeric values rejected.
- Future resource routes use plural kebab-case nouns. GET reads; POST creates with 201 and a resource Location when a retrieval route exists; PUT/PATCH updates; DELETE returns 204.
- Future list endpoints use bounded pagination. Define their fields, filters, ordering, and ownership rules when business requirements are supplied, not now.

## Authentication and authorization

- Use `PasswordHasher<User>` for registration, seeding, and password verification. Rehash when verification requests it, storing the new hash through synchronized store access.
- Unknown email and wrong password produce the same generic login failure. Verify against a precomputed dummy hash for unknown emails as well.
- Issue HS256 JWTs with `sub` set to the user ID, issuer, audience, issued-at, and expiration. Set `MapInboundClaims = false` to read `sub` consistently.
- Generate at least 32 random bytes once per host using the platform cryptographic random generator. The same key signs and validates that host's tokens; never log, persist, or commit it.
- Validate signature, expected algorithm, issuer, audience, and lifetime. Access tokens last 30 minutes with an explicit clock skew of at most 30 seconds.
- Resolve the validated subject to a user in the current store on authenticated requests. Invalid/missing subjects or unknown users return 401.
- No roles, administrators, user directory, refresh tokens, logout, password reset, or email delivery. Clients log in again after expiration; individual token revocation is outside this demo scope.
- Future resource operations obtain the caller from validated identity, never a client-supplied owner ID. Enforce ownership in service/store operations, including reads, when those resources are specified.
- Rate-limit registration/login using built-in middleware: start with 10 requests per minute per client IP, no queue, and a global authentication limit of 100 requests per minute. Return 429 with Retry-After. Do not trust forwarded IP headers without explicit trusted-proxy configuration.

## Memory storage and seed lifecycle

- A fresh host gets a fresh singleton `InMemoryStore`. No static mutable state, database provider (including EF InMemory), disk persistence, or external cache.
- Use private dictionaries with one private lock for this small demo. Synchronize reads and writes; return immutable snapshots instead of live collections, deferred enumerations, or mutable stored references.
- Email uniqueness checking and user insertion must be one atomic operation. A concurrent dictionary alone does not make check-then-insert atomic.
- Compute password hashes outside the lock; perform the final normalized-email uniqueness check and insert inside it. Rehash updates also go through the store.
- Never hold a lock across an await. Future compound mutations and ownership checks must be atomic within their store operation.
- Seed the users specified in STACK.md once before serving requests, using the same validation, normalization, hashing, and insertion rules as registration.
- A restart discards all changes and recreates fixtures in every environment. There is no scheduled reset, reset endpoint, or restore mechanism.
- Multiple instances have independent users, data, and signing keys. This baseline supports one instance only.

## Errors, configuration, and logging

- Use `ProblemDetails` for errors and validation problem details with field errors for invalid inputs.
- Map malformed/invalid input to 400, missing/invalid authentication to 401, forbidden actions to 403, missing resources to 404, uniqueness conflicts to 409, and rate limits to 429.
- Configure authentication challenges and rate-limit rejections consistently; exception middleware alone does not format every non-success response.
- Unexpected exceptions return a generic 500 with a trace ID; log details server-side without exposing stack traces to clients.
- Bind issuer/audience/lifetime and rate-limit configuration to typed options, validated on startup. The generated signing key stays in memory, outside configuration files.
- Application code depends on `ILogger<T>`, using structured placeholders. Never log credentials, password hashes, tokens, authorization headers, or credential request bodies.
- No CORS configuration is needed for same-origin Scalar. Add an explicit origin allowlist only if a later client requires it.

## Scalar and prefilled exploration

- Register `AddOpenApi()`. Map `/openapi/v1.json` and `/scalar` in Development only, explicitly allowing anonymous access to documentation.
- Define the OpenAPI HTTP bearer security scheme and apply it to authenticated operations. Registration/login must not claim they require bearer authentication.
- Supply actual OpenAPI request examples, not just prose: login with Alice's seeded credentials and registration with Charlie's unseeded address, as specified in STACK.md.
- Use an OpenAPI transformer compatible with the pinned package version to add security metadata and examples. Scalar renders the generated document; it does not create missing security schemes.
- Exploration flow: start the API, open Scalar, execute the prefilled login, copy the token into Scalar's bearer field, call `/me`, register Charlie, and log in as Charlie.
- Never prefill generated bearer tokens. Show demo credentials only as clearly labeled fictional examples.

## Tests and quality gates

- Use xUnit assertions and Arrange/Act/Assert. Name tests by behavior, such as `Rejects_registration_when_normalized_email_exists`.
- HTTP integration tests use the real seed, store, password hasher, and bearer authentication. Each independent test/scenario gets an isolated factory/store; no order-dependent shared state.
- Cover registration, normalized-email duplicates, validation boundaries, valid/invalid login, anonymous `/me`, valid/expired/tampered tokens, missing users, and responses without hashes.
- Use a focused concurrent registration test to verify exactly one insertion succeeds for the same normalized email.
- Verify a fresh host contains both seed users, a registered user disappears in a new host, and a token from the previous host is rejected even for a seeded user with the same ID.
- Verify Scalar/OpenAPI are available in Development, absent elsewhere, and contain the bearer scheme plus login/registration examples. Test authentication rate-limit rejection.
- Use isolated configuration or a controllable clock for tests that need expiration/throttling; avoid wall-clock sleeps and do not disable the behavior being tested.
- Run `dotnet restore`, `dotnet build`, `dotnet format --verify-no-changes`, and `dotnet test` before considering implementation complete. CI treats warnings as errors; local style diagnostics may remain warnings during test-first development.
- Database containers, browser tests, separate architecture tests, and additional coverage tooling are outside this baseline.

## Documentation and reuse

- README: prerequisites, HTTPS setup, run/test commands, Scalar URL, demo credentials, registration/login examples, and authentication flow.
- Prominently document public demo credentials, complete data loss on restart, token invalidation on restart, and single-instance scope.
- When starting a new project, replace `<Project>`, implement this baseline, and add only the separately requested business features. Extend seed data and tests to match those features.
- Keep these two guides together; significant later departures should be documented with their reason. No ADR framework or extra documentation scaffolding is required initially.
