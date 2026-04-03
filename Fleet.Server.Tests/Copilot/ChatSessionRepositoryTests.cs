using Fleet.Server.Auth;
using Fleet.Server.Copilot;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Fleet.Server.Tests.Copilot;

[TestClass]
public class ChatSessionRepositoryTests
{
    [TestMethod]
    public async Task DeleteSessionAsync_WhenScopedLookupMisses_DeletesOwnedSessionByIdFallback()
    {
        await using var context = CreateContext();
        context.ChatSessions.Add(new ChatSession
        {
            Id = "session-1",
            OwnerId = "42",
            Title = "Global session",
            LastMessage = "",
            Timestamp = DateTime.UtcNow.ToString("o"),
            IsActive = true,
            ProjectId = null,
        });
        await context.SaveChangesAsync();

        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(42);
        var sut = new ChatSessionRepository(context, auth.Object);

        var deleted = await sut.DeleteSessionAsync("proj-1", "session-1");

        Assert.IsTrue(deleted);
        Assert.IsFalse(await context.ChatSessions.AnyAsync(s => s.Id == "session-1"));
    }

    [TestMethod]
    public async Task DeleteSessionAsync_DoesNotDeleteSessionOwnedByAnotherUser()
    {
        await using var context = CreateContext();
        context.ChatSessions.Add(new ChatSession
        {
            Id = "session-2",
            OwnerId = "99",
            Title = "Other user session",
            LastMessage = "",
            Timestamp = DateTime.UtcNow.ToString("o"),
            IsActive = true,
            ProjectId = null,
        });
        await context.SaveChangesAsync();

        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(42);
        var sut = new ChatSessionRepository(context, auth.Object);

        var deleted = await sut.DeleteSessionAsync("proj-1", "session-2");

        Assert.IsFalse(deleted);
        Assert.IsTrue(await context.ChatSessions.AnyAsync(s => s.Id == "session-2"));
    }

    [TestMethod]
    public async Task BackgroundOwnerOverride_AllowsMessageAndAttachmentAccessWithoutHttpContext()
    {
        await using var context = CreateContext();
        context.ChatSessions.Add(new ChatSession
        {
            Id = "session-3",
            OwnerId = "42",
            Title = "Background session",
            LastMessage = "",
            Timestamp = DateTime.UtcNow.ToString("o"),
            IsActive = true,
            ProjectId = "proj-1",
            RecentActivityJson = "[]",
        });
        context.ChatAttachments.Add(new ChatAttachment
        {
            Id = "att-1",
            FileName = "spec.md",
            Content = "# Spec",
            UploadedAt = DateTime.UtcNow.ToString("o"),
            ChatSessionId = "session-3",
        });
        await context.SaveChangesAsync();

        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        auth.Setup(a => a.GetCurrentUserIdAsync())
            .ThrowsAsync(new UnauthorizedAccessException("No authenticated user."));
        var sut = new ChatSessionRepository(context, auth.Object);

        var attachments = await sut.GetAllAttachmentsBySessionIdAsync("proj-1", "session-3", "42");
        var attachmentContent = await sut.GetAttachmentContentAsync("proj-1", "att-1", "42");
        var assistantMessage = await sut.AddMessageAsync("proj-1", "session-3", "assistant", "Done", "42");

        Assert.AreEqual(1, attachments.Count);
        Assert.AreEqual("# Spec", attachmentContent);
        Assert.AreEqual("Done", assistantMessage.Content);
        auth.Verify(a => a.GetCurrentUserIdAsync(), Times.Never);
    }

    [TestMethod]
    public async Task BackgroundOwnerOverride_AllowsGenerationStateUpdatesWithoutHttpContext()
    {
        await using var context = CreateContext();
        context.ChatSessions.Add(new ChatSession
        {
            Id = "session-4",
            OwnerId = "42",
            Title = "Background session",
            LastMessage = "",
            Timestamp = DateTime.UtcNow.ToString("o"),
            IsActive = true,
            ProjectId = "proj-1",
            RecentActivityJson = "[]",
        });
        await context.SaveChangesAsync();

        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        auth.Setup(a => a.GetCurrentUserIdAsync())
            .ThrowsAsync(new UnauthorizedAccessException("No authenticated user."));
        var sut = new ChatSessionRepository(context, auth.Object);

        await sut.UpdateSessionGenerationStateAsync(
            "proj-1",
            "session-4",
            true,
            ChatGenerationStates.Running,
            "Loading chat context...",
            ownerId: "42");
        await sut.AppendSessionActivityAsync(
            "proj-1",
            "session-4",
            new ChatSessionActivityDto("activity-1", "status", "Still working", DateTime.UtcNow.ToString("O")),
            "42");
        var renamed = await sut.RenameSessionAsync("proj-1", "session-4", "Renamed in background", "42");

        var session = await context.ChatSessions.SingleAsync(s => s.Id == "session-4");
        Assert.IsTrue(renamed);
        Assert.IsTrue(session.IsGenerating);
        Assert.AreEqual(ChatGenerationStates.Running, session.GenerationState);
        Assert.AreEqual("Loading chat context...", session.GenerationStatus);
        Assert.AreEqual("Renamed in background", session.Title);
        StringAssert.Contains(session.RecentActivityJson, "Still working");
        auth.Verify(a => a.GetCurrentUserIdAsync(), Times.Never);
    }

    private static FleetDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase($"chat-session-tests-{Guid.NewGuid():N}")
            .Options;

        return new FleetDbContext(options);
    }
}
