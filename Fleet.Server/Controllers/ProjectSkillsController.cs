using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.Skills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/skills")]
[ServiceFilter(typeof(ProjectOwnershipFilter))]
public class ProjectSkillsController(ISkillService skillService, IAuthService authService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSkills(string projectId, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var skills = await skillService.GetProjectSkillsAsync(userId, projectId, cancellationToken);
        return Ok(skills);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSkill(string projectId, [FromBody] UpsertPromptSkillRequest request, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var created = await skillService.CreateProjectSkillAsync(userId, projectId, request, cancellationToken);
        return Ok(created);
    }

    [HttpPut("{skillId:int}")]
    public async Task<IActionResult> UpdateSkill(string projectId, int skillId, [FromBody] UpsertPromptSkillRequest request, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var updated = await skillService.UpdateProjectSkillAsync(userId, projectId, skillId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{skillId:int}")]
    public async Task<IActionResult> DeleteSkill(string projectId, int skillId, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await skillService.DeleteProjectSkillAsync(userId, projectId, skillId, cancellationToken);
        return NoContent();
    }
}
