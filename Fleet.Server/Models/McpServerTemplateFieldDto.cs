namespace Fleet.Server.Models;

public record McpServerTemplateFieldDto(
    string Name,
    string Description,
    bool IsSecret,
    bool Required,
    string? DefaultValue = null
);
