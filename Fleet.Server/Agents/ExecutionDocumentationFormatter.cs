using System.Text;
using System.Text.RegularExpressions;

namespace Fleet.Server.Agents;

internal static partial class ExecutionDocumentationFormatter
{
    public static string NormalizeMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        return UnwrapMarkdownFences(markdown).Trim();
    }

    public static string FormatPhaseOutput(string output)
    {
        var normalizedOutput = NormalizeNewlines(output);
        if (string.IsNullOrWhiteSpace(normalizedOutput))
            return "```text\n(no output captured)\n```";

        var normalizedMarkdown = NormalizeMarkdown(normalizedOutput);
        if (LooksLikeMarkdownDocument(normalizedMarkdown))
            return normalizedMarkdown;

        return $"```text\n{normalizedOutput}\n```";
    }

    private static string NormalizeNewlines(string value)
        => value.Replace("\r\n", "\n").Trim();

    private static string UnwrapMarkdownFences(string markdown)
    {
        var normalized = NormalizeNewlines(markdown);
        var lines = normalized.Split('\n');
        var output = new StringBuilder();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var openFence = OpenFenceRegex().Match(line);
            if (!openFence.Success)
            {
                AppendLine(output, line);
                continue;
            }

            var fence = openFence.Groups["fence"].Value;
            var info = openFence.Groups["info"].Value;
            var blockLines = new List<string>();
            var closingIndex = index + 1;
            var depth = 1;
            while (closingIndex < lines.Length)
            {
                var candidateLine = lines[closingIndex];
                if (Regex.IsMatch(candidateLine, $"^\\s*{Regex.Escape(fence)}\\S.*$"))
                {
                    depth++;
                    blockLines.Add(candidateLine);
                    closingIndex++;
                    continue;
                }

                if (Regex.IsMatch(candidateLine, $"^\\s*{Regex.Escape(fence)}\\s*$"))
                {
                    depth--;
                    if (depth == 0)
                        break;

                    blockLines.Add(candidateLine);
                    closingIndex++;
                    continue;
                }

                blockLines.Add(candidateLine);
                closingIndex++;
            }

            if (closingIndex >= lines.Length)
            {
                AppendLine(output, line);
                continue;
            }

            var body = string.Join('\n', blockLines);
            if (ShouldUnwrapFence(info, body))
            {
                var unwrapped = UnwrapMarkdownFences(body);
                if (!string.IsNullOrEmpty(unwrapped))
                    AppendLine(output, unwrapped);
            }
            else
            {
                AppendLine(output, line);
                foreach (var blockLine in blockLines)
                    AppendLine(output, blockLine);
                AppendLine(output, lines[closingIndex]);
            }

            index = closingIndex;
        }

        return output.ToString().TrimEnd('\n');
    }

    private static bool ShouldUnwrapFence(string info, string body)
    {
        var language = info.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;
        if (language is "md" or "markdown" or "mdx")
            return true;

        if (!string.IsNullOrWhiteSpace(language) && language is not ("text" or "txt" or "plain" or "plaintext" or "output"))
            return false;

        return EmbeddedMarkdownFenceRegex().IsMatch(body) || LooksLikeMarkdownDocument(body);
    }

    internal static bool LooksLikeMarkdownDocument(string body)
    {
        var normalized = NormalizeNewlines(body);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var nonEmptyLines = normalized
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        if (nonEmptyLines.Count == 0)
            return false;

        var markdownSignals = 0;
        var proseSignals = 0;
        var codeSignals = 0;
        var treeSignals = 0;

        foreach (var line in nonEmptyLines)
        {
            if (MarkdownSignalRegex().IsMatch(line))
                markdownSignals++;

            if (TreeSignalRegex().IsMatch(line) || line.EndsWith('/') || IndentedTreeLineRegex().IsMatch(line))
                treeSignals++;

            if (CodeKeywordRegex().IsMatch(line) || CodeSymbolRegex().IsMatch(line))
                codeSignals++;

            if (LetterRegex().IsMatch(line) && !MarkdownLeadRegex().IsMatch(line))
                proseSignals++;
        }

        if (treeSignals > 0 && markdownSignals == 0)
            return false;

        if (codeSignals > 0 && markdownSignals == 0)
            return false;

        if (EmbeddedMarkdownFenceRegex().IsMatch(normalized))
            return true;

        return markdownSignals >= 2 || (markdownSignals >= 1 && proseSignals >= 1);
    }

    private static void AppendLine(StringBuilder builder, string value)
    {
        if (builder.Length > 0)
            builder.Append('\n');

        builder.Append(value);
    }

    [GeneratedRegex(@"^\s*(?<fence>`{3,})(?<info>[^`]*)\s*$")]
    private static partial Regex OpenFenceRegex();

    [GeneratedRegex(@"^\s*```(?:md|markdown|mdx)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex EmbeddedMarkdownFenceRegex();

    [GeneratedRegex(@"^(#{1,6}\s+|[-*+]\s+|\d+\.\s+|>\s+|\|.+\|$)")]
    private static partial Regex MarkdownSignalRegex();

    [GeneratedRegex(@"[\u2500-\u257F]")]
    private static partial Regex TreeSignalRegex();

    [GeneratedRegex(@"^\s{2,}\S")]
    private static partial Regex IndentedTreeLineRegex();

    [GeneratedRegex(@"^\s*(const|let|var|if|for|while|return|using|public|private|class|function|import|export|select|insert|update|delete)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CodeKeywordRegex();

    [GeneratedRegex(@"[;{}<>]=?|=>")]
    private static partial Regex CodeSymbolRegex();

    [GeneratedRegex(@"[A-Za-z]")]
    private static partial Regex LetterRegex();

    [GeneratedRegex(@"^[-*+>\d#|`]")]
    private static partial Regex MarkdownLeadRegex();
}
