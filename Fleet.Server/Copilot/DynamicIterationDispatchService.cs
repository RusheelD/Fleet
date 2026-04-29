using Fleet.Server.Agents;
using Fleet.Server.Auth;
using Fleet.Server.Data;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Fleet.Server.Copilot;

public class DynamicIterationDispatchService(
    IWorkItemService workItemService,
    IWorkItemLevelService workItemLevelService,
    IAgentOrchestrationService agentOrchestrationService,
    FleetDbContext db,
    ILogger<DynamicIterationDispatchService> logger) : IDynamicIterationDispatchService
{
    private static readonly HashSet<string> SupportedMutationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_work_item",
        "update_work_item",
        "bulk_create_work_items",
        "bulk_update_work_items",
    };

    private static readonly HashSet<string> DispatchableStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "New",
        "Active",
    };

    public async Task<DynamicIterationDispatchResult> DispatchFromToolEventsAsync(
        string projectId,
        int userId,
        IReadOnlyList<ToolEventDto> toolEvents,
        string? targetBranch,
        CancellationToken cancellationToken = default)
    {
        if (toolEvents.Count == 0)
            return DynamicIterationDispatchResult.Empty;

        var candidateIds = CollectCandidateIds(toolEvents);
        if (candidateIds.Count == 0)
            return DynamicIterationDispatchResult.Empty;

        var workItems = await workItemService.GetByProjectIdAsync(projectId);
        var byNumber = workItems.ToDictionary(item => item.WorkItemNumber);
        var levels = await workItemLevelService.GetByProjectIdAsync(projectId);
        var levelNameById = levels.ToDictionary(level => level.Id, level => level.Name);

        var userRole = await db.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == userId)
            .Select(profile => profile.Role)
            .FirstOrDefaultAsync(cancellationToken);
        var tierPolicy = TierPolicyCatalog.Get(userRole);
        var activeExecutions = await db.AgentExecutions
            .AsNoTracking()
            .CountAsync(
                execution => execution.UserId == userId.ToString() &&
                             execution.ParentExecutionId == null &&
                             execution.Status == "running",
                cancellationToken);
        var remainingCapacity = Math.Max(0, tierPolicy.MaxActiveAgentExecutions - activeExecutions);

        var accepted = new List<int>();
        var notes = new List<string>();

        foreach (var candidateId in candidateIds)
        {
            if (!byNumber.TryGetValue(candidateId, out var workItem))
            {
                notes.Add($"#{candidateId} skipped: no longer exists.");
                continue;
            }

            if (!DispatchableStates.Contains(workItem.State))
            {
                notes.Add($"#{candidateId} skipped: state '{workItem.State}' is not dispatchable.");
                continue;
            }

            if (workItem.ChildWorkItemNumbers.Length > 0)
            {
                notes.Add($"#{candidateId} skipped: only leaf items are auto-dispatched.");
                continue;
            }

            if (workItem.ParentWorkItemNumber is null)
            {
                notes.Add($"#{candidateId} skipped: parent is required for auto-dispatch.");
                continue;
            }

            if (!levelNameById.TryGetValue(workItem.LevelId ?? 0, out var levelName) || !string.Equals(levelName, "Task", StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"#{candidateId} skipped: level must be Task.");
                continue;
            }

            accepted.Add(candidateId);
        }

        var started = 0;
        var failed = 0;
        foreach (var workItemNumber in accepted.Take(remainingCapacity))
        {
            try
            {
                await agentOrchestrationService.StartExecutionAsync(
                    projectId,
                    workItemNumber,
                    userId,
                    targetBranch,
                    cancellationToken);
                started++;
            }
            catch (Exception ex)
            {
                failed++;
                notes.Add($"#{workItemNumber} failed: {ex.Message}");
                logger.LogWarning(
                    ex,
                    "Dynamic dispatch failed for work item #{WorkItemNumber} in project {ProjectId}",
                    workItemNumber,
                    projectId);
            }
        }

        if (accepted.Count > remainingCapacity)
        {
            var skippedByCapacity = accepted.Count - remainingCapacity;
            notes.Add($"{skippedByCapacity} candidate(s) skipped: active execution capacity reached.");
        }

        var skipped = Math.Max(0, candidateIds.Count - started - failed);
        return new DynamicIterationDispatchResult(
            candidateIds.Count,
            accepted.Count,
            started,
            skipped,
            failed,
            notes);
    }

    private static List<int> CollectCandidateIds(IReadOnlyList<ToolEventDto> toolEvents)
    {
        var ids = new HashSet<int>();

        foreach (var toolEvent in toolEvents)
        {
            if (!SupportedMutationTools.Contains(toolEvent.ToolName) || toolEvent.Result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var document = JsonDocument.Parse(toolEvent.Result);
                var root = document.RootElement;

                if (root.TryGetProperty("Id", out var idProperty) && idProperty.TryGetInt32(out var directId) && directId > 0)
                    ids.Add(directId);

                if (!root.TryGetProperty("Results", out var resultsProperty) || resultsProperty.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var result in resultsProperty.EnumerateArray())
                {
                    if (result.TryGetProperty("Error", out _))
                        continue;

                    if (result.TryGetProperty("Id", out var nestedIdProperty) && nestedIdProperty.TryGetInt32(out var nestedId) && nestedId > 0)
                        ids.Add(nestedId);
                }
            }
            catch
            {
                // Ignore malformed tool result payloads. Dispatch should stay best-effort.
            }
        }

        return [.. ids.OrderBy(id => id)];
    }
}
