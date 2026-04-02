using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fleet.Server.Auth;
using Fleet.Server.Connections;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.LLM;
using Fleet.Server.Models;
using Fleet.Server.Notifications;
using Fleet.Server.Realtime;
using Fleet.Server.Subscriptions;
using Fleet.Server.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Agents;

/// <summary>
/// Coordinates the sequential agent pipeline: clone repo → run phases → create PR → clean up.
/// Each phase receives the outputs of all previous phases as context.
/// </summary>
public class AgentOrchestrationService(
    FleetDbContext db,
    IAgentTaskRepository agentTaskRepository,
    IConnectionService connectionService,
    IWorkItemRepository workItemRepository,
    IServiceScopeFactory serviceScopeFactory,
    ILLMClient llmClient,
    IHttpClientFactory httpClientFactory,
    ILogger<AgentOrchestrationService> logger,
    IModelCatalog modelCatalog,
    INotificationService notificationService,
    IServerEventPublisher eventPublisher,
    IUsageLedgerService? usageLedgerService = null) : IAgentOrchestrationService
{
    private readonly IUsageLedgerService _usageLedgerService = usageLedgerService ?? NoOpUsageLedgerService.Instance;
    private const int MaxAgentRetries = 2;
    private const int MaxAutomaticReviewLoops = 2;
    private static readonly HashSet<string> InPrOrBeyondStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "In-PR",
        "In-PR (AI)",
        "Resolved",
        "Resolved (AI)",
        "Closed",
    };

    /// <summary>
    /// Tracks CancellationTokenSources for active executions so they can be cancelled/paused externally.
    /// Key = executionId, Value = (CTS, desired final status when cancelled).
    /// </summary>
    private static readonly ConcurrentDictionary<string, (CancellationTokenSource Cts, string FinalStatus)> ActiveExecutions = new();

    /// <summary>
    /// Queues operator steering notes to inject into the next phase prompt.
    /// </summary>
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> SteeringNotes = new();

    /// <summary>
    /// Suppresses any further pipeline persistence once an execution has been explicitly deleted.
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> DeletedExecutions = new();

    private sealed record RetryExecutionPlan(
        string SourceExecutionId,
        string? SourceStatus,
        string? ReuseBranchName,
        string? ReusePullRequestUrl,
        int? ReusePullRequestNumber,
        string? ReusePullRequestTitle,
        double PriorProgressEstimate,
        IReadOnlyDictionary<AgentRole, string> CarryForwardOutputs,
        string RetryContextMarkdown,
        bool ResumeInPlace = false,
        bool ResumeFromRemoteBranch = true)
    {
        public bool ReuseExistingBranch =>
            !string.IsNullOrWhiteSpace(ReuseBranchName);

        public bool ReuseExistingBranchAndPullRequest =>
            ReuseExistingBranch &&
            ResumeFromRemoteBranch &&
            !string.IsNullOrWhiteSpace(ReusePullRequestUrl) &&
            ReusePullRequestNumber is > 0;
    }

    /// <summary>
    /// The ordered pipeline phases. Implementation phases run sequentially within their group.
    /// </summary>
    private static readonly AgentRole[][] FullPipeline =
    [
        [AgentRole.Manager],
        // Phase 1 - Planning
        [AgentRole.Planner],
        // Phase 2 - Contracts / interfaces
        [AgentRole.Contracts],
        // Phase 3 - Implementation (sequential: Backend -> Frontend -> Testing -> Styling)
        [AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling],
        // Phase 4 - Consolidation
        [AgentRole.Consolidation],
        // Phase 5 - Review and documentation
        [AgentRole.Review, AgentRole.Documentation],
    ];

    private static readonly AgentRole[][] OrchestrationPreludePipeline =
    [
        [AgentRole.Manager],
        [AgentRole.Planner],
    ];

    private const int MaxParallelSubFlows = 3;

    /// <summary>
    /// Uses a cheap LLM call to determine which agent roles are needed for the work item.
    /// Returns a pipeline in the standard <c>AgentRole[][]</c> format, arranged in dependency order.
    /// Falls back to the full pipeline on error.
    /// </summary>
    private async Task<AgentRole[][]> SelectPipelineAsync(
        string workItemContext, CancellationToken cancellationToken)
    {
        const string systemPrompt = """
            You are a pipeline optimization assistant for a software development AI system.
            Given a work item, determine which agent roles are needed to complete it.
            
            Available roles and when to include them:
            - Planner: Creates the implementation plan. ALWAYS include this.
            - Contracts: Defines shared interfaces and types. Include when multiple components interact or API contracts change.
            - Backend: Implements server-side changes (APIs, services, database, .NET/C#). Include for any backend work.
            - Frontend: Implements UI changes (React, TypeScript, components). Include for any frontend/UI work.
            - Testing: Writes and runs tests. Include when new functionality is added.
            - Styling: Applies CSS/styling polish. Include only when visual/UI styling changes are needed.
            - Consolidation: Merges and integrates outputs. Include ONLY when BOTH Backend AND Frontend are selected.
            - Review: Reviews code quality. Include for medium-to-large changes.
            - Documentation: Generates documentation. Include only for significant new features.
            
            Rules:
            1. Planner is ALWAYS included.
            2. Include Consolidation ONLY if both Backend and Frontend are selected.
            3. Never include Consolidation if only one of Backend/Frontend is selected.
            4. Minimize the number of roles — fewer = faster and cheaper execution.
            5. For backend-only tasks, you typically need: Planner, Backend, and optionally Testing/Review.
            6. For frontend-only tasks, you typically need: Planner, Frontend, and optionally Styling/Review.
            7. For full-stack tasks, you typically need: Planner, Contracts, Backend, Frontend, Consolidation, and optionally Testing/Review.
            
            Return ONLY a JSON array of role name strings like: ["Planner", "Backend", "Testing"]
            No explanation, no markdown fences — just the raw JSON array.
            """;

        try
        {
            var model = modelCatalog.Get(ModelKeys.Haiku);
            var messages = new List<LLMMessage>
            {
                new() { Role = "user", Content = workItemContext }
            };

            var request = new LLMRequest(systemPrompt, messages, MaxTokens: 256, ModelOverride: model);
            var response = await llmClient.CompleteAsync(request, cancellationToken);

            var roles = ParseSelectedRoles(response.Content);
            if (roles.Count == 0)
            {
                logger.LogWarning("AI pipeline selection returned no roles; falling back to full pipeline");
                return FullPipeline;
            }

            var pipeline = ArrangePipeline(roles);
            logger.LogInformation("AI selected pipeline: {Roles}",
                string.Join(" → ", pipeline.SelectMany(g => g).Select(r => r.ToString())));
            return pipeline;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI pipeline selection failed; falling back to full pipeline");
            return FullPipeline;
        }
    }

    /// <summary>
    /// Parses a JSON array of role name strings into a list of <see cref="AgentRole"/> values.
    /// </summary>
    private static List<AgentRole> ParseSelectedRoles(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        // Strip markdown code fences if present (e.g., ```json ... ```)
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3];
            trimmed = trimmed.Trim();
        }

        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(trimmed);
            if (arr is null) return [];

            var roles = new List<AgentRole>();
            foreach (var name in arr)
            {
                if (Enum.TryParse<AgentRole>(name, ignoreCase: true, out var role))
                    roles.Add(role);
            }
            return roles;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Arranges selected roles into the standard <c>AgentRole[][]</c> pipeline format,
    /// preserving the canonical dependency order.
    /// </summary>
        private static AgentRole[][] ArrangePipeline(List<AgentRole> roles)
    {
        // Ensure Manager and Planner are always present
        if (!roles.Contains(AgentRole.Manager))
            roles.Insert(0, AgentRole.Manager);
        if (!roles.Contains(AgentRole.Planner))
            roles.Insert(0, AgentRole.Planner);

        // Canonical ordering: Manager -> Planner -> Contracts -> [Backend, Frontend, Testing, Styling] -> Consolidation -> [Review, Documentation]
        var pipeline = new List<AgentRole[]>();

        // Group 1: Manager
        pipeline.Add([AgentRole.Manager]);

        // Group 2: Planner
        pipeline.Add([AgentRole.Planner]);

        // Group 3: Contracts (if selected)
        if (roles.Contains(AgentRole.Contracts))
            pipeline.Add([AgentRole.Contracts]);

        // Group 4: Implementation agents (Backend, Frontend, Testing, Styling - in order, if selected)
        var implGroup = new List<AgentRole>();
        foreach (var role in new[] { AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling })
        {
            if (roles.Contains(role))
                implGroup.Add(role);
        }
        if (implGroup.Count > 0)
            pipeline.Add([.. implGroup]);

        // Group 5: Consolidation (if selected and both Backend+Frontend are in the pipeline)
        if (roles.Contains(AgentRole.Consolidation) &&
            roles.Contains(AgentRole.Backend) && roles.Contains(AgentRole.Frontend))
            pipeline.Add([AgentRole.Consolidation]);

        // Group 6: Review and Documentation (if selected)
        var reviewGroup = new List<AgentRole>();
        foreach (var role in new[] { AgentRole.Review, AgentRole.Documentation })
        {
            if (roles.Contains(role))
                reviewGroup.Add(role);
        }
        if (reviewGroup.Count > 0)
            pipeline.Add([.. reviewGroup]);

        return [.. pipeline];
    }

    /// <summary>
    /// Max output tokens per agent role. Lightweight roles get fewer tokens;
    /// implementation roles get more to accommodate code generation.
    /// </summary>
    private static int GetMaxTokensForRole(AgentRole role) => role switch
    {
        AgentRole.Planner => 4096,
        AgentRole.Review => 4096,
        AgentRole.Documentation => 4096,
        AgentRole.Contracts => 8192,
        AgentRole.Backend => 16384,
        AgentRole.Frontend => 16384,
        AgentRole.Testing => 8192,
        AgentRole.Styling => 8192,
        AgentRole.Consolidation => 8192,
        _ => 8192,
    };

    public Task<string> StartExecutionAsync(
        string projectId,
        int workItemNumber,
        int userId,
        CancellationToken cancellationToken = default)
        => StartExecutionInternalAsync(
            projectId,
            workItemNumber,
            userId,
            targetBranch: null,
            retryPlan: null,
            parentExecutionId: null,
            skipQuotaCharge: false,
            skipActiveExecutionCap: false,
            cancellationToken);

    public Task<string> StartExecutionAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? targetBranch,
        CancellationToken cancellationToken = default)
        => StartExecutionInternalAsync(
            projectId,
            workItemNumber,
            userId,
            targetBranch,
            retryPlan: null,
            parentExecutionId: null,
            skipQuotaCharge: false,
            skipActiveExecutionCap: false,
            cancellationToken);

    public Task<string> StartSubFlowExecutionAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string parentExecutionId,
        string? targetBranch,
        CancellationToken cancellationToken = default)
        => StartExecutionInternalAsync(
            projectId,
            workItemNumber,
            userId,
            targetBranch,
            retryPlan: null,
            parentExecutionId,
            skipQuotaCharge: true,
            skipActiveExecutionCap: true,
            cancellationToken);

    private async Task<string> StartExecutionInternalAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? targetBranch,
        RetryExecutionPlan? retryPlan,
        string? parentExecutionId,
        bool skipQuotaCharge,
        bool skipActiveExecutionCap,
        CancellationToken cancellationToken = default)
    {
        var codingRunCharged = false;
        try
        {
            // 1. Load the work item and actionable descendants (recursively).
            var workItem = await workItemRepository.GetByWorkItemNumberAsync(projectId, workItemNumber)
                ?? throw new InvalidOperationException($"Work item #{workItemNumber} not found in project {projectId}.");

            if (IsInPrOrBeyond(workItem.State))
            {
                throw new InvalidOperationException(
                    $"Work item #{workItemNumber} is already in '{workItem.State}' and cannot be executed.");
            }

            var childWorkItems = new List<Models.WorkItemDto>();
            await CollectDescendantsAsync(projectId, workItem.ChildWorkItemNumbers, childWorkItems);
            var directChildWorkItems = GetDirectActionableChildren(workItem, childWorkItems);

            // 2. Load the project to get the repo name
            var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
                ?? throw new InvalidOperationException($"Project {projectId} not found.");

            if (string.IsNullOrWhiteSpace(project.Repo))
                throw new InvalidOperationException("Project has no linked repository.");

            // 3. Resolve the user's tier and enforce concurrent execution caps.
            var userRole = await db.UserProfiles
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Role)
                .FirstOrDefaultAsync(cancellationToken);
            var normalizedRole = UserRoles.Normalize(userRole);
            var tierPolicy = TierPolicyCatalog.Get(normalizedRole);

            var activeExecutions = skipActiveExecutionCap
                ? 0
                : await db.AgentExecutions
                    .AsNoTracking()
                    .CountAsync(
                        e => e.UserId == userId.ToString() &&
                             e.ParentExecutionId == null &&
                             e.Status == "running",
                        cancellationToken);
            if (!skipActiveExecutionCap && activeExecutions >= tierPolicy.MaxActiveAgentExecutions)
            {
                throw new InvalidOperationException(
                    $"Active execution limit reached for the '{tierPolicy.Tier}' tier ({tierPolicy.MaxActiveAgentExecutions}).");
            }

            // 4. Charge quota at accepted run start.
            if (!skipQuotaCharge)
            {
                await _usageLedgerService.ChargeRunAsync(userId, MonthlyRunType.Coding, cancellationToken);
                codingRunCharged = true;
            }

            // 5. Resolve a GitHub access token that can access this repository.
            var accessToken = await ResolveRequiredRepoAccessTokenAsync(
                userId,
                project.Repo,
                cancellationToken);

            var repoFullName = project.Repo;
            var pullRequestTargetBranch = await ResolvePullRequestTargetBranchAsync(
                accessToken,
                repoFullName,
                targetBranch,
                cancellationToken);

            // 6. Use AI to determine which agents are needed for this work item.
            var workItemContext = BuildWorkItemContext(workItem, childWorkItems);
            var selectedModelKey = ModelKeys.Haiku;
            var executionMode = directChildWorkItems.Count > 0
                ? AgentExecutionModes.Orchestration
                : AgentExecutionModes.Standard;
            var pipeline = executionMode == AgentExecutionModes.Orchestration
                ? OrchestrationPreludePipeline
                : await SelectPipelineAsync(workItemContext, cancellationToken);
            logger.LogInformation(
                "Execution: AI-selected pipeline with {PhaseCount} agents, model={Model}",
                pipeline.SelectMany(g => g).Count(), selectedModelKey);

            // 7. Resolve collision-safe branch and PR title values.
            var plannedPrTitle = retryPlan?.ReusePullRequestTitle ?? BuildPullRequestTitle(workItem);
            string branchName;
            string? reusePullRequestUrl = null;
            var reusePullRequestNumber = 0;
            if (retryPlan?.ReuseExistingBranch == true)
            {
                branchName = retryPlan.ReuseBranchName!;
                if (retryPlan.ReuseExistingBranchAndPullRequest)
                {
                    reusePullRequestUrl = retryPlan.ReusePullRequestUrl;
                    reusePullRequestNumber = retryPlan.ReusePullRequestNumber ?? 0;
                }
            }
            else
            {
                var plannedBranch = BuildBranchName(project.BranchPattern, workItemNumber, workItem.Title);
                branchName = await ResolveUniqueBranchNameAsync(accessToken, repoFullName, plannedBranch, cancellationToken);
            }

            var (commitAuthorName, commitAuthorEmail) = ResolveCommitAuthor(
                project.CommitAuthorMode,
                project.CommitAuthorName,
                project.CommitAuthorEmail);

            // 8. Create the execution record
            var executionId = Guid.NewGuid().ToString("N")[..12];
            var totalRoles = pipeline.SelectMany(g => g).Count();
            var carriedRoleCount = CountCarryForwardRoles(pipeline, retryPlan?.CarryForwardOutputs);
            var seededProgress = retryPlan is null
                ? 0
                : Math.Clamp(
                    Math.Max(
                        retryPlan.PriorProgressEstimate,
                        totalRoles == 0 ? 0 : (double)carriedRoleCount / totalRoles),
                    0,
                    0.99);
            var execution = new AgentExecution
            {
                Id = executionId,
                WorkItemId = workItemNumber,
                WorkItemTitle = workItem.Title,
                ExecutionMode = executionMode,
                Status = "running",
                StartedAt = DateTime.UtcNow.ToString("o"),
                StartedAtUtc = DateTime.UtcNow,
                Progress = seededProgress,
                BranchName = branchName,
                PullRequestTitle = plannedPrTitle,
                PullRequestUrl = reusePullRequestUrl,
                CurrentPhase = carriedRoleCount > 0 ? "Resuming prior progress" : "Initializing",
                ParentExecutionId = parentExecutionId,
                UserId = userId.ToString(),
                ProjectId = projectId,
                Agents = BuildAgentInfoList(pipeline, retryPlan?.CarryForwardOutputs),
            };

            db.AgentExecutions.Add(execution);
            await db.SaveChangesAsync(cancellationToken);

            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.AgentsUpdated,
                new { projectId, executionId },
                cancellationToken);

            // 9. Mark the work item as in-progress
            await workItemRepository.UpdateAsync(projectId, workItemNumber,
                new UpdateWorkItemRequest(
                    Title: null, Description: null, Priority: null, Difficulty: null,
                    State: "Planning (AI)", AssignedTo: null, Tags: null, IsAI: null,
                    ParentWorkItemNumber: null, LevelId: null));

            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.WorkItemsUpdated,
                new { projectId, workItemNumber },
                cancellationToken);
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ProjectsUpdated,
                new { projectId },
                cancellationToken);

            // 10. Write an initial log entry
            await WriteLogEntryAsync(
                db,
                projectId,
                "System",
                "info",
                $"Execution {executionId} started for work item #{workItemNumber}: {workItem.Title}",
                executionId: executionId);

            if (retryPlan is not null)
            {
                var retrySummary = $"Retry context loaded from execution {retryPlan.SourceExecutionId} " +
                                   $"(status: {retryPlan.SourceStatus ?? "unknown"}, prior progress: {(int)Math.Round(retryPlan.PriorProgressEstimate * 100)}%)";
                await WriteLogEntryAsync(
                    db,
                    projectId,
                    "System",
                    "info",
                    retrySummary,
                    executionId: executionId);

                if (retryPlan.ReuseExistingBranchAndPullRequest)
                {
                    await WriteLogEntryAsync(
                        db,
                        projectId,
                        "System",
                        "info",
                        $"Resuming existing branch '{retryPlan.ReuseBranchName}' and PR: {retryPlan.ReusePullRequestUrl}",
                        executionId: executionId);
                }
                else if (retryPlan.ReuseExistingBranch)
                {
                    await WriteLogEntryAsync(
                        db,
                        projectId,
                        "System",
                        "info",
                        $"Resuming existing branch '{retryPlan.ReuseBranchName}' (a new PR will be opened if needed).",
                        executionId: executionId);
                }
            }

            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.LogsUpdated,
                new { projectId, executionId },
                cancellationToken);

            // 11. Fire-and-forget the pipeline on a background thread
            var cts = new CancellationTokenSource();
            ActiveExecutions[executionId] = (cts, "cancelled");
            SteeringNotes.TryAdd(executionId, new ConcurrentQueue<string>());
            _ = Task.Run(() => RunPipelineAsync(
                executionId, projectId, workItem, childWorkItems, repoFullName,
                branchName, commitAuthorName, commitAuthorEmail,
                userId, selectedModelKey, pipeline, tierPolicy.MaxConcurrentAgentsPerTask,
                pullRequestTargetBranch, retryPlan, reusePullRequestNumber, !skipQuotaCharge, cts.Token), CancellationToken.None);

            return executionId;
        }
        catch
        {
            if (codingRunCharged)
            {
                try
                {
                    await _usageLedgerService.RefundRunAsync(userId, MonthlyRunType.Coding, cancellationToken);
                }
                catch (Exception refundEx)
                {
                    logger.LogWarning(refundEx, "Failed to refund coding run usage during start-execution rollback");
                }
            }

            throw;
        }
    }

    public async Task<AgentExecutionStatus?> GetExecutionStatusAsync(
        string projectId, string executionId, CancellationToken cancellationToken = default)
    {
        var exec = await db.AgentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId && e.ProjectId == projectId, cancellationToken);

        if (exec is null) return null;

        return new AgentExecutionStatus(
            exec.Id,
            exec.Status,
            exec.CurrentPhase,
            exec.Progress,
            exec.BranchName,
            exec.PullRequestUrl,
            exec.Status == "failed" ? exec.Duration : null  // Duration field reused for error in legacy schema
        );
    }

    public async Task<AgentExecutionStatus?> GetExecutionStatusAsync(
        string executionId, CancellationToken cancellationToken = default)
    {
        var projectId = await db.AgentExecutions
            .AsNoTracking()
            .Where(e => e.Id == executionId)
            .Select(e => e.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        return projectId is null
            ? null
            : await GetExecutionStatusAsync(projectId, executionId, cancellationToken);
    }

    public async Task<bool> CancelExecutionAsync(string executionId)
    {
        var projectId = await db.AgentExecutions
            .AsNoTracking()
            .Where(e => e.Id == executionId)
            .Select(e => e.ProjectId)
            .FirstOrDefaultAsync();

        return projectId is not null && await CancelExecutionAsync(projectId, executionId);
    }

    public async Task<bool> CancelExecutionAsync(string projectId, string executionId)
    {
        var exists = await db.AgentExecutions
            .AsNoTracking()
            .AnyAsync(e => e.Id == executionId && e.ProjectId == projectId);
        if (!exists) return false;

        return await StopExecutionAsync(executionId, "cancelled");
    }

    public async Task<bool> PauseExecutionAsync(string executionId)
    {
        var projectId = await db.AgentExecutions
            .AsNoTracking()
            .Where(e => e.Id == executionId)
            .Select(e => e.ProjectId)
            .FirstOrDefaultAsync();

        return projectId is not null && await PauseExecutionAsync(projectId, executionId);
    }

    public async Task<bool> PauseExecutionAsync(string projectId, string executionId)
    {
        var exists = await db.AgentExecutions
            .AsNoTracking()
            .AnyAsync(e => e.Id == executionId && e.ProjectId == projectId);
        if (!exists) return false;

        return await StopExecutionAsync(executionId, "paused");
    }

    public async Task<bool> ResumeExecutionAsync(
        string projectId,
        string executionId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var pausedExecution = await db.AgentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Id == executionId && e.ProjectId == projectId,
                cancellationToken);

        if (pausedExecution is null)
            return false;

        if (!string.Equals(pausedExecution.Status, "paused", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only paused executions can be resumed.");

        if (ActiveExecutions.ContainsKey(executionId))
            throw new InvalidOperationException("Execution is still finishing its pause. Try resuming again in a moment.");

        var workItem = await workItemRepository.GetByWorkItemNumberAsync(projectId, pausedExecution.WorkItemId)
            ?? throw new InvalidOperationException(
                $"Work item #{pausedExecution.WorkItemId} for execution {executionId} could not be found.");

        var childWorkItems = new List<Models.WorkItemDto>();
        await CollectDescendantsAsync(projectId, workItem.ChildWorkItemNumbers, childWorkItems);

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        if (string.IsNullOrWhiteSpace(project.Repo))
            throw new InvalidOperationException("Project has no linked repository.");

        var userRole = await db.UserProfiles
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(cancellationToken);
        var normalizedRole = UserRoles.Normalize(userRole);
        var tierPolicy = TierPolicyCatalog.Get(normalizedRole);

        var activeExecutions = await db.AgentExecutions
            .AsNoTracking()
            .CountAsync(
                e => e.UserId == userId.ToString() &&
                     e.ParentExecutionId == null &&
                     e.Status == "running" &&
                     e.Id != executionId,
                cancellationToken);
        if (activeExecutions >= tierPolicy.MaxActiveAgentExecutions)
        {
            throw new InvalidOperationException(
                $"Active execution limit reached for the '{tierPolicy.Tier}' tier ({tierPolicy.MaxActiveAgentExecutions}).");
        }

        var repoFullName = project.Repo;
        var accessToken = await ResolveRequiredRepoAccessTokenAsync(userId, repoFullName, cancellationToken);
        var pullRequestTargetBranch = await ResolvePullRequestTargetBranchAsync(
            accessToken,
            repoFullName,
            requestedBranch: null,
            cancellationToken);

        var branchName = string.IsNullOrWhiteSpace(pausedExecution.BranchName)
            ? null
            : pausedExecution.BranchName.Trim();
        if (string.IsNullOrWhiteSpace(branchName))
            throw new InvalidOperationException("Paused execution is missing its branch name and cannot be resumed.");

        var pipeline = BuildPipelineFromExecutionAgents(pausedExecution.Agents);
        if (pipeline.Length == 0)
        {
            var workItemContext = BuildWorkItemContext(workItem, childWorkItems);
            pipeline = await SelectPipelineAsync(workItemContext, cancellationToken);
            logger.LogWarning(
                "Execution {ExecutionId}: paused execution had no reconstructable agent pipeline; falling back to AI-selected pipeline with {PhaseCount} roles",
                executionId,
                pipeline.SelectMany(group => group).Count());
        }

        var priorPhaseResults = await db.AgentPhaseResults
            .AsNoTracking()
            .Where(result => result.ExecutionId == pausedExecution.Id)
            .OrderBy(result => result.PhaseOrder)
            .ToListAsync(cancellationToken);
        var carryForwardOutputs = BuildResumeCarryForwardOutputs(priorPhaseResults, pausedExecution.Agents);

        var client = httpClientFactory.CreateClient("GitHub");
        var resumeFromRemoteBranch = await BranchExistsAsync(
            client,
            accessToken,
            repoFullName,
            branchName,
            cancellationToken);

        var retryPlan = new RetryExecutionPlan(
            SourceExecutionId: pausedExecution.Id,
            SourceStatus: pausedExecution.Status,
            ReuseBranchName: branchName,
            ReusePullRequestUrl: pausedExecution.PullRequestUrl,
            ReusePullRequestNumber: TryParsePullRequestNumber(pausedExecution.PullRequestUrl),
            ReusePullRequestTitle: pausedExecution.PullRequestTitle,
            PriorProgressEstimate: Math.Clamp(pausedExecution.Progress, 0, 1),
            CarryForwardOutputs: carryForwardOutputs,
            RetryContextMarkdown: BuildExecutionResumeContext(pausedExecution, priorPhaseResults),
            ResumeInPlace: true,
            ResumeFromRemoteBranch: resumeFromRemoteBranch);

        var totalRoles = pipeline.SelectMany(group => group).Count();
        var carriedRoleCount = CountCarryForwardRoles(pipeline, carryForwardOutputs);
        var resumedProgress = Math.Clamp(
            Math.Max(
                Math.Clamp(pausedExecution.Progress, 0, 1),
                totalRoles == 0 ? 0 : (double)carriedRoleCount / totalRoles),
            0,
            0.99);

        var trackedExecution = await db.AgentExecutions
            .FirstOrDefaultAsync(
                e => e.Id == executionId && e.ProjectId == projectId,
                cancellationToken);
        if (trackedExecution is null)
            return false;

        var cts = new CancellationTokenSource();
        if (!ActiveExecutions.TryAdd(executionId, (cts, "cancelled")))
        {
            cts.Dispose();
            throw new InvalidOperationException("Execution is already active.");
        }

        try
        {
            trackedExecution.Status = "running";
            trackedExecution.CompletedAtUtc = null;
            trackedExecution.CurrentPhase = carriedRoleCount > 0 ? "Resuming prior progress" : "Resuming paused execution";
            trackedExecution.Progress = resumedProgress;
            trackedExecution.Agents = BuildAgentInfoList(pipeline, carryForwardOutputs);
            await db.SaveChangesAsync(cancellationToken);

            await workItemRepository.UpdateAsync(projectId, workItem.WorkItemNumber,
                new UpdateWorkItemRequest(
                    Title: null, Description: null, Priority: null, Difficulty: null,
                    State: "In Progress (AI)", AssignedTo: null, Tags: null, IsAI: null,
                    ParentWorkItemNumber: null, LevelId: null));

            await WriteLogEntryAsync(
                db,
                projectId,
                "System",
                "info",
                $"Execution {executionId} resumed from paused state",
                executionId: executionId);

            var (commitAuthorName, commitAuthorEmail) = ResolveCommitAuthor(
                project.CommitAuthorMode,
                project.CommitAuthorName,
                project.CommitAuthorEmail);

            SteeringNotes.TryAdd(executionId, new ConcurrentQueue<string>());
            _ = Task.Run(() => RunPipelineAsync(
                executionId,
                projectId,
                workItem,
                childWorkItems,
                repoFullName,
                branchName,
                commitAuthorName,
                commitAuthorEmail,
                userId,
                ModelKeys.Haiku,
                pipeline,
                tierPolicy.MaxConcurrentAgentsPerTask,
                pullRequestTargetBranch,
                retryPlan,
                retryPlan.ReusePullRequestNumber ?? 0,
                string.IsNullOrWhiteSpace(pausedExecution.ParentExecutionId),
                cts.Token), CancellationToken.None);

            return true;
        }
        catch
        {
            if (ActiveExecutions.TryRemove(executionId, out var activeExecution))
                activeExecution.Cts.Dispose();

            SteeringNotes.TryRemove(executionId, out _);
            try
            {
                var executionToRestore = await db.AgentExecutions
                    .FirstOrDefaultAsync(
                        e => e.Id == executionId && e.ProjectId == projectId,
                        CancellationToken.None);
                if (executionToRestore is not null)
                {
                    executionToRestore.Status = "paused";
                    executionToRestore.CurrentPhase = pausedExecution.CurrentPhase ?? "Paused";
                    executionToRestore.Progress = pausedExecution.Progress;
                    executionToRestore.CompletedAtUtc = pausedExecution.CompletedAtUtc;
                    executionToRestore.Agents = pausedExecution.Agents
                        .Select(agent => new AgentInfo
                        {
                            Role = agent.Role,
                            Status = agent.Status,
                            CurrentTask = agent.CurrentTask,
                            Progress = agent.Progress,
                        })
                        .ToList();
                    await db.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception restoreEx)
            {
                logger.LogWarning(
                    restoreEx,
                    "Execution {ExecutionId}: failed to restore paused state after resume setup error",
                    executionId);
            }
            throw;
        }
    }

    public async Task<AgentExecutionDeletionResult?> DeleteExecutionAsync(
        string projectId,
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await db.AgentExecutions
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.Id == executionId)
            .Select(e => new { e.Id, e.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (execution is null)
            return null;

        if (!CanDeleteExecutionStatus(execution.Status))
            throw new InvalidOperationException("Completed runs cannot be deleted.");

        var descendantExecutionIds = await CollectDescendantExecutionIdsAsync(db, projectId, executionId, cancellationToken);
        DeletedExecutions[executionId] = 0;
        foreach (var descendantExecutionId in descendantExecutionIds)
            DeletedExecutions[descendantExecutionId] = 0;
        SteeringNotes.TryRemove(executionId, out _);
        foreach (var descendantExecutionId in descendantExecutionIds)
            SteeringNotes.TryRemove(descendantExecutionId, out _);

        if (ActiveExecutions.TryGetValue(executionId, out var activeExecution) &&
            !activeExecution.Cts.IsCancellationRequested)
        {
            activeExecution.Cts.Cancel();
        }

        foreach (var descendantExecutionId in descendantExecutionIds)
        {
            if (ActiveExecutions.TryGetValue(descendantExecutionId, out var descendantExecution) &&
                !descendantExecution.Cts.IsCancellationRequested)
            {
                descendantExecution.Cts.Cancel();
            }
        }

        var deletionResult = await agentTaskRepository.DeleteExecutionAsync(projectId, executionId);

        if (!ActiveExecutions.ContainsKey(executionId))
            DeletedExecutions.TryRemove(executionId, out _);
        foreach (var descendantExecutionId in descendantExecutionIds.Where(id => !ActiveExecutions.ContainsKey(id)))
            DeletedExecutions.TryRemove(descendantExecutionId, out _);

        return deletionResult;
    }

    public async Task<bool> SteerExecutionAsync(string projectId, string executionId, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return false;

        var execution = await db.AgentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId && e.ProjectId == projectId && e.Status == "running");

        if (execution is null) return false;

        var queue = SteeringNotes.GetOrAdd(executionId, _ => new ConcurrentQueue<string>());
        queue.Enqueue(note.Trim());

        if (int.TryParse(execution.UserId, out var executionUserId))
        {
            await notificationService.PublishAsync(
                userId: executionUserId,
                projectId: projectId,
                type: "execution_needs_input",
                title: "Execution received steering input",
                message: note.Trim(),
                executionId: executionId);
        }
        return true;
    }

    public async Task<string?> RetryExecutionAsync(
        string projectId,
        string executionId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var priorExecution = await db.AgentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId && e.ProjectId == projectId, cancellationToken);

        if (priorExecution is null)
            return null;

        if (string.Equals(priorExecution.Status, "running", StringComparison.OrdinalIgnoreCase))
            return null;

        var priorPhaseResults = await db.AgentPhaseResults
            .AsNoTracking()
            .Where(r => r.ExecutionId == priorExecution.Id)
            .OrderBy(r => r.PhaseOrder)
            .ToListAsync(cancellationToken);
        var carryForwardOutputs = BuildRetryCarryForwardOutputs(priorPhaseResults);

        var priorProgress = Math.Clamp(priorExecution.Progress, 0, 1);
        var reuseBranchName = string.IsNullOrWhiteSpace(priorExecution.BranchName)
            ? null
            : priorExecution.BranchName.Trim();
        var resumeFromRemoteBranch = false;
        if (!string.IsNullOrWhiteSpace(reuseBranchName))
        {
            var projectRepo = await db.Projects
                .AsNoTracking()
                .Where(project => project.Id == projectId)
                .Select(project => project.Repo)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(projectRepo))
            {
                var accessToken = await ResolveRequiredRepoAccessTokenAsync(userId, projectRepo, cancellationToken);
                var client = httpClientFactory.CreateClient("GitHub");
                resumeFromRemoteBranch = await BranchExistsAsync(
                    client,
                    accessToken,
                    projectRepo,
                    reuseBranchName,
                    cancellationToken);
            }
        }
        var shouldReuseExistingPullRequest =
            !string.Equals(priorExecution.Status, "completed", StringComparison.OrdinalIgnoreCase);

        var retryPlan = new RetryExecutionPlan(
            SourceExecutionId: priorExecution.Id,
            SourceStatus: priorExecution.Status,
            ReuseBranchName: reuseBranchName,
            ReusePullRequestUrl: shouldReuseExistingPullRequest ? priorExecution.PullRequestUrl : null,
            ReusePullRequestNumber: shouldReuseExistingPullRequest
                ? TryParsePullRequestNumber(priorExecution.PullRequestUrl)
                : null,
            ReusePullRequestTitle: shouldReuseExistingPullRequest ? priorExecution.PullRequestTitle : null,
            PriorProgressEstimate: priorProgress,
            CarryForwardOutputs: carryForwardOutputs,
            RetryContextMarkdown: BuildExecutionRetryContext(priorExecution, priorPhaseResults),
            ResumeFromRemoteBranch: resumeFromRemoteBranch);

        return await StartExecutionInternalAsync(
            projectId,
            priorExecution.WorkItemId,
            userId,
            targetBranch: null,
            retryPlan,
            parentExecutionId: null,
            skipQuotaCharge: false,
            skipActiveExecutionCap: false,
            cancellationToken);
    }

    public async Task<ExecutionDocumentationDto?> GetExecutionDocumentationAsync(
        string projectId,
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await db.AgentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId && e.ProjectId == projectId, cancellationToken);

        if (execution is null)
            return null;

        var descendantExecutionIds = await CollectDescendantExecutionIdsAsync(db, projectId, executionId, cancellationToken);
        var executionIds = descendantExecutionIds
            .Append(executionId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var phaseResults = await db.AgentPhaseResults
            .AsNoTracking()
            .Where(r => executionIds.Contains(r.ExecutionId))
            .OrderBy(r => r.PhaseOrder)
            .ToListAsync(cancellationToken);
        var phaseResultsByExecution = phaseResults
            .GroupBy(result => result.ExecutionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<AgentPhaseResult>)group.ToList());

        var descendantExecutions = descendantExecutionIds.Count == 0
            ? []
            : await db.AgentExecutions
                .AsNoTracking()
                .Where(child => descendantExecutionIds.Contains(child.Id))
                .OrderBy(child => child.StartedAtUtc)
                .ToListAsync(cancellationToken);

        var title = BuildExecutionDocumentationTitle(execution);
        phaseResultsByExecution.TryGetValue(execution.Id, out var rootPhaseResults);
        var markdown = BuildExecutionDocumentationMarkdown(
            execution,
            rootPhaseResults ?? [],
            descendantExecutions,
            phaseResultsByExecution);
        return new ExecutionDocumentationDto(
            execution.Id,
            title,
            markdown,
            execution.PullRequestUrl,
            BuildDiffUrl(execution.PullRequestUrl));
    }

    /// <summary>
    /// Signals an active execution to stop by cancelling its CancellationTokenSource.
    /// The pipeline loop will observe the token and finalize with the given status.
    /// </summary>
    private static Task<bool> StopExecutionAsync(string executionId, string finalStatus)
    {
        if (!ActiveExecutions.TryGetValue(executionId, out var entry))
            return Task.FromResult(false);

        // Update the desired final status before cancelling so the pipeline
        // picks up the correct status string in its OperationCanceledException handler.
        ActiveExecutions[executionId] = (entry.Cts, finalStatus);

        if (!entry.Cts.IsCancellationRequested)
            entry.Cts.Cancel();

        return Task.FromResult(true);
    }

    private async Task RunPipelineAsync(
        string executionId, string projectId, Models.WorkItemDto workItem,
        List<Models.WorkItemDto> childWorkItems,
        string repoFullName, string branchName,
        string commitAuthorName, string commitAuthorEmail,
        int userId, string selectedModelKey, AgentRole[][] pipeline,
        int maxConcurrentAgentsPerTask,
        string pullRequestTargetBranch,
        RetryExecutionPlan? retryPlan,
        int existingPullRequestNumber,
        bool billableExecution,
        CancellationToken externalCancellation)
    {
        // Use IServiceScopeFactory (singleton) instead of the request-scoped IServiceProvider.
        // The HTTP request scope is disposed after the controller returns Accepted(),
        // so a scoped IServiceProvider would throw ObjectDisposedException here.
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        var scopedConnectionService = scope.ServiceProvider.GetRequiredService<IConnectionService>();
        var scopedPhaseRunner = scope.ServiceProvider.GetRequiredService<IAgentPhaseRunner>();
        var scopedWorkItemRepo = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();
        var scopedNotificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var scopedUsageLedgerService = scope.ServiceProvider.GetRequiredService<IUsageLedgerService>();
        var scopedEventPublisher = scope.ServiceProvider.GetRequiredService<IServerEventPublisher>();

        IRepoSandbox? sandbox = null;

        Task PublishAgentsUpdatedAsync() =>
            scopedEventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.AgentsUpdated,
                new { projectId, executionId });

        Task PublishLogsUpdatedAsync() =>
            scopedEventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.LogsUpdated,
                new { projectId, executionId });

        Task PublishWorkItemsUpdatedAsync() =>
            scopedEventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.WorkItemsUpdated,
                new { projectId, workItemNumber = workItem.WorkItemNumber });

        Task PublishProjectsUpdatedAsync() =>
            scopedEventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ProjectsUpdated,
                new { projectId });

        async Task UpdateOrchestrationProgressAsync(int completedSubFlows, int totalSubFlows, string currentPhase)
        {
            var completionRatio = totalSubFlows <= 0 ? 0 : (double)completedSubFlows / totalSubFlows;
            var progress = Math.Min(0.95, 0.2 + (completionRatio * 0.75));
            await UpdateExecutionAsync(scopedDb, executionId, currentPhase, progress);
            await PublishAgentsUpdatedAsync();
        }

        async Task<string> EnsureSubFlowExecutionRunningAsync(Models.WorkItemDto childWorkItem)
        {
            var existingExecution = await scopedDb.AgentExecutions
                .AsNoTracking()
                .Where(execution =>
                    execution.ProjectId == projectId &&
                    execution.ParentExecutionId == executionId &&
                    execution.WorkItemId == childWorkItem.WorkItemNumber)
                .OrderByDescending(execution => execution.StartedAtUtc)
                .FirstOrDefaultAsync(externalCancellation);

            var orchestrationService = scope.ServiceProvider.GetRequiredService<IAgentOrchestrationService>();
            if (existingExecution is null)
            {
                return await orchestrationService.StartSubFlowExecutionAsync(
                    projectId,
                    childWorkItem.WorkItemNumber,
                    userId,
                    executionId,
                    pullRequestTargetBranch,
                    externalCancellation);
            }

            if (string.Equals(existingExecution.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingExecution.Status, "running", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingExecution.Status, "queued", StringComparison.OrdinalIgnoreCase))
            {
                return existingExecution.Id;
            }

            if (string.Equals(existingExecution.Status, "paused", StringComparison.OrdinalIgnoreCase))
            {
                var resumed = await orchestrationService.ResumeExecutionAsync(
                    projectId,
                    existingExecution.Id,
                    userId,
                    externalCancellation);
                if (!resumed)
                    throw new InvalidOperationException($"Paused sub-flow execution {existingExecution.Id} could not be resumed.");

                return existingExecution.Id;
            }

            throw new InvalidOperationException(
                $"Sub-flow #{childWorkItem.WorkItemNumber} is blocked by prior execution {existingExecution.Id} in status '{existingExecution.Status}'.");
        }

        async Task<Data.Entities.AgentExecution> WaitForTerminalSubFlowExecutionAsync(string childExecutionId)
        {
            while (true)
            {
                externalCancellation.ThrowIfCancellationRequested();

                var current = await scopedDb.AgentExecutions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(execution => execution.Id == childExecutionId, externalCancellation)
                    ?? throw new InvalidOperationException($"Sub-flow execution {childExecutionId} no longer exists.");

                if (!string.Equals(current.Status, "running", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(current.Status, "queued", StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }

                await Task.Delay(TimeSpan.FromSeconds(2), externalCancellation);
            }
        }

        async Task StopDescendantExecutionsAsync(string finalStatus)
        {
            var descendantIds = await CollectDescendantExecutionIdsAsync(scopedDb, projectId, executionId, CancellationToken.None);
            foreach (var descendantId in descendantIds)
            {
                await StopExecutionAsync(descendantId, finalStatus);
            }
        }

        async Task ExecuteSubFlowsAsync(
            Models.WorkItemDto parentWorkItem,
            IReadOnlyList<Models.WorkItemDto> directChildWorkItems)
        {
            var orderedChildren = directChildWorkItems
                .OrderBy(child => child.WorkItemNumber)
                .ToArray();
            var parallelism = Math.Clamp(Math.Min(maxConcurrentAgentsPerTask, MaxParallelSubFlows), 1, MaxParallelSubFlows);
            var completedSubFlows = 0;

            await WriteLogEntryAsync(
                scopedDb,
                projectId,
                "System",
                "info",
                $"Execution {executionId} is orchestrating {orderedChildren.Length} sub-flow(s) for work item #{parentWorkItem.WorkItemNumber}.",
                executionId: executionId);
            await PublishLogsUpdatedAsync();

            for (var batchStart = 0; batchStart < orderedChildren.Length; batchStart += parallelism)
            {
                var batch = orderedChildren.Skip(batchStart).Take(parallelism).ToArray();
                var batchLabel = string.Join(", ", batch.Select(child => $"#{child.WorkItemNumber}"));
                await UpdateOrchestrationProgressAsync(
                    completedSubFlows,
                    orderedChildren.Length,
                    $"Running sub-flows {batchLabel}");

                var launchedChildren = new List<(Models.WorkItemDto WorkItem, string ExecutionId)>(batch.Length);
                foreach (var child in batch)
                {
                    var childExecutionId = await EnsureSubFlowExecutionRunningAsync(child);
                    launchedChildren.Add((child, childExecutionId));
                }

                var terminalExecutions = await Task.WhenAll(
                    launchedChildren.Select(async launched =>
                        (launched.WorkItem, Execution: await WaitForTerminalSubFlowExecutionAsync(launched.ExecutionId))));

                var blockingExecution = terminalExecutions.FirstOrDefault(result =>
                    !string.Equals(result.Execution.Status, "completed", StringComparison.OrdinalIgnoreCase));
                if (blockingExecution.Execution is not null)
                {
                    throw new InvalidOperationException(
                        $"Sub-flow #{blockingExecution.WorkItem.WorkItemNumber} ended in status '{blockingExecution.Execution.Status}'.");
                }

                completedSubFlows += terminalExecutions.Length;
                await UpdateOrchestrationProgressAsync(
                    completedSubFlows,
                    orderedChildren.Length,
                    completedSubFlows >= orderedChildren.Length
                        ? "Sub-flows completed"
                        : $"Completed {completedSubFlows}/{orderedChildren.Length} sub-flows");
            }

            var refreshedDirectChildren = new List<Models.WorkItemDto>();
            await CollectDirectChildrenAsync(projectId, parentWorkItem.ChildWorkItemNumbers, refreshedDirectChildren);
            var parentState = ResolveParentFlowState(refreshedDirectChildren);

            await FinalizeExecutionAsync(scopedDb, executionId, "completed");
            await PublishAgentsUpdatedAsync();

            await scopedWorkItemRepo.UpdateAsync(
                projectId,
                parentWorkItem.WorkItemNumber,
                new UpdateWorkItemRequest(
                    Title: null,
                    Description: null,
                    Priority: null,
                    Difficulty: null,
                    State: parentState,
                    AssignedTo: null,
                    Tags: null,
                    IsAI: null,
                    ParentWorkItemNumber: null,
                    LevelId: null));

            await PublishWorkItemsUpdatedAsync();
            await PublishProjectsUpdatedAsync();

            await WriteLogEntryAsync(
                scopedDb,
                projectId,
                "System",
                "success",
                $"Execution {executionId} completed after orchestrating {orderedChildren.Length} sub-flow(s).",
                executionId: executionId);
            await PublishLogsUpdatedAsync();
            await scopedNotificationService.PublishAsync(
                userId,
                projectId,
                "execution_completed",
                $"Execution completed for #{parentWorkItem.WorkItemNumber}",
                $"{orderedChildren.Length} sub-flow(s) completed.",
                executionId);
        }

        try
        {
            var model = modelCatalog.Get(selectedModelKey);
            logger.LogInformation("Execution {ExecutionId}: using model {Model} (key={ModelKey})",
                executionId, model, selectedModelKey);

            // Create and clone the sandbox
            sandbox = scope.ServiceProvider.GetRequiredService<IRepoSandbox>();
            logger.LogInformation("Execution {ExecutionId}: cloning {Repo} → branch {Branch}",
                executionId, repoFullName, branchName);

            var accessToken = await ResolveRequiredRepoAccessTokenAsync(
                scopedConnectionService,
                userId,
                repoFullName,
                externalCancellation);

            await sandbox.CloneAsync(
                repoFullName,
                accessToken,
                branchName,
                externalCancellation,
                baseBranch: pullRequestTargetBranch,
                resumeFromBranch: retryPlan?.ResumeFromRemoteBranch == true);

            var toolContext = new AgentToolContext(
                sandbox, projectId, userId.ToString(), accessToken, repoFullName, executionId);

            string? prUrl = retryPlan?.ReuseExistingBranchAndPullRequest == true &&
                            !string.IsNullOrWhiteSpace(retryPlan.ReusePullRequestUrl)
                ? retryPlan.ReusePullRequestUrl
                : null;
            var prNumber = existingPullRequestNumber;
            var draftPullRequestReady = !string.IsNullOrWhiteSpace(prUrl);

            // Build the initial user message with work item context (includes children)
            var workItemContext = BuildWorkItemContext(workItem, childWorkItems);
            if (retryPlan is not null && !string.IsNullOrWhiteSpace(retryPlan.RetryContextMarkdown))
            {
                workItemContext = $"{workItemContext}\n\n{retryPlan.RetryContextMarkdown}";
            }

            var totalRoles = pipeline.SelectMany(g => g).Count();
            var phaseOrder = 0;
            var reviewLoopCount = 0;
            var currentWorkItemContext = workItemContext;
            var latestCycleReviewDecision = default(ReviewTriageDecision);
            var pendingReviewDecision = default(ReviewTriageDecision);
            var currentCyclePipeline = pipeline;
            var currentCycleCarryForwardOutputs = retryPlan?.CarryForwardOutputs is null
                ? new Dictionary<AgentRole, string>()
                : retryPlan.CarryForwardOutputs.ToDictionary(entry => entry.Key, entry => entry.Value);
            var currentRawOutputsByRole = new Dictionary<AgentRole, string>();
            var orchestrationExecution = IsOrchestrationPrelude(pipeline);
            using var dbLock = new SemaphoreSlim(1, 1);

            async Task WithDbLockAsync(Func<Task> action)
            {
                await dbLock.WaitAsync(externalCancellation);
                try
                {
                    ThrowIfExecutionDeleted(executionId);
                    await action();
                }
                finally
                {
                    dbLock.Release();
                }
            }

            static string BuildSteeringBlock(IReadOnlyList<string> notes)
            {
                if (notes.Count == 0)
                    return string.Empty;

                var block = new StringBuilder();
                block.AppendLine();
                block.AppendLine("## Live Steering Notes");
                foreach (var note in notes)
                {
                    block.AppendLine($"- {note}");
                }

                return block.ToString();
            }

            // Transition from planning to active AI execution.
            await scopedWorkItemRepo.UpdateAsync(projectId, workItem.WorkItemNumber,
                new UpdateWorkItemRequest(
                    Title: null, Description: null, Priority: null, Difficulty: null,
                    State: "In Progress (AI)", AssignedTo: null, Tags: null, IsAI: null,
                    ParentWorkItemNumber: null, LevelId: null));
            await PublishWorkItemsUpdatedAsync();
            await PublishProjectsUpdatedAsync();

            while (true)
            {
                latestCycleReviewDecision = null;
                currentWorkItemContext = workItemContext;
                if (pendingReviewDecision is not null)
                {
                    var queuedRerunRoles = currentCyclePipeline.SelectMany(group => group).ToArray();
                    currentWorkItemContext = $"{workItemContext}\n\n{ReviewFeedbackLoopPlanner.BuildAutomaticReviewFeedbackContext(pendingReviewDecision, queuedRerunRoles, reviewLoopCount)}";
                }

                var currentOutputsByRole = new Dictionary<AgentRole, string>(currentCycleCarryForwardOutputs);
                var carriedRoles = currentOutputsByRole.Keys.ToHashSet();
                var completedRoles = CountCarryForwardRoles(pipeline, currentOutputsByRole);

                if (carriedRoles.Count > 0)
                {
                    var carriedRoleLabel = reviewLoopCount == 0
                        ? retryPlan?.ResumeInPlace == true
                            ? "paused execution context"
                            : "retry context"
                        : $"review remediation cycle {reviewLoopCount}";
                    await WriteLogEntryAsync(
                        scopedDb,
                        projectId,
                        "System",
                        "info",
                        $"Carrying forward completed phases from {carriedRoleLabel}: {string.Join(", ", pipeline.SelectMany(g => g).Where(carriedRoles.Contains))}",
                        executionId: executionId);
                    await PublishLogsUpdatedAsync();
                }

                foreach (var group in currentCyclePipeline)
                {
                    externalCancellation.ThrowIfCancellationRequested();

                    var rolesInGroup = group.ToArray();
                    if (rolesInGroup.Length == 0)
                        continue;

                    var groupCompletedBase = completedRoles;
                    var currentPhase = rolesInGroup.Length == 1
                        ? rolesInGroup[0].ToString()
                        : $"Parallel: {string.Join(", ", rolesInGroup.Select(r => r.ToString()))}";

                    await WithDbLockAsync(async () =>
                    {
                        await UpdateExecutionAsync(
                            scopedDb,
                            executionId,
                            currentPhase,
                            totalRoles == 0 ? 0 : (double)groupCompletedBase / totalRoles);
                    });
                    await PublishAgentsUpdatedAsync();

                    var notes = new List<string>();
                    if (SteeringNotes.TryGetValue(executionId, out var queue))
                    {
                        while (queue.TryDequeue(out var queuedNote))
                        {
                            notes.Add(queuedNote);
                        }
                    }

                    var steeringBlock = BuildSteeringBlock(notes);
                    if (notes.Count > 0)
                    {
                        await WithDbLockAsync(async () =>
                        {
                            await WriteLogEntryAsync(
                                scopedDb,
                                projectId,
                                "System",
                                "info",
                                $"Applied {notes.Count} steering note(s) before phase group {currentPhase}",
                                executionId: executionId);
                        });
                        await PublishLogsUpdatedAsync();
                    }

                    var priorOutputs = BuildCarryForwardPhaseOutputs(pipeline, currentOutputsByRole);
                    var pendingRoleEntries = rolesInGroup
                        .Select((role, index) => (Role: role, Index: index))
                        .Where(entry => !carriedRoles.Contains(entry.Role))
                        .ToArray();

                    if (pendingRoleEntries.Length == 0)
                    {
                        continue;
                    }

                    var executionOrderResults = new List<RolePhaseExecutionResult>(rolesInGroup.Length);
                    var maxParallel = Math.Max(1, maxConcurrentAgentsPerTask);

                    for (var batchStart = 0; batchStart < pendingRoleEntries.Length; batchStart += maxParallel)
                    {
                        var batchRoles = pendingRoleEntries.Skip(batchStart).Take(maxParallel).ToArray();
                        var batchTasks = batchRoles
                            .Select(entry => RunRoleAsync(
                                entry.Role,
                                entry.Index,
                                priorOutputs,
                                steeringBlock,
                                groupCompletedBase))
                            .ToArray();

                        var batchResults = await Task.WhenAll(batchTasks);
                        executionOrderResults.AddRange(batchResults);

                        var failedRole = batchResults.FirstOrDefault(r => !r.Success);
                        if (failedRole is not null)
                        {
                            throw new InvalidOperationException(
                                $"Agent {failedRole.Role} failed after {failedRole.AttemptsUsed} attempt(s): {failedRole.Error ?? "Unknown error"}");
                        }
                    }

                    foreach (var role in rolesInGroup.Where(role => !carriedRoles.Contains(role)))
                    {
                        var roleResult = executionOrderResults.First(r => r.Role == role);
                        currentOutputsByRole[role] = roleResult.SummarizedOutput;
                        if (!string.IsNullOrWhiteSpace(roleResult.RawOutput))
                            currentRawOutputsByRole[role] = roleResult.RawOutput;
                    }

                    carriedRoles = currentOutputsByRole.Keys.ToHashSet();
                    completedRoles = CountCarryForwardRoles(pipeline, currentOutputsByRole);

                    if (reviewLoopCount == 0 && rolesInGroup.Contains(AgentRole.Planner))
                    {
                        var directChildWorkItems = GetDirectActionableChildren(workItem, childWorkItems);
                        if (directChildWorkItems.Count == 0 &&
                            currentRawOutputsByRole.TryGetValue(AgentRole.Planner, out var plannerOutput))
                        {
                            var generatedPlan = SubFlowPlanner.Parse(plannerOutput);
                            if (generatedPlan is not null)
                            {
                                var createdSubFlows = await MaterializeGeneratedSubFlowsAsync(
                                    scopedWorkItemRepo,
                                    projectId,
                                    workItem,
                                    generatedPlan,
                                    executionId,
                                    scopedDb);
                                if (createdSubFlows.Count > 0)
                                {
                                    childWorkItems.AddRange(createdSubFlows);
                                    directChildWorkItems = GetDirectActionableChildren(workItem, childWorkItems);
                                    orchestrationExecution = true;
                                    currentCyclePipeline = OrchestrationPreludePipeline;
                                    draftPullRequestReady = false;
                                    prUrl = null;
                                    prNumber = 0;
                                    await PublishWorkItemsUpdatedAsync();
                                    await PublishProjectsUpdatedAsync();
                                    await PublishLogsUpdatedAsync();
                                }
                            }
                        }

                        if (directChildWorkItems.Count > 0)
                        {
                            orchestrationExecution = true;
                            await TransitionExecutionToOrchestrationModeAsync(
                                scopedDb,
                                executionId,
                                directChildWorkItems,
                                currentOutputsByRole);
                            await PublishAgentsUpdatedAsync();
                            await ExecuteSubFlowsAsync(workItem, directChildWorkItems);
                            return;
                        }

                        if (!draftPullRequestReady)
                        {
                            (prUrl, prNumber) = await OpenDraftPullRequestAsync(
                                sandbox,
                                accessToken,
                                repoFullName,
                                workItem,
                                commitAuthorName,
                                commitAuthorEmail,
                                pullRequestTargetBranch,
                                scopedDb,
                                executionId,
                                externalCancellation);
                            draftPullRequestReady = true;
                        }
                    }
                }

                if (latestCycleReviewDecision is null || !latestCycleReviewDecision.RequiresAutomaticLoop)
                    break;

                reviewLoopCount++;
                if (reviewLoopCount > MaxAutomaticReviewLoops)
                {
                    throw new InvalidOperationException(
                        $"Review requested {latestCycleReviewDecision.RecommendationLabel} again after {MaxAutomaticReviewLoops} automatic remediation cycle(s). " +
                        $"{latestCycleReviewDecision.Summary}".Trim());
                }

                var rerunRoles = ReviewFeedbackLoopPlanner.DetermineRolesToRerun(pipeline, latestCycleReviewDecision);
                if (rerunRoles.Count == 0)
                    throw new InvalidOperationException("Review requested another remediation loop, but Fleet could not determine which phases to rerun.");

                currentCyclePipeline = ReviewFeedbackLoopPlanner.BuildPipelineSubset(pipeline, rerunRoles);
                currentCycleCarryForwardOutputs = currentOutputsByRole
                    .Where(entry => !rerunRoles.Contains(entry.Key))
                    .ToDictionary(entry => entry.Key, entry => entry.Value);
                pendingReviewDecision = latestCycleReviewDecision;

                await WithDbLockAsync(async () =>
                {
                    await PrepareExecutionForAutomaticReviewLoopAsync(
                        scopedDb,
                        executionId,
                        pipeline,
                        rerunRoles,
                        currentCycleCarryForwardOutputs,
                        latestCycleReviewDecision,
                        reviewLoopCount);

                    await WriteLogEntryAsync(
                        scopedDb,
                        projectId,
                        "System",
                        "warn",
                        BuildAutomaticReviewLoopLogMessage(latestCycleReviewDecision, rerunRoles, reviewLoopCount),
                        executionId: executionId);
                });
                await PublishAgentsUpdatedAsync();
                await PublishLogsUpdatedAsync();
            }

            async Task<RolePhaseExecutionResult> RunRoleAsync(
                AgentRole role,
                int roleIndex,
                List<(AgentRole Role, string Output)> priorOutputs,
                string steeringBlock,
                int groupCompletedBase)
            {
                var maxAttempts = MaxAgentRetries + 1;
                var baseUserMessage = BuildPhaseMessage(role, currentWorkItemContext, priorOutputs, draftPullRequestReady);
                if (!string.IsNullOrWhiteSpace(steeringBlock))
                    baseUserMessage += steeringBlock;

                var priorAttempts = new List<PhaseResult>();

                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    var isRetry = attempt > 1;
                    var retryProgressFloorPercent = priorAttempts.LastOrDefault()?.EstimatedCompletionPercent ?? 0;
                    var lastLoggedPercent = retryProgressFloorPercent > 0 ? retryProgressFloorPercent : -1;
                    var lastLoggedSummary = string.Empty;
                    var initialProgressSummary = isRetry
                        ? retryProgressFloorPercent > 0
                            ? $"Retrying phase (attempt {attempt}/{maxAttempts}, resuming from {retryProgressFloorPercent}%)"
                            : $"Retrying phase (attempt {attempt}/{maxAttempts})"
                        : $"Starting phase: {GetPhaseTaskDescription(role)}";

                    await WithDbLockAsync(async () =>
                    {
                        await SetAgentRunningAsync(scopedDb, executionId, role.ToString(),
                            isRetry
                                ? $"{GetPhaseTaskDescription(role)} (retry {attempt - 1}/{MaxAgentRetries})"
                                : GetPhaseTaskDescription(role),
                            retryProgressFloorPercent / 100.0);

                        await WriteLogEntryAsync(
                            scopedDb,
                            projectId,
                            $"{role} Agent",
                            "info",
                            initialProgressSummary,
                            executionId: executionId);
                    });
                    await PublishAgentsUpdatedAsync();
                    await PublishLogsUpdatedAsync();

                    var userMessage = BuildRetryAwarePhaseMessage(baseUserMessage, role, priorAttempts);
                    logger.LogInformation(
                        "Execution {ExecutionId}: starting phase {Role} attempt {Attempt}/{MaxAttempts}",
                        executionId,
                        role,
                        attempt,
                        maxAttempts);

                    PhaseProgressCallback onProgress = async (estimatedProgress, summary) =>
                    {
                        await WithDbLockAsync(async () =>
                        {
                            var overallProgress = ((double)(groupCompletedBase + roleIndex) + estimatedProgress) / totalRoles;

                            var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
                            if (exec is null) return;

                            var clampedOverall = Math.Clamp(overallProgress, 0, 0.99);
                            exec.Progress = Math.Max(exec.Progress, clampedOverall);

                            var agent = exec.Agents.FirstOrDefault(a => a.Role == role.ToString());
                            string? previousTask = null;
                            if (agent is not null)
                            {
                                previousTask = agent.CurrentTask;
                                if (!string.IsNullOrWhiteSpace(summary))
                                    agent.CurrentTask = summary;

                                var clampedRoleProgress = Math.Clamp(estimatedProgress, 0, 0.99);
                                agent.Progress = Math.Max(agent.Progress, clampedRoleProgress);
                            }

                            await scopedDb.SaveChangesAsync();

                            if (!string.IsNullOrWhiteSpace(summary) &&
                                !string.Equals(previousTask, summary, StringComparison.Ordinal))
                            {
                                var isHeartbeat = IsHeartbeatProgressSummary(summary);
                                await WriteLogEntryAsync(
                                    scopedDb,
                                    projectId,
                                    $"{role} Agent",
                                    "info",
                                    $"Status update: {summary}",
                                    isDetailed: isHeartbeat,
                                    executionId: executionId);
                                lastLoggedSummary = summary;
                            }

                            var pct = (int)Math.Round(estimatedProgress * 100);
                            if (pct != lastLoggedPercent)
                            {
                                await WriteLogEntryAsync(
                                    scopedDb,
                                    projectId,
                                    $"{role} Agent",
                                    "info",
                                    $"Progress: {pct}%",
                                    executionId: executionId);
                                lastLoggedPercent = pct;
                            }
                            else if (!string.IsNullOrWhiteSpace(summary) &&
                                     !string.Equals(lastLoggedSummary, summary, StringComparison.Ordinal))
                            {
                                // Preserve useful status transitions even when percent does not advance.
                                var isHeartbeat = IsHeartbeatProgressSummary(summary);
                                await WriteLogEntryAsync(
                                    scopedDb,
                                    projectId,
                                    $"{role} Agent",
                                    "info",
                                    $"Status update: {summary}",
                                    isDetailed: isHeartbeat,
                                    executionId: executionId);
                                lastLoggedSummary = summary;
                            }
                        });

                        await PublishAgentsUpdatedAsync();
                        await PublishLogsUpdatedAsync();
                    };

                    PhaseToolCallLogger onToolCall = async (toolName, resultSnippet) =>
                    {
                        await WithDbLockAsync(async () =>
                        {
                            var logMsg = $"Tool: {toolName} -> {resultSnippet}";
                            await WriteLogEntryAsync(
                                scopedDb,
                                projectId,
                                $"{role} Agent",
                                "info",
                                logMsg,
                                isDetailed: true,
                                executionId: executionId);
                        });
                        await PublishLogsUpdatedAsync();
                    };

                    var maxTokens = GetMaxTokensForRole(role);
                    var phaseStart = DateTime.UtcNow;
                    var result = await scopedPhaseRunner.RunPhaseAsync(
                        role,
                        userMessage,
                        toolContext,
                        model,
                        maxTokens,
                        onProgress,
                        onToolCall,
                        externalCancellation);
                    var phaseEnd = DateTime.UtcNow;
                    var rolePhaseOrder = Interlocked.Increment(ref phaseOrder) - 1;

                    await WithDbLockAsync(async () =>
                    {
                        ThrowIfExecutionDeleted(executionId);
                        var phaseResultEntity = new AgentPhaseResult
                        {
                            Role = role.ToString(),
                            Output = result.Output,
                            ToolCallCount = result.ToolCallCount,
                            Success = result.Success,
                            Error = result.Error,
                            StartedAt = phaseStart,
                            CompletedAt = phaseEnd,
                            PhaseOrder = rolePhaseOrder,
                            ExecutionId = executionId,
                        };
                        scopedDb.AgentPhaseResults.Add(phaseResultEntity);
                        await scopedDb.SaveChangesAsync();

                        await UpdateAgentInfoAsync(
                            scopedDb,
                            executionId,
                            role.ToString(),
                            result.Success ? "completed" : "failed",
                            result.ToolCallCount,
                            Math.Clamp(result.EstimatedCompletionPercent / 100.0, 0, 0.99),
                            result.Success
                                ? null
                                : BuildRetryFailureTask(result.EstimatedCompletionPercent, result.LastProgressSummary));

                        if (result.Success)
                        {
                            var successMsg = attempt == 1
                                ? $"Phase completed ({result.ToolCallCount} tool calls)"
                                : $"Phase completed after retry ({attempt}/{maxAttempts}, {result.ToolCallCount} tool calls)";
                            await WriteLogEntryAsync(
                                scopedDb,
                                projectId,
                                $"{role} Agent",
                                "success",
                                successMsg,
                                executionId: executionId);
                        }
                        else
                        {
                            var errorText = NormalizeAgentFailureMessage(result.Error);
                            await WriteLogEntryAsync(
                                scopedDb,
                                projectId,
                                $"{role} Agent",
                                "error",
                                $"Phase failed on attempt {attempt}/{maxAttempts}: {errorText}",
                                executionId: executionId);

                            logger.LogWarning(
                                "Execution {ExecutionId}: phase {Role} failed on attempt {Attempt}/{MaxAttempts}: {Error}",
                                executionId,
                                role,
                                attempt,
                                maxAttempts,
                                errorText);
                        }
                    });
                    await PublishAgentsUpdatedAsync();
                    await PublishLogsUpdatedAsync();

                    if (result.Success)
                    {
                        if (role == AgentRole.Review)
                            latestCycleReviewDecision = ReviewFeedbackLoopPlanner.ParseDecision(result.Output);

                        var summarized = await SummarizePhaseOutputAsync(role, result.Output, externalCancellation);
                        return new RolePhaseExecutionResult(
                            role,
                            true,
                            summarized,
                            result.Output,
                            null,
                            attempt);
                    }

                    priorAttempts.Add(result);
                }

                var finalError = NormalizeAgentFailureMessage(priorAttempts.LastOrDefault()?.Error);
                return new RolePhaseExecutionResult(
                    role,
                    false,
                    string.Empty,
                    string.Empty,
                    finalError,
                    maxAttempts);
            }

            if (orchestrationExecution)
            {
                var directChildWorkItems = GetDirectActionableChildren(workItem, childWorkItems);
                if (directChildWorkItems.Count > 0)
                {
                    await TransitionExecutionToOrchestrationModeAsync(
                        scopedDb,
                        executionId,
                        directChildWorkItems,
                        currentCycleCarryForwardOutputs);
                    await PublishAgentsUpdatedAsync();
                    await ExecuteSubFlowsAsync(workItem, directChildWorkItems);
                    return;
                }
            }

            if (!draftPullRequestReady)
            {
                (prUrl, prNumber) = await OpenDraftPullRequestAsync(
                    sandbox,
                    accessToken,
                    repoFullName,
                    workItem,
                    commitAuthorName,
                    commitAuthorEmail,
                    pullRequestTargetBranch,
                    scopedDb,
                    executionId,
                    externalCancellation);
                draftPullRequestReady = true;
            }

            // Pipeline complete — final commit + push so the draft PR has all changes
            accessToken = await ResolveRequiredRepoAccessTokenAsync(
                scopedConnectionService,
                userId,
                repoFullName,
                externalCancellation);

            await sandbox.CommitAndPushAsync(
                accessToken,
                $"fleet: finalize changes for #{workItem.WorkItemNumber}",
                authorName: commitAuthorName,
                authorEmail: commitAuthorEmail,
                externalCancellation);

            // Mark the draft PR as ready for review
            var resolvedPrNumber = prNumber > 0
                ? prNumber
                : (TryParsePullRequestNumber(prUrl) ?? 0);
            if (resolvedPrNumber <= 0)
            {
                var discoveredPr = await FindOpenPullRequestByHeadBranchAsync(
                    accessToken,
                    repoFullName,
                    sandbox.BranchName,
                    externalCancellation);
                if (discoveredPr.Number > 0)
                {
                    resolvedPrNumber = discoveredPr.Number;
                    if (string.IsNullOrWhiteSpace(prUrl))
                        prUrl = discoveredPr.Url;
                }
            }

            if (string.IsNullOrWhiteSpace(prUrl))
            {
                throw new InvalidOperationException(
                    $"Execution completed its work, but Fleet could not create or locate a GitHub pull request for branch '{sandbox.BranchName}'.");
            }

            if (resolvedPrNumber > 0)
            {
                await MarkPullRequestReadyAsync(accessToken, repoFullName, resolvedPrNumber, externalCancellation);
                await scopedNotificationService.PublishAsync(
                    userId,
                    projectId,
                    "pr_ready",
                    $"PR ready for #{workItem.WorkItemNumber}",
                    prUrl ?? "A pull request is ready for review.",
                        executionId);
            }
            else if (!string.IsNullOrWhiteSpace(prUrl))
            {
                logger.LogWarning(
                    "Execution {ExecutionId}: pipeline succeeded but could not resolve PR number from URL {PrUrl}",
                    executionId,
                    prUrl);
            }

            var prLifecycle = resolvedPrNumber > 0
                ? await GetPullRequestLifecycleAsync(accessToken, repoFullName, resolvedPrNumber, externalCancellation)
                : null;

            if (IsExecutionDeleted(executionId))
                return;

            await FinalizeExecutionAsync(scopedDb, executionId, "completed", prUrl);
            await PublishAgentsUpdatedAsync();
            await scopedNotificationService.PublishAsync(
                userId,
                projectId,
                "execution_completed",
                $"Execution completed for #{workItem.WorkItemNumber}",
                workItem.Title,
                executionId);

            // Update the assigned work item and all included descendants so they share PR-linked status.
            var workItemsToUpdate = childWorkItems
                .Append(workItem)
                .GroupBy(item => item.WorkItemNumber)
                .Select(group => group.First());

            foreach (var itemToUpdate in workItemsToUpdate)
            {
                var targetState = ResolveStateFromPullRequestLifecycle(itemToUpdate.IsAI, prLifecycle);
                var observedPullRequestState = ResolveObservedPullRequestState(prLifecycle);
                var updated = await scopedWorkItemRepo.UpdateAsync(projectId, itemToUpdate.WorkItemNumber,
                    new UpdateWorkItemRequest(
                        Title: null, Description: null, Priority: null, Difficulty: null,
                        State: targetState, AssignedTo: null, Tags: null, IsAI: null,
                        ParentWorkItemNumber: null, LevelId: null, LinkedPullRequestUrl: prUrl,
                        LastObservedPullRequestState: observedPullRequestState,
                        LastObservedPullRequestUrl: prUrl));

                if (updated is null)
                {
                    logger.LogWarning(
                        "Execution {ExecutionId}: failed to update work item #{WorkItemNumber} to PR-linked state",
                        executionId,
                        itemToUpdate.WorkItemNumber);
                }
            }

            await PublishWorkItemsUpdatedAsync();
            await PublishProjectsUpdatedAsync();
            await WriteLogEntryAsync(
                scopedDb,
                projectId,
                "System",
                "success",
                $"Execution {executionId} completed successfully" +
                (prUrl is not null ? $" -- PR: {prUrl}" : ""),
                executionId: executionId);
            await PublishLogsUpdatedAsync();

            logger.LogInformation("Execution {ExecutionId}: pipeline completed successfully", executionId);
        }
        catch (OperationCanceledException) when (IsExecutionDeleted(executionId))
        {
            logger.LogInformation("Execution {ExecutionId}: pipeline stopped after deletion", executionId);
        }
        catch (OperationCanceledException)
        {
            // Execution was stopped or paused externally via CancelExecutionAsync / PauseExecutionAsync.
            var finalStatus = ActiveExecutions.TryGetValue(executionId, out var entry) ? entry.FinalStatus : "cancelled";
            logger.LogInformation("Execution {ExecutionId}: pipeline {Status} by user", executionId, finalStatus);

            try
            {
                await StopDescendantExecutionsAsync(finalStatus);
                await FinalizeExecutionAsync(scopedDb, executionId, finalStatus);
                await PublishAgentsUpdatedAsync();
                await WriteLogEntryAsync(
                    scopedDb,
                    projectId,
                    "System",
                    "warn",
                    $"Execution {executionId} was {finalStatus} by user",
                    executionId: executionId);
                await PublishLogsUpdatedAsync();
                if (finalStatus == "paused")
                {
                    await scopedNotificationService.PublishAsync(
                        userId,
                        projectId,
                        "execution_needs_input",
                        $"Execution paused for #{workItem.WorkItemNumber}",
                        "Execution is paused and may require additional guidance.",
                        executionId);
                }
            }
            catch (Exception dbEx)
            {
                logger.LogError(dbEx, "Execution {ExecutionId}: failed to persist {Status} status to DB", executionId, finalStatus);
            }
        }
        catch (Exception ex) when (IsExecutionDeleted(executionId))
        {
            logger.LogInformation(ex, "Execution {ExecutionId}: pipeline error ignored after deletion", executionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Execution {ExecutionId}: pipeline failed with exception", executionId);

            try
            {
                await StopDescendantExecutionsAsync("cancelled");
            }
            catch (Exception descendantStopEx)
            {
                logger.LogWarning(descendantStopEx, "Execution {ExecutionId}: failed to stop descendant sub-flows", executionId);
            }

            if (billableExecution)
            {
                try
                {
                    await scopedUsageLedgerService.RefundRunAsync(userId, MonthlyRunType.Coding, externalCancellation);
                }
                catch (Exception refundEx)
                {
                    logger.LogWarning(refundEx, "Execution {ExecutionId}: failed to refund coding run usage", executionId);
                }
            }

            // Wrap error-handling DB writes in their own try/catch so a secondary failure
            // (e.g., broken DB connection) doesn't mask the original error.
            try
            {
                await FinalizeExecutionAsync(scopedDb, executionId, "failed", errorMessage: ex.Message);
                await PublishAgentsUpdatedAsync();
                await WriteLogEntryAsync(
                    scopedDb,
                    projectId,
                    "System",
                    "error",
                    $"Execution {executionId} failed: {ex.Message}",
                    executionId: executionId);
                await PublishLogsUpdatedAsync();
                await scopedNotificationService.PublishAsync(
                    userId,
                    projectId,
                    "execution_failed",
                    $"Execution failed for #{workItem.WorkItemNumber}",
                    ex.Message,
                    executionId);
                await PublishProjectsUpdatedAsync();
            }
            catch (Exception dbEx)
            {
                logger.LogError(dbEx, "Execution {ExecutionId}: failed to persist error status to DB", executionId);
            }
        }
        finally
        {
            // Clean up the CTS tracking entry
            if (ActiveExecutions.TryRemove(executionId, out var removed))
                removed.Cts.Dispose();

            SteeringNotes.TryRemove(executionId, out _);
            DeletedExecutions.TryRemove(executionId, out _);

            if (sandbox is not null)
                await sandbox.DisposeAsync();
        }
    }

    /// <summary>
    /// Recursively collects actionable descendants of a work item (children, grandchildren, etc.).
    /// Descendants already in PR or beyond are excluded from the agent context.
    /// </summary>
    private async Task CollectDescendantsAsync(string projectId, int[] childNumbers, List<Models.WorkItemDto> results)
    {
        foreach (var childNumber in childNumbers)
        {
            var child = await workItemRepository.GetByWorkItemNumberAsync(projectId, childNumber);
            if (child is null) continue;

            if (IsInPrOrBeyond(child.State))
                continue;

            results.Add(child);

            if (child.ChildWorkItemNumbers.Length > 0)
                await CollectDescendantsAsync(projectId, child.ChildWorkItemNumbers, results);
        }
    }

    private async Task CollectDirectChildrenAsync(string projectId, int[] childNumbers, List<Models.WorkItemDto> results)
    {
        foreach (var childNumber in childNumbers)
        {
            var child = await workItemRepository.GetByWorkItemNumberAsync(projectId, childNumber);
            if (child is not null)
                results.Add(child);
        }
    }

    internal static bool IsOrchestrationPrelude(AgentRole[][] pipeline)
    {
        if (pipeline.Length != OrchestrationPreludePipeline.Length)
            return false;

        for (var index = 0; index < pipeline.Length; index++)
        {
            if (!pipeline[index].SequenceEqual(OrchestrationPreludePipeline[index]))
                return false;
        }

        return true;
    }

    internal static List<Models.WorkItemDto> GetDirectActionableChildren(
        Models.WorkItemDto parent,
        IEnumerable<Models.WorkItemDto> descendants)
        => descendants
            .Where(child => child.ParentWorkItemNumber == parent.WorkItemNumber)
            .OrderBy(child => child.WorkItemNumber)
            .ToList();

    private async Task<List<Models.WorkItemDto>> MaterializeGeneratedSubFlowsAsync(
        IWorkItemRepository scopedWorkItemRepo,
        string projectId,
        Models.WorkItemDto parentWorkItem,
        GeneratedSubFlowPlan generatedPlan,
        string executionId,
        FleetDbContext scopedDb)
    {
        var created = new List<Models.WorkItemDto>();
        foreach (var subFlow in generatedPlan.SubFlows)
        {
            await CreateGeneratedSubFlowAsync(
                scopedWorkItemRepo,
                projectId,
                parentWorkItem,
                parentWorkItem.WorkItemNumber,
                subFlow,
                created);
        }

        if (created.Count > 0)
        {
            await WriteLogEntryAsync(
                scopedDb,
                projectId,
                "Planner Agent",
                "info",
                $"Generated {created.Count} sub-flow work item(s): {generatedPlan.Reason}",
                executionId: executionId);
        }

        return created;
    }

    private static async Task CreateGeneratedSubFlowAsync(
        IWorkItemRepository scopedWorkItemRepo,
        string projectId,
        Models.WorkItemDto rootWorkItem,
        int parentWorkItemNumber,
        GeneratedSubFlowSpec subFlow,
        List<Models.WorkItemDto> created)
    {
        var tags = subFlow.Tags.Count > 0 ? subFlow.Tags.ToArray() : rootWorkItem.Tags;
        var createdWorkItem = await scopedWorkItemRepo.CreateAsync(
            projectId,
            new CreateWorkItemRequest(
                Title: subFlow.Title,
                Description: subFlow.Description,
                Priority: subFlow.Priority,
                Difficulty: subFlow.Difficulty,
                State: "New",
                AssignedTo: string.Empty,
                Tags: tags,
                IsAI: true,
                ParentWorkItemNumber: parentWorkItemNumber,
                LevelId: rootWorkItem.LevelId,
                AssignmentMode: "auto",
                AssignedAgentCount: null,
                AcceptanceCriteria: subFlow.AcceptanceCriteria));

        created.Add(createdWorkItem);

        foreach (var child in subFlow.SubFlows)
        {
            await CreateGeneratedSubFlowAsync(
                scopedWorkItemRepo,
                projectId,
                rootWorkItem,
                createdWorkItem.WorkItemNumber,
                child,
                created);
        }
    }

    private static async Task TransitionExecutionToOrchestrationModeAsync(
        FleetDbContext scopedDb,
        string executionId,
        IReadOnlyList<Models.WorkItemDto> directChildWorkItems,
        IReadOnlyDictionary<AgentRole, string> completedOutputs)
    {
        var execution = await scopedDb.AgentExecutions.FindAsync(executionId);
        if (execution is null)
            return;

        var carryForwardOutputs = completedOutputs
            .Where(entry => entry.Key is AgentRole.Manager or AgentRole.Planner)
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        execution.ExecutionMode = AgentExecutionModes.Orchestration;
        execution.PullRequestUrl = null;
        execution.CurrentPhase = directChildWorkItems.Count == 0
            ? "Orchestrating sub-flows"
            : $"Orchestrating {directChildWorkItems.Count} sub-flow(s)";
        execution.Progress = Math.Max(0.2, Math.Min(execution.Progress, 0.95));
        execution.Agents = BuildAgentInfoList(OrchestrationPreludePipeline, carryForwardOutputs);
        await scopedDb.SaveChangesAsync();
    }

    private static async Task<List<string>> CollectDescendantExecutionIdsAsync(
        FleetDbContext scopedDb,
        string projectId,
        string parentExecutionId,
        CancellationToken cancellationToken)
    {
        var descendants = new List<string>();
        var frontier = new Queue<string>();
        frontier.Enqueue(parentExecutionId);

        while (frontier.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentParentId = frontier.Dequeue();
            var childIds = await scopedDb.AgentExecutions
                .AsNoTracking()
                .Where(execution =>
                    execution.ProjectId == projectId &&
                    execution.ParentExecutionId == currentParentId)
                .Select(execution => execution.Id)
                .ToListAsync(cancellationToken);

            foreach (var childId in childIds)
            {
                descendants.Add(childId);
                frontier.Enqueue(childId);
            }
        }

        return descendants;
    }

    private static string ResolveParentFlowState(IReadOnlyList<Models.WorkItemDto> directChildren)
    {
        if (directChildren.Count == 0)
            return "Resolved (AI)";

        var allResolved = directChildren.All(child =>
            string.Equals(child.State, "Resolved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(child.State, "Resolved (AI)", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(child.State, "Closed", StringComparison.OrdinalIgnoreCase));
        if (allResolved)
            return "Resolved (AI)";

        var allInPrOrResolved = directChildren.All(child => IsInPrOrBeyond(child.State));
        return allInPrOrResolved ? "In-PR (AI)" : "In Progress (AI)";
    }

    private static bool IsInPrOrBeyond(string? state)
        => !string.IsNullOrWhiteSpace(state) && InPrOrBeyondStates.Contains(state);

    /// <summary>
    /// Builds the work item context string that serves as the base input for all phases.
    /// Includes the parent work item and all included descendants with full details.
    /// </summary>
    private static string BuildWorkItemContext(Models.WorkItemDto workItem, List<Models.WorkItemDto> allDescendants)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Work Item");
        sb.AppendLine($"**#{workItem.WorkItemNumber}**: {workItem.Title}");
        sb.AppendLine($"**Priority**: {workItem.Priority}");
        sb.AppendLine($"**Difficulty**: {workItem.Difficulty}");
        sb.AppendLine($"**State**: {workItem.State}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(workItem.Description))
        {
            sb.AppendLine("## Description");
            sb.AppendLine(workItem.Description);
            sb.AppendLine();
        }

        if (workItem.Tags.Length > 0)
        {
            sb.AppendLine($"**Tags**: {string.Join(", ", workItem.Tags)}");
            sb.AppendLine();
        }

        if (allDescendants.Count > 0)
        {
            // Build a lookup from parent number → children for hierarchical rendering
            var childLookup = allDescendants
                .Where(d => d.ParentWorkItemNumber is not null)
                .GroupBy(d => d.ParentWorkItemNumber!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            sb.AppendLine("## Sub-Items");
            sb.AppendLine();
            AppendChildrenRecursive(sb, workItem.WorkItemNumber, childLookup, depth: 0);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Recursively appends child work items with indentation to show hierarchy.
    /// </summary>
    private static void AppendChildrenRecursive(
        System.Text.StringBuilder sb,
        int parentNumber,
        Dictionary<int, List<Models.WorkItemDto>> childLookup,
        int depth)
    {
        if (!childLookup.TryGetValue(parentNumber, out var children))
            return;

        var indent = new string(' ', depth * 2);
        foreach (var child in children)
        {
            sb.AppendLine($"{indent}### #{child.WorkItemNumber}: {child.Title}");
            sb.AppendLine($"{indent}- **Priority**: {child.Priority} | **Difficulty**: {child.Difficulty} | **State**: {child.State}");
            if (child.Tags.Length > 0)
                sb.AppendLine($"{indent}- **Tags**: {string.Join(", ", child.Tags)}");
            if (!string.IsNullOrWhiteSpace(child.Description))
                sb.AppendLine($"{indent}- **Description**: {child.Description}");
            sb.AppendLine();

            // Recurse into this child's children
            AppendChildrenRecursive(sb, child.WorkItemNumber, childLookup, depth + 1);
        }
    }

    /// <summary>
    /// Builds the complete user message for a given phase, including work item context
    /// and all prior phase outputs for continuity.
    /// </summary>
    private static string BuildPhaseMessage(
        AgentRole role,
        string workItemContext,
        List<(AgentRole Role, string Output)> priorOutputs,
        bool draftPullRequestReady)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(workItemContext);

        if (priorOutputs.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine("# Prior Phase Outputs");
            sb.AppendLine();

            foreach (var (priorRole, output) in priorOutputs)
            {
                sb.AppendLine($"## {priorRole} Phase Output");
                sb.AppendLine(output);
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        if (role == AgentRole.Manager)
        {
            sb.AppendLine("You are the **Manager** agent. Your job is orchestration only: setup, planning handoff, and coordination.");
            sb.AppendLine("Use only read/orchestration tools to understand scope and produce a clear handoff to the Planner.");
            sb.AppendLine("Do NOT implement code, do NOT modify files, do NOT run coding commands, and do NOT commit.");
        }
        else
        {
            sb.AppendLine($"You are the **{role}** agent. Execute your role as described in your system prompt.");
            sb.AppendLine("Use your tools to explore the repository, understand the codebase, and make the necessary changes.");
        }
        sb.AppendLine();
        if (role == AgentRole.Manager)
            sb.AppendLine("**IMPORTANT — Manager is orchestration-only. Do not call `commit_and_push`.**");
        else if (draftPullRequestReady)
            sb.AppendLine("**IMPORTANT — A draft PR is already open. Use `commit_and_push` frequently to save progress.**");
        else
            sb.AppendLine("**IMPORTANT — Use `commit_and_push` frequently to save progress. Fleet will open or update the draft PR when appropriate.**");
        sb.AppendLine();
        sb.AppendLine("**Progress Reporting Requirements:**");
        sb.AppendLine("- Call `report_progress` frequently with `percent_complete` and `summary`.");
        sb.AppendLine("- Send a progress update after every meaningful tool call while working.");
        sb.AppendLine("- Include clear milestones (for example: analysis done, implementation started, tests passing).");
        sb.AppendLine("- Send a final `report_progress` update at 100% when your phase is complete.");
        sb.AppendLine();
        sb.AppendLine("**Speed & Cost Constraints:**");
        sb.AppendLine("- Be extremely concise in your reasoning and output. No filler, no restating the problem.");
        sb.AppendLine("- Return ONLY the essential information: files changed, key decisions, errors, and instructions for the next phase.");
        sb.AppendLine("- Do NOT echo file contents you read — summarize what you learned in 1-2 sentences.");
        if (role != AgentRole.Manager)
            sb.AppendLine("- When writing code, write only the changed/new code — do not repeat unchanged sections.");
        sb.AppendLine("- **Call multiple tools at once** whenever possible. For example, read 3-5 files in a single response instead of one at a time. This runs them in parallel and is MUCH faster.");
        sb.AppendLine("- Plan your exploration: list the directory first, then read all relevant files in one batch.");
        sb.AppendLine("- Prefer search_files over reading entire files when you only need to find specific patterns.");

        return sb.ToString();
    }

    internal static IReadOnlyDictionary<AgentRole, string> BuildRetryCarryForwardOutputs(
        IReadOnlyList<AgentPhaseResult> priorPhaseResults)
    {
        var carriedOutputs = new Dictionary<AgentRole, (int PhaseOrder, string Output)>();

        foreach (var phase in priorPhaseResults
                     .Where(phase => phase.Success)
                     .OrderBy(phase => phase.PhaseOrder))
        {
            if (!Enum.TryParse<AgentRole>(phase.Role, ignoreCase: true, out var role))
                continue;

            carriedOutputs[role] = (phase.PhaseOrder, PrepareCarryForwardOutput(phase.Output));
        }

        return carriedOutputs
            .OrderBy(entry => entry.Value.PhaseOrder)
            .ToDictionary(entry => entry.Key, entry => entry.Value.Output);
    }

    internal static IReadOnlyDictionary<AgentRole, string> BuildResumeCarryForwardOutputs(
        IReadOnlyList<AgentPhaseResult> priorPhaseResults,
        IReadOnlyList<AgentInfo> persistedAgents)
    {
        var completedRoles = persistedAgents
            .Where(agent => string.Equals(agent.Status, "completed", StringComparison.OrdinalIgnoreCase))
            .Select(agent => Enum.TryParse<AgentRole>(agent.Role, ignoreCase: true, out var role) ? role : (AgentRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .ToHashSet();

        if (completedRoles.Count == 0)
            return new Dictionary<AgentRole, string>();

        var carriedOutputs = new Dictionary<AgentRole, (int PhaseOrder, string Output)>();
        foreach (var phase in priorPhaseResults
                     .Where(phase => phase.Success)
                     .OrderBy(phase => phase.PhaseOrder))
        {
            if (!Enum.TryParse<AgentRole>(phase.Role, ignoreCase: true, out var role))
                continue;

            if (!completedRoles.Contains(role))
                continue;

            carriedOutputs[role] = (phase.PhaseOrder, PrepareCarryForwardOutput(phase.Output));
        }

        return carriedOutputs
            .OrderBy(entry => entry.Value.PhaseOrder)
            .ToDictionary(entry => entry.Key, entry => entry.Value.Output);
    }

    internal static AgentRole[][] BuildPipelineFromExecutionAgents(IReadOnlyList<AgentInfo> persistedAgents)
    {
        var requestedRoles = persistedAgents
            .Select(agent => Enum.TryParse<AgentRole>(agent.Role, ignoreCase: true, out var role) ? role : (AgentRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .ToHashSet();

        if (requestedRoles.Count == 0)
            return [];

        var reconstructed = FullPipeline
            .Select(group => group.Where(requestedRoles.Contains).ToArray())
            .Where(group => group.Length > 0)
            .ToArray();

        return reconstructed.Length > 0
            ? reconstructed
            : persistedAgents
                .Select(agent => Enum.TryParse<AgentRole>(agent.Role, ignoreCase: true, out var role) ? new[] { role } : null)
                .Where(group => group is not null)
                .Select(group => group!)
                .ToArray();
    }

    internal static List<(AgentRole Role, string Output)> BuildCarryForwardPhaseOutputs(
        AgentRole[][] pipeline,
        IReadOnlyDictionary<AgentRole, string>? carryForwardOutputs)
    {
        var results = new List<(AgentRole Role, string Output)>();
        if (carryForwardOutputs is null || carryForwardOutputs.Count == 0)
            return results;

        foreach (var role in pipeline.SelectMany(group => group))
        {
            if (carryForwardOutputs.TryGetValue(role, out var output))
                results.Add((role, output));
        }

        return results;
    }

    internal static int CountCarryForwardRoles(
        AgentRole[][] pipeline,
        IReadOnlyDictionary<AgentRole, string>? carryForwardOutputs) =>
        BuildCarryForwardPhaseOutputs(pipeline, carryForwardOutputs).Count;

    private static async Task PrepareExecutionForAutomaticReviewLoopAsync(
        FleetDbContext scopedDb,
        string executionId,
        AgentRole[][] pipeline,
        IReadOnlyCollection<AgentRole> rerunRoles,
        IReadOnlyDictionary<AgentRole, string> carryForwardOutputs,
        ReviewTriageDecision reviewDecision,
        int cycleNumber)
    {
        if (IsExecutionDeleted(executionId))
            return;

        var execution = await scopedDb.AgentExecutions.FindAsync(executionId);
        if (execution is null)
            return;

        var rerunSet = rerunRoles.ToHashSet();
        var preservedRoleCount = CountCarryForwardRoles(pipeline, carryForwardOutputs);
        var totalRoleCount = pipeline.SelectMany(group => group).Count();

        execution.CurrentPhase = $"Auto-remediation: {reviewDecision.RecommendationLabel} (cycle {cycleNumber})";
        execution.Progress = totalRoleCount == 0
            ? execution.Progress
            : Math.Clamp((double)preservedRoleCount / totalRoleCount, 0, 0.99);

        foreach (var agent in execution.Agents)
        {
            if (!Enum.TryParse<AgentRole>(agent.Role, ignoreCase: true, out var role))
                continue;

            if (rerunSet.Contains(role))
            {
                agent.Status = "idle";
                agent.CurrentTask = $"Queued from review {reviewDecision.RecommendationLabel}";
                agent.Progress = 0;
                continue;
            }

            if (carryForwardOutputs.ContainsKey(role))
            {
                agent.Status = "completed";
                agent.CurrentTask = cycleNumber == 1
                    ? "Preserved from previous pass"
                    : $"Preserved through review loop {cycleNumber}";
                agent.Progress = 1.0;
            }
        }

        await scopedDb.SaveChangesAsync();
    }

    internal static List<AgentInfo> BuildAgentInfoList(
        AgentRole[][] pipeline,
        IReadOnlyDictionary<AgentRole, string>? carryForwardOutputs = null)
    {
        var carriedRoles = carryForwardOutputs?.Keys.ToHashSet() ?? [];
        return pipeline.SelectMany(g => g).Select(role => new AgentInfo
        {
            Role = role.ToString(),
            Status = carriedRoles.Contains(role) ? "completed" : "idle",
            CurrentTask = carriedRoles.Contains(role)
                ? "Carried forward from previous execution"
                : "Waiting",
            Progress = carriedRoles.Contains(role) ? 1.0 : 0,
        }).ToList();
    }

    private static string BuildRetryAwarePhaseMessage(
        string baseUserMessage,
        AgentRole role,
        IReadOnlyList<PhaseResult> priorAttempts)
    {
        if (priorAttempts.Count == 0)
            return baseUserMessage;

        var sb = new StringBuilder(baseUserMessage);
        sb.AppendLine();
        sb.AppendLine("## Retry Context");
        sb.AppendLine($"You are retrying the {role} phase after one or more failed attempts.");
        sb.AppendLine("Preserve existing repository progress and fix the failures below.");
        sb.AppendLine();

        for (var i = 0; i < priorAttempts.Count; i++)
        {
            var attempt = priorAttempts[i];
            sb.AppendLine($"### Failed Attempt {i + 1}");
            sb.AppendLine($"- Error: {attempt.Error ?? "Unknown error"}");
            if (attempt.EstimatedCompletionPercent > 0)
            {
                var retrySummary = string.IsNullOrWhiteSpace(attempt.LastProgressSummary)
                    ? "No summary captured"
                    : attempt.LastProgressSummary;
                sb.AppendLine($"- Last estimated completion: {attempt.EstimatedCompletionPercent}% ({retrySummary})");
            }
            if (!string.IsNullOrWhiteSpace(attempt.Output))
            {
                sb.AppendLine("- Output excerpt:");
                sb.AppendLine(TrimRetryOutput(attempt.Output));
            }
            sb.AppendLine();
        }

        sb.AppendLine("Focus on resolving the failure and completing this phase.");
        return sb.ToString();
    }

    internal static string BuildExecutionRetryContext(
        AgentExecution priorExecution,
        IReadOnlyList<AgentPhaseResult> priorPhaseResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Previous Execution Context");
        sb.AppendLine($"- Previous execution id: {priorExecution.Id}");
        sb.AppendLine($"- Previous status: {priorExecution.Status}");
        sb.AppendLine($"- Last recorded completion: {(int)Math.Round(Math.Clamp(priorExecution.Progress, 0, 1) * 100)}%");
        if (!string.IsNullOrWhiteSpace(priorExecution.BranchName))
            sb.AppendLine($"- Reused branch: {priorExecution.BranchName}");
        if (!string.IsNullOrWhiteSpace(priorExecution.PullRequestUrl))
            sb.AppendLine($"- Reused PR: {priorExecution.PullRequestUrl}");
        sb.AppendLine();

        if (priorPhaseResults.Count > 0)
        {
            sb.AppendLine("### Prior phase outcomes");
            foreach (var phase in priorPhaseResults.OrderBy(p => p.PhaseOrder))
            {
                var status = phase.Success ? "completed" : "failed";
                sb.Append($"- {phase.Role}: {status}, tool calls={phase.ToolCallCount}");
                if (!phase.Success && !string.IsNullOrWhiteSpace(phase.Error))
                {
                    sb.Append($", error={NormalizeAgentFailureMessage(phase.Error)}");
                }

                sb.AppendLine();
                if (phase.Success && !string.IsNullOrWhiteSpace(phase.Output))
                {
                    sb.AppendLine("  Carried output excerpt:");
                    sb.AppendLine(IndentBlock(PrepareCarryForwardOutput(phase.Output), "  "));
                }
            }
        }
        else
        {
            sb.AppendLine("### Prior phase outcomes");
            sb.AppendLine("- No prior phase outputs were recorded.");
        }

        sb.AppendLine();
        sb.AppendLine("Continue from the current repository state on the reused branch.");
        sb.AppendLine("Do not restart completed work; focus on remaining failures and unfinished parts.");
        return sb.ToString();
    }

    internal static string BuildExecutionResumeContext(
        AgentExecution pausedExecution,
        IReadOnlyList<AgentPhaseResult> priorPhaseResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Paused Execution Context");
        sb.AppendLine($"- Execution id: {pausedExecution.Id}");
        sb.AppendLine($"- Last known status: {pausedExecution.Status}");
        sb.AppendLine($"- Last recorded completion: {(int)Math.Round(Math.Clamp(pausedExecution.Progress, 0, 1) * 100)}%");
        if (!string.IsNullOrWhiteSpace(pausedExecution.CurrentPhase))
            sb.AppendLine($"- Paused while: {pausedExecution.CurrentPhase}");
        if (!string.IsNullOrWhiteSpace(pausedExecution.BranchName))
            sb.AppendLine($"- Branch: {pausedExecution.BranchName}");
        if (!string.IsNullOrWhiteSpace(pausedExecution.PullRequestUrl))
            sb.AppendLine($"- Existing PR: {pausedExecution.PullRequestUrl}");
        sb.AppendLine();

        if (priorPhaseResults.Count > 0)
        {
            sb.AppendLine("### Previously recorded phase outcomes");
            foreach (var phase in priorPhaseResults.OrderBy(result => result.PhaseOrder))
            {
                var status = phase.Success ? "completed" : "failed";
                sb.Append($"- {phase.Role}: {status}, tool calls={phase.ToolCallCount}");
                if (!phase.Success && !string.IsNullOrWhiteSpace(phase.Error))
                    sb.Append($", error={NormalizeAgentFailureMessage(phase.Error)}");

                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("### Previously recorded phase outcomes");
            sb.AppendLine("- No completed phase outputs were recorded before the pause.");
        }

        sb.AppendLine();
        sb.AppendLine("Continue this paused execution from the current repository state.");
        sb.AppendLine("Preserve completed work and finish only the remaining phases.");
        return sb.ToString();
    }

    private static string TrimRetryOutput(string output, int maxChars = 2_000)
    {
        if (string.IsNullOrWhiteSpace(output))
            return "(no output captured)";

        var normalized = output.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maxChars)
            return normalized;

        return $"{normalized[..maxChars]}\n\n[truncated]";
    }

    private static string PrepareCarryForwardOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return "(phase completed; no output captured)";

        return TrimRetryOutput(output, maxChars: 3_000);
    }

    private static string IndentBlock(string text, string prefix) =>
        string.Join('\n', text.Replace("\r\n", "\n").Split('\n').Select(line => $"{prefix}{line}"));

    private static string BuildRetryFailureTask(int estimatedCompletionPercent, string? lastProgressSummary)
    {
        if (estimatedCompletionPercent <= 0)
            return "Failed";

        var summary = string.IsNullOrWhiteSpace(lastProgressSummary)
            ? null
            : lastProgressSummary.Trim();

        return string.IsNullOrWhiteSpace(summary)
            ? $"Failed at {estimatedCompletionPercent}%"
            : $"Failed at {estimatedCompletionPercent}%: {summary}";
    }

    private static string BuildAutomaticReviewLoopLogMessage(
        ReviewTriageDecision reviewDecision,
        IReadOnlyCollection<AgentRole> rerunRoles,
        int cycleNumber)
    {
        var rerunRoleText = rerunRoles.Count == 0
            ? "no phases selected"
            : string.Join(", ", rerunRoles);

        var summaryText = string.IsNullOrWhiteSpace(reviewDecision.Summary)
            ? string.Empty
            : $" Summary: {reviewDecision.Summary}";

        return $"Automatic review remediation cycle {cycleNumber} started after {reviewDecision.RecommendationLabel}. " +
               $"Rerunning: {rerunRoleText}.{summaryText}";
    }

    private static string NormalizeAgentFailureMessage(string? rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError))
            return "Unknown error";

        var trimmed = rawError.Trim();
        var match = Regex.Match(trimmed, "^'(?<min>\\d+)' cannot be greater than (?<max>\\d+)\\.$");
        if (match.Success)
        {
            return "Internal progress-bound calculation failed while estimating completion. " +
                   "This is a Fleet runtime issue, not a repository/code error.";
        }

        return trimmed;
    }

    private static bool IsHeartbeatProgressSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return false;

        return summary.StartsWith("Waiting for model response (", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record RolePhaseExecutionResult(
        AgentRole Role,
        bool Success,
        string SummarizedOutput,
        string RawOutput,
        string? Error,
        int AttemptsUsed);

    private static class ModelKeys
    {
        public const string Haiku = "Haiku";
        public const string Sonnet = "Sonnet";
        public const string Opus = "Opus";
    }

    /// <summary>
    /// Summarizes a phase's output into a compact form using a cheap Haiku call.
    /// This dramatically reduces the context window size for downstream phases,
    /// cutting token costs proportionally to the number of phases.
    /// </summary>
    private async Task<string> SummarizePhaseOutputAsync(
        AgentRole role, string fullOutput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullOutput) || fullOutput.Length < 500)
            return fullOutput; // Not worth summarizing tiny outputs

        var summaryModel = modelCatalog.Get(ModelKeys.Haiku);
        var systemPrompt =
            "You are a concise technical summarizer. Summarize the given agent phase output into a compact form " +
            "that preserves ALL actionable information: files changed, APIs added/modified, key decisions, " +
            "errors encountered, and any instructions for subsequent phases. " +
            "Omit verbose reasoning, repeated tool calls, and file contents that were only read (not written). " +
            "Use bullet points. Be extremely concise — aim for under 500 words.";

        var messages = new List<LLMMessage>
        {
            new()
            {
                Role = "user",
                Content = $"Summarize this {role} phase output:\n\n{fullOutput}",
            }
        };

        try
        {
            var request = new LLMRequest(systemPrompt, messages, MaxTokens: 1024, ModelOverride: summaryModel);
            var response = await llmClient.CompleteAsync(request, cancellationToken);
            return response.Content ?? fullOutput;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to summarize {Role} phase output; using full output", role);
            return fullOutput;
        }
    }

    private static async Task UpdateExecutionAsync(
        FleetDbContext scopedDb, string executionId, string currentPhase, double progress)
    {
        if (IsExecutionDeleted(executionId))
            return;

        var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
        if (exec is null) return;

        exec.CurrentPhase = currentPhase;
        exec.Progress = progress;
        await scopedDb.SaveChangesAsync();
    }

    private static async Task UpdateAgentInfoAsync(
        FleetDbContext scopedDb,
        string executionId,
        string role,
        string status,
        int toolCallCount,
        double preservedProgress = 0,
        string? taskOverride = null)
    {
        if (IsExecutionDeleted(executionId))
            return;

        var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
        if (exec is null) return;

        var agent = exec.Agents.FirstOrDefault(a => a.Role == role);
        if (agent is not null)
        {
            agent.Status = status;
            if (status == "completed")
            {
                agent.CurrentTask = taskOverride ?? $"Done ({toolCallCount} tool calls)";
                agent.Progress = 1.0;
            }
            else
            {
                agent.CurrentTask = taskOverride ?? "Failed";
                agent.Progress = Math.Clamp(Math.Max(agent.Progress, preservedProgress), 0, 0.99);
            }
        }

        await scopedDb.SaveChangesAsync();
    }

    /// <summary>
    /// Marks an agent as "running" with a descriptive task before its phase executes.
    /// </summary>
    private static async Task SetAgentRunningAsync(
        FleetDbContext scopedDb,
        string executionId,
        string role,
        string taskDescription,
        double preservedProgress = 0)
    {
        if (IsExecutionDeleted(executionId))
            return;

        var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
        if (exec is null) return;

        var agent = exec.Agents.FirstOrDefault(a => a.Role == role);
        if (agent is not null)
        {
            agent.Status = "running";
            agent.CurrentTask = taskDescription;
            agent.Progress = Math.Clamp(Math.Max(agent.Progress, preservedProgress), 0, 0.99);
        }

        await scopedDb.SaveChangesAsync();
    }

    /// <summary>
    /// Writes a log entry to the database for real-time pipeline observability.
    /// </summary>
    private static async Task WriteLogEntryAsync(
        FleetDbContext scopedDb,
        string projectId,
        string agent,
        string level,
        string message,
        bool isDetailed = false,
        string? executionId = null)
    {
        if (!string.IsNullOrWhiteSpace(executionId) && IsExecutionDeleted(executionId))
            return;

        scopedDb.LogEntries.Add(new LogEntry
        {
            Time = DateTime.UtcNow.ToString("o"),
            Agent = agent,
            Level = level,
            Message = message,
            IsDetailed = isDetailed,
            ExecutionId = executionId,
            ProjectId = projectId,
        });
        await scopedDb.SaveChangesAsync();
    }

    /// <summary>
    /// Returns a human-readable description of what each agent role does.
    /// </summary>
    private static string GetPhaseTaskDescription(AgentRole role) => role switch
    {
        AgentRole.Manager => "Setting up execution and handing off to planning",
        AgentRole.Planner => "Creating implementation plan",
        AgentRole.Contracts => "Defining interfaces and contracts",
        AgentRole.Backend => "Implementing backend changes",
        AgentRole.Frontend => "Implementing frontend changes",
        AgentRole.Testing => "Writing and running tests",
        AgentRole.Styling => "Applying styles and polish",
        AgentRole.Consolidation => "Merging and resolving conflicts",
        AgentRole.Review => "Reviewing code quality",
        AgentRole.Documentation => "Generating documentation",
        _ => "Processing",
    };

    private static string BuildExecutionDocumentationTitle(AgentExecution execution)
        => $"fleet-execution-{execution.WorkItemId}-{execution.Id}.md";

    private static string BuildExecutionDocumentationMarkdown(
        AgentExecution execution,
        IReadOnlyList<AgentPhaseResult> phaseResults,
        IReadOnlyList<AgentExecution> descendantExecutions,
        IReadOnlyDictionary<string, IReadOnlyList<AgentPhaseResult>> phaseResultsByExecution)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Work Item #{execution.WorkItemId}: {execution.WorkItemTitle}");
        sb.AppendLine();
        sb.AppendLine("## Execution Summary");
        sb.AppendLine($"- Execution ID: `{execution.Id}`");
        sb.AppendLine($"- Status: **{execution.Status}**");
        sb.AppendLine($"- Started: {execution.StartedAt}");
        if (execution.CompletedAtUtc.HasValue)
            sb.AppendLine($"- Completed (UTC): {execution.CompletedAtUtc:O}");
        if (!string.IsNullOrWhiteSpace(execution.Duration))
            sb.AppendLine($"- Duration: {execution.Duration}");
        if (!string.IsNullOrWhiteSpace(execution.CurrentPhase))
            sb.AppendLine($"- Final phase: {execution.CurrentPhase}");
        if (!string.IsNullOrWhiteSpace(execution.BranchName))
            sb.AppendLine($"- Branch: `{execution.BranchName}`");
        if (!string.IsNullOrWhiteSpace(execution.PullRequestUrl))
            sb.AppendLine($"- Pull request: {execution.PullRequestUrl}");
        var diffUrl = BuildDiffUrl(execution.PullRequestUrl);
        if (!string.IsNullOrWhiteSpace(diffUrl))
            sb.AppendLine($"- Diff: {diffUrl}");

        var documentationOutput = phaseResults
            .Where(p => p.Success && string.Equals(p.Role, AgentRole.Documentation.ToString(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.PhaseOrder)
            .Select(p => p.Output)
            .FirstOrDefault(output => !string.IsNullOrWhiteSpace(output));

        var normalizedDocumentationOutput = string.IsNullOrWhiteSpace(documentationOutput)
            ? null
            : ExecutionDocumentationFormatter.NormalizeMarkdown(TrimOutputForDocs(documentationOutput));

        if (!string.IsNullOrWhiteSpace(normalizedDocumentationOutput))
        {
            sb.AppendLine();
            sb.AppendLine("## Generated Documentation");
            sb.AppendLine();
            sb.AppendLine(normalizedDocumentationOutput);
        }

        if (phaseResults.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Phase Timeline");
            sb.AppendLine();
            sb.AppendLine("| # | Role | Status | Tool Calls | Duration |");
            sb.AppendLine("|---|------|--------|------------|----------|");
            foreach (var phase in phaseResults)
            {
                var status = phase.Success ? "completed" : "failed";
                var duration = phase.CompletedAt.HasValue
                    ? $"{(phase.CompletedAt.Value - phase.StartedAt).TotalSeconds:F0}s"
                    : "-";
                sb.AppendLine($"| {phase.PhaseOrder + 1} | {phase.Role} | {status} | {phase.ToolCallCount} | {duration} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Phase Outputs");
            foreach (var phase in phaseResults)
            {
                sb.AppendLine();
                sb.AppendLine($"### {phase.PhaseOrder + 1}. {phase.Role}");
                if (!phase.Success && !string.IsNullOrWhiteSpace(phase.Error))
                    sb.AppendLine($"- Error: {phase.Error}");
                sb.AppendLine();
                var trimmedOutput = TrimOutputForDocs(phase.Output);
                var formattedOutput = ExecutionDocumentationFormatter.FormatPhaseOutput(trimmedOutput);
                var normalizedPhaseOutput = ExecutionDocumentationFormatter.NormalizeMarkdown(trimmedOutput);
                if (!string.IsNullOrWhiteSpace(normalizedDocumentationOutput) &&
                    string.Equals(phase.Role, AgentRole.Documentation.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(normalizedPhaseOutput, normalizedDocumentationOutput, StringComparison.Ordinal))
                {
                    sb.AppendLine("_Rendered above in Generated Documentation._");
                    continue;
                }

                sb.AppendLine(formattedOutput);
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("## Notes");
            sb.AppendLine("No phase outputs were recorded for this execution.");
        }

        if (descendantExecutions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Sub-Flows");
            sb.AppendLine();

            var descendantsByParentExecutionId = descendantExecutions
                .Where(child => !string.IsNullOrWhiteSpace(child.ParentExecutionId))
                .GroupBy(child => child.ParentExecutionId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(child => child.StartedAtUtc).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            AppendSubFlowDocumentationSections(
                sb,
                execution.Id,
                descendantsByParentExecutionId,
                phaseResultsByExecution,
                depth: 0);
        }

        return sb.ToString();
    }

    private static void AppendSubFlowDocumentationSections(
        StringBuilder sb,
        string parentExecutionId,
        IReadOnlyDictionary<string, List<AgentExecution>> descendantsByParentExecutionId,
        IReadOnlyDictionary<string, IReadOnlyList<AgentPhaseResult>> phaseResultsByExecution,
        int depth)
    {
        if (!descendantsByParentExecutionId.TryGetValue(parentExecutionId, out var children))
            return;

        foreach (var child in children)
        {
            var headingLevel = new string('#', Math.Min(6, depth + 3));
            sb.AppendLine($"{headingLevel} Work Item #{child.WorkItemId}: {child.WorkItemTitle}");
            sb.AppendLine();
            sb.AppendLine($"- Execution ID: `{child.Id}`");
            sb.AppendLine($"- Mode: `{child.ExecutionMode}`");
            sb.AppendLine($"- Status: **{child.Status}**");
            if (!string.IsNullOrWhiteSpace(child.BranchName))
                sb.AppendLine($"- Branch: `{child.BranchName}`");
            if (!string.IsNullOrWhiteSpace(child.PullRequestUrl))
                sb.AppendLine($"- Pull request: {child.PullRequestUrl}");
            var childDiffUrl = BuildDiffUrl(child.PullRequestUrl);
            if (!string.IsNullOrWhiteSpace(childDiffUrl))
                sb.AppendLine($"- Diff: {childDiffUrl}");

            if (phaseResultsByExecution.TryGetValue(child.Id, out var childPhases) && childPhases.Count > 0)
            {
                var latestDocumentationOutput = childPhases
                    .Where(phase =>
                        phase.Success &&
                        string.Equals(phase.Role, AgentRole.Documentation.ToString(), StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(phase => phase.PhaseOrder)
                    .Select(phase => phase.Output)
                    .FirstOrDefault(output => !string.IsNullOrWhiteSpace(output));

                if (!string.IsNullOrWhiteSpace(latestDocumentationOutput))
                {
                    sb.AppendLine();
                    sb.AppendLine(ExecutionDocumentationFormatter.NormalizeMarkdown(TrimOutputForDocs(latestDocumentationOutput)));
                }
                else
                {
                    var latestCompletedPhase = childPhases
                        .Where(phase => phase.Success)
                        .OrderByDescending(phase => phase.PhaseOrder)
                        .FirstOrDefault();
                    if (latestCompletedPhase is not null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"Latest completed phase: **{latestCompletedPhase.Role}**");
                    }
                }
            }

            sb.AppendLine();
            AppendSubFlowDocumentationSections(
                sb,
                child.Id,
                descendantsByParentExecutionId,
                phaseResultsByExecution,
                depth + 1);
        }
    }

    private static string TrimOutputForDocs(string output, int maxChars = 6000)
    {
        if (string.IsNullOrWhiteSpace(output))
            return "(no output captured)";

        var normalized = output.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maxChars)
            return normalized;

        return $"{normalized[..maxChars]}\n\n[truncated]";
    }

    private static string? BuildDiffUrl(string? pullRequestUrl)
    {
        if (string.IsNullOrWhiteSpace(pullRequestUrl))
            return null;

        return $"{pullRequestUrl.TrimEnd('/')}/files";
    }

    private static async Task FinalizeExecutionAsync(
        FleetDbContext scopedDb, string executionId, string status,
        string? prUrl = null, string? errorMessage = null)
    {
        if (IsExecutionDeleted(executionId))
            return;

        var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
        if (exec is null) return;

        exec.Status = status;
        exec.CompletedAtUtc = DateTime.UtcNow;
        exec.Progress = status == "completed" ? 1.0 : exec.Progress;
        exec.CurrentPhase = status switch
        {
            "completed" => "Done",
            "cancelled" => "Cancelled",
            "paused" => "Paused",
            _ => "Failed",
        };
        exec.PullRequestUrl = prUrl;

        if (exec.StartedAtUtc.HasValue)
        {
            var elapsed = DateTime.UtcNow - exec.StartedAtUtc.Value;
            exec.Duration = elapsed.TotalMinutes < 1
                ? $"{elapsed.TotalSeconds:F0}s"
                : $"{elapsed.TotalMinutes:F1}m";
        }

        if (errorMessage is not null && exec.Duration == string.Empty)
        {
            exec.Duration = errorMessage.Length > 500 ? errorMessage[..500] : errorMessage;
        }

        if (status is "failed" or "cancelled")
        {
            var terminalTask = status == "failed" ? "Failed" : "Cancelled";
            foreach (var agent in exec.Agents)
            {
                agent.Status = status;
                agent.CurrentTask = terminalTask;
                agent.Progress = 0;
            }
        }

        await scopedDb.SaveChangesAsync();
    }

    private static bool CanDeleteExecutionStatus(string? status)
        => !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutionDeleted(string executionId)
        => DeletedExecutions.ContainsKey(executionId);

    private static void ThrowIfExecutionDeleted(string executionId)
    {
        if (IsExecutionDeleted(executionId))
            throw new OperationCanceledException($"Execution {executionId} was deleted.");
    }

    /// <summary>
    /// Opens a draft pull request on GitHub at the start of the pipeline.
    /// Creates an initial marker commit and pushes the branch so the PR can be opened.
    /// Agents push subsequent commits throughout development — the PR updates automatically.
    /// </summary>
    private async Task<(string? Url, int Number)> OpenDraftPullRequestAsync(
        IRepoSandbox sandbox, string accessToken, string repoFullName,
        WorkItemDto workItem, string commitAuthorName, string commitAuthorEmail,
        string pullRequestTargetBranch,
        FleetDbContext scopedDb, string executionId,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Create an initial marker commit so the branch has something to push.
            sandbox.WriteFile(".fleet",
                $"Fleet execution for work item #{workItem.WorkItemNumber}: {workItem.Title}\n" +
                $"Started: {DateTime.UtcNow:O}\nBranch: {sandbox.BranchName}\n" +
                $"Target branch: {pullRequestTargetBranch}\n");

            await sandbox.CommitAndPushAsync(
                accessToken,
                $"fleet: start work on #{workItem.WorkItemNumber} - {workItem.Title}",
                authorName: commitAuthorName,
                authorEmail: commitAuthorEmail,
                cancellationToken);

            // 2. Resolve a collision-safe PR title.
            var client = httpClientFactory.CreateClient("GitHub");
            var baseBranch = pullRequestTargetBranch;

            var execution = await scopedDb.AgentExecutions.FindAsync(executionId);
            var basePrTitle = execution?.PullRequestTitle ?? BuildPullRequestTitle(workItem);
            var resolvedPrTitle = await ResolveUniquePullRequestTitleAsync(
                client, accessToken, repoFullName, basePrTitle, cancellationToken);

            if (execution is not null)
            {
                execution.PullRequestTitle = resolvedPrTitle;
                await scopedDb.SaveChangesAsync(cancellationToken);
            }

            // 3. Open a draft PR. If we hit a title-collision race, resolve once and retry.
            var openResult = await CreateDraftPullRequestAsync(
                client, accessToken, repoFullName, sandbox.BranchName, baseBranch, workItem, resolvedPrTitle, cancellationToken);

            if (!openResult.Success && IsPrTitleCollision(openResult.StatusCode, openResult.ResponseBody))
            {
                var retriedTitle = await ResolveUniquePullRequestTitleAsync(
                    client, accessToken, repoFullName, basePrTitle, cancellationToken);

                if (!string.Equals(retriedTitle, resolvedPrTitle, StringComparison.Ordinal))
                {
                    if (execution is not null)
                    {
                        execution.PullRequestTitle = retriedTitle;
                        await scopedDb.SaveChangesAsync(cancellationToken);
                    }

                    openResult = await CreateDraftPullRequestAsync(
                        client, accessToken, repoFullName, sandbox.BranchName, baseBranch, workItem, retriedTitle, cancellationToken);
                }
            }

            if (!openResult.Success)
            {
                var existingPullRequest = await FindOpenPullRequestByHeadBranchAsync(
                    accessToken,
                    repoFullName,
                    sandbox.BranchName,
                    cancellationToken);

                if (existingPullRequest.Number > 0 && !string.IsNullOrWhiteSpace(existingPullRequest.Url))
                {
                    logger.LogWarning(
                        "Draft PR creation returned {Status}; reusing existing PR #{PrNumber}: {PrUrl}",
                        openResult.StatusCode,
                        existingPullRequest.Number,
                        existingPullRequest.Url);

                    if (execution is not null)
                    {
                        execution.PullRequestUrl = existingPullRequest.Url;
                        await scopedDb.SaveChangesAsync(cancellationToken);
                    }

                    return (existingPullRequest.Url, existingPullRequest.Number);
                }

                var errorMessage = TryExtractGitHubApiErrorMessage(openResult.ResponseBody)
                    ?? $"GitHub returned {(int)openResult.StatusCode} ({openResult.StatusCode}).";

                throw new InvalidOperationException(
                    $"Fleet could not open a GitHub pull request for branch '{sandbox.BranchName}': {errorMessage}");
            }

            var prResult = JsonSerializer.Deserialize<JsonElement>(openResult.ResponseBody);
            var prUrl = prResult.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
            var prNumber = prResult.TryGetProperty("number", out var numProp) ? numProp.GetInt32() : 0;

            logger.LogInformation("Opened draft PR #{PrNumber}: {PrUrl}", prNumber, prUrl);

            // 4. Persist URL/title on the execution record.
            if (execution is not null)
            {
                execution.PullRequestUrl = prUrl;
                await scopedDb.SaveChangesAsync(cancellationToken);
            }

            return (prUrl, prNumber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open draft PR for branch {Branch}", sandbox.BranchName);
            throw;
        }
    }

    private async Task<DraftPullRequestCreateResult> CreateDraftPullRequestAsync(
        HttpClient client,
        string accessToken,
        string repoFullName,
        string headBranch,
        string baseBranch,
        WorkItemDto workItem,
        string prTitle,
        CancellationToken cancellationToken)
    {
        var prPayload = JsonSerializer.Serialize(new
        {
            title = prTitle,
            body = $"Links F#{workItem.WorkItemNumber}: {workItem.Title}\n\n" +
                   $"fixes F#{workItem.WorkItemNumber} when merged.\n\n" +
                   "_This PR was opened automatically by Fleet. Agents are actively pushing changes._",
            head = headBranch,
            @base = baseBranch,
            draft = true,
        });

        using var prRequest = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.github.com/repos/{repoFullName}/pulls");
        prRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        prRequest.Headers.UserAgent.ParseAdd("Fleet/1.0");
        prRequest.Content = new StringContent(prPayload, Encoding.UTF8, "application/json");

        using var prResponse = await client.SendAsync(prRequest, cancellationToken);
        var prResponseBody = await prResponse.Content.ReadAsStringAsync(cancellationToken);

        return new DraftPullRequestCreateResult(
            prResponse.IsSuccessStatusCode,
            prResponse.StatusCode,
            prResponseBody);
    }

    private async Task<string> ResolvePullRequestTargetBranchAsync(
        string accessToken,
        string repoFullName,
        string? requestedBranch,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("GitHub");
        var normalizedRequestedBranch = requestedBranch?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedRequestedBranch))
        {
            var branchExists = await BranchExistsAsync(
                client,
                accessToken,
                repoFullName,
                normalizedRequestedBranch,
                cancellationToken);

            if (!branchExists)
            {
                throw new InvalidOperationException(
                    $"Target branch '{normalizedRequestedBranch}' was not found in {repoFullName}.");
            }

            return normalizedRequestedBranch;
        }

        var repoJson = await GitHubGetAsync(
            client,
            accessToken,
            $"https://api.github.com/repos/{repoFullName}",
            cancellationToken);

        var defaultBranch = repoJson?.TryGetProperty("default_branch", out var dbProp) == true
            ? dbProp.GetString()
            : null;

        return string.IsNullOrWhiteSpace(defaultBranch) ? "main" : defaultBranch;
    }

    private async Task<string> ResolveUniqueBranchNameAsync(
        string accessToken,
        string repoFullName,
        string requestedBranchName,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("GitHub");

        for (var copyIndex = 0; copyIndex < 100; copyIndex++)
        {
            var candidate = copyIndex == 0
                ? requestedBranchName
                : BuildBranchCopyName(requestedBranchName, copyIndex);

            var exists = await BranchExistsAsync(client, accessToken, repoFullName, candidate, cancellationToken);
            if (!exists)
                return candidate;
        }

        throw new InvalidOperationException("Unable to allocate a unique branch name after 100 attempts.");
    }

    private static async Task<bool> BranchExistsAsync(
        HttpClient client,
        string accessToken,
        string repoFullName,
        string branchName,
        CancellationToken cancellationToken)
    {
        var encodedBranch = Uri.EscapeDataString(branchName);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{repoFullName}/git/ref/heads/{encodedBranch}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        return response.IsSuccessStatusCode;
    }

    private async Task<string> ResolveUniquePullRequestTitleAsync(
        HttpClient client,
        string accessToken,
        string repoFullName,
        string baseTitle,
        CancellationToken cancellationToken)
    {
        var openTitles = await FetchOpenPullRequestTitlesAsync(client, accessToken, repoFullName, cancellationToken);
        if (!openTitles.Contains(baseTitle))
            return baseTitle;

        for (var copyIndex = 1; copyIndex < 100; copyIndex++)
        {
            var candidate = BuildPullRequestCopyTitle(baseTitle, copyIndex);
            if (!openTitles.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException("Unable to allocate a unique pull request title after 100 attempts.");
    }

    private static async Task<HashSet<string>> FetchOpenPullRequestTitlesAsync(
        HttpClient client,
        string accessToken,
        string repoFullName,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{repoFullName}/pulls?state=open&per_page=100");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var root = JsonSerializer.Deserialize<JsonElement>(body);
        if (root.ValueKind != JsonValueKind.Array)
            return titles;

        foreach (var item in root.EnumerateArray())
        {
            if (item.TryGetProperty("title", out var titleProp))
            {
                var title = titleProp.GetString();
                if (!string.IsNullOrWhiteSpace(title))
                    titles.Add(title);
            }
        }

        return titles;
    }

    private static bool IsPrTitleCollision(HttpStatusCode statusCode, string responseBody)
    {
        if (statusCode != HttpStatusCode.UnprocessableEntity)
            return false;

        return responseBody.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
               responseBody.Contains("\"title\"", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractGitHubApiErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            var payload = JsonSerializer.Deserialize<JsonElement>(responseBody);
            if (payload.TryGetProperty("message", out var messageProp))
            {
                var message = messageProp.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                    return message.Trim();
            }
        }
        catch (JsonException)
        {
            // Fall back to the raw body below.
        }

        var trimmed = responseBody.Trim();
        return trimmed.Length <= 400 ? trimmed : trimmed[..400];
    }

    private static string BuildPullRequestTitle(WorkItemDto workItem)
        => $"[Fleet] #{workItem.WorkItemNumber}: {workItem.Title}";

    private static string BuildBranchCopyName(string baseBranchName, int copyIndex)
        => copyIndex <= 1 ? $"{baseBranchName}-copy" : $"{baseBranchName}-copy-{copyIndex}";

    private static string BuildPullRequestCopyTitle(string baseTitle, int copyIndex)
        => copyIndex <= 1 ? $"{baseTitle} (copy)" : $"{baseTitle} (copy {copyIndex})";

    private static int? TryParsePullRequestNumber(string? pullRequestUrl)
    {
        if (string.IsNullOrWhiteSpace(pullRequestUrl))
            return null;

        if (!Uri.TryCreate(pullRequestUrl, UriKind.Absolute, out var uri))
            return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4)
            return null;

        // Expected format: /{owner}/{repo}/pull/{number}
        if (!string.Equals(segments[2], "pull", StringComparison.OrdinalIgnoreCase))
            return null;

        return int.TryParse(segments[3], out var prNumber) ? prNumber : null;
    }

    private async Task<(int Number, string? Url)> FindOpenPullRequestByHeadBranchAsync(
        string accessToken,
        string repoFullName,
        string headBranch,
        CancellationToken cancellationToken)
    {
        try
        {
            var owner = repoFullName.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(headBranch))
                return (0, null);

            var encodedHead = Uri.EscapeDataString($"{owner}:{headBranch}");
            var client = httpClientFactory.CreateClient("GitHub");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{repoFullName}/pulls?state=open&head={encodedHead}&per_page=10");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("Fleet/1.0");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to find open PR for branch {Branch}: status={StatusCode}",
                    headBranch,
                    response.StatusCode);
                return (0, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var root = JsonSerializer.Deserialize<JsonElement>(body);
            if (root.ValueKind != JsonValueKind.Array)
                return (0, null);

            foreach (var pull in root.EnumerateArray())
            {
                if (!pull.TryGetProperty("number", out var numberProp) || !numberProp.TryGetInt32(out var number))
                    continue;

                if (number <= 0)
                    continue;

                var url = pull.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
                return (number, url);
            }

            return (0, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve open PR for branch {Branch}", headBranch);
            return (0, null);
        }
    }

    private sealed record DraftPullRequestCreateResult(
        bool Success,
        HttpStatusCode StatusCode,
        string ResponseBody);

    private sealed record PullRequestLifecycle(
        bool IsOpen,
        bool IsDraft,
        bool IsMerged);

    /// <summary>
    /// Marks a draft pull request as ready for review.
    /// </summary>
    private async Task MarkPullRequestReadyAsync(
        string accessToken, string repoFullName, int prNumber, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("GitHub");
            var payload = JsonSerializer.Serialize(new { draft = false });

            using var request = new HttpRequestMessage(HttpMethod.Patch,
                $"https://api.github.com/repos/{repoFullName}/pulls/{prNumber}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("Fleet/1.0");
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
                logger.LogInformation("Marked PR #{PrNumber} as ready for review", prNumber);
            else
                logger.LogWarning("Failed to mark PR #{PrNumber} as ready: {Status}",
                    prNumber, response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark PR #{PrNumber} as ready for review (non-fatal)", prNumber);
        }
    }

    private static string ResolveStateFromPullRequestLifecycle(bool isAi, PullRequestLifecycle? lifecycle)
    {
        // User-requested mapping:
        // draft -> in-progress, open -> in-pr, closed -> new, merged -> resolved
        if (lifecycle?.IsMerged == true)
            return isAi ? "Resolved (AI)" : "Resolved";

        if (lifecycle?.IsOpen == true && lifecycle.IsDraft == false)
            return isAi ? "In-PR (AI)" : "In-PR";

        if (lifecycle?.IsOpen == true && lifecycle.IsDraft == true)
            return isAi ? "In Progress (AI)" : "In Progress";

        if (lifecycle is null)
            return isAi ? "In Progress (AI)" : "In Progress";

        return "New";
    }

    private static string ResolveObservedPullRequestState(PullRequestLifecycle? lifecycle)
    {
        if (lifecycle?.IsMerged == true)
            return "merged";

        if (lifecycle?.IsOpen == true && lifecycle.IsDraft == true)
            return "draft";

        if (lifecycle?.IsOpen == true)
            return "open";

        return "closed";
    }

    private async Task<PullRequestLifecycle?> GetPullRequestLifecycleAsync(
        string accessToken,
        string repoFullName,
        int prNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("GitHub");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{repoFullName}/pulls/{prNumber}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("Fleet/1.0");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to read PR lifecycle for #{PrNumber}: status={StatusCode}",
                    prNumber,
                    response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonSerializer.Deserialize<JsonElement>(body);
            var state = json.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : null;
            var isOpen = string.Equals(state, "open", StringComparison.OrdinalIgnoreCase);
            var isDraft = json.TryGetProperty("draft", out var draftProp) &&
                          draftProp.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                          draftProp.GetBoolean();
            var isMerged = json.TryGetProperty("merged_at", out var mergedAtProp) &&
                           mergedAtProp.ValueKind != JsonValueKind.Null;

            return new PullRequestLifecycle(isOpen, isDraft, isMerged);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve PR lifecycle for #{PrNumber}", prNumber);
            return null;
        }
    }

    /// <summary>
    /// Sends a GET request to the GitHub API and returns the parsed JSON response.
    /// </summary>
    private static async Task<JsonElement?> GitHubGetAsync(
        HttpClient client, string token, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private async Task<string> ResolveRequiredRepoAccessTokenAsync(
        int userId,
        string repoFullName,
        CancellationToken cancellationToken)
        => await ResolveRequiredRepoAccessTokenAsync(
            connectionService,
            userId,
            repoFullName,
            cancellationToken);

    private static async Task<string> ResolveRequiredRepoAccessTokenAsync(
        IConnectionService connectionService,
        int userId,
        string repoFullName,
        CancellationToken cancellationToken)
    {
        return await connectionService.ResolveGitHubAccessTokenForRepoAsync(
                userId,
                repoFullName,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"No linked GitHub account can access '{repoFullName}'. Link/re-link a GitHub account with repository access.");
    }

    private static string BuildBranchName(string? pattern, int workItemNumber, string title)
    {
        var branchPattern = string.IsNullOrWhiteSpace(pattern)
            ? "fleet/{workItemNumber}-{slug}"
            : pattern.Trim();

        var slug = Slugify(title);
        var raw = branchPattern
            .Replace("{workItemNumber}", workItemNumber.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{slug}", slug, StringComparison.OrdinalIgnoreCase);

        return string.IsNullOrWhiteSpace(raw)
            ? $"fleet/{workItemNumber}-{slug}"
            : raw;
    }

    private static (string Name, string Email) ResolveCommitAuthor(
        string? mode,
        string? customName,
        string? customEmail)
    {
        if (string.Equals(mode, "custom", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(customName) &&
            !string.IsNullOrWhiteSpace(customEmail))
        {
            return (customName.Trim(), customEmail.Trim());
        }

        return ("Fleet Agent", "agent@fleet.dev");
    }

    private static string Slugify(string title)
    {
        var slug = title.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('\t', '-');

        // Keep only alphanumeric and hyphens
        var chars = slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray();
        slug = new string(chars);

        // Collapse multiple hyphens and trim
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        return slug.Trim('-').Length > 50 ? slug[..50].TrimEnd('-') : slug.Trim('-');
    }
}






