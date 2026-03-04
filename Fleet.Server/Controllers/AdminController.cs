using Fleet.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AdminController(FleetDbContext dbContext) : ControllerBase
{
    /// <summary>
    /// Applies any pending EF Core migrations without dropping the database.
    /// </summary>
    [HttpGet("migrate")]
    public async Task<IActionResult> Migrate()
    {
        var pending = await dbContext.Database.GetPendingMigrationsAsync();
        var pendingList = pending.ToList();

        await dbContext.Database.MigrateAsync();

        return Ok(new { message = "Migrations applied.", applied = pendingList });
    }
}
