using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.Skills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/user/skills")]
public class UserSkillsController(ISkillService skillService, IAuthService authService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSkills(CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var skills = await skillService.GetUserSkillsAsync(userId, cancellationToken);
        return Ok(skills);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSkill([FromBody] UpsertPromptSkillRequest request, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var created = await skillService.CreateUserSkillAsync(userId, request, cancellationToken);
        return Ok(created);
    }

    [HttpPut("{skillId:int}")]
    public async Task<IActionResult> UpdateSkill(int skillId, [FromBody] UpsertPromptSkillRequest request, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var updated = await skillService.UpdateUserSkillAsync(userId, skillId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{skillId:int}")]
    public async Task<IActionResult> DeleteSkill(int skillId, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await skillService.DeleteUserSkillAsync(userId, skillId, cancellationToken);
        return NoContent();
    }
}
