using System.Collections.Concurrent;

namespace Fleet.Server.LLM;

/// <summary>
/// In-memory store for oversized tool results within a single request scope.
/// When a tool result exceeds the per-tool output limit, the full result is stored
/// here and the model receives a truncated version with a reference ID. The model
/// can then use <c>recall_tool_output</c> to retrieve specific sections of the
/// full result without re-executing the tool.
/// Adapted from Claude Code's tool result disk persistence pattern (Ch6).
/// </summary>
public sealed class ToolResultStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    /// <summary>
    /// Stores the full result and returns a truncated version with a recall reference.
    /// If the result fits within <paramref name="maxLength"/>, returns as-is.
    /// </summary>
    public string StoreIfOversized(string toolCallId, string result, int maxLength)
    {
        if (result.Length <= maxLength)
            return result;

        _store[toolCallId] = result;

        return result[..maxLength]
               + $"\n\n[Output truncated at {maxLength:N0} of {result.Length:N0} chars. "
               + $"Use recall_tool_output with ref_id \"{toolCallId}\" to retrieve more.]";
    }

    /// <summary>
    /// Retrieves a section of a previously stored tool result.
    /// </summary>
    public string? Recall(string refId, int offset = 0, int length = 8000)
    {
        if (!_store.TryGetValue(refId, out var fullResult))
            return null;

        if (offset >= fullResult.Length)
            return $"[Offset {offset} is past the end of the stored output ({fullResult.Length} chars)]";

        var end = Math.Min(offset + length, fullResult.Length);
        var section = fullResult[offset..end];
        var remaining = fullResult.Length - end;

        return remaining > 0
            ? $"{section}\n\n[Showing chars {offset}–{end - 1} of {fullResult.Length}. {remaining:N0} chars remaining.]"
            : section;
    }

    /// <summary>Whether any results have been stored.</summary>
    public bool HasStoredResults => !_store.IsEmpty;

    /// <summary>Number of stored results.</summary>
    public int Count => _store.Count;
}
