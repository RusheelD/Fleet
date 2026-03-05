using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Searches for text or regex patterns across repository files.
/// </summary>
public class SearchFilesTool : IAgentTool
{
    public string Name => "search_files";
    public bool IsReadOnly => true;

    public string Description =>
        "Search the repository for files containing a text pattern or regex. " +
        "Returns matching file paths, line numbers, and line content. " +
        "Optionally filter by file glob (e.g., '*.cs', 'src/**/*.tsx').";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "pattern": {
                    "type": "string",
                    "description": "The search pattern (plain text or regex)."
                },
                "is_regex": {
                    "type": "boolean",
                    "description": "Whether the pattern is a regular expression. Default: false."
                },
                "file_glob": {
                    "type": "string",
                    "description": "Optional file glob filter (e.g., '*.cs', 'src/**/*.tsx')."
                },
                "max_results": {
                    "type": "integer",
                    "description": "Maximum number of results to return. Default: 50."
                }
            },
            "required": ["pattern"]
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("pattern", out var patternProp) || string.IsNullOrWhiteSpace(patternProp.GetString()))
            return Task.FromResult("Error: 'pattern' parameter is required.");

        var pattern = patternProp.GetString()!;
        var isRegex = args.TryGetProperty("is_regex", out var regexProp) && regexProp.GetBoolean();
        var fileGlob = args.TryGetProperty("file_glob", out var globProp) ? globProp.GetString() : null;
        var maxResults = args.TryGetProperty("max_results", out var maxProp) ? maxProp.GetInt32() : 50;

        try
        {
            var results = context.Sandbox.SearchFiles(pattern, isRegex, fileGlob, maxResults);

            if (results.Count == 0)
                return Task.FromResult($"No matches found for pattern '{pattern}'.");

            var output = new
            {
                pattern,
                matchCount = results.Count,
                matches = results.Select(r => new
                {
                    file = r.FilePath,
                    line = r.LineNumber,
                    content = r.LineContent.Length > 200 ? r.LineContent[..200] + "..." : r.LineContent,
                }).ToList(),
            };

            return Task.FromResult(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error searching files: {ex.Message}");
        }
    }
}
