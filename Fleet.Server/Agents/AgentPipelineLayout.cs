using Fleet.Server.Models;

namespace Fleet.Server.Agents;

internal static class AgentPipelineLayout
{
    internal const int MaxPlannerRoleCopies = 3;

    internal static readonly AgentRole[][] FullPipeline =
    [
        [AgentRole.Manager],
        [AgentRole.Planner],
        [AgentRole.Contracts],
        [AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling],
        [AgentRole.Consolidation],
        [AgentRole.Review, AgentRole.Documentation],
    ];

    internal static readonly AgentRole[][] OrchestrationPreludePipeline =
    [
        [AgentRole.Manager],
        [AgentRole.Planner],
        [AgentRole.Contracts],
    ];

    internal static readonly AgentRole[][] CoordinatorPipeline =
    [
        [AgentRole.Manager],
        [AgentRole.Research],
        [AgentRole.Planner],
        [AgentRole.Contracts],
        [AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling],
        [AgentRole.Consolidation],
        [AgentRole.Review, AgentRole.Documentation],
    ];

    internal static AgentRole[][] ArrangePipeline(IEnumerable<AgentRole> roles)
    {
        var counts = roles
            .GroupBy(role => role)
            .ToDictionary(
                group => group.Key,
                group => Math.Min(group.Count(), group.Key is AgentRole.Manager or AgentRole.Planner ? 1 : MaxPlannerRoleCopies));

        counts[AgentRole.Manager] = 1;
        counts[AgentRole.Planner] = 1;

        var pipeline = new List<AgentRole[]>
        {
            new[] { AgentRole.Manager },
            new[] { AgentRole.Planner },
        };

        if (counts.TryGetValue(AgentRole.Research, out var researchCount) && researchCount > 0)
            pipeline.Add(Enumerable.Repeat(AgentRole.Research, researchCount).ToArray());

        foreach (var role in new[] { AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling })
        {
            if (!counts.TryGetValue(role, out var count) || count <= 0)
                continue;

            pipeline.Add(Enumerable.Repeat(role, count).ToArray());
        }

        if (counts.TryGetValue(AgentRole.Consolidation, out var consolidationCount) &&
            consolidationCount > 0 &&
            counts.TryGetValue(AgentRole.Backend, out var backendCount) &&
            backendCount > 0 &&
            counts.TryGetValue(AgentRole.Frontend, out var frontendCount) &&
            frontendCount > 0)
        {
            pipeline.Add(Enumerable.Repeat(AgentRole.Consolidation, consolidationCount).ToArray());
        }

        foreach (var role in new[] { AgentRole.Review, AgentRole.Documentation })
        {
            if (!counts.TryGetValue(role, out var count) || count <= 0)
                continue;

            pipeline.Add(Enumerable.Repeat(role, count).ToArray());
        }

        if (RequiresContractsForDirectPipeline([.. pipeline]))
        {
            var contractsInsertionIndex = pipeline.FindLastIndex(group => group.Contains(AgentRole.Research));
            pipeline.Insert(contractsInsertionIndex >= 0 ? contractsInsertionIndex + 1 : 2, [AgentRole.Contracts]);
        }

        return [.. pipeline];
    }

    internal static AgentRole[][] ResolveDefaultPipeline(string? executionMode)
        => executionMode switch
        {
            AgentExecutionModes.Orchestration => OrchestrationPreludePipeline,
            AgentExecutionModes.Coordinator => CoordinatorPipeline,
            _ => FullPipeline,
        };

    internal static AgentRole[][] BuildPipelineFromFollowingRoles(
        IReadOnlyCollection<AgentRole> followingRoles,
        string? assignmentMode,
        int? assignedAgentCount)
    {
        var arranged = ArrangePipeline(followingRoles);
        return ApplyAssignedAgentLimit(arranged, assignmentMode, assignedAgentCount);
    }

    internal static AgentRole[][] BuildOrchestrationPipelineFromFollowingRoles(
        IReadOnlyCollection<AgentRole> followingRoles,
        string? assignmentMode,
        int? assignedAgentCount)
    {
        var counts = followingRoles
            .Where(role => role is not AgentRole.Manager and not AgentRole.Planner)
            .GroupBy(role => role)
            .ToDictionary(
                group => group.Key,
                group => Math.Min(group.Count(), group.Key is AgentRole.Contracts or AgentRole.Consolidation ? 1 : MaxPlannerRoleCopies));

        counts[AgentRole.Contracts] = 1;
        counts[AgentRole.Consolidation] = 1;

        var pipeline = new List<AgentRole[]>
        {
            new[] { AgentRole.Manager },
            new[] { AgentRole.Planner },
            new[] { AgentRole.Contracts },
        };

        var implementationGroup = new List<AgentRole>();
        foreach (var role in new[] { AgentRole.Backend, AgentRole.Frontend, AgentRole.Styling })
        {
            if (!counts.TryGetValue(role, out var count) || count <= 0)
                continue;

            implementationGroup.AddRange(Enumerable.Repeat(role, count));
        }

        if (implementationGroup.Count > 0)
            pipeline.Add([.. implementationGroup]);

        pipeline.Add([AgentRole.Consolidation]);

        if (counts.TryGetValue(AgentRole.Testing, out var testingCount) && testingCount > 0)
            pipeline.Add(Enumerable.Repeat(AgentRole.Testing, testingCount).ToArray());

        var reviewGroup = new List<AgentRole>();
        foreach (var role in new[] { AgentRole.Review, AgentRole.Documentation })
        {
            if (!counts.TryGetValue(role, out var count) || count <= 0)
                continue;

            reviewGroup.AddRange(Enumerable.Repeat(role, count));
        }

        if (reviewGroup.Count > 0)
            pipeline.Add([.. reviewGroup]);

        return ApplyAssignedAgentLimitPreservingRoles(
            [.. pipeline],
            assignmentMode,
            assignedAgentCount,
            [AgentRole.Contracts, AgentRole.Consolidation]);
    }

    internal static AgentRole[][] EnsureContractsInOrchestrationPipeline(
        AgentRole[][] pipeline,
        string? executionMode)
    {
        if (!string.Equals(executionMode, AgentExecutionModes.Orchestration, StringComparison.OrdinalIgnoreCase))
            return pipeline;

        var requestedRoles = pipeline
            .SelectMany(group => group)
            .Where(role => role is not AgentRole.Manager and not AgentRole.Planner)
            .ToArray();

        return BuildOrchestrationPipelineFromFollowingRoles(
            requestedRoles,
            assignmentMode: null,
            assignedAgentCount: null);
    }

    internal static IReadOnlyList<AgentRole> NormalizePlannerFollowingRoles(
        IReadOnlyList<AgentRole> plannerFollowingRoles,
        IReadOnlyList<AgentRole> deterministicDirectExecutionRoles)
    {
        var roles = plannerFollowingRoles
            .Where(role => role is not AgentRole.Manager and not AgentRole.Planner)
            .GroupBy(role => role)
            .SelectMany(group => Enumerable.Repeat(group.Key, Math.Min(group.Count(), MaxPlannerRoleCopies)))
            .ToList();
        if (roles.Count == 0)
        {
            roles = deterministicDirectExecutionRoles
                .Where(role => role is not AgentRole.Manager and not AgentRole.Planner)
                .ToList();
        }

        var backendCount = roles.Count(role => role == AgentRole.Backend);
        var frontendCount = roles.Count(role => role == AgentRole.Frontend);
        roles.RemoveAll(role =>
            role == AgentRole.Consolidation &&
            (backendCount == 0 || frontendCount == 0));

        return roles;
    }

    internal static IReadOnlyList<AgentRole> NormalizeOrchestrationFollowingRoles(
        IReadOnlyList<AgentRole> plannerFollowingRoles,
        IReadOnlyList<AgentRole> fallbackOrchestrationFollowingRoles)
    {
        var roles = NormalizePlannerFollowingRoles(plannerFollowingRoles, fallbackOrchestrationFollowingRoles)
            .Where(role => role is not AgentRole.Manager and not AgentRole.Planner)
            .ToList();

        if (!roles.Contains(AgentRole.Contracts))
            roles.Insert(0, AgentRole.Contracts);
        if (!roles.Contains(AgentRole.Consolidation))
        {
            var contractsIndex = roles.IndexOf(AgentRole.Contracts);
            roles.Insert(contractsIndex >= 0 ? contractsIndex + 1 : 0, AgentRole.Consolidation);
        }

        return roles;
    }

    internal static IReadOnlyList<AgentRole> BuildDeterministicOrchestrationFollowingRoles(
        IReadOnlyList<AgentRole> directExecutionRoles,
        string? assignmentMode,
        int? assignedAgentCount)
    {
        var orchestrationRoles = directExecutionRoles
            .Where(role => role is AgentRole.Testing or AgentRole.Consolidation or AgentRole.Review or AgentRole.Documentation)
            .ToList();
        orchestrationRoles.Insert(0, AgentRole.Contracts);

        return BuildOrchestrationPipelineFromFollowingRoles(
                orchestrationRoles,
                assignmentMode,
                assignedAgentCount)
            .SelectMany(group => group)
            .Where(role => role is not AgentRole.Manager and not AgentRole.Planner)
            .ToArray();
    }

    internal static bool RequiresContractsForDirectPipeline(AgentRole[][] pipeline)
        => pipeline.Any(group => group.Count(IsContractsSensitiveDirectRole) > 1);

    private static bool IsContractsSensitiveDirectRole(AgentRole role)
        => role is AgentRole.Backend or AgentRole.Frontend or AgentRole.Testing or AgentRole.Styling;

    internal static AgentRole[][] ApplyAssignedAgentLimit(AgentRole[][] pipeline, string? assignmentMode, int? assignedAgentCount)
    {
        var effectiveAssignedAgentCount = ResolveEffectiveAssignedAgentCount(assignmentMode, assignedAgentCount);
        if (effectiveAssignedAgentCount is null)
            return pipeline;

        var remainingWorkerSlots = effectiveAssignedAgentCount.Value;
        var totalWorkerSlots = pipeline
            .SelectMany(group => group)
            .Count(role => role is not AgentRole.Manager and not AgentRole.Planner);

        if (totalWorkerSlots <= remainingWorkerSlots)
            return pipeline;

        return pipeline
            .Select(group =>
            {
                var retained = new List<AgentRole>();
                foreach (var role in group)
                {
                    if (role is AgentRole.Manager or AgentRole.Planner)
                    {
                        retained.Add(role);
                        continue;
                    }

                    if (remainingWorkerSlots <= 0)
                        continue;

                    retained.Add(role);
                    remainingWorkerSlots--;
                }

                return retained.ToArray();
            })
            .Where(group => group.Length > 0)
            .ToArray();
    }

    internal static int ResolveMaxConcurrentAgentsPerTask(int tierLimit, string? assignmentMode, int? assignedAgentCount)
    {
        var effectiveAssignedAgentCount = ResolveEffectiveAssignedAgentCount(assignmentMode, assignedAgentCount);
        if (effectiveAssignedAgentCount is null)
            return tierLimit;

        return Math.Max(1, Math.Min(tierLimit, effectiveAssignedAgentCount.Value));
    }

    internal static int? ResolveEffectiveAssignedAgentCount(string? assignmentMode, int? assignedAgentCount)
    {
        if (!string.Equals(assignmentMode, "manual", StringComparison.OrdinalIgnoreCase))
            return null;

        if (assignedAgentCount is null || assignedAgentCount.Value <= 0)
            return null;

        return assignedAgentCount.Value;
    }

    internal static bool IsOrchestrationPrelude(AgentRole[][] pipeline)
    {
        if (pipeline.Length != OrchestrationPreludePipeline.Length)
            return false;

        for (var index = 0; index < pipeline.Length; index++)
        {
            if (!pipeline[index].SequenceEqual(OrchestrationPreludePipeline[index]))
                return false;
        }

        return true;
    }

    internal static bool HasOrchestrationFollowUpStages(AgentRole[][] pipeline)
    {
        var contractsGroupIndex = Array.FindIndex(pipeline, group => group.Contains(AgentRole.Contracts));
        if (contractsGroupIndex < 0)
            return false;

        return pipeline
            .Skip(contractsGroupIndex + 1)
            .SelectMany(group => group)
            .Any(role => role is not AgentRole.Manager and not AgentRole.Planner and not AgentRole.Contracts);
    }

    private static AgentRole[][] ApplyAssignedAgentLimitPreservingRoles(
        AgentRole[][] pipeline,
        string? assignmentMode,
        int? assignedAgentCount,
        IReadOnlyCollection<AgentRole> mandatoryRoles)
    {
        var effectiveAssignedAgentCount = ResolveEffectiveAssignedAgentCount(assignmentMode, assignedAgentCount);
        if (effectiveAssignedAgentCount is null)
            return pipeline;

        var mandatoryRoleSet = mandatoryRoles.ToHashSet();
        var mandatoryWorkerSlots = pipeline
            .SelectMany(group => group)
            .Count(role => role is not AgentRole.Manager and not AgentRole.Planner && mandatoryRoleSet.Contains(role));
        var remainingOptionalWorkerSlots = Math.Max(0, effectiveAssignedAgentCount.Value - mandatoryWorkerSlots);
        var totalWorkerSlots = pipeline
            .SelectMany(group => group)
            .Count(role => role is not AgentRole.Manager and not AgentRole.Planner);

        if (totalWorkerSlots <= mandatoryWorkerSlots + remainingOptionalWorkerSlots)
            return pipeline;

        return pipeline
            .Select(group =>
            {
                var retained = new List<AgentRole>();
                foreach (var role in group)
                {
                    if (role is AgentRole.Manager or AgentRole.Planner)
                    {
                        retained.Add(role);
                        continue;
                    }

                    if (mandatoryRoleSet.Contains(role))
                    {
                        retained.Add(role);
                        continue;
                    }

                    if (remainingOptionalWorkerSlots <= 0)
                        continue;

                    retained.Add(role);
                    remainingOptionalWorkerSlots--;
                }

                return retained.ToArray();
            })
            .Where(group => group.Length > 0)
            .ToArray();
    }
}
