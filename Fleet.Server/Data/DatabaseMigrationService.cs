using Microsoft.EntityFrameworkCore;
using Fleet.Server.Logging;

namespace Fleet.Server.Data;

/// <summary>
/// Applies pending EF Core migrations in the background after the host starts.
/// This prevents migration from blocking Kestrel startup, which would cause
/// Aspire health checks to time out and mark the server as unhealthy.
/// </summary>
public class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            await db.Database.MigrateAsync(stoppingToken);
            logger.DataMigrationCompleted();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.DataMigrationFailed(ex);
            throw; // Let the host know startup is broken
        }
    }
}
