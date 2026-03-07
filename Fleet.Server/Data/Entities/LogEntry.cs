namespace Fleet.Server.Data.Entities;

public class LogEntry
{
    public int Id { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Agent { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExecutionId { get; set; }

    /// <summary>When true, this is a detailed/verbose log (e.g. individual tool calls) hidden by default.</summary>
    public bool IsDetailed { get; set; }

    // Foreign key
    public string ProjectId { get; set; } = string.Empty;
    public Project Project { get; set; } = null!;
}
