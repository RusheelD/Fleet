using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Reads a named entry from the shared inter-phase scratchpad, or lists all
/// available entries. Use this to access detailed information written by
/// prior agent phases.
/// </summary>
public class ReadScratchpadTool : IAgentTool
{
    public string Name => "read_scratchpad";
    public bool IsReadOnly => true;

    public string Description =>
        "Read an entry from the shared scratchpad written by a prior agent phase, " +
        "or list all available entries. Use this to access detailed information " +
        "(API contracts, architecture decisions, implementation notes) that other " +
        "phases have shared.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "key": {
                    "type": "string",
                    "description": "The name of the entry to read. Omit to list all available entries."
                }
            },
            "additionalProperties": false
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("key", out var keyProp) || string.IsNullOrWhiteSpace(keyProp.GetString()))
        {
            // List all entries
            return Task.FromResult(context.Scratchpad.ListEntries());
        }

        var key = keyProp.GetString()!;
        var entry = context.Scratchpad.Read(key);

        if (entry is null)
        {
            var available = context.Scratchpad.ListEntries();
            return Task.FromResult($"No scratchpad entry found for key '{key}'.\n{available}");
        }

        return Task.FromResult(
            $"Scratchpad entry '{key}' (by {entry.Author}, updated {entry.UpdatedAt:u}):\n\n{entry.Content}");
    }
}
