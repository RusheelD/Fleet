using Fleet.Server.Auth;
using Fleet.Server.Copilot;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
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

    private static FleetDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase($"chat-session-tests-{Guid.NewGuid():N}")
            .Options;

        return new FleetDbContext(options);
    }
}
