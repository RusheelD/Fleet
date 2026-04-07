using System.Text.Json;
using Fleet.Server.Data.Entities;
using Fleet.Server.LLM;

namespace Fleet.Server.Memories;

/// <summary>
/// Uses a lightweight LLM call to re-rank candidate memories by semantic relevance.
/// Only invoked when there are too many candidates for keyword scoring to be reliable.
/// </summary>
public interface IMemoryReranker
{
    /// <summary>
    /// Given a query and a set of candidate memories, return the indices of the
    /// most relevant ones ordered by relevance. Falls back gracefully on failure.
    /// </summary>
    Task<IReadOnlyList<int>> RerankAsync(
        string query,
        IReadOnlyList<MemoryEntry> candidates,
        int maxResults,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public class MemoryReranker(
    ILLMClient llmClient,
    IModelCatalog modelCatalog,
    ILogger<MemoryReranker> logger) : IMemoryReranker
{
    /// <summary>Minimum candidate count before LLM reranking is worthwhile.</summary>
    internal const int MinCandidatesForRerank = 10;

    private const string RerankPrompt = """
        You are a relevance scoring assistant. Given a user query and a numbered list of saved memories,
        return the indices (0-based) of the most relevant memories for the query, in order of decreasing relevance.

        Return ONLY a JSON array of integers, e.g. [3, 0, 7, 1]
        No explanation, no markdown — just the raw JSON array.
        """;

    public async Task<IReadOnlyList<int>> RerankAsync(
        string query,
        IReadOnlyList<MemoryEntry> candidates,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count < MinCandidatesForRerank || string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            // Build a compact summary of each candidate for the LLM
            var candidateList = string.Join("\n", candidates.Select((m, i) =>
                $"[{i}] {m.Name} ({m.Type}): {Truncate(m.Content, 120)}"));

            var userMessage = $"Query: {query}\n\nMemories:\n{candidateList}";

            var model = modelCatalog.Get("Fast");
            var request = new LLMRequest(
                RerankPrompt,
                [new LLMMessage { Role = "user", Content = userMessage }],
                ModelOverride: model,
                MaxTokens: 256);

            var response = await llmClient.CompleteAsync(request, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Content))
                return [];

            return ParseIndices(response.Content, candidates.Count, maxResults);
        }
        catch (Exception ex)
        {
            // Non-critical — log and fall back to keyword scoring
            logger.LogWarning(ex, "LLM memory reranking failed, falling back to keyword scoring");
            return [];
        }
    }

    private static IReadOnlyList<int> ParseIndices(string content, int candidateCount, int maxResults)
    {
        try
        {
            var start = content.IndexOf('[');
            var end = content.LastIndexOf(']');
            if (start < 0 || end < 0 || end <= start)
                return [];

            var jsonArray = content[start..(end + 1)];
            var indices = JsonSerializer.Deserialize<int[]>(jsonArray);
            if (indices is null)
                return [];

            // Filter to valid indices and deduplicate
            return indices
                .Where(i => i >= 0 && i < candidateCount)
                .Distinct()
                .Take(maxResults)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (text is null || text.Length <= maxLength)
            return text ?? string.Empty;
        return text[..maxLength] + "…";
    }
}
