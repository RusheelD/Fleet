namespace Fleet.Server.Models;

public sealed record ProjectBranchDto(
    string Name,
    bool IsDefault,
    bool IsProtected,
    bool CanUseForDynamicIteration,
    string? DynamicIterationBlockedReason = null);
