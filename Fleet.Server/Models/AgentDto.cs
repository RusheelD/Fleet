namespace Fleet.Server.Models;

public record AgentExecutionDto(
    string Id,
    int WorkItemId,
    string WorkItemTitle,
    string Status,
    AgentInfoDto[] Agents,
    string StartedAt,
    string Duration,
    double Progress
);

public record AgentInfoDto(
    string Role,
    string Status,
    string CurrentTask,
    double Progress
);

public record LogEntryDto(
    string Time,
    string Agent,
    string Level,
    string Message
);

public record DashboardAgentDto(
    string Name,
    string Status,
    string Task,
    double Progress
);
