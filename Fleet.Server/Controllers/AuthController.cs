using Fleet.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Returns the current user's profile, creating a local profile from Entra ID
    /// claims on first call. The frontend should call this after MSAL login.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var profile = await authService.GetOrCreateCurrentUserAsync();
        return Ok(profile);
    }
}
