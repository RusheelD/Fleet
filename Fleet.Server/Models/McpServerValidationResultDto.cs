namespace Fleet.Server.Models;

public record McpServerValidationResultDto(
    bool Success,
    string? Error,
    int ToolCount,
    string[] ToolNames
);
