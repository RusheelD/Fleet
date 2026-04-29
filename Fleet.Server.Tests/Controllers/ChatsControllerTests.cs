using Fleet.Server.Controllers;
using Fleet.Server.Copilot;
using Fleet.Server.Models;
using Fleet.Server.Auth;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class ChatsControllerTests
{
    private Mock<IChatService> _chatService = null!;
    private Mock<IAuthService> _authService = null!;
    private Mock<IServerEventPublisher> _eventPublisher = null!;
    private ChatsController _sut = null!;

    private const string ProjectId = "proj-1";
    private const string SessionId = "sess-1";

    [TestInitialize]
    public void Setup()
    {
        _chatService = new Mock<IChatService>();
        _authService = new Mock<IAuthService>();
        _eventPublisher = new Mock<IServerEventPublisher>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(42);
        _sut = new ChatsController(_chatService.Object, _authService.Object, _eventPublisher.Object);
    }

    // ── GetChatData ──────────────────────────────────────

    [TestMethod]
    public async Task GetChatData_ReturnsOk()
    {
        var data = new ChatDataDto([], [], []);
        _chatService.Setup(s => s.GetChatDataAsync(ProjectId)).ReturnsAsync(data);

        var result = await _sut.GetChatData(ProjectId);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(data, ok.Value);
    }

    // ── GetMessages ──────────────────────────────────────

    [TestMethod]
    public async Task GetMessages_ReturnsOk()
    {
        var messages = new List<ChatMessageDto>();
        _chatService.Setup(s => s.GetMessagesAsync(ProjectId, SessionId)).ReturnsAsync(messages);

        var result = await _sut.GetMessages(ProjectId, SessionId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // ── CreateSession ────────────────────────────────────

    [TestMethod]
    public async Task CreateSession_ReturnsCreated()
    {
        var session = new ChatSessionDto("new-id", "Title", "", "", true);
        _chatService.Setup(s => s.CreateSessionAsync(ProjectId, "Title")).ReturnsAsync(session);

        var result = await _sut.CreateSession(ProjectId, new CreateSessionRequest("Title"));

        var created = result as CreatedResult;
        Assert.IsNotNull(created);
        Assert.AreSame(session, created.Value);
        Assert.IsTrue(created.Location!.Contains("new-id"));
    }

    // ── DeleteSession ────────────────────────────────────

    [TestMethod]
    public async Task DeleteSession_Found_ReturnsNoContent()
    {
        _chatService.Setup(s => s.DeleteSessionAsync(ProjectId, SessionId)).ReturnsAsync(true);

        var result = await _sut.DeleteSession(ProjectId, SessionId);

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task DeleteSession_NotFound_Returns404()
    {
        _chatService.Setup(s => s.DeleteSessionAsync(ProjectId, SessionId)).ReturnsAsync(false);

        var result = await _sut.DeleteSession(ProjectId, SessionId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task CancelGeneration_Found_ReturnsNoContent()
    {
        _chatService.Setup(s => s.CancelGenerationAsync(ProjectId, SessionId)).ReturnsAsync(true);

        var result = await _sut.CancelGeneration(ProjectId, SessionId);

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task CancelGeneration_NotFound_Returns404()
    {
        _chatService.Setup(s => s.CancelGenerationAsync(ProjectId, SessionId)).ReturnsAsync(false);

        var result = await _sut.CancelGeneration(ProjectId, SessionId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task RenameSession_Found_ReturnsNoContent()
    {
        _chatService.Setup(s => s.RenameSessionAsync(ProjectId, SessionId, "Renamed")).ReturnsAsync(true);

        var result = await _sut.RenameSession(ProjectId, SessionId, new RenameSessionRequest("Renamed"));

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task RenameSession_EmptyTitle_ReturnsBadRequest()
    {
        var result = await _sut.RenameSession(ProjectId, SessionId, new RenameSessionRequest("   "));

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task RenameSession_NotFound_Returns404()
    {
        _chatService.Setup(s => s.RenameSessionAsync(ProjectId, SessionId, "Renamed")).ReturnsAsync(false);

        var result = await _sut.RenameSession(ProjectId, SessionId, new RenameSessionRequest("Renamed"));

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task UpdateSessionDynamicIteration_InvalidPolicyJson_ReturnsBadRequest()
    {
        var result = await _sut.UpdateSessionDynamicIteration(
            ProjectId,
            SessionId,
            new UpdateSessionDynamicIterationRequest(true, "feature/foo", "not-json"));

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task UpdateSessionDynamicIteration_Found_ReturnsNoContent()
    {
        _chatService
            .Setup(s => s.UpdateSessionDynamicIterationAsync(ProjectId, SessionId, true, "feature/foo", "{\"maxLoops\":4}"))
            .ReturnsAsync(true);

        var result = await _sut.UpdateSessionDynamicIteration(
            ProjectId,
            SessionId,
            new UpdateSessionDynamicIterationRequest(true, "feature/foo", "{\"maxLoops\":4}"));

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    // ── SendMessage ──────────────────────────────────────

    [TestMethod]
    public async Task SendMessage_ReturnsOk()
    {
        var msg = new ChatMessageDto("m1", "assistant", "Hi", "2025-01-01");
        var response = new SendMessageResponseDto(SessionId, msg, [], null);
        _chatService.Setup(s => s.SendMessageAsync(ProjectId, SessionId, "hello", It.Is<ChatSendOptions?>(o => o != null && !o.GenerateWorkItems && o.DynamicIteration == null))).ReturnsAsync(response);

        var result = await _sut.SendMessage(ProjectId, SessionId, new SendMessageRequest("hello"));

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(response, ok.Value);
    }

    [TestMethod]
    public async Task SendMessage_GenerateModeStarted_ReturnsAccepted()
    {
        var response = new SendMessageResponseDto(SessionId, null, [], null, IsDeferred: true);
        _chatService.Setup(s => s.SendMessageAsync(ProjectId, SessionId, "hello", It.Is<ChatSendOptions?>(o => o != null && o.GenerateWorkItems && o.DynamicIteration == null))).ReturnsAsync(response);

        var result = await _sut.SendMessage(ProjectId, SessionId, new SendMessageRequest("hello", true));

        var accepted = result as AcceptedResult;
        Assert.IsNotNull(accepted);
        Assert.AreSame(response, accepted.Value);
    }

    [TestMethod]
    public async Task SendMessage_WithDynamicIterationOptions_PassesOptionsToService()
    {
        var request = new SendMessageRequest(
            "hello",
            true,
            new DynamicIterationOptionsRequest(true, "parallel", "release/v1"));
        var response = new SendMessageResponseDto(SessionId, null, [], null, IsDeferred: true);
        _chatService
            .Setup(s => s.SendMessageAsync(
                ProjectId,
                SessionId,
                "hello",
                It.Is<ChatSendOptions?>(o =>
                    o != null
                    && o.GenerateWorkItems
                    && o.DynamicIteration != null
                    && o.DynamicIteration.Enabled == true
                    && o.DynamicIteration.ExecutionPolicy == "parallel"
                    && o.DynamicIteration.TargetBranch == "release/v1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.SendMessage(ProjectId, SessionId, request);

        Assert.IsInstanceOfType<AcceptedResult>(result);
    }

    [TestMethod]
    public async Task SendMessage_WithDynamicIterationButNoGenerateWorkItems_ReturnsBadRequest()
    {
        var request = new SendMessageRequest(
            "hello",
            false,
            new DynamicIterationOptionsRequest(true, "parallel", "release/v1"));

        var result = await _sut.SendMessage(ProjectId, SessionId, request);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        _chatService.Verify(
            service => service.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatSendOptions?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SendMessage_GenerateModeStarted_DoesNotPublishWorkItemEventsUntilCompletion()
    {
        var response = new SendMessageResponseDto(SessionId, null, [], null, IsDeferred: true);
        _chatService.Setup(s => s.SendMessageAsync(ProjectId, SessionId, "hello", It.Is<ChatSendOptions?>(o => o != null && o.GenerateWorkItems && o.DynamicIteration == null), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _sut.SendMessage(ProjectId, SessionId, new SendMessageRequest("hello", true));

        Assert.IsInstanceOfType<AcceptedResult>(result);
        _eventPublisher.Verify(
            publisher => publisher.PublishProjectEventAsync(
                It.IsAny<int>(),
                ProjectId,
                ServerEventTopics.WorkItemsUpdated,
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _eventPublisher.Verify(
            publisher => publisher.PublishUserEventAsync(
                It.IsAny<int>(),
                ServerEventTopics.ProjectsUpdated,
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── GetAttachments ───────────────────────────────────

    [TestMethod]
    public async Task GetAttachments_ReturnsOk()
    {
        var attachments = new List<ChatAttachmentDto>();
        _chatService.Setup(s => s.GetAttachmentsAsync(ProjectId, SessionId)).ReturnsAsync(attachments);

        var result = await _sut.GetAttachments(ProjectId, SessionId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // ── UploadAttachment ─────────────────────────────────

    [TestMethod]
    public async Task UploadAttachment_NullFile_ReturnsBadRequest()
    {
        var result = await _sut.UploadAttachment(ProjectId, SessionId, null!);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task UploadAttachment_AllowsNonMarkdownFileTypes()
    {
        var file = CreateFormFile("test.txt", "content");
        var attachment = CreateAttachment("att-1", "test.txt", "text/plain");
        _chatService.Setup(s => s.UploadAttachmentAsync(
                ProjectId,
                SessionId,
                "test.txt",
                "text/plain",
                It.Is<byte[]>(bytes => System.Text.Encoding.UTF8.GetString(bytes) == "content"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        var result = await _sut.UploadAttachment(ProjectId, SessionId, file);

        var created = result as CreatedResult;
        Assert.IsNotNull(created);
        Assert.AreSame(attachment, created.Value);
    }

    [TestMethod]
    public async Task UploadAttachment_FileTooLarge_ReturnsBadRequest()
    {
        var bigContent = new string('x', 10 * 1024 * 1024 + 1);
        var file = CreateFormFile("test.md", bigContent);

        var result = await _sut.UploadAttachment(ProjectId, SessionId, file);

        var bad = result as BadRequestObjectResult;
        Assert.IsNotNull(bad);
        Assert.IsTrue(bad.Value!.ToString()!.Contains("10 MB"));
    }

    [TestMethod]
    public async Task UploadAttachment_ValidFile_ReturnsCreated()
    {
        var file = CreateFormFile("readme.md", "# Hello");
        var attachment = CreateAttachment("att-1", "readme.md", "text/markdown");
        _chatService.Setup(s => s.UploadAttachmentAsync(
                ProjectId,
                SessionId,
                "readme.md",
                "text/markdown",
                It.Is<byte[]>(bytes => System.Text.Encoding.UTF8.GetString(bytes) == "# Hello"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        var result = await _sut.UploadAttachment(ProjectId, SessionId, file);

        var created = result as CreatedResult;
        Assert.IsNotNull(created);
        Assert.AreSame(attachment, created.Value);
    }

    // ── DeleteAttachment ─────────────────────────────────

    [TestMethod]
    public async Task DeleteAttachment_Found_ReturnsNoContent()
    {
        _chatService.Setup(s => s.DeleteAttachmentAsync(ProjectId, SessionId, "att-1")).ReturnsAsync(true);

        var result = await _sut.DeleteAttachment(ProjectId, SessionId, "att-1");

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task DeleteAttachment_NotFound_Returns404()
    {
        _chatService.Setup(s => s.DeleteAttachmentAsync(ProjectId, SessionId, "att-1")).ReturnsAsync(false);

        var result = await _sut.DeleteAttachment(ProjectId, SessionId, "att-1");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    // ── Helpers ──────────────────────────────────────────

    private static IFormFile CreateFormFile(string fileName, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.ContentType).Returns(GetContentType(fileName));
        file.Setup(f => f.Length).Returns(bytes.Length);
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>(async (target, _) =>
            {
                await target.WriteAsync(bytes);
                target.Position = 0;
            });
        return file.Object;
    }

    private static string GetContentType(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };

    private static ChatAttachmentDto CreateAttachment(string id, string fileName, string contentType)
        => new(
            id,
            fileName,
            7,
            "2025-01-01",
            contentType,
            $"/api/chat/attachments/{id}/content",
            $"[{fileName}](/api/chat/attachments/{id}/content)",
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
}
