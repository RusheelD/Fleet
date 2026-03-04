namespace Fleet.Server.Copilot.Tools;

/// <summary>
/// A tool the AI assistant can invoke during a chat session.
/// Each tool has a JSON Schema describing its parameters and
/// an execution method that returns a string result.
/// </summary>
public interface IChatTool
{
    /// <summary>Unique name used by the LLM to invoke this tool (snake_case).</summary>
    string Name { get; }

    /// <summary>Human-readable description shown to the LLM so it knows when to use the tool.</summary>
    string Description { get; }

    /// <summary>JSON Schema (type: object) describing the tool's input parameters.</summary>
    string ParametersJsonSchema { get; }

    /// <summary>True if this tool modifies data (create/update/delete). Only offered in generate mode.</summary>
    bool IsWriteTool => false;

    /// <summary>Execute the tool and return a text result for the LLM to consume.</summary>
    Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default);
}

/// <summary>Runtime context passed to every tool invocation.</summary>
public record ChatToolContext(string ProjectId, string UserId);
