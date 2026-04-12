using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fleet.Server.Auth;
using Fleet.Server.Connections;
using Fleet.Server.Copilot;
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
    private const double IncompleteProgressCeiling = 0.9995;
    private static readonly HashSet<string> InPrOrBeyondStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "In-PR",
        "In-PR (AI)",
        "Resolved",
        "Resolved (AI)",
        "Closed",
    };
    private static readonly HashSet<string> ProtectedBranchNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "main",
        "master",
        "develop",
        "development",
        "release",
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

    /// <summary>
    /// Coordinator pipeline: research-first approach that gathers context before
    /// planning and implementation. Ideal for complex or unfamiliar tasks.
    /// Flow: Manager → Research → Planner → [Implementation] → [Review, Documentation]
    /// </summary>
    private static readonly AgentRole[][] CoordinatorPipeline =
    [
        [AgentRole.Manager],
        [AgentRole.Research],
        [AgentRole.Planner],
        [AgentRole.Contracts],
        [AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling],
        [AgentRole.Consolidation],
        [AgentRole.Review, AgentRole.Documentation],
    ];
    private static readonly Dictionary<AgentRole, int> LimitedPipelineRolePriority = new()
    {
        [AgentRole.Backend] = 1,
        [AgentRole.Frontend] = 2,
        [AgentRole.Contracts] = 3,
        [AgentRole.Testing] = 4,
        [AgentRole.Styling] = 5,
        [AgentRole.Research] = 6,
        [AgentRole.Consolidation] = 7,
        [AgentRole.Review] = 8,
        [AgentRole.Documentation] = 9,
    };

    internal const int MaxSubFlowChildrenPerExecution = 5;
    internal const int MaxSubFlowExecutionDepth = 4;
    private const int MaxParallelSubFlows = MaxSubFlowChildrenPerExecution;
    private const string StagedChatAssetDirectory = ".fleet-assets";
    private static readonly Regex ChatAttachmentReferenceRegex = new(
        @"/api/chat/attachments/(?<id>[A-Za-z0-9\-]+)/content",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WorkItemAttachmentReferenceRegex = new(
        @"/api/projects/[^/\s]+/work-items/\d+/attachments/(?<id>[A-Za-z0-9\-]+)/content",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool IsRecoverableInterruptedExecutionStatus(string? status)
        => string.Equals(status, "running", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase);

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
            var model = modelCatalog.Get(ModelKeys.Fast);
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

        // Group 2b: Research (if selected — runs between Planner and Contracts in coordinator mode)
        if (roles.Contains(AgentRole.Research))
            pipeline.Add([AgentRole.Research]);

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
        AgentRole.Research => 8192,
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

    private static AgentExecutionDto CreateInitializationExecutionDto(AgentExecution execution)
        => new(
            execution.Id,
            execution.WorkItemId,
            execution.WorkItemTitle,
            string.IsNullOrWhiteSpace(execution.ExecutionMode) ? AgentExecutionModes.Standard : execution.ExecutionMode,
            execution.Status,
            [.. execution.Agents.Select(agent => new AgentInfoDto(
                agent.Role.ToString(),
                agent.Status,
                agent.CurrentTask,
                agent.Progress))],
            execution.StartedAt,
            string.IsNullOrWhiteSpace(execution.Duration) ? "just now" : execution.Duration,
            execution.Progress,
            execution.BranchName,
            execution.PullRequestUrl,
            execution.CurrentPhase,
            ParentExecutionId: execution.ParentExecutionId,
            SubFlows: []);

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
            var executionDepth = await ResolveExecutionDepthAsync(db, parentExecutionId, cancellationToken);

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
            var shouldOrchestrateDirectChildren = ShouldOrchestrateExistingSubFlows(
                workItem,
                directChildWorkItems,
                childWorkItems,
                executionDepth);
            var executionMode = shouldOrchestrateDirectChildren
                ? AgentExecutionModes.Orchestration
                : AgentExecutionModes.Standard;
            var selectedModelKey = ModelKeys.Fast;
            var pipeline = ResolveDefaultPipeline(executionMode);
            pipeline = ApplyAssignedAgentLimit(pipeline, workItem.AssignmentMode, workItem.AssignedAgentCount);
            if (directChildWorkItems.Count > 0 && !shouldOrchestrateDirectChildren)
            {
                logger.LogInformation(
                    "Execution: keeping work item #{WorkItemNumber} in a single run despite {DirectChildCount} direct child work items (depth={ExecutionDepth}, difficulty={Difficulty}).",
                    workItem.WorkItemNumber,
                    directChildWorkItems.Count,
                    executionDepth,
                    workItem.Difficulty);
            }
            logger.LogInformation(
                "Execution: using default pipeline with {PhaseCount} agents, mode={ExecutionMode}, assignmentMode={AssignmentMode}",
                pipeline.SelectMany(g => g).Count(),
                executionMode,
                workItem.AssignmentMode ?? "auto");

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
                if (string.IsNullOrWhiteSpace(parentExecutionId))
                {
                    await CleanupStaleTopLevelBranchesAsync(
                        projectId,
                        workItemNumber,
                        accessToken,
                        repoFullName,
                        pullRequestTargetBranch,
                        cancellationToken);
                }
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
                    IncompleteProgressCeiling);
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
                new
                {
                    projectId,
                    executionId,
                    execution = CreateInitializationExecutionDto(execution),
                },
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
                                   $"(status: {retryPlan.SourceStatus ?? "unknown"}, prior progress: {FormatProgressPercent(retryPlan.PriorProgressEstimate * 100)}%)";
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

            var latestExecutionLog = await db.LogEntries
                .AsNoTracking()
                .Where(log => log.ProjectId == projectId && log.ExecutionId == executionId)
                .OrderByDescending(log => log.Time)
                .ThenByDescending(log => log.Id)
                .Select(log => new LogEntryDto(
                    log.Time,
                    log.Agent,
                    log.Level,
                    log.Message,
                    log.IsDetailed,
                    log.ExecutionId))
                .FirstOrDefaultAsync(cancellationToken);

            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.LogsUpdated,
                new { projectId, executionId, logEntry = latestExecutionLog },
                cancellationToken);

            // 11. Fire-and-forget the pipeline on a background thread
            var cts = new CancellationTokenSource();
            ActiveExecutions[executionId] = (cts, "cancelled");
            SteeringNotes.TryAdd(executionId, new ConcurrentQueue<string>());
            _ = Task.Run(() => RunPipelineAsync(
                executionId, projectId, workItem, childWorkItems, repoFullName,
                branchName, commitAuthorName, commitAuthorEmail,
                userId, selectedModelKey, pipeline, ResolveMaxConcurrentAgentsPerTask(tierPolicy.MaxConcurrentAgentsPerTask, workItem.AssignmentMode, workItem.AssignedAgentCount),
                pullRequestTargetBranch, retryPlan, reusePullRequestNumber, !skipQuotaCharge, parentExecutionId, cts.Token), CancellationToken.None);

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

    public Task<bool> ResumeExecutionAsync(
        string projectId,
        string executionId,
        int userId,
        CancellationToken cancellationToken = default)
        => ResumeOrRecoverPersistedExecutionAsync(
            projectId,
            executionId,
            requestedUserId: userId,
            recoveringInterruptedExecution: false,
            publishLifecycleEvents: false,
            cancellationToken);

    public Task<bool> RecoverExecutionAsync(
        string projectId,
        string executionId,
        CancellationToken cancellationToken = default)
        => ResumeOrRecoverPersistedExecutionAsync(
            projectId,
            executionId,
            requestedUserId: null,
            recoveringInterruptedExecution: true,
            publishLifecycleEvents: true,
            cancellationToken);

    public async Task<int> RecoverInterruptedExecutionsAsync(CancellationToken cancellationToken = default)
    {
        var recoverableExecutions = await db.AgentExecutions
            .AsNoTracking()
            .Where(execution =>
                execution.ParentExecutionId == null &&
                (execution.Status == "running" || execution.Status == "queued"))
            .OrderBy(execution => execution.StartedAtUtc)
            .Select(execution => new
            {
                execution.ProjectId,
                execution.Id,
            })
            .ToListAsync(cancellationToken);

        var recoveredCount = 0;
        foreach (var execution in recoverableExecutions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var wasAlreadyActive = ActiveExecutions.ContainsKey(execution.Id);
            try
            {
                var recovered = await RecoverExecutionAsync(
                    execution.ProjectId,
                    execution.Id,
                    cancellationToken);
                if (recovered && !wasAlreadyActive)
                    recoveredCount++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Execution {ExecutionId}: failed to recover interrupted execution during startup",
                    execution.Id);
            }
        }

        return recoveredCount;
    }

    private async Task<bool> ResumeOrRecoverPersistedExecutionAsync(
        string projectId,
        string executionId,
        int? requestedUserId,
        bool recoveringInterruptedExecution,
        bool publishLifecycleEvents,
        CancellationToken cancellationToken)
    {
        var persistedExecution = await db.AgentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                execution => execution.Id == executionId && execution.ProjectId == projectId,
                cancellationToken);

        if (persistedExecution is null)
            return false;

        if (recoveringInterruptedExecution)
        {
            if (!IsRecoverableInterruptedExecutionStatus(persistedExecution.Status))
                return false;
        }
        else if (!string.Equals(persistedExecution.Status, "paused", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only paused executions can be resumed.");
        }

        if (ActiveExecutions.ContainsKey(executionId))
        {
            if (recoveringInterruptedExecution)
                return true;

            throw new InvalidOperationException("Execution is still finishing its pause. Try resuming again in a moment.");
        }

        var userId = requestedUserId;
        if (userId is null)
        {
            if (!int.TryParse(persistedExecution.UserId, out var parsedUserId))
            {
                throw new InvalidOperationException(
                    $"Execution {executionId} has invalid user id '{persistedExecution.UserId}' and cannot be recovered.");
            }

            userId = parsedUserId;
        }

        var workItem = await workItemRepository.GetByWorkItemNumberAsync(projectId, persistedExecution.WorkItemId)
            ?? throw new InvalidOperationException(
                $"Work item #{persistedExecution.WorkItemId} for execution {executionId} could not be found.");

        var childWorkItems = new List<Models.WorkItemDto>();
        await CollectDescendantsAsync(projectId, workItem.ChildWorkItemNumbers, childWorkItems);

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(projectEntity => projectEntity.Id == projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        if (string.IsNullOrWhiteSpace(project.Repo))
            throw new InvalidOperationException("Project has no linked repository.");

        var userRole = await db.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == userId.Value)
            .Select(profile => profile.Role)
            .FirstOrDefaultAsync(cancellationToken);
        var normalizedRole = UserRoles.Normalize(userRole);
        var tierPolicy = TierPolicyCatalog.Get(normalizedRole);

        if (!recoveringInterruptedExecution)
        {
            var activeExecutions = await db.AgentExecutions
                .AsNoTracking()
                .CountAsync(
                    execution => execution.UserId == userId.Value.ToString() &&
                                 execution.ParentExecutionId == null &&
                                 execution.Status == "running" &&
                                 execution.Id != executionId,
                    cancellationToken);
            if (activeExecutions >= tierPolicy.MaxActiveAgentExecutions)
            {
                throw new InvalidOperationException(
                    $"Active execution limit reached for the '{tierPolicy.Tier}' tier ({tierPolicy.MaxActiveAgentExecutions}).");
            }
        }

        var repoFullName = project.Repo;
        var accessToken = await ResolveRequiredRepoAccessTokenAsync(userId.Value, repoFullName, cancellationToken);
        var pullRequestTargetBranch = await ResolvePullRequestTargetBranchAsync(
            accessToken,
            repoFullName,
            requestedBranch: null,
            cancellationToken);

        var branchName = string.IsNullOrWhiteSpace(persistedExecution.BranchName)
            ? null
            : persistedExecution.BranchName.Trim();
        if (string.IsNullOrWhiteSpace(branchName))
        {
            throw new InvalidOperationException(
                recoveringInterruptedExecution
                    ? "Interrupted execution is missing its branch name and cannot be recovered."
                    : "Paused execution is missing its branch name and cannot be resumed.");
        }

        var pipeline = BuildPipelineFromExecutionAgents(persistedExecution.Agents);
        if (pipeline.Length == 0)
        {
            pipeline = ResolveDefaultPipeline(persistedExecution.ExecutionMode);
            pipeline = ApplyAssignedAgentLimit(pipeline, workItem.AssignmentMode, workItem.AssignedAgentCount);
            logger.LogWarning(
                "Execution {ExecutionId}: persisted execution had no reconstructable agent pipeline; falling back to the default pipeline with {PhaseCount} roles",
                executionId,
                pipeline.SelectMany(group => group).Count());
        }

        var priorPhaseResults = await db.AgentPhaseResults
            .AsNoTracking()
            .Where(result => result.ExecutionId == persistedExecution.Id)
            .OrderBy(result => result.PhaseOrder)
            .ToListAsync(cancellationToken);
        var carryForwardOutputs = BuildResumeCarryForwardOutputs(priorPhaseResults, persistedExecution.Agents);

        var client = httpClientFactory.CreateClient("GitHub");
        var resumeFromRemoteBranch = await BranchExistsAsync(
            client,
            accessToken,
            repoFullName,
            branchName,
            cancellationToken);

        var retryPlan = new RetryExecutionPlan(
            SourceExecutionId: persistedExecution.Id,
            SourceStatus: persistedExecution.Status,
            ReuseBranchName: branchName,
            ReusePullRequestUrl: persistedExecution.PullRequestUrl,
            ReusePullRequestNumber: TryParsePullRequestNumber(persistedExecution.PullRequestUrl),
            ReusePullRequestTitle: persistedExecution.PullRequestTitle,
            PriorProgressEstimate: Math.Clamp(persistedExecution.Progress, 0, 1),
            CarryForwardOutputs: carryForwardOutputs,
            RetryContextMarkdown: BuildExecutionResumeContext(persistedExecution, priorPhaseResults),
            ResumeInPlace: true,
            ResumeFromRemoteBranch: resumeFromRemoteBranch);

        var totalRoles = pipeline.SelectMany(group => group).Count();
        var carriedRoleCount = CountCarryForwardRoles(pipeline, carryForwardOutputs);
        var resumedProgress = Math.Clamp(
            Math.Max(
                Math.Clamp(persistedExecution.Progress, 0, 1),
                totalRoles == 0 ? 0 : (double)carriedRoleCount / totalRoles),
            0,
            IncompleteProgressCeiling);
        var resumedPhaseLabel = carriedRoleCount > 0
            ? (recoveringInterruptedExecution ? "Recovering prior progress" : "Resuming prior progress")
            : (recoveringInterruptedExecution ? "Recovering after service restart" : "Resuming paused execution");
        var lifecycleLogMessage = recoveringInterruptedExecution
            ? (resumeFromRemoteBranch
                ? $"Execution {executionId} recovered after service restart and is continuing from the latest pushed branch state."
                : $"Execution {executionId} recovered after service restart, but branch '{branchName}' was not found remotely; continuing from the current base branch state.")
            : $"Execution {executionId} resumed from paused state";

        var trackedExecution = await db.AgentExecutions
            .FirstOrDefaultAsync(
                execution => execution.Id == executionId && execution.ProjectId == projectId,
                cancellationToken);
        if (trackedExecution is null)
            return false;

        var cts = new CancellationTokenSource();
        if (!ActiveExecutions.TryAdd(executionId, (cts, "cancelled")))
        {
            cts.Dispose();
            if (recoveringInterruptedExecution)
                return true;

            throw new InvalidOperationException("Execution is already active.");
        }

        try
        {
            trackedExecution.Status = "running";
            trackedExecution.CompletedAtUtc = null;
            trackedExecution.CurrentPhase = resumedPhaseLabel;
            trackedExecution.Progress = resumedProgress;
            trackedExecution.Agents = BuildAgentInfoList(pipeline, carryForwardOutputs);
            await db.SaveChangesAsync(cancellationToken);

            await workItemRepository.UpdateAsync(
                projectId,
                workItem.WorkItemNumber,
                new UpdateWorkItemRequest(
                    Title: null,
                    Description: null,
                    Priority: null,
                    Difficulty: null,
                    State: "In Progress (AI)",
                    AssignedTo: null,
                    Tags: null,
                    IsAI: null,
                    ParentWorkItemNumber: null,
                    LevelId: null));

            await WriteLogEntryAsync(
                db,
                projectId,
                "System",
                "info",
                lifecycleLogMessage,
                executionId: executionId);

            if (publishLifecycleEvents)
            {
                var latestExecutionLog = await db.LogEntries
                    .AsNoTracking()
                    .Where(log => log.ProjectId == projectId && log.ExecutionId == executionId)
                    .OrderByDescending(log => log.Time)
                    .ThenByDescending(log => log.Id)
                    .Select(log => new LogEntryDto(
                        log.Time,
                        log.Agent,
                        log.Level,
                        log.Message,
                        log.IsDetailed,
                        log.ExecutionId))
                    .FirstOrDefaultAsync(cancellationToken);

                await eventPublisher.PublishProjectEventAsync(
                    userId.Value,
                    projectId,
                    ServerEventTopics.AgentsUpdated,
                    new
                    {
                        projectId,
                        executionId,
                        execution = CreateInitializationExecutionDto(trackedExecution),
                    },
                    cancellationToken);
                await eventPublisher.PublishProjectEventAsync(
                    userId.Value,
                    projectId,
                    ServerEventTopics.WorkItemsUpdated,
                    new { projectId, workItemNumber = workItem.WorkItemNumber },
                    cancellationToken);
                await eventPublisher.PublishUserEventAsync(
                    userId.Value,
                    ServerEventTopics.ProjectsUpdated,
                    new { projectId },
                    cancellationToken);
                await eventPublisher.PublishProjectEventAsync(
                    userId.Value,
                    projectId,
                    ServerEventTopics.LogsUpdated,
                    new { projectId, executionId, logEntry = latestExecutionLog },
                    cancellationToken);
            }

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
                userId.Value,
                ModelKeys.Fast,
                pipeline,
                ResolveMaxConcurrentAgentsPerTask(
                    tierPolicy.MaxConcurrentAgentsPerTask,
                    workItem.AssignmentMode,
                    workItem.AssignedAgentCount),
                pullRequestTargetBranch,
                retryPlan,
                retryPlan.ReusePullRequestNumber ?? 0,
                string.IsNullOrWhiteSpace(persistedExecution.ParentExecutionId),
                persistedExecution.ParentExecutionId,
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
                        execution => execution.Id == executionId && execution.ProjectId == projectId,
                        CancellationToken.None);
                if (executionToRestore is not null)
                {
                    executionToRestore.Status = persistedExecution.Status;
                    executionToRestore.CurrentPhase = persistedExecution.CurrentPhase ??
                        (recoveringInterruptedExecution ? "Interrupted" : "Paused");
                    executionToRestore.Progress = persistedExecution.Progress;
                    executionToRestore.CompletedAtUtc = persistedExecution.CompletedAtUtc;
                    executionToRestore.Agents = persistedExecution.Agents
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
                    "Execution {ExecutionId}: failed to restore persisted state after relaunch setup error",
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
            .Select(e => new { e.Id, e.Status, e.BranchName, e.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (execution is null)
            return null;

        if (!CanDeleteExecutionStatus(execution.Status))
            throw new InvalidOperationException("Completed runs cannot be deleted.");

        var descendantExecutionIds = await CollectDescendantExecutionIdsAsync(db, projectId, executionId, cancellationToken);
        var descendantBranches = descendantExecutionIds.Count == 0
            ? []
            : await db.AgentExecutions
                .AsNoTracking()
                .Where(e => e.ProjectId == projectId && descendantExecutionIds.Contains(e.Id))
                .Select(e => e.BranchName)
                .ToListAsync(cancellationToken);
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

        if (deletionResult is not null &&
            IsBranchCleanupEligibleStatus(execution.Status) &&
            int.TryParse(execution.UserId, out var executionUserId))
        {
            var repoFullName = await db.Projects
                .AsNoTracking()
                .Where(project => project.Id == projectId)
                .Select(project => project.Repo)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(repoFullName))
            {
                try
                {
                    var accessToken = await ResolveRequiredRepoAccessTokenAsync(
                        executionUserId,
                        repoFullName,
                        cancellationToken);
                    var branchesToCleanup = descendantBranches
                        .Append(execution.BranchName)
                        .Where(branch => !string.IsNullOrWhiteSpace(branch))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    foreach (var branch in branchesToCleanup)
                    {
                        await TryDeleteRemoteBranchIfSafeAsync(
                            accessToken,
                            repoFullName,
                            branch,
                            cancellationToken,
                            projectId: projectId,
                            excludedExecutionIds: descendantExecutionIds.Append(executionId).ToArray());
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Execution {ExecutionId}: failed to clean up remote branches while deleting the run",
                        executionId);
                }
            }
        }

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
        string? parentExecutionId,
        CancellationToken externalCancellation)
    {
        // Use IServiceScopeFactory (singleton) instead of the request-scoped IServiceProvider.
        // The HTTP request scope is disposed after the controller returns Accepted(),
        // so a scoped IServiceProvider would throw ObjectDisposedException here.
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        var scopedConnectionService = scope.ServiceProvider.GetRequiredService<IConnectionService>();
        var scopedWorkItemRepo = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();
        var scopedWorkItemAttachmentService = scope.ServiceProvider.GetRequiredService<IWorkItemAttachmentService>();
        var scopedChatSessionRepository = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>();
        var scopedChatAttachmentStorage = scope.ServiceProvider.GetRequiredService<IChatAttachmentStorage>();
        var scopedNotificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var scopedUsageLedgerService = scope.ServiceProvider.GetRequiredService<IUsageLedgerService>();
        var scopedEventPublisher = scope.ServiceProvider.GetRequiredService<IServerEventPublisher>();
        await using var dbQueue = new ExecutionDbRequestQueue(serviceScopeFactory);
        var shouldCreatePullRequest = ShouldCreatePullRequestForExecution(parentExecutionId);
        var executionDepth = await ResolveExecutionDepthAsync(scopedDb, parentExecutionId, externalCancellation);

        IRepoSandbox? sandbox = null;
        string accessToken = string.Empty;

        Task WithDbLockAsync(Func<FleetDbContext, Task> action) => dbQueue.EnqueueAsync(async queuedDb =>
        {
            ThrowIfExecutionDeleted(executionId);
            await action(queuedDb);
        });

        Task<T> WithDbResultAsync<T>(Func<FleetDbContext, Task<T>> action) => dbQueue.ExecuteReadAsync(async queuedDb =>
        {
            ThrowIfExecutionDeleted(executionId);
            return await action(queuedDb);
        });

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

        async Task<AgentExecutionDto?> LoadExecutionSnapshotAsync()
        {
            var currentExecution = await WithDbResultAsync(async queuedDb =>
                await queuedDb.AgentExecutions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        execution => execution.Id == executionId && execution.ProjectId == projectId,
                        CancellationToken.None));

            return currentExecution is null ? null : CreateInitializationExecutionDto(currentExecution);
        }

        async Task<LogEntryDto?> LoadLatestExecutionLogAsync()
            => await WithDbResultAsync(async queuedDb =>
                await queuedDb.LogEntries
                    .AsNoTracking()
                    .Where(log => log.ProjectId == projectId && log.ExecutionId == executionId)
                    .OrderByDescending(log => log.Time)
                    .ThenByDescending(log => log.Id)
                    .Select(log => new LogEntryDto(
                        log.Time,
                        log.Agent,
                        log.Level,
                        log.Message,
                        log.IsDetailed,
                        log.ExecutionId))
                    .FirstOrDefaultAsync(CancellationToken.None));

        async Task PublishAgentsUpdatedAsync()
        {
            var executionSnapshot = await LoadExecutionSnapshotAsync();
            await scopedEventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.AgentsUpdated,
                new { projectId, executionId, execution = executionSnapshot });
        }

        async Task PublishLogsUpdatedAsync()
        {
            var logEntry = await LoadLatestExecutionLogAsync();
            await scopedEventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.LogsUpdated,
                new { projectId, executionId, logEntry });
        }

        async Task UpdateOrchestrationProgressAsync(int completedSubFlows, int totalSubFlows, string currentPhase)
        {
            var completionRatio = totalSubFlows <= 0 ? 0 : (double)completedSubFlows / totalSubFlows;
            var progress = Math.Min(0.95, 0.2 + (completionRatio * 0.75));
            await WithDbLockAsync(queuedDb => UpdateExecutionAsync(queuedDb, executionId, currentPhase, progress));
            await PublishAgentsUpdatedAsync();
        }

        async Task<string> EnsureSubFlowExecutionRunningAsync(Models.WorkItemDto childWorkItem)
        {
            var existingExecution = await WithDbResultAsync(async queuedDb =>
                await queuedDb.AgentExecutions
                    .AsNoTracking()
                    .Where(execution =>
                        execution.ProjectId == projectId &&
                        execution.ParentExecutionId == executionId &&
                        execution.WorkItemId == childWorkItem.WorkItemNumber)
                    .OrderByDescending(execution => execution.StartedAtUtc)
                    .FirstOrDefaultAsync(externalCancellation));

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

            if (string.Equals(existingExecution.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return existingExecution.Id;
            }

            if (IsRecoverableInterruptedExecutionStatus(existingExecution.Status))
            {
                if (ActiveExecutions.ContainsKey(existingExecution.Id))
                    return existingExecution.Id;

                var recovered = await orchestrationService.RecoverExecutionAsync(
                    projectId,
                    existingExecution.Id,
                    externalCancellation);
                if (!recovered)
                {
                    throw new InvalidOperationException(
                        $"Interrupted sub-flow execution {existingExecution.Id} could not be recovered.");
                }

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

        async Task<Dictionary<string, Data.Entities.AgentExecution>> WaitForTerminalSubFlowExecutionsAsync(
            IReadOnlyCollection<string> childExecutionIds)
        {
            while (true)
            {
                externalCancellation.ThrowIfCancellationRequested();

                var currentExecutions = await WithDbResultAsync(async queuedDb =>
                    await queuedDb.AgentExecutions
                        .AsNoTracking()
                        .Where(execution => childExecutionIds.Contains(execution.Id))
                        .ToListAsync(externalCancellation));

                if (currentExecutions.Count != childExecutionIds.Count)
                {
                    var foundIds = currentExecutions.Select(execution => execution.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var missingExecutionId = childExecutionIds.First(id => !foundIds.Contains(id));
                    throw new InvalidOperationException($"Sub-flow execution {missingExecutionId} no longer exists.");
                }

                var allTerminal = currentExecutions.All(current =>
                    !string.Equals(current.Status, "running", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(current.Status, "queued", StringComparison.OrdinalIgnoreCase));

                if (allTerminal)
                {
                    return currentExecutions.ToDictionary(execution => execution.Id, StringComparer.OrdinalIgnoreCase);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), externalCancellation);
            }
        }

        async Task StopDescendantExecutionsAsync(string finalStatus)
        {
            var descendantIds = await WithDbResultAsync(queuedDb =>
                CollectDescendantExecutionIdsAsync(queuedDb, projectId, executionId, CancellationToken.None));
            foreach (var descendantId in descendantIds)
            {
                await StopExecutionAsync(descendantId, finalStatus);
            }
        }

        async Task ExecuteSubFlowsAsync(
            Models.WorkItemDto parentWorkItem,
            IReadOnlyList<Models.WorkItemDto> directChildWorkItems)
        {
            if (sandbox is null)
                throw new InvalidOperationException("Execution sandbox is not ready for sub-flow orchestration.");

            var orderedChildren = directChildWorkItems
                .OrderBy(child => child.WorkItemNumber)
                .ToArray();
            var parallelism = Math.Clamp(Math.Min(maxConcurrentAgentsPerTask, MaxParallelSubFlows), 1, MaxParallelSubFlows);
            var completedSubFlows = 0;
            var mergedChildBranchesPendingCleanup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await WithDbLockAsync(async queuedDb =>
                await WriteLogEntryAsync(
                    queuedDb,
                    projectId,
                    "System",
                    "info",
                    $"Execution {executionId} is orchestrating {orderedChildren.Length} sub-flow(s) for work item #{parentWorkItem.WorkItemNumber}.",
                    executionId: executionId));
            await PublishLogsUpdatedAsync();

            for (var batchStart = 0; batchStart < orderedChildren.Length; batchStart += parallelism)
            {
                var batch = orderedChildren.Skip(batchStart).Take(parallelism).ToArray();
                var batchLabel = string.Join(", ", batch.Select(child => $"#{child.WorkItemNumber}"));
                await UpdateOrchestrationProgressAsync(
                    completedSubFlows,
                    orderedChildren.Length,
                    $"Queuing sub-flows {batchLabel}");

                await WithDbLockAsync(async queuedDb =>
                    await WriteLogEntryAsync(
                        queuedDb,
                        projectId,
                        "System",
                        "info",
                        $"Queuing sub-flow batch {batchLabel}.",
                        executionId: executionId));
                await PublishLogsUpdatedAsync();

                var launchedChildren = new List<(Models.WorkItemDto WorkItem, string ExecutionId)>(batch.Length);
                foreach (var child in batch)
                {
                    var childExecutionId = await EnsureSubFlowExecutionRunningAsync(child);
                    launchedChildren.Add((child, childExecutionId));

                    await WithDbLockAsync(async queuedDb =>
                        await WriteLogEntryAsync(
                            queuedDb,
                            projectId,
                            "System",
                            "info",
                            $"Sub-flow #{child.WorkItemNumber} is running as execution {childExecutionId}.",
                            executionId: executionId));
                    await PublishLogsUpdatedAsync();
                }

                await UpdateOrchestrationProgressAsync(
                    completedSubFlows,
                    orderedChildren.Length,
                    $"Waiting on sub-flows {batchLabel}");

                var terminalExecutionMap = await WaitForTerminalSubFlowExecutionsAsync(
                    launchedChildren.Select(launched => launched.ExecutionId).ToArray());

                var terminalExecutions = launchedChildren
                    .Select(launched => (launched.WorkItem, Execution: terminalExecutionMap[launched.ExecutionId]))
                    .ToArray();

                var blockingExecution = terminalExecutions.FirstOrDefault(result =>
                    !string.Equals(result.Execution.Status, "completed", StringComparison.OrdinalIgnoreCase));
                if (blockingExecution.Execution is not null)
                {
                    throw new InvalidOperationException(
                        $"Sub-flow #{blockingExecution.WorkItem.WorkItemNumber} ended in status '{blockingExecution.Execution.Status}'.");
                }

                accessToken = await ResolveRequiredRepoAccessTokenAsync(
                    scopedConnectionService,
                    userId,
                    repoFullName,
                    externalCancellation);

                await UpdateOrchestrationProgressAsync(
                    completedSubFlows,
                    orderedChildren.Length,
                    $"Merging sub-flows {batchLabel} into parent batch");

                foreach (var terminalExecution in terminalExecutions.OrderBy(result => result.WorkItem.WorkItemNumber))
                {
                    var childBranchName = terminalExecution.Execution.BranchName?.Trim();
                    if (string.IsNullOrWhiteSpace(childBranchName))
                    {
                        throw new InvalidOperationException(
                            $"Sub-flow #{terminalExecution.WorkItem.WorkItemNumber} completed without a branch name to merge.");
                    }

                    await WithDbLockAsync(async queuedDb =>
                        await WriteLogEntryAsync(
                            queuedDb,
                            projectId,
                            "System",
                            "info",
                            $"Merging sub-flow #{terminalExecution.WorkItem.WorkItemNumber} from branch '{childBranchName}' into '{sandbox.BranchName}'.",
                            executionId: executionId));
                    await PublishLogsUpdatedAsync();

                    var childBranchStillExists = await BranchExistsAsync(
                        httpClientFactory.CreateClient("GitHub"),
                        accessToken,
                        repoFullName,
                        childBranchName,
                        externalCancellation);
                    if (!childBranchStillExists)
                    {
                        await WithDbLockAsync(async queuedDb =>
                            await WriteLogEntryAsync(
                                queuedDb,
                                projectId,
                                "System",
                                "warn",
                                $"Sub-flow branch '{childBranchName}' was already gone before merge; assuming its commits were already incorporated into '{sandbox.BranchName}'.",
                                executionId: executionId));
                        await PublishLogsUpdatedAsync();
                        continue;
                    }

                    await sandbox.MergeBranchAsync(
                        accessToken,
                        childBranchName,
                        commitAuthorName,
                        commitAuthorEmail,
                        externalCancellation);
                    mergedChildBranchesPendingCleanup.Add(childBranchName);
                }

                completedSubFlows += terminalExecutions.Length;
                await UpdateOrchestrationProgressAsync(
                    completedSubFlows,
                    orderedChildren.Length,
                    completedSubFlows >= orderedChildren.Length
                        ? "Sub-flows completed"
                        : $"Completed {completedSubFlows}/{orderedChildren.Length} sub-flows");
            }

            accessToken = await ResolveRequiredRepoAccessTokenAsync(
                scopedConnectionService,
                userId,
                repoFullName,
                externalCancellation);

            await UpdateOrchestrationProgressAsync(
                orderedChildren.Length,
                orderedChildren.Length,
                shouldCreatePullRequest
                    ? "Publishing merged parent batch"
                    : "Publishing merged sub-flow batch");

            await sandbox.PushBranchAsync(accessToken, externalCancellation);

            foreach (var childBranchName in mergedChildBranchesPendingCleanup)
            {
                await TryDeleteRemoteBranchIfSafeAsync(
                    accessToken,
                    repoFullName,
                    childBranchName,
                    externalCancellation,
                    protectedBranchName: sandbox.BranchName,
                    projectId: projectId);
            }

            var refreshedDirectChildren = new List<Models.WorkItemDto>();
            await CollectDirectChildrenAsync(projectId, parentWorkItem.ChildWorkItemNumbers, refreshedDirectChildren);
            if (!shouldCreatePullRequest)
            {
                var parentState = ResolveParentFlowState(refreshedDirectChildren);

                await WithDbLockAsync(queuedDb => FinalizeExecutionAsync(queuedDb, executionId, "completed"));
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

                await WithDbLockAsync(async queuedDb =>
                    await WriteLogEntryAsync(
                        queuedDb,
                        projectId,
                        "System",
                        "success",
                        $"Execution {executionId} completed after orchestrating {orderedChildren.Length} sub-flow(s) and merging them into branch '{sandbox.BranchName}'.",
                        executionId: executionId));
                await PublishLogsUpdatedAsync();
                await scopedNotificationService.PublishAsync(
                    userId,
                    projectId,
                    "execution_completed",
                    $"Execution completed for #{parentWorkItem.WorkItemNumber}",
                    $"{orderedChildren.Length} sub-flow(s) completed.",
                    executionId);
                return;
            }

            var (orchestrationPrUrl, orchestrationPrNumber) = await OpenPullRequestAsync(
                sandbox,
                accessToken,
                repoFullName,
                parentWorkItem,
                commitAuthorName,
                commitAuthorEmail,
                pullRequestTargetBranch,
                scopedDb,
                executionId,
                externalCancellation,
                draft: false,
                seedBranchWithMarkerCommit: false);

            var resolvedPrNumber = orchestrationPrNumber > 0
                ? orchestrationPrNumber
                : (TryParsePullRequestNumber(orchestrationPrUrl) ?? 0);
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
                    if (string.IsNullOrWhiteSpace(orchestrationPrUrl))
                        orchestrationPrUrl = discoveredPr.Url;
                }
            }

            if (string.IsNullOrWhiteSpace(orchestrationPrUrl))
            {
                throw new InvalidOperationException(
                    $"Fleet completed the parent batch, but could not create or locate a GitHub pull request for branch '{sandbox.BranchName}'.");
            }

            if (resolvedPrNumber > 0)
            {
                await MarkPullRequestReadyAsync(accessToken, repoFullName, resolvedPrNumber, externalCancellation);
            }

            var prLifecycle = resolvedPrNumber > 0
                ? await GetPullRequestLifecycleAsync(accessToken, repoFullName, resolvedPrNumber, externalCancellation)
                : null;

            await WithDbLockAsync(queuedDb => FinalizeExecutionAsync(queuedDb, executionId, "completed", orchestrationPrUrl));
            await PublishAgentsUpdatedAsync();
            await scopedNotificationService.PublishAsync(
                userId,
                projectId,
                "pr_ready",
                $"PR ready for #{parentWorkItem.WorkItemNumber}",
                orchestrationPrUrl ?? "A pull request is ready for review.",
                executionId);
            await scopedNotificationService.PublishAsync(
                userId,
                projectId,
                "execution_completed",
                $"Execution completed for #{parentWorkItem.WorkItemNumber}",
                parentWorkItem.Title,
                executionId);

            var workItemsToUpdate = childWorkItems
                .Append(parentWorkItem)
                .GroupBy(item => item.WorkItemNumber)
                .Select(group => group.First());

            foreach (var itemToUpdate in workItemsToUpdate)
            {
                var targetState = ResolveStateFromPullRequestLifecycle(itemToUpdate.IsAI, prLifecycle);
                var observedPullRequestState = ResolveObservedPullRequestState(prLifecycle);
                await scopedWorkItemRepo.UpdateAsync(
                    projectId,
                    itemToUpdate.WorkItemNumber,
                    new UpdateWorkItemRequest(
                        Title: null,
                        Description: null,
                        Priority: null,
                        Difficulty: null,
                        State: targetState,
                        AssignedTo: null,
                        Tags: null,
                        IsAI: null,
                        ParentWorkItemNumber: null,
                        LevelId: null,
                        LinkedPullRequestUrl: orchestrationPrUrl,
                        LastObservedPullRequestState: observedPullRequestState,
                        LastObservedPullRequestUrl: orchestrationPrUrl));
            }

            await PublishWorkItemsUpdatedAsync();
            await PublishProjectsUpdatedAsync();

            await WithDbLockAsync(async queuedDb =>
                await WriteLogEntryAsync(
                    queuedDb,
                    projectId,
                    "System",
                    "success",
                    $"Execution {executionId} completed successfully -- PR: {orchestrationPrUrl}",
                    executionId: executionId));
            await PublishLogsUpdatedAsync();
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

            accessToken = await ResolveRequiredRepoAccessTokenAsync(
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
                sandbox, projectId, userId.ToString(), accessToken, repoFullName, executionId)
            {
                Scratchpad = new AgentScratchpad()
            };

            string? prUrl = retryPlan?.ReuseExistingBranchAndPullRequest == true &&
                            !string.IsNullOrWhiteSpace(retryPlan.ReusePullRequestUrl)
                ? retryPlan.ReusePullRequestUrl
                : null;
            var prNumber = existingPullRequestNumber;
            var draftPullRequestReady = !string.IsNullOrWhiteSpace(prUrl);
            if (!shouldCreatePullRequest)
            {
                prUrl = null;
                prNumber = 0;
                draftPullRequestReady = false;
            }

            var stagedChatAssets = await StageReferencedChatAttachmentsAsync(
                sandbox,
                projectId,
                userId.ToString(),
                workItem,
                childWorkItems,
                scopedChatSessionRepository,
                scopedWorkItemAttachmentService,
                scopedChatAttachmentStorage,
                externalCancellation);

            // Build the initial user message with work item context (includes children)
            var workItemContext = BuildWorkItemContext(workItem, childWorkItems);
            if (stagedChatAssets.Count > 0)
            {
                workItemContext = $"{workItemContext}\n\n{BuildStagedChatAssetContext(stagedChatAssets)}";
            }
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
                    await WithDbLockAsync(async queuedDb =>
                        await WriteLogEntryAsync(
                            queuedDb,
                            projectId,
                            "System",
                            "info",
                            $"Carrying forward completed phases from {carriedRoleLabel}: {string.Join(", ", pipeline.SelectMany(g => g).Where(carriedRoles.Contains))}",
                            executionId: executionId));
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

                    await WithDbLockAsync(async queuedDb =>
                    {
                        await UpdateExecutionAsync(
                            queuedDb,
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
                        await WithDbLockAsync(async queuedDb =>
                        {
                            await WriteLogEntryAsync(
                                queuedDb,
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
                            if (generatedPlan is not null &&
                                ShouldMaterializeGeneratedSubFlows(workItem, generatedPlan, executionDepth))
                            {
                                var createdSubFlows = await MaterializeGeneratedSubFlowsAsync(
                                    scopedWorkItemRepo,
                                    projectId,
                                    workItem,
                                    generatedPlan);
                                if (createdSubFlows.Count > 0)
                                {
                                    childWorkItems.AddRange(createdSubFlows);
                                    directChildWorkItems = GetDirectActionableChildren(workItem, childWorkItems);
                                    orchestrationExecution = true;
                                    currentCyclePipeline = OrchestrationPreludePipeline;
                                    draftPullRequestReady = false;
                                    prUrl = null;
                                    prNumber = 0;
                                    await WithDbLockAsync(async queuedDb =>
                                        await WriteLogEntryAsync(
                                            queuedDb,
                                            projectId,
                                            "Planner Agent",
                                            "info",
                                            $"Generated {createdSubFlows.Count} sub-flow work item(s): {generatedPlan.Reason}",
                                            executionId: executionId));
                                    await PublishWorkItemsUpdatedAsync();
                                    await PublishProjectsUpdatedAsync();
                                    await PublishLogsUpdatedAsync();
                                }
                            }
                            else if (generatedPlan is not null)
                            {
                                logger.LogInformation(
                                    "Execution {ExecutionId}: skipped planner-generated sub-flow materialization for work item #{WorkItemNumber} because the task is better handled as a direct run or already hit a sub-flow limit.",
                                    executionId,
                                    workItem.WorkItemNumber);
                            }
                        }

                        if (ShouldOrchestrateExistingSubFlows(workItem, directChildWorkItems, childWorkItems, executionDepth))
                        {
                            orchestrationExecution = true;
                            await WithDbLockAsync(queuedDb => TransitionExecutionToOrchestrationModeAsync(
                                queuedDb,
                                executionId,
                                directChildWorkItems,
                                currentOutputsByRole));
                            await WithDbLockAsync(async queuedDb =>
                                await WriteLogEntryAsync(
                                    queuedDb,
                                    projectId,
                                    "System",
                                    "info",
                                    $"Planner delegated this run into {directChildWorkItems.Count} sub-flow(s).",
                                    executionId: executionId));
                            await PublishAgentsUpdatedAsync();
                            await PublishLogsUpdatedAsync();
                            await ExecuteSubFlowsAsync(workItem, directChildWorkItems);
                            return;
                        }

                        if (shouldCreatePullRequest && !draftPullRequestReady)
                        {
                            (prUrl, prNumber) = await OpenPullRequestAsync(
                                sandbox,
                                accessToken,
                                repoFullName,
                                workItem,
                                commitAuthorName,
                                commitAuthorEmail,
                                pullRequestTargetBranch,
                                scopedDb,
                                executionId,
                                externalCancellation,
                                draft: true,
                                seedBranchWithMarkerCommit: true);
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

                await WithDbLockAsync(async queuedDb =>
                {
                    await PrepareExecutionForAutomaticReviewLoopAsync(
                        queuedDb,
                        executionId,
                        pipeline,
                        rerunRoles,
                        currentCycleCarryForwardOutputs,
                        latestCycleReviewDecision,
                        reviewLoopCount);

                    await WriteLogEntryAsync(
                        queuedDb,
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
                    var lastLoggedPercent = retryProgressFloorPercent > 0
                        ? (int)Math.Floor(Math.Clamp(retryProgressFloorPercent, 0, 99.999))
                        : -1;
                    var lastLoggedSummary = string.Empty;
                    var initialProgressSummary = isRetry
                        ? retryProgressFloorPercent > 0
                            ? $"Retrying phase (attempt {attempt}/{maxAttempts}, resuming from {FormatProgressPercent(retryProgressFloorPercent)}%)"
                            : $"Retrying phase (attempt {attempt}/{maxAttempts})"
                        : $"Starting phase: {GetPhaseTaskDescription(role)}";

                    await WithDbLockAsync(async queuedDb =>
                    {
                        await SetAgentRunningAsync(queuedDb, executionId, role.ToString(),
                            isRetry
                                ? $"{GetPhaseTaskDescription(role)} (retry {attempt - 1}/{MaxAgentRetries})"
                                : GetPhaseTaskDescription(role),
                            retryProgressFloorPercent / 100.0);

                        await WriteLogEntryAsync(
                            queuedDb,
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
                        await WithDbLockAsync(async queuedDb =>
                        {
                            var overallProgress = ((double)(groupCompletedBase + roleIndex) + estimatedProgress) / totalRoles;

                            var exec = await queuedDb.AgentExecutions.FindAsync(executionId);
                            if (exec is null) return;

                            var clampedOverall = Math.Clamp(overallProgress, 0, IncompleteProgressCeiling);
                            exec.Progress = Math.Max(exec.Progress, clampedOverall);

                            var agent = exec.Agents.FirstOrDefault(a => a.Role == role.ToString());
                            string? previousTask = null;
                            if (agent is not null)
                            {
                                previousTask = agent.CurrentTask;
                                if (!string.IsNullOrWhiteSpace(summary))
                                    agent.CurrentTask = summary;

                                var clampedRoleProgress = Math.Clamp(estimatedProgress, 0, IncompleteProgressCeiling);
                                agent.Progress = Math.Max(agent.Progress, clampedRoleProgress);
                            }

                            await queuedDb.SaveChangesAsync();

                            if (!string.IsNullOrWhiteSpace(summary) &&
                                !string.Equals(previousTask, summary, StringComparison.Ordinal))
                            {
                                var isHeartbeat = IsHeartbeatProgressSummary(summary);
                                await WriteLogEntryAsync(
                                    queuedDb,
                                    projectId,
                                    $"{role} Agent",
                                    "info",
                                    $"Status update: {summary}",
                                    isDetailed: isHeartbeat,
                                    executionId: executionId);
                                lastLoggedSummary = summary;
                            }

                            var pct = (int)Math.Floor(Math.Clamp(estimatedProgress * 100, 0, 99.999));
                            if (pct != lastLoggedPercent)
                            {
                                await WriteLogEntryAsync(
                                    queuedDb,
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
                                    queuedDb,
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
                        await WithDbLockAsync(async queuedDb =>
                        {
                            var logMsg = $"Tool: {toolName} -> {resultSnippet}";
                            await WriteLogEntryAsync(
                                queuedDb,
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

                    // Each role gets its own DI scope so that concurrent role execution
                    // (when maxConcurrentAgentsPerTask > 1) doesn't share a DbContext.
                    // The outer scopedDb is only accessed via WithDbLockAsync (serialized).
                    await using var roleScope = serviceScopeFactory.CreateAsyncScope();
                    var rolePhaseRunner = roleScope.ServiceProvider.GetRequiredService<IAgentPhaseRunner>();
                    var result = await rolePhaseRunner.RunPhaseAsync(
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

                    await WithDbLockAsync(async queuedDb =>
                    {
                        ThrowIfExecutionDeleted(executionId);
                        var phaseResultEntity = new AgentPhaseResult
                        {
                            Role = role.ToString(),
                            Output = result.Output,
                            ToolCallCount = result.ToolCallCount,
                            InputTokens = result.InputTokens,
                            OutputTokens = result.OutputTokens,
                            Success = result.Success,
                            Error = result.Error,
                            StartedAt = phaseStart,
                            CompletedAt = phaseEnd,
                            PhaseOrder = rolePhaseOrder,
                            ExecutionId = executionId,
                        };
                        queuedDb.AgentPhaseResults.Add(phaseResultEntity);
                        await queuedDb.SaveChangesAsync();

                        await UpdateAgentInfoAsync(
                            queuedDb,
                            executionId,
                            role.ToString(),
                            result.Success ? "completed" : "failed",
                            result.ToolCallCount,
                            Math.Clamp(result.EstimatedCompletionPercent / 100.0, 0, IncompleteProgressCeiling),
                            result.Success
                                ? null
                                : BuildRetryFailureTask(result.EstimatedCompletionPercent, result.LastProgressSummary));

                        if (result.Success)
                        {
                            var successMsg = attempt == 1
                                ? $"Phase completed ({result.ToolCallCount} tool calls)"
                                : $"Phase completed after retry ({attempt}/{maxAttempts}, {result.ToolCallCount} tool calls)";
                            await WriteLogEntryAsync(
                                queuedDb,
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
                                queuedDb,
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
                if (ShouldOrchestrateExistingSubFlows(workItem, directChildWorkItems, childWorkItems, executionDepth))
                {
                    await WithDbLockAsync(async queuedDb =>
                        await TransitionExecutionToOrchestrationModeAsync(
                            queuedDb,
                            executionId,
                            directChildWorkItems,
                            currentCycleCarryForwardOutputs));
                    await WithDbLockAsync(async queuedDb =>
                        await WriteLogEntryAsync(
                            queuedDb,
                            projectId,
                            "System",
                            "info",
                            $"Execution is switching from planning into orchestration for {directChildWorkItems.Count} sub-flow(s).",
                            executionId: executionId));
                    await PublishAgentsUpdatedAsync();
                    await PublishLogsUpdatedAsync();
                    await ExecuteSubFlowsAsync(workItem, directChildWorkItems);
                    return;
                }
            }

            if (shouldCreatePullRequest && !draftPullRequestReady)
            {
                (prUrl, prNumber) = await OpenPullRequestAsync(
                    sandbox,
                    accessToken,
                    repoFullName,
                    workItem,
                    commitAuthorName,
                    commitAuthorEmail,
                    pullRequestTargetBranch,
                    scopedDb,
                    executionId,
                    externalCancellation,
                    draft: true,
                    seedBranchWithMarkerCommit: true);
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

            if (!shouldCreatePullRequest)
            {
                await sandbox.PushBranchAsync(accessToken, externalCancellation);
                await WithDbLockAsync(queuedDb => FinalizeExecutionAsync(queuedDb, executionId, "completed"));
                await PublishAgentsUpdatedAsync();
                await WithDbLockAsync(async queuedDb =>
                    await WriteLogEntryAsync(
                        queuedDb,
                        projectId,
                        "System",
                        "success",
                        $"Execution {executionId} completed successfully on branch '{sandbox.BranchName}' and is ready to merge into its parent batch.",
                        executionId: executionId));
                await PublishLogsUpdatedAsync();
                await PublishProjectsUpdatedAsync();

                logger.LogInformation(
                    "Execution {ExecutionId}: sub-flow pipeline completed successfully without opening a pull request",
                    executionId);
                return;
            }

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

            await WithDbLockAsync(queuedDb => FinalizeExecutionAsync(queuedDb, executionId, "completed", prUrl));
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
            await WithDbLockAsync(async queuedDb =>
                await WriteLogEntryAsync(
                    queuedDb,
                    projectId,
                    "System",
                    "success",
                    $"Execution {executionId} completed successfully" +
                    (prUrl is not null ? $" -- PR: {prUrl}" : ""),
                    executionId: executionId));
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
                await WithDbLockAsync(queuedDb => FinalizeExecutionAsync(queuedDb, executionId, finalStatus));
                await PublishAgentsUpdatedAsync();
                await WithDbLockAsync(async queuedDb =>
                    await WriteLogEntryAsync(
                        queuedDb,
                        projectId,
                        "System",
                        "warn",
                        $"Execution {executionId} was {finalStatus} by user",
                        executionId: executionId));
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
                await WithDbLockAsync(queuedDb => FinalizeExecutionAsync(queuedDb, executionId, "failed", errorMessage: ex.Message));
                await PublishAgentsUpdatedAsync();
                await WithDbLockAsync(async queuedDb =>
                    await WriteLogEntryAsync(
                        queuedDb,
                        projectId,
                        "System",
                        "error",
                        $"Execution {executionId} failed: {ex.Message}",
                        executionId: executionId));
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

    internal static bool ShouldOrchestrateExistingSubFlows(
        Models.WorkItemDto workItem,
        IReadOnlyCollection<Models.WorkItemDto> directChildWorkItems,
        IReadOnlyCollection<Models.WorkItemDto> descendants,
        int executionDepth)
    {
        if (executionDepth >= MaxSubFlowExecutionDepth)
            return false;

        if (directChildWorkItems.Count < 2 || directChildWorkItems.Count > MaxSubFlowChildrenPerExecution)
            return false;

        if (workItem.Difficulty <= 2)
            return false;

        if (workItem.ParentWorkItemNumber is not null && workItem.Difficulty <= 3)
            return false;

        var childLookup = BuildChildWorkItemLookup(descendants);
        var leafCount = CountLeafWorkItems(workItem.WorkItemNumber, childLookup);
        if (leafCount < 2)
            return false;

        if (HasSingleChildChain(workItem.WorkItemNumber, childLookup))
            return false;

        var branchAnalyses = directChildWorkItems
            .Select(child =>
            {
                var directGrandchildren = GetDirectChildCount(child.WorkItemNumber, childLookup);
                var leafDescendants = CountLeafWorkItems(child.WorkItemNumber, childLookup);
                var branchDepth = CountWorkItemBranchDepth(child.WorkItemNumber, childLookup);
                var complexityScore = ComputeExistingBranchComplexityScore(
                    child.Difficulty,
                    directGrandchildren,
                    leafDescendants,
                    branchDepth);

                return new
                {
                    Child = child,
                    DirectGrandchildren = directGrandchildren,
                    LeafDescendants = leafDescendants,
                    BranchDepth = branchDepth,
                    ComplexityScore = complexityScore,
                };
            })
            .ToArray();

        var substantialDirectBranches = branchAnalyses.Count(branch =>
            branch.Child.Difficulty >= 2 ||
            branch.DirectGrandchildren > 0 ||
            branch.LeafDescendants > 1);
        if (substantialDirectBranches < 2)
            return false;

        var hasNestedChildren = branchAnalyses.Any(branch => branch.DirectGrandchildren > 0);
        var totalDirectDifficulty = branchAnalyses.Sum(branch => branch.Child.Difficulty);
        var totalBranchComplexity = branchAnalyses.Sum(branch => branch.ComplexityScore);
        var highValueParallelBranches = branchAnalyses.Count(branch =>
            branch.Child.Difficulty >= 3 &&
            (branch.DirectGrandchildren > 0 || branch.LeafDescendants > 1));
        var moderateParallelBranches = !hasNestedChildren &&
            workItem.Difficulty >= 4 &&
            branchAnalyses.Count(branch => branch.Child.Difficulty >= 3) >= 2 &&
            totalDirectDifficulty >= 6;

        if (!hasNestedChildren && workItem.Difficulty <= 3 && totalDirectDifficulty <= 5)
            return false;

        if (!hasNestedChildren && branchAnalyses.All(branch => branch.Child.Difficulty <= 2))
            return false;

        if (highValueParallelBranches >= 2)
            return true;

        if (moderateParallelBranches)
            return true;

        var minimumComplexityThreshold = hasNestedChildren ? 8 : 10;
        if (totalBranchComplexity < minimumComplexityThreshold)
            return false;

        return true;
    }

    internal static bool ShouldMaterializeGeneratedSubFlows(
        Models.WorkItemDto workItem,
        GeneratedSubFlowPlan generatedPlan,
        int executionDepth = 0)
    {
        if (executionDepth >= MaxSubFlowExecutionDepth)
            return false;

        if (generatedPlan.SubFlows.Count < 2 || generatedPlan.SubFlows.Count > MaxSubFlowChildrenPerExecution)
            return false;

        if (workItem.Difficulty <= 2)
            return false;

        if (workItem.ParentWorkItemNumber is not null && workItem.Difficulty <= 3)
            return false;

        var flattenedSubFlows = FlattenGeneratedSubFlows(generatedPlan.SubFlows).ToArray();
        if (flattenedSubFlows.Length == 0)
            return false;

        if (HasNodeExceedingGeneratedChildLimit(generatedPlan.SubFlows))
            return false;

        if (flattenedSubFlows.Any(subFlow => subFlow.Difficulty > workItem.Difficulty + 1))
            return false;

        if (CountGeneratedLeafSubFlows(generatedPlan.SubFlows) < 2)
            return false;

        var branchAnalyses = generatedPlan.SubFlows
            .Select(subFlow =>
            {
                var directChildren = subFlow.SubFlows.Count;
                var leafDescendants = CountGeneratedLeafSubFlows(subFlow.SubFlows);
                var branchDepth = CountGeneratedBranchDepth(subFlow);
                var complexityScore = ComputeGeneratedBranchComplexityScore(
                    subFlow.Difficulty,
                    directChildren,
                    leafDescendants,
                    branchDepth);

                return new
                {
                    SubFlow = subFlow,
                    DirectChildren = directChildren,
                    LeafDescendants = leafDescendants,
                    BranchDepth = branchDepth,
                    ComplexityScore = complexityScore,
                };
            })
            .ToArray();

        var substantialDirectBranches = branchAnalyses.Count(branch =>
            branch.SubFlow.Difficulty >= 2 ||
            branch.DirectChildren > 0 ||
            branch.LeafDescendants > 1);
        if (substantialDirectBranches < 2)
            return false;

        var materiallyReducesComplexity =
            flattenedSubFlows.Any(subFlow => subFlow.Difficulty < workItem.Difficulty) ||
            (generatedPlan.SubFlows.Count >= 2 &&
             branchAnalyses.Count(branch => branch.SubFlow.Difficulty >= workItem.Difficulty - 1) >= 2);
        if (!materiallyReducesComplexity)
            return false;

        var hasNestedDirectBranch = branchAnalyses.Any(branch => branch.DirectChildren > 0);
        var totalDirectDifficulty = branchAnalyses.Sum(branch => branch.SubFlow.Difficulty);
        var totalBranchComplexity = branchAnalyses.Sum(branch => branch.ComplexityScore);
        var highValueParallelBranches = branchAnalyses.Count(branch =>
            branch.SubFlow.Difficulty >= 3 &&
            (branch.DirectChildren > 0 || branch.LeafDescendants > 1));
        var moderateParallelBranches = !hasNestedDirectBranch &&
            workItem.Difficulty >= 4 &&
            branchAnalyses.Count(branch => branch.SubFlow.Difficulty >= 3) >= 2 &&
            totalDirectDifficulty >= 6;
        if (!hasNestedDirectBranch && workItem.Difficulty <= 3 && totalDirectDifficulty <= 5)
            return false;

        if (!hasNestedDirectBranch && branchAnalyses.All(branch => branch.SubFlow.Difficulty <= 2))
            return false;

        if (highValueParallelBranches >= 2)
            return true;

        if (moderateParallelBranches)
            return true;

        var minimumComplexityThreshold = hasNestedDirectBranch ? 8 : 6;
        if (totalBranchComplexity < minimumComplexityThreshold)
            return false;

        return true;
    }

    private static Dictionary<int, List<Models.WorkItemDto>> BuildChildWorkItemLookup(
        IReadOnlyCollection<Models.WorkItemDto> descendants)
        => descendants
            .Where(descendant => descendant.ParentWorkItemNumber is not null)
            .GroupBy(descendant => descendant.ParentWorkItemNumber!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.WorkItemNumber).ToList());

    private static int CountLeafWorkItems(
        int parentWorkItemNumber,
        IReadOnlyDictionary<int, List<Models.WorkItemDto>> childLookup)
    {
        if (!childLookup.TryGetValue(parentWorkItemNumber, out var children) || children.Count == 0)
            return 0;

        var leafCount = 0;
        foreach (var child in children)
        {
            leafCount += childLookup.ContainsKey(child.WorkItemNumber)
                ? CountLeafWorkItems(child.WorkItemNumber, childLookup)
                : 1;
        }

        return leafCount;
    }

    private static int CountWorkItemBranchDepth(
        int workItemNumber,
        IReadOnlyDictionary<int, List<Models.WorkItemDto>> childLookup)
    {
        if (!childLookup.TryGetValue(workItemNumber, out var children) || children.Count == 0)
            return 1;

        return 1 + children.Max(child => CountWorkItemBranchDepth(child.WorkItemNumber, childLookup));
    }

    private static int ComputeExistingBranchComplexityScore(
        int difficulty,
        int directGrandchildren,
        int leafDescendants,
        int branchDepth)
        => difficulty
            + (difficulty >= 4 ? 2 : 0)
            + Math.Min(2, directGrandchildren)
            + Math.Min(2, Math.Max(0, leafDescendants - 1))
            + Math.Min(2, Math.Max(0, branchDepth - 1));

    private static int CountGeneratedBranchDepth(GeneratedSubFlowSpec subFlow)
    {
        if (subFlow.SubFlows.Count == 0)
            return 1;

        return 1 + subFlow.SubFlows.Max(CountGeneratedBranchDepth);
    }

    private static int ComputeGeneratedBranchComplexityScore(
        int difficulty,
        int directChildren,
        int leafDescendants,
        int branchDepth)
        => difficulty
            + (difficulty >= 4 ? 2 : 0)
            + Math.Min(2, directChildren)
            + Math.Min(2, Math.Max(0, leafDescendants - 1))
            + Math.Min(2, Math.Max(0, branchDepth - 1));

    private static int GetDirectChildCount(
        int parentWorkItemNumber,
        IReadOnlyDictionary<int, List<Models.WorkItemDto>> childLookup)
        => childLookup.TryGetValue(parentWorkItemNumber, out var children) ? children.Count : 0;

    private static bool HasSingleChildChain(
        int parentWorkItemNumber,
        IReadOnlyDictionary<int, List<Models.WorkItemDto>> childLookup)
    {
        var currentParentNumber = parentWorkItemNumber;
        var traversedAnyChildren = false;

        while (childLookup.TryGetValue(currentParentNumber, out var children) && children.Count > 0)
        {
            traversedAnyChildren = true;
            if (children.Count != 1)
                return false;

            currentParentNumber = children[0].WorkItemNumber;
        }

        return traversedAnyChildren;
    }

    private static IEnumerable<GeneratedSubFlowSpec> FlattenGeneratedSubFlows(IEnumerable<GeneratedSubFlowSpec> subFlows)
    {
        foreach (var subFlow in subFlows)
        {
            yield return subFlow;

            foreach (var child in FlattenGeneratedSubFlows(subFlow.SubFlows))
                yield return child;
        }
    }

    private static int CountGeneratedLeafSubFlows(IReadOnlyList<GeneratedSubFlowSpec> subFlows)
    {
        if (subFlows.Count == 0)
            return 0;

        var leafCount = 0;
        foreach (var subFlow in subFlows)
        {
            leafCount += subFlow.SubFlows.Count > 0
                ? CountGeneratedLeafSubFlows(subFlow.SubFlows)
                : 1;
        }

        return leafCount;
    }

    private static bool HasNodeExceedingGeneratedChildLimit(IReadOnlyList<GeneratedSubFlowSpec> subFlows)
        => subFlows.Count > MaxSubFlowChildrenPerExecution ||
           subFlows.Any(subFlow => HasNodeExceedingGeneratedChildLimit(subFlow.SubFlows));

    private static async Task<int> ResolveExecutionDepthAsync(
        FleetDbContext context,
        string? parentExecutionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentExecutionId))
            return 0;

        var depth = 0;
        var currentExecutionId = parentExecutionId;
        var visitedExecutionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!string.IsNullOrWhiteSpace(currentExecutionId) &&
               visitedExecutionIds.Add(currentExecutionId) &&
               depth < 32)
        {
            depth++;
            currentExecutionId = await context.AgentExecutions
                .AsNoTracking()
                .Where(execution => execution.Id == currentExecutionId)
                .Select(execution => execution.ParentExecutionId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return depth;
    }

    private async Task<List<Models.WorkItemDto>> MaterializeGeneratedSubFlowsAsync(
        IWorkItemRepository scopedWorkItemRepo,
        string projectId,
        Models.WorkItemDto parentWorkItem,
        GeneratedSubFlowPlan generatedPlan)
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

    private sealed record StagedChatAsset(
        string AttachmentId,
        string FileName,
        string RelativePath,
        string ContentType,
        IReadOnlyList<int> ReferencedByWorkItemNumbers);

    private async Task<IReadOnlyList<StagedChatAsset>> StageReferencedChatAttachmentsAsync(
        IRepoSandbox sandbox,
        string projectId,
        string ownerId,
        Models.WorkItemDto workItem,
        List<Models.WorkItemDto> allDescendants,
        IChatSessionRepository scopedChatSessionRepository,
        IWorkItemAttachmentService scopedWorkItemAttachmentService,
        IChatAttachmentStorage scopedChatAttachmentStorage,
        CancellationToken cancellationToken)
    {
        var chatReferences = CollectReferencedChatAttachments(workItem, allDescendants);
        var workItemReferences = CollectReferencedWorkItemAttachments(workItem, allDescendants);
        if (chatReferences.Count == 0 && workItemReferences.Count == 0)
            return [];

        var stagedAssets = new List<StagedChatAsset>();
        foreach (var (attachmentId, workItemNumbers) in chatReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var attachment = await scopedChatSessionRepository.GetAttachmentRecordAsync(attachmentId, ownerId);
            if (attachment is null)
            {
                logger.LogWarning(
                    "Execution asset staging skipped unknown chat attachment {AttachmentId} for work item #{WorkItemNumber}",
                    attachmentId,
                    workItem.WorkItemNumber);
                continue;
            }

            byte[]? content = null;
            if (!string.IsNullOrWhiteSpace(attachment.StoragePath))
                content = await scopedChatAttachmentStorage.ReadAsync(attachment.StoragePath, cancellationToken);

            if (content is null)
                content = Encoding.UTF8.GetBytes(attachment.Content ?? string.Empty);

            if (content.Length == 0)
                continue;

            var safeFileName = SanitizeAttachmentFileName(attachment.FileName);
            var relativePath = $"{StagedChatAssetDirectory}/{attachmentId}-{safeFileName}";
            sandbox.WriteBinaryFile(relativePath, content);
            stagedAssets.Add(new StagedChatAsset(
                attachmentId,
                attachment.FileName,
                relativePath.Replace('\\', '/'),
                attachment.ContentType,
                workItemNumbers));
        }

        foreach (var (attachmentId, workItemNumbers) in workItemReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var attachment = await scopedWorkItemAttachmentService.GetAttachmentRecordAsync(projectId, attachmentId, cancellationToken);
            if (attachment is null)
            {
                logger.LogWarning(
                    "Execution asset staging skipped unknown work item attachment {AttachmentId} for work item #{WorkItemNumber}",
                    attachmentId,
                    workItem.WorkItemNumber);
                continue;
            }

            var content = string.IsNullOrWhiteSpace(attachment.StoragePath)
                ? null
                : await scopedChatAttachmentStorage.ReadAsync(attachment.StoragePath, cancellationToken);

            if (content is null || content.Length == 0)
                continue;

            var safeFileName = SanitizeAttachmentFileName(attachment.FileName);
            var relativePath = $"{StagedChatAssetDirectory}/{attachmentId}-{safeFileName}";
            sandbox.WriteBinaryFile(relativePath, content);
            stagedAssets.Add(new StagedChatAsset(
                attachmentId,
                attachment.FileName,
                relativePath.Replace('\\', '/'),
                attachment.ContentType,
                workItemNumbers));
        }

        return stagedAssets;
    }

    private static string BuildStagedChatAssetContext(IReadOnlyList<StagedChatAsset> stagedAssets)
    {
        if (stagedAssets.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("## Attached Assets");
        sb.AppendLine("The following user-uploaded assets were staged locally for this execution. Use these local files instead of the original remote attachment URLs when you need the asset contents.");
        sb.AppendLine();

        foreach (var asset in stagedAssets)
        {
            var referencedBy = string.Join(", ", asset.ReferencedByWorkItemNumbers.Select(number => $"#{number}"));
            sb.AppendLine($"- `{asset.RelativePath}` ({asset.ContentType}) from `{asset.FileName}`; referenced by {referencedBy}");
        }

        return sb.ToString().TrimEnd();
    }

    private static Dictionary<string, IReadOnlyList<int>> CollectReferencedChatAttachments(
        Models.WorkItemDto workItem,
        List<Models.WorkItemDto> allDescendants)
    {
        var references = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        void AddReferences(int workItemNumber, string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return;

            foreach (Match match in ChatAttachmentReferenceRegex.Matches(markdown))
            {
                if (!match.Success)
                    continue;

                var attachmentId = match.Groups["id"].Value;
                if (string.IsNullOrWhiteSpace(attachmentId))
                    continue;

                if (!references.TryGetValue(attachmentId, out var workItemNumbers))
                {
                    workItemNumbers = [];
                    references[attachmentId] = workItemNumbers;
                }

                workItemNumbers.Add(workItemNumber);
            }
        }

        AddReferences(workItem.WorkItemNumber, workItem.Description);
        AddReferences(workItem.WorkItemNumber, workItem.AcceptanceCriteria);
        foreach (var descendant in allDescendants)
        {
            AddReferences(descendant.WorkItemNumber, descendant.Description);
            AddReferences(descendant.WorkItemNumber, descendant.AcceptanceCriteria);
        }

        return references.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<int>)entry.Value.OrderBy(number => number).ToArray(),
            StringComparer.Ordinal);
    }

    private static Dictionary<string, IReadOnlyList<int>> CollectReferencedWorkItemAttachments(
        Models.WorkItemDto workItem,
        List<Models.WorkItemDto> allDescendants)
    {
        var references = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        void AddReferences(int workItemNumber, string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return;

            foreach (Match match in WorkItemAttachmentReferenceRegex.Matches(markdown))
            {
                if (!match.Success)
                    continue;

                var attachmentId = match.Groups["id"].Value;
                if (string.IsNullOrWhiteSpace(attachmentId))
                    continue;

                if (!references.TryGetValue(attachmentId, out var workItemNumbers))
                {
                    workItemNumbers = [];
                    references[attachmentId] = workItemNumbers;
                }

                workItemNumbers.Add(workItemNumber);
            }
        }

        AddReferences(workItem.WorkItemNumber, workItem.Description);
        AddReferences(workItem.WorkItemNumber, workItem.AcceptanceCriteria);
        foreach (var descendant in allDescendants)
        {
            AddReferences(descendant.WorkItemNumber, descendant.Description);
            AddReferences(descendant.WorkItemNumber, descendant.AcceptanceCriteria);
        }

        return references.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<int>)entry.Value.OrderBy(number => number).ToArray(),
            StringComparer.Ordinal);
    }

    private static string SanitizeAttachmentFileName(string fileName)
    {
        var leafName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(leafName))
            leafName = "attachment.bin";

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(leafName.Select(ch => invalidCharacters.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "attachment.bin" : sanitized;
    }

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

        if (!string.IsNullOrWhiteSpace(workItem.AcceptanceCriteria))
        {
            sb.AppendLine("## Acceptance Criteria");
            sb.AppendLine(workItem.AcceptanceCriteria);
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
            if (!string.IsNullOrWhiteSpace(child.AcceptanceCriteria))
                sb.AppendLine($"{indent}- **Acceptance Criteria**: {child.AcceptanceCriteria}");
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
        sb.AppendLine("- Send a progress update after every meaningful tool call and during longer thinking stretches.");
        sb.AppendLine("- `percent_complete` may be fractional. Prefer smaller realistic increments (for example 12.35, 48.7, 83.15) instead of whole-percent jumps.");
        sb.AppendLine("- Include clear milestones (for example: analysis done, implementation started, tests passing).");
        sb.AppendLine("- Do not jump to 99-100% early. Reserve 100% for true completion.");
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

    internal static AgentRole[][] ResolveDefaultPipeline(string? executionMode)
        => executionMode switch
        {
            AgentExecutionModes.Orchestration => OrchestrationPreludePipeline,
            AgentExecutionModes.Coordinator => CoordinatorPipeline,
            _ => FullPipeline,
        };

    internal static AgentRole[][] ApplyAssignedAgentLimit(AgentRole[][] pipeline, string? assignmentMode, int? assignedAgentCount)
    {
        var effectiveAssignedAgentCount = ResolveEffectiveAssignedAgentCount(assignmentMode, assignedAgentCount);
        if (effectiveAssignedAgentCount is null)
            return pipeline;

        var workerLimit = effectiveAssignedAgentCount.Value;
        var workerRoles = pipeline
            .SelectMany(group => group)
            .Where(role => role is not AgentRole.Manager and not AgentRole.Planner)
            .Distinct()
            .ToList();

        if (workerRoles.Count <= workerLimit)
            return pipeline;

        var retainedWorkers = workerRoles
            .OrderBy(role => LimitedPipelineRolePriority.TryGetValue(role, out var priority) ? priority : int.MaxValue)
            .ThenBy(role => role.ToString())
            .Take(workerLimit)
            .ToHashSet();

        return pipeline
            .Select(group => group.Where(role =>
                role is AgentRole.Manager or AgentRole.Planner || retainedWorkers.Contains(role)).ToArray())
            .Where(group => group.Length > 0)
            .ToArray();
    }

    internal static int ResolveMaxConcurrentAgentsPerTask(int tierLimit, string? assignmentMode, int? assignedAgentCount)
    {
        var effectiveAssignedAgentCount = ResolveEffectiveAssignedAgentCount(assignmentMode, assignedAgentCount);
        if (effectiveAssignedAgentCount is null)
            return tierLimit;

        return Math.Max(1, Math.Min(tierLimit, effectiveAssignedAgentCount.Value));
    }

    internal static int? ResolveEffectiveAssignedAgentCount(string? assignmentMode, int? assignedAgentCount)
    {
        if (!string.Equals(assignmentMode, "manual", StringComparison.OrdinalIgnoreCase))
            return null;

        if (assignedAgentCount is null || assignedAgentCount.Value <= 0)
            return null;

        return assignedAgentCount.Value;
    }

    private sealed class ExecutionDbRequestQueue : IAsyncDisposable
    {
        private readonly Channel<IQueuedDbRequest> _channel = Channel.CreateUnbounded<IQueuedDbRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

        private readonly Task _processorTask;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ExecutionDbRequestQueue(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _processorTask = Task.Run(ProcessAsync);
        }

        public async Task EnqueueAsync(Func<FleetDbContext, Task> action)
        {
            var request = new QueuedDbRequest<object?>(async () =>
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var queuedDb = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
                await action(queuedDb);
                return null;
            });

            await _channel.Writer.WriteAsync(request);
            await request.Completion;
        }

        public async Task<T> ExecuteReadAsync<T>(Func<FleetDbContext, Task<T>> action)
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var queuedDb = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            return await action(queuedDb);
        }

        public async ValueTask DisposeAsync()
        {
            _channel.Writer.TryComplete();
            await _processorTask;
        }

        private async Task ProcessAsync()
        {
            await foreach (var request in _channel.Reader.ReadAllAsync())
            {
                await request.ExecuteAsync();
            }
        }

        private interface IQueuedDbRequest
        {
            Task ExecuteAsync();
        }

        private sealed class QueuedDbRequest<T>(Func<Task<T>> action) : IQueuedDbRequest
        {
            private readonly TaskCompletionSource<T> _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<T> Completion => _completion.Task;

            public async Task ExecuteAsync()
            {
                try
                {
                    var result = await action();
                    _completion.TrySetResult(result);
                }
                catch (OperationCanceledException canceled) when (canceled.CancellationToken.CanBeCanceled)
                {
                    _completion.TrySetCanceled(canceled.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _completion.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    _completion.TrySetException(ex);
                }
            }
        }
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
            : Math.Clamp((double)preservedRoleCount / totalRoleCount, 0, IncompleteProgressCeiling);

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
                sb.AppendLine($"- Last estimated completion: {FormatProgressPercent(attempt.EstimatedCompletionPercent)}% ({retrySummary})");
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
        sb.AppendLine($"- Last recorded completion: {FormatProgressPercent(Math.Clamp(priorExecution.Progress, 0, 1) * 100)}%");
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
        sb.AppendLine($"- Last recorded completion: {FormatProgressPercent(Math.Clamp(pausedExecution.Progress, 0, 1) * 100)}%");
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

    private static string BuildRetryFailureTask(double estimatedCompletionPercent, string? lastProgressSummary)
    {
        if (estimatedCompletionPercent <= 0)
            return "Failed";

        var summary = string.IsNullOrWhiteSpace(lastProgressSummary)
            ? null
            : lastProgressSummary.Trim();

        return string.IsNullOrWhiteSpace(summary)
            ? $"Failed at {FormatProgressPercent(estimatedCompletionPercent)}%"
            : $"Failed at {FormatProgressPercent(estimatedCompletionPercent)}%: {summary}";
    }

    private static string FormatProgressPercent(double percent)
    {
        var rounded = Math.Round(Math.Clamp(percent, 0, 100), 2, MidpointRounding.AwayFromZero);
        var format = rounded % 1 == 0
            ? "0"
            : rounded % 0.1 == 0
                ? "0.0"
                : "0.##";
        return rounded.ToString(format, CultureInfo.InvariantCulture);
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
        public const string Fast = "Fast";
        public const string Standard = "Standard";
        public const string Premium = "Premium";
    }

    /// <summary>
    /// Summarizes a phase's output into a compact form using a cheap Fast-tier call.
    /// This dramatically reduces the context window size for downstream phases,
    /// cutting token costs proportionally to the number of phases.
    /// </summary>
    private async Task<string> SummarizePhaseOutputAsync(
        AgentRole role, string fullOutput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullOutput) || fullOutput.Length < 500)
            return fullOutput; // Not worth summarizing tiny outputs

        var summaryModel = modelCatalog.Get(ModelKeys.Fast);
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
                agent.Progress = Math.Clamp(Math.Max(agent.Progress, preservedProgress), 0, IncompleteProgressCeiling);
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
            agent.Progress = Math.Clamp(Math.Max(agent.Progress, preservedProgress), 0, IncompleteProgressCeiling);
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

    private static bool IsBranchCleanupEligibleStatus(string? status)
        => string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutionDeleted(string executionId)
        => DeletedExecutions.ContainsKey(executionId);

    private static void ThrowIfExecutionDeleted(string executionId)
    {
        if (IsExecutionDeleted(executionId))
            throw new OperationCanceledException($"Execution {executionId} was deleted.");
    }

    /// <summary>
    /// Opens a pull request on GitHub for the current execution branch.
    /// When requested, seeds the branch with an initial marker commit before opening the PR.
    /// </summary>
    private async Task<(string? Url, int Number)> OpenPullRequestAsync(
        IRepoSandbox sandbox, string accessToken, string repoFullName,
        WorkItemDto workItem, string commitAuthorName, string commitAuthorEmail,
        string pullRequestTargetBranch,
        FleetDbContext scopedDb, string executionId,
        CancellationToken cancellationToken,
        bool draft,
        bool seedBranchWithMarkerCommit)
    {
        try
        {
            if (seedBranchWithMarkerCommit)
            {
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
            }

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

            var openResult = await CreatePullRequestAsync(
                client, accessToken, repoFullName, sandbox.BranchName, baseBranch, workItem, resolvedPrTitle, draft, cancellationToken);

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

                    openResult = await CreatePullRequestAsync(
                        client, accessToken, repoFullName, sandbox.BranchName, baseBranch, workItem, retriedTitle, draft, cancellationToken);
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
                        "PR creation returned {Status}; reusing existing PR #{PrNumber}: {PrUrl}",
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

            logger.LogInformation("Opened {DraftState}PR #{PrNumber}: {PrUrl}", draft ? "draft " : string.Empty, prNumber, prUrl);

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
            logger.LogError(ex, "Failed to open pull request for branch {Branch}", sandbox.BranchName);
            throw;
        }
    }

    private async Task<PullRequestCreateResult> CreatePullRequestAsync(
        HttpClient client,
        string accessToken,
        string repoFullName,
        string headBranch,
        string baseBranch,
        WorkItemDto workItem,
        string prTitle,
        bool draft,
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
            draft,
        });

        using var prRequest = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.github.com/repos/{repoFullName}/pulls");
        prRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        prRequest.Headers.UserAgent.ParseAdd("Fleet/1.0");
        prRequest.Content = new StringContent(prPayload, Encoding.UTF8, "application/json");

        using var prResponse = await client.SendAsync(prRequest, cancellationToken);
        var prResponseBody = await prResponse.Content.ReadAsStringAsync(cancellationToken);

        return new PullRequestCreateResult(
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

    private async Task CleanupStaleTopLevelBranchesAsync(
        string projectId,
        int workItemNumber,
        string accessToken,
        string repoFullName,
        string protectedBranchName,
        CancellationToken cancellationToken)
    {
        var staleBranches = await db.AgentExecutions
            .AsNoTracking()
            .Where(execution =>
                execution.ProjectId == projectId &&
                execution.WorkItemId == workItemNumber &&
                execution.ParentExecutionId == null &&
                execution.BranchName != null &&
                (execution.Status == "failed" || execution.Status == "cancelled"))
            .Select(execution => execution.BranchName!)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var staleBranch in staleBranches)
        {
            await TryDeleteRemoteBranchIfSafeAsync(
                accessToken,
                repoFullName,
                staleBranch,
                cancellationToken,
                protectedBranchName,
                projectId: projectId);
        }
    }

    private async Task TryDeleteRemoteBranchIfSafeAsync(
        string accessToken,
        string repoFullName,
        string? branchName,
        CancellationToken cancellationToken,
        string? protectedBranchName = null,
        string? projectId = null,
        IReadOnlyCollection<string>? excludedExecutionIds = null)
    {
        var normalizedBranchName = branchName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBranchName))
            return;

        if (ProtectedBranchNames.Contains(normalizedBranchName))
            return;

        if (!string.IsNullOrWhiteSpace(protectedBranchName) &&
            string.Equals(normalizedBranchName, protectedBranchName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(projectId))
        {
            var branchStillInUse = await db.AgentExecutions
                .AsNoTracking()
                .Where(execution =>
                    execution.ProjectId == projectId &&
                    execution.BranchName == normalizedBranchName &&
                    (execution.Status == "running" || execution.Status == "paused"))
                .Where(execution => excludedExecutionIds == null || !excludedExecutionIds.Contains(execution.Id))
                .AnyAsync(cancellationToken);
            if (branchStillInUse)
            {
                logger.LogInformation(
                    "Skipping cleanup of branch {Branch} because another recoverable execution still references it",
                    normalizedBranchName);
                return;
            }
        }

        try
        {
            var openPr = await FindOpenPullRequestByHeadBranchAsync(
                accessToken,
                repoFullName,
                normalizedBranchName,
                cancellationToken);
            if (openPr.Number > 0)
            {
                logger.LogInformation(
                    "Skipping cleanup of branch {Branch} because it still has an open PR ({PullRequestUrl})",
                    normalizedBranchName,
                    openPr.Url);
                return;
            }

            var client = httpClientFactory.CreateClient("GitHub");
            var deleted = await DeleteRemoteBranchAsync(
                client,
                accessToken,
                repoFullName,
                normalizedBranchName,
                cancellationToken);
            if (deleted)
            {
                logger.LogInformation(
                    "Deleted stale Fleet branch {Branch} in repository {RepoFullName}",
                    normalizedBranchName,
                    repoFullName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to clean up stale Fleet branch {Branch} in repository {RepoFullName}",
                normalizedBranchName,
                repoFullName);
        }
    }

    private static async Task<bool> DeleteRemoteBranchAsync(
        HttpClient client,
        string accessToken,
        string repoFullName,
        string branchName,
        CancellationToken cancellationToken)
    {
        var encodedBranch = Uri.EscapeDataString(branchName);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://api.github.com/repos/{repoFullName}/git/refs/heads/{encodedBranch}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"GitHub branch cleanup failed for '{branchName}': {TryExtractGitHubApiErrorMessage(details) ?? details}");
        }

        return true;
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

    internal static bool ShouldCreatePullRequestForExecution(string? parentExecutionId)
        => string.IsNullOrWhiteSpace(parentExecutionId);

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

    private sealed record PullRequestCreateResult(
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
            var repoParts = repoFullName.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (repoParts.Length != 2)
            {
                logger.LogWarning("Failed to mark PR #{PrNumber} as ready because repository name '{RepoFullName}' is invalid", prNumber, repoFullName);
                return;
            }

            var owner = repoParts[0];
            var name = repoParts[1];

            var lookupPayload = JsonSerializer.Serialize(new
            {
                query = """
                    query PullRequestNode($owner: String!, $name: String!, $number: Int!) {
                      repository(owner: $owner, name: $name) {
                        pullRequest(number: $number) {
                          id
                          isDraft
                        }
                      }
                    }
                    """,
                variables = new { owner, name, number = prNumber },
            });

            using var lookupRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
            lookupRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            lookupRequest.Headers.UserAgent.ParseAdd("Fleet/1.0");
            lookupRequest.Content = new StringContent(lookupPayload, Encoding.UTF8, "application/json");

            using var lookupResponse = await client.SendAsync(lookupRequest, cancellationToken);
            var lookupBody = await lookupResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!lookupResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to look up PR #{PrNumber} before marking ready: {Status} {Error}",
                    prNumber,
                    lookupResponse.StatusCode,
                    TryExtractGitHubGraphQlErrorMessage(lookupBody) ?? TryExtractGitHubApiErrorMessage(lookupBody));
                return;
            }

            var lookupJson = JsonSerializer.Deserialize<JsonElement>(lookupBody);
            if (!TryGetPullRequestGraphQlNode(lookupJson, out var prNodeId, out var isDraft))
            {
                logger.LogWarning("Failed to resolve a GraphQL node id for PR #{PrNumber}", prNumber);
                return;
            }

            if (!isDraft)
            {
                logger.LogInformation("PR #{PrNumber} is already open for review", prNumber);
                return;
            }

            var mutationPayload = JsonSerializer.Serialize(new
            {
                query = """
                    mutation MarkReady($pullRequestId: ID!) {
                      markPullRequestReadyForReview(input: { pullRequestId: $pullRequestId }) {
                        pullRequest {
                          number
                          isDraft
                        }
                      }
                    }
                    """,
                variables = new { pullRequestId = prNodeId },
            });

            using var mutationRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
            mutationRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            mutationRequest.Headers.UserAgent.ParseAdd("Fleet/1.0");
            mutationRequest.Content = new StringContent(mutationPayload, Encoding.UTF8, "application/json");

            using var mutationResponse = await client.SendAsync(mutationRequest, cancellationToken);
            var mutationBody = await mutationResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!mutationResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to mark PR #{PrNumber} as ready: {Status} {Error}",
                    prNumber,
                    mutationResponse.StatusCode,
                    TryExtractGitHubGraphQlErrorMessage(mutationBody) ?? TryExtractGitHubApiErrorMessage(mutationBody));
                return;
            }

            var lifecycle = await GetPullRequestLifecycleAsync(accessToken, repoFullName, prNumber, cancellationToken);
            if (lifecycle?.IsDraft == true)
            {
                logger.LogWarning("PR #{PrNumber} still appears to be draft after the ready-for-review mutation", prNumber);
                return;
            }

            logger.LogInformation("Marked PR #{PrNumber} as ready for review", prNumber);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark PR #{PrNumber} as ready for review (non-fatal)", prNumber);
        }
    }

    private static bool TryGetPullRequestGraphQlNode(JsonElement payload, out string? nodeId, out bool isDraft)
    {
        nodeId = null;
        isDraft = false;

        if (payload.TryGetProperty("errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array &&
            errors.GetArrayLength() > 0)
        {
            return false;
        }

        if (!payload.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("repository", out var repository) ||
            repository.ValueKind == JsonValueKind.Null ||
            !repository.TryGetProperty("pullRequest", out var pullRequest) ||
            pullRequest.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        nodeId = pullRequest.TryGetProperty("id", out var nodeProp) ? nodeProp.GetString() : null;
        isDraft = pullRequest.TryGetProperty("isDraft", out var draftProp) && draftProp.ValueKind == JsonValueKind.True;
        return !string.IsNullOrWhiteSpace(nodeId);
    }

    private static string? TryExtractGitHubGraphQlErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            var payload = JsonSerializer.Deserialize<JsonElement>(responseBody);
            if (!payload.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
                return null;

            var messages = errors.EnumerateArray()
                .Select(error => error.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : null)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!.Trim())
                .ToArray();

            return messages.Length == 0 ? null : string.Join("; ", messages);
        }
        catch (JsonException)
        {
            return null;
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
