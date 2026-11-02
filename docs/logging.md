# PostgreSQL application logs

ClockIn sends structured Serilog events directly to `telemetry.public.clockin_logs` in the existing `shared-postgres` PostgreSQL 16 container. The existing Grafana `telemetry` datasource reads them. No Loki, collector, additional database server, or public ingestion endpoint is introduced. The existing browser telemetry receiver continues serving its clients and also cleans up old ClockIn logs.

## Setup and configuration

Run `bash scripts/prepare-production-secrets.sh` to generate the separate ignored `.secrets/clockin-logging-password.txt` without replacing existing secrets. Run `bash scripts/configure-jenkins.sh /path/to/jenkins-local` to apply `scripts/jenkins-logging.patch` and add the password to the server's ignored `.env`. The patch adds the log table and indexes to `telemetry/schema.sql`, a restricted-role setup command, the Grafana dashboard, a Jenkins credential, and log retention in the existing receiver.

From the live Jenkins checkout, run `bash telemetry/setup-clockin-logs.sh` before deploying the API. It idempotently applies the schema and provisions `clockin_log_writer`, granting CONNECT, schema USAGE and INSERT on `clockin_logs`. It grants no SELECT, UPDATE, DELETE or DDL privilege to this writer. Existing `grafana_ro` receives SELECT through the existing schema setup. The table is owned by `telemetry`, whose receiver deletes rows by server-side `received_at` after seven days.

Rebuild/recreate only Jenkins and telemetry after applying their changes: `docker compose up -d --build --no-deps jenkins telemetry`. Grafana's mounted dashboard provisioner loads `grafana/dashboards/clockin.json` automatically. Its dashboard UID is `clockin-logs`, available at `http://localhost:3001/d/clockin-logs` under Apps. The existing datasource and default dashboard remain unchanged.

Jenkins binds `clockin-logging-password` as `CLOCKIN_LOGGING_PASSWORD`. The deploy script creates `CLOCKIN_LOGGING_CONNECTION_STRING` for the dedicated writer, with a maximum pool size of two and short connection/command timeouts; the secret is injected into API environment variable `PostgresLogging__ConnectionString`. `PostgresLogging__Enabled=true` turns shipping on; `PostgresLogging__Release` identifies the deployed image tag. Missing configuration fails startup when enabled. Local development and ordinary API tests leave shipping disabled and keep their normal logging.

## Storage and privacy

Stored fields are event timestamp, severity, rendered message, message template, exception, selected JSON properties, service, application environment, release tag, correlation ID and server receipt time. Safe properties are SourceContext, CorrelationId, RequestMethod, RequestPath, StatusCode, Elapsed, UserId, ActorId and Role. Arbitrary nested payloads and unlisted fields are omitted.

Events carrying credential properties (including bootstrap activation tokens) are excluded from the database sink. Email addresses, labeled password/token/secret values, and Basic/Bearer credentials are redacted in stored text. The separate console sink preserves the existing protected bootstrap-activation workflow. Redaction is an additional safeguard, not permission to log request bodies, credentials, or arbitrary personal data. Restrict access to local console logs.

The dashboard offers severity and correlation-ID filters, recent structured logs, event/error/warning counts and average logged request duration. Send `X-Correlation-Id` with a short plain identifier to find a particular request, or use the response's generated identifier.

## Delivery, retention and recovery

The pinned `Serilog.Sinks.Postgresql.Alternative` 4.3.0 sink batches up to 50 events every two seconds, with an in-memory queue capped at 1,000 events. Requests do not wait for database writes. Console logging remains enabled; Serilog self-diagnostics report a generic delivery failure to stderr without printing credentials. Buffering is best effort: sustained outages, queue overflow or process termination can lose database log events. This is not a durable audit store.

Table creation and cleanup are infrastructure responsibilities. The API cannot create tables or delete logs. Cleanup runs at receiver startup and hourly, independently of the demo database's daily reset. Logs live in `telemetry`; resetting `clockin_demo` does not remove them. Existing cluster backups cover the telemetry database; the application's pre-migration dump covers only demo data.

When rotating the logging password, update the private server value and Jenkins credential, rerun `telemetry/setup-clockin-logs.sh`, and redeploy the API together. Application-image rollback does not reverse logging schema changes. Old releases without the optional logging integration can continue using console logging.

## Validation

`PostgresLoggingTests` runs against disposable PostgreSQL 16 and verifies real INSERT-only delivery, structured metadata, exclusion of activation tokens, redaction of messages/exceptions/nested payloads, preservation of console events, and non-blocking bounded log calls while the logging destination is unreachable. Live verification additionally checks Grafana queries with its existing read-only datasource and the receiver's actual seven-day cleanup.

Validation on 2026-11-01: all 464 backend tests passed, strict build reported zero warnings/errors, and backend format checks passed. All six provisioned Grafana panel queries succeeded. A real rejected sign-in produced redacted audit/request logs with the expected correlation ID and deployed release. Live table privileges, expired/fresh retention probes, the existing telemetry regression suite, Jenkinsfile validation and demo sign-in/refresh checks also passed.
