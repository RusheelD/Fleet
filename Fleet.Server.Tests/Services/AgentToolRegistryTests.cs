using Fleet.Server.Agents;
using Fleet.Server.Agents.Tools;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentToolRegistryTests
{
    [TestMethod]
    public void ToLLMDefinitions_Manager_IncludesOnlyOrchestrationTools()
    {
        var registry = new AgentToolRegistry([
            new StubAgentTool("list_directory", isReadOnly: true),
            new StubAgentTool("read_file", isReadOnly: true),
            new StubAgentTool("search_files", isReadOnly: true),
            new StubAgentTool("get_change_summary", isReadOnly: true),
            new StubAgentTool("report_progress", isReadOnly: true),
            new StubAgentTool("write_file", isReadOnly: false),
            new StubAgentTool("run_command", isReadOnly: false),
            new StubAgentTool("commit_and_push", isReadOnly: false),
        ]);

        var defs = registry.ToLLMDefinitions(AgentRole.Manager);
        var names = defs.Select(d => d.Name).ToList();

        CollectionAssert.Contains(names, "list_directory");
        CollectionAssert.Contains(names, "read_file");
        CollectionAssert.Contains(names, "search_files");
        CollectionAssert.Contains(names, "get_change_summary");
        CollectionAssert.Contains(names, "report_progress");
        CollectionAssert.DoesNotContain(names, "write_file");
        CollectionAssert.DoesNotContain(names, "run_command");
        CollectionAssert.DoesNotContain(names, "commit_and_push");
    }

    [TestMethod]
    public void IsToolAllowed_Manager_BlocksCodingTools()
    {
        var registry = new AgentToolRegistry([
            new StubAgentTool("read_file", isReadOnly: true),
            new StubAgentTool("write_file", isReadOnly: false),
        ]);

        Assert.IsTrue(registry.IsToolAllowed(AgentRole.Manager, "read_file"));
        Assert.IsFalse(registry.IsToolAllowed(AgentRole.Manager, "write_file"));
    }

    [TestMethod]
    public void ToLLMDefinitions_Backend_IncludesCodingTools()
    {
        var registry = new AgentToolRegistry([
            new StubAgentTool("read_file", isReadOnly: true),
            new StubAgentTool("write_file", isReadOnly: false),
            new StubAgentTool("commit_and_push", isReadOnly: false),
        ]);

        var defs = registry.ToLLMDefinitions(AgentRole.Backend);
        var names = defs.Select(d => d.Name).ToList();

        CollectionAssert.Contains(names, "read_file");
        CollectionAssert.Contains(names, "write_file");
        CollectionAssert.Contains(names, "commit_and_push");
    }

    private sealed class StubAgentTool(string name, bool isReadOnly) : IAgentTool
    {
        public string Name => name;
        public string Description => "stub";
        public string ParametersJsonSchema => "{}";
        public bool IsReadOnly => isReadOnly;

        public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken = default)
            => Task.FromResult("ok");
    }
}
