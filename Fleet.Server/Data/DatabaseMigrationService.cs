using Microsoft.EntityFrameworkCore;
using Fleet.Server.Logging;

namespace Fleet.Server.Data;

/// <summary>
/// Applies pending EF Core migrations in the background after the host starts.
/// This prevents migration from blocking Kestrel startup, which would cause
/// Aspire health checks to time out and mark the server as unhealthy.
/// </summary>
public class DatabaseMigrationService(
    FleetDatabaseMigrator databaseMigrator,
    ILogger<DatabaseMigrationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Startup migration is critical infrastructure work; run it on the
            // dedicated migration context rather than the request/runtime one.
            await databaseMigrator.MigrateAsync();
            logger.DataMigrationCompleted();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.DataMigrationFailed(ex);
            throw; // Let the host know startup is broken
        }
    }
}
