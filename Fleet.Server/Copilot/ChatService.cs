using Fleet.Server.Auth;
using Fleet.Server.Copilot.Tools;
using Fleet.Server.LLM;
using Fleet.Server.Models;
using Microsoft.Extensions.Options;

namespace Fleet.Server.Copilot;

public class ChatService(
    IChatSessionRepository chatSessionRepository,
    ILLMClient llmClient,
    ChatToolRegistry toolRegistry,
    IAuthService authService,
    IOptions<LLMOptions> llmOptions,
    ILogger<ChatService> logger) : IChatService
{
    private const string SystemPrompt = """
        You are Fleet AI, an expert software project assistant embedded in the Fleet project management app.
        You help users plan features, create and manage work items, summarise project status, and answer questions about their project.

        Guidelines:
        - Be concise and actionable. Prefer bullet points over long paragraphs.
        - When the user asks you to create work items, use the create_work_item tool.
        - When asked about the project, use get_project_info first.
        - When asked about work items, use list_work_items.
        - Always confirm what you did after using a tool (e.g. "I created work item #42: ...").
        - If you're unsure, ask a clarifying question instead of guessing.
        - Format responses in Markdown when it aids readability.
        - If the user has uploaded reference documents, use their contents to answer questions accurately.
        - When asked about the codebase or repository, use get_repo_tree to browse the file/folder structure.
        - Use read_repo_file to read specific files from the connected GitHub repository.
        """;

    /// <summary>Max total chars of attachment content to inject into the prompt.</summary>
    private const int MaxAttachmentContextLength = 50_000;

    public async Task<ChatDataDto> GetChatDataAsync(string projectId)
    {
        logger.LogInformation("Retrieving chat data for project {ProjectId}", projectId);
        var sessions = await chatSessionRepository.GetSessionsByProjectIdAsync(projectId);
        var activeSession = sessions.FirstOrDefault(s => s.IsActive);
        var messages = activeSession is not null
            ? await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, activeSession.Id)
            : [];
        var suggestions = await chatSessionRepository.GetSuggestionsAsync(projectId);

        logger.LogInformation("Retrieved {SessionCount} sessions for project {ProjectId}", sessions.Count, projectId);
        return new ChatDataDto([.. sessions], [.. messages], suggestions);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(string projectId, string sessionId)
    {
        logger.LogInformation("Retrieving messages for session {SessionId} in project {ProjectId}", sessionId, projectId);
        return await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, sessionId);
    }

    public async Task<ChatSessionDto> CreateSessionAsync(string projectId, string title)
    {
        logger.LogInformation("Creating chat session in project {ProjectId} with title: {Title}", projectId, title);
        return await chatSessionRepository.CreateSessionAsync(projectId, title);
    }

    public async Task<SendMessageResponseDto> SendMessageAsync(string projectId, string sessionId, string content)
    {
        logger.LogInformation("Sending message in session {SessionId} for project {ProjectId}", sessionId, projectId);
        var config = llmOptions.Value;

        // 1. Persist the user message
        var userMessage = await chatSessionRepository.AddMessageAsync(projectId, sessionId, "user", content);

        // 2. Load full session history for context
        var history = await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, sessionId);
        var llmMessages = history.Select(ToLLMMessage).ToList();

        // 2b. Build system prompt with any uploaded documents
        var systemPrompt = await BuildSystemPromptAsync(sessionId);

        // 3. Get tool definitions
        var toolDefs = toolRegistry.ToLLMDefinitions();

        // 4. Run the tool-calling loop with safety limits
        var toolEvents = new List<ToolEventDto>();
        var userId = (await authService.GetCurrentUserIdAsync()).ToString();
        var toolContext = new ChatToolContext(projectId, userId);
        var totalToolCalls = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));

        for (var loop = 0; loop < config.MaxToolLoops; loop++)
        {
            var request = new LLMRequest(systemPrompt, llmMessages, toolDefs);
            LLMResponse response;

            try
            {
                response = await llmClient.CompleteAsync(request, cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("LLM request timed out after {Timeout}s", config.TimeoutSeconds);
                var timeoutMsg = await chatSessionRepository.AddMessageAsync(
                    projectId, sessionId, "assistant", "I'm sorry, the request took too long. Please try again.");
                return new SendMessageResponseDto(sessionId, timeoutMsg, [.. toolEvents], null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LLM request failed");
                var errorMsg = await chatSessionRepository.AddMessageAsync(
                    projectId, sessionId, "assistant", "I encountered an error connecting to the AI service. Please try again.");
                return new SendMessageResponseDto(sessionId, errorMsg, [.. toolEvents], ex.Message);
            }

            // If the model returned tool calls, execute them
            if (response.ToolCalls is { Count: > 0 })
            {
                // Add the assistant's tool-call message to context
                llmMessages.Add(new LLMMessage
                {
                    Role = "assistant",
                    Content = response.Content,
                    ToolCalls = response.ToolCalls,
                });

                foreach (var toolCall in response.ToolCalls)
                {
                    totalToolCalls++;
                    if (totalToolCalls > config.MaxToolCallsTotal)
                    {
                        logger.LogWarning("Max total tool calls ({Max}) exceeded", config.MaxToolCallsTotal);
                        break;
                    }

                    var toolResult = await ExecuteToolAsync(toolCall, toolContext, config, cts.Token);
                    toolEvents.Add(new ToolEventDto(toolCall.Name, toolCall.ArgumentsJson, toolResult));

                    // Add tool result to context for the LLM
                    llmMessages.Add(new LLMMessage
                    {
                        Role = "tool",
                        Content = toolResult,
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name,
                    });
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

            logger.LogInformation("AI response generated for session {SessionId} (loops={Loops}, tools={Tools})",
                sessionId, loop + 1, totalToolCalls);

            return new SendMessageResponseDto(sessionId, assistantMessage, [.. toolEvents], null);
        }

        // Exhausted tool loops — force a text response
        logger.LogWarning("Exhausted tool loops ({Max}) for session {SessionId}", config.MaxToolLoops, sessionId);
        var fallbackMsg = await chatSessionRepository.AddMessageAsync(
            projectId, sessionId, "assistant",
            "I used several tools but wasn't able to finish. Here's what I found so far — could you clarify what you need?");
        return new SendMessageResponseDto(sessionId, fallbackMsg, [.. toolEvents], null);
    }

    // ── Helpers ───────────────────────────────────────────────

    private async Task<string> ExecuteToolAsync(
        LLMToolCall toolCall, ChatToolContext context, LLMOptions config, CancellationToken ct)
    {
        var tool = toolRegistry.Get(toolCall.Name);
        if (tool is null)
        {
            logger.LogWarning("Unknown tool requested: {Tool}", toolCall.Name);
            return $"Error: unknown tool '{toolCall.Name}'.";
        }

        try
        {
            logger.LogInformation("Executing tool {Tool} with args: {Args}", toolCall.Name, toolCall.ArgumentsJson);
            var result = await tool.ExecuteAsync(toolCall.ArgumentsJson, context, ct);

            // Truncate large outputs to avoid context blowup
            if (result.Length > config.MaxToolOutputLength)
            {
                result = result[..config.MaxToolOutputLength] + "\n... (truncated)";
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool {Tool} failed", toolCall.Name);
            return $"Error executing tool '{toolCall.Name}': {ex.Message}";
        }
    }

    private static LLMMessage ToLLMMessage(ChatMessageDto msg) => new()
    {
        Role = msg.Role,
        Content = msg.Content,
    };

    private async Task<string> BuildSystemPromptAsync(string sessionId)
    {
        var attachments = await chatSessionRepository.GetAttachmentsBySessionIdAsync(sessionId);
        if (attachments.Count == 0)
            return SystemPrompt;

        var builder = new System.Text.StringBuilder(SystemPrompt);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("## Uploaded Reference Documents");
        builder.AppendLine("The user has uploaded the following documents for you to reference:");

        var totalLength = 0;
        foreach (var attachment in attachments)
        {
            var content = await chatSessionRepository.GetAttachmentContentAsync(attachment.Id);
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

    // ── Attachment CRUD ───────────────────────────────────────

    public async Task<ChatAttachmentDto> UploadAttachmentAsync(string projectId, string sessionId, string fileName, string content)
    {
        logger.LogInformation("Uploading attachment '{FileName}' ({Length} chars) to session {SessionId}",
            fileName, content.Length, sessionId);
        return await chatSessionRepository.AddAttachmentAsync(sessionId, fileName, content);
    }

    public async Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsAsync(string sessionId)
    {
        return await chatSessionRepository.GetAttachmentsBySessionIdAsync(sessionId);
    }

    public async Task<bool> DeleteAttachmentAsync(string attachmentId)
    {
        logger.LogInformation("Deleting attachment {AttachmentId}", attachmentId);
        return await chatSessionRepository.DeleteAttachmentAsync(attachmentId);
    }
}
