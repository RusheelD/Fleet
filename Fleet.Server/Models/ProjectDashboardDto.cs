namespace Fleet.Server.Models;

public record ProjectDashboardDto(
    string Id,
    string Slug,
    string Title,
    string Repo,
    MetricDto[] Metrics,
    ActivityDto[] Activities,
    DashboardAgentDto[] Agents
);
