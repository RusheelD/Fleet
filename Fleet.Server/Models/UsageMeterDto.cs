namespace Fleet.Server.Models;

public record UsageMeterDto(
    string Label,
    string Usage,
    double Value,
    string Color,
    string Remaining
);
