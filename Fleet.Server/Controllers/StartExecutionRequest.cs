namespace Fleet.Server.Controllers;

public record StartExecutionRequest(int WorkItemNumber, string? TargetBranch = null);
