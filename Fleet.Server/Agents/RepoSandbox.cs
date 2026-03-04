using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Fleet.Server.Agents;

/// <summary>
/// Manages a temporary local git clone of a GitHub repository.
/// Provides sandboxed file access and command execution for agent tasks.
/// </summary>
public class RepoSandbox : IRepoSandbox
{
    private readonly ILogger<RepoSandbox> _logger;
    private string _repoRoot = string.Empty;
    private string _repoFullName = string.Empty;
    private string _branchName = string.Empty;
    private string _accessToken = string.Empty;
    private bool _disposed;

    /// <summary>Max output length from a command (256 KB).</summary>
    private const int MaxOutputLength = 256 * 1024;

    /// <summary>Max file size to read (1 MB).</summary>
    private const int MaxFileReadSize = 1_048_576;

    /// <summary>Binary file extensions to skip during search.</summary>
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bin", ".obj", ".o", ".so", ".dylib",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg",
        ".zip", ".tar", ".gz", ".rar", ".7z",
        ".pdf", ".woff", ".woff2", ".ttf", ".eot",
        ".lock", ".min.js", ".min.css",
    };

    public RepoSandbox(ILogger<RepoSandbox> logger)
    {
        _logger = logger;
    }

    public string RepoRoot => _repoRoot;
    public string RepoFullName => _repoFullName;
    public string BranchName => _branchName;

    public async Task CloneAsync(string repoFullName, string accessToken, string branchName, CancellationToken cancellationToken)
    {
        _repoFullName = repoFullName;
        _branchName = branchName;
        _accessToken = accessToken;

        // Create a unique temp directory
        _repoRoot = Path.Combine(Path.GetTempPath(), "fleet-agent", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repoRoot);

        _logger.LogInformation("Cloning {Repo} into {Path}", repoFullName, _repoRoot);

        // Clone with token-based auth
        var cloneUrl = $"https://x-access-token:{accessToken}@github.com/{repoFullName}.git";
        var result = await RunGitAsync($"clone --depth 50 \"{cloneUrl}\" \"{_repoRoot}\"", workingDir: Path.GetTempPath(), cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git clone failed: {result.Stderr}");

        // Create and checkout the feature branch
        result = await RunGitAsync($"checkout -b {branchName}", cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git checkout failed: {result.Stderr}");

        _logger.LogInformation("Cloned {Repo} and created branch {Branch}", repoFullName, branchName);
    }

    public IReadOnlyList<SandboxEntry> ListDirectory(string relativePath = "")
    {
        EnsureInitialized();
        var fullPath = ResolveSafePath(relativePath);

        if (!Directory.Exists(fullPath))
            return [];

        var entries = new List<SandboxEntry>();

        foreach (var dir in Directory.GetDirectories(fullPath))
        {
            var name = Path.GetFileName(dir);
            if (name == ".git") continue;
            entries.Add(new SandboxEntry(name, "dir", 0));
        }

        foreach (var file in Directory.GetFiles(fullPath))
        {
            var info = new FileInfo(file);
            entries.Add(new SandboxEntry(info.Name, "file", info.Length));
        }

        return entries;
    }

    public string ReadFile(string relativePath)
    {
        EnsureInitialized();
        var fullPath = ResolveSafePath(relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}");

        var info = new FileInfo(fullPath);
        if (info.Length > MaxFileReadSize)
            throw new InvalidOperationException($"File too large ({info.Length:N0} bytes). Max is {MaxFileReadSize:N0} bytes.");

        return File.ReadAllText(fullPath);
    }

    public void WriteFile(string relativePath, string content)
    {
        EnsureInitialized();
        var fullPath = ResolveSafePath(relativePath);

        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(fullPath, content);
    }

    public bool DeleteFile(string relativePath)
    {
        EnsureInitialized();
        var fullPath = ResolveSafePath(relativePath);

        if (!File.Exists(fullPath))
            return false;

        File.Delete(fullPath);
        return true;
    }

    public IReadOnlyList<SearchResult> SearchFiles(string pattern, bool isRegex = false, string? fileGlob = null, int maxResults = 50)
    {
        EnsureInitialized();
        var results = new List<SearchResult>();
        var regex = isRegex
            ? new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(5))
            : null;

        // Collect files to search
        IEnumerable<string> files;
        if (!string.IsNullOrEmpty(fileGlob))
        {
            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddInclude(fileGlob);
            var matchResult = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(_repoRoot)));
            files = matchResult.Files.Select(f => Path.Combine(_repoRoot, f.Path));
        }
        else
        {
            files = Directory.EnumerateFiles(_repoRoot, "*", SearchOption.AllDirectories);
        }

        foreach (var file in files)
        {
            if (results.Count >= maxResults) break;

            var relativePath = Path.GetRelativePath(_repoRoot, file);
            if (relativePath.StartsWith(".git" + Path.DirectorySeparatorChar)) continue;
            if (BinaryExtensions.Contains(Path.GetExtension(file))) continue;

            // Skip large files
            var info = new FileInfo(file);
            if (info.Length > MaxFileReadSize) continue;

            try
            {
                var lines = File.ReadLines(file);
                var lineNum = 0;
                foreach (var line in lines)
                {
                    lineNum++;
                    if (results.Count >= maxResults) break;

                    var matches = regex is not null
                        ? regex.IsMatch(line)
                        : line.Contains(pattern, StringComparison.OrdinalIgnoreCase);

                    if (matches)
                    {
                        results.Add(new SearchResult(relativePath, lineNum, line.TrimEnd()));
                    }
                }
            }
            catch (Exception)
            {
                // Skip files that can't be read (binary, encoding issues, etc.)
            }
        }

        return results;
    }

    public async Task<CommandResult> RunCommandAsync(string command, string arguments, int timeoutSeconds = 120, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        _logger.LogInformation("Running command: {Command} {Arguments} (timeout: {Timeout}s)", command, arguments, timeoutSeconds);

        // Validate the command doesn't try to escape the sandbox
        ValidateCommand(command, arguments);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Restrict environment to prevent leaks
        psi.Environment["HOME"] = _repoRoot;
        psi.Environment["USERPROFILE"] = _repoRoot;

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var timedOut = false;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null && stdout.Length < MaxOutputLength)
                stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null && stderr.Length < MaxOutputLength)
                stderr.AppendLine(e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            try { process.Kill(entireProcessTree: true); }
            catch { /* best effort */ }
        }

        _logger.LogInformation("Command finished: exit={ExitCode} timedOut={TimedOut}", process.ExitCode, timedOut);

        return new CommandResult(
            timedOut ? -1 : process.ExitCode,
            stdout.ToString().TrimEnd(),
            stderr.ToString().TrimEnd(),
            timedOut
        );
    }

    public async Task CommitAndPushAsync(string commitMessage, string authorName, string authorEmail, CancellationToken cancellationToken)
    {
        EnsureInitialized();

        // Stage all changes
        var result = await RunGitAsync("add -A", cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git add failed: {result.Stderr}");

        // Check if there are changes to commit
        var status = await RunGitAsync("status --porcelain", cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(status.Stdout))
        {
            _logger.LogInformation("No changes to commit");
            return;
        }

        // Commit
        result = await RunGitAsync(
            $"commit -m \"{commitMessage.Replace("\"", "\\\"")}\" --author=\"{authorName} <{authorEmail}>\"",
            cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git commit failed: {result.Stderr}");

        // Push to remote
        result = await RunGitAsync($"push -u origin {_branchName}", cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git push failed: {result.Stderr}");

        _logger.LogInformation("Committed and pushed to {Branch}", _branchName);
    }

    public async Task<IReadOnlyList<FileChange>> GetChangeSummaryAsync(CancellationToken cancellationToken)
    {
        EnsureInitialized();

        // Get diff against the base branch
        var result = await RunGitAsync("diff --name-status HEAD~1..HEAD 2>/dev/null || git diff --name-status --cached", cancellationToken: cancellationToken);

        // Also check unstaged changes
        var unstaged = await RunGitAsync("status --porcelain", cancellationToken: cancellationToken);

        var changes = new List<FileChange>();

        foreach (var line in unstaged.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 4) continue;
            var statusCode = line[..2].Trim();
            var path = line[3..].Trim();
            var status = statusCode switch
            {
                "A" or "??" => "added",
                "M" => "modified",
                "D" => "deleted",
                "R" => "renamed",
                _ => "modified",
            };
            changes.Add(new FileChange(path, status));
        }

        return changes;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (!string.IsNullOrEmpty(_repoRoot) && Directory.Exists(_repoRoot))
        {
            _logger.LogInformation("Cleaning up sandbox: {Path}", _repoRoot);
            try
            {
                // Git makes files read-only; reset attributes before delete
                foreach (var file in Directory.EnumerateFiles(_repoRoot, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(_repoRoot, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up sandbox directory: {Path}", _repoRoot);
            }
        }

        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }

    // ── Private helpers ─────────────────────────────────────────

    private void EnsureInitialized()
    {
        if (string.IsNullOrEmpty(_repoRoot))
            throw new InvalidOperationException("Sandbox not initialized. Call CloneAsync first.");
    }

    /// <summary>
    /// Resolves a relative path against the repo root and validates it stays within bounds.
    /// </summary>
    private string ResolveSafePath(string relativePath)
    {
        // Normalize and block path traversal
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains(".."))
            throw new UnauthorizedAccessException($"Path traversal is not allowed: {relativePath}");

        var fullPath = Path.GetFullPath(Path.Combine(_repoRoot, normalized));

        if (!fullPath.StartsWith(_repoRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Path escapes the sandbox: {relativePath}");

        // Block access to .git directory
        var repoRelPath = Path.GetRelativePath(_repoRoot, fullPath);
        if (repoRelPath.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Access to .git directory is not allowed.");

        return fullPath;
    }

    /// <summary>
    /// Basic validation to prevent dangerous commands.
    /// </summary>
    private void ValidateCommand(string command, string arguments)
    {
        var combined = $"{command} {arguments}".ToLowerInvariant();

        // Block commands that could escape the sandbox
        string[] blocked = ["rm -rf /", "format ", "shutdown", "reboot", "mkfs", "dd if=", ":(){ :|:& };:"];
        foreach (var b in blocked)
        {
            if (combined.Contains(b))
                throw new UnauthorizedAccessException($"Blocked command: {command} {arguments}");
        }
    }

    private async Task<CommandResult> RunGitAsync(string arguments, string? workingDir = null, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir ?? _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new CommandResult(process.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd(), false);
    }
}
