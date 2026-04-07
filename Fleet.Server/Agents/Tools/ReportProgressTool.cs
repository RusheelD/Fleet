using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Allows the agent to report its estimated completion percentage.
/// The orchestrator uses this to update the live progress indicator
/// instead of approximating from tool-call counts.
/// </summary>
public class ReportProgressTool : IAgentTool
{
    public string Name => "report_progress";

    public string Description =>
        "Report your estimated completion percentage (0-100, decimals allowed). " +
        "Call this after each meaningful tool call and during longer stretches of thinking so the user can see live progress. " +
        "Prefer realistic smaller updates instead of jumping whole percentages too early. " +
        "Use concise milestone summaries and send 100% only when your phase is truly complete.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "percent_complete": {
                    "type": "number",
                    "description": "Your estimated completion percentage, from 0 to 100. Fractional values such as 12.35 are allowed.",
                    "minimum": 0,
                    "maximum": 100,
                    "multipleOf": 0.05
                },
                "summary": {
                    "type": "string",
                    "description": "A brief one-line summary of what you just finished or are working on."
                }
            },
            "required": ["percent_complete", "summary"]
        }
        """;

    /// <summary>
    /// This tool is read-only - it does not modify files or external state.
    /// The actual progress update is handled by the phase runner via the callback.
    /// </summary>
    public bool IsReadOnly => true;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        // The phase runner intercepts this tool call and invokes the progress callback.
        // The tool itself just acknowledges the report.
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        var percent = args.TryGetProperty("percent_complete", out var pctProp) && pctProp.ValueKind == JsonValueKind.Number
            ? pctProp.GetDouble()
            : 0;
        return Task.FromResult($"Progress reported: {percent:0.##}%");
    }
}
