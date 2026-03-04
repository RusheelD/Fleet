using Fleet.Server.Auth;
using Fleet.Server.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController(ISearchService searchService, IAuthService authService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? type)
    {
        var ownerId = (await authService.GetCurrentUserIdAsync()).ToString();
        var results = await searchService.SearchAsync(ownerId, q, type);
        return Ok(results);
    }
}
