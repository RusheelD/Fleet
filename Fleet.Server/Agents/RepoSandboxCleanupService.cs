using Microsoft.Extensions.Options;

namespace Fleet.Server.Agents;

public sealed class RepoSandboxCleanupService(
    IOptions<RepoSandboxCleanupOptions> options,
    ILogger<RepoSandboxCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var interval = settings.Interval <= TimeSpan.Zero ? TimeSpan.FromHours(1) : settings.Interval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = RepoSandbox.CleanupStaleSandboxes(settings.RootPath, settings.StaleAfter, logger);
                if (deleted > 0)
                {
                    logger.LogInformation(
                        "Deleted {DeletedCount} stale repo sandbox director{Plural} from {RootPath}",
                        deleted,
                        deleted == 1 ? "y" : "ies",
                        settings.RootPath);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Repo sandbox cleanup pass failed for root {RootPath}", settings.RootPath);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
