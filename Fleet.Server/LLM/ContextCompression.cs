using System.Text;

namespace Fleet.Server.LLM;

/// <summary>
/// Multi-layer context compression inspired by Claude Code's 4-layer system.
/// Each layer is progressively more aggressive:
///   Layer 0 — Tool result budget: truncate oversized tool results.
///   Layer 1 — Snip: remove oldest messages beyond a retention window.
///   Layer 2 — Collapse tool results: replace tool output with summaries.
///   Layer 3 — Summarize: replace early conversation spans with a compact summary.
/// </summary>
public static class ContextCompression
{
    /// <summary>Approximate characters-per-token ratio for estimation.</summary>
    private const double CharsPerToken = 4.0;

    /// <summary>Maximum characters allowed in a single tool result message.</summary>
    private const int MaxToolResultChars = 50_000;

    /// <summary>When a tool result exceeds this, it is truncated with a note.</summary>
    private const int ToolResultTruncationThreshold = 40_000;

    /// <summary>Percentage of context window to target after compression (leave headroom).</summary>
    private const double TargetCapacityRatio = 0.80;

    /// <summary>Minimum number of recent messages to always preserve.</summary>
    private const int MinPreservedMessages = 6;

    /// <summary>
    /// Compress a message list so it fits within the effective context window.
    /// Returns a new list — the original is not modified.
    /// </summary>
    public static IReadOnlyList<LLMMessage> Compress(
        IReadOnlyList<LLMMessage> messages,
        int contextWindowTokens,
        int reservedOutputTokens)
    {
        if (messages.Count == 0)
        {
            return messages;
        }

        var effectiveLimit = contextWindowTokens - reservedOutputTokens;
        var targetChars = (int)(effectiveLimit * CharsPerToken * TargetCapacityRatio);

        var result = new List<LLMMessage>(messages);

        // Layer 0: Truncate oversized tool results.
        result = ApplyToolResultBudget(result);

        if (EstimateChars(result) <= targetChars)
        {
            return result;
        }

        // Layer 1: Snip oldest messages (keep the most recent ones).
        result = SnipOldMessages(result, targetChars);

        if (EstimateChars(result) <= targetChars)
        {
            return result;
        }

        // Layer 2: Collapse remaining tool results to short summaries.
        result = CollapseToolResults(result);

        if (EstimateChars(result) <= targetChars)
        {
            return result;
        }

        // Layer 3: Aggressive — replace early messages with a single summary.
        result = SummarizeEarlyMessages(result, targetChars);

        return result;
    }

    /// <summary>Layer 0 — Truncate any tool result content that exceeds the budget.</summary>
    private static List<LLMMessage> ApplyToolResultBudget(List<LLMMessage> messages)
    {
        return messages
            .Select(m =>
            {
                if (m.Role != "tool" || m.Content is null || m.Content.Length <= MaxToolResultChars)
                {
                    return m;
                }

                return m with
                {
                    Content = m.Content[..ToolResultTruncationThreshold]
                              + $"\n\n[Tool result truncated — original was {m.Content.Length:N0} chars, showing first {ToolResultTruncationThreshold:N0}]"
                };
            })
            .ToList();
    }

    /// <summary>Layer 1 — Remove oldest messages until we're under the target.</summary>
    private static List<LLMMessage> SnipOldMessages(List<LLMMessage> messages, int targetChars)
    {
        if (messages.Count <= MinPreservedMessages)
        {
            return messages;
        }

        // Keep removing from the front until under target, but always keep the tail.
        var result = new List<LLMMessage>(messages);
        int removedCount = 0;

        while (result.Count > MinPreservedMessages && EstimateChars(result) > targetChars)
        {
            result.RemoveAt(0);
            removedCount++;
        }

        if (removedCount > 0)
        {
            result.Insert(0, new LLMMessage
            {
                Role = "user",
                Content = $"[{removedCount} earlier messages were removed to fit the context window. The conversation continues from here.]",
            });
        }

        return result;
    }

    /// <summary>Layer 2 — Replace tool result content with a short indicator.</summary>
    private static List<LLMMessage> CollapseToolResults(List<LLMMessage> messages)
    {
        // Collapse all tool results except the most recent 2.
        int toolResultCount = messages.Count(m => m.Role == "tool");
        if (toolResultCount <= 2)
        {
            return messages;
        }

        int seen = 0;
        int preserveFrom = toolResultCount - 2;

        return messages
            .Select(m =>
            {
                if (m.Role != "tool")
                {
                    return m;
                }

                seen++;
                if (seen > preserveFrom)
                {
                    return m;
                }

                var preview = m.Content is { Length: > 80 }
                    ? m.Content[..80] + "..."
                    : m.Content ?? "(empty)";

                return m with
                {
                    Content = $"[Collapsed tool result: {preview}]"
                };
            })
            .ToList();
    }

    /// <summary>Layer 3 — Replace the first half of messages with a compact summary.</summary>
    private static List<LLMMessage> SummarizeEarlyMessages(List<LLMMessage> messages, int targetChars)
    {
        if (messages.Count <= MinPreservedMessages)
        {
            return messages;
        }

        // Keep the most recent half; summarize the first half.
        int keepCount = Math.Max(MinPreservedMessages, messages.Count / 2);
        int summarizeCount = messages.Count - keepCount;

        if (summarizeCount <= 1)
        {
            return messages;
        }

        var summarized = messages.Take(summarizeCount).ToList();
        var preserved = messages.Skip(summarizeCount).ToList();

        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine($"[Context summary — {summarizeCount} earlier messages condensed]");
        summaryBuilder.AppendLine("Topics covered:");

        foreach (var msg in summarized)
        {
            if (msg.Role == "user" && msg.Content is { Length: > 0 })
            {
                var preview = msg.Content.Length > 120 ? msg.Content[..120] + "..." : msg.Content;
                summaryBuilder.AppendLine($"  - User: {preview}");
            }
            else if (msg.Role == "assistant" && msg.Content is { Length: > 0 })
            {
                var preview = msg.Content.Length > 120 ? msg.Content[..120] + "..." : msg.Content;
                summaryBuilder.AppendLine($"  - Assistant: {preview}");
            }
            else if (msg.Role == "tool")
            {
                summaryBuilder.AppendLine($"  - Tool result ({msg.ToolName ?? "unknown"})");
            }
        }

        var summaryMessage = new LLMMessage
        {
            Role = "user",
            Content = summaryBuilder.ToString().TrimEnd()
        };

        var result = new List<LLMMessage>(1 + preserved.Count) { summaryMessage };
        result.AddRange(preserved);
        return result;
    }

    /// <summary>Rough character count of all message content.</summary>
    private static int EstimateChars(IReadOnlyList<LLMMessage> messages)
    {
        int total = 0;
        foreach (var m in messages)
        {
            total += m.Content?.Length ?? 0;
            if (m.ToolCalls is not null)
            {
                foreach (var tc in m.ToolCalls)
                {
                    total += tc.ArgumentsJson?.Length ?? 0;
                }
            }
        }

        return total;
    }

    /// <summary>Estimate token count from characters.</summary>
    public static int EstimateTokens(IReadOnlyList<LLMMessage> messages)
        => (int)(EstimateChars(messages) / CharsPerToken);
}
