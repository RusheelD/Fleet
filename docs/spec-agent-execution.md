# Fleet — Agent Execution & Architecture

## Agent Infrastructure

**TBD** — The execution environment is not yet decided. The approach:

1. **MVP / Testing phase** — Choose the simplest approach to get agents running (likely server-side orchestration calling LLM APIs directly from the Fleet backend).
2. **Future** — Migrate to a more scalable/isolated model (e.g., sandboxed containers, GitHub-hosted execution) as requirements solidify.

### Recommended: Containerized Repo Clone

The most scalable approach is to **clone the repo into an isolated container** per agent task:

- Each agent task gets a fresh container with the repo cloned
- Agents read/write files locally within the container
- On completion, changes are committed and pushed to GitHub via the GitHub API or Git CLI
- Containers are ephemeral — destroyed after the task completes
- This provides isolation (agents can't interfere with each other), security (sandboxed), and scalability (spin up as many as needed)

> The spec documents should be updated once the execution environment is chosen.

## Failure Handling

When an agent fails mid-task (LLM error, GitHub rate limit, code that doesn't compile):

- The **manager agent decides** the recovery strategy:
  - **Retry** the failed step (e.g., transient LLM error)
  - **Reassign** the sub-task to a different approach or agent
  - **Skip** the sub-task if non-critical and continue
  - **Escalate** to the user if the failure is unrecoverable (via notification)
- The manager has full context on the task state and can make informed decisions
- All failures are logged for user visibility in the log stream

### Automatic Review Feedback Loop

Fleet should take a work item to completion inside a single execution whenever possible. That means review findings are not only user-visible; they are also actionable orchestration signals.

- If the **Review** agent recommends **STOP**, Fleet finalizes the execution and readies the PR.
- If the **Review** agent recommends **PATCH**, Fleet automatically re-runs the targeted fixing agents, then re-runs downstream validation/review phases in the same execution.
- If the **Review** agent recommends **RESTART**, Fleet automatically re-enters the pipeline from the specified earlier phase and continues toward completion in the same execution.
- This loop stays on the same execution record, branch, and draft PR; it is **not** a user-triggered retry.
- The monitor UX should clearly show when Fleet is in an automatic review remediation cycle, and completed runs should indicate that they self-corrected before finishing.
- To keep the loop machine-actionable, the Review agent must end with a structured decision block containing recommendation, severity, target roles, and restart phase.

## Hierarchical Flows

Fleet executions may generate and orchestrate child sub-flows when a work item is too large or cross-cutting for one pipeline.

- The **Planner** can ask Fleet to split a work item into child work items using a structured sub-flow JSON block.
- Child work items become child executions under the parent flow instead of being treated as mere context.
- Sibling sub-flows should run in parallel only when they are independent.
- Sequencing is represented by nesting a dependent sub-flow under the work item it depends on.
- Parent flows are orchestration runs: they coordinate child executions, track aggregate progress, and complete when the child flow tree reaches a terminal state.
- Leaf sub-flows still use the normal full agent pipeline, branch creation, PR flow, and documentation.
- The monitor UX should show sub-flows nested under the parent run so complex work remains visible as one coordinated execution.

## LLM Providers

**Multi-provider** is the long-term goal — users select models based on their plan tier.

### Tier → Model mapping (conceptual)

| Tier | Example Models |
| --- | --- |
| Free | GPT 5.1-Codex-Mini or equivalent lightweight/fast deployment |
| Mid-tier | GPT 5.2-Codex or equivalent balanced deployment |
| Premium | GPT 5.2-Codex or equivalent full-capability deployment |

### MVP approach

- **Azure OpenAI** is the sole provider
- Build the abstraction layer so swapping/adding providers is straightforward
- Provider selection per-task or per-project is a future enhancement

## Agent Roles

When the manager agent decomposes a work item, it assigns workers to specialized roles:

| Role | Responsibility |
| --- | --- |
| **Planning** | Break down the work item into sub-tasks, define approach and order of operations |
| **Contracts** | Define interfaces, API contracts, data models, and type definitions |
| **Backend** | Implement server-side logic, endpoints, data access |
| **Frontend** | Implement UI components, pages, client-side logic |
| **Styling** | Apply visual design, theming, responsive layouts |
| **Testing** | Write and run tests (unit, integration, e2e) |
| **Consolidation** | Merge outputs from parallel agents, resolve conflicts, ensure coherence |
| **Review** | Code review, quality checks, adherence to standards |
| **Documentation** | Write/update docs, READMEs, inline comments, API docs |

> **Note:** Further detailed role specifications exist in separate documentation. The manager agent dynamically assigns roles based on the nature of the work item — not all roles are needed for every task.

## Monitoring & Control

Users have multiple ways to track and control agent activity:

### Status Dashboard (primary)

- Easily accessible page showing all active tasks and their state
- Per-task progress indicators (e.g., which roles are active, % complete)
- Quick view of recent PRs produced

### Real-Time Log Stream (detailed)

- Available but **not front-and-center** — accessible behind a few menu interactions
- Shows live agent activity: what each agent is doing, LLM calls, file changes
- From the log stream, users can:
  - **Stop** an agent or task entirely
  - **Pause** execution
  - **Steer** agents (provide additional instructions or corrections mid-task)

### Notifications

- Users receive notifications when:
  - A pull request is ready for review
  - An agent encounters an error or needs input
  - A task completes or fails
