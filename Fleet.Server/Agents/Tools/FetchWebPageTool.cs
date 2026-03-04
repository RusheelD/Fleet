using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Fetches the content of a web page by URL.
/// Useful for reading documentation, API references, and library guides.
/// </summary>
public class FetchWebPageTool(IHttpClientFactory httpClientFactory) : IAgentTool
{
    /// <summary>Max response body size (256 KB).</summary>
    private const int MaxResponseSize = 256 * 1024;

    public string Name => "fetch_web_page";

    public string Description =>
        "Fetch the text content of a web page by URL. " +
        "Use this to read documentation, API references, or library guides. " +
        "Returns the raw text content (HTML tags stripped where possible). " +
        "Only HTTP(S) URLs are supported.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "The URL to fetch (must start with http:// or https://)."
                }
            },
            "required": ["url"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("url", out var urlProp) || string.IsNullOrWhiteSpace(urlProp.GetString()))
            return "Error: 'url' parameter is required.";

        var url = urlProp.GetString()!;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "Error: only HTTP and HTTPS URLs are supported.";

        try
        {
            var client = httpClientFactory.CreateClient("GitHub"); // Reuse configured client
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Fleet/1.0");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            using var response = await client.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cts.Token);

            if (body.Length > MaxResponseSize)
                body = body[..MaxResponseSize] + $"\n\n[Truncated at {MaxResponseSize:N0} characters]";

            // Basic HTML tag stripping for readability
            body = StripHtmlTags(body);

            var result = new
            {
                url,
                statusCode = (int)response.StatusCode,
                contentLength = body.Length,
                content = body,
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (OperationCanceledException)
        {
            return $"Error: request to '{url}' timed out after 30 seconds.";
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching '{url}': {ex.Message}";
        }
    }

    private static string StripHtmlTags(string input)
    {
        // Simple tag stripping — not a full HTML parser, but good enough for extracting readable text
        if (!input.Contains('<')) return input;

        // Remove script and style blocks entirely
        input = System.Text.RegularExpressions.Regex.Replace(input,
            @"<(script|style)[^>]*>[\s\S]*?</\1>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Replace block-level tags with newlines
        input = System.Text.RegularExpressions.Regex.Replace(input,
            @"<(br|p|div|h[1-6]|li|tr)[^>]*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove remaining tags
        input = System.Text.RegularExpressions.Regex.Replace(input, @"<[^>]+>", "");

        // Decode common entities
        input = input.Replace("&amp;", "&")
                     .Replace("&lt;", "<")
                     .Replace("&gt;", ">")
                     .Replace("&quot;", "\"")
                     .Replace("&#39;", "'")
                     .Replace("&nbsp;", " ");

        // Collapse multiple blank lines
        input = System.Text.RegularExpressions.Regex.Replace(input, @"\n{3,}", "\n\n");

        return input.Trim();
    }
}
