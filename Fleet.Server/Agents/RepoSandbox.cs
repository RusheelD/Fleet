using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Options;

namespace Fleet.Server.Agents;

/// <summary>
/// Manages a temporary local git clone of a GitHub repository.
/// Provides sandboxed file access and command execution for agent tasks.
/// </summary>
public class RepoSandbox : IRepoSandbox
{
    private readonly ILogger<RepoSandbox> _logger;
    private readonly string _sandboxRoot;
    private readonly string _gitExecutable;
    private readonly string _gitProcessPath;
    private string _repoRoot = string.Empty;
    private string _repoFullName = string.Empty;
    private string _branchName = string.Empty;
    private bool _disposed;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

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

    public RepoSandbox(
        ILogger<RepoSandbox> logger,
        IOptions<RepoSandboxOptions> options,
        IConfiguration configuration)
    {
        _logger = logger;
        _sandboxRoot = EnsureSandboxWorkspaceRoot(options.Value.RootPath);
        _gitProcessPath = GitExecutableResolver.BuildProcessPath(Environment.GetEnvironmentVariable("PATH"));
        _gitExecutable = GitExecutableResolver.Resolve(configuration, _gitProcessPath);
    }

    public string RepoRoot => _repoRoot;
    public string RepoFullName => _repoFullName;
    public string BranchName => _branchName;

    public async Task CloneAsync(
        string repoFullName,
        string accessToken,
        string branchName,
        CancellationToken cancellationToken,
        string? baseBranch = null,
        bool resumeFromBranch = false)
    {
        _repoFullName = repoFullName;
        _branchName = branchName;

        // Create a unique repo directory underneath the configured sandbox root.
        _repoRoot = Path.Combine(_sandboxRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repoRoot);

        _logger.LogInformation("Cloning {Repo} into {Path}", repoFullName, _repoRoot);

        // Clone with standard HTTPS username/token auth. The linked account flow stores
        // GitHub OAuth access tokens, not GitHub App installation tokens, so the
        // `x-access-token` username pattern is not appropriate here.
        var cloneUrl = BuildAuthenticatedCloneUrl(repoFullName, accessToken);
        var result = await RunGitAsync($"clone --depth 50 \"{cloneUrl}\" \"{_repoRoot}\"", workingDir: _sandboxRoot, cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git clone failed: {result.Stderr}");

        // Resume an existing feature branch from origin if requested.
        if (resumeFromBranch)
        {
            var escapedBranch = EscapeGitArgument(branchName);
            result = await RunGitAsync($"ls-remote --heads origin \"{escapedBranch}\"", cancellationToken: cancellationToken);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Stdout))
            {
                throw new InvalidOperationException(
                    $"Git remote branch lookup failed for '{branchName}'. " +
                    "The branch was not found on origin, so retry cannot resume from it.");
            }

            result = await RunGitAsync(
                $"fetch --depth 1 origin \"+refs/heads/{escapedBranch}:refs/remotes/origin/{escapedBranch}\"",
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Git fetch of existing branch '{branchName}' failed: {result.Stderr}");

            result = await RunGitAsync(
                $"checkout -B \"{escapedBranch}\" \"refs/remotes/origin/{escapedBranch}\"",
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Git checkout of existing branch '{branchName}' failed: {result.Stderr}");
        }
        // Create and checkout a new feature branch from a caller-provided base branch.
        else if (!string.IsNullOrWhiteSpace(baseBranch))
        {
            var escapedBaseBranch = EscapeGitArgument(baseBranch);
            var escapedBranch = EscapeGitArgument(branchName);

            result = await RunGitAsync(
                $"ls-remote --heads origin \"{escapedBaseBranch}\"",
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Stdout))
                throw new InvalidOperationException(
                    $"Target/base branch '{baseBranch}' was not found on origin.");

            result = await RunGitAsync(
                $"fetch --depth 1 origin \"+refs/heads/{escapedBaseBranch}:refs/remotes/origin/{escapedBaseBranch}\"",
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Git fetch of base branch '{baseBranch}' failed: {result.Stderr}");

            result = await RunGitAsync(
                $"checkout -B \"{escapedBranch}\" \"refs/remotes/origin/{escapedBaseBranch}\"",
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Git checkout from base branch '{baseBranch}' failed: {result.Stderr}");
        }
        else
        {
            result = await RunGitAsync($"checkout -b \"{EscapeGitArgument(branchName)}\"", cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git checkout failed: {result.Stderr}");
        }

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
        _writeLock.Wait();
        try
        {
            var fullPath = ResolveSafePath(relativePath);

            var dir = Path.GetDirectoryName(fullPath);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, content);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public bool DeleteFile(string relativePath)
    {
        EnsureInitialized();
        _writeLock.Wait();
        try
        {
            var fullPath = ResolveSafePath(relativePath);

            if (!File.Exists(fullPath))
                return false;

            File.Delete(fullPath);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
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

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
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
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task CommitAndPushAsync(string accessToken, string commitMessage, string authorName, string authorEmail, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
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
                BuildCommitArguments(commitMessage, authorName, authorEmail),
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git commit failed: {result.Stderr}");

            // Push to remote
            await UpdateOriginRemoteAsync(accessToken, cancellationToken);
            result = await RunGitAsync(
                $"push -u origin \"{EscapeGitArgument(_branchName)}\"",
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git push failed: {result.Stderr}");

            _logger.LogInformation("Committed and pushed to {Branch}", _branchName);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task UpdateOriginRemoteAsync(string accessToken, CancellationToken cancellationToken)
    {
        var remoteUrl = BuildAuthenticatedCloneUrl(_repoFullName, accessToken);
        var result = await RunGitAsync(
            $"remote set-url origin \"{EscapeGitArgument(remoteUrl)}\"",
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git remote update failed: {result.Stderr}");
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

        // Block access to the .git directory only (allow files like .gitignore).
        var repoRelPath = Path.GetRelativePath(_repoRoot, fullPath);
        var normalizedRepoRelPath = repoRelPath.Replace('\\', '/');
        if (normalizedRepoRelPath.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            normalizedRepoRelPath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Access to .git directory is not allowed.");

        return fullPath;
    }

    /// <summary>
    /// Basic validation to prevent dangerous commands.
    /// </summary>
    private void ValidateCommand(string command, string arguments)
    {
        var commandName = Path.GetFileName(command).ToLowerInvariant();
        var combined = $"{commandName} {arguments}".ToLowerInvariant();

        // Block commands that could escape the sandbox
        string[] blocked =
        [
            "rm -rf /",
            "rm -rf ..",
            "format ",
            "shutdown",
            "reboot",
            "mkfs",
            "dd if=",
            ":(){ :|:& };:",
            "git push --force",
            "git push -f",
            "git push origin :",
            "git push --delete",
            "git branch -d",
            "git branch -D",
        ];
        foreach (var b in blocked)
        {
            if (combined.Contains(b))
                throw new UnauthorizedAccessException($"Blocked command: {command} {arguments}");
        }

        if (IsProtectedBranchPush(combined))
            throw new UnauthorizedAccessException($"Blocked push to protected branch: {command} {arguments}");
    }

    private static bool IsProtectedBranchPush(string combinedCommand)
    {
        if (!combinedCommand.Contains("git push", StringComparison.OrdinalIgnoreCase))
            return false;

        string[] protectedBranchMarkers =
        [
            " origin main",
            " origin master",
            " origin develop",
            " origin development",
            " origin release",
            " refs/heads/main",
            " refs/heads/master",
            " refs/heads/develop",
            " refs/heads/development",
            " refs/heads/release",
        ];

        return protectedBranchMarkers.Any(marker =>
            combinedCommand.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string EscapeGitArgument(string value) => value.Replace("\"", "\\\"");

    internal static string BuildCommitArguments(string commitMessage, string authorName, string authorEmail)
    {
        if (string.IsNullOrWhiteSpace(commitMessage))
            throw new ArgumentException("Commit message is required.", nameof(commitMessage));

        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("Author name is required.", nameof(authorName));

        if (string.IsNullOrWhiteSpace(authorEmail))
            throw new ArgumentException("Author email is required.", nameof(authorEmail));

        var escapedCommitMessage = EscapeGitArgument(commitMessage);
        var escapedAuthorName = EscapeGitArgument(authorName);
        var escapedAuthorEmail = EscapeGitArgument(authorEmail);

        return
            $"-c user.name=\"{escapedAuthorName}\" " +
            $"-c user.email=\"{escapedAuthorEmail}\" " +
            $"commit -m \"{escapedCommitMessage}\" " +
            $"--author=\"{escapedAuthorName} <{escapedAuthorEmail}>\"";
    }

    internal static string BuildAuthenticatedCloneUrl(
        string repoFullName,
        string accessToken,
        string username = "git")
    {
        if (string.IsNullOrWhiteSpace(repoFullName))
            throw new ArgumentException("Repository name is required.", nameof(repoFullName));

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token is required.", nameof(accessToken));

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));

        var escapedUsername = Uri.EscapeDataString(username);
        var escapedToken = Uri.EscapeDataString(accessToken);
        return $"https://{escapedUsername}:{escapedToken}@github.com/{repoFullName}.git";
    }

    internal static string GetAppOwnedSandboxRoot(string? appBaseOverride = null)
    {
        var appBase = string.IsNullOrWhiteSpace(appBaseOverride)
            ? AppContext.BaseDirectory
            : appBaseOverride;

        if (string.IsNullOrWhiteSpace(appBase))
            throw new InvalidOperationException("An app-owned base directory is required to create a repo sandbox fallback.");

        return Path.Combine(appBase, ".fleet-sandboxes");
    }

    internal static string EnsureSandboxWorkspaceRoot(string? configuredRoot, string? appBaseOverride = null)
    {
        var preferredRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? RepoSandboxOptions.GetDefaultRootPath()
            : configuredRoot;

        try
        {
            Directory.CreateDirectory(preferredRoot);
            return preferredRoot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var fallbackRoot = GetAppOwnedSandboxRoot(appBaseOverride);
            Directory.CreateDirectory(fallbackRoot);
            return fallbackRoot;
        }
    }

    internal static string EnsureGitWorkingDirectory(string? workingDir, string repoRoot, string? sandboxRoot = null)
    {
        var effectiveWorkingDir = string.IsNullOrWhiteSpace(workingDir)
            ? repoRoot
            : workingDir;

        if (string.IsNullOrWhiteSpace(effectiveWorkingDir))
            effectiveWorkingDir = EnsureSandboxWorkspaceRoot(sandboxRoot);

        Directory.CreateDirectory(effectiveWorkingDir);
        return effectiveWorkingDir;
    }

    private static InvalidOperationException CreateGitStartException(
        Win32Exception exception,
        string workingDirectory,
        string gitExecutable,
        string pathEnvironment)
    {
        if (!Directory.Exists(workingDirectory))
        {
            return new InvalidOperationException(
                $"Git working directory '{workingDirectory}' is missing before launch. The repo sandbox could not prepare a usable workspace.",
                exception);
        }

        var gitOnPath = GitExecutableResolver.IsGitAvailableOnPath(pathEnvironment);
        if (!gitOnPath && (gitExecutable.Equals("git", StringComparison.OrdinalIgnoreCase) || gitExecutable.Equals("git.exe", StringComparison.OrdinalIgnoreCase)))
        {
            return new InvalidOperationException(
                "Git executable could not be found. Install git on the host, add it to PATH, or set GIT_EXECUTABLE_PATH to the full git binary path.",
                exception);
        }

        return new InvalidOperationException(
            $"Failed to start git from '{gitExecutable}' in working directory '{workingDirectory}'. PATH='{pathEnvironment}'.",
            exception);
    }

    private async Task<CommandResult> RunGitAsync(string arguments, string? workingDir = null, CancellationToken cancellationToken = default)
    {
        // Prepend flags that disable all credential helpers and interactive prompts.
        // Without this, Git Credential Manager (GCM) on Windows opens a browser/dialog
        // asking the user to pick an account — which blocks headless server execution.
        var fullArgs = $"-c credential.helper= {arguments}";
        var effectiveWorkingDir = EnsureGitWorkingDirectory(workingDir, _repoRoot, _sandboxRoot);

        var psi = new ProcessStartInfo
        {
            FileName = _gitExecutable,
            Arguments = fullArgs,
            WorkingDirectory = effectiveWorkingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Prevent any interactive credential prompt from GCM or git itself
        psi.Environment["PATH"] = _gitProcessPath;
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["GCM_INTERACTIVE"] = "never";
        psi.Environment["GIT_ASKPASS"] = "";

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

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw CreateGitStartException(ex, effectiveWorkingDir, _gitExecutable, _gitProcessPath);
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new CommandResult(process.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd(), false);
    }
}
