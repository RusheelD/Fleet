# Fleet — Users, Auth & Plans

## Authentication

Fleet supports **multiple OAuth providers** for sign-up and sign-in:

- GitHub (required to link repos — can be connected as a primary login or linked later)
- Google
- Microsoft

Users must link at least one GitHub account to create projects, but they can sign up with any supported provider.

## Account Model

- **Individual accounts only** at launch
- **Teams / orgs** — planned for a later phase (shared projects, org-level billing, role-based access)

## Pricing Tiers

All plans share the same feature set. The differentiators are resource limits:

| Dimension | Description |
| --- | --- |
| **Concurrent agents per task** | Max agents working in parallel on a single work item |
| **Total agents** | Max agents the user can have active across ALL tasks simultaneously |
| **Credits / agent-hours** | Monthly budget for agent compute time |
| **Available models** | Which AI models agents can use (e.g., higher tiers unlock more capable models) |

### How concurrency works

- A user with **25 total agents** and **5 concurrent per task** can run up to 5 tasks simultaneously, each with up to 5 agents.
- If a user assigns only 1 agent to a task, it works solo — freeing agents for other tasks.

### Example tier structure (TBD — exact numbers to be determined)

| Tier | Concurrent / Task | Total Agents | Credits | Models |
| --- | --- | --- | --- | --- |
| Free | 1 | 1 | Limited | Base model only |
| Pro | 5 | 10 | Higher | + Mid-tier models |
| Team | 10 | 25 | Highest | + Premium models |

> **Note:** Exact tier names, limits, and prices are TBD.

## Data Storage

**PostgreSQL** as the primary database, leveraging:

- Standard relational tables for users, accounts, subscriptions, projects, and billing
- JSONB columns for flexible/semi-structured data like agent state, work-item metadata, and chat history

This gives the relational integrity needed for auth, billing, and project ownership while supporting document-like flexibility where schemas evolve rapidly (e.g., agent execution logs, spec documents).
