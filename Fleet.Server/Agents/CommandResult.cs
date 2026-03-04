namespace Fleet.Server.Agents;

/// <summary>Result of a sandboxed command execution.</summary>
public record CommandResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);
