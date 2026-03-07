using Fleet.Server.Models;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

internal static class ChatToolProjectResolver
{
    public static async Task<IReadOnlyList<ProjectDto>> ResolveProjectsAsync(
        IProjectService projectService,
        ChatToolContext context,
        string? projectId = null,
        string? projectSlug = null)
    {
        var projects = await projectService.GetAllProjectsAsync();

        if (context.IsProjectScoped)
            return projects.Where(project => project.Id == context.ProjectId).ToList();

        if (!string.IsNullOrWhiteSpace(projectId))
            return projects.Where(project => project.Id == projectId).ToList();

        if (!string.IsNullOrWhiteSpace(projectSlug))
            return projects.Where(project => project.Slug.Equals(projectSlug, StringComparison.OrdinalIgnoreCase)).ToList();

        return projects.ToList();
    }

    public static async Task<ProjectDto?> ResolveSingleProjectAsync(
        IProjectService projectService,
        ChatToolContext context,
        string? projectId = null,
        string? projectSlug = null,
        bool requireSelectorInGlobalScope = false)
    {
        var projects = await ResolveProjectsAsync(projectService, context, projectId, projectSlug);
        if (!context.IsProjectScoped && requireSelectorInGlobalScope && string.IsNullOrWhiteSpace(projectId) && string.IsNullOrWhiteSpace(projectSlug))
            return null;

        return projects.FirstOrDefault();
    }
}
