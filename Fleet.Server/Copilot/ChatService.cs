using Fleet.Server.Auth;
using Fleet.Server.Diagnostics;
using Fleet.Server.Agents;
using Fleet.Server.Copilot.Tools;
using Fleet.Server.LLM;
using Fleet.Server.Logging;
using Fleet.Server.Mcp;
using Fleet.Server.Memories;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Fleet.Server.Skills;
using Fleet.Server.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Fleet.Server.Copilot;

public class ChatService(
    IChatSessionRepository chatSessionRepository,
    IChatAttachmentStorage chatAttachmentStorage,
    ILLMClient llmClient,
    ChatToolRegistry toolRegistry,
    IAuthService authService,
    IOptions<LLMOptions> llmOptions,
    ILogger<ChatService> logger,
    IUsageLedgerService? usageLedgerService = null,
    IServerEventPublisher? eventPublisher = null,
    IServiceScopeFactory? serviceScopeFactory = null,
    IMcpToolSessionFactory? mcpToolSessionFactory = null,
    IMemoryService? memoryService = null,
    ISkillService? skillService = null,
    ITokenTracker? tokenTracker = null,
    ToolLifecycleRunner? lifecycleRunner = null,
    ToolResultStore? toolResultStore = null,
    PromptBlockCache? promptBlockCache = null,
    IDynamicIterationDispatchService? dynamicIterationDispatchService = null) : IChatService
{
    private readonly IUsageLedgerService _usageLedgerService = usageLedgerService ?? NoOpUsageLedgerService.Instance;
    private readonly IServerEventPublisher? _eventPublisher = eventPublisher;
    private readonly IServiceScopeFactory? _serviceScopeFactory = serviceScopeFactory;
    private readonly IMcpToolSessionFactory _mcpToolSessionFactory = mcpToolSessionFactory ?? NoOpMcpToolSessionFactory.Instance;
    private readonly IMemoryService _memoryService = memoryService ?? NoOpMemoryService.Instance;
    private readonly ISkillService _skillService = skillService ?? NoOpSkillService.Instance;
    private readonly ToolResultStore _toolResultStore = toolResultStore ?? new ToolResultStore();
    private readonly PromptBlockCache _promptBlockCache = promptBlockCache ?? new PromptBlockCache();
    private readonly IDynamicIterationDispatchService _dynamicIterationDispatchService = dynamicIterationDispatchService ?? NoOpDynamicIterationDispatchService.Instance;
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveSessionRequests = new();
    private static readonly HashSet<string> WorkItemMutationToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_work_item",
        "update_work_item",
        "try_update_work_item",
        "bulk_create_work_items",
        "bulk_update_work_items",
        "try_bulk_update_work_items",
    };

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
    private static readonly TimeSpan GenerationHeartbeatInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan GenerationStaleAfter = TimeSpan.FromSeconds(20);

    internal sealed record DeferredGenerateWorkItemsRequest(
        string ProjectId,
        string SessionId,
        int UserId,
        string RequestKey,
        bool ChargedWorkItemRun,
        IReadOnlyList<ChatAttachmentDto> CurrentMessageAttachments,
        DynamicIterationRuntimeOptions DynamicIteration);

    internal sealed record DynamicIterationRuntimeOptions(
        bool Enabled,
        string? TargetBranch = null,
        string? ExecutionPolicy = null)
    {
        public static readonly DynamicIterationRuntimeOptions Disabled = new(false);
    }

    internal static Task RunBackgroundMemoryExtractionDetachedAsync(
        IServiceScopeFactory serviceScopeFactory,
        int userId,
        string? projectId,
        IReadOnlyList<LLMMessage> snapshot)
        => RunBackgroundMemoryExtractionDetachedCoreAsync(
            serviceScopeFactory,
            userId,
            projectId,
            snapshot);

    internal static Task RunDeferredGenerateWorkItemsDetachedAsync(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChatService> logger,
        DeferredGenerateWorkItemsRequest request,
        CancellationTokenSource sessionCts)
        => RunDeferredGenerateWorkItemsDetachedCoreAsync(
            serviceScopeFactory,
            logger,
            request,
            sessionCts);

    public async Task<ChatDataDto> GetChatDataAsync(string projectId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId
        });

        logger.CopilotChatDataRetrieving(projectId.SanitizeForLogging());

        // Run sequentially — repository methods share a scoped DbContext
        // which does not support concurrent async operations.
        var sessions = await RepairStaleGeneratingSessionsAsync(projectId);
        var suggestions = await chatSessionRepository.GetSuggestionsAsync(projectId);
        var activeSession = sessions.FirstOrDefault(s => s.IsActive);
        var messages = activeSession is not null
            ? await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, activeSession.Id)
            : [];

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

    public async Task<bool> UpdateSessionDynamicIterationAsync(
        string projectId,
        string sessionId,
        bool isEnabled,
        string? branch,
        string? policyJson)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["SessionId"] = sessionId,
            ["IsDynamicIterationEnabled"] = isEnabled
        });

        if (isEnabled && IsGlobalScope(projectId))
            throw new InvalidOperationException("Dynamic iteration is only available in project-scoped chat sessions.");

        return await chatSessionRepository.UpdateDynamicIterationAsync(projectId, sessionId, isEnabled, branch, policyJson);
    }

    public async Task<bool> DeleteSessionAsync(string projectId, string sessionId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["SessionId"] = sessionId
        });

        logger.CopilotSessionDeleting(projectId.SanitizeForLogging(), sessionId.SanitizeForLogging());
        CancelInFlightRequest(projectId, sessionId);
        var attachments = await chatSessionRepository.GetAttachmentRecordsBySessionIdAsync(projectId, sessionId);
        var deleted = await chatSessionRepository.DeleteSessionAsync(projectId, sessionId);
        if (!deleted)
            return false;

        await DeleteStoredAttachmentsAsync(attachments);
        return true;
    }

    public async Task<bool> CancelGenerationAsync(string projectId, string sessionId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["SessionId"] = sessionId
        });

        var session = (await chatSessionRepository.GetSessionsByProjectIdAsync(projectId))
            .FirstOrDefault(candidate => string.Equals(candidate.Id, sessionId, StringComparison.Ordinal));
        if (session is null)
            return false;

        var requestKey = BuildSessionRequestKey(projectId, sessionId);
        var hasInFlightRequest = ActiveSessionRequests.ContainsKey(requestKey);

        if (hasInFlightRequest)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await chatSessionRepository.UpdateSessionGenerationStateAsync(
                projectId,
                sessionId,
                true,
                ChatGenerationStates.Canceling,
                "Canceling generation...",
                BuildStatusActivity("Canceling generation..."));
            await PublishChatSessionEventAsync(
                userId,
                projectId,
                sessionId,
                true,
                ChatGenerationStates.Canceling,
                "Canceling generation...");
            CancelInFlightRequest(projectId, sessionId);
            return true;
        }

        if (session.IsGenerating)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await chatSessionRepository.UpdateSessionGenerationStateAsync(
                projectId,
                sessionId,
                false,
                ChatGenerationStates.Canceled,
                "Generation canceled.",
                BuildStatusActivity("Generation canceled."));
            await PublishChatSessionEventAsync(
                userId,
                projectId,
                sessionId,
                false,
                ChatGenerationStates.Canceled,
                "Generation canceled.");
        }

        return true;
    }

    public async Task<SendMessageResponseDto> SendMessageAsync(
        string projectId,
        string sessionId,
        string content,
        ChatSendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = options ?? new ChatSendOptions();
        var generateWorkItems = resolvedOptions.GenerateWorkItems;
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["SessionId"] = sessionId,
            ["GenerateWorkItems"] = generateWorkItems,
            ["DynamicIterationEnabledOverride"] = resolvedOptions.DynamicIteration?.Enabled,
            ["DynamicIterationExecutionPolicyOverride"] = resolvedOptions.DynamicIteration?.ExecutionPolicy,
            ["DynamicIterationTargetBranchOverride"] = resolvedOptions.DynamicIteration?.TargetBranch,
        });

        if (generateWorkItems && IsGlobalScope(projectId))
            throw new InvalidOperationException("Work-item generation is only available in project-scoped chat sessions.");

        logger.CopilotMessageSending(projectId.SanitizeForLogging(), sessionId.SanitizeForLogging(), generateWorkItems);
        var dynamicIteration = await ResolveDynamicIterationOptionsAsync(
            projectId,
            sessionId,
            generateWorkItems,
            resolvedOptions.DynamicIteration);

        if (generateWorkItems && _serviceScopeFactory is not null)
            return await StartDeferredGenerateWorkItemsAsync(projectId, sessionId, content, dynamicIteration);

        if (generateWorkItems && _serviceScopeFactory is null)
        {
            logger.LogWarning(
                "No IServiceScopeFactory available for deferred work-item generation. Falling back to inline execution for session {SessionId}.",
                sessionId.SanitizeForLogging());
        }

        return await SendMessageInlineAsync(projectId, sessionId, content, generateWorkItems, dynamicIteration, cancellationToken);
    }


    public Task<SendMessageResponseDto> SendMessageAsync(
        string projectId,
        string sessionId,
        string content,
        bool generateWorkItems,
        CancellationToken cancellationToken = default)
        => SendMessageAsync(
            projectId,
            sessionId,
            content,
            new ChatSendOptions(generateWorkItems),
            cancellationToken);

    private sealed class GenerationProgressState
    {
        public bool IsGenerating { get; set; }
        public string GenerationState { get; set; } = ChatGenerationStates.Idle;
        public string? GenerationStatus { get; set; }
    }

    private async Task<SendMessageResponseDto> SendMessageInlineAsync(
        string projectId,
        string sessionId,
        string content,
        bool generateWorkItems = false,
        DynamicIterationRuntimeOptions? dynamicIteration = null,
        CancellationToken cancellationToken = default)
    {
        dynamicIteration ??= DynamicIterationRuntimeOptions.Disabled;
        var config = llmOptions.Value;
        var timeoutSeconds = generateWorkItems ? config.GenerateTimeoutSeconds : config.TimeoutSeconds;
        var maxLoops = generateWorkItems ? config.GenerateMaxToolLoops : config.MaxToolLoops;
        var maxToolCallsTotal = generateWorkItems ? config.GenerateMaxToolCallsTotal : config.MaxToolCallsTotal;
        var requestKey = BuildSessionRequestKey(projectId, sessionId);
        var toolEvents = new List<ToolEventDto>();
        var userId = 0;
        var chargedWorkItemRun = false;
        var progress = new GenerationProgressState();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var sessionCts = RegisterInFlightRequest(requestKey);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token,
            sessionCts.Token,
            cancellationToken);
        var requestCancellation = linkedCts.Token;
        CancellationTokenSource? heartbeatCts = null;
        var heartbeatTask = Task.CompletedTask;
        IReadOnlyList<ChatAttachmentDto> messageAttachments = [];

        try
        {
            // 1. Persist the user message and claim any pending attachments for it.
            messageAttachments = await PersistUserMessageAsync(projectId, sessionId, content);

            // 1a. Mark session as generating so the UI shows a spinner on refresh
            if (generateWorkItems)
            {
                await UpdateGenerationProgressAsync(
                    projectId,
                    sessionId,
                    userId: 0,
                    progress,
                    isGenerating: true,
                    generationState: ChatGenerationStates.Running,
                    generationStatus: BuildPreparingGenerationStatus(dynamicIteration),
                    publish: false);
                heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation);
                heartbeatTask = RunGenerationHeartbeatLoopAsync(projectId, sessionId, progress, heartbeatCts.Token);
            }

            userId = await authService.GetCurrentUserIdAsync();
            if (generateWorkItems)
            {
                await _usageLedgerService.ChargeRunAsync(userId, MonthlyRunType.WorkItem);
                chargedWorkItemRun = true;
                await PublishChatUpdatedAsync(userId, projectId, sessionId);
            }
            else
            {
                await PublishChatUpdatedAsync(userId, projectId, sessionId);
            }

            // 2. Load full session history for context
            if (generateWorkItems)
            {
                await UpdateGenerationProgressAsync(
                    projectId,
                    sessionId,
                    userId,
                    progress,
                    isGenerating: true,
                    generationState: ChatGenerationStates.Running,
                    generationStatus: BuildLoadingContextStatus(dynamicIteration));
            }
            var history = await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, sessionId);
            var llmMessages = history.Select(ToLLMMessage).ToList();

            // 2a. If generation was triggered, inject the system trigger message
            if (generateWorkItems)
            {
                llmMessages.Add(new LLMMessage
                {
                    Role = "user",
                    Content = BuildGenerationTriggerMessage(dynamicIteration),
                });
            }

            // 2b. Build system prompt with any uploaded documents
            var systemPrompt = await BuildSystemPromptAsync(
                projectId,
                sessionId,
                userId,
                content,
                requestCancellation,
                conversationHistory: llmMessages,
                generateWorkItems: generateWorkItems,
                dynamicIteration: dynamicIteration);

            // 3. Get tool definitions — only include write tools when generation is requested.
            //    In generation mode, exclude single-item write tools (create/update/delete_work_item)
            //    so the LLM is forced to use bulk equivalents, reducing API round-trips and 429 errors.
            var tooling = await CreateChatToolingAsync(projectId, userId, generateWorkItems, requestCancellation);
            await using var mcpToolSession = tooling.Session;
            var toolDefs = tooling.Definitions;

            // 4. Auto-name the session before generation starts (fast model call)
            if (generateWorkItems)
            {
                await UpdateGenerationProgressAsync(
                    projectId,
                    sessionId,
                    userId,
                    progress,
                    isGenerating: true,
                    generationState: ChatGenerationStates.Running,
                    generationStatus: BuildPlanningGenerationStatus(dynamicIteration));
                await GenerateSessionNameAsync(projectId, sessionId, llmMessages, config, requestCancellation);
            }

            // 5. Run the tool-calling loop with safety limits
            var toolContext = new ChatToolContext(projectId, userId.ToString(), messageAttachments);
            var totalToolCalls = 0;
            var performedWorkItemMutation = false;

            var outputTokenCap = AdaptiveTokenCap.DefaultCap;
            var errorRecovery = new ErrorRecoveryLadder();

            for (var loop = 0; loop < maxLoops; loop++)
            {
                // Use Standard tier for generation, default model for normal chat
                var modelOverride = generateWorkItems ? config.GenerateModel : null;

                // Compress context when approaching the token budget
                var compressedMessages = ContextCompression.Compress(
                    llmMessages,
                    config.ContextWindowTokens,
                    config.ReservedOutputTokens);

                var request = AdaptiveTokenCap.ApplyCap(
                    new LLMRequest(systemPrompt, compressedMessages, toolDefs, modelOverride),
                    outputTokenCap);
                LLMResponse response;

                try
                {
                    if (generateWorkItems)
                    {
                        await UpdateGenerationProgressAsync(
                            projectId,
                            sessionId,
                            userId,
                            progress,
                            isGenerating: true,
                            generationState: ChatGenerationStates.Running,
                            generationStatus: BuildThinkingGenerationStatus(dynamicIteration));
                    }
                    response = await llmClient.CompleteAsync(request, requestCancellation);
                    tokenTracker?.Record(response.Usage);
                    outputTokenCap = AdaptiveTokenCap.GetNextCap(outputTokenCap, response.WasTruncated);
                }
                catch (OperationCanceledException) when (sessionCts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
                {
                    SetGenerationProgress(progress, false, ChatGenerationStates.Canceled, "Generation canceled.");
                    await HandleCanceledRequestAsync(projectId, sessionId, generateWorkItems, chargedWorkItemRun, userId, requestKey, sessionCts);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    logger.CopilotLlmTimeout(timeoutSeconds);
                    SetGenerationProgress(progress, false, ChatGenerationStates.Failed, "Generation timed out.");
                    var timeoutMsg = await chatSessionRepository.AddMessageAsync(
                        projectId, sessionId, "assistant", "I'm sorry, the request took too long. Please try again.");
                    await ClearGeneratingFlagAsync(
                        projectId,
                        sessionId,
                        generateWorkItems,
                        ChatGenerationStates.Failed,
                        "Generation timed out.",
                        userId: userId,
                        publishSessionEvent: true);
                    await PublishChatUpdatedAsync(userId, projectId, sessionId);
                    await RefundIfNeededAsync(generateWorkItems, chargedWorkItemRun, userId);
                    return new SendMessageResponseDto(sessionId, timeoutMsg, [.. toolEvents], null);
                }
                catch (Exception ex)
                {
                    logger.CopilotLlmFailed(ex);
                    SetGenerationProgress(progress, false, ChatGenerationStates.Failed, BuildAssistantErrorStatus(ex));
                    var assistantError = BuildAssistantErrorMessage(ex);
                    var errorMsg = await chatSessionRepository.AddMessageAsync(
                        projectId, sessionId, "assistant", assistantError);
                    await ClearGeneratingFlagAsync(
                        projectId,
                        sessionId,
                        generateWorkItems,
                        ChatGenerationStates.Failed,
                        BuildAssistantErrorStatus(ex),
                        userId: userId,
                        publishSessionEvent: true);
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

                    var exceededToolCallLimit = false;

                    // Aggregate result budget: prevent context blowup from many tool results
                    var resultBudget = new ToolResultBudget(config.MaxAggregateToolOutputChars);

                    // Execute ALL tools sequentially — tool handlers share a scoped DbContext
                    // which does not support concurrent async operations.
                    foreach (var toolCall in response.ToolCalls)
                    {
                        // Don't count write tools (create/update/delete) toward the limit —
                        // the LLM should be able to modify as many work items as needed
                        if (CountsTowardToolCallLimit(toolCall, mcpToolSession))
                        {
                            totalToolCalls++;
                            if (totalToolCalls > maxToolCallsTotal)
                            {
                                logger.CopilotMaxToolCallsExceeded(maxToolCallsTotal);
                                exceededToolCallLimit = true;
                                break;
                            }
                        }

                        if (generateWorkItems)
                        {
                            await UpdateGenerationProgressAsync(
                                projectId,
                                sessionId,
                                userId,
                                progress,
                                isGenerating: true,
                                generationState: ChatGenerationStates.Running,
                                generationStatus: $"Running {FormatToolStatus(toolCall.Name)}...");
                        }

                        var toolResult = await ExecuteToolAsync(toolCall, toolContext, config, toolDefs, mcpToolSession, requestCancellation);
                        toolResult = resultBudget.Apply(toolResult);
                        await ApplyToolResultAsync(
                            projectId,
                            sessionId,
                            userId,
                            progress,
                            generateWorkItems,
                            toolEvents,
                            llmMessages,
                            mcpToolSession,
                            toolCall,
                            toolResult);
                        if (DidWorkItemMutationSucceed(toolCall.Name, toolResult))
                        {
                            performedWorkItemMutation = true;
                        }
                    }

                    if (exceededToolCallLimit)
                        break;

                    // Check error recovery ladder after processing all tool results
                    var recoveryLevel = RecoveryLevel.None;
                    foreach (var msg in llmMessages.TakeLast(response.ToolCalls.Count))
                    {
                        if (msg.Role == "tool")
                        {
                            var isError = msg.Content?.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) == true;
                            recoveryLevel = errorRecovery.RecordResult(isError);
                        }
                    }

                    if (recoveryLevel == RecoveryLevel.Abort)
                    {
                        logger.LogWarning("Error recovery ladder reached abort threshold ({Errors} consecutive errors)",
                            errorRecovery.ConsecutiveErrors);
                        break;
                    }

                    if (recoveryLevel == RecoveryLevel.InjectHint)
                    {
                        llmMessages.Add(ErrorRecoveryLadder.CreateRecoveryHint(errorRecovery.ConsecutiveErrors));
                    }

                    // Continue the loop so the LLM can process results
                    continue;
                }

                // No tool calls — the model produced a final text response
                if (generateWorkItems && !performedWorkItemMutation)
                {
                    await UpdateGenerationProgressAsync(
                        projectId,
                        sessionId,
                        userId,
                        progress,
                        isGenerating: true,
                        generationState: ChatGenerationStates.Running,
                        generationStatus: BuildRefiningGenerationStatus(dynamicIteration));
                    llmMessages.Add(new LLMMessage
                    {
                        Role = "user",
                        Content = BuildMissingWorkItemMutationInstruction(dynamicIteration),
                    });
                    continue;
                }

                var assistantContent = response.Content ?? "I wasn't able to generate a response.";
                var dynamicDispatchResult = await AppendDynamicDispatchActivityAsync(
                    projectId,
                    sessionId,
                    userId,
                    toolEvents,
                    dynamicIteration,
                    requestCancellation);
                var assistantMessage = await chatSessionRepository.AddMessageAsync(
                    projectId, sessionId, "assistant", assistantContent);

                logger.CopilotAiResponseGenerated(sessionId.SanitizeForLogging(), loop + 1, totalToolCalls);

                var completionStatus = BuildGenerationCompletionStatus(
                    dynamicIteration,
                    performedWorkItemMutation,
                    dynamicDispatchResult);
                SetGenerationProgress(
                    progress,
                    false,
                    ChatGenerationStates.Completed,
                    completionStatus);
                await ClearGeneratingFlagAsync(
                    projectId,
                    sessionId,
                    generateWorkItems,
                    ChatGenerationStates.Completed,
                    completionStatus,
                    userId: userId,
                    publishSessionEvent: true);
                await PublishChatUpdatedAsync(userId, projectId, sessionId);

                // Fire-and-forget: extract memories from the conversation
                FireBackgroundMemoryExtraction(userId, projectId, llmMessages);

                return new SendMessageResponseDto(sessionId, assistantMessage, [.. toolEvents], null);
            }

            // Exhausted tool loops — force a text response
            logger.CopilotToolLoopExhausted(sessionId.SanitizeForLogging(), maxLoops);
            if (generateWorkItems && !performedWorkItemMutation)
            {
                var failureMsg = await chatSessionRepository.AddMessageAsync(
                    projectId,
                    sessionId,
                    "assistant",
                    BuildNoWorkItemMutationMessage(dynamicIteration));
                SetGenerationProgress(progress, false, ChatGenerationStates.Failed, BuildNoWorkItemMutationStatus(dynamicIteration));
                await ClearGeneratingFlagAsync(
                    projectId,
                    sessionId,
                    generateWorkItems,
                    ChatGenerationStates.Failed,
                    BuildNoWorkItemMutationStatus(dynamicIteration),
                    userId: userId,
                    publishSessionEvent: true);
                await PublishChatUpdatedAsync(userId, projectId, sessionId);
                await RefundIfNeededAsync(generateWorkItems, chargedWorkItemRun, userId);
                return new SendMessageResponseDto(sessionId, failureMsg, [.. toolEvents], BuildNoWorkItemMutationStatus(dynamicIteration));
            }

            var fallbackMsg = await chatSessionRepository.AddMessageAsync(
                projectId, sessionId, "assistant",
                "I used several tools but wasn't able to finish. Here's what I found so far — could you clarify what you need?");
            SetGenerationProgress(progress, false, ChatGenerationStates.Failed, "Generation stopped before a final completion.");
            await ClearGeneratingFlagAsync(
                projectId,
                sessionId,
                generateWorkItems,
                ChatGenerationStates.Failed,
                "Generation stopped before a final completion.",
                userId: userId,
                publishSessionEvent: true);
            await PublishChatUpdatedAsync(userId, projectId, sessionId);
            await RefundIfNeededAsync(generateWorkItems, chargedWorkItemRun, userId);
            return new SendMessageResponseDto(sessionId, fallbackMsg, [.. toolEvents], null);
        }
        catch (OperationCanceledException) when (sessionCts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            SetGenerationProgress(progress, false, ChatGenerationStates.Canceled, "Generation canceled.");
            await HandleCanceledRequestAsync(projectId, sessionId, generateWorkItems, chargedWorkItemRun, userId, requestKey, sessionCts);
            throw;
        }
        catch (Exception ex)
        {
            logger.CopilotLlmFailed(ex);
            SetGenerationProgress(progress, false, ChatGenerationStates.Failed, BuildAssistantErrorStatus(ex));
            var errorMessage = await PersistAssistantErrorAsync(projectId, sessionId, ex);

            await ClearGeneratingFlagAsync(
                projectId,
                sessionId,
                generateWorkItems,
                ChatGenerationStates.Failed,
                BuildAssistantErrorStatus(ex),
                userId: userId,
                publishSessionEvent: true);
            if (userId != 0)
            {
                await PublishChatUpdatedAsync(userId, projectId, sessionId);
                await RefundIfNeededAsync(generateWorkItems, chargedWorkItemRun, userId);
            }

            return new SendMessageResponseDto(sessionId, errorMessage, [.. toolEvents], ex.Message);
        }
        finally
        {
            heartbeatCts?.Cancel();
            await AwaitHeartbeatCompletionAsync(heartbeatTask);
            heartbeatCts?.Dispose();
            ReleaseInFlightRequest(requestKey, sessionCts);
        }
    }

    /// <summary>
    /// Fire-and-forget background memory extraction from a conversation.
    /// Uses a new DI scope so it doesn't depend on the request lifetime.
    /// </summary>
    private void FireBackgroundMemoryExtraction(
        int userId,
        string? projectId,
        IReadOnlyList<LLMMessage> llmMessages)
    {
        if (_serviceScopeFactory is null || llmMessages.Count < 4)
            return;

        // Snapshot the messages so the background task owns its own copy
        var snapshot = llmMessages.ToList();
        var factory = _serviceScopeFactory;

        _ = Task.Run(
            () => RunBackgroundMemoryExtractionDetachedAsync(
                factory,
                userId,
                projectId,
                snapshot),
            CancellationToken.None);
    }

    private async Task<SendMessageResponseDto> StartDeferredGenerateWorkItemsAsync(
        string projectId,
        string sessionId,
        string content,
        DynamicIterationRuntimeOptions dynamicIteration)
    {
        var requestKey = BuildSessionRequestKey(projectId, sessionId);
        CancellationTokenSource? sessionCts = null;
        var userId = 0;
        var chargedWorkItemRun = false;
        IReadOnlyList<ChatAttachmentDto> messageAttachments = [];

        try
        {
            userId = await authService.GetCurrentUserIdAsync();
            sessionCts = RegisterInFlightRequest(requestKey);
            messageAttachments = await PersistUserMessageAsync(projectId, sessionId, content);
            var queuedStatus = BuildQueuedGenerationStatus(dynamicIteration);
            await chatSessionRepository.UpdateSessionGenerationStateAsync(
                projectId,
                sessionId,
                true,
                ChatGenerationStates.Running,
                queuedStatus,
                BuildStatusActivity(queuedStatus));
            await _usageLedgerService.ChargeRunAsync(userId, MonthlyRunType.WorkItem);
            chargedWorkItemRun = true;
            await PublishChatSessionEventAsync(
                userId,
                projectId,
                sessionId,
                true,
                ChatGenerationStates.Running,
                queuedStatus);
            await PublishChatUpdatedAsync(userId, projectId, sessionId);
            logger.LogInformation(
                "Queued deferred work-item generation for session {SessionId} in project {ProjectId}",
                sessionId.SanitizeForLogging(),
                projectId.SanitizeForLogging());

            var backgroundSessionCts = sessionCts!;
            var deferredRequest = new DeferredGenerateWorkItemsRequest(
                projectId,
                sessionId,
                userId,
                requestKey,
                chargedWorkItemRun,
                messageAttachments,
                dynamicIteration);
            _ = Task.Run(
                () => RunDeferredGenerateWorkItemsDetachedAsync(
                    _serviceScopeFactory!,
                    logger,
                    deferredRequest,
                    backgroundSessionCts),
                CancellationToken.None);

            return new SendMessageResponseDto(sessionId, null, [], null, true);
        }
        catch (Exception ex)
        {
            if (sessionCts is not null)
                ReleaseInFlightRequest(requestKey, sessionCts);

            await ClearGeneratingFlagAsync(
                projectId,
                sessionId,
                wasGenerating: true,
                ChatGenerationStates.Failed,
                BuildAssistantErrorStatus(ex),
                userId: userId,
                publishSessionEvent: true);
            if (userId != 0)
            {
                await RefundIfNeededAsync(true, chargedWorkItemRun, userId);
            }

            var errorMessage = await PersistAssistantErrorAsync(projectId, sessionId, ex);
            if (userId != 0)
                await PublishChatUpdatedAsync(userId, projectId, sessionId);

            return new SendMessageResponseDto(sessionId, errorMessage, [], ex.Message);
        }
    }

    private async Task ExecutePersistedGenerateWorkItemsAsync(
        string projectId,
        string sessionId,
        int userId,
        string requestKey,
        CancellationTokenSource sessionCts,
        bool chargedWorkItemRun,
        IReadOnlyList<ChatAttachmentDto> currentMessageAttachments,
        DynamicIterationRuntimeOptions dynamicIteration)
    {
        var ownerId = userId.ToString();
        logger.LogInformation(
            "Bootstrapping deferred work-item generation state for session {SessionId} in project {ProjectId}",
            sessionId.SanitizeForLogging(),
            projectId.SanitizeForLogging());
        var config = llmOptions.Value;
        var timeoutSeconds = config.GenerateTimeoutSeconds;
        var maxLoops = config.GenerateMaxToolLoops;
        var maxToolCallsTotal = config.GenerateMaxToolCallsTotal;
        var progress = new GenerationProgressState
        {
            IsGenerating = true,
            GenerationState = ChatGenerationStates.Running,
            GenerationStatus = BuildLoadingContextStatus(dynamicIteration),
        };
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, sessionCts.Token);
        var requestCancellation = linkedCts.Token;
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation);
        var heartbeatTask = RunGenerationHeartbeatLoopAsync(projectId, sessionId, progress, heartbeatCts.Token, ownerId);

        try
        {
            await UpdateGenerationProgressAsync(
                projectId,
                sessionId,
                userId,
                progress,
                isGenerating: true,
                generationState: ChatGenerationStates.Running,
                generationStatus: BuildLoadingContextStatus(dynamicIteration),
                ownerId: ownerId);
            var history = await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, sessionId, ownerId);
            var llmMessages = history.Select(ToLLMMessage).ToList();
            var generationTrigger = BuildGenerationTriggerMessage(dynamicIteration);
            llmMessages.Add(new LLMMessage
            {
                Role = "user",
                Content = generationTrigger,
            });

            var systemPrompt = await BuildSystemPromptAsync(
                projectId,
                sessionId,
                userId,
                generationTrigger,
                requestCancellation,
                ownerId,
                conversationHistory: llmMessages,
                generateWorkItems: true,
                dynamicIteration: dynamicIteration);
            var tooling = await CreateChatToolingAsync(projectId, userId, generateWorkItems: true, requestCancellation);
            await using var mcpToolSession = tooling.Session;
            var toolDefs = tooling.Definitions;

            await UpdateGenerationProgressAsync(
                projectId,
                sessionId,
                userId,
                progress,
                isGenerating: true,
                generationState: ChatGenerationStates.Running,
                generationStatus: BuildPlanningGenerationStatus(dynamicIteration),
                ownerId: ownerId);
            await GenerateSessionNameAsync(projectId, sessionId, llmMessages, config, requestCancellation, ownerId);

            var toolContext = new ChatToolContext(projectId, userId.ToString(), currentMessageAttachments);
            var totalToolCalls = 0;
            var performedWorkItemMutation = false;
            var toolEvents = new List<ToolEventDto>();

            for (var loop = 0; loop < maxLoops; loop++)
            {
                await UpdateGenerationProgressAsync(
                    projectId,
                    sessionId,
                    userId,
                    progress,
                    isGenerating: true,
                    generationState: ChatGenerationStates.Running,
                    generationStatus: BuildThinkingGenerationStatus(dynamicIteration),
                    ownerId: ownerId);
                var request = new LLMRequest(systemPrompt, llmMessages, toolDefs, config.GenerateModel);
                var response = await llmClient.CompleteAsync(request, requestCancellation);

                logger.CopilotLlmResponseReceived(
                    loop + 1,
                    response.ToolCalls is { Count: > 0 },
                    response.ToolCalls?.Count ?? 0,
                    response.Content?.Length ?? 0);

                if (response.ToolCalls is { Count: > 0 })
                {
                    logger.CopilotToolBatchStarting(response.ToolCalls.Count, totalToolCalls);
                    llmMessages.Add(new LLMMessage
                    {
                        Role = "assistant",
                        Content = response.Content,
                        ToolCalls = response.ToolCalls,
                    });

                    var exceededToolCallLimit = false;

                    // Aggregate result budget: prevent context blowup from many tool results
                    var resultBudget = new ToolResultBudget(config.MaxAggregateToolOutputChars);

                    // Execute ALL tools sequentially — tool handlers share a scoped DbContext
                    // which does not support concurrent async operations.
                    foreach (var toolCall in response.ToolCalls)
                    {
                        if (CountsTowardToolCallLimit(toolCall, mcpToolSession))
                        {
                            totalToolCalls++;
                            if (totalToolCalls > maxToolCallsTotal)
                            {
                                logger.CopilotMaxToolCallsExceeded(maxToolCallsTotal);
                                exceededToolCallLimit = true;
                                break;
                            }
                        }

                        await UpdateGenerationProgressAsync(
                            projectId,
                            sessionId,
                            userId,
                            progress,
                            isGenerating: true,
                            generationState: ChatGenerationStates.Running,
                            generationStatus: $"Running {FormatToolStatus(toolCall.Name)}...",
                            ownerId: ownerId);
                        var toolResult = await ExecuteToolAsync(toolCall, toolContext, config, toolDefs, mcpToolSession, requestCancellation);
                        toolResult = resultBudget.Apply(toolResult);
                        await ApplyToolResultAsync(
                            projectId,
                            sessionId,
                            userId,
                            progress,
                            true,
                            toolEvents,
                            llmMessages,
                            mcpToolSession,
                            toolCall,
                            toolResult,
                            ownerId);
                        if (DidWorkItemMutationSucceed(toolCall.Name, toolResult))
                        {
                            performedWorkItemMutation = true;
                        }
                    }

                    if (exceededToolCallLimit)
                        break;

                    continue;
                }

                if (!performedWorkItemMutation)
                {
                    await UpdateGenerationProgressAsync(
                        projectId,
                        sessionId,
                        userId,
                        progress,
                        isGenerating: true,
                        generationState: ChatGenerationStates.Running,
                        generationStatus: BuildRefiningGenerationStatus(dynamicIteration),
                        ownerId: ownerId);
                    llmMessages.Add(new LLMMessage
                    {
                        Role = "user",
                        Content = BuildMissingWorkItemMutationInstruction(dynamicIteration),
                    });
                    continue;
                }

                var assistantContent = response.Content ?? "I wasn't able to generate a response.";
                var dynamicDispatchResult = await AppendDynamicDispatchActivityAsync(
                    projectId,
                    sessionId,
                    userId,
                    toolEvents,
                    dynamicIteration,
                    requestCancellation,
                    ownerId);
                await chatSessionRepository.AddMessageAsync(projectId, sessionId, "assistant", assistantContent, ownerId);
                logger.CopilotAiResponseGenerated(sessionId.SanitizeForLogging(), loop + 1, totalToolCalls);

                var completionStatus = BuildGenerationCompletionStatus(
                    dynamicIteration,
                    performedWorkItemMutation,
                    dynamicDispatchResult);
                SetGenerationProgress(
                    progress,
                    false,
                    ChatGenerationStates.Completed,
                    completionStatus);
                await ClearGeneratingFlagAsync(
                    projectId,
                    sessionId,
                    wasGenerating: true,
                    generationState: ChatGenerationStates.Completed,
                    generationStatus: completionStatus,
                    ownerId: ownerId,
                    userId: userId,
                    publishSessionEvent: true);
                await PublishChatUpdatedAsync(userId, projectId, sessionId);
                return;
            }

            logger.CopilotToolLoopExhausted(sessionId.SanitizeForLogging(), maxLoops);
            if (!performedWorkItemMutation)
            {
                await chatSessionRepository.AddMessageAsync(
                    projectId,
                    sessionId,
                    "assistant",
                    BuildNoWorkItemMutationMessage(dynamicIteration),
                    ownerId);
                SetGenerationProgress(progress, false, ChatGenerationStates.Failed, BuildNoWorkItemMutationStatus(dynamicIteration));
                await ClearGeneratingFlagAsync(
                    projectId,
                    sessionId,
                    wasGenerating: true,
                    generationState: ChatGenerationStates.Failed,
                    generationStatus: BuildNoWorkItemMutationStatus(dynamicIteration),
                    ownerId: ownerId,
                    userId: userId,
                    publishSessionEvent: true);
                await PublishChatUpdatedAsync(userId, projectId, sessionId);
                await RefundIfNeededAsync(true, chargedWorkItemRun, userId);
                return;
            }

            await chatSessionRepository.AddMessageAsync(
                projectId,
                sessionId,
                "assistant",
                "I used several tools but wasn't able to finish. Here's what I found so far - could you clarify what you need?",
                ownerId);
            SetGenerationProgress(progress, false, ChatGenerationStates.Failed, "Generation stopped before a final completion.");
            await ClearGeneratingFlagAsync(
                projectId,
                sessionId,
                wasGenerating: true,
                generationState: ChatGenerationStates.Failed,
                generationStatus: "Generation stopped before a final completion.",
                ownerId: ownerId,
                userId: userId,
                publishSessionEvent: true);
            await PublishChatUpdatedAsync(userId, projectId, sessionId);
            await RefundIfNeededAsync(true, chargedWorkItemRun, userId);
        }
        catch (OperationCanceledException) when (sessionCts.IsCancellationRequested)
        {
            SetGenerationProgress(progress, false, ChatGenerationStates.Canceled, "Generation canceled.");
            await HandleCanceledRequestAsync(projectId, sessionId, wasGenerating: true, chargedRun: chargedWorkItemRun, userId, requestKey, sessionCts, ownerId);
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.CopilotLlmTimeout(timeoutSeconds);
            SetGenerationProgress(progress, false, ChatGenerationStates.Failed, "Generation timed out.");
            await chatSessionRepository.AddMessageAsync(
                projectId,
                sessionId,
                "assistant",
                "I'm sorry, the request took too long. Please try again.",
                ownerId);
            await ClearGeneratingFlagAsync(
                projectId,
                sessionId,
                wasGenerating: true,
                generationState: ChatGenerationStates.Failed,
                generationStatus: "Generation timed out.",
                ownerId: ownerId,
                userId: userId,
                publishSessionEvent: true);
            await PublishChatUpdatedAsync(userId, projectId, sessionId);
            await RefundIfNeededAsync(true, chargedWorkItemRun, userId);
        }
        catch (Exception ex)
        {
            logger.CopilotLlmFailed(ex);
            SetGenerationProgress(progress, false, ChatGenerationStates.Failed, BuildAssistantErrorStatus(ex));
            var assistantError = BuildAssistantErrorMessage(ex);
            await chatSessionRepository.AddMessageAsync(projectId, sessionId, "assistant", assistantError, ownerId);
            await ClearGeneratingFlagAsync(
                projectId,
                sessionId,
                wasGenerating: true,
                generationState: ChatGenerationStates.Failed,
                generationStatus: BuildAssistantErrorStatus(ex),
                ownerId: ownerId,
                userId: userId,
                publishSessionEvent: true);
            await PublishChatUpdatedAsync(userId, projectId, sessionId);
            await RefundIfNeededAsync(true, chargedWorkItemRun, userId);
        }
        finally
        {
            heartbeatCts.Cancel();
            await AwaitHeartbeatCompletionAsync(heartbeatTask);
            ReleaseInFlightRequest(requestKey, sessionCts);
        }
    }

    private async Task<IReadOnlyList<ChatAttachmentDto>> PersistUserMessageAsync(string projectId, string sessionId, string content)
    {
        var userMessage = await chatSessionRepository.AddMessageAsync(projectId, sessionId, "user", content);
        await chatSessionRepository.AssignPendingAttachmentsToMessageAsync(projectId, sessionId, userMessage.Id);
        return await chatSessionRepository.GetAttachmentsByMessageIdAsync(projectId, userMessage.Id);
    }

    private async Task<DynamicIterationRuntimeOptions> ResolveDynamicIterationOptionsAsync(
        string projectId,
        string sessionId,
        bool generateWorkItems,
        DynamicIterationOptionsRequest? requestOptions)
    {
        if (!generateWorkItems || IsGlobalScope(projectId))
            return DynamicIterationRuntimeOptions.Disabled;

        var session = await TryGetSessionAsync(projectId, sessionId);
        var enabled = requestOptions?.Enabled ?? session?.IsDynamicIterationEnabled ?? false;
        if (!enabled)
            return DynamicIterationRuntimeOptions.Disabled;

        var targetBranch = NormalizeNullableString(requestOptions?.TargetBranch)
            ?? NormalizeNullableString(session?.DynamicIterationBranch);
        var executionPolicy = NormalizeExecutionPolicy(requestOptions?.ExecutionPolicy)
            ?? ResolveExecutionPolicyFromPolicyJson(session?.DynamicIterationPolicyJson)
            ?? "balanced";

        return new DynamicIterationRuntimeOptions(true, targetBranch, executionPolicy);
    }

    private async Task<ChatSessionDto?> TryGetSessionAsync(string projectId, string sessionId)
    {
        var sessions = await chatSessionRepository.GetSessionsByProjectIdAsync(projectId);
        return sessions.FirstOrDefault(session => string.Equals(session.Id, sessionId, StringComparison.Ordinal));
    }

    private static string? ResolveExecutionPolicyFromPolicyJson(string? policyJson)
    {
        if (string.IsNullOrWhiteSpace(policyJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(policyJson);
            if (TryGetPolicyString(document.RootElement, "executionPolicy", out var executionPolicy))
                return NormalizeExecutionPolicy(executionPolicy);
            if (TryGetPolicyString(document.RootElement, "strategy", out var strategy))
                return NormalizeExecutionPolicy(strategy);
        }
        catch (JsonException)
        {
            // Session policy is validated on write. Ignore stale malformed values defensively.
        }

        return null;
    }

    private static bool TryGetPolicyString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static string? NormalizeExecutionPolicy(string? executionPolicy)
    {
        var normalized = NormalizeNullableString(executionPolicy)?.ToLowerInvariant();
        return normalized is "balanced" or "parallel" or "sequential"
            ? normalized
            : null;
    }

    private static string? NormalizeNullableString(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task HandleDeferredGenerationFailureAsync(
        string projectId,
        string sessionId,
        int userId,
        string ownerId,
        bool chargedWorkItemRun,
        Exception exception)
    {
        await PersistAssistantErrorAsync(projectId, sessionId, exception, ownerId);
        await ClearGeneratingFlagAsync(
            projectId,
            sessionId,
            wasGenerating: true,
            generationState: ChatGenerationStates.Failed,
            generationStatus: BuildAssistantErrorStatus(exception),
            ownerId: ownerId,
            userId: userId,
            publishSessionEvent: true);
        await RefundIfNeededAsync(true, chargedWorkItemRun, userId);
        await PublishChatUpdatedAsync(userId, projectId, sessionId);
    }

    private async Task PublishChatToolEventAsync(
        int userId,
        string projectId,
        string sessionId,
        string toolName,
        string argumentsJson,
        string result,
        bool succeeded)
    {
        if (_eventPublisher is null)
            return;

        var payload = new
        {
            projectId = IsGlobalScope(projectId) ? null : projectId,
            sessionId,
            toolName,
            argumentsJson,
            result,
            succeeded,
            timestampUtc = DateTime.UtcNow,
        };

        try
        {
            if (IsGlobalScope(projectId))
            {
                await _eventPublisher.PublishUserEventAsync(
                    userId,
                    ServerEventTopics.ChatToolEvent,
                    payload);
                return;
            }

            await _eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.ChatToolEvent,
                payload);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to publish chat tool event for session {SessionId} and tool {ToolName}",
                sessionId.SanitizeForLogging(),
                toolName.SanitizeForLogging());
        }
    }

    private async Task<ChatMessageDto> PersistAssistantErrorAsync(string projectId, string sessionId, Exception exception, string? ownerId = null)
    {
        var assistantError = BuildAssistantErrorMessage(exception);
        return await chatSessionRepository.AddMessageAsync(projectId, sessionId, "assistant", assistantError, ownerId);
    }

    private static bool DidToolResultSucceed(string toolResult)
        => !toolResult.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);

    private static bool DidWorkItemMutationSucceed(string toolName, string toolResult)
        => WorkItemMutationToolNames.Contains(toolName) && DidToolResultSucceed(toolResult);

    private static string BuildQueuedGenerationStatus(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled ? "Queued dynamic iteration..." : "Queued work-item generation...";

    private static string BuildPreparingGenerationStatus(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled ? "Preparing dynamic iteration..." : "Preparing work-item generation...";

    private static string BuildLoadingContextStatus(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled ? "Loading iteration context..." : "Loading chat context...";

    private static string BuildPlanningGenerationStatus(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled ? "Naming and planning the iteration..." : "Naming and planning the backlog...";

    private static string BuildThinkingGenerationStatus(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled ? "Thinking through the requested code changes..." : "Thinking through the next work-item changes...";

    private static string BuildRefiningGenerationStatus(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled ? "Refining the iteration plan before applying changes..." : "Refining the backlog before applying changes...";

    private static string BuildGenerationCompletionStatus(
        DynamicIterationRuntimeOptions dynamicIteration,
        bool performedWorkItemMutation,
        DynamicIterationDispatchResult? dispatchResult = null)
    {
        if (dynamicIteration.Enabled && performedWorkItemMutation)
        {
            if (dispatchResult?.StartedCount > 0)
                return "Dynamic iteration started.";
            if (dispatchResult?.AcceptedCount > 0)
                return "Dynamic iteration queued.";
            if (dispatchResult?.FailedCount > 0)
                return "Dynamic iteration dispatch needs attention.";

            return "Dynamic iteration updated work items.";
        }

        return performedWorkItemMutation
            ? "Work-item generation completed."
            : "Response completed.";
    }

    private static string BuildGenerationTriggerMessage(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled
            ? "Create or update Fleet work items for the user's requested code iteration so Fleet can dispatch eligible work items."
            : "Generate work-items based on provided context";

    private static string BuildMissingWorkItemMutationInstruction(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled
            ? "You have not actually created or updated any Fleet work items for this iteration yet. Before responding, you must call a work-item mutation tool now, preferably bulk_create_work_items or bulk_update_work_items."
            : "You have not actually created or updated any work items yet. Before responding, you must call a work-item mutation tool now, preferably bulk_create_work_items or bulk_update_work_items.";

    private static string BuildNoWorkItemMutationMessage(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled
            ? "I wasn't able to create or update any work items for this iteration yet. Please try again or simplify the requested code change."
            : "I wasn't able to create or update any work items yet. Please try again or simplify the request.";

    private static string BuildNoWorkItemMutationStatus(DynamicIterationRuntimeOptions dynamicIteration)
        => dynamicIteration.Enabled
            ? "No dynamic iteration work-item mutation was completed."
            : "No work-item mutation was completed.";

    private async Task<ChatTooling> CreateChatToolingAsync(
        string projectId,
        int userId,
        bool generateWorkItems,
        CancellationToken cancellationToken)
    {
        // Work-item generation should stay focused on Fleet's built-in planning/work-item tools.
        // Excluding MCP tools here prevents unrelated system servers (for example Playwright)
        // from expanding the request payload or introducing schema regressions during backlog creation.
        IMcpToolSession mcpToolSession = generateWorkItems
            ? EmptyMcpToolSession.Instance
            : await _mcpToolSessionFactory.CreateForChatAsync(
                userId,
                includeWriteTools: false,
                cancellationToken);

        var definitions = toolRegistry.ToLLMDefinitions(
            includeWriteTools: generateWorkItems,
            bulkOnly: generateWorkItems,
            includeGlobalRepoTools: IsGlobalScope(projectId),
            includeNormalChatWriteTools: !generateWorkItems && !IsGlobalScope(projectId),
            workItemGenerationOnly: generateWorkItems)
            .Concat(mcpToolSession.Definitions)
            .ToList();

        if (generateWorkItems)
        {
            logger.LogDebug(
                "Prepared work-item generation tool surface with {BuiltInToolCount} built-in tools and {McpToolCount} MCP tools.",
                definitions.Count - mcpToolSession.Definitions.Count,
                mcpToolSession.Definitions.Count);
        }

        return new ChatTooling(mcpToolSession, definitions);
    }

    private async Task<IReadOnlyList<ChatSessionDto>> RepairStaleGeneratingSessionsAsync(string projectId)
    {
        try
        {
            var sessions = await chatSessionRepository.GetSessionsByProjectIdAsync(projectId);
            var cutoffUtc = DateTime.UtcNow - GenerationStaleAfter;
            var staleSessions = sessions
                .Where(session => IsSessionStaleForRepair(projectId, session, cutoffUtc))
                .ToList();

            if (staleSessions.Count == 0)
                return sessions;

            foreach (var staleSession in staleSessions)
            {
                await chatSessionRepository.UpdateSessionGenerationStateAsync(
                    projectId,
                    staleSession.Id,
                    false,
                    ChatGenerationStates.Interrupted,
                    "Generation was interrupted. You can start it again.",
                    BuildStatusActivity("Generation was interrupted. You can start it again."));
            }

            return await chatSessionRepository.GetSessionsByProjectIdAsync(projectId);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to repair stale generating sessions for project scope {ProjectId}",
                projectId.SanitizeForLogging());
            return await chatSessionRepository.GetSessionsByProjectIdAsync(projectId);
        }
    }

    private static bool IsSessionStaleForRepair(string projectId, ChatSessionDto session, DateTime cutoffUtc)
    {
        if (!session.IsGenerating)
            return false;

        if (HasActiveInFlightRequest(projectId, session.Id))
            return false;

        if (string.IsNullOrWhiteSpace(session.GenerationUpdatedAtUtc))
            return true;

        return !DateTimeOffset.TryParse(session.GenerationUpdatedAtUtc, out var updatedAtUtc) || updatedAtUtc.UtcDateTime < cutoffUtc;
    }

    private static async Task RunBackgroundMemoryExtractionDetachedCoreAsync(
        IServiceScopeFactory serviceScopeFactory,
        int userId,
        string? projectId,
        IReadOnlyList<LLMMessage> snapshot)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var extractor = scope.ServiceProvider.GetService<IMemoryExtractor>();
            if (extractor is not null)
                await extractor.ExtractAndSaveAsync(userId, projectId, snapshot, CancellationToken.None);
        }
        catch
        {
            // Non-critical — swallow all errors.
        }
    }

    private static async Task RunDeferredGenerateWorkItemsDetachedCoreAsync(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChatService> logger,
        DeferredGenerateWorkItemsRequest request,
        CancellationTokenSource sessionCts)
    {
        try
        {
            logger.LogInformation(
                "Starting deferred work-item generation background execution for session {SessionId} in project {ProjectId}",
                request.SessionId.SanitizeForLogging(),
                request.ProjectId.SanitizeForLogging());
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var scopedChatService = scope.ServiceProvider.GetRequiredService<ChatService>();
            await scopedChatService.ExecutePersistedGenerateWorkItemsAsync(
                request.ProjectId,
                request.SessionId,
                request.UserId,
                request.RequestKey,
                sessionCts,
                request.ChargedWorkItemRun,
                request.CurrentMessageAttachments,
                request.DynamicIteration);
        }
        catch (OperationCanceledException)
        {
            // Cancellation was already handled by the scoped execution path.
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Deferred work-item generation failed for session {SessionId}",
                request.SessionId.SanitizeForLogging());

            await TryHandleDeferredGenerationFailureDetachedCoreAsync(
                serviceScopeFactory,
                logger,
                request,
                sessionCts,
                ex);
        }
    }

    private static async Task TryHandleDeferredGenerationFailureDetachedCoreAsync(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChatService> logger,
        DeferredGenerateWorkItemsRequest request,
        CancellationTokenSource sessionCts,
        Exception exception)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var scopedChatService = scope.ServiceProvider.GetRequiredService<ChatService>();
            await scopedChatService.HandleDeferredGenerationFailureAsync(
                request.ProjectId,
                request.SessionId,
                request.UserId,
                request.UserId.ToString(),
                request.ChargedWorkItemRun,
                exception);
        }
        catch (Exception cleanupEx)
        {
            logger.LogWarning(
                cleanupEx,
                "Failed to clean up deferred work-item generation failure for session {SessionId}",
                request.SessionId.SanitizeForLogging());
        }
        finally
        {
            ReleaseInFlightRequest(request.RequestKey, sessionCts);
        }
    }

    private async Task UpdateGenerationProgressAsync(
        string projectId,
        string sessionId,
        int userId,
        GenerationProgressState progress,
        bool isGenerating,
        string generationState,
        string? generationStatus,
        bool publish = true,
        bool trackActivity = true,
        string? ownerId = null)
    {
        var stateChanged = progress.IsGenerating != isGenerating
            || !string.Equals(progress.GenerationState, generationState, StringComparison.Ordinal)
            || !string.Equals(progress.GenerationStatus, generationStatus, StringComparison.Ordinal);
        var activity = stateChanged && trackActivity && !string.IsNullOrWhiteSpace(generationStatus)
            ? BuildStatusActivity(generationStatus)
            : null;

        progress.IsGenerating = isGenerating;
        progress.GenerationState = generationState;
        progress.GenerationStatus = generationStatus;

        await chatSessionRepository.UpdateSessionGenerationStateAsync(
            projectId,
            sessionId,
            isGenerating,
            generationState,
            generationStatus,
            activity,
            ownerId);

        if (publish && userId != 0)
            await PublishChatSessionEventAsync(
                userId,
                projectId,
                sessionId,
                isGenerating,
                generationState,
                generationStatus,
                activity);
    }

    private async Task RunGenerationHeartbeatLoopAsync(
        string projectId,
        string sessionId,
        GenerationProgressState progress,
        CancellationToken cancellationToken,
        string? ownerId = null)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(GenerationHeartbeatInterval, cancellationToken);

                try
                {
                    await UpdateGenerationHeartbeatAsync(
                        projectId,
                        sessionId,
                        progress,
                        ownerId);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        ex,
                        "Failed to heartbeat chat generation for session {SessionId}",
                        sessionId.SanitizeForLogging());
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the generation completes or is canceled.
        }
    }

    private async Task UpdateGenerationHeartbeatAsync(
        string projectId,
        string sessionId,
        GenerationProgressState progress,
        string? ownerId)
    {
        if (_serviceScopeFactory is not null)
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var scopedRepository = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>();
            await scopedRepository.UpdateSessionGenerationStateAsync(
                projectId,
                sessionId,
                progress.IsGenerating,
                progress.GenerationState,
                progress.GenerationStatus,
                ownerId: ownerId);
            return;
        }

        await chatSessionRepository.UpdateSessionGenerationStateAsync(
            projectId,
            sessionId,
            progress.IsGenerating,
            progress.GenerationState,
            progress.GenerationStatus,
            ownerId: ownerId);
    }

    private static Task AwaitHeartbeatCompletionAsync(Task heartbeatTask)
        => heartbeatTask.IsCompleted ? Task.CompletedTask : heartbeatTask;

    private static string FormatToolStatus(string toolName)
        => toolName.Replace('_', ' ');

    private static string BuildToolCompletionStatus(string toolName, string toolResult)
        => DidToolResultSucceed(toolResult)
            ? $"{FormatToolStatus(toolName)} completed."
            : $"{FormatToolStatus(toolName)} reported an issue.";

    private static ChatSessionActivityDto BuildStatusActivity(string message)
        => new(
            Guid.NewGuid().ToString(),
            "status",
            message,
            DateTime.UtcNow.ToString("O"));

    private static void SetGenerationProgress(
        GenerationProgressState progress,
        bool isGenerating,
        string generationState,
        string? generationStatus)
    {
        progress.IsGenerating = isGenerating;
        progress.GenerationState = generationState;
        progress.GenerationStatus = generationStatus;
    }

    private static ChatSessionActivityDto BuildToolActivity(string toolName, string toolResult)
        => new(
            Guid.NewGuid().ToString(),
            "tool",
            BuildToolCompletionStatus(toolName, toolResult),
            DateTime.UtcNow.ToString("O"),
            toolName,
            DidToolResultSucceed(toolResult));

    private static ChatSessionActivityDto BuildDispatchActivity(DynamicIterationDispatchResult result)
        => new(
            Guid.NewGuid().ToString(),
            "status",
            result.BuildSummaryMessage(),
            DateTime.UtcNow.ToString("O"),
            "dynamic_iteration_dispatch",
            result.FailedCount == 0);

    private bool CountsTowardToolCallLimit(LLMToolCall toolCall, IMcpToolSession mcpToolSession)
        => !IsWriteToolCall(toolCall, mcpToolSession);

    private bool IsWriteToolCall(LLMToolCall toolCall, IMcpToolSession mcpToolSession)
    {
        var tool = toolRegistry.Get(toolCall.Name);
        if (tool is not null)
            return tool.IsWriteTool;

        return mcpToolSession.HasTool(toolCall.Name) && !mcpToolSession.IsReadOnly(toolCall.Name);
    }

    private async Task ApplyToolResultAsync(
        string projectId,
        string sessionId,
        int userId,
        GenerationProgressState progress,
        bool generateWorkItems,
        List<ToolEventDto> toolEvents,
        List<LLMMessage> llmMessages,
        IMcpToolSession mcpToolSession,
        LLMToolCall toolCall,
        string toolResult,
        string? ownerId = null)
    {
        toolEvents.Add(new ToolEventDto(toolCall.Name, toolCall.ArgumentsJson, toolResult));

        llmMessages.Add(new LLMMessage
        {
            Role = "tool",
            Content = toolResult,
            ToolCallId = toolCall.Id,
            ToolName = toolCall.Name,
        });

        await PublishChatToolEventAsync(
            userId,
            projectId,
            sessionId,
            toolCall.Name,
            toolCall.ArgumentsJson,
            toolResult,
            DidToolResultSucceed(toolResult));
        var toolActivity = BuildToolActivity(toolCall.Name, toolResult);
        await chatSessionRepository.AppendSessionActivityAsync(projectId, sessionId, toolActivity, ownerId);
        await PublishChatSessionEventAsync(
            userId,
            projectId,
            sessionId,
            progress.IsGenerating,
            progress.GenerationState,
            progress.GenerationStatus,
            toolActivity);
        if (generateWorkItems)
        {
            await UpdateGenerationProgressAsync(
                projectId,
                sessionId,
                userId,
                progress,
                isGenerating: true,
                generationState: ChatGenerationStates.Running,
                generationStatus: BuildToolCompletionStatus(toolCall.Name, toolResult),
                trackActivity: false,
                ownerId: ownerId);
        }

        await PublishChatUpdatedAsync(userId, projectId, sessionId);
        await PublishWriteSideEffectsAsync(
            userId,
            projectId,
            sessionId,
            toolCall.Name,
            IsWriteToolCall(toolCall, mcpToolSession));
    }

    private static string BuildAssistantErrorStatus(Exception exception)
        => exception.Message.Contains("quota", StringComparison.OrdinalIgnoreCase)
            ? "Generation stopped due to quota limits."
            : "Generation failed. Please try again.";

    private async Task<DynamicIterationDispatchResult?> AppendDynamicDispatchActivityAsync(
        string projectId,
        string sessionId,
        int userId,
        IReadOnlyList<ToolEventDto> toolEvents,
        DynamicIterationRuntimeOptions dynamicIteration,
        CancellationToken cancellationToken,
        string? ownerId = null)
    {
        if (!dynamicIteration.Enabled)
            return null;

        try
        {
            var dispatchResult = await _dynamicIterationDispatchService.DispatchFromToolEventsAsync(
                projectId,
                sessionId,
                userId,
                toolEvents,
                dynamicIteration.TargetBranch,
                dynamicIteration.ExecutionPolicy,
                cancellationToken);
            if (!dispatchResult.HasOutcome)
                return null;

            var activity = BuildDispatchActivity(dispatchResult);
            await chatSessionRepository.AppendSessionActivityAsync(projectId, sessionId, activity, ownerId);
            await PublishChatSessionEventAsync(
                userId,
                projectId,
                sessionId,
                isGenerating: true,
                generationState: ChatGenerationStates.Running,
                generationStatus: dispatchResult.BuildSummaryMessage(),
                activity);
            await PublishChatUpdatedAsync(userId, projectId, sessionId);
            return dispatchResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to append dynamic dispatch activity for session {SessionId}",
                sessionId.SanitizeForLogging());
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Clears the IsGenerating flag on the session when a generate request completes.
    /// Best-effort — failures are logged but do not propagate.
    /// </summary>
    private async Task ClearGeneratingFlagAsync(
        string projectId,
        string sessionId,
        bool wasGenerating,
        string generationState = ChatGenerationStates.Idle,
        string? generationStatus = null,
        string? ownerId = null,
        int? userId = null,
        bool publishSessionEvent = false)
    {
        if (!wasGenerating) return;
        try
        {
            await chatSessionRepository.UpdateSessionGenerationStateAsync(
                projectId,
                sessionId,
                false,
                generationState,
                generationStatus,
                !string.IsNullOrWhiteSpace(generationStatus)
                    ? BuildStatusActivity(generationStatus)
                    : null,
                ownerId);

            if (publishSessionEvent && userId.HasValue)
            {
                await PublishChatSessionEventAsync(
                    userId.Value,
                    projectId,
                    sessionId,
                    false,
                    generationState,
                    generationStatus);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear IsGenerating flag for session {SessionId}", sessionId.SanitizeForLogging());
        }
    }

    private async Task HandleCanceledRequestAsync(
        string projectId,
        string sessionId,
        bool wasGenerating,
        bool chargedRun,
        int userId,
        string requestKey,
        CancellationTokenSource sessionCts,
        string? ownerId = null)
    {
        if (!wasGenerating ||
            !ActiveSessionRequests.TryGetValue(requestKey, out var currentRequest) ||
            ReferenceEquals(currentRequest, sessionCts))
        {
            await ClearGeneratingFlagAsync(
                projectId,
                sessionId,
                wasGenerating,
                ChatGenerationStates.Canceled,
                "Generation canceled.",
                ownerId);
            await PublishChatSessionEventAsync(
                userId,
                projectId,
                sessionId,
                false,
                ChatGenerationStates.Canceled,
                "Generation canceled.");
        }

        await RefundIfNeededAsync(wasGenerating, chargedRun, userId);
        await PublishChatUpdatedAsync(userId, projectId, sessionId);
    }

    private static string BuildSessionRequestKey(string projectId, string sessionId)
        => $"{NormalizeScope(projectId)}::{sessionId}";

    private static string NormalizeScope(string projectId)
        => IsGlobalScope(projectId) ? "__global__" : projectId.Trim();

    private static CancellationTokenSource RegisterInFlightRequest(string requestKey)
    {
        var cancellationSource = new CancellationTokenSource();

        ActiveSessionRequests.AddOrUpdate(
            requestKey,
            _ => cancellationSource,
            (_, existing) =>
            {
                existing.Cancel();
                existing.Dispose();
                return cancellationSource;
            });

        return cancellationSource;
    }

    private void CancelInFlightRequest(string projectId, string sessionId)
    {
        var requestKey = BuildSessionRequestKey(projectId, sessionId);
        if (!ActiveSessionRequests.TryRemove(requestKey, out var cancellationSource))
            return;

        try
        {
            cancellationSource.Cancel();
        }
        finally
        {
            cancellationSource.Dispose();
        }
    }

    private static void ReleaseInFlightRequest(string requestKey, CancellationTokenSource cancellationSource)
    {
        if (!ActiveSessionRequests.TryGetValue(requestKey, out var current) || !ReferenceEquals(current, cancellationSource))
            return;

        if (ActiveSessionRequests.TryRemove(requestKey, out var removed))
        {
            removed.Dispose();
        }
    }

    private static bool HasActiveInFlightRequest(string projectId, string sessionId)
        => ActiveSessionRequests.ContainsKey(BuildSessionRequestKey(projectId, sessionId));

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

    private async Task PublishChatSessionEventAsync(
        int userId,
        string projectId,
        string sessionId,
        bool isGenerating,
        string generationState,
        string? generationStatus,
        ChatSessionActivityDto? activity = null)
    {
        if (_eventPublisher is null)
            return;

        var timestampUtc = DateTime.UtcNow.ToString("O");
        var payload = new
        {
            projectId = IsGlobalScope(projectId) ? null : projectId,
            sessionId,
            isGenerating,
            generationState,
            generationStatus,
            generationUpdatedAtUtc = timestampUtc,
            activity,
        };

        try
        {
            if (IsGlobalScope(projectId))
            {
                await _eventPublisher.PublishUserEventAsync(
                    userId,
                    ServerEventTopics.ChatSessionEvent,
                    payload);
                return;
            }

            await _eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.ChatSessionEvent,
                payload);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to publish chat session event for session {SessionId}",
                sessionId.SanitizeForLogging());
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
    /// Calls the Fast-tier model to generate a concise name for the chat session based on conversation context,
    /// then persists it. Failures are logged but do not propagate — naming is best-effort.
    /// </summary>
    private async Task GenerateSessionNameAsync(
        string projectId,
        string sessionId,
        List<LLMMessage> conversationMessages,
        LLMOptions config,
        CancellationToken cancellationToken,
        string? ownerId = null)
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
                ModelOverride: config.Model); // Use Fast tier for speed

            using var namingTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, namingTimeout.Token);
            var response = await llmClient.CompleteAsync(request, linkedCts.Token);

            var name = response.Content?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                // Cap at 60 chars to keep UI tidy
                if (name.Length > 60)
                    name = name[..60];

                await chatSessionRepository.RenameSessionAsync(projectId, sessionId, name, ownerId);
            }
        }
        catch (OperationCanceledException)
        {
            // Request cancellation or session deletion should stop naming silently.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to auto-name chat session {SessionId}", sessionId.SanitizeForLogging());
        }
    }

    private async Task<string> ExecuteToolAsync(
        LLMToolCall toolCall, ChatToolContext context, LLMOptions config,
        IReadOnlyList<LLMToolDefinition> allowedTools, IMcpToolSession mcpToolSession, CancellationToken ct)
    {
        // Guard: only execute tools that were actually sent to the LLM.
        // Models sometimes hallucinate tool calls for tools not in their definitions.
        var isAllowed = allowedTools.Any(t => string.Equals(t.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase));
        if (!isAllowed)
        {
            logger.CopilotUnknownTool(toolCall.Name.SanitizeForLogging());
            return $"Error: tool '{toolCall.Name}' is not available. Do not call it. Use only the tools provided in your tool definitions.";
        }

        // Run lifecycle hooks — BeforeExecute can veto a tool call
        if (lifecycleRunner is not null)
        {
            var hookContext = new ToolHookContext(toolCall.Name, toolCall.ArgumentsJson, context.ProjectId, context.UserId);
            var hookOverride = await lifecycleRunner.RunBeforeExecuteAsync(hookContext, ct);
            if (hookOverride is not null)
                return hookOverride;
        }

        var tool = toolRegistry.Get(toolCall.Name);
        if (tool is null && mcpToolSession.HasTool(toolCall.Name))
        {
            try
            {
                var result = await mcpToolSession.ExecuteAsync(toolCall.Name, toolCall.ArgumentsJson, ct);
                if (result.Length > config.MaxToolOutputLength)
                {
                    result = _toolResultStore.StoreIfOversized(toolCall.Id, result, config.MaxToolOutputLength);
                }

                return await RunAfterHookAsync(toolCall, context, result, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.CopilotToolExecutionFailed(ex, toolCall.Name.SanitizeForLogging());
                return $"Error executing tool '{toolCall.Name}': {ex.Message}";
            }
        }

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
                result = _toolResultStore.StoreIfOversized(toolCall.Id, result, config.MaxToolOutputLength);
            }

            return await RunAfterHookAsync(toolCall, context, result, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.CopilotToolExecutionFailed(ex, toolCall.Name.SanitizeForLogging());
            return $"Error executing tool '{toolCall.Name}': {ex.Message}";
        }
    }

    private async Task<string> RunAfterHookAsync(
        LLMToolCall toolCall, ChatToolContext context, string result, CancellationToken ct)
    {
        if (lifecycleRunner is null)
            return result;

        var hookContext = new ToolHookContext(toolCall.Name, toolCall.ArgumentsJson, context.ProjectId, context.UserId);
        return await lifecycleRunner.RunAfterExecuteAsync(hookContext, result, ct);
    }

    private static LLMMessage ToLLMMessage(ChatMessageDto msg) => new()
    {
        Role = msg.Role,
        Content = msg.Content,
    };

    private async Task<string> BuildSystemPromptAsync(
        string projectId,
        string sessionId,
        int userId,
        string? latestUserMessage,
        CancellationToken cancellationToken = default,
        string? ownerId = null,
        IReadOnlyList<LLMMessage>? conversationHistory = null,
        bool generateWorkItems = false,
        DynamicIterationRuntimeOptions? dynamicIteration = null)
    {
        dynamicIteration ??= DynamicIterationRuntimeOptions.Disabled;
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

        var builder = new StringBuilder(SystemPrompt);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(scopePrompt);

        if (generateWorkItems)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(
                """
                ## Active Mode
                Work-item generation has been explicitly requested now.
                Do not ask for confirmation before creating or updating backlog items.
                Do not refuse just because context is incomplete. Use reasonable assumptions, inspect available project context, and persist a best-effort draft backlog with Fleet's work-item tools.
                Stay focused on project analysis and backlog mutation. Ignore unrelated automation or non-backlog write tasks.
                """
            );
        }

        if (dynamicIteration.Enabled)
        {
            var targetBranch = string.IsNullOrWhiteSpace(dynamicIteration.TargetBranch)
                ? "the project's default branch strategy"
                : $"'{dynamicIteration.TargetBranch}'";
            var executionPolicy = string.IsNullOrWhiteSpace(dynamicIteration.ExecutionPolicy)
                ? "balanced"
                : dynamicIteration.ExecutionPolicy;

            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine($"""
                ## Dynamic Iteration
                Dynamic Iteration is enabled for this turn. Treat the user's message as an instruction to change the codebase through Fleet work items, similar to an agentic coding chat.
                Create or update the smallest useful set of Fleet work items for each concrete bug, task, feature, or component implied by the request.
                Prefer actionable leaf work items that can be executed independently. Keep parent/child relationships intact when the request needs a larger feature breakdown.
                Fleet will automatically start eligible work items after your tool calls complete. Target branch: {targetBranch}. Dispatch strategy: {executionPolicy}.
                After persisting the work items, summarize what was queued and call out anything that still needs user clarification.
                """);
        }

        var memoryPrompt = await _promptBlockCache.GetMemoryBlockAsync(
            _memoryService,
            userId,
            IsGlobalScope(projectId) ? null : projectId,
            latestUserMessage,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(memoryPrompt))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(memoryPrompt);
        }

        // Extract recent conversation text for skill context matching
        var conversationContext = conversationHistory?
            .Where(m => m.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m.Content!)
            .TakeLast(8)
            .ToList();

        var skillPrompt = await _promptBlockCache.GetSkillBlockAsync(
            _skillService,
            userId,
            IsGlobalScope(projectId) ? null : projectId,
            latestUserMessage,
            conversationContext,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(skillPrompt))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(skillPrompt);
        }

        var attachments = await chatSessionRepository.GetAllAttachmentsBySessionIdAsync(projectId, sessionId, ownerId) ?? [];
        if (attachments.Count == 0)
            return builder.ToString();

        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("## Uploaded References And Assets");
        builder.AppendLine("The user has uploaded the following references and assets for you to use.");
        builder.AppendLine("When an uploaded image or binary asset is relevant to a work item, preserve its provided markdown reference directly inside that work item's description or acceptance criteria so downstream builders can stage and use it.");
        builder.AppendLine("Never invent attachment URLs. Only use the exact markdown reference provided below.");

        var totalLength = 0;
        foreach (var attachment in attachments)
        {
            var content = await chatSessionRepository.GetAttachmentContentAsync(projectId, attachment.Id, ownerId);
            if (attachment.IsImage || string.IsNullOrWhiteSpace(content))
            {
                builder.AppendLine();
                builder.AppendLine($"### {attachment.FileName}");
                builder.AppendLine($"- Content type: {attachment.ContentType}");
                builder.AppendLine($"- Markdown reference: {attachment.MarkdownReference}");
                continue;
            }

            if (totalLength + content.Length > MaxAttachmentContextLength)
            {
                builder.AppendLine();
                builder.AppendLine($"(Remaining documents truncated — {MaxAttachmentContextLength} char limit reached)");
                break;
            }

            builder.AppendLine();
            builder.AppendLine($"### {attachment.FileName}");
            builder.AppendLine($"- Content type: {attachment.ContentType}");
            builder.AppendLine($"- Markdown reference: {attachment.MarkdownReference}");
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

        if (exception is InvalidOperationException &&
            !string.IsNullOrWhiteSpace(message) &&
            (message.Contains("limit reached", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("only available", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("project-scoped", StringComparison.OrdinalIgnoreCase)))
        {
            return message;
        }

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

        if (TryParseAzureResponsesApiError(message, out var statusCode, out var providerError))
        {
            if (statusCode is 401 or 403)
            {
                return "The AI service API key is missing or invalid. Update your Azure OpenAI API key in user secrets and restart Fleet.AppHost.";
            }

            if (statusCode == 429)
            {
                return "The AI service is rate-limiting requests right now. Wait a moment and try again.";
            }

            if (statusCode == 404 ||
                providerError.Contains("deployment", StringComparison.OrdinalIgnoreCase) ||
                providerError.Contains("model", StringComparison.OrdinalIgnoreCase))
            {
                return "The configured Azure OpenAI model or deployment could not be found. Update the Fleet LLM settings and try again.";
            }

            if (providerError.Contains("context", StringComparison.OrdinalIgnoreCase) ||
                providerError.Contains("too many tokens", StringComparison.OrdinalIgnoreCase) ||
                providerError.Contains("max", StringComparison.OrdinalIgnoreCase) && providerError.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                return "The AI service rejected this request because the prompt was too large. Reduce the amount of chat context or attached material and try again.";
            }

            if (statusCode is 400 or 422)
            {
                return $"The AI service rejected Fleet's request configuration: {providerError}";
            }

            if (statusCode >= 500)
            {
                return "The AI service is currently unavailable. Please try again in a moment.";
            }
        }

        if (message.Contains("429", StringComparison.OrdinalIgnoreCase))
        {
            return "The AI service is rate-limiting requests right now. Wait a moment and try again.";
        }

        if (message.Contains("A second operation was started on this context", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("A command is already in progress", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("NpgsqlOperationInProgressException", StringComparison.OrdinalIgnoreCase))
        {
            return "Fleet hit a database concurrency error while processing this request. Please try again.";
        }

        if (message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "The AI service request timed out. Please try again.";
        }

        return "I encountered an error connecting to the AI service. Please try again.";
    }

    private static bool TryParseAzureResponsesApiError(string message, out int statusCode, out string providerError)
    {
        const string prefix = "Azure OpenAI Responses API returned ";
        statusCode = 0;
        providerError = string.Empty;

        var prefixIndex = message.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0)
            return false;

        var remainder = message[(prefixIndex + prefix.Length)..].Trim();
        var separatorIndex = remainder.IndexOf(':');
        var statusToken = separatorIndex >= 0 ? remainder[..separatorIndex].Trim() : remainder;
        providerError = separatorIndex >= 0 ? remainder[(separatorIndex + 1)..].Trim() : string.Empty;

        statusCode = ParseHttpStatusCodeToken(statusToken);
        providerError = NormalizeProviderError(
            TryExtractProviderErrorMessage(providerError) ?? providerError);
        return statusCode != 0 || !string.IsNullOrWhiteSpace(providerError);
    }

    private static int ParseHttpStatusCodeToken(string statusToken)
    {
        if (int.TryParse(statusToken, out var numericStatus))
            return numericStatus;

        if (statusToken.StartsWith("BadRequest", StringComparison.OrdinalIgnoreCase))
            return 400;
        if (statusToken.StartsWith("Unauthorized", StringComparison.OrdinalIgnoreCase))
            return 401;
        if (statusToken.StartsWith("Forbidden", StringComparison.OrdinalIgnoreCase))
            return 403;
        if (statusToken.StartsWith("NotFound", StringComparison.OrdinalIgnoreCase))
            return 404;
        if (statusToken.StartsWith("Conflict", StringComparison.OrdinalIgnoreCase))
            return 409;
        if (statusToken.StartsWith("TooManyRequests", StringComparison.OrdinalIgnoreCase))
            return 429;
        if (statusToken.StartsWith("Unprocessable", StringComparison.OrdinalIgnoreCase))
            return 422;
        if (statusToken.StartsWith("InternalServerError", StringComparison.OrdinalIgnoreCase))
            return 500;
        if (statusToken.StartsWith("BadGateway", StringComparison.OrdinalIgnoreCase))
            return 502;
        if (statusToken.StartsWith("ServiceUnavailable", StringComparison.OrdinalIgnoreCase))
            return 503;
        if (statusToken.StartsWith("GatewayTimeout", StringComparison.OrdinalIgnoreCase))
            return 504;

        return 0;
    }

    private static string? TryExtractProviderErrorMessage(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("error", out var errorElement))
            {
                if (errorElement.TryGetProperty("message", out var errorMessage) &&
                    errorMessage.ValueKind == JsonValueKind.String)
                {
                    return errorMessage.GetString();
                }

                if (errorElement.TryGetProperty("innererror", out var innerErrorElement) &&
                    innerErrorElement.TryGetProperty("message", out var innerMessage) &&
                    innerMessage.ValueKind == JsonValueKind.String)
                {
                    return innerMessage.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // The provider sometimes returns plain text bodies; fall back to the raw payload.
        }

        return null;
    }

    private static string NormalizeProviderError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "The provider did not return additional details.";

        var normalized = string.Join(
            ' ',
            value.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            .Trim();

        const int maxLength = 240;
        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..(maxLength - 3)]}...";
    }

    // ── Attachment CRUD ───────────────────────────────────────

    public async Task<ChatAttachmentDto> UploadAttachmentAsync(
        string projectId,
        string sessionId,
        string fileName,
        string? contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        logger.CopilotAttachmentUploading(sessionId.SanitizeForLogging(), fileName.SanitizeForLogging(), content.Length);
        var attachmentId = Guid.NewGuid().ToString();
        var storedAttachment = await chatAttachmentStorage.SaveAsync(
            attachmentId,
            fileName,
            contentType,
            content,
            cancellationToken);

        try
        {
            return await chatSessionRepository.AddAttachmentAsync(
                attachmentId,
                projectId,
                sessionId,
                fileName,
                storedAttachment.ExtractedText,
                storedAttachment.ContentType,
                storedAttachment.ContentLength,
                storedAttachment.StoragePath);
        }
        catch
        {
            await chatAttachmentStorage.DeleteAsync(storedAttachment.StoragePath, cancellationToken);
            throw;
        }
    }

    private async Task RefundIfNeededAsync(bool isGenerateRequest, bool chargedRun, int userId)
    {
        if (!isGenerateRequest || !chargedRun)
            return;

        try
        {
            await _usageLedgerService.RefundRunAsync(userId, MonthlyRunType.WorkItem);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to refund work-item generation usage for user {UserId}",
                userId);
        }
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

    public async Task<ChatAttachmentContentResult?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await chatSessionRepository.GetAttachmentRecordAsync(attachmentId);
        if (attachment is null)
            return null;

        byte[]? content = null;
        if (!string.IsNullOrWhiteSpace(attachment.StoragePath))
            content = await chatAttachmentStorage.ReadAsync(attachment.StoragePath, cancellationToken);

        if (content is null)
        {
            content = Encoding.UTF8.GetBytes(attachment.Content ?? string.Empty);
        }

        return new ChatAttachmentContentResult(
            attachment.FileName,
            string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
            content);
    }

    public Task<bool> DeleteAttachmentAsync(string attachmentId)
    {
        // Backward-compatible overload for existing tests/callers.
        return DeleteAttachmentAsync(string.Empty, string.Empty, attachmentId);
    }

    public async Task<bool> DeleteAttachmentAsync(string projectId, string sessionId, string attachmentId)
    {
        logger.CopilotAttachmentDeleting(attachmentId.SanitizeForLogging());
        var attachment = await chatSessionRepository.GetAttachmentRecordAsync(attachmentId);
        var deleted = await chatSessionRepository.DeleteAttachmentAsync(projectId, sessionId, attachmentId);
        if (deleted)
            await chatAttachmentStorage.DeleteAsync(attachment?.StoragePath);

        return deleted;
    }

    private static bool IsGlobalScope(string projectId)
        => string.IsNullOrWhiteSpace(projectId);

    private async Task DeleteStoredAttachmentsAsync(IReadOnlyList<ChatAttachmentRecord> attachments)
    {
        if (attachments is null || attachments.Count == 0)
            return;

        foreach (var attachment in attachments)
        {
            await chatAttachmentStorage.DeleteAsync(attachment.StoragePath);
        }
    }

    private sealed record ChatTooling(
        IMcpToolSession Session,
        IReadOnlyList<LLMToolDefinition> Definitions);

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
