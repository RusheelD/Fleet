using Fleet.Server.Skills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/skill-templates")]
public class SkillTemplatesController(ISkillService skillService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        var templates = await skillService.GetTemplatesAsync(cancellationToken);
        return Ok(templates);
    }
}
