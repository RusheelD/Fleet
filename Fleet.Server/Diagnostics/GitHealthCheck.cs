using System.Diagnostics;
using Fleet.Server.Agents;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fleet.Server.Diagnostics;

public sealed class GitHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public GitHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var processPath = GitExecutableResolver.BuildProcessPath(Environment.GetEnvironmentVariable("PATH"));
        var gitExecutable = GitExecutableResolver.Resolve(_configuration, processPath);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = gitExecutable,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            psi.Environment["PATH"] = processPath;

            using var process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                return HealthCheckResult.Unhealthy("Failed to start git process.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = (await stdoutTask).Trim();
            var stderr = (await stderrTask).Trim();

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return HealthCheckResult.Unhealthy(
                    $"Git unavailable. ExitCode={process.ExitCode}. {error}",
                    data: new Dictionary<string, object>
                    {
                        ["gitPath"] = gitExecutable,
                        ["path"] = processPath,
                    });
            }

            return HealthCheckResult.Healthy(
                stdout,
                new Dictionary<string, object>
                {
                    ["gitPath"] = gitExecutable,
                    ["path"] = processPath,
                    ["version"] = stdout,
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Git probe failed.",
                ex,
                new Dictionary<string, object>
                {
                    ["gitPath"] = gitExecutable,
                    ["path"] = processPath,
                });
        }
    }
}
