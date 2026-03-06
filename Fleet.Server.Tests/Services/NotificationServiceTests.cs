using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class NotificationServiceTests
{
    private FleetDbContext _dbContext = null!;
    private NotificationRepository _repository = null!;
    private NotificationService _sut = null!;
    private const int UserId = 77;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _dbContext = new FleetDbContext(options);
        _dbContext.UserProfiles.Add(new UserProfile
        {
            Id = UserId,
            EntraObjectId = "oid-77",
            Username = "user77",
            Email = "user77@fleet.dev",
            DisplayName = "User 77",
            Preferences = new UserPreferences
            {
                AgentCompletedNotification = true,
                PrOpenedNotification = true,
                AgentErrorsNotification = true,
                WorkItemUpdatesNotification = true,
            },
        });
        _dbContext.Projects.Add(new Project
        {
            Id = "proj-1",
            OwnerId = UserId.ToString(),
            Slug = "proj-1",
            Title = "Project 1",
            Description = "desc",
            Repo = "org/repo",
        });
        _dbContext.SaveChanges();

        _repository = new NotificationRepository(_dbContext);
        _sut = new NotificationService(_repository, _dbContext, Mock.Of<ILogger<NotificationService>>());
    }

    [TestMethod]
    public async Task PublishAsync_DisabledByPreference_DoesNotPersist()
    {
        var user = await _dbContext.UserProfiles.SingleAsync(u => u.Id == UserId);
        user.Preferences.AgentCompletedNotification = false;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.PublishAsync(
            userId: UserId,
            projectId: "proj-1",
            type: "execution_completed",
            title: "done",
            message: "done");

        Assert.IsNull(result);
        Assert.AreEqual(0, await _dbContext.NotificationEvents.CountAsync());
    }

    [TestMethod]
    public async Task PublishAsync_EnabledByPreference_Persists()
    {
        var result = await _sut.PublishAsync(
            userId: UserId,
            projectId: "proj-1",
            type: "pr_ready",
            title: "ready",
            message: "pr");

        Assert.IsNotNull(result);
        Assert.AreEqual(1, await _dbContext.NotificationEvents.CountAsync());
    }
}
