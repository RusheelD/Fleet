using System.Text;

namespace Fleet.Server.Logging;

public static class LoggerExtensions
{
    private const int MaxLogFieldLength = 200;

    public static string SanitizeForLogging(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        var normalized = new string(trimmed.Where(ch => !char.IsControl(ch) || ch == '\n' || ch == '\r' || ch == '\t').ToArray());

        if (normalized.Length <= MaxLogFieldLength)
            return normalized;

        return normalized[..MaxLogFieldLength] + "...";
    }
}
