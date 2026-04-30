using System.Diagnostics.CodeAnalysis;
using Fleet.Server.Models;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Runtime context passed to every chat tool invocation.</summary>
public record ChatToolContext(
    string? ProjectId,
    string UserId,
    IReadOnlyList<ChatAttachmentDto>? MessageAttachments = null,
    bool DynamicIterationEnabled = false)
{
    public bool IsProjectScoped => !string.IsNullOrWhiteSpace(ProjectId);
    public IReadOnlyList<ChatAttachmentDto> CurrentMessageAttachments => MessageAttachments ?? [];
    public bool DefaultCreatedWorkItemIsAi => DynamicIterationEnabled;
    public string DefaultCreatedWorkItemAssignee => DynamicIterationEnabled ? "Fleet AI" : "Unassigned";
    public string? DefaultCreatedWorkItemAssignmentMode => DynamicIterationEnabled ? "auto" : null;

    public static string ProjectScopeRequiredMessage =>
        "Error: this tool requires a project-scoped chat session. Open the project chat and try again.";

    public bool TryGetProjectId([NotNullWhen(true)] out string? projectId)
    {
        projectId = string.IsNullOrWhiteSpace(ProjectId) ? null : ProjectId;
        return projectId is not null;
    }
}
