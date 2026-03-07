namespace Fleet.Server.Copilot.Tools;

/// <summary>Runtime context passed to every chat tool invocation.</summary>
public record ChatToolContext(string? ProjectId, string UserId)
{
    public bool IsProjectScoped => !string.IsNullOrWhiteSpace(ProjectId);
}
