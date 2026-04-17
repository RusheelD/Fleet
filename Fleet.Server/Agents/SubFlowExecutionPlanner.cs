using Fleet.Server.Models;

namespace Fleet.Server.Agents;

internal sealed record ExistingSubFlowDependencySpec(
    int WorkItemNumber,
    IReadOnlyList<int> DependsOnWorkItemNumbers,
    string Reason);

internal sealed record ResolvedSubFlowExecutionBatch(
    IReadOnlyList<WorkItemDto> WorkItems);

internal sealed record ResolvedSubFlowExecutionPlan(
    IReadOnlyList<ResolvedSubFlowExecutionBatch> Batches,
    IReadOnlyDictionary<int, IReadOnlyList<int>> DependenciesByWorkItemNumber,
    string? SummaryMessage = null,
    string? WarningMessage = null);

internal static class SubFlowExecutionPlanner
{
    public static ResolvedSubFlowExecutionPlan Resolve(
        IReadOnlyList<WorkItemDto> directChildWorkItems,
        string? plannerOutput)
    {
        var orderedChildren = directChildWorkItems
            .OrderBy(child => child.WorkItemNumber)
            .ToArray();
        if (orderedChildren.Length == 0)
        {
            return new ResolvedSubFlowExecutionPlan(
                [],
                new Dictionary<int, IReadOnlyList<int>>());
        }

        if (orderedChildren.Length == 1)
            return BuildFallbackPlan(orderedChildren);

        var warnings = new List<string>();
        var dependencyMap = BuildExistingDependencyMap(orderedChildren, plannerOutput, warnings) ??
                            BuildGeneratedDependencyMap(orderedChildren, plannerOutput, warnings) ??
                            BuildEmptyDependencyMap(orderedChildren);
        dependencyMap = ApplyImplicitGitHubPagesDependencies(orderedChildren, dependencyMap);

        if (!TryBuildBatches(orderedChildren, dependencyMap, out var batches))
        {
            warnings.Add("Planner sub-flow dependencies contained a cycle, so Fleet fell back to the default numeric scheduling order.");
            var fallback = BuildFallbackPlan(orderedChildren, warnings);
            return fallback;
        }

        if (batches.Count <= 1 && dependencyMap.All(entry => entry.Value.Count == 0))
            return BuildFallbackPlan(orderedChildren, warnings);

        var batchSummary = string.Join(
            " -> ",
            batches.Select(batch => string.Join(", ", batch.WorkItems.Select(child => $"#{child.WorkItemNumber}"))));

        return new ResolvedSubFlowExecutionPlan(
            batches,
            dependencyMap,
            SummaryMessage: $"Planner scheduled sub-flow dependency stages: {batchSummary}.",
            WarningMessage: warnings.Count == 0 ? null : string.Join(" ", warnings));
    }

    private static ResolvedSubFlowExecutionPlan BuildFallbackPlan(
        IReadOnlyList<WorkItemDto> orderedChildren,
        IReadOnlyList<string>? warnings = null)
        => new(
            [new ResolvedSubFlowExecutionBatch(orderedChildren)],
            BuildEmptyDependencyMap(orderedChildren),
            WarningMessage: warnings is { Count: > 0 } ? string.Join(" ", warnings) : null);

    private static Dictionary<int, IReadOnlyList<int>> BuildEmptyDependencyMap(IReadOnlyList<WorkItemDto> orderedChildren)
        => orderedChildren.ToDictionary(
            child => child.WorkItemNumber,
            _ => (IReadOnlyList<int>)[],
            EqualityComparer<int>.Default);

    private static Dictionary<int, IReadOnlyList<int>>? BuildExistingDependencyMap(
        IReadOnlyList<WorkItemDto> orderedChildren,
        string? plannerOutput,
        List<string> warnings)
    {
        var existingSpecs = PlannerExecutionShapeParser.Parse(plannerOutput)?
            .ExistingSubFlowDependencies?
            .Where(spec => spec is not null)
            .ToArray();
        if (existingSpecs is null || existingSpecs.Length == 0)
            return null;

        var knownChildren = orderedChildren
            .Select(child => child.WorkItemNumber)
            .ToHashSet();
        var dependencyMap = BuildEmptyDependencyMap(orderedChildren)
            .ToDictionary(entry => entry.Key, entry => new HashSet<int>(entry.Value));
        var hasDependency = false;

        foreach (var spec in existingSpecs)
        {
            if (!knownChildren.Contains(spec.WorkItemNumber))
            {
                warnings.Add($"Planner referenced unknown existing sub-flow #{spec.WorkItemNumber}; Fleet ignored that dependency entry.");
                continue;
            }

            foreach (var dependency in spec.DependsOnWorkItemNumbers.Distinct())
            {
                if (dependency == spec.WorkItemNumber)
                {
                    warnings.Add($"Planner made sub-flow #{spec.WorkItemNumber} depend on itself; Fleet ignored that self-dependency.");
                    continue;
                }

                if (!knownChildren.Contains(dependency))
                {
                    warnings.Add($"Planner made sub-flow #{spec.WorkItemNumber} depend on unknown sub-flow #{dependency}; Fleet ignored that dependency.");
                    continue;
                }

                dependencyMap[spec.WorkItemNumber].Add(dependency);
                hasDependency = true;
            }
        }

        return hasDependency
            ? dependencyMap.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<int>)entry.Value.OrderBy(number => number).ToArray())
            : null;
    }

    private static Dictionary<int, IReadOnlyList<int>>? BuildGeneratedDependencyMap(
        IReadOnlyList<WorkItemDto> orderedChildren,
        string? plannerOutput,
        List<string> warnings)
    {
        var generatedPlan = SubFlowPlanner.Parse(plannerOutput);
        if (generatedPlan is null || generatedPlan.SubFlows.Count == 0)
            return null;

        var generatedSpecsWithDependencies = generatedPlan.SubFlows
            .Where(spec => spec.DependsOn is { Count: > 0 })
            .ToArray();
        if (generatedSpecsWithDependencies.Length == 0)
            return null;

        var childTitleGroups = orderedChildren
            .GroupBy(child => child.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        if (childTitleGroups.Values.Any(group => group.Length > 1))
        {
            warnings.Add("Planner generated sibling title dependencies, but multiple direct sub-flows share the same title. Fleet ignored the generated dependency hints for safety.");
            return null;
        }

        var dependencyMap = BuildEmptyDependencyMap(orderedChildren)
            .ToDictionary(entry => entry.Key, entry => new HashSet<int>(entry.Value));
        var hasDependency = false;

        foreach (var spec in generatedSpecsWithDependencies)
        {
            if (!childTitleGroups.TryGetValue(spec.Title.Trim(), out var matchingChildren))
            {
                warnings.Add($"Planner generated dependency hints for sub-flow '{spec.Title}', but Fleet could not match that generated child title after materialization.");
                continue;
            }

            var childWorkItemNumber = matchingChildren[0].WorkItemNumber;
            foreach (var dependencyTitle in spec.DependsOn.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!childTitleGroups.TryGetValue(dependencyTitle, out var dependencyChildren))
                {
                    warnings.Add($"Planner made generated sub-flow '{spec.Title}' depend on unknown sibling '{dependencyTitle}'. Fleet ignored that dependency.");
                    continue;
                }

                var dependencyWorkItemNumber = dependencyChildren[0].WorkItemNumber;
                if (dependencyWorkItemNumber == childWorkItemNumber)
                {
                    warnings.Add($"Planner made generated sub-flow '{spec.Title}' depend on itself. Fleet ignored that self-dependency.");
                    continue;
                }

                dependencyMap[childWorkItemNumber].Add(dependencyWorkItemNumber);
                hasDependency = true;
            }
        }

        return hasDependency
            ? dependencyMap.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<int>)entry.Value.OrderBy(number => number).ToArray())
            : null;
    }

    private static bool TryBuildBatches(
        IReadOnlyList<WorkItemDto> orderedChildren,
        IReadOnlyDictionary<int, IReadOnlyList<int>> dependenciesByWorkItemNumber,
        out List<ResolvedSubFlowExecutionBatch> batches)
    {
        var remainingDependencies = dependenciesByWorkItemNumber.ToDictionary(
            entry => entry.Key,
            entry => new HashSet<int>(entry.Value));
        var orderedChildrenByNumber = orderedChildren.ToDictionary(child => child.WorkItemNumber);
        batches = [];

        while (remainingDependencies.Count > 0)
        {
            var readyNumbers = remainingDependencies
                .Where(entry => entry.Value.Count == 0)
                .Select(entry => entry.Key)
                .OrderBy(number => number)
                .ToArray();
            if (readyNumbers.Length == 0)
                return false;

            batches.Add(new ResolvedSubFlowExecutionBatch(
                readyNumbers
                    .Select(number => orderedChildrenByNumber[number])
                    .ToArray()));

            foreach (var readyNumber in readyNumbers)
                remainingDependencies.Remove(readyNumber);

            foreach (var entry in remainingDependencies.Values)
            {
                foreach (var readyNumber in readyNumbers)
                    entry.Remove(readyNumber);
            }
        }

        return true;
    }

    private static Dictionary<int, IReadOnlyList<int>> ApplyImplicitGitHubPagesDependencies(
        IReadOnlyList<WorkItemDto> orderedChildren,
        IReadOnlyDictionary<int, IReadOnlyList<int>> dependenciesByWorkItemNumber)
    {
        if (orderedChildren.Count <= 1)
            return dependenciesByWorkItemNumber.ToDictionary(entry => entry.Key, entry => entry.Value);

        var dependencyMap = dependenciesByWorkItemNumber.ToDictionary(
            entry => entry.Key,
            entry => new HashSet<int>(entry.Value));
        var gitHubPagesDeploymentWorkItemNumbers = orderedChildren
            .Where(IsGitHubPagesDeploymentSubFlow)
            .Select(child => child.WorkItemNumber)
            .ToHashSet();
        if (gitHubPagesDeploymentWorkItemNumbers.Count == 0)
            return dependencyMap.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<int>)entry.Value.OrderBy(number => number).ToArray());

        var prerequisiteNumbers = orderedChildren
            .Select(child => child.WorkItemNumber)
            .Where(number => !gitHubPagesDeploymentWorkItemNumbers.Contains(number))
            .ToArray();
        if (prerequisiteNumbers.Length == 0)
            return dependencyMap.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<int>)entry.Value.OrderBy(number => number).ToArray());

        foreach (var deploymentNumber in gitHubPagesDeploymentWorkItemNumbers)
        {
            foreach (var prerequisiteNumber in prerequisiteNumbers)
                dependencyMap[deploymentNumber].Add(prerequisiteNumber);
        }

        return dependencyMap.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<int>)entry.Value.OrderBy(number => number).ToArray());
    }

    private static bool IsGitHubPagesDeploymentSubFlow(WorkItemDto workItem)
    {
        var text = string.Join(
            '\n',
            new[]
            {
                workItem.Title,
                workItem.Description,
                workItem.AcceptanceCriteria,
                string.Join(' ', workItem.Tags),
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return ContainsAnyKeyword(text, "github pages", "gh-pages") &&
               ContainsAnyKeyword(text, "deploy", "deployment", "publish", "hosting", "host");
    }

    private static bool ContainsAnyKeyword(string text, params string[] keywords)
        => keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
