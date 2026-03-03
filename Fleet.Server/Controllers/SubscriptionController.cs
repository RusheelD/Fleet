using Fleet.Server.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SubscriptionController(ISubscriptionService subscriptionService) : ControllerBase
{
    [HttpGet]
    [OutputCache(Duration = 5)]
    public async Task<IActionResult> Get()
    {
        var data = await subscriptionService.GetSubscriptionDataAsync();
        return Ok(data);
    }
}
