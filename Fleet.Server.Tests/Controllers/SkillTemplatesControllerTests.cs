using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Skills;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class SkillTemplatesControllerTests
{
    [TestMethod]
    public async Task GetTemplates_ReturnsOk()
    {
        var service = new Mock<ISkillService>();
        var templates = new List<PromptSkillTemplateDto>
        {
            new("prd-to-backlog", "PRD to Backlog", "desc", "when", "content"),
        };
        service.Setup(skillService => skillService.GetTemplatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(templates);
        var sut = new SkillTemplatesController(service.Object);

        var result = await sut.GetTemplates(CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(templates, ok.Value);
    }
}
