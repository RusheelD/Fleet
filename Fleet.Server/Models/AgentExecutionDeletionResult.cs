namespace Fleet.Server.Models;

public record AgentExecutionDeletionResult(
    string ExecutionId,
    int DeletedLogCount
);
