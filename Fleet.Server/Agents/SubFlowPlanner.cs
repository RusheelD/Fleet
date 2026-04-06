using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Fleet.Server.Agents;

internal static partial class SubFlowPlanner
{
    private const int MaxTotalSubFlows = 24;
    private const int MaxNestedDepth = 3;
    private const int MaxDirectSubFlowsPerNode = AgentOrchestrationService.MaxSubFlowChildrenPerExecution;

    public static GeneratedSubFlowPlan? Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        foreach (Match match in JsonFencePattern().Matches(output))
        {
            var json = match.Groups["json"].Value.Trim();
            if (!json.Contains("\"subflows\"", StringComparison.OrdinalIgnoreCase))
                continue;

            var parsed = TryParse(json);
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    private static GeneratedSubFlowPlan? TryParse(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<SubFlowPlanContract>(json, SerializerOptions);
            if (payload?.Split != true || payload.SubFlows is null || payload.SubFlows.Count == 0)
                return null;

            var subFlows = payload.SubFlows
                .Select(Normalize)
                .Where(spec => spec is not null)
                .Select(spec => spec!)
                .ToArray();
            if (subFlows.Length == 0)
                return null;

            if (CountSubFlows(subFlows) > MaxTotalSubFlows)
                return null;

            if (GetMaxDepth(subFlows) > MaxNestedDepth)
                return null;

            if (HasNodeExceedingDirectChildLimit(subFlows))
                return null;

            return new GeneratedSubFlowPlan(
                string.IsNullOrWhiteSpace(payload.Reason) ? "Planner requested decomposition." : payload.Reason.Trim(),
                subFlows);
        }
        catch
        {
            return null;
        }
    }

    private static GeneratedSubFlowSpec? Normalize(SubFlowSpecContract? contract)
    {
        if (contract is null || string.IsNullOrWhiteSpace(contract.Title))
            return null;

        var children = contract.SubFlows?
            .Select(Normalize)
            .Where(spec => spec is not null)
            .Select(spec => spec!)
            .ToArray() ?? [];

        return new GeneratedSubFlowSpec(
            contract.Title.Trim(),
            contract.Description?.Trim() ?? string.Empty,
            NormalizeScale(contract.Priority),
            NormalizeScale(contract.Difficulty),
            contract.Tags?
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [],
            contract.AcceptanceCriteria?.Trim() ?? string.Empty,
            children);
    }

    private static int NormalizeScale(int? value)
    {
        if (value is null)
            return 3;

        return Math.Clamp(value.Value, 1, 5);
    }

    private static int CountSubFlows(IEnumerable<GeneratedSubFlowSpec> specs)
        => specs.Sum(spec => 1 + CountSubFlows(spec.SubFlows));

    private static int GetMaxDepth(IEnumerable<GeneratedSubFlowSpec> specs)
        => specs.Any()
            ? specs.Max(spec => 1 + GetMaxDepth(spec.SubFlows))
            : 0;

    private static bool HasNodeExceedingDirectChildLimit(IReadOnlyList<GeneratedSubFlowSpec> specs)
        => specs.Count > MaxDirectSubFlowsPerNode ||
           specs.Any(spec => HasNodeExceedingDirectChildLimit(spec.SubFlows));

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    [GeneratedRegex("```(?:json)?\\s*(?<json>\\{[\\s\\S]*?\\})\\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonFencePattern();

    private sealed class SubFlowPlanContract
    {
        [JsonPropertyName("split")]
        public bool Split { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("subflows")]
        public List<SubFlowSpecContract>? SubFlows { get; set; }
    }

    private sealed class SubFlowSpecContract
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("priority")]
        public int? Priority { get; set; }

        [JsonPropertyName("difficulty")]
        public int? Difficulty { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("acceptance_criteria")]
        public string? AcceptanceCriteria { get; set; }

        [JsonPropertyName("subflows")]
        public List<SubFlowSpecContract>? SubFlows { get; set; }
    }
}

internal sealed record GeneratedSubFlowPlan(
    string Reason,
    IReadOnlyList<GeneratedSubFlowSpec> SubFlows);

internal sealed record GeneratedSubFlowSpec(
    string Title,
    string Description,
    int Priority,
    int Difficulty,
    IReadOnlyList<string> Tags,
    string AcceptanceCriteria,
    IReadOnlyList<GeneratedSubFlowSpec> SubFlows);
