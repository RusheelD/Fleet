using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Agent tool that generates Mermaid diagram files and writes them to the
/// repository sandbox. Useful for auto-generating architecture and dependency
/// documentation during agent pipeline execution.
/// </summary>
public class GenerateMermaidTool : IAgentTool
{
    public string Name => "generate_mermaid";

    public string Description =>
        "Generate a Mermaid diagram file and write it to the repository. " +
        "Creates .md files containing Mermaid diagram markup for architecture docs, " +
        "dependency graphs, ER diagrams, sequence diagrams, and CI/CD flow visualisations. " +
        "Use this to produce visual documentation as part of the agent pipeline.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "File path relative to repo root. Defaults to 'docs/diagrams/<diagram_type>.md'."
                },
                "diagram_type": {
                    "type": "string",
                    "description": "Type of diagram being generated.",
                    "enum": ["architecture", "stack", "dependencies", "ci-cd", "entity-relationship", "sequence", "class"]
                },
                "content": {
                    "type": "string",
                    "description": "The full Mermaid diagram content (including mermaid code fences and any surrounding markdown)."
                },
                "title": {
                    "type": "string",
                    "description": "Optional title/heading for the diagram document."
                }
            },
            "required": ["diagram_type", "content"]
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        var diagramType = args.TryGetProperty("diagram_type", out var dtProp) ? dtProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(diagramType))
            return Task.FromResult("Error: 'diagram_type' parameter is required.");

        var content = args.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(content))
            return Task.FromResult("Error: 'content' parameter is required.");

        var title = args.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
        var path = args.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : null;

        path ??= $"docs/diagrams/{diagramType}.md";

        // If a title is provided and content doesn't start with a heading, prepend it
        if (!string.IsNullOrWhiteSpace(title) && !content.TrimStart().StartsWith('#'))
        {
            content = $"# {title}\n\n{content}";
        }

        try
        {
            context.Sandbox.WriteFile(path, content);
            return Task.FromResult($"Successfully wrote {diagramType} diagram to '{path}' ({content.Length:N0} characters).");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
