using Fleet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Data;

/// <summary>
/// Seeds the database with initial demo data on first run.
/// Mirrors all the in-memory seed data from the original repositories.
/// </summary>
public static class FleetDbSeeder
{
    /// <summary>
    /// Applies any pending migrations and seeds the database if it is empty.
    /// Called on every startup — safe and non-destructive.
    /// </summary>
    public static async Task SeedAsync(FleetDbContext context)
    {
        await context.Database.MigrateAsync();

        if (await context.Projects.AnyAsync())
            return; // Already seeded

        SeedProjects(context);
        SeedWorkItemLevels(context);
        SeedWorkItems(context);
        SeedAgentExecutions(context);
        SeedLogEntries(context);
        SeedDashboardAgents(context);
        SeedChatSessions(context);
        SeedChatMessages(context);
        SeedUserProfile(context);
        SeedSubscription(context);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Drops the entire database, recreates it from migrations, and seeds fresh data.
    /// Use this when you change the schema and want to start from scratch.
    /// </summary>
    public static async Task ResetAsync(FleetDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        SeedProjects(context);
        SeedWorkItemLevels(context);
        SeedWorkItems(context);
        SeedAgentExecutions(context);
        SeedLogEntries(context);
        SeedDashboardAgents(context);
        SeedChatSessions(context);
        SeedChatMessages(context);
        SeedUserProfile(context);
        SeedSubscription(context);

        await context.SaveChangesAsync();
    }

    private static void SeedProjects(FleetDbContext context)
    {
        context.Projects.AddRange(
            new Project
            {
                Id = "1",
                OwnerId = "default",
                Title = "Fleet Platform",
                Slug = "fleet-platform",
                Description = "AI-powered project management tool",
                Repo = "RusheelD/fleet-platform",
                LastActivity = "2 hours ago",
                WorkItemSummary = new WorkItemSummary { Total = 15, Active = 4, Resolved = 9 },
                AgentSummary = new AgentSummary { Total = 3, Running = 2 },
            },
            new Project
            {
                Id = "2",
                OwnerId = "default",
                Title = "E-Commerce API",
                Slug = "e-commerce-api",
                Description = "RESTful API for online marketplace",
                Repo = "RusheelD/ecommerce-api",
                LastActivity = "5 hours ago",
                WorkItemSummary = new WorkItemSummary { Total = 23, Active = 7, Resolved = 14 },
                AgentSummary = new AgentSummary { Total = 2, Running = 1 },
            },
            new Project
            {
                Id = "3",
                OwnerId = "default",
                Title = "Mobile App",
                Slug = "mobile-app",
                Description = "Cross-platform mobile application",
                Repo = "RusheelD/mobile-app",
                LastActivity = "1 day ago",
                WorkItemSummary = new WorkItemSummary { Total = 8, Active = 2, Resolved = 5 },
                AgentSummary = new AgentSummary { Total = 1, Running = 0 },
            },
            new Project
            {
                Id = "4",
                OwnerId = "default",
                Title = "Data Pipeline",
                Slug = "data-pipeline",
                Description = "ETL pipeline for analytics",
                Repo = "RusheelD/data-pipeline",
                LastActivity = "1 day ago",
                WorkItemSummary = new WorkItemSummary { Total = 12, Active = 3, Resolved = 8 },
                AgentSummary = new AgentSummary { Total = 2, Running = 0 },
            },
            new Project
            {
                Id = "5",
                OwnerId = "default",
                Title = "Auth Service",
                Slug = "auth-service",
                Description = "Microservice for authentication and authorization",
                Repo = "RusheelD/auth-service",
                LastActivity = "5 hours ago",
                WorkItemSummary = new WorkItemSummary { Total = 9, Active = 2, Resolved = 6 },
                AgentSummary = new AgentSummary { Total = 2, Running = 1 },
            },
            new Project
            {
                Id = "6",
                OwnerId = "default",
                Title = "Design System",
                Slug = "design-system",
                Description = "Shared UI component library",
                Repo = "RusheelD/design-system",
                LastActivity = "3 days ago",
                WorkItemSummary = new WorkItemSummary { Total = 15, Active = 4, Resolved = 7 },
                AgentSummary = new AgentSummary { Total = 1, Running = 0 },
            }
        );
    }

    private static void SeedWorkItemLevels(FleetDbContext context)
    {
        // Default levels for all seeded projects (IDs are auto-generated)
        var projectIds = new[] { "1", "2", "3", "4", "5", "6" };
        var defaults = new (string Name, string Icon, string Color, int Ordinal)[]
        {
            ("Domain",    "globe",        "#8764B8", 0),
            ("Module",    "puzzle-piece", "#0078D4", 1),
            ("Feature",   "lightbulb",    "#00B7C3", 2),
            ("Component", "code",         "#498205", 3),
            ("Bug",       "bug",          "#D13438", 4),
            ("Task",      "task-list",    "#8A8886", 5),
        };

        foreach (var projectId in projectIds)
        {
            foreach (var (name, icon, color, ordinal) in defaults)
            {
                context.WorkItemLevels.Add(new WorkItemLevel
                {
                    Name = name,
                    IconName = icon,
                    Color = color,
                    Ordinal = ordinal,
                    IsDefault = true,
                    ProjectId = projectId,
                });
            }
        }
    }

    private static void SeedWorkItems(FleetDbContext context)
    {
        context.WorkItems.AddRange(
            // ── Epic: Authentication ────────────────────────────────
            new WorkItem { Id = 101, Title = "Set up authentication with OAuth", State = "In Progress (AI)", Priority = 1, AssignedTo = "Agent", Tags = ["auth", "backend"], IsAI = true, Description = "Implement GitHub and Google OAuth sign-in flow.", ProjectId = "1" },
            new WorkItem { Id = 113, Title = "Implement OAuth callback endpoint", State = "In Progress (AI)", Priority = 1, AssignedTo = "Agent", Tags = ["auth", "backend"], IsAI = true, Description = "Create /api/auth/callback for OAuth code exchange.", ProjectId = "1", ParentId = 101 },
            new WorkItem { Id = 114, Title = "Build login page UI", State = "Active", Priority = 2, AssignedTo = "Agent", Tags = ["auth", "frontend"], IsAI = true, Description = "Create the login page with GitHub and Google OAuth buttons.", ProjectId = "1", ParentId = 101 },
            new WorkItem { Id = 115, Title = "Session management & token storage", State = "New", Priority = 1, AssignedTo = "Unassigned", Tags = ["auth", "backend"], IsAI = false, Description = "Implement JWT session tokens and secure storage.", ProjectId = "1", ParentId = 101 },

            // ── Epic: Frontend pages ────────────────────────────────
            new WorkItem { Id = 102, Title = "Design landing page", State = "New", Priority = 2, AssignedTo = "Unassigned", Tags = ["frontend", "design"], IsAI = false, Description = "Create the initial landing page design with hero section.", ProjectId = "1" },
            new WorkItem { Id = 104, Title = "Implement work item board view", State = "In Progress (AI)", Priority = 2, AssignedTo = "Agent", Tags = ["frontend", "ui"], IsAI = true, Description = "Build the Kanban-style board view for work items.", ProjectId = "1" },
            new WorkItem { Id = 116, Title = "Add drag-and-drop reordering", State = "New", Priority = 3, AssignedTo = "Unassigned", Tags = ["frontend", "ui"], IsAI = false, Description = "Enable drag-and-drop between Kanban columns.", ProjectId = "1", ParentId = 104 },
            new WorkItem { Id = 117, Title = "Work item detail panel", State = "New", Priority = 2, AssignedTo = "Unassigned", Tags = ["frontend", "ui"], IsAI = false, Description = "Slide-out panel showing full work item details.", ProjectId = "1", ParentId = 104 },
            new WorkItem { Id = 107, Title = "User profile page", State = "New", Priority = 3, AssignedTo = "Unassigned", Tags = ["frontend"], IsAI = false, Description = "Build the user profile and account settings page.", ProjectId = "1" },

            // ── Epic: Backend API ───────────────────────────────────
            new WorkItem { Id = 103, Title = "Create project model & API", State = "Active", Priority = 1, AssignedTo = "Agent", Tags = ["backend", "api"], IsAI = true, Description = "Define the Project data model and CRUD endpoints.", ProjectId = "1" },
            new WorkItem { Id = 106, Title = "Add Redis caching layer", State = "Active", Priority = 3, AssignedTo = "Unassigned", Tags = ["backend", "performance"], IsAI = false, Description = "Integrate Redis output caching for API endpoints.", ProjectId = "1", ParentId = 103 },

            // ── Epic: DevOps ────────────────────────────────────────
            new WorkItem { Id = 105, Title = "Set up CI/CD pipeline", State = "Resolved (AI)", Priority = 1, AssignedTo = "Agent", Tags = ["devops"], IsAI = true, Description = "Configure GitHub Actions for build, test, and deploy.", ProjectId = "1" },

            // ── Epic: Agents ────────────────────────────────────────
            new WorkItem { Id = 108, Title = "Agent execution logs", State = "In Progress", Priority = 2, AssignedTo = "Agent", Tags = ["backend", "agents"], IsAI = true, Description = "Implement log capture and storage for agent executions.", ProjectId = "1" },

            // ── Standalone items ────────────────────────────────────
            new WorkItem { Id = 109, Title = "GitHub repo integration", State = "Resolved", Priority = 1, AssignedTo = "You", Tags = ["integration"], IsAI = false, Description = "Enable linking GitHub repos to Fleet projects.", ProjectId = "1" },
            new WorkItem { Id = 110, Title = "Dark mode support", State = "Closed", Priority = 4, AssignedTo = "You", Tags = ["frontend", "theme"], IsAI = false, Description = "Add dark/light theme toggle support.", ProjectId = "1" },
            new WorkItem { Id = 111, Title = "Search functionality", State = "New", Priority = 2, AssignedTo = "Unassigned", Tags = ["frontend", "backend"], IsAI = false, Description = "Global search across projects, work items, and chats.", ProjectId = "1" },
            new WorkItem { Id = 112, Title = "Notification system", State = "New", Priority = 3, AssignedTo = "Unassigned", Tags = ["backend", "frontend"], IsAI = false, Description = "Push notifications for PR ready, agent errors, task completion.", ProjectId = "1" }
        );
    }

    private static void SeedAgentExecutions(FleetDbContext context)
    {
        context.AgentExecutions.AddRange(
            new AgentExecution
            {
                Id = "e1",
                WorkItemId = 101,
                WorkItemTitle = "Set up authentication with OAuth",
                Status = "running",
                Agents =
                [
                    new AgentInfo { Role = "Manager", Status = "running", CurrentTask = "Coordinating phase 3", Progress = 0.6 },
                    new AgentInfo { Role = "Backend", Status = "running", CurrentTask = "Implementing OAuth endpoints", Progress = 0.45 },
                    new AgentInfo { Role = "Frontend", Status = "running", CurrentTask = "Building login UI", Progress = 0.35 },
                    new AgentInfo { Role = "Testing", Status = "idle", CurrentTask = "Waiting for implementation", Progress = 0 },
                ],
                StartedAt = "2:15 PM",
                Duration = "23 min",
                Progress = 0.55,
                ProjectId = "1",
            },
            new AgentExecution
            {
                Id = "e2",
                WorkItemId = 104,
                WorkItemTitle = "Implement work item board view",
                Status = "running",
                Agents =
                [
                    new AgentInfo { Role = "Manager", Status = "running", CurrentTask = "Monitoring progress", Progress = 0.75 },
                    new AgentInfo { Role = "Frontend", Status = "running", CurrentTask = "Adding drag-and-drop", Progress = 0.65 },
                    new AgentInfo { Role = "Styling", Status = "completed", CurrentTask = "Theme applied", Progress = 1.0 },
                ],
                StartedAt = "1:40 PM",
                Duration = "58 min",
                Progress = 0.7,
                ProjectId = "1",
            },
            new AgentExecution
            {
                Id = "e3",
                WorkItemId = 105,
                WorkItemTitle = "Set up CI/CD pipeline",
                Status = "completed",
                Agents =
                [
                    new AgentInfo { Role = "Manager", Status = "completed", CurrentTask = "PR opened", Progress = 1.0 },
                    new AgentInfo { Role = "Backend", Status = "completed", CurrentTask = "Pipeline configured", Progress = 1.0 },
                    new AgentInfo { Role = "Documentation", Status = "completed", CurrentTask = "README updated", Progress = 1.0 },
                ],
                StartedAt = "11:00 AM",
                Duration = "15 min",
                Progress = 1.0,
                ProjectId = "1",
            },
            new AgentExecution
            {
                Id = "e4",
                WorkItemId = 108,
                WorkItemTitle = "Agent execution logs",
                Status = "failed",
                Agents =
                [
                    new AgentInfo { Role = "Manager", Status = "failed", CurrentTask = "Error: build failure", Progress = 0.3 },
                    new AgentInfo { Role = "Backend", Status = "failed", CurrentTask = "Compilation error in LogService", Progress = 0.4 },
                ],
                StartedAt = "10:20 AM",
                Duration = "12 min",
                Progress = 0.3,
                ProjectId = "1",
            }
        );
    }

    private static void SeedLogEntries(FleetDbContext context)
    {
        context.LogEntries.AddRange(
            new LogEntry { Time = "2:38:12 PM", Agent = "Manager", Level = "info", Message = "Phase 3 parallel execution started - Backend, Frontend, Testing agents deployed", ProjectId = "1" },
            new LogEntry { Time = "2:38:10 PM", Agent = "Backend", Level = "info", Message = "Creating OAuth callback endpoint at /api/auth/callback", ProjectId = "1" },
            new LogEntry { Time = "2:37:55 PM", Agent = "Frontend", Level = "info", Message = "Generating LoginPage.tsx component with GitHub OAuth button", ProjectId = "1" },
            new LogEntry { Time = "2:37:40 PM", Agent = "Backend", Level = "success", Message = "OAuth middleware configured successfully", ProjectId = "1" },
            new LogEntry { Time = "2:37:22 PM", Agent = "Manager", Level = "info", Message = "Contracts phase completed - data models shared with all agents", ProjectId = "1" },
            new LogEntry { Time = "2:36:15 PM", Agent = "Contracts", Level = "success", Message = "Generated AuthUser, OAuthToken, and Session interfaces", ProjectId = "1" },
            new LogEntry { Time = "2:35:48 PM", Agent = "Planner", Level = "info", Message = "Work item decomposed into 8 sub-tasks", ProjectId = "1" },
            new LogEntry { Time = "2:35:30 PM", Agent = "Manager", Level = "info", Message = "Analyzing work item #101: Set up authentication with OAuth", ProjectId = "1" },
            new LogEntry { Time = "2:35:05 PM", Agent = "Frontend", Level = "warn", Message = "No existing auth components found - creating from scratch", ProjectId = "1" },
            new LogEntry { Time = "2:34:50 PM", Agent = "Backend", Level = "info", Message = "Reading existing project structure and dependencies", ProjectId = "1" }
        );
    }

    private static void SeedDashboardAgents(FleetDbContext context)
    {
        context.DashboardAgents.AddRange(
            new DashboardAgent { Name = "Manager Agent", Status = "running", Task = "Coordinating auth implementation", Progress = 0.6, ProjectId = "1" },
            new DashboardAgent { Name = "Backend Agent", Status = "running", Task = "Implementing OAuth endpoints", Progress = 0.4, ProjectId = "1" },
            new DashboardAgent { Name = "Frontend Agent", Status = "running", Task = "Building login components", Progress = 0.35, ProjectId = "1" },
            new DashboardAgent { Name = "Testing Agent", Status = "idle", Task = "Waiting for code completion", Progress = 0, ProjectId = "1" }
        );
    }

    private static void SeedChatSessions(FleetDbContext context)
    {
        context.ChatSessions.AddRange(
            new ChatSession { Id = "c1", Title = "Product Spec Discussion", LastMessage = "I've generated 12 work items based on your specification...", Timestamp = "2 hours ago", IsActive = true, ProjectId = "1" },
            new ChatSession { Id = "c2", Title = "Auth Implementation Plan", LastMessage = "The OAuth flow should follow these steps...", Timestamp = "1 day ago", IsActive = false, ProjectId = "1" },
            new ChatSession { Id = "c3", Title = "Database Schema Design", LastMessage = "Here's the proposed PostgreSQL schema...", Timestamp = "3 days ago", IsActive = false, ProjectId = "1" },
            new ChatSession { Id = "c4", Title = "API Endpoint Planning", LastMessage = "I recommend the following REST endpoints...", Timestamp = "1 week ago", IsActive = false, ProjectId = "1" }
        );
    }

    private static void SeedChatMessages(FleetDbContext context)
    {
        context.ChatMessages.AddRange(
            new ChatMessage
            {
                Id = "1",
                Role = "user",
                ChatSessionId = "c1",
                Timestamp = "2:30 PM",
                Content = "I want to build a project management tool that uses AI agents to complete software tasks. The agents should be able to create PRs in GitHub.",
            },
            new ChatMessage
            {
                Id = "2",
                Role = "assistant",
                ChatSessionId = "c1",
                Timestamp = "2:31 PM",
                Content = "That sounds like a great project! Let me understand your requirements better. Based on what you've described, here are some key areas I'd want to cover:\n\n**Core Features:**\n\u2022 Project creation with GitHub repo linking\n\u2022 AI-powered spec generation through conversation\n\u2022 Work item management (similar to Azure DevOps)\n\u2022 Multi-agent execution with manager/worker hierarchy\n\u2022 Automated PR creation and code review\n\n**Would you like me to:**\n1. Generate a detailed product specification document?\n2. Create work items directly from this conversation?\n3. Discuss the technical architecture first?\n\nLet me know how you'd like to proceed, and I'll help shape the product vision.",
            },
            new ChatMessage
            {
                Id = "3",
                Role = "user",
                ChatSessionId = "c1",
                Timestamp = "2:35 PM",
                Content = "Let's create work items. I want authentication with GitHub OAuth, a project dashboard, a Kanban-style work item board, and an agent monitoring page.",
            },
            new ChatMessage
            {
                Id = "4",
                Role = "assistant",
                ChatSessionId = "c1",
                Timestamp = "2:36 PM",
                Content = "I've generated 12 work items based on your specification. Here's a summary:\n\n**Authentication (Priority 1)**\n\u2022 Set up GitHub OAuth sign-in flow\n\u2022 Implement session management & token storage\n\n**Project Dashboard (Priority 1)**\n\u2022 Create project metrics overview\n\u2022 Build recent activity feed\n\u2022 Add agent status widgets\n\n**Work Item Board (Priority 2)**\n\u2022 Implement Kanban board view\n\u2022 Add list/backlog view\n\u2022 Build work item creation dialog\n\u2022 Add drag-and-drop reordering\n\n**Agent Monitor (Priority 2)**\n\u2022 Create agent status dashboard\n\u2022 Implement real-time log streaming\n\u2022 Build agent execution history\n\nAll work items have been added to your board. You can view and edit them in the **Work Items** tab. Would you like me to assign agents to start working on any of these?",
            }
        );
    }

    private static void SeedUserProfile(FleetDbContext context)
    {
        var user = new UserProfile
        {
            EntraObjectId = "00000000-0000-0000-0000-000000000000",
            Username = "demo",
            Email = "user@fleet.dev",
            DisplayName = "Fleet User",
            Bio = "Building the future with AI agents",
            Location = "San Francisco, CA",
            AvatarUrl = "",
            CreatedAt = DateTime.UtcNow,
            Preferences = new UserPreferences
            {
                AgentCompletedNotification = true,
                PrOpenedNotification = true,
                AgentErrorsNotification = true,
                WorkItemUpdatesNotification = false,
                DarkMode = true,
                CompactMode = false,
                SidebarCollapsed = false,
            },
        };

        context.UserProfiles.Add(user);

        // Use the navigation property so EF resolves the FK after SaveChanges
        user.LinkedAccounts.AddRange([
            new LinkedAccount { Provider = "GitHub", ConnectedAs = "RusheelD" },
            new LinkedAccount { Provider = "Google", ConnectedAs = null },
            new LinkedAccount { Provider = "Microsoft", ConnectedAs = null },
        ]);
    }

    private static void SeedSubscription(FleetDbContext context)
    {
        context.Subscriptions.Add(new Subscription
        {
            CurrentPlanName = "Free Plan",
            CurrentPlanDescription = "You're on the Free plan. Upgrade to unlock more agents and capabilities.",
            UsageMeters =
            [
                new UsageMeterData { Label = "Agent Credits", Usage = "45 / 100", Value = 0.45, Color = "brand", Remaining = "55 credits remaining" },
                new UsageMeterData { Label = "Agent Hours", Usage = "3.2 / 10 hrs", Value = 0.32, Color = "brand", Remaining = "6.8 hours remaining" },
                new UsageMeterData { Label = "Active Agents", Usage = "1 / 1", Value = 1.0, Color = "warning", Remaining = "At limit — upgrade for more" },
                new UsageMeterData { Label = "Projects", Usage = "2 / 3", Value = 0.67, Color = "brand", Remaining = "1 project slot remaining" },
            ],
            Plans =
            [
                new PlanData
                {
                    Name = "Free", Icon = "rocket", Price = "$0", Period = "/month",
                    Description = "Get started with AI-assisted development",
                    Features = ["1 concurrent agent per task", "1 total agent", "Limited monthly credits", "Base AI model only", "3 projects"],
                    ButtonLabel = "Current Plan", IsCurrent = true, ButtonAppearance = "outline",
                },
                new PlanData
                {
                    Name = "Pro", Icon = "diamond", Price = "$29", Period = "/month",
                    Description = "For serious builders shipping fast",
                    Features = ["5 concurrent agents per task", "10 total agents", "Higher monthly credits", "Base + Mid-tier AI models", "Unlimited projects", "Priority support"],
                    ButtonLabel = "Upgrade to Pro", IsCurrent = false, ButtonAppearance = "primary",
                },
                new PlanData
                {
                    Name = "Team", Icon = "sparkle", Price = "$99", Period = "/month",
                    Description = "Maximum power for teams and enterprises",
                    Features = ["10 concurrent agents per task", "25 total agents", "Highest monthly credits", "All AI models including premium", "Unlimited projects", "Priority support", "Team collaboration (coming soon)"],
                    ButtonLabel = "Upgrade to Team", IsCurrent = false, ButtonAppearance = "primary",
                },
            ],
        });
    }
}
