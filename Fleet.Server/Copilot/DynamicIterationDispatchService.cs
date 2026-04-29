using Fleet.Server.Models;
using System.Text.Json;

namespace Fleet.Server.Copilot;

public class DynamicIterationDispatchService(
    IAgentAutoExecutionDispatcher autoExecutionDispatcher,
    ILogger<DynamicIterationDispatchService> logger) : IDynamicIterationDispatchService
{
    private static readonly HashSet<string> SupportedMutationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_work_item",
        "update_work_item",
        "try_update_work_item",
        "bulk_create_work_items",
        "bulk_update_work_items",
        "try_bulk_update_work_items",
    };

    public async Task<DynamicIterationDispatchResult> DispatchFromToolEventsAsync(
        string projectId,
        string sessionId,
        int userId,
        IReadOnlyList<ToolEventDto> toolEvents,
        string? targetBranch,
        string? executionPolicy,
        CancellationToken cancellationToken = default)
    {
        if (toolEvents.Count == 0)
            return DynamicIterationDispatchResult.Empty;

        var candidateIds = CollectCandidateIds(toolEvents);
        if (candidateIds.Count == 0)
            return DynamicIterationDispatchResult.Empty;

        try
        {
            var dispatchResult = await autoExecutionDispatcher.DispatchAsync(
                projectId,
                sessionId,
                userId,
                candidateIds,
                targetBranch,
                executionPolicy,
                cancellationToken);

            return DynamicIterationDispatchResult.FromAutoDispatch(candidateIds.Count, dispatchResult);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Dynamic iteration dispatch failed for session {SessionId} in project {ProjectId}.",
                sessionId,
                projectId);

            return new DynamicIterationDispatchResult(
                candidateIds.Count,
                0,
                0,
                candidateIds.Count,
                1,
                ["Dynamic iteration dispatch failed before executions could be started."]);
        }
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
                CollectWorkItemIds(document.RootElement, ids);
            }
            catch
            {
                // Ignore malformed tool result payloads. Dispatch should stay best-effort.
            }
        }

        return [.. ids.OrderBy(id => id)];
    }

    private static void CollectWorkItemIds(JsonElement element, ISet<int> ids)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (HasProperty(element, "error"))
                    return;

                foreach (var property in element.EnumerateObject())
                {
                    if ((string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(property.Name, "workItemNumber", StringComparison.OrdinalIgnoreCase)) &&
                        property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var id) &&
                        id > 0)
                    {
                        ids.Add(id);
                        continue;
                    }

                    CollectWorkItemIds(property.Value, ids);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectWorkItemIds(item, ids);
                }
                break;
        }
    }

    private static bool HasProperty(JsonElement element, string propertyName)
        => element.EnumerateObject().Any(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
}
