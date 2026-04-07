namespace Fleet.Server.Models;

public record PromptSkillDto(
    int Id,
    string Name,
    string Description,
    string WhenToUse,
    string Content,
    bool Enabled,
    string Scope,
    string? ProjectId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);
