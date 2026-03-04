namespace Fleet.Server.Agents;

/// <summary>A file change in the working directory.</summary>
public record FileChange(string Path, string Status);
