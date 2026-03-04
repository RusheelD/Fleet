namespace Fleet.Server.Agents;

/// <summary>A search result from the repository files.</summary>
public record SearchResult(string FilePath, int LineNumber, string LineContent);
