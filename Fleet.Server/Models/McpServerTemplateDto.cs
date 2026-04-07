namespace Fleet.Server.Models;

public record McpServerTemplateDto(
    string Key,
    string Name,
    string Description,
    string TransportType,
    string? Command,
    string[] Arguments,
    string? WorkingDirectory,
    string? Endpoint,
    McpServerTemplateFieldDto[] EnvironmentVariables,
    McpServerTemplateFieldDto[] Headers,
    string[] Notes
);
