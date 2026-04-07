using Fleet.Server.Data.Entities;
using Fleet.Server.Memories;
using Fleet.Server.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class MemoryServiceTests
{
    private Mock<IMemoryRepository> _repository = null!;
    private Mock<ILogger<MemoryService>> _logger = null!;
    private MemoryService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _repository = new Mock<IMemoryRepository>();
        _logger = new Mock<ILogger<MemoryService>>();
        _sut = new MemoryService(_repository.Object, _logger.Object);
    }

    [TestMethod]
    public async Task BuildPromptBlockAsync_SelectsRelevantAndPinnedMemories()
    {
        _repository.Setup(repository => repository.GetPromptMemoriesAsync(42, "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>
            {
                new()
                {
                    Id = 1,
                    UserProfileId = 42,
                    Name = "Testing policy",
                    Description = "Integration tests should use the real database",
                    Type = MemoryEntryTypes.Feedback,
                    Content = "Use the real DB for integration tests.",
                    AlwaysInclude = true,
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-2),
                },
                new()
                {
                    Id = 2,
                    UserProfileId = 42,
                    ProjectId = "proj-1",
                    Name = "Release date",
                    Description = "The launch date is fixed at 2026-05-15",
                    Type = MemoryEntryTypes.Project,
                    Content = "The go-live date is 2026-05-15 and cannot slip.",
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-20),
                },
            });

        var prompt = await _sut.BuildPromptBlockAsync(42, "proj-1", "Please plan integration test coverage before the 2026-05-15 launch.");

        StringAssert.Contains(prompt, "Memory Index");
        StringAssert.Contains(prompt, "Testing policy");
        StringAssert.Contains(prompt, "Release date");
        StringAssert.Contains(prompt, "Selected Memories");
    }

    [TestMethod]
    public async Task CreateUserMemoryAsync_NormalizesAndPersists()
    {
        MemoryEntry? persisted = null;
        _repository.Setup(repository => repository.AddAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns<MemoryEntry, CancellationToken>((memory, _) =>
            {
                persisted = memory;
                return Task.FromResult(memory);
            });

        var result = await _sut.CreateUserMemoryAsync(
            42,
            new UpsertMemoryEntryRequest("  Testing policy  ", "  Use the real DB  ", "Feedback", "  Use the real DB in integration tests.  ", true));

        Assert.IsNotNull(persisted);
        Assert.AreEqual("Testing policy", persisted.Name);
        Assert.AreEqual(MemoryEntryTypes.Feedback, persisted.Type);
        Assert.AreEqual("personal", result.Scope);
        Assert.IsTrue(result.AlwaysInclude);
    }
}
