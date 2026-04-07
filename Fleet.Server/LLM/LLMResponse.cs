namespace Fleet.Server.LLM;

/// <summary>Normalized response from any LLM provider.</summary>
public record LLMResponse(
    string? Content,
    IReadOnlyList<LLMToolCall>? ToolCalls,
    LLMUsage? Usage = null
);

/// <summary>Token usage reported by the LLM provider.</summary>
public record LLMUsage(
    int InputTokens,
    int OutputTokens,
    int? CachedInputTokens = null
)
{
    public int TotalTokens => InputTokens + OutputTokens;
}

