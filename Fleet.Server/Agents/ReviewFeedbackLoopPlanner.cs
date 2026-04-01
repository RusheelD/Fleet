using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fleet.Server.Data.Entities;

namespace Fleet.Server.Agents;

internal enum ReviewTriageRecommendation
{
    Unknown = 0,
    Stop,
    Patch,
    Restart,
}

internal sealed record ReviewFindingDecision(
    string Severity,
    string Description,
    string Suggestion,
    AgentRole? TargetRole);

internal sealed record ReviewTriageDecision(
    ReviewTriageRecommendation Recommendation,
    string HighestSeverity,
    string Summary,
    string Rationale,
    IReadOnlyList<ReviewFindingDecision> Findings,
    IReadOnlyList<AgentRole> TargetRoles,
    AgentRole? RestartFromRole)
{
    public bool RequiresAutomaticLoop =>
        Recommendation is ReviewTriageRecommendation.Patch or ReviewTriageRecommendation.Restart;

    public string RecommendationLabel => Recommendation switch
    {
        ReviewTriageRecommendation.Stop => "STOP",
        ReviewTriageRecommendation.Patch => "PATCH",
        ReviewTriageRecommendation.Restart => "RESTART",
        _ => "UNKNOWN",
    };
}

internal sealed record ReviewExecutionSummary(
    int AutomaticLoopCount,
    string? LastRecommendation);

internal static partial class ReviewFeedbackLoopPlanner
{
    private static readonly AgentRole[] ReviewLoopRoleSearchOrder =
    [
        AgentRole.Manager,
        AgentRole.Planner,
        AgentRole.Contracts,
        AgentRole.Backend,
        AgentRole.Frontend,
        AgentRole.Testing,
        AgentRole.Styling,
        AgentRole.Consolidation,
        AgentRole.Review,
        AgentRole.Documentation,
    ];

    public static ReviewTriageDecision ParseDecision(string reviewOutput)
    {
        var normalizedOutput = NormalizeText(reviewOutput);
        if (TryParseDecisionManifest(normalizedOutput, out var manifestDecision))
            return manifestDecision;

        var recommendationMatch = RecommendationRegex().Match(normalizedOutput);
        var inferredRecommendation = recommendationMatch.Success
            ? ParseRecommendationLabel(recommendationMatch.Groups["value"].Value)
            : ReviewTriageRecommendation.Unknown;
        var findings = ParseFallbackFindings(normalizedOutput);
        var highestSeverity = ParseHighestSeverity(normalizedOutput, findings);
        if (inferredRecommendation == ReviewTriageRecommendation.Unknown)
            inferredRecommendation = InferRecommendationFromSeverity(highestSeverity);

        var targetRoles = ParseMentionedRoles(normalizedOutput, includeReview: true);
        var restartFromRole = ParseRestartRole(normalizedOutput);
        var summary = ParseFallbackSummary(normalizedOutput);
        var rationale = ParseFallbackRationale(normalizedOutput);

        return new ReviewTriageDecision(
            inferredRecommendation,
            highestSeverity,
            summary,
            rationale,
            findings,
            targetRoles,
            restartFromRole);
    }

    public static IReadOnlyList<AgentRole> DetermineRolesToRerun(
        AgentRole[][] pipeline,
        ReviewTriageDecision decision)
    {
        var orderedRoles = pipeline.SelectMany(group => group).ToArray();
        if (orderedRoles.Length == 0 || !decision.RequiresAutomaticLoop)
            return [];

        var availableRoles = orderedRoles.ToHashSet();

        if (decision.Recommendation == ReviewTriageRecommendation.Restart)
        {
            var restartRole = NormalizeRestartRole(decision.RestartFromRole, decision.TargetRoles, orderedRoles);
            if (restartRole is null)
                return [];

            return ExpandRolesFrom(restartRole.Value, orderedRoles);
        }

        var rerunRoles = new HashSet<AgentRole>();
        var effectiveTargets = decision.TargetRoles
            .Where(availableRoles.Contains)
            .ToList();

        if (effectiveTargets.Count == 0)
        {
            effectiveTargets = [.. ParseFallbackPatchTargets(decision, orderedRoles)];
        }

        foreach (var targetRole in effectiveTargets)
        {
            foreach (var role in ExpandPatchRole(targetRole, orderedRoles))
                rerunRoles.Add(role);
        }

        if (rerunRoles.Count == 0)
        {
            var fallbackStart = orderedRoles.FirstOrDefault(role =>
                role is AgentRole.Contracts or AgentRole.Backend or AgentRole.Frontend or AgentRole.Testing or AgentRole.Styling);

            if (fallbackStart != default)
            {
                foreach (var role in ExpandRolesFrom(fallbackStart, orderedRoles))
                    rerunRoles.Add(role);
            }
        }

        return orderedRoles.Where(rerunRoles.Contains).ToArray();
    }

    public static AgentRole[][] BuildPipelineSubset(
        AgentRole[][] pipeline,
        IReadOnlyCollection<AgentRole> rolesToInclude)
    {
        if (rolesToInclude.Count == 0)
            return [];

        var includeSet = rolesToInclude.ToHashSet();
        return pipeline
            .Select(group => group.Where(includeSet.Contains).ToArray())
            .Where(group => group.Length > 0)
            .ToArray();
    }

    public static string BuildAutomaticReviewFeedbackContext(
        ReviewTriageDecision decision,
        IReadOnlyCollection<AgentRole> rerunRoles,
        int cycleNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Automatic Review Feedback Loop");
        sb.AppendLine($"Fleet automatically entered review remediation cycle {cycleNumber}.");
        sb.AppendLine($"- Review recommendation: {decision.RecommendationLabel}");
        if (!string.IsNullOrWhiteSpace(decision.HighestSeverity))
            sb.AppendLine($"- Highest severity: {decision.HighestSeverity}");
        if (rerunRoles.Count > 0)
            sb.AppendLine($"- Roles rerunning: {string.Join(", ", rerunRoles)}");
        if (decision.RestartFromRole is not null)
            sb.AppendLine($"- Restart from phase: {decision.RestartFromRole}");
        if (!string.IsNullOrWhiteSpace(decision.Summary))
            sb.AppendLine($"- Summary: {decision.Summary}");
        if (!string.IsNullOrWhiteSpace(decision.Rationale))
            sb.AppendLine($"- Rationale: {decision.Rationale}");

        if (decision.Findings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Findings To Address");
            foreach (var finding in decision.Findings.Take(8))
            {
                var targetRoleText = finding.TargetRole is null ? string.Empty : $" ({finding.TargetRole})";
                sb.AppendLine($"- [{finding.Severity}]{targetRoleText} {finding.Description}");
                if (!string.IsNullOrWhiteSpace(finding.Suggestion))
                    sb.AppendLine($"  Fix: {finding.Suggestion}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Resolve these review findings before handing back to Review.");
        return sb.ToString().TrimEnd();
    }

    public static ReviewExecutionSummary SummarizeExecutionReviews(IEnumerable<AgentPhaseResult> phaseResults)
    {
        var reviewResults = phaseResults
            .Where(result => result.Success && string.Equals(result.Role, AgentRole.Review.ToString(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(result => result.PhaseOrder)
            .ToList();

        if (reviewResults.Count == 0)
            return new ReviewExecutionSummary(0, null);

        var decisions = reviewResults
            .Select(result => ParseDecision(result.Output))
            .ToList();

        var automaticLoopCount = decisions.Count(decision => decision.RequiresAutomaticLoop);
        var lastRecommendation = decisions.LastOrDefault()?.RecommendationLabel;
        return new ReviewExecutionSummary(automaticLoopCount, lastRecommendation);
    }

    private static bool TryParseDecisionManifest(string reviewOutput, out ReviewTriageDecision decision)
    {
        var fencedBlocks = DecisionBlockRegex().Matches(reviewOutput)
            .Select(match => match.Groups["body"].Value.Trim())
            .Reverse()
            .ToList();

        foreach (var block in fencedBlocks)
        {
            if (!TryParseJsonDecision(block, out decision))
                continue;

            return true;
        }

        decision = new ReviewTriageDecision(
            ReviewTriageRecommendation.Unknown,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            [],
            null);
        return false;
    }

    private static bool TryParseJsonDecision(string json, out ReviewTriageDecision decision)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                decision = default!;
                return false;
            }

            var root = document.RootElement;
            if (!root.TryGetProperty("recommendation", out var recommendationProp))
            {
                decision = default!;
                return false;
            }

            var recommendation = ParseRecommendationLabel(recommendationProp.GetString());
            var highestSeverity = root.TryGetProperty("highest_severity", out var severityProp)
                ? NormalizeSeverity(severityProp.GetString())
                : string.Empty;
            var summary = root.TryGetProperty("summary", out var summaryProp)
                ? summaryProp.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var rationale = root.TryGetProperty("rationale", out var rationaleProp)
                ? rationaleProp.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var targetRoles = root.TryGetProperty("target_roles", out var targetRolesProp)
                ? ParseRolesFromJsonArray(targetRolesProp)
                : [];
            var restartFromRole = root.TryGetProperty("restart_from", out var restartProp)
                ? ParseRoleLabel(restartProp.GetString())
                : null;
            var findings = root.TryGetProperty("findings", out var findingsProp)
                ? ParseFindingsFromJson(findingsProp)
                : [];

            if (recommendation == ReviewTriageRecommendation.Unknown)
                recommendation = InferRecommendationFromSeverity(highestSeverity);

            if (string.IsNullOrWhiteSpace(highestSeverity))
                highestSeverity = InferHighestSeverityFromFindings(findings);

            decision = new ReviewTriageDecision(
                recommendation,
                highestSeverity,
                summary,
                rationale,
                findings,
                targetRoles,
                restartFromRole);

            return true;
        }
        catch (JsonException)
        {
            decision = default!;
            return false;
        }
    }

    private static IReadOnlyList<ReviewFindingDecision> ParseFindingsFromJson(JsonElement findingsProp)
    {
        if (findingsProp.ValueKind != JsonValueKind.Array)
            return [];

        var findings = new List<ReviewFindingDecision>();
        foreach (var finding in findingsProp.EnumerateArray())
        {
            if (finding.ValueKind != JsonValueKind.Object)
                continue;

            var severity = finding.TryGetProperty("severity", out var severityProp)
                ? NormalizeSeverity(severityProp.GetString())
                : string.Empty;
            var description = finding.TryGetProperty("description", out var descriptionProp)
                ? descriptionProp.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var suggestion = finding.TryGetProperty("suggestion", out var suggestionProp)
                ? suggestionProp.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var targetRole = finding.TryGetProperty("role", out var roleProp)
                ? ParseRoleLabel(roleProp.GetString())
                : null;

            if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(suggestion))
                continue;

            findings.Add(new ReviewFindingDecision(severity, description, suggestion, targetRole));
        }

        return findings;
    }

    private static IReadOnlyList<ReviewFindingDecision> ParseFallbackFindings(string reviewOutput)
    {
        var findings = new List<ReviewFindingDecision>();
        foreach (Match match in FindingLineRegex().Matches(reviewOutput))
        {
            var severity = NormalizeSeverity(match.Groups["severity"].Value);
            var description = match.Groups["body"].Value.Trim();
            if (string.IsNullOrWhiteSpace(description))
                continue;

            var targetRole = ParseMentionedRoles(description, includeReview: true)
                .Cast<AgentRole?>()
                .FirstOrDefault();
            findings.Add(new ReviewFindingDecision(severity, description, string.Empty, targetRole));
        }

        return findings;
    }

    private static IReadOnlyList<AgentRole> ParseRolesFromJsonArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            return [];

        return value.EnumerateArray()
            .Select(item => item.GetString())
            .Select(ParseRoleLabel)
            .OfType<AgentRole>()
            .Distinct()
            .ToArray();
    }

    private static AgentRole? ParseRestartRole(string reviewOutput)
    {
        var restartFromMatch = RestartFromRegex().Match(reviewOutput);
        if (restartFromMatch.Success)
        {
            var explicitRole = ParseRoleLabel(restartFromMatch.Groups["role"].Value);
            if (explicitRole is not null)
                return explicitRole;
        }

        foreach (var role in ParseMentionedRoles(reviewOutput, includeReview: false))
        {
            if (role is AgentRole.Planner or AgentRole.Contracts or AgentRole.Backend or AgentRole.Frontend or AgentRole.Testing or AgentRole.Styling or AgentRole.Consolidation)
                return role;
        }

        return null;
    }

    private static IReadOnlyList<AgentRole> ParseMentionedRoles(string reviewOutput, bool includeReview)
    {
        var mentionedRoles = new List<AgentRole>();
        foreach (var role in ReviewLoopRoleSearchOrder)
        {
            if (!includeReview && role == AgentRole.Review)
                continue;

            if (RoleMentionRegex(role.ToString()).IsMatch(reviewOutput))
                mentionedRoles.Add(role);
        }

        return mentionedRoles;
    }

    private static string ParseHighestSeverity(string reviewOutput, IReadOnlyList<ReviewFindingDecision> findings)
    {
        var explicitMatches = SeverityRegex().Matches(reviewOutput)
            .Select(match => NormalizeSeverity(match.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (explicitMatches.Count > 0)
            return explicitMatches.OrderBy(GetSeverityRank).First();

        return InferHighestSeverityFromFindings(findings);
    }

    private static string InferHighestSeverityFromFindings(IReadOnlyList<ReviewFindingDecision> findings)
    {
        return findings
            .Select(finding => NormalizeSeverity(finding.Severity))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(GetSeverityRank)
            .FirstOrDefault() ?? string.Empty;
    }

    private static ReviewTriageRecommendation ParseRecommendationLabel(string? recommendation)
    {
        var normalized = recommendation?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "STOP" => ReviewTriageRecommendation.Stop,
            "PATCH" => ReviewTriageRecommendation.Patch,
            "RESTART" => ReviewTriageRecommendation.Restart,
            _ => ReviewTriageRecommendation.Unknown,
        };
    }

    private static ReviewTriageRecommendation InferRecommendationFromSeverity(string highestSeverity)
        => highestSeverity switch
        {
            "P0" => ReviewTriageRecommendation.Restart,
            "P1" => ReviewTriageRecommendation.Patch,
            "P2" => ReviewTriageRecommendation.Patch,
            _ => ReviewTriageRecommendation.Stop,
        };

    private static string ParseFallbackSummary(string reviewOutput)
    {
        var summaryMatch = SummaryRegex().Match(reviewOutput);
        if (summaryMatch.Success)
            return summaryMatch.Groups["summary"].Value.Trim();

        var firstContentLine = reviewOutput
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line =>
                line.Length > 0 &&
                !line.StartsWith("#", StringComparison.Ordinal) &&
                !line.StartsWith("- ", StringComparison.Ordinal) &&
                !line.StartsWith("* ", StringComparison.Ordinal) &&
                !line.StartsWith("```", StringComparison.Ordinal));

        return firstContentLine ?? string.Empty;
    }

    private static string ParseFallbackRationale(string reviewOutput)
    {
        var rationaleMatch = RationaleRegex().Match(reviewOutput);
        return rationaleMatch.Success
            ? rationaleMatch.Groups["rationale"].Value.Trim()
            : string.Empty;
    }

    private static AgentRole? NormalizeRestartRole(
        AgentRole? restartFromRole,
        IReadOnlyList<AgentRole> targetRoles,
        IReadOnlyList<AgentRole> orderedRoles)
    {
        if (restartFromRole is AgentRole.Manager)
            restartFromRole = AgentRole.Planner;

        if (restartFromRole is not null && orderedRoles.Contains(restartFromRole.Value))
            return restartFromRole;

        if (targetRoles.Count > 0)
            return targetRoles.OrderBy(role => Array.IndexOf(orderedRoles.ToArray(), role)).First();

        return orderedRoles.FirstOrDefault(role => role != AgentRole.Manager);
    }

    private static IReadOnlyList<AgentRole> ParseFallbackPatchTargets(
        ReviewTriageDecision decision,
        IReadOnlyList<AgentRole> orderedRoles)
    {
        var explicitTargets = decision.Findings
            .Select(finding => finding.TargetRole)
            .OfType<AgentRole>()
            .Where(orderedRoles.Contains)
            .Distinct()
            .ToList();

        if (explicitTargets.Count > 0)
            return explicitTargets;

        var mentionedTargets = ParseMentionedRoles($"{decision.Summary}\n{decision.Rationale}", includeReview: false)
            .Where(orderedRoles.Contains)
            .Distinct()
            .ToList();

        if (mentionedTargets.Count > 0)
            return mentionedTargets;

        return orderedRoles
            .Where(role => role is AgentRole.Backend or AgentRole.Frontend or AgentRole.Testing or AgentRole.Styling or AgentRole.Consolidation)
            .ToArray();
    }

    private static IReadOnlyList<AgentRole> ExpandPatchRole(AgentRole targetRole, IReadOnlyList<AgentRole> orderedRoles)
    {
        var rerunRoles = new HashSet<AgentRole>();

        void AddIfPresent(AgentRole role)
        {
            if (orderedRoles.Contains(role))
                rerunRoles.Add(role);
        }

        switch (targetRole)
        {
            case AgentRole.Manager:
            case AgentRole.Planner:
                foreach (var role in ExpandRolesFrom(AgentRole.Planner, orderedRoles))
                    rerunRoles.Add(role);
                break;
            case AgentRole.Contracts:
                foreach (var role in ExpandRolesFrom(AgentRole.Contracts, orderedRoles))
                    rerunRoles.Add(role);
                break;
            case AgentRole.Backend:
                AddIfPresent(AgentRole.Backend);
                AddIfPresent(AgentRole.Testing);
                AddIfPresent(AgentRole.Consolidation);
                AddIfPresent(AgentRole.Review);
                AddIfPresent(AgentRole.Documentation);
                break;
            case AgentRole.Frontend:
                AddIfPresent(AgentRole.Frontend);
                AddIfPresent(AgentRole.Styling);
                AddIfPresent(AgentRole.Testing);
                AddIfPresent(AgentRole.Consolidation);
                AddIfPresent(AgentRole.Review);
                AddIfPresent(AgentRole.Documentation);
                break;
            case AgentRole.Testing:
                AddIfPresent(AgentRole.Testing);
                AddIfPresent(AgentRole.Review);
                AddIfPresent(AgentRole.Documentation);
                break;
            case AgentRole.Styling:
                AddIfPresent(AgentRole.Styling);
                AddIfPresent(AgentRole.Review);
                AddIfPresent(AgentRole.Documentation);
                break;
            case AgentRole.Consolidation:
                AddIfPresent(AgentRole.Consolidation);
                AddIfPresent(AgentRole.Review);
                AddIfPresent(AgentRole.Documentation);
                break;
            case AgentRole.Documentation:
                AddIfPresent(AgentRole.Documentation);
                AddIfPresent(AgentRole.Review);
                break;
            case AgentRole.Review:
                AddIfPresent(AgentRole.Review);
                break;
        }

        return orderedRoles.Where(rerunRoles.Contains).ToArray();
    }

    private static IReadOnlyList<AgentRole> ExpandRolesFrom(AgentRole startRole, IReadOnlyList<AgentRole> orderedRoles)
    {
        var effectiveStartRole = startRole == AgentRole.Manager ? AgentRole.Planner : startRole;
        var startIndex = Array.IndexOf(orderedRoles.ToArray(), effectiveStartRole);
        if (startIndex < 0)
            startIndex = 0;

        return orderedRoles.Skip(startIndex).ToArray();
    }

    private static AgentRole? ParseRoleLabel(string? roleLabel)
    {
        if (string.IsNullOrWhiteSpace(roleLabel))
            return null;

        return Enum.TryParse<AgentRole>(roleLabel.Trim(), ignoreCase: true, out var role)
            ? role
            : null;
    }

    private static string NormalizeSeverity(string? severity)
    {
        var normalized = severity?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "P0" or "P1" or "P2" or "P3" => normalized,
            _ => string.Empty,
        };
    }

    private static int GetSeverityRank(string severity)
        => severity switch
        {
            "P0" => 0,
            "P1" => 1,
            "P2" => 2,
            "P3" => 3,
            _ => int.MaxValue,
        };

    private static string NormalizeText(string value)
        => value.Replace("\r\n", "\n").Trim();

    [GeneratedRegex(@"(?ms)^```(?:json|review[_-]?decision|review[_-]?feedback)?\s*(?<body>\{.*?\})\s*^```", RegexOptions.Multiline)]
    private static partial Regex DecisionBlockRegex();

    [GeneratedRegex(@"\bP[0-3]\b", RegexOptions.IgnoreCase)]
    private static partial Regex SeverityRegex();

    [GeneratedRegex(@"(?im)^\s*[-*]?\s*\**Recommendation\**\s*:\s*(?<value>STOP|PATCH|RESTART)\b")]
    private static partial Regex RecommendationRegex();

    [GeneratedRegex(@"(?im)^\s*[-*]?\s*\**Restart(?:\s+From)?\**\s*:\s*(?<role>Manager|Planner|Contracts|Backend|Frontend|Testing|Styling|Consolidation|Review|Documentation)\b")]
    private static partial Regex RestartFromRegex();

    [GeneratedRegex(@"(?im)^\s*[-*]?\s*\**(?:Overall Assessment|Summary)\**\s*:\s*(?<summary>.+)$")]
    private static partial Regex SummaryRegex();

    [GeneratedRegex(@"(?im)^\s*[-*]?\s*\**Rationale\**\s*:\s*(?<rationale>.+)$")]
    private static partial Regex RationaleRegex();

    [GeneratedRegex(@"(?im)^\s*[-*]\s*(?<severity>P[0-3])(?:\s+|:|\])(?<body>.+)$")]
    private static partial Regex FindingLineRegex();

    private static Regex RoleMentionRegex(string role)
        => new($@"\b{Regex.Escape(role)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
