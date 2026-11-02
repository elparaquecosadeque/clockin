# TaskTickets

A local C# / .NET 10 Minimal API for task tickets, with JWT authentication and a synchronized in-memory store. The implementation follows [CODE_STYLE.md](CODE_STYLE.md), [STACK.md](STACK.md), and [DECISIONS.md](DECISIONS.md).

**Disposable demo only:** all users and tickets are lost on restart, fixtures are recreated, and all previous tokens become invalid. Demo credentials are public; use fictional data only. Run a single instance: instances do not share data or signing keys.

## Run

Install .NET SDK **10.0.403** (or a later patch in that SDK feature band). From this directory:

```powershell
dotnet dev-certs https --trust
dotnet restore
dotnet run --project src/TaskTickets.Api --launch-profile https
```

Open [Scalar](https://localhost:7285/scalar). The OpenAPI document is at `https://localhost:7285/openapi/v1.json`. Both are anonymous and available only in Development. The launch profile uses Development automatically. HTTP on port 5177 redirects to HTTPS; send credentials and bearer tokens directly over HTTPS.

## Seed users

Both users have the public password **`DemoPassword123!`**.

| User | Email | Stable ID |
|---|---|---|
| Alice | `alice@example.test` | `00000000-0000-4000-8000-000000000001` |
| Bob | `bob@example.test` | `00000000-0000-4000-8000-000000000002` |

In Scalar:

1. Open `POST /api/auth/login` and choose the prefilled **Alice** or **Bob** request example. Execute it.
2. Copy the returned `accessToken` into Scalar's **Bearer** authentication field. No token is prefilled or persisted by the server.
3. Call `GET /api/auth/me`, then list or edit tickets.
4. Try `POST /api/auth/register` with the prefilled Charlie example. Registration returns the user's public ID, name, and email, without a token. Log in separately as Charlie.

There is no user directory. Assign tickets using these documented IDs or IDs returned by registration. Tokens expire after 30 minutes; log in again. No refresh token, logout, password reset, or individual token revocation is provided.

### Login and registration

```http
POST /api/auth/login
Content-Type: application/json

{"email":"alice@example.test","password":"DemoPassword123!"}
```

```http
POST /api/auth/register
Content-Type: application/json

{"displayName":"Charlie","email":"charlie@example.test","password":"DemoPassword123!"}
```

Registration trims display names and emails, compares emails case-insensitively, and preserves passwords exactly. Names must be 1–100 characters, emails valid and at most 254 characters, and passwords 12–128 characters. Responses never include password hashes.

## Ticket API

All ticket routes require `Authorization: Bearer <accessToken>`.

| Method | Route | Success |
|---|---|---|
| POST | `/api/task-tickets` | 201 with ticket and Location |
| GET | `/api/task-tickets?status=todo&page=1&pageSize=20` | 200 with paginated visible tickets |
| GET | `/api/task-tickets/{id}` | 200 with ticket |
| PUT | `/api/task-tickets/{id}` | 200 with updated ticket |
| PUT | `/api/task-tickets/{id}/assignee` | 200 with reassigned ticket |
| DELETE | `/api/task-tickets/{id}` | 204; permanent deletion |

Create a ticket assigned to Bob:

```json
{
  "title": "Review the API",
  "description": "Try the seeded examples in Scalar.",
  "dueDate": "2026-11-02",
  "assignedToUserId": "00000000-0000-4000-8000-000000000002"
}
```

Creation always sets status to `todo`. Omit `assignedToUserId` to assign to yourself. The creator is always derived from the token. Every assignee must exist.

Replace editable fields:

```json
{
  "title": "Review complete",
  "description": null,
  "status": "done",
  "dueDate": null
}
```

PUT requires title and status. Omitted description or dueDate clears that field. Unknown JSON properties are rejected; PUT cannot change the creator or assignee. Reassign separately with `{ "assignedToUserId": "<existing-user-id>" }`.

- Title: required, trimmed, 1–200 characters. Description: optional, up to 5,000 characters.
- Status: `todo`, `in_progress`, or `done`; all transitions are allowed. Undefined and numeric enum values are rejected.
- Due date: optional `YYYY-MM-DD` calendar date, without a time zone. Past dates are allowed.
- Creator and current assignee can read and edit. Only the creator can reassign or delete. A former assignee loses access unless they are the creator.
- Unrelated users receive 404; a visible ticket's assignee receives 403 for creator-only actions. Missing or invalid authentication returns 401.
- Lists include tickets created by or currently assigned to you, without duplicates, newest first (ID descending breaks timestamp ties). Optional `status` filtering applies before pagination and counting. Page starts at 1; pageSize defaults to 20 and must be 1–100. Pages beyond the end return empty items.
- List responses contain `items`, `page`, `pageSize`, and `totalCount`. Ticket responses contain `id`, `title`, `description`, `status`, `dueDate`, `createdByUserId`, and `assignedToUserId`.
- Concurrent edits use last-write-wins, so one user's edit can overwrite another's. Permission checks and mutations are atomic under the same lock.

### Ticket fixtures

| ID suffix | Creator → assignee | Title | Status | Due date |
|---|---|---|---|---|
| `101` | Alice → Alice | Plan the demo | `todo` | 2026-11-01 |
| `102` | Alice → Bob | Review the API | `in_progress` | 2026-11-02 |
| `103` | Bob → Bob | Explore Scalar | `done` | null |

Full fixture IDs are `00000000-0000-4000-8000-000000000101` through `00000000-0000-4000-8000-000000000103`. Dates are fixed examples and may become overdue. Each user initially sees two tickets.

## Errors and configuration

Errors use ProblemDetails; validation errors include field errors. Invalid input returns 400, duplicate email 409, and unexpected failures a generic 500 with a trace ID. Authentication endpoints share limits of 10 requests per minute per client IP and 100 per minute globally; 429 responses include Retry-After. Forwarded IP headers are not trusted.

`src/TaskTickets.Api/appsettings.json` contains typed, startup-validated `Auth` issuer/audience/lifetime and `AuthRateLimit` settings. Environment variables such as `Auth__Issuer` can override them. The random signing key exists only in host memory and is never configured or logged. Allowed hosts default to localhost and loopback addresses. No CORS or external database setup is required.

## Verify

```powershell
dotnet restore
dotnet build --no-restore
dotnet format --no-restore --verify-no-changes
dotnet test --no-restore
```

Tests use xUnit and isolated `WebApplicationFactory` hosts with the real store, password hasher, seed data, and bearer middleware. They cover authentication, malformed tokens, validation, rate limits, restart resets, CRUD, access rules, reassignment, concurrent edits, list behavior, and documentation. Expiration tests advance an injected clock without sleeping. CI runs the same checks with warnings as errors.

If NuGet networking is unavailable and all pinned packages already exist in your global cache, restore offline with:

```powershell
dotnet restore --source "$env:USERPROFILE\.nuget\packages" -p:NuGetAudit=false
```

This offline workaround skips online vulnerability auditing; ordinary restore and CI retain the default audit behavior.

## Structure

`Features/Auth` and `Features/Tasks` contain endpoints, direct services, and contracts. `Data/InMemoryStore.cs` owns private dictionaries and their lock; `Data/DemoSeed.cs` supplies fixtures. `Common` contains authentication setup, token issuance, configuration, error handling, and OpenAPI customization. There are no database providers, generic repositories, mediator layers, or frontend.

OpenAPI customization follows [Microsoft's transformer documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0); API exploration uses the [Scalar ASP.NET Core integration](https://github.com/scalar/scalar/blob/main/documentation/integrations/aspnetcore/integration.md).
