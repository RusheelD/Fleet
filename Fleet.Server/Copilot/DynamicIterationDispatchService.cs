using Fleet.Server.Models;
using Fleet.Server.GitHub;
using Fleet.Server.Projects;
using System.Text.Json;

namespace Fleet.Server.Copilot;

public class DynamicIterationDispatchService(
    IAgentAutoExecutionDispatcher autoExecutionDispatcher,
    IProjectRepository projectRepository,
    IGitHubApiService gitHubApiService,
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

        var branchPolicyError = await ValidateTargetBranchAsync(
            projectId,
            userId,
            targetBranch,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(branchPolicyError))
        {
            return new DynamicIterationDispatchResult(
                candidateIds.Count,
                0,
                0,
                candidateIds.Count,
                0,
                [branchPolicyError]);
        }

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

    private async Task<string?> ValidateTargetBranchAsync(
        string projectId,
        int userId,
        string? targetBranch,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(projectId, userId.ToString());
        if (project is null || string.IsNullOrWhiteSpace(project.Repo))
            return "Dynamic iteration could not resolve the project repository.";

        IReadOnlyList<GitHubBranchInfo> branches;
        try
        {
            branches = await gitHubApiService.GetBranchesAsync(userId, project.Repo, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to verify dynamic iteration branch protection for project {ProjectId}.",
                projectId);
            return "Unable to verify repository branch protection before dynamic iteration dispatch.";
        }

        if (branches.Count == 0)
            return null;

        var normalizedTargetBranch = NormalizeNullableString(targetBranch);
        var effectiveTargetBranch = normalizedTargetBranch
            ?? branches.FirstOrDefault(branch => branch.IsDefault)?.Name;
        if (string.IsNullOrWhiteSpace(effectiveTargetBranch))
            return null;

        var matchingBranch = branches.FirstOrDefault(branch =>
            string.Equals(branch.Name, effectiveTargetBranch, StringComparison.OrdinalIgnoreCase));
        if (IsMainBranchName(effectiveTargetBranch) && branches.Any(branch => branch.IsProtected))
            return "Dynamic iteration can target main only when the repository has no branch protection. Select or create an unprotected integration branch.";

        if (matchingBranch?.IsProtected == true)
            return $"Dynamic iteration cannot target protected branch '{matchingBranch.Name}'. Select or create an unprotected integration branch.";

        return null;
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

    private static string? NormalizeNullableString(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsMainBranchName(string branchName)
        => string.Equals(branchName, "main", StringComparison.OrdinalIgnoreCase);
}
