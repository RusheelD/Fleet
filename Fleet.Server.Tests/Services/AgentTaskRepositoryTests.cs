using Fleet.Server.Agents;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentTaskRepositoryTests
{
    private const string ProjectId = "proj-1";

    [TestMethod]
    public async Task ClearLogsByExecutionIdAsync_DeletesOnlyMatchingRunLogs()
    {
        await using var db = CreateDbContext();
        SeedProject(db);
        db.LogEntries.AddRange(
            new LogEntry { Time = "2026-03-31T00:00:00Z", Agent = "System", Level = "info", Message = "run 1", ExecutionId = "exec-1", ProjectId = ProjectId },
            new LogEntry { Time = "2026-03-31T00:00:01Z", Agent = "System", Level = "info", Message = "run 2", ExecutionId = "exec-2", ProjectId = ProjectId },
            new LogEntry { Time = "2026-03-31T00:00:02Z", Agent = "System", Level = "info", Message = "general", ProjectId = ProjectId });
        await db.SaveChangesAsync();

        var sut = new AgentTaskRepository(db);

        var deletedCount = await sut.ClearLogsByExecutionIdAsync(ProjectId, "exec-1");

        Assert.AreEqual(1, deletedCount);
        Assert.AreEqual(2, await db.LogEntries.CountAsync());
        Assert.IsFalse(await db.LogEntries.AnyAsync(log => log.ExecutionId == "exec-1"));
        Assert.IsTrue(await db.LogEntries.AnyAsync(log => log.ExecutionId == "exec-2"));
        Assert.IsTrue(await db.LogEntries.AnyAsync(log => log.ExecutionId == null));
    }

    [TestMethod]
    public async Task DeleteExecutionAsync_RemovesExecutionLogsAndPhaseResults()
    {
        await using var db = CreateDbContext();
        SeedProject(db);
        db.AgentExecutions.Add(new AgentExecution
        {
            Id = "exec-1",
            WorkItemId = 42,
            WorkItemTitle = "Fix auth",
            Status = "failed",
            StartedAt = "2026-03-31T00:00:00Z",
            Duration = "2m",
            Progress = 0.5,
            UserId = "7",
            ProjectId = ProjectId,
            Agents = [],
        });
        db.AgentPhaseResults.AddRange(
            new AgentPhaseResult
            {
                ExecutionId = "exec-1",
                Role = "Planner",
                Output = "plan",
                ToolCallCount = 1,
                Success = true,
                StartedAt = DateTime.UtcNow.AddMinutes(-2),
                CompletedAt = DateTime.UtcNow.AddMinutes(-1),
                PhaseOrder = 0,
            },
            new AgentPhaseResult
            {
                ExecutionId = "exec-1",
                Role = "Backend",
                Output = "impl",
                ToolCallCount = 2,
                Success = false,
                Error = "boom",
                StartedAt = DateTime.UtcNow.AddMinutes(-1),
                CompletedAt = DateTime.UtcNow,
                PhaseOrder = 1,
            });
        db.LogEntries.AddRange(
            new LogEntry { Time = "2026-03-31T00:00:00Z", Agent = "System", Level = "info", Message = "run 1", ExecutionId = "exec-1", ProjectId = ProjectId },
            new LogEntry { Time = "2026-03-31T00:00:01Z", Agent = "System", Level = "info", Message = "run 2", ExecutionId = "exec-2", ProjectId = ProjectId });
        await db.SaveChangesAsync();

        var sut = new AgentTaskRepository(db);

        var result = await sut.DeleteExecutionAsync(ProjectId, "exec-1");

        Assert.IsNotNull(result);
        Assert.AreEqual("exec-1", result.ExecutionId);
        Assert.AreEqual(1, result.DeletedLogCount);
        Assert.IsFalse(await db.AgentExecutions.AnyAsync(execution => execution.Id == "exec-1"));
        Assert.IsFalse(await db.AgentPhaseResults.AnyAsync(result => result.ExecutionId == "exec-1"));
        Assert.IsFalse(await db.LogEntries.AnyAsync(log => log.ExecutionId == "exec-1"));
        Assert.IsTrue(await db.LogEntries.AnyAsync(log => log.ExecutionId == "exec-2"));
    }

    private static FleetDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new FleetDbContext(options);
    }

    private static void SeedProject(FleetDbContext db)
    {
        db.Projects.Add(new Project
        {
            Id = ProjectId,
            OwnerId = "owner-1",
            Title = "Fleet",
            Slug = "fleet",
            Description = "Test project",
            Repo = "owner/repo",
            LastActivity = "2026-03-31T00:00:00Z",
        });
    }
}
