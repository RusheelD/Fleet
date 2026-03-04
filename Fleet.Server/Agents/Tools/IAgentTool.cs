namespace Fleet.Server.Agents.Tools;

/// <summary>
/// A tool that an AI agent can invoke during a pipeline phase.
/// Each tool has a JSON Schema describing its parameters and
/// an execution method that returns a string result.
/// </summary>
public interface IAgentTool
{
    /// <summary>Unique name used by the LLM to invoke this tool (snake_case).</summary>
    string Name { get; }

    /// <summary>Human-readable description shown to the LLM so it knows when to use the tool.</summary>
    string Description { get; }

    /// <summary>JSON Schema (type: object) describing the tool's input parameters.</summary>
    string ParametersJsonSchema { get; }

    /// <summary>Execute the tool and return a text result for the LLM to consume.</summary>
    Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken = default);
}
