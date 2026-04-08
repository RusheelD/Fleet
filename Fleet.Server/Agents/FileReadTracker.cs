using System.Collections.Concurrent;

namespace Fleet.Server.Agents;

/// <summary>
/// Tracks file content hashes from read_file calls so that edit_file can detect
/// when a file has been modified since the model last read it. This prevents
/// stale edits — if the model's view of the file is outdated, the edit is
/// rejected with a prompt to re-read.
/// Inspired by Claude Code's file edit staleness detection (Ch6).
/// </summary>
/// <remarks>
/// Scoped per request — shared across all tool calls within a single agent phase.
/// </remarks>
public sealed class FileReadTracker
{
    private readonly ConcurrentDictionary<string, int> _contentHashes = new();

    /// <summary>
    /// Records the content of a file read by the model.
    /// </summary>
    public void RecordRead(string relativePath, string content)
    {
        _contentHashes[NormalizePath(relativePath)] = content.GetHashCode();
    }

    /// <summary>
    /// Checks if the file has changed since the model last read it.
    /// Returns an error message if stale, or null if fresh (or never tracked).
    /// </summary>
    public string? CheckFreshness(string relativePath, string currentContent)
    {
        var key = NormalizePath(relativePath);
        if (!_contentHashes.TryGetValue(key, out var storedHash))
            return null; // Never read by the model — skip check

        if (currentContent.GetHashCode() == storedHash)
            return null; // Fresh — content matches what the model last saw

        return $"Error: the file '{relativePath}' has been modified since you last read it. "
               + "Please use read_file to review the current content before editing.";
    }

    /// <summary>
    /// Updates the tracker after a successful edit or write so subsequent
    /// edits to the same file don't trigger a false staleness warning.
    /// </summary>
    public void RecordWrite(string relativePath, string newContent)
    {
        _contentHashes[NormalizePath(relativePath)] = newContent.GetHashCode();
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
