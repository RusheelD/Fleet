using System.Collections.Concurrent;
using System.Text;

namespace Fleet.Server.Agents;

/// <summary>
/// Thread-safe key-value scratchpad shared across all agent phases within
/// a single execution. Agents can write named entries (e.g., "api_contracts",
/// "architecture_decisions") that downstream phases can selectively read,
/// preserving detail that would otherwise be lost in phase output summarization.
/// Adapted from Claude Code's inter-agent scratchpad / peer messaging pattern.
/// </summary>
public sealed class AgentScratchpad
{
    private readonly ConcurrentDictionary<string, ScratchpadEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Writes or overwrites a named entry. The author role is recorded for attribution.
    /// </summary>
    public void Write(string key, string content, AgentRole author)
    {
        _entries[key] = new ScratchpadEntry(content, author, DateTime.UtcNow);
    }

    /// <summary>
    /// Appends to an existing entry or creates a new one.
    /// </summary>
    public void Append(string key, string content, AgentRole author)
    {
        _entries.AddOrUpdate(
            key,
            new ScratchpadEntry(content, author, DateTime.UtcNow),
            (_, existing) => existing with
            {
                Content = existing.Content + "\n" + content,
                Author = author,
                UpdatedAt = DateTime.UtcNow
            });
    }

    /// <summary>
    /// Reads a named entry, or null if not found.
    /// </summary>
    public ScratchpadEntry? Read(string key)
    {
        return _entries.TryGetValue(key, out var entry) ? entry : null;
    }

    /// <summary>
    /// Lists all entry keys with their author and size.
    /// </summary>
    public string ListEntries()
    {
        if (_entries.IsEmpty)
            return "Scratchpad is empty. No entries have been written by any phase.";

        var sb = new StringBuilder();
        sb.AppendLine("Scratchpad entries:");
        foreach (var (key, entry) in _entries.OrderBy(kv => kv.Value.UpdatedAt))
        {
            sb.AppendLine($"  - {key} (by {entry.Author}, {entry.Content.Length:N0} chars)");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Whether any entries exist.</summary>
    public bool HasEntries => !_entries.IsEmpty;

    /// <summary>Number of entries.</summary>
    public int Count => _entries.Count;

    public sealed record ScratchpadEntry(string Content, AgentRole Author, DateTime UpdatedAt);
}
