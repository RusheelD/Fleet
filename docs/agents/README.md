# Fleet — Agent Role Prompts

This folder contains the **system prompt definitions** for each agent role in Fleet's multi-agent execution pipeline. When Fleet orchestrates AI agents to complete a work item, each agent is initialized with the prompt from its corresponding file.

## How These Files Are Used

1. A user assigns agents to a work item (or Fleet auto-detects the count)
2. The **Manager** agent analyzes the work item and assigns roles to worker agents
3. Each worker agent receives the system prompt from its `.prompt.md` file as the role definition
4. Agents execute their role within the phased pipeline described in [spec-agent-roles-flow.md](../spec-agent-roles-flow.md)
5. Every agent also receives branch-local execution memory under `.fleet/.docs/changes/<change-id>/`, and those OpenSpec files should be treated as the canonical retry/resume context for the current execution branch

## Architecture Reference

- [spec-agent-roles-flow.md](../spec-agent-roles-flow.md) — Role definitions, phase sequencing, agent communication
- [spec-agent-execution.md](../spec-agent-execution.md) — Execution infrastructure, LLM providers, monitoring & control
- [spec-product-vision.md](../spec-product-vision.md) — Agent model (single, multi, auto-detect, user-directed)
- [spec-github-integration.md](../spec-github-integration.md) — Branch conventions, safety guardrails, PR output

## Role Prompts

| File | Role | Phase | Description |
| --- | --- | --- | --- |
| [manager.prompt.md](manager.prompt.md) | Manager | Orchestration | Analyzes work item, assigns roles, coordinates phases, triages after review |
| [planner.prompt.md](planner.prompt.md) | Planner | Phase 1 | Breaks down the work item into actionable sub-tasks |
| [contracts.prompt.md](contracts.prompt.md) | Contracts | Phase 2 | Defines shared data models, API interfaces, and type definitions |
| [backend.prompt.md](backend.prompt.md) | Backend | Phase 3 (parallel) | Implements server-side logic, endpoints, data access |
| [frontend.prompt.md](frontend.prompt.md) | Frontend | Phase 3 (parallel) | Implements UI components, pages, client-side logic |
| [testing.prompt.md](testing.prompt.md) | Testing | Phase 4 (parallel) | Writes and runs unit, integration, and e2e tests |
| [styling.prompt.md](styling.prompt.md) | Styling | Phase 4 (parallel) | Applies visual design, theming, responsive layouts |
| [consolidation.prompt.md](consolidation.prompt.md) | Consolidation | Phase 5 | Merges outputs from parallel agents, resolves conflicts |
| [review.prompt.md](review.prompt.md) | Review | Phase 6 (parallel) | Code review, quality checks, standards adherence |
| [documentation.prompt.md](documentation.prompt.md) | Documentation | Phase 6 (parallel) | Writes/updates docs, READMEs, inline comments |

## Execution Flow

```text
Manager → Planner → Contracts → [Backend | Frontend | Testing | Styling] → Consolidation → [Review | Documentation] → Manager (triage)
```

See [spec-agent-roles-flow.md](../spec-agent-roles-flow.md) for the full flow diagram.

## Naming Convention

- `<role>.prompt.md` — One file per agent role
- Files are plain Markdown — the full content is used as the agent's system prompt at runtime
