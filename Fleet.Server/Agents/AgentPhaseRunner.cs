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

    /// <summary>How often (in tool calls) to invoke the progress callback.</summary>
    private const int ProgressReportInterval = 3;

    public async Task<PhaseResult> RunPhaseAsync(
        AgentRole role,
        string userMessage,
        AgentToolContext toolContext,
        string? modelOverride = null,
        int? maxTokens = null,
        PhaseProgressCallback? onProgress = null,
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

                        // Add tool result to context
                        messages.Add(new LLMMessage
                        {
                            Role = "tool",
                            Content = toolResult,
                            ToolCallId = toolCall.Id,
                            ToolName = toolCall.Name,
                        });

                        // Report progress periodically so the DB/UI stays up-to-date
                        if (onProgress is not null && totalToolCalls % ProgressReportInterval == 0)
                        {
                            try
                            {
                                await onProgress(totalToolCalls, toolCall.Name);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Phase {Role}: progress callback failed (non-fatal)", role);
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

            // Truncate very large results
            var maxLen = llmOptions.Value.MaxToolOutputLength;
            if (result.Length > maxLen)
            {
                result = result[..maxLen] + $"\n\n[Output truncated at {maxLen:N0} characters]";
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Agent tool {ToolName} failed", toolCall.Name);
            return $"Error executing tool '{toolCall.Name}': {ex.Message}";
        }
    }
}
