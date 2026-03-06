using Fleet.Server.Subscriptions;
using Fleet.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SubscriptionController(ISubscriptionService subscriptionService, IAuthService authService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var data = await subscriptionService.GetSubscriptionDataAsync(userId);
        return Ok(data);
    }
}
