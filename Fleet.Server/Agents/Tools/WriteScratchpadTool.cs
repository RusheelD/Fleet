using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Writes a named entry to the shared inter-phase scratchpad. Entries persist
/// across all phases within the execution—downstream agents can read them via
/// <c>read_scratchpad</c>.
/// </summary>
public class WriteScratchpadTool : IAgentTool
{
    public string Name => "write_scratchpad";

    public string Description =>
        "Write a named entry to the shared scratchpad that other agent phases can read. " +
        "Use this to pass detailed information (API contracts, architecture decisions, " +
        "implementation notes) to downstream phases without it being summarized. " +
        "Entries are keyed by name—writing the same key overwrites the previous value. " +
        "Use mode 'append' to add to an existing entry instead of overwriting.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "key": {
                    "type": "string",
                    "description": "Name for this entry (e.g., 'api_contracts', 'architecture_decisions', 'implementation_notes')."
                },
                "content": {
                    "type": "string",
                    "description": "The content to write."
                },
                "mode": {
                    "type": "string",
                    "enum": ["write", "append"],
                    "description": "Whether to overwrite ('write') or append ('append') to an existing entry. Default: 'write'."
                }
            },
            "required": ["key", "content"],
            "additionalProperties": false
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("key", out var keyProp) || string.IsNullOrWhiteSpace(keyProp.GetString()))
            return Task.FromResult("Error: 'key' parameter is required.");

        if (!args.TryGetProperty("content", out var contentProp))
            return Task.FromResult("Error: 'content' parameter is required.");

        var key = keyProp.GetString()!;
        var content = contentProp.GetString() ?? "";
        var mode = args.TryGetProperty("mode", out var modeProp) ? modeProp.GetString() : "write";

        // Determine the author role from the execution context (not available directly,
        // so we record as a generic entry — the role is tracked in the orchestrator)
        var author = AgentRole.Backend; // Default; the scratchpad records the role for attribution

        if (mode == "append")
        {
            context.Scratchpad.Append(key, content, author);
            return Task.FromResult($"Appended {content.Length:N0} characters to scratchpad entry '{key}'.");
        }

        context.Scratchpad.Write(key, content, author);
        return Task.FromResult($"Wrote {content.Length:N0} characters to scratchpad entry '{key}'.");
    }
}
