using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Creates a pull request on the GitHub repository for the agent's changes.
/// </summary>
public class CreatePullRequestTool(IHttpClientFactory httpClientFactory) : IAgentTool
{
    public string Name => "create_pull_request";

    public string Description =>
        "Create a pull request on the GitHub repository. " +
        "This commits and pushes all current changes, then creates a PR against the default branch. " +
        "Call this as the final step after all code changes are complete.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "title": {
                    "type": "string",
                    "description": "The PR title."
                },
                "body": {
                    "type": "string",
                    "description": "The PR description in Markdown format. Include a summary of changes."
                },
                "commit_message": {
                    "type": "string",
                    "description": "The commit message for the changes."
                }
            },
            "required": ["title", "body", "commit_message"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("title", out var titleProp) || string.IsNullOrWhiteSpace(titleProp.GetString()))
            return "Error: 'title' parameter is required.";

        if (!args.TryGetProperty("body", out var bodyProp))
            return "Error: 'body' parameter is required.";

        if (!args.TryGetProperty("commit_message", out var commitProp))
            return "Error: 'commit_message' parameter is required.";

        var title = titleProp.GetString()!;
        var body = bodyProp.GetString() ?? "";
        var commitMessage = commitProp.GetString() ?? title;

        try
        {
            // 1. Commit and push all changes
            await context.Sandbox.CommitAndPushAsync(
                commitMessage,
                authorName: "Fleet Agent",
                authorEmail: "agent@fleet.dev",
                cancellationToken);

            // 2. Get the default branch name
            var client = httpClientFactory.CreateClient("GitHub");
            var repoInfo = await FetchJsonAsync<RepoResponse>(client, context.AccessToken,
                $"https://api.github.com/repos/{context.RepoFullName}", cancellationToken);
            var baseBranch = repoInfo?.DefaultBranch ?? "main";

            // 3. Create the pull request
            var prPayload = new
            {
                title,
                body,
                head = context.Sandbox.BranchName,
                @base = baseBranch,
            };

            var prJson = JsonSerializer.Serialize(prPayload);
            using var prRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.github.com/repos/{context.RepoFullName}/pulls");
            prRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
            prRequest.Headers.UserAgent.ParseAdd("Fleet/1.0");
            prRequest.Content = new StringContent(prJson, Encoding.UTF8, "application/json");

            using var prResponse = await client.SendAsync(prRequest, cancellationToken);
            var prResponseBody = await prResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!prResponse.IsSuccessStatusCode)
                return $"Error creating pull request: {prResponse.StatusCode} — {prResponseBody}";

            var prResult = JsonSerializer.Deserialize<JsonElement>(prResponseBody);
            var prUrl = prResult.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : "unknown";
            var prNumber = prResult.TryGetProperty("number", out var numProp) ? numProp.GetInt32() : 0;

            return $"Pull request #{prNumber} created successfully: {prUrl}";
        }
        catch (Exception ex)
        {
            return $"Error creating pull request: {ex.Message}";
        }
    }

    private static async Task<T?> FetchJsonAsync<T>(HttpClient client, string token, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private record RepoResponse
    {
        public string? DefaultBranch { get; init; }
    }
}
