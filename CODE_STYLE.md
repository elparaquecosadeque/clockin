# Code style

Baseline conventions for applications built with an **ASP.NET Core backend** (Web API controllers, no Razor views) and an **Angular frontend**. Library choices live in [`STACK.md`](STACK.md); project-specific deviations are recorded as ADRs in [`docs/adr/`](docs/adr/).

> In the examples, `<Project>` is the solution's root name and `<Feature>` is a domain feature, in plural (`Orders`, `Customers`, …). A feature called `Orders` is used for illustration.

## Principles

- **Readability first.** Code is read far more often than it is written. Prefer short files that can be understood at a glance over a few large files.
- **Clean Architecture.** Dependencies point inward, toward the domain. The domain knows nothing about databases, HTTP or frameworks.
- **DDD.** The domain model holds the business rules; the rest of the code serves it.
- **SOLID.** Small classes with one reason to change, depending on abstractions (ports) rather than implementations.
- **TDD.** Behavior is specified by a failing test first: red → green → refactor.
- **Few dependencies.** Before adding a package, check whether the framework already covers the need. Every new package needs a reason and goes through [`STACK.md`](STACK.md).

---

## Repository layout

```
/
├─ backend/
│  ├─ src/
│  │  ├─ <Project>.Domain/
│  │  ├─ <Project>.Application/
│  │  ├─ <Project>.Persistence/
│  │  ├─ <Project>.Infrastructure/
│  │  ├─ <Project>.Api/              (API for the Angular app, unversioned)
│  │  └─ <Project>.PublicApi/        (API for external consumers, versioned)
│  └─ tests/
│     ├─ <Project>.Domain.Tests/
│     ├─ <Project>.Application.Tests/
│     ├─ <Project>.Api.Tests/        (integration)
│     ├─ <Project>.PublicApi.Tests/  (integration)
│     ├─ <Project>.Architecture.Tests/
│     └─ <Project>.Tests.Common/     (fakers, shared helpers)
├─ frontend/                         (Angular workspace)
├─ docs/
│  ├─ adr/
│  └─ architecture/                  (Excalidraw diagrams)
├─ CODE_STYLE.md
├─ STACK.md
├─ CLAUDE.md
└─ README.md
```

Backend and frontend are separate applications, deployed independently, that only talk over HTTP. The frontend never references backend code; it mirrors the backend contracts (DTOs) in its own models.

---

# Backend

## Project dependencies

```
Api, PublicApi ──► Application ──► Domain
      │                 ▲
      └──► Persistence ─┤
      └──► Infrastructure ┘
```

- **Domain** references nothing in the solution.
- **Application** references only Domain.
- **Persistence** and **Infrastructure** reference Application (to implement its ports) and Domain.
- **Api** and **PublicApi** reference Application, plus Persistence and Infrastructure **only to register them** (composition root). Controllers never use Persistence or Infrastructure types.
- These rules are enforced by architecture tests.

## Language conventions

- Nullable reference types enabled.
- File-scoped namespaces.
- Classes are `sealed` by default; unseal only for a reason.
- Primary constructors for dependency injection.
- Async methods end in `Async`, take a `CancellationToken` as the last parameter and always pass it on.
- `var` only when the type is obvious from the right-hand side.
- Implicit usings only for BCL namespaces.
- Style is defined in a committed `.editorconfig` and checked by analyzers (see [Quality gates](#quality-gates)).

## One type per file

- Each top-level type (class, record, interface, enum) goes in **its own file**, named after the type: `OrderDto` → `OrderDto.cs`. For generic types, the file name omits the type parameters: `PagedResult<T>` → `PagedResult.cs`.
- **Exception:** a command or query shares a file with its validator and handler (`CreateOrderCommand.cs` contains `CreateOrderCommand`, `CreateOrderValidator` and `CreateOrderHandler`). They form one unit and are read together.
- Small **nested** types may stay in their containing type's file, e.g. a controller's request records (`AuthController.LoginRequest`).

## Folders by feature

Organize by feature first and, within each feature, by kind of piece.

```
<Project>.Domain/
├─ Common/        Entity.cs, AggregateRoot.cs, IDomainEvent.cs, DomainException.cs, Guards/
└─ <Feature>/
   ├─ Events/     OrderPlaced.cs, …
   ├─ Order.cs                       (aggregate root)
   ├─ OrderId.cs                     (strongly-typed ID)
   ├─ OrderLine.cs                   (entity inside the aggregate)
   └─ Money.cs, OrderStatus.cs, …    (value objects, enums)

<Project>.Application/
├─ Common/        Exceptions/, Interfaces/, Middleware/, Results/
└─ <Feature>/
   ├─ Commands/       CreateOrderCommand.cs      (+ validator + handler)
   ├─ Queries/        GetOrderByIdQuery.cs       (+ validator + handler)
   ├─ EventHandlers/  SendConfirmationOnOrderPlacedHandler.cs
   ├─ Results/        OrderDto.cs, OrderSummaryRow.cs
   ├─ Interfaces/     IOrderRepository.cs, IOrderReads.cs
   ├─ Mappings/       OrderMappings.cs
   └─ OrderPricing.cs                            (feature helpers)

<Project>.Persistence/
├─ Common/        data connection, unit of work, ID/type mappings
├─ Migrations/
├─ Seeds/
└─ <Feature>/
   ├─ <Feature>PersistenceModule.cs              (DI registration only)
   ├─ OrderRecord.cs                             (table mapping)
   ├─ OrderRepository.cs
   └─ OrderReads.cs

<Project>.Infrastructure/<Feature>/              (email, files, external APIs)

<Project>.Api/
├─ Controllers/   <Feature>Controller.cs
└─ Common/        exception handler, auth setup, …
```

- **Results/**: everything a command or query returns (`*Dto`, `*Result`, `*Row`, `*Payload`), plus enums that exist only within those results.
- **Interfaces/**: interfaces are **never** declared next to their implementation or next to their consumers. Feature interfaces go in `<Feature>/Interfaces/`; cross-cutting ones in `Common/Interfaces/`.
- Anything that is not a command, query, event handler, result, interface or mapping goes in the feature's root folder.
- Names don't change to match the folder: a `*Dto` in `Results/` is still called `*Dto`.
- Types that exist only for one class (e.g. a private projection row) are nested as `private` inside that class.
- Cross-cutting code goes in `Common/` within each layer, with the same subfolders where applicable.

## Namespaces = folders

The namespace always matches the folder, even when it feels redundant (`<Project>.Application.Orders.Commands`). That way each `using` shows where a type comes from. Use explicit `using` directives for our own namespaces, never `global using`, and no unused `using`s.

## Program with an explicit `Main`

No top-level statements. `Program` is a class (`public`, **not** `static`, because `WebApplicationFactory<Program>` uses it as a generic argument) and its `Main` reads like a table of contents:

```csharp
public static void Main(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    AddLogging(builder);
    AddOptions(builder.Services, builder.Configuration);
    AddModules(builder.Services, builder.Configuration);
    AddMediator(builder.Services);
    AddAuth(builder.Services, builder.Configuration);
    AddRateLimiting(builder.Services);
    AddCors(builder.Services, builder.Configuration);
    AddApiDocs(builder.Services);
    AddHealthChecks(builder.Services);

    var app = builder.Build();
    UsePipeline(app);
    MapEndpoints(app);
    app.Run();
}
```

Each configuration block goes in a named private method. Both hosts (`Api`, `PublicApi`) follow the same shape.

## Responsibilities by layer

- **Domain**: aggregates, entities, value objects, domain events and business rules. No dependencies on persistence, infrastructure or frameworks (the guard-clause library is the only allowed package).
- **Application**: use cases (commands, queries, event handlers), validation, results, mappings, and the ports (interfaces) the use cases need. It knows nothing about the database engine or HTTP.
- **Persistence**: the only layer that talks to the database. Implements repositories and reads, and owns table records, migrations, seed data, transactions and domain-event dispatch.
- **Infrastructure**: implementations of every other port (email, file storage, external APIs, token signing, …).
- **Api / PublicApi**: thin controllers that only translate HTTP ↔ command/query, plus host setup. No business rules.
- `*Module.cs` files only register dependencies.

## Domain modeling (DDD)

### Aggregates

- State changes only through methods that protect invariants. No public setters.
- Created through static factory methods (`Order.Create(...)`) or constructors that leave the aggregate valid.
- **One repository per aggregate root.** Entities inside an aggregate (`OrderLine`) are reached only through their root.
- Aggregates reference other aggregates **by ID only**, never by object reference.
- **One aggregate is changed per transaction.** Effects on other aggregates happen through domain events.
- Aggregate roots carry a `Version` for optimistic concurrency.

### Value objects

- Immutable `record`s that validate themselves in the constructor (`Money`, `Email`, `DateRange`).
- Prefer value objects over primitives whenever a value has rules or meaning.

### Strongly-typed IDs

- Every aggregate root and entity has its own ID type: `public readonly record struct OrderId(Guid Value)`.
- IDs are **Guid v7** (`Guid.CreateVersion7()`), generated in the domain, never by the database. `Guid.NewGuid()` is banned.
- Each ID type exposes a factory (`OrderId.New()`) so callers never build GUIDs themselves.

### Domain events

- Raised inside aggregate methods (`AddDomainEvent(new OrderPlaced(Id))`) and collected by the aggregate.
- Named in past tense, as immutable records in `<Feature>/Events/`.
- Dispatched **inline, in the same transaction** as the change (see [Domain event dispatch](#domain-event-dispatch)).

### Guard clauses

- Use the guard-clause library for preconditions instead of hand-written `if … throw` blocks:

  ```csharp
  public static Order Create(CustomerId customerId, IReadOnlyList<OrderLineInput> lines)
  {
      Guard.Against.Default(customerId);
      Guard.Against.NullOrEmpty(lines);
      …
  }
  ```

- Guards protect invariants against programming errors. A broken **business rule** throws `DomainException`; invalid **user input** is caught earlier by validation.
- Custom guards go in `Domain/Common/Guards/` as extension methods, one per file.

## Commands, queries and validation

- A **command** changes state; a **query** only reads. Both are immutable records, dispatched in-process through the mediator (see [`STACK.md`](STACK.md)).
- Handlers end in `Handler` and expose one `HandleAsync` method. Validators end in `Validator`.
- Controllers dispatch through the mediator; they never inject or call a handler directly.
- **Validation** runs as middleware before the handler. A failure becomes a 400 with field errors. Handlers assume valid input.
- **Command handlers** load aggregates through repositories, call domain methods and save. **Query handlers** use reads that project straight into results and may skip the domain.
- Cross-cutting concerns (validation, transactions, logging) are mediator middleware in `Application/Common/Middleware/`, never repeated inside handlers.

```csharp
// Application/Orders/Commands/CreateOrderCommand.cs
public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineInput> Lines);

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Lines).NotEmpty();
    }
}

public sealed class CreateOrderHandler(IOrderRepository orders)
{
    public async Task<OrderDto> HandleAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var order = Order.Create(new CustomerId(command.CustomerId), command.Lines);
        await orders.AddAsync(order, ct);
        return order.ToDto();
    }
}
```

## Domain event dispatch

- The unit of work collects domain events from the aggregates saved in the current command.
- After saving and **before committing**, it dispatches each event to its handlers through the mediator. Event handlers run inside the same transaction: either everything succeeds or nothing is saved.
- Event handlers live in `Application/<Feature>/EventHandlers/`, one per file, named `<Reaction>On<Event>Handler` (`SendConfirmationOnOrderPlacedHandler`).
- Events sent to **other systems** (integration events, outbox) are out of scope for the baseline and need an ADR.

## Error handling

Handlers and the domain **throw** specific exceptions. One exception-handling middleware (`IExceptionHandler`), shared by both hosts, turns them into RFC 9457 `ProblemDetails`. Controllers never catch exceptions.

| Exception | Declared in | HTTP |
|---|---|---|
| Validation failure (thrown by the validation middleware) | — | 400, with field errors |
| `NotFoundException` | `Application/Common/Exceptions/` | 404 |
| `ForbiddenException` | `Application/Common/Exceptions/` | 403 |
| `ConflictException` (incl. concurrency conflicts) | `Application/Common/Exceptions/` | 409 |
| `DomainException` (business rule broken) | `Domain/Common/` | 422 |
| Anything else, incl. guard failures | — | 500, generic message; details only in logs |

Every `ProblemDetails` includes the request's correlation ID.

## Object mapping

No mapping libraries. Mapping is plain C# that you can step through in the debugger.

- Domain → result: static extension methods in `<Feature>/Mappings/<Entity>Mappings.cs`:

  ```csharp
  public static class OrderMappings
  {
      public static OrderDto ToDto(this Order order) =>
          new(order.Id.Value, order.CustomerId.Value, order.Total.Amount, order.Status.ToString());
  }
  ```

- Record ↔ domain: private methods inside the repository (`ToDomain`, `ToRecord`). Table records never leave Persistence.
- Every property is assigned explicitly. Prefer positional records and `required` members so a missed field doesn't compile.

## Persistence

### Repositories, reads and records

- Data access goes through a single data connection type in `Persistence/Common/`.
- Tables are mapped by **records** in Persistence (`OrderRecord`), never by domain types. Domain classes have no persistence attributes.
- **Repositories** (`IOrderRepository`) load and save whole aggregates for commands. **Reads** (`IOrderReads`) project straight into results for queries.
- Queries are written in LINQ. Raw SQL is a last resort and must work on every supported engine.

### Transactions and concurrency

- **One transaction per command**, opened and committed by mediator middleware; it also covers domain-event dispatch. Queries run without transactions.
- **Optimistic concurrency:** saving an aggregate checks and increments its `Version`. A mismatch throws `ConflictException` → 409.

### Time

- Inject `TimeProvider`; never call `DateTime.Now`/`UtcNow` or `DateTimeOffset.Now`/`UtcNow` (banned at build time).
- Store `DateTimeOffset` in UTC.

### Database engines

- The engine and connection string come from configuration, never from code. Per-project engines are listed in [`STACK.md`](STACK.md).
- Code must not branch on the engine outside `Persistence/Common/`. If something behaves differently between engines, solve it there or avoid the feature.
- Integration tests run against every engine the project deploys to.

### Migrations and seed data

```
<Project>.Persistence/
├─ Migrations/    M202610301200_CreateOrders.cs, …
└─ Seeds/         DevelopmentSeed.cs, …
```

- One migration per file, named `M<yyyyMMddHHmm>_<Description>`, with the same number as its version. Never edit a migration that has been applied in a shared environment; write a new one.
- **Reference data** every environment needs (statuses, countries, roles) is inserted by regular migrations.
- **Development/test data** lives in `Seeds/` and runs only in the environments it targets. Seeds run every time, so they must be idempotent.
- Locally, migrations run on startup. In remote environments they run as a separate deployment step, never on app startup.

## API conventions

### Two hosts

| | `Api` | `PublicApi` |
|---|---|---|
| Consumers | The Angular app | External systems |
| Versioning | None (ships with the frontend) | URL segment: `/api/v1/...` |
| Auth | User tokens | Client credentials tokens (audience `PublicApi`) |
| Shape | Endpoints shaped for the UI | Stable, documented contract |

Breaking changes in `PublicApi` require a new version; old versions stay until consumers migrate.

### Routes and responses

- Plural nouns in kebab-case: `/api/orders/{id}/line-items`.
- `GET` reads, `POST` creates (`201 Created` with `Location`), `PUT`/`PATCH` updates (`204` or `200` with the result), `DELETE` (`204`).
- Errors are always `ProblemDetails` (see [Error handling](#error-handling)).
- List endpoints return `PagedResult<T>` with `items`, `page`, `pageSize` and `totalCount`; `pageSize` has a server-side maximum.
- JSON: dates in ISO 8601 UTC, enums as strings, camelCase properties.

### Documentation

- Both hosts generate an OpenAPI document with the built-in OpenAPI support. The interactive UI is enabled only in Development.
- The `PublicApi` document is published per version.

## Authentication and authorization

### Tokens

- The application issues its own tokens, from an `Auth` feature in `<Project>.Api`. Moving to an external identity provider is an ADR when the project needs it; the APIs only validate tokens, so they don't change.
- **Asymmetric signing** (ES256 or RS256). Only `<Project>.Api` holds the private key; `PublicApi` validates with the public key.
- Signing keys come from the secret store per environment. Locally, a development key is generated and never committed.
- **Access token:** short-lived JWT (10–15 minutes) carrying identity only: `sub`, `security_stamp`, audience. **No permissions in the token.**
- **Refresh token:** opaque random value, stored **hashed**, **rotated** on every use. Reuse of an old refresh token revokes the whole token family.
- **Users** are a domain aggregate (`User`) persisted like any other. Passwords are hashed with a versioned, well-tested hasher (see [`STACK.md`](STACK.md)), never with hand-written crypto.
- **External consumers** of `PublicApi` use client credentials: a client id and secret (stored hashed) exchanged at the token endpoint for a short-lived JWT with audience `PublicApi` and scoped permissions.

### Permissions are checked on every request

- After the token is validated, the user's **current** permissions and security stamp are loaded from the database, with a short cache that is invalidated when permissions change.
- A security stamp that differs from the token's (password reset, user disabled, role revoked) rejects the request with 401.
- So a permission change takes effect on the next request, not when the token expires.

### Authorization rules

- **Permission-based policies**; never check roles in code. Policy names are constants in one place (`Policies.Orders.Create`) and are registered once in `Program.AddAuth`.
- Every endpoint requires authentication through a **fallback policy**. Anonymous endpoints opt out explicitly with `[AllowAnonymous]`.
- Controllers declare the policy: `[Authorize(Policy = Policies.Orders.Create)]`.
- **Resource-level checks** ("can this user change *this* order?") happen in the Application layer through an `ICurrentUser` port, and throw `ForbiddenException` → 403.

## Security hardening

- **Rate limiting** with the built-in middleware: strict limits on login, refresh and token endpoints; a per-client limit on `PublicApi`.
- **Account lockout** after repeated failed logins. Login errors never reveal whether a user exists.
- HTTPS only, with HSTS in remote environments.
- Security headers (CSP, `X-Content-Type-Options`, frame options) on API responses and on the frontend hosting.
- **CORS** only in remote environments, with an explicit list of allowed origins per environment from configuration. Never `AllowAnyOrigin`.
- **Audit log** of security events (logins, failed logins, refresh-token reuse, permission changes) through the logger, under a dedicated `Audit` category.

## Configuration and secrets

- Settings are bound to typed classes with the **Options pattern** and validated on startup (`ValidateDataAnnotations().ValidateOnStart()`). A missing setting fails at boot, not mid-request.
- Classes inject `IOptions<T>`. Only `Program` and `*Module.cs` read `IConfiguration` directly.
- **No secrets in `appsettings*.json`:** locally use user-secrets; remotely use environment variables or the platform's secret store. This covers connection strings, signing keys and client secrets.

## Logging and observability

- Application code depends only on `ILogger<T>`. No class references the logging provider; the provider and its sinks are configured only in `Program.AddLogging`.
- Structured messages with named placeholders, never string interpolation:

  ```csharp
  logger.LogInformation("Order {OrderId} created for customer {CustomerId}", order.Id, order.CustomerId);
  ```

- Never log secrets, tokens, passwords or personal data.
- Levels and categories are configured in `appsettings.*.json`, not in code.
- **Correlation ID:** every request takes `X-Correlation-Id` from the incoming header (or generates one), adds it to every log entry, and returns it in the response header and in `ProblemDetails`. The frontend sends it on every call.
- **Health checks:** `/health/live` (the process is up) and `/health/ready` (dependencies such as the database are reachable).
- Distributed tracing and metrics are deferred (see [`docs/adr/`](docs/adr/)).

## Testing (TDD)

Write the failing test first, make it pass with the simplest code, then refactor. Tests describe behavior, not implementation.

| Project | Covers | Doubles |
|---|---|---|
| `Domain.Tests` | Aggregate and value-object invariants, domain events raised | None; pure code |
| `Application.Tests` | One test class per use case (handler, validator, event handler). **This is the main TDD loop.** | Ports substituted |
| `Api.Tests` / `PublicApi.Tests` | Integration through HTTP with `WebApplicationFactory<Program>` and a real database: happy path, auth, error mapping, a few per feature | None for the database |
| `Architecture.Tests` | Project dependency rules, folder and naming conventions | — |

- **Naming:** test classes are named after the unit under test (`CreateOrderHandlerTests`); methods describe behavior as `<Behavior>_when_<condition>`: `Creates_order_when_customer_exists`, `Rejects_order_when_lines_are_empty`. No method names in test names, so they survive refactors.
- Arrange / Act / Assert, one behavior per test.
- **Assertions:** the assertion library's `.Should()` style only; don't mix with `Assert.*`.
- **Test data:** one faker per type in `<Project>.Tests.Common/Fakers/` (`OrderFaker.cs`). Use a fixed seed when a test depends on specific values.
- **Substitutes:** only substitute ports (interfaces from `Interfaces/`). Never substitute domain types or the class under test; build real instances with fakers.
- Integration tests use disposable databases (containers), one per test run, and never shared environments.

---

# Frontend (Angular)

## Version and defaults

- The **latest stable major** of Angular, kept on a version under active support.
- Standalone components only (no `NgModule`s).
- Strict TypeScript (`"strict": true`) and strict templates.
- Styles in **SCSS**.

## One concern per file

A component is split into four files with the same base name. No inline `template` or `styles`.

```
order-list/
├─ order-list.component.ts        (logic)
├─ order-list.component.html      (DOM)
├─ order-list.component.scss      (styles)
└─ order-list.component.spec.ts   (tests)
```

- File names are kebab-case and keep the type suffix: `.component`, `.service`, `.directive`, `.pipe`, `.guard`, `.interceptor`, `.model`, `.routes`. Class names match: `OrderListComponent`, `OrderService`.
- Recent Angular CLIs generate files without suffixes by default. Keep the suffixes with these schematics in `angular.json`:

  ```json
  "schematics": {
    "@schematics/angular:component": { "type": "component", "style": "scss" },
    "@schematics/angular:directive": { "type": "directive" },
    "@schematics/angular:service": { "type": "service" },
    "@schematics/angular:guard": { "typeSeparator": "." },
    "@schematics/angular:interceptor": { "typeSeparator": "." },
    "@schematics/angular:pipe": { "typeSeparator": "." },
    "@schematics/angular:resolver": { "typeSeparator": "." }
  }
  ```

- One model per file; models are plain TypeScript interfaces or types.

## Core / Features / Shared

```
frontend/src/app/
├─ core/                      app-wide singletons, loaded once
│  ├─ guards/                 auth.guard.ts, permission.guard.ts
│  ├─ interceptors/           auth.interceptor.ts, error.interceptor.ts, correlation-id.interceptor.ts
│  ├─ layout/                 shell, header, nav
│  ├─ models/                 api-error.model.ts, paged-result.model.ts
│  └─ services/               session.service.ts, …
├─ features/
│  └─ orders/                 one folder per feature, lazy-loaded
│     ├─ components/          presentational pieces of this feature
│     ├─ pages/               routed components
│     ├─ services/            order.service.ts (+ .spec.ts)
│     ├─ models/              order.model.ts (mirrors backend DTOs)
│     └─ orders.routes.ts
└─ shared/                    reusable, feature-agnostic building blocks
   ├─ components/
   ├─ directives/
   ├─ pipes/
   └─ models/
```

- **core/**: things that exist once per app. Services here are `providedIn: 'root'`.
- **features/**: each feature is self-contained and lazy-loaded from its `*.routes.ts`.
- **shared/**: reusable, presentational pieces. They receive data through inputs and emit through outputs; they never inject feature services or call the API.
- **Services** always live in a `services/` folder, never next to a component.

### Dependency rules

- `features/*` may import from `core/` and `shared/`, **never** from another feature. If two features need the same thing, move it to `shared/` (UI) or `core/` (state and services).
- `shared/` never imports from `features/` or `core/services/`.
- `core/` never imports from `features/`.

## Responsibilities

- **Components**: presentation and user interaction. No `HttpClient`; they call services.
- **Pages**: routed components that compose feature components and connect them to services.
- **Services**: HTTP calls, state and frontend logic. All backend access goes through a service.

## UI and accessibility

- UI components come from the design-system library in [`STACK.md`](STACK.md). Check it before building a custom component.
- The theme (palette, typography, density) is defined once in `src/styles/_theme.scss`. Components use theme tokens or CSS variables, never hard-coded colors or fonts.
- Custom components follow the same design guidelines so they look and behave like the rest of the app. Reusable wrappers (e.g. a preconfigured data table) live in `shared/components/`.
- **Accessibility:** every form control has a label, every interaction works with the keyboard, and critical flows are checked automatically (see [Frontend testing](#frontend-testing)).
- Internationalization is deferred until a project needs it (ADR).

## State

- **Signals** for component state; services expose signals (read-only) for feature state.
- RxJS only for streams (HTTP, events); convert to signals at the edge of the service.
- No state-management library in the baseline. Adopting one requires an ADR.

## Forms

- **Typed reactive forms** only; no template-driven forms.
- 400 validation errors from the API are mapped onto the matching form controls.

## HTTP, session and errors

- **Session:** `core/services/session.service.ts` holds the access token **in memory only** and the current user's permissions, loaded from the API's `/me` endpoint. Tokens are never stored in `localStorage` or `sessionStorage`.
- **Refresh token:** travels in an HttpOnly, Secure, SameSite=Strict cookie scoped to the refresh endpoint; JavaScript never reads it.
- **Auth interceptor:** attaches the access token. On a 401 it calls refresh **once** and retries the request; if refresh fails, it signs the user out.
- **Correlation ID interceptor:** sends `X-Correlation-Id` on every request.
- **Error interceptor:** turns `ProblemDetails` into a typed `ApiError`. 400 → form errors; 403/404/409/422 → a snackbar with the message; 500 → a generic message, with details logged. Components never inspect raw HTTP errors.
- **Guards and visibility:** routes and UI elements are shown or hidden based on the session's permissions. This is convenience only; the API always enforces.
- **Local development:** the Angular dev server proxies `/api/*` to the local backend (`proxy.conf.json`), so there is no CORS locally.

## Frontend testing

- **Unit tests** with the Angular CLI's default runner, in `.spec.ts` files next to the code. Every component, service, pipe, directive, guard and interceptor has one.
- Test components through their rendered DOM and public inputs/outputs, not their internals.
- **TDD** for services, guards, interceptors and logic-heavy components. Purely presentational components are tested after.
- **End-to-end tests** with Playwright for critical user flows only, including automated accessibility checks.

---

# Quality gates

## Local vs CI

- **Locally:** analyzers report **warnings** only, so the build never fails on style during the red phase of TDD. The IDE applies `.editorconfig` fixes on save.
- **CI:** strict. Warnings are errors (`TreatWarningsAsErrors` only when `ContinuousIntegrationBuild=true`) and a pull request can't merge unless every step passes.

## CI pipeline

1. Restore and build backend (warnings as errors).
2. `dotnet format --verify-no-changes`.
3. Banned packages and banned APIs (see [`STACK.md`](STACK.md)).
4. Unit tests (Domain, Application) with a **coverage threshold of 80% on Domain and Application**.
5. Architecture tests.
6. Integration tests (every deployed database engine, in containers).
7. Frontend: lint, unit tests, production build.
8. End-to-end tests for critical flows.

## Git workflow

- **Trunk-based:** short-lived branches, pull requests into `main`.
- **Conventional Commits:** `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`; a breaking change is marked with `!`.
- A pull request needs green CI and includes the tests for the change.

---

# Documentation

- **`README.md`**: how to run backend and frontend locally, run tests and apply migrations.
- **`STACK.md`**: approved and forbidden libraries, and per-project choices.
- **`docs/adr/`**: one Architecture Decision Record per significant decision or deviation from this guide, numbered `NNNN-title.md`, using the template in that folder.
- **`docs/architecture/`**: Excalidraw diagrams of the architecture and the command/query/event flow, **updated on each major version**.
- **`CLAUDE.md`**: entry point for coding agents; points to this guide, `STACK.md` and the skills used in the project.
