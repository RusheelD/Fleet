using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;

namespace Fleet.Server.Logging;

public static class LogSanitizer
{
    private static readonly string[] SensitiveFragments =
    [
        "token",
        "secret",
        "password",
        "api_key",
        "apikey",
        "authorization",
        "client_secret",
        "code"
    ];

    public static string SanitizeByKey(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return IsSensitiveKey(key) ? "***REDACTED***" : value;
    }

    public static string SanitizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var sanitizedQuery = SanitizeQueryString(uri.Query);
        var builder = new UriBuilder(uri) { Query = sanitizedQuery.TrimStart('?') };
        return builder.Uri.ToString();
    }

    public static string SanitizeQueryString(string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
            return string.Empty;

        var parsed = QueryHelpers.ParseQuery(queryString);
        var parts = new List<string>(parsed.Count);

        foreach (var kvp in parsed)
        {
            var key = kvp.Key;
            var values = kvp.Value;
            var sanitizedValues = values.Select(v => Uri.EscapeDataString(SanitizeByKey(key, v))).ToArray();
            parts.Add($"{Uri.EscapeDataString(key)}={string.Join(",", sanitizedValues)}");
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    public static Dictionary<string, object?> SanitizeRouteValues(RouteValueDictionary routeValues)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in routeValues)
        {
            var value = kvp.Value?.ToString();
            result[kvp.Key] = SanitizeByKey(kvp.Key, value);
        }

        return result;
    }

    public static string SanitizeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is null)
                return json;

            SanitizeNode(node, null);
            return node.ToJsonString();
        }
        catch
        {
            return json;
        }
    }

    private static void SanitizeNode(JsonNode node, string? currentKey)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var prop in obj.ToList())
                {
                    if (prop.Value is null)
                        continue;

                    if (prop.Value is JsonValue && IsSensitiveKey(prop.Key))
                    {
                        obj[prop.Key] = "***REDACTED***";
                        continue;
                    }

                    SanitizeNode(prop.Value, prop.Key);
                }
                break;

            case JsonArray arr:
                foreach (var item in arr)
                {
                    if (item is not null)
                        SanitizeNode(item, currentKey);
                }
                break;

            case JsonValue when currentKey is not null && IsSensitiveKey(currentKey):
                if (node.GetValue<object?>() is not null)
                {
                    var parent = node.Parent;
                    if (parent is JsonObject parentObj)
                    {
                        parentObj[currentKey] = "***REDACTED***";
                    }
                }
                break;
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return SensitiveFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
