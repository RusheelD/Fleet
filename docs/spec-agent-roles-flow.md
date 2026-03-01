# Fleet — Agent Roles & Execution Flow

## Agent Role Definitions

Detailed role specifications will be provided in `.agent.md` files (to be added separately). This document covers the role overview and execution flow.

### Roles Overview

| Role | Phase | Responsibility |
| --- | --- | --- |
| **Manager** | Orchestration | Analyzes work item, assigns roles, coordinates phases, decides if re-iteration is needed |
| **Planner** | Phase 1 | Creates to-dos, breaks down the work item into actionable sub-tasks |
| **Contracts** | Phase 2 | Defines data models, API interfaces, and type definitions — mirrored between frontend and backend |
| **Backend** | Phase 3 (parallel) | Implements server-side logic, endpoints, data access |
| **Frontend** | Phase 3 (parallel) | Implements UI components, pages, client-side logic |
| **Testing** | Phase 3 (parallel) | Writes unit, integration, and e2e tests |
| **Styling** | Phase 3 (parallel) | Applies visual design, theming, responsive layouts |
| **Consolidation** | Phase 4 | Merges outputs from parallel agents, resolves conflicts |
| **Review** | Phase 5 (parallel) | Code review, quality checks, standards adherence |
| **Documentation** | Phase 5 (parallel) | Writes/updates docs, READMEs, inline comments, API docs |

## Execution Flow

```text
┌─────────┐
│ Manager │  Analyzes work item, assigns roles to agents
└────┬────┘
     │
     ▼
┌──────────┐
│ Planner  │  Creates to-dos, breaks work into sub-tasks
└────┬─────┘
     │
     ▼
┌───────────┐
│ Contracts │  Defines data models & interfaces (frontend + backend)
└────┬──────┘
     │
     ▼
┌──────────────────────────────────────────┐
│  Phase 3 — Parallel Execution            │
│  ┌──────────┐  ┌──────────┐              │
│  │ Backend  │  │ Frontend │              │
│  └──────────┘  └──────────┘              │
│  ┌──────────┐  ┌──────────┐              │
│  │ Testing  │  │ Styling  │              │
│  └──────────┘  └──────────┘              │
└────────────────────┬─────────────────────┘
                     │
                     ▼
           ┌───────────────┐
           │ Consolidation │  Merges code, resolves conflicts
           └───────┬───────┘
                   │
                   ▼
     ┌─────────────────────────────┐
     │  Phase 5 — Parallel         │
     │  ┌────────┐  ┌─────────┐    │
     │  │ Review │  │  Docs   │    │
     │  └────────┘  └─────────┘    │
     └─────────────┬───────────────┘
                   │
                   ▼
            ┌─────────┐
            │ Manager │  Final check — if large bugs or changes
            └────┬────┘  needed, loop back to appropriate phase
                 │
                 ▼
            ┌────────┐
            │   PR   │  Open pull request(s) in GitHub
            └────────┘
```

### Phase Rules

1. **Sequential phases (1→2→3→4→5)** — each phase must complete before the next begins
2. **Parallel within phases** — agents in the same phase run concurrently
3. **Contracts before code** — the Contracts agent runs before Backend/Frontend/Testing/Styling so that all parallel agents share the same data models and interfaces
4. **Consolidation is singular** — one agent merges all parallel outputs and resolves conflicts before review begins
5. **Manager re-iteration** — after Review/Docs, the Manager evaluates the result. If large bugs or significant changes are needed, it loops back to the appropriate phase (not necessarily the beginning)

## Agent Communication

Agents communicate **directly with each other** (not solely through the manager):

- **Contracts → Code agents**: The contracts agent produces shared type definitions and interfaces that Backend, Frontend, Testing, and Styling agents all consume
- **Parallel agents**: Can read each other's in-progress outputs (e.g., the testing agent can see what the backend agent is producing)
- **Consolidation**: Has full visibility into all parallel agent outputs to merge and resolve conflicts
- **Manager**: Receives status from all agents and can intervene if needed

> **Note:** The exact communication mechanism (shared file system, message passing, shared memory/context) is TBD and will depend on the agent execution infrastructure chosen.
