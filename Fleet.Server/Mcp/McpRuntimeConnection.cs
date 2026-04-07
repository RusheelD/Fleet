using ModelContextProtocol.Client;

namespace Fleet.Server.Mcp;

public sealed class McpRuntimeConnection(McpClient client, IReadOnlyList<McpRuntimeToolDescriptor> tools) : IAsyncDisposable
{
    public McpClient Client { get; } = client;
    public IReadOnlyList<McpRuntimeToolDescriptor> Tools { get; } = tools;

    public async ValueTask DisposeAsync()
    {
        if (Client is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        if (Client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
