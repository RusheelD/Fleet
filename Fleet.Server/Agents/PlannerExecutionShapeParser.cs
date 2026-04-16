using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Fleet.Server.Agents;

internal enum PlannerSubFlowMode
{
    Direct,
    UseExistingSubFlows,
    GenerateSubFlows,
}

internal sealed record PlannerExecutionShape(
    int EffectiveDifficulty,
    string DifficultyReason,
    PlannerSubFlowMode SubFlowMode,
    string SubFlowReason,
    IReadOnlyList<AgentRole> FollowingAgents,
    int FollowingAgentCount);

internal static partial class PlannerExecutionShapeParser
{
    private static readonly HashSet<AgentRole> AllowedFollowingRoles =
    [
        AgentRole.Research,
        AgentRole.Contracts,
        AgentRole.Backend,
        AgentRole.Frontend,
        AgentRole.Testing,
        AgentRole.Styling,
        AgentRole.Consolidation,
        AgentRole.Review,
        AgentRole.Documentation,
    ];

    public static PlannerExecutionShape? Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var markerJson = ExtractJsonAfterMarker(output, "EXECUTION_PLAN_JSON");
        if (!string.IsNullOrWhiteSpace(markerJson))
        {
            var parsedFromMarker = TryParse(markerJson);
            if (parsedFromMarker is not null)
                return parsedFromMarker;
        }

        foreach (Match match in JsonFencePattern().Matches(output))
        {
            var json = match.Groups["json"].Value.Trim();
            if (!json.Contains("\"following_agents\"", StringComparison.OrdinalIgnoreCase) &&
                !json.Contains("\"effective_difficulty\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parsed = TryParse(json);
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    private static PlannerExecutionShape? TryParse(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<ExecutionPlanContract>(json, SerializerOptions);
            if (payload is null)
                return null;

            var followingAgents = payload.FollowingAgents?
                .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
                .Select(roleName => Enum.TryParse<AgentRole>(roleName, ignoreCase: true, out var role)
                    ? role
                    : (AgentRole?)null)
                .Where(role => role.HasValue && AllowedFollowingRoles.Contains(role.Value))
                .Select(role => role!.Value)
                .ToArray() ?? [];

            var subFlowMode = ParseSubFlowMode(payload.SubFlowMode);
            if (followingAgents.Length == 0)
            {
                if (subFlowMode is PlannerSubFlowMode.UseExistingSubFlows or PlannerSubFlowMode.GenerateSubFlows)
                {
                    followingAgents = [AgentRole.Contracts];
                }
                else
                {
                    return null;
                }
            }

            return new PlannerExecutionShape(
                Math.Clamp(payload.EffectiveDifficulty ?? 3, 1, 5),
                payload.DifficultyReason?.Trim() ?? string.Empty,
                subFlowMode,
                payload.SubFlowReason?.Trim() ?? string.Empty,
                followingAgents,
                followingAgents.Length);
        }
        catch
        {
            return null;
        }
    }

    private static PlannerSubFlowMode ParseSubFlowMode(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "use_existing_subflows" => PlannerSubFlowMode.UseExistingSubFlows,
            "generate_subflows" => PlannerSubFlowMode.GenerateSubFlows,
            _ => PlannerSubFlowMode.Direct,
        };

    private static string? ExtractJsonAfterMarker(string output, string marker)
    {
        var markerIndex = output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;

        var jsonStart = output.IndexOf('{', markerIndex + marker.Length);
        if (jsonStart < 0)
            return null;

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = jsonStart; i < output.Length; i++)
        {
            var ch = output[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                    inString = false;

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                depth++;
                continue;
            }

            if (ch != '}')
                continue;

            depth--;
            if (depth == 0)
                return output.Substring(jsonStart, i - jsonStart + 1);
        }

        return null;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    [GeneratedRegex("```(?:json)?\\s*(?<json>\\{[\\s\\S]*?\\})\\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonFencePattern();

    private sealed class ExecutionPlanContract
    {
        [JsonPropertyName("effective_difficulty")]
        public int? EffectiveDifficulty { get; set; }

        [JsonPropertyName("difficulty_reason")]
        public string? DifficultyReason { get; set; }

        [JsonPropertyName("subflow_mode")]
        public string? SubFlowMode { get; set; }

        [JsonPropertyName("subflow_reason")]
        public string? SubFlowReason { get; set; }

        [JsonPropertyName("following_agent_count")]
        public int? FollowingAgentCount { get; set; }

        [JsonPropertyName("following_agents")]
        public List<string>? FollowingAgents { get; set; }
    }
}
