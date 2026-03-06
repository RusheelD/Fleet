using Fleet.Server.Models;
using Fleet.Server.Subscriptions;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Linq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class SubscriptionServiceTests
{
    private Mock<ILogger<SubscriptionService>> _logger = null!;
    private FleetDbContext _dbContext = null!;
    private IUsageLedgerService _usageLedgerService = null!;
    private SubscriptionService _sut = null!;
    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _logger = new Mock<ILogger<SubscriptionService>>();

        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .Options;
        _dbContext = new FleetDbContext(options);
        _dbContext.UserProfiles.Add(new UserProfile
        {
            Id = UserId,
            EntraObjectId = "oid-42",
            Username = "user42",
            Email = "user42@fleet.dev",
            DisplayName = "User 42",
            Role = Fleet.Server.Auth.UserRoles.Free,
        });
        _dbContext.SaveChanges();

        _usageLedgerService = new UsageLedgerService(_dbContext);
        _sut = new SubscriptionService(_usageLedgerService, _dbContext, _logger.Object);
    }

    [TestMethod]
    public async Task GetSubscriptionDataAsync_ReturnsTierAndUsage()
    {
        var result = await _sut.GetSubscriptionDataAsync(UserId);

        Assert.AreEqual("Free Tier", result.CurrentPlan.Name);
        Assert.IsTrue(result.Usage.Length >= 2);
        Assert.IsTrue(result.Plans.Any(p => p.IsCurrent && p.Name == "Free"));
    }
}
