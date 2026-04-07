using ModelContextProtocol.Client;

namespace Fleet.Server.Mcp;

public sealed class McpRuntimeToolDescriptor
{
    public required string FleetToolName { get; init; }
    public required string ServerName { get; init; }
    public required string ToolName { get; init; }
    public required string Description { get; init; }
    public required string ParametersJsonSchema { get; init; }
    public required bool IsReadOnly { get; init; }
    public required McpClientTool ClientTool { get; init; }
}
