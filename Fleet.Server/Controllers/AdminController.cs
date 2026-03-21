using Fleet.Server.Data;
using Fleet.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/[controller]")]
public class AdminController(FleetDbContext dbContext, FleetDatabaseMigrator databaseMigrator) : ControllerBase
{
    /// <summary>
    /// Applies any pending EF Core migrations without dropping the database.
    /// </summary>
    [HttpGet("migrate")]
    public async Task<IActionResult> Migrate()
    {
        var pendingList = await databaseMigrator.GetPendingMigrationsAsync();

        await databaseMigrator.MigrateAsync();

        return Ok(new { message = "Migrations applied.", applied = pendingList });
    }

    /// <summary>
    /// Sets a user's subscription tier role.
    /// </summary>
    [HttpPut("users/{userId:int}/tier")]
    public async Task<IActionResult> SetUserTier(int userId, [FromBody] SetUserTierRequest request)
    {
        if (!UserRoles.IsValid(request.Tier))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = $"Tier must be one of: {string.Join(", ", UserRoles.All)}.",
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext?.Request?.Path.ToString() ?? $"/api/admin/users/{userId}/tier",
            });
        }

        var user = await dbContext.UserProfiles.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return NotFound();

        user.Role = UserRoles.Normalize(request.Tier);
        await dbContext.SaveChangesAsync();

        return Ok(new
        {
            userId = user.Id,
            tier = user.Role,
        });
    }
}
