using Fleet.Server.Agents;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;

namespace Fleet.Server.Copilot;

public interface IAgentAutoExecutionDispatcher
{
    Task<AgentAutoExecutionDispatchResult> DispatchAsync(
        string projectId,
        string sessionId,
        int userId,
        IReadOnlyCollection<int> candidateWorkItemNumbers,
        string? targetBranch = null,
        string? executionPolicy = null,
        CancellationToken cancellationToken = default);
}

public sealed class AgentAutoExecutionDispatcher(
    IAgentExecutionDispatcher executionDispatcher,
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
    private static readonly HashSet<string> DispatchableStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "New",
        "Active",
    };

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> ActiveExecutionIdsBySession = new(StringComparer.Ordinal);

    public async Task<AgentAutoExecutionDispatchResult> DispatchAsync(
        string projectId,
        string sessionId,
        int userId,
        IReadOnlyCollection<int> candidateWorkItemNumbers,
        string? targetBranch = null,
        string? executionPolicy = null,
        CancellationToken cancellationToken = default)
    {
        var policy = policyOptions.Value;
        if (candidateWorkItemNumbers.Count == 0)
            return AgentAutoExecutionDispatchResult.Empty;

        var normalizedExecutionPolicy = NormalizeExecutionPolicy(executionPolicy);
        var maxAutoStartPerMessage = ResolveMaxAutoStartPerMessage(policy, normalizedExecutionPolicy);
        var maxActiveExecutionsPerSession = ResolveMaxActiveExecutionsPerSession(policy, normalizedExecutionPolicy);

        var normalizedAllowedLevels = new HashSet<string>(
            ResolveAllowedLevels(policy, normalizedExecutionPolicy)
                .Where(level => !string.IsNullOrWhiteSpace(level))
                .Select(level => level.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var levels = await workItemLevelService.GetByProjectIdAsync(projectId);
        var levelNameById = levels.ToDictionary(level => level.Id, level => level.Name);

        var uniqueCandidates = candidateWorkItemNumbers
            .Where(number => number > 0)
            .Distinct()
            .ToArray();

        var workItemsByNumber = new Dictionary<int, WorkItemDto>();
        foreach (var workItemNumber in uniqueCandidates)
        {
            var workItem = await workItemService.GetByWorkItemNumberAsync(projectId, workItemNumber);
            if (workItem is not null)
            {
                workItemsByNumber[workItemNumber] = workItem;
                await LoadCandidateAncestorsAsync(projectId, workItem, workItemsByNumber);
            }
        }

        var activeExecutions = await agentService.GetExecutionsAsync(projectId);
        var activeExecutionSet = new HashSet<int>(
            activeExecutions
                .Where(execution => string.Equals(execution.Status, "running", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(execution.Status, "queued", StringComparison.OrdinalIgnoreCase))
                .Select(execution => execution.WorkItemId));
        var activeExecutionIds = new HashSet<string>(
            activeExecutions
                .Where(execution => string.Equals(execution.Status, "running", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(execution.Status, "queued", StringComparison.OrdinalIgnoreCase))
                .Select(execution => execution.Id),
            StringComparer.Ordinal);

        var sessionActiveExecutionIds = GetOrCreateSessionExecutionSet(projectId, sessionId);
        foreach (var executionId in sessionActiveExecutionIds.Keys)
        {
            if (!activeExecutionIds.Contains(executionId))
                sessionActiveExecutionIds.TryRemove(executionId, out _);
        }

        var plannedCandidates = ResolveExecutionPlanCandidates(
            uniqueCandidates,
            workItemsByNumber,
            normalizedAllowedLevels,
            levelNameById,
            normalizedExecutionPolicy);
        var orderedCandidates = OrderCandidatesParentsFirst(plannedCandidates, workItemsByNumber);
        var plannedCandidateSet = plannedCandidates.ToHashSet();
        var activeOrStartedCandidateNumbers = activeExecutionSet
            .Where(plannedCandidateSet.Contains)
            .ToHashSet();
        var results = new List<AgentAutoExecutionWorkItemResult>(uniqueCandidates.Length);
        var startedExecutionIds = new List<string>();
        var startedCount = 0;

        foreach (var workItemNumber in orderedCandidates)
        {
            if (!workItemsByNumber.TryGetValue(workItemNumber, out var workItem))
            {
                results.Add(new AgentAutoExecutionWorkItemResult(workItemNumber, "skipped", "Work item was not found."));
                SkippedPolicyCounter.Add(1);
                continue;
            }

            if (TryResolveActiveCandidateAncestor(workItem, workItemsByNumber, activeOrStartedCandidateNumbers, out var parentWorkItemNumber))
            {
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "covered",
                    $"Covered by parent work item #{parentWorkItemNumber}; it will run as part of that parent execution."));
                continue;
            }

            if (!DispatchableStates.Contains(workItem.State))
            {
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "skipped",
                    $"Skipped by policy: state '{workItem.State}' is not eligible for auto-start."));
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
                activeOrStartedCandidateNumbers.Add(workItemNumber);
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "skipped",
                    "Skipped start: execution already running or queued for this work item."));
                SkippedPolicyCounter.Add(1);
                continue;
            }

            var activeInSessionCount = sessionActiveExecutionIds.Count;
            if (maxActiveExecutionsPerSession > 0 && activeInSessionCount >= maxActiveExecutionsPerSession)
            {
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "skipped",
                    $"Skipped by policy: session already has {activeInSessionCount} active auto-start execution(s) (limit {maxActiveExecutionsPerSession})."));
                SkippedPolicyCounter.Add(1);
                continue;
            }

            if (maxAutoStartPerMessage > 0 && startedCount >= maxAutoStartPerMessage)
            {
                results.Add(new AgentAutoExecutionWorkItemResult(
                    workItemNumber,
                    "skipped",
                    $"Skipped by policy: message auto-start limit reached ({maxAutoStartPerMessage})."));
                SkippedPolicyCounter.Add(1);
                continue;
            }

            var willQueueForSharedCapacity = !agentCallCapacityManager.TryAcquire(out var lease);
            lease?.Dispose();

            try
            {
                var executionId = await executionDispatcher.DispatchWorkItemAsync(
                    projectId,
                    workItemNumber,
                    userId,
                    targetBranch,
                    sessionId,
                    cancellationToken: cancellationToken);

                startedCount++;
                startedExecutionIds.Add(executionId);
                sessionActiveExecutionIds.TryAdd(executionId, 0);
                activeExecutionSet.Add(workItemNumber);
                activeOrStartedCandidateNumbers.Add(workItemNumber);
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
                    "skipped",
                    $"Skipped start due to orchestration limits: {ex.Message}"));
            }
            catch (OperationCanceledException)
            {
                throw;
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
                    BuildStartFailureMessage(ex)));
            }
        }

        return new AgentAutoExecutionDispatchResult(startedExecutionIds, results);
    }

    private async Task LoadCandidateAncestorsAsync(
        string projectId,
        WorkItemDto workItem,
        IDictionary<int, WorkItemDto> workItemsByNumber)
    {
        var visited = new HashSet<int>();
        var current = workItem;

        while (current.ParentWorkItemNumber is { } parentWorkItemNumber && visited.Add(parentWorkItemNumber))
        {
            if (!workItemsByNumber.TryGetValue(parentWorkItemNumber, out var parent))
            {
                parent = await workItemService.GetByWorkItemNumberAsync(projectId, parentWorkItemNumber);
                if (parent is null)
                    return;

                workItemsByNumber[parentWorkItemNumber] = parent;
            }

            current = parent;
        }
    }

    private static string BuildStartFailureMessage(Exception exception)
    {
        var message = RedactSensitiveFailureMessage(exception.Message.SanitizeForLogging());
        return string.IsNullOrWhiteSpace(message)
            ? "Failed to start execution automatically due to an internal error."
            : $"Failed to start execution automatically: {message}";
    }

    private static string RedactSensitiveFailureMessage(string message)
        => Regex.Replace(
            message,
            @"https://[^@\s]+@",
            "https://***@",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private static int ResolveMaxAutoStartPerMessage(
        AgentAutoExecutionDispatchPolicyOptions policy,
        string? executionPolicy)
    {
        if (string.Equals(executionPolicy, "sequential", StringComparison.OrdinalIgnoreCase))
            return 1;

        if (IsDynamicExecutionPolicy(executionPolicy))
            return policy.MaxDynamicAutoStartPerMessage;

        return policy.MaxAutoStartPerMessage;
    }

    private static int ResolveMaxActiveExecutionsPerSession(
        AgentAutoExecutionDispatchPolicyOptions policy,
        string? executionPolicy)
    {
        if (string.Equals(executionPolicy, "sequential", StringComparison.OrdinalIgnoreCase))
            return 1;

        if (IsDynamicExecutionPolicy(executionPolicy))
            return policy.MaxDynamicActiveExecutionsPerSession;

        return policy.MaxActiveExecutionsPerSession;
    }

    private static string[] ResolveAllowedLevels(
        AgentAutoExecutionDispatchPolicyOptions policy,
        string? executionPolicy)
        => IsDynamicExecutionPolicy(executionPolicy)
            ? policy.DynamicAllowedLevels ?? []
            : policy.AllowedLevels ?? [];

    private static bool IsDynamicExecutionPolicy(string? executionPolicy)
        => string.Equals(executionPolicy, "balanced", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(executionPolicy, "parallel", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(executionPolicy, "sequential", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeExecutionPolicy(string? executionPolicy)
    {
        var normalized = executionPolicy?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToLowerInvariant();
    }

    private static int[] ResolveExecutionPlanCandidates(
        IReadOnlyCollection<int> candidates,
        IReadOnlyDictionary<int, WorkItemDto> workItemsByNumber,
        ISet<string> allowedLevels,
        IReadOnlyDictionary<int, string> levelNameById,
        string? executionPolicy)
    {
        if (!IsDynamicExecutionPolicy(executionPolicy))
            return candidates.ToArray();

        var candidateSet = candidates.ToHashSet();
        var executionRoots = new HashSet<int>();
        foreach (var candidate in candidates)
        {
            if (!workItemsByNumber.TryGetValue(candidate, out var workItem))
            {
                executionRoots.Add(candidate);
                continue;
            }

            executionRoots.Add(ResolveInitialDynamicExecutionRoot(
                workItem,
                candidateSet,
                workItemsByNumber,
                allowedLevels,
                levelNameById));
        }

        PromoteSiblingRoots(executionRoots, workItemsByNumber, allowedLevels, levelNameById);
        executionRoots.UnionWith(candidates);
        return executionRoots.ToArray();
    }

    private static int ResolveInitialDynamicExecutionRoot(
        WorkItemDto workItem,
        ISet<int> candidateSet,
        IReadOnlyDictionary<int, WorkItemDto> workItemsByNumber,
        ISet<string> allowedLevels,
        IReadOnlyDictionary<int, string> levelNameById)
    {
        var topmostCandidateAncestor = ResolveTopmostCandidateAncestor(workItem, candidateSet, workItemsByNumber);
        if (topmostCandidateAncestor != workItem.WorkItemNumber)
            return topmostCandidateAncestor;

        if (!IsTaskLevel(workItem, levelNameById))
            return workItem.WorkItemNumber;

        var visited = new HashSet<int>();
        var current = workItem;
        while (current.ParentWorkItemNumber is { } parentWorkItemNumber &&
               workItemsByNumber.TryGetValue(parentWorkItemNumber, out var parent) &&
               visited.Add(parentWorkItemNumber))
        {
            if (!IsTaskLevel(parent, levelNameById) &&
                IsDispatchableWorkItem(parent, allowedLevels, levelNameById))
            {
                return parent.WorkItemNumber;
            }

            current = parent;
        }

        return workItem.WorkItemNumber;
    }

    private static int ResolveTopmostCandidateAncestor(
        WorkItemDto workItem,
        ISet<int> candidateSet,
        IReadOnlyDictionary<int, WorkItemDto> workItemsByNumber)
    {
        var selected = workItem.WorkItemNumber;
        var visited = new HashSet<int>();
        var current = workItem;

        while (current.ParentWorkItemNumber is { } parentWorkItemNumber &&
               workItemsByNumber.TryGetValue(parentWorkItemNumber, out var parent) &&
               visited.Add(parentWorkItemNumber))
        {
            if (candidateSet.Contains(parentWorkItemNumber))
                selected = parentWorkItemNumber;

            current = parent;
        }

        return selected;
    }

    private static void PromoteSiblingRoots(
        ISet<int> executionRoots,
        IReadOnlyDictionary<int, WorkItemDto> workItemsByNumber,
        ISet<string> allowedLevels,
        IReadOnlyDictionary<int, string> levelNameById)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            var promotionGroups = executionRoots
                .Select(rootNumber => workItemsByNumber.TryGetValue(rootNumber, out var workItem) ? workItem : null)
                .Where(workItem => workItem?.ParentWorkItemNumber is not null)
                .GroupBy(workItem => workItem!.ParentWorkItemNumber!.Value)
                .Where(group => group.Count() > 1)
                .ToArray();

            foreach (var group in promotionGroups)
            {
                if (!workItemsByNumber.TryGetValue(group.Key, out var parent) ||
                    !IsDispatchableWorkItem(parent, allowedLevels, levelNameById))
                {
                    continue;
                }

                foreach (var child in group)
                {
                    if (child is not null)
                        executionRoots.Remove(child.WorkItemNumber);
                }

                executionRoots.Add(parent.WorkItemNumber);
                changed = true;
            }
        }
    }

    private static bool IsDispatchableWorkItem(
        WorkItemDto workItem,
        ISet<string> allowedLevels,
        IReadOnlyDictionary<int, string> levelNameById)
    {
        if (!DispatchableStates.Contains(workItem.State))
            return false;

        if (allowedLevels.Count == 0)
            return true;

        var levelName = workItem.LevelId.HasValue && levelNameById.TryGetValue(workItem.LevelId.Value, out var resolvedLevel)
            ? resolvedLevel
            : null;

        return !string.IsNullOrWhiteSpace(levelName) && allowedLevels.Contains(levelName);
    }

    private static bool IsTaskLevel(
        WorkItemDto workItem,
        IReadOnlyDictionary<int, string> levelNameById)
        => workItem.LevelId.HasValue &&
           levelNameById.TryGetValue(workItem.LevelId.Value, out var levelName) &&
           string.Equals(levelName, "Task", StringComparison.OrdinalIgnoreCase);

    private static int[] OrderCandidatesParentsFirst(
        IReadOnlyCollection<int> candidates,
        IReadOnlyDictionary<int, WorkItemDto> workItemsByNumber)
    {
        var candidateNumberSet = candidates.ToHashSet();
        var depthByNumber = new Dictionary<int, int>();

        return candidates
            .OrderBy(number => ResolveCandidateAncestorDepth(number, workItemsByNumber, candidateNumberSet, depthByNumber, []))
            .ThenBy(number => number)
            .ToArray();
    }

    private static int ResolveCandidateAncestorDepth(
        int workItemNumber,
        IReadOnlyDictionary<int, WorkItemDto> workItemsByNumber,
        ISet<int> candidateNumberSet,
        IDictionary<int, int> depthByNumber,
        HashSet<int> visited)
    {
        if (depthByNumber.TryGetValue(workItemNumber, out var depth))
            return depth;

        if (!visited.Add(workItemNumber) ||
            !workItemsByNumber.TryGetValue(workItemNumber, out var workItem) ||
            workItem.ParentWorkItemNumber is not { } parentWorkItemNumber ||
            !candidateNumberSet.Contains(parentWorkItemNumber))
        {
            depthByNumber[workItemNumber] = 0;
            return 0;
        }

        var resolvedDepth = 1 + ResolveCandidateAncestorDepth(
            parentWorkItemNumber,
            workItemsByNumber,
            candidateNumberSet,
            depthByNumber,
            visited);
        depthByNumber[workItemNumber] = resolvedDepth;
        return resolvedDepth;
    }

    private static bool TryResolveActiveCandidateAncestor(
        WorkItemDto workItem,
        IReadOnlyDictionary<int, WorkItemDto> workItemsByNumber,
        ISet<int> activeOrStartedCandidateNumbers,
        out int parentWorkItemNumber)
    {
        var visited = new HashSet<int>();
        var current = workItem;

        while (current.ParentWorkItemNumber is { } candidateParentNumber &&
               workItemsByNumber.TryGetValue(candidateParentNumber, out var parent) &&
               visited.Add(candidateParentNumber))
        {
            if (activeOrStartedCandidateNumbers.Contains(candidateParentNumber))
            {
                parentWorkItemNumber = candidateParentNumber;
                return true;
            }

            current = parent;
        }

        parentWorkItemNumber = 0;
        return false;
    }

    private static ConcurrentDictionary<string, byte> GetOrCreateSessionExecutionSet(string projectId, string sessionId)
        => ActiveExecutionIdsBySession.GetOrAdd(
            $"{projectId}::{sessionId}",
            _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
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
        var covered = WorkItems.Count(item => string.Equals(item.Status, "covered", StringComparison.OrdinalIgnoreCase));
        var skipped = WorkItems.Count(item => string.Equals(item.Status, "skipped", StringComparison.OrdinalIgnoreCase));
        var failed = WorkItems.Count(item => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase));

        return $"Auto-dispatch: {started} started, {queued} queued, {covered} covered, {skipped} skipped, {failed} failed.";
    }
}

public sealed record AgentAutoExecutionWorkItemResult(
    int WorkItemNumber,
    string Status,
    string Reason,
    string? ExecutionId = null);
