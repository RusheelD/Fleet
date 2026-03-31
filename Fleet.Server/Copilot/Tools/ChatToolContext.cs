using System.Diagnostics.CodeAnalysis;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Runtime context passed to every chat tool invocation.</summary>
public record ChatToolContext(string? ProjectId, string UserId)
{
    public bool IsProjectScoped => !string.IsNullOrWhiteSpace(ProjectId);

    public static string ProjectScopeRequiredMessage =>
        "Error: this tool requires a project-scoped chat session. Open the project chat and try again.";

    public bool TryGetProjectId([NotNullWhen(true)] out string? projectId)
    {
        projectId = string.IsNullOrWhiteSpace(ProjectId) ? null : ProjectId;
        return projectId is not null;
    }
}
