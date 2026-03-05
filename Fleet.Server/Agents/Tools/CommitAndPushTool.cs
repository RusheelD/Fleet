using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Commits and pushes all pending changes to the remote branch.
/// The orchestrator creates the draft PR up front — this tool just pushes commits to it.
/// </summary>
public class CommitAndPushTool : IAgentTool
{
    public string Name => "commit_and_push";

    public string Description =>
        "Stage, commit, and push all current changes to the remote branch. " +
        "A draft pull request is already open — your commits will appear there automatically. " +
        "Call this frequently after meaningful progress to save your work. " +
        "If there are no changes to commit, this is a no-op.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "commit_message": {
                    "type": "string",
                    "description": "A short, descriptive commit message (e.g., 'Add ProjectService with CRUD methods')."
                }
            },
            "required": ["commit_message"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("commit_message", out var msgProp) || string.IsNullOrWhiteSpace(msgProp.GetString()))
            return "Error: 'commit_message' parameter is required.";

        var commitMessage = msgProp.GetString()!;

        try
        {
            await context.Sandbox.CommitAndPushAsync(
                commitMessage,
                authorName: "Fleet Agent",
                authorEmail: "agent@fleet.dev",
                cancellationToken);

            return $"Changes committed and pushed: \"{commitMessage}\"";
        }
        catch (Exception ex)
        {
            return $"Error committing/pushing: {ex.Message}";
        }
    }
}
