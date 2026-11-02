# Architecture diagrams

Excalidraw diagrams (`.excalidraw`, plus an exported `.svg` for previews) describing the system. **Update them on each major version.**

| Diagram | Shows |
|---|---|
| `architecture.excalidraw` | Projects and layers (Domain, Application, Persistence, Infrastructure, Api), their dependencies, the Angular app, the databases and the logs. |
| `message-flow.excalidraw` | How a request travels: pipeline → controller → mediator → validation → transaction → handler → repository → domain events → event subscribers → commit, and how a query reaches the reads. |
| [`deployment-architecture.excalidraw`](deployment-architecture.excalidraw) ([SVG preview](deployment-architecture.svg)) | Live Cloudflare → cloudflared → nginx/Angular → .NET API topology on Ubuntu WSL, shared PostgreSQL, isolated demo database, Serilog → telemetry logs → existing Grafana, preserved original data, secrets and SQLite development. |
| [`deployment-process.excalidraw`](deployment-process.excalidraw) ([SVG preview](deployment-process.svg)) | Jenkins quality gates, versioned Docker builds, provisioning, backups, migrations, deployment, smoke checks, failure handling and schema-compatible manual rollback. |

Keep diagrams at the level of projects, features and flows; don't mirror individual classes.

The deployment diagrams describe the public demo and the prepared Jenkins pipeline. The live site was initially deployed from the local checkout; its Jenkins job remains disabled until explicitly enabled. To edit, open an `.excalidraw` file in Excalidraw. SVG files are matching static previews.
