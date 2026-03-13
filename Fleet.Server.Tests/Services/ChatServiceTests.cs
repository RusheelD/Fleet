using Fleet.Server.Auth;
using Fleet.Server.Copilot;
using Fleet.Server.Copilot.Tools;
using Fleet.Server.LLM;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Fleet.Server.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ChatServiceTests
{
    private Mock<IChatSessionRepository> _chatRepo = null!;
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

        _sut = new ChatService(
            _chatRepo.Object,
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
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAllAttachmentsBySessionIdAsync(ProjectId, SessionId))
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
            .ReturnsAsync((string _, string _, string _, string content) =>
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
            _chatRepo.Object, _llmClient.Object, toolRegistryWithTools,
            _authService.Object, _llmOptions, _logger.Object);

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
    public async Task SendMessageAsync_GenerateModeFailure_RefundsQuota()
    {
        var userMsg = new ChatMessageDto("msg-1", "user", "Build auth", "now");
        var assistantMsg = new ChatMessageDto("msg-2", "assistant", "error", "now");

        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "user", "Build auth"))
            .ReturnsAsync(userMsg);
        _chatRepo.Setup(r => r.SetSessionGeneratingAsync(ProjectId, SessionId, true))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
        _chatRepo.Setup(r => r.AddMessageAsync(ProjectId, SessionId, "assistant", It.IsAny<string>()))
            .ReturnsAsync(assistantMsg);

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM failed"));

        await _sut.SendMessageAsync(ProjectId, SessionId, "Build auth", generateWorkItems: true);

        _usageLedgerService.Verify(s => s.RefundRunAsync(UserId, MonthlyRunType.WorkItem, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Attachment methods ───────────────────────────────────

    [TestMethod]
    public async Task UploadAttachmentAsync_DelegatesToRepo()
    {
        var expected = new ChatAttachmentDto("att-1", "doc.md", 100, "2024-01-01");
        _chatRepo.Setup(r => r.AddAttachmentAsync(ProjectId, SessionId, "doc.md", "# Content"))
            .ReturnsAsync(expected);

        var result = await _sut.UploadAttachmentAsync(ProjectId, SessionId, "doc.md", "# Content");

        Assert.AreEqual("att-1", result.Id);
        Assert.AreEqual("doc.md", result.FileName);
    }

    [TestMethod]
    public async Task GetAttachmentsAsync_DelegatesToRepo()
    {
        var attachments = new List<ChatAttachmentDto>
        {
            new("att-1", "doc.md", 100, "2024-01-01"),
        };
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId)).ReturnsAsync(attachments);

        var result = await _sut.GetAttachmentsAsync(ProjectId, SessionId);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task DeleteAttachmentAsync_Found_ReturnsTrue()
    {
        _chatRepo.Setup(r => r.DeleteAttachmentAsync(ProjectId, SessionId, "att-1")).ReturnsAsync(true);

        var result = await _sut.DeleteAttachmentAsync(ProjectId, SessionId, "att-1");

        Assert.IsTrue(result);
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
                new("att-1", "spec.md", 50, "2024-01-01"),
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
        _chatRepo.Setup(r => r.SetSessionGeneratingAsync(ProjectId, SessionId, true))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.SetSessionGeneratingAsync(ProjectId, SessionId, false))
            .Returns(Task.CompletedTask);
        _chatRepo.Setup(r => r.GetMessagesBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatMessageDto> { userMsg });
        _chatRepo.Setup(r => r.GetAttachmentsBySessionIdAsync(ProjectId, SessionId))
            .ReturnsAsync(new List<ChatAttachmentDto>());
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
            p => p.PublishUserEventAsync(
                UserId,
                ServerEventTopics.ProjectsUpdated,
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
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
}

