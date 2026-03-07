using System.Text.Json;
using Fleet.Server.Connections;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Lists GitHub repositories for the current user in global chat scope.</summary>
public class ListGitHubReposTool(IConnectionService connectionService) : IChatTool
{
    public string Name => "list_github_repos";

    public string Description =>
        "List GitHub repositories linked to the current user. Only available when no project is open (global chat scope).";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of repositories to return (default 100, max 250)."
                }
            },
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (context.IsProjectScoped)
            return "This tool is only available in global chat scope when no project is open.";

        if (!int.TryParse(context.UserId, out var userId))
            return "Error: invalid user ID.";

        int limit;
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
            limit = args.TryGetProperty("limit", out var limitEl) && limitEl.TryGetInt32(out var value)
                ? value
                : 100;
        }
        catch
        {
            limit = 100;
        }

        limit = Math.Clamp(limit, 1, 250);

        try
        {
            var repos = await connectionService.GetGitHubRepositoriesAsync(userId);
            var result = repos
                .Take(limit)
                .Select(repo => new
                {
                    repo.FullName,
                    repo.Name,
                    repo.Owner,
                    repo.Description,
                    repo.Private,
                    Url = repo.HtmlUrl,
                });

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }
}
