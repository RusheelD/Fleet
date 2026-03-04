namespace Fleet.Server.LLM;

/// <summary>Normalized chat message (user, assistant, or tool result).</summary>
public record LLMMessage
{
    public required string Role { get; init; }
    public string? Content { get; init; }
    public IReadOnlyList<LLMToolCall>? ToolCalls { get; init; }
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
}
