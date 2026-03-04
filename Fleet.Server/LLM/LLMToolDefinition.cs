namespace Fleet.Server.LLM;

/// <summary>Schema definition for a tool the LLM can invoke.</summary>
public record LLMToolDefinition(string Name, string Description, string ParametersJsonSchema);
