using Fleet.Server.Auth;
using Fleet.Server.Copilot.Tools;
using Fleet.Server.LLM;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Fleet.Server.Subscriptions;
using Microsoft.Extensions.Options;

namespace Fleet.Server.Copilot;

public class ChatService(
    IChatSessionRepository chatSessionRepository,
    ILLMClient llmClient,
    ChatToolRegistry toolRegistry,
    IAuthService authService,
    IOptions<LLMOptions> llmOptions,
    ILogger<ChatService> logger,
    IUsageLedgerService? usageLedgerService = null,
    IServerEventPublisher? eventPublisher = null) : IChatService
{
    private readonly IUsageLedgerService _usageLedgerService = usageLedgerService ?? NoOpUsageLedgerService.Instance;
    private readonly IServerEventPublisher? _eventPublisher = eventPublisher;

    private static readonly Lazy<string> SystemPromptLazy = new(() =>
    {
        var promptPath = Path.Combine(AppContext.BaseDirectory, "Copilot", "chat_prompt.txt");
        if (File.Exists(promptPath))
            return File.ReadAllText(promptPath);

        // Fallback: try relative to the project directory (development)
        var devPath = Path.Combine(Directory.GetCurrentDirectory(), "Copilot", "chat_prompt.txt");
        if (File.Exists(devPath))
            return File.ReadAllText(devPath);

        return "You are Fleet AI, an expert software project assistant. Help users plan features and create work items.";
    });

    private static string SystemPrompt => SystemPromptLazy.Value;

    /// <summary>Max total chars of attachment content to inject into the prompt.</summary>
    private const int MaxAttachmentContextLength = 50_000;

    public async Task<ChatDataDto> GetChatDataAsync(string projectId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId
        });

        logger.CopilotChatDataRetrieving(projectId.SanitizeForLogging());
        var sessions = await chatSessionRepository.GetSessionsByProjectIdAsync(projectId);
        var activeSession = sessions.FirstOrDefault(s => s.IsActive);
        var messages = activeSession is not null
            ? await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, activeSession.Id)
            : [];
        var suggestions = await chatSessionRepository.GetSuggestionsAsync(projectId);

        logger.CopilotChatDataRetrieved(projectId.SanitizeForLogging(), sessions.Count);
        return new ChatDataDto([.. sessions], [.. messages], suggestions);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(string projectId, string sessionId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["SessionId"] = sessionId
        });

        logger.CopilotMessagesRetrieving(projectId.SanitizeForLogging(), sessionId.SanitizeForLogging());
        return await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, sessionId);
    }

    public async Task<ChatSessionDto> CreateSessionAsync(string projectId, string title)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId
        });

        logger.CopilotSessionCreating(projectId.SanitizeForLogging(), title.SanitizeForLogging());
        return await chatSessionRepository.CreateSessionAsync(projectId, title);
    }

    public async Task<bool> RenameSessionAsync(string projectId, string sessionId, string title)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["SessionId"] = sessionId
        });

        logger.CopilotSessionRenaming(
            projectId.SanitizeForLogging(),
            sessionId.SanitizeForLogging(),
            title.SanitizeForLogging());
        return await chatSessionRepository.RenameSessionAsync(projectId, sessionId, title);
    }

    public async Task<bool> DeleteSessionAsync(string projectId, string sessionId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["SessionId"] = sessionId
        });

        logger.CopilotSessionDeleting(projectId.SanitizeForLogging(), sessionId.SanitizeForLogging());
        return await chatSessionRepository.DeleteSessionAsync(projectId, sessionId);
    }

    public async Task<SendMessageResponseDto> SendMessageAsync(string projectId, string sessionId, string content, bool generateWorkItems = false)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["SessionId"] = sessionId,
            ["GenerateWorkItems"] = generateWorkItems
        });

        if (generateWorkItems && IsGlobalScope(projectId))
            throw new InvalidOperationException("Work-item generation is only available in project-scoped chat sessions.");

        logger.CopilotMessageSending(projectId.SanitizeForLogging(), sessionId.SanitizeForLogging(), generateWorkItems);
        var config = llmOptions.Value;

        // 1. Persist the user message
        var userMessage = await chatSessionRepository.AddMessageAsync(projectId, sessionId, "user", content);

        // 1a. Mark session as generating so the UI shows a spinner on refresh
        if (generateWorkItems)
            await chatSessionRepository.SetSessionGeneratingAsync(projectId, sessionId, true);

        var userId = await authService.GetCurrentUserIdAsync();
        var chargedWorkItemRun = false;
        if (generateWorkItems)
        {
            await _usageLedgerService.ChargeRunAsync(userId, MonthlyRunType.WorkItem);
            chargedWorkItemRun = true;
        }

        await PublishChatUpdatedAsync(userId, projectId, sessionId);

        // 2. Load full session history for context
        var history = await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, sessionId);
        var llmMessages = history.Select(ToLLMMessage).ToList();

        // 2a. If generation was triggered, inject the system trigger message
        if (generateWorkItems)
        {
            llmMessages.Add(new LLMMessage
            {
                Role = "user",
                Content = "Generate work-items based on provided context",
            });
        }

        // 2b. Build system prompt with any uploaded documents
        var systemPrompt = await BuildSystemPromptAsync(projectId, sessionId);

        // 3. Get tool definitions — only include write tools when generation is requested.
        //    In generation mode, exclude single-item write tools (create/update/delete_work_item)
        //    so the LLM is forced to use bulk equivalents, reducing API round-trips and 429 errors.
        var toolDefs = toolRegistry.ToLLMDefinitions(
            includeWriteTools: generateWorkItems,
            bulkOnly: generateWorkItems,
            includeGlobalRepoTools: IsGlobalScope(projectId));

        // 4. Auto-name the session before generation starts (fast Haiku call)
        if (generateWorkItems)
        {
            await GenerateSessionNameAsync(projectId, sessionId, llmMessages, config);
        }

        // 5. Run the tool-calling loop with safety limits
        var toolEvents = new List<ToolEventDto>();
        var toolContext = new ChatToolContext(projectId, userId.ToString());
        var totalToolCalls = 0;

        var timeoutSeconds = generateWorkItems ? config.GenerateTimeoutSeconds : config.TimeoutSeconds;
        var maxLoops = generateWorkItems ? config.GenerateMaxToolLoops : config.MaxToolLoops;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        for (var loop = 0; loop < maxLoops; loop++)
        {
            // Use Sonnet for generation, Haiku for normal chat
            var modelOverride = generateWorkItems ? config.GenerateModel : null;
            var request = new LLMRequest(systemPrompt, llmMessages, toolDefs, modelOverride);
            LLMResponse response;

            try
            {
                response = await llmClient.CompleteAsync(request, cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.CopilotLlmTimeout(timeoutSeconds);
                var timeoutMsg = await chatSessionRepository.AddMessageAsync(
                    projectId, sessionId, "assistant", "I'm sorry, the request took too long. Please try again.");
                await ClearGeneratingFlagAsync(projectId, sessionId, generateWorkItems);
                await PublishChatUpdatedAsync(userId, projectId, sessionId);
                await RefundIfNeededAsync(generateWorkItems, chargedWorkItemRun, userId);
                return new SendMessageResponseDto(sessionId, timeoutMsg, [.. toolEvents], null);
            }
            catch (Exception ex)
            {
                logger.CopilotLlmFailed(ex);
                var assistantError = BuildAssistantErrorMessage(ex);
                var errorMsg = await chatSessionRepository.AddMessageAsync(
                    projectId, sessionId, "assistant", assistantError);
                await ClearGeneratingFlagAsync(projectId, sessionId, generateWorkItems);
                await PublishChatUpdatedAsync(userId, projectId, sessionId);
                await RefundIfNeededAsync(generateWorkItems, chargedWorkItemRun, userId);
                return new SendMessageResponseDto(sessionId, errorMsg, [.. toolEvents], ex.Message);
            }

            // Log what the LLM returned
            logger.CopilotLlmResponseReceived(
                loop + 1,
                response.ToolCalls is { Count: > 0 },
                response.ToolCalls?.Count ?? 0,
                response.Content?.Length ?? 0);

            // If the model returned tool calls, execute them
            if (response.ToolCalls is { Count: > 0 })
            {
                logger.CopilotToolBatchStarting(response.ToolCalls.Count, totalToolCalls);

                // Add the assistant's tool-call message to context
                llmMessages.Add(new LLMMessage
                {
                    Role = "assistant",
                    Content = response.Content,
                    ToolCalls = response.ToolCalls,
                });

                foreach (var toolCall in response.ToolCalls)
                {
                    // Don't count write tools (create/update/delete) toward the limit —
                    // the LLM should be able to modify as many work items as needed
                    var toolDef = toolRegistry.Get(toolCall.Name);
                    if (toolDef is null || !toolDef.IsWriteTool)
                    {
                        totalToolCalls++;
                        if (totalToolCalls > config.MaxToolCallsTotal)
                        {
                            logger.CopilotMaxToolCallsExceeded(config.MaxToolCallsTotal);
                            break;
                        }
                    }

                    var toolResult = await ExecuteToolAsync(toolCall, toolContext, config, toolDefs, cts.Token);
                    toolEvents.Add(new ToolEventDto(toolCall.Name, toolCall.ArgumentsJson, toolResult));

                    // Add tool result to context for the LLM
                    llmMessages.Add(new LLMMessage
                    {
                        Role = "tool",
                        Content = toolResult,
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name,
                    });

                    await PublishChatUpdatedAsync(userId, projectId, sessionId);
                    await PublishWriteSideEffectsAsync(userId, projectId, sessionId, toolCall.Name, toolDef?.IsWriteTool == true);
                }

                if (totalToolCalls > config.MaxToolCallsTotal)
                    break;

                // Continue the loop so the LLM can process results
                continue;
            }

            // No tool calls — the model produced a final text response
            var assistantContent = response.Content ?? "I wasn't able to generate a response.";
            var assistantMessage = await chatSessionRepository.AddMessageAsync(
                projectId, sessionId, "assistant", assistantContent);

            logger.CopilotAiResponseGenerated(sessionId.SanitizeForLogging(), loop + 1, totalToolCalls);

            await ClearGeneratingFlagAsync(projectId, sessionId, generateWorkItems);
            await PublishChatUpdatedAsync(userId, projectId, sessionId);
            return new SendMessageResponseDto(sessionId, assistantMessage, [.. toolEvents], null);
        }

        // Exhausted tool loops — force a text response
        logger.CopilotToolLoopExhausted(sessionId.SanitizeForLogging(), maxLoops);
        var fallbackMsg = await chatSessionRepository.AddMessageAsync(
            projectId, sessionId, "assistant",
            "I used several tools but wasn't able to finish. Here's what I found so far — could you clarify what you need?");
        await ClearGeneratingFlagAsync(projectId, sessionId, generateWorkItems);
        await PublishChatUpdatedAsync(userId, projectId, sessionId);
        await RefundIfNeededAsync(generateWorkItems, chargedWorkItemRun, userId);
        return new SendMessageResponseDto(sessionId, fallbackMsg, [.. toolEvents], null);
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Clears the IsGenerating flag on the session when a generate request completes.
    /// Best-effort — failures are logged but do not propagate.
    /// </summary>
    private async Task ClearGeneratingFlagAsync(string projectId, string sessionId, bool wasGenerating)
    {
        if (!wasGenerating) return;
        try
        {
            await chatSessionRepository.SetSessionGeneratingAsync(projectId, sessionId, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear IsGenerating flag for session {SessionId}", sessionId.SanitizeForLogging());
        }
    }

    private async Task PublishChatUpdatedAsync(int userId, string projectId, string sessionId)
    {
        if (_eventPublisher is null)
            return;

        try
        {
            if (IsGlobalScope(projectId))
            {
                await _eventPublisher.PublishUserEventAsync(
                    userId,
                    ServerEventTopics.ChatUpdated,
                    new { projectId = (string?)null, sessionId });
                return;
            }

            await _eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.ChatUpdated,
                new { projectId, sessionId });
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to publish chat update event for session {SessionId}", sessionId.SanitizeForLogging());
        }
    }

    private async Task PublishWriteSideEffectsAsync(
        int userId,
        string projectId,
        string sessionId,
        string toolName,
        bool isWriteTool)
    {
        if (!isWriteTool || _eventPublisher is null)
            return;

        try
        {
            if (IsGlobalScope(projectId))
            {
                if (string.Equals(toolName, "create_project", StringComparison.OrdinalIgnoreCase))
                {
                    await _eventPublisher.PublishUserEventAsync(
                        userId,
                        ServerEventTopics.ProjectsUpdated,
                        new { projectId = (string?)null, sessionId, toolName });
                }

                return;
            }

            await _eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.WorkItemsUpdated,
                new { projectId, sessionId, toolName });

            await _eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ProjectsUpdated,
                new { projectId, sessionId, toolName });
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to publish write side-effect events for tool {ToolName} in session {SessionId}",
                toolName.SanitizeForLogging(),
                sessionId.SanitizeForLogging());
        }
    }

    /// <summary>
    /// Calls Haiku to generate a concise name for the chat session based on conversation context,
    /// then persists it. Failures are logged but do not propagate — naming is best-effort.
    /// </summary>
    private async Task GenerateSessionNameAsync(string projectId, string sessionId, List<LLMMessage> conversationMessages, LLMOptions config)
    {
        try
        {
            var namingPrompt = "Based on the conversation so far, generate a short descriptive name (3-6 words) for this chat session. " +
                "The name should capture the main topic or purpose. Respond with ONLY the name — no quotes, no punctuation, no explanation.";

            // Use a small slice of the conversation for naming context (first few + last few messages)
            var contextMessages = conversationMessages
                .Where(m => m.Role is "user" or "assistant")
                .Take(6)
                .ToList();

            contextMessages.Add(new LLMMessage { Role = "user", Content = namingPrompt });

            var request = new LLMRequest(
                "You are a helpful assistant that generates concise chat session names.",
                contextMessages,
                Tools: null,
                ModelOverride: config.Model); // Use Haiku for speed

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await llmClient.CompleteAsync(request, cts.Token);

            var name = response.Content?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                // Cap at 60 chars to keep UI tidy
                if (name.Length > 60)
                    name = name[..60];

                await chatSessionRepository.RenameSessionAsync(projectId, sessionId, name);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to auto-name chat session {SessionId}", sessionId.SanitizeForLogging());
        }
    }

    private async Task<string> ExecuteToolAsync(
        LLMToolCall toolCall, ChatToolContext context, LLMOptions config,
        IReadOnlyList<LLMToolDefinition> allowedTools, CancellationToken ct)
    {
        // Guard: only execute tools that were actually sent to the LLM.
        // Models sometimes hallucinate tool calls for tools not in their definitions.
        var isAllowed = allowedTools.Any(t => string.Equals(t.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase));
        if (!isAllowed)
        {
            logger.CopilotUnknownTool(toolCall.Name.SanitizeForLogging());
            return $"Error: tool '{toolCall.Name}' is not available. Do not call it. Use only the tools provided in your tool definitions.";
        }

        var tool = toolRegistry.Get(toolCall.Name);
        if (tool is null)
        {
            logger.CopilotUnknownTool(toolCall.Name.SanitizeForLogging());
            return $"Error: unknown tool '{toolCall.Name}'.";
        }

        try
        {
            var sanitizedArgs = LogSanitizer.SanitizeJson(toolCall.ArgumentsJson);
            logger.CopilotExecutingTool(toolCall.Name.SanitizeForLogging(), sanitizedArgs.SanitizeForLogging());

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await tool.ExecuteAsync(toolCall.ArgumentsJson, context, ct);
            sw.Stop();

            logger.CopilotToolExecutionCompleted(
                toolCall.Name.SanitizeForLogging(), result.Length, sw.ElapsedMilliseconds);

            // Truncate large outputs to avoid context blowup
            if (result.Length > config.MaxToolOutputLength)
            {
                result = result[..config.MaxToolOutputLength] + "\n... (truncated)";
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.CopilotToolExecutionFailed(ex, toolCall.Name.SanitizeForLogging());
            return $"Error executing tool '{toolCall.Name}': {ex.Message}";
        }
    }

    private static LLMMessage ToLLMMessage(ChatMessageDto msg) => new()
    {
        Role = msg.Role,
        Content = msg.Content,
    };

    private async Task<string> BuildSystemPromptAsync(string projectId, string sessionId)
    {
        var scopePrompt = IsGlobalScope(projectId)
            ? """
            ## Scope
            You are in global workspace mode (no specific project is open).
            You may read across the user's projects and repositories, but do not attempt to generate or modify work items.
            """
            : $"""
            ## Scope
            You are in project-scoped mode for project id '{projectId}'.
            Keep all analysis and actions constrained to this active project.
            """;

        var attachments = await chatSessionRepository.GetAttachmentsBySessionIdAsync(projectId, sessionId) ?? [];
        if (attachments.Count == 0)
            return $"{SystemPrompt}\n\n{scopePrompt}";

        var builder = new System.Text.StringBuilder(SystemPrompt);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(scopePrompt);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("## Uploaded Reference Documents");
        builder.AppendLine("The user has uploaded the following documents for you to reference:");

        var totalLength = 0;
        foreach (var attachment in attachments)
        {
            var content = await chatSessionRepository.GetAttachmentContentAsync(projectId, attachment.Id);
            if (content is null) continue;

            if (totalLength + content.Length > MaxAttachmentContextLength)
            {
                builder.AppendLine();
                builder.AppendLine($"(Remaining documents truncated — {MaxAttachmentContextLength} char limit reached)");
                break;
            }

            builder.AppendLine();
            builder.AppendLine($"### {attachment.FileName}");
            builder.AppendLine("```markdown");
            builder.AppendLine(content);
            builder.AppendLine("```");
            totalLength += content.Length;
        }

        return builder.ToString();
    }

    private static string BuildAssistantErrorMessage(Exception exception)
    {
        var message = exception.Message;

        if (message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("quota", StringComparison.OrdinalIgnoreCase))
        {
            return "The AI service quota for this key is exhausted or disabled. Update your Azure OpenAI quota/billing settings or use a different key, then try again.";
        }

        if (message.Contains("API key", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("401", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("403", StringComparison.OrdinalIgnoreCase))
        {
            return "The AI service API key is missing or invalid. Update your Azure OpenAI API key in user secrets and restart Fleet.AppHost.";
        }

        if (message.Contains("429", StringComparison.OrdinalIgnoreCase))
        {
            return "The AI service is rate-limiting requests right now. Wait a moment and try again.";
        }

        if (message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "The AI service request timed out. Please try again.";
        }

        return "I encountered an error connecting to the AI service. Please try again.";
    }

    // ── Attachment CRUD ───────────────────────────────────────

    public async Task<ChatAttachmentDto> UploadAttachmentAsync(string projectId, string sessionId, string fileName, string content)
    {
        logger.CopilotAttachmentUploading(sessionId.SanitizeForLogging(), fileName.SanitizeForLogging(), content.Length);
        return await chatSessionRepository.AddAttachmentAsync(projectId, sessionId, fileName, content);
    }

    private async Task RefundIfNeededAsync(bool isGenerateRequest, bool chargedRun, int userId)
    {
        if (!isGenerateRequest || !chargedRun)
            return;

        await _usageLedgerService.RefundRunAsync(userId, MonthlyRunType.WorkItem);
    }

    public Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsAsync(string sessionId)
    {
        // Backward-compatible overload for existing tests/callers.
        return GetAttachmentsAsync(string.Empty, sessionId);
    }

    public async Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsAsync(string projectId, string sessionId)
    {
        return await chatSessionRepository.GetAttachmentsBySessionIdAsync(projectId, sessionId);
    }

    public Task<bool> DeleteAttachmentAsync(string attachmentId)
    {
        // Backward-compatible overload for existing tests/callers.
        return DeleteAttachmentAsync(string.Empty, string.Empty, attachmentId);
    }

    public async Task<bool> DeleteAttachmentAsync(string projectId, string sessionId, string attachmentId)
    {
        logger.CopilotAttachmentDeleting(attachmentId.SanitizeForLogging());
        return await chatSessionRepository.DeleteAttachmentAsync(projectId, sessionId, attachmentId);
    }

    private static bool IsGlobalScope(string projectId)
        => string.IsNullOrWhiteSpace(projectId);
}
