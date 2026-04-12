using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
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
    private const int CleanupIntervalWrites = 64;

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private int _writesSinceCleanup;

    /// <summary>
    /// Retrieves or builds the memory prompt block.
    /// Cached by (userId, projectId, query) with a short TTL.
    /// </summary>
    public async Task<string> GetMemoryBlockAsync(
        IMemoryService memoryService,
        int userId,
        string? projectId,
        string? query,
        CancellationToken cancellationToken)
    {
        var key = BuildMemoryKey(userId, projectId, query);
        if (TryGetCached(key, out var cached))
            return cached;

        var block = await memoryService.BuildPromptBlockAsync(userId, projectId, query, cancellationToken);
        Set(key, block);
        return block;
    }

    /// <summary>
    /// Retrieves or builds the skill prompt block.
    /// Cached by (userId, projectId, query, conversationContext) with a short TTL.
    /// </summary>
    public async Task<string> GetSkillBlockAsync(
        ISkillService skillService,
        int userId,
        string? projectId,
        string? query,
        CancellationToken cancellationToken)
    {
        var key = BuildSkillKey(userId, projectId, query, conversationContext: null);
        if (TryGetCached(key, out var cached))
            return cached;

        var block = await skillService.BuildPromptBlockAsync(userId, projectId, query, cancellationToken);
        Set(key, block);
        return block;
    }

    /// <summary>
    /// Retrieves or builds the skill prompt block with conversation context.
    /// Cached by (userId, projectId, query, conversationContext) with a short TTL.
    /// </summary>
    public async Task<string> GetSkillBlockAsync(
        ISkillService skillService,
        int userId,
        string? projectId,
        string? query,
        IReadOnlyList<string>? conversationContext,
        CancellationToken cancellationToken)
    {
        var key = BuildSkillKey(userId, projectId, query, conversationContext);
        if (TryGetCached(key, out var cached))
            return cached;

        var block = await skillService.BuildPromptBlockAsync(userId, projectId, query, conversationContext, cancellationToken);
        Set(key, block);
        return block;
    }

    /// <summary>
    /// Memory blocks include personal memories in every project-scoped prompt,
    /// so any memory edit for the user should invalidate all cached memory keys.
    /// </summary>
    public void InvalidateMemoryBlocks(int userId) => RemoveByPrefix($"memory:{userId}:");

    /// <summary>
    /// Skill blocks include personal playbooks in every project-scoped prompt,
    /// so any skill edit for the user should invalidate all cached skill keys.
    /// </summary>
    public void InvalidateSkillBlocks(int userId) => RemoveByPrefix($"skill:{userId}:");

    private bool TryGetCached(string key, out string value)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow - entry.CachedAt < DefaultTtl)
            {
                value = entry.Value;
                return true;
            }

            _cache.TryRemove(key, out _);
        }

        value = string.Empty;
        return false;
    }

    private void Set(string key, string value)
    {
        _cache[key] = new CacheEntry(value, DateTime.UtcNow);

        var writesSinceCleanup = Interlocked.Increment(ref _writesSinceCleanup);
        if (writesSinceCleanup < CleanupIntervalWrites)
        {
            return;
        }

        if (Interlocked.Exchange(ref _writesSinceCleanup, 0) >= CleanupIntervalWrites)
        {
            TrimExpiredEntries();
        }
    }

    private void RemoveByPrefix(string prefix)
    {
        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    private void TrimExpiredEntries()
    {
        var cutoff = DateTime.UtcNow - DefaultTtl;

        foreach (var pair in _cache)
        {
            if (pair.Value.CachedAt >= cutoff)
            {
                continue;
            }

            _cache.TryRemove(pair.Key, out _);
        }
    }

    private static string BuildMemoryKey(int userId, string? projectId, string? query)
        => $"memory:{userId}:{NormalizeProjectId(projectId)}:{ComputeHash(query)}";

    private static string BuildSkillKey(int userId, string? projectId, string? query, IReadOnlyList<string>? conversationContext)
        => $"skill:{userId}:{NormalizeProjectId(projectId)}:{ComputeHash(query)}:{ComputeConversationHash(conversationContext)}";

    private static string NormalizeProjectId(string? projectId)
        => string.IsNullOrWhiteSpace(projectId) ? "__global__" : projectId.Trim();

    private static string ComputeConversationHash(IReadOnlyList<string>? conversationContext)
    {
        if (conversationContext is null || conversationContext.Count == 0)
        {
            return "none";
        }

        return ComputeHash(string.Join('\n', conversationContext));
    }

    private static string ComputeHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes[..8]);
    }

    private sealed record CacheEntry(string Value, DateTime CachedAt);
}
