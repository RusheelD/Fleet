using Fleet.Server.Copilot.Tools;
using Fleet.Server.WorkItems;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class WorkItemToolScopeTests
{
    [TestMethod]
    public async Task CreateWorkItemTool_GlobalScope_ReturnsScopeError()
    {
        var workItemService = new Mock<IWorkItemService>();
        var workItemLevelService = new Mock<IWorkItemLevelService>();
        var sut = new CreateWorkItemTool(workItemService.Object, workItemLevelService.Object);

        var result = await sut.ExecuteAsync("""{"title":"Test"}""", new ChatToolContext(null, "42"));

        StringAssert.Contains(result, "requires a project-scoped chat session");
        workItemLevelService.Verify(service => service.GetByProjectIdAsync(It.IsAny<string>()), Times.Never);
        workItemService.Verify(service => service.CreateAsync(It.IsAny<string>(), It.IsAny<Fleet.Server.Models.CreateWorkItemRequest>()), Times.Never);
    }

    [TestMethod]
    public async Task BulkDeleteWorkItemsTool_GlobalScope_ReturnsScopeError()
    {
        var workItemService = new Mock<IWorkItemService>();
        var sut = new BulkDeleteWorkItemsTool(workItemService.Object);

        var result = await sut.ExecuteAsync("""{"ids":[1,2,3]}""", new ChatToolContext(null, "42"));

        StringAssert.Contains(result, "requires a project-scoped chat session");
        workItemService.Verify(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}
