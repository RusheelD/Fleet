using Fleet.Server.Copilot.Tools;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ChatToolRegistryTests
{
    [TestMethod]
    public void ToLLMDefinitions_GlobalScope_IncludesCreateProjectWithoutGeneralWrites()
    {
        var registry = new ChatToolRegistry([
            new StubTool("read_only", isWrite: false),
            new StubTool("create_project", isWrite: true),
            new StubTool("update_work_item", isWrite: true),
        ]);

        var defs = registry.ToLLMDefinitions(
            includeWriteTools: false,
            bulkOnly: false,
            includeGlobalRepoTools: true);
        var names = defs.Select(d => d.Name).ToList();

        CollectionAssert.Contains(names, "read_only");
        CollectionAssert.Contains(names, "create_project");
        CollectionAssert.DoesNotContain(names, "update_work_item");
    }

    [TestMethod]
    public void ToLLMDefinitions_ProjectScope_ExcludesCreateProject()
    {
        var registry = new ChatToolRegistry([
            new StubTool("read_only", isWrite: false),
            new StubTool("create_project", isWrite: true),
        ]);

        var defs = registry.ToLLMDefinitions(
            includeWriteTools: false,
            bulkOnly: false,
            includeGlobalRepoTools: false);
        var names = defs.Select(d => d.Name).ToList();

        CollectionAssert.Contains(names, "read_only");
        CollectionAssert.DoesNotContain(names, "create_project");
    }

    private sealed class StubTool(string name, bool isWrite) : IChatTool
    {
        public string Name => name;
        public string Description => "stub";
        public string ParametersJsonSchema => "{}";
        public bool IsWriteTool => isWrite;

        public Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
            => Task.FromResult("ok");
    }
}
