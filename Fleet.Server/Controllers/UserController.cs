using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.Users;
using Fleet.Server.Memories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UserController(IUserService userService, IAuthService authService, IMemoryService memoryService) : ControllerBase
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

    [HttpGet("memories")]
    public async Task<IActionResult> GetMemories(CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var memories = await memoryService.GetUserMemoriesAsync(userId, cancellationToken);
        return Ok(memories);
    }

    [HttpPost("memories")]
    public async Task<IActionResult> CreateMemory([FromBody] UpsertMemoryEntryRequest request, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var created = await memoryService.CreateUserMemoryAsync(userId, request, cancellationToken);
        return Ok(created);
    }

    [HttpPut("memories/{memoryId:int}")]
    public async Task<IActionResult> UpdateMemory(int memoryId, [FromBody] UpsertMemoryEntryRequest request, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var updated = await memoryService.UpdateUserMemoryAsync(userId, memoryId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("memories/{memoryId:int}")]
    public async Task<IActionResult> DeleteMemory(int memoryId, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await memoryService.DeleteUserMemoryAsync(userId, memoryId, cancellationToken);
        return NoContent();
    }
}
