using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class SearchServiceTests
{
    private FleetDbContext _db = null!;
    private SearchService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        _db = new FleetDbContext(options);
        _sut = new SearchService(_db, Mock.Of<ILogger<SearchService>>());
    }

    [TestMethod]
    public async Task SearchAsync_Chats_IncludesGlobalAndProjectScopedSessions()
    {
        _db.Projects.Add(new Project
        {
            Id = "proj-1",
            OwnerId = "42",
            Title = "Project One",
            Slug = "project-one",
            Description = "desc",
            Repo = "owner/repo",
            LastActivity = "now",
        });

        _db.ChatSessions.AddRange(
            new ChatSession
            {
                Id = "chat-global",
                OwnerId = "42",
                Title = "Global conversation",
                LastMessage = "Global update",
                Timestamp = "today",
                IsActive = true,
                ProjectId = null,
            },
            new ChatSession
            {
                Id = "chat-project",
                OwnerId = "42",
                Title = "Project conversation",
                LastMessage = "Project update",
                Timestamp = "today",
                IsActive = true,
                ProjectId = "proj-1",
            });

        await _db.SaveChangesAsync();

        var results = await _sut.SearchAsync("42", null, "chats");

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.Any(r => r.Title == "Global conversation" && r.Meta.Contains("Global chat", StringComparison.Ordinal)));
        Assert.IsTrue(results.Any(r => r.Title == "Project conversation" && r.Meta.Contains("Project One", StringComparison.Ordinal)));
    }
}
