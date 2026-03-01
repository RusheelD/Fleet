# Fleet — GitHub Integration & Safety

## GitHub Connection Model

Fleet will support **both** OAuth App and GitHub App models long-term:

| Model | How it works | When to use |
| --- | --- | --- |
| **OAuth App** | User grants access; Fleet acts on the user's behalf | MVP — simpler to implement, needed for repo linking anyway |
| **GitHub App** | Installed per-repo; Fleet acts as a bot with its own identity | Future — granular permissions, higher rate limits, cleaner audit trail |

### MVP Approach

Start with **OAuth App** (user tokens) since OAuth is already required for authentication. The GitHub App model will be added later for production-grade installations.

## Commit Identity & Branch Conventions

Both are **user-configurable** per project:

### Commit Author

- Default: "Fleet Bot <fleet-bot@users.noreply.github.com>" (or the GitHub App identity once available)
- Configurable: user can set commits to appear as themselves or as the bot

### Branch Naming

- Default pattern: `fleet/<work-item-id>-<short-description>` (e.g., `fleet/42-add-login-page`)
- Configurable: user can define a custom branch pattern per project

## Safety Guardrails

| Rule | Description |
| --- | --- |
| **Feature branches only** | Agents can only push to feature branches — never directly to `main`, `master`, or other protected branches |
| **PRs require human approval** | Agents open PRs but cannot merge them; a human must review and approve |
| **No destructive operations** | Agents cannot delete branches, repos, or force-push |
| **Credit-based cost caps** | Agent execution is bounded by the user's credits/agent-hours; when credits run out, agents stop. No separate per-task cost cap is needed since credits are the natural limiter |
| **Long-running by design** | Agents are intended to run until the task is thoroughly completed, not cut short by artificial time limits |

## MVP Scope

The MVP includes the **full path** up to and including multi-agent task execution:

1. Auth (OAuth with GitHub, Google, Microsoft via Azure AD B2C)
2. Project creation & GitHub repo linking
3. AI chat for spec/work-item generation (no streaming — complete responses only)
4. Work item management
5. Agent orchestration with multi-agent capabilities (manager + workers)
6. PR output to GitHub

**Everything is free in the MVP** — no Stripe integration, no billing, no plan enforcement. All users get full capabilities during the MVP phase. Billing is added post-MVP.

Work items board view details (Kanban vs. list vs. hybrid) will be decided during implementation.

### Deferred (post-MVP)

- GitHub App installation model
- In-browser code viewer
- Team/org accounts
- Advanced billing UI
- GitHub Actions CI integration
- Project board / milestone management by agents
