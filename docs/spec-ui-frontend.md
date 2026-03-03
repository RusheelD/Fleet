# Fleet — UI Structure & Frontend Architecture

## Navigation & Pages

Fleet's navigation follows an **Azure DevOps-like** layout with a project-centric hierarchy:

### Top-Level Views

| Page | Description |
| --- | --- |
| **All Projects** | List/grid of the user's projects with quick stats |
| **Search** | Global search across projects, work items, chats |
| **Settings** | Account settings, linked GitHub accounts, preferences |
| **Subscription** | Plan management, usage metrics, billing |

### Per-Project Views (scoped to a selected project)

| Page | Description |
| --- | --- |
| **Dashboard** | Project overview — recent activity, agent status, key metrics |
| **Work Items** | Board/list view of all work items (ADO-style boards, backlog, sprint views) |
| **Agent Monitor** | Status dashboard + log stream access for running agents |

### Chat Pane

The AI chat is **not a separate page** — it is a **slide-out drawer/pane** accessible from any project-scoped view. Users can open the chat from the sidebar or a button on any project page (Dashboard, Work Items, Agent Monitor). This keeps the chat contextually available without navigating away from the current view.

- The chat pane overlays the current page content (typically on the right side)
- It persists session state while the user switches between project pages
- Chat sessions are scoped to the current project

### Navigation Model

- **Global nav** (sidebar or top bar): All Projects, Search, Settings, Subscription
- **Project nav** (contextual, appears after selecting a project): Dashboard, Work Items, Agent Monitor
- **Chat** is accessible via a toggle button in the project nav — opens a slide-out pane, not a page
- Similar to how ADO scopes navigation to the selected project/org

> **Note:** Exact layout, menu placement, and information architecture will evolve. This captures the intent, not pixel-perfect design.

## Client-Side Routing

**React Router** — standard choice for React SPAs, well-supported by the ecosystem.

### Route Design Principles

- **No IDs in page routes** — Page URLs must be human-readable. For projects, use the project **slug** (derived from the title) instead of an opaque ID. Other entity references (work items, agents, chat sessions) are managed via client-side state (React Context, component state), not URL parameters.
- Backend API routes (e.g., `/api/projects/{projectId}/work-items`) still use IDs — those are API calls, not page routes.

### Route structure

```text
/                                 → Redirect to /projects
/projects                         → All Projects list
/projects/:slug                   → Project Dashboard
/projects/:slug/work-items        → Work Items board/list
/projects/:slug/agents            → Agent Monitor
/settings                         → Account settings
/subscription                     → Plan & billing
```

> **Note:** There is no `/projects/:slug/chat` route. Chat is a pane/drawer accessible from any project-scoped page.

## State Management

**Dual approach:**

| Layer | Tool | Scope |
| --- | --- | --- |
| **Server state** | **TanStack Query** (`@tanstack/react-query`) | API data — projects, work items, agent status, chat history. Handles caching, background refetch, optimistic updates. |
| **Client state** | **React Context** + hooks | UI-only state — theme, sidebar open/closed, selected filters, auth token. |

This keeps server data concerns separate from UI state and avoids over-engineering with a global store.

## Real-Time Communication

| Phase | Transport | Notes |
| --- | --- | --- |
| **MVP** | **Polling** | Simple `setInterval` + TanStack Query `refetchInterval` for agent status and notifications. Quick to implement, no infra changes. |
| **Final** | **SignalR** | ASP.NET SignalR for full real-time: agent log streams, live status updates, push notifications. Server-side hub in `Fleet.Server`, client uses `@microsoft/signalr` package. |

### SignalR Hub Design (future)

- `AgentHub` — streams agent logs, status changes, task progress
- `NotificationHub` — push notifications for PR ready, errors, task completion
- Connected per-project (user subscribes to project channels)
