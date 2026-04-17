using System.Text;
using System.Text.RegularExpressions;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;

namespace Fleet.Server.Agents;

internal sealed record OpenSpecExecutionPaths(
    string ChangeId,
    string CapabilityId,
    string ProposalPath,
    string TasksPath,
    string DesignPath,
    string SpecPath);

internal sealed record OpenSpecExecutionSnapshot(
    OpenSpecExecutionPaths Paths,
    IReadOnlyList<string> TrackedPaths,
    string ProposalMarkdown,
    string TasksMarkdown,
    string DesignMarkdown,
    string SpecMarkdown,
    string PromptContext);

internal static class OpenSpecExecutionArtifacts
{
    private const string OpenSpecDocsRoot = ".fleet/.docs";

    internal static OpenSpecExecutionPaths BuildPaths(string branchName, WorkItemDto workItem)
    {
        var branchSlug = Slugify(branchName);
        var titleSlug = Slugify(workItem.Title);
        var changeId = string.IsNullOrWhiteSpace(branchSlug)
            ? $"work-item-{workItem.WorkItemNumber}"
            : branchSlug;
        var capabilityId = string.IsNullOrWhiteSpace(titleSlug)
            ? $"work-item-{workItem.WorkItemNumber}"
            : $"work-item-{workItem.WorkItemNumber}-{titleSlug}";
        var changeRoot = $"{OpenSpecDocsRoot}/changes/{changeId}";
        var proposalPath = $"{changeRoot}/proposal.md";
        var tasksPath = $"{changeRoot}/tasks.md";
        var designPath = $"{changeRoot}/design.md";
        var specPath = $"{changeRoot}/specs/{capabilityId}/spec.md";

        return new OpenSpecExecutionPaths(changeId, capabilityId, proposalPath, tasksPath, designPath, specPath);
    }

    internal static OpenSpecExecutionSnapshot BuildSnapshot(
        AgentExecution execution,
        WorkItemDto workItem,
        string targetBranch,
        IReadOnlyList<AgentPhaseResult> phaseResults,
        IReadOnlyList<AgentExecution> descendantExecutions,
        string executionDocumentationMarkdown)
    {
        var paths = BuildPaths(execution.BranchName ?? execution.Id, workItem);
        var proposalMarkdown = BuildProposalMarkdown(execution, workItem, targetBranch, descendantExecutions);
        var tasksMarkdown = BuildTasksMarkdown(execution, descendantExecutions);
        var designMarkdown = BuildDesignMarkdown(execution, phaseResults, descendantExecutions, executionDocumentationMarkdown);
        var specMarkdown = BuildSpecMarkdown(execution, workItem, descendantExecutions);
        var promptContext = BuildPromptContext(paths, execution, phaseResults, descendantExecutions);

        return new OpenSpecExecutionSnapshot(
            paths,
            [paths.ProposalPath, paths.TasksPath, paths.DesignPath, paths.SpecPath],
            proposalMarkdown,
            tasksMarkdown,
            designMarkdown,
            specMarkdown,
            promptContext);
    }

    internal static string BuildPromptContext(
        OpenSpecExecutionPaths paths,
        AgentExecution execution,
        IReadOnlyList<AgentPhaseResult> phaseResults,
        IReadOnlyList<AgentExecution> descendantExecutions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Branch-local OpenSpec files are the living execution memory for this run.");
        sb.AppendLine($"- Proposal: `{paths.ProposalPath}`");
        sb.AppendLine($"- Tasks: `{paths.TasksPath}`");
        sb.AppendLine($"- Design journal: `{paths.DesignPath}`");
        sb.AppendLine($"- Spec delta: `{paths.SpecPath}`");
        sb.AppendLine("- Fleet refreshes those files after each phase; read them before making changes, and if you edit them manually, keep them aligned with the current implementation state.");
        sb.AppendLine($"- Current execution status: `{execution.Status}` on branch `{execution.BranchName}`.");

        var completedRoles = phaseResults
            .Where(phase => phase.Success)
            .OrderBy(phase => phase.PhaseOrder)
            .Select(phase => phase.Role)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var failedRoles = phaseResults
            .Where(phase => !phase.Success)
            .OrderBy(phase => phase.PhaseOrder)
            .Select(phase => phase.Role)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (completedRoles.Length > 0)
            sb.AppendLine($"- Completed phases so far: {string.Join(", ", completedRoles)}.");
        if (failedRoles.Length > 0)
            sb.AppendLine($"- Failed phases recorded so far: {string.Join(", ", failedRoles)}.");

        if (descendantExecutions.Count > 0)
        {
            var completedSubFlows = descendantExecutions.Count(execution => string.Equals(execution.Status, "completed", StringComparison.OrdinalIgnoreCase));
            var activeSubFlows = descendantExecutions.Count(execution =>
                string.Equals(execution.Status, "running", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(execution.Status, "queued", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(execution.Status, "paused", StringComparison.OrdinalIgnoreCase));
            var failedSubFlows = descendantExecutions.Count(execution =>
                string.Equals(execution.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(execution.Status, "cancelled", StringComparison.OrdinalIgnoreCase));
            sb.AppendLine($"- Sub-flow status: {completedSubFlows} completed, {activeSubFlows} active/waiting, {failedSubFlows} failed/cancelled.");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildProposalMarkdown(
        AgentExecution execution,
        WorkItemDto workItem,
        string targetBranch,
        IReadOnlyList<AgentExecution> descendantExecutions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Change Proposal: Work Item #{workItem.WorkItemNumber}");
        sb.AppendLine();
        sb.AppendLine("## Why");
        sb.AppendLine($"- Deliver `{workItem.Title}` on execution branch `{execution.BranchName}`.");
        if (!string.IsNullOrWhiteSpace(workItem.Description))
            sb.AppendLine($"- Problem / scope: {workItem.Description.Trim()}");
        if (!string.IsNullOrWhiteSpace(workItem.AcceptanceCriteria))
            sb.AppendLine($"- Acceptance focus: {NormalizeInline(workItem.AcceptanceCriteria)}");
        sb.AppendLine();
        sb.AppendLine("## Scope");
        sb.AppendLine($"- Work item id: `#{workItem.WorkItemNumber}`");
        sb.AppendLine($"- Priority / difficulty: `P{workItem.Priority}` / `D{workItem.Difficulty}`");
        sb.AppendLine($"- Current execution status: `{execution.Status}`");
        sb.AppendLine($"- Base branch: `{targetBranch}`");
        sb.AppendLine($"- Working branch: `{execution.BranchName}`");
        if (workItem.Tags.Length > 0)
            sb.AppendLine($"- Tags: {string.Join(", ", workItem.Tags)}");
        if (descendantExecutions.Count > 0)
            sb.AppendLine($"- Sub-flows planned or created: {descendantExecutions.Count}");

        return sb.ToString().TrimEnd();
    }

    private static string BuildTasksMarkdown(
        AgentExecution execution,
        IReadOnlyList<AgentExecution> descendantExecutions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Tasks");
        sb.AppendLine();
        sb.AppendLine("## Parent Execution");
        foreach (var agent in execution.Agents)
        {
            var checkbox = string.Equals(agent.Status, "completed", StringComparison.OrdinalIgnoreCase) ? "[x]" : "[ ]";
            var detail = string.IsNullOrWhiteSpace(agent.CurrentTask) ? agent.Status : $"{agent.Status} - {agent.CurrentTask}";
            sb.AppendLine($"- {checkbox} {agent.Role}: {detail}");
        }

        if (descendantExecutions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Sub-Flows");
            foreach (var child in descendantExecutions
                         .OrderBy(child => child.WorkItemId)
                         .ThenBy(child => child.StartedAtUtc))
            {
                var checkbox = string.Equals(child.Status, "completed", StringComparison.OrdinalIgnoreCase) ? "[x]" : "[ ]";
                sb.AppendLine($"- {checkbox} #{child.WorkItemId} {child.WorkItemTitle} (`{child.Status}`)");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildDesignMarkdown(
        AgentExecution execution,
        IReadOnlyList<AgentPhaseResult> phaseResults,
        IReadOnlyList<AgentExecution> descendantExecutions,
        string executionDocumentationMarkdown)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Design Notes");
        sb.AppendLine();
        sb.AppendLine("## Current State");
        sb.AppendLine($"- Execution: `{execution.Id}`");
        sb.AppendLine($"- Branch: `{execution.BranchName}`");
        sb.AppendLine($"- Status: `{execution.Status}`");
        if (!string.IsNullOrWhiteSpace(execution.CurrentPhase))
            sb.AppendLine($"- Current phase: `{execution.CurrentPhase}`");
        if (descendantExecutions.Count > 0)
            sb.AppendLine($"- Sub-flows tracked: {descendantExecutions.Count}");

        if (phaseResults.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Latest Phase Notes");
            foreach (var phase in phaseResults.OrderBy(phase => phase.PhaseOrder))
            {
                sb.AppendLine($"### {phase.Role}");
                sb.AppendLine($"- Status: {(phase.Success ? "completed" : "failed")}");
                sb.AppendLine($"- Tool calls: {phase.ToolCallCount}");
                if (!string.IsNullOrWhiteSpace(phase.Error))
                    sb.AppendLine($"- Error: {NormalizeInline(phase.Error)}");
                var summary = string.IsNullOrWhiteSpace(phase.Output)
                    ? "(no output captured)"
                    : TrimForMarkdown(phase.Output, 1000);
                sb.AppendLine();
                sb.AppendLine(summary);
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Execution Journal");
        sb.AppendLine();
        sb.AppendLine(executionDocumentationMarkdown.Trim());

        return sb.ToString().TrimEnd();
    }

    private static string BuildSpecMarkdown(
        AgentExecution execution,
        WorkItemDto workItem,
        IReadOnlyList<AgentExecution> descendantExecutions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Spec Delta");
        sb.AppendLine();
        sb.AppendLine("## ADDED Requirements");
        sb.AppendLine();
        sb.AppendLine($"### Requirement: Deliver work item #{workItem.WorkItemNumber} - {workItem.Title}");
        sb.AppendLine($"Fleet MUST complete the implementation for `{workItem.Title}` on branch `{execution.BranchName}` while preserving accumulated retry and sub-flow context.");
        sb.AppendLine();
        sb.AppendLine("#### Scenario: Execution tracks live implementation context");
        sb.AppendLine($"- **GIVEN** execution `{execution.Id}` is working on branch `{execution.BranchName}`");
        sb.AppendLine("- **WHEN** phases, retries, or sub-flow merges occur");
        sb.AppendLine("- **THEN** the OpenSpec artifacts for this change reflect the latest known plan, tasks, and design state");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(workItem.AcceptanceCriteria))
        {
            sb.AppendLine("#### Scenario: Acceptance criteria remain visible");
            foreach (var criterion in SplitBullets(workItem.AcceptanceCriteria))
                sb.AppendLine($"- **THEN** {criterion}");
            sb.AppendLine();
        }

        if (descendantExecutions.Count > 0)
        {
            sb.AppendLine("#### Scenario: Sub-flows preserve execution memory");
            sb.AppendLine($"- **GIVEN** {descendantExecutions.Count} sub-flow(s) contribute to the parent branch");
            sb.AppendLine("- **WHEN** retries or consolidation happen later");
            sb.AppendLine("- **THEN** the same OpenSpec change folder records their latest status and merge progress");
        }

        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<string> SplitBullets(string acceptanceCriteria)
        => acceptanceCriteria
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', '*', ' '))
            .Where(line => !string.IsNullOrWhiteSpace(line));

    private static string NormalizeInline(string value)
        => Regex.Replace(value.Replace("\r\n", " ").Trim(), "\\s+", " ");

    private static string TrimForMarkdown(string value, int maxChars)
    {
        var normalized = value.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maxChars)
            return normalized;

        return $"{normalized[..maxChars]}\n\n[truncated]";
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var slug = Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 80 ? slug[..80].Trim('-') : slug;
    }
}
