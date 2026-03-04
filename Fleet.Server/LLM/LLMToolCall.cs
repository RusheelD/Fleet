namespace Fleet.Server.LLM;

/// <summary>A tool invocation requested by the LLM.</summary>
public record LLMToolCall(string Id, string Name, string ArgumentsJson);
