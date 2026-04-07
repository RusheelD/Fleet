namespace Fleet.Server.Memories;

public static class MemoryEntryTypes
{
    public const string User = "user";
    public const string Feedback = "feedback";
    public const string Project = "project";
    public const string Reference = "reference";

    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        User,
        Feedback,
        Project,
        Reference,
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && ValidTypes.Contains(value.Trim());

    public static string Normalize(string value)
        => value.Trim().ToLowerInvariant();

    public static IReadOnlyList<string> All => [User, Feedback, Project, Reference];
}
