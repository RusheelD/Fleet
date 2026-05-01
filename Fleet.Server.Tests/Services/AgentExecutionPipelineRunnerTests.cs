using Fleet.Server.Agents;
using Fleet.Server.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentExecutionPipelineRunnerTests
{
    [TestMethod]
    public async Task RunPipelineDetachedAsync_UsesFreshScopeThatOutlivesCallerScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton<PipelineRunnerState>();
        services.AddScoped<ScopedPipelineProbe>();
        services.AddScoped<IAgentExecutionPipelineRunner, FakePipelineRunner>();

        await using var provider = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory;
        await using (var callerScope = provider.CreateAsyncScope())
        {
            scopeFactory = callerScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        }

        var request = new ExecutionPipelineLaunchRequest(
            ExecutionId: "exec-1",
            ProjectId: "proj-1",
            WorkItem: new WorkItemDto(
                WorkItemNumber: 42,
                Title: "Retry parent flow",
                State: "New",
                Priority: 1,
                Difficulty: 4,
                AssignedTo: "Fleet AI",
                Tags: [],
                IsAI: true,
                Description: "Test work item",
                ParentWorkItemNumber: null,
                ChildWorkItemNumbers: [],
                LevelId: null),
            ChildWorkItems: [],
            RepoFullName: "owner/repo",
            BranchName: "fleet/42-retry-parent-flow",
            CommitAuthorName: "Fleet",
            CommitAuthorEmail: "fleet@example.com",
            UserId: 7,
            SelectedModelKey: "Fast",
            Pipeline: [[AgentRole.Manager], [AgentRole.Planner]],
            MaxConcurrentAgentsPerTask: 2,
            PullRequestTargetBranch: "main",
            DeliveryMode: AgentExecutionDeliveryModes.PullRequest,
            ExistingPullRequestNumber: 0,
            BillableExecution: true,
            ParentExecutionId: "parent-1",
            RetrySourceExecutionId: null,
            RetrySourceStatus: null,
            RetryReuseBranchName: null,
            RetryReusePullRequestUrl: null,
            RetryReusePullRequestNumber: null,
            RetryReusePullRequestTitle: null,
            RetryPriorProgressEstimate: 0,
            RetryCarryForwardOutputs: null,
            RetryLineageExecutionIds: null,
            RetryContextMarkdown: null,
            RetryResumeInPlace: false,
            RetryResumeFromRemoteBranch: true);

        await AgentOrchestrationService.RunPipelineDetachedAsync(scopeFactory, request, CancellationToken.None);

        var state = provider.GetRequiredService<PipelineRunnerState>();
        Assert.IsTrue(state.WasCalled);
        Assert.IsFalse(state.WasProbeDisposedDuringExecution);
        Assert.IsTrue(state.WasProbeDisposedAfterExecution);
        Assert.AreEqual("exec-1", state.LastExecutionId);
    }

    private sealed class PipelineRunnerState
    {
        public bool WasCalled { get; set; }
        public bool WasProbeDisposedDuringExecution { get; set; }
        public bool WasProbeDisposedAfterExecution { get; set; }
        public string? LastExecutionId { get; set; }
    }

    private sealed class ScopedPipelineProbe(PipelineRunnerState state) : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            state.WasProbeDisposedAfterExecution = true;
        }
    }

    private sealed class FakePipelineRunner(
        PipelineRunnerState state,
        ScopedPipelineProbe probe) : IAgentExecutionPipelineRunner
    {
        public async Task RunExecutionPipelineAsync(
            ExecutionPipelineLaunchRequest request,
            CancellationToken cancellationToken)
        {
            state.WasCalled = true;
            state.LastExecutionId = request.ExecutionId;
            state.WasProbeDisposedDuringExecution |= probe.IsDisposed;
            await Task.Delay(10, cancellationToken);
            state.WasProbeDisposedDuringExecution |= probe.IsDisposed;
        }
    }
}
