using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fleet.Server.Connections;
using Fleet.Server.Logging;

namespace Fleet.Server.GitHub;

public class GitHubApiService(
    IConnectionRepository connectionRepository,
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubApiService> logger) : IGitHubApiService
{
    private const string GitHubApiBase = "https://api.github.com";

    public async Task<GitHubRepoStats> GetRepoStatsAsync(int userId, string repoFullName)
    {
        var account = await connectionRepository.GetByProviderAsync(userId, "GitHub");
        if (account is null || string.IsNullOrEmpty(account.AccessToken))
        {
            logger.GitHubNoToken(userId);
            return new GitHubRepoStats(0, 0, 0, []);
        }

        var client = httpClientFactory.CreateClient("GitHub");

        try
        {
            var (openPrs, mergedPrs) = await FetchPullRequestStatsAsync(client, account.AccessToken, repoFullName);
            var recentCommits = await FetchRecentCommitCountAsync(client, account.AccessToken, repoFullName);
            var events = await FetchRecentEventsAsync(client, account.AccessToken, repoFullName);

            return new GitHubRepoStats(openPrs, mergedPrs, recentCommits, events);
        }
        catch (HttpRequestException ex)
        {
            logger.GitHubApiFailed(ex, repoFullName.SanitizeForLogging());
            return new GitHubRepoStats(0, 0, 0, []);
        }
    }

    // ── Pull Requests ─────────────────────────────────────────

    private async Task<(int Open, int Merged)> FetchPullRequestStatsAsync(
        HttpClient client, string token, string repo)
    {
        // Open PRs
        var openPrs = await FetchPullRequestsAsync(client, token, repo, "open");

        // Recently merged PRs (closed PRs that have a merge commit)
        var closedPrs = await FetchPullRequestsAsync(client, token, repo, "closed");
        var mergedCount = closedPrs.Count(pr => pr.MergedAt is not null);

        return (openPrs.Count, mergedCount);
    }

    private async Task<List<GitHubPullRequest>> FetchPullRequestsAsync(
        HttpClient client, string token, string repo, string state)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{GitHubApiBase}/repos/{repo}/pulls?state={state}&per_page=100&sort=updated&direction=desc");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<GitHubPullRequest>>() ?? [];
    }

    // ── Commits ───────────────────────────────────────────────

    private async Task<int> FetchRecentCommitCountAsync(
        HttpClient client, string token, string repo)
    {
        // Commits in the last 30 days
        var since = DateTimeOffset.UtcNow.AddDays(-30).ToString("o");

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{GitHubApiBase}/repos/{repo}/commits?since={since}&per_page=100");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var commits = await response.Content.ReadFromJsonAsync<List<JsonElement>>() ?? [];
        return commits.Count;
    }

    // ── Recent Events ─────────────────────────────────────────

    private async Task<IReadOnlyList<GitHubActivityEvent>> FetchRecentEventsAsync(
        HttpClient client, string token, string repo)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{GitHubApiBase}/repos/{repo}/events?per_page=30");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var events = await response.Content.ReadFromJsonAsync<List<GitHubEvent>>() ?? [];

        var result = new List<GitHubActivityEvent>();
        foreach (var evt in events.Take(10))
        {
            var mapped = MapEvent(evt);
            if (mapped is not null)
                result.Add(mapped);

            if (result.Count >= 5) break;
        }

        return result;
    }

    private static GitHubActivityEvent? MapEvent(GitHubEvent evt)
    {
        var actor = evt.Actor?.Login ?? "Someone";
        var timestamp = evt.CreatedAt;

        return evt.Type switch
        {
            "PushEvent" =>
                new GitHubActivityEvent("commit",
                    $"{actor} pushed {evt.Payload?.Commits?.Count ?? 0} commit(s) to {evt.Payload?.Ref?.Replace("refs/heads/", "") ?? "branch"}",
                    timestamp),

            "PullRequestEvent" =>
                new GitHubActivityEvent("branch",
                    $"{actor} {evt.Payload?.Action ?? "updated"} PR #{evt.Payload?.PullRequest?.Number}: \"{evt.Payload?.PullRequest?.Title}\"",
                    timestamp),

            "IssuesEvent" =>
                new GitHubActivityEvent("board",
                    $"{actor} {evt.Payload?.Action ?? "updated"} issue #{evt.Payload?.Issue?.Number}: \"{evt.Payload?.Issue?.Title}\"",
                    timestamp),

            "CreateEvent" =>
                new GitHubActivityEvent("branch",
                    $"{actor} created {evt.Payload?.RefType} {evt.Payload?.Ref ?? ""}",
                    timestamp),

            "DeleteEvent" =>
                new GitHubActivityEvent("branch",
                    $"{actor} deleted {evt.Payload?.RefType} {evt.Payload?.Ref ?? ""}",
                    timestamp),

            "IssueCommentEvent" =>
                new GitHubActivityEvent("chat",
                    $"{actor} commented on #{evt.Payload?.Issue?.Number}: \"{evt.Payload?.Issue?.Title}\"",
                    timestamp),

            "PullRequestReviewEvent" =>
                new GitHubActivityEvent("checkmark",
                    $"{actor} reviewed PR #{evt.Payload?.PullRequest?.Number}: \"{evt.Payload?.PullRequest?.Title}\"",
                    timestamp),

            _ => null,
        };
    }

    // ── GitHub API response models ────────────────────────────

    private sealed class GitHubPullRequest
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("merged_at")]
        public DateTimeOffset? MergedAt { get; set; }
    }

    private sealed class GitHubEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("actor")]
        public GitHubActor? Actor { get; set; }

        [JsonPropertyName("payload")]
        public GitHubPayload? Payload { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class GitHubActor
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;
    }

    private sealed class GitHubPayload
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        [JsonPropertyName("ref_type")]
        public string? RefType { get; set; }

        [JsonPropertyName("pull_request")]
        public GitHubPayloadPullRequest? PullRequest { get; set; }

        [JsonPropertyName("issue")]
        public GitHubPayloadIssue? Issue { get; set; }

        [JsonPropertyName("commits")]
        public List<JsonElement>? Commits { get; set; }
    }

    private sealed class GitHubPayloadPullRequest
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
    }

    private sealed class GitHubPayloadIssue
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
    }
}
