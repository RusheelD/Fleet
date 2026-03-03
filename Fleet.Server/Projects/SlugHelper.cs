using System.Text.RegularExpressions;

namespace Fleet.Server.Projects;

public static partial class SlugHelper
{
    /// <summary>
    /// Converts a title into a URL-safe slug.
    /// Example: "E-Commerce API" → "e-commerce-api"
    /// </summary>
    public static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant().Trim();
        slug = WhitespaceRegex().Replace(slug, "-");
        slug = NonSlugCharRegex().Replace(slug, "");
        slug = MultiDashRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        return slug;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex NonSlugCharRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiDashRegex();
}
