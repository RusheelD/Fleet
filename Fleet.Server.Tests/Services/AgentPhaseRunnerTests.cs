using Fleet.Server.Agents.Tools;
using Fleet.Server.Agents;
using Fleet.Server.LLM;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

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

    private sealed class StubAgentTool(string name, string result, bool isReadOnly) : IAgentTool
    {
        public string Name => name;
        public string Description => name;
        public string ParametersJsonSchema => """{"type":"object","properties":{}}""";
        public bool IsReadOnly => isReadOnly;

        public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
