using Microsoft.Extensions.Configuration;

namespace Fleet.Server.Agents;

public sealed class RepoSandboxOptions
{
    public const string SectionName = "RepoSandbox";

    public string RootPath { get; set; } = GetDefaultRootPath();

    public static string GetDefaultRootPath()
    {
        return OperatingSystem.IsWindows()
            ? Path.Combine(Path.GetTempPath(), "fleet-sandboxes")
            : "/tmp/fleet-sandboxes";
    }

    public static string ResolveRootPath(IConfiguration configuration)
    {
        var configuredRoot =
            configuration[$"{SectionName}:RootPath"] ??
            configuration["REPO_SANDBOX_ROOT"] ??
            Environment.GetEnvironmentVariable("REPO_SANDBOX_ROOT");

        return string.IsNullOrWhiteSpace(configuredRoot)
            ? GetDefaultRootPath()
            : configuredRoot;
    }
}
