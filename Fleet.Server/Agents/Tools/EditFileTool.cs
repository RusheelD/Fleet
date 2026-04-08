using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Applies a targeted edit to an existing file by replacing a specific string.
/// </summary>
public class EditFileTool(FileReadTracker fileReadTracker) : IAgentTool
{
    public string Name => "edit_file";

    public string Description =>
        "Edit an existing file by replacing one occurrence of an exact string with new content. " +
        "Include enough surrounding context in 'old_string' to uniquely identify the location. " +
        "For creating new files, use the write_file tool instead.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "File path relative to the repo root."
                },
                "old_string": {
                    "type": "string",
                    "description": "The exact text to find and replace. Must match exactly, including whitespace and indentation."
                },
                "new_string": {
                    "type": "string",
                    "description": "The replacement text."
                }
            },
            "required": ["path", "old_string", "new_string"]
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("path", out var pathProp) || string.IsNullOrWhiteSpace(pathProp.GetString()))
            return Task.FromResult("Error: 'path' parameter is required.");

        if (!args.TryGetProperty("old_string", out var oldProp))
            return Task.FromResult("Error: 'old_string' parameter is required.");

        if (!args.TryGetProperty("new_string", out var newProp))
            return Task.FromResult("Error: 'new_string' parameter is required.");

        var filePath = pathProp.GetString()!;
        var oldString = oldProp.GetString() ?? "";
        var newString = newProp.GetString() ?? "";

        try
        {
            var content = context.Sandbox.ReadFile(filePath);

            // Staleness check: reject if the file changed since the model last read it
            var staleError = fileReadTracker.CheckFreshness(filePath, content);
            if (staleError is not null)
                return Task.FromResult(staleError);

            var index = content.IndexOf(oldString, StringComparison.Ordinal);

            if (index < 0)
                return Task.FromResult($"Error: could not find the specified text in '{filePath}'. Make sure 'old_string' matches exactly, including whitespace.");

            // Check for multiple occurrences
            var secondIndex = content.IndexOf(oldString, index + oldString.Length, StringComparison.Ordinal);
            if (secondIndex >= 0)
                return Task.FromResult($"Error: 'old_string' matches multiple locations in '{filePath}'. Include more surrounding context to make it unique.");

            var newContent = content[..index] + newString + content[(index + oldString.Length)..];
            context.Sandbox.WriteFile(filePath, newContent);
            fileReadTracker.RecordWrite(filePath, newContent);

            return Task.FromResult($"Successfully edited '{filePath}'. Replaced {oldString.Length} characters with {newString.Length} characters.");
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult($"File not found: {filePath}. Use write_file to create new files.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
