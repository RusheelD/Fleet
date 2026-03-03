using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UserController(IUserService userService, IAuthService authService) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var settings = await userService.GetSettingsAsync(userId);
        return Ok(settings);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var profile = await userService.UpdateProfileAsync(userId, request);
        return Ok(profile);
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UserPreferencesDto preferences)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var updated = await userService.UpdatePreferencesAsync(userId, preferences);
        return Ok(updated);
    }
}
