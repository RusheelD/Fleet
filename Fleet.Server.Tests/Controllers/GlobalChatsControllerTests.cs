using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Copilot;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class GlobalChatsControllerTests
{
    private Mock<IChatService> _chatService = null!;
    private Mock<IAuthService> _authService = null!;
    private Mock<IServerEventPublisher> _eventPublisher = null!;
    private GlobalChatsController _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _chatService = new Mock<IChatService>();
        _authService = new Mock<IAuthService>();
        _eventPublisher = new Mock<IServerEventPublisher>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(42);
        _sut = new GlobalChatsController(_chatService.Object, _authService.Object, _eventPublisher.Object);
    }

    [TestMethod]
    public async Task SendMessage_WithGenerateRequested_ReturnsBadRequest()
    {
        var result = await _sut.SendMessage("sess-1", new SendMessageRequest("hello", true));

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        _chatService.Verify(
            service => service.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatSendOptions?>()),
            Times.Never);
    }


    [TestMethod]
    public async Task SendMessage_WithDynamicIterationRequested_ReturnsBadRequest()
    {
        var request = new SendMessageRequest(
            "hello",
            false,
            new DynamicIterationOptionsRequest(true, "parallel", "main"));

        var result = await _sut.SendMessage("sess-1", request);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        _chatService.Verify(
            service => service.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatSendOptions?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SendMessage_WithNormalMessage_ReturnsOk()
    {
        var assistantMessage = new ChatMessageDto("m1", "assistant", "hi", "now");
        var response = new SendMessageResponseDto("sess-1", assistantMessage, [], null);
        _chatService
            .Setup(service => service.SendMessageAsync("", "sess-1", "hello", It.Is<ChatSendOptions?>(o => o != null && !o.GenerateWorkItems)))
            .ReturnsAsync(response);

        var result = await _sut.SendMessage("sess-1", new SendMessageRequest("hello", false));

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(response, ok.Value);
    }

    [TestMethod]
    public async Task RenameSession_Found_ReturnsNoContent()
    {
        _chatService
            .Setup(service => service.RenameSessionAsync("", "sess-1", "Renamed"))
            .ReturnsAsync(true);

        var result = await _sut.RenameSession("sess-1", new RenameSessionRequest("Renamed"));

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task RenameSession_EmptyTitle_ReturnsBadRequest()
    {
        var result = await _sut.RenameSession("sess-1", new RenameSessionRequest("   "));

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }
}
