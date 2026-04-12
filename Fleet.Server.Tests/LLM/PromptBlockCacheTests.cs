using Fleet.Server.LLM;
using Fleet.Server.Memories;
using Fleet.Server.Models;
using Fleet.Server.Skills;

namespace Fleet.Server.Tests.LLM;

[TestClass]
public class PromptBlockCacheTests
{
    [TestMethod]
    public async Task GetMemoryBlockAsync_DifferentQueries_DoNotReuseStaleCachedBlock()
    {
        var cache = new PromptBlockCache();
        var memoryService = new TestMemoryService();

        var first = await cache.GetMemoryBlockAsync(memoryService, 42, "proj-1", "auth", CancellationToken.None);
        var second = await cache.GetMemoryBlockAsync(memoryService, 42, "proj-1", "billing", CancellationToken.None);

        Assert.AreEqual("memory:auth", first);
        Assert.AreEqual("memory:billing", second);
        Assert.AreEqual(2, memoryService.BuildPromptBlockCalls);
    }

    [TestMethod]
    public async Task GetSkillBlockAsync_DifferentConversationContext_UsesDistinctCacheEntries()
    {
        var cache = new PromptBlockCache();
        var skillService = new TestSkillService();

        var first = await cache.GetSkillBlockAsync(
            skillService,
            42,
            "proj-1",
            "release",
            ["first context"],
            CancellationToken.None);
        var second = await cache.GetSkillBlockAsync(
            skillService,
            42,
            "proj-1",
            "release",
            ["different context"],
            CancellationToken.None);

        Assert.AreEqual("skill:release:first context", first);
        Assert.AreEqual("skill:release:different context", second);
        Assert.AreEqual(2, skillService.BuildPromptBlockWithContextCalls);
    }

    [TestMethod]
    public async Task InvalidateMemoryBlocks_RemovesCachedEntriesForUser()
    {
        var cache = new PromptBlockCache();
        var memoryService = new TestMemoryService();

        var first = await cache.GetMemoryBlockAsync(memoryService, 42, "proj-1", "auth", CancellationToken.None);
        var cached = await cache.GetMemoryBlockAsync(memoryService, 42, "proj-1", "auth", CancellationToken.None);
        cache.InvalidateMemoryBlocks(42);
        var rebuilt = await cache.GetMemoryBlockAsync(memoryService, 42, "proj-1", "auth", CancellationToken.None);

        Assert.AreEqual(first, cached);
        Assert.AreEqual(first, rebuilt);
        Assert.AreEqual(2, memoryService.BuildPromptBlockCalls);
    }

    [TestMethod]
    public async Task InvalidateSkillBlocks_RemovesCachedEntriesForUser()
    {
        var cache = new PromptBlockCache();
        var skillService = new TestSkillService();

        var first = await cache.GetSkillBlockAsync(skillService, 42, "proj-1", "release", CancellationToken.None);
        var cached = await cache.GetSkillBlockAsync(skillService, 42, "proj-1", "release", CancellationToken.None);
        cache.InvalidateSkillBlocks(42);
        var rebuilt = await cache.GetSkillBlockAsync(skillService, 42, "proj-1", "release", CancellationToken.None);

        Assert.AreEqual(first, cached);
        Assert.AreEqual(first, rebuilt);
        Assert.AreEqual(2, skillService.BuildPromptBlockCalls);
    }

    private sealed class TestMemoryService : IMemoryService
    {
        public int BuildPromptBlockCalls { get; private set; }

        public Task<string> BuildPromptBlockAsync(int userId, string? projectId, string? query, CancellationToken cancellationToken = default)
        {
            BuildPromptBlockCalls++;
            return Task.FromResult($"memory:{query}");
        }

        public Task<IReadOnlyList<MemoryEntryDto>> GetUserMemoriesAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MemoryEntryDto>> GetProjectMemoriesAsync(int userId, string projectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemoryEntryDto> CreateUserMemoryAsync(int userId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemoryEntryDto> UpdateUserMemoryAsync(int userId, int memoryId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteUserMemoryAsync(int userId, int memoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemoryEntryDto> CreateProjectMemoryAsync(int userId, string projectId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemoryEntryDto> UpdateProjectMemoryAsync(int userId, string projectId, int memoryId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteProjectMemoryAsync(int userId, string projectId, int memoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestSkillService : ISkillService
    {
        public int BuildPromptBlockCalls { get; private set; }
        public int BuildPromptBlockWithContextCalls { get; private set; }

        public Task<string> BuildPromptBlockAsync(int userId, string? projectId, string? query, CancellationToken cancellationToken = default)
        {
            BuildPromptBlockCalls++;
            return Task.FromResult($"skill:{query}");
        }

        public Task<string> BuildPromptBlockAsync(int userId, string? projectId, string? query, IReadOnlyList<string>? conversationContext, CancellationToken cancellationToken = default)
        {
            BuildPromptBlockWithContextCalls++;
            var context = conversationContext is { Count: > 0 } ? string.Join(" ", conversationContext) : "none";
            return Task.FromResult($"skill:{query}:{context}");
        }

        public Task<IReadOnlyList<PromptSkillTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PromptSkillDto>> GetUserSkillsAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PromptSkillDto>> GetProjectSkillsAsync(int userId, string projectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PromptSkillDto> CreateUserSkillAsync(int userId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PromptSkillDto> UpdateUserSkillAsync(int userId, int skillId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteUserSkillAsync(int userId, int skillId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PromptSkillDto> CreateProjectSkillAsync(int userId, string projectId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PromptSkillDto> UpdateProjectSkillAsync(int userId, string projectId, int skillId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteProjectSkillAsync(int userId, string projectId, int skillId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
