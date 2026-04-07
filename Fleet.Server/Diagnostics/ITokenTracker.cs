using Fleet.Server.LLM;

namespace Fleet.Server.Diagnostics;

/// <summary>
/// Tracks cumulative LLM token usage for a logical request (chat turn, agent phase).
/// Scoped per DI scope — one instance per HTTP request / background operation.
/// </summary>
public interface ITokenTracker
{
    /// <summary>Record usage from a single LLM completion call.</summary>
    void Record(LLMUsage? usage);

    /// <summary>Total input tokens accumulated so far.</summary>
    int TotalInputTokens { get; }

    /// <summary>Total output tokens accumulated so far.</summary>
    int TotalOutputTokens { get; }

    /// <summary>Total cached input tokens accumulated so far.</summary>
    int TotalCachedTokens { get; }
}
