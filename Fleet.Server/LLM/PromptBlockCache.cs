using System.Collections.Concurrent;
using Fleet.Server.Memories;
using Fleet.Server.Skills;

namespace Fleet.Server.LLM;

/// <summary>
/// Short-lived cache for memory and skill prompt blocks. Within a configurable
/// window, repeated requests from the same user+project reuse previously computed
/// blocks instead of re-querying the database and re-running relevance ranking.
/// This is especially valuable during agent executions where multiple phases
/// share the same user+project context.
/// Adapted from Claude Code's system prompt memoization pattern (Ch7).
/// </summary>
public sealed class PromptBlockCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    /// <summary>
    /// Retrieves or builds the memory prompt block.
    /// Cached by (userId, projectId) with a short TTL.
    /// </summary>
    public async Task<string> GetMemoryBlockAsync(
        IMemoryService memoryService,
        int userId,
        string? projectId,
        string? query,
        CancellationToken cancellationToken)
    {
        var key = $"memory:{userId}:{projectId ?? ""}";
        if (TryGetCached(key, out var cached))
            return cached;

        var block = await memoryService.BuildPromptBlockAsync(userId, projectId, query, cancellationToken);
        Set(key, block);
        return block;
    }

    /// <summary>
    /// Retrieves or builds the skill prompt block.
    /// Cached by (userId, projectId) with a short TTL.
    /// </summary>
    public async Task<string> GetSkillBlockAsync(
        ISkillService skillService,
        int userId,
        string? projectId,
        string? query,
        CancellationToken cancellationToken)
    {
        var key = $"skill:{userId}:{projectId ?? ""}";
        if (TryGetCached(key, out var cached))
            return cached;

        var block = await skillService.BuildPromptBlockAsync(userId, projectId, query, cancellationToken);
        Set(key, block);
        return block;
    }

    /// <summary>
    /// Retrieves or builds the skill prompt block with conversation context.
    /// Cached by (userId, projectId) with a short TTL.
    /// </summary>
    public async Task<string> GetSkillBlockAsync(
        ISkillService skillService,
        int userId,
        string? projectId,
        string? query,
        IReadOnlyList<string>? conversationContext,
        CancellationToken cancellationToken)
    {
        var key = $"skill:{userId}:{projectId ?? ""}";
        if (TryGetCached(key, out var cached))
            return cached;

        var block = await skillService.BuildPromptBlockAsync(userId, projectId, query, conversationContext, cancellationToken);
        Set(key, block);
        return block;
    }

    private bool TryGetCached(string key, out string value)
    {
        if (_cache.TryGetValue(key, out var entry) && DateTime.UtcNow - entry.CachedAt < DefaultTtl)
        {
            value = entry.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private void Set(string key, string value)
    {
        _cache[key] = new CacheEntry(value, DateTime.UtcNow);
    }

    private sealed record CacheEntry(string Value, DateTime CachedAt);
}
