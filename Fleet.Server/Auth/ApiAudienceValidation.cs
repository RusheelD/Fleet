namespace Fleet.Server.Auth;

public static class ApiAudienceValidation
{
    private const string ApiSchemePrefix = "api://";

    public static string[] ResolveValidAudiences(string? clientId, string? configuredAudience)
    {
        var audiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddAudience(audiences, clientId);
        AddAudience(audiences, configuredAudience);

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            audiences.Add($"{ApiSchemePrefix}{clientId.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(configuredAudience) &&
            configuredAudience.Trim().StartsWith(ApiSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            audiences.Add(configuredAudience.Trim()[ApiSchemePrefix.Length..]);
        }

        return [.. audiences];
    }

    private static void AddAudience(ISet<string> audiences, string? value)
    {
        var normalized = value?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            audiences.Add(normalized);
        }
    }
}
