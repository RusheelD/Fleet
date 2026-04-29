namespace Fleet.Server.Models;

public record UpdateSessionDynamicIterationRequest(
    bool IsDynamicIterationEnabled,
    string? DynamicIterationBranch,
    string? DynamicIterationPolicyJson);
