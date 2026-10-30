# Architecture diagrams

Excalidraw diagrams (`.excalidraw`, plus an exported `.svg` for previews) describing the system. **Update them on each major version.**

| Diagram | Shows |
|---|---|
| `architecture.excalidraw` | Projects and layers (Domain, Application, Persistence, Infrastructure, Api, PublicApi), their dependencies, the Angular app, databases and external systems. |
| `message-flow.excalidraw` | How a request travels: controller → mediator → validation → transaction → handler → repository → domain events → event handlers → commit, and how a query reaches the reads. |

Keep diagrams at the level of projects, features and flows; don't mirror individual classes.
