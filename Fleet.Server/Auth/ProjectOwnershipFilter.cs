using Fleet.Server.Projects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fleet.Server.Auth;

/// <summary>
/// Action filter that verifies the current user owns the project specified by the {projectId} route parameter.
/// Returns 404 if the project doesn't exist or the user doesn't own it.
/// Apply to controllers or actions that operate on project sub-resources.
/// </summary>
public class ProjectOwnershipFilter(IAuthService authService, IProjectRepository projectRepository) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.RouteData.Values.TryGetValue("projectId", out var projectIdObj) &&
            projectIdObj is string projectId &&
            !string.IsNullOrWhiteSpace(projectId))
        {
            var ownerId = (await authService.GetCurrentUserIdAsync()).ToString();
            var project = await projectRepository.GetByIdAsync(projectId, ownerId);

            if (project is null)
            {
                context.Result = new NotFoundResult();
                return;
            }
        }

        await next();
    }
}
