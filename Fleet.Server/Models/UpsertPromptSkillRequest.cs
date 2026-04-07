namespace Fleet.Server.Models;

public record UpsertPromptSkillRequest(
    string Name,
    string Description,
    string WhenToUse,
    string Content,
    bool Enabled
);
