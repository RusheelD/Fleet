namespace Fleet.Server.LLM;

/// <summary>
/// Manages adaptive output token limits. Starts conservative and escalates
/// when the model hits the token limit (truncated response).
/// Inspired by Claude Code's output token cap optimization.
/// </summary>
public static class AdaptiveTokenCap
{
    /// <summary>Default starting cap when no explicit MaxTokens is set.</summary>
    public const int DefaultCap = 8192;

    /// <summary>Escalated cap after truncation detection.</summary>
    public const int EscalatedCap = 32768;

    /// <summary>Maximum cap to prevent runaway costs.</summary>
    public const int MaxCap = 65536;

    /// <summary>
    /// Returns the appropriate output token cap for the next request.
    /// If the previous response was truncated, doubles the cap (up to <see cref="MaxCap"/>).
    /// </summary>
    public static int GetNextCap(int currentCap, bool wasTruncated)
    {
        if (!wasTruncated)
            return currentCap;

        var next = Math.Min(currentCap * 2, MaxCap);
        return next;
    }

    /// <summary>
    /// Applies the adaptive cap to a request if no explicit MaxTokens is set.
    /// </summary>
    public static LLMRequest ApplyCap(LLMRequest request, int cap)
    {
        // Don't override an explicit cap already set by the caller
        if (request.MaxTokens.HasValue)
            return request;

        return request with { MaxTokens = cap };
    }
}
