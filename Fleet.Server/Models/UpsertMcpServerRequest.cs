using System.ComponentModel.DataAnnotations;

namespace Fleet.Server.Models;

public record UpsertMcpServerRequest(
    [param: Required] string Name,
    string? Description = null,
    [param: Required] string TransportType = "stdio",
    string? Command = null,
    string[]? Arguments = null,
    string? WorkingDirectory = null,
    string? Endpoint = null,
    string? BuiltInTemplateKey = null,
    bool Enabled = true,
    McpServerVariableInput[]? EnvironmentVariables = null,
    McpServerVariableInput[]? Headers = null
);
