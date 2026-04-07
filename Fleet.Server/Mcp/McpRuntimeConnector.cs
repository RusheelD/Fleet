using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Fleet.Server.Mcp;

public class McpRuntimeConnector(
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    ILogger<McpRuntimeConnector> logger) : IMcpRuntimeConnector
{
    public async Task<McpRuntimeConnection> ConnectAsync(McpServerRuntimeConfig server, CancellationToken cancellationToken = default)
    {
        var transport = CreateTransport(server);
        var client = await McpClient.CreateAsync(transport, null, loggerFactory, cancellationToken);
        try
        {
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            var descriptors = tools
                .Select(tool => new McpRuntimeToolDescriptor
                {
                    FleetToolName = BuildFleetToolName(server.Name, tool.Name),
                    ServerName = server.Name,
                    ToolName = tool.Name,
                    Description = BuildToolDescription(server.Name, tool),
                    ParametersJsonSchema = tool.JsonSchema.ValueKind == JsonValueKind.Undefined
                        ? "{}"
                        : tool.JsonSchema.GetRawText(),
                    IsReadOnly = tool.ProtocolTool.Annotations?.ReadOnlyHint ?? false,
                    ClientTool = tool,
                })
                .ToList();

            return new McpRuntimeConnection(client, descriptors);
        }
        catch
        {
            await DisposeClientAsync(client);
            throw;
        }
    }

    private IClientTransport CreateTransport(McpServerRuntimeConfig server)
    {
        if (string.Equals(server.TransportType, "http", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(server.Endpoint))
                throw new InvalidOperationException($"MCP server '{server.Name}' is missing an endpoint.");

            var transportOptions = new HttpClientTransportOptions
            {
                Name = server.Name,
                Endpoint = new Uri(server.Endpoint, UriKind.Absolute),
                TransportMode = HttpTransportMode.AutoDetect,
            };

            foreach (var header in server.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Value))
                    continue;

                transportOptions.AdditionalHeaders ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                transportOptions.AdditionalHeaders[header.Key] = header.Value;
            }

            var httpClient = httpClientFactory.CreateClient();
            return new HttpClientTransport(transportOptions, httpClient, loggerFactory, ownsHttpClient: false);
        }

        if (string.IsNullOrWhiteSpace(server.Command))
            throw new InvalidOperationException($"MCP server '{server.Name}' is missing a command.");

        return new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = server.Name,
                Command = server.Command,
                Arguments = [.. server.Arguments],
                WorkingDirectory = string.IsNullOrWhiteSpace(server.WorkingDirectory) ? null : server.WorkingDirectory,
                EnvironmentVariables = server.EnvironmentVariables
                    .Where(pair => pair.Value is not null)
                    .ToDictionary(pair => pair.Key, pair => (string?)pair.Value!, StringComparer.OrdinalIgnoreCase),
                StandardErrorLines = line =>
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        logger.LogDebug("MCP server {ServerName} stderr: {Line}", server.Name, line);
                    }
                },
            },
            loggerFactory);
    }

    private static string BuildFleetToolName(string serverName, string toolName)
        => $"mcp__{Slugify(serverName)}__{toolName}";

    private static string BuildToolDescription(string serverName, McpClientTool tool)
    {
        var serverLabel = string.IsNullOrWhiteSpace(serverName) ? "External MCP server" : $"MCP server '{serverName}'";
        var description = string.IsNullOrWhiteSpace(tool.Description)
            ? "No description provided by the MCP server."
            : tool.Description.Trim();

        return $"{serverLabel}: {description}";
    }

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        var slug = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "server" : slug;
    }

    private static async Task DisposeClientAsync(McpClient client)
    {
        if (client is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        if (client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
