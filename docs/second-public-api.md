# SecondPublicApi

`ClockIn.SecondPublicApi` is a separate Development-only controller host for account
creation, activation, login and authorization. It shares the existing database and
application handlers, without Wolverine. It is not deployed remotely. See
[ADR 0011](adr/0011-second-public-api-account-workflows.md) and the
[feature specification](specs/second-public-api/accounts.feature).

## Run locally

From the repository root, start the existing API once to migrate and seed:

```sh
dotnet run --project backend/src/ClockIn.Api --launch-profile http
```

After startup finishes, you may leave it running or stop it. Then run:

```sh
dotnet run --project backend/src/ClockIn.SecondPublicApi --launch-profile http
```

- Scalar: **http://localhost:5246/scalar/v1**
- OpenAPI: **http://localhost:5246/openapi/v1.json**
- Anonymous information: **http://localhost:5246/api/v1/info**

The default database is `backend/src/ClockIn.Api/clockin.dev.db`. SecondPublicApi
never creates, migrates, seeds or resets it. For a different database, configure
`Database__Provider`, `Database__ConnectionString` and `Company__TimeZone`
consistently in all hosts, and prepare it through the existing API first.

## Endpoints and access

| Method | Route | Access / result |
|---|---|---|
| GET | `/api/v1/info` | Anonymous; only API name and version |
| POST | `/api/v1/auth/login` | Anonymous; email/password returns `accessToken`, `expiresAt` |
| POST | `/api/v1/auth/set-password` | Anonymous; secret activation token and password; returns 204 |
| GET | `/api/v1/me` | Any authenticated user; identity, current permissions and team |
| POST | `/api/v1/users` | Admin (`ManageUsers`) only; returns activation `token`, `expiresAt` |

Tokens last 15 minutes. Sign in again after expiry or a host restart. Tokens from
the SPA API or first PublicApi cannot be used here, or vice versa. There are no
refresh cookies, logout, user-list, update or delete endpoints. Login and activation
share the per-client sign-in rate limit. Existing account lockout, current
permissions, session and security-stamp checks apply.

## Scalar walkthrough and prefilled data

1. Call `/info` anonymously. Calling `/me` without a token returns 401.
2. Select **Ada** in login: `ada@clockin.demo` / `clockin-demo`. Copy the returned
   `accessToken` into Scalar's Bearer authentication field.
3. Create a user with the **Supervisor** or **Employee** example. Supervisors and
   admins have no team. **Employees require `teamId`**; the employee example reads
   the active **Office** team's real ID. Reload Scalar after demo resets to refresh
   it. If Office is unavailable, prepare demo data or use another active team ID.
4. Copy the returned activation `token` into set-password. The sample password is
   `new-user-password`; the existing rule requires 12–128 characters. This endpoint
   needs no bearer token, but does require a valid, unexpired, single-use activation
   token. Login is rejected before activation.
5. Select **NewSupervisor** in login after creating that example, then call `/me`
   with the new bearer token. For an employee, use the email you created.
6. Creating a user with that non-admin token returns 403. **Mia**
   (`mia@clockin.demo` / `clockin-demo`) demonstrates the same ordinary-user access.

Fixed Scalar emails (`sample-supervisor@clockin.demo`, `sample-employee@clockin.demo`)
return 409 when reused. Change the email to create another account. Creation accepts
the existing Employee, Supervisor and Admin roles and corresponding team rules.
Documentation only reads examples; it never creates users or issues activation tokens.

Errors use shared ProblemDetails and correlation IDs: 400 field validation, 401
invalid/expired bearer session, 403 insufficient permission, 404 unknown team,
409 duplicate email, 422 invalid activation/credentials or archived team, 429 rate limit.

## Bruno and JSON bodies

Open the [collection](../bruno/ClockIn.SecondPublicApi/) and select **Local**. Run
the root requests in order: public info, 401, Ada login, supervisor creation,
activation, new-user login, `/me`, then expected 403, 409 and 400 responses. Error
examples assert their expected status and count as successful demonstrations.

Creation generates a unique email each run. Scripts capture activation and bearer
tokens in runtime variables, without saving them to environment files. The
**Employee** folder reads Office's ID from OpenAPI, creates an employee, activates
it, logs in and calls `/me`. Run Ada login first when using that folder separately.

[Standalone JSON bodies](../bruno/ClockIn.SecondPublicApi/examples/) include Ada/Mia
login, both creation examples, activation and new-supervisor login. Replace the
employee `teamId` placeholder with Scalar/OpenAPI's value and the activation-token
placeholder with the actual creation response.

Accounts and sessions are shared. The existing API's daily demo reset removes
newly created users and invalidates sessions and activation tokens. Use fresh
IDs/tokens after a reset. SecondPublicApi never schedules its own reset.
