using System.Collections.Concurrent;

namespace Fleet.Server.LLM;

/// <summary>
/// Speculatively pre-executes all read-only tool calls from a response concurrently,
/// caching results so that the batch-processing loop can retrieve them without re-executing.
/// This overlaps read-only tool execution across batch boundaries — reads in batch 3 start
/// running at the same time as reads in batch 1, even if a write batch separates them.
/// Inspired by Claude Code's streaming tool executor (Ch7).
/// </summary>
public sealed class SpeculativeToolExecutor
{
    private readonly ConcurrentDictionary<string, Task<string>> _prefetched = new();

    /// <summary>
    /// Fire off all read-only tool calls concurrently. Call this immediately after
    /// <see cref="ToolCallBatchPlanner.PartitionByReadOnly"/> and before iterating batches.
    /// </summary>
    /// <param name="toolCalls">All tool calls from the LLM response.</param>
    /// <param name="isReadOnly">Predicate that classifies a tool call as read-only.</param>
    /// <param name="executeAsync">The async function that actually runs a tool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public void PrefetchReadOnlyTools(
        IReadOnlyList<LLMToolCall> toolCalls,
        Func<LLMToolCall, bool> isReadOnly,
        Func<LLMToolCall, CancellationToken, Task<string>> executeAsync,
        CancellationToken cancellationToken)
    {
        foreach (var toolCall in toolCalls)
        {
            if (!isReadOnly(toolCall)) continue;

            // Fire and store the task — execution starts immediately
            _prefetched[toolCall.Id] = executeAsync(toolCall, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the cached result for a read-only tool call, or falls back to executing it.
    /// For write tools or tools not pre-fetched, this simply runs the provided delegate.
    /// </summary>
    public Task<string> GetOrExecuteAsync(
        LLMToolCall toolCall,
        Func<LLMToolCall, CancellationToken, Task<string>> executeAsync,
        CancellationToken cancellationToken)
    {
        if (_prefetched.TryRemove(toolCall.Id, out var cachedTask))
        {
            return cachedTask;
        }

        return executeAsync(toolCall, cancellationToken);
    }

    /// <summary>Number of tool calls that were pre-fetched.</summary>
    public int PrefetchedCount => _prefetched.Count;
}
