using Fleet.Server.Models;

namespace Fleet.Server.Agents;

internal static class AgentPlannerHeuristics
{
    internal static bool ContainsAnyKeyword(string text, params string[] keywords)
        => keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    internal static bool IsNarrowFixScope(string text)
        => ContainsAnyKeyword(
            text,
            "typo",
            "copy change",
            "text update",
            "rename only",
            "small fix",
            "minor bug",
            "minor issue",
            "small polish",
            "polish");

    internal static IReadOnlyList<AgentRole> ResolveDeterministicDirectExecutionRoles(
        WorkItemDto workItem,
        IReadOnlyCollection<WorkItemDto> directChildWorkItems,
        IReadOnlyCollection<WorkItemDto> descendants,
        int effectiveDifficulty,
        string? assignmentMode,
        int? assignedAgentCount,
        Func<IReadOnlyCollection<AgentRole>, string?, int?, AgentRole[][]> buildPipelineFromFollowingRoles)
    {
        var keywordContext = AgentExecutionPromptBuilder.BuildPlannerKeywordContext(workItem);
        var backend = ContainsAnyKeyword(
            keywordContext,
            "backend",
            "server",
            "api",
            "endpoint",
            "controller",
            "service",
            "database",
            "migration",
            "entity",
            "repository",
            ".net",
            "c#",
            "queue",
            "sse",
            "signalr",
            "auth",
            "worker",
            "orchestration");
        var frontend = ContainsAnyKeyword(
            keywordContext,
            "frontend",
            "ui",
            "react",
            "page",
            "component",
            "screen",
            "layout",
            "mobile",
            "responsive",
            "browser",
            "route",
            "view",
            "client");
        var styling = ContainsAnyKeyword(
            keywordContext,
            "style",
            "styling",
            "css",
            "theme",
            "animation",
            "visual",
            "spacing",
            "typography",
            "color",
            "motion");
        var testing = ContainsAnyKeyword(
            keywordContext,
            "test",
            "testing",
            "regression",
            "coverage",
            "vitest",
            "pytest",
            "mstest",
            "xunit") || backend || frontend;
        var documentation = ContainsAnyKeyword(
            keywordContext,
            "documentation",
            "readme",
            "guide",
            "playbook",
            "changelog",
            "docs");
        var research = ContainsAnyKeyword(
            keywordContext,
            "research",
            "investigate",
            "spike",
            "prototype",
            "explore",
            "analyze");
        var contractSignals = ContainsAnyKeyword(
            keywordContext,
            "contract",
            "schema",
            "dto",
            "interface",
            "shared type",
            "payload",
            "request",
            "response",
            "graphql") ||
            (backend && frontend) ||
            directChildWorkItems.Count >= 2;

        if (styling)
            frontend = true;

        var docsOnly = documentation && !backend && !frontend && !contractSignals && !research;
        if (!backend && !frontend && !documentation && !research)
            backend = true;

        var review = effectiveDifficulty >= 4 ||
                     (backend && frontend) ||
                     contractSignals ||
                     directChildWorkItems.Count >= 2 ||
                     descendants.Count >= 3;

        var roles = new List<AgentRole>();
        if (research)
            roles.Add(AgentRole.Research);
        if (backend)
            roles.Add(AgentRole.Backend);
        if (frontend)
            roles.Add(AgentRole.Frontend);
        if (testing && !docsOnly)
            roles.Add(AgentRole.Testing);
        if (styling)
            roles.Add(AgentRole.Styling);
        if (backend && frontend)
            roles.Add(AgentRole.Consolidation);
        if (review)
            roles.Add(AgentRole.Review);
        if (documentation)
            roles.Add(AgentRole.Documentation);

        if (roles.Count == 0)
        {
            roles.Add(AgentRole.Backend);
            if (effectiveDifficulty >= 3)
                roles.Add(AgentRole.Testing);
        }

        var pipeline = buildPipelineFromFollowingRoles(roles, assignmentMode, assignedAgentCount);
        return pipeline
            .SelectMany(group => group)
            .Where(role => role is not AgentRole.Manager and not AgentRole.Planner)
            .Distinct()
            .ToArray();
    }

    internal static int ResolveDeterministicPlannerDifficulty(
        WorkItemDto workItem,
        IReadOnlyCollection<WorkItemDto> directChildWorkItems,
        IReadOnlyCollection<WorkItemDto> descendants,
        IReadOnlyList<AgentRole> directExecutionRoles)
    {
        var keywordContext = AgentExecutionPromptBuilder.BuildPlannerKeywordContext(workItem);
        var effectiveDifficulty = workItem.Difficulty;
        var crossStack = directExecutionRoles.Contains(AgentRole.Backend) &&
                         directExecutionRoles.Contains(AgentRole.Frontend);
        var implementationBreadth = directExecutionRoles.Count(role =>
            role is AgentRole.Research or AgentRole.Contracts or AgentRole.Backend or AgentRole.Frontend or AgentRole.Testing or AgentRole.Styling);

        if (crossStack)
            effectiveDifficulty++;

        if (directExecutionRoles.Contains(AgentRole.Contracts) &&
            (directExecutionRoles.Contains(AgentRole.Backend) || directExecutionRoles.Contains(AgentRole.Frontend)))
        {
            effectiveDifficulty++;
        }

        if (directChildWorkItems.Count >= 2 || descendants.Count >= 3)
            effectiveDifficulty++;

        if (directExecutionRoles.Contains(AgentRole.Research))
            effectiveDifficulty++;

        if (implementationBreadth <= 2 &&
            directChildWorkItems.Count == 0 &&
            descendants.Count == 0 &&
            !directExecutionRoles.Contains(AgentRole.Contracts))
        {
            effectiveDifficulty--;
        }

        if (IsNarrowFixScope(keywordContext))
            effectiveDifficulty--;

        return Math.Clamp(effectiveDifficulty, 1, 5);
    }

    internal static string BuildDeterministicDifficultyReason(
        WorkItemDto workItem,
        IReadOnlyCollection<WorkItemDto> directChildWorkItems,
        IReadOnlyCollection<WorkItemDto> descendants,
        IReadOnlyList<AgentRole> directExecutionRoles)
    {
        var reasons = new List<string>();
        if (directExecutionRoles.Contains(AgentRole.Backend) && directExecutionRoles.Contains(AgentRole.Frontend))
            reasons.Add("cross-stack implementation scope");
        if (directExecutionRoles.Contains(AgentRole.Contracts))
            reasons.Add("shared contract or API coordination");
        if (directChildWorkItems.Count >= 2)
            reasons.Add($"{directChildWorkItems.Count} existing direct branches already in scope");
        if (descendants.Count >= 3)
            reasons.Add($"{descendants.Count} descendant work items increase coordination cost");
        if (directExecutionRoles.Contains(AgentRole.Research))
            reasons.Add("extra investigation work is likely needed");
        if (IsNarrowFixScope(AgentExecutionPromptBuilder.BuildPlannerKeywordContext(workItem)))
            reasons.Add("task language looks narrower than the stored difficulty suggests");

        return reasons.Count == 0
            ? "The current work item shape and role breadth look aligned."
            : string.Join("; ", reasons) + ".";
    }
}
