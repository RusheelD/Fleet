namespace Fleet.Server.Models;

/// <summary>Describes a single tool invocation during the AI response.</summary>
public record ToolEventDto(
    string ToolName,
    string ArgumentsJson,
    string Result
);
