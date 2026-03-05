using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fleet.Server.Connections;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.LLM;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Agents;

/// <summary>
/// Coordinates the sequential agent pipeline: clone repo → run phases → create PR → clean up.
/// Each phase receives the outputs of all previous phases as context.
/// </summary>
public class AgentOrchestrationService(
    FleetDbContext db,
    IConnectionRepository connectionRepository,
    IWorkItemRepository workItemRepository,
    IServiceScopeFactory serviceScopeFactory,
    ILLMClient llmClient,
    IHttpClientFactory httpClientFactory,
    ILogger<AgentOrchestrationService> logger,
    IModelCatalog modelCatalog) : IAgentOrchestrationService
{
    /// <summary>
    /// Tracks CancellationTokenSources for active executions so they can be cancelled/paused externally.
    /// Key = executionId, Value = (CTS, desired final status when cancelled).
    /// </summary>
    private static readonly ConcurrentDictionary<string, (CancellationTokenSource Cts, string FinalStatus)> ActiveExecutions = new();

    /// <summary>
    /// The ordered pipeline phases. Implementation phases run sequentially within their group.
    /// </summary>
    private static readonly AgentRole[][] FullPipeline =
    [
        // Phase 1 — Planning
        [AgentRole.Planner],
        // Phase 2 — Contracts / interfaces
        [AgentRole.Contracts],
        // Phase 3 — Implementation (sequential: Backend → Frontend → Testing → Styling)
        [AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling],
        // Phase 4 — Consolidation
        [AgentRole.Consolidation],
        // Phase 5 — Review & documentation
        [AgentRole.Review, AgentRole.Documentation],
    ];

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
        // Ensure Planner is always present
        if (!roles.Contains(AgentRole.Planner))
            roles.Insert(0, AgentRole.Planner);

        // Canonical ordering: Planner → Contracts → [Backend, Frontend, Testing, Styling] → Consolidation → [Review, Documentation]
        var pipeline = new List<AgentRole[]>();

        // Group 1: Planner
        pipeline.Add([AgentRole.Planner]);

        // Group 2: Contracts (if selected)
        if (roles.Contains(AgentRole.Contracts))
            pipeline.Add([AgentRole.Contracts]);

        // Group 3: Implementation agents (Backend, Frontend, Testing, Styling — in order, if selected)
        var implGroup = new List<AgentRole>();
        foreach (var role in new[] { AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling })
        {
            if (roles.Contains(role))
                implGroup.Add(role);
        }
        if (implGroup.Count > 0)
            pipeline.Add([.. implGroup]);

        // Group 4: Consolidation (if selected and both Backend+Frontend are in the pipeline)
        if (roles.Contains(AgentRole.Consolidation) &&
            roles.Contains(AgentRole.Backend) && roles.Contains(AgentRole.Frontend))
            pipeline.Add([AgentRole.Consolidation]);

        // Group 5: Review and Documentation (if selected)
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

    public async Task<string> StartExecutionAsync(
        string projectId, int workItemNumber, int userId, CancellationToken cancellationToken = default)
    {
        // 1. Load the work item and ALL of its descendants (recursively)
        var workItem = await workItemRepository.GetByWorkItemNumberAsync(projectId, workItemNumber)
            ?? throw new InvalidOperationException($"Work item #{workItemNumber} not found in project {projectId}.");

        var childWorkItems = new List<Models.WorkItemDto>();
        await CollectDescendantsAsync(projectId, workItem.ChildWorkItemNumbers, childWorkItems);

        // 2. Load the project to get the repo name
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        if (string.IsNullOrWhiteSpace(project.Repo))
            throw new InvalidOperationException("Project has no linked repository.");

        // 3. Get the user's GitHub access token
        var linkedAccount = await connectionRepository.GetByProviderAsync(userId, "GitHub")
            ?? throw new InvalidOperationException("No GitHub account linked. Please connect your GitHub account first.");

        if (string.IsNullOrWhiteSpace(linkedAccount.AccessToken))
            throw new InvalidOperationException("GitHub access token is missing. Please re-link your GitHub account.");

        var accessToken = linkedAccount.AccessToken;
        var repoFullName = project.Repo;

        // 4. Use AI to determine which agents are needed for this work item.
        //    Always use Haiku for cost savings.
        var selectedModelKey = ModelKeys.Haiku;
        var workItemContext = BuildWorkItemContext(workItem, childWorkItems);
        var pipeline = await SelectPipelineAsync(workItemContext, cancellationToken);
        logger.LogInformation(
            "Execution: AI-selected pipeline with {PhaseCount} agents, model={Model}",
            pipeline.SelectMany(g => g).Count(), selectedModelKey);

        // 5. Create the execution record
        var executionId = Guid.NewGuid().ToString("N")[..12];
        var branchName = $"fleet/{workItemNumber}-{Slugify(workItem.Title)}";

        var execution = new AgentExecution
        {
            Id = executionId,
            WorkItemId = workItemNumber,
            WorkItemTitle = workItem.Title,
            Status = "running",
            StartedAt = DateTime.UtcNow.ToString("o"),
            StartedAtUtc = DateTime.UtcNow,
            Progress = 0,
            BranchName = branchName,
            CurrentPhase = "Initializing",
            UserId = userId.ToString(),
            ProjectId = projectId,
            Agents = BuildAgentInfoList(pipeline),
        };

        db.AgentExecutions.Add(execution);
        await db.SaveChangesAsync(cancellationToken);

        // 6. Mark the work item as in-progress
        await workItemRepository.UpdateAsync(projectId, workItemNumber,
            new UpdateWorkItemRequest(
                Title: null, Description: null, Priority: null, Difficulty: null,
                State: "Active", AssignedTo: null, Tags: null, IsAI: null,
                ParentWorkItemNumber: null, LevelId: null));

        // 7. Write an initial log entry
        await WriteLogEntryAsync(db, projectId, "System", "info",
            $"Execution {executionId} started for work item #{workItemNumber}: {workItem.Title}");

        // 8. Fire-and-forget the pipeline on a background thread
        //    We use IServiceScopeFactory (singleton) to create a new scope so the DbContext isn't shared.
        var cts = new CancellationTokenSource();
        ActiveExecutions[executionId] = (cts, "cancelled");
        _ = Task.Run(() => RunPipelineAsync(
            executionId, projectId, workItem, childWorkItems, repoFullName,
            accessToken, branchName, userId, selectedModelKey, pipeline, cts.Token), CancellationToken.None);

        return executionId;
    }

    public async Task<AgentExecutionStatus?> GetExecutionStatusAsync(
        string executionId, CancellationToken cancellationToken = default)
    {
        var exec = await db.AgentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

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

    public Task<bool> CancelExecutionAsync(string executionId)
    {
        return StopExecutionAsync(executionId, "cancelled");
    }

    public Task<bool> PauseExecutionAsync(string executionId)
    {
        return StopExecutionAsync(executionId, "paused");
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
        string repoFullName, string accessToken, string branchName,
        int userId, string selectedModelKey, AgentRole[][] pipeline,
        CancellationToken externalCancellation)
    {
        // Use IServiceScopeFactory (singleton) instead of the request-scoped IServiceProvider.
        // The HTTP request scope is disposed after the controller returns Accepted(),
        // so a scoped IServiceProvider would throw ObjectDisposedException here.
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        var scopedPhaseRunner = scope.ServiceProvider.GetRequiredService<IAgentPhaseRunner>();
        var scopedWorkItemRepo = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();

        IRepoSandbox? sandbox = null;

        try
        {
            var model = modelCatalog.Get(selectedModelKey);
            logger.LogInformation("Execution {ExecutionId}: using model {Model} (key={ModelKey})",
                executionId, model, selectedModelKey);

            // Create and clone the sandbox
            sandbox = scope.ServiceProvider.GetRequiredService<IRepoSandbox>();
            logger.LogInformation("Execution {ExecutionId}: cloning {Repo} → branch {Branch}",
                executionId, repoFullName, branchName);

            await sandbox.CloneAsync(repoFullName, accessToken, branchName);

            var toolContext = new AgentToolContext(
                sandbox, projectId, userId.ToString(), accessToken, repoFullName, executionId);

            // Open a draft PR immediately so agent commits are visible throughout development
            var (prUrl, prNumber) = await OpenDraftPullRequestAsync(
                sandbox, accessToken, repoFullName, workItem, scopedDb, executionId, externalCancellation);

            // Build the initial user message with work item context (includes children)
            var workItemContext = BuildWorkItemContext(workItem, childWorkItems);
            var phaseOutputs = new List<(AgentRole Role, string Output)>();

            var totalRoles = pipeline.SelectMany(g => g).Count();
            var completedRoles = 0;
            var phaseOrder = 0;

            foreach (var group in pipeline)
            {
                foreach (var role in group)
                {
                    // Check for external cancellation (stop/pause) before starting each phase
                    externalCancellation.ThrowIfCancellationRequested();

                    // Update execution status
                    await UpdateExecutionAsync(scopedDb, executionId, role.ToString(),
                        (double)completedRoles / totalRoles);

                    // Mark this agent as running with a meaningful task description
                    await SetAgentRunningAsync(scopedDb, executionId, role.ToString(),
                        GetPhaseTaskDescription(role));

                    // Write a log entry for phase start
                    await WriteLogEntryAsync(scopedDb, projectId, $"{role} Agent", "info",
                        $"Starting phase: {GetPhaseTaskDescription(role)}");

                    // Build the message for this phase: work item context + all prior outputs
                    var userMessage = BuildPhaseMessage(role, workItemContext, phaseOutputs);

                    logger.LogInformation("Execution {ExecutionId}: starting phase {Role}",
                        executionId, role);

                    // Progress callback: flushes tool-call progress to the DB so the polling
                    // endpoint returns live data instead of waiting until the phase finishes.
                    PhaseProgressCallback onProgress = async (estimatedProgress, summary) =>
                    {
                        var overallProgress = ((double)completedRoles + estimatedProgress) / totalRoles;

                        var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
                        if (exec is null) return;

                        exec.Progress = Math.Clamp(overallProgress, 0, 0.99);

                        var agent = exec.Agents.FirstOrDefault(a => a.Role == role.ToString());
                        if (agent is not null)
                        {
                            agent.CurrentTask = summary;
                            agent.Progress = Math.Clamp(estimatedProgress, 0, 0.99);
                        }

                        await scopedDb.SaveChangesAsync();

                        // Write a log entry so progress reports appear in the execution logs
                        var pct = (int)Math.Round(estimatedProgress * 100);
                        var logMessage = string.IsNullOrWhiteSpace(summary)
                            ? $"Progress: {pct}%"
                            : $"Progress: {pct}% — {summary}";
                        await WriteLogEntryAsync(scopedDb, projectId, $"{role} Agent", "info", logMessage);
                    };

                    // Tool-call logger: writes each tool invocation as a detailed log entry
                    PhaseToolCallLogger onToolCall = async (toolName, resultSnippet) =>
                    {
                        var logMsg = $"Tool: {toolName} → {resultSnippet}";
                        await WriteLogEntryAsync(scopedDb, projectId, $"{role} Agent", "info", logMsg, isDetailed: true);
                    };

                    var maxTokens = GetMaxTokensForRole(role);
                    var phaseStart = DateTime.UtcNow;
                    var result = await scopedPhaseRunner.RunPhaseAsync(role, userMessage, toolContext, model, maxTokens, onProgress, onToolCall, externalCancellation);
                    var phaseEnd = DateTime.UtcNow;

                    // Persist the phase result
                    var phaseResultEntity = new AgentPhaseResult
                    {
                        Role = role.ToString(),
                        Output = result.Output,
                        ToolCallCount = result.ToolCallCount,
                        Success = result.Success,
                        Error = result.Error,
                        StartedAt = phaseStart,
                        CompletedAt = phaseEnd,
                        PhaseOrder = phaseOrder++,
                        ExecutionId = executionId,
                    };
                    scopedDb.AgentPhaseResults.Add(phaseResultEntity);
                    await scopedDb.SaveChangesAsync();

                    // Update the agent info status
                    await UpdateAgentInfoAsync(scopedDb, executionId, role.ToString(),
                        result.Success ? "completed" : "failed", result.ToolCallCount);

                    // Write a log entry for phase completion
                    if (result.Success)
                    {
                        await WriteLogEntryAsync(scopedDb, projectId, $"{role} Agent", "success",
                            $"Phase completed ({result.ToolCallCount} tool calls)");
                    }
                    else
                    {
                        await WriteLogEntryAsync(scopedDb, projectId, $"{role} Agent", "error",
                            $"Phase failed: {result.Error ?? "Unknown error"}");

                        logger.LogWarning("Execution {ExecutionId}: phase {Role} failed: {Error}",
                            executionId, role, result.Error);
                        // Continue — downstream phases can still attempt recovery
                    }

                    // Summarize the phase output before passing to downstream phases.
                    // The full output is already saved to AgentPhaseResult above for debugging.
                    var summarized = await SummarizePhaseOutputAsync(role, result.Output);
                    phaseOutputs.Add((role, summarized));
                    completedRoles++;
                }
            }

            // Pipeline complete — final commit + push so the draft PR has all changes
            try
            {
                await sandbox.CommitAndPushAsync(
                    $"fleet: finalize changes for #{workItem.WorkItemNumber}",
                    authorName: "Fleet Agent",
                    authorEmail: "agent@fleet.dev",
                    externalCancellation);
            }
            catch (Exception pushEx)
            {
                logger.LogWarning(pushEx, "Final push failed (changes may already be pushed)");
            }

            // Mark the draft PR as ready for review
            if (prNumber > 0)
            {
                await MarkPullRequestReadyAsync(accessToken, repoFullName, prNumber, externalCancellation);
            }

            await FinalizeExecutionAsync(scopedDb, executionId, "completed", prUrl);

            // Update work item state to reflect AI completion
            await scopedWorkItemRepo.UpdateAsync(projectId, workItem.WorkItemNumber,
                new UpdateWorkItemRequest(
                    Title: null, Description: null, Priority: null, Difficulty: null,
                    State: "Resolved (AI)", AssignedTo: null, Tags: null, IsAI: null,
                    ParentWorkItemNumber: null, LevelId: null));

            await WriteLogEntryAsync(scopedDb, projectId, "System", "success",
                $"Execution {executionId} completed successfully" +
                (prUrl is not null ? $" — PR: {prUrl}" : ""));

            logger.LogInformation("Execution {ExecutionId}: pipeline completed successfully", executionId);
        }
        catch (OperationCanceledException)
        {
            // Execution was stopped or paused externally via CancelExecutionAsync / PauseExecutionAsync.
            var finalStatus = ActiveExecutions.TryGetValue(executionId, out var entry) ? entry.FinalStatus : "cancelled";
            logger.LogInformation("Execution {ExecutionId}: pipeline {Status} by user", executionId, finalStatus);

            try
            {
                await FinalizeExecutionAsync(scopedDb, executionId, finalStatus);
                await WriteLogEntryAsync(scopedDb, projectId, "System", "warn",
                    $"Execution {executionId} was {finalStatus} by user");
            }
            catch (Exception dbEx)
            {
                logger.LogError(dbEx, "Execution {ExecutionId}: failed to persist {Status} status to DB", executionId, finalStatus);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Execution {ExecutionId}: pipeline failed with exception", executionId);

            // Wrap error-handling DB writes in their own try/catch so a secondary failure
            // (e.g., broken DB connection) doesn't mask the original error.
            try
            {
                await FinalizeExecutionAsync(scopedDb, executionId, "failed", errorMessage: ex.Message);
                await WriteLogEntryAsync(scopedDb, projectId, "System", "error",
                    $"Execution {executionId} failed: {ex.Message}");
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

            if (sandbox is not null)
                await sandbox.DisposeAsync();
        }
    }

    /// <summary>
    /// Recursively collects all descendants of a work item (children, grandchildren, etc.).
    /// </summary>
    private async Task CollectDescendantsAsync(string projectId, int[] childNumbers, List<Models.WorkItemDto> results)
    {
        foreach (var childNumber in childNumbers)
        {
            var child = await workItemRepository.GetByWorkItemNumberAsync(projectId, childNumber);
            if (child is null) continue;

            results.Add(child);

            if (child.ChildWorkItemNumbers.Length > 0)
                await CollectDescendantsAsync(projectId, child.ChildWorkItemNumbers, results);
        }
    }

    /// <summary>
    /// Builds the work item context string that serves as the base input for all phases.
    /// Includes the parent work item and all its descendants with full details.
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
        List<(AgentRole Role, string Output)> priorOutputs)
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

        sb.AppendLine($"You are the **{role}** agent. Execute your role as described in your system prompt.");
        sb.AppendLine("Use your tools to explore the repository, understand the codebase, and make the necessary changes.");
        sb.AppendLine();
        sb.AppendLine("**IMPORTANT — A draft PR is already open. Use `commit_and_push` frequently to save progress.**");
        sb.AppendLine();
        sb.AppendLine("**Speed & Cost Constraints:**");
        sb.AppendLine("- Be extremely concise in your reasoning and output. No filler, no restating the problem.");
        sb.AppendLine("- Return ONLY the essential information: files changed, key decisions, errors, and instructions for the next phase.");
        sb.AppendLine("- Do NOT echo file contents you read — summarize what you learned in 1-2 sentences.");
        sb.AppendLine("- When writing code, write only the changed/new code — do not repeat unchanged sections.");
        sb.AppendLine("- **Call multiple tools at once** whenever possible. For example, read 3-5 files in a single response instead of one at a time. This runs them in parallel and is MUCH faster.");
        sb.AppendLine("- Plan your exploration: list the directory first, then read all relevant files in one batch.");
        sb.AppendLine("- Prefer search_files over reading entire files when you only need to find specific patterns.");

        return sb.ToString();
    }

    private static List<AgentInfo> BuildAgentInfoList(AgentRole[][] pipeline)
    {
        return pipeline.SelectMany(g => g).Select(role => new AgentInfo
        {
            Role = role.ToString(),
            Status = "idle",
            CurrentTask = "Waiting",
            Progress = 0,
        }).ToList();
    }

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
        var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
        if (exec is null) return;

        exec.CurrentPhase = currentPhase;
        exec.Progress = progress;
        await scopedDb.SaveChangesAsync();
    }

    private static async Task UpdateAgentInfoAsync(
        FleetDbContext scopedDb, string executionId, string role, string status, int toolCallCount)
    {
        var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
        if (exec is null) return;

        var agent = exec.Agents.FirstOrDefault(a => a.Role == role);
        if (agent is not null)
        {
            agent.Status = status;
            agent.CurrentTask = status == "completed"
                ? $"Done ({toolCallCount} tool calls)"
                : $"Failed";
            agent.Progress = status == "completed" ? 1.0 : 0;
        }

        await scopedDb.SaveChangesAsync();
    }

    /// <summary>
    /// Marks an agent as "running" with a descriptive task before its phase executes.
    /// </summary>
    private static async Task SetAgentRunningAsync(
        FleetDbContext scopedDb, string executionId, string role, string taskDescription)
    {
        var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
        if (exec is null) return;

        var agent = exec.Agents.FirstOrDefault(a => a.Role == role);
        if (agent is not null)
        {
            agent.Status = "running";
            agent.CurrentTask = taskDescription;
            agent.Progress = 0;
        }

        await scopedDb.SaveChangesAsync();
    }

    /// <summary>
    /// Writes a log entry to the database for real-time pipeline observability.
    /// </summary>
    private static async Task WriteLogEntryAsync(
        FleetDbContext scopedDb, string projectId, string agent, string level, string message, bool isDetailed = false)
    {
        scopedDb.LogEntries.Add(new LogEntry
        {
            Time = DateTime.UtcNow.ToString("o"),
            Agent = agent,
            Level = level,
            Message = message,
            IsDetailed = isDetailed,
            ProjectId = projectId,
        });
        await scopedDb.SaveChangesAsync();
    }

    /// <summary>
    /// Returns a human-readable description of what each agent role does.
    /// </summary>
    private static string GetPhaseTaskDescription(AgentRole role) => role switch
    {
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

    private static async Task FinalizeExecutionAsync(
        FleetDbContext scopedDb, string executionId, string status,
        string? prUrl = null, string? errorMessage = null)
    {
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

        await scopedDb.SaveChangesAsync();
    }

    /// <summary>
    /// Opens a draft pull request on GitHub at the start of the pipeline.
    /// Creates an initial marker commit and pushes the branch so the PR can be opened.
    /// Agents push subsequent commits throughout development — the PR updates automatically.
    /// </summary>
    private async Task<(string? Url, int Number)> OpenDraftPullRequestAsync(
        IRepoSandbox sandbox, string accessToken, string repoFullName,
        WorkItemDto workItem, FleetDbContext scopedDb, string executionId,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Create an initial marker commit so the branch has something to push
            sandbox.WriteFile(".fleet",
                $"Fleet execution for work item #{workItem.WorkItemNumber}: {workItem.Title}\n" +
                $"Started: {DateTime.UtcNow:O}\nBranch: {sandbox.BranchName}\n");

            await sandbox.CommitAndPushAsync(
                $"fleet: start work on #{workItem.WorkItemNumber} — {workItem.Title}",
                authorName: "Fleet Agent",
                authorEmail: "agent@fleet.dev",
                cancellationToken);

            // 2. Fetch the default branch name
            var client = httpClientFactory.CreateClient("GitHub");
            var repoJson = await GitHubGetAsync(client, accessToken,
                $"https://api.github.com/repos/{repoFullName}", cancellationToken);
            var baseBranch = repoJson?.TryGetProperty("default_branch", out var dbProp) == true
                ? dbProp.GetString() ?? "main"
                : "main";

            // 3. Open a draft PR
            var prPayload = JsonSerializer.Serialize(new
            {
                title = $"[Fleet] #{workItem.WorkItemNumber}: {workItem.Title}",
                body = $"Resolves work item **#{workItem.WorkItemNumber}**: {workItem.Title}\n\n" +
                       $"_This PR was opened automatically by Fleet. Agents are actively pushing changes._",
                head = sandbox.BranchName,
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

            if (!prResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to open draft PR: {Status} — {Body}",
                    prResponse.StatusCode, prResponseBody);
                return (null, 0);
            }

            var prResult = JsonSerializer.Deserialize<JsonElement>(prResponseBody);
            var prUrl = prResult.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
            var prNumber = prResult.TryGetProperty("number", out var numProp) ? numProp.GetInt32() : 0;

            logger.LogInformation("Opened draft PR #{PrNumber}: {PrUrl}", prNumber, prUrl);

            // 4. Immediately store the PR URL in the execution record so it's visible in the UI
            var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
            if (exec is not null)
            {
                exec.PullRequestUrl = prUrl;
                await scopedDb.SaveChangesAsync(cancellationToken);
            }

            return (prUrl, prNumber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open draft PR for branch {Branch}", sandbox.BranchName);
            return (null, 0);
        }
    }

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
