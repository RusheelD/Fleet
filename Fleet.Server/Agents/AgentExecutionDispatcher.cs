using Fleet.Server.Copilot;
using Fleet.Server.Data;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Agents;

public class AgentExecutionDispatcher(
    FleetDbContext db,
    IAgentOrchestrationService orchestrationService,
    IChatSessionRepository chatSessionRepository) : IAgentExecutionDispatcher
{
    public async Task<string> DispatchWorkItemAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? requestedTargetBranch = null,
        string? chatSessionId = null,
        string? parentExecutionId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTargetBranch = await ResolveTargetBranchAsync(
            projectId,
            requestedTargetBranch,
            chatSessionId,
            parentExecutionId,
            cancellationToken);

        var executionId = string.IsNullOrWhiteSpace(parentExecutionId)
            ? await orchestrationService.StartExecutionAsync(
                projectId,
                workItemNumber,
                userId,
                resolvedTargetBranch,
                cancellationToken)
            : await orchestrationService.StartSubFlowExecutionAsync(
                projectId,
                workItemNumber,
                userId,
                parentExecutionId,
                resolvedTargetBranch,
                cancellationToken);

        await AppendResolvedBranchActivityAsync(
            projectId,
            chatSessionId,
            workItemNumber,
            resolvedTargetBranch);

        return executionId;
    }

    private async Task<string?> ResolveTargetBranchAsync(
        string projectId,
        string? requestedTargetBranch,
        string? chatSessionId,
        string? parentExecutionId,
        CancellationToken cancellationToken)
    {
        var normalizedRequestedTargetBranch = NormalizeBranch(requestedTargetBranch);
        if (!string.IsNullOrWhiteSpace(normalizedRequestedTargetBranch))
            return normalizedRequestedTargetBranch;

        if (string.IsNullOrWhiteSpace(chatSessionId))
            return null;

        var sessionConfig = await db.ChatSessions
            .AsNoTracking()
            .Where(session => session.ProjectId == projectId && session.Id == chatSessionId)
            .Select(session => new
            {
                session.BranchStrategy,
                session.SessionPinnedBranch,
                session.InheritParentBranchForSubFlows,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sessionConfig is null)
            return null;

        var branchStrategy = ChatSessionBranchStrategy.Normalize(sessionConfig.BranchStrategy);
        var normalizedPinnedBranch = NormalizeBranch(sessionConfig.SessionPinnedBranch);
        if (branchStrategy == ChatSessionBranchStrategy.SessionPinnedBranch && !string.IsNullOrWhiteSpace(normalizedPinnedBranch))
            return normalizedPinnedBranch;

        var isSubFlowWorkItem = !string.IsNullOrWhiteSpace(parentExecutionId);
        if (isSubFlowWorkItem && sessionConfig.InheritParentBranchForSubFlows)
            return null;

        return null;
    }

    private async Task AppendResolvedBranchActivityAsync(
        string projectId,
        string? chatSessionId,
        int workItemNumber,
        string? resolvedTargetBranch)
    {
        if (string.IsNullOrWhiteSpace(chatSessionId))
            return;

        var branchLabel = string.IsNullOrWhiteSpace(resolvedTargetBranch)
            ? "inherit parent/default branch"
            : resolvedTargetBranch;
        await chatSessionRepository.AppendSessionActivityAsync(
            projectId,
            chatSessionId,
            new ChatSessionActivityDto(
                Id: Guid.NewGuid().ToString("N"),
                Kind: "status",
                Message: $"Queued work item #{workItemNumber} with target branch '{branchLabel}'.",
                TimestampUtc: DateTime.UtcNow.ToString("O")));
    }

    private static string? NormalizeBranch(string? branch)
    {
        var normalizedBranch = branch?.Trim();
        return string.IsNullOrWhiteSpace(normalizedBranch) ? null : normalizedBranch;
    }
}
