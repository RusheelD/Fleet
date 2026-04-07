namespace Fleet.Server.Mcp;

public interface IMcpRuntimeConnector
{
    Task<McpRuntimeConnection> ConnectAsync(McpServerRuntimeConfig server, CancellationToken cancellationToken = default);
}
