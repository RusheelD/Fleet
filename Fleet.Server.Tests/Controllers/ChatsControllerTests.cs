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

    // ── SendMessage ──────────────────────────────────────

    [TestMethod]
    public async Task SendMessage_ReturnsOk()
    {
        var msg = new ChatMessageDto("m1", "assistant", "Hi", "2025-01-01");
        var response = new SendMessageResponseDto(SessionId, msg, [], null);
        _chatService.Setup(s => s.SendMessageAsync(ProjectId, SessionId, "hello", false)).ReturnsAsync(response);

        var result = await _sut.SendMessage(ProjectId, SessionId, new SendMessageRequest("hello"));

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(response, ok.Value);
    }

    [TestMethod]
    public async Task SendMessage_GenerateModeStarted_ReturnsAccepted()
    {
        var response = new SendMessageResponseDto(SessionId, null, [], null, IsDeferred: true);
        _chatService.Setup(s => s.SendMessageAsync(ProjectId, SessionId, "hello", true)).ReturnsAsync(response);

        var result = await _sut.SendMessage(ProjectId, SessionId, new SendMessageRequest("hello", true));

        var accepted = result as AcceptedResult;
        Assert.IsNotNull(accepted);
        Assert.AreSame(response, accepted.Value);
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
    public async Task UploadAttachment_NonMarkdownFile_ReturnsBadRequest()
    {
        var file = CreateFormFile("test.txt", "content");

        var result = await _sut.UploadAttachment(ProjectId, SessionId, file);

        var bad = result as BadRequestObjectResult;
        Assert.IsNotNull(bad);
        Assert.IsTrue(bad.Value!.ToString()!.Contains(".md"));
    }

    [TestMethod]
    public async Task UploadAttachment_FileTooLarge_ReturnsBadRequest()
    {
        // Create a file over 500 KB
        var bigContent = new string('x', 512_001);
        var file = CreateFormFile("test.md", bigContent);

        var result = await _sut.UploadAttachment(ProjectId, SessionId, file);

        var bad = result as BadRequestObjectResult;
        Assert.IsNotNull(bad);
        Assert.IsTrue(bad.Value!.ToString()!.Contains("500 KB"));
    }

    [TestMethod]
    public async Task UploadAttachment_ValidFile_ReturnsCreated()
    {
        var file = CreateFormFile("readme.md", "# Hello");
        var attachment = new ChatAttachmentDto("att-1", "readme.md", 7, "2025-01-01");
        _chatService.Setup(s => s.UploadAttachmentAsync(ProjectId, SessionId, "readme.md", "# Hello"))
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
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(stream.Length);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        return file.Object;
    }
}
