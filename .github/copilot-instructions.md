# Copilot Instructions for Fleet

Fleet orchestrates AI agents to plan, build, and complete software tasks in GitHub.

## Architecture

Three-project .NET Aspire solution (`Fleet.slnx`) targeting **net10.0**:

| Project         | Role                                                                                                                          |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `Fleet.AppHost` | Aspire orchestrator — wires Redis, the API server, and the Vite frontend. **Entry point for running the app.**                |
| `Fleet.Server`  | ASP.NET Core API (MVC controllers). Serves `/api/*` endpoints and static files (`wwwroot`). Uses Redis-backed output caching. |
| `frontend`      | React 19 + TypeScript SPA built with Vite. Uses **Fluent UI v9** (`@fluentui/react-components`) for all UI.                   |

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
- **ALL UI must use Fluent UI v9 components and icons wherever possible.** Use components from `@fluentui/react-components` and icons from `@fluentui/react-icons`. Do not use Material UI, Chakra, or other component libraries.
- **Minimize raw HTML in `.tsx` files.** Instead of `<div>`, `<span>`, `<button>`, etc., use their Fluent UI equivalents (`<Card>`, `<Text>`, `<Button>`, `<Divider>`, etc.). Raw HTML elements are only acceptable when no Fluent UI equivalent exists or for structural layout wrappers.
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
2. Use Fluent UI components and icons — minimize raw HTML elements
3. Style with `makeStyles` and Fluent `tokens`
4. Fetch data from `/api/*` endpoints

## Code Quality — Zero Tolerance for Errors & Warnings

**ALL errors and warnings MUST be fixed** — including compiler errors, linting warnings, TypeScript strict-mode violations, and ESLint issues. No suppression comments (`// eslint-disable`, `// @ts-ignore`, `#pragma warning disable`) unless there is a documented, unavoidable reason.

- **Backend:** Build must produce zero warnings. Run `dotnet build` and fix everything.
- **Frontend:** `npm run lint` and `npx tsc --noEmit` must both pass with zero warnings. Fix all ESLint and TypeScript errors before committing.

## Coding Standards

### General

- Write clean, readable code — prefer clarity over cleverness
- Use meaningful, descriptive names for variables, functions, classes, and files
- Keep functions small and focused on a single responsibility
- Don't leave dead code, commented-out code, or TODO comments without a linked work item
- Every public API (controller action, service method, component prop) should have clear intent from its name; add doc comments when the name alone isn't sufficient

### Backend (C#)

- Use `async`/`await` for all I/O-bound operations — never block with `.Result` or `.Wait()`
- Use records for DTOs and immutable data; classes for stateful services
- Validate inputs at the controller boundary; trust data in inner service layers
- Use dependency injection — no `new` for services; register in `Program.cs`
- Follow C# naming conventions: `PascalCase` for public members, `_camelCase` for private fields

### Frontend (TypeScript/React)

- Use functional components with hooks — no class components
- Prefer named exports over default exports (except for page-level components)
- Type everything explicitly — avoid `any`; use `unknown` if the type is truly unknown
- Co-locate related files (component + styles + tests) in the same folder
- Destructure props in function signatures for readability

## Key Files

- `Fleet.AppHost/AppHost.cs` — service orchestration and resource wiring
- `Fleet.Server/Controllers/` — API controller classes (one per domain area)
- `Fleet.Server/Program.cs` — App setup, middleware pipeline, `AddControllers()`
- `Fleet.Server/Extensions.cs` — shared Aspire service defaults (do not duplicate in other projects)
- `frontend/src/main.tsx` — app bootstrap with FluentProvider
- `frontend/src/App.tsx` — main application component
- `frontend/vite.config.ts` — dev proxy configuration
