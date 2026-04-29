using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

public interface IDynamicIterationDispatchService
{
    Task<DynamicIterationDispatchResult> DispatchFromToolEventsAsync(
        string projectId,
        int userId,
        IReadOnlyList<ToolEventDto> toolEvents,
        string? targetBranch,
        CancellationToken cancellationToken = default);
}

public sealed class NoOpDynamicIterationDispatchService : IDynamicIterationDispatchService
{
    public static readonly NoOpDynamicIterationDispatchService Instance = new();

    private NoOpDynamicIterationDispatchService()
    {
    }

    public Task<DynamicIterationDispatchResult> DispatchFromToolEventsAsync(
        string projectId,
        int userId,
        IReadOnlyList<ToolEventDto> toolEvents,
        string? targetBranch,
        CancellationToken cancellationToken = default)
        => Task.FromResult(DynamicIterationDispatchResult.Empty);
}
