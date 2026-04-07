namespace Fleet.Server.Mcp;

public record McpServerRuntimeConfig(
    int Id,
    string Name,
    string TransportType,
    string? Command,
    string[] Arguments,
    string? WorkingDirectory,
    string? Endpoint,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    IReadOnlyDictionary<string, string?> Headers
);
