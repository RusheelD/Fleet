using System.Text.Json;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;

namespace Fleet.Server.Mcp;

public class McpServerService(
    IMcpServerRepository repository,
    IMcpSecretProtector secretProtector,
    IMcpRuntimeConnector runtimeConnector,
    ILogger<McpServerService> logger) : IMcpServerService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<McpServerDto>> GetServersAsync(int userId)
    {
        var servers = await repository.GetAllAsync(userId);
        return servers.Select(MapToDto).ToList();
    }

    public Task<IReadOnlyList<McpServerTemplateDto>> GetBuiltInTemplatesAsync()
        => Task.FromResult<IReadOnlyList<McpServerTemplateDto>>(BuiltInTemplates);

    public async Task<McpServerDto> CreateAsync(int userId, UpsertMcpServerRequest request)
    {
        var normalized = NormalizeRequest(request);
        await EnsureNameAvailableAsync(userId, normalized.Name, null);

        var entity = new McpServerConnection
        {
            UserProfileId = userId,
            Name = normalized.Name,
            Description = normalized.Description,
            TransportType = normalized.TransportType,
            Command = normalized.Command,
            ArgumentsJson = JsonSerializer.Serialize(normalized.Arguments, JsonOptions),
            WorkingDirectory = normalized.WorkingDirectory,
            Endpoint = normalized.Endpoint,
            ProtectedEnvironmentVariables = ProtectVariables(normalized.EnvironmentVariables),
            ProtectedHeaders = ProtectVariables(normalized.Headers),
            BuiltInTemplateKey = normalized.BuiltInTemplateKey,
            Enabled = normalized.Enabled,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        var created = await repository.CreateAsync(entity);
        logger.LogInformation("Created MCP server {ServerName} for user {UserId}", created.Name, userId);
        return MapToDto(created);
    }

    public async Task<McpServerDto> UpdateAsync(int userId, int id, UpsertMcpServerRequest request)
    {
        var existing = await repository.GetByIdAsync(userId, id)
            ?? throw new InvalidOperationException("MCP server not found.");

        var normalized = NormalizeRequest(request);
        await EnsureNameAvailableAsync(userId, normalized.Name, id);

        existing.Name = normalized.Name;
        existing.Description = normalized.Description;
        existing.TransportType = normalized.TransportType;
        existing.Command = normalized.Command;
        existing.ArgumentsJson = JsonSerializer.Serialize(normalized.Arguments, JsonOptions);
        existing.WorkingDirectory = normalized.WorkingDirectory;
        existing.Endpoint = normalized.Endpoint;
        existing.ProtectedEnvironmentVariables = ProtectVariables(MergeVariables(
            DeserializeVariables(secretProtector.Unprotect(existing.ProtectedEnvironmentVariables)),
            normalized.EnvironmentVariables));
        existing.ProtectedHeaders = ProtectVariables(MergeVariables(
            DeserializeVariables(secretProtector.Unprotect(existing.ProtectedHeaders)),
            normalized.Headers));
        existing.BuiltInTemplateKey = normalized.BuiltInTemplateKey;
        existing.Enabled = normalized.Enabled;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpdateAsync(existing);
        logger.LogInformation("Updated MCP server {ServerId} ({ServerName}) for user {UserId}", existing.Id, existing.Name, userId);
        return MapToDto(existing);
    }

    public async Task DeleteAsync(int userId, int id)
    {
        var existing = await repository.GetByIdAsync(userId, id)
            ?? throw new InvalidOperationException("MCP server not found.");

        await repository.DeleteAsync(existing);
        logger.LogInformation("Deleted MCP server {ServerId} ({ServerName}) for user {UserId}", existing.Id, existing.Name, userId);
    }

    public async Task<McpServerValidationResultDto> ValidateAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdAsync(userId, id)
            ?? throw new InvalidOperationException("MCP server not found.");

        try
        {
            var runtimeConfig = ToRuntimeConfig(existing);
            await using var connection = await runtimeConnector.ConnectAsync(runtimeConfig, cancellationToken);
            var toolNames = connection.Tools
                .Select(tool => tool.ToolName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            existing.LastValidatedAtUtc = DateTime.UtcNow;
            existing.LastValidationError = null;
            existing.LastToolCount = toolNames.Length;
            existing.DiscoveredToolsJson = JsonSerializer.Serialize(toolNames, JsonOptions);
            existing.UpdatedAtUtc = DateTime.UtcNow;

            await repository.UpdateAsync(existing);
            return new McpServerValidationResultDto(true, null, toolNames.Length, toolNames);
        }
        catch (Exception ex)
        {
            existing.LastValidatedAtUtc = DateTime.UtcNow;
            existing.LastValidationError = ex.Message;
            existing.LastToolCount = 0;
            existing.DiscoveredToolsJson = "[]";
            existing.UpdatedAtUtc = DateTime.UtcNow;
            await repository.UpdateAsync(existing);

            logger.LogWarning(ex, "Failed to validate MCP server {ServerId} for user {UserId}", existing.Id, userId);
            return new McpServerValidationResultDto(false, ex.Message, 0, []);
        }
    }

    public async Task<IReadOnlyList<McpServerRuntimeConfig>> GetEnabledRuntimeConfigsAsync(int userId)
    {
        var servers = await repository.GetEnabledAsync(userId);
        return servers.Select(ToRuntimeConfig).ToList();
    }

    private static readonly IReadOnlyList<McpServerTemplateDto> BuiltInTemplates =
    [
        new(
            Key: "playwright",
            Name: "Playwright Browser",
            Description: "Browse pages, click around, capture snapshots, and inspect network activity through the official Playwright MCP server.",
            TransportType: "stdio",
            Command: "npx",
            Arguments: ["-y", "@playwright/mcp@latest"],
            WorkingDirectory: null,
            Endpoint: null,
            EnvironmentVariables: [],
            Headers: [],
            Notes:
            [
                "Requires Node.js and npm or npx on the Fleet host.",
                "Useful for QA, PM validation, demos, and UI debugging."
            ]),
        new(
            Key: "github",
            Name: "GitHub",
            Description: "Use the official GitHub MCP server to work with repositories, issues, pull requests, and review workflows.",
            TransportType: "stdio",
            Command: "docker",
            Arguments: ["run", "-i", "--rm", "-e", "GITHUB_PERSONAL_ACCESS_TOKEN", "ghcr.io/github/github-mcp-server"],
            WorkingDirectory: null,
            Endpoint: null,
            EnvironmentVariables:
            [
                new McpServerTemplateFieldDto(
                    "GITHUB_PERSONAL_ACCESS_TOKEN",
                    "Personal access token used by the GitHub MCP server.",
                    IsSecret: true,
                    Required: true)
            ],
            Headers: [],
            Notes:
            [
                "Requires Docker on the Fleet host.",
                "The GitHub image is the simplest official option for a built-in GitHub server."
            ]),
        new(
            Key: "filesystem",
            Name: "Filesystem",
            Description: "Expose extra filesystem roots to Fleet via the reference filesystem MCP server.",
            TransportType: "stdio",
            Command: "npx",
            Arguments: ["-y", "@modelcontextprotocol/server-filesystem", "."],
            WorkingDirectory: null,
            Endpoint: null,
            EnvironmentVariables: [],
            Headers: [],
            Notes:
            [
                "Update the trailing path argument to point at the folder you want the server to expose.",
                "Useful for shared docs, assets, or repo-adjacent folders outside Fleet's normal sandbox."
            ]),
        new(
            Key: "fetch",
            Name: "Fetch",
            Description: "Fetch and summarize web pages through the reference fetch MCP server.",
            TransportType: "stdio",
            Command: "npx",
            Arguments: ["-y", "@modelcontextprotocol/server-fetch"],
            WorkingDirectory: null,
            Endpoint: null,
            EnvironmentVariables: [],
            Headers: [],
            Notes:
            [
                "Requires Node.js and npm or npx on the Fleet host.",
                "Helpful for lightweight research and docs lookup without wiring a full browser server."
            ])
    ];

    private async Task EnsureNameAvailableAsync(int userId, string name, int? excludingId)
    {
        if (await repository.NameExistsAsync(userId, name, excludingId))
            throw new InvalidOperationException($"An MCP server named '{name}' already exists.");
    }

    private McpServerDto MapToDto(McpServerConnection entity)
    {
        var environmentVariables = DeserializeVariables(secretProtector.Unprotect(entity.ProtectedEnvironmentVariables))
            .Select(variable => new McpServerVariableDto(
                variable.Name,
                variable.IsSecret ? null : variable.Value,
                variable.IsSecret,
                !string.IsNullOrWhiteSpace(variable.Value)))
            .ToArray();

        var headers = DeserializeVariables(secretProtector.Unprotect(entity.ProtectedHeaders))
            .Select(variable => new McpServerVariableDto(
                variable.Name,
                variable.IsSecret ? null : variable.Value,
                variable.IsSecret,
                !string.IsNullOrWhiteSpace(variable.Value)))
            .ToArray();

        var arguments = DeserializeStringArray(entity.ArgumentsJson);
        var discoveredTools = DeserializeStringArray(entity.DiscoveredToolsJson);

        return new McpServerDto(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.TransportType,
            entity.Command,
            arguments,
            entity.WorkingDirectory,
            entity.Endpoint,
            entity.BuiltInTemplateKey,
            entity.Enabled,
            environmentVariables,
            headers,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.LastValidatedAtUtc,
            entity.LastValidationError,
            entity.LastToolCount,
            discoveredTools);
    }

    private McpServerRuntimeConfig ToRuntimeConfig(McpServerConnection entity)
    {
        var environmentVariables = DeserializeVariables(secretProtector.Unprotect(entity.ProtectedEnvironmentVariables))
            .Where(variable => !string.IsNullOrWhiteSpace(variable.Name))
            .ToDictionary(variable => variable.Name, variable => variable.Value, StringComparer.OrdinalIgnoreCase);

        var headers = DeserializeVariables(secretProtector.Unprotect(entity.ProtectedHeaders))
            .Where(variable => !string.IsNullOrWhiteSpace(variable.Name) && !string.IsNullOrWhiteSpace(variable.Value))
            .ToDictionary(variable => variable.Name, variable => variable.Value, StringComparer.OrdinalIgnoreCase);

        return new McpServerRuntimeConfig(
            entity.Id,
            entity.Name,
            entity.TransportType,
            entity.Command,
            DeserializeStringArray(entity.ArgumentsJson),
            entity.WorkingDirectory,
            entity.Endpoint,
            environmentVariables,
            headers);
    }

    private NormalizedRequest NormalizeRequest(UpsertMcpServerRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Server name is required.");

        var transportType = (request.TransportType ?? string.Empty).Trim().ToLowerInvariant();
        if (transportType is not ("stdio" or "http"))
            throw new InvalidOperationException("Transport type must be either 'stdio' or 'http'.");

        var command = NormalizeOptional(request.Command);
        var endpoint = NormalizeOptional(request.Endpoint);
        if (transportType == "stdio" && string.IsNullOrWhiteSpace(command))
            throw new InvalidOperationException("A command is required for stdio MCP servers.");
        if (transportType == "http")
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new InvalidOperationException("An endpoint is required for HTTP MCP servers.");
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsedUri) ||
                (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("HTTP MCP server endpoints must be absolute http or https URLs.");
            }
        }

        return new NormalizedRequest(
            name,
            NormalizeOptional(request.Description) ?? string.Empty,
            transportType,
            command,
            (request.Arguments ?? []).Select(arg => arg ?? string.Empty).ToArray(),
            NormalizeOptional(request.WorkingDirectory),
            endpoint,
            NormalizeOptional(request.BuiltInTemplateKey),
            request.Enabled,
            NormalizeVariables(request.EnvironmentVariables),
            NormalizeVariables(request.Headers));
    }

    private string ProtectVariables(IReadOnlyList<StoredVariable> variables)
    {
        var json = JsonSerializer.Serialize(variables, JsonOptions);
        return secretProtector.Protect(json);
    }

    private static IReadOnlyList<StoredVariable> DeserializeVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<StoredVariable>>(json, JsonOptions) ?? [];
    }

    private static string[] DeserializeStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
    }

    private static IReadOnlyList<StoredVariable> NormalizeVariables(McpServerVariableInput[]? variables)
    {
        if (variables is null || variables.Length == 0)
            return [];

        var normalized = new List<StoredVariable>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in variables)
        {
            var name = (variable.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                continue;

            normalized.Add(new StoredVariable(
                name,
                variable.Value,
                variable.IsSecret,
                variable.PreserveExistingValue));
        }

        return normalized;
    }

    private static IReadOnlyList<StoredVariable> MergeVariables(
        IReadOnlyList<StoredVariable> existing,
        IReadOnlyList<StoredVariable> requested)
    {
        var existingByName = existing.ToDictionary(variable => variable.Name, StringComparer.OrdinalIgnoreCase);
        var merged = new List<StoredVariable>(requested.Count);

        foreach (var variable in requested)
        {
            if (variable.PreserveExistingValue &&
                existingByName.TryGetValue(variable.Name, out var existingValue) &&
                string.IsNullOrWhiteSpace(variable.Value))
            {
                merged.Add(existingValue with { IsSecret = variable.IsSecret });
                continue;
            }

            merged.Add(variable with { PreserveExistingValue = false });
        }

        return merged;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record StoredVariable(
        string Name,
        string? Value,
        bool IsSecret,
        bool PreserveExistingValue = false
    );

    private sealed record NormalizedRequest(
        string Name,
        string Description,
        string TransportType,
        string? Command,
        string[] Arguments,
        string? WorkingDirectory,
        string? Endpoint,
        string? BuiltInTemplateKey,
        bool Enabled,
        IReadOnlyList<StoredVariable> EnvironmentVariables,
        IReadOnlyList<StoredVariable> Headers
    );
}
