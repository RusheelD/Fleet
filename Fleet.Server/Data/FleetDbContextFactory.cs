using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fleet.Server.Data;

/// <summary>
/// Design-time factory for <see cref="FleetDbContext"/>.
/// Used by <c>dotnet ef</c> CLI tools (migrations, etc.) when the Aspire
/// service-discovery connection string is unavailable.
/// </summary>
public class FleetDbContextFactory : IDesignTimeDbContextFactory<FleetDbContext>
{
    public FleetDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<FleetDbContext>();
        builder.UseNpgsql(
            "Host=localhost;Database=fleetdb;Username=postgres;Password=postgres",
            o => o.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null));
        return new FleetDbContext(builder.Options);
    }
}
