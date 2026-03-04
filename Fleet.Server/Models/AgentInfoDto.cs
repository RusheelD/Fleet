namespace Fleet.Server.Models;

public record AgentInfoDto(
    string Role,
    string Status,
    string CurrentTask,
    double Progress
);
