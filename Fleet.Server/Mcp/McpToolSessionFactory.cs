using System.Text.Json;
using Fleet.Server.Agents;
using Fleet.Server.LLM;
using ModelContextProtocol.Protocol;

namespace Fleet.Server.Mcp;

public class McpToolSessionFactory(
    IMcpServerService serverService,
    IMcpRuntimeConnector runtimeConnector,
    ILogger<McpToolSessionFactory> logger) : IMcpToolSessionFactory
{
    public Task<IMcpToolSession> CreateForChatAsync(int userId, bool includeWriteTools, CancellationToken cancellationToken = default)
        => CreateSessionAsync(
            userId,
            descriptor => includeWriteTools || descriptor.IsReadOnly,
            cancellationToken);

    public Task<IMcpToolSession> CreateForAgentAsync(string userId, AgentRole role, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(userId, out var parsedUserId))
            return Task.FromResult<IMcpToolSession>(EmptyMcpToolSession.Instance);

        return CreateSessionAsync(
            parsedUserId,
            descriptor => role != AgentRole.Manager || descriptor.IsReadOnly,
            cancellationToken);
    }

    private async Task<IMcpToolSession> CreateSessionAsync(
        int userId,
        Func<McpRuntimeToolDescriptor, bool> includeTool,
        CancellationToken cancellationToken)
    {
        var userConfigs = await serverService.GetEnabledRuntimeConfigsAsync(userId);
        var systemConfigs = serverService.GetSystemRuntimeConfigs();
        var configs = MergeConfigs(systemConfigs, userConfigs);

        if (configs.Count == 0)
            return EmptyMcpToolSession.Instance;

        var connections = new List<McpRuntimeConnection>();
        var definitions = new List<LLMToolDefinition>();
        var toolsByName = new Dictionary<string, McpRuntimeToolDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var config in configs)
        {
            try
            {
                var connection = await runtimeConnector.ConnectAsync(config, cancellationToken);
                connections.Add(connection);

                foreach (var descriptor in connection.Tools.Where(includeTool))
                {
                    if (!toolsByName.TryAdd(descriptor.FleetToolName, descriptor))
                        continue;

                    definitions.Add(new LLMToolDefinition(
                        descriptor.FleetToolName,
                        descriptor.Description,
                        descriptor.ParametersJsonSchema));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Skipping MCP server {ServerName} for user {UserId} because the connection could not be established.",
                    config.Name,
                    userId);
            }
        }

        if (toolsByName.Count == 0)
        {
            foreach (var connection in connections)
            {
                await connection.DisposeAsync();
            }

            return EmptyMcpToolSession.Instance;
        }

        return new McpToolSession(connections, definitions, toolsByName);
    }

    internal static IReadOnlyList<McpServerRuntimeConfig> MergeConfigs(
        IReadOnlyList<McpServerRuntimeConfig> systemConfigs,
        IReadOnlyList<McpServerRuntimeConfig> userConfigs)
    {
        var merged = new List<McpServerRuntimeConfig>(systemConfigs.Count + userConfigs.Count);
        var indicesByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var config in systemConfigs)
        {
            if (indicesByName.ContainsKey(config.Name))
                continue;

            indicesByName[config.Name] = merged.Count;
            merged.Add(config);
        }

        foreach (var config in userConfigs)
        {
            if (indicesByName.TryGetValue(config.Name, out var existingIndex))
            {
                merged[existingIndex] = config;
                continue;
            }

            indicesByName[config.Name] = merged.Count;
            merged.Add(config);
        }

        return merged;
    }

    private sealed class McpToolSession(
        IReadOnlyList<McpRuntimeConnection> connections,
        IReadOnlyList<LLMToolDefinition> definitions,
        IReadOnlyDictionary<string, McpRuntimeToolDescriptor> toolsByName) : IMcpToolSession
    {
        public IReadOnlyList<LLMToolDefinition> Definitions { get; } = definitions;

        public bool HasTool(string toolName) => toolsByName.ContainsKey(toolName);

        public bool IsReadOnly(string toolName)
            => toolsByName.TryGetValue(toolName, out var descriptor) && descriptor.IsReadOnly;

        public async Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
        {
            if (!toolsByName.TryGetValue(toolName, out var descriptor))
                return $"Error: unknown MCP tool '{toolName}'.";

            try
            {
                var arguments = ParseArguments(argumentsJson);
                var result = await descriptor.ClientTool.CallAsync(arguments, cancellationToken: cancellationToken);
                return FormatResult(result);
            }
            catch (Exception ex)
            {
                return $"Error executing MCP tool '{toolName}': {ex.Message}";
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var connection in connections)
            {
                await connection.DisposeAsync();
            }
        }

        private static IReadOnlyDictionary<string, object?> ParseArguments(string argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            return JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson)
                   ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        private static string FormatResult(CallToolResult result)
        {
            var sections = new List<string>();

            if (result.Content is { Count: > 0 })
            {
                foreach (var block in result.Content)
                {
                    var formatted = block switch
                    {
                        TextContentBlock text => text.Text,
                        ImageContentBlock image => $"[Image output: {image.MimeType}]",
                        AudioContentBlock audio => $"[Audio output: {audio.MimeType}]",
                        EmbeddedResourceBlock resource => FormatResource(resource.Resource),
                        _ => block.ToString(),
                    };

                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        sections.Add(formatted.Trim());
                    }
                }
            }

            if (result.StructuredContent is not null)
            {
                sections.Add($"Structured content:\n{JsonSerializer.Serialize(result.StructuredContent.Value)}");
            }

            if (sections.Count == 0)
            {
                sections.Add(result.IsError == true
                    ? "The MCP server reported an error without any additional details."
                    : "The MCP server completed successfully without returning text content.");
            }

            var prefix = result.IsError == true ? "Tool reported an error:\n" : string.Empty;
            return prefix + string.Join("\n\n", sections);
        }

        private static string? FormatResource(ResourceContents? resource)
        {
            return resource switch
            {
                TextResourceContents text => text.Text,
                BlobResourceContents blob => $"[Binary resource: {blob.MimeType ?? "application/octet-stream"}]",
                _ => resource?.ToString(),
            };
        }
    }

    private sealed class EmptyMcpToolSession : IMcpToolSession
    {
        public static readonly EmptyMcpToolSession Instance = new();

        public IReadOnlyList<LLMToolDefinition> Definitions => [];

        public bool HasTool(string toolName) => false;

        public bool IsReadOnly(string toolName) => false;

        public Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
            => Task.FromResult($"Error: unknown MCP tool '{toolName}'.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
