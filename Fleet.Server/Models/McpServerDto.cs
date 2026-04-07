namespace Fleet.Server.Models;

public record McpServerDto(
    int Id,
    string Name,
    string Description,
    string TransportType,
    string? Command,
    string[] Arguments,
    string? WorkingDirectory,
    string? Endpoint,
    string? BuiltInTemplateKey,
    bool Enabled,
    McpServerVariableDto[] EnvironmentVariables,
    McpServerVariableDto[] Headers,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastValidatedAtUtc,
    string? LastValidationError,
    int LastToolCount,
    string[] DiscoveredTools
);
