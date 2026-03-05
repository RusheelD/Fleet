using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Lists the directory tree of the local repo clone.
/// </summary>
public class ListDirectoryTool : IAgentTool
{
    public string Name => "list_directory";
    public bool IsReadOnly => true;

    public string Description =>
        "List files and directories at a given path in the repository. " +
        "Returns names, types (file/dir), and sizes. Use an empty path for the repo root.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "Directory path relative to the repo root. Empty string for root."
                }
            },
            "required": []
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        var path = args.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? "" : "";

        var entries = context.Sandbox.ListDirectory(path);

        if (entries.Count == 0)
            return Task.FromResult($"No entries found at '{path}'. The directory may not exist.");

        var result = new
        {
            path,
            entries = entries.Select(e => new { e.Name, e.Type, e.Size }).ToList(),
            totalEntries = entries.Count,
        };

        return Task.FromResult(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
}
