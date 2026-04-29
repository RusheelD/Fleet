using Fleet.Server.Agents;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Fleet.Server.Copilot;

public interface IAgentAutoExecutionDispatcher
{
    Task<AgentAutoExecutionDispatchResult> DispatchAsync(
        string projectId,
        string sessionId,
        int userId,
        IReadOnlyCollection<int> candidateWorkItemNumbers,
        CancellationToken cancellationToken = default);
}

public sealed class AgentAutoExecutionDispatcher(
    IAgentOrchestrationService orchestrationService,
    IAgentService agentService,
    IWorkItemService workItemService,
    IWorkItemLevelService workItemLevelService,
    IOptions<AgentAutoExecutionDispatchPolicyOptions> policyOptions,
    AgentCallCapacityManager agentCallCapacityManager,
    ILogger<AgentAutoExecutionDispatcher> logger) : IAgentAutoExecutionDispatcher
{
    private static readonly Meter Meter = new("Fleet.UsageMetrics");
    private static readonly Counter<long> EligibleCounter = Meter.CreateCounter<long>("fleet.agent_auto_dispatch.eligible");
    private static readonly Counter<long> StartedCounter = Meter.CreateCounter<long>("fleet.agent_auto_dispatch.started");
    private static readonly Counter<long> SkippedPolicyCounter = Meter.CreateCounter<long>("fleet.agent_auto_dispatch.skipped_policy");
    private static readonly Counter<long> FailedStartCounter = Meter.CreateCounter<long>("fleet.agent_auto_dispatch.failed_start");

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> ActiveExecutionIdsBySession = new(StringComparer.Ordinal);

    public async Task<AgentAutoExecutionDispatchResult> DispatchAsync(
        string projectId,
        string sessionId,
        int userId,
        IReadOnlyCollection<int> candidateWorkItemNumbers,
        CancellationToken cancellationToken = default)
    {
        var policy = policyOptions.Value;
        if (candidateWorkItemNumbers.Count == 0)
            return AgentAutoExecutionDispatchResult.Empty;

        var normalizedAllowedLevels = new HashSet<string>(
            policy.AllowedLevels
                .Where(level => !string.IsNullOrWhiteSpace(level))
                .Select(level => level.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var levels = await workItemLevelService.GetByProjectIdAsync(projectId);
        var levelNameById = levels.ToDictionary(level => level.Id, level => level.Name);

        var uniqueCandidates = candidateWorkItemNumbers
            .Where(number => number > 0)
            .Distinct()
            .ToArray();

        var activeExecutions = await agentService.GetExecutionsAsync(projectId);
        var activeExecutionSet = new HashSet<int>(
            activeExecutions
                .Where(execution => string.Equals(execution.Status, "running", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(execution.Status, "queued", StringComparison.OrdinalIgnoreCase))
                .Select(execution => execution.WorkItemId));

        var sessionActiveExecutionIds = GetOrCreateSessionExecutionSet(sessionId);
        var results = new List<AgentAutoExecutionWorkItemResult>(uniqueCandidates.Length);
        var startedExecutionIds = new List<string>();
        var startedCount = 0;

        foreach (var workItemNumber in uniqueCandidates)
        {
            var workItem = await workItemService.GetByWorkItemNumberAsync(projectId, workItemNumber);
            if (workItem is null)
            {
                results.Add(new AgentAutoExecutionWorkItemResult(workItemNumber, "skipped", "Work item was not found."));
                SkippedPolicyCounter.Add(1);
                continue;
            }

            var levelName = workItem.LevelId.HasValue && levelNameById.TryGetValue(workItem.LevelId.Value, out var resolvedLevel)
                ? resolvedLevel
                : null;
            if (normalizedAllowedLevels.Count > 0 &&
                (string.IsNullOrWhiteSpace(levelName) || !normalizedAllowedLevels.Contains(levelName)))
            {
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "skipped",
                    $"Skipped by policy: level '{levelName ?? "Unspecified"}' is not eligible for auto-start."));
                SkippedPolicyCounter.Add(1);
                continue;
            }

            EligibleCounter.Add(1);

            if (activeExecutionSet.Contains(workItemNumber))
            {
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "queued",
                    "Skipped start: execution already running or queued for this work item."));
                continue;
            }

            var activeInSessionCount = sessionActiveExecutionIds.Count;
            if (policy.MaxActiveExecutionsPerSession > 0 && activeInSessionCount >= policy.MaxActiveExecutionsPerSession)
            {
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "skipped",
                    $"Skipped by policy: session already has {activeInSessionCount} active auto-start execution(s) (limit {policy.MaxActiveExecutionsPerSession})."));
                SkippedPolicyCounter.Add(1);
                continue;
            }

            if (policy.MaxAutoStartPerMessage > 0 && startedCount >= policy.MaxAutoStartPerMessage)
            {
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "skipped",
                    $"Skipped by policy: message auto-start limit reached ({policy.MaxAutoStartPerMessage})."));
                SkippedPolicyCounter.Add(1);
                continue;
            }

            var willQueueForSharedCapacity = !agentCallCapacityManager.TryAcquire(out var lease);
            lease?.Dispose();

            try
            {
                var executionId = await orchestrationService.StartExecutionAsync(
                    projectId,
                    workItemNumber,
                    userId,
                    cancellationToken);

                startedCount++;
                startedExecutionIds.Add(executionId);
                sessionActiveExecutionIds.TryAdd(executionId, 0);
                activeExecutionSet.Add(workItemNumber);
                StartedCounter.Add(1);
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    willQueueForSharedCapacity ? "queued" : "started",
                    willQueueForSharedCapacity
                        ? "Started and queued due to shared agent-call capacity limits."
                        : "Started automatically.",
                    executionId));
            }
            catch (InvalidOperationException ex)
            {
                FailedStartCounter.Add(1);
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "queued",
                    $"Skipped start due to orchestration limits: {ex.Message}"));
            }
            catch (Exception ex)
            {
                FailedStartCounter.Add(1);
                logger.LogWarning(
                    ex,
                    "Failed to auto-start execution for work item {WorkItemNumber} in project {ProjectId}.",
                    workItemNumber,
                    projectId.SanitizeForLogging());
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "failed",
                    "Failed to start execution automatically due to an internal error."));
            }
        }

        return new AgentAutoExecutionDispatchResult(startedExecutionIds, results);
    }

    private static ConcurrentDictionary<string, byte> GetOrCreateSessionExecutionSet(string sessionId)
        => ActiveExecutionIdsBySession.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
}

public sealed record AgentAutoExecutionDispatchResult(
    IReadOnlyList<string> StartedExecutionIds,
    IReadOnlyList<AgentAutoExecutionWorkItemResult> WorkItems)
{
    public static AgentAutoExecutionDispatchResult Empty { get; } = new([], []);

    public bool HasOutcome => WorkItems.Count > 0;

    public string BuildSummary()
    {
        if (WorkItems.Count == 0)
            return "No eligible work items were found for auto-start.";

        var started = WorkItems.Count(item => string.Equals(item.Status, "started", StringComparison.OrdinalIgnoreCase));
        var queued = WorkItems.Count(item => string.Equals(item.Status, "queued", StringComparison.OrdinalIgnoreCase));
        var skipped = WorkItems.Count(item => string.Equals(item.Status, "skipped", StringComparison.OrdinalIgnoreCase));
        var failed = WorkItems.Count(item => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase));

        return $"Auto-dispatch: {started} started, {queued} queued, {skipped} skipped, {failed} failed.";
    }
}

public sealed record AgentAutoExecutionWorkItemResult(
    int WorkItemNumber,
    string Status,
    string Reason,
    string? ExecutionId = null);
