using Fleet.Server.Data;
using Microsoft.Extensions.Configuration;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class DbConnectionStringResolverTests
{
    [TestMethod]
    public void ResolveFleetDbMigrationConnectionString_PrefersMigrationConnection()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:fleetdb_migrations"] = "Host=migrations-db;Port=5432;Database=fleet;",
            ["ConnectionStrings:fleetdb"] = "Host=app-db;Port=5432;Database=fleet;",
        });

        var result = DbConnectionStringResolver.ResolveFleetDbMigrationConnectionString(configuration);

        Assert.IsNotNull(result);
        StringAssert.Contains(result, "Host=migrations-db");
        Assert.IsFalse(result.Contains("Host=app-db", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ResolveFleetDbMigrationConnectionString_ForcesSupabaseSessionPooling()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:fleetdb"] = "Host=db.abc.pooler.supabase.com;Port=6543;Database=fleet;Username=user;Password=pw",
        });

        var result = DbConnectionStringResolver.ResolveFleetDbMigrationConnectionString(configuration);

        Assert.IsNotNull(result);
        StringAssert.Contains(result, "Host=db.abc.pooler.supabase.com");
        StringAssert.Contains(result, "Port=5432");
        StringAssert.Contains(result, "Maximum Pool Size=10");
    }

    [TestMethod]
    public void ResolveFleetDbConnectionString_ClampsSupabaseSessionPoolSize()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:fleetdb"] = "Host=db.abc.pooler.supabase.com;Port=5432;Database=fleet;Username=user;Password=pw;Maximum Pool Size=50",
        });

        var result = DbConnectionStringResolver.ResolveFleetDbConnectionString(configuration);

        Assert.IsNotNull(result);
        StringAssert.Contains(result, "Host=db.abc.pooler.supabase.com");
        StringAssert.Contains(result, "Maximum Pool Size=10");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
