using Fleet.Server.Auth;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class UsageLedgerServiceTests
{
    private FleetDbContext _db = null!;
    private UsageLedgerService _sut = null!;
    private const int FreeUserId = 7;
    private const int UnlimitedUserId = 8;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _db = new FleetDbContext(options);
        _db.UserProfiles.AddRange(
            new UserProfile
            {
                Id = FreeUserId,
                EntraObjectId = "oid-free",
                Username = "free",
                Email = "free@fleet.dev",
                DisplayName = "Free User",
                Role = UserRoles.Free,
            },
            new UserProfile
            {
                Id = UnlimitedUserId,
                EntraObjectId = "oid-unlimited",
                Username = "unlimited",
                Email = "unlimited@fleet.dev",
                DisplayName = "Unlimited User",
                Role = UserRoles.Unlimited,
            });
        _db.SaveChanges();

        _sut = new UsageLedgerService(_db);
    }

    [TestMethod]
    public async Task ChargeRunAsync_FreeWorkItemLimit_ThrowsAfterFour()
    {
        for (var i = 0; i < 4; i++)
            await _sut.ChargeRunAsync(FreeUserId, MonthlyRunType.WorkItem);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            _sut.ChargeRunAsync(FreeUserId, MonthlyRunType.WorkItem));
    }

    [TestMethod]
    public async Task RefundRunAsync_ReducesUsedCount()
    {
        await _sut.ChargeRunAsync(FreeUserId, MonthlyRunType.Coding);
        await _sut.RefundRunAsync(FreeUserId, MonthlyRunType.Coding);

        var snapshot = await _sut.GetCurrentMonthUsageAsync(FreeUserId);
        Assert.AreEqual(0, snapshot.CodingRunsUsed);
    }

    [TestMethod]
    public async Task ChargeRunAsync_UnlimitedTier_DoesNotCap()
    {
        for (var i = 0; i < 20; i++)
            await _sut.ChargeRunAsync(UnlimitedUserId, MonthlyRunType.WorkItem);

        var snapshot = await _sut.GetCurrentMonthUsageAsync(UnlimitedUserId);
        Assert.AreEqual(20, snapshot.WorkItemRunsUsed);
        Assert.IsNull(snapshot.WorkItemRunsRemaining);
    }
}
