using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, byte> ActiveSandboxRoots = new(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>Default shallow history depth for branch setup and lightweight fetches.</summary>
    private const int DefaultGitHistoryDepth = 50;

    /// <summary>Binary file extensions to skip during search.</summary>
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bin", ".obj", ".o", ".so", ".dylib",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg",
        ".zip", ".tar", ".gz", ".rar", ".7z",
        ".pdf", ".woff", ".woff2", ".ttf", ".eot",
        ".lock", ".min.js", ".min.css",
    };

    private static readonly string[] LocalGitIgnoreEntries =
    [
        ".venv/",
        "node_modules/",
        ".fleet-assets/",
    ];

    private const string PythonVirtualEnvironmentDirectoryName = ".venv";
    internal const string SharedPythonSitePackagesEnvVar = "FLEET_SHARED_PYTHON_SITE_PACKAGES";
    internal const string SharedNodeModulesPathEnvVar = "FLEET_SHARED_NODE_MODULES_PATH";
    internal const string SharedNodeBinPathEnvVar = "FLEET_SHARED_NODE_BIN_PATH";

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
        bool resumeFromBranch = false,
        bool rebaseOntoBaseBranchWhenResuming = false)
    {
        _repoFullName = repoFullName;
        _branchName = branchName;

        // Create a unique repo directory underneath the configured sandbox root.
        _repoRoot = Path.Combine(_sandboxRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repoRoot);
        TrackActiveSandboxRoot(_repoRoot);

        _logger.LogInformation("Cloning {Repo} into {Path}", repoFullName, _repoRoot);

        // Clone with standard HTTPS username/token auth. The linked account flow stores
        // GitHub OAuth access tokens, not GitHub App installation tokens, so the
        // `x-access-token` username pattern is not appropriate here.
        var cloneUrl = BuildAuthenticatedCloneUrl(repoFullName, accessToken);
        var result = await RunGitAsync(
            ["clone", "--depth", DefaultGitHistoryDepth.ToString(), cloneUrl, _repoRoot],
            workingDir: _sandboxRoot,
            cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git clone failed: {result.Stderr}");

        // Resume an existing feature branch from origin if requested.
        if (resumeFromBranch)
        {
            result = await WaitForRemoteBranchAsync(branchName, cancellationToken);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Stdout))
            {
                throw new InvalidOperationException(
                    $"Git remote branch lookup failed for '{branchName}'. " +
                    "The branch was not found on origin, so retry cannot resume from it.");
            }

            result = await FetchRemoteBranchAsync(
                branchName,
                cancellationToken,
                depth: DefaultGitHistoryDepth);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Git fetch of existing branch '{branchName}' failed: {result.Stderr}");

            result = await RunGitAsync(
                ["checkout", "-B", branchName, $"refs/remotes/origin/{branchName}"],
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Git checkout of existing branch '{branchName}' failed: {result.Stderr}");

            if (rebaseOntoBaseBranchWhenResuming &&
                !string.IsNullOrWhiteSpace(baseBranch) &&
                !string.Equals(branchName, baseBranch, StringComparison.OrdinalIgnoreCase))
            {
                await RebaseCurrentBranchOntoBaseBranchAsync(baseBranch, cancellationToken);
            }
        }
        // Create and checkout a new feature branch from a caller-provided base branch.
        else if (!string.IsNullOrWhiteSpace(baseBranch))
        {
            result = await WaitForRemoteBranchAsync(baseBranch, cancellationToken);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Stdout))
                throw new InvalidOperationException(
                    $"Target/base branch '{baseBranch}' was not found on origin.");

            result = await FetchRemoteBranchAsync(
                baseBranch,
                cancellationToken,
                depth: DefaultGitHistoryDepth);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Git fetch of base branch '{baseBranch}' failed: {result.Stderr}");

            result = await RunGitAsync(
                ["checkout", "-B", branchName, $"refs/remotes/origin/{baseBranch}"],
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Git checkout from base branch '{baseBranch}' failed: {result.Stderr}");
        }
        else
        {
            result = await RunGitAsync(
                ["checkout", "-b", branchName],
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git checkout failed: {result.Stderr}");
        }

        EnsureLocalGitIgnoreEntries(_repoRoot, LocalGitIgnoreEntries);

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
            WriteFileIfChangedCore(fullPath, content, skipWhenUnchanged: false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public bool WriteFileIfChanged(string relativePath, string content)
    {
        EnsureInitialized();
        _writeLock.Wait();
        try
        {
            var fullPath = ResolveSafePath(relativePath);
            return WriteFileIfChangedCore(fullPath, content, skipWhenUnchanged: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void WriteBinaryFile(string relativePath, byte[] content)
    {
        EnsureInitialized();
        _writeLock.Wait();
        try
        {
            var fullPath = ResolveSafePath(relativePath);

            var dir = Path.GetDirectoryName(fullPath);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(fullPath, content);
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

    internal static bool WriteFileIfChangedCore(string fullPath, string content, bool skipWhenUnchanged)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (skipWhenUnchanged && File.Exists(fullPath))
        {
            var existingContent = File.ReadAllText(fullPath);
            if (string.Equals(existingContent, content, StringComparison.Ordinal))
                return false;
        }

        File.WriteAllText(fullPath, content);
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

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var normalizedCommand = command.Trim();
            var commandName = Path.GetFileName(normalizedCommand).ToLowerInvariant();
            var requiresPythonEnvironment = RequiresPythonVirtualEnvironment(commandName, arguments);
            var requiresNodeEnvironment = RequiresNodeLocalEnvironment(commandName, arguments);

            if (requiresPythonEnvironment)
                await EnsurePythonVirtualEnvironmentAsync(cts.Token);

            if (requiresNodeEnvironment)
                EnsureNodeLocalEnvironment();

            var effectiveCommand = normalizedCommand;
            if (requiresPythonEnvironment)
            {
                if (IsDirectPipCommand(commandName))
                    effectiveCommand = GetPipExecutablePath(_repoRoot);
                else if (IsDirectPythonCommand(commandName))
                    effectiveCommand = GetPythonExecutablePath(_repoRoot);
            }

            var psi = new ProcessStartInfo
            {
                FileName = effectiveCommand,
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

            if (requiresPythonEnvironment)
            {
                var existingPath = psi.Environment.TryGetValue("PATH", out var pathValue)
                    ? pathValue ?? string.Empty
                    : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                psi.Environment["PATH"] = PrependToPath(GetPythonVirtualEnvironmentBinDirectory(_repoRoot), existingPath);
                psi.Environment["VIRTUAL_ENV"] = GetPythonVirtualEnvironmentRoot(_repoRoot);
                psi.Environment["PIP_REQUIRE_VIRTUALENV"] = "1";
                psi.Environment["PIP_DISABLE_PIP_VERSION_CHECK"] = "1";
                psi.Environment["PIP_NO_INPUT"] = "1";
                psi.Environment["PYTHONNOUSERSITE"] = "1";
                psi.Environment["PIP_CACHE_DIR"] = EnsureGitInternalToolPath(_repoRoot, "fleet-pip-cache");
                ApplySharedPythonToolingEnvironment(psi.Environment);
            }

            if (requiresNodeEnvironment)
            {
                var npmCache = EnsureGitInternalToolPath(_repoRoot, "fleet-npm-cache");
                var npmPrefix = EnsureGitInternalToolPath(_repoRoot, "fleet-npm-global");
                var npmUserConfig = EnsureGitInternalFilePath(
                    _repoRoot,
                    "fleet-npmrc",
                    "fund=false\nupdate-notifier=false\naudit=false\n");
                psi.Environment["npm_config_cache"] = npmCache;
                psi.Environment["NPM_CONFIG_CACHE"] = npmCache;
                psi.Environment["npm_config_prefix"] = npmPrefix;
                psi.Environment["NPM_CONFIG_PREFIX"] = npmPrefix;
                psi.Environment["npm_config_userconfig"] = npmUserConfig;
                psi.Environment["NPM_CONFIG_USERCONFIG"] = npmUserConfig;
                psi.Environment["npm_config_update_notifier"] = "false";
                psi.Environment["npm_config_fund"] = "false";
                psi.Environment["npm_config_audit"] = "false";
                ApplySharedNodeToolingEnvironment(psi.Environment);
            }

            return await RunProcessAsync(psi, cts.Token);
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
            var result = await RunGitAsync(
                ["add", "-A"],
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git add failed: {result.Stderr}");

            // Check if there are changes to commit
            var status = await RunGitAsync(
                ["status", "--porcelain"],
                cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(status.Stdout))
            {
                _logger.LogInformation("No changes to commit");
                return;
            }

            // Commit
            result = await RunGitAsync(
                BuildCommitArgumentList(commitMessage),
                BuildCommitEnvironment(authorName, authorEmail),
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git commit failed: {result.Stderr}");

            // Push to remote
            await UpdateOriginRemoteAsync(accessToken, cancellationToken);
            result = await RunGitAsync(
                ["push", "-u", "origin", _branchName],
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

    public async Task CommitFilesAndPushAsync(
        string accessToken,
        IReadOnlyList<string> relativePaths,
        string commitMessage,
        string authorName,
        string authorEmail,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(relativePaths);

        var normalizedPaths = relativePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeRelativePathSpec)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedPaths.Length == 0)
            return;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var result = await RunGitAsync(
                BuildAddPathspecArgumentList(normalizedPaths),
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git add failed: {result.Stderr}");

            var stagedStatus = await RunGitAsync(
                BuildDiffCachedNameOnlyArgumentList(normalizedPaths),
                cancellationToken: cancellationToken);
            if (stagedStatus.ExitCode != 0)
                throw new InvalidOperationException($"Git diff failed: {stagedStatus.Stderr}");
            if (string.IsNullOrWhiteSpace(stagedStatus.Stdout))
            {
                _logger.LogInformation("No targeted changes to commit for {Paths}", string.Join(", ", normalizedPaths));
                return;
            }

            result = await RunGitAsync(
                BuildCommitOnlyArgumentList(commitMessage, normalizedPaths),
                BuildCommitEnvironment(authorName, authorEmail),
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git commit failed: {result.Stderr}");

            await UpdateOriginRemoteAsync(accessToken, cancellationToken);
            result = await RunGitAsync(
                ["push", "-u", "origin", _branchName],
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git push failed: {result.Stderr}");

            _logger.LogInformation(
                "Committed and pushed targeted files to {Branch}: {Paths}",
                _branchName,
                string.Join(", ", normalizedPaths));
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task MergeBranchAsync(
        string accessToken,
        string sourceBranchName,
        string authorName,
        string authorEmail,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(sourceBranchName))
            throw new ArgumentException("Source branch name is required.", nameof(sourceBranchName));

            await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await UpdateOriginRemoteAsync(accessToken, cancellationToken);

            var normalizedSourceBranch = NormalizeBranchName(sourceBranchName, nameof(sourceBranchName));
            var result = await FetchRemoteBranchAsync(
                normalizedSourceBranch,
                cancellationToken,
                depth: DefaultGitHistoryDepth);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Git fetch of merge source branch '{normalizedSourceBranch}' failed: {result.Stderr}");
            }

            var hasCommonMergeBase = await EnsureCommonMergeBaseAsync(normalizedSourceBranch, cancellationToken);
            if (!hasCommonMergeBase)
            {
                throw new InvalidOperationException(
                    $"Git merge of branch '{normalizedSourceBranch}' into '{_branchName}' failed: " +
                    "Fleet could not determine a common merge base even after fetching full history. " +
                    "This sub-flow branch no longer matches the current parent-flow lineage and should be rerun from the active parent context.");
            }

            result = await RunGitAsync(
                BuildMergeBranchArgumentList(normalizedSourceBranch),
                BuildCommitEnvironment(authorName, authorEmail),
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
            {
                await AbortMergeIfNeededAsync(cancellationToken);

                if (LooksLikeUnrelatedHistoriesMergeFailure(result.Stderr))
                {
                    await EnsureFullMergeHistoryAsync(normalizedSourceBranch, cancellationToken);
                    hasCommonMergeBase = await HasCommonMergeBaseAsync(normalizedSourceBranch, cancellationToken);
                    if (!hasCommonMergeBase)
                    {
                        throw new InvalidOperationException(
                            $"Git merge of branch '{normalizedSourceBranch}' into '{_branchName}' failed: " +
                            "Fleet still could not determine a common merge base after fetching full history. " +
                            "This sub-flow branch no longer matches the current parent-flow lineage and should be rerun from the active parent context.");
                    }

                    result = await RunGitAsync(
                        BuildMergeBranchArgumentList(normalizedSourceBranch),
                        BuildCommitEnvironment(authorName, authorEmail),
                        cancellationToken: cancellationToken);
                    if (result.ExitCode == 0)
                    {
                        _logger.LogInformation(
                            "Merged branch {SourceBranch} into {TargetBranch} after deepening git history",
                            normalizedSourceBranch,
                            _branchName);
                        return;
                    }

                    await AbortMergeIfNeededAsync(cancellationToken);
                }

                throw new InvalidOperationException(
                    $"Git merge of branch '{normalizedSourceBranch}' into '{_branchName}' failed: {result.Stderr}");
            }

            _logger.LogInformation("Merged branch {SourceBranch} into {TargetBranch}", normalizedSourceBranch, _branchName);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> IsRemoteBranchMergedIntoCurrentBranchAsync(
        string accessToken,
        string sourceBranchName,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(sourceBranchName))
            throw new ArgumentException("Source branch name is required.", nameof(sourceBranchName));

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await UpdateOriginRemoteAsync(accessToken, cancellationToken);

            var normalizedSourceBranch = NormalizeBranchName(sourceBranchName, nameof(sourceBranchName));
            var result = await FetchRemoteBranchAsync(
                normalizedSourceBranch,
                cancellationToken,
                depth: DefaultGitHistoryDepth);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Git fetch of branch '{normalizedSourceBranch}' failed while checking whether it is already merged into '{_branchName}': {result.Stderr}");
            }

            result = await RunGitAsync(
                BuildBranchHeadContainedInCurrentBranchArgumentList(normalizedSourceBranch),
                cancellationToken: cancellationToken);
            if (result.ExitCode == 0)
                return true;

            if (result.ExitCode == 1)
            {
                await EnsureFullMergeHistoryAsync(normalizedSourceBranch, cancellationToken);
                result = await RunGitAsync(
                    BuildBranchHeadContainedInCurrentBranchArgumentList(normalizedSourceBranch),
                    cancellationToken: cancellationToken);
                if (result.ExitCode == 0)
                    return true;
                if (result.ExitCode == 1)
                    return false;
            }

            throw new InvalidOperationException(
                $"Git branch containment check for '{normalizedSourceBranch}' against '{_branchName}' failed: {result.Stderr}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task RebaseCurrentBranchOntoBaseBranchAsync(string baseBranch, CancellationToken cancellationToken)
    {
        var normalizedBaseBranch = NormalizeBranchName(baseBranch, nameof(baseBranch));
        var result = await FetchRemoteBranchAsync(
            normalizedBaseBranch,
            cancellationToken,
            depth: DefaultGitHistoryDepth);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git fetch of rebase base branch '{normalizedBaseBranch}' failed: {result.Stderr}");
        }

        var hasCommonMergeBase = await EnsureCommonMergeBaseAsync(normalizedBaseBranch, cancellationToken);
        if (!hasCommonMergeBase)
        {
            throw new InvalidOperationException(
                $"Git rebase of branch '{_branchName}' onto '{normalizedBaseBranch}' failed: " +
                "Fleet could not determine a common merge base even after fetching full history. " +
                "This sub-flow branch no longer matches the current parent-flow lineage and should be rerun from the active parent context.");
        }

        result = await RunGitAsync(
            BuildRebaseOntoRemoteBranchArgumentList(normalizedBaseBranch),
            cancellationToken: cancellationToken);
        if (result.ExitCode == 0)
        {
            _logger.LogInformation("Rebased branch {Branch} onto {BaseBranch}", _branchName, normalizedBaseBranch);
            return;
        }

        await AbortRebaseIfNeededAsync(cancellationToken);

        if (LooksLikeUnrelatedHistoriesMergeFailure(result.Stderr))
        {
            await EnsureFullMergeHistoryAsync(normalizedBaseBranch, cancellationToken);
            hasCommonMergeBase = await HasCommonMergeBaseAsync(normalizedBaseBranch, cancellationToken);
            if (!hasCommonMergeBase)
            {
                throw new InvalidOperationException(
                    $"Git rebase of branch '{_branchName}' onto '{normalizedBaseBranch}' failed: " +
                    "Fleet still could not determine a common merge base after fetching full history. " +
                    "This sub-flow branch no longer matches the current parent-flow lineage and should be rerun from the active parent context.");
            }

            result = await RunGitAsync(
                BuildRebaseOntoRemoteBranchArgumentList(normalizedBaseBranch),
                cancellationToken: cancellationToken);
            if (result.ExitCode == 0)
            {
                _logger.LogInformation(
                    "Rebased branch {Branch} onto {BaseBranch} after deepening git history",
                    _branchName,
                    normalizedBaseBranch);
                return;
            }

            await AbortRebaseIfNeededAsync(cancellationToken);
        }

        throw new InvalidOperationException(
            $"Git rebase of branch '{_branchName}' onto '{normalizedBaseBranch}' failed: {result.Stderr}");
    }

    public async Task PushBranchAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await UpdateOriginRemoteAsync(accessToken, cancellationToken);
            var result = await RunGitAsync(
                ["push", "-u", "origin", _branchName],
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Git push failed: {result.Stderr}");

            _logger.LogInformation("Pushed branch {Branch}", _branchName);
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
            ["remote", "set-url", "origin", remoteUrl],
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Git remote update failed: {result.Stderr}");
    }

    private async Task AbortRebaseIfNeededAsync(CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            ["rebase", "--abort"],
            cancellationToken: cancellationToken);

        if (result.ExitCode == 0)
            _logger.LogDebug("Aborted in-progress git rebase in sandbox");
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

        if (!string.IsNullOrEmpty(_repoRoot))
        {
            if (Directory.Exists(_repoRoot))
            {
                _logger.LogInformation("Cleaning up sandbox: {Path}", _repoRoot);
                try
                {
                    DeleteSandboxDirectory(_repoRoot);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up sandbox directory: {Path}", _repoRoot);
                }
            }

            ReleaseActiveSandboxRoot(_repoRoot);
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

        var toolchainBlockReason = DetectGlobalToolchainMutation(command, arguments);
        if (!string.IsNullOrWhiteSpace(toolchainBlockReason))
            throw new UnauthorizedAccessException(toolchainBlockReason);

        if (IsProtectedBranchPush(combined))
            throw new UnauthorizedAccessException($"Blocked push to protected branch: {command} {arguments}");
    }

    internal static string? DetectGlobalToolchainMutation(string command, string arguments)
    {
        var commandName = Path.GetFileName(command).ToLowerInvariant();
        var combined = $"{commandName} {arguments}".ToLowerInvariant();

        string[] blockedSystemPackageMutations =
        [
            "apt-get install",
            "apt install",
            "apt-get upgrade",
            "apt upgrade",
            "yum install",
            "dnf install",
            "apk add",
            "pacman -s",
            "brew install",
            "winget install",
            "choco install",
            "scoop install",
            "dotnet workload install",
        ];

        if (blockedSystemPackageMutations.Any(pattern => combined.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            return "Global toolchain mutation is blocked. Use project-local dependencies only.";
        }

        if (Regex.IsMatch(combined, @"\bnpm\s+(install|i|add|update|uninstall|remove|rm|link)\b.*(?:^|\s)(-g|--global)\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(combined, @"\bnpm\s+(?:-g|--global)\b.*\b(install|i|add|update|uninstall|remove|rm|link)\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(combined, @"\bnpm\s+global\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(combined, @"\bnpm\s+config\s+set\s+(prefix|cache)\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(combined, @"\bnpm\b.*(?:^|\s)--location=global\b", RegexOptions.IgnoreCase))
        {
            return "Global npm mutations are blocked. Use repo-local npm installs only.";
        }

        if (Regex.IsMatch(combined, @"\bdotnet\s+tool\s+(install|update|uninstall)\b.*(?:^|\s)(-g|--global)\b", RegexOptions.IgnoreCase))
        {
            return "Global dotnet tool mutations are blocked. Use repo-local tooling only.";
        }

        var referencesPip = Regex.IsMatch(combined, @"\b(pip3?|python3?\s+-m\s+pip|py\s+-m\s+pip)\b", RegexOptions.IgnoreCase);
        if (referencesPip &&
            Regex.IsMatch(combined, @"(?:^|\s)(--user|--target|-t|--prefix|--root|--break-system-packages)\b", RegexOptions.IgnoreCase))
        {
            return "Python package installs are forced into the run-local .venv. Global or redirected pip install flags are blocked.";
        }

        return null;
    }

    internal static string GetPythonVirtualEnvironmentRoot(string repoRoot)
        => Path.Combine(repoRoot, PythonVirtualEnvironmentDirectoryName);

    internal static string GetPythonVirtualEnvironmentBinDirectory(string repoRoot)
        => Path.Combine(
            GetPythonVirtualEnvironmentRoot(repoRoot),
            OperatingSystem.IsWindows() ? "Scripts" : "bin");

    internal static string GetPythonExecutablePath(string repoRoot)
        => Path.Combine(
            GetPythonVirtualEnvironmentBinDirectory(repoRoot),
            OperatingSystem.IsWindows() ? "python.exe" : "python");

    internal static string GetPipExecutablePath(string repoRoot)
        => Path.Combine(
            GetPythonVirtualEnvironmentBinDirectory(repoRoot),
            OperatingSystem.IsWindows() ? "pip.exe" : "pip");

    internal static void EnsureLocalGitIgnoreEntries(string repoRoot, IEnumerable<string> entries)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
            throw new ArgumentException("Repository root is required.", nameof(repoRoot));

        var filteredEntries = entries
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (filteredEntries.Length == 0)
            return;

        var infoDirectory = Path.Combine(repoRoot, ".git", "info");
        Directory.CreateDirectory(infoDirectory);

        var excludePath = Path.Combine(infoDirectory, "exclude");
        var existingLines = File.Exists(excludePath)
            ? File.ReadAllLines(excludePath)
            : [];

        var knownEntries = existingLines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToHashSet(StringComparer.Ordinal);

        var missingEntries = filteredEntries
            .Where(entry => !knownEntries.Contains(entry))
            .ToArray();
        if (missingEntries.Length == 0)
            return;

        using var writer = new StreamWriter(excludePath, append: existingLines.Length > 0);
        if (existingLines.Length > 0)
            writer.WriteLine();

        foreach (var entry in missingEntries)
            writer.WriteLine(entry);
    }

    private static bool RequiresPythonVirtualEnvironment(string commandName, string arguments)
    {
        var combined = $"{commandName} {arguments}".ToLowerInvariant();
        return Regex.IsMatch(
            combined,
            @"\b(pip3?|python3?|py|pytest|ruff|mypy|tox|coverage|uvicorn|flask|django-admin)\b",
            RegexOptions.IgnoreCase);
    }

    private static bool RequiresNodeLocalEnvironment(string commandName, string arguments)
    {
        var combined = $"{commandName} {arguments}".ToLowerInvariant();
        return Regex.IsMatch(combined, @"\b(npm|npx|node)\b", RegexOptions.IgnoreCase);
    }

    private static bool IsDirectPythonCommand(string commandName)
        => commandName is "python" or "python3" or "py";

    private static bool IsDirectPipCommand(string commandName)
        => commandName is "pip" or "pip3";

    private async Task EnsurePythonVirtualEnvironmentAsync(CancellationToken cancellationToken)
    {
        EnsureLocalGitIgnoreEntries(_repoRoot, [".venv/"]);

        var pythonExecutablePath = GetPythonExecutablePath(_repoRoot);
        if (File.Exists(pythonExecutablePath))
            return;

        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("venv");
        psi.ArgumentList.Add(PythonVirtualEnvironmentDirectoryName);
        psi.Environment["HOME"] = _repoRoot;
        psi.Environment["USERPROFILE"] = _repoRoot;

        var result = await RunProcessAsync(psi, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to create Python virtual environment at '{PythonVirtualEnvironmentDirectoryName}': {result.Stderr}");
        }
    }

    private void EnsureNodeLocalEnvironment()
    {
        EnsureGitInternalToolPath(_repoRoot, "fleet-npm-cache");
        EnsureGitInternalToolPath(_repoRoot, "fleet-npm-global");
        EnsureGitInternalFilePath(
            _repoRoot,
            "fleet-npmrc",
            "fund=false\nupdate-notifier=false\naudit=false\n");
        EnsureLocalGitIgnoreEntries(_repoRoot, ["node_modules/"]);
    }

    private static string EnsureGitInternalToolPath(string repoRoot, string entryName)
    {
        var path = Path.Combine(repoRoot, ".git", entryName);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string EnsureGitInternalFilePath(string repoRoot, string entryName, string content)
    {
        var path = Path.Combine(repoRoot, ".git", entryName);
        var parentDirectory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
            Directory.CreateDirectory(parentDirectory);

        if (!File.Exists(path))
            File.WriteAllText(path, content);

        return path;
    }

    private static string PrependToPath(string segment, string existingPath)
    {
        if (string.IsNullOrWhiteSpace(existingPath))
            return segment;

        var separator = Path.PathSeparator;
        var existingSegments = existingPath.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        if (existingSegments.Any(path => string.Equals(path, segment, StringComparison.OrdinalIgnoreCase)))
            return existingPath;

        return $"{segment}{separator}{existingPath}";
    }

    internal static void ApplySharedPythonToolingEnvironment(
        IDictionary<string, string?> environment,
        string? sharedPythonSitePackages = null)
    {
        var effectiveSharedPath = string.IsNullOrWhiteSpace(sharedPythonSitePackages)
            ? Environment.GetEnvironmentVariable(SharedPythonSitePackagesEnvVar)
            : sharedPythonSitePackages;

        if (string.IsNullOrWhiteSpace(effectiveSharedPath))
            return;

        var existingPythonPath = environment.TryGetValue("PYTHONPATH", out var pythonPathValue)
            ? pythonPathValue ?? string.Empty
            : string.Empty;
        environment["PYTHONPATH"] = PrependToPath(effectiveSharedPath, existingPythonPath);
    }

    internal static void ApplySharedNodeToolingEnvironment(
        IDictionary<string, string?> environment,
        string? sharedNodeModulesPath = null,
        string? sharedNodeBinPath = null)
    {
        var effectiveNodeModulesPath = string.IsNullOrWhiteSpace(sharedNodeModulesPath)
            ? Environment.GetEnvironmentVariable(SharedNodeModulesPathEnvVar)
            : sharedNodeModulesPath;
        if (!string.IsNullOrWhiteSpace(effectiveNodeModulesPath))
        {
            var existingNodePath = environment.TryGetValue("NODE_PATH", out var nodePathValue)
                ? nodePathValue ?? string.Empty
                : Environment.GetEnvironmentVariable("NODE_PATH") ?? string.Empty;
            environment["NODE_PATH"] = PrependToPath(effectiveNodeModulesPath, existingNodePath);
        }

        var effectiveNodeBinPath = string.IsNullOrWhiteSpace(sharedNodeBinPath)
            ? Environment.GetEnvironmentVariable(SharedNodeBinPathEnvVar)
            : sharedNodeBinPath;
        if (!string.IsNullOrWhiteSpace(effectiveNodeBinPath))
        {
            var existingPath = environment.TryGetValue("PATH", out var pathValue)
                ? pathValue ?? string.Empty
                : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            environment["PATH"] = PrependToPath(effectiveNodeBinPath, existingPath);
        }
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

    internal static IReadOnlyList<string> BuildCommitArgumentList(string commitMessage)
    {
        if (string.IsNullOrWhiteSpace(commitMessage))
            throw new ArgumentException("Commit message is required.", nameof(commitMessage));

        return
        [
            "commit",
            "-m",
            commitMessage,
        ];
    }

    internal static IReadOnlyList<string> BuildCommitOnlyArgumentList(string commitMessage, IReadOnlyList<string> relativePaths)
    {
        if (string.IsNullOrWhiteSpace(commitMessage))
            throw new ArgumentException("Commit message is required.", nameof(commitMessage));
        if (relativePaths is null || relativePaths.Count == 0)
            throw new ArgumentException("At least one path is required.", nameof(relativePaths));

        return
        [
            "commit",
            "--only",
            "-m",
            commitMessage,
            "--",
            .. relativePaths,
        ];
    }

    internal static IReadOnlyList<string> BuildAddPathspecArgumentList(IReadOnlyList<string> relativePaths)
    {
        if (relativePaths is null || relativePaths.Count == 0)
            throw new ArgumentException("At least one path is required.", nameof(relativePaths));

        return
        [
            "add",
            "--",
            .. relativePaths,
        ];
    }

    internal static IReadOnlyList<string> BuildDiffCachedNameOnlyArgumentList(IReadOnlyList<string> relativePaths)
    {
        if (relativePaths is null || relativePaths.Count == 0)
            throw new ArgumentException("At least one path is required.", nameof(relativePaths));

        return
        [
            "diff",
            "--cached",
            "--name-only",
            "--",
            .. relativePaths,
        ];
    }

    internal static IReadOnlyDictionary<string, string> BuildCommitEnvironment(string authorName, string authorEmail)
    {
        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("Author name is required.", nameof(authorName));

        if (string.IsNullOrWhiteSpace(authorEmail))
            throw new ArgumentException("Author email is required.", nameof(authorEmail));

        var normalizedAuthorName = authorName.Trim();
        var normalizedAuthorEmail = authorEmail.Trim();

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_AUTHOR_NAME"] = normalizedAuthorName,
            ["GIT_AUTHOR_EMAIL"] = normalizedAuthorEmail,
            ["GIT_COMMITTER_NAME"] = normalizedAuthorName,
            ["GIT_COMMITTER_EMAIL"] = normalizedAuthorEmail,
        };
    }

    internal static IReadOnlyList<string> BuildMergeBranchArgumentList(string sourceBranchName)
    {
        var normalizedSourceBranch = NormalizeBranchName(sourceBranchName, nameof(sourceBranchName));
        return
        [
            "merge",
            "--no-ff",
            "--no-edit",
            "-X",
            "theirs",
            BuildRemoteTrackingBranchRef(normalizedSourceBranch),
        ];
    }

    internal static IReadOnlyList<string> BuildFetchRemoteBranchArgumentList(string branchName, int? depth = null)
    {
        var normalizedBranch = NormalizeBranchName(branchName, nameof(branchName));
        var remoteRef = BuildRemoteTrackingBranchRef(normalizedBranch);

        var arguments = new List<string> { "fetch" };
        if (depth is > 0)
        {
            arguments.Add("--depth");
            arguments.Add(depth.Value.ToString());
        }

        arguments.Add("origin");
        arguments.Add($"+refs/heads/{normalizedBranch}:{remoteRef}");
        return arguments;
    }

    internal static IReadOnlyList<string> BuildMergeBaseArgumentList(string sourceBranchName)
    {
        var normalizedSourceBranch = NormalizeBranchName(sourceBranchName, nameof(sourceBranchName));
        return
        [
            "merge-base",
            "HEAD",
            BuildRemoteTrackingBranchRef(normalizedSourceBranch),
        ];
    }

    internal static IReadOnlyList<string> BuildBranchHeadContainedInCurrentBranchArgumentList(string sourceBranchName)
    {
        var normalizedSourceBranch = NormalizeBranchName(sourceBranchName, nameof(sourceBranchName));
        return
        [
            "merge-base",
            "--is-ancestor",
            BuildRemoteTrackingBranchRef(normalizedSourceBranch),
            "HEAD",
        ];
    }

    internal static IReadOnlyList<string> BuildRebaseOntoRemoteBranchArgumentList(string branchName)
    {
        var normalizedBranch = NormalizeBranchName(branchName, nameof(branchName));
        return
        [
            "rebase",
            BuildRemoteTrackingBranchRef(normalizedBranch),
        ];
    }

    internal static bool LooksLikeUnrelatedHistoriesMergeFailure(string? stderr)
        => !string.IsNullOrWhiteSpace(stderr) &&
           stderr.Contains("refusing to merge unrelated histories", StringComparison.OrdinalIgnoreCase);

    internal static bool IsAlreadyCompleteHistoryFetchMessage(string? stderr)
        => !string.IsNullOrWhiteSpace(stderr) &&
           (stderr.Contains("does not make sense", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("not a shallow repository", StringComparison.OrdinalIgnoreCase));

    internal static string BuildRemoteTrackingBranchRef(string branchName)
        => $"refs/remotes/origin/{NormalizeBranchName(branchName, nameof(branchName))}";

    internal static string NormalizeBranchName(string branchName, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
            throw new ArgumentException("Branch name is required.", argumentName);

        return branchName.Trim();
    }

    private string NormalizeRelativePathSpec(string relativePath)
    {
        var fullPath = ResolveSafePath(relativePath);
        var normalized = Path.GetRelativePath(_repoRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized == ".")
            throw new ArgumentException("Relative path must target a file inside the repository.", nameof(relativePath));

        return normalized;
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

    internal static void TrackActiveSandboxRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var fullPath = Path.GetFullPath(path);
        ActiveSandboxRoots[fullPath] = 0;
    }

    internal static void ReleaseActiveSandboxRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var fullPath = Path.GetFullPath(path);
        ActiveSandboxRoots.TryRemove(fullPath, out _);
    }

    internal static int CleanupStaleSandboxes(string sandboxRoot, TimeSpan staleAfter, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(sandboxRoot) || !Directory.Exists(sandboxRoot))
            return 0;

        var cutoffUtc = DateTime.UtcNow - (staleAfter <= TimeSpan.Zero ? TimeSpan.FromHours(12) : staleAfter);
        var deleted = 0;

        foreach (var directory in Directory.EnumerateDirectories(sandboxRoot))
        {
            var fullPath = Path.GetFullPath(directory);
            if (ActiveSandboxRoots.ContainsKey(fullPath))
                continue;

            DateTime lastWriteUtc;
            try
            {
                lastWriteUtc = Directory.GetLastWriteTimeUtc(fullPath);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Skipping sandbox cleanup for {SandboxPath} because its age could not be determined", fullPath);
                continue;
            }

            if (lastWriteUtc > cutoffUtc)
                continue;

            try
            {
                DeleteSandboxDirectory(fullPath);
                deleted++;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to delete stale sandbox directory: {SandboxPath}", fullPath);
            }
        }

        return deleted;
    }

    private static void DeleteSandboxDirectory(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
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

    private async Task<CommandResult> RunProcessAsync(ProcessStartInfo psi, CancellationToken cancellationToken)
    {
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

            await process.WaitForExitAsync(cancellationToken);
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
            timedOut);
    }

    private async Task<CommandResult> RunGitAsync(string arguments, string? workingDir = null, CancellationToken cancellationToken = default)
    {
        // Prepend flags that disable all credential helpers and interactive prompts.
        // Without this, Git Credential Manager (GCM) on Windows opens a browser/dialog
        // asking the user to pick an account — which blocks headless server execution.
        var effectiveWorkingDir = EnsureGitWorkingDirectory(workingDir, _repoRoot, _sandboxRoot);

        var psi = new ProcessStartInfo
        {
            FileName = _gitExecutable,
            Arguments = $"-c credential.helper= {arguments}",
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

        return await RunGitAsync(psi, effectiveWorkingDir, cancellationToken);
    }

    private async Task<CommandResult> RunGitAsync(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        string? workingDir = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveWorkingDir = EnsureGitWorkingDirectory(workingDir, _repoRoot, _sandboxRoot);

        var psi = new ProcessStartInfo
        {
            FileName = _gitExecutable,
            WorkingDirectory = effectiveWorkingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("credential.helper=");
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        psi.Environment["PATH"] = _gitProcessPath;
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["GCM_INTERACTIVE"] = "never";
        psi.Environment["GIT_ASKPASS"] = "";

        if (environment is not null)
        {
            foreach (var entry in environment)
                psi.Environment[entry.Key] = entry.Value;
        }

        return await RunGitAsync(psi, effectiveWorkingDir, cancellationToken);
    }

    private async Task<CommandResult> RunGitAsync(
        ProcessStartInfo psi,
        string effectiveWorkingDir,
        CancellationToken cancellationToken)
    {
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

    private async Task AbortMergeIfNeededAsync(CancellationToken cancellationToken)
    {
        try
        {
            var abortResult = await RunGitAsync(
                ["merge", "--abort"],
                cancellationToken: cancellationToken);

            if (abortResult.ExitCode != 0)
            {
                _logger.LogDebug("Merge abort was not needed or could not complete cleanly: {Error}", abortResult.Stderr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to abort an in-progress merge cleanly");
        }
    }

    private async Task<CommandResult> FetchRemoteBranchAsync(
        string branchName,
        CancellationToken cancellationToken,
        int? depth = null)
        => await RunGitAsync(
            BuildFetchRemoteBranchArgumentList(branchName, depth),
            cancellationToken: cancellationToken);

    private async Task<CommandResult> WaitForRemoteBranchAsync(
        string branchName,
        CancellationToken cancellationToken)
    {
        CommandResult? lastResult = null;
        var delays = new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
        };

        foreach (var delay in delays)
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);

            lastResult = await RunGitAsync(
                ["ls-remote", "--heads", "origin", branchName],
                cancellationToken: cancellationToken);
            if (lastResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(lastResult.Stdout))
                return lastResult;
        }

        return lastResult ?? new CommandResult(-1, string.Empty, "Remote branch lookup did not run.", false);
    }

    private async Task<bool> EnsureCommonMergeBaseAsync(string sourceBranchName, CancellationToken cancellationToken)
    {
        if (await HasCommonMergeBaseAsync(sourceBranchName, cancellationToken))
            return true;

        _logger.LogWarning(
            "Git merge between {TargetBranch} and {SourceBranch} has no merge base in the current sandbox history; fetching deeper history before retrying",
            _branchName,
            sourceBranchName);

        await EnsureFullMergeHistoryAsync(sourceBranchName, cancellationToken);
        return await HasCommonMergeBaseAsync(sourceBranchName, cancellationToken);
    }

    private async Task EnsureFullMergeHistoryAsync(string sourceBranchName, CancellationToken cancellationToken)
    {
        if (await IsShallowRepositoryAsync(cancellationToken))
        {
            var unshallowResult = await RunGitAsync(
                ["fetch", "--unshallow", "origin"],
                cancellationToken: cancellationToken);

            if (unshallowResult.ExitCode != 0 && !IsAlreadyCompleteHistoryFetchMessage(unshallowResult.Stderr))
            {
                _logger.LogWarning(
                    "Git unshallow fetch failed while preparing to merge {SourceBranch} into {TargetBranch}: {Error}",
                    sourceBranchName,
                    _branchName,
                    unshallowResult.Stderr);
            }
        }

        var sourceFetchResult = await FetchRemoteBranchAsync(sourceBranchName, cancellationToken, depth: null);
        if (sourceFetchResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git fetch of merge source branch '{sourceBranchName}' failed while deepening history: {sourceFetchResult.Stderr}");
        }

        if (!string.Equals(sourceBranchName, _branchName, StringComparison.OrdinalIgnoreCase) &&
            await RemoteTrackingBranchExistsAsync(_branchName, cancellationToken))
        {
            var targetFetchResult = await FetchRemoteBranchAsync(_branchName, cancellationToken, depth: null);
            if (targetFetchResult.ExitCode != 0)
            {
                _logger.LogWarning(
                    "Git fetch of target branch history '{TargetBranch}' failed while preparing merge from {SourceBranch}: {Error}",
                    _branchName,
                    sourceBranchName,
                    targetFetchResult.Stderr);
            }
        }
    }

    private async Task<bool> HasCommonMergeBaseAsync(string sourceBranchName, CancellationToken cancellationToken)
    {
        var mergeBaseResult = await RunGitAsync(
            BuildMergeBaseArgumentList(sourceBranchName),
            cancellationToken: cancellationToken);

        return mergeBaseResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(mergeBaseResult.Stdout);
    }

    private async Task<bool> IsShallowRepositoryAsync(CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            ["rev-parse", "--is-shallow-repository"],
            cancellationToken: cancellationToken);

        return result.ExitCode == 0 &&
               string.Equals(result.Stdout.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> RemoteTrackingBranchExistsAsync(string branchName, CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            ["show-ref", "--verify", "--quiet", BuildRemoteTrackingBranchRef(branchName)],
            cancellationToken: cancellationToken);

        return result.ExitCode == 0;
    }
}
