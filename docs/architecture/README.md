# Architecture diagrams

Excalidraw diagrams (`.excalidraw`, plus an exported `.svg` for previews) describing the system. **Update them on each major version.**

| Diagram | Shows |
|---|---|
| `architecture.excalidraw` | Projects and layers (Domain, Application, Persistence, Infrastructure, Api), their dependencies, the Angular app, the databases and the logs. |
| `message-flow.excalidraw` | How a request travels: pipeline → controller → mediator → validation → transaction → handler → repository → domain events → event subscribers → commit, and how a query reaches the reads. |

Keep diagrams at the level of projects, features and flows; don't mirror individual classes.
