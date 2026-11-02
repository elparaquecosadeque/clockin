# Stack

Reusable baseline for a disposable C# API following [CODE_STYLE.md](CODE_STYLE.md). Copy both guides into a new project, choose its name, and supply business requirements separately. This baseline specifies users and authentication only; it does not prescribe a business domain.

## Approved libraries

| Concern | Library / platform | Notes |
|---|---|---|
| Runtime | .NET 10 LTS, C# 14 | Target `net10.0`; pin an installed, supported SDK in `global.json`. |
| HTTP | ASP.NET Core Minimal APIs | One host with feature route groups; no controllers. |
| Persistence | BCL collections | Singleton store with private dictionaries and synchronized access; no database. |
| Password hashing | `Microsoft.Extensions.Identity.Core` | `PasswordHasher<User>` only; no Identity stores. |
| JWT validation | `Microsoft.AspNetCore.Authentication.JwtBearer` | Validate signature, issuer, audience, and lifetime. |
| JWT issuance | `System.IdentityModel.Tokens.Jwt` | HS256 with a fresh process-local key. |
| Validation | Built-in Minimal API validation and data annotations | Register `AddValidation()`; ordinary C# for additional rules. |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` | Built-in document generation. |
| API exploration | `Scalar.AspNetCore` | Development-only Scalar, bearer authentication, and prefilled request examples. |
| Logging | Built-in `ILogger<T>` and console logging | Structured messages; no additional provider needed. |
| Errors, rate limiting, options | ASP.NET Core | `ProblemDetails`, built-in middleware, startup option validation. |
| Tests | xUnit, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` | Use built-in xUnit assertions and fixed fixtures. |
| HTTP integration tests | `Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory<Program>` with the real store and authentication. |

Pin compatible stable package versions in the project files; ASP.NET Core packages match the framework major. A new dependency needs a concrete requirement the framework or installed packages cannot reasonably cover.

## Excluded libraries and infrastructure

| Do not add | Use instead |
|---|---|
| EF Core, including InMemory; linq2db; Dapper; database drivers | Private in-process collections |
| SQLite, Redis, migrations, Testcontainers, file persistence | Fresh seeded state on every startup |
| ASP.NET Core Identity stores or `MapIdentityApi` | Small registration/login endpoints with `PasswordHasher<User>` |
| Wolverine, MediatR, generic repositories | Direct feature service calls |
| AutoMapper | Explicit DTO construction |
| FluentValidation or guard-clause packages | Built-in validation and C# checks |
| Frontend frameworks or custom UI | Scalar |
| Faker, mocking, architecture-test packages | Fixed fixtures and HTTP tests with real implementations |

## Project choices

| Choice | Baseline |
|---|---|
| Solution name | `<Project>`; replace when creating the application |
| Projects | `<Project>.Api` and `<Project>.Api.Tests` |
| Features | Anonymous registration/login and authenticated current-user profile |
| Registration fields | Display name, email, password |
| Public user fields | ID, display name, email |
| Authorization | Authenticated by default; no roles or administrators |
| Access tokens | JWT bearer; 30-minute lifetime; no refresh tokens or logout endpoint |
| Signing key | At least 256 cryptographically random bits, generated once per host startup |
| Persistence lifetime | Per host; reset and re-seed on every startup in every environment |
| API exploration | `/scalar`, backed by `/openapi/v1.json`, in Development |
| Hosting | Disposable single-instance demo over HTTPS |
| CORS | None needed for same-origin Scalar |
| Business features | Defined later, using the conventions in CODE_STYLE.md |

## Seed data and prefilled examples

- Seed exactly two fictional users before accepting requests: Alice (`alice@example.test`) and Bob (`bob@example.test`). Both use the public demo password `DemoPassword123!`.
- Give fixtures stable IDs. Hash their passwords through the same hasher used by registration; no plaintext password fields in stored users.
- Keep seed definitions in `Data/DemoSeed.cs`. Seed a fresh store once per startup; do not periodically reset or expose a reset endpoint.
- Include a prefilled login example using Alice and a registration example using a third address, `charlie@example.test`, so registration works immediately after startup.
- Publish demo credentials and the exploration flow in the README and OpenAPI descriptions/examples. Never embed generated bearer tokens.
- New users and all later business data disappear on restart. A new signing key also invalidates every previous token.
- Seeded credentials are public. This baseline is for disposable demos with fictional data, not real accounts or production use.

## Official references

- [Microsoft Minimal API tutorial](https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api?view=aspnetcore-10.0): framework and Scalar setup; this baseline excludes the tutorial's database persistence.
- [Minimal API authentication](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-10.0).
- [Scalar ASP.NET Core integration](https://github.com/scalar/scalar/blob/main/documentation/integrations/aspnetcore/integration.md): authentication schemes must be declared in OpenAPI for Scalar to expose them.
