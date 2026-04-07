namespace Fleet.Server.Data.Entities;

public class McpServerConnection
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public UserProfile? UserProfile { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TransportType { get; set; } = "stdio";
    public string? Command { get; set; }
    public string ArgumentsJson { get; set; } = "[]";
    public string? WorkingDirectory { get; set; }
    public string? Endpoint { get; set; }
    public string? ProtectedEnvironmentVariables { get; set; }
    public string? ProtectedHeaders { get; set; }
    public string? BuiltInTemplateKey { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastValidatedAtUtc { get; set; }
    public string? LastValidationError { get; set; }
    public int LastToolCount { get; set; }
    public string DiscoveredToolsJson { get; set; } = "[]";
}
