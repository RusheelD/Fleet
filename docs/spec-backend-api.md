# Fleet — Backend API & Data Model

## Architecture: Controllers → Services → Repositories

The backend follows a strict **three-tier layered architecture**:

| Layer | Responsibility | Location |
| --- | --- | --- |
| **Controllers** | HTTP endpoints — accept requests, call services, return responses | `Fleet.Server/Controllers/` |
| **Services** | Business logic — orchestrate repositories and other services | Domain folders (e.g., `Fleet.Server/Copilot/`) |
| **Repositories** | Data access — communicate with database(s) directly | Domain folders (same as their service) |

**Dependency rules:**

- Controllers → Services only (never call Repositories directly)
- Services → Repositories + other Services (never access HTTP context)
- Repositories → Database only (no business logic, no service calls)

**Interface requirement:** Every service and repository **must** have an interface (`I<Name>Service`, `I<Name>Repository`). Inject the interface everywhere; register implementations in `Program.cs` via DI.

## API Style

**MVC Controllers** with `[ApiController]` — structured, convention-based endpoints organized by domain area. Each domain gets its own controller class under `Controllers/`. Controllers are thin — validate inputs, call a service method, return the result.

### Endpoint Organization Pattern

```text
Fleet.Server/
  Program.cs                          → App setup, DI registration, middleware
  Extensions.cs                       → Aspire service defaults
  Controllers/
    AuthController.cs                 → [Route("api/auth")]
    ProjectsController.cs             → [Route("api/projects")]
    WorkItemsController.cs            → [Route("api/projects/{projectId}/work-items")]
    ChatsController.cs                → [Route("api/projects/{projectId}/chat")]
    AgentsController.cs               → [Route("api/projects/{projectId}/agents")]
    GitHubController.cs               → [Route("api/github")]
    BillingController.cs              → [Route("api/billing")]
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
    IWorkItemService.cs               → Interface for work item logic
    WorkItemService.cs                → Implementation
    IWorkItemRepository.cs            → Interface for work item data access
    WorkItemRepository.cs             → Implementation
  Agents/
    IAgentService.cs                  → Interface for agent orchestration
    AgentService.cs                   → Implementation
    IAgentTaskRepository.cs           → Interface for agent task data access
    AgentTaskRepository.cs            → Implementation
  Auth/
    IAuthService.cs                   → Interface for authentication logic
    AuthService.cs                    → Implementation
    IUserRepository.cs                → Interface for user data access
    UserRepository.cs                 → Implementation
  Billing/
    IBillingService.cs                → Interface for subscription/billing logic
    BillingService.cs                 → Implementation
    ISubscriptionRepository.cs        → Interface for subscription data access
    SubscriptionRepository.cs         → Implementation
  GitHub/
    IGitHubService.cs                 → Interface for GitHub integration logic
    GitHubService.cs                  → Implementation
```

### DI Registration (Program.cs)

```csharp
// Services
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ICopilotChatService, CopilotChatService>();
builder.Services.AddScoped<IWorkItemService, WorkItemService>();
builder.Services.AddScoped<IAgentService, AgentService>();

// Repositories
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
builder.Services.AddScoped<IWorkItemRepository, WorkItemRepository>();
builder.Services.AddScoped<IAgentTaskRepository, AgentTaskRepository>();
```

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
