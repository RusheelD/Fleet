using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Fleet.Server.Data;

/// <summary>
/// Builds a dedicated DbContext for schema migrations so runtime startup can use
/// a migration-specific connection string and safer migration services.
/// </summary>
public sealed class FleetDatabaseMigrator(IConfiguration configuration)
{
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync()
    {
        await using var dbContext = CreateMigrationDbContext();
        return (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
    }

    public async Task MigrateAsync()
    {
        await using var dbContext = CreateMigrationDbContext();
        await dbContext.Database.MigrateAsync();
    }

    private FleetDbContext CreateMigrationDbContext()
    {
        var connectionString = DbConnectionStringResolver.ResolveFleetDbMigrationConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Fleet database migration connection string is missing. " +
                "Set one of: ConnectionStrings:fleetdb_migrations, ConnectionStrings:fleetdb, ConnectionStrings:Default, ConnectionString, or DATABASE_URL.");
        }

        var builder = new DbContextOptionsBuilder<FleetDbContext>();
        builder.UseNpgsql(
            connectionString,
            options => options.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null));
        builder.ReplaceService<IHistoryRepository, NoLockNpgsqlHistoryRepository>();
        builder.ReplaceService<IMigrationCommandExecutor, NoTransactionMigrationCommandExecutor>();

        return new FleetDbContext(builder.Options);
    }
}
