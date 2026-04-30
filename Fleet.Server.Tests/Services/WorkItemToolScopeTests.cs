using Fleet.Server.Copilot.Tools;
using Fleet.Server.Models;
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
        var workItemRepository = new Mock<IWorkItemRepository>();
        var sut = new BulkDeleteWorkItemsTool(workItemService.Object, workItemRepository.Object);

        var result = await sut.ExecuteAsync("""{"ids":[1,2,3]}""", new ChatToolContext(null, "42"));

        StringAssert.Contains(result, "requires a project-scoped chat session");
        workItemService.Verify(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task CreateWorkItemTool_DynamicIterationContext_AssignsCreatedItemToAiAuto()
    {
        var capturedRequest = default(CreateWorkItemRequest);
        var workItemService = new Mock<IWorkItemService>();
        workItemService
            .Setup(service => service.CreateAsync("p1", It.IsAny<CreateWorkItemRequest>()))
            .Callback<string, CreateWorkItemRequest>((_, request) => capturedRequest = request)
            .ReturnsAsync((string _, CreateWorkItemRequest request) => new WorkItemDto(
                WorkItemNumber: 1,
                Title: request.Title,
                State: request.State,
                Priority: request.Priority,
                Difficulty: request.Difficulty,
                AssignedTo: request.AssignedTo,
                Tags: request.Tags,
                IsAI: request.IsAI,
                Description: request.Description,
                ParentWorkItemNumber: request.ParentWorkItemNumber,
                ChildWorkItemNumbers: [],
                LevelId: request.LevelId,
                AssignmentMode: request.AssignmentMode ?? "auto",
                AssignedAgentCount: request.AssignedAgentCount,
                AcceptanceCriteria: request.AcceptanceCriteria ?? string.Empty,
                LinkedPullRequestUrl: null));
        var workItemLevelService = new Mock<IWorkItemLevelService>();
        workItemLevelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync([]);
        var sut = new CreateWorkItemTool(workItemService.Object, workItemLevelService.Object);

        await sut.ExecuteAsync(
            """{"title":"Fix login","level":"Bug"}""",
            new ChatToolContext("p1", "42", DynamicIterationEnabled: true));

        Assert.IsNotNull(capturedRequest);
        Assert.IsTrue(capturedRequest.IsAI);
        Assert.AreEqual("Fleet AI", capturedRequest.AssignedTo);
        Assert.AreEqual("auto", capturedRequest.AssignmentMode);
    }

    [TestMethod]
    public async Task BulkCreateWorkItemsTool_DynamicIterationContext_AssignsCreatedItemsToAiAuto()
    {
        var capturedRequests = new List<CreateWorkItemRequest>();
        var nextWorkItemNumber = 10;
        var workItemService = new Mock<IWorkItemService>();
        workItemService
            .Setup(service => service.CreateAsync("p1", It.IsAny<CreateWorkItemRequest>()))
            .Callback<string, CreateWorkItemRequest>((_, request) => capturedRequests.Add(request))
            .ReturnsAsync((string _, CreateWorkItemRequest request) => new WorkItemDto(
                WorkItemNumber: nextWorkItemNumber++,
                Title: request.Title,
                State: request.State,
                Priority: request.Priority,
                Difficulty: request.Difficulty,
                AssignedTo: request.AssignedTo,
                Tags: request.Tags,
                IsAI: request.IsAI,
                Description: request.Description,
                ParentWorkItemNumber: request.ParentWorkItemNumber,
                ChildWorkItemNumbers: [],
                LevelId: request.LevelId,
                AssignmentMode: request.AssignmentMode ?? "auto",
                AssignedAgentCount: request.AssignedAgentCount,
                AcceptanceCriteria: request.AcceptanceCriteria ?? string.Empty,
                LinkedPullRequestUrl: null));
        var workItemLevelService = new Mock<IWorkItemLevelService>();
        workItemLevelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync([]);
        var sut = new BulkCreateWorkItemsTool(workItemService.Object, workItemLevelService.Object);

        await sut.ExecuteAsync(
            """{"items":[{"title":"Parent feature"},{"title":"Child task","parent_id":"@0"}]}""",
            new ChatToolContext("p1", "42", DynamicIterationEnabled: true));

        Assert.AreEqual(2, capturedRequests.Count);
        Assert.IsTrue(capturedRequests.All(request => request.IsAI));
        Assert.IsTrue(capturedRequests.All(request => request.AssignedTo == "Fleet AI"));
        Assert.IsTrue(capturedRequests.All(request => request.AssignmentMode == "auto"));
    }

    [TestMethod]
    public async Task TryUpdateWorkItemTool_DynamicIterationCreateFallback_AssignsCreatedItemToAiAuto()
    {
        var capturedRequest = default(CreateWorkItemRequest);
        var workItemService = new Mock<IWorkItemService>();
        workItemService.Setup(service => service.UpdateAsync("p1", 999, It.IsAny<UpdateWorkItemRequest>()))
            .ReturnsAsync((WorkItemDto?)null);
        workItemService
            .Setup(service => service.CreateAsync("p1", It.IsAny<CreateWorkItemRequest>()))
            .Callback<string, CreateWorkItemRequest>((_, request) => capturedRequest = request)
            .ReturnsAsync((string _, CreateWorkItemRequest request) => CreateCreatedWorkItem(20, request));
        var workItemLevelService = new Mock<IWorkItemLevelService>();
        workItemLevelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync([]);
        var sut = new TryUpdateWorkItemTool(workItemService.Object, workItemLevelService.Object);

        await sut.ExecuteAsync(
            """{"id":999,"title":"Fallback create","level":"Bug"}""",
            new ChatToolContext("p1", "42", DynamicIterationEnabled: true));

        Assert.IsNotNull(capturedRequest);
        Assert.IsTrue(capturedRequest.IsAI);
        Assert.AreEqual("Fleet AI", capturedRequest.AssignedTo);
        Assert.AreEqual("auto", capturedRequest.AssignmentMode);
    }

    [TestMethod]
    public async Task TryBulkUpdateWorkItemsTool_DynamicIterationCreateFallback_AssignsCreatedItemsToAiAuto()
    {
        var capturedRequests = new List<CreateWorkItemRequest>();
        var nextWorkItemNumber = 30;
        var workItemService = new Mock<IWorkItemService>();
        workItemService
            .Setup(service => service.CreateAsync("p1", It.IsAny<CreateWorkItemRequest>()))
            .Callback<string, CreateWorkItemRequest>((_, request) => capturedRequests.Add(request))
            .ReturnsAsync((string _, CreateWorkItemRequest request) => CreateCreatedWorkItem(nextWorkItemNumber++, request));
        var workItemLevelService = new Mock<IWorkItemLevelService>();
        workItemLevelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync([]);
        var sut = new TryBulkUpdateWorkItemsTool(workItemService.Object, workItemLevelService.Object);

        await sut.ExecuteAsync(
            """{"items":[{"id":0,"title":"Parent feature"},{"id":0,"title":"Child task","parent_id":"@0"}]}""",
            new ChatToolContext("p1", "42", DynamicIterationEnabled: true));

        Assert.AreEqual(2, capturedRequests.Count);
        Assert.IsTrue(capturedRequests.All(request => request.IsAI));
        Assert.IsTrue(capturedRequests.All(request => request.AssignedTo == "Fleet AI"));
        Assert.IsTrue(capturedRequests.All(request => request.AssignmentMode == "auto"));
    }

    private static WorkItemDto CreateCreatedWorkItem(int workItemNumber, CreateWorkItemRequest request)
        => new(
            WorkItemNumber: workItemNumber,
            Title: request.Title,
            State: request.State,
            Priority: request.Priority,
            Difficulty: request.Difficulty,
            AssignedTo: request.AssignedTo,
            Tags: request.Tags,
            IsAI: request.IsAI,
            Description: request.Description,
            ParentWorkItemNumber: request.ParentWorkItemNumber,
            ChildWorkItemNumbers: [],
            LevelId: request.LevelId,
            AssignmentMode: request.AssignmentMode ?? "auto",
            AssignedAgentCount: request.AssignedAgentCount,
            AcceptanceCriteria: request.AcceptanceCriteria ?? string.Empty,
            LinkedPullRequestUrl: null);
}
