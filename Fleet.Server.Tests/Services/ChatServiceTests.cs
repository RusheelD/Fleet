using Fleet.Server.Auth;
using Fleet.Server.Copilot;
using Fleet.Server.Copilot.Tools;
using Fleet.Server.LLM;
using Fleet.Server.Mcp;
using Fleet.Server.Memories;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Fleet.Server.Skills;
using Fleet.Server.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Reflection;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ChatServiceTests
{
    private Mock<IChatSessionRepository> _chatRepo = null!;
    private Mock<IChatAttachmentStorage> _attachmentStorage = null!;
    private Mock<ILLMClient> _llmClient = null!;
    private Mock<IAuthService> _authService = null!;
    private Mock<IUsageLedgerService> _usageLedgerService = null!;
    private Mock<IServerEventPublisher> _eventPublisher = null!;
    private Mock<ILogger<ChatService>> _logger = null!;
    private ChatToolRegistry _toolRegistry = null!;
    private IOptions<LLMOptions> _llmOptions = null!;
    private ChatService _sut = null!;

    private const string ProjectId = "proj-1";
    private const string SessionId = "sess-1";
    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _chatRepo = new Mock<IChatSessionRepository>();
        _attachmentStorage = new Mock<IChatAttachmentStorage>();
        _llmClient = new Mock<ILLMClient>();
        _authService = new Mock<IAuthService>();
        _usageLedgerService = new Mock<IUsageLedgerService>();
        _eventPublisher = new Mock<IServerEventPublisher>();
        _logger = new Mock<ILogger<ChatService>>();

        // Empty tool registry (no tools registered)
        _toolRegistry = new ChatToolRegistry([]);

        _llmOptions = Options.Create(new LLMOptions
        {
            MaxToolLoops = 5,
            MaxToolCallsTotal = 10,
            TimeoutSeconds = 30,
            GenerateTimeoutSeconds = 60,
            GenerateMaxToolLoops = 10,
            GenerateMaxToolCallsTotal = 20,
            MaxToolOutputLength = 8000
        });

        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _usageLedgerService.Setup(s => s.ChargeRunAsync(It.IsAny<int>(), It.IsAny<MonthlyRunType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _usageLedgerService.Setup(s => s.RefundRunAsync(It.IsAny<int>(), It.IsAny<MonthlyRunType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _eventPublisher.Setup(s => s.PublishProjectEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _eventPublisher.Setup(s => s.PublishUserEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.MarkStaleGeneratingSessionsAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(0);
        _chatRepo.Setup(r => r.UpdateSessionGenerationStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ChatSessionActivityDto?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.AppendSessionActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatSessionActivityDto>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.AssignPendingAttachmentsToMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.GetAttachmentsByMessageIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.RenameSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        _attachmentStorage.Setup(s => s.DeleteAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _attachmentStorage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _sut = new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            _toolRegistry,
            _authService.Object,
            _llmOptions,
            _logger.Object,
            _usageLedgerService.Object,
            _eventPublisher.Object);
    }

    // ── GetChatDataAsync ─────────────────────────────────────

    [TestMethod]
    public async Task GetChatDataAsync_WithActiveSession_ReturnsChatData()
    {
        var sessions = new List<ChatSessionDto>
        {
            new("sess-1", "Chat 1", "Hello", "2024-01-01", true),
            new("sess-2", "Chat 2", "World", "2024-01-02", false),
        };
        var messages = new List<ChatMessageDto>
        {
            new("msg-1", "user", "Hello", "2024-01-01T12:00:00"),
            new("msg-2", "assistant", "Hi there!", "2024-01-01T12:00:01"),
        };

        _chatRepo.Setup(r => r.GetSessionsByProjectIdAsync(ProjectId)).ReturnsAsync(sessions);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, "sess-1")).ReturnsAsync(messages);
        _chatRepo.Setup(r => r.GetSuggestionsAsync(ProjectId))
            .ReturnsAsync(["Tell me about this project"]);

        var result = await _sut.GetChatDataAsync(ProjectId);

        Assert.AreEqual(2, result.Sessions.Length);
        Assert.AreEqual(2, result.Messages.Length);
        Assert.AreEqual(1, result.Suggestions.Length);
    }

    [TestMethod]
    public async Task GetChatDataAsync_NoActiveSession_ReturnsEmptyMessages()
    {
        var sessions = new List<ChatSessionDto>
        {
            new("sess-1", "Chat 1", "Hello", "2024-01-01", false),
        };

        _chatRepo.Setup(r => r.GetSessionsByProjectIdAsync(ProjectId)).ReturnsAsync(sessions);
        _chatRepo.Setup(r => r.GetSuggestionsAsync(ProjectId)).ReturnsAsync([]);

        var result = await _sut.GetChatDataAsync(ProjectId);

        Assert.AreEqual(1, result.Sessions.Length);
        Assert.AreEqual(0, result.Messages.Length);
    }

    [TestMethod]
    public async Task GetChatDataAsync_NoSessions_ReturnsEmpty()
    {
        _chatRepo.Setup(r => r.GetSessionsByProjectIdAsync(ProjectId)).ReturnsAsync([]);
        _chatRepo.Setup(r => r.GetSuggestionsAsync(ProjectId)).ReturnsAsync([]);

        var result = await _sut.GetChatDataAsync(ProjectId);

        Assert.AreEqual(0, result.Sessions.Length);
        Assert.AreEqual(0, result.Messages.Length);
    }

    [TestMethod]
    public async Task GetChatDataAsync_SkipsStaleRepair_WhenSessionStillHasInFlightRequest()
    {
        var staleSession = new ChatSessionDto(
            SessionId,
            "Chat 1",
            "Generating",
            "2024-01-01",
            true,
            true,
            ChatGenerationStates.Running,
            "Generating...",
            DateTime.UtcNow.AddMinutes(-5).ToString("O"));
        _chatRepo.Setup(r => r.GetSessionsByProjectIdAsync(ProjectId))
            .ReturnsAsync(new List<ChatSessionDto> { staleSession });
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto>());
        _chatRepo.Setup(r => r.GetSuggestionsAsync(ProjectId)).ReturnsAsync([]);

        var activeRequests = GetActiveSessionRequests();
        var requestKey = $"{ProjectId}::{SessionId}";
        using var inFlightCts = new CancellationTokenSource();
        activeRequests[requestKey] = inFlightCts;

        try
        {
            var result = await _sut.GetChatDataAsync(ProjectId);

            Assert.AreEqual(1, result.Sessions.Length);
            Assert.IsTrue(result.Sessions[0].IsGenerating);
            _chatRepo.Verify(r => r.UpdateSessionGenerationStateAsync(
                ProjectId,
                SessionId,
                false,
                ChatGenerationStates.Interrupted,
                "Generation was interrupted. You can start it again.",
                It.IsAny<ChatSessionActivityDto?>(),
                It.IsAny<string?>()), Times.Never);
        }
        finally
        {
            activeRequests.TryRemove(requestKey, out _);
        }
    }

    [TestMethod]
    public async Task GetChatDataAsync_RepairsTrulyStaleGeneratingSession()
    {
        var staleSession = new ChatSessionDto(
            SessionId,
            "Chat 1",
            "Generating",
            "2024-01-01",
            true,
            true,
            ChatGenerationStates.Running,
            "Generating...",
            DateTime.UtcNow.AddMinutes(-5).ToString("O"));
        var repairedSession = staleSession with
        {
            IsGenerating = false,
            GenerationState = ChatGenerationStates.Interrupted,
            GenerationStatus = "Generation was interrupted. You can start it again.",
            GenerationUpdatedAtUtc = DateTime.UtcNow.ToString("O"),
        };
        _chatRepo.SetupSequence(r => r.GetSessionsByProjectIdAsync(ProjectId))
            .ReturnsAsync(new List<ChatSessionDto> { staleSession })
            .ReturnsAsync(new List<ChatSessionDto> { repairedSession });
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto>());
        _chatRepo.Setup(r => r.GetSuggestionsAsync(ProjectId)).ReturnsAsync([]);

        var result = await _sut.GetChatDataAsync(ProjectId);

        Assert.AreEqual(1, result.Sessions.Length);
        Assert.AreEqual(ChatGenerationStates.Interrupted, result.Sessions[0].GenerationState);
        Assert.AreEqual("Generation was interrupted. You can start it again.", result.Sessions[0].GenerationStatus);
        _chatRepo.Verify(r => r.UpdateSessionGenerationStateAsync(
            ProjectId,
            SessionId,
            false,
            ChatGenerationStates.Interrupted,
            "Generation was interrupted. You can start it again.",
            It.IsAny<ChatSessionActivityDto?>(),
            It.IsAny<string?>()), Times.Once);
    }

    // ── GetMessagesAsync ─────────────────────────────────────

    [TestMethod]
    public async Task GetMessagesAsync_DelegatesToRepo()
    {
        var messages = new List<ChatMessageDto>
        {
            new("msg-1", "user", "Hello", "2024-01-01"),
        };
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId)).ReturnsAsync(messages);

        var result = await _sut.GetMessagesAsync(ProjectId, SessionId);

        Assert.AreEqual(1, result.Count);
    }

    // ── CreateSessionAsync ───────────────────────────────────

    [TestMethod]
    public async Task CreateSessionAsync_DelegatesToRepo()
    {
        var expected = new ChatSessionDto("sess-new", "New Chat", "", "2024-01-01", true);
        _chatRepo.Setup(r => r.CreateSessionAsync(ProjectId, "New Chat")).ReturnsAsync(expected);

        var result = await _sut.CreateSessionAsync(ProjectId, "New Chat");

        Assert.AreEqual("sess-new", result.Id);
        Assert.AreEqual("New Chat", result.Title);
    }

    // ── DeleteSessionAsync ───────────────────────────────────

    [TestMethod]
    public async Task DeleteSessionAsync_Found_ReturnsTrue()
    {
        _chatRepo.Setup(r => r.DeleteSessionAsync(ProjectId, SessionId)).ReturnsAsync(true);

        var result = await _sut.DeleteSessionAsync(ProjectId, SessionId);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task DeleteSessionAsync_NotFound_ReturnsFalse()
    {
        _chatRepo.Setup(r => r.DeleteSessionAsync(ProjectId, "missing")).ReturnsAsync(false);

        var result = await _sut.DeleteSessionAsync(ProjectId, "missing");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task RenameSessionAsync_DelegatesToRepo()
    {
        _chatRepo.Setup(r => r.RenameSessionAsync(ProjectId, SessionId, "Renamed")).ReturnsAsync(true);

        var result = await _sut.RenameSessionAsync(ProjectId, SessionId, "Renamed");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task DeleteSessionAsync_CancelsInFlightMessageRequest()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "2024-01-01");
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.AssignPendingAttachmentsToMessageAsync(ProjectId, SessionId, userMsg.Id))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.DeleteSessionAsync(ProjectId, SessionId))
            .ReturnsAsync(true);

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LLMRequest, CancellationToken>(async (_, cancellationToken) =>
            {
                requestStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new LLMResponse("unreachable", null);
            });

        var sendTask = _sut.SendMessageAsync(ProjectId, SessionId, "Hello");
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var deleted = await _sut.DeleteSessionAsync(ProjectId, SessionId);

        Assert.IsTrue(deleted);
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await sendTask);
    }

    // ── SendMessageAsync ─────────────────────────────────────

    [TestMethod]
    public async Task CancelGenerationAsync_MissingSession_ReturnsFalse()
    {
        _chatRepo.Setup(r => r.GetSessionsByProjectIdAsync(ProjectId))
            .ReturnsAsync(new List<ChatSessionDto>());

        var result = await _sut.CancelGenerationAsync(ProjectId, SessionId);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CancelGenerationAsync_ClearsStuckGeneratingFlag_WhenNoInFlightRequestExists()
    {
        _chatRepo.Setup(r => r.GetSessionsByProjectIdAsync(ProjectId))
            .ReturnsAsync(new List<ChatSessionDto>
            {
                new(SessionId, "Chat 1", "Generating", "2024-01-01", true, true),
            });

        var result = await _sut.CancelGenerationAsync(ProjectId, SessionId);

        Assert.IsTrue(result);
        _chatRepo.Verify(r => r.UpdateSessionGenerationStateAsync(
            ProjectId,
            SessionId,
            false,
            ChatGenerationStates.Canceled,
            "Generation canceled.",
            It.IsAny<ChatSessionActivityDto?>()), Times.Once);
    }

    [TestMethod]
    public async Task CancelGenerationAsync_CancelsActiveDeferredGeneration()
    {
        _chatRepo.Setup(r => r.GetSessionsByProjectIdAsync(ProjectId))
            .ReturnsAsync(new List<ChatSessionDto>
            {
                new(SessionId, "Chat 1", "Generating", "2024-01-01", true, true),
            });
        var activeRequests = GetActiveSessionRequests();
        var requestKey = $"{ProjectId}::{SessionId}";
        using var inFlightCts = new CancellationTokenSource();
        activeRequests[requestKey] = inFlightCts;

        var canceled = await _sut.CancelGenerationAsync(ProjectId, SessionId);

        Assert.IsTrue(canceled);
        Assert.IsTrue(inFlightCts.IsCancellationRequested);

        _chatRepo.Verify(r => r.UpdateSessionGenerationStateAsync(
            ProjectId,
            SessionId,
            true,
            ChatGenerationStates.Canceling,
            "Canceling generation...",
            It.IsAny<ChatSessionActivityDto?>(),
            It.IsAny<string?>()), Times.AtLeastOnce);
        Assert.IsFalse(activeRequests.ContainsKey(requestKey));
    }

    [TestMethod]
    public async Task SendMessageAsync_SimpleResponse_ReturnsAssistantMessage()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "2024-01-01");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Hi there!", "2024-01-01");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Hi there!"))
            .ReturnsAsync(assistantMsg);

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("Hi there!", null));

        var result = await _sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        Assert.AreEqual(SessionId, result.SessionId);
        Assert.AreEqual("Hi there!", result.AssistantMessage.Content);
        Assert.AreEqual(0, result.ToolEvents.Length);
        Assert.IsNull(result.Error);
    }

    [TestMethod]
    public async Task SendMessageAsync_GlobalScopeGenerate_ThrowsInvalidOperation()
    {
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            _sut.SendMessageAsync(string.Empty, SessionId, "Hello", generateWorkItems: true));

        _chatRepo.Verify(r => r.AddMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task SendMessageAsync_NullContent_ReturnsDefault()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "2024-01-01");
        var defaultMsg = new ChatMessageDto("msg-2", "assistant", "I wasn't able to generate a response.", "2024-01-01");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant",
            "I wasn't able to generate a response.")).ReturnsAsync(defaultMsg);

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse(null, null));

        var result = await _sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        Assert.AreEqual("I wasn't able to generate a response.", result.AssistantMessage.Content);
    }

    [TestMethod]
    public async Task SendMessageAsync_ClaimsPendingAttachmentsForUserMessage()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "2024-01-01");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Hi there!", "2024-01-01");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.AssignPendingAttachmentsToMessageAsync(ProjectId, SessionId, userMsg.Id))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Hi there!"))
            .ReturnsAsync(assistantMsg);

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("Hi there!", null));

        await _sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        _chatRepo.Verify(
            r => r.AssignPendingAttachmentsToMessageAsync(ProjectId, SessionId, userMsg.Id),
            Times.Once);
    }

    [TestMethod]
    public async Task SendMessageAsync_LLMException_ReturnsErrorMessage()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "2024-01-01");
        var errorMsg = new ChatMessageDto("msg-2", "assistant", "I encountered an error connecting to the AI service. Please try again.", "2024-01-01");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant",
            It.IsAny<string>())).ReturnsAsync(errorMsg);

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        var result = await _sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        Assert.IsNotNull(result.Error);
        Assert.AreEqual("Connection failed", result.Error);
    }

    [TestMethod]
    public async Task SendMessageAsync_QuotaException_ReturnsQuotaMessage()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "2024-01-01");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", It.IsAny<string>()))
            .ReturnsAsync((string _, string _, string _, string content, string? __) =>
                new ChatMessageDto("msg-err", "assistant", content, "now"));

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("RESOURCE_EXHAUSTED: quota exceeded"));

        var result = await _sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        Assert.IsTrue(result.AssistantMessage.Content.Contains("quota", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SendMessageAsync_WithToolCalls_ExecutesToolsAndReturnsEvents()
    {
        // Set up a tool
        var mockTool = new Mock<IChatTool>();
        mockTool.Setup(t => t.Name).Returns("test_tool");
        mockTool.Setup(t => t.Description).Returns("A test tool");
        mockTool.Setup(t => t.ParametersJsonSchema).Returns("{}");
        mockTool.Setup(t => t.IsWriteTool).Returns(false);
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<ChatToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Tool result");

        var toolRegistryWithTools = new ChatToolRegistry([mockTool.Object]);
        var sut = new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            toolRegistryWithTools,
            _authService.Object,
            _llmOptions,
            _logger.Object);

        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Here's what I found", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Here's what I found"))
            .ReturnsAsync(assistantMsg);

        // First call returns tool calls, second returns text
        var toolCalls = new List<LLMToolCall> { new("call-1", "test_tool", "{}") };

        var callCount = 0;
        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new LLMResponse(null, toolCalls)
                    : new LLMResponse("Here's what I found", null);
            });

        var result = await sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        Assert.AreEqual(1, result.ToolEvents.Length);
        Assert.AreEqual("test_tool", result.ToolEvents[0].ToolName);
        Assert.AreEqual("Tool result", result.ToolEvents[0].Result);
        Assert.AreEqual("Here's what I found", result.AssistantMessage.Content);
    }

    [TestMethod]
    public async Task SendMessageAsync_GenerateMode_InjectsSystemMessage()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Build auth", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Generated work items", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Build auth"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Generated work items"))
            .ReturnsAsync(assistantMsg);

        LLMRequest? capturedRequest = null;
        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LLMRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new LLMResponse("Generated work items", null));

        await _sut.SendMessageAsync(ProjectId, SessionId, "Build auth", generateWorkItems: true);

        Assert.IsNotNull(capturedRequest);
        // The generate mode should add an extra message about generating work items
        Assert.IsTrue(capturedRequest.Messages.Any(m =>
            m.Content != null && m.Content.Contains("Generate work-items")));
        _usageLedgerService.Verify(s => s.ChargeRunAsync(UserId, MonthlyRunType.WorkItem, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMessageAsync_GenerateMode_DoesNotFinishBeforeMutatingWorkItems()
    {
        var writeTool = new TestChatTool(
            name: "bulk_create_work_items",
            description: "Bulk create work items",
            isWriteTool: true,
            result: "{ \"Created\": 1, \"Results\": [{ \"Id\": 101, \"Title\": \"Create auth endpoint\" }] }");

        var toolRegistryWithTools = new ChatToolRegistry([writeTool]);
        var sut = new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            toolRegistryWithTools,
            _authService.Object,
            _llmOptions,
            _logger.Object,
            _usageLedgerService.Object,
            _eventPublisher.Object);

        var userMsg = new ChatMessageDto("msg-1", "user", "Build auth", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Created work items", "now");
        var requests = new List<LLMRequest>();

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Build auth"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Created work items"))
            .ReturnsAsync(assistantMsg);

        var toolCalls = new List<LLMToolCall>
        {
            new("call-1", "bulk_create_work_items", "{\"items\":[{\"title\":\"Create auth endpoint\"}]}")
        };

        var responses = new Queue<LLMResponse>(new[]
        {
            new LLMResponse("Auth backlog", null),
            new LLMResponse("Here is the backlog I would create.", null),
            new LLMResponse(null, toolCalls),
            new LLMResponse("Created work items", null),
        });

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LLMRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(() => responses.Dequeue());

        var result = await sut.SendMessageAsync(ProjectId, SessionId, "Build auth", generateWorkItems: true);

        Assert.AreEqual(1, writeTool.ExecuteCount);
        Assert.AreEqual("Created work items", result.AssistantMessage.Content);
        Assert.IsTrue(requests.Any(request =>
            request.Messages.Any(message =>
                message.Content != null &&
                message.Content.Contains("You have not actually created or updated any work items yet", StringComparison.Ordinal))));
    }

    [TestMethod]
    public async Task SendMessageAsync_GenerateModeFailure_RefundsQuota()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Build auth", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "error", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Build auth"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", It.IsAny<string>()))
            .ReturnsAsync(assistantMsg);

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM failed"));

        await _sut.SendMessageAsync(ProjectId, SessionId, "Build auth", generateWorkItems: true);

        _usageLedgerService.Verify(s => s.RefundRunAsync(UserId, MonthlyRunType.WorkItem, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMessageAsync_DeferredGenerateStartupFailure_ReturnsAssistantError()
    {
        var backgroundSut = CreateBackgroundCapableChatService();
        var userMsg = new ChatMessageDto("msg-1", "user", "Build auth", "now");
        var assistantMsg = new ChatMessageDto(
            "msg-2",
            "assistant",
            "Monthly work-item run limit reached for the 'free' tier (4).",
            "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Build auth"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.AssignPendingAttachmentsToMessageAsync(ProjectId, SessionId, userMsg.Id))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.AddMessageAsync(
                ProjectId,
                SessionId,
                "assistant",
                "Monthly work-item run limit reached for the 'free' tier (4)."))
            .ReturnsAsync(assistantMsg);

        _usageLedgerService.Setup(s => s.ChargeRunAsync(UserId, MonthlyRunType.WorkItem, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Monthly work-item run limit reached for the 'free' tier (4)."));

        var result = await backgroundSut.SendMessageAsync(ProjectId, SessionId, "Build auth", generateWorkItems: true);

        Assert.IsFalse(result.IsDeferred);
        Assert.IsNotNull(result.AssistantMessage);
        Assert.AreEqual("Monthly work-item run limit reached for the 'free' tier (4).", result.AssistantMessage.Content);
        Assert.AreEqual("Monthly work-item run limit reached for the 'free' tier (4).", result.Error);
        _usageLedgerService.Verify(
            s => s.RefundRunAsync(UserId, MonthlyRunType.WorkItem, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SendMessageAsync_GenerateModeContinuesAfterRequestCancellation_WhenBackgroundExecutionEnabled()
    {
        var writeTool = new TestChatTool(
            name: "bulk_create_work_items",
            description: "Bulk create work items",
            isWriteTool: true,
            result: "{ \"Created\": 1, \"Results\": [{ \"Id\": 101, \"Title\": \"Create auth endpoint\" }] }");
        var backgroundSut = CreateBackgroundCapableChatService(new ChatToolRegistry([writeTool]));
        var userMsg = new ChatMessageDto("msg-1", "user", "Build auth", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Generated work items", "now");
        var assistantPersisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generationCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Build auth"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.AssignPendingAttachmentsToMessageAsync(ProjectId, SessionId, userMsg.Id))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Generated work items", It.IsAny<string?>()))
            .ReturnsAsync(assistantMsg)
            .Callback(() => assistantPersisted.TrySetResult());
        _chatRepo.Setup(r => r.UpdateSessionGenerationStateAsync(
                ProjectId,
                SessionId,
                false,
                ChatGenerationStates.Completed,
                "Work-item generation completed.",
                It.IsAny<ChatSessionActivityDto?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask)
            .Callback(() => generationCompleted.TrySetResult());

        var toolCalls = new List<LLMToolCall>
        {
            new("call-1", "bulk_create_work_items", "{\"items\":[{\"title\":\"Create auth endpoint\"}]}")
        };

        var responses = new Queue<LLMResponse>(new[]
        {
            new LLMResponse("Auth backlog", null),
            new LLMResponse(null, toolCalls),
            new LLMResponse("Generated work items", null),
        });

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responses.Dequeue());

        using var requestCts = new CancellationTokenSource();
        var response = await backgroundSut.SendMessageAsync(
            ProjectId,
            SessionId,
            "Build auth",
            generateWorkItems: true,
            cancellationToken: requestCts.Token);

        requestCts.Cancel();

        Assert.IsTrue(response.IsDeferred);
        Assert.IsNull(response.AssistantMessage);
        await assistantPersisted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await generationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _chatRepo.Verify(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Generated work items", It.IsAny<string?>()), Times.Once);
        _chatRepo.Verify(r => r.UpdateSessionGenerationStateAsync(
            ProjectId,
            SessionId,
            true,
            ChatGenerationStates.Running,
            It.IsAny<string?>(),
            It.IsAny<ChatSessionActivityDto?>(),
            It.IsAny<string?>()), Times.AtLeastOnce);
        _chatRepo.Verify(r => r.UpdateSessionGenerationStateAsync(
            ProjectId,
            SessionId,
            false,
            ChatGenerationStates.Completed,
            "Work-item generation completed.",
            It.IsAny<ChatSessionActivityDto?>(),
            It.IsAny<string?>()), Times.Once);
    }

    // ── Attachment methods ───────────────────────────────────

    [TestMethod]
    public async Task UploadAttachmentAsync_DelegatesToRepo()
    {
        var expected = CreateAttachment("att-1", "doc.md");
        var contentBytes = Encoding.UTF8.GetBytes("# Content");
        _attachmentStorage.Setup(s => s.SaveAsync(
                It.IsAny<string>(),
                "doc.md",
                "text/markdown",
                contentBytes,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredChatAttachment("ab\\att-1\\doc.md", contentBytes.Length, "text/markdown", "# Content"));
        _chatRepo.Setup(r => r.AddAttachmentAsync(
                It.IsAny<string>(),
                ProjectId,
                SessionId,
                "doc.md",
                "# Content",
                "text/markdown",
                contentBytes.Length,
                "ab\\att-1\\doc.md"))
            .ReturnsAsync(expected);

        var result = await _sut.UploadAttachmentAsync(ProjectId, SessionId, "doc.md", "text/markdown", contentBytes);

        Assert.AreEqual("att-1", result.Id);
        Assert.AreEqual("doc.md", result.FileName);
    }

    [TestMethod]
    public async Task GetAttachmentsAsync_DelegatesToRepo()
    {
        var attachments = new List<ChatAttachmentDto>
        {
            CreateAttachment("att-1", "doc.md"),
        };
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId)).ReturnsAsync(attachments);

        var result = await _sut.GetAttachmentsAsync(ProjectId, SessionId);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task DeleteAttachmentAsync_Found_ReturnsTrue()
    {
        _chatRepo.Setup(r => r.DeleteAttachmentAsync(ProjectId, SessionId, "att-1")).ReturnsAsync(true);
        _chatRepo.Setup(r => r.GetAttachmentRecordAsync("att-1", It.IsAny<string?>()))
            .ReturnsAsync(new ChatAttachmentRecord("att-1", "doc.md", 100, "2024-01-01", "text/markdown", "# Content", "stored/doc.md", SessionId, null));

        var result = await _sut.DeleteAttachmentAsync(ProjectId, SessionId, "att-1");

        Assert.IsTrue(result);
        _attachmentStorage.Verify(s => s.DeleteAsync("stored/doc.md", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteAttachmentAsync_NotFound_ReturnsFalse()
    {
        _chatRepo.Setup(r => r.DeleteAttachmentAsync(ProjectId, SessionId, "missing")).ReturnsAsync(false);

        var result = await _sut.DeleteAttachmentAsync(ProjectId, SessionId, "missing");

        Assert.IsFalse(result);
    }

    // ── BuildSystemPromptAsync with attachments ──────────────

    [TestMethod]
    public async Task SendMessageAsync_WithAttachments_IncludesInSystemPrompt()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Response", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>
            {
                CreateAttachment("att-1", "spec.md", contentType: "text/markdown", contentUrl: "/api/chat/attachments/att-1/content", markdownReference: "[spec.md](/api/chat/attachments/att-1/content)"),
            });
        _chatRepo.Setup(r => r.GetAttachmentContentAsync(ProjectId, "att-1"))
            .ReturnsAsync("# Specification\nBuild authentication module.");
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Response"))
            .ReturnsAsync(assistantMsg);

        LLMRequest? capturedRequest = null;
        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LLMRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new LLMResponse("Response", null));

        await _sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        Assert.IsNotNull(capturedRequest);
        Assert.IsTrue(capturedRequest.SystemPrompt.Contains("spec.md"));
        Assert.IsTrue(capturedRequest.SystemPrompt.Contains("Build authentication module."));
    }

    [TestMethod]
    public async Task SendMessageAsync_WithMemoryPrompt_IncludesMemoryInSystemPrompt()
    {
        var memoryService = new Mock<IMemoryService>();
        memoryService.Setup(service => service.BuildPromptBlockAsync(UserId, ProjectId, "Hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync("## Memory\nRemember the real database testing rule.");

        var sut = new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            _toolRegistry,
            _authService.Object,
            _llmOptions,
            _logger.Object,
            _usageLedgerService.Object,
            _eventPublisher.Object,
            memoryService: memoryService.Object);

        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Response", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Response"))
            .ReturnsAsync(assistantMsg);

        LLMRequest? capturedRequest = null;
        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LLMRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new LLMResponse("Response", null));

        await sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        Assert.IsNotNull(capturedRequest);
        StringAssert.Contains(capturedRequest.SystemPrompt, "Remember the real database testing rule.");
    }

    [TestMethod]
    public async Task SendMessageAsync_WithSkillPrompt_IncludesPlaybookInSystemPrompt()
    {
        var skillService = new Mock<ISkillService>();
        skillService.Setup(service => service.BuildPromptBlockAsync(UserId, ProjectId, "Hello", It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("## Playbooks\nUse the bug triage playbook.");

        var sut = new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            _toolRegistry,
            _authService.Object,
            _llmOptions,
            _logger.Object,
            _usageLedgerService.Object,
            _eventPublisher.Object,
            skillService: skillService.Object);

        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Response", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Response"))
            .ReturnsAsync(assistantMsg);

        LLMRequest? capturedRequest = null;
        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LLMRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new LLMResponse("Response", null));

        await sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        Assert.IsNotNull(capturedRequest);
        StringAssert.Contains(capturedRequest.SystemPrompt, "Use the bug triage playbook.");
    }

    [TestMethod]
    public async Task SendMessageAsync_WithWriteTool_PublishesStreamingEvents()
    {
        var writeTool = new TestChatTool(
            name: "bulk_update_work_items",
            description: "Bulk update work items",
            isWriteTool: true,
            result: "Updated 1 work item.");

        var toolRegistryWithTools = new ChatToolRegistry([writeTool]);
        var sut = new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            toolRegistryWithTools,
            _authService.Object,
            _llmOptions,
            _logger.Object,
            _usageLedgerService.Object,
            _eventPublisher.Object);

        var userMsg = new ChatMessageDto("msg-1", "user", "Generate", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Done", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Generate"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Done"))
            .ReturnsAsync(assistantMsg);

        var toolCalls = new List<LLMToolCall> { new("call-1", "bulk_update_work_items", "{}") };
        var responses = new Queue<LLMResponse>(new[]
        {
            new LLMResponse(string.Empty, null), // Session auto-naming request
            new LLMResponse(null, toolCalls),    // Main loop: tool call
            new LLMResponse("Done", null),       // Main loop: final text
        });

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responses.Dequeue());

        await sut.SendMessageAsync(ProjectId, SessionId, "Generate", generateWorkItems: true);
        Assert.AreEqual(1, writeTool.ExecuteCount);

        _eventPublisher.Verify(
            p => p.PublishProjectEventAsync(
                UserId,
                ProjectId,
                ServerEventTopics.ChatUpdated,
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _eventPublisher.Verify(
            p => p.PublishProjectEventAsync(
                UserId,
                ProjectId,
                ServerEventTopics.WorkItemsUpdated,
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _eventPublisher.Verify(
            p => p.PublishProjectEventAsync(
                UserId,
                ProjectId,
                ServerEventTopics.ChatToolEvent,
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _eventPublisher.Verify(
            p => p.PublishProjectEventAsync(
                UserId,
                ProjectId,
                ServerEventTopics.ChatSessionEvent,
                It.Is<object?>(payload =>
                    PayloadContains(payload, "\"generationState\":\"completed\"") &&
                    PayloadContains(payload, "\"isGenerating\":false")),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _eventPublisher.Verify(
            p => p.PublishUserEventAsync(
                UserId,
                ServerEventTopics.ProjectsUpdated,
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task SendMessageAsync_GenerateWorkItems_RestrictsWriteToolSurfaceToFleetBacklogTools()
    {
        var readTool = new TestChatTool(
            name: "list_work_items",
            description: "List work items",
            isWriteTool: false,
            result: "[]");
        var allowedWriteTool = new TestChatTool(
            name: "bulk_update_work_items",
            description: "Bulk update work items",
            isWriteTool: true,
            result: "Updated 1 work item.");
        var unrelatedWriteTool = new TestChatTool(
            name: "generate_mermaid_diagram",
            description: "Generate a Mermaid diagram",
            isWriteTool: true,
            result: "graph TD");
        var toolRegistryWithTools = new ChatToolRegistry([readTool, allowedWriteTool, unrelatedWriteTool]);
        var mcpSession = new StubMcpToolSession(
            [
                new LLMToolDefinition("mcp__repo__search", "Search repository", "{}"),
            ],
            ["mcp__repo__search"]);
        var mcpFactory = new RecordingMcpToolSessionFactory(mcpSession);
        var sut = new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            toolRegistryWithTools,
            _authService.Object,
            _llmOptions,
            _logger.Object,
            _usageLedgerService.Object,
            _eventPublisher.Object,
            mcpToolSessionFactory: mcpFactory);

        var userMsg = new ChatMessageDto("msg-1", "user", "Generate", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Done", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Generate"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.AssignPendingAttachmentsToMessageAsync(ProjectId, SessionId, userMsg.Id))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Done", It.IsAny<string?>()))
            .ReturnsAsync(assistantMsg);

        var toolCalls = new List<LLMToolCall> { new("call-1", "bulk_update_work_items", "{}") };
        var responses = new Queue<LLMResponse>(new[]
        {
            new LLMResponse(string.Empty, null),
            new LLMResponse(null, toolCalls),
            new LLMResponse("Done", null),
        });
        var capturedRequests = new List<LLMRequest>();

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LLMRequest, CancellationToken>((request, _) => capturedRequests.Add(request))
            .ReturnsAsync(() => responses.Dequeue());

        var result = await sut.SendMessageAsync(ProjectId, SessionId, "Generate", generateWorkItems: true);

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, allowedWriteTool.ExecuteCount);
        Assert.AreEqual(0, mcpFactory.CreateForChatCallCount);

        var generationRequest = capturedRequests.First(request => request.Tools is { Count: > 0 });
        var toolNames = generationRequest.Tools!.Select(tool => tool.Name).ToArray();
        CollectionAssert.Contains(toolNames, "list_work_items");
        CollectionAssert.Contains(toolNames, "bulk_update_work_items");
        Assert.IsFalse(toolNames.Contains("generate_mermaid_diagram"));
        Assert.IsFalse(toolNames.Contains("mcp__repo__search"));
    }

    [TestMethod]
    public async Task SendMessageAsync_AzureBadRequest_SurfacesProviderDiagnostic()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Hello", "2024-01-01");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Hello"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((string _, string _, string _, string content, string? _) =>
                new ChatMessageDto("msg-2", "assistant", content, "2024-01-01"));

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Azure OpenAI Responses API returned BadRequest: " +
                "{\"error\":{\"message\":\"Invalid schema for function 'mcp__playwright_browser_click': schema must be a JSON object.\"}}"));

        var result = await _sut.SendMessageAsync(ProjectId, SessionId, "Hello");

        Assert.IsNotNull(result.AssistantMessage);
        StringAssert.Contains(result.AssistantMessage.Content, "The AI service rejected Fleet's request configuration:");
        StringAssert.Contains(result.AssistantMessage.Content, "Invalid schema for function");
    }

    [TestMethod]
    public async Task SendMessageAsync_GenerateWorkItems_AddsExplicitGenerationOverrideToSystemPrompt()
    {
        var writeTool = new TestChatTool(
            name: "bulk_update_work_items",
            description: "Bulk update work items",
            isWriteTool: true,
            result: "Updated 1 work item.");
        var sut = new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            new ChatToolRegistry([writeTool]),
            _authService.Object,
            _llmOptions,
            _logger.Object,
            _usageLedgerService.Object,
            _eventPublisher.Object);

        var userMsg = new ChatMessageDto("msg-1", "user", "Generate", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Done", "now");
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Generate"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.AssignPendingAttachmentsToMessageAsync(ProjectId, SessionId, userMsg.Id))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Done", It.IsAny<string?>()))
            .ReturnsAsync(assistantMsg);

        var toolCalls = new List<LLMToolCall> { new("call-1", "bulk_update_work_items", "{}") };
        var responses = new Queue<LLMResponse>(new[]
        {
            new LLMResponse(string.Empty, null),
            new LLMResponse(null, toolCalls),
            new LLMResponse("Done", null),
        });
        var capturedRequests = new List<LLMRequest>();

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LLMRequest, CancellationToken>((request, _) => capturedRequests.Add(request))
            .ReturnsAsync(() => responses.Dequeue());

        await sut.SendMessageAsync(ProjectId, SessionId, "Generate", generateWorkItems: true);

        var generationRequest = capturedRequests.First(request => request.Tools is { Count: > 0 });
        StringAssert.Contains(generationRequest.SystemPrompt, "Work-item generation has been explicitly requested now.");
        StringAssert.Contains(generationRequest.SystemPrompt, "Do not ask for confirmation before creating or updating backlog items.");
    }

    [TestMethod]
    public async Task SendMessageAsync_MixedToolBatch_ExecutesAllToolsSequentially()
    {
        var executionOrder = new List<string>();
        var readOne = new OrderTrackingChatTool("read_spec", "Read spec", isWriteTool: false, "Spec loaded", executionOrder);
        var readTwo = new OrderTrackingChatTool("search_notes", "Search notes", isWriteTool: false, "Notes loaded", executionOrder);
        var writeTool = new OrderTrackingChatTool("bulk_update_work_items", "Bulk update work items", isWriteTool: true, "Updated 1 work item.", executionOrder);
        var toolRegistryWithTools = new ChatToolRegistry([readOne, readTwo, writeTool]);
        var sut = new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            toolRegistryWithTools,
            _authService.Object,
            _llmOptions,
            _logger.Object,
            _usageLedgerService.Object,
            _eventPublisher.Object);

        var userMsg = new ChatMessageDto("msg-1", "user", "Generate", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "Done", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Generate"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.AssignPendingAttachmentsToMessageAsync(ProjectId, SessionId, userMsg.Id))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId, It.IsAny<string?>()))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", "Done", It.IsAny<string?>()))
            .ReturnsAsync(assistantMsg);

        var toolCalls = new List<LLMToolCall>
        {
            new("call-1", readOne.Name, "{}"),
            new("call-2", readTwo.Name, "{}"),
            new("call-3", writeTool.Name, "{}"),
        };

        var responses = new Queue<LLMResponse>(new[]
        {
            new LLMResponse(string.Empty, null),
            new LLMResponse(null, toolCalls),
            new LLMResponse("Done", null),
        });

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responses.Dequeue());

        var result = await sut.SendMessageAsync(ProjectId, SessionId, "Generate", generateWorkItems: true);

        Assert.IsNull(result.Error);
        // All tools must execute sequentially in the order returned by the LLM,
        // regardless of read-only status, to prevent concurrent DbContext access.
        CollectionAssert.AreEqual(
            new[] { "read_spec", "search_notes", "bulk_update_work_items" },
            executionOrder);
        Assert.AreEqual(1, writeTool.ExecuteCount);
    }

    private sealed class TestChatTool(
        string name,
        string description,
        bool isWriteTool,
        string result) : IChatTool
    {
        public string Name => name;
        public string Description => description;
        public string ParametersJsonSchema => "{}";
        public bool IsWriteTool => isWriteTool;
        public int ExecuteCount { get; private set; }

        public Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class OrderTrackingChatTool(
        string name,
        string description,
        bool isWriteTool,
        string result,
        List<string> executionOrder) : IChatTool
    {
        public string Name => name;
        public string Description => description;
        public string ParametersJsonSchema => "{}";
        public bool IsWriteTool => isWriteTool;
        public int ExecuteCount { get; private set; }

        public Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            executionOrder.Add(Name);
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingMcpToolSessionFactory(IMcpToolSession session) : IMcpToolSessionFactory
    {
        public bool? LastIncludeWriteTools { get; private set; }
        public int CreateForChatCallCount { get; private set; }

        public Task<IMcpToolSession> CreateForChatAsync(int userId, bool includeWriteTools, CancellationToken cancellationToken = default)
        {
            LastIncludeWriteTools = includeWriteTools;
            CreateForChatCallCount++;
            return Task.FromResult(session);
        }

        public Task<IMcpToolSession> CreateForAgentAsync(string userId, Fleet.Server.Agents.AgentRole role, CancellationToken cancellationToken = default)
            => Task.FromResult(session);
    }

    private sealed class StubMcpToolSession(
        IReadOnlyList<LLMToolDefinition> definitions,
        IReadOnlyCollection<string> readOnlyToolNames) : IMcpToolSession
    {
        private readonly HashSet<string> _toolNames = definitions.Select(definition => definition.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _readOnlyToolNames = readOnlyToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<LLMToolDefinition> Definitions { get; } = definitions;

        public bool HasTool(string toolName) => _toolNames.Contains(toolName);

        public bool IsReadOnly(string toolName) => _readOnlyToolNames.Contains(toolName);

        public Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
            => Task.FromResult($"Executed {toolName}");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private ChatService CreateBackgroundCapableChatService(ChatToolRegistry? toolRegistry = null)
    {
        var registry = toolRegistry ?? _toolRegistry;
        var services = new ServiceCollection();
        services.AddScoped<ChatService>();
        services.AddScoped<IChatSessionRepository>(_ => _chatRepo.Object);
        services.AddScoped<IChatAttachmentStorage>(_ => _attachmentStorage.Object);
        services.AddScoped<ILLMClient>(_ => _llmClient.Object);
        services.AddScoped(_ => registry);
        services.AddScoped<IAuthService>(_ => _authService.Object);
        services.AddScoped<IOptions<LLMOptions>>(_ => _llmOptions);
        services.AddScoped<ILogger<ChatService>>(_ => _logger.Object);
        services.AddScoped<IUsageLedgerService>(_ => _usageLedgerService.Object);
        services.AddScoped<IServerEventPublisher>(_ => _eventPublisher.Object);

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new ChatService(
            _chatRepo.Object,
            _attachmentStorage.Object,
            _llmClient.Object,
            registry,
            _authService.Object,
            _llmOptions,
            _logger.Object,
            _usageLedgerService.Object,
            _eventPublisher.Object,
            scopeFactory);
    }

    private static ConcurrentDictionary<string, CancellationTokenSource> GetActiveSessionRequests()
    {
        var field = typeof(ChatService).GetField("ActiveSessionRequests", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field);
        var requests = field.GetValue(null) as ConcurrentDictionary<string, CancellationTokenSource>;
        Assert.IsNotNull(requests);
        return requests;
    }

    private static ChatAttachmentDto CreateAttachment(
        string id,
        string fileName,
        int contentLength = 100,
        string uploadedAt = "2024-01-01",
        string contentType = "application/octet-stream",
        string? contentUrl = null,
        string? markdownReference = null,
        bool isImage = false)
        => new(
            id,
            fileName,
            contentLength,
            uploadedAt,
            contentType,
            contentUrl ?? $"/api/chat/attachments/{id}/content",
            markdownReference ?? $"[{fileName}]({contentUrl ?? $"/api/chat/attachments/{id}/content"})",
            isImage);

    private static bool PayloadContains(object? payload, string fragment)
        => payload is not null &&
           JsonSerializer.Serialize(payload).Contains(fragment, StringComparison.Ordinal);
}
