using Fleet.Server.Agents.Tools;
using Fleet.Server.Diagnostics;
using Fleet.Server.LLM;
using Fleet.Server.Mcp;
using Fleet.Server.Memories;
using Fleet.Server.Models;
using Fleet.Server.Skills;
using Microsoft.Extensions.Options;

namespace Fleet.Server.Agents;

/// <summary>
/// Runs a single agent phase by executing an LLM tool-calling loop.
/// Modeled after <c>ChatService.SendMessageAsync</c> but designed for
/// autonomous agent execution rather than interactive chat.
/// </summary>
public class AgentPhaseRunner(
    IAgentPromptLoader promptLoader,
    ILLMClient llmClient,
    AgentToolRegistry toolRegistry,
    IOptions<LLMOptions> llmOptions,
    ILogger<AgentPhaseRunner> logger,
    IMcpToolSessionFactory? mcpToolSessionFactory = null,
    IMemoryService? memoryService = null,
    ISkillService? skillService = null,
    ITokenTracker? tokenTracker = null) : IAgentPhaseRunner
{
    private readonly IMcpToolSessionFactory _mcpToolSessionFactory = mcpToolSessionFactory ?? NoOpMcpToolSessionFactory.Instance;
    private readonly IMemoryService _memoryService = memoryService ?? NoOpMemoryService.Instance;
    private readonly ISkillService _skillService = skillService ?? NoOpSkillService.Instance;
    /// <summary>Default max tool-calling loops per phase.</summary>
    private const int DefaultMaxToolLoops = 200;

    /// <summary>Returns the max tool calls allowed for a given agent role.</summary>
    public static int GetMaxToolCalls(AgentRole role) => role switch
    {
        AgentRole.Manager => 200,
        AgentRole.Planner => 250,
        AgentRole.Contracts => 400,
        AgentRole.Review => 250,
        AgentRole.Documentation => 200,
        AgentRole.Consolidation => 400,
        _ => 500,  // Backend, Frontend, Testing, Styling
    };

    internal static int GetMaxToolLoops(AgentRole role) => role switch
    {
        AgentRole.Backend => 500,
        AgentRole.Frontend => 500,
        AgentRole.Testing => 500,
        AgentRole.Styling => 500,
        AgentRole.Contracts => 400,
        AgentRole.Consolidation => 400,
        _ => DefaultMaxToolLoops,
    };

    /// <summary>Returns the timeout for a given phase role (10-30 minutes).</summary>
    private static TimeSpan GetPhaseTimeout(AgentRole role) => role switch
    {
        AgentRole.Backend => TimeSpan.FromMinutes(30),
        AgentRole.Frontend => TimeSpan.FromMinutes(30),
        AgentRole.Consolidation => TimeSpan.FromMinutes(20),
        AgentRole.Contracts => TimeSpan.FromMinutes(15),
        AgentRole.Testing => TimeSpan.FromMinutes(15),
        AgentRole.Styling => TimeSpan.FromMinutes(15),
        AgentRole.Manager => TimeSpan.FromMinutes(10),
        AgentRole.Planner => TimeSpan.FromMinutes(10),
        AgentRole.Review => TimeSpan.FromMinutes(10),
        AgentRole.Documentation => TimeSpan.FromMinutes(10),
        _ => TimeSpan.FromMinutes(15),
    };

    /// <summary>
    /// Max characters per tool result for agent phases. Lower than the interactive
    /// chat limit to keep context windows lean and inference fast.
    /// </summary>
    private const int AgentMaxToolOutputLength = 12_000;
    private const double EstimatedProgressCeilingPercent = 100.0;
    private const double EstimatedProgressSoftCeilingPercent = 99.95;
    private const double MinimumProgressIncrementPercent = 0.05;
    private const double ProgressComparisonEpsilon = 0.0001;
    private const int FallbackProgressCadenceToolCalls = 1;
    private static readonly TimeSpan ProgressHeartbeatInterval = TimeSpan.FromSeconds(20);

    public async Task<PhaseResult> RunPhaseAsync(
        AgentRole role,
        string userMessage,
        AgentToolContext toolContext,
        string? modelOverride = null,
        int? maxTokens = null,
        PhaseProgressCallback? onProgress = null,
        PhaseToolCallLogger? onToolCall = null,
        CancellationToken cancellationToken = default)
    {
        var model = modelOverride ?? llmOptions.Value.GenerateModel;
        logger.LogInformation("Starting phase: {Role} for execution {ExecutionId} using model {Model}",
            role, toolContext.ExecutionId, model);

        var systemPrompt = promptLoader.GetPrompt(role);
        if (int.TryParse(toolContext.UserId, out var parsedUserId))
        {
            var memoryPrompt = await _memoryService.BuildPromptBlockAsync(
                parsedUserId,
                toolContext.ProjectId,
                userMessage,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(memoryPrompt))
            {
                systemPrompt = $"{systemPrompt}\n\n{memoryPrompt}";
            }

            var skillPrompt = await _skillService.BuildPromptBlockAsync(
                parsedUserId,
                toolContext.ProjectId,
                userMessage,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(skillPrompt))
            {
                systemPrompt = $"{systemPrompt}\n\n{skillPrompt}";
            }
        }

        await using var mcpToolSession = await _mcpToolSessionFactory.CreateForAgentAsync(
            toolContext.UserId,
            role,
            cancellationToken);
        var toolDefs = toolRegistry.ToLLMDefinitions(role)
            .Concat(mcpToolSession.Definitions)
            .ToList();

        var messages = new List<LLMMessage>
        {
            new() { Role = "user", Content = userMessage }
        };

        var totalToolCalls = 0;
        var maxToolCalls = GetMaxToolCalls(role);
        var maxToolLoops = GetMaxToolLoops(role);
        var phaseTimeout = GetPhaseTimeout(role);
        var lastReportedPercent = 0.0;
        var lastProgressSummary = "Starting phase";
        var nonProgressToolCallsSinceLastReport = 0;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(phaseTimeout);

        async Task ReportProgressAsync(double percent, string summary, bool force = false)
        {
            if (onProgress is null)
            {
                return;
            }

            var normalizedSummary = string.IsNullOrWhiteSpace(summary)
                ? "Working"
                : summary.Trim();
            var clampedPercent = NormalizeProgressPercent(percent);
            if (force)
            {
                if (clampedPercent < lastReportedPercent)
                {
                    clampedPercent = lastReportedPercent;
                }
            }
            else
            {
                if (lastReportedPercent >= EstimatedProgressSoftCeilingPercent - ProgressComparisonEpsilon)
                {
                    return;
                }

                var nextMinimum = lastReportedPercent + MinimumProgressIncrementPercent;
                var safeMinimum = Math.Min(nextMinimum, EstimatedProgressSoftCeilingPercent);
                clampedPercent = ClampWithinProgressWindow(clampedPercent, safeMinimum, EstimatedProgressSoftCeilingPercent);
            }

            if (!force && clampedPercent <= lastReportedPercent + ProgressComparisonEpsilon)
            {
                return;
            }

            lastReportedPercent = clampedPercent;
            lastProgressSummary = normalizedSummary;
            try
            {
                await onProgress(clampedPercent / 100.0, normalizedSummary);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Phase {Role}: progress callback failed (non-fatal)", role);
            }
        }

        double EstimateProgressPercentFromToolCalls()
        {
            if (lastReportedPercent >= EstimatedProgressSoftCeilingPercent - ProgressComparisonEpsilon)
            {
                return EstimatedProgressSoftCeilingPercent;
            }

            var nextMinimum = lastReportedPercent + MinimumProgressIncrementPercent;
            if (nextMinimum >= EstimatedProgressSoftCeilingPercent)
            {
                return EstimatedProgressSoftCeilingPercent;
            }

            if (maxToolCalls <= 0)
            {
                return ClampWithinProgressWindow(nextMinimum, MinimumProgressIncrementPercent, EstimatedProgressSoftCeilingPercent);
            }

            var estimated = (double)totalToolCalls / maxToolCalls * 100.0;
            var minAllowed = Math.Max(MinimumProgressIncrementPercent, nextMinimum);
            if (minAllowed >= EstimatedProgressSoftCeilingPercent)
            {
                return EstimatedProgressSoftCeilingPercent;
            }

            return ClampWithinProgressWindow(estimated, minAllowed, EstimatedProgressSoftCeilingPercent);
        }

        static double ClampWithinProgressWindow(double value, double min, double max)
        {
            // Defensive bound clamp that never throws when min/max are inconsistent.
            if (min > max)
            {
                return max;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        static double NormalizeProgressPercent(double value)
        {
            var clamped = Math.Clamp(value, 0, EstimatedProgressCeilingPercent);
            var snapped = Math.Round(clamped / MinimumProgressIncrementPercent, MidpointRounding.AwayFromZero)
                          * MinimumProgressIncrementPercent;
            return Math.Round(
                Math.Clamp(snapped, 0, EstimatedProgressCeilingPercent),
                2,
                MidpointRounding.AwayFromZero);
        }

        try
        {
            await ReportProgressAsync(MinimumProgressIncrementPercent, "Starting phase", force: true);
            var outputTokenCap = AdaptiveTokenCap.DefaultCap;
            var errorRecovery = new ErrorRecoveryLadder();
            for (var loop = 0; loop < maxToolLoops; loop++)
            {
                // Compress context when approaching the token budget
                var compressedMessages = ContextCompression.Compress(
                    messages,
                    llmOptions.Value.ContextWindowTokens,
                    llmOptions.Value.ReservedOutputTokens);

                // Use the selected model for agent work (opus for complex, sonnet otherwise)
                var request = AdaptiveTokenCap.ApplyCap(
                    new LLMRequest(systemPrompt, compressedMessages, toolDefs, model, maxTokens,
                        CacheFirstUserMessage: true),
                    outputTokenCap);
                LLMResponse response;

                try
                {
                    var responseTask = llmClient.CompleteAsync(request, cts.Token);
                    var heartbeatCount = 0;
                    while (!responseTask.IsCompleted)
                    {
                        var completedTask = await Task.WhenAny(
                            responseTask,
                            Task.Delay(ProgressHeartbeatInterval, cts.Token));
                        if (completedTask == responseTask)
                        {
                            break;
                        }

                        heartbeatCount++;
                        var waitedSeconds = heartbeatCount * (int)ProgressHeartbeatInterval.TotalSeconds;
                        await ReportProgressAsync(
                            lastReportedPercent,
                            $"Waiting for model response ({waitedSeconds}s)",
                            force: true);
                    }

                    response = await responseTask;
                    tokenTracker?.Record(response.Usage);
                    outputTokenCap = AdaptiveTokenCap.GetNextCap(outputTokenCap, response.WasTruncated);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Phase {Role} timed out after {Timeout}", role, phaseTimeout);
                    return new PhaseResult(role, string.Empty, totalToolCalls, false,
                        $"Phase timed out after {phaseTimeout.TotalMinutes} minutes.",
                        lastReportedPercent,
                        lastProgressSummary,
                        tokenTracker?.TotalInputTokens ?? 0,
                        tokenTracker?.TotalOutputTokens ?? 0);
                }

                // If the model returned tool calls, execute them
                if (response.ToolCalls is { Count: > 0 })
                {
                    logger.LogDebug("Phase {Role}: loop {Loop}, {ToolCallCount} tool calls",
                        role, loop + 1, response.ToolCalls.Count);

                    // Add the assistant's tool-call message to context
                    messages.Add(new LLMMessage
                    {
                        Role = "assistant",
                        Content = response.Content,
                        ToolCalls = response.ToolCalls,
                    });

                    // Partition tool calls into consecutive read-only and mutating batches.
                    // This keeps writes ordered while still parallelizing safe reads around them.
                    var toolBatches = ToolCallBatchPlanner.PartitionByReadOnly(
                        response.ToolCalls,
                        toolCall => IsReadOnlyToolCall(toolCall, mcpToolSession));

                    foreach (var toolBatch in toolBatches)
                    {
                        if (toolBatch.CanRunInParallel && toolBatch.ToolCalls.Count > 1)
                        {
                        // ── Parallel execution for read-only batches ──
                        var tasks = toolBatch.ToolCalls.Select(async toolCall =>
                        {
                            var result = await ExecuteToolAsync(role, toolCall, toolContext, mcpToolSession, cts.Token);
                            return (toolCall, result);
                        }).ToList();

                        var results = await Task.WhenAll(tasks);

                        foreach (var (toolCall, toolResult) in results)
                        {
                            totalToolCalls++;
                            if (totalToolCalls > maxToolCalls) break;

                            messages.Add(new LLMMessage
                            {
                                Role = "tool",
                                Content = toolResult,
                                ToolCallId = toolCall.Id,
                                ToolName = toolCall.Name,
                            });

                            // Log tool call as detailed entry (skip report_progress — it has its own log)
                            if (onToolCall is not null && !toolCall.Name.Equals("report_progress", StringComparison.OrdinalIgnoreCase))
                            {
                                var snippet = toolResult.Length > 200 ? toolResult[..200] + "…" : toolResult;
                                try { await onToolCall(toolCall.Name, snippet); }
                                catch (Exception ex) { logger.LogWarning(ex, "Phase {Role}: tool-call logger failed (non-fatal)", role); }
                            }
                        }

                        foreach (var (toolCall, _) in results)
                        {
                            if (toolCall.Name.Equals("report_progress", StringComparison.OrdinalIgnoreCase))
                            {
                                var percent = ParseProgressPercent(toolCall.ArgumentsJson);
                                var summary = ParseProgressSummary(toolCall.ArgumentsJson);
                                await ReportProgressAsync(percent, summary);
                                nonProgressToolCallsSinceLastReport = 0;
                            }
                            else
                            {
                                nonProgressToolCallsSinceLastReport++;
                                if (nonProgressToolCallsSinceLastReport >= FallbackProgressCadenceToolCalls)
                                {
                                    var estimatedPercent = EstimateProgressPercentFromToolCalls();
                                    await ReportProgressAsync(
                                        estimatedPercent,
                                        $"Working via {toolCall.Name} (step {totalToolCalls})");
                                    nonProgressToolCallsSinceLastReport = 0;
                                }
                            }
                        }
                    }
                    else
                    {
                        // ── Sequential execution for write operations ──
                        foreach (var toolCall in toolBatch.ToolCalls)
                        {
                            totalToolCalls++;
                            if (totalToolCalls > maxToolCalls)
                            {
                                logger.LogWarning("Phase {Role}: exceeded max tool calls ({Max})",
                                    role, maxToolCalls);
                                break;
                            }

                            var toolResult = await ExecuteToolAsync(role, toolCall, toolContext, mcpToolSession, cts.Token);

                            messages.Add(new LLMMessage
                            {
                                Role = "tool",
                                Content = toolResult,
                                ToolCallId = toolCall.Id,
                                ToolName = toolCall.Name,
                            });

                            // Log tool call as detailed entry (skip report_progress — it has its own log)
                            if (onToolCall is not null && !toolCall.Name.Equals("report_progress", StringComparison.OrdinalIgnoreCase))
                            {
                                var snippet = toolResult.Length > 200 ? toolResult[..200] + "…" : toolResult;
                                try { await onToolCall(toolCall.Name, snippet); }
                                catch (Exception ex) { logger.LogWarning(ex, "Phase {Role}: tool-call logger failed (non-fatal)", role); }
                            }

                            if (toolCall.Name.Equals("report_progress", StringComparison.OrdinalIgnoreCase))
                            {
                                var percent = ParseProgressPercent(toolCall.ArgumentsJson);
                                var summary = ParseProgressSummary(toolCall.ArgumentsJson);
                                await ReportProgressAsync(percent, summary);
                                nonProgressToolCallsSinceLastReport = 0;
                            }
                            else
                            {
                                nonProgressToolCallsSinceLastReport++;
                                if (nonProgressToolCallsSinceLastReport >= FallbackProgressCadenceToolCalls)
                                {
                                    var estimatedPercent = EstimateProgressPercentFromToolCalls();
                                    await ReportProgressAsync(
                                        estimatedPercent,
                                        $"Working via {toolCall.Name} (step {totalToolCalls})");
                                    nonProgressToolCallsSinceLastReport = 0;
                                }
                            }
                        }
                    }

                    }

                    if (totalToolCalls > maxToolCalls)
                        break;

                    // Check error recovery ladder
                    var recoveryLevel = RecoveryLevel.None;
                    foreach (var msg in messages.TakeLast(response.ToolCalls.Count))
                    {
                        if (msg.Role == "tool")
                        {
                            var isError = msg.Content?.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) == true;
                            recoveryLevel = errorRecovery.RecordResult(isError);
                        }
                    }

                    if (recoveryLevel == RecoveryLevel.Abort)
                    {
                        logger.LogWarning("Phase {Role}: error recovery ladder reached abort ({Errors} consecutive errors)",
                            role, errorRecovery.ConsecutiveErrors);
                        return new PhaseResult(role, string.Empty, totalToolCalls, false,
                            $"Aborted after {errorRecovery.ConsecutiveErrors} consecutive tool errors.",
                            lastReportedPercent, lastProgressSummary,
                            tokenTracker?.TotalInputTokens ?? 0,
                            tokenTracker?.TotalOutputTokens ?? 0);
                    }

                    if (recoveryLevel == RecoveryLevel.InjectHint)
                    {
                        messages.Add(ErrorRecoveryLadder.CreateRecoveryHint(errorRecovery.ConsecutiveErrors));
                    }

                    continue;
                }

                // No tool calls — the model produced final output
                var output = response.Content ?? string.Empty;

                // Stop verification: if the model did substantial work but returns
                // a nearly empty response, re-prompt once to confirm the work is
                // genuinely complete (prevents premature termination in complex phases).
                if (totalToolCalls >= 5 && output.Length < 20 && loop < maxToolLoops - 1)
                {
                    logger.LogInformation(
                        "Phase {Role}: short final output ({Len} chars) after {ToolCalls} tool calls — running stop verification",
                        role, output.Length, totalToolCalls);

                    messages.Add(new LLMMessage
                    {
                        Role = "assistant",
                        Content = output,
                    });
                    messages.Add(new LLMMessage
                    {
                        Role = "user",
                        Content = "Your response was very brief. Are you sure you have completed all required work for this phase? " +
                                  "If there are remaining tasks, continue executing them now. " +
                                  "If you are genuinely done, provide your complete final summary.",
                    });
                    continue;
                }

                logger.LogInformation("Phase {Role} completed: {Loops} loops, {ToolCalls} tool calls",
                    role, loop + 1, totalToolCalls);

                await ReportProgressAsync(100, "Phase completed", force: true);
                return new PhaseResult(role, output, totalToolCalls, true,
                    InputTokens: tokenTracker?.TotalInputTokens ?? 0,
                    OutputTokens: tokenTracker?.TotalOutputTokens ?? 0);
            }

            // Exhausted tool loops
            logger.LogWarning("Phase {Role}: exhausted {Max} tool loops", role, maxToolLoops);
            return new PhaseResult(role, string.Empty, totalToolCalls, false,
                $"Exhausted {maxToolLoops} tool-calling loops without producing final output.",
                lastReportedPercent,
                lastProgressSummary,
                tokenTracker?.TotalInputTokens ?? 0,
                tokenTracker?.TotalOutputTokens ?? 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Phase {Role} failed with exception", role);
            return new PhaseResult(role, string.Empty, totalToolCalls, false, ex.Message, lastReportedPercent, lastProgressSummary,
                tokenTracker?.TotalInputTokens ?? 0,
                tokenTracker?.TotalOutputTokens ?? 0);
        }
    }

    private async Task<string> ExecuteToolAsync(
        AgentRole role, LLMToolCall toolCall, AgentToolContext context, IMcpToolSession mcpToolSession, CancellationToken ct)
    {
        if (!toolRegistry.IsToolAllowed(role, toolCall.Name) &&
            !(mcpToolSession.HasTool(toolCall.Name) && (role != AgentRole.Manager || mcpToolSession.IsReadOnly(toolCall.Name))))
        {
            logger.LogWarning("Tool {ToolName} is not allowed for role {Role}", toolCall.Name, role);
            return $"Error: tool '{toolCall.Name}' is not allowed for role '{role}'.";
        }

        var tool = toolRegistry.Get(toolCall.Name);
        if (tool is null && mcpToolSession.HasTool(toolCall.Name))
        {
            try
            {
                return await mcpToolSession.ExecuteAsync(toolCall.Name, toolCall.ArgumentsJson, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MCP tool {ToolName} failed in agent phase {Role}", toolCall.Name, role);
                return $"Error executing tool '{toolCall.Name}': {ex.Message}";
            }
        }

        if (tool is null)
        {
            logger.LogWarning("Unknown agent tool: {ToolName}", toolCall.Name);
            return $"Error: unknown tool '{toolCall.Name}'.";
        }

        try
        {
            var result = await tool.ExecuteAsync(toolCall.ArgumentsJson, context, ct);

            // Truncate large results using the tighter agent-specific limit
            if (result.Length > AgentMaxToolOutputLength)
            {
                result = result[..AgentMaxToolOutputLength] + $"\n\n[Output truncated at {AgentMaxToolOutputLength:N0} characters]";
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Agent tool {ToolName} failed", toolCall.Name);
            return $"Error executing tool '{toolCall.Name}': {ex.Message}";
        }
    }

    private bool IsReadOnlyToolCall(LLMToolCall toolCall, IMcpToolSession mcpToolSession)
    {
        var tool = toolRegistry.Get(toolCall.Name);
        if (tool is not null)
            return tool.IsReadOnly;

        return mcpToolSession.HasTool(toolCall.Name) && mcpToolSession.IsReadOnly(toolCall.Name);
    }

    private static double ParseProgressPercent(string argumentsJson)
    {
        try
        {
            var args = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(argumentsJson);
            if (!args.TryGetProperty("percent_complete", out var pct) || pct.ValueKind != System.Text.Json.JsonValueKind.Number)
            {
                return 0;
            }

            var value = pct.GetDouble();
            var snapped = Math.Round(value / MinimumProgressIncrementPercent, MidpointRounding.AwayFromZero)
                          * MinimumProgressIncrementPercent;
            return Math.Round(Math.Clamp(snapped, 0, EstimatedProgressCeilingPercent), 2, MidpointRounding.AwayFromZero);
        }
        catch { return 0; }
    }

    private static string ParseProgressSummary(string argumentsJson)
    {
        try
        {
            var args = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(argumentsJson);
            return args.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
        }
        catch { return ""; }
    }

    private sealed class NoOpMcpToolSessionFactory : IMcpToolSessionFactory
    {
        public static readonly NoOpMcpToolSessionFactory Instance = new();

        public Task<IMcpToolSession> CreateForChatAsync(int userId, bool includeWriteTools, CancellationToken cancellationToken = default)
            => Task.FromResult<IMcpToolSession>(EmptyMcpToolSession.Instance);

        public Task<IMcpToolSession> CreateForAgentAsync(string userId, AgentRole role, CancellationToken cancellationToken = default)
            => Task.FromResult<IMcpToolSession>(EmptyMcpToolSession.Instance);
    }

    private sealed class EmptyMcpToolSession : IMcpToolSession
    {
        public static readonly EmptyMcpToolSession Instance = new();

        public IReadOnlyList<LLMToolDefinition> Definitions => [];

        public bool HasTool(string toolName) => false;

        public bool IsReadOnly(string toolName) => false;

        public Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
            => Task.FromResult($"Error: unknown MCP tool '{toolName}'.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoOpMemoryService : IMemoryService
    {
        public static readonly NoOpMemoryService Instance = new();

        public Task<IReadOnlyList<MemoryEntryDto>> GetUserMemoriesAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemoryEntryDto>>([]);

        public Task<IReadOnlyList<MemoryEntryDto>> GetProjectMemoriesAsync(int userId, string projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemoryEntryDto>>([]);

        public Task<MemoryEntryDto> CreateUserMemoryAsync(int userId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryEntryDto> UpdateUserMemoryAsync(int userId, int memoryId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteUserMemoryAsync(int userId, int memoryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryEntryDto> CreateProjectMemoryAsync(int userId, string projectId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryEntryDto> UpdateProjectMemoryAsync(int userId, string projectId, int memoryId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteProjectMemoryAsync(int userId, string projectId, int memoryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string> BuildPromptBlockAsync(int userId, string? projectId, string? query, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class NoOpSkillService : ISkillService
    {
        public static readonly NoOpSkillService Instance = new();

        public Task<IReadOnlyList<PromptSkillTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptSkillTemplateDto>>([]);

        public Task<IReadOnlyList<PromptSkillDto>> GetUserSkillsAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptSkillDto>>([]);

        public Task<IReadOnlyList<PromptSkillDto>> GetProjectSkillsAsync(int userId, string projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptSkillDto>>([]);

        public Task<PromptSkillDto> CreateUserSkillAsync(int userId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PromptSkillDto> UpdateUserSkillAsync(int userId, int skillId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteUserSkillAsync(int userId, int skillId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PromptSkillDto> CreateProjectSkillAsync(int userId, string projectId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PromptSkillDto> UpdateProjectSkillAsync(int userId, string projectId, int skillId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteProjectSkillAsync(int userId, string projectId, int skillId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string> BuildPromptBlockAsync(int userId, string? projectId, string? query, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<string> BuildPromptBlockAsync(int userId, string? projectId, string? query, IReadOnlyList<string>? conversationContext, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }
}
