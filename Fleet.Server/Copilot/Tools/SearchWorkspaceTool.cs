using System.Text.Json;
using Fleet.Server.Search;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Runs workspace search across projects/work items/chats.</summary>
public class SearchWorkspaceTool(ISearchService searchService) : IChatTool
{
    public string Name => "search_workspace";

    public string Description =>
        "Search across projects, work items, and chat content for the current user. " +
        "Use 'type' to narrow categories (projects/work-items/chats).";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Search query text."
                },
                "type": {
                    "type": "string",
                    "description": "Optional category filter: projects, project, work-items, work-item, chats, or chat."
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of results to return (default 25)."
                }
            },
            "required": ["query"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = ParseArgs(argumentsJson);
        if (string.IsNullOrWhiteSpace(args.Query))
            return "Error: 'query' is required.";

        var results = await searchService.SearchAsync(context.UserId, args.Query, args.Type);
        var limited = results.Take(args.Limit).Select(result => new
        {
            result.Type,
            result.Title,
            result.Description,
            result.Meta,
            result.ProjectSlug,
        }).ToList();

        return limited.Count == 0
            ? "No results found."
            : JsonSerializer.Serialize(limited, new JsonSerializerOptions { WriteIndented = true });
    }

    private static SearchWorkspaceArgs ParseArgs(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            var query = root.TryGetProperty("query", out var queryEl) ? queryEl.GetString() : null;
            var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            var limit = root.TryGetProperty("limit", out var limitEl) && limitEl.TryGetInt32(out var limitValue)
                ? limitValue
                : 25;
            return new SearchWorkspaceArgs(query, type, Math.Clamp(limit, 1, 100));
        }
        catch
        {
            return new SearchWorkspaceArgs(null, null, 25);
        }
    }

    private sealed record SearchWorkspaceArgs(string? Query, string? Type, int Limit);
}
