# Task management API decisions

Status: shared understanding confirmed; implementation complete.

## Technical baseline

Follow [CODE_STYLE.md](CODE_STYLE.md) and [STACK.md](STACK.md): .NET 10 / C# 14, ASP.NET Core Minimal APIs, feature services, a synchronized singleton dictionary store, JWT authentication, and xUnit HTTP integration tests. Use Development-only Scalar for API exploration. No database provider or persistent storage.

## Confirmed product decisions

- Build a REST API for creating, reading, updating, and deleting task tickets, with user authentication.
- Tickets contain title, description, status, and a due date.
- The creator remains the owner after reassignment. Tickets expose separate `createdByUserId` and `assignedToUserId` fields; the creator ID comes from the authenticated caller.
- Every ticket has an assignee. Creation may specify `assignedToUserId`; omission defaults to the creator. Assignment must reference an existing user; an unknown user ID produces a validation error.
- JSON follows the baseline camelCase convention, including `dueDate`.
- Title is required, trimmed, and 1–200 characters after trimming. Description is optional, at most 5,000 characters, and becomes `null` when omitted.
- Status values are `todo`, `in_progress`, and `done`. New tickets start as `todo`; any transition between these states is allowed.
- Creation requests have no status field: POST always creates a `todo` ticket. Change status through PUT, where status is required. Non-todo seed fixtures are initialized directly by the seed process.
- The due date is optional, uses a calendar date (`YYYY-MM-DD`), and may be in the past. It has no time or time zone.
- Restarting the host discards users and tickets and invalidates tokens. Startup reseeds demo data; there is no persistence requirement.
- Use the baseline Alice and Bob seed users. Seed three tickets with stable IDs and fixed example dates: Alice's `todo`, Alice's `in_progress` assigned to Bob, and Bob's `done`.
- No user directory or search endpoint. Callers use documented seed IDs or IDs returned by registration.
- The implementation README must list Alice and Bob's stable IDs, emails (`alice@example.test`, `bob@example.test`), and public demo password (`DemoPassword123!`). Scalar must provide prefilled login credentials for these seeded users through request examples; retain the baseline Charlie registration example. Never prefill bearer tokens.
- The first version targets local development through the API explorer or an API client, without a frontend or public deployment.
- Solution name: `TaskTickets`, with `TaskTickets.Api` and `TaskTickets.Api.Tests` projects.

## Ticket permissions

- The creator and current assignee can read a ticket and edit its title, description, status, and due date.
- Only the creator can reassign or delete a ticket.
- A previous assignee loses access after reassignment unless they are the creator.
- Concurrent edits use last-write-wins, without version fields, ETags, or conflict detection. Authorization checks and mutations occur atomically under the store lock; a former assignee cannot edit after reassignment completes unless they are the creator.
- Unrelated users receive 404 for ticket operations to avoid exposing its existence. Visible tickets requested through creator-only operations by a non-creator return 403, following CODE_STYLE.md.

## HTTP contract

All ticket endpoints require authentication. Responses expose ticket ID, title, description, status, dueDate, createdByUserId, and assignedToUserId using camelCase JSON.

| Method | Route | Behavior |
|---|---|---|
| POST | `/api/task-tickets` | Create; return 201, ticket response, and Location. |
| GET | `/api/task-tickets` | List tickets created by or currently assigned to the caller, without duplicates. |
| GET | `/api/task-tickets/{id}` | Read a visible ticket. |
| PUT | `/api/task-tickets/{id}` | Replace editable fields; title and status required; omitted description or dueDate clears them. Return the updated ticket. |
| PUT | `/api/task-tickets/{id}/assignee` | Creator-only reassignment using assignedToUserId; return the updated ticket. |
| DELETE | `/api/task-tickets/{id}` | Creator-only permanent deletion; return 204. |

- Lists support optional status filtering and page/pageSize pagination (default page 1, pageSize 20; maximum pageSize 100). Invalid pagination or status returns 400.
- Sort newest first, with deterministic ordering for equal creation times. Store an internal UTC creation timestamp using TimeProvider for ordering; no additional public timestamp fields are required.
- List response: `{ items, page, pageSize, totalCount }`. Count only visible tickets matching the filter, before pagination; pages beyond the result set return empty items.

## Implementation and verification scope

- Add the Tasks feature using the baseline endpoint/service/store structure, request/response DTOs, atomic authorization and mutations, and immutable snapshots.
- Extend startup seed data, OpenAPI examples, README usage, and HTTP integration tests for the ticket contract.
- Cover CRUD, validation boundaries, assignment to unknown users, creator/assignee/unrelated-user permissions, loss of access after reassignment, status transitions, list filtering/pagination/order, and restart reseeding.
- Retain all baseline authentication, concurrency, documentation, and quality checks in CODE_STYLE.md.

## Implementation sequence

1. Create the TaskTickets solution, API/test projects, pinned SDK/packages, and repository configuration following the two baseline guides.
2. Implement and test the baseline store, authentication, seed users, errors, rate limiting, and Development-only OpenAPI/Scalar.
3. Implement ticket models, request/response contracts, validation, synchronized storage, services, endpoints, and the three ticket fixtures. Use test-first development for validation and concurrent authorization/mutations.
4. Add HTTP acceptance tests for the agreed ticket behavior, including last-write-wins and creation always starting in todo.
5. Document run/test commands, reset behavior, seed user IDs and credentials, routes, permissions, and examples in README. Supply both seeded login examples and the Charlie registration example in Scalar/OpenAPI.
6. Run dotnet restore, dotnet build, dotnet format --verify-no-changes, and dotnet test; resolve failures before reporting implementation complete.

## Review status

No open product decisions remain. The user confirmed shared understanding and authorized implementation. Restart data loss, public demo credentials, single-instance hosting, and possible overwrites from last-write-wins are intentional demo limitations.

Implementation includes the API, seeded users/tickets, Scalar examples, README, CI workflow, and 45 passing HTTP integration tests. Local restore used cached pinned packages because NuGet TLS failed; online vulnerability auditing was not verified. Build and format checks are part of the completion gates above.
