using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Gets a summary of changes made in the current sandbox (files added, modified, deleted).
/// </summary>
public class GetChangeSummaryTool : IAgentTool
{
    public string Name => "get_change_summary";

    public string Description =>
        "Get a summary of all file changes made so far in this execution. " +
        "Returns a list of files with their change status (added, modified, deleted).";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        try
        {
            var changes = await context.Sandbox.GetChangeSummaryAsync(cancellationToken);

            if (changes.Count == 0)
                return "No file changes detected.";

            var result = new
            {
                totalChanges = changes.Count,
                changes = changes.Select(c => new { c.Path, c.Status }).ToList(),
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error getting change summary: {ex.Message}";
        }
    }
}
