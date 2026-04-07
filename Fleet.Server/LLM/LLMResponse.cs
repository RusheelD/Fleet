namespace Fleet.Server.LLM;

/// <summary>Normalized response from any LLM provider.</summary>
public record LLMResponse(
    string? Content,
    IReadOnlyList<LLMToolCall>? ToolCalls,
    LLMUsage? Usage = null,
    /// <summary>Why the model stopped generating. "max_tokens" indicates truncation.</summary>
    string? StopReason = null
)
{
    /// <summary>True when the model hit the output token limit and the response may be incomplete.</summary>
    public bool WasTruncated => string.Equals(StopReason, "max_tokens", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(StopReason, "length", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Token usage reported by the LLM provider.</summary>
public record LLMUsage(
    int InputTokens,
    int OutputTokens,
    int? CachedInputTokens = null
)
{
    public int TotalTokens => InputTokens + OutputTokens;
}

