using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

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
        var currentDirectory = Directory.GetCurrentDirectory();
        var configBasePath = File.Exists(Path.Combine(currentDirectory, "appsettings.json"))
            ? currentDirectory
            : Path.Combine(currentDirectory, "Fleet.Server");

        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(configBasePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddUserSecrets<FleetDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = DbConnectionStringResolver.ResolveFleetDbConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "FleetDbContext design-time connection string is missing. " +
                "Set one of: ConnectionStrings:fleetdb, ConnectionStrings:Default, ConnectionString, or DATABASE_URL.");
        }

        var builder = new DbContextOptionsBuilder<FleetDbContext>();
        builder.UseNpgsql(
            connectionString,
            o => o.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null));
        return new FleetDbContext(builder.Options);
    }
}
