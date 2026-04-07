using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Fleet.Server.Skills;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class SkillServiceTests
{
    private Mock<ISkillRepository> _repository = null!;
    private Mock<ILogger<SkillService>> _logger = null!;
    private SkillService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _repository = new Mock<ISkillRepository>();
        _logger = new Mock<ILogger<SkillService>>();
        _sut = new SkillService(_repository.Object, _logger.Object);
    }

    [TestMethod]
    public async Task BuildPromptBlockAsync_SelectsRelevantBuiltInAndCustomPlaybooks()
    {
        _repository.Setup(repository => repository.GetEnabledPromptSkillsAsync(42, "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptSkill>
            {
                new()
                {
                    Id = 7,
                    UserProfileId = 42,
                    ProjectId = "proj-1",
                    Name = "Rollback readiness",
                    Description = "Project-specific rollback steps",
                    WhenToUse = "Use when preparing a risky release.",
                    Content = "List the rollback owner, trigger threshold, and communications path.",
                    Enabled = true,
                },
            });

        var prompt = await _sut.BuildPromptBlockAsync(42, "proj-1", "We need a release readiness review and rollback plan.");

        StringAssert.Contains(prompt, "Available Playbooks");
        StringAssert.Contains(prompt, "Release Readiness");
        StringAssert.Contains(prompt, "Rollback readiness");
        StringAssert.Contains(prompt, "Relevant Playbooks");
    }

    [TestMethod]
    public async Task CreateUserSkillAsync_NormalizesAndPersists()
    {
        PromptSkill? persisted = null;
        _repository.Setup(repository => repository.AddAsync(It.IsAny<PromptSkill>(), It.IsAny<CancellationToken>()))
            .Returns<PromptSkill, CancellationToken>((skill, _) =>
            {
                persisted = skill;
                return Task.FromResult(skill);
            });

        var result = await _sut.CreateUserSkillAsync(
            42,
            new UpsertPromptSkillRequest(
                "  Bug Triage  ",
                "  Triage incidents fast  ",
                "  Use when the team is working a live issue.  ",
                "  Produce severity, reproduction, and mitigations.  ",
                true));

        Assert.IsNotNull(persisted);
        Assert.AreEqual("Bug Triage", persisted.Name);
        Assert.AreEqual("personal", result.Scope);
        Assert.IsTrue(result.Enabled);
    }
}
