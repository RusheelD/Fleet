using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Runs a command in the repo sandbox with resource limits.
/// </summary>
public class RunCommandTool : IAgentTool
{
    public string Name => "run_command";

    public string Description =>
        "Execute a shell command in the repository directory. " +
        "Use this to run build commands, tests, linters, or other development tools. " +
        "Commands run with a timeout and path restrictions for safety. " +
        "Python package installs are forced into a repo-local .venv, npm uses repo-local cache/prefix paths, " +
        "and global package-manager mutations are blocked. " +
        "Examples: 'dotnet build', 'npm test', 'npm run lint', 'dotnet test', 'python -m pytest'.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "command": {
                    "type": "string",
                    "description": "The command to run (e.g., 'dotnet', 'npm', 'bash', 'pwsh')."
                },
                "arguments": {
                    "type": "string",
                    "description": "Command arguments (e.g., 'build', 'run test', 'run lint')."
                },
                "timeout_seconds": {
                    "type": "integer",
                    "description": "Maximum execution time in seconds. Default: 120. Max: 300."
                }
            },
            "required": ["command", "arguments"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        if (!args.TryGetProperty("command", out var cmdProp) || string.IsNullOrWhiteSpace(cmdProp.GetString()))
            return "Error: 'command' parameter is required.";

        if (!args.TryGetProperty("arguments", out var argsProp))
            return "Error: 'arguments' parameter is required.";

        var command = cmdProp.GetString()!;
        var normalizedCommand = command.Trim();
        var arguments = argsProp.GetString() ?? "";
        var timeout = args.TryGetProperty("timeout_seconds", out var timeoutProp) ? timeoutProp.GetInt32() : 120;
        timeout = Math.Min(timeout, 300); // Hard cap at 5 minutes

        try
        {
            var result = await context.Sandbox.RunCommandAsync(normalizedCommand, arguments, timeout, cancellationToken);

            var output = new
            {
                command = $"{normalizedCommand} {arguments}",
                exitCode = result.ExitCode,
                timedOut = result.TimedOut,
                stdout = TruncateOutput(result.Stdout, 8000),
                stderr = TruncateOutput(result.Stderr, 4000),
            };

            return JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"Error: command blocked — {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error running command: {ex.Message}";
        }
    }

    private static string TruncateOutput(string output, int maxLength)
    {
        if (string.IsNullOrEmpty(output)) return "";
        return output.Length <= maxLength ? output : output[..maxLength] + $"\n[Truncated at {maxLength} chars]";
    }
}
