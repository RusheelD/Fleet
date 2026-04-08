using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Fleet.Server.Connections;
using Fleet.Server.Logging;

namespace Fleet.Server.GitHub;

public class GitHubApiService(
    IConnectionService connectionService,
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubApiService> logger) : IGitHubApiService
{
    private const string GitHubApiBase = "https://api.github.com";
    private static readonly Regex WorkItemReferenceRegex = new(@"F#(?<number>\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PullRequestUrlRegex = new(
        @"^/(?<owner>[^/]+)/(?<repo>[^/]+)/pull/(?<number>\d+)(?:/.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WordRegex = new(@"[A-Za-z]+", RegexOptions.Compiled);
    private static readonly HashSet<string> FixKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "fix",
        "fixes",
        "fixed",
    };

    public async Task<GitHubRepoStats> GetRepoStatsAsync(int userId, string repoFullName)
    {
        var accessToken = await ResolveAccessTokenForRepoAsync(userId, repoFullName);
        if (string.IsNullOrEmpty(accessToken))
        {
            logger.GitHubNoToken(userId);
            return new GitHubRepoStats(0, 0, 0, []);
        }

        var client = httpClientFactory.CreateClient("GitHub");

        try
        {
            // Run all three independent GitHub API calls in parallel
            var prStatsTask = FetchPullRequestStatsAsync(client, accessToken, repoFullName);
            var commitsTask = FetchRecentCommitCountAsync(client, accessToken, repoFullName);
            var eventsTask = FetchRecentEventsAsync(client, accessToken, repoFullName);
            await Task.WhenAll(prStatsTask, commitsTask, eventsTask);

            var (openPrs, mergedPrs) = await prStatsTask;
            var recentCommits = await commitsTask;
            var events = await eventsTask;

            return new GitHubRepoStats(openPrs, mergedPrs, recentCommits, events);
        }
        catch (HttpRequestException ex)
        {
            logger.GitHubApiFailed(ex, repoFullName.SanitizeForLogging());
            return new GitHubRepoStats(0, 0, 0, []);
        }
    }

    public async Task<IReadOnlyList<GitHubWorkItemReference>> GetWorkItemReferencesAsync(int userId, string repoFullName)
    {
        var accessToken = await ResolveAccessTokenForRepoAsync(userId, repoFullName);
        if (string.IsNullOrEmpty(accessToken))
        {
            logger.GitHubNoToken(userId);
            return [];
        }

        var client = httpClientFactory.CreateClient("GitHub");

        try
        {
            // Fetch open and closed PRs in parallel
            var openPrsTask = FetchPullRequestsAsync(client, accessToken, repoFullName, "open");
            var closedPrsTask = FetchPullRequestsAsync(client, accessToken, repoFullName, "closed");
            await Task.WhenAll(openPrsTask, closedPrsTask);
            var openPrs = await openPrsTask;
            var closedPrs = await closedPrsTask;

            var allRefs = new List<GitHubWorkItemReference>();
            foreach (var pr in openPrs.Concat(closedPrs))
            {
                allRefs.AddRange(ParseWorkItemReferences(pr));
            }

            return MergeDuplicateReferences(allRefs);
        }
        catch (HttpRequestException ex)
        {
            logger.GitHubApiFailed(ex, repoFullName.SanitizeForLogging());
            return [];
        }
    }

    public async Task<GitHubPullRequestLifecycle?> GetPullRequestLifecycleByUrlAsync(int userId, string pullRequestUrl)
    {
        if (string.IsNullOrWhiteSpace(pullRequestUrl))
            return null;

        if (!TryParsePullRequestUrl(pullRequestUrl, out var repoFullName, out var pullRequestNumber))
            return null;

        var accessToken = await ResolveAccessTokenForRepoAsync(userId, repoFullName);
        if (string.IsNullOrEmpty(accessToken))
        {
            logger.GitHubNoToken(userId);
            return null;
        }

        var client = httpClientFactory.CreateClient("GitHub");

        try
        {
            var pr = await FetchPullRequestAsync(client, accessToken, repoFullName, pullRequestNumber);
            if (pr is null)
                return null;

            var normalizedPrUrl = string.IsNullOrWhiteSpace(pr.HtmlUrl)
                ? pullRequestUrl.Trim()
                : pr.HtmlUrl.Trim();

            return new GitHubPullRequestLifecycle(
                normalizedPrUrl,
                string.Equals(pr.State, "open", StringComparison.OrdinalIgnoreCase),
                pr.Draft,
                pr.MergedAt is not null);
        }
        catch (HttpRequestException ex)
        {
            logger.GitHubApiFailed(ex, repoFullName.SanitizeForLogging());
            return null;
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

    private async Task<GitHubPullRequest?> FetchPullRequestAsync(
        HttpClient client,
        string token,
        string repo,
        int pullRequestNumber)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{GitHubApiBase}/repos/{repo}/pulls/{pullRequestNumber}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        var response = await client.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubPullRequest>();
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

    private static IReadOnlyList<GitHubWorkItemReference> ParseWorkItemReferences(GitHubPullRequest pr)
    {
        var text = $"{pr.Title}\n{pr.Body}";
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var refs = new List<GitHubWorkItemReference>();

        foreach (Match match in WorkItemReferenceRegex.Matches(text))
        {
            if (!int.TryParse(match.Groups["number"].Value, out var workItemNumber))
                continue;

            var previousWord = GetPreviousWord(text, match.Index);
            var isFixReference = previousWord is not null && FixKeywords.Contains(previousWord);
            var url = string.IsNullOrWhiteSpace(pr.HtmlUrl) ? string.Empty : pr.HtmlUrl;

            refs.Add(new GitHubWorkItemReference(
                workItemNumber,
                url,
                pr.Title,
                isFixReference,
                pr.MergedAt is not null,
                pr.UpdatedAt,
                string.Equals(pr.State, "open", StringComparison.OrdinalIgnoreCase),
                pr.Draft));
        }

        return refs;
    }

    private static IReadOnlyList<GitHubWorkItemReference> MergeDuplicateReferences(
        IReadOnlyList<GitHubWorkItemReference> references)
    {
        if (references.Count == 0)
            return [];

        var merged = new Dictionary<(int WorkItemNumber, string PullRequestUrl), GitHubWorkItemReference>();
        foreach (var reference in references)
        {
            var key = (reference.WorkItemNumber, reference.PullRequestUrl);
            if (!merged.TryGetValue(key, out var existing))
            {
                merged[key] = reference;
                continue;
            }

            merged[key] = existing with
            {
                IsFixReference = existing.IsFixReference || reference.IsFixReference,
                IsMerged = existing.IsMerged || reference.IsMerged,
                IsOpen = existing.IsOpen || reference.IsOpen,
                IsDraft = existing.IsDraft || reference.IsDraft,
                UpdatedAt = existing.UpdatedAt >= reference.UpdatedAt ? existing.UpdatedAt : reference.UpdatedAt,
            };
        }

        return [.. merged.Values];
    }

    private static string? GetPreviousWord(string text, int index)
    {
        if (index <= 0)
            return null;

        var prefix = text[..index];
        var matches = WordRegex.Matches(prefix);
        if (matches.Count == 0)
            return null;

        return matches[matches.Count - 1].Value;
    }

    private static bool TryParsePullRequestUrl(string pullRequestUrl, out string repoFullName, out int pullRequestNumber)
    {
        repoFullName = string.Empty;
        pullRequestNumber = 0;

        if (!Uri.TryCreate(pullRequestUrl, UriKind.Absolute, out var uri))
            return false;

        if (!uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var match = PullRequestUrlRegex.Match(uri.AbsolutePath);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups["number"].Value, out pullRequestNumber))
            return false;

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value;
        repoFullName = $"{owner}/{repo}";
        return true;
    }

    private async Task<string?> ResolveAccessTokenForRepoAsync(int userId, string repoFullName)
    {
        return await connectionService.ResolveGitHubAccessTokenForRepoAsync(userId, repoFullName);
    }

    // ── GitHub API response models ────────────────────────────

    private sealed class GitHubPullRequest
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("merged_at")]
        public DateTimeOffset? MergedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }
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
