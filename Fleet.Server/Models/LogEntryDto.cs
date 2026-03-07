namespace Fleet.Server.Models;

public record LogEntryDto(
    string Time,
    string Agent,
    string Level,
    string Message,
    bool IsDetailed = false,
    string? ExecutionId = null
);
