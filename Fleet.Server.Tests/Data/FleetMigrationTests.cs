using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Tests.Data;

[TestClass]
public class FleetMigrationTests
{
    private static readonly string[] LegacySourceOnlyMigrationIds =
    [
        "20260420120000_AddChatSessionDynamicIteration",
        "20260429000000_AddChatSessionBranchStrategy",
    ];

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

    [TestMethod]
    public void Migrations_SourceFilesAreDiscoverableByEf()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseNpgsql("Host=localhost;Database=fleet_test;Username=fleet;Password=fleet")
            .Options;
        using var context = new FleetDbContext(options);

        var discoveredMigrations = context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var allowedLegacyMigrationIds = LegacySourceOnlyMigrationIds.ToHashSet(StringComparer.Ordinal);
        var sourceMigrationIds = Directory
            .GetFiles(GetMigrationsDirectory(), "*.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(fileName =>
                fileName is not null &&
                !fileName.EndsWith(".Designer", StringComparison.Ordinal) &&
                !string.Equals(fileName, "FleetDbContextModelSnapshot", StringComparison.Ordinal))
            .Cast<string>()
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();

        var missingDiscoveryMetadata = sourceMigrationIds
            .Where(migrationId =>
                !discoveredMigrations.Contains(migrationId) &&
                !allowedLegacyMigrationIds.Contains(migrationId))
            .ToArray();

        Assert.AreEqual(
            0,
            missingDiscoveryMetadata.Length,
            "Migration source files must be discoverable by EF. Add the matching .Designer.cs metadata file before merging. Missing: " +
            string.Join(", ", missingDiscoveryMetadata));

        CollectionAssert.IsSubsetOf(
            LegacySourceOnlyMigrationIds,
            sourceMigrationIds,
            "The legacy source-only chat migrations must stay visible in source because the repair migration exists specifically to cover them.");
        CollectionAssert.Contains(
            discoveredMigrations.ToArray(),
            "20260429235900_RepairChatSessionDynamicIterationColumns");
    }

    [TestMethod]
    public void ChatSessionModel_IncludesCriticalChatColumns()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseNpgsql("Host=localhost;Database=fleet_test;Username=fleet;Password=fleet")
            .Options;
        using var context = new FleetDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(ChatSession));
        Assert.IsNotNull(entityType);

        var criticalProperties = new[]
        {
            nameof(ChatSession.IsGenerating),
            nameof(ChatSession.GenerationState),
            nameof(ChatSession.GenerationStatus),
            nameof(ChatSession.GenerationUpdatedAtUtc),
            nameof(ChatSession.RecentActivityJson),
            nameof(ChatSession.BranchStrategy),
            nameof(ChatSession.SessionPinnedBranch),
            nameof(ChatSession.InheritParentBranchForSubFlows),
            nameof(ChatSession.IsDynamicIterationEnabled),
            nameof(ChatSession.DynamicIterationBranch),
            nameof(ChatSession.DynamicIterationPolicyJson),
        };

        foreach (var propertyName in criticalProperties)
        {
            Assert.IsNotNull(
                entityType.FindProperty(propertyName),
                $"ChatSessions is missing EF model property '{propertyName}'. Chat data queries depend on this column.");
        }
    }

    private static string GetMigrationsDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Fleet.Server", "Migrations");
            if (Directory.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        Assert.Fail("Could not locate Fleet.Server/Migrations from the test output directory.");
        return string.Empty;
    }
}
