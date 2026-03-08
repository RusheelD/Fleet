namespace Fleet.Server.Agents;

/// <summary>
/// Manages a temporary local clone of a GitHub repository for agent execution.
/// Provides path-restricted file access and sandboxed command execution.
/// </summary>
public interface IRepoSandbox : IAsyncDisposable
{
    /// <summary>Absolute path to the root of the cloned repository.</summary>
    string RepoRoot { get; }

    /// <summary>The GitHub repository full name (owner/repo).</summary>
    string RepoFullName { get; }

    /// <summary>The branch name created for this execution.</summary>
    string BranchName { get; }

    /// <summary>
    /// Clones the repository and checks out a new feature branch.
    /// </summary>
    /// <param name="repoFullName">GitHub repo full name (e.g., "owner/repo").</param>
    /// <param name="accessToken">GitHub OAuth token for authentication.</param>
    /// <param name="branchName">Branch name to create (e.g., "fleet/42-add-auth").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="baseBranch">Optional remote base branch to branch from before creating <paramref name="branchName"/>.</param>
    /// <param name="resumeFromBranch">
    /// When true, attempts to checkout the existing remote <paramref name="branchName"/> branch
    /// instead of creating a new branch from <paramref name="baseBranch"/>.
    /// </param>
    Task CloneAsync(
        string repoFullName,
        string accessToken,
        string branchName,
        CancellationToken cancellationToken = default,
        string? baseBranch = null,
        bool resumeFromBranch = false);

    /// <summary>
    /// Lists files and directories at the given relative path.
    /// </summary>
    /// <param name="relativePath">Path relative to the repo root. Empty string for root.</param>
    /// <returns>List of entries with name, type (file/dir), and size.</returns>
    IReadOnlyList<SandboxEntry> ListDirectory(string relativePath = "");

    /// <summary>
    /// Reads the content of a file relative to the repo root.
    /// </summary>
    /// <param name="relativePath">File path relative to the repo root.</param>
    /// <returns>File content as a string.</returns>
    string ReadFile(string relativePath);

    /// <summary>
    /// Writes content to a file, creating directories as needed.
    /// </summary>
    /// <param name="relativePath">File path relative to the repo root.</param>
    /// <param name="content">Content to write.</param>
    void WriteFile(string relativePath, string content);

    /// <summary>
    /// Deletes a file relative to the repo root.
    /// </summary>
    /// <param name="relativePath">File path relative to the repo root.</param>
    /// <returns>True if the file was deleted; false if it didn't exist.</returns>
    bool DeleteFile(string relativePath);

    /// <summary>
    /// Searches for text matching a pattern in the repository files.
    /// </summary>
    /// <param name="pattern">Search pattern (plain text or regex).</param>
    /// <param name="isRegex">Whether the pattern is a regex.</param>
    /// <param name="fileGlob">Optional file glob filter (e.g., "*.cs").</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>List of matching results with file path, line number, and line content.</returns>
    IReadOnlyList<SearchResult> SearchFiles(string pattern, bool isRegex = false, string? fileGlob = null, int maxResults = 50);

    /// <summary>
    /// Executes a command in the repo root directory with safety limits.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">Command arguments.</param>
    /// <param name="timeoutSeconds">Maximum execution time in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Command execution result with stdout, stderr, and exit code.</returns>
    Task<CommandResult> RunCommandAsync(string command, string arguments, int timeoutSeconds = 120, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages all changes, commits, and pushes to the remote branch.
    /// </summary>
    /// <param name="commitMessage">Commit message.</param>
    /// <param name="authorName">Commit author name.</param>
    /// <param name="authorEmail">Commit author email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CommitAndPushAsync(string commitMessage, string authorName, string authorEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a summary of all file changes since the branch was created.
    /// </summary>
    /// <returns>List of changed files with their status (added/modified/deleted).</returns>
    Task<IReadOnlyList<FileChange>> GetChangeSummaryAsync(CancellationToken cancellationToken = default);
}
