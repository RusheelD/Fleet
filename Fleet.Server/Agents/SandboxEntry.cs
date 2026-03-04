namespace Fleet.Server.Agents;

/// <summary>A file or directory entry in the sandbox.</summary>
public record SandboxEntry(string Name, string Type, long Size);
