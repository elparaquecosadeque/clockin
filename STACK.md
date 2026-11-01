# Stack

Default libraries for applications that follow [`CODE_STYLE.md`](CODE_STYLE.md). Each project copies this file and adjusts the [per-project choices](#per-project-choices); any other deviation is recorded as an ADR in [`docs/adr/`](docs/adr/).

Keep this list short. A new package needs a reason the framework can't cover, and agreement before it's added.

## Approved libraries

### Backend

| Concern | Library | Notes |
|---|---|---|
| Data access | **linq2db** (+ the engine drivers **Npgsql**, **Microsoft.Data.Sqlite**) | LINQ queries over table records; no change tracking. |
| Migrations and seed data | **FluentMigrator** (`FluentMigrator.Runner.Postgres`, `FluentMigrator.Runner.SQLite`) | One migration works for every supported engine. Reference the engine runners, not the `FluentMigrator.Runner` meta-package, which pulls every engine (and SQL Server's vulnerable crypto dependency). |
| Mediator (commands, queries, events) | **Wolverine** (+ `WolverineFx.RuntimeCompilation`) | In-process mediator only. Since v6 the runtime code compiler is its own package. See [Wolverine](#wolverine). |
| Validation | **FluentValidation** + **WolverineFx.FluentValidation** | Validators run as Wolverine middleware. |
| Guard clauses | **Ardalis.GuardClauses** | The only package allowed in Domain. |
| Password hashing | **Microsoft.Extensions.Identity.Core** | `PasswordHasher<T>` only; no Identity stores (they require EF Core). |
| JWT validation | **Microsoft.AspNetCore.Authentication.JwtBearer** | |
| Logging | **Serilog** (`Serilog.AspNetCore`, `Serilog.Sinks.File`) | Behind `ILogger<T>`. See [Serilog](#serilog). |
| OpenAPI | **Microsoft.AspNetCore.OpenApi** | Built-in document generation. |
| Rate limiting, health checks, CORS, options | Built into ASP.NET Core | No extra packages. |
| Banned APIs | **Microsoft.CodeAnalysis.BannedApiAnalyzers** | See [Enforcement](#enforcement). |

### Tests

| Concern | Library |
|---|---|
| Test framework | **xUnit** |
| Assertions | **AwesomeAssertions** |
| Test data | **Bogus** |
| Test doubles | **NSubstitute** |
| Integration host | **Microsoft.AspNetCore.Mvc.Testing** (`WebApplicationFactory`) |
| Disposable databases | **Testcontainers** |
| Architecture rules | **NetArchTest.Rules** |
| Backend coverage | **Microsoft.Testing.Extensions.CodeCoverage**, the extension for the existing Microsoft Testing Platform runner |
| Coverage reports | **ReportGenerator**, pinned in the local `.config/dotnet-tools.json` manifest; HTML, Markdown and JSON from backend Cobertura and frontend LCOV |
| Executable Gherkin specs | None: `@api` scenarios are plain xUnit HTTP tests. See [ADR 0004](docs/adr/0004-http-tests-instead-of-reqnroll.md) |

### Frontend

| Concern | Library |
|---|---|
| Framework | **Angular** (latest stable major) |
| UI components | **Angular Material** + **CDK** |
| Unit tests | The Angular CLI's default runner |
| Coverage | **@vitest/coverage-v8**, matching the installed Vitest version |
| End-to-end and accessibility tests | **Playwright** + **@axe-core/playwright** |

## Forbidden libraries

| Don't use | Use instead | Why |
|---|---|---|
| Entity Framework Core (any package) | linq2db | One data-access technology; no change tracking. |
| Dapper | linq2db | One data-access technology. |
| ASP.NET Core Identity stores | Own `User` aggregate + `PasswordHasher<T>` | The stores depend on EF Core. |
| MediatR | Wolverine | Commercial license since v13. |
| AutoMapper | Explicit manual mapping | Commercial license since v15; manual mapping is easier to read and debug. |
| Moq | NSubstitute | SponsorLink controversy (collected developer data during builds). |
| FluentAssertions | AwesomeAssertions | v8 moved to a commercial license; AwesomeAssertions is the open-source drop-in fork. |
| Other UI component libraries | Angular Material | One design system per frontend. |
| NgRx / other state libraries | Signals | Not in the baseline; adopting one requires an ADR. |

## Per-project choices

Fill these in for each project.

| Choice | Default | This project |
|---|---|---|
| Database, local development | SQLite | SQLite |
| Database, remote (dev, QA, staging, production) | PostgreSQL | PostgreSQL |
| Token issuer | Own `Auth` feature in `<Project>.Api` | Own `Auth` feature in `ClockIn.Api` |
| Production log sink | — (file only) | File only, until hosting is chosen |
| Hosting (backend / frontend) | — | To be decided; deployed from a local Jenkins server |
| CI platform | — | GitHub Actions |
| Solution name | — | `ClockIn` |
| PublicApi | — | Not built in v1 (no external consumers) |
| Internationalization | Deferred | `@angular/localize`, `en` and `es`. See [ADR 0001](docs/adr/0001-internationalization.md) |

## Library notes

### Wolverine

Used **only as an in-process mediator**.

- Handlers live in the Application assembly, which Wolverine doesn't scan by default, so register it explicitly and turn off the messaging and durability features:

  ```csharp
  private static void AddMediator(IServiceCollection services) =>
      services.AddWolverine(opts =>
      {
          opts.Discovery.IncludeAssembly(typeof(CreateOrderHandler).Assembly);
          opts.Durability.Mode = DurabilityMode.MediatorOnly;
          opts.UseFluentValidation();
      });
  ```

- Controllers inject `IMessageBus` and call `InvokeAsync<TResult>(command, ct)`.
- Use only the `Handler` / `HandleAsync` naming, even though Wolverine accepts others (`Consumer`, `Consume`, …).
- Validation failures surface as FluentValidation's `ValidationException`; the exception handler maps it to 400.
- **Out of scope without an ADR:** message transports, durable inbox/outbox, Wolverine.HTTP endpoints, Marten or other JasperFx persistence.

### linq2db

- One `AppDataConnection` in `Persistence/Common/`, configured from the `Database` settings (provider + connection string).
- Strongly-typed IDs and value objects get value converters registered once in `Persistence/Common/`.
- Prefer `InsertOrReplace` for idempotent upserts and `BulkCopy` for large inserts.

### FluentMigrator

- Migrations: `[Migration(yyyyMMddHHmm)]` classes in `Persistence/Migrations/`, matching the file name `M<yyyyMMddHHmm>_<Description>.cs`.
- Reference data: `Insert.IntoTable(...)` inside regular migrations.
- Development and demo data: `Persistence/Seeds/DemoSeed.cs`, run by the API in the Development and Demo environments, which wipes and rebuilds the data relative to now, daily unless `Demo:DailyReset` is off ([ADR 0005](docs/adr/0005-demo-data-and-daily-reset.md)).
- Use `IfDatabase(...)` only inside Persistence when an engine needs special handling.

### Serilog

- Registered once in `Program.AddLogging`; configured from `appsettings*.json`:

  ```json
  "Serilog": {
    "MinimumLevel": { "Default": "Information" },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/app-.log", "rollingInterval": "Day" } }
    ],
    "Enrich": [ "FromLogContext" ]
  }
  ```

- The correlation ID is pushed into the log context by middleware.
- The production sink is added later as another `WriteTo` entry; application code doesn't change.

## Enforcement

### Banned packages

`Directory.Build.targets` at the backend root fails the build when a forbidden package is referenced:

```xml
<Project>
  <Target Name="CheckBannedPackages" BeforeTargets="CollectPackageReferences">
    <ItemGroup>
      <_BannedEfCore Include="@(PackageReference)"
                     Condition="$([System.String]::Copy('%(Identity)').StartsWith('Microsoft.EntityFrameworkCore'))" />
    </ItemGroup>
    <Error Condition="'@(_BannedEfCore)' != ''" Text="Entity Framework Core is not allowed (@(_BannedEfCore)). Use linq2db. See STACK.md." />
    <Error Condition="'@(PackageReference->WithMetadataValue('Identity','Dapper'))' != ''" Text="Dapper is not allowed. Use linq2db. See STACK.md." />
    <Error Condition="'@(PackageReference->WithMetadataValue('Identity','MediatR'))' != ''" Text="MediatR is not allowed. Use Wolverine. See STACK.md." />
    <Error Condition="'@(PackageReference->WithMetadataValue('Identity','AutoMapper'))' != ''" Text="AutoMapper is not allowed. Map manually. See STACK.md." />
    <Error Condition="'@(PackageReference->WithMetadataValue('Identity','Moq'))' != ''" Text="Moq is not allowed. Use NSubstitute. See STACK.md." />
    <Error Condition="'@(PackageReference->WithMetadataValue('Identity','FluentAssertions'))' != ''" Text="FluentAssertions is not allowed. Use AwesomeAssertions. See STACK.md." />
  </Target>
</Project>
```

Direct references only; check transitive ones with `dotnet list package --include-transitive` when adding a package.

### Banned APIs

`BannedSymbols.txt`, used by `Microsoft.CodeAnalysis.BannedApiAnalyzers` (rule RS0030):

```
P:System.DateTime.Now;Use TimeProvider.GetUtcNow()
P:System.DateTime.UtcNow;Use TimeProvider.GetUtcNow()
P:System.DateTimeOffset.Now;Use TimeProvider.GetUtcNow()
P:System.DateTimeOffset.UtcNow;Use TimeProvider.GetUtcNow()
M:System.Guid.NewGuid;Use the strongly-typed ID factory (Guid v7)
```

### Package versions

Use Central Package Management (`Directory.Packages.props`) so every version lives in one file and new packages are visible in pull requests.
