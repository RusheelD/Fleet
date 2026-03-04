using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Deletes a file from the local repo clone.
/// </summary>
public class DeleteFileTool : IAgentTool
{
    public string Name => "delete_file";

    public string Description =>
        "Delete a file from the repository. " +
        "Provide the file path relative to the repo root.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "File path relative to the repo root."
                }
            },
            "required": ["path"]
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("path", out var pathProp) || string.IsNullOrWhiteSpace(pathProp.GetString()))
            return Task.FromResult("Error: 'path' parameter is required.");

        var filePath = pathProp.GetString()!;

        try
        {
            var deleted = context.Sandbox.DeleteFile(filePath);
            return Task.FromResult(deleted
                ? $"Successfully deleted '{filePath}'."
                : $"File not found: {filePath}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
