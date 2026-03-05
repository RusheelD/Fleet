using Fleet.Server.Agents.Tools;
using Fleet.Server.LLM;
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
    ILogger<AgentPhaseRunner> logger) : IAgentPhaseRunner
{
    /// <summary>Max tool-calling loops per phase.</summary>
    private const int MaxToolLoops = 50;

    /// <summary>Max total tool calls per phase.</summary>
    public const int MaxToolCallsTotal = 100;

    /// <summary>Timeout per phase (30 minutes).</summary>
    private static readonly TimeSpan PhaseTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Max characters per tool result for agent phases. Lower than the interactive
    /// chat limit to keep context windows lean and inference fast.
    /// </summary>
    private const int AgentMaxToolOutputLength = 12_000;

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
        var config = llmOptions.Value;
        var toolDefs = toolRegistry.ToLLMDefinitions();

        var messages = new List<LLMMessage>
        {
            new() { Role = "user", Content = userMessage }
        };

        var totalToolCalls = 0;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(PhaseTimeout);

        try
        {
            for (var loop = 0; loop < MaxToolLoops; loop++)
            {
                // Use the selected model for agent work (opus for complex, sonnet otherwise)
                var request = new LLMRequest(systemPrompt, messages, toolDefs, model, maxTokens,
                    CacheFirstUserMessage: true);
                LLMResponse response;

                try
                {
                    response = await llmClient.CompleteAsync(request, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Phase {Role} timed out after {Timeout}", role, PhaseTimeout);
                    return new PhaseResult(role, string.Empty, totalToolCalls, false,
                        $"Phase timed out after {PhaseTimeout.TotalMinutes} minutes.");
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

                    // Determine if ALL tool calls in this batch are read-only.
                    // If so, execute them all in parallel for maximum speed.
                    // Otherwise, fall back to sequential execution to preserve ordering.
                    var allReadOnly = response.ToolCalls.All(tc =>
                    {
                        var tool = toolRegistry.Get(tc.Name);
                        return tool is { IsReadOnly: true };
                    });

                    if (allReadOnly && response.ToolCalls.Count > 1)
                    {
                        // ── Parallel execution for read-only batches ──
                        var tasks = response.ToolCalls.Select(async toolCall =>
                        {
                            var result = await ExecuteToolAsync(toolCall, toolContext, cts.Token);
                            return (toolCall, result);
                        }).ToList();

                        var results = await Task.WhenAll(tasks);

                        foreach (var (toolCall, toolResult) in results)
                        {
                            totalToolCalls++;
                            if (totalToolCalls > MaxToolCallsTotal) break;

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

                        // Report progress once for the whole parallel batch
                        if (onProgress is not null)
                        {
                            foreach (var (toolCall, toolResult) in results)
                            {
                                if (toolCall.Name.Equals("report_progress", StringComparison.OrdinalIgnoreCase))
                                {
                                    var percent = ParseProgressPercent(toolCall.ArgumentsJson);
                                    var summary = ParseProgressSummary(toolCall.ArgumentsJson);
                                    try { await onProgress(percent / 100.0, summary); }
                                    catch (Exception ex) { logger.LogWarning(ex, "Phase {Role}: progress callback failed (non-fatal)", role); }
                                }
                            }
                        }
                    }
                    else
                    {
                        // ── Sequential execution for write operations ──
                        foreach (var toolCall in response.ToolCalls)
                        {
                            totalToolCalls++;
                            if (totalToolCalls > MaxToolCallsTotal)
                            {
                                logger.LogWarning("Phase {Role}: exceeded max tool calls ({Max})",
                                    role, MaxToolCallsTotal);
                                break;
                            }

                            var toolResult = await ExecuteToolAsync(toolCall, toolContext, cts.Token);

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

                            if (onProgress is not null && toolCall.Name.Equals("report_progress", StringComparison.OrdinalIgnoreCase))
                            {
                                var percent = ParseProgressPercent(toolCall.ArgumentsJson);
                                var summary = ParseProgressSummary(toolCall.ArgumentsJson);
                                try { await onProgress(percent / 100.0, summary); }
                                catch (Exception ex) { logger.LogWarning(ex, "Phase {Role}: progress callback failed (non-fatal)", role); }
                            }
                        }
                    }

                    if (totalToolCalls > MaxToolCallsTotal)
                        break;

                    continue;
                }

                // No tool calls — the model produced final output
                var output = response.Content ?? string.Empty;
                logger.LogInformation("Phase {Role} completed: {Loops} loops, {ToolCalls} tool calls",
                    role, loop + 1, totalToolCalls);

                return new PhaseResult(role, output, totalToolCalls, true);
            }

            // Exhausted tool loops
            logger.LogWarning("Phase {Role}: exhausted {Max} tool loops", role, MaxToolLoops);
            return new PhaseResult(role, string.Empty, totalToolCalls, false,
                $"Exhausted {MaxToolLoops} tool-calling loops without producing final output.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Phase {Role} failed with exception", role);
            return new PhaseResult(role, string.Empty, totalToolCalls, false, ex.Message);
        }
    }

    private async Task<string> ExecuteToolAsync(
        LLMToolCall toolCall, AgentToolContext context, CancellationToken ct)
    {
        var tool = toolRegistry.Get(toolCall.Name);
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

    private static int ParseProgressPercent(string argumentsJson)
    {
        try
        {
            var args = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(argumentsJson);
            return args.TryGetProperty("percent_complete", out var pct) ? Math.Clamp(pct.GetInt32(), 0, 100) : 0;
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
}
