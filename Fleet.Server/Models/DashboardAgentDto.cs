namespace Fleet.Server.Models;

public record DashboardAgentDto(
    string Name,
    string Status,
    string Task,
    double Progress
);
