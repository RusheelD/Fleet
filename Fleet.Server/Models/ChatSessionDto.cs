namespace Fleet.Server.Models;

public record ChatSessionDto(
    string Id,
    string Title,
    string LastMessage,
    string Timestamp,
    bool IsActive,
    bool IsGenerating = false,
    string GenerationState = ChatGenerationStates.Idle,
    string? GenerationStatus = null,
    string? GenerationUpdatedAtUtc = null,
    ChatSessionActivityDto[]? RecentActivity = null,
    string BranchStrategy = ChatSessionBranchStrategy.AutoFromProjectPattern,
    string? SessionPinnedBranch = null,
    bool InheritParentBranchForSubFlows = true
);
