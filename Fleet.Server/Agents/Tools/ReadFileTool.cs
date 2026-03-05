using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Reads the content of a single file from the local repo clone.
/// </summary>
public class ReadFileTool : IAgentTool
{
    public string Name => "read_file";
    public bool IsReadOnly => true;

    public string Description =>
        "Read the content of a file from the repository. " +
        "Provide the file path relative to the repo root (e.g., 'src/App.tsx'). " +
        "Files up to 1 MB are supported.";

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
            var content = context.Sandbox.ReadFile(filePath);
            var result = new { path = filePath, content, length = content.Length };
            return Task.FromResult(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult($"File not found: {filePath}");
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
