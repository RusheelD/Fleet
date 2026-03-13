using System.Text.Json;
using Fleet.Server.Copilot.Tools;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Fleet.Server.WorkItems;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ListWorkItemsToolTests
{
    [TestMethod]
    public async Task ExecuteAsync_IncludesLevelParentAndChildren()
    {
        var workItemService = new Mock<IWorkItemService>();
        var projectService = new Mock<IProjectService>();
        var workItemLevelService = new Mock<IWorkItemLevelService>();

        projectService.Setup(service => service.GetAllProjectsAsync())
            .ReturnsAsync([
                new ProjectDto(
                    Id: "proj-1",
                    OwnerId: "42",
                    Title: "Project One",
                    Slug: "project-one",
                    Description: "",
                    Repo: "owner/repo",
                    WorkItems: new WorkItemSummaryDto(0, 0, 0),
                    Agents: new AgentSummaryDto(0, 0),
                    LastActivity: "now"),
            ]);

        workItemLevelService.Setup(service => service.GetByProjectIdAsync("proj-1"))
            .ReturnsAsync([
                new WorkItemLevelDto(2, "Feature", "Rocket", "#fff", 1, true),
            ]);

        workItemService.Setup(service => service.GetByProjectIdAsync("proj-1"))
            .ReturnsAsync([
                new WorkItemDto(
                    WorkItemNumber: 2,
                    Title: "Child item",
                    State: "todo",
                    Priority: 2,
                    Difficulty: 3,
                    AssignedTo: "",
                    Tags: [],
                    IsAI: true,
                    Description: "",
                    ParentWorkItemNumber: 1,
                    ChildWorkItemNumbers: [],
                    LevelId: 2),
                new WorkItemDto(
                    WorkItemNumber: 1,
                    Title: "Parent item",
                    State: "todo",
                    Priority: 1,
                    Difficulty: 5,
                    AssignedTo: "",
                    Tags: [],
                    IsAI: true,
                    Description: "",
                    ParentWorkItemNumber: null,
                    ChildWorkItemNumbers: [2],
                    LevelId: 2),
            ]);

        var sut = new ListWorkItemsTool(
            workItemService.Object,
            projectService.Object,
            workItemLevelService.Object);

        var result = await sut.ExecuteAsync("{}", new ChatToolContext("proj-1", "42"));

        using var document = JsonDocument.Parse(result);
        var first = document.RootElement[0];
        var parent = first.GetProperty("Parent");
        var children = first.GetProperty("Children");

        Assert.AreEqual(2, first.GetProperty("LevelId").GetInt32());
        Assert.AreEqual("Feature", first.GetProperty("LevelName").GetString());
        Assert.AreEqual("Feature", first.GetProperty("Type").GetString());
        Assert.AreEqual(1, parent.GetProperty("Id").GetInt32());
        Assert.AreEqual("Parent item", parent.GetProperty("Title").GetString());
        Assert.AreEqual(0, children.GetArrayLength());

        var second = document.RootElement[1];
        var secondChildren = second.GetProperty("Children");
        Assert.AreEqual(1, secondChildren.GetArrayLength());
        Assert.AreEqual(2, secondChildren[0].GetProperty("Id").GetInt32());
        Assert.AreEqual("Child item", secondChildren[0].GetProperty("Title").GetString());
    }
}
