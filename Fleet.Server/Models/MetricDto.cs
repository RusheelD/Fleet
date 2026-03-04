namespace Fleet.Server.Models;

public record MetricDto(
    string Icon,
    string Label,
    string Value,
    string Subtext,
    double? Progress
);
