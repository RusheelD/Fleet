using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Creates or overwrites a file in the local repo clone.
/// </summary>
public class WriteFileTool : IAgentTool
{
    public string Name => "write_file";

    public string Description =>
        "Create a new file or overwrite an existing file in the repository. " +
        "Provide the file path relative to the repo root and the full content. " +
        "Directories are created automatically.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "File path relative to the repo root."
                },
                "content": {
                    "type": "string",
                    "description": "The complete content to write to the file."
                }
            },
            "required": ["path", "content"]
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("path", out var pathProp) || string.IsNullOrWhiteSpace(pathProp.GetString()))
            return Task.FromResult("Error: 'path' parameter is required.");

        if (!args.TryGetProperty("content", out var contentProp))
            return Task.FromResult("Error: 'content' parameter is required.");

        var filePath = pathProp.GetString()!;
        var content = contentProp.GetString() ?? "";

        try
        {
            context.Sandbox.WriteFile(filePath, content);
            return Task.FromResult($"Successfully wrote {content.Length:N0} characters to '{filePath}'.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
