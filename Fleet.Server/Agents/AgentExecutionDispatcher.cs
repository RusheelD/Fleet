using Fleet.Server.Copilot;
using Fleet.Server.Data;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Agents;

public class AgentExecutionDispatcher(
    FleetDbContext db,
    IAgentOrchestrationService orchestrationService,
    IChatSessionRepository chatSessionRepository,
    ILogger<AgentExecutionDispatcher> logger) : IAgentExecutionDispatcher
{
    public async Task<string> DispatchWorkItemAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? requestedTargetBranch = null,
        string? chatSessionId = null,
        string? parentExecutionId = null,
        CancellationToken cancellationToken = default)
        => await DispatchWorkItemInternalAsync(
            projectId,
            workItemNumber,
            userId,
            requestedTargetBranch,
            chatSessionId,
            parentExecutionId,
            AgentExecutionDeliveryModes.PullRequest,
            cancellationToken);

    public async Task<string> DispatchWorkItemToTargetBranchAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? requestedTargetBranch = null,
        string? chatSessionId = null,
        string? parentExecutionId = null,
        CancellationToken cancellationToken = default)
        => await DispatchWorkItemInternalAsync(
            projectId,
            workItemNumber,
            userId,
            requestedTargetBranch,
            chatSessionId,
            parentExecutionId,
            AgentExecutionDeliveryModes.TargetBranch,
            cancellationToken);

    private async Task<string> DispatchWorkItemInternalAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? requestedTargetBranch,
        string? chatSessionId,
        string? parentExecutionId,
        string deliveryMode,
        CancellationToken cancellationToken)
    {
        var normalizedDeliveryMode = AgentExecutionDeliveryModes.Normalize(deliveryMode);
        var resolvedTargetBranch = await ResolveTargetBranchAsync(
            projectId,
            requestedTargetBranch,
            chatSessionId,
            parentExecutionId,
            cancellationToken);

        var executionId = AgentExecutionDeliveryModes.IsTargetBranch(normalizedDeliveryMode)
            ? string.IsNullOrWhiteSpace(parentExecutionId)
                ? await orchestrationService.StartExecutionAsync(
                    projectId,
                    workItemNumber,
                    userId,
                    resolvedTargetBranch,
                    normalizedDeliveryMode,
                    cancellationToken)
                : await orchestrationService.StartSubFlowExecutionAsync(
                    projectId,
                    workItemNumber,
                    userId,
                    parentExecutionId,
                    resolvedTargetBranch,
                    normalizedDeliveryMode,
                    cancellationToken)
            : string.IsNullOrWhiteSpace(parentExecutionId)
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
            userId.ToString(),
            workItemNumber,
            resolvedTargetBranch,
            normalizedDeliveryMode);

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
        string ownerId,
        int workItemNumber,
        string? resolvedTargetBranch,
        string deliveryMode)
    {
        if (string.IsNullOrWhiteSpace(chatSessionId))
            return;

        try
        {
            var branchLabel = string.IsNullOrWhiteSpace(resolvedTargetBranch)
                ? "inherit parent/default branch"
                : resolvedTargetBranch;
            var branchPurpose = AgentExecutionDeliveryModes.IsTargetBranch(deliveryMode)
                ? "output branch"
                : "target branch";
            await chatSessionRepository.AppendSessionActivityAsync(
                projectId,
                chatSessionId,
                new ChatSessionActivityDto(
                    Id: Guid.NewGuid().ToString("N"),
                    Kind: "status",
                    Message: $"Queued work item #{workItemNumber} with {branchPurpose} '{branchLabel}'.",
                    TimestampUtc: DateTime.UtcNow.ToString("O")),
                ownerId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to append dynamic iteration branch activity for work item {WorkItemNumber} in chat session {SessionId}.",
                workItemNumber,
                chatSessionId);
        }
    }

    private static string? NormalizeBranch(string? branch)
    {
        var normalizedBranch = branch?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBranch))
            return null;

        return ContainsBranchTemplateToken(normalizedBranch) ? null : normalizedBranch;
    }

    private static bool ContainsBranchTemplateToken(string branch)
        => branch.Contains("{workItemNumber}", StringComparison.OrdinalIgnoreCase) ||
           branch.Contains("{slug}", StringComparison.OrdinalIgnoreCase);
}
