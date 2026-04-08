namespace Fleet.Server.LLM;

/// <summary>
/// Tracks cumulative tool-result size within a single LLM turn and progressively
/// truncates later results as the aggregate budget is consumed. This prevents
/// pathological turns (e.g., 15 read-file calls) from flooding the context window.
/// Inspired by Claude Code's per-conversation aggregate result budget (Ch6).
/// </summary>
/// <remarks>
/// Per-tool truncation (MaxToolOutputLength) still applies first — this class only
/// enforces the aggregate cap across all tools in one response batch.
/// </remarks>
public sealed class ToolResultBudget(int totalBudgetChars)
{
    private int _consumed;

    /// <summary>Characters remaining before the aggregate budget is exhausted.</summary>
    public int Remaining => Math.Max(0, totalBudgetChars - _consumed);

    /// <summary>Total characters consumed so far.</summary>
    public int Consumed => _consumed;

    /// <summary>
    /// Applies the aggregate budget to a (possibly already per-tool-truncated) result.
    /// Returns the result unchanged if within budget, or truncated/omitted if the
    /// budget is exhausted.
    /// </summary>
    public string Apply(string result)
    {
        if (totalBudgetChars <= 0)
        {
            // No aggregate budget configured — pass through
            _consumed += result.Length;
            return result;
        }

        var remaining = Remaining;

        if (remaining <= 0)
        {
            _consumed += 0; // nothing added
            return "[Output omitted — aggregate tool-result budget exhausted]";
        }

        if (result.Length <= remaining)
        {
            _consumed += result.Length;
            return result;
        }

        // Partial fit — truncate to remaining budget
        var truncated = result[..remaining] + "\n... (truncated — aggregate tool-result budget reached)";
        _consumed += remaining;
        return truncated;
    }
}
