namespace Fleet.Server.Models;

public record ActivityDto(
    string Icon,
    string Text,
    string Time
);

public record MetricDto(
    string Icon,
    string Label,
    string Value,
    string Subtext,
    double? Progress
);

public record ProjectDashboardDto(
    string Id,
    string Slug,
    string Title,
    string Repo,
    MetricDto[] Metrics,
    ActivityDto[] Activities,
    DashboardAgentDto[] Agents
);
