namespace Fleet.Server.Auth;

public static class UserRoles
{
    public const string Free = "free";
    public const string Basic = "basic";
    public const string Pro = "pro";
    public const string Unlimited = "unlimited";

    public static IReadOnlyList<string> All { get; } = [Free, Basic, Pro, Unlimited];

    public static bool IsUnlimited(string? role) =>
        string.Equals(Normalize(role), Unlimited, StringComparison.OrdinalIgnoreCase);

    public static bool IsValid(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var normalized = role.Trim().ToLowerInvariant();
        return All.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return Free;

        return role.Trim().ToLowerInvariant() switch
        {
            Free => Free,
            Basic => Basic,
            Pro => Pro,
            Unlimited => Unlimited,
            "member" => Free,
            "admin" => Unlimited,
            "owner" => Unlimited,
            _ => Free
        };
    }
}
