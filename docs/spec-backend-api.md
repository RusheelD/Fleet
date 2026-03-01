# Fleet — Backend API & Data Model

## API Style

**MVC Controllers** with `[ApiController]` — structured, convention-based endpoints organized by domain area. Each domain gets its own controller class under a `Controllers/` folder.

### Endpoint Organization Pattern

```text
Fleet.Server/
  Program.cs              → App setup, middleware, AddControllers()
  Controllers/
    AuthController.cs      → [Route("api/auth")]
    ProjectsController.cs  → [Route("api/projects")]
    WorkItemsController.cs → [Route("api/projects/{projectId}/work-items")]
    ChatsController.cs     → [Route("api/projects/{projectId}/chat")]
    AgentsController.cs    → [Route("api/projects/{projectId}/agents")]
    GitHubController.cs    → [Route("api/github")]
    BillingController.cs   → [Route("api/billing")]
```

Each controller inherits `ControllerBase`, uses `[ApiController]` for automatic model validation and problem-details responses, and groups related actions (CRUD + custom operations) in one class.

## API Domain Areas (Priority Order)

1. **Auth / Users** — OAuth flows (GitHub, Google, Microsoft), session management, user profile
2. **Projects** — CRUD, link to GitHub repo, project settings
3. **GitHub Integration** — Repo browsing, branch listing, file reading, webhook handling
4. **Work Items** — CRUD, board state, hierarchy, status transitions
5. **Chat / AI** — Conversation CRUD, message streaming, spec generation, file attachment
6. **Agent Orchestration** — Launch tasks, assign agents, monitor status, pause/stop/steer
7. **Billing** — Subscription management, usage tracking, plan changes

### Future

- **Code Viewer** — In-browser view of the GitHub repo's code (post-MVP)

## Data Access

**EF Core with Npgsql** — simplest path to Postgres with JSONB support:

- EF Core provides quick scaffolding, migrations, and LINQ queries
- Npgsql's EF Core provider has first-class JSONB support (map C# objects to JSONB columns)
- For complex queries, raw SQL via EF Core's `FromSqlRaw` or fallback to Dapper if needed

### Core Entities (initial sketch)

```text
User
  - Id, Email, DisplayName, AvatarUrl, CreatedAt
  - OAuthAccounts[] (provider, externalId, accessToken)

Subscription
  - Id, UserId, Tier, ConcurrentPerTask, TotalAgents, CreditsRemaining, RenewsAt

Project
  - Id, UserId, Title, GitHubRepoOwner, GitHubRepoName, CreatedAt

Chat
  - Id, ProjectId, Title, CreatedAt
  - Messages[] (JSONB — role, content, timestamp, attachments)

WorkItem
  - Id, ProjectId, Title, Description, State, Priority, ParentId
  - AcceptanceCriteria, Tags[], AssignedAgentCount
  - Metadata (JSONB — flexible fields for future customization)

AgentTask
  - Id, WorkItemId, Status, AgentCount, ManagerAgentId
  - RoleAssignments (JSONB — agent → role mapping)
  - Logs (JSONB or separate table — execution log entries)
  - PullRequests[] (GitHub PR URLs produced)
```

> **Note:** This is a starting sketch. The schema will evolve as features are implemented.

## Background Processing

**TBD** — The approach for long-running agent tasks (LLM calls, GitHub operations) is not yet decided. Candidates:

- .NET `BackgroundService` / hosted services (simplest for MVP)
- Message queue (RabbitMQ, Azure Service Bus) + dedicated workers (more scalable)
- Job scheduler (Hangfire) for retries and scheduling

Decision will be made when agent execution architecture is finalized.
