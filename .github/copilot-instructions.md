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

### Three-Tier Architecture: Controllers → Services → Repositories

The backend follows a strict **Controllers → Services → Repositories** layered architecture:

| Layer            | Responsibility                                                    | Location                                       |
| ---------------- | ----------------------------------------------------------------- | ---------------------------------------------- |
| **Controllers**  | HTTP endpoints — accept requests, call services, return responses | `Fleet.Server/Controllers/`                    |
| **Services**     | Business logic — orchestrate repositories and other services      | Domain folders (e.g., `Fleet.Server/Copilot/`) |
| **Repositories** | Data access — communicate with database(s) directly               | Domain folders (same as their service)         |

**Dependency rules:**

- Controllers depend on **Services** only — never call Repositories directly
- Services depend on **Repositories** and other **Services** — never access HTTP context
- Repositories depend on the **database** only — no business logic, no service calls

**Interface requirement:** Every service and repository **must** have an interface. Inject the interface (not the concrete class) everywhere. Register implementations in `Program.cs` via DI.

### Controllers

- **MVC Controllers** with `[ApiController]` — each domain area gets its own controller in `Controllers/`
- Inherit from `ControllerBase` (not `Controller`) — API-only, no view support
- Use `[Route("api/[controller]")]` or explicit route templates for nested resources
- Controllers are thin — validate inputs, call a service method, return the result
- Inject services via constructor: `public ProjectsController(IProjectService projectService)`

### Services

- Live in **domain folders** alongside their repositories (e.g., `Fleet.Server/Copilot/`, `Fleet.Server/Projects/`)
- Always defined as an **interface + implementation** pair (e.g., `ICopilotChatService` + `CopilotChatService`)
- Contain business logic, validation rules, and orchestration
- May call multiple repositories and other services
- Use `async`/`await` for all I/O-bound operations

### Repositories

- Live in the **same domain folder** as their related service
- Always defined as an **interface + implementation** pair (e.g., `IChatSessionRepository` + `ChatSessionRepository`)
- Responsible for all database communication (queries, inserts, updates, deletes)
- Return domain entities or DTOs — no HTTP or business logic concerns
- One repository per aggregate root / entity group

### Domain Folder Structure

Services and repositories are organized by **domain** — the model or feature area they act on:

```
Fleet.Server/
  Program.cs                          → App setup, DI registration, middleware
  Extensions.cs                       → Aspire service defaults
  Controllers/
    ProjectsController.cs             → [Route("api/projects")]
    WorkItemsController.cs            → [Route("api/projects/{projectId}/work-items")]
    ChatsController.cs                → [Route("api/projects/{projectId}/chat")]
    AgentsController.cs               → [Route("api/projects/{projectId}/agents")]
  Projects/
    IProjectService.cs                → Interface for project business logic
    ProjectService.cs                 → Implementation
    IProjectRepository.cs             → Interface for project data access
    ProjectRepository.cs              → Implementation
  Copilot/
    ICopilotChatService.cs            → Interface for AI chat orchestration
    CopilotChatService.cs             → Implementation
    IChatSessionRepository.cs         → Interface for chat session data access
    ChatSessionRepository.cs          → Implementation
  WorkItems/
    IWorkItemService.cs
    WorkItemService.cs
    IWorkItemRepository.cs
    WorkItemRepository.cs
  Agents/
    IAgentService.cs
    AgentService.cs
    IAgentTaskRepository.cs
    AgentTaskRepository.cs
```

### DI Registration

All services and repositories are registered in `Program.cs`:

```csharp
// Services
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ICopilotChatService, CopilotChatService>();

// Repositories
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
```

### Other Conventions

- Service defaults (health checks, OpenTelemetry, resilience, service discovery) live in `Extensions.cs` and are registered via `builder.AddServiceDefaults()`
- Health endpoints: `/health` (readiness), `/alive` (liveness) — dev only
- OpenAPI is enabled in development (`app.MapOpenApi()`)
- Redis output caching: use `[OutputCache]` attribute on controller actions
- `Fleet.Server.http` contains sample HTTP requests for manual testing
- Use records for DTOs and request/response models; classes for services and repositories

## Frontend Conventions (frontend/)

- **React 19** with **TypeScript strict mode** (`noUnusedLocals`, `noUnusedParameters`, `erasableSyntaxOnly`)
- **ALL UI must use Fluent UI v9 components and icons wherever possible.** Use components from `@fluentui/react-components` and icons from `@fluentui/react-icons`. Do not use Material UI, Chakra, or other component libraries.
- **Minimize raw HTML in `.tsx` files.** Instead of `<div>`, `<span>`, `<button>`, etc., use their Fluent UI equivalents (`<Card>`, `<Text>`, `<Button>`, `<Divider>`, etc.). Raw HTML elements are only acceptable when no Fluent UI equivalent exists or for structural layout wrappers.
- Styling: use `makeStyles` from `@fluentui/react-components` and Fluent `tokens` for colors/spacing — no CSS modules, Tailwind, or inline style objects (except trivial one-offs)
- `FluentProvider` wraps the app in `main.tsx` with automatic dark/light theme
- API calls go to `/api/*` (proxied by Vite in dev)
- ESLint config: `eslint.config.js` — run `npm run lint` from `frontend/`

### File Organization

- **One component per `.tsx` file.** Every React component must live in its own file — no multi-component files.
- **Every directory must have an `index.ts` barrel file** that re-exports all public members from that directory. This enables clean imports like `from './pages'` or `from './components/layout'`.
- **Reusable UI must be extracted to its own component.** If a piece of UI appears in more than one place, or is a logically distinct unit (card, list item, dialog, tab panel), extract it.

### Data Models (`src/models/`)

- **All shared data-model interfaces and types live in `src/models/`** — one file per domain (e.g., `work-item.ts`, `agent.ts`, `project.ts`, `chat.ts`, `plan.ts`, `search.ts`, `navigation.ts`).
- `src/models/index.ts` barrel re-exports everything for a single import point: `import type { WorkItem, ProjectData } from '../../models'`.
- **Props interfaces stay co-located with their component** — they are component-specific and should NOT be moved to `models/`.
- When creating a new domain model, add the file to `src/models/` and re-export it from `src/models/index.ts`.

### Component & Page Structure

```
src/
  models/              # Shared interfaces & types (one file per domain + index.ts barrel)
  components/
    index.ts           # Barrel: re-exports from layout/, chat/, shared/
    layout/            # Layout shell components (Layout, SidebarNavItem, TopBar, etc.)
      index.ts
    chat/              # Chat drawer components (ChatDrawer, ChatMessage, etc.)
      index.ts
    shared/            # Reusable atoms (PageHeader, EmptyState, SettingRow)
      index.ts
  pages/
    index.ts           # Barrel: re-exports all page components
    ProjectsPage/      # One folder per page
      ProjectsPage.tsx # Main page component
      ProjectCard.tsx  # Sub-components specific to this page
      index.ts         # Barrel for the folder
    ...
```

## Adding New API Endpoints

1. **Controller** — Create or extend a controller in `Fleet.Server/Controllers/` using `[ApiController]` and `ControllerBase`
2. **Service** — Create an interface (`I<Name>Service`) and implementation (`<Name>Service`) in the appropriate domain folder (e.g., `Fleet.Server/Projects/`)
3. **Repository** — If the feature requires data access, create an interface (`I<Name>Repository`) and implementation (`<Name>Repository`) in the same domain folder
4. **Register DI** — Add `builder.Services.AddScoped<IService, Service>()` and `builder.Services.AddScoped<IRepository, Repository>()` in `Program.cs`
5. **Inject** — Inject the service interface into the controller via constructor
6. **Cache** — Apply `[OutputCache]` if the response is cacheable
7. **Test** — Add a sample request to `Fleet.Server.http` for manual testing

## Adding New Frontend Pages/Components

1. Create a new folder under `frontend/src/pages/<PageName>/`
2. Add the main page component (`<PageName>.tsx`) and any sub-components — one component per `.tsx` file
3. Add an `index.ts` barrel that re-exports the page component (and sub-components if they're reused elsewhere)
4. Re-export the page from `frontend/src/pages/index.ts`
5. Any new shared data-model interfaces go in `frontend/src/models/<domain>.ts` and are re-exported from `frontend/src/models/index.ts`
6. Use Fluent UI components and icons — minimize raw HTML elements
7. Style with `makeStyles` and Fluent `tokens`
8. Fetch data from `/api/*` endpoints

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
- `Fleet.Server/Program.cs` — App setup, middleware pipeline, DI registration, `AddControllers()`
- `Fleet.Server/Extensions.cs` — shared Aspire service defaults (do not duplicate in other projects)
- `Fleet.Server/Controllers/` — API controller classes (one per domain area)
- `Fleet.Server/<Domain>/` — Service + Repository pairs per domain (e.g., `Projects/`, `Copilot/`, `WorkItems/`, `Agents/`)
- `frontend/src/main.tsx` — app bootstrap with FluentProvider
- `frontend/src/App.tsx` — main application component
- `frontend/vite.config.ts` — dev proxy configuration
