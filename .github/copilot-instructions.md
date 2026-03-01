# Copilot Instructions for Fleet

Fleet orchestrates AI agents to plan, build, and complete software tasks in GitHub.

## Architecture

Three-project .NET Aspire solution (`Fleet.slnx`) targeting **net10.0**:

| Project         | Role                                                                                                                       |
| --------------- | -------------------------------------------------------------------------------------------------------------------------- |
| `Fleet.AppHost` | Aspire orchestrator — wires Redis, the API server, and the Vite frontend. **Entry point for running the app.**             |
| `Fleet.Server`  | ASP.NET Core API (MVC controllers). Serves `/api/*` endpoints and static files (`wwwroot`). Uses Redis-backed output caching. |
| `frontend`      | React 19 + TypeScript SPA built with Vite. Uses **Fluent UI v9** (`@fluentui/react-components`) for all UI.                |

**Data flow:** Browser → Vite dev server (proxies `/api/*` via `SERVER_HTTPS`/`SERVER_HTTP` env vars) → `Fleet.Server` → Redis (output cache).

In production, the frontend is published as static files into the server's `wwwroot` folder (`server.PublishWithContainerFiles(webfrontend, "wwwroot")` in `AppHost.cs`).

## Running the App

```powershell
# Start everything (Redis, server, frontend) via Aspire:
dotnet run --project Fleet.AppHost
```

The Aspire dashboard opens automatically. Do **not** run `Fleet.Server` or the Vite dev server independently — the AppHost manages service discovery and environment wiring.

## Backend Conventions (Fleet.Server)

- **MVC Controllers** with `[ApiController]` — each domain area gets its own controller in `Controllers/` (e.g., `ProjectsController`, `WorkItemsController`)
- Inherit from `ControllerBase` (not `Controller`) — API-only, no view support
- Use `[Route("api/[controller]")]` or explicit route templates for nested resources
- Service defaults (health checks, OpenTelemetry, resilience, service discovery) live in `Extensions.cs` and are registered via `builder.AddServiceDefaults()`
- Health endpoints: `/health` (readiness), `/alive` (liveness) — dev only
- OpenAPI is enabled in development (`app.MapOpenApi()`)
- Redis output caching: use `[OutputCache]` attribute on controller actions
- `Fleet.Server.http` contains sample HTTP requests for manual testing

## Frontend Conventions (frontend/)

- **React 19** with **TypeScript strict mode** (`noUnusedLocals`, `noUnusedParameters`, `erasableSyntaxOnly`)
- **Fluent UI v9** only — do not use Material UI, Chakra, or other component libraries
- Styling: use `makeStyles` from `@fluentui/react-components` and Fluent `tokens` for colors/spacing — no CSS modules, Tailwind, or inline style objects (except trivial one-offs)
- `FluentProvider` wraps the app in `main.tsx` with automatic dark/light theme
- API calls go to `/api/*` (proxied by Vite in dev)
- ESLint config: `eslint.config.js` — run `npm run lint` from `frontend/`

## Adding New API Endpoints

1. Create or extend a controller in `Fleet.Server/Controllers/` using `[ApiController]` and `ControllerBase`
2. Apply `[OutputCache]` if the response is cacheable
3. Add a sample request to `Fleet.Server.http` for manual testing

## Adding New Frontend Pages/Components

1. Create components in `frontend/src/`
2. Use Fluent UI components and `makeStyles` for styling
3. Fetch data from `/api/*` endpoints

## Key Files

- `Fleet.AppHost/AppHost.cs` — service orchestration and resource wiring
- `Fleet.Server/Controllers/` — API controller classes (one per domain area)
- `Fleet.Server/Program.cs` — App setup, middleware pipeline, `AddControllers()`
- `Fleet.Server/Extensions.cs` — shared Aspire service defaults (do not duplicate in other projects)
- `frontend/src/main.tsx` — app bootstrap with FluentProvider
- `frontend/src/App.tsx` — main application component
- `frontend/vite.config.ts` — dev proxy configuration
