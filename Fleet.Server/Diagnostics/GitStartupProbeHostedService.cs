using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fleet.Server.Diagnostics;

public sealed class GitStartupProbeHostedService : IHostedService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<GitStartupProbeHostedService> _logger;

    public GitStartupProbeHostedService(
        HealthCheckService healthCheckService,
        ILogger<GitStartupProbeHostedService> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(
            registration => string.Equals(registration.Name, "git", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        if (!report.Entries.TryGetValue("git", out var gitEntry))
        {
            _logger.LogWarning("Git startup probe did not return a git health-check entry.");
            return;
        }

        var gitPath = gitEntry.Data.TryGetValue("gitPath", out var gitPathValue) ? gitPathValue : null;
        var version = gitEntry.Data.TryGetValue("version", out var versionValue) ? versionValue : null;
        var path = gitEntry.Data.TryGetValue("path", out var pathValue) ? pathValue : null;

        if (gitEntry.Status == HealthStatus.Healthy)
        {
            _logger.LogInformation(
                "Git probe on startup succeeded. Path={GitPath}, Version={GitVersion}, PATH={Path}",
                gitPath,
                version,
                path);
            return;
        }

        _logger.LogError(
            gitEntry.Exception,
            "Git probe on startup failed. Path={GitPath}, Description={Description}, PATH={Path}",
            gitPath,
            gitEntry.Description,
            path);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
