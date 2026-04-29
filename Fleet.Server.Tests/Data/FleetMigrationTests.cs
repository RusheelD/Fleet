using Fleet.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Tests.Data;

[TestClass]
public class FleetMigrationTests
{
    [TestMethod]
    public void Migrations_IncludeChatSessionDynamicIterationRepair()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseNpgsql("Host=localhost;Database=fleet_test;Username=fleet;Password=fleet")
            .Options;
        using var context = new FleetDbContext(options);

        var migrations = context.Database.GetMigrations().ToArray();

        CollectionAssert.Contains(
            migrations,
            "20260429235900_RepairChatSessionDynamicIterationColumns");
    }
}
