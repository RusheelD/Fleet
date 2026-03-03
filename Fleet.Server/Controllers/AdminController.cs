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
    /// Drops the database and recreates it from migrations. Does not seed data.
    /// </summary>
    [HttpGet("reset")]
    public async Task<IActionResult> Reset()
    {
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        return Ok(new { message = "Database has been reset." });
    }

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

    /// <summary>
    /// Seeds the database if it is empty. No-op if data already exists.
    /// </summary>
    [HttpGet("seed")]
    public async Task<IActionResult> Seed()
    {
        await FleetDbSeeder.SeedAsync(dbContext);
        return Ok(new { message = "Seed complete." });
    }
}
