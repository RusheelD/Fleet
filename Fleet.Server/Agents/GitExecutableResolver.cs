using Microsoft.Extensions.Configuration;

namespace Fleet.Server.Agents;

public static class GitExecutableResolver
{
    private static readonly string[] CommonUnixGitLocations =
    [
        "/usr/bin/git",
        "/usr/local/bin/git",
        "/bin/git",
    ];

    private static readonly string[] CommonWindowsGitLocations =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "git.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "git.exe"),
    ];

    public static string Resolve(IConfiguration configuration, string? pathEnvironment = null)
    {
        return Resolve(
            configuration["GIT_EXECUTABLE_PATH"] ??
            Environment.GetEnvironmentVariable("GIT_EXECUTABLE_PATH"),
            pathEnvironment);
    }

    internal static string Resolve(string? configuredPath = null, string? pathEnvironment = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        foreach (var candidate in GetGitExecutableCandidates(pathEnvironment))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return OperatingSystem.IsWindows() ? "git.exe" : "git";
    }

    internal static string BuildProcessPath(string? existingPath = null)
    {
        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(existingPath))
        {
            segments.AddRange(existingPath.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        foreach (var extra in GetKnownGitDirectories())
        {
            if (segments.Any(path => string.Equals(path, extra, StringComparison.OrdinalIgnoreCase)))
                continue;

            segments.Add(extra);
        }

        return string.Join(separator, segments);
    }

    internal static bool IsGitAvailableOnPath(string? pathEnvironment)
    {
        return GetGitExecutableCandidates(pathEnvironment).Any(File.Exists);
    }

    private static IEnumerable<string> GetKnownGitDirectories()
    {
        foreach (var candidate in OperatingSystem.IsWindows() ? CommonWindowsGitLocations : CommonUnixGitLocations)
        {
            var directory = Path.GetDirectoryName(candidate);
            if (!string.IsNullOrWhiteSpace(directory))
                yield return directory;
        }
    }

    private static IEnumerable<string> GetGitExecutableCandidates(string? pathEnvironment)
    {
        foreach (var candidate in OperatingSystem.IsWindows() ? CommonWindowsGitLocations : CommonUnixGitLocations)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                yield return candidate;
        }

        if (string.IsNullOrWhiteSpace(pathEnvironment))
            yield break;

        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var fileName = OperatingSystem.IsWindows() ? "git.exe" : "git";
        foreach (var segment in pathEnvironment.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(segment, fileName);
        }
    }
}
