using System.Text.Json;
using Fleet.Server.LLM;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Retrieves sections of previously truncated tool output without re-executing the tool.
/// When a tool result is oversized and stored by <see cref="ToolResultStore"/>,
/// the model can call this tool with the reference ID to access the full content.
/// </summary>
public class RecallToolOutputAgentTool(ToolResultStore store) : IAgentTool
{
    public string Name => "recall_tool_output";

    public string Description =>
        "Retrieve a section of a previously truncated tool output. " +
        "When a tool result was truncated, the truncation message includes a ref_id. " +
        "Use this tool to read more of that output without re-running the original tool.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "ref_id": {
                    "type": "string",
                    "description": "The reference ID from the truncation message."
                },
                "offset": {
                    "type": "integer",
                    "description": "Character offset to start reading from (default: 0).",
                    "default": 0
                },
                "length": {
                    "type": "integer",
                    "description": "Number of characters to retrieve (default: 8000).",
                    "default": 8000
                }
            },
            "required": ["ref_id"],
            "additionalProperties": false
        }
        """;

    public bool IsReadOnly => true;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("ref_id", out var refIdProp))
            return Task.FromResult("Error: ref_id is required.");

        var refId = refIdProp.GetString();
        if (string.IsNullOrWhiteSpace(refId))
            return Task.FromResult("Error: ref_id must not be empty.");

        var offset = root.TryGetProperty("offset", out var offsetProp) ? offsetProp.GetInt32() : 0;
        var length = root.TryGetProperty("length", out var lengthProp) ? lengthProp.GetInt32() : 8000;

        var result = store.Recall(refId, offset, length);
        return Task.FromResult(result ?? $"Error: no stored output found for ref_id \"{refId}\". The reference may have expired.");
    }
}
