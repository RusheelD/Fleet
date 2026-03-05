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
    ILogger<AgentOrchestrationService> logger,
    IModelCatalog modelCatalog) : IAgentOrchestrationService
{
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
    /// Returns a reduced pipeline based on how hard the work is.
    /// Difficulty 1-2: Planner → Backend → Frontend (lean)
    /// Difficulty 3:   Planner → Backend → Frontend → Testing → Consolidation
    /// Difficulty 4+:  Full pipeline
    /// </summary>
    private static AgentRole[][] BuildPipeline(int maxDifficulty)
    {
        if (maxDifficulty <= 2)
        {
            return
            [
                [AgentRole.Planner],
                [AgentRole.Backend, AgentRole.Frontend],
            ];
        }

        if (maxDifficulty <= 3)
        {
            return
            [
                [AgentRole.Planner],
                [AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing],
                [AgentRole.Consolidation],
            ];
        }

        return FullPipeline;
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
        // 1. Load the work item and ALL of its children
        var workItem = await workItemRepository.GetByWorkItemNumberAsync(projectId, workItemNumber)
            ?? throw new InvalidOperationException($"Work item #{workItemNumber} not found in project {projectId}.");

        var childWorkItems = new List<Models.WorkItemDto>();
        foreach (var childNumber in workItem.ChildWorkItemNumbers)
        {
            var child = await workItemRepository.GetByWorkItemNumberAsync(projectId, childNumber);
            if (child is not null)
                childWorkItems.Add(child);
        }

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

        // 4. Determine difficulty and build the pipeline
        //    Always use Haiku for cost savings.
        var allDifficulties = new List<int> { workItem.Difficulty };
        allDifficulties.AddRange(childWorkItems.Select(c => c.Difficulty));
        var maxDifficulty = allDifficulties.Max();
        var selectedModelKey = ModelKeys.Haiku;

        var pipeline = BuildPipeline(maxDifficulty);
        logger.LogInformation(
            "Execution: max difficulty={MaxDifficulty}, model={Model}, phases={PhaseCount}",
            maxDifficulty, selectedModelKey, pipeline.SelectMany(g => g).Count());

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
        _ = Task.Run(() => RunPipelineAsync(
            executionId, projectId, workItem, childWorkItems, repoFullName,
            accessToken, branchName, userId, selectedModelKey, pipeline), CancellationToken.None);

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

    private async Task RunPipelineAsync(
        string executionId, string projectId, Models.WorkItemDto workItem,
        List<Models.WorkItemDto> childWorkItems,
        string repoFullName, string accessToken, string branchName,
        int userId, string selectedModelKey, AgentRole[][] pipeline)
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
                    PhaseProgressCallback onProgress = async (toolCallsSoFar, lastToolName) =>
                    {
                        var phaseProgress = (double)toolCallsSoFar / AgentPhaseRunner.MaxToolCallsTotal;
                        var overallProgress = ((double)completedRoles + phaseProgress) / totalRoles;

                        var exec = await scopedDb.AgentExecutions.FindAsync(executionId);
                        if (exec is null) return;

                        exec.Progress = Math.Clamp(overallProgress, 0, 0.99);

                        var agent = exec.Agents.FirstOrDefault(a => a.Role == role.ToString());
                        if (agent is not null)
                        {
                            agent.CurrentTask = $"{GetPhaseTaskDescription(role)} ({toolCallsSoFar} tool calls — last: {lastToolName})";
                            agent.Progress = Math.Clamp(phaseProgress, 0, 0.99);
                        }

                        await scopedDb.SaveChangesAsync();
                    };

                    var maxTokens = GetMaxTokensForRole(role);
                    var phaseStart = DateTime.UtcNow;
                    var result = await scopedPhaseRunner.RunPhaseAsync(role, userMessage, toolContext, model, maxTokens, onProgress);
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

            // Pipeline complete — check if any PR was created
            var prUrl = await GetPullRequestUrlAsync(sandbox);

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
            if (sandbox is not null)
                await sandbox.DisposeAsync();
        }
    }

    /// <summary>
    /// Builds the work item context string that serves as the base input for all phases.
    /// Includes the parent work item and all its children with full details.
    /// </summary>
    private static string BuildWorkItemContext(Models.WorkItemDto workItem, List<Models.WorkItemDto> children)
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

        if (children.Count > 0)
        {
            sb.AppendLine("## Sub-Items");
            sb.AppendLine();
            foreach (var child in children)
            {
                sb.AppendLine($"### #{child.WorkItemNumber}: {child.Title}");
                sb.AppendLine($"- **Priority**: {child.Priority} | **Difficulty**: {child.Difficulty} | **State**: {child.State}");
                if (child.Tags.Length > 0)
                    sb.AppendLine($"- **Tags**: {string.Join(", ", child.Tags)}");
                if (!string.IsNullOrWhiteSpace(child.Description))
                {
                    sb.AppendLine($"- **Description**: {child.Description}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
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
        sb.AppendLine("**IMPORTANT — Speed & Cost Constraints:**");
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
        FleetDbContext scopedDb, string projectId, string agent, string level, string message)
    {
        scopedDb.LogEntries.Add(new LogEntry
        {
            Time = DateTime.UtcNow.ToString("o"),
            Agent = agent,
            Level = level,
            Message = message,
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
        exec.CurrentPhase = status == "completed" ? "Done" : "Failed";
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
    /// Attempts to detect if a PR was created by checking git log for PR-related tool output.
    /// The create_pull_request tool stores the URL in its output — look for it in the sandbox.
    /// </summary>
    private static Task<string?> GetPullRequestUrlAsync(IRepoSandbox sandbox)
    {
        // The CreatePullRequestTool returns the PR URL in its tool output,
        // but we don't have direct access to it here. Instead, we could check
        // if the branch was pushed (it has commits beyond origin/main).
        // For now, the PR URL will be extracted from the Review/Documentation phase output.
        // A future improvement: store PR URL directly on AgentToolContext.
        _ = sandbox; // Suppress unused warning
        return Task.FromResult<string?>(null);
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
