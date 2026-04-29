using Fleet.Server.Auth;
using Fleet.Server.Models;
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

    [HttpGet("login-identities")]
    public async Task<IActionResult> GetLoginIdentities()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var identities = await authService.GetLoginIdentitiesAsync(userId);
        return Ok(identities);
    }

    [HttpPost("login-identities/link-state")]
    public async Task<IActionResult> CreateLoginProviderLinkState([FromBody] CreateLoginProviderLinkRequest request)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var state = await authService.CreateLoginProviderLinkStateAsync(userId, request.Provider);
        return Ok(state);
    }

    [HttpPost("login-identities/complete-link")]
    public async Task<IActionResult> CompleteLoginProviderLink([FromBody] CompleteLoginProviderLinkRequest request)
    {
        var profile = await authService.CompleteLoginProviderLinkAsync(request.State);
        return Ok(profile);
    }

    [HttpDelete("login-identities/{identityId:int}")]
    public async Task<IActionResult> DeleteLoginIdentity(int identityId)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await authService.DeleteLoginIdentityAsync(userId, identityId);
        return NoContent();
    }
}
