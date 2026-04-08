using Fleet.Server.Agents.Tools;
using Fleet.Server.Agents;
using Fleet.Server.LLM;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentPhaseRunnerTests
{
    [TestMethod]
    public void GetMaxToolCalls_UsesExpandedAgentCeilings()
    {
        Assert.AreEqual(200, AgentPhaseRunner.GetMaxToolCalls(AgentRole.Manager));
        Assert.AreEqual(250, AgentPhaseRunner.GetMaxToolCalls(AgentRole.Planner));
        Assert.AreEqual(400, AgentPhaseRunner.GetMaxToolCalls(AgentRole.Contracts));
        Assert.AreEqual(500, AgentPhaseRunner.GetMaxToolCalls(AgentRole.Backend));
    }

    [TestMethod]
    public void GetMaxToolLoops_UsesExpandedAgentCeilings()
    {
        Assert.AreEqual(200, AgentPhaseRunner.GetMaxToolLoops(AgentRole.Manager));
        Assert.AreEqual(400, AgentPhaseRunner.GetMaxToolLoops(AgentRole.Consolidation));
        Assert.AreEqual(500, AgentPhaseRunner.GetMaxToolLoops(AgentRole.Frontend));
    }

    [TestMethod]
    public async Task RunPhaseAsync_UsesFractionalFallbackProgressUpdates()
    {
        var promptLoader = new Mock<IAgentPromptLoader>();
        promptLoader.Setup(loader => loader.GetPrompt(It.IsAny<AgentRole>())).Returns("system");

        var llmClient = new Mock<ILLMClient>();
        var responses = new Queue<LLMResponse>([
            new(null, [new LLMToolCall("call-1", "test_tool", "{}")]),
            new("done", null),
        ]);
        llmClient
            .Setup(client => client.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responses.Dequeue());

        var registry = new AgentToolRegistry([
            new ReportProgressTool(),
            new StubAgentTool("test_tool", "ok", isReadOnly: true),
        ]);
        var runner = new AgentPhaseRunner(
            promptLoader.Object,
            llmClient.Object,
            registry,
            Options.Create(new LLMOptions { GenerateModel = "test-model" }),
            NullLogger<AgentPhaseRunner>.Instance);

        var progressUpdates = new List<double>();
        var result = await runner.RunPhaseAsync(
            AgentRole.Backend,
            "Do the thing",
            new AgentToolContext(Mock.Of<IRepoSandbox>(), "proj", "user", "token", "owner/repo", "exec"),
            onProgress: (progress, _) =>
            {
                progressUpdates.Add(Math.Round(progress * 100, 2, MidpointRounding.AwayFromZero));
                return Task.CompletedTask;
            });

        Assert.IsTrue(result.Success);
        CollectionAssert.Contains(progressUpdates, 0.05);
        CollectionAssert.Contains(progressUpdates, 0.20);
        CollectionAssert.Contains(progressUpdates, 100.0);
    }

    [TestMethod]
    public async Task RunPhaseAsync_AcceptsFractionalReportedProgress()
    {
        var promptLoader = new Mock<IAgentPromptLoader>();
        promptLoader.Setup(loader => loader.GetPrompt(It.IsAny<AgentRole>())).Returns("system");

        var llmClient = new Mock<ILLMClient>();
        var responses = new Queue<LLMResponse>([
            new(null, [new LLMToolCall("call-1", "report_progress", """{"percent_complete":42.35,"summary":"Implementing repository changes"}""")]),
            new("done", null),
        ]);
        llmClient
            .Setup(client => client.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responses.Dequeue());

        var runner = new AgentPhaseRunner(
            promptLoader.Object,
            llmClient.Object,
            new AgentToolRegistry([new ReportProgressTool()]),
            Options.Create(new LLMOptions { GenerateModel = "test-model" }),
            NullLogger<AgentPhaseRunner>.Instance);

        var progressUpdates = new List<double>();
        var result = await runner.RunPhaseAsync(
            AgentRole.Manager,
            "Plan the work",
            new AgentToolContext(Mock.Of<IRepoSandbox>(), "proj", "user", "token", "owner/repo", "exec"),
            onProgress: (progress, _) =>
            {
                progressUpdates.Add(Math.Round(progress * 100, 2, MidpointRounding.AwayFromZero));
                return Task.CompletedTask;
            });

        Assert.IsTrue(result.Success);
        CollectionAssert.Contains(progressUpdates, 42.35);
        CollectionAssert.Contains(progressUpdates, 100.0);
    }

    [TestMethod]
    public async Task RunPhaseAsync_CapsNonFinalReportedProgressBelowCompletion()
    {
        var promptLoader = new Mock<IAgentPromptLoader>();
        promptLoader.Setup(loader => loader.GetPrompt(It.IsAny<AgentRole>())).Returns("system");

        var llmClient = new Mock<ILLMClient>();
        var responses = new Queue<LLMResponse>([
            new(null, [new LLMToolCall("call-1", "report_progress", """{"percent_complete":100,"summary":"Almost done"}""")]),
            new(null, [new LLMToolCall("call-2", "test_tool", "{}")]),
            new("done", null),
        ]);
        llmClient
            .Setup(client => client.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responses.Dequeue());

        var runner = new AgentPhaseRunner(
            promptLoader.Object,
            llmClient.Object,
            new AgentToolRegistry([
                new ReportProgressTool(),
                new StubAgentTool("test_tool", "ok", isReadOnly: true),
            ]),
            Options.Create(new LLMOptions { GenerateModel = "test-model" }),
            NullLogger<AgentPhaseRunner>.Instance);

        var progressUpdates = new List<double>();
        var result = await runner.RunPhaseAsync(
            AgentRole.Backend,
            "Implement the feature",
            new AgentToolContext(Mock.Of<IRepoSandbox>(), "proj", "user", "token", "owner/repo", "exec"),
            onProgress: (progress, _) =>
            {
                progressUpdates.Add(Math.Round(progress * 100, 2, MidpointRounding.AwayFromZero));
                return Task.CompletedTask;
            });

        Assert.IsTrue(result.Success);
        CollectionAssert.Contains(progressUpdates, 99.95);
        CollectionAssert.Contains(progressUpdates, 100.0);
    }

    [TestMethod]
    public async Task RunPhaseAsync_MixedToolBatch_ParallelizesReadOnlySegmentsAroundWriteCalls()
    {
        var promptLoader = new Mock<IAgentPromptLoader>();
        promptLoader.Setup(loader => loader.GetPrompt(It.IsAny<AgentRole>())).Returns("system");

        var startOrder = new ConcurrentQueue<string>();
        var releaseReadBatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readOne = new BlockingAgentTool("read_one", "read one", isReadOnly: true, releaseReadBatch.Task, startOrder);
        var readTwo = new BlockingAgentTool("read_two", "read two", isReadOnly: true, releaseReadBatch.Task, startOrder);
        var writeTool = new BlockingAgentTool("write_one", "write one", isReadOnly: false, releaseWrite.Task, startOrder);
        var readThree = new BlockingAgentTool("read_three", "read three", isReadOnly: true, Task.CompletedTask, startOrder);

        var llmClient = new Mock<ILLMClient>();
        var responses = new Queue<LLMResponse>([
            new(null, [
                new LLMToolCall("call-1", readOne.Name, "{}"),
                new LLMToolCall("call-2", readTwo.Name, "{}"),
                new LLMToolCall("call-3", writeTool.Name, "{}"),
                new LLMToolCall("call-4", readThree.Name, "{}"),
            ]),
            new("done", null),
        ]);
        llmClient
            .Setup(client => client.CompleteAsync(It.IsAny<LLMRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responses.Dequeue());

        var runner = new AgentPhaseRunner(
            promptLoader.Object,
            llmClient.Object,
            new AgentToolRegistry([
                readOne,
                readTwo,
                writeTool,
                readThree,
            ]),
            Options.Create(new LLMOptions { GenerateModel = "test-model" }),
            NullLogger<AgentPhaseRunner>.Instance);

        var runTask = runner.RunPhaseAsync(
            AgentRole.Backend,
            "Implement the feature",
            new AgentToolContext(Mock.Of<IRepoSandbox>(), "proj", "user", "token", "owner/repo", "exec"));

        // All read-only tools are prefetched speculatively — including readThree
        await readOne.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await readTwo.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await readThree.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        // Write tools are never prefetched — they respect batch ordering
        Assert.IsFalse(writeTool.Started.Task.IsCompleted);

        releaseReadBatch.TrySetResult();

        await writeTool.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        releaseWrite.TrySetResult();

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsTrue(result.Success);

        var startedTools = startOrder.ToArray();
        Assert.AreEqual(4, startedTools.Length);
        // All three reads were prefetched concurrently before the write started
        CollectionAssert.AreEquivalent(new[] { readOne.Name, readTwo.Name, readThree.Name }, startedTools.Take(3).ToArray());
        Assert.AreEqual(writeTool.Name, startedTools[3]);
    }

    private sealed class StubAgentTool(string name, string result, bool isReadOnly) : IAgentTool
    {
        public string Name => name;
        public string Description => name;
        public string ParametersJsonSchema => """{"type":"object","properties":{}}""";
        public bool IsReadOnly => isReadOnly;

        public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class BlockingAgentTool(
        string name,
        string result,
        bool isReadOnly,
        Task releaseTask,
        ConcurrentQueue<string> startOrder) : IAgentTool
    {
        public string Name => name;
        public string Description => name;
        public string ParametersJsonSchema => """{"type":"object","properties":{}}""";
        public bool IsReadOnly => isReadOnly;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
        {
            startOrder.Enqueue(Name);
            Started.TrySetResult();
            await releaseTask.WaitAsync(cancellationToken);
            return result;
        }
    }
}
