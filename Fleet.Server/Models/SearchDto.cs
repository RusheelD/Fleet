namespace Fleet.Server.Models;

public record SearchResultDto(
    string Type,
    string Title,
    string Description,
    string Meta
);
