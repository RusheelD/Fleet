namespace Fleet.Server.Models;

public record McpServerVariableDto(
    string Name,
    string? Value,
    bool IsSecret,
    bool HasValue
);
